using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;

public enum BenchmarkTab{
    Cpu,
    Chart,
}

public enum BenchmarkLowerTab{
    Cpu,
    Log,
    ImprovementPrompt,
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
    BenchmarkType,
    BenchmarkMethod,
    BenchmarkName,
    LogicalCpu,
    ArgumentCount,
    ArgumentIndex,
    ArgumentPage,
    ArgumentSegment,
    ArgumentMemory,
    ArgumentFraction,
    ArgumentInitial,
    ArgumentData,
    PairRatioTime,
    PairRatioAllocated,
    LeftTime,
    LeftAllocated,
    RightTime,
    RightAllocated,
}

public sealed class BenchmarkSortRule:ObservableObject{
    private BenchmarkGridSortColumn column;
    private bool descending;

    public BenchmarkSortRule(BenchmarkGridSortColumn column,bool descending = false){
        this.column=column;
        this.descending=descending;
    }

    public BenchmarkGridSortColumn Column{
        get => this.column;
        set{
            if(this.SetProperty(ref this.column,value)){
                this.OnPropertyChanged(nameof(this.ColumnText));
            }
        }
    }

    public bool Descending{
        get => this.descending;
        set{
            if(this.SetProperty(ref this.descending,value)){
                this.OnPropertyChanged(nameof(this.DirectionText));
            }
        }
    }

    public string ColumnText => GetColumnText(this.Column);

    public string DirectionText => this.Descending ? "降順" : "昇順";

    public static string GetColumnText(BenchmarkGridSortColumn column) =>
        column switch{
            BenchmarkGridSortColumn.BenchmarkType => "型",
            BenchmarkGridSortColumn.BenchmarkMethod => "ベンチマーク",
            BenchmarkGridSortColumn.BenchmarkName => "ベンチマーク",
            BenchmarkGridSortColumn.LogicalCpu => "CPU",
            BenchmarkGridSortColumn.ArgumentCount => "Count",
            BenchmarkGridSortColumn.ArgumentIndex => "Index",
            BenchmarkGridSortColumn.ArgumentPage => "Page",
            BenchmarkGridSortColumn.ArgumentSegment => "Segment",
            BenchmarkGridSortColumn.ArgumentMemory => "Memory",
            BenchmarkGridSortColumn.ArgumentFraction => "Fraction",
            BenchmarkGridSortColumn.ArgumentInitial => "Initial",
            BenchmarkGridSortColumn.ArgumentData => "Data",
            BenchmarkGridSortColumn.PairRatioTime => "時間比",
            BenchmarkGridSortColumn.PairRatioAllocated => "メモリ比",
            BenchmarkGridSortColumn.LeftTime => "Left 時間",
            BenchmarkGridSortColumn.LeftAllocated => "Left メモリ",
            BenchmarkGridSortColumn.RightTime => "Right 時間",
            BenchmarkGridSortColumn.RightAllocated => "Right メモリ",
            _ => column.ToString(),
        };
}

public sealed record BenchmarkSortColumnOption(BenchmarkGridSortColumn Column,string DisplayName);

/// <summary>選択/非選択タブヘッダーの配色を一元管理する。3つのタブストリップ(ソース/下段/チャート診断)で共有する。</summary>
public static class BenchmarkTabHeaderStyle{
    public static FontAttributes GetFontAttributes(bool isSelected) => isSelected ? FontAttributes.Bold : FontAttributes.None;
    public static Color GetTextColor(bool isSelected) => isSelected ? Color.FromArgb("#0F172A") : Color.FromArgb("#475569");
    public static Color GetBackgroundColor(bool isSelected) => isSelected ? Colors.White : Color.FromArgb("#F1F5F9");
    public static Brush GetStroke(bool isSelected) => new SolidColorBrush(isSelected ? Color.FromArgb("#0F766E") : Color.FromArgb("#CBD5E1"));
}

/// <summary>タブヘッダーの選択状態を持つ共通基底。SourceTabOption と TabHeaderOption が実装する。</summary>
public abstract class TabHeaderOptionBase:ObservableObject{
    private bool isSelected;

    public bool IsSelected{
        get => this.isSelected;
        private set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.OnPropertyChanged(nameof(this.HeaderFontAttributes));
                this.OnPropertyChanged(nameof(this.HeaderTextColor));
                this.OnPropertyChanged(nameof(this.HeaderBackgroundColor));
                this.OnPropertyChanged(nameof(this.HeaderStroke));
            }
        }
    }

    public abstract string HeaderText { get; }
    public FontAttributes HeaderFontAttributes => BenchmarkTabHeaderStyle.GetFontAttributes(this.IsSelected);
    public Color HeaderTextColor => BenchmarkTabHeaderStyle.GetTextColor(this.IsSelected);
    public Color HeaderBackgroundColor => BenchmarkTabHeaderStyle.GetBackgroundColor(this.IsSelected);
    public Brush HeaderStroke => BenchmarkTabHeaderStyle.GetStroke(this.IsSelected);

    public void SetSelected(bool value){
        this.IsSelected=value;
    }
}

/// <summary>進捗リング等の付帯情報を持たない、静的タイトルのタブヘッダー(下段/チャート診断タブ用)。</summary>
public sealed class TabHeaderOption:TabHeaderOptionBase{
    public TabHeaderOption(string headerText){
        this.HeaderText=headerText;
    }

    public override string HeaderText { get; }
}

public sealed class SourceTabOption:TabHeaderOptionBase{
    private static readonly Color RunningIndicatorColor=Color.FromArgb("#00FF00");
    private static readonly Color CompletedProgressOutlineColor=Color.FromArgb("#22C55E");
    private static readonly Color DefaultProgressOutlineColor=Color.FromArgb("#64748B");
    private BenchmarkTabProgress progress;
    private bool isRunning;
    private bool isFinalCompleted;

    public SourceTabOption(BenchmarkSourceTabDefinition definition){
        this.Definition=definition;
    }

