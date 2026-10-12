using System;
using System.Globalization;

namespace PayTracker.Models;

public sealed record Payment
{
    private static readonly CultureInfo Pounds =  CultureInfo.GetCultureInfo("en-GB");

    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PaidDate { get; init; }

    public bool IsPaid => PaidDate.HasValue;

    public string AmountText => Amount.ToString("C2", Pounds);

    public string DueText => $"Due: {DueDate:dd MMM yyyy}";

    public string StatusText => PaidDate is DateTime date  ? $"Paid: {date:dd MMM yyyy}" : "Unpaid";

    public string PayButtonText => IsPaid ? "Change paid date"  : "Mark paid";

}