using System.Globalization;
using Benchmark.Worker;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark;

public enum BenchmarkTab{
    Cpu,
    Results,
    Chart,
}

public enum BenchmarkMetricKind{
    Time,
    Allocated,
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

public sealed record MetricOption(string DisplayName,BenchmarkMetricKind Kind);

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

public sealed class BenchmarkTableRow:ObservableObject{
    private bool isSelected;
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
    }

    public string MethodName { get; }
    public string BenchmarkName { get; }
    public IReadOnlyDictionary<string,BenchmarkCase> CasesByProject { get; }

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
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
        return this.CasesByProject
            .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static item => item.Value)
            .ToArray();
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
        this.SystemZLinqTimeText=FormatTime(value.SystemZLinq);
        this.SystemZLinqAllocatedText=FormatAllocated(value.SystemZLinq);
        this.PersistedRatioTimeText=FormatRatio(value.PersistedRatio.TimeRatio);
        this.PersistedRatioAllocatedText=FormatRatio(value.PersistedRatio.AllocatedRatio);
        this.PersistedFLinqTimeText=FormatTime(value.PersistedFLinq);
        this.PersistedFLinqAllocatedText=FormatAllocated(value.PersistedFLinq);
        this.PersistedLinqTimeText=FormatTime(value.PersistedLinq);
        this.PersistedLinqAllocatedText=FormatAllocated(value.PersistedLinq);
        this.TsavoriteRatioTimeText=FormatRatio(value.TsavoriteRatio.TimeRatio);
        this.TsavoriteRatioAllocatedText=FormatRatio(value.TsavoriteRatio.AllocatedRatio);
        this.TsavoriteFLinqTimeText=FormatTime(value.TsavoriteFLinq);
        this.TsavoriteFLinqAllocatedText=FormatAllocated(value.TsavoriteFLinq);
        this.TsavoriteLinqTimeText=FormatTime(value.TsavoriteLinq);
        this.TsavoriteLinqAllocatedText=FormatAllocated(value.TsavoriteLinq);
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
    public string DisplayName => "CPU "+this.LogicalProcessorIndex+" ("+this.CoreLabel+")";

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }
}

public sealed class CpuTableCell:ObservableObject{
    private bool isSelected;

    public CpuTableCell(CpuSelectionItem cpu){
        this.Cpu=cpu;
        this.DisplayText=cpu.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture);
        this.isSelected=cpu.IsSelected;
        cpu.PropertyChanged+=(_,args) => {
            if(args.PropertyName==nameof(CpuSelectionItem.IsSelected)){
                this.IsSelected=cpu.IsSelected;
            }
        };
    }

    public CpuSelectionItem Cpu { get; }
    public string DisplayText { get; }

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                if(this.Cpu.IsSelected!=value){
                    this.Cpu.IsSelected=value;
                }
                this.OnPropertyChanged(nameof(this.DisplayWithState));
            }
        }
    }

    public string DisplayWithState => this.IsSelected ? "X "+this.DisplayText : "  "+this.DisplayText;
}

public sealed class CpuTableRow{
    public CpuTableRow(string label,IReadOnlyList<CpuTableCell> cells){
        this.Label=label;
        this.Cells=cells;
    }

    public string Label { get; }
    public IReadOnlyList<CpuTableCell> Cells { get; }
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
    public string DisplayText => string.IsNullOrEmpty(this.StateText) ? this.Title : this.StateText+" "+this.Title;

    public void Refresh(){
        this.OnPropertyChanged(nameof(this.StateText));
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
            }
        }
    }

    public string StateText => this.State switch{
        CpuSelectionState.Checked => "X",
        CpuSelectionState.Mixed => "-",
        _ => string.Empty,
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
    public BenchmarkResultItem(BenchmarkRunResult result){
        this.Project=result.Project;
        this.Method=result.Method;
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
    }

    public BenchmarkResultItem(string project,string method,ProjectMetrics metrics){
        this.Project=project;
        this.Method=method;
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
    }

    private BenchmarkResultItem(BenchmarkCase benchmark,Exception exception){
        this.Project=benchmark.Project;
        this.Method=benchmark.MethodName;
        this.Name=benchmark.Project+" / "+benchmark.MethodName;
        this.Status="Failed";
        this.Mean=exception.GetType().Name;
        this.AllocatedBytes=string.Empty;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=1;
    }

    public string Project { get; }
    public string Method { get; }
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

public sealed record BenchmarkRunReport(string? CsvPath);
