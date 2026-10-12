using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public partial class DashboardPage : ContentPage
{
    private static readonly CultureInfo Pounds =
        CultureInfo.GetCultureInfo("en-GB");

    private readonly PaymentStore _store = PaymentStore.Instance;

    private bool _refreshing;
    private bool _hasLoaded;

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await RefreshDashboardAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await RefreshDashboardAsync();
    }

    private async Task RefreshDashboardAsync()
    {
        if (_refreshing)
        {
            return;
        }

        SetRefreshing(true);
        ErrorLabel.IsVisible = false;

        try
        {
            // Load from SQLite, including when Dashboard is the first tab.
            await _store.LoadAsync();

            // Use one snapshot for all calculations.
            Payment[] payments = _store.Payments.ToArray();

            DateTime today = DateTime.Today;

            Payment[] paidPayments = payments
                .Where(payment => payment.IsPaid)
                .ToArray();

            Payment[] unpaidPayments = payments
                .Where(payment => !payment.IsPaid)
                .ToArray();

            Payment[] overduePayments = unpaidPayments
                .Where(payment => payment.DueDate.Date < today)
                .ToArray();

            Payment[] nextPayments = unpaidPayments
                .OrderBy(payment => payment.DueDate)
                .ThenBy(payment => payment.Name)
                .Take(5)
                .ToArray();

            decimal total = payments.Sum(payment => payment.Amount);
            decimal paid = paidPayments.Sum(payment => payment.Amount);
            decimal remaining = unpaidPayments.Sum(payment => payment.Amount);
            decimal overdue = overduePayments.Sum(payment => payment.Amount);

            TotalLabel.Text = FormatMoney(total);
            PaidLabel.Text = FormatMoney(paid);
            RemainingLabel.Text = FormatMoney(remaining);
            OverdueLabel.Text = FormatMoney(overdue);

            PaymentCountLabel.Text = CountText(
                payments.Length, "payment");

            PaidCountLabel.Text = CountText(
                paidPayments.Length, "paid payment");

            UnpaidCountLabel.Text = CountText(
                unpaidPayments.Length, "unpaid payment");

            OverdueCountLabel.Text = CountText(
                overduePayments.Length, "overdue payment");

            BindableLayout.SetItemsSource(
                UpcomingPaymentsLayout,
                nextPayments);

            EmptyPaymentsLabel.IsVisible = nextPayments.Length == 0;

            UpdatedLabel.Text =
                $"Last refreshed: {DateTime.Now:dd MMM yyyy HH:mm}";

            _hasLoaded = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);

            ErrorLabel.Text = _hasLoaded
                ? "Could not refresh. The figures below are from the " +
                  "last successful refresh. Tap Refresh dashboard to retry."
                : "Could not load payments. Tap Refresh dashboard to retry.";

            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SetRefreshing(false);
        }
    }

    private void SetRefreshing(bool refreshing)
    {
        _refreshing = refreshing;

        RefreshButton.IsEnabled = !refreshing;
        LoadingIndicator.IsVisible = refreshing;
        LoadingIndicator.IsRunning = refreshing;
    }

    private static string FormatMoney(decimal amount)
    {
        return amount.ToString("C2", Pounds);
    }

    private static string CountText(int count, string singular)
    {
        return count == 1
            ? $"1 {singular}"
            : $"{count} {singular}s";
    }
}