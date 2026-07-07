using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class CpuTableColumn:ObservableObject{
    public CpuTableColumn(int index,string title){
        this.Index=index;
        this.Title=title;
    }

    public int Index { get; }
    public string Title { get; }
    public CpuTableSection? Section { get; internal set; }
    public string StateText => this.Section?.GetColumnState(this.Index) switch{
        CpuSelectionState.Checked => "X",
        CpuSelectionState.Mixed => "-",
        _ => string.Empty,
    };
    public string StateGlyph => this.Section?.GetColumnState(this.Index) switch{
        CpuSelectionState.Checked => "[X]",
        CpuSelectionState.Mixed => "[-]",
        _ => "[ ]",
    };
    public bool IsChecked => this.Section?.GetColumnState(this.Index)==CpuSelectionState.Checked;
    public string DisplayText => string.IsNullOrEmpty(this.StateText) ? this.Title : this.StateText+" "+this.Title;
    public string HeaderText => "T"+this.Index;

    public void Refresh(){
        this.OnPropertyChanged(nameof(this.StateText));
        this.OnPropertyChanged(nameof(this.StateGlyph));
        this.OnPropertyChanged(nameof(this.IsChecked));
        this.OnPropertyChanged(nameof(this.DisplayText));
    }
}
