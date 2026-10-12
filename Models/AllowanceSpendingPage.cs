using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public sealed class AllowanceSpendingPage : ContentPage
{
    private readonly BudgetStore _store = BudgetStore.Instance;
    private readonly DateTime _month;
    private readonly Label _error = new() { IsVisible = false, TextColor = Colors.OrangeRed };
    private readonly VerticalStackLayout _rows = new() { Spacing = 8 };
    private readonly VerticalStackLayout _form = new() { Spacing = 10 };
    private readonly VerticalStackLayout _toolbar = new() { Spacing = 8 };
    private readonly ScrollView _listScroll;
    private readonly ScrollView _formScroll;
    private readonly Entry _description = new() { Placeholder = "For example, food delivery" };
    private readonly Entry _amount = new() { Keyboard = Keyboard.Numeric, Placeholder = "18.50" };
    private readonly DatePicker _date = new() { Format = "dd MMM yyyy" };
    private readonly Button _add;
    private AllowanceExpenseRow? _editing;

    public AllowanceSpendingPage(DateTime month)
    {
        _month = PaymentStore.MonthOf(month);
        Title = $"Spending — {_month:MMM yyyy}";
        _add = LessonUi.Button("Add spending", () => { OpenEditor(null); return Task.CompletedTask; });
        _add.IsEnabled = false;
        _toolbar.Children.Add(LessonUi.Text(
            "Record paid discretionary purchases here. Do not also record the same purchase as a bill in Task Reminders."));
        _toolbar.Children.Add(_add);
        _toolbar.Children.Add(LessonUi.Button("Refresh", () => LessonUi.RunAsync(this, LoadAsync, _error)));
        _listScroll = new ScrollView { Content = _rows };
        _formScroll = new ScrollView { Content = _form, IsVisible = false };
        var grid = new Grid
        {
            Padding = 16,
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        grid.Add(_toolbar, 0, 0); grid.Add(_error, 0, 1);
        grid.Add(_listScroll, 0, 2); grid.Add(_formScroll, 0, 2);
        Content = grid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_formScroll.IsVisible) await LessonUi.RunAsync(this, LoadAsync, _error);
    }

    private async Task LoadAsync()
    {
        var expenses = await _store.GetExpensesAsync(_month);
        _rows.Children.Clear();
        _add.IsEnabled = _month <= PaymentStore.MonthOf(DateTime.Today);
        if (!_add.IsEnabled) _rows.Children.Add(LessonUi.Text("Actual spending can be entered when this month arrives."));
        if (expenses.Count == 0) _rows.Children.Add(LessonUi.Text("No allowance spending recorded in this month."));
        foreach (var expense in expenses)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(LessonUi.Heading(expense.Description));
            stack.Children.Add(LessonUi.Text(expense.AmountText));
            stack.Children.Add(LessonUi.Text(expense.DateText));
            stack.Children.Add(LessonUi.Button("Edit", () => { OpenEditor(expense); return Task.CompletedTask; }));
            stack.Children.Add(LessonUi.Button("Delete", () => LessonUi.RunAsync(this, async () =>
            {
                if (await DisplayAlertAsync("Delete spending?", $"Delete {expense.Description}?", "Delete", "Cancel"))
                {
                    await _store.DeleteExpenseAsync(expense);
                    await LoadAsync();
                }
            }, _error)));
            _rows.Children.Add(LessonUi.Card(stack));
        }
    }

    private void OpenEditor(AllowanceExpenseRow? expense)
    {
        if (_month > PaymentStore.MonthOf(DateTime.Today)) return;
        _editing = expense;
        _description.Text = expense?.Description ?? "";
        _amount.Text = expense is null ? "" : LessonUi.AmountEntryText(expense.AmountPence / 100m);
        _date.MinimumDate = new DateTime(1900, 1, 1);
        _date.MaximumDate = new DateTime(2099, 12, 31);
        DateTime end = _month.AddMonths(1).AddDays(-1);
        DateTime maximum = end < DateTime.Today ? end : DateTime.Today;
        _date.Date = expense?.SpentDate ?? maximum;
        _date.MinimumDate = _month; _date.MaximumDate = maximum;
        _error.IsVisible = false; _form.Children.Clear();
        _form.Children.Add(LessonUi.Heading(expense is null ? "Add spending" : "Edit spending"));
        _form.Children.Add(LessonUi.Text("Description")); _form.Children.Add(_description);
        _form.Children.Add(LessonUi.Text("Amount (£), using a decimal point")); _form.Children.Add(_amount);
        _form.Children.Add(LessonUi.Text("Date spent")); _form.Children.Add(_date);
        _form.Children.Add(LessonUi.Button("Save spending", () => LessonUi.RunAsync(this, SaveAsync, _error)));
        _form.Children.Add(LessonUi.Button("Cancel", () => { CloseEditor(); return Task.CompletedTask; }));
        _toolbar.IsVisible = false; _listScroll.IsVisible = false; _formScroll.IsVisible = true;
    }

    private async Task SaveAsync()
    {
        string description = _description.Text?.Trim() ?? "";
        if (description.Length == 0) throw new ArgumentException("Enter a description.");
        if (!LessonUi.TryAmount(_amount.Text, out decimal amount))
            throw new ArgumentException("Enter a positive amount with at most two decimal places, using a decimal point.");
        if (_date.Date is not DateTime date) throw new ArgumentException("Choose a date.");
        var expense = new AllowanceExpenseRow
        {
            Id = _editing?.Id ?? Guid.NewGuid().ToString(),
            MonthKey = BudgetCalculator.MonthKey(_month),
            Description = description,
            AmountPence = BudgetCalculator.ToPence(amount),
            SpentDate = date.Date
        };
        await _store.SaveExpenseAsync(expense, _editing is null);
        CloseEditor();
        await LoadAsync();
    }

    private void CloseEditor()
    {
        _formScroll.IsVisible = false; _toolbar.IsVisible = true; _listScroll.IsVisible = true;
        _editing = null; _error.IsVisible = false;
    }
}