    public BenchmarkSourceTabDefinition Definition { get; }
    public BenchmarkSourceTab Tab => this.Definition.Tab;
    public string Id => this.Definition.Id;
    public string DisplayName => this.Definition.DisplayName;
    public BenchmarkTabProgress Progress{
        get => this.progress;
        private set{
            if(this.SetProperty(ref this.progress,value)){
                this.OnPropertyChanged(nameof(this.HeaderText));
                this.OnPropertyChanged(nameof(this.HasProgress));
                this.OnPropertyChanged(nameof(this.IsCompleted));
                this.OnPropertyChanged(nameof(this.IsCompletionIconVisible));
                this.OnPropertyChanged(nameof(this.CompletionIconSource));
                this.OnPropertyChanged(nameof(this.ProgressFraction));
                this.OnPropertyChanged(nameof(this.ProgressText));
                this.OnPropertyChanged(nameof(this.ProgressTooltipText));
                this.OnPropertyChanged(nameof(this.ProgressOutlineColor));
            }
        }
    }
    public override string HeaderText => this.DisplayName;
    public bool HasProgress => this.Progress.Total>0;
    public bool IsCompleted => this.Progress.Total>0&&this.Progress.Completed>=this.Progress.Total;
    public bool IsFinalCompleted{
        get => this.isFinalCompleted;
        private set{
            if(this.SetProperty(ref this.isFinalCompleted,value)){
                this.OnPropertyChanged(nameof(this.CompletionIconSource));
                this.OnPropertyChanged(nameof(this.ProgressTooltipText));
            }
        }
    }
    public bool IsCompletionIconVisible => this.IsCompleted&&!this.IsRunning;
    public string CompletionIconSource => this.IsFinalCompleted
        ? BenchmarkStatusImageNames.FinalCompleted
        : BenchmarkStatusImageNames.Completed;
    public double ProgressFraction => this.Progress.Total<=0 ? 0 : this.Progress.Fraction;
    public string ProgressText => this.Progress.Total<=0 ? "0/0" : this.Progress.FractionText;
    public string ProgressTooltipText => this.IsRunning
        ? "実行中 "+this.ProgressText
        : this.IsCompleted ? "完了 "+this.ProgressText : "確定 "+this.ProgressText;
    public Color ProgressOutlineColor => this.IsRunning
        ? RunningIndicatorColor
        : this.IsCompleted ? CompletedProgressOutlineColor : DefaultProgressOutlineColor;
    public bool IsRunning{
        get => this.isRunning;
        private set{
            if(this.SetProperty(ref this.isRunning,value)){
                this.OnPropertyChanged(nameof(this.ProgressOutlineColor));
                this.OnPropertyChanged(nameof(this.ProgressTooltipText));
                this.OnPropertyChanged(nameof(this.IsCompletionIconVisible));
                this.OnPropertyChanged(nameof(this.CompletionIconSource));
            }
        }
    }

    public void SetProgress(BenchmarkTabProgress value){
        this.Progress=value;
        if(value.Total<=0||value.Completed<value.Total){
            this.SetFinalCompleted(false);
        }
    }

    public void SetFinalCompleted(bool value){
        this.IsFinalCompleted=value;
    }

    public void SetRunning(bool value){
        this.IsRunning=value;
    }
}

public sealed class ObservableRangeCollection<T>:ObservableCollection<T>{
    public void ReplaceRange(IEnumerable<T> items){
        this.CheckReentrancy();
        this.Items.Clear();
        foreach(var item in items){
            this.Items.Add(item);
        }
        this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.Count)));
        this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

public sealed class BenchmarkSourceTabViewState:ObservableObject{
    private double chartHeight = 300;
    private string benchmarkRatioSummaryText = string.Empty;
    private string chartEmptyReasonText = "ベンチマークを実行するか、保存済み DB 実行を選択してください。";
    private string chartEmptyReasonTooltipText = "計測結果または保存済み DB 実行が選択されると比較チャートを表示します。";

    public BenchmarkSourceTabViewState(BenchmarkSourceTabDefinition definition){
        this.Definition=definition;
    }

