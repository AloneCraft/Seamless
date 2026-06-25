using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark;

public enum BenchmarkTab{
    Cpu,
    Chart,
}

public enum BenchmarkMetricKind{
    Time,
    Allocated,
}

public enum BenchmarkChartMode{
    ElementComparison,
    ScaleTrend,
}

public enum BenchmarkGridSortColumn{
    BenchmarkName,
    SystemRatioTime,
    SystemRatioAllocated,
    SystemFLinqTime,
    SystemFLinqAllocated,
    SystemZLinqTime,
    SystemZLinqAllocated,
    PersistedRatioTime,
    PersistedRatioAllocated,
    PersistedFLinqTime,
    PersistedFLinqAllocated,
    PersistedLinqTime,
    PersistedLinqAllocated,
    TsavoriteRatioTime,
    TsavoriteRatioAllocated,
    TsavoriteFLinqTime,
    TsavoriteFLinqAllocated,
    TsavoriteLinqTime,
    TsavoriteLinqAllocated,
}

public sealed record ComparisonPairOption(string Key,string DisplayName,string LeftProject,string RightProject);

public sealed record ScaleOption(string DisplayName,string RunValue);

public sealed class ElementCountOption:ObservableObject{
    private bool isSelected;

    public ElementCountOption(int value,bool isSelected){
        this.Value=value;
        this.DisplayName="N="+value.ToString("N0",CultureInfo.InvariantCulture);
        this.isSelected=isSelected;
    }

    public int Value { get; }
    public string DisplayName { get; }

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }
}

public sealed record RunModeOption(string DisplayName,BenchmarkRunMode Mode);

public sealed record MetricOption(string DisplayName,BenchmarkMetricKind Kind);

public sealed record ChartModeOption(string DisplayName,BenchmarkChartMode Mode);

public sealed record ResultElementCountOption(int Value,string DisplayName);

public sealed record ThresholdOption(string DisplayName,int Milliseconds);

public readonly record struct BenchmarkSortValue(string? Text,double? Number,bool IsMissing){
    public static BenchmarkSortValue FromText(string text) => new(text,null,string.IsNullOrWhiteSpace(text));

    public static BenchmarkSortValue FromMetric(ProjectMetrics metrics,BenchmarkMetricKind kind){
        if(!metrics.HasValue){
            return Missing;
        }

        return kind==BenchmarkMetricKind.Time
            ? FromNumber(metrics.TimeNanoseconds)
            : FromNumber(metrics.AllocatedBytes);
    }

    public static BenchmarkSortValue FromRatio(RatioValue ratio){
        return ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => FromNumber(value),
            RatioValueKind.BothZero => FromNumber(0),
            RatioValueKind.LeftOnly => FromNumber(double.PositiveInfinity),
            RatioValueKind.RightOnly => FromNumber(0),
            _ => Missing,
        };
    }

    public static BenchmarkSortValue FromNumber(double? value){
        return value is { } number&&double.IsFinite(number)
            ? new(null,number,false)
            : Missing;
    }

    public static readonly BenchmarkSortValue Missing = new(null,null,true);

    public static int Compare(BenchmarkSortValue left,BenchmarkSortValue right,bool descending){
        if(left.IsMissing&&right.IsMissing){
            return 0;
        }

        if(left.IsMissing){
            return 1;
        }

        if(right.IsMissing){
            return -1;
        }

        var result = left.Number is { } leftNumber&&right.Number is { } rightNumber
            ? leftNumber.CompareTo(rightNumber)
            : string.Compare(left.Text,right.Text,StringComparison.OrdinalIgnoreCase);
        return descending ? -result : result;
    }
}

public sealed class BenchmarkProjectCell:ObservableObject{
    private bool isSelected;
    private string statusIcon;
    private string statusText;
    private string executionText = string.Empty;
    private string timeText = string.Empty;
    private string allocatedText = string.Empty;
    private bool isRunning;
    private DateTimeOffset? lastMeasuredAt;
    private ProjectMetrics metrics = ProjectMetrics.Missing;

    public BenchmarkProjectCell(string project,bool hasCase){
        this.Project=project;
        this.HasCase=hasCase;
        this.isSelected=hasCase;
        this.statusIcon=hasCase?"○":"-";
        this.statusText=hasCase?"未実行":"対象なし";
    }

