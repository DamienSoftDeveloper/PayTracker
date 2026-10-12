using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using PayTracker.Models;
using SQLite;

namespace PayTracker.Services;

public sealed class BudgetStore
{
    private static readonly Lazy<BudgetStore> Shared = new(() => new BudgetStore(
        Path.Combine(FileSystem.AppDataDirectory, "paytrack.db3"), PaymentStore.Instance));
    public static BudgetStore Instance => Shared.Value;
    private readonly SQLiteAsyncConnection _database;
    private readonly PaymentStore _payments;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public BudgetStore(string databasePath, PaymentStore payments)
    {
        _payments = payments;
        _database = new SQLiteAsyncConnection(databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        await _database.RunInTransactionAsync(db =>
        {
            db.CreateTable<MonthlyBudgetRow>();
            db.CreateTable<AllowanceExpenseRow>();
        });
        _initialized = true;
    }

    public async Task<MonthlyBudgetRow?> GetBudgetAsync(DateTime month)
    {
        string key = BudgetCalculator.MonthKey(month);
        BudgetCalculator.MonthFromKey(key);
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            return await _database.FindAsync<MonthlyBudgetRow>(key);
        }
        finally { _gate.Release(); }
    }

    public async Task<List<AllowanceExpenseRow>> GetExpensesAsync(DateTime month)
    {
        string key = BudgetCalculator.MonthKey(month);
        BudgetCalculator.MonthFromKey(key);
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            return await _database.Table<AllowanceExpenseRow>()
                .Where(e => e.MonthKey == key).OrderByDescending(e => e.SpentDate).ToListAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task SaveExpenseAsync(AllowanceExpenseRow expense, bool isNew)
    {
        BudgetCalculator.MonthFromKey(expense.MonthKey);
        if (string.IsNullOrWhiteSpace(expense.Description)) throw new ArgumentException("Enter a description.");
        if (expense.AmountPence <= 0) throw new ArgumentException("Spending must be greater than zero.");
        BudgetCalculator.ToPence(expense.AmountPence / 100m);
        if (BudgetCalculator.MonthKey(expense.SpentDate) != expense.MonthKey || expense.SpentDate.Date > DateTime.Today)
            throw new ArgumentException("Choose a spending date in this month, no later than today.");
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            if (isNew) await _database.InsertAsync(expense);
            else
            {
                var previous = await _database.FindAsync<AllowanceExpenseRow>(expense.Id);
                if (previous is null || previous.MonthKey != expense.MonthKey)
                    throw new InvalidOperationException("This spending entry no longer exists in this month.");
                if (await _database.UpdateAsync(expense) != 1)
                    throw new InvalidOperationException("The spending entry could not be saved.");
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteExpenseAsync(AllowanceExpenseRow expense)
    {
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            if (await _database.DeleteAsync<AllowanceExpenseRow>(expense.Id) != 1)
                throw new InvalidOperationException("This spending entry no longer exists.");
        }
        finally { _gate.Release(); }
    }

    public async Task<BudgetResult> SaveAndCalculateAsync(MonthlyBudgetRow budget)
    {
        BudgetCalculator.Validate(budget);
        DateTime month = BudgetCalculator.MonthFromKey(budget.MonthKey);
        if (budget.UseCurrentBalance && month != PaymentStore.MonthOf(DateTime.Today))
            throw new ArgumentException("Today's balance mode is only available for the current month. Use the monthly plan for other months.");

        // Generate missing records using the existing shared payment service.
        await _payments.SelectMonthAsync(month);
        await _gate.WaitAsync();
        try
        {
            await InitializeAsync();
            BudgetResult? result = null;
            DateTime now = DateTime.UtcNow;
            await _database.RunInTransactionAsync(db =>
            {
                var bills = db.Table<PaymentRow>().Where(p => p.RecordMonth <= month).ToList()
                    .Where(p => !p.IsDeleted).Select(p => p.ToPayment()).ToArray();
                var expenses = db.Table<AllowanceExpenseRow>().Where(e => e.MonthKey == budget.MonthKey).ToList();
                // Calculate first: a failure leaves previously saved inputs intact.
                result = BudgetCalculator.Calculate(budget, bills, expenses, now);
                budget.UpdatedUtc = now;
                db.InsertOrReplace(budget);
            });
            return result!;
        }
        finally { _gate.Release(); }
    }
}
