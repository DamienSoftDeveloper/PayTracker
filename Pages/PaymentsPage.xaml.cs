using Microsoft.Maui.Controls;
using PayTracker.Models;
using PayTracker.Services;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace PayTracker.Pages;

public partial class PaymentsPage : ContentPage
{
    private static readonly CultureInfo Pounds =
        CultureInfo.GetCultureInfo("en-GB");

    private readonly PaymentStore _store = PaymentStore.Instance;

    private Payment? _editingPayment;
    private bool _recordingPaidDate;
    private bool _busy;
    private bool _loaded;

    public PaymentsPage()
    {
        InitializeComponent();

        PaymentsList.ItemsSource = _store.Payments;

        // Enable adding only after the initial database load succeeds.
        AddButton.IsEnabled = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded || _busy)
        {
            return;
        }

        await RunDatabaseOperationAsync(
            async () =>
            {
                await _store.LoadAsync();
                _loaded = true;
            },
            "Could not load payments");
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        if (_busy || !_loaded)
        {
            return;
        }

        OpenEditor(null, recordingPaidDate: false);
    }

    private void OnEditClicked(object sender, EventArgs e)
    {
        if (_busy || !_loaded)
        {
            return;
        }

        if (GetPayment(sender) is Payment payment)
        {
            OpenEditor(payment, recordingPaidDate: false);
        }
    }

    private void OnMarkPaidClicked(object sender, EventArgs e)
    {
        if (_busy || !_loaded)
        {
            return;
        }

        if (GetPayment(sender) is Payment payment)
        {
            OpenEditor(payment, recordingPaidDate: true);
        }
    }

    private void OpenEditor(
        Payment? payment,
        bool recordingPaidDate)
    {
        _editingPayment = payment;
        _recordingPaidDate = recordingPaidDate;

        EditorTitle.Text = recordingPaidDate
            ? $"Record payment: {payment!.Name}"
            : payment is null
                ? "Add payment"
                : "Edit payment";

        NameEntry.Text = payment?.Name ?? "";

        AmountEntry.Text = payment is null
            ? ""
            : payment.Amount.ToString("0.00", Pounds);

        DueDatePicker.Date = payment?.DueDate ?? DateTime.Today;

        PaidDatePicker.MaximumDate = DateTime.Today;
        PaidDatePicker.Date = payment?.PaidDate ?? DateTime.Today;

        DetailsFields.IsVisible = !recordingPaidDate;
        PaidFields.IsVisible = recordingPaidDate;

        ErrorLabel.IsVisible = false;

        AddButton.IsVisible = false;
        PaymentsList.IsVisible = false;
        EditorPanel.IsVisible = true;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_busy || !_loaded)
        {
            return;
        }

        Payment savedPayment;
        bool isNew;

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

            savedPayment = _editingPayment with
            {
                PaidDate = paidDate.Date
            };

            isNew = false;
        }
        else
        {
            string name = NameEntry.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Enter a payment name.");
                return;
            }

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

            // A generous limit that also keeps conversion to pence safe.
            if (amount > 999_999_999.99m)
            {
                ShowError("The maximum amount is £999,999,999.99.");
                return;
            }

            if (DueDatePicker.Date is not DateTime dueDate)
            {
                ShowError("Choose a due date.");
                return;
            }

            isNew = _editingPayment is null;

            savedPayment = (_editingPayment ?? new Payment()) with
            {
                Name = name,
                Amount = amount,
                DueDate = dueDate.Date
            };
        }

        await RunDatabaseOperationAsync(
            async () =>
            {
                if (isNew)
                {
                    await _store.AddAsync(savedPayment);
                }
                else
                {
                    await _store.UpdateAsync(savedPayment);
                }

                CloseEditor();
            },
            "Could not save payment");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_busy || !_loaded)
        {
            return;
        }

        if (GetPayment(sender) is not Payment payment)
        {
            return;
        }

        await RunDatabaseOperationAsync(
            async () =>
            {
                bool confirmed = await DisplayAlertAsync(
                    "Delete payment?",
                    $"Delete \"{payment.Name}\"?",
                    "Delete",
                    "Cancel");

                if (confirmed)
                {
                    await _store.DeleteAsync(payment);
                }
            },
            "Could not delete payment");
    }

    private async void OnMarkUnpaidClicked(object sender, EventArgs e)
    {
        if (_busy || !_loaded)
        {
            return;
        }

        if (GetPayment(sender) is not Payment payment)
        {
            return;
        }

        await RunDatabaseOperationAsync(
            async () =>
            {
                bool confirmed = await DisplayAlertAsync(
                    "Mark unpaid?",
                    $"Clear the paid date for \"{payment.Name}\"?",
                    "Mark unpaid",
                    "Cancel");

                if (confirmed)
                {
                    await _store.UpdateAsync(payment with
                    {
                        PaidDate = null
                    });
                }
            },
            "Could not change paid status");
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        if (!_busy)
        {
            CloseEditor();
        }
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

    private async Task RunDatabaseOperationAsync(
        Func<Task> operation,
        string errorTitle)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);

            // Preserve the form so the user can retry saving.
            if (EditorPanel.IsVisible)
            {
                ShowError(
                    "The change could not be saved. " +
                    "Your entries are still here; try again.");
            }

            await DisplayAlertAsync(
                errorTitle,
                "PayTrack could not complete the database operation. " +
                "Please try again. Technical details are in " +
                "Visual Studio's Debug Output.",
                "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;

        // Prevent repeated taps while an operation is running.
        if (Content is not null)
        {
            Content.IsEnabled = !busy;
        }

        AddButton.IsEnabled = !busy && _loaded;
    }

    private static Payment? GetPayment(object sender)
    {
        return sender is Button button
            ? button.CommandParameter as Payment
            : null;
    }
}