    public string Project { get; }
    public bool HasCase { get; }
    public ProjectMetrics Metrics => this.metrics;
    public DateTimeOffset? LastMeasuredAt => this.lastMeasuredAt;

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,this.HasCase&&value);
    }

    public string StatusIcon{
        get => this.statusIcon;
        private set => this.SetProperty(ref this.statusIcon,value);
    }

    public string StatusText{
        get => this.statusText;
        private set => this.SetProperty(ref this.statusText,value);
    }

    public string ExecutionText{
        get => this.executionText;
        private set => this.SetProperty(ref this.executionText,value);
    }

    public string TimeText{
        get => this.timeText;
        private set => this.SetProperty(ref this.timeText,value);
    }

    public string AllocatedText{
        get => this.allocatedText;
        private set => this.SetProperty(ref this.allocatedText,value);
    }

    public bool IsRunning{
        get => this.isRunning;
        private set => this.SetProperty(ref this.isRunning,value);
    }

    public void ApplyMetrics(ProjectMetrics value){
        this.metrics=value;
        this.TimeText=FormatTime(value);
        this.AllocatedText=FormatAllocated(value);
        if(value.HasValue&&!this.IsRunning){
            this.StatusIcon="✓";
            this.StatusText="完了";
        }
    }

    public void ApplyStoredResult(BenchmarkStoredResult result){
        this.lastMeasuredAt=result.MeasuredAt;
        this.ApplyMetrics(result.ToProjectMetrics());
        this.IsRunning=false;
        if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
            this.StatusIcon="✓";
            this.StatusText="完了";
        }else if(string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
            this.StatusIcon="×";
            this.StatusText="失敗";
        }else{
            this.StatusIcon="○";
            this.StatusText=result.Status;
        }
        this.ExecutionText=result.SourceCsvAt is { } csvAt
            ? "CSV "+csvAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)
            : result.MeasuredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
    }

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu){
        this.lastMeasuredAt=DateTimeOffset.Now;
        this.TimeText=string.IsNullOrWhiteSpace(result.Mean) ? this.TimeText : result.Mean;
        this.AllocatedText=string.IsNullOrWhiteSpace(result.AllocatedBytes) ? this.AllocatedText : result.AllocatedBytes;
        this.IsRunning=string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        this.StatusIcon=this.IsRunning
            ? "⟳"
            : string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "✓"
                : string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "×"
                    : "○";
        this.StatusText=this.IsRunning
            ? "実行中"
            : string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "完了"
                : string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "失敗"
                    : result.Status;
        this.ExecutionText=cpu is null
            ? "N="+result.ElementCount.ToString(CultureInfo.InvariantCulture)
            : "CPU "+cpu.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture)+" "+cpu.CoreKindLabel+" N="+result.ElementCount.ToString(CultureInfo.InvariantCulture);
        if(result.MeanNanoseconds is not null||result.AllocatedBytesValue is not null){
            this.metrics=new ProjectMetrics(result.MeanNanoseconds,result.AllocatedBytesValue,string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase));
        }
    }

    public void ClearMetrics(){
        this.metrics=ProjectMetrics.Missing;
        this.TimeText=string.Empty;
        this.AllocatedText=string.Empty;
        if(this.HasCase){
            this.StatusIcon="○";
            this.StatusText="未実行";
            this.ExecutionText=string.Empty;
        }
    }

    private static string FormatTime(ProjectMetrics metrics){
        return metrics.HasValue&&metrics.TimeNanoseconds is { } value
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : string.Empty;
    }

    private static string FormatAllocated(ProjectMetrics metrics){
        return metrics.HasValue&&metrics.AllocatedBytes is { } value
            ? BenchmarkFormatting.FormatBytes(value)
            : string.Empty;
    }
}

public sealed class BenchmarkColumnSelectionGroup:ObservableObject{
    private bool? isChecked;

    public BenchmarkColumnSelectionGroup(string headerText,IReadOnlyList<string> columnKeys){
        this.HeaderText=headerText;
        this.ColumnKeys=columnKeys;
    }

    public string HeaderText { get; }
    public IReadOnlyList<string> ColumnKeys { get; }

    public bool? IsChecked{
        get => this.isChecked;
        set => this.SetProperty(ref this.isChecked,value);
    }
}

