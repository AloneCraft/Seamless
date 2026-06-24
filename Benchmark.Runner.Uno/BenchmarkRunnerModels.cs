using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Media;

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
    private string timeHeaderText;
    private string allocatedHeaderText;

    public BenchmarkProject(string displayName,Type benchmarkType,bool isSelected){
        this.DisplayName=displayName;
        this.BenchmarkType=benchmarkType;
        this.AssemblyName=benchmarkType.Assembly.GetName().Name??benchmarkType.Assembly.FullName??string.Empty;
        this.TypeFullName=benchmarkType.FullName??benchmarkType.Name;
        this.isSelected=isSelected;
        this.timeHeaderText="時間";
        this.allocatedHeaderText="割当";
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

    public double ColumnOpacity => this.IsSelected ? 1.0 : 0.35;

    public string TimeHeaderText{
        get => this.timeHeaderText;
        set => this.SetProperty(ref this.timeHeaderText,value);
    }

    public string AllocatedHeaderText{
        get => this.allocatedHeaderText;
        set => this.SetProperty(ref this.allocatedHeaderText,value);
    }
}

public sealed class BenchmarkItem:BindableObject{
    private bool isSelected;

    public BenchmarkItem(
        string methodName,
        string displayName,
        string[] categories,
        IReadOnlyDictionary<string,BenchmarkProjectMethod> projectMethods,
        IReadOnlyList<BenchmarkTableColumn> displayColumns,
        IEnumerable<BenchmarkProject> projectColumns){
        this.MethodName=methodName;
        this.DisplayName=displayName;
        this.Categories=categories;
        this.ProjectMethods=projectMethods;
        this.CategoryText=categories.Length==0?"-":string.Join(", ",categories);
        this.ProjectStatuses=new ObservableCollection<BenchmarkProjectStatusCell>(
            projectColumns.Select(static project => new BenchmarkProjectStatusCell(project.ProjectKey,project.DisplayName,project.IsSelected)));
        this.DisplayCells=new ObservableCollection<BenchmarkDisplayCell>(
            displayColumns.Select(column => new BenchmarkDisplayCell(column)));
        this.RefreshDisplayCells();
    }

    public string MethodName { get; }
    public string DisplayName { get; }
    public string[] Categories { get; }
    public string CategoryText { get; }
    public IReadOnlyDictionary<string,BenchmarkProjectMethod> ProjectMethods { get; }
    public ObservableCollection<BenchmarkProjectStatusCell> ProjectStatuses { get; }
    public ObservableCollection<BenchmarkDisplayCell> DisplayCells { get; }

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.OnPropertyChanged(nameof(this.CheckGlyph));
            }
        }
    }

    public string CheckGlyph => this.IsSelected?"✓":string.Empty;

    public void SetProjectSelected(string projectKey,bool isProjectSelected){
        var cell = this.ProjectStatuses.FirstOrDefault(cell => string.Equals(cell.ProjectKey,projectKey,StringComparison.Ordinal));
        if(cell is not null){
            cell.IsProjectSelected=isProjectSelected;
            if(!isProjectSelected){
                cell.Status=BenchmarkProjectRunStatus.NotSelected;
                cell.ClearResult();
            }
        }

        this.RefreshDisplayCells();
    }

    public void ResetProjectStatuses(IEnumerable<BenchmarkProject> projects){
        var selectedProjects = projects.ToDictionary(static project => project.ProjectKey,static project => project.IsSelected,StringComparer.Ordinal);
        foreach(var cell in this.ProjectStatuses){
            cell.IsProjectSelected=selectedProjects.TryGetValue(cell.ProjectKey,out var selected)&&selected;
            cell.Status=cell.IsProjectSelected
                ? BenchmarkProjectRunStatus.Pending
                : BenchmarkProjectRunStatus.NotSelected;
            cell.ClearResult();
        }

        this.RefreshDisplayCells();
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
        this.RefreshDisplayCells();
    }

    public void RefreshDisplayCells(){
        var byProject = this.ProjectStatuses.ToDictionary(static cell => cell.DisplayName,StringComparer.OrdinalIgnoreCase);
        foreach(var cell in this.DisplayCells){
            if(cell.Column.Kind==BenchmarkTableColumnKind.Project){
                cell.SetProjectStatus(byProject.GetValueOrDefault(cell.Column.ProjectName));
            }else{
                cell.SetRatioStatus(
                    byProject.GetValueOrDefault(cell.Column.LeftProjectName),
                    byProject.GetValueOrDefault(cell.Column.RightProjectName));
            }
        }
    }
}

