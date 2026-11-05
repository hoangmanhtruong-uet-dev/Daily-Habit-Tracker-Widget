using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Ô chọn giờ đơn giản: nhập trực tiếp theo định dạng HH:mm kèm nút tăng/giảm 30 phút.
/// </summary>
public partial class TimeField : UserControl
{
    private static readonly DependencyProperty SelectedTimeProperty = DependencyProperty.Register(
        nameof(SelectedTime),
        typeof(TimeOnly?),
        typeof(TimeField),
        new PropertyMetadata(null, OnSelectedTimeChanged));

    public TimeField()
    {
        InitializeComponent();

        TimeText.Text = new TimeOnly(21, 0).ToString("HH\\:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Giờ đang chọn.</summary>
    public TimeOnly? SelectedTime
    {
        get => (TimeOnly?)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (TimeField)d;

        if (e.NewValue is TimeOnly time)
        {
            field.TimeText.Text = time.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        }
    }

    private void OnTimeTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = TimeText.Text.Trim();

        if (TimeOnly.TryParseExact(text, "HH\\:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            SelectedTime = parsed;
            TimeText.BorderBrush = System.Windows.Media.Brushes.Gray;
        }
        else
        {
            TimeText.BorderBrush = System.Windows.Media.Brushes.IndianRed;
        }
    }

    private void OnIncreaseClick(object sender, RoutedEventArgs e) => Shift(TimeSpan.FromMinutes(30));

    private void OnDecreaseClick(object sender, RoutedEventArgs e) => Shift(TimeSpan.FromMinutes(-30));

    private void Shift(TimeSpan delta)
    {
        var current = SelectedTime ?? new TimeOnly(21, 0);

        var minutes = (current.Hour * 60) + current.Minute + (int)delta.TotalMinutes;
        minutes = ((minutes % 1440) + 1440) % 1440;

        SelectedTime = new TimeOnly(minutes / 60, minutes % 60);
    }
}