public sealed class BenchmarkTableRow:ObservableObject{
    private bool isSelected;
    private bool? isSelectionChecked;
    private string systemRatioTimeText = string.Empty;
    private string systemRatioAllocatedText = string.Empty;
    private string systemFLinqTimeText = string.Empty;
    private string systemFLinqAllocatedText = string.Empty;
    private string systemZLinqTimeText = string.Empty;
    private string systemZLinqAllocatedText = string.Empty;
    private string persistedRatioTimeText = string.Empty;
    private string persistedRatioAllocatedText = string.Empty;
    private string persistedFLinqTimeText = string.Empty;
    private string persistedFLinqAllocatedText = string.Empty;
    private string persistedLinqTimeText = string.Empty;
    private string persistedLinqAllocatedText = string.Empty;
    private string tsavoriteRatioTimeText = string.Empty;
    private string tsavoriteRatioAllocatedText = string.Empty;
    private string tsavoriteFLinqTimeText = string.Empty;
    private string tsavoriteFLinqAllocatedText = string.Empty;
    private string tsavoriteLinqTimeText = string.Empty;
    private string tsavoriteLinqAllocatedText = string.Empty;
    private BenchmarkComparisonRow? comparisonRow;

    public BenchmarkTableRow(string methodName,IReadOnlyDictionary<string,BenchmarkCase> casesByProject){
        this.MethodName=methodName;
        this.BenchmarkName=methodName;
        this.CasesByProject=casesByProject;
        this.SystemFLinqCell=this.CreateCell(BenchmarkComparisonFormatter.SystemFLinq);
        this.SystemZLinqCell=this.CreateCell(BenchmarkComparisonFormatter.SystemZLinq);
        this.PersistedFLinqCell=this.CreateCell(BenchmarkComparisonFormatter.PersistedFLinq);
        this.PersistedLinqCell=this.CreateCell(BenchmarkComparisonFormatter.PersistedLinq);
        this.TsavoriteFLinqCell=this.CreateCell(BenchmarkComparisonFormatter.TsavoriteFLinq);
        this.TsavoriteLinqCell=this.CreateCell(BenchmarkComparisonFormatter.TsavoriteLinq);
    }

