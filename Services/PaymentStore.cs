using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using PayTracker.Models;
using SQLite;

namespace PayTracker.Services;

public sealed class PaymentStore
{
    private static readonly Lazy<PaymentStore> Shared = new(() => new PaymentStore(
        Path.Combine(FileSystem.AppDataDirectory, "paytrack.db3")));
    public static PaymentStore Instance => Shared.Value;
    public ObservableCollection<Payment> Payments { get; } = new();
    public DateTime SelectedMonth { get; private set; } = MonthOf(DateTime.Today);
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    // The optional separate instance is useful for testing a temporary database.
    public PaymentStore(string databasePath)
    {
        _database = new SQLiteAsyncConnection(databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public static DateTime MonthOf(DateTime date) => new(date.Year, date.Month, 1);

    public static DateTime DueDateFor(DateTime month, int dueDay)
    {
        if (dueDay is < 1 or > 31)
            throw new ArgumentException("The due day must be from 1 to 31.");
        return new DateTime(month.Year, month.Month,
            Math.Min(dueDay, DateTime.DaysInMonth(month.Year, month.Month)));
    }

    // Call under _gate. Add columns, then migrate legacy rows in a transaction.
    private async Task InitializeAsync()
    {
        if (_initialized) return;
        await _database.RunInTransactionAsync(db =>
        {
            db.CreateTable<PaymentRow>();
            db.CreateTable<RecurringPaymentRow>();
            foreach (var row in db.Table<PaymentRow>().ToList())
            {
                if (row.RecordMonth is null)
                {
                    row.RecordMonth = MonthOf(row.DueDate);
                    row.IsDeleted = false;
                    db.Update(row);
                }
            }
            // One row per schedule/month; one-off rows have a NULL ScheduleId.
            db.Execute("CREATE UNIQUE INDEX IF NOT EXISTS IX_Payments_ScheduleMonth " +
                "ON Payments(ScheduleId, RecordMonth)");
        });
        _initialized = true;
    }

    public Task LoadAsync() => SelectMonthAsync(SelectedMonth);

    public async Task SelectMonthAsync(DateTime requestedMonth)
    {
        DateTime month = MonthOf(requestedMonth);
        if (month.Year is < 1900 or > 2099)
            throw new ArgumentException("Choose a month from 1900 through 2099.");
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            List<Payment> snapshot = new();
            await _database.RunInTransactionAsync(db =>
            {
                var existing = db.Table<PaymentRow>()
                    .Where(row => row.RecordMonth == month).ToList();
                var scheduleIds = existing.Where(row => row.ScheduleId is not null)
                    .Select(row => row.ScheduleId!).ToHashSet();
                var schedules = db.Table<RecurringPaymentRow>().ToList();

                foreach (var schedule in schedules)
                {
                    if (!schedule.IsActive || schedule.StartMonth > month ||
                        scheduleIds.Contains(schedule.Id)) continue;
                    var row = new PaymentRow
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = schedule.Name,
                        AmountPence = schedule.AmountPence,
                        DueDate = DueDateFor(month, schedule.DueDay),
                        RecordMonth = month,
                        ScheduleId = schedule.Id,
                        PaidDate = null
                    };
                    db.Insert(row);
                    existing.Add(row);
                    scheduleIds.Add(schedule.Id);
                }
                // Deleted rows still count above, preventing regeneration.
                snapshot = existing.Where(row => !row.IsDeleted)
                    .Select(row => row.ToPayment())
                    .OrderBy(payment => payment.DueDate)
                    .ThenBy(payment => payment.Name).ToList();
            });
            SelectedMonth = month;
            Payments.Clear();
            foreach (var payment in snapshot) Payments.Add(payment);
        }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(Payment payment)
    {
        ValidatePayment(payment);
        if (payment.ScheduleId is not null)
            throw new ArgumentException("Use a schedule to create recurring payments.");
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            await _database.InsertAsync(PaymentRow.FromPayment(payment));
            Publish(payment);
        }
        finally { _gate.Release(); }
    }

    public async Task UpdateAsync(Payment payment)
    {
        ValidatePayment(payment);
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            var previous = await _database.FindAsync<PaymentRow>(payment.Id.ToString());
            if (previous is null || previous.IsDeleted)
                throw new InvalidOperationException("This payment no longer exists.");
            if (previous.ScheduleId != payment.ScheduleId || previous.RecordMonth != payment.RecordMonth)
                throw new InvalidOperationException("A payment's schedule and month cannot be changed.");
            if (await _database.UpdateAsync(PaymentRow.FromPayment(payment)) != 1)
                throw new InvalidOperationException("The payment could not be updated.");
            Publish(payment);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(Payment payment)
    {
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            var row = await _database.FindAsync<PaymentRow>(payment.Id.ToString());
            if (row is null || row.IsDeleted)
                throw new InvalidOperationException("This payment no longer exists.");
            // Keep a hidden row so refresh doesn't recreate a deleted occurrence.
            row.IsDeleted = true;
            if (await _database.UpdateAsync(row) != 1)
                throw new InvalidOperationException("The payment could not be deleted.");
            int index = FindIndex(payment.Id);
            if (index >= 0) Payments.RemoveAt(index);
        }
        finally { _gate.Release(); }
    }

    public async Task<List<RecurringPaymentRow>> GetSchedulesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            return await _database.Table<RecurringPaymentRow>().OrderBy(row => row.Name).ToListAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task SaveScheduleAsync(RecurringPaymentRow schedule, bool isNew)
    {
        ValidateAmount(schedule.AmountPence / 100m);
        if (string.IsNullOrWhiteSpace(schedule.Name) || schedule.DueDay is < 1 or > 31)
            throw new ArgumentException("Enter a name and a due day from 1 to 31.");
        schedule.StartMonth = MonthOf(schedule.StartMonth);
        if (schedule.StartMonth.Year is < 1900 or > 2099)
            throw new ArgumentException("Choose a start month from 1900 through 2099.");
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            if (isNew) await _database.InsertAsync(schedule);
            else if (await _database.UpdateAsync(schedule) != 1)
                throw new InvalidOperationException("This schedule no longer exists.");
        }
        finally { _gate.Release(); }
    }

    private void Publish(Payment payment)
    {
        int index = FindIndex(payment.Id);
        if (index >= 0) Payments.RemoveAt(index);
        if (payment.RecordMonth != SelectedMonth) return;
        int position = 0;
        while (position < Payments.Count && Payments[position].DueDate <= payment.DueDate) position++;
        Payments.Insert(position, payment);
    }

    private int FindIndex(Guid id)
    {
        for (int i = 0; i < Payments.Count; i++)
            if (Payments[i].Id == id) return i;
        return -1;
    }

    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || amount > 999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Enter an amount from £0.01 to £999,999,999.99, with at most two decimal places.");
    }

    private static void ValidatePayment(Payment payment)
    {
        ValidateAmount(payment.Amount);
        if (string.IsNullOrWhiteSpace(payment.Name)) throw new ArgumentException("Enter a name.");
        if (payment.RecordMonth != MonthOf(payment.RecordMonth) ||
            MonthOf(payment.DueDate) != payment.RecordMonth)
            throw new ArgumentException("The due date must be within the payment's month.");
        if (payment.PaidDate?.Date > DateTime.Today)
            throw new ArgumentException("The paid date cannot be in the future.");
    }
}
