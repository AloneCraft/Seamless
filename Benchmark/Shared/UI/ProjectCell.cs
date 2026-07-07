using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;

public sealed class ProjectCell:ObservableObject{
    private static readonly Color DefaultExecutionTextColor = Color.FromArgb("#334155");
    private static readonly Color DefaultMetricTextColor = Color.FromArgb("#1E293B");
    private static readonly Color FailureExecutionTextColorValue = Color.FromArgb("#DC2626");
    private const string MissingMetadataText = "CPU/日時不明";

    private bool isSelected;
    private bool isUiSelected;
    private bool isFailed;
    private bool isCompletedWithoutMeasurement;
    private string statusImageSource;
    private string statusText;
    private string elementCountText = string.Empty;
    private string executionText = string.Empty;
    private string failureExecutionText = string.Empty;
    private string failureReasonText = string.Empty;
    private string executionTooltipText = string.Empty;
    private string resultCheckText = string.Empty;
    private string resultSeedText = string.Empty;
    private string logicalProcessorText = string.Empty;
    private string coreKindLabel = string.Empty;
    private string coreKindImageSource = string.Empty;
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
    private bool isRowFinalCompleted;

    public ProjectCell(string project,bool hasCase){
        this.Project=project;
        this.HasCase=hasCase;
        this.isSelected=hasCase;
        this.statusImageSource=hasCase ? BenchmarkStatusImageNames.Waiting : string.Empty;
        this.statusText=hasCase?"待機":"対象なし";
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
        set{
            if(this.SetProperty(ref this.isSelected,this.HasCase&&value)){
                this.RefreshStatusDisplay();
            }
        }
    }

    public bool IsUiSelected{
        get => this.isUiSelected;
        set{
            if(this.SetProperty(ref this.isUiSelected,this.HasCase&&value)){
                this.UiSelectionChanged?.Invoke(this,EventArgs.Empty);
            }
        }
    }

    public string StatusImageSource{
        get => this.statusImageSource;
        private set => this.SetProperty(ref this.statusImageSource,value);
    }

    public bool IsStatusIconVisible => !string.IsNullOrWhiteSpace(this.StatusImageSource)&&!this.IsRunningVisual;

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

    public string ResultCheckText{
        get => this.resultCheckText;
        private set => this.SetProperty(ref this.resultCheckText,value);
    }

    public string ResultSeedText{
        get => this.resultSeedText;
        private set => this.SetProperty(ref this.resultSeedText,value);
    }

    public string LogicalProcessorText{
        get => this.logicalProcessorText;
        private set => this.SetProperty(ref this.logicalProcessorText,value);
    }

    public string CoreKindLabel{
        get => this.coreKindLabel;
        private set => this.SetProperty(ref this.coreKindLabel,value);
    }

