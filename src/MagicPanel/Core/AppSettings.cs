namespace MagicPanel.Core;

public sealed class AppSettings
{
    /// <summary>Maximum reference risk per trade in account currency. Set once, saved.</summary>
    public decimal MaxRiskPerTrade { get; set; } = 250m;

    /// <summary>Stop-loss distance beyond the candle high/low, in ticks. Not used for sizing.</summary>
    public int StopOffsetTicks { get; set; } = 3;

    /// <summary>Round-turn commission per contract, in currency.</summary>
    public decimal CommissionPerContract { get; set; } = 0m;

    /// <summary>Assumed slippage per contract, in ticks.</summary>
    public decimal SlippageTicks { get; set; } = 0m;

    public string LastInstrument { get; set; } = "NQ";
}
