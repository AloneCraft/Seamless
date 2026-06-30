using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI;

public abstract class BenchmarkBindableObject:INotifyPropertyChanged{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field,T value,[CallerMemberName] string? propertyName = null){
        if(EqualityComparer<T>.Default.Equals(field,value)){
            return false;
        }

        field=value;
        this.OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null){
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName));
    }
}

// Shared models for Worker and the Uno runner.
// Runner must not redefine these model types (for example CpuCoreInfo / ReportRow).
public enum CpuCoreKind{
    Performance,
    Efficiency,
    Unknown,
}

public sealed class CpuCoreInfo:BenchmarkBindableObject{
    private bool isSelected;

    public CpuCoreInfo(
        int logicalProcessorIndex,
        CpuCoreKind coreKind,
        int physicalCoreIndex,
        int kindCoreIndex,
        long affinityMask,
        bool supportsAffinity){
        this.LogicalProcessorIndex=logicalProcessorIndex;
        this.CoreKind=coreKind;
        this.PhysicalCoreIndex=physicalCoreIndex;
        this.KindCoreIndex=kindCoreIndex;
        this.AffinityMask=affinityMask;
        this.SupportsAffinity=supportsAffinity;
        this.isSelected=false;
    }

    public int LogicalProcessorIndex { get; }
    public CpuCoreKind CoreKind { get; }
    public int PhysicalCoreIndex { get; }
    public int KindCoreIndex { get; }
    public long AffinityMask { get; }
    public bool SupportsAffinity { get; }
    public string GroupDisplayText => this.CoreLabel;
    public string CpuDisplayText => $"{this.LogicalProcessorIndex}({this.GroupDisplayText})";

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.OnPropertyChanged(nameof(this.CheckGlyph));
            }
        }
    }

    public string DisplayText => this.CpuDisplayText;
    public string CheckGlyph => this.isSelected ?"X":string.Empty;
    public string CoreLabel => this.CoreKind switch{
        CpuCoreKind.Performance => $"P{this.KindCoreIndex}",
        CpuCoreKind.Efficiency => $"E{this.KindCoreIndex}",
        _ => $"Core {this.PhysicalCoreIndex}",
    };
    public string AffinityText => this.SupportsAffinity
        ? $"mask {this.AffinityMask}"
        : "affinity unsupported";
}

internal sealed record CpuRowItem(
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
    string AllocatedBytes,
    string PeakRssBytes,
    string EstimatedPeakRssBytes,
    double MeanNanoseconds,
    double ErrorNanoseconds,
    double StdDevNanoseconds,
    long AllocatedBytesValue,
    long PeakRssBytesValue,
    long EstimatedPeakRssBytesValue,
    string LogicalCpu,
    string Core,
    string CpuDisplay,
    string StartedAt,
    string FinishedAt,
    string ArtifactFolder,
    int? ExitCode,
    string ArgumentKey = "",
    IReadOnlyList<BenchmarkArgument>? ArgumentValues = null){
    public IReadOnlyList<BenchmarkArgument> Arguments { get; } = ArgumentValues??[];
}
