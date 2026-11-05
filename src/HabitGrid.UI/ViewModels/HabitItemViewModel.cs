using CommunityToolkit.Mvvm.ComponentModel;
using HabitGrid.Core.Models;
using HabitGrid.UI.Theme;

namespace HabitGrid.UI.ViewModels;

/// <summary>
/// ViewModel của một thẻ thói quen trong danh sách bên trái.
/// </summary>
public partial class HabitItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isWidgetVisible = true;

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private bool _isSelected;

    public HabitItemViewModel(Habit model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <summary>Thực thể domain gốc.</summary>
    public Habit Model { get; }

    public Guid Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (string.Equals(Model.Name, value, StringComparison.Ordinal))
            {
                return;
            }

            Model.Name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Biểu tượng hiển thị.</summary>
    public HabitIcon Icon => IconCatalog.Get(Model.Icon);

    public HabitKind Kind => Model.Kind;

    /// <summary>True nếu thói quen bị khoá chỉ đọc do vượt hạn mức gói Free.</summary>
    public bool IsReadOnly => Model.IsReadOnly;

    /// <summary>True nếu thói quen đã lưu trữ.</summary>
    public bool IsArchived => Model.IsArchived;

    /// <summary>Chuỗi hiện tại dạng chữ.</summary>
    public string StreakText => CurrentStreak > 0 ? $"{CurrentStreak} ngày" : "—";

    /// <summary>Chuỗi hiện tại dạng số (dùng để sắp xếp nếu cần).</summary>
    public int Streak => CurrentStreak;

    partial void OnCurrentStreakChanged(int value) => OnPropertyChanged(nameof(StreakText));

    /// <summary>Thông báo lại các thuộc tính hiển thị sau khi mô hình thay đổi bên ngoài.</summary>
    public void RefreshDisplay()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(StreakText));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(IsArchived));
    }
}
