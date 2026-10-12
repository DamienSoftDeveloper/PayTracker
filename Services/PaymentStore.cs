using System;
using System.Collections.ObjectModel;
using PayTracker.Models;

namespace PayTracker.Services;

public sealed class PaymentStore
{
    public static PaymentStore Instance { get; } = new();

    public ObservableCollection<Payment> Payments { get; } = new();

    private PaymentStore()
    {
        DateTime today = DateTime.Today;

        Payments.Add(new Payment
        {
            Name = "Rent",
            Amount = 300m,
            DueDate = new DateTime(today.Year, today.Month, 1)
        });

        Payments.Add(new Payment
        {
            Name = "YouTube subscription",
            Amount = 12.99m,
            DueDate = new DateTime(today.Year, today.Month, 15)
        });
    }

    public void Add(Payment payment)
    {
        Payments.Add(payment);
    }

    public void Update(Payment payment)
    {
        for (int i = 0; i < Payments.Count; i++)
        {
            if (Payments[i].Id == payment.Id)
            {
                Payments[i] = payment;
                return;
            }
        }
    }

    public void Delete(Payment payment)
    {
        for (int i = 0; i < Payments.Count; i++)
        {
            if (Payments[i].Id == payment.Id)
            {
                Payments.RemoveAt(i);
                return;
            }
        }
    }
}