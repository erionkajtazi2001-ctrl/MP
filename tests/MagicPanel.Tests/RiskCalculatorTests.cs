using MagicPanel.Core;
using Xunit;

namespace MagicPanel.Tests;

public class RiskCalculatorTests
{
    private static InstrumentSpec Spec(string symbol) => InstrumentSpec.Defaults.First(s => s.Symbol == symbol);
    private static AppSettings Settings(decimal risk = 250m, int offset = 3, decimal comm = 0m, decimal slip = 0m) =>
        new() { MaxRiskPerTrade = risk, StopOffsetTicks = offset, CommissionPerContract = comm, SlippageTicks = slip };

    [Fact]
    public void Sell_NQ_matches_the_worked_example()
    {
        var c = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25005m, Settings());
        Assert.True(c.IsValid);
        Assert.Equal(5m, c.DistancePoints);
        Assert.Equal(100m, c.RiskPerContract);
        Assert.Equal(2, c.Contracts);                 // 2.5 rounded DOWN
        Assert.Equal(200m, c.ReferenceRiskTotal);
        Assert.Equal(25005.75m, c.StopPrice);         // high + 3 ticks
        Assert.Equal(5.75m, c.StopDistancePoints);
        Assert.Equal(230m, c.StopExposureTotal);      // 2 x 5.75 x 20
    }

    [Fact]
    public void Buy_NQ_uses_candle_low_and_stop_below()
    {
        var c = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Buy, 25000m, 24995m, Settings());
        Assert.Equal(2, c.Contracts);
        Assert.Equal(24994.25m, c.StopPrice);
        Assert.Equal(5.75m, c.StopDistancePoints);
    }

    [Fact]
    public void Sizing_does_not_include_the_stop_offset()
    {
        var a = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25005m, Settings(offset: 3));
        var b = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25005m, Settings(offset: 40));
        Assert.Equal(a.Contracts, b.Contracts);
        Assert.Equal(a.RiskPerContract, b.RiskPerContract);
    }

    [Fact]
    public void One_contract_over_the_limit_gives_zero_contracts()
    {
        var c = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25020m, Settings());
        Assert.True(c.IsValid);
        Assert.Equal(0, c.Contracts);
        Assert.Equal(0m, c.StopExposureTotal);
    }

    [Fact]
    public void Zero_or_negative_distance_is_invalid()
    {
        Assert.False(RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25000m, Settings()).IsValid);
        Assert.False(RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 24999m, Settings()).IsValid);
        Assert.False(RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Buy, 25000m, 25001m, Settings()).IsValid);
    }

    [Fact]
    public void Prices_off_the_tick_grid_are_invalid()
    {
        Assert.False(RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000.10m, 25005m, Settings()).IsValid);
    }

    [Fact]
    public void MNQ_has_one_tenth_the_point_value()
    {
        var c = RiskCalculator.Calculate(Spec("MNQ"), TradeDirection.Sell, 25000m, 25005m, Settings());
        Assert.Equal(10m, c.RiskPerContract);
        Assert.Equal(25, c.Contracts);
    }

    [Fact]
    public void GC_uses_tick_0_10_and_point_value_100()
    {
        var c = RiskCalculator.Calculate(Spec("GC"), TradeDirection.Sell, 4000.0m, 4001.5m, Settings());
        Assert.Equal(150m, c.RiskPerContract);
        Assert.Equal(1, c.Contracts);
        Assert.Equal(4001.8m, c.StopPrice);           // 3 ticks x 0.10
    }

    [Fact]
    public void MGC_uses_point_value_10()
    {
        var c = RiskCalculator.Calculate(Spec("MGC"), TradeDirection.Buy, 4000.0m, 3998.5m, Settings());
        Assert.Equal(15m, c.RiskPerContract);
        Assert.Equal(16, c.Contracts);                // 250 / 15 = 16.67 -> 16
    }

    [Fact]
    public void Commission_and_slippage_reduce_contracts()
    {
        var withComm = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25005m, Settings(comm: 30m));
        Assert.Equal(130m, withComm.RiskPerContract);
        Assert.Equal(1, withComm.Contracts);

        var withSlip = RiskCalculator.Calculate(Spec("NQ"), TradeDirection.Sell, 25000m, 25005m, Settings(slip: 2m));
        Assert.Equal(110m, withSlip.RiskPerContract);  // 2 ticks x $5
        Assert.Equal(2, withSlip.Contracts);
    }

    [Fact]
    public void Settings_round_trip_through_the_store()
    {
        var path = Path.Combine(Path.GetTempPath(), "magicpanel-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings { MaxRiskPerTrade = 321m, StopOffsetTicks = 5, LastInstrument = "GC" });
            var loaded = new SettingsStore(path).Load();
            Assert.Equal(321m, loaded.MaxRiskPerTrade);
            Assert.Equal(5, loaded.StopOffsetTicks);
            Assert.Equal("GC", loaded.LastInstrument);
        }
        finally { File.Delete(path); }
    }
}
