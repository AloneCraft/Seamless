using System.Globalization;
using System.ComponentModel;
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
    PairRatioTime,
    PairRatioAllocated,
    LeftTime,
    LeftAllocated,
    RightTime,
    RightAllocated,
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

public sealed record SourceTabOption(BenchmarkSourceTabDefinition Definition){
    public BenchmarkSourceTab Tab => this.Definition.Tab;
    public string Id => this.Definition.Id;
    public string DisplayName => this.Definition.DisplayName;
}

public sealed record ExecutionProfileOption(string DisplayName,BenchmarkExecutionProfile Profile);

public sealed record ScaleOption(string DisplayName,string RunValue);

public sealed record MetricOption(string DisplayName,BenchmarkMetricKind Kind);

public sealed record ChartModeOption(string DisplayName,BenchmarkChartMode Mode);

public sealed record ResultElementCountOption(int Value,string DisplayName);

public sealed record ThresholdOption(string DisplayName,int Milliseconds);

public readonly record struct BenchmarkSortValue(string? Text,double? Number,bool IsMissing){
    public static BenchmarkSortValue FromText(string text) => new(text,null,string.IsNullOrWhiteSpace(text));

    public static BenchmarkSortValue FromMetric(ProjectMetrics metrics,BenchmarkMetricKind kind){
        if(!metrics.HasMeasuredValue){
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
    private static readonly Color DefaultExecutionTextColor = Color.FromArgb("#334155");
    private static readonly Color DefaultMetricTextColor = Color.FromArgb("#1E293B");
    private static readonly Color RunHighlightTextColor = Color.FromArgb("#16A34A");
    private static readonly Color FailureExecutionTextColorValue = Color.FromArgb("#DC2626");

    private bool isSelected;
    private bool isUiSelected;
    private string statusIcon;
    private string statusText;
    private string elementCountText = string.Empty;
    private string executionText = string.Empty;
    private string failureExecutionText = string.Empty;
    private string executionTooltipText = string.Empty;
    private string logicalProcessorText = string.Empty;
    private string coreKindLabel = string.Empty;
    private string timeText = string.Empty;
    private string allocatedText = string.Empty;
    private Color executionTextColor = DefaultExecutionTextColor;
    private Color metricTextColor = DefaultMetricTextColor;
    private FontAttributes executionFontAttributes;
    private FontAttributes metricFontAttributes;
    private bool isRunning;
    private bool hasCpuDisplay;
    private BenchmarkProjectLiveState liveState;
    private DateTimeOffset? lastMeasuredAt;
    private int? lastSuccessfulElementCount;
    private int? lastFailedElementCount;
    private DateTimeOffset? lastFailedAt;
    private BenchmarkMeasurementHighlightState measurementHighlightState;
    private ProjectMetrics metrics = ProjectMetrics.Missing;
    private Color badgeBackgroundColor = Color.FromArgb("#E2E8F0");
    private Color badgeTextColor = Color.FromArgb("#334155");

    public BenchmarkProjectCell(string project,bool hasCase){
        this.Project=project;
        this.HasCase=hasCase;
        this.isSelected=hasCase;
        this.statusIcon=hasCase?"笳・:"-";
        this.statusText=hasCase?"譛ｪ螳溯｡・:"蟇ｾ雎｡縺ｪ縺・;
    }

    public event EventHandler? UiSelectionChanged;

    public string Project { get; }
    public bool HasCase { get; }
    public ProjectMetrics Metrics => this.metrics;
    public DateTimeOffset? LastMeasuredAt => this.lastMeasuredAt;
    public int? LastSuccessfulElementCount => this.lastSuccessfulElementCount;
    public BenchmarkMeasurementHighlightState MeasurementHighlightState => this.measurementHighlightState;

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,this.HasCase&&value);
    }

    public bool IsUiSelected{
        get => this.isUiSelected;
        set{
            if(this.SetProperty(ref this.isUiSelected,this.HasCase&&value)){
                this.UiSelectionChanged?.Invoke(this,EventArgs.Empty);
            }
        }
    }

    public string StatusIcon{
        get => this.statusIcon;
        private set => this.SetProperty(ref this.statusIcon,value);
    }

    public string StatusText{
        get => this.statusText;
        private set => this.SetProperty(ref this.statusText,value);
    }

    public string ElementCountText{
        get => this.elementCountText;
        private set => this.SetProperty(ref this.elementCountText,value);
    }

    public string ExecutionText{
        get => this.executionText;
        private set => this.SetProperty(ref this.executionText,value);
    }

    public string FailureExecutionText{
        get => this.failureExecutionText;
        private set => this.SetProperty(ref this.failureExecutionText,value);
    }

    public string ExecutionTooltipText{
        get => this.executionTooltipText;
        private set => this.SetProperty(ref this.executionTooltipText,value);
    }

    public string LogicalProcessorText{
        get => this.logicalProcessorText;
        private set => this.SetProperty(ref this.logicalProcessorText,value);
    }

    public string CoreKindLabel{
        get => this.coreKindLabel;
        private set => this.SetProperty(ref this.coreKindLabel,value);
    }

    public string TimeText{
        get => this.timeText;
        private set => this.SetProperty(ref this.timeText,value);
    }

    public string AllocatedText{
        get => this.allocatedText;
        private set => this.SetProperty(ref this.allocatedText,value);
    }

    public Color ExecutionTextColor{
        get => this.executionTextColor;
        private set => this.SetProperty(ref this.executionTextColor,value);
    }

    public Color FailureExecutionTextColor => FailureExecutionTextColorValue;

    public Color MetricTextColor{
        get => this.metricTextColor;
        private set => this.SetProperty(ref this.metricTextColor,value);
    }

    public FontAttributes ExecutionFontAttributes{
        get => this.executionFontAttributes;
        private set => this.SetProperty(ref this.executionFontAttributes,value);
    }

    public FontAttributes MetricFontAttributes{
        get => this.metricFontAttributes;
        private set => this.SetProperty(ref this.metricFontAttributes,value);
    }

    public bool IsRunning{
        get => this.isRunning;
        private set => this.SetProperty(ref this.isRunning,value);
    }

    public bool HasCpuDisplay{
        get => this.hasCpuDisplay;
        private set => this.SetProperty(ref this.hasCpuDisplay,value);
    }

    public Color BadgeBackgroundColor{
        get => this.badgeBackgroundColor;
        private set => this.SetProperty(ref this.badgeBackgroundColor,value);
    }

    public Color BadgeTextColor{
        get => this.badgeTextColor;
        private set => this.SetProperty(ref this.badgeTextColor,value);
    }

    public BenchmarkProjectLiveState LiveState{
        get => this.liveState;
        private set{
            if(this.SetProperty(ref this.liveState,value)){
                this.OnPropertyChanged(nameof(this.IsPendingVisual));
                this.OnPropertyChanged(nameof(this.IsRunningVisual));
            }
        }
    }

    public bool IsPendingVisual => BenchmarkProjectLiveStateLogic.IsPending(this.LiveState,this.Metrics.HasMeasuredValue);

    public bool IsRunningVisual => BenchmarkProjectLiveStateLogic.IsRunning(this.LiveState);

    public void ApplyMetrics(ProjectMetrics value,bool preserveRetainedDisplay = false){
        if(!value.HasMeasuredValue&&preserveRetainedDisplay&&this.metrics.HasMeasuredValue){
            this.OnPropertyChanged(nameof(this.IsPendingVisual));
            return;
        }

        this.metrics=value;
        this.TimeText=FormatTime(value);
        this.AllocatedText=FormatAllocated(value);
        this.OnPropertyChanged(nameof(this.IsPendingVisual));
        if(value.HasMeasuredValue&&!this.IsRunning){
            this.StatusIcon="笨・;
            this.StatusText="螳御ｺ・;
        }
    }

    public void ApplyStoredResult(BenchmarkStoredResult result){
        var measuredAt = result.SourceCsvAt??result.MeasuredAt;
        this.lastMeasuredAt=measuredAt;
        var resultMetrics = result.ToProjectMetrics();
        if(resultMetrics.HasMeasuredValue){
            this.ApplySuccessfulDisplay(resultMetrics,result.ElementCount,measuredAt,BenchmarkMeasurementHighlightState.None);
            this.ClearFailureDisplay();
        }else{
            this.ApplyMetrics(resultMetrics);
            if(string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
                this.ApplyFailureDisplay(result.ElementCount,measuredAt);
            }
        }
        this.IsRunning=false;
        if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
            this.StatusIcon="笨・;
            this.StatusText="螳御ｺ・;
        }else if(string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
            this.StatusIcon="ﾃ・;
            this.StatusText="螟ｱ謨・;
        }else{
            this.StatusIcon="笳・;
            this.StatusText=result.Status;
        }
        this.LiveState=BenchmarkProjectLiveState.Idle;
        this.ClearCpuDisplay();
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
    }

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu,DateTimeOffset completedAt,bool highlightCompleted = true){
        var isCompleted = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        var isFailed = string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        if(isCompleted&&result.MeanNanoseconds is not null){
            this.lastMeasuredAt=completedAt;
            this.ApplySuccessfulDisplay(
                ProjectMetrics.FromMeasurement(result.MeanNanoseconds,result.AllocatedBytesValue),
                result.ElementCount,
                completedAt,
                highlightCompleted ? BenchmarkMeasurementHighlightState.Fresh : BenchmarkMeasurementHighlightState.None);
            this.ClearFailureDisplay();
        }else if(isFailed){
            this.lastMeasuredAt=completedAt;
            this.ApplyFailureDisplay(result.ElementCount,completedAt);
        }
        this.IsRunning=string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        this.StatusIcon=this.IsRunning
            ? "筺ｳ"
            : string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "笨・
                : string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "ﾃ・
                    : "笳・;
        this.StatusText=this.IsRunning
            ? "螳溯｡御ｸｭ"
            : string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? "螳御ｺ・
                : string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)
                    ? "螟ｱ謨・
                    : result.Status;
        this.ApplyCpuDisplay(cpu);
        this.LiveState=this.IsRunning
            ? BenchmarkProjectLiveState.Running
            : string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                ? BenchmarkProjectLiveState.Completed
                : BenchmarkProjectLiveState.Idle;
    }

    public void ApplySkipped(int stoppedAtElementCount,ProjectMetrics stoppedMetrics){
        this.metrics=stoppedMetrics;
        this.TimeText=FormatTime(stoppedMetrics);
        this.AllocatedText=FormatAllocated(stoppedMetrics);
        this.IsRunning=false;
        this.StatusIcon=stoppedAtElementCount.ToString(CultureInfo.InvariantCulture);
        this.StatusText=FormatElementCount(stoppedAtElementCount);
        this.ElementCountText=FormatElementCount(stoppedAtElementCount);
        this.ExecutionText=string.Empty;
        this.FailureExecutionText=string.Empty;
        this.ExecutionTooltipText=string.Empty;
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
        this.LiveState=BenchmarkProjectLiveState.Idle;
        this.ClearCpuDisplay();
        this.OnPropertyChanged(nameof(this.IsPendingVisual));
    }

    public void ClearLiveState(){
        this.IsRunning=false;
        this.LiveState=BenchmarkProjectLiveState.Idle;
    }

    public void RetainCompletedHighlightForRunningElement(int runningElementCount){
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightLogic.AdvanceForRunningElement(
            this.MeasurementHighlightState,
            this.LastSuccessfulElementCount,
            runningElementCount));
    }

    public void ClearRunHighlight(){
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightLogic.ClearAfterRun(this.MeasurementHighlightState));
    }

    public void ClearMetrics(){
        this.metrics=ProjectMetrics.Missing;
        this.lastSuccessfulElementCount=null;
        this.lastMeasuredAt=null;
        this.lastFailedElementCount=null;
        this.lastFailedAt=null;
        this.TimeText=string.Empty;
        this.AllocatedText=string.Empty;
        this.ElementCountText=string.Empty;
        this.ClearLiveState();
        if(this.HasCase){
            this.StatusIcon="笳・;
            this.StatusText="譛ｪ螳溯｡・;
            this.ExecutionText=string.Empty;
            this.FailureExecutionText=string.Empty;
            this.ExecutionTooltipText=string.Empty;
            this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
        }
    }

    private void ApplySuccessfulDisplay(
        ProjectMetrics value,
        int elementCount,
        DateTimeOffset measuredAt,
        BenchmarkMeasurementHighlightState highlightState){
        this.metrics=value;
        this.lastSuccessfulElementCount=elementCount;
        this.lastMeasuredAt=measuredAt;
        this.TimeText=FormatTime(value);
        this.AllocatedText=FormatAllocated(value);
        this.ElementCountText=FormatElementCount(elementCount);
        this.ExecutionText=FormatTimestamp(measuredAt);
        this.RefreshExecutionTooltipText();
        this.ApplyMeasurementHighlight(highlightState);
        this.OnPropertyChanged(nameof(this.IsPendingVisual));
        this.OnPropertyChanged(nameof(this.LastSuccessfulElementCount));
    }

    private void ApplyFailureDisplay(int elementCount,DateTimeOffset failedAt){
        this.lastFailedElementCount=elementCount;
        this.lastFailedAt=failedAt;
        this.ElementCountText=FormatElementCount(elementCount);
        this.FailureExecutionText="  "+FormatTimestamp(failedAt);
        this.RefreshExecutionTooltipText();
    }

    private void ClearFailureDisplay(){
        this.lastFailedElementCount=null;
        this.lastFailedAt=null;
        this.FailureExecutionText=string.Empty;
        this.RefreshExecutionTooltipText();
    }

    private void RefreshExecutionTooltipText(){
        var failureText = this.lastFailedElementCount is { } failedN&&this.lastFailedAt is { } failedAt
            ? "螟ｱ謨・"+FormatElementCount(failedN)+" "+FormatTimestamp(failedAt)
            : string.Empty;
        this.ExecutionTooltipText=string.Join(
            Environment.NewLine,
            new[] { FormatElementTooltip(this.ElementCountText,this.ExecutionText),failureText }.Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    private void ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState value){
        if(this.SetProperty(ref this.measurementHighlightState,value,nameof(this.MeasurementHighlightState))){
            this.OnPropertyChanged(nameof(this.MeasurementHighlightState));
        }

        var isHighlighted = value is BenchmarkMeasurementHighlightState.Fresh or BenchmarkMeasurementHighlightState.RetainedDuringRun;
        var attributes = value==BenchmarkMeasurementHighlightState.Fresh
            ? FontAttributes.Bold
            : FontAttributes.None;
        this.ExecutionTextColor=isHighlighted ? RunHighlightTextColor : DefaultExecutionTextColor;
        this.MetricTextColor=isHighlighted ? RunHighlightTextColor : DefaultMetricTextColor;
        this.ExecutionFontAttributes=attributes;
        this.MetricFontAttributes=attributes;
    }

    private static string FormatElementCount(int elementCount) =>
        elementCount.ToString(CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        BenchmarkTimestampDisplay.FormatLocal(timestamp);

    private static string FormatElementTooltip(string elementCountText,string timestampText) =>
        string.IsNullOrWhiteSpace(elementCountText)
            ? timestampText
            : string.IsNullOrWhiteSpace(timestampText)
                ? elementCountText
                : elementCountText+" "+timestampText;

    private static string FormatTime(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } value
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : string.Empty;
    }

    private void ApplyCpuDisplay(CpuSelectionItem? cpu){
        if(cpu is null){
            this.ClearCpuDisplay();
            return;
        }

        this.HasCpuDisplay=true;
        this.LogicalProcessorText=cpu.LogicalProcessorText;
        this.CoreKindLabel=cpu.CoreKindLabel;
        this.BadgeBackgroundColor=cpu.BadgeBackgroundColor;
        this.BadgeTextColor=cpu.BadgeTextColor;
    }

    private void ClearCpuDisplay(){
        this.HasCpuDisplay=false;
        this.LogicalProcessorText=string.Empty;
        this.CoreKindLabel=string.Empty;
        this.BadgeBackgroundColor=Color.FromArgb("#E2E8F0");
        this.BadgeTextColor=Color.FromArgb("#334155");
    }

    private static string FormatAllocated(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } value
            ? BenchmarkFormatting.FormatBytes(value)
            : string.Empty;
    }
}

