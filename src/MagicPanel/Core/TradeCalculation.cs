namespace MagicPanel.Core;

public sealed record TradeCalculation(
    InstrumentSpec Spec,
    TradeDirection Direction,
    decimal Entry,
    decimal Reference,            // candle high (SELL) or candle low (BUY)
    decimal DistancePoints,       // entry <-> reference (NOT the stop)
    decimal RiskPerContract,      // reference risk incl. commission + slippage
    int Contracts,
    decimal ReferenceRiskTotal,   // Contracts x RiskPerContract
    decimal StopPrice,            // reference +/- offset ticks
    decimal StopDistancePoints,   // entry <-> stop
    decimal StopExposureTotal,    // actual exposure at the real stop
    bool IsValid,
    string Message)
{
    public static TradeCalculation Invalid(
        InstrumentSpec spec, TradeDirection direction, decimal entry, decimal reference, string message)
        => new(spec, direction, entry, reference, 0, 0, 0, 0, 0, 0, 0, false, message);
}
