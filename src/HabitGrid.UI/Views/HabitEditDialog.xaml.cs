using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HabitGrid.Core.Models;
using HabitGrid.UI.Controls;
using HabitGrid.UI.Services;

namespace HabitGrid.UI.Views;

/// <summary>
/// Hộp thoại tạo / chỉnh sửa thói quen.
/// </summary>
public partial class HabitEditDialog : Window
{
    private readonly bool _isPro;
    private string _paletteId = "default";

    public HabitEditDialog(Habit? existing = null, bool isPro = false)
    {
        InitializeComponent();

        _isPro = isPro;

        if (existing is not null)
        {
            Load(existing);
            Title = "Sửa thói quen";
        }
        else
        {
            Title = "Thêm thói quen";
            NameBox.Focus();
        }

        HighlightPalette(_paletteId);
    }

    /// <summary>Kết quả trả về khi người dùng bấm Lưu.</summary>
    public HabitEditResult? Result { get; private set; }

    private void Load(Habit habit)
    {
        NameBox.Text = habit.Name;
        IconPicker.SelectedIconKey = habit.Icon;

        if (habit.Kind == HabitKind.Count)
        {
            CountOption.IsChecked = true;
            TargetBox.Text = habit.Target.ToString();
        }
        else
        {
            BooleanOption.IsChecked = true;
        }

        SetScheduleMask(habit.ScheduleMask);
        _paletteId = habit.PaletteId;
        HighlightPalette(_paletteId);

        if (habit.ReminderTime is not null)
        {
            ReminderCheck.IsChecked = true;
            ReminderTime.SelectedTime = habit.ReminderTime;
        }

        UpdateTargetVisibility();
    }

    private void OnKindChanged(object sender, RoutedEventArgs e) => UpdateTargetVisibility();

    private void UpdateTargetVisibility()
    {
        TargetPanel.Visibility = CountOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnScheduleChanged(object sender, RoutedEventArgs e) { }

    private void OnPaletteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string paletteId })
        {
            if (!_isPro && paletteId != "default")
            {
                MessageBox.Show(
                    "Bảng màu tùy chỉnh là tính năng Pro.\n\nNâng cấp để chọn màu riêng cho thói quen.",
                    "HabitGrid Pro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            _paletteId = paletteId;
            HighlightPalette(_paletteId);
        }
    }

    private void HighlightPalette(string paletteId)
    {
        var buttons = new[]
        {
            PaletteDefault, PaletteBlue, PalettePurple, PaletteOrange, PalettePink, PaletteGray
        };

        foreach (var button in buttons)
        {
            var isSelected = string.Equals(button.Tag as string, paletteId, StringComparison.OrdinalIgnoreCase);
            button.BorderThickness = new Thickness(isSelected ? 2 : 0);
            button.BorderBrush = System.Windows.Media.Brushes.White;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();

        if (name.Length == 0)
        {
            MessageBox.Show("Vui lòng nhập tên thói quen.", "HabitGrid", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (name.Length > 40)
        {
            name = name[..40];
        }

        var target = 1;

        if (CountOption.IsChecked == true && !int.TryParse(TargetBox.Text, out target))
        {
            target = 1;
        }

        Result = new HabitEditResult
        {
            Name = name,
            IconKey = IconPicker.SelectedIconKey,
            Kind = CountOption.IsChecked == true ? HabitKind.Count : HabitKind.Boolean,
            Target = Math.Max(1, target),
            ScheduleMask = GetScheduleMask(),
            PaletteId = _paletteId,
            ReminderEnabled = ReminderCheck.IsChecked == true,
            ReminderTime = ReminderTime.SelectedTime
        };

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private DayOfWeekMask GetScheduleMask()
    {
        var mask = DayOfWeekMask.None;

        foreach (var element in SchedulePanel.Children.OfType<ToggleButton>())
        {
            if (element.IsChecked == true && element.Tag is string tag && int.TryParse(tag, out var value))
            {
                mask |= (DayOfWeekMask)value;
            }
        }

        return mask == DayOfWeekMask.None ? DayOfWeekMask.AllWeek : mask;
    }

    private void SetScheduleMask(DayOfWeekMask mask)
    {
        foreach (var element in SchedulePanel.Children.OfType<ToggleButton>())
        {
            if (element.Tag is string tag && int.TryParse(tag, out var value))
            {
                element.IsChecked = (mask & (DayOfWeekMask)value) == (DayOfWeekMask)value;
            }
        }
    }
}