    public BenchmarkSourceTabDefinition Definition { get; }
    public ObservableRangeCollection<BenchmarkTableRow> Rows { get; } = [];
    public ObservableCollection<ChartComparisonPoint> ChartPoints { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> LatencyHotspotRows { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> AllocationDiagnosticRows { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> CpuTimelineRows { get; } = [];
    public ObservableCollection<BenchmarkRankedRatioPoint> ImprovementCandidateRankPoints { get; } = [];
    public ObservableCollection<BenchmarkRankedRatioPoint> TimeRatioWorstPoints { get; } = [];
    public ObservableCollection<BenchmarkRankedRatioPoint> AllocationRatioWorstPoints { get; } = [];
    public ObservableCollection<BenchmarkCategoryRatioPoint> CategoryRatioPoints { get; } = [];
    public ObservableCollection<BenchmarkBadgeItem> MissingFailureBadges { get; } = [];
    public ObservableCollection<BenchmarkQualityGaugePoint> MeasurementQualityGauges { get; } = [];
    public ObservableCollection<BenchmarkBadgeItem> FallbackDiagnosticBadges { get; } = [];
    public ObservableCollection<BenchmarkTrendSeries> HistoryTrendSeries { get; } = [];
    public ObservableCollection<BenchmarkTrendSeries> ScaleTrendSeries { get; } = [];
    public IReadOnlyList<BenchmarkComparisonRow> ComparisonRows { get; set; } = [];

    public double ChartHeight{
        get => this.chartHeight;
        set => this.SetProperty(ref this.chartHeight,value);
    }

    public string BenchmarkRatioSummaryText{
        get => this.benchmarkRatioSummaryText;
        set => this.SetProperty(ref this.benchmarkRatioSummaryText,value);
    }

    public string ChartEmptyReasonText{
        get => this.chartEmptyReasonText;
        set => this.SetProperty(ref this.chartEmptyReasonText,value);
    }

    public string ChartEmptyReasonTooltipText{
        get => this.chartEmptyReasonTooltipText;
        set => this.SetProperty(ref this.chartEmptyReasonTooltipText,value);
    }
}

public sealed record ExecutionProfileOption(string DisplayName,BenchmarkExecutionProfile Profile);

public sealed record ScaleOption(string DisplayName,string RunValue);

public sealed record MetricOption(string DisplayName,BenchmarkMetricKind Kind);

public sealed record ChartModeOption(string DisplayName,BenchmarkChartMode Mode);

public sealed record ResultElementCountOption(int Value,string DisplayName);

public sealed record ThresholdOption(string DisplayName,int Milliseconds);

public sealed record BenchmarkDiagnosticRow(
    string Category,
    string Method,
    string WorstN,
    string RatioText,
    string LeftText,
    string RightText,
    string DetailText);

public sealed record BenchmarkAutoScrollRequest(BenchmarkSourceTab SourceTab,string MethodName,string RowKey,int Sequence);

public sealed class BenchmarkImprovementCandidateRow:ObservableObject{
    private bool isSelected;

    public BenchmarkImprovementCandidateRow(BenchmarkImprovementCandidate candidate,bool isSelected){
        this.Candidate=candidate;
        this.isSelected=isSelected;
    }

    public event EventHandler? SelectionChanged;

    internal BenchmarkImprovementCandidate Candidate { get; }
    public string Key => this.Candidate.Key;
    public string Category => this.Candidate.Category;
    public string MethodName => this.Candidate.MethodName;
    public string SourceTabText => this.Candidate.SourceTab.DisplayName;
    public string WorstElementCountText =>
        this.Candidate.TimeElementCount==this.Candidate.AllocatedElementCount
            ? this.Candidate.TimeElementCount.ToString("N0",CultureInfo.InvariantCulture)
            : this.Candidate.TimeElementCount.ToString("N0",CultureInfo.InvariantCulture)+"/"+this.Candidate.AllocatedElementCount.ToString("N0",CultureInfo.InvariantCulture);
    public string TimeRatioText => BenchmarkImprovementCandidateBuilder.FormatRatio(this.Candidate.TimeRatio);
    public string AllocatedRatioText => BenchmarkImprovementCandidateBuilder.FormatRatio(this.Candidate.AllocatedRatio);
    public string TimeGaugeText => CreateGaugeText("時間",this.Candidate.TimeRatio);
    public string AllocatedGaugeText => CreateGaugeText("メモリ",this.Candidate.AllocatedRatio);
    public double TimeGaugeFraction => CreateGaugeFraction(this.Candidate.TimeRatio);
    public double AllocatedGaugeFraction => CreateGaugeFraction(this.Candidate.AllocatedRatio);
    public double WorstLeftRatioSortValue => Math.Max(
        BenchmarkImprovementCandidateBuilder.GetRankValue(this.Candidate.TimeRatio),
        BenchmarkImprovementCandidateBuilder.GetRankValue(this.Candidate.AllocatedRatio));
    public string LeftTimeText => BenchmarkImprovementCandidateBuilder.FormatTime(this.Candidate.TimeLeft.TimeNanoseconds);
    public string RightTimeText => BenchmarkImprovementCandidateBuilder.FormatTime(this.Candidate.TimeRight.TimeNanoseconds);
    public string LeftAllocatedText => BenchmarkImprovementCandidateBuilder.FormatAllocated(this.Candidate.AllocatedLeft.AllocatedBytes);
    public string RightAllocatedText => BenchmarkImprovementCandidateBuilder.FormatAllocated(this.Candidate.AllocatedRight.AllocatedBytes);
    public string TooltipText =>
        this.SourceTabText+"\n"
        +this.Candidate.SourceTab.LeftProject+" / "+this.Candidate.SourceTab.RightProject+"\n"
        +"Time "+this.TimeRatioText+" / Alloc "+this.AllocatedRatioText;

    private static double CreateGaugeFraction(RatioValue ratio) =>
        ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => Math.Clamp(value/3.0,0,1),
            RatioValueKind.LeftOnly => 1.0,
            _ => 0.0,
        };

    private static string CreateGaugeText(string label,RatioValue ratio) =>
        ratio.Kind==RatioValueKind.Numeric
            ? label+" "+BenchmarkImprovementCandidateBuilder.FormatRatio(ratio)+"x"
            : label+" "+BenchmarkImprovementCandidateBuilder.FormatRatio(ratio);

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.SelectionChanged?.Invoke(this,EventArgs.Empty);
            }
        }
    }
}

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
            RatioValueKind.LeftOnly => FromFiniteOrInfinity(double.PositiveInfinity),
            RatioValueKind.RightOnly => FromNumber(0),
            _ => Missing,
        };
    }

    public static BenchmarkSortValue FromNumber(double? value){
        return value is { } number&&double.IsFinite(number)
            ? new(null,number,false)
            : Missing;
    }

    public static BenchmarkSortValue FromFiniteOrInfinity(double? value){
        return value is { } number&&!double.IsNaN(number)
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

    public BenchmarkProjectCell(string project,bool hasCase){
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
    public int? LastFailedElementCount => this.lastFailedElementCount;
    public DateTimeOffset? LastFailedAt => this.lastFailedAt;
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

    // 保存Runロードでは、DBの1行を画面グリッド1行としてそのまま復元する。
    // CPU/日時/要素数も現在のCPUリストやカタログに再照合せず、保存値を表示値として反映する。
    public void ApplySnapshotResult(BenchmarkResultItem result,int? cpuNumber,DateTimeOffset? measuredAt){
        var isCompleted = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        var isFailed = string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        this.ResultCheckText=result.ResultCheckText;
        this.ResultSeedText=result.ResultSeedText;
        this.IsRunning=false;
        this.LiveState=isCompleted
            ? BenchmarkProjectLiveState.Completed
            : BenchmarkProjectLiveState.Idle;
        if(isCompleted&&result.MeanNanoseconds is not null){
            this.metrics=ProjectMetrics.FromMeasurement(result.MeanNanoseconds,result.AllocatedBytesValue);
            this.isFailed=false;
            this.isCompletedWithoutMeasurement=false;
            this.lastSuccessfulElementCount=result.ElementCount>0 ? result.ElementCount : null;
            this.lastMeasuredAt=measuredAt;
            this.TimeText=FormatTime(this.metrics);
            this.AllocatedText=FormatAllocated(this.metrics);
            this.ElementCountText=result.ElementCount>0 ? FormatElementCount(result.ElementCount) : string.Empty;
            this.ExecutionText=measuredAt is { } completedAt ? FormatTimestamp(completedAt) : string.Empty;
            this.ClearFailureDisplay();
        }else if(isCompleted){
            this.metrics=ProjectMetrics.Missing;
            this.isFailed=false;
            this.isCompletedWithoutMeasurement=true;
            this.lastSuccessfulElementCount=result.ElementCount>0 ? result.ElementCount : null;
            this.lastMeasuredAt=measuredAt;
            this.TimeText=string.Empty;
            this.AllocatedText=string.Empty;
            this.ElementCountText=result.ElementCount>0 ? FormatElementCount(result.ElementCount) : string.Empty;
            this.ExecutionText=measuredAt is { } completedAt ? FormatTimestamp(completedAt) : string.Empty;
            this.ClearFailureDisplay();
        }else if(isFailed){
            this.metrics=ProjectMetrics.Missing;
            this.isFailed=true;
            this.isCompletedWithoutMeasurement=false;
            this.lastFailedElementCount=result.ElementCount>0 ? result.ElementCount : null;
            this.lastFailedAt=measuredAt;
            this.lastMeasuredAt=measuredAt;
            this.TimeText=string.Empty;
            this.AllocatedText=string.Empty;
            this.ElementCountText=result.ElementCount>0 ? FormatElementCount(result.ElementCount) : string.Empty;
            this.FailureExecutionText=measuredAt is { } failedAt ? "  "+FormatTimestamp(failedAt) : string.Empty;
            this.failureReasonText=BenchmarkFailureDisplayLogic.CreateTooltipReason(result.FailureReason);
            this.ExecutionText=string.Empty;
        }else{
            this.ApplyMetrics(ProjectMetrics.Missing);
        }

        this.ApplyCpuNumberDisplay(cpuNumber);
        this.ApplyMeasurementHighlight(BenchmarkMeasurementHighlightState.None);
        this.RefreshExecutionTooltipText();
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

    private void ApplyCpuNumberDisplay(int? cpuNumber){
        if(cpuNumber is not { } value){
            this.ClearCpuDisplay();
            return;
        }

        this.HasCpuDisplay=true;
        this.LogicalProcessorText=value.ToString(CultureInfo.InvariantCulture);
        this.CoreKindLabel=string.Empty;
        this.CoreKindImageSource=string.Empty;
        this.BadgeBackgroundColor=Color.FromArgb("#E2E8F0");
        this.BadgeTextColor=Color.FromArgb("#334155");
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
    private string pairRatioTimeText = string.Empty;
    private string pairRatioAllocatedText = string.Empty;
    private string stopNText = string.Empty;
    private Color stopNTextColor = DefaultStopNTextColor;
    private FontAttributes stopNFontAttributes;
    private string leftTimeText = string.Empty;
    private string leftAllocatedText = string.Empty;
    private string rightTimeText = string.Empty;
    private string rightAllocatedText = string.Empty;
    private string benchmarkName = string.Empty;
    private string benchmarkTypeText = string.Empty;
    private string benchmarkMethodText = string.Empty;
    private readonly string snapshotBenchmarkTypeText;
    private bool hasCpuDisplay;
    private string logicalProcessorText = string.Empty;
    private string coreKindLabel = string.Empty;
    private string coreKindImageSource = string.Empty;
    private Color badgeBackgroundColor = Color.FromArgb("#E2E8F0");
    private Color badgeTextColor = Color.FromArgb("#334155");
    private double? logicalCpuSortValue;
    private bool isFinalCompleted;
    private static readonly Color DefaultStopNTextColor = Color.FromArgb("#334155");

    public BenchmarkTableRow(
        string rowKey,
        string methodName,
        string benchmarkName,
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject)
        : this(rowKey,methodName,benchmarkName,sourceTab,casesByProject,string.Empty){
    }

    public BenchmarkTableRow(
        string rowKey,
        string methodName,
        string benchmarkName,
        BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyDictionary<string,BenchmarkCase> casesByProject,
        string snapshotBenchmarkTypeText){
        this.RowKey=rowKey;
        this.MethodName=methodName;
        this.benchmarkName=benchmarkName;
        // 保存Runの「型」は完全型名ではなく、グリッドの型列に出す表示文字列。
        // CreateBenchmarkTypeText("int") のような再フォーマットを通すと空になるため、明示値を保持する。
        this.snapshotBenchmarkTypeText=snapshotBenchmarkTypeText;
        this.SourceTab=sourceTab;
        this.CasesByProject=casesByProject;
        this.RefreshBenchmarkDisplayParts();
        this.LeftCell=this.CreateCell(sourceTab.LeftProject);
        this.RightCell=this.CreateCell(sourceTab.RightProject);
        foreach(var cell in this.ProjectCells){
            cell.UiSelectionChanged+=this.OnCellUiSelectionChanged;
            cell.PropertyChanged+=this.OnCellPropertyChanged;
        }
        this.RefreshUiSelectionState();
    }

    public string RowKey { get; }
    public string MethodName { get; }
    public string BenchmarkName{
        get => this.benchmarkName;
        private set{
            if(this.SetProperty(ref this.benchmarkName,value)){
                this.RefreshBenchmarkDisplayParts();
            }
        }
    }
    public string BenchmarkTypeText{
        get => this.benchmarkTypeText;
        private set => this.SetProperty(ref this.benchmarkTypeText,value);
    }
    public string BenchmarkMethodText{
        get => this.benchmarkMethodText;
        private set => this.SetProperty(ref this.benchmarkMethodText,value);
    }
    public bool HasCpuDisplay{
        get => this.hasCpuDisplay;
        private set => this.SetProperty(ref this.hasCpuDisplay,value);
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
    public Color BadgeBackgroundColor{
        get => this.badgeBackgroundColor;
        private set => this.SetProperty(ref this.badgeBackgroundColor,value);
    }
    public Color BadgeTextColor{
        get => this.badgeTextColor;
        private set => this.SetProperty(ref this.badgeTextColor,value);
    }
    public string CpuTooltipText => this.HasCpuDisplay
        ? (string.IsNullOrWhiteSpace(this.CoreKindLabel) ? "CPU" : this.CoreKindLabel)+" / CPU "+this.LogicalProcessorText
        : string.Empty;
    public double? LogicalCpuSortValue{
        get => this.logicalCpuSortValue;
        private set => this.SetProperty(ref this.logicalCpuSortValue,value);
    }
    public BenchmarkSourceTabDefinition SourceTab { get; }
    public IReadOnlyDictionary<string,BenchmarkCase> CasesByProject { get; }
    public BenchmarkProjectCell LeftCell { get; }
    public BenchmarkProjectCell RightCell { get; }
    public string CountArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Count);
    public string IndexArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Index);
    public string PageArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Page);
    public string SegmentArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Segment);
    public string MemoryArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Memory);
    public string FractionArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Fraction);
    public string InitialArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Initial);
    public string DataArgument => this.GetArgumentValue(ScalableBenchmarkArgumentColumn.Data);

    public bool IsSelected{
        get => this.isSelected;
        set => this.SetProperty(ref this.isSelected,value);
    }

    public bool IsUiSelected{
        get => this.isUiSelected;
        private set => this.SetProperty(ref this.isUiSelected,value);
    }

    public bool IsLeftUiSelected => this.LeftCell.IsUiSelected;
    public bool IsRightUiSelected => this.RightCell.IsUiSelected;
    public bool IsLeftPartialUiSelected => this.LeftCell.IsUiSelected&&!this.IsUiSelected;
    public bool IsRightPartialUiSelected => this.RightCell.IsUiSelected&&!this.IsUiSelected;

    public bool? IsSelectionChecked{
        get => this.isSelectionChecked;
        private set => this.SetProperty(ref this.isSelectionChecked,value);
    }

    public bool IsFinalCompleted{
        get => this.isFinalCompleted;
        private set{
            if(this.SetProperty(ref this.isFinalCompleted,value)){
                foreach(var cell in this.ProjectCells){
                    cell.SetRowFinalCompleted(value);
                }
            }
        }
    }

    public string PairRatioTimeText{
        get => this.pairRatioTimeText;
        private set => this.SetProperty(ref this.pairRatioTimeText,value);
    }

    public string PairRatioAllocatedText{
        get => this.pairRatioAllocatedText;
        private set => this.SetProperty(ref this.pairRatioAllocatedText,value);
    }

    public string StopNText => string.IsNullOrWhiteSpace(this.stopNText)
        ? this.ElementCountText
        : this.stopNText;

    public double? ElementCountSortValue{
        get{
            var values = new[] {
                this.LeftCell.LastSuccessfulElementCount,
                this.RightCell.LastSuccessfulElementCount,
            }.Where(static value => value is not null).Select(static value => (double)value!.Value).ToArray();
            return values.Length==0 ? null : values.Max();
        }
    }

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

    public Color StopNTextColor{
        get => this.stopNTextColor;
        private set => this.SetProperty(ref this.stopNTextColor,value);
    }

    public FontAttributes StopNFontAttributes{
        get => this.stopNFontAttributes;
        private set => this.SetProperty(ref this.stopNFontAttributes,value);
    }

    public string LeftTimeText{
        get => this.leftTimeText;
        private set => this.SetProperty(ref this.leftTimeText,value);
    }

    public string LeftAllocatedText{
        get => this.leftAllocatedText;
        private set => this.SetProperty(ref this.leftAllocatedText,value);
    }

    public string RightTimeText{
        get => this.rightTimeText;
        private set => this.SetProperty(ref this.rightTimeText,value);
    }

    public string RightAllocatedText{
        get => this.rightAllocatedText;
        private set => this.SetProperty(ref this.rightAllocatedText,value);
    }

    public IReadOnlyList<BenchmarkCase> GetRunCases(){
        // 保存Runスナップショットの型列は表示値であり、worker実行用の完全型名ではない。
        // 現行カタログへ一意接続できなかったセルは TypeFullName を空にして保持し、計測キューには入れない。
        return this.ProjectCells
            .Where(static cell => cell.HasCase&&cell.IsSelected)
            .Select(cell => this.CasesByProject[cell.Project])
            .Where(static benchmark => !string.IsNullOrWhiteSpace(benchmark.TypeFullName))
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
        this.OnPropertyChanged(nameof(this.IsLeftUiSelected));
        this.OnPropertyChanged(nameof(this.IsRightUiSelected));
        this.OnPropertyChanged(nameof(this.IsLeftPartialUiSelected));
        this.OnPropertyChanged(nameof(this.IsRightPartialUiSelected));
    }

    public void SetFinalCompleted(bool value){
        this.IsFinalCompleted=value;
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
            this.SetProjectMetricText(result.Project,FormatTime(cell.Metrics),FormatAllocated(cell.Metrics));
            this.RefreshDisplayedRatios();
        }
    }

    public void ApplyRunResult(BenchmarkResultItem result,CpuSelectionItem? cpu,DateTimeOffset completedAt,bool highlightCompleted = true){
        this.ApplyCpuDisplay(cpu,preserveWhenNull:true);
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplyRunResult(result,cpu,completedAt,highlightCompleted);
            this.SetProjectMetricText(result.Project,FormatTime(cell.Metrics),FormatAllocated(cell.Metrics));
            this.RefreshDisplayedRatios();
        }
    }

    public void ApplySnapshotResult(BenchmarkResultItem result,int? cpuNumber,DateTimeOffset? measuredAt){
        this.ApplyCpuNumberDisplay(cpuNumber);
        if(this.GetProjectCell(result.Project) is { } cell){
            cell.ApplySnapshotResult(result,cpuNumber,measuredAt);
            this.SetProjectMetricText(result.Project,FormatTime(cell.Metrics),FormatAllocated(cell.Metrics));
            this.RefreshDisplayedRatios();
        }
    }

    public void ApplySnapshotResult(string project,BenchmarkResultItem result,int? cpuNumber,DateTimeOffset? measuredAt){
        this.ApplyCpuNumberDisplay(cpuNumber);
        if(this.GetProjectCell(project) is { } cell){
            cell.ApplySnapshotResult(result,cpuNumber,measuredAt);
            this.SetProjectMetricText(project,FormatTime(cell.Metrics),FormatAllocated(cell.Metrics));
            this.RefreshDisplayedRatios();
        }
    }

    public void ApplyResultCheck(string project,string checkText,string seedText){
        this.GetProjectCell(project)?.ApplyResultCheck(checkText,seedText);
    }

    public void ApplySkipped(string project,int stoppedAtElementCount,ProjectMetrics stoppedMetrics){
        this.ApplySkipped(project,stoppedAtElementCount,stoppedMetrics,null,null,false);
    }

    public void ApplySkipped(string project,int stoppedAtElementCount,ProjectMetrics stoppedMetrics,CpuSelectionItem? cpu,DateTimeOffset? measuredAt,bool metadataMissing){
        this.ApplyCpuDisplay(cpu,preserveWhenNull:true);
        if(this.GetProjectCell(project) is { } cell){
            cell.ApplySkipped(stoppedAtElementCount,stoppedMetrics,cpu,measuredAt,metadataMissing);
        }

        this.SetProjectMetricText(project,FormatTime(stoppedMetrics),FormatAllocated(stoppedMetrics));
        this.RefreshDisplayedRatios();
    }

    public void ApplyPairStopped(string leftProject,string rightProject,int stoppedAtElementCount,bool highlight){
        var text = stoppedAtElementCount.ToString(CultureInfo.InvariantCulture);
        if(IsPair(leftProject,rightProject,this.SourceTab.LeftProject,this.SourceTab.RightProject)){
            this.stopNText=text;
            this.ApplyStopNStyle(highlight);
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
        this.SetFinalCompleted(false);
        this.ClearCpuDisplay();
    }

    public void ClearUnselectedResultDisplays(){
        foreach(var cell in this.ProjectCells){
            if(!cell.IsSelected){
                cell.ClearMetrics();
            }
        }
        this.LeftTimeText=FormatTime(this.LeftCell.Metrics);
        this.LeftAllocatedText=FormatAllocated(this.LeftCell.Metrics);
        this.RightTimeText=FormatTime(this.RightCell.Metrics);
        this.RightAllocatedText=FormatAllocated(this.RightCell.Metrics);
        this.RefreshDisplayedRatios();
    }

    public void ApplyComparison(BenchmarkComparisonRow? row,bool preserveRetainedDisplay = false){
        if(row is null){
            this.ClearComparison(preserveRetainedDisplay);
            return;
        }

        var value = row.Value;
        // 比較行の値があるときだけ比較ラベルを更新し、セルの保持値は別に残す。
        this.ClearStopNText();
        var left = value.GetProject(this.SourceTab.LeftProject);
        var right = value.GetProject(this.SourceTab.RightProject);
        this.LeftCell.ApplyMetrics(left,preserveRetainedDisplay);
        this.RightCell.ApplyMetrics(right,preserveRetainedDisplay);
        this.LeftTimeText=FormatTime(this.LeftCell.Metrics);
        this.LeftAllocatedText=FormatAllocated(this.LeftCell.Metrics);
        this.RightTimeText=FormatTime(this.RightCell.Metrics);
        this.RightAllocatedText=FormatAllocated(this.RightCell.Metrics);
        this.RefreshDisplayedRatios();
    }

    public BenchmarkSortValue GetSortValue(BenchmarkGridSortColumn column){
        return column switch{
            BenchmarkGridSortColumn.BenchmarkType => BenchmarkSortValue.FromText(this.BenchmarkTypeText),
            BenchmarkGridSortColumn.BenchmarkMethod => BenchmarkSortValue.FromText(this.BenchmarkMethodText),
            BenchmarkGridSortColumn.BenchmarkName => BenchmarkSortValue.FromText(this.BenchmarkName),
            BenchmarkGridSortColumn.LogicalCpu => BenchmarkSortValue.FromNumber(this.LogicalCpuSortValue),
            BenchmarkGridSortColumn.ArgumentCount => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.CountArgument)),
            BenchmarkGridSortColumn.ArgumentIndex => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.IndexArgument)),
            BenchmarkGridSortColumn.ArgumentPage => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.PageArgument)),
            BenchmarkGridSortColumn.ArgumentSegment => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.SegmentArgument)),
            BenchmarkGridSortColumn.ArgumentMemory => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.MemoryArgument)),
            BenchmarkGridSortColumn.ArgumentFraction => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.FractionArgument)),
            BenchmarkGridSortColumn.ArgumentInitial => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.InitialArgument)),
            BenchmarkGridSortColumn.ArgumentData => BenchmarkSortValue.FromNumber(ParseArgumentNumber(this.DataArgument)),
            BenchmarkGridSortColumn.PairRatioTime => BenchmarkSortValue.FromRatio(this.GetDisplayedTimeRatio()),
            BenchmarkGridSortColumn.PairRatioAllocated => BenchmarkSortValue.FromRatio(this.GetDisplayedAllocatedRatio()),
            BenchmarkGridSortColumn.LeftTime => BenchmarkSortValue.FromMetric(this.LeftCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.LeftAllocated => BenchmarkSortValue.FromMetric(this.LeftCell.Metrics,BenchmarkMetricKind.Allocated),
            BenchmarkGridSortColumn.RightTime => BenchmarkSortValue.FromMetric(this.RightCell.Metrics,BenchmarkMetricKind.Time),
            BenchmarkGridSortColumn.RightAllocated => BenchmarkSortValue.FromMetric(this.RightCell.Metrics,BenchmarkMetricKind.Allocated),
            _ => BenchmarkSortValue.Missing,
        };
    }

    private void ClearComparison(bool preserveRetainedDisplay){
        this.PairRatioTimeText=string.Empty;
        this.PairRatioAllocatedText=string.Empty;
        this.stopNText=string.Empty;
        if(preserveRetainedDisplay){
            // 実行中/完了後は、比較行が消えてもセルの確定値を維持する。
            this.LeftTimeText=FormatTime(this.LeftCell.Metrics);
            this.LeftAllocatedText=FormatAllocated(this.LeftCell.Metrics);
            this.RightTimeText=FormatTime(this.RightCell.Metrics);
            this.RightAllocatedText=FormatAllocated(this.RightCell.Metrics);
            this.RefreshDisplayedRatios();
        }else{
            this.LeftTimeText=string.Empty;
            this.LeftAllocatedText=string.Empty;
            this.RightTimeText=string.Empty;
            this.RightAllocatedText=string.Empty;
        }
        this.OnPropertyChanged(nameof(this.StopNText));
        foreach(var cell in this.ProjectCells){
            if(!preserveRetainedDisplay){
                cell.ClearMetrics();
            }
        }
    }

    private void RefreshDisplayedRatios(){
        this.PairRatioTimeText=FormatRatio(this.GetDisplayedTimeRatio());
        this.PairRatioAllocatedText=FormatRatio(this.GetDisplayedAllocatedRatio());
    }

    private RatioValue GetDisplayedTimeRatio() =>
        BenchmarkComparisonFormatter.CalculateTimeRatio(this.LeftCell.Metrics,this.RightCell.Metrics);

    private RatioValue GetDisplayedAllocatedRatio() =>
        BenchmarkComparisonFormatter.CalculateAllocatedRatio(this.LeftCell.Metrics,this.RightCell.Metrics);

    private void SetProjectMetricText(string project,string timeText,string allocatedText){
        if(string.Equals(project,this.SourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)){
            this.LeftTimeText=timeText;
            this.LeftAllocatedText=allocatedText;
        }else if(string.Equals(project,this.SourceTab.RightProject,StringComparison.OrdinalIgnoreCase)){
            this.RightTimeText=timeText;
            this.RightAllocatedText=allocatedText;
        }
    }

    private void ClearStopNText(){
        this.stopNText=string.Empty;
        this.ApplyStopNStyle(false);
        this.OnPropertyChanged(nameof(this.StopNText));
    }

    private void ApplyCpuDisplay(CpuSelectionItem? cpu,bool preserveWhenNull = false){
        if(cpu is null){
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
        this.LogicalCpuSortValue=cpu.LogicalProcessorIndex;
        this.OnPropertyChanged(nameof(this.CpuTooltipText));
    }

    private void ApplyCpuNumberDisplay(int? cpuNumber){
        if(cpuNumber is not { } value){
            this.ClearCpuDisplay();
            return;
        }

        this.HasCpuDisplay=true;
        this.LogicalProcessorText=value.ToString(CultureInfo.InvariantCulture);
        this.CoreKindLabel=string.Empty;
        this.CoreKindImageSource=string.Empty;
        this.BadgeBackgroundColor=Color.FromArgb("#E2E8F0");
        this.BadgeTextColor=Color.FromArgb("#334155");
        this.LogicalCpuSortValue=value;
        this.OnPropertyChanged(nameof(this.CpuTooltipText));
    }

    private void ClearCpuDisplay(){
        this.HasCpuDisplay=false;
        this.LogicalProcessorText=string.Empty;
        this.CoreKindLabel=string.Empty;
        this.CoreKindImageSource=string.Empty;
        this.BadgeBackgroundColor=Color.FromArgb("#E2E8F0");
        this.BadgeTextColor=Color.FromArgb("#334155");
        this.LogicalCpuSortValue=null;
        this.OnPropertyChanged(nameof(this.CpuTooltipText));
    }

    private void ApplyStopNStyle(bool highlight){
        this.StopNTextColor=DefaultStopNTextColor;
        this.StopNFontAttributes=FontAttributes.None;
    }

    private static bool IsPair(string leftProject,string rightProject,string expectedLeft,string expectedRight) =>
        string.Equals(leftProject,expectedLeft,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(rightProject,expectedRight,StringComparison.OrdinalIgnoreCase);

    private BenchmarkProjectCell CreateCell(string project) =>
        new(project,this.CasesByProject.ContainsKey(project));

    public void RefreshBenchmarkName(int elementCount) {
        var typeFullName = this.FirstTypeFullName();
        this.BenchmarkName=ScalableBenchmarkNameFormatter.CreateBenchmarkName(typeFullName,this.MethodName);
    }

    private void RefreshBenchmarkDisplayParts(){
        var typeFullName = this.FirstTypeFullName();
        var typeText = ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText(typeFullName);
        if(!string.IsNullOrWhiteSpace(this.snapshotBenchmarkTypeText)){
            typeText=this.snapshotBenchmarkTypeText;
        }
        if(string.IsNullOrWhiteSpace(typeText)){
            typeText=ExtractTypeText(this.benchmarkName);
        }
        this.BenchmarkTypeText=typeText;
        this.BenchmarkMethodText=this.MethodName;
    }

    private string FirstTypeFullName(){
        foreach(var project in this.SourceTab.ProjectOrder){
            if(this.CasesByProject.TryGetValue(project,out var benchmark)&&!string.IsNullOrWhiteSpace(benchmark.TypeFullName)){
                return benchmark.TypeFullName;
            }
        }

        return string.Empty;
    }

    private static string ExtractTypeText(string benchmarkName){
        if(string.IsNullOrWhiteSpace(benchmarkName)||benchmarkName[0]!='<'){
            return string.Empty;
        }

        var end = benchmarkName.IndexOf('>');
        return end>1
            ? benchmarkName[1..end]
            : string.Empty;
    }

    private string GetArgumentValue(ScalableBenchmarkArgumentColumn column) =>
        this.CasesByProject.Values
            .Select(item => ScalableBenchmarkNameFormatter.GetDisplayArgumentValue(this.SourceTab,item.Arguments,column))
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;

    public string BenchmarkArgumentTooltipText => BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(this);

    private static double? ParseArgumentNumber(string text) =>
        double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)
            ? value
            : null;

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

internal static class BenchmarkArgumentTooltipFormatter {
    public static string CreateArgumentTooltip(BenchmarkTableRow row) {
        var items = BenchmarkArgumentColumns.Get(row.SourceTab)
            .Select(column => (column.ToString(),GetArgumentValue(row,column)))
            .ToArray();
        var summary = items
            .Where(static item => item.Item1 is "Count" or "Memory" or "Initial")
            .ToArray();
        var summaryText = string.Join(
            " / ",
            summary
                .Where(static item => !string.IsNullOrWhiteSpace(item.Item2))
                .Select(static item => item.Item1+"="+item.Item2));
        var detailText = string.Join(
            " / ",
            items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Item2))
                .Select(static item => item.Item1+"="+item.Item2));
        if(string.IsNullOrWhiteSpace(summaryText)) {
            return detailText;
        }

        return string.IsNullOrWhiteSpace(detailText)
            ? summaryText
            : summaryText+"\n"+detailText;
    }

    private static string GetArgumentValue(BenchmarkTableRow row,ScalableBenchmarkArgumentColumn column) =>
        column switch{
            ScalableBenchmarkArgumentColumn.Count => row.CountArgument,
            ScalableBenchmarkArgumentColumn.Index => row.IndexArgument,
            ScalableBenchmarkArgumentColumn.Page => row.PageArgument,
            ScalableBenchmarkArgumentColumn.Segment => row.SegmentArgument,
            ScalableBenchmarkArgumentColumn.Memory => row.MemoryArgument,
            ScalableBenchmarkArgumentColumn.Fraction => row.FractionArgument,
            ScalableBenchmarkArgumentColumn.Initial => row.InitialArgument,
            ScalableBenchmarkArgumentColumn.Data => row.DataArgument,
            _ => string.Empty,
        };
}

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
                ||args.PropertyName==nameof(CpuSelectionItem.StatusImageSource)
                ||args.PropertyName==nameof(CpuSelectionItem.RunStatusText)
                ||args.PropertyName==nameof(CpuSelectionItem.IsRunningJob)){
                this.OnPropertyChanged(nameof(this.DisplayText));
                this.OnPropertyChanged(nameof(this.LogicalProcessorText));
                this.OnPropertyChanged(nameof(this.CoreKindLabel));
                this.OnPropertyChanged(nameof(this.CellStatusText));
                this.OnPropertyChanged(nameof(this.BadgeBackgroundColor));
                this.OnPropertyChanged(nameof(this.BadgeTextColor));
                this.OnPropertyChanged(nameof(this.IsRunningJob));
                this.OnPropertyChanged(nameof(this.StatusImageSource));
                this.OnPropertyChanged(nameof(this.IsStatusImageVisible));
            }
        };
    }

    public CpuSelectionItem Cpu { get; }
    public string DisplayText => this.Cpu.StatusDisplayText;
    public string LogicalProcessorText => this.Cpu.LogicalProcessorText;
    public string CoreKindLabel => this.Cpu.CoreKindLabel;
    public string CoreKindImageSource => this.Cpu.CoreKindImageSource;
    public string CellStatusText => this.Cpu.CellStatusText;
    public string StatusImageSource => this.Cpu.StatusImageSource;
    public bool IsStatusImageVisible => !this.IsRunningJob&&!string.IsNullOrWhiteSpace(this.StatusImageSource);
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
    public string BadgeImageSource => BenchmarkCpuCoreImageNames.FromCoreKind(this.CoreKind);
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
    public string BadgeImageSource => BenchmarkCpuCoreImageNames.FromCoreKind(this.CoreKind);
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

