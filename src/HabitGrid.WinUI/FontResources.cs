using HabitGrid.Core.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace HabitGrid.WinUI;

/// <summary>
/// Từ điển phông chữ của app (P5). Được App.xaml dựng trước Tokens.xaml nên các style dùng <c>{StaticResource DisplayFont}</c> nhận đúng phông ngay từ đầu;
/// đổi phông vì thế có hiệu lực ở lần mở app kế tiếp. Lựa chọn đọc từ file nhỏ trong thư mục dữ liệu (CSDL chưa mở ở thời điểm này).
/// </summary>
public sealed class FontResources : ResourceDictionary
{
    private const string BundledDisplay = "ms-appx:///Assets/Fonts/BeVietnamPro-ExtraBold.ttf#Be Vietnam Pro";
    private const string BundledBold = "ms-appx:///Assets/Fonts/BeVietnamPro-Bold.ttf#Be Vietnam Pro";
    private const string BundledSemiBold = "ms-appx:///Assets/Fonts/BeVietnamPro-SemiBold.ttf#Be Vietnam Pro";

    public FontResources()
    {
        var family = AppFonts.Resolve(ReadChoice()).Family;
        this["DisplayFont"] = new FontFamily(family ?? BundledDisplay);
        this["DisplayBoldFont"] = new FontFamily(family ?? BundledBold);
        this["DisplaySemiBoldFont"] = new FontFamily(family ?? BundledSemiBold);
    }

    public static string FilePath => Path.Combine(AppPaths.DataDir, AppFonts.FileName);

    /// <summary>Ghi lựa chọn cho lần khởi động sau; chọn mặc định thì xóa file.</summary>
    public static void WriteChoice(string id)
    {
        try
        {
            if (AppFonts.Resolve(id).Family is null)
            {
                File.Delete(FilePath);
                return;
            }

            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(FilePath, AppFonts.Resolve(id).Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Không ghi được thì giữ phông cũ; không được làm app lỗi vì chuyện thẩm mỹ.
        }
    }

    public static string ReadChoice()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : AppFonts.DefaultId;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AppFonts.DefaultId;
        }
    }
}
