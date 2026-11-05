namespace HabitGrid.UI.Controls;

/// <summary>
/// Thông tin một ô trong lưới heatmap, suy luận bằng toán học từ toạ độ chuột (O(1)).
/// </summary>
/// <param name="WeekColumnIndex">Chỉ số cột tuần (0 = cột xa nhất bên trái).</param>
/// <param name="DayRowIndex">Chỉ số hàng ngày (0 = ngày đầu tuần theo cài đặt).</param>
/// <param name="Date">Ngày logic của ô.</param>
/// <param name="CellBounds">Hình chữ nhật của ô trong toạ độ control.</param>
/// <param name="IsToday">True nếu là hôm nay.</param>
/// <param name="IsFuture">True nếu là ngày tương lai (không cho đánh dấu).</param>
/// <param name="IsScheduled">True nếu ngày thuộc lịch lặp của thói quen.</param>
public record GridCellInfo(
    int WeekColumnIndex,
    int DayRowIndex,
    DateOnly Date,
    System.Windows.Rect CellBounds,
    bool IsToday,
    bool IsFuture,
    bool IsScheduled)
{
    /// <summary>True nếu người dùng có thể tương tác với ô này.</summary>
    public bool IsInteractive => !IsFuture;
}
