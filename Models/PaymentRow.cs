using PayTracker.Models;
using SQLite;
using System;

namespace PayTracker.Models;

[Table("Payments")]
public sealed class PaymentRow
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    // Store money as whole pence to preserve the exact amount.
    public long AmountPence { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public static PaymentRow FromPayment(Payment payment)
    {
        return new PaymentRow
        {
            Id = payment.Id.ToString(),
            Name = payment.Name,
            AmountPence = checked((long)(payment.Amount * 100m)),
            DueDate = payment.DueDate.Date,
            PaidDate = payment.PaidDate?.Date
        };
    }

    public Payment ToPayment()
    {
        return new Payment
        {
            Id = Guid.Parse(Id),
            Name = Name,
            Amount = AmountPence / 100m,
            DueDate = DueDate.Date,
            PaidDate = PaidDate?.Date
        };
    }
}