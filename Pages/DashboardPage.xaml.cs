using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly PaymentStore _store = PaymentStore.Instance;
    private readonly Label _month = LessonUi.Heading("");
    private readonly Label _error = new() { TextColor = Colors.OrangeRed, IsVisible = false };
    private readonly Label _total = LessonUi.Heading("--");
    private readonly Label _paid = LessonUi.Heading("--");
    private readonly Label _remaining = LessonUi.Heading("--");
    private readonly Label _overdue = LessonUi.Heading("--");
    private readonly Label _count = LessonUi.Text("");
    private readonly Label _updated = LessonUi.Text("");
    private readonly VerticalStackLayout _preview = new() { Spacing = 8 };

    public DashboardPage()
    {
        InitializeComponent();
        var cards = new Grid
        {
            ColumnSpacing = 12, RowSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        cards.Add(SummaryCard("Scheduled for month", _total), 0, 0);
        cards.Add(SummaryCard("Paid for month", _paid), 1, 0);
        cards.Add(SummaryCard("Remaining", _remaining), 0, 1);
        cards.Add(SummaryCard("Overdue as of today", _overdue), 1, 1);
        var body = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        body.Children.Add(LessonUi.Heading("Welcome to PayTracker"));
        body.Children.Add(LessonUi.MonthBar(this, _store, SelectMonthAsync, _month, _error));
        body.Children.Add(LessonUi.Button("Refresh dashboard", () => LessonUi.RunAsync(this,
            () => SelectMonthAsync(_store.SelectedMonth), _error)));
        body.Children.Add(_error);
        body.Children.Add(LessonUi.Text("Totals belong to the selected payment month, even when you paid on a different date."));
        body.Children.Add(cards); body.Children.Add(_count);
        body.Children.Add(LessonUi.Heading("Next unpaid payments"));
        body.Children.Add(LessonUi.Text("Up to five in this month, earliest due first. Includes overdue payments."));
        body.Children.Add(_preview); body.Children.Add(_updated);
        Content = new ScrollView { Content = body };
    }

    private static Border SummaryCard(string title, Label value) => LessonUi.Card(
        new VerticalStackLayout { Spacing = 8, Children = { LessonUi.Text(title), value } });

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LessonUi.RunAsync(this, () => SelectMonthAsync(_store.SelectedMonth), _error);
    }

    private async Task SelectMonthAsync(DateTime month)
    {
        await _store.SelectMonthAsync(month);
        Payment[] payments = _store.Payments.ToArray();
        var unpaid = payments.Where(p => !p.IsPaid).ToArray();
        var paid = payments.Where(p => p.IsPaid).ToArray();
        var overdue = unpaid.Where(p => p.DueDate.Date < DateTime.Today).ToArray();
        _month.Text = _store.SelectedMonth.ToString("MMMM yyyy");
        _total.Text = LessonUi.Money(payments.Sum(p => p.Amount));
        _paid.Text = LessonUi.Money(paid.Sum(p => p.Amount));
        _remaining.Text = LessonUi.Money(unpaid.Sum(p => p.Amount));
        _overdue.Text = LessonUi.Money(overdue.Sum(p => p.Amount));
        _count.Text = $"{payments.Length} payments: {paid.Length} paid, {unpaid.Length} unpaid, {overdue.Length} overdue.";
        _preview.Children.Clear();
        if (unpaid.Length == 0) _preview.Children.Add(LessonUi.Text("No unpaid payments in this month."));
        foreach (var payment in unpaid.OrderBy(p => p.DueDate).ThenBy(p => p.Name).Take(5))
        {
            _preview.Children.Add(LessonUi.Card(new VerticalStackLayout
            {
                Spacing = 6, Children =
                {
                    LessonUi.Heading(payment.Name), LessonUi.Text(payment.AmountText),
                    LessonUi.Text(payment.DueText), LessonUi.Text(payment.KindText)
                }
            }));
        }
        _updated.Text = $"Last refreshed: {DateTime.Now:dd MMM yyyy HH:mm}";
    }
}
