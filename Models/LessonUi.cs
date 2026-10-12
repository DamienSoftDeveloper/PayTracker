using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    internal enum ButtonTone { Primary, Secondary, Danger }
    public static readonly Color Accent = Color.FromArgb("#0F766E");
    public static readonly Color AccentDark = Color.FromArgb("#5EEAD4");
    public static readonly Color Ink = Color.FromArgb("#16263D");
    public static readonly Color InkDark = Color.FromArgb("#EDF4FC");
    public static readonly Color Muted = Color.FromArgb("#53657B");
    public static readonly Color MutedDark = Color.FromArgb("#B2C0D3");
    private sealed class OperationState { public bool Busy; public OperationState() { } }
    private static readonly ConditionalWeakTable<ContentPage, OperationState> States = new();
    public static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-GB"));
    public static Label Heading(string text)
    {
        var label = new Label { Text = text, FontSize = 20, FontFamily = "OpenSansSemibold", LineBreakMode = LineBreakMode.WordWrap };
        label.SetAppThemeColor(Label.TextColorProperty, Ink, InkDark);
        return label;
    }
    public static Label Text(string text)
    {
        var label = new Label { Text = text, FontSize = 13, LineHeight = 1.2, LineBreakMode = LineBreakMode.WordWrap };
        label.SetAppThemeColor(Label.TextColorProperty, Muted, MutedDark);
        return label;
    }
    public static Label Metric(string text)
    {
        var label = Heading(text); label.FontSize = 28;
        return label;
    }
    public static VerticalStackLayout Header(string section, string title, string description)
    {
        var eyebrow = Text(section.ToUpperInvariant()); eyebrow.FontSize = 11; eyebrow.CharacterSpacing = 2;
        eyebrow.SetAppThemeColor(Label.TextColorProperty, Accent, AccentDark);
        var heading = Heading(title); heading.FontSize = 30;
        return new VerticalStackLayout { Spacing = 6, Margin = new Thickness(0, 4, 0, 8),
            Children = { eyebrow, heading, Text(description) } };
    }
    public static Button Button(string text, Func<Task> action, ButtonTone tone = ButtonTone.Secondary)
    {
        var button = new Button { Text = text };
        StyleButton(button, tone);
        button.Clicked += async (_, _) => await action();
        return button;
    }
    public static void StyleButton(Button button, ButtonTone tone = ButtonTone.Secondary)
    {
        button.FontFamily = "OpenSansSemibold"; button.FontSize = 13;
        button.CornerRadius = 12; button.Padding = new Thickness(14, 10); button.MinimumHeightRequest = 46;
        if (tone == ButtonTone.Primary)
        {
            button.SetAppThemeColor(VisualElement.BackgroundColorProperty, Accent, AccentDark);
            button.SetAppThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, Colors.White, Color.FromArgb("#102A2B"));
        }
        else if (tone == ButtonTone.Danger)
        {
            button.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#FFF1F2"), Color.FromArgb("#402B37"));
            button.SetAppThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, Color.FromArgb("#B42335"), Color.FromArgb("#FDA4AF"));
        }
        else
        {
            button.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#EAF0F6"), Color.FromArgb("#29384D"));
            button.SetAppThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, Ink, InkDark);
        }
    }
    public static Border Card(View content)
    {
        var card = new Border { Padding = 18, Margin = new Thickness(0, 0, 0, 8), Content = content,
            StrokeShape = new RoundRectangle { CornerRadius = 18 }, StrokeThickness = 1 };
        card.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Color.FromArgb("#1A2638"));
        card.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Color.FromArgb("#DFE7EF")),
            new SolidColorBrush(Color.FromArgb("#34445A")));
        return card;
    }
    public static Border MetricCard(string title, Label value, Color? accent = null)
    {
        value.FontFamily = "OpenSansSemibold"; value.FontSize = 28;
        if (accent is not null) value.SetAppThemeColor(Label.TextColorProperty, accent, InkDark);
        var stripe = new BoxView { HeightRequest = 3, WidthRequest = 30, HorizontalOptions = LayoutOptions.Start,
            Color = accent ?? Accent, Margin = new Thickness(0, 0, 0, 4) };
        return Card(new VerticalStackLayout { Spacing = 8, Children = { stripe, Text(title), value } });
    }
    public static Grid Cards()
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        grid.SizeChanged += (_, _) => ArrangeCards(grid);
        return grid;
    }
    public static void AddCard(Grid grid, View card)
    {
        grid.Children.Add(card); ArrangeCards(grid);
    }
    public static void ArrangeCards(Grid grid)
    {
        var children = grid.Children.OfType<View>().ToArray();
        int columns = Math.Min(Math.Max(children.Length, 1), grid.Width >= 860 ? 4 : grid.Width >= 350 ? 2 : 1);
        if (grid.ColumnDefinitions.Count != columns)
        {
            grid.ColumnDefinitions.Clear();
            for (int i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        int rows = (children.Length + columns - 1) / columns;
        if (grid.RowDefinitions.Count != rows)
        {
            grid.RowDefinitions.Clear();
            for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (int i = 0; i < children.Length; i++)
        {
            Grid.SetColumn(children[i], i % columns); Grid.SetRow(children[i], i / columns);
        }
    }
    public static ScrollView Scroll(VerticalStackLayout body)
    {
        body.Padding = new Thickness(22, 22, 22, 32); body.Spacing = 16;
        body.HorizontalOptions = LayoutOptions.Center;
        var scroll = new ScrollView { Content = body };
        scroll.SizeChanged += (_, _) => body.WidthRequest = Math.Max(0, Math.Min(scroll.Width, 1120));
        return scroll;
    }
    public static Grid Constrain(View content)
    {
        content.HorizontalOptions = LayoutOptions.Center;
        var host = new Grid(); host.Children.Add(content);
        host.SizeChanged += (_, _) => content.WidthRequest = Math.Max(0, Math.Min(host.Width, 1120));
        return host;
    }
    public static Grid Actions(params View[] actions)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        for (int i = 0; i < actions.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.Add(actions[i], i, 0);
        }
        return grid;
    }
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
            Spacing = 10,
            Children =
            {
                caption,
                Actions(
                        Button("Previous", () => RunAsync(page,
                            () => selectMonth(store.SelectedMonth.AddMonths(-1)), error)),
                        Button("Current", () => RunAsync(page,
                            () => selectMonth(DateTime.Today), error)),
                        Button("Next", () => RunAsync(page,
                            () => selectMonth(store.SelectedMonth.AddMonths(1)), error)))
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
