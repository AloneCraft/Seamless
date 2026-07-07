using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class CpuTableRow:ObservableObject{
    private CpuSelectionState state;

    public CpuTableRow(string label,CpuCoreKind coreKind,IReadOnlyList<CpuTableCell> cells,int columnCount){
        this.Label=label;
        this.CoreKind=coreKind;
        this.Cells=cells;
        this.GridCells=cells
            .Cast<CpuTableCell?>()
            .Concat(Enumerable.Repeat<CpuTableCell?>(null,Math.Max(0,columnCount-cells.Count)))
            .ToArray();
        foreach(var cell in cells){
            cell.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(CpuTableCell.IsSelected)){
                    this.RefreshState();
                }
            };
        }
        this.RefreshState();
    }

    public string Label { get; }
    public CpuCoreKind CoreKind { get; }
    public IReadOnlyList<CpuTableCell> Cells { get; }
    public IReadOnlyList<CpuTableCell?> GridCells { get; }
    public CpuSelectionState State{
        get => this.state;
        private set{
            if(this.SetProperty(ref this.state,value)){
                this.OnPropertyChanged(nameof(this.IsChecked));
            }
        }
    }
    public bool IsChecked{
        get => this.State==CpuSelectionState.Checked;
        set{
            foreach(var cell in this.Cells){
                cell.IsSelected=value;
            }
        }
    }
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

    public void RefreshState(){
        this.State=GetState(this.Cells);
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
