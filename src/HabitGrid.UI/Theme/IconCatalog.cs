namespace HabitGrid.UI.Theme;

/// <summary>
/// Một biểu tượng trong bộ icon Fluent dùng cho thói quen.
/// </summary>
/// <param name="Key">Khóa lưu trong CSDL, ví dụ "Book24Regular".</param>
/// <param name="Glyph">Ký tự trong font Segoe Fluent Icons / Segoe MDL2 Assets.</param>
/// <param name="DisplayName">Tên hiển thị tiếng Việt trong bảng chọn icon.</param>
public sealed record HabitIcon(string Key, string Glyph, string DisplayName);

/// <summary>
/// Danh mục 60 biểu tượng Fluent phổ biến để người dùng chọn cho thói quen.
/// Mã ký tự được lưu dạng số nên file nguồn luôn là ASCII, không phụ thuộc encoding.
/// </summary>
public static class IconCatalog
{
    private static readonly (string Key, int CodePoint, string Name)[] Items =
    {
        ("Checkmark24Regular", 0xE73E, "Dấu tick"),
        ("Book24Regular", 0xE82A, "Sách"),
        ("WaterDrop24Regular", 0xE706, "Giọt nước"),
        ("Bicycle24Regular", 0xE7F4, "Xe đạp"),
        ("Run24Regular", 0xE7EE, "Chạy bộ"),
        ("WeatherSunny24Regular", 0xE706, "Nắng"),
        ("WeatherMoon24Regular", 0xE708, "Trăng"),
        ("Heart24Regular", 0xE734, "Tim"),
        ("LightningBolt24Regular", 0xE945, "Sét"),
        ("Star24Regular", 0xE735, "Ngôi sao"),
        ("Home24Regular", 0xE80F, "Nhà"),
        ("Building24Regular", 0xE80F, "Tòa nhà"),
        ("Briefcase24Regular", 0xE823, "Cặp tài liệu"),
        ("ShoppingCart24Regular", 0xE8D6, "Giỏ hàng"),
        ("Food24Regular", 0xE719, "Đồ ăn"),
        ("Coffee24Regular", 0xE7F4, "Cà phê"),
        ("ForkKnife24Regular", 0xE8D1, "Ăn uống"),
        ("Medical24Regular", 0xE8A9, "Y tế"),
        ("Fitness24Regular", 0xE8F6, "Thể hình"),
        ("MusicNote2-24Regular", 0xE95E, "Nhạc"),
        ("Video24Regular", 0xE8B2, "Video"),
        ("Camera24Regular", 0xE722, "Máy ảnh"),
        ("Mail24Regular", 0xE715, "Thư"),
        ("Chat24Regular", 0xE8BD, "Trò chuyện"),
        ("People24Regular", 0xE77B, "Nhóm người"),
        ("Person24Regular", 0xE77B, "Cá nhân"),
        ("Earth24Regular", 0xE909, "Trái đất"),
        ("Globe24Regular", 0xE774, "Website"),
        ("Code24Regular", 0xE943, "Lập trình"),
        ("Document24Regular", 0xE8A5, "Tài liệu"),
        ("Note24Regular", 0xE70B, "Ghi chú"),
        ("Edit24Regular", 0xE70F, "Chỉnh sửa"),
        ("Add24Regular", 0xE710, "Thêm"),
        ("Delete24Regular", 0xE74D, "Xóa"),
        ("Archive24Regular", 0xE7C1, "Lưu trữ"),
        ("Search24Regular", 0xE721, "Tìm kiếm"),
        ("Setting24Regular", 0xE713, "Cài đặt"),
        ("Clock24Regular", 0xE823, "Đồng hồ"),
        ("Calendar24Regular", 0xE787, "Lịch"),
        ("Alarm24Regular", 0xE7E7, "Báo thức"),
        ("Eye24Regular", 0xE7B3, "Hiện"),
        ("EyeOff24Regular", 0xE7B4, "Ẩn"),
        ("Pin24Regular", 0xE718, "Ghim"),
        ("Grid24Regular", 0xE80F, "Lưới"),
        ("Chart24Regular", 0xE9D9, "Biểu đồ"),
        ("Target24Regular", 0xE81C, "Mục tiêu"),
        ("Trophy24Regular", 0xE3D6, "Cúp"),
        ("Fire24Regular", 0xE721, "Lửa"),
        ("Leaf24Regular", 0xE734, "Lá"),
        ("Tree24Regular", 0xE8B3, "Cây"),
        ("Animal24Regular", 0xE7E6, "Động vật"),
        ("Car24Regular", 0xE804, "Ô tô"),
        ("Airplane24Regular", 0xE709, "Máy bay"),
        ("Boat24Regular", 0xE7C3, "Thuyền"),
        ("Money24Regular", 0xE1D3, "Tiền"),
        ("Gift24Regular", 0xE8E7, "Quà"),
        ("Bookmark24Regular", 0xE718, "Dấu trang"),
        ("Tag24Regular", 0xE8EC, "Thẻ"),
        ("Flag24Regular", 0xE7E5, "Cờ"),
        ("Key24Regular", 0xE8B7, "Chìa khóa"),
        ("Lock24Regular", 0xE72E, "Khóa"),
        ("Cloud24Regular", 0xE753, "Mây"),
        ("Tools24Regular", 0xE90F, "Dụng cụ"),
        ("Game24Regular", 0xE7FC, "Trò chơi"),
        ("Palette24Regular", 0xE8B3, "Màu sắc"),
        ("Print24Regular", 0xE749, "In"),
        ("Save24Regular", 0xE74E, "Lưu"),
        ("Share24Regular", 0xE72D, "Chia sẻ")
    };

    /// <summary>Toàn bộ biểu tượng trong catalog.</summary>
    public static IReadOnlyList<HabitIcon> All { get; } = Items
        .Select(i => new HabitIcon(i.Key, char.ConvertFromUtf32(i.CodePoint), i.Name))
        .ToArray();

    /// <summary>Lấy biểu tượng theo khóa, có phương án dự phòng.</summary>
    public static HabitIcon Get(string? key)
    {
        return All.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase))
               ?? All[0];
    }

    /// <summary>Biểu tượng dùng khi không tìm thấy khóa.</summary>
    public static HabitIcon Default => All[0];

    /// <summary>Khóa biểu tượng dùng khi không tìm thấy khóa.</summary>
    public const string DefaultKey = "Checkmark24Regular";
}
