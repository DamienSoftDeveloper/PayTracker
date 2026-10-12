using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using PayTracker.Models;
using SQLite;

namespace PayTracker.Services;

public sealed class StatisticsStore
{
    private static readonly Lazy<StatisticsStore> Shared = new(() => new StatisticsStore(
        Path.Combine(FileSystem.AppDataDirectory, "paytrack.db3")));
    public static StatisticsStore Instance => Shared.Value;
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public StatisticsStore(string databasePath)
    {
        _database = new SQLiteAsyncConnection(databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public async Task<IReadOnlyList<MonthStatistics>> LoadAsync(DateTime endMonth)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_initialized)
            {
                // Ensure the existing schema without generating recurring occurrences.
                await _database.RunInTransactionAsync(db =>
                {
                    db.CreateTable<PaymentRow>();
                    db.CreateTable<MonthlyBudgetRow>();
                    db.CreateTable<AllowanceExpenseRow>();
                    foreach (var row in db.Table<PaymentRow>().ToList())
                    {
                        if (row.RecordMonth is null)
                        {
                            row.RecordMonth = PaymentStore.MonthOf(row.DueDate);
                            db.Update(row);
                        }
                    }
                });
                _initialized = true;
            }
            IReadOnlyList<MonthStatistics>? result = null;
            await _database.RunInTransactionAsync(db =>
            {
                // One consistent snapshot. Paid dates may fall outside a bill's due month.
                result = StatisticsCalculator.CalculateRange(endMonth,
                    db.Table<PaymentRow>().ToList(),
                    db.Table<MonthlyBudgetRow>().ToList(),
                    db.Table<AllowanceExpenseRow>().ToList());
            });
            return result!;
        }
        finally { _gate.Release(); }
    }
}
