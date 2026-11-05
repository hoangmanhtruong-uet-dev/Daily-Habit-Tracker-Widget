namespace HabitGrid.UI.Services;

/// <summary>
/// Các hành động cần tới ứng dụng mà ViewModel cài đặt không thể tự thực hiện.
/// </summary>
public interface ISettingsHost
{
    /// <summary>Đưa toàn bộ widget về giữa màn hình chính.</summary>
    Task ResetWidgetPositionsAsync();

    /// <summary>Mở thư mục chứa dữ liệu trong Windows Explorer.</summary>
    void OpenDataFolder();

    /// <summary>Xuất bản sao lưu JSON ra tệp do người dùng chọn.</summary>
    Task ExportJsonAsync();

    /// <summary>Xuất bảng CSV của một thói quen.</summary>
    Task ExportCsvAsync();

    /// <summary>Nhập bản sao lưu JSON.</summary>
    Task ImportJsonAsync();

    /// <summary>Sao chép thông tin phiên bản vào clipboard.</summary>
    void CopyVersionInfo(string versionText);

    /// <summary>Mở màn hình nâng cấp Pro.</summary>
    Task ShowPaywallAsync();
}
