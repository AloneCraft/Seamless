using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class CpuTableCell:ObservableObject{
    private bool isSelected;

    public CpuTableCell(CpuSelectionItem cpu){
        this.Cpu=cpu;
        this.isSelected=cpu.IsSelected;
        cpu.PropertyChanged+=(_,args) => {
            if(args.PropertyName==nameof(CpuSelectionItem.IsSelected)){
                this.IsSelected=cpu.IsSelected;
            }else if(args.PropertyName==nameof(CpuSelectionItem.StatusDisplayText)
                ||args.PropertyName==nameof(CpuSelectionItem.CurrentJobText)
                ||args.PropertyName==nameof(CpuSelectionItem.StatusImageSource)
                ||args.PropertyName==nameof(CpuSelectionItem.RunStatusText)
                ||args.PropertyName==nameof(CpuSelectionItem.IsRunningJob)){
                this.OnPropertyChanged(nameof(this.DisplayText));
                this.OnPropertyChanged(nameof(this.LogicalProcessorText));
                this.OnPropertyChanged(nameof(this.CoreKindLabel));
                this.OnPropertyChanged(nameof(this.CellStatusText));
                this.OnPropertyChanged(nameof(this.BadgeBackgroundColor));
                this.OnPropertyChanged(nameof(this.BadgeTextColor));
                this.OnPropertyChanged(nameof(this.IsRunningJob));
                this.OnPropertyChanged(nameof(this.StatusImageSource));
                this.OnPropertyChanged(nameof(this.IsStatusImageVisible));
            }
        };
    }

    public CpuSelectionItem Cpu { get; }
    public string DisplayText => this.Cpu.StatusDisplayText;
    public string LogicalProcessorText => this.Cpu.LogicalProcessorText;
    public string CoreKindLabel => this.Cpu.CoreKindLabel;
    public string CoreKindImageSource => this.Cpu.CoreKindImageSource;
    public string CellStatusText => this.Cpu.CellStatusText;
    public string StatusImageSource => this.Cpu.StatusImageSource;
    public bool IsStatusImageVisible => !this.IsRunningJob&&!string.IsNullOrWhiteSpace(this.StatusImageSource);
    public Color BadgeBackgroundColor => this.Cpu.BadgeBackgroundColor;
    public Color BadgeTextColor => this.Cpu.BadgeTextColor;
    public bool IsRunningJob => this.Cpu.IsRunningJob;

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                if(this.Cpu.IsSelected!=value){
                    this.Cpu.IsSelected=value;
                }
            }
        }
    }
}
