using System;
using SQLite;

namespace PayTracker.Models;

[Table("RecurringPayments")]
public sealed class RecurringPaymentRow
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public long AmountPence { get; set; }
    public int DueDay { get; set; } = 1;
    public DateTime StartMonth { get; set; }
    public bool IsActive { get; set; } = true;
}
