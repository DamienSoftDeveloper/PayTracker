using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public sealed class RecurringPaymentsPage : ContentPage
{
    private readonly PaymentStore _store = PaymentStore.Instance;
    private readonly Label _error = new() { TextColor = Colors.OrangeRed, IsVisible = false };
    private readonly VerticalStackLayout _rows = new() { Spacing = 10 };
    private readonly VerticalStackLayout _form = new() { Spacing = 10 };
    private readonly ScrollView _listScroll;
    private readonly ScrollView _formScroll;
    private readonly VerticalStackLayout _toolbar = new() { Spacing = 8 };
    private readonly Entry _name = new() { Placeholder = "For example, Rent" };
    private readonly Entry _amount = new() { Keyboard = Keyboard.Numeric, Placeholder = "300.00" };
    private readonly Entry _day = new() { Keyboard = Keyboard.Numeric, Placeholder = "1 to 31" };
    private readonly DatePicker _start = new() { Format = "MMM yyyy",
        MinimumDate = new DateTime(1900, 1, 1), MaximumDate = new DateTime(2099, 12, 31) };
    private readonly Switch _active = new();
    private RecurringPaymentRow? _editing;

    public RecurringPaymentsPage()
    {
        Title = "Recurring schedules";
        _toolbar.Children.Add(LessonUi.Button("Add recurring schedule", () =>
        {
            OpenEditor(null); return Task.CompletedTask;
        }));
        _toolbar.Children.Add(LessonUi.Button("Refresh", () => LessonUi.RunAsync(this, LoadAsync, _error)));
        _listScroll = new ScrollView { Content = _rows };
        _formScroll = new ScrollView { Content = _form, IsVisible = false };
        var grid = new Grid
        {
            Padding = 16, RowSpacing = 10,
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
        var schedules = await _store.GetSchedulesAsync();
        _rows.Children.Clear();
        _rows.Children.Add(LessonUi.Text(
            "Opening a month creates its missing records. Existing records keep their names, " +
            "amounts, due dates and paid status when a schedule changes."));
        _rows.Children.Add(LessonUi.Text(
            "Turning a schedule off stops new records being created. It keeps records already created, " +
            "including any future months you have opened."));
        if (schedules.Count == 0) _rows.Children.Add(LessonUi.Text("No recurring schedules yet."));
        foreach (var schedule in schedules)
        {
            var body = new VerticalStackLayout { Spacing = 6 };
            body.Children.Add(LessonUi.Heading(schedule.Name));
            body.Children.Add(LessonUi.Text(LessonUi.Money(schedule.AmountPence / 100m)));
            body.Children.Add(LessonUi.Text($"Due day: {schedule.DueDay}; starts {schedule.StartMonth:MMM yyyy}"));
            body.Children.Add(LessonUi.Text(schedule.IsActive ? "Active" : "Inactive"));
            body.Children.Add(LessonUi.Button("Edit schedule", () =>
            {
                OpenEditor(schedule); return Task.CompletedTask;
            }));
            _rows.Children.Add(LessonUi.Card(body));
        }
    }

    private void OpenEditor(RecurringPaymentRow? schedule)
    {
        _editing = schedule;
        _name.Text = schedule?.Name ?? "";
        _amount.Text = schedule is null ? "" : LessonUi.AmountEntryText(schedule.AmountPence / 100m);
        _day.Text = schedule?.DueDay.ToString() ?? "1";
        _start.Date = schedule?.StartMonth ?? _store.SelectedMonth;
        _active.IsToggled = schedule?.IsActive ?? true;
        _error.IsVisible = false;
        _form.Children.Clear();
        _form.Children.Add(LessonUi.Heading(schedule is null ? "New recurring schedule" : "Edit recurring schedule"));
        _form.Children.Add(LessonUi.Text("Name")); _form.Children.Add(_name);
        _form.Children.Add(LessonUi.Text("Amount (£), using a decimal point")); _form.Children.Add(_amount);
        _form.Children.Add(LessonUi.Text("Due day (1 to 31). Short months use their last day.")); _form.Children.Add(_day);
        _form.Children.Add(LessonUi.Text("Start month (the selected day is ignored)")); _form.Children.Add(_start);
        _form.Children.Add(LessonUi.Text("Active: generate missing monthly records")); _form.Children.Add(_active);
        _form.Children.Add(LessonUi.Text(
            "Changes apply to records that have not been created yet. Records already created stay unchanged."));
        _form.Children.Add(LessonUi.Button("Save schedule", () => LessonUi.RunAsync(this, SaveAsync, _error)));
        _form.Children.Add(LessonUi.Button("Cancel", () => { CloseEditor(); return Task.CompletedTask; }));
        _toolbar.IsVisible = false; _listScroll.IsVisible = false; _formScroll.IsVisible = true;
    }

    private async Task SaveAsync()
    {
        string name = _name.Text?.Trim() ?? "";
        if (name.Length == 0) throw new ArgumentException("Enter a name.");
        if (!LessonUi.TryAmount(_amount.Text, out decimal amount))
            throw new ArgumentException("Enter a positive amount with at most two decimal places, using a decimal point.");
        if (!int.TryParse(_day.Text, out int day) || day is < 1 or > 31)
            throw new ArgumentException("Enter a whole due day from 1 to 31.");
        if (_start.Date is not DateTime start) throw new ArgumentException("Choose a start month.");
        var schedule = new RecurringPaymentRow
        {
            Id = _editing?.Id ?? Guid.NewGuid().ToString(), Name = name,
            AmountPence = checked((long)(amount * 100m)), DueDay = day,
            StartMonth = PaymentStore.MonthOf(start), IsActive = _active.IsToggled
        };
        await _store.SaveScheduleAsync(schedule, _editing is null);
        CloseEditor();
        await LoadAsync();
    }

    private void CloseEditor()
    {
        _formScroll.IsVisible = false; _toolbar.IsVisible = true; _listScroll.IsVisible = true;
        _editing = null; _error.IsVisible = false;
    }
}
