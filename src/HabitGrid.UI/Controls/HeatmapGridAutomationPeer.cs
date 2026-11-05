using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using HabitGrid.UI.Localization;

namespace HabitGrid.UI.Controls;

/// <summary>
/// Lớp tự động hóa cho một ô của lưới heatmap.
/// Người dùng Narrator sẽ nghe được ngày và trạng thái của từng ô, và có thể kích hoạt ô bằng phím.
/// </summary>
public sealed class HeatmapCellAutomationPeer : AutomationPeer, IInvokeProvider
{
    private readonly HeatmapGridControl _owner;
    private readonly GridCellInfo _cell;

    public HeatmapCellAutomationPeer(HeatmapGridControl owner, GridCellInfo cell)
    {
        _owner = owner;
        _cell = cell;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => "HabitGridHeatmapCell";

    protected override string GetNameCore() => BuildName();

    protected override string GetItemTypeCore() => "HabitGridHeatmapCell";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => true;

    protected override bool IsEnabledCore() => !_cell.IsFuture;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override string GetAutomationIdCore() => $"{_cell.Date:yyyy-MM-dd}";

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetItemStatusCore() => string.Empty;

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.Horizontal;

    protected override string GetHelpTextCore() => BuildName();

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override bool IsOffscreenCore() => false;

    protected override Rect GetBoundingRectangleCore() => _cell.CellBounds;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override void SetFocusCore() => _owner.Focus();

    protected override Point GetClickablePointCore() => _cell.CellBounds.TopLeft;

    public override object? GetPattern(PatternInterface patternInterface)
    {
        // Ô lưới hỗ trợ mẫu Invoke (nhấn để đánh dấu).
        return patternInterface == PatternInterface.Invoke ? this : null;
    }

    void IInvokeProvider.Invoke() => _owner.InvokeCell(_cell);

    /// <summary>
    /// Tên trợ năng: "Ngày 21 tháng 10 năm 2026, trạng thái: Đã hoàn thành".
    /// </summary>
    private string BuildName()
    {
        var culture = CultureInfo.CurrentUICulture;
        var dateFormat = culture.TwoLetterISOLanguageName == "vi" ? "d MMMM yyyy" : "D, MMM d, yyyy";
        var date = _cell.Date.ToDateTime(TimeOnly.MinValue).ToString(dateFormat, culture);

        var status = _cell.IsFuture
            ? LocalizationManager.Translate("cell.future")
            : _owner.IsCellCompleted(_cell.Date)
                ? LocalizationManager.Translate("cell.completed")
                : LocalizationManager.Translate("cell.notCompleted");

        return LocalizationManager.Instance.Format("cell.accessibility", date, status);
    }
}

/// <summary>
/// Lớp tự động hóa cho control lưới: báo cáo lưới 7 × N và cung cấp từng ô cho trình đọc màn hình.
/// </summary>
public sealed class HeatmapGridControlAutomationPeer : FrameworkElementAutomationPeer, IGridProvider
{
    private readonly HeatmapGridControl _owner;

    public HeatmapGridControlAutomationPeer(HeatmapGridControl owner)
        : base(owner)
    {
        _owner = owner;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override string GetClassNameCore() => "HabitGridHeatmap";

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var peers = new List<AutomationPeer>();

        var model = _owner.Model;

        if (model is null)
        {
            return peers;
        }

        var layout = model.Layout;

        for (var column = 0; column < layout.TotalWeeks; column++)
        {
            for (var row = 0; row < HeatmapLayout.DayRows; row++)
            {
                var cell = _owner.GetCell(column, row);

                if (cell is not null)
                {
                    peers.Add(new HeatmapCellAutomationPeer(_owner, cell));
                }
            }
        }

        return peers;
    }

    public int RowCount => HeatmapLayout.DayRows;

    public int ColumnCount => _owner.Model?.Layout.TotalWeeks ?? 0;

    public bool IsRowHeader => false;

    public bool IsColumnHeader => false;

    public IRawElementProviderSimple? GetItem(int row, int column)
    {
        var cell = _owner.GetCell(column, row);

        if (cell is null)
        {
            return null;
        }

        return ProviderFromPeer(new HeatmapCellAutomationPeer(_owner, cell));
    }

    public new string GetOrientation() => "Vertical";
}