public enum BenchmarkTableColumnKind{
    Ratio,
    Project,
}

public sealed class BenchmarkTableColumn:BindableObject{
    private string timeHeaderText;
    private string allocatedHeaderText;

    public BenchmarkTableColumn(
        string key,
        BenchmarkTableColumnKind kind,
        string headerText,
        string tooltipText,
        string? projectName = null,
        string? leftProjectName = null,
        string? rightProjectName = null){
        this.Key=key;
        this.Kind=kind;
        this.HeaderText=headerText;
        this.TooltipText=tooltipText;
        this.ProjectName=projectName??string.Empty;
        this.LeftProjectName=leftProjectName??string.Empty;
        this.RightProjectName=rightProjectName??string.Empty;
        this.timeHeaderText="時間";
        this.allocatedHeaderText="割当";
    }

    public string Key { get; }
    public BenchmarkTableColumnKind Kind { get; }
    public string HeaderText { get; }
    public string TooltipText { get; }
    public string ProjectName { get; }
    public string LeftProjectName { get; }
    public string RightProjectName { get; }

    public string TimeHeaderText{
        get => this.timeHeaderText;
        set => this.SetProperty(ref this.timeHeaderText,value);
    }

    public string AllocatedHeaderText{
        get => this.allocatedHeaderText;
        set => this.SetProperty(ref this.allocatedHeaderText,value);
    }
}

public sealed class BenchmarkDisplayCell:BindableObject{
    private string timeDisplay = string.Empty;
    private string timeFullText = string.Empty;
    private double? timeSortValue;
    private string allocatedDisplay = string.Empty;
    private string allocatedFullText = string.Empty;
    private double? allocatedSortValue;
    private BenchmarkProjectRunStatus status = BenchmarkProjectRunStatus.Pending;
    private bool isProjectSelected = true;

    public BenchmarkDisplayCell(BenchmarkTableColumn column){
        this.Column=column;
    }

    public BenchmarkTableColumn Column { get; }
    public string ColumnKey => this.Column.Key;

    public string TimeDisplay{
        get => this.timeDisplay;
        private set => this.SetProperty(ref this.timeDisplay,value);
    }

    public string TimeFullText{
        get => this.timeFullText;
        private set => this.SetProperty(ref this.timeFullText,value);
    }

    public double? TimeSortValue{
        get => this.timeSortValue;
        private set => this.SetProperty(ref this.timeSortValue,value);
    }

    public string AllocatedDisplay{
        get => this.allocatedDisplay;
        private set => this.SetProperty(ref this.allocatedDisplay,value);
    }

    public string AllocatedFullText{
        get => this.allocatedFullText;
        private set => this.SetProperty(ref this.allocatedFullText,value);
    }

    public double? AllocatedSortValue{
        get => this.allocatedSortValue;
        private set => this.SetProperty(ref this.allocatedSortValue,value);
    }

    public BenchmarkProjectRunStatus Status{
        get => this.status;
        private set{
            if(this.SetProperty(ref this.status,value)){
                this.OnVisualStateChanged();
            }
        }
    }

    public bool IsProjectSelected{
        get => this.isProjectSelected;
        private set{
            if(this.SetProperty(ref this.isProjectSelected,value)){
                this.OnVisualStateChanged();
            }
        }
    }

