using System;
using SQLite;

namespace PayTracker.Models;

[Table("Payments")]
public sealed class PaymentRow
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long AmountPence { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }

    // Nullable so Lesson 4 rows can be migrated without losing data.
    public DateTime? RecordMonth { get; set; }
    public string? ScheduleId { get; set; }
    public bool IsDeleted { get; set; }

    public static PaymentRow FromPayment(Payment payment) => new()
    {
        Id = payment.Id.ToString(), Name = payment.Name,
        AmountPence = checked((long)(payment.Amount * 100m)),
        DueDate = payment.DueDate.Date, PaidDate = payment.PaidDate?.Date,
        RecordMonth = payment.RecordMonth == default
            ? new DateTime(payment.DueDate.Year, payment.DueDate.Month, 1)
            : payment.RecordMonth,
        ScheduleId = payment.ScheduleId
    };

    public Payment ToPayment() => new()
    {
        Id = Guid.Parse(Id), Name = Name, Amount = AmountPence / 100m,
        DueDate = DueDate.Date, PaidDate = PaidDate?.Date,
        RecordMonth = RecordMonth ?? new DateTime(DueDate.Year, DueDate.Month, 1),
        ScheduleId = ScheduleId
    };
}
