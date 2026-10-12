using System;
using SQLite;

namespace PayTracker.Models;

[Table("MonthlyBudgets")]
public sealed class MonthlyBudgetRow
{
    [PrimaryKey]
    public string MonthKey { get; set; } = "";
    public long BalancePence { get; set; }
    public long ExpectedIncomePence { get; set; }
    public long IncomeReceivedPence { get; set; }
    public long AllowancePence { get; set; }
    public long SavingsTargetPence { get; set; }
    public bool UseCurrentBalance { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