    public bool IsRatio => this.Column.Kind==BenchmarkTableColumnKind.Ratio;
    public string Glyph => this.Status switch{
        BenchmarkProjectRunStatus.Completed => "✓",
        BenchmarkProjectRunStatus.Failed => "×",
        BenchmarkProjectRunStatus.Canceled => "-",
        _ => string.Empty,
    };
    public double Opacity => this.IsProjectSelected ? 1.0 : 0.35;
    public Visibility RunningVisibility => !this.IsRatio&&this.Status==BenchmarkProjectRunStatus.Running ? Visibility.Visible : Visibility.Collapsed;
    public Visibility GlyphVisibility => !this.IsRatio&&(this.Status==BenchmarkProjectRunStatus.Failed||this.Status==BenchmarkProjectRunStatus.Canceled)&&!string.IsNullOrEmpty(this.Glyph) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MetricsVisibility => this.IsRatio||!string.IsNullOrWhiteSpace(this.TimeDisplay)||!string.IsNullOrWhiteSpace(this.AllocatedDisplay) ? Visibility.Visible : Visibility.Collapsed;

    public void SetProjectStatus(BenchmarkProjectStatusCell? statusCell){
        if(statusCell is null){
            this.IsProjectSelected=false;
            this.Status=BenchmarkProjectRunStatus.NotSelected;
            this.SetMetrics(string.Empty,string.Empty,null,string.Empty,string.Empty,null);
            return;
        }

        this.IsProjectSelected=statusCell.IsProjectSelected;
        this.Status=statusCell.Status;
        this.SetMetrics(
            statusCell.TimeDisplay,
            statusCell.TimeFullText,
            statusCell.TimeSortValue,
            statusCell.AllocatedDisplay,
            statusCell.AllocatedFullText,
            statusCell.AllocatedSortValue);
    }

    public void SetRatioStatus(BenchmarkProjectStatusCell? left,BenchmarkProjectStatusCell? right){
        this.IsProjectSelected=(left?.IsProjectSelected??false)&&(right?.IsProjectSelected??false);
        this.Status=BenchmarkProjectRunStatus.Completed;
        var leftMetrics = CreateMetrics(left);
        var rightMetrics = CreateMetrics(right);
        var timeRatio = BenchmarkComparisonFormatter.CalculateTimeRatio(leftMetrics,rightMetrics);
        var allocatedRatio = BenchmarkComparisonFormatter.CalculateAllocatedRatio(leftMetrics,rightMetrics);
        this.SetMetrics(
            timeRatio.DisplayText,
            this.CreateRatioTooltip("時間",left?.TimeFullText,right?.TimeFullText,timeRatio),
            timeRatio.NumericValue,
            allocatedRatio.DisplayText,
            this.CreateRatioTooltip("割当",left?.AllocatedFullText,right?.AllocatedFullText,allocatedRatio),
            allocatedRatio.NumericValue);
    }

    private void SetMetrics(
        string timeDisplay,
        string timeFullText,
        double? timeSortValue,
        string allocatedDisplay,
        string allocatedFullText,
        double? allocatedSortValue){
        this.TimeDisplay=timeDisplay;
        this.TimeFullText=timeFullText;
        this.TimeSortValue=timeSortValue;
        this.AllocatedDisplay=allocatedDisplay;
        this.AllocatedFullText=allocatedFullText;
        this.AllocatedSortValue=allocatedSortValue;
        this.OnVisualStateChanged();
    }

    private string CreateRatioTooltip(string metric,string? left,string? right,RatioValue ratio){
        return $"{this.Column.TooltipText} {metric}: {ratio.DisplayText} ({left??"-"} / {right??"-"})";
    }

    private static ProjectMetrics CreateMetrics(BenchmarkProjectStatusCell? cell){
        if(cell is null||cell.Status!=BenchmarkProjectRunStatus.Completed){
            return ProjectMetrics.Missing;
        }

        return new ProjectMetrics(cell.TimeSortValue,cell.AllocatedSortValue is { } value ? (long)value : null,true);
    }