    public string MethodName { get; }
    public string BenchmarkName { get; }
    public IReadOnlyDictionary<string,BenchmarkCase> CasesByProject { get; }
    public BenchmarkProjectCell SystemFLinqCell { get; }
    public BenchmarkProjectCell SystemZLinqCell { get; }
    public BenchmarkProjectCell PersistedFLinqCell { get; }
    public BenchmarkProjectCell PersistedLinqCell { get; }
    public BenchmarkProjectCell TsavoriteFLinqCell { get; }
    public BenchmarkProjectCell TsavoriteLinqCell { get; }

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }

    public bool? IsSelectionChecked{
        get => this.isSelectionChecked;
        private set => this.SetProperty(ref this.isSelectionChecked,value);
    }

    public string SystemRatioTimeText{
        get => this.systemRatioTimeText;
        private set => this.SetProperty(ref this.systemRatioTimeText,value);
    }

    public string SystemRatioAllocatedText{
        get => this.systemRatioAllocatedText;
        private set => this.SetProperty(ref this.systemRatioAllocatedText,value);
    }

    public string SystemFLinqTimeText{
        get => this.systemFLinqTimeText;
        private set => this.SetProperty(ref this.systemFLinqTimeText,value);
    }

    public string SystemFLinqAllocatedText{
        get => this.systemFLinqAllocatedText;
        private set => this.SetProperty(ref this.systemFLinqAllocatedText,value);
    }

    public string SystemZLinqTimeText{
        get => this.systemZLinqTimeText;
        private set => this.SetProperty(ref this.systemZLinqTimeText,value);
    }

    public string SystemZLinqAllocatedText{
        get => this.systemZLinqAllocatedText;
        private set => this.SetProperty(ref this.systemZLinqAllocatedText,value);
    }

    public string PersistedRatioTimeText{
        get => this.persistedRatioTimeText;
        private set => this.SetProperty(ref this.persistedRatioTimeText,value);
    }

    public string PersistedRatioAllocatedText{
        get => this.persistedRatioAllocatedText;
        private set => this.SetProperty(ref this.persistedRatioAllocatedText,value);
    }

    public string PersistedFLinqTimeText{
        get => this.persistedFLinqTimeText;
        private set => this.SetProperty(ref this.persistedFLinqTimeText,value);
    }

    public string PersistedFLinqAllocatedText{
        get => this.persistedFLinqAllocatedText;
        private set => this.SetProperty(ref this.persistedFLinqAllocatedText,value);
    }

    public string PersistedLinqTimeText{
        get => this.persistedLinqTimeText;
        private set => this.SetProperty(ref this.persistedLinqTimeText,value);
    }

    public string PersistedLinqAllocatedText{
        get => this.persistedLinqAllocatedText;
        private set => this.SetProperty(ref this.persistedLinqAllocatedText,value);
    }

    public string TsavoriteRatioTimeText{
        get => this.tsavoriteRatioTimeText;
        private set => this.SetProperty(ref this.tsavoriteRatioTimeText,value);
    }

    public string TsavoriteRatioAllocatedText{
        get => this.tsavoriteRatioAllocatedText;
        private set => this.SetProperty(ref this.tsavoriteRatioAllocatedText,value);
    }

    public string TsavoriteFLinqTimeText{
        get => this.tsavoriteFLinqTimeText;
        private set => this.SetProperty(ref this.tsavoriteFLinqTimeText,value);
    }

    public string TsavoriteFLinqAllocatedText{
        get => this.tsavoriteFLinqAllocatedText;
        private set => this.SetProperty(ref this.tsavoriteFLinqAllocatedText,value);
    }

    public string TsavoriteLinqTimeText{
        get => this.tsavoriteLinqTimeText;
        private set => this.SetProperty(ref this.tsavoriteLinqTimeText,value);
    }

    public string TsavoriteLinqAllocatedText{
        get => this.tsavoriteLinqAllocatedText;
        private set => this.SetProperty(ref this.tsavoriteLinqAllocatedText,value);
    }

    public IReadOnlyList<BenchmarkCase> GetRunCases(){
        return this.ProjectCells
            .Where(static cell => cell.HasCase&&cell.IsSelected)
            .Select(cell => this.CasesByProject[cell.Project])
            .ToArray();
    }

    public IEnumerable<BenchmarkProjectCell> ProjectCells{
        get{
            yield return this.SystemFLinqCell;
            yield return this.SystemZLinqCell;
            yield return this.PersistedFLinqCell;
            yield return this.PersistedLinqCell;
            yield return this.TsavoriteFLinqCell;
            yield return this.TsavoriteLinqCell;
        }
    }

    public BenchmarkProjectCell? GetProjectCell(string project) =>
        this.ProjectCells.FirstOrDefault(cell => string.Equals(cell.Project,project,StringComparison.OrdinalIgnoreCase));

    public void RefreshSelectionState(){
        var state = GetSelectionState(this.ProjectCells);
        this.IsSelectionChecked=state;
        this.IsSelected=state is true or null;
    }

    private static bool? GetSelectionState(IEnumerable<BenchmarkProjectCell> cells){
        var targets = cells.Where(static cell => cell.HasCase).ToArray();
        if(targets.Length==0){
            return false;
        }

        var selected = targets.Count(static cell => cell.IsSelected);
        return selected==0
            ? false
            : selected==targets.Length
                ? true
                : null;
    }

    public void ApplyStoredResult(BenchmarkStoredResult result){
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplyStoredResult(result);
        }
    }

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu){
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplyRunResult(result,cpu);
        }
    }

    public void ApplyComparison(BenchmarkComparisonRow? row){
        this.comparisonRow=row;
        if(row is null){
            this.ClearComparison();
            return;
        }

        var value = row.Value;
        this.SystemRatioTimeText=FormatRatio(value.SystemRatio.TimeRatio);
        this.SystemRatioAllocatedText=FormatRatio(value.SystemRatio.AllocatedRatio);
        this.SystemFLinqTimeText=FormatTime(value.SystemFLinq);
        this.SystemFLinqAllocatedText=FormatAllocated(value.SystemFLinq);
        this.SystemFLinqCell.ApplyMetrics(value.SystemFLinq);
        this.SystemZLinqTimeText=FormatTime(value.SystemZLinq);
        this.SystemZLinqAllocatedText=FormatAllocated(value.SystemZLinq);
        this.SystemZLinqCell.ApplyMetrics(value.SystemZLinq);
        this.PersistedRatioTimeText=FormatRatio(value.PersistedRatio.TimeRatio);
        this.PersistedRatioAllocatedText=FormatRatio(value.PersistedRatio.AllocatedRatio);
        this.PersistedFLinqTimeText=FormatTime(value.PersistedFLinq);
        this.PersistedFLinqAllocatedText=FormatAllocated(value.PersistedFLinq);
        this.PersistedFLinqCell.ApplyMetrics(value.PersistedFLinq);
        this.PersistedLinqTimeText=FormatTime(value.PersistedLinq);
        this.PersistedLinqAllocatedText=FormatAllocated(value.PersistedLinq);
        this.PersistedLinqCell.ApplyMetrics(value.PersistedLinq);
        this.TsavoriteRatioTimeText=FormatRatio(value.TsavoriteRatio.TimeRatio);
        this.TsavoriteRatioAllocatedText=FormatRatio(value.TsavoriteRatio.AllocatedRatio);
        this.TsavoriteFLinqTimeText=FormatTime(value.TsavoriteFLinq);
        this.TsavoriteFLinqAllocatedText=FormatAllocated(value.TsavoriteFLinq);
        this.TsavoriteFLinqCell.ApplyMetrics(value.TsavoriteFLinq);
        this.TsavoriteLinqTimeText=FormatTime(value.TsavoriteLinq);
        this.TsavoriteLinqAllocatedText=FormatAllocated(value.TsavoriteLinq);
        this.TsavoriteLinqCell.ApplyMetrics(value.TsavoriteLinq);
    }

    public BenchmarkSortValue GetSortValue(BenchmarkGridSortColumn column){
        var row = this.comparisonRow;
        return column switch{
            BenchmarkGridSortColumn.BenchmarkName => BenchmarkSortValue.FromText(this.BenchmarkName),
            BenchmarkGridSortColumn.SystemRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.SystemRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.SystemRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemFLinqTime => row is { } value ? BenchmarkSortValue.FromMetric(value.SystemFLinq,BenchmarkMetricKind.Time) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemFLinqAllocated => row is { } value ? BenchmarkSortValue.FromMetric(value.SystemFLinq,BenchmarkMetricKind.Allocated) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemZLinqTime => row is { } value ? BenchmarkSortValue.FromMetric(value.SystemZLinq,BenchmarkMetricKind.Time) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemZLinqAllocated => row is { } value ? BenchmarkSortValue.FromMetric(value.SystemZLinq,BenchmarkMetricKind.Allocated) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.PersistedRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.PersistedRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedFLinqTime => row is { } value ? BenchmarkSortValue.FromMetric(value.PersistedFLinq,BenchmarkMetricKind.Time) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedFLinqAllocated => row is { } value ? BenchmarkSortValue.FromMetric(value.PersistedFLinq,BenchmarkMetricKind.Allocated) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedLinqTime => row is { } value ? BenchmarkSortValue.FromMetric(value.PersistedLinq,BenchmarkMetricKind.Time) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedLinqAllocated => row is { } value ? BenchmarkSortValue.FromMetric(value.PersistedLinq,BenchmarkMetricKind.Allocated) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.TsavoriteRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.TsavoriteRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteFLinqTime => row is { } value ? BenchmarkSortValue.FromMetric(value.TsavoriteFLinq,BenchmarkMetricKind.Time) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteFLinqAllocated => row is { } value ? BenchmarkSortValue.FromMetric(value.TsavoriteFLinq,BenchmarkMetricKind.Allocated) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteLinqTime => row is { } value ? BenchmarkSortValue.FromMetric(value.TsavoriteLinq,BenchmarkMetricKind.Time) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteLinqAllocated => row is { } value ? BenchmarkSortValue.FromMetric(value.TsavoriteLinq,BenchmarkMetricKind.Allocated) : BenchmarkSortValue.Missing,
            _ => BenchmarkSortValue.Missing,
        };
    }

    private void ClearComparison(){
        this.SystemRatioTimeText=string.Empty;
        this.SystemRatioAllocatedText=string.Empty;
        this.SystemFLinqTimeText=string.Empty;
        this.SystemFLinqAllocatedText=string.Empty;
        this.SystemZLinqTimeText=string.Empty;
        this.SystemZLinqAllocatedText=string.Empty;
        this.PersistedRatioTimeText=string.Empty;
        this.PersistedRatioAllocatedText=string.Empty;
        this.PersistedFLinqTimeText=string.Empty;
        this.PersistedFLinqAllocatedText=string.Empty;
        this.PersistedLinqTimeText=string.Empty;
        this.PersistedLinqAllocatedText=string.Empty;
        this.TsavoriteRatioTimeText=string.Empty;
        this.TsavoriteRatioAllocatedText=string.Empty;
        this.TsavoriteFLinqTimeText=string.Empty;
        this.TsavoriteFLinqAllocatedText=string.Empty;
        this.TsavoriteLinqTimeText=string.Empty;
        this.TsavoriteLinqAllocatedText=string.Empty;
        foreach(var cell in this.ProjectCells){
            cell.ClearMetrics();
        }
    }

    private BenchmarkProjectCell CreateCell(string project) =>
        new(project,this.CasesByProject.ContainsKey(project));

    private static string FormatTime(ProjectMetrics metrics){
        return metrics.HasValue&&metrics.TimeNanoseconds is { } value
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : string.Empty;
    }

    private static string FormatAllocated(ProjectMetrics metrics){
        return metrics.HasValue&&metrics.AllocatedBytes is { } value
            ? BenchmarkFormatting.FormatBytes(value)
            : string.Empty;
    }

    private static string FormatRatio(RatioValue ratio){
        return ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => value.ToString("0.00",CultureInfo.InvariantCulture),
            RatioValueKind.BothZero => "0",
            RatioValueKind.LeftOnly => "inf",
            RatioValueKind.RightOnly => "0",
            _ => string.Empty,
        };
    }
}

