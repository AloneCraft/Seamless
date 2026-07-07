using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;

public sealed class CpuSelectionItem:ObservableObject{
    private bool isSelected;
    private string runStatusText = "待機";
    private string statusImageSource = BenchmarkStatusImageNames.CpuIdle;
    private string currentJobText = string.Empty;
    private bool isRunningJob;

    public CpuSelectionItem(int logicalProcessorIndex,CpuCoreKind coreKind,int physicalCoreIndex,int kindCoreIndex,bool isSelected){
        this.LogicalProcessorIndex=logicalProcessorIndex;
        this.CoreKind=coreKind;
        this.PhysicalCoreIndex=physicalCoreIndex;
        this.KindCoreIndex=kindCoreIndex;
        this.isSelected=isSelected;
    }

    public int LogicalProcessorIndex { get; }
    public CpuCoreKind CoreKind { get; }
    public int PhysicalCoreIndex { get; }
    public int KindCoreIndex { get; }
    public string CoreLabel => this.CoreKind switch{
        CpuCoreKind.Performance => "P"+this.KindCoreIndex,
        CpuCoreKind.Efficiency => "E"+this.KindCoreIndex,
        _ => "Core "+this.PhysicalCoreIndex,
    };
    public string CoreKindLabel => this.CoreKind switch{
        CpuCoreKind.Performance => "P",
        CpuCoreKind.Efficiency => "E",
        _ => "U",
    };
    public string CoreKindImageSource => BenchmarkCpuCoreImageNames.FromCoreKind(this.CoreKind);
    public string DisplayName => "CPU "+this.LogicalProcessorIndex+" ("+this.CoreLabel+")";
    public string RunStatusText{
        get => this.runStatusText;
        private set => this.SetProperty(ref this.runStatusText,value);
    }
    public string StatusImageSource{
        get => this.statusImageSource;
        private set => this.SetProperty(ref this.statusImageSource,value);
    }
    public string CurrentJobText{
        get => this.currentJobText;
        private set => this.SetProperty(ref this.currentJobText,value);
    }
    public bool IsRunningJob{
        get => this.isRunningJob;
        private set => this.SetProperty(ref this.isRunningJob,value);
    }
    public string StatusDisplayText =>
        this.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture)
        +" "+this.CoreKindLabel
        +" "+this.RunStatusText
        +(string.IsNullOrWhiteSpace(this.CurrentJobText) ? string.Empty : " "+this.CurrentJobText);
    public string LogicalProcessorText => this.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture);
    public string CellStatusText => BenchmarkCpuStatusImageLogic.GetCellStatusText(this.RunStatusText,this.CurrentJobText);
    public Color BadgeBackgroundColor => this.CoreKind switch{
        CpuCoreKind.Performance => Color.FromArgb("#DBEAFE"),
        CpuCoreKind.Efficiency => Color.FromArgb("#DCFCE7"),
        _ => Color.FromArgb("#E2E8F0"),
    };
    public Color BadgeTextColor => this.CoreKind switch{
        CpuCoreKind.Performance => Color.FromArgb("#1D4ED8"),
        CpuCoreKind.Efficiency => Color.FromArgb("#15803D"),
        _ => Color.FromArgb("#334155"),
    };

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }

    public void ApplyJob(BenchmarkResultItem item){
        this.IsRunningJob=string.Equals(item.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        this.StatusImageSource=BenchmarkCpuStatusImageLogic.GetImageSource(item.Status);
        this.RunStatusText=BenchmarkCpuStatusImageLogic.GetStatusText(item.Status);
        this.CurrentJobText=item.Project+" / "+item.Method+" / "+item.ElementCount.ToString(CultureInfo.InvariantCulture);
        this.OnPropertyChanged(nameof(this.StatusDisplayText));
        this.OnPropertyChanged(nameof(this.CellStatusText));
    }

    public void ClearJob(){
        this.IsRunningJob=false;
        this.StatusImageSource=BenchmarkStatusImageNames.CpuIdle;
        this.RunStatusText="待機";
        this.CurrentJobText=string.Empty;
        this.OnPropertyChanged(nameof(this.StatusDisplayText));
        this.OnPropertyChanged(nameof(this.CellStatusText));
    }
}
