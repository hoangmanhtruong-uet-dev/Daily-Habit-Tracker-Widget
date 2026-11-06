using System.Globalization;
using HabitGrid.Core.Domain;
using HabitGrid.Core.Domain.Insights;
using HabitGrid.Presentation.Services;
using HabitGrid.Presentation.ViewModels;
using HabitGrid.WinUI.Plus;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace HabitGrid.WinUI.Pages;

/// <summary>
/// Trang Insights (SPEC-V2-07): ô số liệu, gợi ý tự động, xu hướng tuần, theo thứ, theo giờ, mood, từng thói quen.
/// Free xem 30 ngày; 90 ngày / toàn bộ / xuất CSV thuộc Plus (bấm vào thì mở paywall).
/// </summary>
public sealed partial class InsightsPage : Page
{
    private readonly StackPanel _root = new() { Spacing = 16, Padding = new Thickness(36, 28, 36, 32), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Stretch };

    public InsightsPage()
    {
        ViewModel = App.Host.Get<InsightsViewModel>();
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Disabled;
        Scroll.Content = _root;
    }

    public InsightsViewModel ViewModel { get; }

    /// <summary>Hoàn tất khi đã nạp và dựng xong (smoke test chờ).</summary>
    public Task Ready { get; private set; } = Task.CompletedTask;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Ready = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await ViewModel.LoadAsync();
            Build();
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Mở trang Insights lỗi", ex);
        }
    }

    private void Build()
    {
        _root.Children.Clear();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = Ui.T("insights.title"), Style = Res("TitleDisplayTextStyle") });
        var export = new Button { Style = Res("GhostButtonStyle"), Content = Ui.T("insights.export") + (ViewModel.CanExport ? string.Empty : "  🔒") };
        AutomationProperties.SetName(export, Ui.T("insights.export"));
        export.Click += async (_, _) => await ExportAsync();
        var wrapped = new Button { Style = Res("GhostButtonStyle"), Content = "✨ " + Ui.T("wrapped.button") };
        AutomationProperties.SetName(wrapped, Ui.T("wrapped.title"));
        wrapped.Click += async (_, _) => await HabitGrid.WinUI.Moments.WrappedDialog.ShowAsync(XamlRoot);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(wrapped);
        actions.Children.Add(export);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        _root.Children.Add(header);
        _root.Children.Add(BuildRangeChips());

        if (ViewModel.Report is not { } report || report.IsEmpty)
        {
            _root.Children.Add(new TextBlock { Text = Ui.T("insights.empty"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
            return;
        }

        _root.Children.Add(BuildTiles(report));
        _root.Children.Add(BuildAutoInsights());

        var charts = new Grid { ColumnSpacing = 12 };
        charts.ColumnDefinitions.Add(new ColumnDefinition());
        charts.ColumnDefinitions.Add(new ColumnDefinition());
        var weekdays = Card(Ui.T("insights.weekdays"), Bars(
            report.Weekdays.Select(w => (Ui.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(w.Day), w.Rate)).ToList(), 120, showValues: true));
        var hours = Card(Ui.T("insights.hours"), HourBars(report.Hours));
        charts.Children.Add(weekdays);
        Grid.SetColumn(hours, 1);
        charts.Children.Add(hours);

        _root.Children.Add(Card(Ui.T("insights.trend"), Bars(
            report.Weeks.TakeLast(26).Select(w => (w.WeekStart.ToString("dd/MM", Ui.CurrentCulture), w.Rate)).ToList(), 120, showValues: false)));
        _root.Children.Add(charts);
        _root.Children.Add(BuildMood(report.Mood));
        _root.Children.Add(BuildHabits(report));
        _root.Children.Add(BuildAdvanced());
    }

    private UIElement BuildRangeChips()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        foreach (var (range, key) in new[] { (InsightsRange.Days30, "insights.range.30"), (InsightsRange.Days90, "insights.range.90"), (InsightsRange.All, "insights.range.all") })
        {
            var locked = !ViewModel.CanUse(range);
            var chip = new ToggleButton
            {
                Style = Res("ChipStyle"),
                Content = Ui.T(key) + (locked ? "  🔒" : string.Empty),
                IsChecked = ViewModel.Range == range
            };
            AutomationProperties.SetName(chip, Ui.T(key));
            chip.Click += async (_, _) =>
            {
                if (!await ViewModel.SetRangeAsync(range))
                {
                    // Khoảng của Plus: người dùng vừa chọn nó nên mới mở paywall.
                    if (await PaywallDialog.ShowAsync(XamlRoot, PaywallSource.Menu))
                    {
                        await ViewModel.SetRangeAsync(range);
                    }
                }

                Build();
            };
            row.Children.Add(chip);
        }

        return row;
    }

    private UIElement BuildTiles(InsightsReport report)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        var tiles = new (string Label, string Value)[]
        {
            (Ui.T("insights.rate"), report.OverallRate is { } r ? $"{Math.Round(r * 100)}%" : "—"),
            (Ui.T("insights.perfect"), report.PerfectDays.ToString(Ui.CurrentCulture)),
            (Ui.T("insights.checkins"), report.CheckIns.ToString(Ui.CurrentCulture)),
            (Ui.T("insights.beststreak"), report.BestStreak.ToString(Ui.CurrentCulture))
        };

        for (var i = 0; i < tiles.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var stack = new StackPanel { Spacing = 2 };
            stack.Children.Add(new TextBlock { Text = tiles[i].Label, Style = Res("BodySecondaryTextStyle") });
            stack.Children.Add(new TextBlock { Text = tiles[i].Value, Style = Res("TitleDisplayTextStyle") });
            var border = new Border { Style = Res("CardBorder"), Child = stack };
            AutomationProperties.SetName(border, $"{tiles[i].Label}: {tiles[i].Value}");
            Grid.SetColumn(border, i);
            grid.Children.Add(border);
        }

        return grid;
    }

    private UIElement BuildAutoInsights()
    {
        var list = new StackPanel { Spacing = 8 };

        if (ViewModel.Insights.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = Ui.T("insights.auto.none"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
        }

        foreach (var insight in ViewModel.Insights)
        {
            list.Children.Add(new TextBlock { Text = "💡  " + Describe(insight), TextWrapping = TextWrapping.Wrap });
        }

        return Card(Ui.T("insights.auto"), list);
    }

    /// <summary>Dịch insight: khóa + tham số, thay {tên} bằng giá trị (thứ trong tuần dịch theo ngôn ngữ).</summary>
    public static string Describe(Insight insight)
    {
        var key = insight.Key == "slipping" && string.IsNullOrEmpty(insight.Params.GetValueOrDefault("suggested")) ? "insight.slipping.plain" : "insight." + insight.Key;
        var text = Ui.T(key);

        foreach (var (name, value) in insight.Params)
        {
            var shown = name == "weekday" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                ? Ui.CurrentCulture.DateTimeFormat.GetDayName((DayOfWeek)day)
                : value;
            text = text.Replace("{" + name + "}", shown, StringComparison.Ordinal);
        }

        return text;
    }

    private UIElement BuildMood(MoodSummary mood)
    {
        var stack = new StackPanel { Spacing = 4 };

        if (mood.Samples == 0)
        {
            stack.Children.Add(new TextBlock { Text = Ui.T("insights.mood.none"), Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            stack.Children.Add(new TextBlock { Text = Ui.Loc.Format("insights.mood.avg", Format1(mood.Average)), FontSize = 18 });

            if (mood.DoneDaysAverage is not null && mood.OtherDaysAverage is not null)
            {
                stack.Children.Add(new TextBlock { Text = Ui.Loc.Format("insights.mood.compare", Format1(mood.DoneDaysAverage), Format1(mood.OtherDaysAverage)), Style = Res("BodySecondaryTextStyle") });
            }
        }

        return Card(Ui.T("insights.mood"), stack);
    }

    private UIElement BuildHabits(InsightsReport report)
    {
        var list = new StackPanel { Spacing = 10 };

        foreach (var row in report.Habits)
        {
            var line = new Grid { ColumnSpacing = 12 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

            line.Children.Add(new TextBlock { Text = Ui.IconText(row.Icon), FontSize = 24, VerticalAlignment = VerticalAlignment.Center });

            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = row.Name, Style = Res("CardTitleTextStyle"), TextTrimming = TextTrimming.CharacterEllipsis });
            var detail = Ui.HabitSubText(row.CurrentStreak, row.StreakUnit, row.Rate is { } rr ? (int)Math.Round(rr * 100) : null);

            if (row.Kind is HabitKind.Count or HabitKind.Duration && row.AveragePerActiveDay is { } avg)
            {
                detail += " · " + Ui.Loc.Format("insights.row.total", row.TotalValue, row.Unit ?? string.Empty, Math.Round(avg, 1), row.BestValue);
            }

            text.Children.Add(new TextBlock { Text = detail, Style = Res("BodySecondaryTextStyle"), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(text, 1);
            line.Children.Add(text);

            var track = new Grid { Height = 10, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center, Background = (Brush)Application.Current.Resources["HeatEmptyBrush"] };
            var fill = new Border { CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Background = Ui.GradientBrush(HabitGradient.From(row.ColorHue)) };
            track.Children.Add(fill);
            track.SizeChanged += (_, e) => fill.Width = e.NewSize.Width * (row.Rate ?? 0);
            AutomationProperties.SetName(track, Ui.Loc.Format("insights.row.rate", row.Rate is { } r ? Math.Round(r * 100) : 0));
            Grid.SetColumn(track, 2);
            line.Children.Add(track);

            list.Children.Add(line);
        }

        return Card(Ui.T("insights.habits"), list);
    }

    private async Task ExportAsync()
    {
        if (!ViewModel.CanExport)
        {
            if (!await PaywallDialog.ShowAsync(XamlRoot, PaywallSource.Menu))
            {
                return;
            }

            Build();
        }

        try
        {
            var csv = await ViewModel.ExportCsvAsync();

            if (csv is null)
            {
                return;
            }

            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HabitGrid");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"habitgrid-{DateTime.Now:yyyyMMdd-HHmm}.csv");
            await File.WriteAllTextAsync(path, csv, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var open = new Button { Content = Ui.T("settings.data.openFolder"), Style = Res("GhostButtonStyle") };
            open.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            _root.Children.Insert(1, new InfoBar { IsOpen = true, Severity = InfoBarSeverity.Success, Message = Ui.Loc.Format("insights.export.done", path), ActionButton = open });
        }
        catch (Exception ex)
        {
            App.Host.Get<IAppLogger>().Error("Xuất CSV lỗi", ex);
            _root.Children.Insert(1, new InfoBar { IsOpen = true, Severity = InfoBarSeverity.Error, Message = Ui.T("insights.export.failed") });
        }
    }

    private static Border Card(string title, UIElement content)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), Style = Res("SectionHeader"), Margin = new Thickness(0) });
        stack.Children.Add(content);
        return new Border { Style = Res("CardBorder"), Child = stack };
    }

    /// <summary>Biểu đồ cột đơn giản (tỷ lệ 0–1), cột null hiện mờ. Mỗi cột có nhãn đọc được cho trình đọc màn hình.</summary>
    private static Grid Bars(IReadOnlyList<(string Label, double? Value)> values, double height, bool showValues)
    {
        var grid = new Grid { Height = height + 40, ColumnSpacing = values.Count > 14 ? 3 : 8 };
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < values.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var (label, value) = values[i];
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 2 };

            if (showValues)
            {
                stack.Children.Add(new TextBlock { Text = value is { } v ? $"{Math.Round(v * 100)}" : "–", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"] });
            }

            stack.Children.Add(new Border
            {
                Height = Math.Max(3, (value ?? 0) * height),
                CornerRadius = new CornerRadius(4),
                Background = value is null ? (Brush)Application.Current.Resources["HeatEmptyBrush"] : (Brush)Application.Current.Resources["AccentGradientBrush"]
            });
            AutomationProperties.SetName(stack, value is { } pv ? $"{label}: {Math.Round(pv * 100)}%" : label);
            Grid.SetColumn(stack, i);
            grid.Children.Add(stack);

            // Nhiều cột thì chỉ ghi nhãn cách quãng để không chồng chữ.
            if (values.Count <= 14 || i % 4 == 0)
            {
                var caption = new TextBlock { Text = label, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.Resources["TextMutedBrush"] };
                Grid.SetRow(caption, 1);
                Grid.SetColumn(caption, i);
                grid.Children.Add(caption);
            }
        }

        return grid;
    }

    private static Grid HourBars(IReadOnlyList<int> hours)
    {
        var max = Math.Max(1, hours.Max());
        return Bars(hours.Select((count, hour) => ($"{hour}h", count == 0 ? (double?)0 : count / (double)max)).ToList(), 120, showValues: false);
    }

    private static string Format1(double? value) => value is { } v ? v.ToString("0.0", Ui.CurrentCulture) : "—";

    private static Style Res(string key) => (Style)Application.Current.Resources[key];
}
