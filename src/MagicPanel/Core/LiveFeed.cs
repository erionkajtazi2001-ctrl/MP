using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MagicPanel.Core;

/// <summary>One market-data update received from the NinjaTrader MagicPanelFeed indicator.</summary>
public sealed record LiveSnapshot(
    string Symbol, string FullName, decimal TickSize, decimal PointValue,
    decimal Bid, decimal Ask, decimal Last, decimal High, decimal Low, DateTime ReceivedUtc);

/// <summary>
/// Message format (one UDP datagram, ';' separated, '.' decimals):
/// MP1;symbol;fullName;tickSize;pointValue;bid;ask;last;candleHigh;candleLow;sequence
/// </summary>
public static class LiveFeedParser
{
    public static bool TryParse(string line, DateTime receivedUtc, out LiveSnapshot? snapshot)
    {
        snapshot = null;
        var p = line.Trim().Split(';');
        if (p.Length < 10 || p[0] != "MP1") return false;

        if (!Num(p[3], out var tick) || !Num(p[4], out var pointValue) ||
            !Num(p[5], out var bid) || !Num(p[6], out var ask) || !Num(p[7], out var last) ||
            !Num(p[8], out var high) || !Num(p[9], out var low))
            return false;

        if (tick <= 0m || pointValue <= 0m) return false;

        // Remove any floating-point noise by snapping prices to the tick grid.
        snapshot = new LiveSnapshot(p[1], p[2], tick, pointValue,
            Snap(bid, tick), Snap(ask, tick), Snap(last, tick), Snap(high, tick), Snap(low, tick), receivedUtc);
        return true;
    }

    private static bool Num(string s, out decimal v) =>
        decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static decimal Snap(decimal price, decimal tick) =>
        Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;
}

/// <summary>Listens on 127.0.0.1:47321 (this PC only) for the NinjaTrader indicator.</summary>
public sealed class LiveFeedListener : IDisposable
{
    public const int Port = 47321;

    private readonly ConcurrentDictionary<string, LiveSnapshot> _latest = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private UdpClient? _udp;

    public string? StartError { get; private set; }

    public bool TryGet(string symbol, out LiveSnapshot? snapshot) => _latest.TryGetValue(symbol, out snapshot);

    public void Start()
    {
        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, Port));
            _ = Task.Run(() => LoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            StartError = ex.Message;
            AppLog.Write("Live feed could not start: " + ex.Message);
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udp!.ReceiveAsync(ct);
                var text = Encoding.ASCII.GetString(result.Buffer);
                if (LiveFeedParser.TryParse(text, DateTime.UtcNow, out var snap) && snap != null)
                    _latest[snap.Symbol] = snap;
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                AppLog.Write("Live feed error: " + ex.Message);
                await Task.Delay(200);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udp?.Dispose();
    }
}
