namespace MagicPanel.Core;

/// <summary>
/// Extension point for take-profit rules. Your TP instructions will be added later
/// as new classes implementing this interface; nothing else needs rewriting.
/// </summary>
public interface ITakeProfitRule
{
    string Name { get; }
    string Describe(TradeCalculation calculation);
}

public sealed class NoTakeProfitRule : ITakeProfitRule
{
    public string Name => "None";
    public string Describe(TradeCalculation calculation) => "Not configured";
}
