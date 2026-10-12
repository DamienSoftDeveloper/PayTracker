using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public partial class IncomePage : ContentPage
{
    private readonly PaymentStore _payments = PaymentStore.Instance;
    private readonly BudgetStore _budgets = BudgetStore.Instance;
    private readonly Label _month = LessonUi.Heading("");
    private readonly Label _error = new() { IsVisible = false, TextColor = Colors.OrangeRed };
    private readonly Picker _mode = new() { Title = "Calculation mode" };
    private readonly Label _balanceLabel = LessonUi.Text("Opening bank balance (£)");
    private readonly Entry _balance = new() { Placeholder = "Bank balance (negative is allowed)", Keyboard = Keyboard.Default };
    private readonly Entry _income = MoneyEntry("Expected take-home income for the month");
    private readonly Entry _received = MoneyEntry("Income already received this month");
    private readonly Entry _allowance = MoneyEntry("Monthly allowance");
    private readonly Entry _savings = MoneyEntry("Savings target");
    private readonly CheckBox _confirmed = new();
    private readonly VerticalStackLayout _currentFields = new() { Spacing = 8, IsVisible = false };
    private readonly VerticalStackLayout _settings = new() { Spacing = 10, IsEnabled = false };
    private readonly VerticalStackLayout _results = new() { Spacing = 8, IsVisible = false };
    private readonly Grid _resultCards = LessonUi.Cards();
    private readonly Label _resultNote = LessonUi.Text("Enter the month's inputs, then select Calculate.");
    private readonly Label _spendingSummary = LessonUi.Text("");
    private readonly Label _savedNote = LessonUi.Text("");
    private bool _loaded;
    private bool _applying;
    private bool _dirty;
    private DateTime _loadedMonth;

    public IncomePage()
    {
        InitializeComponent();
        _mode.Items.Add("Monthly plan — start-of-month balance");
        _mode.Items.Add("From today's balance — current month only");
        _mode.SelectedIndex = 0;
        _mode.SelectedIndexChanged += (_, _) =>
        {
            _currentFields.IsVisible = _mode.SelectedIndex == 1;
            _balanceLabel.Text = _mode.SelectedIndex == 1 ? "Bank balance now (£)" : "Opening bank balance (£)";
            if (!_applying)
            {
                // Changing mode changes the meaning of this number. Require re-entry.
                _balance.Text = "";
                MarkDirty();
            }
        };
        foreach (var entry in new[] { _balance, _income, _received, _allowance, _savings })
            entry.TextChanged += (_, _) => { if (!_applying) MarkDirty(); };

        _currentFields.Children.Add(LessonUi.Text("Take-home income already received this month (£)"));
        _currentFields.Children.Add(_received);
        _currentFields.Children.Add(LessonUi.Text(
            "Before every calculation, refresh your bank balance and income received. " +
            "The balance must already include every bill marked paid and every recorded purchase."));
        var confirmationRow = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        confirmationRow.Add(_confirmed, 0, 0);
        confirmationRow.Add(LessonUi.Text("I have checked these figures against my account now."), 1, 0);
        _currentFields.Children.Add(confirmationRow);

        _settings.Children.Add(LessonUi.Text("Calculation mode")); _settings.Children.Add(_mode);
        var inputs = LessonUi.Cards();
        LessonUi.AddCard(inputs, LessonUi.Card(new VerticalStackLayout { Spacing = 8, Children = { _balanceLabel, _balance } }));
        LessonUi.AddCard(inputs, InputCard("Expected take-home income (£)", _income));
        LessonUi.AddCard(inputs, InputCard("Allowance budget (£)", _allowance));
        LessonUi.AddCard(inputs, InputCard("Savings target (£)", _savings));
        _settings.Children.Add(inputs);
        _settings.Children.Add(_currentFields);
        _settings.Children.Add(LessonUi.Text(
            "Monthly plan: use the balance at the start of the month, before that month's income and spending. " +
            "All bills and allowance spending must be recorded once. Values use a decimal point; zero is allowed."));
        _settings.Children.Add(LessonUi.Button("Calculate budget", () => LessonUi.RunAsync(this, CalculateAsync, _error), LessonUi.ButtonTone.Primary));
        _settings.Children.Add(LessonUi.Button("Allowance spending", () => LessonUi.RunAsync(this,
            () => Navigation.PushAsync(new AllowanceSpendingPage(_loadedMonth)), _error)));

        var body = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        body.Children.Add(LessonUi.Header("PayTracker / Budget", "Make a plan for your money",
            "Bring your income, allowance and savings goals into one monthly view."));
        body.Children.Add(LessonUi.MonthBar(this, _payments, LoadMonthAsync, _month, _error));
        body.Children.Add(LessonUi.Button("Refresh saved inputs", () => LessonUi.RunAsync(this,
            () => LoadMonthAsync(_payments.SelectedMonth, forceReload: true), _error)));
        body.Children.Add(_error); body.Children.Add(_savedNote);
        body.Children.Add(_settings); body.Children.Add(_spendingSummary);
        body.Children.Add(_resultNote); body.Children.Add(_results);
        body.Children.Add(LessonUi.Text(
            "These are estimates from your entries, not a live bank balance. A positive closing balance " +
            "does not guarantee cash is available before each bill's due date."));
        Content = LessonUi.Scroll(body);
    }

    private static Border InputCard(string title, View field) => LessonUi.Card(
        new VerticalStackLayout { Spacing = 8, Children = { LessonUi.Text(title), field } });

    private static Entry MoneyEntry(string placeholder) => new() { Placeholder = placeholder, Keyboard = Keyboard.Numeric };

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LessonUi.RunAsync(this, () => LoadMonthAsync(_payments.SelectedMonth), _error);
    }

    private Task LoadMonthAsync(DateTime requestedMonth) => LoadMonthAsync(requestedMonth, false);

    private async Task LoadMonthAsync(DateTime requestedMonth, bool forceReload)
    {
        var month = PaymentStore.MonthOf(requestedMonth);
        bool replaceInputs = !_loaded || month != _loadedMonth || forceReload;
        if (replaceInputs && _dirty)
        {
            bool discard = await DisplayAlertAsync("Unsaved budget inputs",
                "Discard your unsaved inputs and load this month?", "Discard", "Keep editing");
            if (!discard)
            {
                // Another tab may already have changed the shared selected month.
                await _payments.SelectMonthAsync(_loadedMonth);
                return;
            }
        }
        await _payments.SelectMonthAsync(month);
        var budget = replaceInputs ? await _budgets.GetBudgetAsync(month) : null;
        var expenses = await _budgets.GetExpensesAsync(month);
        if (replaceInputs)
        {
            _applying = true;
            try
            {
                _mode.SelectedIndex = budget?.UseCurrentBalance == true ? 1 : 0;
                _balance.Text = budget is null ? "" : InputText(budget.BalancePence);
                _income.Text = budget is null ? "" : InputText(budget.ExpectedIncomePence);
                _received.Text = budget is null ? "0.00" : InputText(budget.IncomeReceivedPence);
                _allowance.Text = budget is null ? "100.00" : InputText(budget.AllowancePence);
                _savings.Text = budget is null ? "0.00" : InputText(budget.SavingsTargetPence);
                _savedNote.Text = budget is null ? "No budget saved for this month yet." :
                    $"Inputs last saved: {budget.UpdatedUtc.ToLocalTime():dd MMM yyyy HH:mm}. Bank figures are manually entered.";
                _dirty = false;
            }
            finally { _applying = false; }
        }
        _loadedMonth = month; _loaded = true; _settings.IsEnabled = true;
        _month.Text = month.ToString("MMMM yyyy");
        _spendingSummary.Text = $"Allowance purchases: {expenses.Count}; recorded spending: " +
            LessonUi.Money(expenses.Sum(e => e.AmountPence / 100m));
        InvalidateResults("Select Calculate to use the latest bills, spending and inputs.");
    }

    private static string InputText(long pence) => (pence / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private void MarkDirty()
    {
        _dirty = true;
        InvalidateResults("Inputs changed. Select Calculate to save and update the results.");
    }

    private void InvalidateResults(string text)
    {
        _results.IsVisible = false;
        _resultNote.Text = text;
        _confirmed.IsChecked = false;
    }

    private static long ReadMoney(Entry entry, string label, bool allowNegative = false)
    {
        const NumberStyles style = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(entry.Text, style, CultureInfo.InvariantCulture, out decimal amount))
            throw new ArgumentException($"Enter {label} using a decimal point, for example 100.00.");
        try { return BudgetCalculator.ToPence(amount, allowNegative); }
        catch (ArgumentException)
        {
            throw new ArgumentException($"{label} must be within the supported range and have at most two decimal places. " +
                (allowNegative ? "A negative bank balance is allowed." : "Zero is allowed; negative amounts are not."));
        }
    }

    private async Task CalculateAsync()
    {
        if (!_loaded) throw new ArgumentException("Load a month first.");
        bool current = _mode.SelectedIndex == 1;
        if (current && !_confirmed.IsChecked)
            throw new ArgumentException("Refresh the bank balance and income received, then tick the confirmation before calculating.");
        var budget = new MonthlyBudgetRow
        {
            MonthKey = BudgetCalculator.MonthKey(_loadedMonth),
            UseCurrentBalance = current,
            BalancePence = ReadMoney(_balance, "the bank balance", allowNegative: true),
            ExpectedIncomePence = ReadMoney(_income, "expected take-home income"),
            IncomeReceivedPence = current ? ReadMoney(_received, "income already received") : 0,
            AllowancePence = ReadMoney(_allowance, "the allowance budget"),
            SavingsTargetPence = ReadMoney(_savings, "the savings target")
        };
        // Hide old results even if the refresh/save fails.
        _results.IsVisible = false;
        _resultNote.Text = "Calculation pending. Previous figures are no longer current.";
        BudgetResult result = await _budgets.SaveAndCalculateAsync(budget);
        _dirty = false;
        _confirmed.IsChecked = false;
        _savedNote.Text = $"Inputs saved: {budget.UpdatedUtc.ToLocalTime():dd MMM yyyy HH:mm}.";
        Render(result);
    }

    private void Render(BudgetResult r)
    {
        _results.Children.Clear();
        _resultCards.Children.Clear();
        _results.Children.Add(LessonUi.Heading("Your budget outlook"));
        _results.Children.Add(_resultCards);
        AddFigure("All bills scheduled for this month", r.ScheduledBills);
        AddFigure("Bills marked paid", r.PaidBills);
        AddFigure("Bills still unpaid", r.UnpaidBills);
        if (r.UseCurrentBalance)
            AddFigure("Recorded unpaid bills from earlier months", r.PriorMonthUnpaidBills);
        AddFigure("Allowance spent", r.AllowanceSpent);
        AddFigure("Allowance still reserved", r.AllowanceRemaining);
        AddFigure("Allowance overspend", r.AllowanceOverspend);
        if (r.UseCurrentBalance) AddFigure("Income still expected", r.IncomeStillExpected);
        AddFigure("Projected monthly surplus / shortfall", r.ProjectedMonthlySurplus);
        AddFigure("Projected closing bank balance", r.ProjectedClosingBalance);
        AddFigure("Savings target (a reservation, not a transfer)", r.SavingsTarget);
        AddFigure("Projected balance after reserving the savings target", r.BalanceAfterSavingsTarget);
        if (r.ProjectedClosingBalance < 0)
            _results.Children.Add(LessonUi.Text("Projected funding shortfall: " + LessonUi.Money(-r.ProjectedClosingBalance)));
        if (r.SavingsTargetShortfall > 0)
            _results.Children.Add(LessonUi.Text("Savings target cannot be fully reserved. Target shortfall: " + LessonUi.Money(r.SavingsTargetShortfall)));
        _resultNote.Text = $"Calculated {r.CalculatedUtc.ToLocalTime():dd MMM yyyy HH:mm}. " +
            (r.UseCurrentBalance ? "Forecast from the bank balance you entered now." : "Monthly plan from your opening balance.");
        _results.IsVisible = true;
    }

    private void AddFigure(string title, decimal amount) => LessonUi.AddCard(_resultCards,
        LessonUi.MetricCard(title, LessonUi.Metric(LessonUi.Money(amount)),
            amount < 0 ? Color.FromArgb("#B42335") : null));
}
