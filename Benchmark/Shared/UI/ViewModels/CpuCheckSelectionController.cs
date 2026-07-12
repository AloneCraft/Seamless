using Syncfusion.Maui.Core.Internals;
using Syncfusion.Maui.DataGrid;

namespace UI;

/// <summary>
/// SfDataGrid の標準セル選択へ、修飾なし Space のチェック操作だけを追加します。
/// </summary>
internal sealed class CpuCheckSelectionController:DataGridCellSelectionController{
    private readonly GridCheckSelectionBehavior<CpuSelectionItem> checkSelection;

    public CpuCheckSelectionController(SfDataGrid dataGrid,Action<IReadOnlyList<CpuSelectionItem>> toggleChecks)
        :base(dataGrid){
        this.checkSelection=new GridCheckSelectionBehavior<CpuSelectionItem>(
            () => GetSelectedCpus(dataGrid),
            toggleChecks);
    }

    protected override void ProcessKeyDown(KeyEventArgs args,bool isCtrlKeyPressed,bool isShiftKeyPressed){
        if(this.checkSelection.ProcessKeyDown(args,isCtrlKeyPressed,isShiftKeyPressed)){
            return;
        }
        base.ProcessKeyDown(args,isCtrlKeyPressed,isShiftKeyPressed);
    }

    private static IReadOnlyList<CpuSelectionItem> GetSelectedCpus(SfDataGrid dataGrid) =>
        dataGrid.GetSelectedCells()
            .Select(GetCpu)
            .Where(static cpu => cpu is not null)
            .Select(static cpu => cpu!)
            .DistinctBy(static cpu => cpu.LogicalProcessorIndex)
            .ToArray();

    internal static bool IsSelectableCpuCell(SelectedCellInfo cell) => GetCpu(cell) is not null;

    private static CpuSelectionItem? GetCpu(SelectedCellInfo cell){
        if(cell.RowData is not CpuTableRow row||cell.Column is null||!TryGetCpuColumnIndex(cell.Column.MappingName,out var columnIndex)){
            return null;
        }
        return columnIndex<row.GridCells.Count ? row.GridCells[columnIndex]?.Cpu : null;
    }

    private static bool TryGetCpuColumnIndex(string? mappingName,out int columnIndex){
        const string prefix="Cpu";
        columnIndex=-1;
        return mappingName is not null
            &&mappingName.StartsWith(prefix,StringComparison.Ordinal)
            &&int.TryParse(mappingName.AsSpan(prefix.Length),out columnIndex)
            &&columnIndex>=0;
    }
}