public sealed class BenchmarkColumnSelectionGroup:ObservableObject{
    private bool? isChecked;
    private string headerText;

    public BenchmarkColumnSelectionGroup(string id,string headerText,IReadOnlyList<string> columnKeys){
        this.Id=id;
        this.headerText=headerText;
        this.ColumnKeys=columnKeys;
    }

    public string Id { get; }
    public string HeaderText{
        get => this.headerText;
        private set => this.SetProperty(ref this.headerText,value);
    }
    public IReadOnlyList<string> ColumnKeys { get; }

    public bool? IsChecked{
        get => this.isChecked;
        set => this.SetProperty(ref this.isChecked,value);
    }

    public void SetHeaderText(string value){
        this.HeaderText=value;
    }
}

public sealed class BenchmarkTableRow:ObservableObject{
    private bool isSelected;
    private bool isUiSelected;
    private bool? isSelectionChecked;
    private string systemRatioTimeText = string.Empty;
    private string systemRatioAllocatedText = string.Empty;
    private string systemStopNText = string.Empty;
    private Color systemStopNTextColor = DefaultStopNTextColor;
    private FontAttributes systemStopNFontAttributes;
    private string systemFLinqTimeText = string.Empty;
    private string systemFLinqAllocatedText = string.Empty;
    private string systemZLinqTimeText = string.Empty;
    private string systemZLinqAllocatedText = string.Empty;
    private string persistedRatioTimeText = string.Empty;
    private string persistedRatioAllocatedText = string.Empty;
    private string persistedStopNText = string.Empty;
    private Color persistedStopNTextColor = DefaultStopNTextColor;
    private FontAttributes persistedStopNFontAttributes;
    private string persistedFLinqTimeText = string.Empty;
    private string persistedFLinqAllocatedText = string.Empty;
    private string persistedLinqTimeText = string.Empty;
    private string persistedLinqAllocatedText = string.Empty;
    private string tsavoriteRatioTimeText = string.Empty;
    private string tsavoriteRatioAllocatedText = string.Empty;
    private string tsavoriteStopNText = string.Empty;
    private Color tsavoriteStopNTextColor = DefaultStopNTextColor;
    private FontAttributes tsavoriteStopNFontAttributes;
    private string tsavoriteFLinqTimeText = string.Empty;
    private string tsavoriteFLinqAllocatedText = string.Empty;
    private string tsavoriteLinqTimeText = string.Empty;
    private string tsavoriteLinqAllocatedText = string.Empty;
    private BenchmarkComparisonRow? comparisonRow;
    private static readonly Color DefaultStopNTextColor = Color.FromArgb("#334155");
    private static readonly Color HighlightStopNTextColor = Color.FromArgb("#16A34A");

