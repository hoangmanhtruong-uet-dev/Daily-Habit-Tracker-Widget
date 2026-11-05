using System.Numerics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI.ViewManagement;

namespace HabitGrid.WinUI.Controls;

/// <summary>
/// Nút tròn check-in (design system §8). Có/không: vòng rỗng → tròn đặc + dấu tick; đếm/thời lượng: hiện nhãn tiến độ
/// và vòng đầy dần. Khi hoàn thành chạy animation nảy lò xo (bỏ qua nếu Windows tắt hiệu ứng).
/// </summary>
public sealed class HabitCheckButton : UserControl
{
    public static readonly DependencyProperty ColorHueProperty =
        DependencyProperty.Register(nameof(ColorHue), typeof(string), typeof(HabitCheckButton), new PropertyMetadata("#8B5CF6", OnVisualChanged));

    public static readonly DependencyProperty IsDoneProperty =
        DependencyProperty.Register(nameof(IsDone), typeof(bool), typeof(HabitCheckButton), new PropertyMetadata(false, OnIsDoneChanged));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(HabitCheckButton), new PropertyMetadata(0.0, OnVisualChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(HabitCheckButton), new PropertyMetadata(string.Empty, OnVisualChanged));

    private const double Size = 40;

    private readonly Ellipse _ring = new()
    {
        Width = Size,
        Height = Size,
        StrokeThickness = 2.5,
        Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
    };

    private readonly Ellipse _fill = new() { Width = Size, Height = Size };

    private readonly FontIcon _tick = new()
    {
        Glyph = "",
        FontSize = 18,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly TextBlock _label = new()
    {
        FontSize = 11,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    public HabitCheckButton()
    {
        Width = Size;
        Height = Size;
        IsTabStop = true;
        UseSystemFocusVisuals = true;

        var root = new Grid { Width = Size, Height = Size, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        root.Children.Add(_fill);
        root.Children.Add(_ring);
        root.Children.Add(_tick);
        root.Children.Add(_label);
        Content = root;

        Tapped += (_, _) => Click?.Invoke(this, EventArgs.Empty);
        KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Space or VirtualKey.Enter)
            {
                Click?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };

        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        Refresh();
    }

    public event EventHandler? Click;

    /// <summary>Kích hoạt như người dùng bấm (dùng cho smoke test và trợ năng).</summary>
    public void PerformClick() => Click?.Invoke(this, EventArgs.Empty);

    public string ColorHue
    {
        get => (string)GetValue(ColorHueProperty);
        set => SetValue(ColorHueProperty, value);
    }

    public bool IsDone
    {
        get => (bool)GetValue(IsDoneProperty);
        set => SetValue(IsDoneProperty, value);
    }

    /// <summary>0..1 cho vòng tiến độ của loại đếm/thời lượng.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Chữ nhỏ trong vòng (ví dụ "3/8"); rỗng cho loại có/không.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HabitCheckButton)d).Refresh();

    private static void OnIsDoneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (HabitCheckButton)d;
        button.Refresh();

        if (e.NewValue is true && e.OldValue is false)
        {
            button.Bounce();
        }
    }

    private void Refresh()
    {
        var brush = new SolidColorBrush(Ui.ParseColor(ColorHue, Microsoft.UI.Colors.Gray));

        _ring.Stroke = brush;
        _fill.Fill = brush;
        _fill.Opacity = IsDone ? 1 : Math.Clamp(Progress, 0, 1) * 0.35;
        _tick.Visibility = IsDone ? Visibility.Visible : Visibility.Collapsed;

        _label.Text = IsDone ? string.Empty : Label;
        _label.Foreground = brush;
        _label.Visibility = !IsDone && !string.IsNullOrEmpty(Label) ? Visibility.Visible : Visibility.Collapsed;

        AutomationProperties.SetName(this, IsDone ? "✓" : Label);
    }

    /// <summary>Nảy 0.82 → 1 bằng spring trên Composition, không chặn luồng UI.</summary>
    private void Bounce()
    {
        if (!new UISettings().AnimationsEnabled)
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(this);
        visual.CenterPoint = new Vector3((float)(Size / 2), (float)(Size / 2), 0);

        var spring = visual.Compositor.CreateSpringVector3Animation();
        spring.InitialValue = new Vector3(0.82f, 0.82f, 1f);
        spring.FinalValue = Vector3.One;
        spring.DampingRatio = 0.45f;
        spring.Period = TimeSpan.FromMilliseconds(55);
        visual.StartAnimation("Scale", spring);
    }
}