public sealed class ChartComparisonPoint{
    public ChartComparisonPoint(
        string benchmark,
        string displayName,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio)
        : this(benchmark,displayName,string.Empty,displayName,false,false,leftValue,rightValue,leftText,rightText,tooltipText,ratio,false,false,false,false,false,false){
    }

    public ChartComparisonPoint(
        string benchmark,
        string displayName,
        string groupDisplayName,
        string rowDisplayName,
        bool isGroupFirst,
        bool isGroupLast,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio)
        : this(benchmark,displayName,groupDisplayName,rowDisplayName,isGroupFirst,isGroupLast,leftValue,rightValue,leftText,rightText,tooltipText,ratio,false,false,false,false,false,false){
    }

    private ChartComparisonPoint(
        string benchmark,
        string displayName,
        string groupDisplayName,
        string rowDisplayName,
        bool isGroupFirst,
        bool isGroupLast,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio,
        bool isRunning,
        bool isLeftUiSelected,
        bool isRightUiSelected,
        bool isRowUiSelected,
        bool isLeftFallback,
        bool isRightFallback){
        this.Benchmark=benchmark;
        this.BaseDisplayName=displayName;
        this.GroupDisplayName=groupDisplayName;
        this.RowDisplayName=rowDisplayName;
        this.IsGroupFirst=isGroupFirst;
        this.IsGroupLast=isGroupLast;
        this.LeftValue=leftValue;
        this.RightValue=rightValue;
        this.LeftText=leftText;
        this.RightText=rightText;
        this.TooltipText=tooltipText;
        this.Ratio=ratio;
        this.IsRunning=isRunning;
        this.IsLeftUiSelected=isLeftUiSelected;
        this.IsRightUiSelected=isRightUiSelected;
        this.IsRowUiSelected=isRowUiSelected;
        this.IsLeftFallback=isLeftFallback;
        this.IsRightFallback=isRightFallback;
    }

