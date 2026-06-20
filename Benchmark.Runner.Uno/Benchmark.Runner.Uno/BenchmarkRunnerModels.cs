using global::System.ComponentModel;
using global::System.Text.RegularExpressions;
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

public sealed class BenchmarkProject:BindableObject{
    private bool isSelected;

    public BenchmarkProject(string displayName,Type benchmarkType,bool isSelected){
        DisplayName=displayName;
        BenchmarkType=benchmarkType;
        AssemblyName=benchmarkType.Assembly.GetName().Name??benchmarkType.Assembly.FullName??string.Empty;
        TypeFullName=benchmarkType.FullName??benchmarkType.Name;
        this.isSelected=isSelected;
    }

    public string DisplayName { get; }
    public string AssemblyName { get; }
    public string TypeFullName { get; }
    public Type BenchmarkType { get; }
    public string ProjectKey => TypeFullName;

    public bool IsSelected{
        get => isSelected;
        set => SetProperty(ref isSelected,value);
    }

}

public sealed class BenchmarkItem:BindableObject{
    private bool isSelected;

    public BenchmarkItem(
        string methodName,
        string displayName,
        string[] categories,
        IReadOnlyDictionary<string,BenchmarkProjectMethod> projectMethods){
        MethodName=methodName;
        DisplayName=displayName;
        Categories=categories;
        ProjectMethods=projectMethods;
        CategoryText=categories.Length==0?"-":string.Join(", ",categories);
    }

    public string MethodName { get; }
    public string DisplayName { get; }
    public string[] Categories { get; }
    public string CategoryText { get; }
    public IReadOnlyDictionary<string,BenchmarkProjectMethod> ProjectMethods { get; }

    public bool IsSelected{
        get => isSelected;
        set{
            if(SetProperty(ref isSelected,value)){
                OnPropertyChanged(nameof(CheckGlyph));
            }
        }
    }

    public string CheckGlyph => isSelected?"X":string.Empty;
}

public sealed record BenchmarkProjectMethod(
    string TypeName,
    string TypeFullName,
    string MethodName,
    string DisplayName,
    string[] Categories){
    public string ExactFilter => string.IsNullOrWhiteSpace(TypeFullName)
        ? "*."+MethodName
        : "*"+TypeFullName+"."+MethodName;
}

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

public enum BenchmarkRunStatus{
    Pending,
    Running,
    Completed,
    Failed,
    Canceled,
}

public sealed class BenchmarkRunItem:BindableObject{
    private BenchmarkRunStatus status = BenchmarkRunStatus.Pending;
    private CpuCoreInfo? assignedCpu;
    private int processId;
    private int attempt;
    private string? artifactFolder;
    private string? mean;
    private string? error;
    private string? stdDev;
    private int? exitCode;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? finishedAt;

    public BenchmarkRunItem(BenchmarkProject project,BenchmarkItem method,BenchmarkProjectMethod projectMethod){
        Project=project;
        Method=method;
        ProjectMethod=projectMethod;
    }

    public BenchmarkProject Project { get; }
    public BenchmarkItem Method { get; }
    public BenchmarkProjectMethod ProjectMethod { get; }

    public string ProjectDisplayName => Project.DisplayName;
    public string MethodName => Method.MethodName;

    public BenchmarkRunStatus Status{
        get => status;
        set{
            if(SetProperty(ref status,value)){
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public CpuCoreInfo? AssignedCpu{
        get => assignedCpu;
        set{
            if(SetProperty(ref assignedCpu,value)){
                OnPropertyChanged(nameof(CpuDisplay));
            }
        }
    }

    public int ProcessId{
        get => processId;
        set{
            if(SetProperty(ref processId,value)){
                OnPropertyChanged(nameof(ProcessIdText));
            }
        }
    }

    public int Attempt{
        get => attempt;
        set => SetProperty(ref attempt,value);
    }

    public string? ArtifactFolder{
        get => artifactFolder;
        set => SetProperty(ref artifactFolder,value);
    }

    public string? Mean{
        get => mean;
        set => SetProperty(ref mean,value);
    }

    public string? Error{
        get => error;
        set => SetProperty(ref error,value);
    }

    public string? StdDev{
        get => stdDev;
        set => SetProperty(ref stdDev,value);
    }

    public int? ExitCode{
        get => exitCode;
        set => SetProperty(ref exitCode,value);
    }

    public DateTimeOffset? StartedAt{
        get => startedAt;
        set => SetProperty(ref startedAt,value);
    }

    public DateTimeOffset? FinishedAt{
        get => finishedAt;
        set => SetProperty(ref finishedAt,value);
    }

    public string StatusText => Status.ToString();
    public string CpuDisplay => AssignedCpu?.CpuDisplayText??"-";
    public string ProcessIdText => ProcessId==0?"-":ProcessId.ToString();
}

public sealed record BenchmarkListItem(
    string TypeName,
    string TypeFullName,
    string MethodName,
    string DisplayName,
    string Filter,
    string[]? Categories);

public sealed record ProcessResult(int ExitCode,string StandardOutput,string StandardError);

public sealed record WorkerTaskMessage(
    string Command,
    string TaskId,
    string AssemblyName,
    string TypeFullName,
    string MethodName,
    string ArtifactFolder);

public sealed record WorkerResultMessage(
    string TaskId,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    int ExitCode,
    string ArtifactFolder,
    string Log);

public sealed record ReportRow(
    string Project,
    string Method,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    string LogicalCpu,
    string Core,
    string CpuDisplay,
    string ArtifactFolder,
    int? ExitCode);

public static class BenchmarkText{
    private static readonly Regex invalidPathChars = new("["+Regex.Escape(new string(Path.GetInvalidFileNameChars()))+"]",RegexOptions.Compiled);

    public static string SanitizePathPart(string value){
        var sanitized = invalidPathChars.Replace(value,"_").Trim();
        return string.IsNullOrWhiteSpace(sanitized)?"_":sanitized;
    }
}
