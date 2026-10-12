using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PayTracker.Models;

namespace PayTracker.Services;

public static class BudgetCalculator
{
    public const decimal MaximumAmount = 999_999_999.99m;
    public static string MonthKey(DateTime month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public static DateTime MonthFromKey(string key)
    {
        if (!DateTime.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var month) || month.Year is < 1900 or > 2099)
            throw new ArgumentException("Choose a month from January 1900 through December 2099.");
        return month;
    }

    public static long ToPence(decimal amount, bool allowNegative = false)
    {
        if ((!allowNegative && amount < 0) || Math.Abs(amount) > MaximumAmount ||
            decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Enter a valid amount with no more than two decimal places.");
        return checked((long)(amount * 100m));
    }

    public static void Validate(MonthlyBudgetRow budget)
    {
        MonthFromKey(budget.MonthKey);
        ToPence(budget.BalancePence / 100m, allowNegative: true);
        ToPence(budget.ExpectedIncomePence / 100m);
        ToPence(budget.IncomeReceivedPence / 100m);
        ToPence(budget.AllowancePence / 100m);
        ToPence(budget.SavingsTargetPence / 100m);
    }

    public static BudgetResult Calculate(MonthlyBudgetRow budget,
        IEnumerable<Payment> payments, IEnumerable<AllowanceExpenseRow> expenses,
        DateTime calculatedUtc)
    {
        Validate(budget);
        DateTime month = MonthFromKey(budget.MonthKey);
        Payment[] allRecords = payments.ToArray();
        Payment[] bills = allRecords.Where(p => p.RecordMonth == month).ToArray();
        decimal scheduled = bills.Sum(p => p.Amount);
        decimal paid = bills.Where(p => p.IsPaid).Sum(p => p.Amount);
        decimal unpaid = bills.Where(p => !p.IsPaid).Sum(p => p.Amount);
        decimal priorUnpaid = allRecords.Where(p => p.RecordMonth < month && !p.IsPaid).Sum(p => p.Amount);
        decimal spent = expenses.Where(e => e.MonthKey == budget.MonthKey).Sum(e => e.AmountPence / 100m);
        decimal allowance = budget.AllowancePence / 100m;
        decimal remaining = Math.Max(allowance - spent, 0);
        decimal overspend = Math.Max(spent - allowance, 0);
        decimal income = budget.ExpectedIncomePence / 100m;
        decimal incomeLeft = Math.Max(income - budget.IncomeReceivedPence / 100m, 0);
        decimal surplus = income - scheduled - Math.Max(allowance, spent);
        decimal balance = budget.BalancePence / 100m;
        decimal closing = budget.UseCurrentBalance
            ? balance + incomeLeft - unpaid - priorUnpaid - remaining
            : balance + surplus;
        decimal target = budget.SavingsTargetPence / 100m;
        return new BudgetResult
        {
            ScheduledBills = scheduled,
            PaidBills = paid,
            UnpaidBills = unpaid,
            PriorMonthUnpaidBills = priorUnpaid,
            AllowanceSpent = spent,
            AllowanceRemaining = remaining,
            AllowanceOverspend = overspend,
            IncomeStillExpected = incomeLeft,
            ProjectedMonthlySurplus = surplus,
            ProjectedClosingBalance = closing,
            SavingsTarget = target,
            BalanceAfterSavingsTarget = closing - target,
            SavingsTargetShortfall = Math.Max(target - Math.Max(closing, 0), 0),
            UseCurrentBalance = budget.UseCurrentBalance,
            CalculatedUtc = calculatedUtc
        };
    }
}