    private void OnVisualStateChanged(){
        this.OnPropertyChanged(nameof(this.Glyph));
        this.OnPropertyChanged(nameof(this.Opacity));
        this.OnPropertyChanged(nameof(this.RunningVisibility));
        this.OnPropertyChanged(nameof(this.GlyphVisibility));
        this.OnPropertyChanged(nameof(this.MetricsVisibility));
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
    private string timeDisplay = string.Empty;
    private string timeFullText = string.Empty;
    private double? timeSortValue;
    private string allocatedDisplay = string.Empty;
    private string allocatedFullText = string.Empty;
    private long? allocatedSortValue;

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

    public string Glyph => this.Status switch{
        BenchmarkProjectRunStatus.Completed => "✓",
        BenchmarkProjectRunStatus.Failed => "×",
        BenchmarkProjectRunStatus.Canceled => "-",
        _ => string.Empty,
    };

    public double Opacity => this.IsProjectSelected ? 1.0 : 0.35;

    public string TimeDisplay{
        get => this.timeDisplay;
        private set => this.SetProperty(ref this.timeDisplay,value);
    }

    public string TimeFullText{
        get => this.timeFullText;
        private set => this.SetProperty(ref this.timeFullText,value);
    }

    public double? TimeSortValue{
        get => this.timeSortValue;
        private set => this.SetProperty(ref this.timeSortValue,value);
    }

    public string AllocatedDisplay{
        get => this.allocatedDisplay;
        private set => this.SetProperty(ref this.allocatedDisplay,value);
    }

    public string AllocatedFullText{
        get => this.allocatedFullText;
        private set => this.SetProperty(ref this.allocatedFullText,value);
    }

    public long? AllocatedSortValue{
        get => this.allocatedSortValue;
        private set => this.SetProperty(ref this.allocatedSortValue,value);
    }

    public Visibility RunningVisibility => this.Status==BenchmarkProjectRunStatus.Running ? Visibility.Visible : Visibility.Collapsed;
    public Visibility GlyphVisibility => this.Status!=BenchmarkProjectRunStatus.Failed||string.IsNullOrEmpty(this.Glyph) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MetricsVisibility => this.Status is BenchmarkProjectRunStatus.Completed or BenchmarkProjectRunStatus.Canceled
        &&(!string.IsNullOrWhiteSpace(this.TimeDisplay)||!string.IsNullOrWhiteSpace(this.AllocatedDisplay))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public void SetResult(
        string timeDisplay,
        string timeFullText,
        double? timeSortValue,
        string allocatedDisplay,
        string allocatedFullText,
        long? allocatedSortValue){
        this.TimeDisplay=timeDisplay;
        this.TimeFullText=timeFullText;
        this.TimeSortValue=timeSortValue;
        this.AllocatedDisplay=allocatedDisplay;
        this.AllocatedFullText=allocatedFullText;
        this.AllocatedSortValue=allocatedSortValue;
        this.OnVisualStateChanged();
    }

    public void ClearResult(){
        this.SetResult(string.Empty,string.Empty,null,string.Empty,string.Empty,null);
    }

    private void OnVisualStateChanged(){
        this.OnPropertyChanged(nameof(this.Glyph));
        this.OnPropertyChanged(nameof(this.Opacity));
        this.OnPropertyChanged(nameof(this.RunningVisibility));
        this.OnPropertyChanged(nameof(this.GlyphVisibility));
        this.OnPropertyChanged(nameof(this.MetricsVisibility));
    }
}

public sealed record BenchmarkProjectMethod(
    string TypeName,
    string TypeFullName,
    string MethodName,
    string DisplayName,
    string[] Categories){
    public string ExactFilter => string.IsNullOrWhiteSpace(this.TypeFullName)
        ? "*."+this.MethodName
        : "*"+this.TypeFullName+"."+this.MethodName;
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
    private string? allocatedBytes;
    private string? peakRssBytes;
    private string? estimatedPeakRssBytes;
    private double? meanNanoseconds;
    private double? errorNanoseconds;
    private double? stdDevNanoseconds;
    private long? allocatedBytesValue;
    private long? peakRssBytesValue;
    private long? estimatedPeakRssBytesValue;
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

    public string? AllocatedBytes{
        get => this.allocatedBytes;
        set => this.SetProperty(ref this.allocatedBytes,value);
    }

    public string? PeakRssBytes{
        get => this.peakRssBytes;
        set => this.SetProperty(ref this.peakRssBytes,value);
    }

    public string? EstimatedPeakRssBytes{
        get => this.estimatedPeakRssBytes;
        set => this.SetProperty(ref this.estimatedPeakRssBytes,value);
    }

    public double? MeanNanoseconds{
        get => this.meanNanoseconds;
        set => this.SetProperty(ref this.meanNanoseconds,value);
    }

    public double? ErrorNanoseconds{
        get => this.errorNanoseconds;
        set => this.SetProperty(ref this.errorNanoseconds,value);
    }

    public double? StdDevNanoseconds{
        get => this.stdDevNanoseconds;
        set => this.SetProperty(ref this.stdDevNanoseconds,value);
    }

    public long? AllocatedBytesValue{
        get => this.allocatedBytesValue;
        set => this.SetProperty(ref this.allocatedBytesValue,value);
    }

    public long? PeakRssBytesValue{
        get => this.peakRssBytesValue;
        set => this.SetProperty(ref this.peakRssBytesValue,value);
    }

    public long? EstimatedPeakRssBytesValue{
        get => this.estimatedPeakRssBytesValue;
        set => this.SetProperty(ref this.estimatedPeakRssBytesValue,value);
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

    public string StatusText => this.Status switch{
        BenchmarkRunStatus.Pending => "待機",
        BenchmarkRunStatus.Running => "実行中",
        BenchmarkRunStatus.Completed => "完了",
        BenchmarkRunStatus.Failed => "失敗",
        BenchmarkRunStatus.Canceled => "中止",
        _ => this.Status.ToString(),
    };
    public string CpuDisplay => this.AssignedCpu?.CpuDisplayText??"-";
    public string ProcessIdText => this.ProcessId==0?"-":this.ProcessId.ToString();
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
    double? MeanNanoseconds,
    double? ErrorNanoseconds,
    double? StdDevNanoseconds,
    long? AllocatedBytes,
    int ExitCode,
    string ArtifactFolder,
    string Log);

public sealed record AnalysisRankingItem(
    string Benchmark,
    string PairKey,
    string DisplayText,
    double BarWidth,
    Brush BarBrush,
    Brush BorderBrush,
    Thickness BorderThickness);

public sealed record AnalysisHeatmapCell(
    string Benchmark,
    string PairKey,
    string DisplayText,
    Brush Background);

public sealed record AnalysisHeatmapRow(
    string Benchmark,
    AnalysisHeatmapCell SystemCell,
    AnalysisHeatmapCell PersistedCell,
    AnalysisHeatmapCell TsavoriteCell,
    AnalysisHeatmapCell PersistedLinqTsavoriteLinqCell,
    AnalysisHeatmapCell PersistedFLinqTsavoriteFLinqCell){
    public string SystemDisplay => this.SystemCell.DisplayText;
    public string PersistedDisplay => this.PersistedCell.DisplayText;
    public string TsavoriteDisplay => this.TsavoriteCell.DisplayText;
    public string PersistedLinqTsavoriteLinqDisplay => this.PersistedLinqTsavoriteLinqCell.DisplayText;
    public string PersistedFLinqTsavoriteFLinqDisplay => this.PersistedFLinqTsavoriteFLinqCell.DisplayText;
}

public sealed record AnalysisDetailItem(
    string PairName,
    string TimeRatio,
    string AllocatedRatio,
    string LeftValue,
    string RightValue);

public static class BenchmarkText{
    private static readonly Regex invalidPathChars = new("["+Regex.Escape(new string(Path.GetInvalidFileNameChars()))+"]",RegexOptions.Compiled);

    public static string SanitizePathPart(string value){
        var sanitized = invalidPathChars.Replace(value,"_").Trim();
        return string.IsNullOrWhiteSpace(sanitized)?"_":sanitized;
    }
}
