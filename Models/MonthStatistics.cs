using System;

namespace PayTracker.Models;

public sealed record MonthStatistics
{
    public DateTime Month { get; init; }
    public bool HasAnyData { get; init; }
    public bool HasBillRecords { get; init; }
    public int BillCount { get; init; }
    public decimal ScheduledBills { get; init; }
    public decimal PaidBillsForMonth { get; init; }
    public decimal UnpaidBillsForMonth { get; init; }
    public decimal BillsPaidByDate { get; init; }
    public decimal AllowanceSpent { get; init; }
    public decimal? ExpectedIncome { get; init; }
    public decimal? AllowanceBudget { get; init; }
    public decimal? SavingsTarget { get; init; }
    public decimal? ProjectedMonthlySurplus { get; init; }
    public decimal RecordedOutflow => BillsPaidByDate + AllowanceSpent;
    public decimal? AllowanceOverspend => AllowanceBudget is decimal budget
        ? Math.Max(AllowanceSpent - budget, 0) : null;
    public decimal? AllowanceRemaining => AllowanceBudget is decimal budget
        ? Math.Max(budget - AllowanceSpent, 0) : null;
}
