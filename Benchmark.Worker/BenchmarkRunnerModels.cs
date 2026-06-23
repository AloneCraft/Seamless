using global::System.ComponentModel;
using global::System.Runtime.CompilerServices;

namespace Benchmark.Runner.Uno;

public abstract class BindableObject:INotifyPropertyChanged{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field,T value,[CallerMemberName] string? propertyName = null){
        if(EqualityComparer<T>.Default.Equals(field,value)){
            return false;
        }

        field=value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null){
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName));
    }
}

// Shared models for Benchmark.Worker and the Uno runner.
// Runner must not redefine these model types (for example CpuCoreInfo / ReportRow).
public enum CpuCoreKind{
    Performance,
    Efficiency,
    Unknown,
}

public sealed class CpuCoreInfo:BindableObject{
    private bool isSelected;

    public CpuCoreInfo(
        int logicalProcessorIndex,
        CpuCoreKind coreKind,
        int physicalCoreIndex,
        int kindCoreIndex,
        long affinityMask,
        bool supportsAffinity){
        LogicalProcessorIndex=logicalProcessorIndex;
        CoreKind=coreKind;
        PhysicalCoreIndex=physicalCoreIndex;
        KindCoreIndex=kindCoreIndex;
        AffinityMask=affinityMask;
        SupportsAffinity=supportsAffinity;
        isSelected=false;
    }

    public int LogicalProcessorIndex { get; }
    public CpuCoreKind CoreKind { get; }
    public int PhysicalCoreIndex { get; }
    public int KindCoreIndex { get; }
    public long AffinityMask { get; }
    public bool SupportsAffinity { get; }
    public string GroupDisplayText => CoreLabel;
    public string CpuDisplayText => $"{LogicalProcessorIndex}({GroupDisplayText})";

    public bool IsSelected{
        get => isSelected;
        set{
            if(SetProperty(ref isSelected,value)){
                OnPropertyChanged(nameof(CheckGlyph));
            }
        }
    }

    public string DisplayText => CpuDisplayText;
    public string CheckGlyph => isSelected?"X":string.Empty;
    public string CoreLabel => CoreKind switch{
        CpuCoreKind.Performance => $"P{KindCoreIndex}",
        CpuCoreKind.Efficiency => $"E{KindCoreIndex}",
        _ => $"Core {PhysicalCoreIndex}",
    };
    public string AffinityText => SupportsAffinity
        ? $"mask {AffinityMask}"
        : "affinity unsupported";
}

public sealed record CpuRowItem(
    CpuCoreInfo? Column0,
    CpuCoreInfo? Column1,
    CpuCoreInfo? Column2,
    CpuCoreInfo? Column3);

public sealed record ReportRow(
    string Project,
    string Method,
    string TypeFullName,
    string FullFunctionName,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    string LogicalCpu,
    string Core,
    string CpuDisplay,
    string StartedAt,
    string FinishedAt,
    string ArtifactFolder,
    int? ExitCode);
