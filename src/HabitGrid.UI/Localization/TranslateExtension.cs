using System.Windows.Data;
using System.Windows.Markup;

namespace HabitGrid.UI.Localization;

/// <summary>
/// Markup extension tra cứu chuỗi đa ngôn ngữ:
/// <c>Text="{loc:Translate habit.add}"</c>.
/// Binding được tạo động nên khi đổi ngôn ngữ, chuỗi hiển thị cập nhật ngay lập tức.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TranslateExtension : MarkupExtension
{
    public TranslateExtension()
    {
    }

    public TranslateExtension(string key)
    {
        Key = key;
    }

    /// <summary>Khóa chuỗi trong file tài nguyên.</summary>
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };

        // Phải để Binding tự phân giải để WPF gán đúng vào DependencyProperty.
        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>
/// Markup extension tra cứu chuỗi có tham số thay thế.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TranslateFormatExtension : MarkupExtension
{
    public TranslateFormatExtension()
    {
    }

    public TranslateFormatExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    /// <summary>Các tham số dùng cho <c>{0}</c>, <c>{1}</c>...</summary>
    public object?[] Args { get; set; } = Array.Empty<object?>();

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        return LocalizationManager.Instance.Format(Key, Args);
    }
}