public sealed class CpuSelectionItem:ObservableObject{
    private bool isSelected;
    private string runStatusText = "待機";
    private string runIcon = "○";
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
    public string DisplayName => "CPU "+this.LogicalProcessorIndex+" ("+this.CoreLabel+")";
    public string RunStatusText{
        get => this.runStatusText;
        private set => this.SetProperty(ref this.runStatusText,value);
    }
    public string RunIcon{
        get => this.runIcon;
        private set => this.SetProperty(ref this.runIcon,value);
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
        +" "+this.RunIcon
        +(string.IsNullOrWhiteSpace(this.CurrentJobText) ? string.Empty : " "+this.CurrentJobText);

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }

    public void ApplyJob(BenchmarkResultItem item){
        this.IsRunningJob=string.Equals(item.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        this.RunIcon=this.IsRunningJob
            ? "⟳"
            : string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "✓"
                : string.Equals(item.Status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "×"
                    : "○";
        this.RunStatusText=this.IsRunningJob
            ? "実行中"
            : string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "完了"
                : string.Equals(item.Status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "失敗"
                    : item.Status;
        this.CurrentJobText=item.Project+" / "+item.Method+" / N="+item.ElementCount.ToString(CultureInfo.InvariantCulture);
        this.OnPropertyChanged(nameof(this.StatusDisplayText));
    }

    public void ClearJob(){
        this.IsRunningJob=false;
        this.RunIcon="○";
        this.RunStatusText="待機";
        this.CurrentJobText=string.Empty;
        this.OnPropertyChanged(nameof(this.StatusDisplayText));
    }
}

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
                ||args.PropertyName==nameof(CpuSelectionItem.RunIcon)
                ||args.PropertyName==nameof(CpuSelectionItem.RunStatusText)){
                this.OnPropertyChanged(nameof(this.DisplayText));
                this.OnPropertyChanged(nameof(this.IsRunningJob));
            }
        };
    }

    public CpuSelectionItem Cpu { get; }
    public string DisplayText => this.Cpu.StatusDisplayText;
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

