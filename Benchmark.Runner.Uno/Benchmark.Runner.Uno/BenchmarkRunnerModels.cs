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
        this.OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null){
        this.PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class BenchmarkProject:BindableObject{
    private bool isSelected;

    public BenchmarkProject(string displayName,Type benchmarkType,bool isSelected){
        this.DisplayName=displayName;
        this.BenchmarkType=benchmarkType;
        this.AssemblyName=benchmarkType.Assembly.GetName().Name??benchmarkType.Assembly.FullName??string.Empty;
        this.TypeFullName=benchmarkType.FullName??benchmarkType.Name;
        this.isSelected=isSelected;
    }

    public string DisplayName { get; }
    public string AssemblyName { get; }
    public string TypeFullName { get; }
    public Type BenchmarkType { get; }
    public string ProjectKey => this.TypeFullName;

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.OnPropertyChanged(nameof(this.ColumnOpacity));
            }
        }
    }

    public double ColumnOpacity => this.isSelected ? 1.0 : 0.35;

}

public sealed class BenchmarkItem:BindableObject{
    private bool isSelected;

    public BenchmarkItem(
        string methodName,
        string displayName,
        string[] categories,
        IReadOnlyDictionary<string,BenchmarkProjectMethod> projectMethods,
        IEnumerable<BenchmarkProject> projectColumns){
        this.MethodName=methodName;
        this.DisplayName=displayName;
        this.Categories=categories;
        this.ProjectMethods=projectMethods;
        this.CategoryText=categories.Length==0?"-":string.Join(", ",categories);
        this.ProjectStatuses=new global::System.Collections.ObjectModel.ObservableCollection<BenchmarkProjectStatusCell>(
            projectColumns.Select(static project => new BenchmarkProjectStatusCell(project.ProjectKey,project.DisplayName,project.IsSelected)));
    }

    public string MethodName { get; }
    public string DisplayName { get; }
    public string[] Categories { get; }
    public string CategoryText { get; }
    public IReadOnlyDictionary<string,BenchmarkProjectMethod> ProjectMethods { get; }
    public global::System.Collections.ObjectModel.ObservableCollection<BenchmarkProjectStatusCell> ProjectStatuses { get; }

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.OnPropertyChanged(nameof(this.CheckGlyph));
            }
        }
    }

    public string CheckGlyph => this.isSelected?"X":string.Empty;

    public void SetProjectSelected(string projectKey,bool isProjectSelected){
        var cell = this.ProjectStatuses.FirstOrDefault(cell => string.Equals(cell.ProjectKey,projectKey,StringComparison.Ordinal));
        if(cell is not null){
            cell.IsProjectSelected=isProjectSelected;
            if(!isProjectSelected){
                cell.Status=BenchmarkProjectRunStatus.NotSelected;
            }
        }
    }

    public void ResetProjectStatuses(IEnumerable<BenchmarkProject> projects){
        var selectedProjects = projects.ToDictionary(static project => project.ProjectKey,static project => project.IsSelected,StringComparer.Ordinal);
        foreach(var cell in this.ProjectStatuses){
            cell.IsProjectSelected=selectedProjects.TryGetValue(cell.ProjectKey,out var selected)&&selected;
            cell.Status=cell.IsProjectSelected
                ? BenchmarkProjectRunStatus.Pending
                : BenchmarkProjectRunStatus.NotSelected;
        }
    }

    public void SetProjectStatus(string projectKey,BenchmarkRunStatus status){
        var cell = this.ProjectStatuses.FirstOrDefault(cell => string.Equals(cell.ProjectKey,projectKey,StringComparison.Ordinal));
        if(cell is null){
            return;
        }

        cell.Status = status switch{
            BenchmarkRunStatus.Pending => BenchmarkProjectRunStatus.Pending,
            BenchmarkRunStatus.Running => BenchmarkProjectRunStatus.Running,
            BenchmarkRunStatus.Completed => BenchmarkProjectRunStatus.Completed,
            BenchmarkRunStatus.Failed => BenchmarkProjectRunStatus.Failed,
            BenchmarkRunStatus.Canceled => BenchmarkProjectRunStatus.Canceled,
            _ => BenchmarkProjectRunStatus.Pending,
        };
    }
}

public enum BenchmarkProjectRunStatus{
    NotSelected,
    Pending,
    Running,
    Completed,
    Failed,
    Canceled,
}

public sealed class BenchmarkProjectStatusCell:BindableObject{
    private bool isProjectSelected;
    private BenchmarkProjectRunStatus status;

