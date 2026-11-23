namespace MagicPanel.Core;

/// <summary>
/// Two deliberately separate calculations:
///   1) SIZING uses entry -> candle high/low (the "reference").
///   2) STOP-LOSS is reference +/- StopOffsetTicks and is NEVER used for sizing.
/// Contract count is always rounded DOWN.
/// </summary>
public static class RiskCalculator
{
    public static TradeCalculation Calculate(
        InstrumentSpec spec, TradeDirection direction, decimal entry, decimal reference, AppSettings settings)
    {
        if (settings.MaxRiskPerTrade <= 0m)
            return TradeCalculation.Invalid(spec, direction, entry, reference, "Max risk must be greater than zero.");

        if (entry <= 0m || reference <= 0m)
            return TradeCalculation.Invalid(spec, direction, entry, reference, "Prices must be positive.");

        if (!IsOnTick(entry, spec.TickSize) || !IsOnTick(reference, spec.TickSize))
            return TradeCalculation.Invalid(spec, direction, entry, reference,
                $"Prices must be multiples of the {spec.TickSize} tick size.");

        decimal distance = direction == TradeDirection.Sell ? reference - entry : entry - reference;
        if (distance <= 0m)
            return TradeCalculation.Invalid(spec, direction, entry, reference,
                direction == TradeDirection.Sell
                    ? "Candle high must be above the entry price (zero/negative distance)."
                    : "Candle low must be below the entry price (zero/negative distance).");

        decimal costPerContract = settings.CommissionPerContract + settings.SlippageTicks * spec.TickValue;
        decimal riskPerContract = distance * spec.PointValue + costPerContract;

        int contracts = (int)Math.Floor(settings.MaxRiskPerTrade / riskPerContract);

        decimal offset = settings.StopOffsetTicks * spec.TickSize;
        decimal stop = direction == TradeDirection.Sell ? reference + offset : reference - offset;
        decimal stopDistance = Math.Abs(stop - entry);
        decimal stopExposure = contracts * (stopDistance * spec.PointValue + costPerContract);

        string message = contracts == 0
            ? "One contract already exceeds the reference risk: 0 eligible contracts."
            : string.Empty;

        return new TradeCalculation(
            spec, direction, entry, reference, distance, riskPerContract, contracts,
            contracts * riskPerContract, stop, stopDistance, stopExposure, true, message);
    }

    private static bool IsOnTick(decimal price, decimal tickSize) => price % tickSize == 0m;
}