public enum CpuSelectionState{
    Unchecked,
    Mixed,
    Checked,
}

public sealed class BenchmarkResultItem{
    public BenchmarkResultItem(BenchmarkJob job){
        this.Project=job.Project;
        this.Method=job.MethodName;
        this.ElementCount=job.ElementCount;
        this.Name=job.Project+" / "+job.MethodName+" / N="+job.ElementCount.ToString(CultureInfo.InvariantCulture);
        this.Status=job.Status.ToString();
        this.Mean=job.Status==JobStatus.Failed&&string.IsNullOrWhiteSpace(job.Mean) ? "Failed" : job.Mean;
        this.AllocatedBytes=job.AllocatedBytes?.ToString("N0",CultureInfo.InvariantCulture)??string.Empty;
        this.AllocatedBytesValue=job.AllocatedBytes;
        this.MeanNanoseconds=job.MeanNanoseconds;
        this.ErrorNanoseconds=job.ErrorNanoseconds;
        this.StdDevNanoseconds=job.StdDevNanoseconds;
        this.ArtifactFolder=job.ArtifactFolder;
        this.ExitCode=job.ExitCode;
        this.AssignedLogicalCpu=job.AssignedLogicalCpu;
        this.FailureReason=job.FailureReason;
    }

    public BenchmarkResultItem(BenchmarkRunResult result){
        this.Project=result.Project;
        this.Method=result.Method;
        this.ElementCount=0;
        this.Name=result.Project+" / "+result.Method;
        this.Status=result.Status;
        this.Mean=result.Mean;
        this.AllocatedBytes=result.AllocatedBytes?.ToString("N0",CultureInfo.InvariantCulture)??string.Empty;
        this.AllocatedBytesValue=result.AllocatedBytes;
        this.MeanNanoseconds=result.MeanNanoseconds;
        this.ErrorNanoseconds=result.ErrorNanoseconds;
        this.StdDevNanoseconds=result.StdDevNanoseconds;
        this.ArtifactFolder=result.ArtifactFolder;
        this.ExitCode=result.ExitCode;
        this.AssignedLogicalCpu=null;
        this.FailureReason=result.Log;
    }

