using System.Windows;

using HabitGrid.Core.Models;
namespace HabitGrid.UI.Controls;

/// <summary>
/// Toán học bố trí của lưới heatmap: vị trí ô, ánh xạ ngày và hit-test.
/// Không phụ thuộc giao diện nên kiểm thử được hoàn toàn bằng toán học.
/// </summary>
public sealed class HeatmapLayout
{
    /// <summary>Số hàng ngày trong lưới (luôn là 7).</summary>
    public const int DayRows = 7;

    private readonly WidgetSizeSpec _spec;

    /// <param name="preset">Cỡ widget.</param>
    /// <param name="today">Ngày logic hôm nay (cột cuối cùng chứa tuần hiện tại).</param>
    /// <param name="weekStart">Ngày bắt đầu tuần (Thứ Hai hoặc Chủ Nhật).</param>
    public HeatmapLayout(WidgetSize preset, DateOnly today, DayOfWeek weekStart = DayOfWeek.Monday)
    {
        _spec = WidgetSizeSpec.Get(preset);
        Today = today;
        WeekStart = weekStart;
        CurrentWeekStart = GetCurrentWeekStart(today, weekStart);
        FirstDate = CurrentWeekStart.AddDays(-(_spec.TotalWeeks - 1) * 7);
    }

    public WidgetSize Size => _spec.Size;

    public DateOnly Today { get; }

    public DayOfWeek WeekStart { get; }

    /// <summary>Ngày đầu tiên của tuần chứa hôm nay.</summary>
    public DateOnly CurrentWeekStart { get; }

    /// <summary>Ngày của ô đầu tiên (góc trên bên trái) của lưới.</summary>
    public DateOnly FirstDate { get; }

    public int TotalWeeks => _spec.TotalWeeks;

    public double CellSize => _spec.CellSize;

    public double CellGap => _spec.CellGap;

    public bool ShowDayLabels => _spec.ShowDayLabels;

    /// <summary>Toạ độ X bắt đầu của lưới (sau cột nhãn thứ).</summary>
    public double GridOriginX => _spec.ShowDayLabels ? WidgetSizeSpec.DayLabelWidth : 0;

    /// <summary>Toạ độ Y bắt đầu của lưới (sau hàng nhãn tháng).</summary>
    public double GridOriginY => WidgetSizeSpec.MonthLabelHeight;

    /// <summary>Chiều rộng phần lưới (không tính nhãn).</summary>
    public double GridWidth => (TotalWeeks * (CellSize + CellGap)) - CellGap;

    /// <summary>Chiều cao phần lưới (không tính nhãn).</summary>
    public double GridHeight => (DayRows * (CellSize + CellGap)) - CellGap;

    /// <summary>Chiều rộng do control cần chiếm.</summary>
    public double TotalWidth => GridOriginX + GridWidth;

    /// <summary>Chiều cao do control cần chiếm.</summary>
    public double TotalHeight => GridOriginY + GridHeight;

    /// <summary>Ngày của ô tại (cột tuần, hàng ngày).</summary>
    public DateOnly GetDate(int weekColumn, int dayRow)
    {
        return FirstDate.AddDays((weekColumn * DayRows) + dayRow);
    }

    /// <summary>Hình chữ nhật của ô tại (cột tuần, hàng ngày) trong toạ độ control.</summary>
    public Rect GetCellBounds(int weekColumn, int dayRow)
    {
        var pitch = CellSize + CellGap;

        var left = GridOriginX + (weekColumn * pitch);
        var top = GridOriginY + (dayRow * pitch);

        return new Rect(left, top, CellSize, CellSize);
    }

    /// <summary>
    /// Suy luận ô từ toạ độ chuột. Trả về null nếu chuột không nằm trong lòng ô
    /// (nằm ở khoảng trống giữa các ô hoặc ngoài lưới).
    /// </summary>
    public GridCellInfo? HitTest(Point mousePoint, Func<DateOnly, bool>? isScheduled = null)
    {
        var relativeX = mousePoint.X - GridOriginX;
        var relativeY = mousePoint.Y - GridOriginY;

        if (relativeX < 0 || relativeY < 0)
        {
            return null;
        }

        var pitch = CellSize + CellGap;

        var column = (int)(relativeX / pitch);
        var row = (int)(relativeY / pitch);

        if (column < 0 || column >= TotalWeeks || row < 0 || row >= DayRows)
        {
            return null;
        }

        // Chuột phải nằm đúng trong lòng ô, không tính khoảng gap.
        var insideX = relativeX % pitch;
        var insideY = relativeY % pitch;

        if (insideX > CellSize || insideY > CellSize)
        {
            return null;
        }

        var date = GetDate(column, row);

        return new GridCellInfo(
            column,
            row,
            date,
            GetCellBounds(column, row),
            date == Today,
            date > Today,
            isScheduled?.Invoke(date) ?? true);
    }

    /// <summary>
    /// Danh sách nhãn tháng: mỗi cột bắt đầu một tháng mới sẽ có một nhãn.
    /// </summary>
    public IReadOnlyList<MonthLabel> GetMonthLabels()
    {
        var labels = new List<MonthLabel>();
        var previousMonth = -1;

        for (var column = 0; column < TotalWeeks; column++)
        {
            var firstDayOfColumn = GetDate(column, 0);

            if (firstDayOfColumn.Month != previousMonth)
            {
                labels.Add(new MonthLabel(column, firstDayOfColumn.Month, firstDayOfColumn.Year));
                previousMonth = firstDayOfColumn.Month;
            }
        }

        return labels;
    }

    /// <summary>
    /// Các nhãn thứ trong tuần (T2/T4/T6 theo đặc tả), trả về theo chỉ số hàng.
    /// </summary>
    public IReadOnlyDictionary<int, DayOfWeek> GetDayLabelRows()
    {
        var rows = new Dictionary<int, DayOfWeek>();

        for (var row = 0; row < DayRows; row++)
        {
            var day = (DayOfWeek)(((int)WeekStart + row) % 7);

            // Chỉ hiển thị T2/T4/T6 để tiết kiệm diện tích.
            if (day is DayOfWeek.Tuesday or DayOfWeek.Thursday or DayOfWeek.Saturday)
            {
                rows[row] = day;
            }
        }

        return rows;
    }

    private static DateOnly GetCurrentWeekStart(DateOnly today, DayOfWeek weekStart)
    {
        var offset = ((int)today.DayOfWeek - (int)weekStart + 7) % 7;
        return today.AddDays(-offset);
    }
}

/// <summary>
/// Nhãn tháng hiển thị phía trên lưới.
/// </summary>
/// <param name="ColumnIndex">Cột bắt đầu tháng.</param>
/// <param name="Month">Tháng (1–12).</param>
/// <param name="Year">Năm.</param>
public sealed record MonthLabel(int ColumnIndex, int Month, int Year);
