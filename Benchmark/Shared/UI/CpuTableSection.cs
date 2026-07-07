using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class CpuTableSection:ObservableObject{
    private CpuSelectionState state;

    public CpuTableSection(string title,CpuCoreKind coreKind,IReadOnlyList<CpuTableColumn> columns,IReadOnlyList<CpuTableRow> rows){
        this.Title=title;
        this.CoreKind=coreKind;
        this.Columns=columns;
        this.Rows=rows;
        foreach(var column in columns){
            column.Section=this;
        }
        this.RefreshState();
        foreach(var cell in rows.SelectMany(static row => row.Cells)){
            cell.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(CpuTableCell.IsSelected)){
                    this.RefreshState();
                }
            };
        }
    }

    public string Title { get; }
    public CpuCoreKind CoreKind { get; }
    public IReadOnlyList<CpuTableColumn> Columns { get; }
    public IReadOnlyList<CpuTableRow> Rows { get; }
    public IEnumerable<CpuTableCell> Cells => this.Rows.SelectMany(static row => row.Cells);

    public CpuSelectionState State{
        get => this.state;
        private set{
            if(this.SetProperty(ref this.state,value)){
                this.OnPropertyChanged(nameof(this.StateText));
                this.OnPropertyChanged(nameof(this.StateGlyph));
            }
        }
    }

    public string StateText => this.State switch{
        CpuSelectionState.Checked => "X",
        CpuSelectionState.Mixed => "-",
        _ => string.Empty,
    };
    public string StateGlyph => this.State switch{
        CpuSelectionState.Checked => "[X]",
        CpuSelectionState.Mixed => "[-]",
        _ => "[ ]",
    };
    public string BadgeText => this.CoreKind switch{
        CpuCoreKind.Performance => "P",
        CpuCoreKind.Efficiency => "E",
        _ => "U",
    };
    public string BadgeImageSource => BenchmarkCpuCoreImageNames.FromCoreKind(this.CoreKind);
    public Color BadgeBackgroundColor => this.CoreKind switch{
        CpuCoreKind.Performance => Color.FromArgb("#DBEAFE"),
        CpuCoreKind.Efficiency => Color.FromArgb("#DCFCE7"),
        _ => Color.FromArgb("#E2E8F0"),
    };
    public Color BadgeTextColor => this.CoreKind switch{
        CpuCoreKind.Performance => Color.FromArgb("#1D4ED8"),
        CpuCoreKind.Efficiency => Color.FromArgb("#15803D"),
        _ => Color.FromArgb("#475569"),
    };

    public CpuSelectionState GetColumnState(int columnIndex){
        var cells = this.Rows
            .Where(row => columnIndex<row.Cells.Count)
            .Select(row => row.Cells[columnIndex])
            .ToArray();
        return GetState(cells);
    }

    public void RefreshState(){
        this.State=GetState(this.Cells.ToArray());
        foreach(var row in this.Rows){
            row.RefreshState();
        }
        foreach(var column in this.Columns){
            column.Refresh();
        }
    }

    private static CpuSelectionState GetState(IReadOnlyList<CpuTableCell> cells){
        if(cells.Count==0){
            return CpuSelectionState.Unchecked;
        }

        var selected = cells.Count(static cell => cell.IsSelected);
        return selected==0
            ? CpuSelectionState.Unchecked
            : selected==cells.Count
                ? CpuSelectionState.Checked
                : CpuSelectionState.Mixed;
    }
}
