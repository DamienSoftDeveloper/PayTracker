using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using PayTracker.Services;

namespace PayTracker.Pages;

internal static class LessonUi
{
    private sealed class OperationState { public bool Busy; public OperationState() { } }
    private static readonly ConditionalWeakTable<ContentPage, OperationState> States = new();
    public static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-GB"));
    public static Label Heading(string text) => new() { Text = text, FontSize = 22, FontAttributes = FontAttributes.Bold };
    public static Label Text(string text) => new() { Text = text, FontSize = 14 };
    public static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => await action();
        return button;
    }
    public static Border Card(View content) => new()
    {
        Padding = 14, Margin = new Thickness(0, 0, 0, 10), Content = content,
        Stroke = new SolidColorBrush(Colors.SlateGray),
        StrokeShape = new RoundRectangle { CornerRadius = 12 }
    };
    public static bool TryAmount(string? text, out decimal amount)
    {
        const NumberStyles style = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(text, style, CultureInfo.InvariantCulture, out amount)) return false;
        return amount > 0 && amount <= 999_999_999.99m && decimal.Round(amount, 2) == amount;
    }
    public static string AmountEntryText(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static VerticalStackLayout MonthBar(ContentPage page, PaymentStore store,
        Func<DateTime, Task> selectMonth, Label caption, Label error)
    {
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                caption,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        Button("Previous", () => RunAsync(page,
                            () => selectMonth(store.SelectedMonth.AddMonths(-1)), error)),
                        Button("This month", () => RunAsync(page,
                            () => selectMonth(DateTime.Today), error)),
                        Button("Next", () => RunAsync(page,
                            () => selectMonth(store.SelectedMonth.AddMonths(1)), error))
                    }
                }
            }
        };
    }

    public static async Task RunAsync(ContentPage page, Func<Task> action, Label error)
    {
        var state = States.GetOrCreateValue(page);
        if (state.Busy) return;
        state.Busy = true;
        if (page.Content is not null) page.Content.IsEnabled = false;
        error.IsVisible = false;
        try { await action(); }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            error.Text = ex is ArgumentException ? ex.Message :
                "Could not complete the operation. Any displayed figures may be out of date. " +
                "Try Refresh, or retry saving. Details are in Visual Studio's Debug Output.";
            error.IsVisible = true;
        }
        finally
        {
            state.Busy = false;
            if (page.Content is not null) page.Content.IsEnabled = true;
        }
    }
}
