using System;
using System.Globalization;
using SQLite;

namespace PayTracker.Models;

[Table("AllowanceExpenses")]
public sealed class AllowanceExpenseRow
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MonthKey { get; set; } = "";
    public string Description { get; set; } = "";
    public long AmountPence { get; set; }
    public DateTime SpentDate { get; set; }
    [Ignore]
    public string AmountText => (AmountPence / 100m).ToString("C2", CultureInfo.GetCultureInfo("en-GB"));
    [Ignore]
    public string DateText => $"Spent: {SpentDate:dd MMM yyyy}";
}
