namespace Hakari.Core.Limits;

/// <summary>Paid usage credits that cover the account after it hits its plan limits.</summary>
public sealed record ExtraUsage(bool IsEnabled, decimal Used, decimal MonthlyLimit, string Currency)
{
    public double Utilization => MonthlyLimit == 0 ? 0 : (double)(Used / MonthlyLimit);
}
