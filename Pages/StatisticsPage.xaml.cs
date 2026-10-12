using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsStore _store = StatisticsStore.Instance;
    private readonly Label _month = LessonUi.Heading("");
    private readonly Label _error = new() { TextColor = Colors.OrangeRed, IsVisible = false };
    private readonly Label _comparison = LessonUi.Text("");
    private readonly Label _periodNote = LessonUi.Text("");
    private readonly Label _updated = LessonUi.Text("");
    private readonly Grid _figures = LessonUi.Cards();
    private readonly VerticalStackLayout _history = new() { Spacing = 8 };
    private readonly StatisticsChart _outflowDrawing = new() { Kind = StatisticsChartKind.RecordedOutflow };
    private readonly StatisticsChart _surplusDrawing = new() { Kind = StatisticsChartKind.ProjectedSurplus };
    private readonly GraphicsView _outflowChart;
    private readonly GraphicsView _surplusChart;
    private readonly VerticalStackLayout _data = new() { Spacing = 14, IsVisible = false };
    private DateTime _selectedMonth;

    public StatisticsPage()
    {
        InitializeComponent();
        _selectedMonth = PaymentStore.Instance.SelectedMonth;
        _outflowChart = new GraphicsView { Drawable = _outflowDrawing, HeightRequest = 240 };
        _surplusChart = new GraphicsView { Drawable = _surplusDrawing, HeightRequest = 240 };
        var buttons = LessonUi.Actions(
                LessonUi.Button("Previous", () => RunLoadAsync(() => _selectedMonth.AddMonths(-1))),
                LessonUi.Button("Current", () => RunLoadAsync(() => DateTime.Today)),
                LessonUi.Button("Next", () => RunLoadAsync(() => _selectedMonth.AddMonths(1))));
        _data.Children.Add(_periodNote);
        _data.Children.Add(_figures);
        _data.Children.Add(_comparison);
        _data.Children.Add(LessonUi.Heading("Recorded spending by payment date"));
        _data.Children.Add(LessonUi.Text("Blue: bills marked paid. Purple: allowance purchases. Exact values are listed below."));
        _data.Children.Add(LessonUi.Card(_outflowChart));
        _data.Children.Add(LessonUi.Heading("Projected monthly surplus / shortfall"));
        _data.Children.Add(LessonUi.Text("Green: surplus. Red: shortfall. A dash means income inputs have not been saved. This is a plan, not confirmed savings."));
        _data.Children.Add(LessonUi.Card(_surplusChart));
        _data.Children.Add(LessonUi.Heading("Monthly figures"));
        _data.Children.Add(_history);
        _data.Children.Add(_updated);
        var body = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        body.Children.Add(LessonUi.Header("PayTracker / Insights", "See the bigger picture",
            "Explore your spending patterns and the plans behind them.")); body.Children.Add(_month);
        body.Children.Add(buttons);
        body.Children.Add(LessonUi.Button("Refresh statistics", () => RunLoadAsync(() => _selectedMonth)));
        body.Children.Add(_error); body.Children.Add(_data);
        body.Children.Add(LessonUi.Text(
            "Recorded spending is based on paid dates and saved purchases. It is not a complete bank statement. " +
            "Scheduled bill totals belong to their due month. Editing or deleting records revises these statistics."));
        Content = LessonUi.Scroll(body);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Application.Current is not null) Application.Current.RequestedThemeChanged += OnThemeChanged;
        await RunLoadAsync(() => PaymentStore.Instance.SelectedMonth);
    }

    protected override void OnDisappearing()
    {
        if (Application.Current is not null) Application.Current.RequestedThemeChanged -= OnThemeChanged;
        base.OnDisappearing();
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        _outflowChart.Invalidate(); _surplusChart.Invalidate();
    }

    private Task RunLoadAsync(Func<DateTime> month) => LessonUi.RunAsync(this, async () =>
    {
        // Hide previous figures if a reload fails, rather than leaving stale charts visible.
        _data.IsVisible = false;
        DateTime requested = PaymentStore.MonthOf(month());
        IReadOnlyList<MonthStatistics> months = await _store.LoadAsync(requested);
        _selectedMonth = requested;
        _month.Text = $"{months.Count} month{(months.Count == 1 ? "" : "s")} ending {requested:MMMM yyyy}";
        Render(months);
    }, _error);

    private void Render(IReadOnlyList<MonthStatistics> months)
    {
        MonthStatistics selected = months[^1];
        DateTime todayMonth = PaymentStore.MonthOf(DateTime.Today);
        _periodNote.Text = selected.Month == todayMonth
            ? "Current month: in progress. Comparing its recorded spending with a full earlier month can be misleading."
            : selected.Month > todayMonth
                ? "Future month: saved planning records only. Actual spending may not exist yet."
                : "Historical records: completeness depends on what you entered.";
        if (!selected.HasBillRecords)
            _periodNote.Text += " No bill records are saved for this month; its full bill plan may be incomplete.";
        _figures.Children.Clear();
        if (!selected.HasAnyData)
            LessonUi.AddCard(_figures, LessonUi.Card(LessonUi.Text("No saved records for the selected month.")));
        else
        {
            Figure("Recorded spending by payment date", selected.RecordedOutflow);
            Figure("Bills paid during this month", selected.BillsPaidByDate);
            Figure("Allowance purchases during this month", selected.AllowanceSpent);
            Figure("Bills scheduled for this month", selected.ScheduledBills);
            Figure("Scheduled bills still unpaid", selected.UnpaidBillsForMonth);
            Figure("Expected monthly income", selected.ExpectedIncome);
            Figure("Allowance budget", selected.AllowanceBudget);
            Figure("Allowance overspend", selected.AllowanceOverspend);
            Figure("Projected monthly surplus / shortfall", selected.ProjectedMonthlySurplus);
            Figure("Savings target (not a recorded transfer)", selected.SavingsTarget);
        }
        _comparison.Text = StatisticsCalculator.CompareRecordedOutflow(selected,
            months.Count > 1 ? months[^2] : null);
        _outflowDrawing.Months = months; _surplusDrawing.Months = months;
        _outflowChart.Invalidate(); _surplusChart.Invalidate();
        _history.Children.Clear();
        foreach (var month in months.Reverse())
        {
            var card = new VerticalStackLayout { Spacing = 5 };
            card.Children.Add(LessonUi.Heading(month.Month.ToString("MMMM yyyy")));
            if (!month.HasAnyData) card.Children.Add(LessonUi.Text("No saved records. Coverage unknown."));
            else
            {
                card.Children.Add(LessonUi.Text($"Recorded spending: {LessonUi.Money(month.RecordedOutflow)} " +
                    $"(paid bills {LessonUi.Money(month.BillsPaidByDate)} + purchases {LessonUi.Money(month.AllowanceSpent)})"));
                card.Children.Add(LessonUi.Text($"Scheduled bills: {LessonUi.Money(month.ScheduledBills)}; " +
                    $"unpaid: {LessonUi.Money(month.UnpaidBillsForMonth)}"));
                card.Children.Add(LessonUi.Text("Expected income: " + MoneyOrMissing(month.ExpectedIncome)));
                card.Children.Add(LessonUi.Text("Projected surplus / shortfall: " + MoneyOrMissing(month.ProjectedMonthlySurplus)));
                if (!month.HasBillRecords) card.Children.Add(LessonUi.Text("No saved bill records; plan may be incomplete."));
            }
            _history.Children.Add(LessonUi.Card(card));
        }
        _updated.Text = $"Refreshed: {DateTime.Now:dd MMM yyyy HH:mm}.";
        _data.IsVisible = true;
    }

    private static string MoneyOrMissing(decimal? amount) => amount is decimal value ? LessonUi.Money(value) : "Not entered";
    private void Figure(string title, decimal? amount) => LessonUi.AddCard(_figures,
        LessonUi.MetricCard(title, LessonUi.Metric(MoneyOrMissing(amount)),
            amount < 0 ? Color.FromArgb("#B42335") : null));
}