    public BenchmarkProjectStatusCell(string projectKey,string displayName,bool isProjectSelected){
        this.ProjectKey=projectKey;
        this.DisplayName=displayName;
        this.isProjectSelected=isProjectSelected;
        this.status=isProjectSelected
            ? BenchmarkProjectRunStatus.Pending
            : BenchmarkProjectRunStatus.NotSelected;
    }

    public string ProjectKey { get; }
    public string DisplayName { get; }

    public bool IsProjectSelected{
        get => this.isProjectSelected;
        set{
            if(this.SetProperty(ref this.isProjectSelected,value)){
                this.OnVisualStateChanged();
            }
        }
    }

    public BenchmarkProjectRunStatus Status{
        get => this.status;
        set{
            if(this.SetProperty(ref this.status,value)){
                this.OnVisualStateChanged();
            }
        }
    }

    public string Glyph =>
        this.Status switch{
        BenchmarkProjectRunStatus.Completed => "✓",
        BenchmarkProjectRunStatus.Failed => "X",
        BenchmarkProjectRunStatus.Canceled => "-",
        _ => string.Empty,
    };

    public double Opacity => this.IsProjectSelected ? 1.0 : 0.35;
    public Microsoft.UI.Xaml.Visibility RunningVisibility =>
        this.Status==BenchmarkProjectRunStatus.Running
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility GlyphVisibility =>
        string.IsNullOrEmpty(this.Glyph)
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

    private void OnVisualStateChanged(){
        this.OnPropertyChanged(nameof(this.Glyph));
        this.OnPropertyChanged(nameof(this.Opacity));
        this.OnPropertyChanged(nameof(this.RunningVisibility));
        this.OnPropertyChanged(nameof(this.GlyphVisibility));
    }
}

public sealed record BenchmarkProjectMethod(
    string TypeName,
    string TypeFullName,
    string MethodName,
    string DisplayName,
    string[] Categories){
    public string ExactFilter => string.IsNullOrWhiteSpace(this.TypeFullName)
        ? "*."+ this.MethodName
        : "*"+ this.TypeFullName+"."+ this.MethodName;
}

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
        this.Project=project;
        this.Method=method;
        this.ProjectMethod=projectMethod;
    }

    public BenchmarkProject Project { get; }
    public BenchmarkItem Method { get; }
    public BenchmarkProjectMethod ProjectMethod { get; }

    public string ProjectDisplayName => this.Project.DisplayName;
    public string MethodName => this.Method.MethodName;

    public BenchmarkRunStatus Status{
        get => this.status;
        set{
            if(this.SetProperty(ref this.status,value)){
                this.OnPropertyChanged(nameof(this.StatusText));
            }
        }
    }

    public CpuCoreInfo? AssignedCpu{
        get => this.assignedCpu;
        set{
            if(this.SetProperty(ref this.assignedCpu,value)){
                this.OnPropertyChanged(nameof(this.CpuDisplay));
            }
        }
    }

    public int ProcessId{
        get => this.processId;
        set{
            if(this.SetProperty(ref this.processId,value)){
                this.OnPropertyChanged(nameof(this.ProcessIdText));
            }
        }
    }

    public int Attempt{
        get => this.attempt;
        set => this.SetProperty(ref this.attempt,value);
    }

    public string? ArtifactFolder{
        get => this.artifactFolder;
        set => this.SetProperty(ref this.artifactFolder,value);
    }

    public string? Mean{
        get => this.mean;
        set => this.SetProperty(ref this.mean,value);
    }

    public string? Error{
        get => this.error;
        set => this.SetProperty(ref this.error,value);
    }

    public string? StdDev{
        get => this.stdDev;
        set => this.SetProperty(ref this.stdDev,value);
    }

    public int? ExitCode{
        get => this.exitCode;
        set => this.SetProperty(ref this.exitCode,value);
    }

    public DateTimeOffset? StartedAt{
        get => this.startedAt;
        set => this.SetProperty(ref this.startedAt,value);
    }

    public DateTimeOffset? FinishedAt{
        get => this.finishedAt;
        set => this.SetProperty(ref this.finishedAt,value);
    }

    public string StatusText => this.Status.ToString();
    public string CpuDisplay => this.AssignedCpu?.CpuDisplayText??"-";
    public string ProcessIdText => this.ProcessId==0?"-": this.ProcessId.ToString();
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
    string ElementScale,
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

public static class BenchmarkText{
    private static readonly Regex invalidPathChars = new("["+Regex.Escape(new string(Path.GetInvalidFileNameChars()))+"]",RegexOptions.Compiled);

    public static string SanitizePathPart(string value){
        var sanitized = invalidPathChars.Replace(value,"_").Trim();
        return string.IsNullOrWhiteSpace(sanitized)?"_":sanitized;
    }
}
