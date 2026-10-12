using System;

namespace PayTracker.Models;

public sealed record BudgetResult
{
    public decimal ScheduledBills { get; init; }
    public decimal PaidBills { get; init; }
    public decimal UnpaidBills { get; init; }
    public decimal PriorMonthUnpaidBills { get; init; }
    public decimal AllowanceSpent { get; init; }
    public decimal AllowanceRemaining { get; init; }
    public decimal AllowanceOverspend { get; init; }
    public decimal IncomeStillExpected { get; init; }
    public decimal ProjectedMonthlySurplus { get; init; }
    public decimal ProjectedClosingBalance { get; init; }
    public decimal SavingsTarget { get; init; }
    public decimal BalanceAfterSavingsTarget { get; init; }
    public decimal SavingsTargetShortfall { get; init; }
    public bool UseCurrentBalance { get; init; }
    public DateTime CalculatedUtc { get; init; }
}
