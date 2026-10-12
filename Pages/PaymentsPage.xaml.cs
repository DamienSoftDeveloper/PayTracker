
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using PayTracker.Models;
using PayTracker.Services;

namespace PayTracker.Pages;

public partial class PaymentsPage : ContentPage
{
    private static readonly CultureInfo Pounds = CultureInfo.GetCultureInfo("en-GB");

    private readonly PaymentStore _store = PaymentStore.Instance;

    private Payment? _editingPayment;
    private bool _recordingPaidDate;

    public PaymentsPage()
    {
        InitializeComponent();
        PaymentsList.ItemsSource = _store.Payments;
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        OpenEditor(null, recordingPaidDate: false);
    }

    private void OnEditClicked(object sender, EventArgs e)
    {
        if (GetPayment(sender) is Payment payment)
        {
            OpenEditor(payment, recordingPaidDate: false);
        }
    }

    private void OnMarkPaidClicked(object sender, EventArgs e)
    {
        if (GetPayment(sender) is Payment payment)
        {
            OpenEditor(payment, recordingPaidDate: true);
        }
    }

    private void OpenEditor(Payment? payment, bool recordingPaidDate)
    {
        _editingPayment = payment;
        _recordingPaidDate = recordingPaidDate;

        EditorTitle.Text = recordingPaidDate ? $"Record payment: {payment!.Name}"   : payment is null   ? "Add payment"  : "Edit payment";

        NameEntry.Text = payment?.Name ?? "";
        AmountEntry.Text = payment is null  ? "" : payment.Amount.ToString("0.00", Pounds);

        DueDatePicker.Date = payment?.DueDate ?? DateTime.Today;

        // Assign the maximum first, then the selected date.
        PaidDatePicker.MaximumDate = DateTime.Today;
        PaidDatePicker.Date = payment?.PaidDate ?? DateTime.Today;

        DetailsFields.IsVisible = !recordingPaidDate;
        PaidFields.IsVisible = recordingPaidDate;
        ErrorLabel.IsVisible = false;

        AddButton.IsVisible = false;
        PaymentsList.IsVisible = false;
        EditorPanel.IsVisible = true;
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        if (_recordingPaidDate)
        {
            if (_editingPayment is null)
            {
                ShowError("Select a payment first.");
                return;
            }

            if (PaidDatePicker.Date is not DateTime paidDate)
            {
                ShowError("Choose the date you paid.");
                return;
            }

            if (paidDate.Date > DateTime.Today)
            {
                ShowError("The paid date cannot be in the future.");
                return;
            }

            _store.Update(_editingPayment with
            {
                PaidDate = paidDate.Date
            });

            CloseEditor();
            return;
        }

        string name = NameEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Enter a payment name.");
            return;
        }

        // Allow a decimal point, but reject commas and currency symbols.
        // This prevents a mistyped comma from changing the amount.
        const NumberStyles amountStyle =
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

        if (!decimal.TryParse(
                AmountEntry.Text,
                amountStyle,
                CultureInfo.InvariantCulture,
                out decimal amount)
            || amount <= 0)
        {
            ShowError(
                "Enter an amount greater than zero, such as 12.99. " +
                "Use a decimal point and omit the £ symbol.");
            return;
        }

        if (decimal.Round(amount, 2) != amount)
        {
            ShowError("Use no more than two decimal places.");
            return;
        }

        if (DueDatePicker.Date is not DateTime dueDate)
        {
            ShowError("Choose a due date.");
            return;
        }

        Payment savedPayment = (_editingPayment ?? new Payment()) with
        {
            Name = name,
            Amount = amount,
            DueDate = dueDate.Date
        };

        if (_editingPayment is null)
        {
            _store.Add(savedPayment);
        }
        else
        {
            _store.Update(savedPayment);
        }

        CloseEditor();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (GetPayment(sender) is not Payment payment)
        {
            return;
        }

        bool confirmed = await DisplayAlertAsync(
            "Delete payment?",
            $"Delete \"{payment.Name}\"?",
            "Delete",
            "Cancel");

        if (confirmed)
        {
            _store.Delete(payment);
        }
    }

    private async void OnMarkUnpaidClicked(object sender, EventArgs e)
    {
        if (GetPayment(sender) is not Payment payment)
        {
            return;
        }

        bool confirmed = await DisplayAlertAsync(
            "Mark unpaid?",
            $"Clear the paid date for \"{payment.Name}\"?",
            "Mark unpaid",
            "Cancel");

        if (confirmed)
        {
            _store.Update(payment with
            {
                PaidDate = null
            });
        }
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        CloseEditor();
    }

    private void CloseEditor()
    {
        EditorPanel.IsVisible = false;
        AddButton.IsVisible = true;
        PaymentsList.IsVisible = true;

        _editingPayment = null;
        _recordingPaidDate = false;
        ErrorLabel.IsVisible = false;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private static Payment? GetPayment(object sender)
    {
        return sender is Button button
            ? button.CommandParameter as Payment
            : null;
    }
}