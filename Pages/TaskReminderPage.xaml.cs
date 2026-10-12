using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public partial class TaskReminderPage : ContentPage
{
    private readonly PaymentStore _store = PaymentStore.Instance;
    private readonly Label _month = LessonUi.Heading("");
    private readonly Label _error = new() { TextColor = Colors.OrangeRed, IsVisible = false };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly VerticalStackLayout _toolbar = new() { Spacing = 8 };
    private readonly VerticalStackLayout _form = new() { Spacing = 10 };
    private readonly ScrollView _editor;
    private readonly Button _add;
    private readonly Entry _name = new() { Placeholder = "Payment name" };
    private readonly Entry _amount = new() { Keyboard = Keyboard.Numeric, Placeholder = "25.00" };
    private readonly DatePicker _due = new() { Format = "dd MMM yyyy" };
    private readonly DatePicker _paid = new() { Format = "dd MMM yyyy" };
    private Payment? _editing;
    private bool _paidMode;
    private bool _loaded;

    public TaskReminderPage()
    {
        InitializeComponent();
        _add = LessonUi.Button("Add payment", () =>
        {
            if (_loaded) OpenEditor(null, false);
            return Task.CompletedTask;
        }, LessonUi.ButtonTone.Primary);
        _add.IsEnabled = false;
        _toolbar.Children.Add(LessonUi.Header("PayTracker / Payments", "Stay ahead of your bills",
            "Track due dates, recurring payments and the things you've already paid."));
        _toolbar.Children.Add(LessonUi.MonthBar(this, _store, SelectMonthAsync, _month, _error));
        var schedules = LessonUi.Button("Recurring schedules", () => LessonUi.RunAsync(this,
            () => Navigation.PushAsync(new RecurringPaymentsPage()), _error));
        var refresh = LessonUi.Button("Refresh", () => LessonUi.RunAsync(this,
            () => SelectMonthAsync(_store.SelectedMonth), _error));
        _toolbar.Children.Add(LessonUi.Actions(_add, refresh));
        _toolbar.Children.Add(schedules);

        _list.ItemsSource = _store.Payments;
        _list.EmptyView = LessonUi.Card(LessonUi.Header("ALL CLEAR", "No payments yet",
            "Add a one-off payment or set up a recurring schedule for this month."));
        _list.ItemTemplate = new DataTemplate(CreatePaymentCard);
        _editor = new ScrollView { Content = _form, IsVisible = false };

        var grid = new Grid
        {
            Padding = 22, RowSpacing = 14,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };
        grid.Add(_toolbar, 0, 0);
        grid.Add(_error, 0, 1);
        grid.Add(_list, 0, 2);
        grid.Add(_editor, 0, 2);
        Content = LessonUi.Constrain(grid);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_editor.IsVisible) return;
        await LessonUi.RunAsync(this, () => SelectMonthAsync(_store.SelectedMonth), _error);
    }

    private async Task SelectMonthAsync(DateTime month)
    {
        await _store.SelectMonthAsync(month);
        _loaded = true;
        _add.IsEnabled = true;
        _month.Text = _store.SelectedMonth.ToString("MMMM yyyy");
    }

    private View CreatePaymentCard()
    {
        var name = LessonUi.Heading("");
        name.SetBinding(Label.TextProperty, nameof(Payment.Name));
        var trigger = new DataTrigger(typeof(Label))
        {
            Binding = new Binding(nameof(Payment.IsPaid)), Value = true
        };
        trigger.Setters.Add(new Setter { Property = Label.TextDecorationsProperty, Value = TextDecorations.Strikethrough });
        name.Triggers.Add(trigger);
        var stack = new VerticalStackLayout { Spacing = 6 };
        var amount = LessonUi.Metric("");
        amount.FontSize = 24; amount.MaximumWidthRequest = 160;
        amount.HorizontalTextAlignment = TextAlignment.End;
        amount.SetBinding(Label.TextProperty, nameof(Payment.AmountText));
        var heading = new Grid { ColumnSpacing = 10, ColumnDefinitions =
            { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        heading.Add(name, 0, 0); heading.Add(amount, 1, 0);
        stack.Children.Add(heading);
        foreach (string property in new[] { nameof(Payment.DueText),
            nameof(Payment.StatusText), nameof(Payment.KindText) })
        {
            var label = LessonUi.Text("");
            label.SetBinding(Label.TextProperty, property);
            stack.Children.Add(label);
        }
        var edit = PaymentButton("Edit", p =>
        {
            OpenEditor(p, false); return Task.CompletedTask;
        });
        var record = PaymentButton("Paid date", p =>
        {
            OpenEditor(p, true); return Task.CompletedTask;
        });
        LessonUi.StyleButton(record, LessonUi.ButtonTone.Primary);
        stack.Children.Add(LessonUi.Actions(edit, record));
        var unpaid = PaymentButton("Mark unpaid", async p =>
        {
            if (await DisplayAlertAsync("Mark unpaid?", "Clear this month's paid date?", "Clear", "Cancel"))
                await _store.UpdateAsync(p with { PaidDate = null });
        });
        unpaid.SetBinding(IsVisibleProperty, nameof(Payment.IsPaid));
        var delete = PaymentButton("Delete", async p =>
        {
            if (await DisplayAlertAsync("Delete payment?",
                "Delete this month's record? Other months and its recurring schedule are kept.", "Delete", "Cancel"))
                await _store.DeleteAsync(p);
        });
        LessonUi.StyleButton(delete, LessonUi.ButtonTone.Danger);
        stack.Children.Add(LessonUi.Actions(unpaid, delete));
        return LessonUi.Card(stack);
    }

    private Button PaymentButton(string text, Func<Payment, Task> action)
    {
        var button = new Button { Text = text };
        LessonUi.StyleButton(button);
        button.SetBinding(Button.CommandParameterProperty, ".");
        button.Clicked += async (_, _) =>
        {
            if (button.CommandParameter is Payment payment)
                await LessonUi.RunAsync(this, () => action(payment), _error);
        };
        return button;
    }

    private void OpenEditor(Payment? payment, bool paidMode)
    {
        _editing = payment;
        _paidMode = paidMode;
        _error.IsVisible = false;
        _form.Children.Clear();
        _form.Children.Add(LessonUi.Heading(paidMode ? "Record paid date" :
            payment is null ? "Add one-off payment" : "Edit this month's payment"));
        if (paidMode)
        {
            _paid.MaximumDate = DateTime.Today;
            _paid.Date = payment?.PaidDate ?? DateTime.Today;
            _form.Children.Add(LessonUi.Text("Paid date (may be in another month)"));
            _form.Children.Add(_paid);
        }
        else
        {
            _name.Text = payment?.Name ?? "";
            _amount.Text = payment is null ? "" : LessonUi.AmountEntryText(payment.Amount);
            // Clear the old range before switching months.
            _due.MinimumDate = new DateTime(1900, 1, 1);
            _due.MaximumDate = new DateTime(2100, 12, 31);
            var month = payment?.RecordMonth ?? _store.SelectedMonth;
            _due.Date = payment?.DueDate ?? month;
            _due.MinimumDate = month;
            _due.MaximumDate = month.AddMonths(1).AddDays(-1);
            _form.Children.Add(LessonUi.Text("Name")); _form.Children.Add(_name);
            _form.Children.Add(LessonUi.Text("Amount (£), for example 12.99")); _form.Children.Add(_amount);
            _form.Children.Add(LessonUi.Text("Due date within the selected month")); _form.Children.Add(_due);
            if (payment?.IsRecurring == true)
                _form.Children.Add(LessonUi.Text("This edit affects only this month's record. Change future defaults in Recurring schedules."));
        }
        _form.Children.Add(LessonUi.Button("Save payment", () => LessonUi.RunAsync(this, SaveAsync, _error), LessonUi.ButtonTone.Primary));
        _form.Children.Add(LessonUi.Button("Cancel", () => { CloseEditor(); return Task.CompletedTask; }));
        _toolbar.IsVisible = false;
        _list.IsVisible = false;
        _editor.IsVisible = true;
    }

    private async Task SaveAsync()
    {
        Payment payment;
        if (_paidMode)
        {
            if (_editing is null || _paid.Date is not DateTime paid)
                throw new ArgumentException("Choose a paid date.");
            payment = _editing with { PaidDate = paid.Date };
        }
        else
        {
            string name = _name.Text?.Trim() ?? "";
            if (name.Length == 0) throw new ArgumentException("Enter a name.");
            if (!LessonUi.TryAmount(_amount.Text, out decimal amount))
                throw new ArgumentException("Enter a positive amount with at most two decimal places, using a decimal point.");
            if (_due.Date is not DateTime due) throw new ArgumentException("Choose a due date.");
            payment = (_editing ?? new Payment { RecordMonth = _store.SelectedMonth }) with
                { Name = name, Amount = amount, DueDate = due.Date };
        }
        if (_editing is null) await _store.AddAsync(payment);
        else await _store.UpdateAsync(payment);
        CloseEditor();
    }

    private void CloseEditor()
    {
        _editor.IsVisible = false; _toolbar.IsVisible = true; _list.IsVisible = true;
        _editing = null; _error.IsVisible = false;
    }
}
