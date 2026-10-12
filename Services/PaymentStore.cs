using Microsoft.Maui.Storage;
using PayTracker.Models;
using SQLite;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PayTracker.Services;

public sealed class PaymentStore
{
    public static PaymentStore Instance { get; } = new();

    public ObservableCollection<Payment> Payments { get; } = new();

    private readonly SQLiteAsyncConnection _database;

    // Serialize initialization and database operations.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _initialized;

    private PaymentStore()
    {
        string path = Path.Combine(
            FileSystem.AppDataDirectory,
            "paytrack.db3");

        _database = new SQLiteAsyncConnection(
            path,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.FullMutex);
    }

    // Call only while holding _gate.
    private async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _database.CreateTableAsync<PaymentRow>();

        // Set this only after table creation succeeds.
        _initialized = true;
    }

    public async Task LoadAsync()
    {
        await _gate.WaitAsync();

        try
        {
            await InitializeAsync();

            var rows = await _database
                .Table<PaymentRow>()
                .OrderBy(row => row.DueDate)
                .ThenBy(row => row.Name)
                .ToListAsync();

            // Convert everything before replacing the displayed list.
            var payments = rows
                .Select(row => row.ToPayment())
                .ToList();

            Payments.Clear();

            foreach (Payment payment in payments)
            {
                Payments.Add(payment);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task AddAsync(Payment payment)
    {
        return SaveAsync(payment, isNew: true);
    }

    public Task UpdateAsync(Payment payment)
    {
        return SaveAsync(payment, isNew: false);
    }

    private async Task SaveAsync(Payment payment, bool isNew)
    {
        await _gate.WaitAsync();

        try
        {
            await InitializeAsync();

            PaymentRow row = PaymentRow.FromPayment(payment);

            if (isNew)
            {
                await _database.InsertAsync(row);

                // Update the screen only after the database save succeeds.
                Payments.Add(payment);
            }
            else
            {
                int index = FindIndex(payment.Id);

                if (index < 0)
                {
                    throw new InvalidOperationException(
                        "The payment is no longer in the list.");
                }

                int affectedRows = await _database.UpdateAsync(row);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        "The payment could not be found in the database.");
                }

                Payments[index] = payment;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Payment payment)
    {
        await _gate.WaitAsync();

        try
        {
            await InitializeAsync();

            int index = FindIndex(payment.Id);

            if (index < 0)
            {
                throw new InvalidOperationException(
                    "The payment is no longer in the list.");
            }

            int affectedRows = await _database
                .DeleteAsync<PaymentRow>(payment.Id.ToString());

            if (affectedRows != 1)
            {
                throw new InvalidOperationException(
                    "The payment could not be found in the database.");
            }

            Payments.RemoveAt(index);
        }
        finally
        {
            _gate.Release();
        }
    }

    private int FindIndex(Guid id)
    {
        for (int i = 0; i < Payments.Count; i++)
        {
            if (Payments[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}