    public string Benchmark { get; }
    public string BaseDisplayName { get; }
    public string DisplayName => this.IsRunning
        ? "● "+this.BaseDisplayName
        : this.BaseDisplayName;
    public string GroupDisplayName { get; }
    public string RowDisplayName { get; }
    public bool IsGroupFirst { get; }
    public bool IsGroupLast { get; }
    public double LeftValue { get; }
    public double RightValue { get; }
    public double LeftNormalValue => this.IsLeftFallback ? 0 : this.LeftValue;
    public double LeftFallbackValue => this.IsLeftFallback ? this.LeftValue : 0;
    public double RightNormalValue => this.IsRightFallback ? 0 : this.RightValue;
    public double RightFallbackValue => this.IsRightFallback ? this.RightValue : 0;
    public string LeftText { get; }
    public string RightText { get; }
    public string TooltipText { get; }
    public double? Ratio { get; }
    public bool IsRunning { get; }
    public bool IsLeftUiSelected { get; }
    public bool IsRightUiSelected { get; }
    public bool IsRowUiSelected { get; }
    public bool IsLeftFallback { get; }
    public bool IsRightFallback { get; }

    public ChartComparisonPoint WithRunningState(bool isRunning) =>
        new(
            this.Benchmark,
            this.BaseDisplayName,
            this.GroupDisplayName,
            this.RowDisplayName,
            this.IsGroupFirst,
            this.IsGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            isRunning,
            this.IsLeftUiSelected,
            this.IsRightUiSelected,
            this.IsRowUiSelected,
            this.IsLeftFallback,
            this.IsRightFallback);

