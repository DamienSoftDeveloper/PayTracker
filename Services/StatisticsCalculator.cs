using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PayTracker.Models;

namespace PayTracker.Services;

public static class StatisticsCalculator
{
    public static IReadOnlyList<MonthStatistics> CalculateRange(DateTime endMonth,
        IEnumerable<PaymentRow> paymentRows, IEnumerable<MonthlyBudgetRow> budgetRows,
        IEnumerable<AllowanceExpenseRow> expenseRows)
    {
        DateTime end = PaymentStore.MonthOf(endMonth);
        if (end.Year is < 1900 or > 2099) throw new ArgumentException("Choose a month from 1900 through 2099.");
        DateTime earliest = new(1900, 1, 1);
        DateTime start = end.AddMonths(-5);
        if (start < earliest) start = earliest;
        var payments = paymentRows.ToArray();
        var budgets = budgetRows.ToDictionary(b => b.MonthKey);
        var expenses = expenseRows.ToArray();
        var result = new List<MonthStatistics>();
        for (DateTime month = start; month <= end; month = month.AddMonths(1))
        {
            DateTime next = month.AddMonths(1);
            var allBillRows = payments.Where(p =>
                (p.RecordMonth ?? PaymentStore.MonthOf(p.DueDate)) == month).ToArray();
            var bills = allBillRows.Where(p => !p.IsDeleted).ToArray();
            var paidByDate = payments.Where(p => !p.IsDeleted && p.PaidDate is DateTime paid &&
                paid.Date >= month && paid.Date < next).ToArray();
            var purchases = expenses.Where(e => e.SpentDate.Date >= month && e.SpentDate.Date < next).ToArray();
            budgets.TryGetValue(BudgetCalculator.MonthKey(month), out var budget);
            decimal scheduled = bills.Sum(p => p.AmountPence / 100m);
            decimal spent = purchases.Sum(e => e.AmountPence / 100m);
            decimal? allowance = budget is null ? null : budget.AllowancePence / 100m;
            decimal? income = budget is null ? null : budget.ExpectedIncomePence / 100m;
            result.Add(new MonthStatistics
            {
                Month = month,
                HasAnyData = allBillRows.Length > 0 || paidByDate.Length > 0 || purchases.Length > 0 || budget is not null,
                HasBillRecords = allBillRows.Length > 0,
                BillCount = bills.Length,
                ScheduledBills = scheduled,
                PaidBillsForMonth = bills.Where(p => p.PaidDate.HasValue).Sum(p => p.AmountPence / 100m),
                UnpaidBillsForMonth = bills.Where(p => !p.PaidDate.HasValue).Sum(p => p.AmountPence / 100m),
                BillsPaidByDate = paidByDate.Sum(p => p.AmountPence / 100m),
                AllowanceSpent = spent,
                ExpectedIncome = income,
                AllowanceBudget = allowance,
                SavingsTarget = budget is null ? null : budget.SavingsTargetPence / 100m,
                ProjectedMonthlySurplus = budget is null ? null :
                    income!.Value - scheduled - Math.Max(allowance!.Value, spent)
            });
        }
        return result;
    }

    public static string CompareRecordedOutflow(MonthStatistics current, MonthStatistics? previous)
    {
        if (previous is null || !current.HasAnyData || !previous.HasAnyData)
            return "Previous-month comparison unavailable: one or both months have no saved records.";
        decimal difference = current.RecordedOutflow - previous.RecordedOutflow;
        string money = Math.Abs(difference).ToString("C2", CultureInfo.GetCultureInfo("en-GB"));
        if (difference == 0) return "Recorded outflow matches the previous month.";
        string direction = difference > 0 ? "more" : "less";
        string percent = previous.RecordedOutflow == 0 ? "Percentage unavailable because the previous total is £0." :
            $"Change: {(Math.Abs(difference) / previous.RecordedOutflow * 100m):0.0}%.";
        return $"Recorded outflow is {money} {direction} than the previous month. {percent}";
    }
}