    public string CoreKindImageSource{
        get => this.coreKindImageSource;
        private set => this.SetProperty(ref this.coreKindImageSource,value);
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
        private set{
            if(this.SetProperty(ref this.isRunning,value)){
                this.RefreshStatusDisplay();
            }
        }
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
                this.OnPropertyChanged(nameof(this.IsStatusIconVisible));
            }
        }
    }

    public bool IsPendingVisual => BenchmarkProjectLiveStateLogic.IsPending(this.LiveState,this.Metrics.HasMeasuredValue);

    public bool IsRunningVisual => BenchmarkProjectLiveStateLogic.IsRunning(this.LiveState);

    public bool IsRowFinalCompleted => this.isRowFinalCompleted;

    public void SetRowFinalCompleted(bool value){
        if(this.SetProperty(ref this.isRowFinalCompleted,value,nameof(this.IsRowFinalCompleted))){
            this.RefreshStatusDisplay();
        }
    }

    public void ApplyMetrics(ProjectMetrics value,bool preserveRetainedDisplay = false){
        // preserveRetainedDisplay=true のときは、完了済みセルを Missing/待機に戻さない。
        // 途中で比較行が欠けても、最後に見えていた確定値を画面上に残す。
        if(!value.HasMeasuredValue&&preserveRetainedDisplay&&(this.metrics.HasMeasuredValue||this.isCompletedWithoutMeasurement||this.IsRunning)){
            this.isFailed=false;
            if(this.IsRunning){
                this.LiveState=BenchmarkProjectLiveState.Running;
            }else{
                this.isCompletedWithoutMeasurement=!this.metrics.HasMeasuredValue;
                this.LiveState=BenchmarkProjectLiveState.Completed;
            }
            this.OnPropertyChanged(nameof(this.IsPendingVisual));
            this.RefreshStatusDisplay();
            return;
        }

        if(!value.HasMeasuredValue){
            this.metrics=ProjectMetrics.Missing;
            this.isFailed=false;
            this.isCompletedWithoutMeasurement=false;
            this.TimeText=string.Empty;
            this.AllocatedText=string.Empty;
            this.ElementCountText=string.Empty;
            this.ExecutionText=string.Empty;
            this.FailureExecutionText=string.Empty;
            this.failureReasonText=string.Empty;
            this.ExecutionTooltipText=string.Empty;
            this.ResultCheckText=string.Empty;
            this.ResultSeedText=string.Empty;
            this.ClearCpuDisplay();
            this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
            this.OnPropertyChanged(nameof(this.IsPendingVisual));
            this.RefreshStatusDisplay();
            return;
        }

        this.metrics=value;
        this.isCompletedWithoutMeasurement=false;
        this.TimeText=FormatTime(value);
        this.AllocatedText=FormatAllocated(value);
        this.OnPropertyChanged(nameof(this.IsPendingVisual));
        if(value.HasMeasuredValue){
            this.isFailed=false;
        }
        this.RefreshStatusDisplay();
    }

    public void ApplyStoredResult(BenchmarkStoredResult result){
        var measuredAt = result.SourceUpdatedAt??result.MeasuredAt;
        this.lastMeasuredAt=measuredAt;
        var resultMetrics = result.ToProjectMetrics();
        if(resultMetrics.HasMeasuredValue){
            this.ApplySuccessfulDisplay(resultMetrics,result.ElementCount,measuredAt,BenchmarkMeasurementHighlightState.None);
            this.ClearFailureDisplay();
        }else{
            this.ApplyMetrics(resultMetrics);
            if(string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
                this.ApplyFailureDisplay(result.ElementCount,measuredAt,string.Empty);
            }else if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)){
                this.isCompletedWithoutMeasurement=true;
                this.ElementCountText=FormatElementCount(result.ElementCount);
                this.ExecutionText=FormatTimestamp(measuredAt);
                this.RefreshExecutionTooltipText();
            }
        }
        this.IsRunning=false;
        this.isFailed=string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        this.ResultCheckText=string.Empty;
        this.ResultSeedText=string.Empty;
        this.LiveState=BenchmarkProjectLiveState.Idle;
        this.ClearCpuDisplay();
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
        this.RefreshStatusDisplay();
    }

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu,DateTimeOffset completedAt,bool highlightCompleted = true){
        var isCompleted = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        var isFailed = string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        var isRunning = string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        if(isRunning){
            // Running は確定値の更新ではなく、実行中の状態表示だけを切り替える。
            this.IsRunning=true;
            this.isFailed=false;
            this.isCompletedWithoutMeasurement=false;
            this.FailureExecutionText=string.Empty;
            this.failureReasonText=string.Empty;
            if(result.ElementCount>0){
                this.ElementCountText=FormatElementCount(result.ElementCount);
            }
            this.ExecutionText=FormatTimestamp(BenchmarkRunTimestampDisplayLogic.ResolveRunningTimestamp(result.StartedAt,completedAt));
            this.RefreshExecutionTooltipText();
            this.ApplyCpuDisplay(cpu);
            this.LiveState=BenchmarkProjectLiveState.Running;
            this.RefreshStatusDisplay();
            return;
        }

        this.ResultCheckText=result.ResultCheckText;
        this.ResultSeedText=result.ResultSeedText;

        if(result.ElementCount<=0){
            // ElementCount=0 の callback は比較表/チャートの確定データとして扱わない。
            this.IsRunning=false;
            this.ApplyCpuDisplay(cpu,preserveWhenNull:true);
            this.LiveState=this.Metrics.HasMeasuredValue||this.isCompletedWithoutMeasurement
                ? BenchmarkProjectLiveState.Completed
                : BenchmarkProjectLiveState.Idle;
            this.RefreshStatusDisplay();
            return;
        }

        if(isCompleted&&result.MeanNanoseconds is not null){
            // Completed かつ測定値ありだけが、棒グラフと表の確定値を更新する。
            this.isFailed=false;
            this.isCompletedWithoutMeasurement=false;
            this.lastMeasuredAt=completedAt;
            this.ApplySuccessfulDisplay(
                ProjectMetrics.FromMeasurement(result.MeanNanoseconds,result.AllocatedBytesValue),
                result.ElementCount,
                completedAt,
                highlightCompleted ? BenchmarkMeasurementHighlightState.Fresh : BenchmarkMeasurementHighlightState.None);
            this.ClearFailureDisplay();
        }else if(isCompleted){
            // Completed でも測定値がない場合は、待機に戻さず「測定値なし」として終端表示する。
            this.isFailed=false;
            this.isCompletedWithoutMeasurement=true;
            this.lastMeasuredAt=completedAt;
            this.ElementCountText=FormatElementCount(result.ElementCount);
            this.ExecutionText=FormatTimestamp(completedAt);
            this.RefreshExecutionTooltipText();
            this.ClearFailureDisplay();
        }else if(isFailed){
            this.isFailed=true;
            this.isCompletedWithoutMeasurement=false;
            this.lastMeasuredAt=completedAt;
            this.ApplyFailureDisplay(result.ElementCount,completedAt,result.FailureReason);
        }
        this.IsRunning=false;
        this.ApplyCpuDisplay(cpu,preserveWhenNull:true);
        this.LiveState=isCompleted
            ? BenchmarkProjectLiveState.Completed
            : BenchmarkProjectLiveState.Idle;
        this.RefreshStatusDisplay();
    }

    public void ApplyResultCheck(string checkText,string seedText){
        this.ResultCheckText=checkText;
        this.ResultSeedText=seedText;
    }

    public void ApplySkipped(int stoppedAtElementCount,ProjectMetrics stoppedMetrics){
        this.ApplySkipped(stoppedAtElementCount,stoppedMetrics,null,null,false);
    }

    public void ApplySkipped(int stoppedAtElementCount,ProjectMetrics stoppedMetrics,CpuSelectionItem? cpu,DateTimeOffset? measuredAt,bool metadataMissing){
        // adaptive で止まった行は、低い N の completed metrics をそのまま表示する。
        this.metrics=stoppedMetrics;
        this.isCompletedWithoutMeasurement=false;
        this.TimeText=FormatTime(stoppedMetrics);
        this.AllocatedText=FormatAllocated(stoppedMetrics);
        this.IsRunning=false;
        this.isFailed=false;
        this.lastSuccessfulElementCount=stoppedAtElementCount;
        this.lastMeasuredAt=measuredAt;
        this.ElementCountText=FormatElementCount(stoppedAtElementCount);
        // 日時と論理コアは独立に判定する: 片方のメタデータが欠けていても、判明している方は表示し続ける。
        this.ExecutionText=measuredAt is null ? MissingMetadataText : FormatTimestamp(measuredAt.Value);
        this.FailureExecutionText=string.Empty;
        this.ApplyCpuDisplay(cpu,preserveWhenNull:true);
        this.RefreshExecutionTooltipText();
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
        this.LiveState=BenchmarkProjectLiveState.Completed;
        this.OnPropertyChanged(nameof(this.IsPendingVisual));
        this.OnPropertyChanged(nameof(this.LastSuccessfulElementCount));
        this.RefreshStatusDisplay();
    }

    public void ClearLiveState(){
        this.IsRunning=false;
        this.LiveState=this.Metrics.HasMeasuredValue||this.isCompletedWithoutMeasurement
            ? BenchmarkProjectLiveState.Completed
            : BenchmarkProjectLiveState.Idle;
        this.RefreshStatusDisplay();
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
        this.isFailed=false;
        this.isCompletedWithoutMeasurement=false;
        this.lastSuccessfulElementCount=null;
        this.lastMeasuredAt=null;
        this.lastFailedElementCount=null;
        this.lastFailedAt=null;
        this.failureReasonText=string.Empty;
        this.TimeText=string.Empty;
        this.AllocatedText=string.Empty;
        this.ElementCountText=string.Empty;
        this.ClearLiveState();
        if(this.HasCase){
            this.StatusImageSource=BenchmarkStatusImageNames.Waiting;
            this.StatusText="待機";
        this.ExecutionText=string.Empty;
        this.FailureExecutionText=string.Empty;
        this.failureReasonText=string.Empty;
        this.ExecutionTooltipText=string.Empty;
        this.ResultCheckText=string.Empty;
        this.ResultSeedText=string.Empty;
        this.ClearCpuDisplay();
            this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
            this.RefreshStatusDisplay();
        }
    }

    private void ApplySuccessfulDisplay(
        ProjectMetrics value,
        int elementCount,
        DateTimeOffset measuredAt,
        BenchmarkMeasurementHighlightState highlightState){
        this.metrics=value;
        this.isFailed=false;
        this.isCompletedWithoutMeasurement=false;
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

    private void ApplyFailureDisplay(int elementCount,DateTimeOffset failedAt,string failureReason){
        this.isFailed=true;
        this.isCompletedWithoutMeasurement=false;
        this.lastFailedElementCount=elementCount;
        this.lastFailedAt=failedAt;
        this.failureReasonText=BenchmarkFailureDisplayLogic.CreateTooltipReason(failureReason);
        this.ElementCountText=FormatElementCount(elementCount);
        this.FailureExecutionText="  "+FormatTimestamp(failedAt);
        this.RefreshExecutionTooltipText();
    }

    private void ClearFailureDisplay(){
        this.lastFailedElementCount=null;
        this.lastFailedAt=null;
        this.failureReasonText=string.Empty;
        this.FailureExecutionText=string.Empty;
        this.RefreshExecutionTooltipText();
    }

    private void RefreshExecutionTooltipText(){
        var failureText = this.lastFailedElementCount is { } failedN&&this.lastFailedAt is { } failedAt
            ? "失敗 "+FormatElementCount(failedN)+" "+FormatTimestamp(failedAt)
            : string.Empty;
        this.ExecutionTooltipText=string.Join(
            Environment.NewLine,
            new[] { FormatElementTooltip(this.ElementCountText,this.ExecutionText),failureText,this.failureReasonText }.Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    private void ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState value){
        if(this.SetProperty(ref this.measurementHighlightState,value,nameof(this.MeasurementHighlightState))){
            this.OnPropertyChanged(nameof(this.MeasurementHighlightState));
        }

        this.ExecutionTextColor=DefaultExecutionTextColor;
        this.MetricTextColor=DefaultMetricTextColor;
        this.ExecutionFontAttributes=FontAttributes.None;
        this.MetricFontAttributes=FontAttributes.None;
    }

    private void RefreshStatusDisplay(){
        var kind = BenchmarkStatusImageLogic.GetProjectKind(new BenchmarkStatusImageInput(
            this.HasCase,
            this.IsRunning,
            this.isFailed,
            this.Metrics.HasMeasuredValue,
            this.isCompletedWithoutMeasurement,
            this.IsSelected,
            this.IsRowFinalCompleted));
        this.StatusImageSource=BenchmarkStatusImageNames.FromKind(kind);
        if(!this.HasCase){
            this.StatusText="対象なし";
        }else if(this.IsRunning){
            this.StatusText="実行中";
        }else if(this.isFailed){
            this.StatusText="失敗";
        }else if(this.Metrics.HasMeasuredValue){
            this.StatusText="完了";
        }else if(this.isCompletedWithoutMeasurement){
            this.StatusText="測定値なし";
        }else if(this.IsSelected){
            this.StatusText="待機";
        }else{
            this.StatusText="予定なし";
        }

        this.OnPropertyChanged(nameof(this.IsStatusIconVisible));
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

    private void ApplyCpuDisplay(CpuSelectionItem? cpu,bool preserveWhenNull = false){
        if(cpu is null){
            // 終端状態の適用で CPU を解決できなかった場合は、実行中に表示していた論理コアを保持する
            // (実装未完了などで測定値が無くても、実行したコアは変わらないため表示し続ける)。
            if(!preserveWhenNull){
                this.ClearCpuDisplay();
            }
            return;
        }

        this.HasCpuDisplay=true;
        this.LogicalProcessorText=cpu.LogicalProcessorText;
        this.CoreKindLabel=cpu.CoreKindLabel;
        this.CoreKindImageSource=cpu.CoreKindImageSource;
        this.BadgeBackgroundColor=cpu.BadgeBackgroundColor;
        this.BadgeTextColor=cpu.BadgeTextColor;
    }

    private void ClearCpuDisplay(){
        this.HasCpuDisplay=false;
        this.LogicalProcessorText=string.Empty;
        this.CoreKindLabel=string.Empty;
        this.CoreKindImageSource=string.Empty;
        this.BadgeBackgroundColor=Color.FromArgb("#E2E8F0");
        this.BadgeTextColor=Color.FromArgb("#334155");
    }

    private static string FormatAllocated(ProjectMetrics metrics){
        return metrics.HasMeasuredValue&&metrics.AllocatedBytes is { } value
            ? BenchmarkFormatting.FormatBytes(value)
            : string.Empty;
    }
}