    public ChartComparisonPoint WithUiSelection(bool isLeftUiSelected,bool isRightUiSelected,bool isRowUiSelected) =>
        new(
            this.Benchmark,
            this.BaseDisplayName,
            this.GroupDisplayName,
            this.RowDisplayName,
            this.IsGroupFirst,
            this.IsGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            this.IsRunning,
            isLeftUiSelected,
            isRightUiSelected,
            isRowUiSelected,
            this.IsLeftFallback,
            this.IsRightFallback);

    public ChartComparisonPoint WithFallbackState(bool isLeftFallback,bool isRightFallback) =>
        new(
            this.Benchmark,
            this.BaseDisplayName,
            this.GroupDisplayName,
            this.RowDisplayName,
            this.IsGroupFirst,
            this.IsGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            this.IsRunning,
            this.IsLeftUiSelected,
            this.IsRightUiSelected,
            this.IsRowUiSelected,
            isLeftFallback,
            isRightFallback);

    public ChartComparisonPoint WithGroupDisplay(string groupDisplayName,string rowDisplayName,bool isGroupFirst,bool isGroupLast) =>
        new(
            this.Benchmark,
            rowDisplayName,
            groupDisplayName,
            rowDisplayName,
            isGroupFirst,
            isGroupLast,
            this.LeftValue,
            this.RightValue,
            this.LeftText,
            this.RightText,
            this.TooltipText,
            this.Ratio,
            this.IsRunning,
            this.IsLeftUiSelected,
            this.IsRightUiSelected,
            this.IsRowUiSelected,
            this.IsLeftFallback,
            this.IsRightFallback);
}

public sealed record BenchmarkRunReport(string? RunRoot);
