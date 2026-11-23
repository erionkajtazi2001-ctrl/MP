namespace MagicPanel.Core;

/// <summary>
/// Contract specification. These are FALLBACK values; in Phase 5 the NinjaTrader
/// bridge will supply the real tick size / point value for the contract in use.
/// Decimal is used everywhere so tick math has no floating-point error.
/// </summary>
public sealed record InstrumentSpec(string Symbol, string Name, decimal TickSize, decimal PointValue)
{
    public decimal TickValue => TickSize * PointValue;

    public static readonly IReadOnlyList<InstrumentSpec> Defaults = new[]
    {
        new InstrumentSpec("NQ",  "E-mini Nasdaq-100",      0.25m, 20m),
        new InstrumentSpec("MNQ", "Micro E-mini Nasdaq-100", 0.25m, 2m),
        new InstrumentSpec("GC",  "Gold futures",           0.10m, 100m),
        new InstrumentSpec("MGC", "Micro Gold futures",     0.10m, 10m),
    };
}
