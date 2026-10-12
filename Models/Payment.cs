using System;
using System.Globalization;

namespace PayTracker.Models;

public sealed record Payment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PaidDate { get; init; }
    public DateTime RecordMonth { get; init; }
    public string? ScheduleId { get; init; }
    public bool IsPaid => PaidDate.HasValue;
    public bool IsRecurring => ScheduleId is not null;
    public string AmountText => Amount.ToString("C2", CultureInfo.GetCultureInfo("en-GB"));
    public string DueText => $"Due: {DueDate:dd MMM yyyy}";
    public string StatusText => PaidDate is DateTime date ? $"Paid: {date:dd MMM yyyy}" : "Unpaid";
    public string KindText => IsRecurring ? "Monthly payment" : "One-off payment";
    public string PayButtonText => IsPaid ? "Change paid date" : "Mark paid";
}