    public BenchmarkTableRow(string methodName,BenchmarkSourceTabDefinition sourceTab,IReadOnlyDictionary<string,BenchmarkCase> casesByProject){
        this.MethodName=methodName;
        this.BenchmarkName=methodName;
        this.SourceTab=sourceTab;
        this.CasesByProject=casesByProject;
        this.LeftCell=this.CreateCell(sourceTab.LeftProject);
        this.RightCell=this.CreateCell(sourceTab.RightProject);
        foreach(var cell in this.ProjectCells){
            cell.UiSelectionChanged+=this.OnCellUiSelectionChanged;
            cell.PropertyChanged+=this.OnCellPropertyChanged;
        }
        this.RefreshUiSelectionState();
    }

    public string MethodName { get; }
    public string BenchmarkName { get; }
    public BenchmarkSourceTabDefinition SourceTab { get; }
    public IReadOnlyDictionary<string,BenchmarkCase> CasesByProject { get; }
    public BenchmarkProjectCell LeftCell { get; }
    public BenchmarkProjectCell RightCell { get; }
    public BenchmarkProjectCell SystemFLinqCell => this.LeftCell;
    public BenchmarkProjectCell SystemZLinqCell => this.RightCell;
    public BenchmarkProjectCell PersistedFLinqCell => this.LeftCell;
    public BenchmarkProjectCell PersistedLinqCell => this.RightCell;
    public BenchmarkProjectCell TsavoriteFLinqCell => this.LeftCell;
    public BenchmarkProjectCell TsavoriteLinqCell => this.RightCell;

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }

    public bool IsUiSelected{
        get => this.isUiSelected;
        private set => this.SetProperty(ref this.isUiSelected,value);
    }

    public bool? IsSelectionChecked{
        get => this.isSelectionChecked;
        private set => this.SetProperty(ref this.isSelectionChecked,value);
    }

    public string SystemRatioTimeText{
        get => this.systemRatioTimeText;
        private set => this.SetProperty(ref this.systemRatioTimeText,value);
    }

    public string PairRatioTimeText => this.SystemRatioTimeText;

    public string SystemRatioAllocatedText{
        get => this.systemRatioAllocatedText;
        private set => this.SetProperty(ref this.systemRatioAllocatedText,value);
    }

    public string PairRatioAllocatedText => this.SystemRatioAllocatedText;

    public string SystemStopNText{
        get => this.systemStopNText;
        private set => this.SetProperty(ref this.systemStopNText,value);
    }

    public string StopNText => string.IsNullOrWhiteSpace(this.SystemStopNText)
        ? this.ElementCountText
        : this.SystemStopNText;

    private string ElementCountText{
        get{
            var left = this.LeftCell.ElementCountText;
            var right = this.RightCell.ElementCountText;
            if(string.Equals(left,right,StringComparison.Ordinal)){
                return left;
            }

            if(string.IsNullOrWhiteSpace(left)){
                return right;
            }

            if(string.IsNullOrWhiteSpace(right)){
                return left;
            }

            return left+"/"+right;
        }
    }

    public Color SystemStopNTextColor{
        get => this.systemStopNTextColor;
        private set => this.SetProperty(ref this.systemStopNTextColor,value);
    }

    public Color StopNTextColor => this.SystemStopNTextColor;

    public FontAttributes SystemStopNFontAttributes{
        get => this.systemStopNFontAttributes;
        private set => this.SetProperty(ref this.systemStopNFontAttributes,value);
    }

    public FontAttributes StopNFontAttributes => this.SystemStopNFontAttributes;

    public string SystemFLinqTimeText{
        get => this.systemFLinqTimeText;
        private set => this.SetProperty(ref this.systemFLinqTimeText,value);
    }

    public string LeftTimeText => this.SystemFLinqTimeText;

    public string SystemFLinqAllocatedText{
        get => this.systemFLinqAllocatedText;
        private set => this.SetProperty(ref this.systemFLinqAllocatedText,value);
    }

    public string LeftAllocatedText => this.SystemFLinqAllocatedText;

    public string SystemZLinqTimeText{
        get => this.systemZLinqTimeText;
        private set => this.SetProperty(ref this.systemZLinqTimeText,value);
    }

    public string RightTimeText => this.SystemZLinqTimeText;

    public string SystemZLinqAllocatedText{
        get => this.systemZLinqAllocatedText;
        private set => this.SetProperty(ref this.systemZLinqAllocatedText,value);
    }

    public string RightAllocatedText => this.SystemZLinqAllocatedText;

    public string PersistedRatioTimeText{
        get => this.persistedRatioTimeText;
        private set => this.SetProperty(ref this.persistedRatioTimeText,value);
    }

    public string PersistedRatioAllocatedText{
        get => this.persistedRatioAllocatedText;
        private set => this.SetProperty(ref this.persistedRatioAllocatedText,value);
    }

    public string PersistedStopNText{
        get => this.persistedStopNText;
        private set => this.SetProperty(ref this.persistedStopNText,value);
    }

    public Color PersistedStopNTextColor{
        get => this.persistedStopNTextColor;
        private set => this.SetProperty(ref this.persistedStopNTextColor,value);
    }

    public FontAttributes PersistedStopNFontAttributes{
        get => this.persistedStopNFontAttributes;
        private set => this.SetProperty(ref this.persistedStopNFontAttributes,value);
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

    public string TsavoriteStopNText{
        get => this.tsavoriteStopNText;
        private set => this.SetProperty(ref this.tsavoriteStopNText,value);
    }

    public Color TsavoriteStopNTextColor{
        get => this.tsavoriteStopNTextColor;
        private set => this.SetProperty(ref this.tsavoriteStopNTextColor,value);
    }

    public FontAttributes TsavoriteStopNFontAttributes{
        get => this.tsavoriteStopNFontAttributes;
        private set => this.SetProperty(ref this.tsavoriteStopNFontAttributes,value);
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
            yield return this.LeftCell;
            yield return this.RightCell;
        }
    }

    public BenchmarkProjectCell? GetProjectCell(string project) =>
        this.ProjectCells.FirstOrDefault(cell => string.Equals(cell.Project,project,StringComparison.OrdinalIgnoreCase));

    public void RefreshSelectionState(){
        var state = GetSelectionState(this.ProjectCells);
        this.IsSelectionChecked=state;
        this.IsSelected=state is true or null;
    }

    public void RefreshUiSelectionState(){
        this.IsUiSelected=BenchmarkRowUiSelectionLogic.IsWholeRowSelected(
            this.ProjectCells.Select(static cell => new BenchmarkRowUiSelectionCell(cell.HasCase,cell.IsUiSelected)));
    }

    private void OnCellUiSelectionChanged(object? sender,EventArgs args){
        this.RefreshUiSelectionState();
    }

    private void OnCellPropertyChanged(object? sender,PropertyChangedEventArgs args){
        if(args.PropertyName==nameof(BenchmarkProjectCell.ElementCountText)){
            this.OnPropertyChanged(nameof(this.StopNText));
        }
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

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu,DateTimeOffset completedAt,bool highlightCompleted = true){
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplyRunResult(result,cpu,completedAt,highlightCompleted);
        }
    }

    public void ApplySkipped(string project,int stoppedAtElementCount,ProjectMetrics stoppedMetrics){
        if(this.GetProjectCell(project) is { } cell){
            cell.ApplySkipped(stoppedAtElementCount,stoppedMetrics);
        }

        this.SetProjectMetricText(project,FormatTime(stoppedMetrics),FormatAllocated(stoppedMetrics));
    }

    public void ApplyPairStopped(string leftProject,string rightProject,int stoppedAtElementCount,bool highlight){
        var text = stoppedAtElementCount.ToString(CultureInfo.InvariantCulture);
        if(IsPair(leftProject,rightProject,this.SourceTab.LeftProject,this.SourceTab.RightProject)){
            this.SystemStopNText=text;
            this.ApplySystemStopNStyle(highlight);
            this.OnPropertyChanged(nameof(this.StopNText));
            this.OnPropertyChanged(nameof(this.StopNTextColor));
            this.OnPropertyChanged(nameof(this.StopNFontAttributes));
        }
    }

    public void ClearLiveState(){
        foreach(var cell in this.ProjectCells){
            cell.ClearLiveState();
        }
    }

    public void RetainCompletedHighlightsForRunningElement(int runningElementCount){
        foreach(var cell in this.ProjectCells){
            cell.RetainCompletedHighlightForRunningElement(runningElementCount);
        }
    }

    public void ClearRunHighlights(){
        foreach(var cell in this.ProjectCells){
            cell.ClearRunHighlight();
        }
    }

    public void ClearResultDisplay(){
        this.ClearComparison(false);
    }

    public void ApplyComparison(BenchmarkComparisonRow? row,bool preserveRetainedDisplay = false){
        this.comparisonRow=row;
        if(row is null){
            this.ClearComparison(preserveRetainedDisplay);
            return;
        }

        var value = row.Value;
        this.ClearStopNText();
        this.SystemRatioTimeText=FormatRatio(value.PairRatio.TimeRatio);
        this.SystemRatioAllocatedText=FormatRatio(value.PairRatio.AllocatedRatio);
        this.OnPropertyChanged(nameof(this.PairRatioTimeText));
        this.OnPropertyChanged(nameof(this.PairRatioAllocatedText));
        var left = value.GetProject(this.SourceTab.LeftProject);
        var right = value.GetProject(this.SourceTab.RightProject);
        this.SystemFLinqTimeText=FormatTime(left);
        this.SystemFLinqAllocatedText=FormatAllocated(left);
        this.LeftCell.ApplyMetrics(left,preserveRetainedDisplay);
        this.SystemZLinqTimeText=FormatTime(right);
        this.SystemZLinqAllocatedText=FormatAllocated(right);
        this.RightCell.ApplyMetrics(right,preserveRetainedDisplay);
        this.OnPropertyChanged(nameof(this.LeftTimeText));
        this.OnPropertyChanged(nameof(this.LeftAllocatedText));
        this.OnPropertyChanged(nameof(this.RightTimeText));
        this.OnPropertyChanged(nameof(this.RightAllocatedText));
    }

    public BenchmarkSortValue GetSortValue(BenchmarkGridSortColumn column){
        var row = this.comparisonRow;
        return column switch{
            BenchmarkGridSortColumn.BenchmarkName => BenchmarkSortValue.FromText(this.BenchmarkName),
            BenchmarkGridSortColumn.PairRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.PairRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PairRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.PairRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.LeftTime => BenchmarkSortValue.FromMetric(this.LeftCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.LeftAllocated => BenchmarkSortValue.FromMetric(this.LeftCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.RightTime => BenchmarkSortValue.FromMetric(this.RightCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.RightAllocated => BenchmarkSortValue.FromMetric(this.RightCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.SystemRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.SystemRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.SystemRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.SystemFLinqTime => BenchmarkSortValue.FromMetric(this.SystemFLinqCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.SystemFLinqAllocated => BenchmarkSortValue.FromMetric(this.SystemFLinqCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.SystemZLinqTime => BenchmarkSortValue.FromMetric(this.SystemZLinqCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.SystemZLinqAllocated => BenchmarkSortValue.FromMetric(this.SystemZLinqCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.PersistedRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.PersistedRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.PersistedRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.PersistedFLinqTime => BenchmarkSortValue.FromMetric(this.PersistedFLinqCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.PersistedFLinqAllocated => BenchmarkSortValue.FromMetric(this.PersistedFLinqCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.PersistedLinqTime => BenchmarkSortValue.FromMetric(this.PersistedLinqCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.PersistedLinqAllocated => BenchmarkSortValue.FromMetric(this.PersistedLinqCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.TsavoriteRatioTime => row is { } value ? BenchmarkSortValue.FromRatio(value.TsavoriteRatio.TimeRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteRatioAllocated => row is { } value ? BenchmarkSortValue.FromRatio(value.TsavoriteRatio.AllocatedRatio) : BenchmarkSortValue.Missing,
            BenchmarkGridSortColumn.TsavoriteFLinqTime => BenchmarkSortValue.FromMetric(this.TsavoriteFLinqCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.TsavoriteFLinqAllocated => BenchmarkSortValue.FromMetric(this.TsavoriteFLinqCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.TsavoriteLinqTime => BenchmarkSortValue.FromMetric(this.TsavoriteLinqCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.TsavoriteLinqAllocated => BenchmarkSortValue.FromMetric(this.TsavoriteLinqCell.Metrics,BenchmarkMetricKind.Allocated),
            _ => BenchmarkSortValue.Missing,
        };
    }

    private void ClearComparison(bool preserveRetainedDisplay){
        this.SystemRatioTimeText=string.Empty;
        this.SystemRatioAllocatedText=string.Empty;
        this.SystemStopNText=string.Empty;
        this.SystemFLinqTimeText=string.Empty;
        this.SystemFLinqAllocatedText=string.Empty;
        this.SystemZLinqTimeText=string.Empty;
        this.SystemZLinqAllocatedText=string.Empty;
        this.PersistedRatioTimeText=string.Empty;
        this.PersistedRatioAllocatedText=string.Empty;
        this.PersistedStopNText=string.Empty;
        this.PersistedFLinqTimeText=string.Empty;
        this.PersistedFLinqAllocatedText=string.Empty;
        this.PersistedLinqTimeText=string.Empty;
        this.PersistedLinqAllocatedText=string.Empty;
        this.TsavoriteRatioTimeText=string.Empty;
        this.TsavoriteRatioAllocatedText=string.Empty;
        this.TsavoriteStopNText=string.Empty;
        this.TsavoriteFLinqTimeText=string.Empty;
        this.TsavoriteFLinqAllocatedText=string.Empty;
        this.TsavoriteLinqTimeText=string.Empty;
        this.TsavoriteLinqAllocatedText=string.Empty;
        foreach(var cell in this.ProjectCells){
            if(!preserveRetainedDisplay){
                cell.ClearMetrics();
            }
        }
    }

    private void SetProjectMetricText(string project,string timeText,string allocatedText){
        if(string.Equals(project,this.SourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)){
            this.SystemFLinqTimeText=timeText;
            this.SystemFLinqAllocatedText=allocatedText;
            this.OnPropertyChanged(nameof(this.LeftTimeText));
            this.OnPropertyChanged(nameof(this.LeftAllocatedText));
        }else if(string.Equals(project,this.SourceTab.RightProject,StringComparison.OrdinalIgnoreCase)){
            this.SystemZLinqTimeText=timeText;
            this.SystemZLinqAllocatedText=allocatedText;
            this.OnPropertyChanged(nameof(this.RightTimeText));
            this.OnPropertyChanged(nameof(this.RightAllocatedText));
        }
    }

    private void ClearStopNText(){
        this.SystemStopNText=string.Empty;
        this.ApplySystemStopNStyle(false);
        this.OnPropertyChanged(nameof(this.StopNText));
        this.OnPropertyChanged(nameof(this.StopNTextColor));
        this.OnPropertyChanged(nameof(this.StopNFontAttributes));
        this.PersistedStopNText=string.Empty;
        this.ApplyPersistedStopNStyle(false);
        this.TsavoriteStopNText=string.Empty;
        this.ApplyTsavoriteStopNStyle(false);
    }

    private void ApplySystemStopNStyle(bool highlight){
        this.SystemStopNTextColor=highlight ? HighlightStopNTextColor : DefaultStopNTextColor;
        this.SystemStopNFontAttributes=highlight ? FontAttributes.Bold : FontAttributes.None;
    }

    private void ApplyPersistedStopNStyle(bool highlight){
        this.PersistedStopNTextColor=highlight ? HighlightStopNTextColor : DefaultStopNTextColor;
        this.PersistedStopNFontAttributes=highlight ? FontAttributes.Bold : FontAttributes.None;
    }

    private void ApplyTsavoriteStopNStyle(bool highlight){
        this.TsavoriteStopNTextColor=highlight ? HighlightStopNTextColor : DefaultStopNTextColor;
        this.TsavoriteStopNFontAttributes=highlight ? FontAttributes.Bold : FontAttributes.None;
    }

    private static bool IsPair(string leftProject,string rightProject,string expectedLeft,string expectedRight) =>
        string.Equals(leftProject,expectedLeft,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(rightProject,expectedRight,StringComparison.OrdinalIgnoreCase);

    private BenchmarkProjectCell CreateCell(string project) =>
        new(project,this.CasesByProject.ContainsKey(project));

    private static string FormatTime(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } value
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : string.Empty;
    }

    private static string FormatAllocated(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } value
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
    private string runStatusText = "蠕・ｩ・;
    private string runIcon = "笳・;
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
    public string LogicalProcessorText => this.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture);
    public string CellStatusText =>
        this.RunIcon
        +(string.IsNullOrWhiteSpace(this.CurrentJobText) ? string.Empty : " "+this.CurrentJobText);
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
        this.RunIcon=this.IsRunningJob
            ? "●"
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
        this.CurrentJobText=item.Project+" / "+item.Method+" / "+item.ElementCount.ToString(CultureInfo.InvariantCulture);
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
                this.OnPropertyChanged(nameof(this.LogicalProcessorText));
                this.OnPropertyChanged(nameof(this.CoreKindLabel));
                this.OnPropertyChanged(nameof(this.CellStatusText));
                this.OnPropertyChanged(nameof(this.BadgeBackgroundColor));
                this.OnPropertyChanged(nameof(this.BadgeTextColor));
                this.OnPropertyChanged(nameof(this.IsRunningJob));
            }
        };
    }

    public CpuSelectionItem Cpu { get; }
    public string DisplayText => this.Cpu.StatusDisplayText;
    public string LogicalProcessorText => this.Cpu.LogicalProcessorText;
    public string CoreKindLabel => this.Cpu.CoreKindLabel;
    public string CellStatusText => this.Cpu.CellStatusText;
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
        this.Mean=job.MeanNanoseconds is { } mean
            ? BenchmarkFormatting.FormatNanoseconds(mean)
            : job.Status==JobStatus.Failed&&string.IsNullOrWhiteSpace(job.Mean) ? "Failed" : job.Mean;
        this.AllocatedBytes=job.AllocatedBytes is { } allocated
            ? BenchmarkFormatting.FormatBytes(allocated)
            : string.Empty;
        this.AllocatedBytesValue=job.AllocatedBytes;
        this.MeanNanoseconds=job.MeanNanoseconds;
        this.ErrorNanoseconds=job.ErrorNanoseconds;
        this.StdDevNanoseconds=job.StdDevNanoseconds;
        this.ArtifactFolder=job.ArtifactFolder;
        this.ExitCode=job.ExitCode;
        this.AssignedLogicalCpu=job.AssignedLogicalCpu;
        this.StartedAt=job.StartedAt;
        this.FailureReason=job.FailureReason;
    }

    public BenchmarkResultItem(BenchmarkRunResult result){
        this.Project=result.Project;
        this.Method=result.Method;
        this.ElementCount=0;
        this.Name=result.Project+" / "+result.Method;
        this.Status=result.Status;
        this.Mean=result.MeanNanoseconds is { } mean
            ? BenchmarkFormatting.FormatNanoseconds(mean)
            : result.Mean;
        this.AllocatedBytes=result.AllocatedBytes is { } allocated
            ? BenchmarkFormatting.FormatBytes(allocated)
            : string.Empty;
        this.AllocatedBytesValue=result.AllocatedBytes;
        this.MeanNanoseconds=result.MeanNanoseconds;
        this.ErrorNanoseconds=result.ErrorNanoseconds;
        this.StdDevNanoseconds=result.StdDevNanoseconds;
        this.ArtifactFolder=result.ArtifactFolder;
        this.ExitCode=result.ExitCode;
        this.AssignedLogicalCpu=null;
        this.StartedAt=null;
        this.FailureReason=result.Log;
    }

    public BenchmarkResultItem(string project,string method,ProjectMetrics metrics,int elementCount = 0){
        this.Project=project;
        this.Method=method;
        this.ElementCount=elementCount;
        this.Name=project+" / "+method;
        this.Status=metrics.HasMeasuredValue?"Completed":"Missing";
        this.Mean=metrics.HasMeasuredValue&&metrics.TimeNanoseconds is { } time ? BenchmarkFormatting.FormatNanoseconds(time) : string.Empty;
        this.AllocatedBytes=metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } allocated ? BenchmarkFormatting.FormatBytes(allocated) : string.Empty;
        this.AllocatedBytesValue=metrics.AllocatedBytes;
        this.MeanNanoseconds=metrics.TimeNanoseconds;
        this.ErrorNanoseconds=null;
        this.StdDevNanoseconds=null;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=metrics.HasMeasuredValue?0:null;
        this.AssignedLogicalCpu=null;
        this.StartedAt=null;
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
        this.StartedAt=null;
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
    public DateTimeOffset? StartedAt { get; }
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
