using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PayTracker.Models;

namespace PayTracker.Pages;

public enum StatisticsChartKind { RecordedOutflow, ProjectedSurplus }

public sealed class StatisticsChart : IDrawable
{
    public IReadOnlyList<MonthStatistics> Months { get; set; } = Array.Empty<MonthStatistics>();
    public StatisticsChartKind Kind { get; init; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        try
        {
            bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            Color textColor = dark ? LessonUi.InkDark : LessonUi.Muted;
            canvas.FillColor = dark ? Color.FromArgb("#1A2638") : Colors.White;
            canvas.FillRectangle(dirtyRect);
            if (Months.Count == 0 || dirtyRect.Width < 100 || dirtyRect.Height < 100) return;
            const float left = 55, right = 10, top = 24, bottom = 48;
            float width = dirtyRect.Width - left - right;
            float height = dirtyRect.Height - top - bottom;
            decimal[] values = Months.Select(m => Kind == StatisticsChartKind.RecordedOutflow
                ? m.RecordedOutflow : m.ProjectedMonthlySurplus ?? 0).ToArray();
            decimal maximum = Math.Max(values.Max(), 0);
            decimal minimum = Math.Min(values.Min(), 0);
            if (maximum == minimum) maximum = minimum + 1;
            float Y(decimal value) => top + (float)((maximum - value) / (maximum - minimum)) * height;
            float zero = Y(0);
            canvas.FontColor = textColor;
            canvas.FontSize = 11;
            canvas.StrokeColor = dark ? Color.FromArgb("#34445A") : Color.FromArgb("#DFE7EF");
            canvas.StrokeSize = 1;
            foreach (decimal tick in new[] { maximum, 0, minimum }.Distinct())
            {
                float y = Y(tick);
                canvas.DrawLine(left, y, left + width, y);
                canvas.DrawString(AxisMoney(tick), 0, y - 8, left - 5, 16,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }
            float cell = width / Months.Count;
            float bar = Math.Min(32, cell * 0.65f);
            for (int i = 0; i < Months.Count; i++)
            {
                var month = Months[i];
                float x = left + cell * i + (cell - bar) / 2;
                bool missing = Kind == StatisticsChartKind.RecordedOutflow
                    ? !month.HasAnyData : month.ProjectedMonthlySurplus is null;
                if (missing)
                {
                    canvas.DrawString("—", x - 4, zero - 20, bar + 8, 18,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else if (Kind == StatisticsChartKind.RecordedOutflow)
                {
                    float paidY = Y(month.BillsPaidByDate);
                    float totalY = Y(month.RecordedOutflow);
                    canvas.FillColor = dark ? Color.FromArgb("#93B4FF") : Color.FromArgb("#4767B1");
                    canvas.FillRectangle(x, paidY, bar, Math.Max(0, zero - paidY));
                    canvas.FillColor = dark ? Color.FromArgb("#C4B5FD") : Color.FromArgb("#8572BC");
                    canvas.FillRectangle(x, totalY, bar, Math.Max(0, paidY - totalY));
                    if (month.RecordedOutflow == 0) ZeroMarker(canvas, x, zero, bar);
                }
                else
                {
                    decimal value = month.ProjectedMonthlySurplus!.Value;
                    float y = Y(value);
                    canvas.FillColor = value < 0 ? (dark ? Color.FromArgb("#FDA4AF") : Color.FromArgb("#B42335")) :
                        (dark ? LessonUi.AccentDark : LessonUi.Accent);
                    canvas.FillRectangle(x, Math.Min(y, zero), bar, Math.Abs(y - zero));
                    if (value == 0) ZeroMarker(canvas, x, zero, bar);
                }
                canvas.FontColor = textColor;
                canvas.DrawString(month.Month.ToString("MMM", CultureInfo.GetCultureInfo("en-GB")),
                    left + cell * i, top + height + 8, cell, 16,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                canvas.DrawString(month.Month.ToString("yyyy"),
                    left + cell * i, top + height + 24, cell, 16,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
        finally { canvas.RestoreState(); }
    }

    private static void ZeroMarker(ICanvas canvas, float x, float y, float width)
    {
        canvas.StrokeColor = Colors.DimGray;
        canvas.StrokeSize = 3;
        canvas.DrawLine(x, y, x + width, y);
        canvas.StrokeSize = 1;
    }

    private static string AxisMoney(decimal value)
    {
        decimal absolute = Math.Abs(value);
        string sign = value < 0 ? "−" : "";
        string number = absolute >= 1_000_000 ? $"{absolute / 1_000_000m:0.#}m" :
            absolute >= 1000 ? $"{absolute / 1000m:0.#}k" : $"{absolute:0.#}";
        return $"{sign}£{number}";
    }
}