    public BenchmarkResultItem(string project,string method,ProjectMetrics metrics){
        this.Project=project;
        this.Method=method;
        this.ElementCount=0;
        this.Name=project+" / "+method;
        this.Status=metrics.HasValue?"Completed":"Missing";
        this.Mean=metrics.HasValue&&metrics.TimeNanoseconds is { } time ? BenchmarkFormatting.FormatNanoseconds(time) : string.Empty;
        this.AllocatedBytes=metrics.HasValue&&metrics.AllocatedBytes is { } allocated ? BenchmarkFormatting.FormatBytes(allocated) : string.Empty;
        this.AllocatedBytesValue=metrics.AllocatedBytes;
        this.MeanNanoseconds=metrics.TimeNanoseconds;
        this.ErrorNanoseconds=null;
        this.StdDevNanoseconds=null;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=metrics.HasValue?0:null;
        this.AssignedLogicalCpu=null;
        this.FailureReason=string.Empty;
    }

    private BenchmarkResultItem(BenchmarkCase benchmark,Exception exception){
        this.Project=benchmark.Project;
        this.Method=benchmark.MethodName;
        this.ElementCount=0;
        this.Name=benchmark.Project+" / "+benchmark.MethodName;
        this.Status="Failed";
        this.Mean=exception.GetType().Name;
        this.AllocatedBytes=string.Empty;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=1;
        this.AssignedLogicalCpu=null;
        this.FailureReason=exception.ToString();
    }

    public string Project { get; }
    public string Method { get; }
    public int ElementCount { get; }
    public string Name { get; }
    public string Status { get; }
    public string Mean { get; }
    public string AllocatedBytes { get; }
    public long? AllocatedBytesValue { get; }
    public double? MeanNanoseconds { get; }
    public double? ErrorNanoseconds { get; }
    public double? StdDevNanoseconds { get; }
    public string ArtifactFolder { get; }
    public int? ExitCode { get; }
    public int? AssignedLogicalCpu { get; }
    public string FailureReason { get; }

    public static BenchmarkResultItem Failed(BenchmarkCase benchmark,Exception exception) => new(benchmark,exception);
}

public sealed class ChartComparisonPoint{
    public ChartComparisonPoint(
        string benchmark,
        string displayName,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio){
        this.Benchmark=benchmark;
        this.DisplayName=displayName;
        this.LeftValue=leftValue;
        this.RightValue=rightValue;
        this.LeftText=leftText;
        this.RightText=rightText;
        this.TooltipText=tooltipText;
        this.Ratio=ratio;
    }

    public string Benchmark { get; }
    public string DisplayName { get; }
    public double LeftValue { get; }
    public double RightValue { get; }
    public string LeftText { get; }
    public string RightText { get; }
    public string TooltipText { get; }
    public double? Ratio { get; }
}

public sealed record BenchmarkRunReport(string? RunRoot,IReadOnlyList<string> CsvPaths){
    public BenchmarkRunReport(string? csvPath):this(csvPath,string.IsNullOrWhiteSpace(csvPath)?[]:[csvPath]){
    }

    public string? CsvPath => this.CsvPaths.FirstOrDefault();
}
