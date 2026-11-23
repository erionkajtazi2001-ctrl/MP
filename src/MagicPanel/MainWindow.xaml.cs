using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MagicPanel.Core;

namespace MagicPanel;

public sealed record ResultRow(string Label, string Value);

public partial class MainWindow : Window
{
    private enum FeedMode { Manual, Live, Stale }

    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xE5, 0x48, 0x4D));
    private static readonly SolidColorBrush WarnBrush = new(Color.FromRgb(0xF5, 0xB8, 0x3D));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(0x8B, 0x92, 0xA5));
    private static readonly SolidColorBrush GoodBrush = new(Color.FromRgb(0x1F, 0xBF, 0x75));
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(3);

    private readonly SettingsStore _store = new();
    private readonly AppSettings _settings;
    private readonly ITakeProfitRule _takeProfit = new NoTakeProfitRule();
    private readonly LiveFeedListener _feed = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private InstrumentSpec _current = InstrumentSpec.Defaults[0];
    private DateTime _lastTradeClick = DateTime.MinValue;

    public MainWindow()
    {
        _settings = _store.Load();
        InitializeComponent();

        MaxRiskBox.Text = Inv(_settings.MaxRiskPerTrade);
        OffsetBox.Text = _settings.StopOffsetTicks.ToString(CultureInfo.InvariantCulture);
        CommissionBox.Text = Inv(_settings.CommissionPerContract);
        SlippageBox.Text = Inv(_settings.SlippageTicks);

        _feed.Start();
        _timer.Tick += (_, _) => RefreshLive();
        _timer.Start();
        Closed += (_, _) => { _timer.Stop(); _feed.Dispose(); };

        var tabs = new[] { TabNQ, TabMNQ, TabGC, TabMGC };
        var tab = tabs.FirstOrDefault(t => (t.Tag as string) == _settings.LastInstrument) ?? TabNQ;
        tab.IsChecked = true;
    }

    private void OnInstrumentChecked(object sender, RoutedEventArgs e)
    {
        var symbol = (string)((RadioButton)sender).Tag;
        _current = InstrumentSpec.Defaults.First(s => s.Symbol == symbol);
        _settings.LastInstrument = symbol;
        TrySave();

        ResultsList.ItemsSource = null;
        SetStatus(string.Empty, false);
        RefreshLive();
    }

    // ----- Live data from NinjaTrader -----

    private FeedMode GetMode(out LiveSnapshot? snap)
    {
        if (!_feed.TryGet(_current.Symbol, out snap) || snap == null) return FeedMode.Manual;
        return DateTime.UtcNow - snap.ReceivedUtc <= MaxAge ? FeedMode.Live : FeedMode.Stale;
    }

    private void RefreshLive()
    {
        var mode = GetMode(out var snap);
        bool fromNt = mode != FeedMode.Manual;
        EntryBox.IsReadOnly = fromNt;
        HighBox.IsReadOnly = fromNt;
        LowBox.IsReadOnly = fromNt;

        switch (mode)
        {
            case FeedMode.Live:
                var live = _current with { TickSize = snap!.TickSize, PointValue = snap.PointValue };
                EntryBox.Text = Plain(snap.Last);
                HighBox.Text = Plain(snap.High);
                LowBox.Text = Plain(snap.Low);
                ModeBadge.Text = "SIM · NINJATRADER LIVE";
                ModeBadge.Foreground = GoodBrush;
                SpecText.Text = SpecLine(live) + "  ·  from NinjaTrader";
                break;

            case FeedMode.Stale:
                ModeBadge.Text = "NINJATRADER DATA STALE";
                ModeBadge.Foreground = ErrorBrush;
                SpecText.Text = SpecLine(_current) + "  ·  no data for 3s+";
                break;

            default:
                ModeBadge.Text = _feed.StartError == null ? "SIM · MANUAL DATA" : "FEED PORT BUSY · MANUAL";
                ModeBadge.Foreground = WarnBrush;
                SpecText.Text = SpecLine(_current) + "  ·  default values";
                break;
        }
    }

    // ----- Settings -----

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDecimal(MaxRiskBox.Text, out var risk) || risk <= 0m)
        {
            SetStatus("Max risk must be a number above 0. Use '.' for decimals and no thousands separators.", true);
            return;
        }
        if (!int.TryParse(OffsetBox.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            SetStatus("Stop-loss offset must be a whole number of ticks (0 or more).", true);
            return;
        }
        if (!TryParseDecimal(CommissionBox.Text, out var commission) || commission < 0m)
        {
            SetStatus("Commission must be a number, 0 or more.", true);
            return;
        }
        if (!TryParseDecimal(SlippageBox.Text, out var slippage) || slippage < 0m)
        {
            SetStatus("Slippage must be a number of ticks, 0 or more.", true);
            return;
        }

        _settings.MaxRiskPerTrade = risk;
        _settings.StopOffsetTicks = offset;
        _settings.CommissionPerContract = commission;
        _settings.SlippageTicks = slippage;

        if (TrySave())
        {
            AppLog.Write($"Settings saved: risk={risk} offsetTicks={offset} comm={commission} slip={slippage}");
            SetStatus("Settings saved.", false);
        }
    }

    // ----- BUY / SELL -----

    private void BuyButton_Click(object sender, RoutedEventArgs e) => RunCalculation(TradeDirection.Buy);

    private void SellButton_Click(object sender, RoutedEventArgs e) => RunCalculation(TradeDirection.Sell);

    private void RunCalculation(TradeDirection direction)
    {
        // Ignore accidental double-clicks.
        var now = DateTime.UtcNow;
        if ((now - _lastTradeClick).TotalMilliseconds < 400) return;
        _lastTradeClick = now;

        InstrumentSpec spec = _current;
        decimal entry, high, low;
        string source;
        string capturedAt = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

        var mode = GetMode(out var snap);
        if (mode == FeedMode.Stale)
        {
            SetStatus("NinjaTrader data is older than 3 seconds. Check that the MagicPanelFeed indicator is on a chart and the market is open.", true);
            return;
        }

        if (mode == FeedMode.Live)
        {
            // Snapshot captured at the moment of the click.
            spec = _current with { TickSize = snap!.TickSize, PointValue = snap.PointValue };
            high = snap.High;
            low = snap.Low;
            entry = direction == TradeDirection.Buy
                ? (snap.Ask > 0m ? snap.Ask : snap.Last)
                : (snap.Bid > 0m ? snap.Bid : snap.Last);
            source = "NinjaTrader live";
        }
        else
        {
            if (!TryParseDecimal(EntryBox.Text, out entry) ||
                !TryParseDecimal(HighBox.Text, out high) ||
                !TryParseDecimal(LowBox.Text, out low))
            {
                SetStatus("Entry, high and low must be numbers (use '.' for decimals, no commas).", true);
                return;
            }
            source = "Manual";
        }

        if (high < low)
        {
            SetStatus("Candle high cannot be below candle low.", true);
            return;
        }

        decimal reference = direction == TradeDirection.Sell ? high : low;
        var calc = RiskCalculator.Calculate(spec, direction, entry, reference, _settings);

        AppLog.Write($"[{source}] {calc.Spec.Symbol} {direction} entry={entry} ref={reference} high={high} low={low} " +
                     $"tick={spec.TickSize} pv={spec.PointValue} maxRisk={_settings.MaxRiskPerTrade} " +
                     $"offsetTicks={_settings.StopOffsetTicks} comm={_settings.CommissionPerContract} " +
                     $"slipTicks={_settings.SlippageTicks} -> valid={calc.IsValid} contracts={calc.Contracts} " +
                     $"riskPerContract={calc.RiskPerContract} stop={calc.StopPrice} " +
                     $"stopExposure={calc.StopExposureTotal} msg={calc.Message}");

        if (!calc.IsValid)
        {
            ResultsList.ItemsSource = new List<ResultRow> { new("Result", "Invalid") };
            SetStatus(calc.Message, true);
            return;
        }

        bool sell = direction == TradeDirection.Sell;
        var rows = new List<ResultRow>
        {
            new("Instrument", calc.Spec.Symbol),
            new("Direction", sell ? "SELL / SHORT" : "BUY"),
            new("Data source", source + " @ " + capturedAt),
            new("Max risk (saved)", Money(_settings.MaxRiskPerTrade)),
            new(sell ? "Expected entry (bid)" : "Expected entry (ask)", Price(calc.Entry)),
            new(sell ? "Captured candle high" : "Captured candle low", Price(calc.Reference)),
            new(sell ? "Entry to candle high" : "Entry to candle low", Price(calc.DistancePoints) + " pts"),
            new("Reference risk / contract", Money(calc.RiskPerContract)),
            new("Contracts", calc.Contracts.ToString(CultureInfo.InvariantCulture)),
            new("Reference risk (total)", Money(calc.ReferenceRiskTotal)),
            new($"Actual stop-loss ({(sell ? "+" : "-")}{_settings.StopOffsetTicks} ticks)", Price(calc.StopPrice)),
            new("Stop distance", Price(calc.StopDistancePoints) + " pts"),
            new("Actual stop exposure (total)", Money(calc.StopExposureTotal)),
            new("Take-profit", _takeProfit.Describe(calc)),
        };
        ResultsList.ItemsSource = rows;

        if (calc.Contracts == 0) SetStatus(calc.Message, true, warn: true);
        else SetStatus("Calculation only. No order was sent.", false);
    }

    // ----- Helpers -----

    private bool TrySave()
    {
        try
        {
            _store.Save(_settings);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("Could not save settings: " + ex.Message, true);
            return false;
        }
    }

    private void SetStatus(string text, bool isProblem, bool warn = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = !isProblem ? MutedBrush : warn ? WarnBrush : ErrorBrush;
    }

    // Only '.' as the decimal separator and no thousands separators, so "25,000" can never be misread.
    private static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text?.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);

    private static string SpecLine(InstrumentSpec s) =>
        $"{s.Name}  ·  tick {Inv(s.TickSize)}  ·  ${Inv(s.PointValue)}/pt  ·  ${Inv(s.TickValue)}/tick";

    private static string Inv(decimal v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Plain(decimal v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static string Price(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

    private static string Money(decimal v) => "$" + v.ToString("N2", CultureInfo.InvariantCulture);
}
