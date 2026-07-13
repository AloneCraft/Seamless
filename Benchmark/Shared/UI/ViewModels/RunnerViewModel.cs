using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Seamless.UI.Storage;
using UI;

namespace UI.ViewModels;

internal sealed class BenchmarkDataGridUpdateBatchEventArgs:EventArgs {
    public static readonly BenchmarkDataGridUpdateBatchEventArgs Begin = new(true);
    public static readonly BenchmarkDataGridUpdateBatchEventArgs End = new(false);
    private BenchmarkDataGridUpdateBatchEventArgs(bool isBeginning) => this.IsBeginning=isBeginning;
    public bool IsBeginning { get; }
}

public sealed partial class RunnerViewModel:ObservableObject {
    /// <summary>
    /// NewRunNumber フィールドです。
    /// </summary>
    private const int NewRunNumber = 0;
    /// <summary>
    /// UnsavedRunNumber フィールドです。
    /// </summary>
    private const int UnsavedRunNumber = -1;
    /// <summary>
    /// ExternalRunNumber フィールドです。
    /// </summary>
    private const int ExternalRunNumber = -2;
    /// <summary>
    /// BenchmarkChartRowHeight フィールドです。
    /// </summary>
    private const double BenchmarkChartRowHeight = 24;
    /// <summary>
    /// BenchmarkChartVerticalPadding フィールドです。
    /// </summary>
    private const double BenchmarkChartVerticalPadding = 82;
    /// <summary>
    /// BenchmarkChartMinimumHeight フィールドです。
    /// </summary>
    private const double BenchmarkChartMinimumHeight = 120;
    /// <summary>
    /// BenchmarkSortSettingsPreferenceKey フィールドです。
    /// </summary>
    private const string BenchmarkSortSettingsPreferenceKey = "Seamless.SortRules.v2";
    private static readonly BenchmarkRunOption NewRunOption = new(
        NewRunNumber,
        0,
        "新規",
        string.Empty,
        BenchmarkExecutionProfile.Fast,
        DateTimeOffset.MinValue,
        DateTimeOffset.MinValue,
        0,
        0,
        []);

    /// <summary>
    /// catalogService フィールドです。
    /// </summary>
    private readonly IBenchmarkCatalogService catalogService;
    /// <summary>
    /// cpuTopologyService フィールドです。
    /// </summary>
    private readonly ICpuTopologyService cpuTopologyService;
    /// <summary>
    /// runService フィールドです。
    /// </summary>
    private readonly IBenchmarkRunService runService;
    /// <summary>
    /// comparisonService フィールドです。
    /// </summary>
    private readonly IBenchmarkComparisonService comparisonService;
    /// <summary>
    /// uiRunStore フィールドです。
    /// </summary>
    private readonly UiRunStore uiRunStore;
    /// <summary>
    /// uiRunRepository フィールドです。
    /// </summary>
    private readonly BenchmarkUiRunRepository uiRunRepository;
    /// <summary>
    /// uiDispatcher フィールドです。
    /// </summary>
    private readonly IBenchmarkUiDispatcher uiDispatcher;
    /// <summary>
    /// uiPreferences フィールドです。
    /// </summary>
    private readonly IBenchmarkUiPreferences uiPreferences;
    /// <summary>
    /// runEventPipeline フィールドです。
    /// </summary>
    private readonly BenchmarkRunEventPipeline runEventPipeline;
    /// <summary>
    /// pipelineDiagnostics フィールドです。
    /// </summary>
    private readonly BenchmarkUiDiagnostics pipelineDiagnostics;
    /// <summary>
    /// renderScheduler フィールドです。
    /// </summary>
    private readonly BenchmarkUiRenderScheduler renderScheduler;
    /// <summary>
    /// persistenceBatcher フィールドです。
    /// </summary>
    private readonly BenchmarkPersistenceBatcher persistenceBatcher;
    /// <summary>
    /// runProjectionService フィールドです。
    /// </summary>
    private readonly BenchmarkRunProjectionService runProjectionService = new();
    /// <summary>
    /// benchmarkRoot フィールドです。
    /// </summary>
    private readonly string benchmarkRoot;
    /// <summary>
    /// runCancellation フィールドです。
    /// </summary>
    private CancellationTokenSource? runCancellation;
    /// <summary>
    /// activeRunRoot フィールドです。
    /// </summary>
    private string? activeRunRoot;
    /// <summary>
    /// pendingRunTerminationKind フィールドです。
    /// </summary>
    private RunTerminationKind pendingRunTerminationKind;
    private int runCommandGate;
    private int stopCommandGate;
    /// <summary>
    /// statusText フィールドです。
    /// </summary>
    private string statusText = "Ready";
    /// <summary>
    /// isBenchmarkRunning フィールドです。
    /// </summary>
    private bool isBenchmarkRunning;
    /// <summary>
    /// isRefreshing フィールドです。
    /// </summary>
    private bool isRefreshing;
    /// <summary>
    /// isAllBenchmarkSelectionChecked フィールドです。
    /// </summary>
    private bool? isAllBenchmarkSelectionChecked;
    /// <summary>
    /// hasSelectedBenchmarkCells フィールドです。
    /// </summary>
    private bool hasSelectedBenchmarkCells;
    /// <summary>
    /// selectedTab フィールドです。
    /// </summary>
    private BenchmarkTab selectedTab = BenchmarkTab.Cpu;
    /// <summary>
    /// selectedSourceTab フィールドです。
    /// </summary>
    private SourceTabOption selectedSourceTab;
    /// <summary>
    /// sourceTabViewStates フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkSourceTabViewState> sourceTabViewStates = [];
    /// <summary>
    /// sourceTabViewStatesByTab フィールドです。
    /// </summary>
    private readonly Dictionary<BenchmarkSourceTab,BenchmarkSourceTabViewState> sourceTabViewStatesByTab = [];
    /// <summary>
    /// selectedExecutionProfile フィールドです。
    /// </summary>
    private ExecutionProfileOption selectedExecutionProfile;
    /// <summary>
    /// selectedScale フィールドです。
    /// </summary>
    private ScaleOption? selectedScale;
    /// <summary>
    /// selectedThreshold フィールドです。
    /// </summary>
    private ThresholdOption selectedThreshold;
    /// <summary>
    /// selectedRunOption フィールドです。
    /// </summary>
    private BenchmarkRunOption? selectedRunOption;
    /// <summary>
    /// adaptiveThresholdSecondsText フィールドです。
    /// </summary>
    private string adaptiveThresholdSecondsText = "1";
    /// <summary>
    /// selectedMetric フィールドです。
    /// </summary>
    private MetricOption selectedMetric;
    /// <summary>
    /// selectedChartMode フィールドです。
    /// </summary>
    private ChartModeOption selectedChartMode;
    /// <summary>
    /// selectedResultElementCount フィールドです。
    /// </summary>
    private ResultElementCountOption? selectedResultElementCount;
    /// <summary>
    /// selectedResultElementCountIndex フィールドです。
    /// </summary>
    private double selectedResultElementCountIndex;
    /// <summary>
    /// currentElementCountText フィールドです。
    /// </summary>
    private string currentElementCountText = "1";
    /// <summary>
    /// improvementPromptText フィールドです。
    /// </summary>
    private string improvementPromptText = string.Empty;
    /// <summary>
    /// improvementCandidateSummaryText フィールドです。
    /// </summary>
    private string improvementCandidateSummaryText = string.Empty;
    private string 全体ベンチマークシェア概要文字列 = BenchmarkImprovementPromptBuilder.概要を作成([]).OverallDisplayText;
    private string 全体ベンチマークシェア概要短縮文字列 = BenchmarkImprovementPromptBuilder.概要を作成([]).ShortDisplayText;
    /// <summary>
    /// generatedCodeAuditReport フィールドです。
    /// </summary>
    private BenchmarkGeneratedCodeAuditReport? generatedCodeAuditReport;
    /// <summary>
    /// isApplyingCpuSelection フィールドです。
    /// </summary>
    private bool isApplyingCpuSelection;
    /// <summary>
    /// isApplyingUiState フィールドです。
    /// </summary>
    private bool isApplyingUiState;
    /// <summary>
    /// isApplyingBenchmarkSelection フィールドです。
    /// </summary>
    private bool isApplyingBenchmarkSelection;
    /// <summary>
    /// isApplyingResultElementCount フィールドです。
    /// </summary>
    private bool isApplyingResultElementCount;
    /// <summary>
    /// isApplyingRunOptionSelection フィールドです。
    /// </summary>
    private bool isApplyingRunOptionSelection;
    /// <summary>
    /// isApplyingSourceTabSelection フィールドです。
    /// </summary>
    private bool isApplyingSourceTabSelection;
    /// <summary>
    /// isApplyingSortRules フィールドです。
    /// </summary>
    private bool isApplyingSortRules;
    /// <summary>
    /// isLoadingSelectedRunDb フィールドです。
    /// </summary>
    private bool isLoadingSelectedRunDb;
    /// <summary>
    /// isRunHistoryLoading フィールドです。
    /// </summary>
    private bool isRunHistoryLoading;
    /// <summary>
    /// selectedRunLoadRequestToken フィールドです。
    /// </summary>
    private int selectedRunLoadRequestToken;
    /// <summary>
    /// isRefreshingBenchmarkSelectionDisplay フィールドです。
    /// </summary>
    private bool isRefreshingBenchmarkSelectionDisplay;
    /// <summary>
    /// hasPendingCloseStateSave フィールドです。
    /// </summary>
    private bool hasPendingCloseStateSave;
    /// <summary>
    /// hasUnsavedBenchmarkResults フィールドです。
    /// </summary>
    private bool hasUnsavedBenchmarkResults;
    /// <summary>
    /// hasBenchmarkTypeColumn フィールドです。
    /// </summary>
    private bool hasBenchmarkTypeColumn = true;
    /// <summary>
    /// currentSortColumn フィールドです。
    /// </summary>
    private BenchmarkGridSortColumn currentSortColumn = BenchmarkGridSortColumn.BenchmarkMethod;
    /// <summary>
    /// currentSortDescending フィールドです。
    /// </summary>
    private bool currentSortDescending;
    /// <summary>
    /// comparisonRows プロパティです。
    /// </summary>
    /// <value>comparisonRows の値を取得/設定します。</value>
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows {
        get => this.SelectedSourceTabViewState.ComparisonRows;
        set => this.SelectedSourceTabViewState.ComparisonRows=value;
    }
    /// <summary>
    /// currentRunSession フィールドです。
    /// </summary>
    private readonly BenchmarkRunSession currentRunSession = new();
    private IReadOnlyList<BenchmarkResultItem> recentCompletedResultItems = [];
    /// <summary>
    /// benchmarkRowSelections フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> benchmarkRowSelections = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// improvementCandidateSelections フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> improvementCandidateSelections = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// benchmarkCasesByProjectMethod フィールドです。
    /// </summary>
    private readonly Dictionary<string,BenchmarkCase> benchmarkCasesByProjectMethod = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// benchmarkRowsByResultKey フィールドです。
    /// </summary>
    private readonly Dictionary<string,BenchmarkTableRow> benchmarkRowsByResultKey = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// benchmarkRowsByProjectMethod フィールドです。
    /// </summary>
    private readonly Dictionary<string,BenchmarkTableRow> benchmarkRowsByProjectMethod = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// sourceTabRunningState フィールドです。
    /// </summary>
    private readonly BenchmarkSourceTabRunningState sourceTabRunningState = new();
    /// <summary>
    /// catalogCases フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkCase> catalogCases = [];
    /// <summary>
    /// savedSortRuleSnapshots フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkSortRuleSnapshot> savedSortRuleSnapshots = [];
    /// <summary>
    /// activeRunSourceTab フィールドです。
    /// </summary>
    private BenchmarkSourceTab? activeRunSourceTab;
    /// <summary>
    /// currentRunSelectedBenchmarkCases フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkCase> currentRunSelectedBenchmarkCases = [];
    /// <summary>
    /// selectedLogicalCpus フィールドです。
    /// </summary>
    private int[] selectedLogicalCpus = [];
    /// <summary>
    /// logText フィールドです。
    /// </summary>
    private string logText = string.Empty;
    /// <summary>
    /// selectedLowerTab フィールドです。
    /// </summary>
    private BenchmarkLowerTab selectedLowerTab = BenchmarkLowerTab.Cpu;
    /// <summary>
    /// overallSourceTabProgress フィールドです。
    /// </summary>
    private BenchmarkTabProgress overallSourceTabProgress;
    /// <summary>
    /// logSubscription フィールドです。
    /// </summary>
    private IDisposable? logSubscription;
    /// <summary>
    /// isClosing フィールドです。
    /// </summary>
    private bool isClosing;
    /// <summary>
    /// reportedMetadataWarnings フィールドです。
    /// </summary>
    private readonly HashSet<string> reportedMetadataWarnings = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// runStatusGate フィールドです。
    /// </summary>
    private readonly object runStatusGate = new();
    /// <summary>
    /// activeChartMethodName フィールドです。
    /// </summary>
    private string activeChartMethodName = string.Empty;
    private string appliedActiveChartMethodName = string.Empty;
    /// <summary>
    /// pendingRunStatusText フィールドです。
    /// </summary>
    private string pendingRunStatusText = string.Empty;
    /// <summary>
    /// runResultGeneration フィールドです。
    /// </summary>
    private int runResultGeneration;
    /// <summary>
    /// 最終クリーンアップ後に発火する古いジョブ単位の遅延適用を無効化するためのトークンです。
    /// </summary>
    private int finalLiveVisualCleanupToken;
    /// <summary>
    /// 実行終了後、パイプライン排出完了時に一度だけ表示最終化を行うためのフラグです。
    /// </summary>
    private bool pendingPostRunDisplayFinalize;
    /// <summary>
    /// 表示最終化を最終クリーンアップ実施後まで遅らせるための、フラグ設定時点のクリーンアップトークンです。
    /// </summary>
    private int pendingPostRunFinalizeCleanupToken;
    /// <summary>
    /// 実行終了時にパイプライン未反映イベントが残っていたため、最終クリーンアップを延期しているかどうかです。
    /// </summary>
    private bool pendingFinalLiveVisualCleanup;
    /// <summary>
    /// 延期中の最終クリーンアップでパイプライン更新を破棄するかどうかです。
    /// </summary>
    private bool pendingFinalLiveVisualCleanupDiscard;
    /// <summary>
    /// benchmarkAutoScrollSequence フィールドです。
    /// </summary>
    private int benchmarkAutoScrollSequence;
    /// <summary>
    /// isRunStatusUpdateScheduled フィールドです。
    /// </summary>
    private int isRunStatusUpdateScheduled;
    /// <summary>
    /// currentUnsavedRunRoot フィールドです。
    /// </summary>
    private string? currentUnsavedRunRoot;
    /// <summary>
    /// loadedExternalState フィールドです。
    /// </summary>
    private ProgressState? loadedExternalState;
    /// <summary>
    /// activeUiRunId フィールドです。
    /// </summary>
    private long? activeUiRunId;
    /// <summary>
    /// unsavedUiRunId フィールドです。
    /// </summary>
    private long? unsavedUiRunId;
    /// <summary>
    /// pendingSourceTabOption フィールドです。
    /// </summary>
    private SourceTabOption? pendingSourceTabOption;
    // 選択中の保存済み Run の ProgressState スナップショット。
    // タブ切替時に DB を再読み込みしないため、Run 選択の変更と実行終了時に破棄する。
    private (long UiRunId,ProgressState? State)? selectedRunStateCache;
    /// <summary>
    /// loadedImprovementSelectionRunId フィールドです。
    /// </summary>
    private long? loadedImprovementSelectionRunId;
    /// <summary>
    /// hasLoadedImprovementCandidateSelections フィールドです。
    /// </summary>
    private bool hasLoadedImprovementCandidateSelections;
    /// <summary>
    /// isApplyingImprovementCandidateSelection フィールドです。
    /// </summary>
    private bool isApplyingImprovementCandidateSelection;

    /// <summary>
    /// RunTerminationKind プロパティです。
    /// </summary>
    /// <value>RunTerminationKind の値を取得/設定します。</value>
    private enum RunTerminationKind {
        None,
        Stop,
        Pause,
    }


    /// <summary>
    /// この型のインスタンスを作成し、初期状態を構築するための処理を実行します。
    /// </summary>
    /// <param name="catalogService">catalogService を指定して実行条件を制御します。</param>
    /// <param name="cpuTopologyService">cpuTopologyService を指定して実行条件を制御します。</param>
    /// <param name="runService">runService を指定して実行条件を制御します。</param>
    /// <param name="comparisonService">comparisonService を指定して実行条件を制御します。</param>
    /// <param name="uiRunStore">uiRunStore を指定して実行条件を制御します。</param>
    /// <param name="benchmarkRoot">benchmarkRoot を指定して実行条件を制御します。</param>
    /// <param name="uiDispatcher">uiDispatcher を指定して実行条件を制御します。</param>
    /// <param name="uiPreferences">uiPreferences を指定して実行条件を制御します。</param>
    internal RunnerViewModel(
        IBenchmarkCatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IBenchmarkRunService runService,
        IBenchmarkComparisonService comparisonService,
        UiRunStore uiRunStore,
        string benchmarkRoot,
        IBenchmarkUiDispatcher? uiDispatcher = null,
        IBenchmarkUiPreferences? uiPreferences = null) {
        this.catalogService=catalogService;
        this.cpuTopologyService=cpuTopologyService;
        this.runService=runService;
        this.comparisonService=comparisonService;
        this.uiRunStore=uiRunStore;
        this.uiRunRepository=new BenchmarkUiRunRepository(uiRunStore,StartupDiagnostics.Write);
        this.uiDispatcher=uiDispatcher??new MauiBenchmarkUiDispatcher();
        this.uiPreferences=uiPreferences??new MauiBenchmarkUiPreferences();
        this.runEventPipeline=new BenchmarkRunEventPipeline();
        this.pipelineDiagnostics=new BenchmarkUiDiagnostics(StartupDiagnostics.Write);
        this.renderScheduler=new BenchmarkUiRenderScheduler(this.uiDispatcher,this.runEventPipeline,this.pipelineDiagnostics,this.ApplyRunUiSnapshot);
        this.persistenceBatcher=new BenchmarkPersistenceBatcher(this.uiDispatcher,this.pipelineDiagnostics,this.QueueBenchmarkResultsSave);
        this.pipelineDiagnostics.StartHeartbeat(this.uiDispatcher);
        this.benchmarkRoot=benchmarkRoot;
        this.SourceTabs=BenchmarkSourceTabs.All.Select(static item => new SourceTabOption(item)).ToArray();
        this.sourceTabViewStates=this.SourceTabs.Select(static item => new BenchmarkSourceTabViewState(item.Definition)).ToArray();
        foreach(var state in this.sourceTabViewStates) {
            this.sourceTabViewStatesByTab[state.Definition.Tab]=state;
        }
        this.selectedSourceTab=this.SourceTabs[0];
        this.ApplySourceTabVisualSelection();
        this.BenchmarkColumnSelectionGroups=this.CreateBenchmarkColumnSelectionGroups();
        this.BenchmarkProjectSelectionGroups=this.CreateBenchmarkProjectSelectionGroups();
        this.savedSortRuleSnapshots=this.LoadSavedBenchmarkSortRules();
        this.RefreshBenchmarkSortColumnOptions();
        this.ResetBenchmarkSortRulesToDefault(false);
        this.ExecutionProfiles=CreateExecutionProfiles();
        this.selectedExecutionProfile=this.ExecutionProfiles.First(static item => item.Profile==BenchmarkExecutionProfile.Fast);
        var initialRunOption = CreateNewRunOption(this.selectedExecutionProfile.Profile);
        this.RunOptions.Add(initialRunOption);
        this.selectedRunOption=initialRunOption;
        this.ThresholdOptions=CreateThresholdOptions();
        this.selectedThreshold=this.ThresholdOptions[0];
        this.Metrics= [
            new MetricOption("Time",BenchmarkMetricKind.Time),
            new MetricOption("Allocated",BenchmarkMetricKind.Allocated),
        ];
        this.selectedMetric=this.Metrics[0];
        this.ChartModes= [
            new ChartModeOption("N comparison",BenchmarkChartMode.ElementComparison),
        ];
        this.selectedChartMode=this.ChartModes[0];
        this.ScaleOptions= [
            new ScaleOption("1/8","0.125"),
            new ScaleOption("1/4","0.25"),
            new ScaleOption("1/2","0.5"),
            new ScaleOption("1","1"),
            new ScaleOption("2","2"),
            new ScaleOption("4","4"),
            new ScaleOption("8","8"),
        ];
        this.selectedScale=this.ScaleOptions.First(static item => item.DisplayName=="1");
        this.SelectAllCommand=new RelayCommand(
            () => this.SetAllBenchmarkSelection(true),
            () => this.CanChangeBenchmarkSelection);
        this.ClearSelectionCommand=new RelayCommand(
            () => this.SetAllBenchmarkSelection(false),
            () => this.CanChangeBenchmarkSelection);
        this.RunCommand=new AsyncRelayCommand(this.RunOrResumeAsync,() => this.CanRunBenchmark);
        this.SaveDatabaseCommand=new AsyncRelayCommand(this.SaveDatabaseAsync,() => this.CanSaveDatabase);
        this.NewGridCommand=new RelayCommand(this.NewGrid,() => this.CanCreateNewGrid);
#if DEBUG
        this.RunInfiniteCommand=new AsyncRelayCommand(this.RunInfiniteAsync,() => this.CanRunBenchmark);
#endif
        this.ClearRunHistoryCommand=new AsyncRelayCommand(this.ClearRunHistoryAsync,() => this.CanClearRunHistory);
        this.ToggleRunCommand=new RelayCommand(this.ToggleRun,() => this.CanUseRunToolbarButton);
        this.StopCommand=new AsyncRelayCommand(this.StopAsync,() => this.CanStopBenchmark);
        this.PauseCommand=new AsyncRelayCommand(this.PauseAsync,() => this.CanStopBenchmark);
        this.CancelCommand=new RelayCommand(this.Cancel);
        this.CopyImprovementPromptCommand=new AsyncRelayCommand(this.CopyImprovementPromptAsync,() => this.CanCopyImprovementPrompt);
        this.ShowCpuCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Cpu);
        this.ShowChartCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Chart);
        StartupDiagnostics.InitializeFromExistingLog();
        this.logText=StartupDiagnostics.GetLogText();
        this.logSubscription=StartupDiagnostics.Subscribe(this.AppendLogText);
        if(!string.IsNullOrWhiteSpace(this.logText)) {
            this.LogEntries.Add(this.logText);
        }

        foreach(var cpu in this.cpuTopologyService.Load()) {
            this.CpuItems.Add(cpu);
            cpu.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(CpuSelectionItem.IsSelected)&&!this.isApplyingCpuSelection) {
                    if(this.IsRefreshing) {
                        this.isApplyingCpuSelection=true;
                        try {
                            foreach(var item in this.CpuItems) {
                                item.IsSelected=this.selectedLogicalCpus.Contains(item.LogicalProcessorIndex);
                            }
                        }
                        finally {
                            this.isApplyingCpuSelection=false;
                            this.RefreshCpuSectionStates();
                        }
                        return;
                    }

                    this.RefreshSelectedLogicalCpus();
                    this.RefreshCpuSectionStates();
                }
            };
        }
        this.RebuildCpuSections();
        this.RefreshSelectedLogicalCpus();
    }

    /// <summary>
    /// CreateExecutionProfiles メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<ExecutionProfileOption> CreateExecutionProfiles() => [
            new ExecutionProfileOption("高精度",BenchmarkExecutionProfile.Accurate),
            new ExecutionProfileOption("バランス",BenchmarkExecutionProfile.Intermediate),
            new ExecutionProfileOption("Fast",BenchmarkExecutionProfile.Fast),
#if DEBUG
            new ExecutionProfileOption("Test",BenchmarkExecutionProfile.Test),
#endif
        ];

    /// <summary>
    /// CreateBenchmarkColumnSelectionGroups メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkColumnSelectionGroup> CreateBenchmarkColumnSelectionGroups() => [];

    /// <summary>
    /// CreateBenchmarkProjectSelectionGroups メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkColumnSelectionGroup> CreateBenchmarkProjectSelectionGroups() => [
        new BenchmarkColumnSelectionGroup("Left",this.SelectedSourceDefinition.LeftHeader,["Left"]),
        new BenchmarkColumnSelectionGroup("Right",this.SelectedSourceDefinition.RightHeader,["Right"]),
    ];

    /// <summary>
    /// RefreshBenchmarkProjectHeaderTexts メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkProjectHeaderTexts() {
        foreach(var group in this.BenchmarkProjectSelectionGroups) {
            if(group.Id=="Left") {
                group.SetHeaderText(this.SelectedSourceDefinition.LeftHeader);
            }
            else if(group.Id=="Right") {
                group.SetHeaderText(this.SelectedSourceDefinition.RightHeader);
            }
        }
    }

    /// <summary>
    /// SourceTabViewStates プロパティです。
    /// </summary>
    /// <value>SourceTabViewStates の値を取得/設定します。</value>
    public IReadOnlyList<BenchmarkSourceTabViewState> SourceTabViewStates => this.sourceTabViewStates;
    /// <summary>
    /// SelectedSourceTabViewState プロパティです。
    /// </summary>
    /// <value>SelectedSourceTabViewState の値を取得/設定します。</value>
    public BenchmarkSourceTabViewState SelectedSourceTabViewState => this.GetSourceTabViewState(this.SelectedSourceDefinition.Tab);
    /// <summary>
    /// BenchmarkRows プロパティです。
    /// </summary>
    /// <value>BenchmarkRows の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkTableRow> BenchmarkRows => this.SelectedSourceTabViewState.Rows;
    /// <summary>
    /// ResultItems プロパティです。
    /// </summary>
    /// <value>ResultItems の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkResultItem> ResultItems { get; } = [];
    /// <summary>
    /// VisibleResultItems プロパティです。
    /// </summary>
    /// <value>VisibleResultItems の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkResultItem> VisibleResultItems { get; } = [];
    /// <summary>
    /// CpuItems プロパティです。
    /// </summary>
    /// <value>CpuItems の値を取得/設定します。</value>
    public ObservableCollection<CpuSelectionItem> CpuItems { get; } = [];
    /// <summary>
    /// CpuSections プロパティです。
    /// </summary>
    /// <value>CpuSections の値を取得/設定します。</value>
    public ObservableCollection<CpuTableSection> CpuSections { get; } = [];
    /// <summary>
    /// ImprovementCandidates プロパティです。
    /// </summary>
    /// <value>ImprovementCandidates の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkImprovementCandidateRow> ImprovementCandidates { get; } = [];
    public ObservableCollection<BenchmarkImprovementCandidateRow> TimeImprovementCandidates { get; } = [];
    public ObservableCollection<BenchmarkImprovementCandidateRow> AllocatedImprovementCandidates { get; } = [];
    /// <summary>
    /// LatencyHotspotRows プロパティです。
    /// </summary>
    /// <value>LatencyHotspotRows の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkDiagnosticRow> LatencyHotspotRows => this.SelectedSourceTabViewState.LatencyHotspotRows;
    /// <summary>
    /// AllocationDiagnosticRows プロパティです。
    /// </summary>
    /// <value>AllocationDiagnosticRows の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkDiagnosticRow> AllocationDiagnosticRows => this.SelectedSourceTabViewState.AllocationDiagnosticRows;
    /// <summary>
    /// CpuTimelineRows プロパティです。
    /// </summary>
    /// <value>CpuTimelineRows の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkDiagnosticRow> CpuTimelineRows => this.SelectedSourceTabViewState.CpuTimelineRows;
    /// <summary>
    /// PipelineDiagnosticRows プロパティです。
    /// </summary>
    /// <value>PipelineDiagnosticRows の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkDiagnosticRow> PipelineDiagnosticRows { get; } = [];
    /// <summary>
    /// PerformanceCpuSections プロパティです。
    /// </summary>
    /// <value>PerformanceCpuSections の値を取得/設定します。</value>
    public IEnumerable<CpuTableSection> PerformanceCpuSections => this.GetCpuSections(CpuCoreKind.Performance);
    /// <summary>
    /// EfficiencyCpuSections プロパティです。
    /// </summary>
    /// <value>EfficiencyCpuSections の値を取得/設定します。</value>
    public IEnumerable<CpuTableSection> EfficiencyCpuSections => this.GetCpuSections(CpuCoreKind.Efficiency);
    /// <summary>
    /// UnknownCpuSections プロパティです。
    /// </summary>
    /// <value>UnknownCpuSections の値を取得/設定します。</value>
    public IEnumerable<CpuTableSection> UnknownCpuSections => this.GetCpuSections(CpuCoreKind.Unknown);
    /// <summary>
    /// BenchmarkSortRules プロパティです。
    /// </summary>
    /// <value>BenchmarkSortRules の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkSortRule> BenchmarkSortRules { get; } = [];
    /// <summary>
    /// BenchmarkSortColumnOptions プロパティです。
    /// </summary>
    /// <value>BenchmarkSortColumnOptions の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkSortColumnOption> BenchmarkSortColumnOptions { get; } = [];
    /// <summary>
    /// ChartPoints プロパティです。
    /// </summary>
    /// <value>ChartPoints の値を取得/設定します。</value>
    public ObservableCollection<ChartComparisonPoint> ChartPoints => this.SelectedSourceTabViewState.ChartPoints;
    public string ChartEmptyReasonText => this.SelectedSourceTabViewState.ChartEmptyReasonText;
    public string ChartEmptyReasonTooltipText => this.SelectedSourceTabViewState.ChartEmptyReasonTooltipText;
    /// <summary>
    /// ScaleOptions プロパティです。
    /// </summary>
    /// <value>ScaleOptions の値を取得/設定します。</value>
    public ObservableCollection<ScaleOption> ScaleOptions { get; } = [];
    /// <summary>
    /// ResultElementCountOptions プロパティです。
    /// </summary>
    /// <value>ResultElementCountOptions の値を取得/設定します。</value>
    public ObservableCollection<ResultElementCountOption> ResultElementCountOptions { get; } = [];
    /// <summary>
    /// RunOptions プロパティです。
    /// </summary>
    /// <value>RunOptions の値を取得/設定します。</value>
    public ObservableCollection<BenchmarkRunOption> RunOptions { get; } = [];
    /// <summary>
    /// LogEntries プロパティです。
    /// </summary>
    /// <value>LogEntries の値を取得/設定します。</value>
    public ObservableCollection<string> LogEntries { get; } = [];
    /// <summary>
    /// LogText プロパティです。
    /// </summary>
    /// <value>LogText の値を取得/設定します。</value>
    public string LogText {
        get => this.logText;
        private set => this.SetProperty(ref this.logText,value);
    }
    /// <summary>
    /// BenchmarkColumnSelectionGroups プロパティです。
    /// </summary>
    /// <value>BenchmarkColumnSelectionGroups の値を取得/設定します。</value>
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkColumnSelectionGroups { get; }
    /// <summary>
    /// BenchmarkProjectSelectionGroups プロパティです。
    /// </summary>
    /// <value>BenchmarkProjectSelectionGroups の値を取得/設定します。</value>
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkProjectSelectionGroups { get; }
    /// <summary>
    /// SourceTabs プロパティです。
    /// </summary>
    /// <value>SourceTabs の値を取得/設定します。</value>
    public IReadOnlyList<SourceTabOption> SourceTabs { get; }
    /// <summary>
    /// OverallSourceTabProgress プロパティです。
    /// </summary>
    /// <value>OverallSourceTabProgress の値を取得/設定します。</value>
    public BenchmarkTabProgress OverallSourceTabProgress {
        get => this.overallSourceTabProgress;
        private set{
            if(this.SetProperty(ref this.overallSourceTabProgress,value)){
                this.OnPropertyChanged(nameof(this.OverallSourceProgressSummaryText));
            }
        }
    }
    /// <summary>
    /// ExecutionProfiles プロパティです。
    /// </summary>
    /// <value>ExecutionProfiles の値を取得/設定します。</value>
    public IReadOnlyList<ExecutionProfileOption> ExecutionProfiles { get; }
    /// <summary>
    /// Metrics プロパティです。
    /// </summary>
    /// <value>Metrics の値を取得/設定します。</value>
    public IReadOnlyList<MetricOption> Metrics { get; }
    /// <summary>
    /// ChartModes プロパティです。
    /// </summary>
    /// <value>ChartModes の値を取得/設定します。</value>
    public IReadOnlyList<ChartModeOption> ChartModes { get; }
    /// <summary>
    /// ThresholdOptions プロパティです。
    /// </summary>
    /// <value>ThresholdOptions の値を取得/設定します。</value>
    public IReadOnlyList<ThresholdOption> ThresholdOptions { get; }
    /// <summary>
    /// SelectAllCommand プロパティです。
    /// </summary>
    /// <value>SelectAllCommand の値を取得/設定します。</value>
    public IRelayCommand SelectAllCommand { get; }
    /// <summary>
    /// ClearSelectionCommand プロパティです。
    /// </summary>
    /// <value>ClearSelectionCommand の値を取得/設定します。</value>
    public IRelayCommand ClearSelectionCommand { get; }
    /// <summary>
    /// RunCommand プロパティです。
    /// </summary>
    /// <value>RunCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand RunCommand { get; }
    /// <summary>
    /// SaveDatabaseCommand プロパティです。
    /// </summary>
    /// <value>SaveDatabaseCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand SaveDatabaseCommand { get; }
    /// <summary>
    /// NewGridCommand プロパティです。
    /// </summary>
    /// <value>NewGridCommand の値を取得/設定します。</value>
    public IRelayCommand NewGridCommand { get; }
#if DEBUG
    /// <summary>
    /// RunInfiniteCommand プロパティです。
    /// </summary>
    /// <value>RunInfiniteCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand RunInfiniteCommand { get; }
#endif
    /// <summary>
    /// ClearRunHistoryCommand プロパティです。
    /// </summary>
    /// <value>ClearRunHistoryCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand ClearRunHistoryCommand { get; }
    /// <summary>
    /// ToggleRunCommand プロパティです。
    /// </summary>
    /// <value>ToggleRunCommand の値を取得/設定します。</value>
    public IRelayCommand ToggleRunCommand { get; }
    /// <summary>
    /// StopCommand プロパティです。
    /// </summary>
    /// <value>StopCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand StopCommand { get; }
    /// <summary>
    /// PauseCommand プロパティです。
    /// </summary>
    /// <value>PauseCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand PauseCommand { get; }
    /// <summary>
    /// CancelCommand プロパティです。
    /// </summary>
    /// <value>CancelCommand の値を取得/設定します。</value>
    public IRelayCommand CancelCommand { get; }
    /// <summary>
    /// CopyImprovementPromptCommand プロパティです。
    /// </summary>
    /// <value>CopyImprovementPromptCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand CopyImprovementPromptCommand { get; }
    /// <summary>
    /// ShowCpuCommand プロパティです。
    /// </summary>
    /// <value>ShowCpuCommand の値を取得/設定します。</value>
    public IRelayCommand ShowCpuCommand { get; }
    /// <summary>
    /// ShowChartCommand プロパティです。
    /// </summary>
    /// <value>ShowChartCommand の値を取得/設定します。</value>
    public IRelayCommand ShowChartCommand { get; }

    /// <summary>
    /// StatusText プロパティです。
    /// </summary>
    /// <value>StatusText の値を取得/設定します。</value>
    public string StatusText {
        get => this.statusText;
        set => this.SetProperty(ref this.statusText,value);
    }

    /// <summary>
    /// CurrentSortColumn プロパティです。
    /// </summary>
    /// <value>CurrentSortColumn の値を取得/設定します。</value>
    public BenchmarkGridSortColumn CurrentSortColumn {
        get => this.currentSortColumn;
        private set => this.SetProperty(ref this.currentSortColumn,value);
    }

    /// <summary>
    /// CurrentSortDescending プロパティです。
    /// </summary>
    /// <value>CurrentSortDescending の値を取得/設定します。</value>
    public bool CurrentSortDescending {
        get => this.currentSortDescending;
        private set => this.SetProperty(ref this.currentSortDescending,value);
    }

    /// <summary>
    /// HasBenchmarkTypeColumn プロパティです。
    /// </summary>
    /// <value>HasBenchmarkTypeColumn の値を取得/設定します。</value>
    public bool HasBenchmarkTypeColumn {
        get => this.hasBenchmarkTypeColumn;
        private set => this.SetProperty(ref this.hasBenchmarkTypeColumn,value);
    }

    /// <summary>
    /// BenchmarkSortSummaryText プロパティです。
    /// </summary>
    /// <value>BenchmarkSortSummaryText の値を取得/設定します。</value>
    public string BenchmarkSortSummaryText =>
        this.BenchmarkSortRules.Count==0
            ? "Default"
            : string.Join(" > ",this.BenchmarkSortRules.Select(static rule => rule.ColumnText+" "+rule.DirectionText));

    /// <summary>
    /// ベンチマークシェア概要表示 プロパティです。
    /// </summary>
    /// <value>ベンチマークシェア概要表示 の値を取得/設定します。</value>
    public string ベンチマークシェア概要表示 {
        get => this.SelectedSourceTabViewState.ベンチマークシェア概要表示;
        /// <summary>
        /// set プロパティを取得/設定します。
        /// </summary>
        /// <value>set の値です。</value>
        private set {
            if(this.SelectedSourceTabViewState.ベンチマークシェア概要表示!=value) {
                this.SelectedSourceTabViewState.ベンチマークシェア概要表示=value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// ImprovementPromptText プロパティです。
    /// </summary>
    /// <value>ImprovementPromptText の値を取得/設定します。</value>
    public string ImprovementPromptText {
        get => this.improvementPromptText;
        set {
            if(this.SetProperty(ref this.improvementPromptText,value)) {
                this.OnPropertyChanged(nameof(this.CanCopyImprovementPrompt));
                this.CopyImprovementPromptCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// ImprovementCandidateSummaryText プロパティです。
    /// </summary>
    /// <value>ImprovementCandidateSummaryText の値を取得/設定します。</value>
    public string ImprovementCandidateSummaryText {
        get => this.improvementCandidateSummaryText;
        private set => this.SetProperty(ref this.improvementCandidateSummaryText,value);
    }

    /// <summary>
    /// GeneratedCodeAuditReport プロパティです。
    /// </summary>
    /// <value>GeneratedCodeAuditReport の値を取得/設定します。</value>
    public BenchmarkGeneratedCodeAuditReport? GeneratedCodeAuditReport {
        get => this.generatedCodeAuditReport;
        private set => this.SetProperty(ref this.generatedCodeAuditReport,value);
    }

    /// <summary>
    /// isGeneratedCodeAuditTabVisible フィールドです。
    /// </summary>
    private bool isGeneratedCodeAuditTabVisible;
    /// <summary>
    /// IsGeneratedCodeAuditTabVisible プロパティです。
    /// </summary>
    /// <value>IsGeneratedCodeAuditTabVisible の値を取得/設定します。</value>
    public bool IsGeneratedCodeAuditTabVisible {
        get => this.isGeneratedCodeAuditTabVisible;
        private set {
            if(this.SetProperty(ref this.isGeneratedCodeAuditTabVisible,value)&&value) {
                this.RefreshGeneratedCodeAuditIfNeeded();
            }
        }
    }

    /// <summary>
    /// SetGeneratedCodeAuditTabVisible メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="visible">visible を指定して実行条件を制御します。</param>
    public void SetGeneratedCodeAuditTabVisible(bool visible) => this.IsGeneratedCodeAuditTabVisible=visible;

    /// <summary>
    /// CanCopyImprovementPrompt プロパティです。
    /// </summary>
    /// <value>CanCopyImprovementPrompt の値を取得/設定します。</value>
    public bool CanCopyImprovementPrompt => !string.IsNullOrWhiteSpace(this.ImprovementPromptText);

    /// <summary>
    /// IsBenchmarkRunning プロパティです。
    /// </summary>
    /// <value>IsBenchmarkRunning の値を取得/設定します。</value>
    public bool IsBenchmarkRunning {
        get => this.isBenchmarkRunning;
        private set {
            if(this.SetProperty(ref this.isBenchmarkRunning,value)) {
                this.RunCommand.NotifyCanExecuteChanged();
                this.SaveDatabaseCommand.NotifyCanExecuteChanged();
                this.NewGridCommand.NotifyCanExecuteChanged();
#if DEBUG
                this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
                this.ClearRunHistoryCommand.NotifyCanExecuteChanged();
                this.ToggleRunCommand.NotifyCanExecuteChanged();
                this.StopCommand.NotifyCanExecuteChanged();
                this.PauseCommand.NotifyCanExecuteChanged();
                this.RefreshToolbarCommandStates();
                this.SelectAllCommand.NotifyCanExecuteChanged();
                this.ClearSelectionCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.IsRunning));
                this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
                this.OnPropertyChanged(nameof(this.IsBenchmarkPaused));
                this.OnPropertyChanged(nameof(this.IsSortRuleEditingEnabled));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
                this.OnPropertyChanged(nameof(this.CanSelectRunHistory));
                this.OnPropertyChanged(nameof(this.CanRunBenchmark));
                this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
                this.OnPropertyChanged(nameof(this.CanSaveDatabase));
                this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
                this.OnPropertyChanged(nameof(this.CanStopBenchmark));
                this.OnPropertyChanged(nameof(this.CanClearRunHistory));
                this.OnPropertyChanged(nameof(this.ClearRunHistoryTooltipText));
                this.OnPropertyChanged(nameof(this.RunButtonText));
                this.OnPropertyChanged(nameof(this.RunButtonTooltipText));
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            }
        }
    }

    /// <summary>
    /// IsRunning プロパティです。
    /// </summary>
    /// <value>IsRunning の値を取得/設定します。</value>
    public bool IsRunning {
        get => this.IsBenchmarkRunning;
        private set => this.IsBenchmarkRunning=value;
    }

    /// <summary>
    /// IsRefreshing プロパティです。
    /// </summary>
    /// <value>IsRefreshing の値を取得/設定します。</value>
    public bool IsRefreshing {
        get => this.isRefreshing;
        private set {
            if(this.SetProperty(ref this.isRefreshing,value)) {
                this.RunCommand.NotifyCanExecuteChanged();
                this.SaveDatabaseCommand.NotifyCanExecuteChanged();
                this.NewGridCommand.NotifyCanExecuteChanged();
#if DEBUG
                this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
                this.ClearRunHistoryCommand.NotifyCanExecuteChanged();
                this.ToggleRunCommand.NotifyCanExecuteChanged();
                this.StopCommand.NotifyCanExecuteChanged();
                this.PauseCommand.NotifyCanExecuteChanged();
                this.RefreshToolbarCommandStates();
                this.SelectAllCommand.NotifyCanExecuteChanged();
                this.ClearSelectionCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
                this.OnPropertyChanged(nameof(this.CanSelectRunHistory));
                this.OnPropertyChanged(nameof(this.CanRunBenchmark));
                this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
                this.OnPropertyChanged(nameof(this.CanSaveDatabase));
                this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
                this.OnPropertyChanged(nameof(this.CanStopBenchmark));
                this.OnPropertyChanged(nameof(this.CanClearRunHistory));
                this.OnPropertyChanged(nameof(this.ClearRunHistoryTooltipText));
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            }
        }
    }

    /// <summary>
    /// CanChangeBenchmarkConditions プロパティです。
    /// </summary>
    /// <value>CanChangeBenchmarkConditions の値を取得/設定します。</value>
    public bool CanChangeBenchmarkConditions =>
        !this.IsRefreshing
        &&!this.IsRunHistoryLoading
        &&!this.isLoadingSelectedRunDb
        &&!this.IsBenchmarkRunActive
        &&!this.IsBenchmarkPaused;

    /// <summary>
    /// CanChangeBenchmarkSelection プロパティです。
    /// </summary>
    /// <value>CanChangeBenchmarkSelection の値を取得/設定します。</value>
    public bool CanChangeBenchmarkSelection =>
        !this.IsRefreshing;

    /// <summary>
    /// IsRunHistoryLoading プロパティです。
    /// </summary>
    /// <value>IsRunHistoryLoading の値を取得/設定します。</value>
    public bool IsRunHistoryLoading {
        get => this.isRunHistoryLoading;
        private set {
            if(this.SetProperty(ref this.isRunHistoryLoading,value)) {
                this.OnPropertyChanged(nameof(this.CanSelectRunHistory));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
                this.OnPropertyChanged(nameof(this.CanRunBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
                this.OnPropertyChanged(nameof(this.CanSaveDatabase));
                this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
                this.RunCommand.NotifyCanExecuteChanged();
                this.ToggleRunCommand.NotifyCanExecuteChanged();
                this.SaveDatabaseCommand.NotifyCanExecuteChanged();
                this.NewGridCommand.NotifyCanExecuteChanged();
#if DEBUG
                this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
            }
        }
    }

    /// <summary>
    /// CanSelectRunHistory プロパティです。
    /// </summary>
    /// <value>CanSelectRunHistory の値を取得/設定します。</value>
    public bool CanSelectRunHistory =>
        !this.IsRunHistoryLoading
        &&!this.IsRefreshing
        &&!this.IsBenchmarkRunActive
        &&!this.IsBenchmarkPaused;

    /// <summary>
    /// IsBenchmarkRunActive プロパティです。
    /// </summary>
    /// <value>IsBenchmarkRunActive の値を取得/設定します。</value>
    public bool IsBenchmarkRunActive => this.runCancellation is not null&&this.IsBenchmarkRunning;

    /// <summary>
    /// IsBenchmarkPaused プロパティです。
    /// </summary>
    /// <value>IsBenchmarkPaused の値を取得/設定します。</value>
    public bool IsBenchmarkPaused => this.runCancellation is null&&this.activeRunRoot is not null;

    public bool CanStopBenchmark =>
        (this.IsBenchmarkRunActive||this.IsBenchmarkPaused)
        &&this.pendingRunTerminationKind==RunTerminationKind.None;

    // 実行中は行位置を固定するため、ソートルールの編集(追加/削除/移動/方向切替)をUIから禁止する。
    /// <summary>
    /// IsSortRuleEditingEnabled プロパティです。
    /// </summary>
    /// <value>IsSortRuleEditingEnabled の値を取得/設定します。</value>
    public bool IsSortRuleEditingEnabled => !this.IsBenchmarkRunActive;

    /// <summary>
    /// RunButtonText プロパティです。
    /// </summary>
    /// <value>RunButtonText の値を取得/設定します。</value>
    public string RunButtonText => "計測";

    /// <summary>
    /// CanResumeBenchmark プロパティです。
    /// </summary>
    /// <value>CanResumeBenchmark の値を取得/設定します。</value>
    public bool CanResumeBenchmark => !this.IsRefreshing&&this.IsBenchmarkPaused;

    /// <summary>
    /// CanRunBenchmark プロパティです。
    /// </summary>
    /// <value>CanRunBenchmark の値を取得/設定します。</value>
    public bool CanRunBenchmark =>
        !this.IsRefreshing
        &&!this.IsRunHistoryLoading
        &&!this.isLoadingSelectedRunDb
        &&!this.isApplyingRunOptionSelection
        &&!this.IsBenchmarkRunning
        &&this.GetSelectedLogicalCpus().Count>0
        &&(this.HasSelectedBenchmarkCells||IsSavedDatabaseRunOption(this.SelectedRunOption));

    /// <summary>
    /// CanUseRunToolbarButton プロパティです。
    /// </summary>
    /// <value>CanUseRunToolbarButton の値を取得/設定します。</value>
    public bool CanUseRunToolbarButton => this.CanRunBenchmark||this.CanResumeBenchmark;

    /// <summary>
    /// CanSaveDatabase プロパティです。
    /// </summary>
    /// <value>CanSaveDatabase の値を取得/設定します。</value>
    public bool CanSaveDatabase =>
        !this.IsRefreshing
        &&!this.IsRunHistoryLoading
        &&!this.isLoadingSelectedRunDb
        &&!this.IsBenchmarkRunActive
        &&!this.IsBenchmarkPaused
        &&IsNewRunOption(this.SelectedRunOption)
        &&this.HasSaveableBenchmarkResults;

    /// <summary>
    /// CanCreateNewGrid プロパティです。
    /// </summary>
    /// <value>CanCreateNewGrid の値を取得/設定します。</value>
    public bool CanCreateNewGrid =>
        !this.IsRefreshing
        &&!this.IsRunHistoryLoading
        &&!this.isLoadingSelectedRunDb
        &&!this.IsBenchmarkRunActive
        &&!this.IsBenchmarkPaused;

    /// <summary>
    /// CanClearRunHistory プロパティです。
    /// </summary>
    /// <value>CanClearRunHistory の値を取得/設定します。</value>
    public bool CanClearRunHistory =>
        !this.IsRefreshing
        &&!this.IsBenchmarkRunActive
        &&!this.IsBenchmarkPaused;

    public string ClearRunHistoryTooltipText => this.IsBenchmarkRunActive
        ? "ベンチマーク実行中は履歴を消去できません。"
        : this.IsBenchmarkPaused
            ? "ベンチマーク一時停止中は履歴を消去できません。"
            : this.IsRefreshing
                ? "処理中は履歴を消去できません。"
                : "履歴消去";

    /// <summary>
    /// HasSelectedBenchmarkCells プロパティです。
    /// </summary>
    /// <value>HasSelectedBenchmarkCells の値を取得/設定します。</value>
    public bool HasSelectedBenchmarkCells {
        get => this.hasSelectedBenchmarkCells;
        private set {
            if(this.SetProperty(ref this.hasSelectedBenchmarkCells,value)) {
                this.RunCommand.NotifyCanExecuteChanged();
#if DEBUG
                this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
                this.ToggleRunCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.CanRunBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
                this.OnPropertyChanged(nameof(this.RunButtonTooltipText));
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            }
        }
    }

    /// <summary>
    /// RunButtonTooltipText プロパティです。
    /// </summary>
    /// <value>RunButtonTooltipText の値を取得/設定します。</value>
    public string RunButtonTooltipText =>
        this.HasSelectedBenchmarkCells||IsSavedDatabaseRunOption(this.SelectedRunOption)
            ? this.RunButtonText
            : "ベンチマークを1つ以上選択してください";

    /// <summary>
    /// ActiveRunSourceTab プロパティです。
    /// </summary>
    /// <value>ActiveRunSourceTab の値を取得/設定します。</value>
    public BenchmarkSourceTab? ActiveRunSourceTab {
        get => this.activeRunSourceTab;
        private set {
            if(this.SetProperty(ref this.activeRunSourceTab,value)) {
                this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
                this.OnPropertyChanged(nameof(this.IsSortRuleEditingEnabled));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
                this.OnPropertyChanged(nameof(this.RunButtonText));
                this.OnPropertyChanged(nameof(this.IsBenchmarkPaused));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
                this.OnPropertyChanged(nameof(this.CanSelectRunHistory));
                this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
                this.OnPropertyChanged(nameof(this.CanSaveDatabase));
                this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
                this.ClearRunHistoryCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.CanClearRunHistory));
                this.OnPropertyChanged(nameof(this.ClearRunHistoryTooltipText));
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            }
        }
    }

    /// <summary>
    /// HasPendingCloseStateSave プロパティです。
    /// </summary>
    /// <value>HasPendingCloseStateSave の値を取得/設定します。</value>
    public bool HasPendingCloseStateSave {
        get => this.hasPendingCloseStateSave;
        private set => this.SetProperty(ref this.hasPendingCloseStateSave,value);
    }

    /// <summary>
    /// HasUnsavedBenchmarkResults プロパティです。
    /// </summary>
    /// <value>HasUnsavedBenchmarkResults の値を取得/設定します。</value>
    public bool HasUnsavedBenchmarkResults {
        get => this.hasUnsavedBenchmarkResults;
        private set {
            if(this.SetProperty(ref this.hasUnsavedBenchmarkResults,value)) {
                this.SaveDatabaseCommand.NotifyCanExecuteChanged();
                this.RefreshToolbarCommandStates();
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
                this.OnPropertyChanged(nameof(this.CanSaveDatabase));
            }
        }
    }

    /// <summary>
    /// ToolbarStateInput プロパティです。
    /// </summary>
    /// <value>ToolbarStateInput の値を取得/設定します。</value>
    internal BenchmarkToolbarStateInput ToolbarStateInput => new(
        this.IsRefreshing,
        this.IsBenchmarkRunActive,
        this.IsBenchmarkPaused,
        this.CanRunBenchmark,
        this.IsBenchmarkRunActive,
        false,
        this.CanSaveDatabase,
        this.CanCreateNewGrid);

    /// <summary>
    /// 指 定 さ れ た 対 象 情 報 を 取 得 し 、 呼 び 出 し 元 へ 返 却 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    /// <param name="role">対 象 の 引 数 を 受 け 取 り 処 理 を 制 御 す る エ 組 み で す 。</param>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    internal BenchmarkToolbarButtonState GetToolbarButtonState(BenchmarkToolbarButtonRole role) =>
        BenchmarkToolbarStateLogic.CreateState(role,this.ToolbarStateInput);

    /// <summary>
    /// IsAllBenchmarkSelectionChecked プロパティです。
    /// </summary>
    /// <value>IsAllBenchmarkSelectionChecked の値を取得/設定します。</value>
    public bool? IsAllBenchmarkSelectionChecked {
        get => this.isAllBenchmarkSelectionChecked;
        private set => this.SetProperty(ref this.isAllBenchmarkSelectionChecked,value);
    }

    /// <summary>
    /// CancelAndWaitAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task CancelAndWaitAsync() {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        this.Cancel();
        await this.WaitForRunStopAsync();
    }

    /// <summary>
    /// PauseAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task PauseAsync() {
        if(!this.CanStopBenchmark) {
            return;
        }

        if(Interlocked.Exchange(ref this.stopCommandGate,1)==1) {
            return;
        }

        try {
            this.pendingRunTerminationKind=RunTerminationKind.Pause;
            this.NotifyRunCommandStatesChanged();
            this.Cancel();
            await this.WaitForRunStopAsync();
        }
        finally {
            this.pendingRunTerminationKind=RunTerminationKind.None;
            Interlocked.Exchange(ref this.stopCommandGate,0);
            this.NotifyRunCommandStatesChanged();
        }
    }

    /// <summary>
    /// StopAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task StopAsync() {
        if(!this.CanStopBenchmark) {
            return;
        }

        if(Interlocked.Exchange(ref this.stopCommandGate,1)==1) {
            return;
        }

        try {
            this.pendingRunTerminationKind=RunTerminationKind.Stop;
            this.NotifyRunCommandStatesChanged();
            this.Cancel();
            await this.WaitForRunStopAsync();
            if(this.IsBenchmarkPaused) {
                this.SetActiveRunRoot(null);
            }
            this.pendingRunTerminationKind=RunTerminationKind.None;
        }
        finally {
            this.pendingRunTerminationKind=RunTerminationKind.None;
            Interlocked.Exchange(ref this.stopCommandGate,0);
            this.NotifyRunCommandStatesChanged();
        }
    }

    /// <summary>
    /// WaitForRunStopAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task WaitForRunStopAsync() {
        // DB ロックなどで RunAsync の終了処理が戻らない場合でも、UI を実行可能状態へ復帰させる。
        var deadline = DateTimeOffset.UtcNow+TimeSpan.FromSeconds(10);
        while(this.IsBenchmarkRunActive) {
            if(DateTimeOffset.UtcNow>=deadline) {
                this.ForceResetRunState("Benchmark run did not stop within 10 seconds. Forcing UI run state reset.");
                return;
            }

            await Task.Delay(100);
        }
    }

    /// <summary>
    /// ForceResetRunState メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="reason">reason を指定して実行条件を制御します。</param>
    private void ForceResetRunState(string reason) {
        StartupDiagnostics.Write(reason);
        this.runCancellation?.Cancel();
        this.runCancellation=null;
        this.pendingRunTerminationKind=RunTerminationKind.None;
        Interlocked.Exchange(ref this.runCommandGate,0);
        Interlocked.Exchange(ref this.stopCommandGate,0);
        this.runResultGeneration=unchecked(this.runResultGeneration+1);
        this.IsRunning=false;
        this.ActiveRunSourceTab=null;
        this.ApplyFinalLiveVisualCleanup(true);
        this.SetActiveRunRoot(null);
        this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
        this.OnPropertyChanged(nameof(this.IsSortRuleEditingEnabled));
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
        this.StopCommand.NotifyCanExecuteChanged();
        this.PauseCommand.NotifyCanExecuteChanged();
        this.RefreshToolbarCommandStates();
    }

    private void NotifyRunCommandStatesChanged() {
        this.RunCommand.NotifyCanExecuteChanged();
        this.ToggleRunCommand.NotifyCanExecuteChanged();
        this.SaveDatabaseCommand.NotifyCanExecuteChanged();
        this.NewGridCommand.NotifyCanExecuteChanged();
#if DEBUG
        this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
        this.StopCommand.NotifyCanExecuteChanged();
        this.PauseCommand.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.CanRunBenchmark));
        this.OnPropertyChanged(nameof(this.CanSaveDatabase));
        this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
        this.OnPropertyChanged(nameof(this.CanStopBenchmark));
        this.ClearRunHistoryCommand.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.CanClearRunHistory));
        this.OnPropertyChanged(nameof(this.ClearRunHistoryTooltipText));
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// SetActiveRunRoot メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    private void SetActiveRunRoot(string? runRoot) {
        if(this.activeRunRoot==runRoot) {
            return;
        }

        this.activeRunRoot=runRoot;
        this.OnPropertyChanged(nameof(this.IsBenchmarkPaused));
        this.OnPropertyChanged(nameof(this.RunButtonText));
        this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
        this.OnPropertyChanged(nameof(this.CanRunBenchmark));
        this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
        this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
        this.OnPropertyChanged(nameof(this.CanSelectRunHistory));
        this.OnPropertyChanged(nameof(this.CanSaveDatabase));
        this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
        this.OnPropertyChanged(nameof(this.CanStopBenchmark));
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
        this.RunCommand.NotifyCanExecuteChanged();
#if DEBUG
        this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
        this.ToggleRunCommand.NotifyCanExecuteChanged();
        this.StopCommand.NotifyCanExecuteChanged();
        this.PauseCommand.NotifyCanExecuteChanged();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// SelectedTab プロパティです。
    /// </summary>
    /// <value>SelectedTab の値を取得/設定します。</value>
    public BenchmarkTab SelectedTab {
        get => this.selectedTab;
        set {
            if(this.SetProperty(ref this.selectedTab,value)) {
                this.OnPropertyChanged(nameof(this.IsCpuVisible));
                this.OnPropertyChanged(nameof(this.IsChartVisible));
            }
        }
    }

    /// <summary>
    /// IsCpuVisible プロパティです。
    /// </summary>
    /// <value>IsCpuVisible の値を取得/設定します。</value>
    public bool IsCpuVisible => this.SelectedTab==BenchmarkTab.Cpu;
    /// <summary>
    /// IsChartVisible プロパティです。
    /// </summary>
    /// <value>IsChartVisible の値を取得/設定します。</value>
    public bool IsChartVisible => this.SelectedTab==BenchmarkTab.Chart;

    /// <summary>
    /// SelectedLowerTab プロパティです。
    /// </summary>
    /// <value>SelectedLowerTab の値を取得/設定します。</value>
    public BenchmarkLowerTab SelectedLowerTab {
        get => this.selectedLowerTab;
        set {
            if(this.SetProperty(ref this.selectedLowerTab,value)) {
                this.OnPropertyChanged(nameof(this.IsCpuLowerTabVisible));
                this.OnPropertyChanged(nameof(this.IsLogLowerTabVisible));
                this.OnPropertyChanged(nameof(this.IsImprovementPromptLowerTabVisible));
            }
        }
    }

    /// <summary>
    /// IsCpuLowerTabVisible プロパティです。
    /// </summary>
    /// <value>IsCpuLowerTabVisible の値を取得/設定します。</value>
    public bool IsCpuLowerTabVisible => this.SelectedLowerTab==BenchmarkLowerTab.Cpu;
    /// <summary>
    /// IsLogLowerTabVisible プロパティです。
    /// </summary>
    /// <value>IsLogLowerTabVisible の値を取得/設定します。</value>
    public bool IsLogLowerTabVisible => this.SelectedLowerTab==BenchmarkLowerTab.Log;
    /// <summary>
    /// IsImprovementPromptLowerTabVisible プロパティです。
    /// </summary>
    /// <value>IsImprovementPromptLowerTabVisible の値を取得/設定します。</value>
    public bool IsImprovementPromptLowerTabVisible => this.SelectedLowerTab==BenchmarkLowerTab.ImprovementPrompt;

    /// <summary>
    /// SelectedComparisonPair プロパティです。
    /// </summary>
    /// <value>SelectedComparisonPair の値を取得/設定します。</value>
    public ComparisonPairOption SelectedComparisonPair => this.SelectedSourceDefinition.Pair;

    /// <summary>
    /// SelectedSourceTab プロパティです。
    /// </summary>
    /// <value>SelectedSourceTab の値を取得/設定します。</value>
    public SourceTabOption SelectedSourceTab {
        get => this.selectedSourceTab;
        set {
            if(value==this.selectedSourceTab) {
                return;
            }

            if(this.IsRefreshing) {
                // 読み込み中の切替は破棄せず保留し、読み込み完了後に最後の要求だけを適用する。
                this.pendingSourceTabOption=value;
                return;
            }

            if(this.isApplyingSourceTabSelection) {
                this.ApplySourceTabSelection(value);
                return;
            }

            _=this.SelectSourceTabAsync(value);
        }
    }

    /// <summary>
    /// SelectedSourceDefinition プロパティです。
    /// </summary>
    /// <value>SelectedSourceDefinition の値を取得/設定します。</value>
    public BenchmarkSourceTabDefinition SelectedSourceDefinition => this.SelectedSourceTab.Definition;

    /// <summary>
    /// LatestBenchmarkAutoScrollRequest プロパティです。
    /// </summary>
    /// <value>LatestBenchmarkAutoScrollRequest の値を取得/設定します。</value>
    public BenchmarkAutoScrollRequest? LatestBenchmarkAutoScrollRequest { get; private set; }

    /// <summary>
    /// ChartLeftLegendLabel プロパティです。
    /// </summary>
    /// <value>ChartLeftLegendLabel の値を取得/設定します。</value>
    public string ChartLeftLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.LeftProject);
    /// <summary>
    /// ChartRightLegendLabel プロパティです。
    /// </summary>
    /// <value>ChartRightLegendLabel の値を取得/設定します。</value>
    public string ChartRightLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.RightProject);

    /// <summary>
    /// SelectSourceTabAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    private async Task SelectSourceTabAsync(SourceTabOption option) {
        await Task.CompletedTask;
        this.CaptureCurrentBenchmarkSelections();
        this.ApplySourceTabSelection(option);
    }

    /// <summary>
    /// ApplySourceTabSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    private void ApplySourceTabSelection(SourceTabOption option) {
        var previousTab = this.selectedSourceTab.Definition.Id;
        var nextTab = option.Definition.Id;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var changed = false;
        StartupDiagnostics.AddBreadcrumb(
            "Source tab switch start "
            +previousTab+"->"+nextTab
            +" running="+this.IsBenchmarkRunActive.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" active="+(this.ActiveRunSourceTab?.ToString()??"-")
            +" progress="+this.OverallSourceTabProgress.FractionText);
        this.isApplyingSourceTabSelection=true;
        this.InvalidateSelectedRunLoadRequests();
        try {
            using(this.BeginChartRefreshBatch()) {
                if(this.SetProperty(ref this.selectedSourceTab,option)) {
                    changed=true;
                    this.ApplySourceTabVisualSelection();
                    this.OnPropertyChanged(nameof(this.SelectedSourceTabViewState));
                    this.OnPropertyChanged(nameof(this.BenchmarkRows));
                    this.OnPropertyChanged(nameof(this.ChartPoints));
                    this.OnPropertyChanged(nameof(this.ChartEmptyReasonText));
                    this.OnPropertyChanged(nameof(this.ChartEmptyReasonTooltipText));
                    this.OnPropertyChanged(nameof(this.ChartHeight));
                    this.OnPropertyChanged(nameof(this.ベンチマークシェア概要表示));
                    this.OnPropertyChanged(nameof(this.LatencyHotspotRows));
                    this.OnPropertyChanged(nameof(this.AllocationDiagnosticRows));
                    this.OnPropertyChanged(nameof(this.CpuTimelineRows));
                    this.OnPropertyChanged(nameof(this.SelectedComparisonPair));
                    this.OnPropertyChanged(nameof(this.SelectedSourceDefinition));
                    this.OnPropertyChanged(nameof(this.ChartLeftLegendLabel));
                    this.OnPropertyChanged(nameof(this.ChartRightLegendLabel));
                    this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
                    this.RefreshBenchmarkProjectHeaderTexts();
                    // タブ切替では DB もカタログも再読み込みせず、メモリ上のカタログと結果バッファだけで再構築する。
                    this.RefreshBenchmarkSortColumnOptions();
                    // ヘッダーのまとめチェック(FLinq/ZLinq)は現在のSourceタブだけを反映する共有インスタンスのため、
                    // 切替直後に再計算しないと直前のタブのチェック状態が見えたままになる。
                    this.RefreshBenchmarkSelectionAggregates();
                }
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write(
                "Source tab switch failed "
                +previousTab+"->"+nextTab
                +" running="+this.IsBenchmarkRunActive.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" active="+(this.ActiveRunSourceTab?.ToString()??"-"),
                ex);
            StartupDiagnostics.WriteCrashContext("Source tab switch failed");
            throw;
        }
        finally {
            stopwatch.Stop();
            if(changed) {
                StartupDiagnostics.Write(
                    $"Source tab switch {previousTab}->{nextTab} elapsed={FormatSwitchMeasureMilliseconds(stopwatch.Elapsed)}ms rows={this.BenchmarkRows.Count}");
            }
            StartupDiagnostics.AddBreadcrumb(
                "Source tab switch end "
                +previousTab+"->"+nextTab
                +" changed="+changed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" elapsed="+FormatSwitchMeasureMilliseconds(stopwatch.Elapsed)
                +" rows="+this.BenchmarkRows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" progress="+this.OverallSourceTabProgress.FractionText);
            this.isApplyingSourceTabSelection=false;
        }
    }

    /// <summary>
    /// ApplySourceTabVisualSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplySourceTabVisualSelection() {
        foreach(var item in this.SourceTabs) {
            item.SetSelected(ReferenceEquals(item,this.selectedSourceTab));
        }
    }

    /// <summary>
    /// SelectedExecutionProfile プロパティです。
    /// </summary>
    /// <value>SelectedExecutionProfile の値を取得/設定します。</value>
    public ExecutionProfileOption SelectedExecutionProfile {
        get => this.selectedExecutionProfile;
        set {
            if(!this.CanChangeBenchmarkConditions) {
                return;
            }

            if(this.SetProperty(ref this.selectedExecutionProfile,value)) {
                this.SelectNewRunOptionForFreshRun(false);
                _=this.RefreshRunsForSelectedProfileAsync();
            }
        }
    }

    /// <summary>
    /// SelectedScale プロパティです。
    /// </summary>
    /// <value>SelectedScale の値を取得/設定します。</value>
    public ScaleOption? SelectedScale {
        get => this.selectedScale;
        set => this.SetProperty(ref this.selectedScale,value);
    }

    /// <summary>
    /// SelectedThreshold プロパティです。
    /// </summary>
    /// <value>SelectedThreshold の値を取得/設定します。</value>
    public ThresholdOption SelectedThreshold {
        get => this.selectedThreshold;
        set {
            if(!this.CanChangeBenchmarkConditions) {
                return;
            }

            this.SetProperty(ref this.selectedThreshold,value);
        }
    }

    /// <summary>
    /// SelectedRunOption プロパティです。
    /// </summary>
    /// <value>SelectedRunOption の値を取得/設定します。</value>
    public BenchmarkRunOption? SelectedRunOption {
        get => this.NormalizeSelectedRunOptionReference();
        set {
            if(this.isApplyingRunOptionSelection||this.isApplyingUiState) {
                if(this.SetProperty(ref this.selectedRunOption,value)&&!this.isApplyingUiState) {
                    this.ApplySelectedRunOption(value,true);
                    this.SaveUiState();
                }
                return;
            }

            if(this.selectedRunOption==value) {
                return;
            }

            _=this.SelectRunOptionAsync(value);
        }
    }

    /// <summary>
    /// AdaptiveThresholdSecondsText プロパティです。
    /// </summary>
    /// <value>AdaptiveThresholdSecondsText の値を取得/設定します。</value>
    public string AdaptiveThresholdSecondsText {
        get => this.adaptiveThresholdSecondsText;
        set => this.SetProperty(ref this.adaptiveThresholdSecondsText,value);
    }

    /// <summary>
    /// SelectedMetric プロパティです。
    /// </summary>
    /// <value>SelectedMetric の値を取得/設定します。</value>
    public MetricOption SelectedMetric {
        get => this.selectedMetric;
        set {
            if(this.SetProperty(ref this.selectedMetric,value)) {
                this.UpdateAllSourceTabCharts();
                this.OnPropertyChanged(nameof(this.IsTimeMetricSelected));
                this.OnPropertyChanged(nameof(this.IsAllocatedMetricSelected));
            }
        }
    }

    /// <summary>
    /// IsTimeMetricSelected プロパティです。
    /// </summary>
    /// <value>IsTimeMetricSelected の値を取得/設定します。</value>
    public bool IsTimeMetricSelected {
        get => this.SelectedMetric.Kind==BenchmarkMetricKind.Time;
        set {
            if(value) {
                this.SetMetric(BenchmarkMetricKind.Time);
            }
        }
    }

    /// <summary>
    /// IsAllocatedMetricSelected プロパティです。
    /// </summary>
    /// <value>IsAllocatedMetricSelected の値を取得/設定します。</value>
    public bool IsAllocatedMetricSelected {
        get => this.SelectedMetric.Kind==BenchmarkMetricKind.Allocated;
        set {
            if(value) {
                this.SetMetric(BenchmarkMetricKind.Allocated);
            }
        }
    }

    /// <summary>
    /// SelectedChartMode プロパティです。
    /// </summary>
    /// <value>SelectedChartMode の値を取得/設定します。</value>
    public ChartModeOption SelectedChartMode {
        get => this.selectedChartMode;
        set {
            if(this.SetProperty(ref this.selectedChartMode,value)) {
                this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
                this.UpdateAllSourceTabCharts();
            }
        }
    }

    /// <summary>
    /// SelectedResultElementCount プロパティです。
    /// </summary>
    /// <value>SelectedResultElementCount の値を取得/設定します。</value>
    public ResultElementCountOption? SelectedResultElementCount {
        get => this.selectedResultElementCount;
        set {
            if(this.SetProperty(ref this.selectedResultElementCount,value)) {
                if(value is not null) {
                    this.CurrentElementCountText=FormatElementCountText(value.Value);
                }
                this.SyncSelectedResultElementCountIndex();
                if(!this.isApplyingResultElementCount) {
                    this.RefreshVisibleResults();
                    this.ApplySelectedElementCountToTable();
                    this.UpdateChart();
                }
                if(!this.isApplyingUiState&&!this.isApplyingResultElementCount&&!this.IsBenchmarkRunning) {
                    this.SaveUiState();
                }
            }
        }
    }

    /// <summary>
    /// SelectedResultElementCountIndex プロパティです。
    /// </summary>
    /// <value>SelectedResultElementCountIndex の値を取得/設定します。</value>
    public double SelectedResultElementCountIndex {
        get => this.selectedResultElementCountIndex;
        set {
            var rounded = Math.Round(value,MidpointRounding.AwayFromZero);
            var clamped = Math.Clamp(rounded,0,this.ChartElementCountSliderMaximum);
            if(this.SetProperty(ref this.selectedResultElementCountIndex,clamped)) {
                this.SelectResultElementCountByIndex((int)clamped);
            }
        }
    }

    /// <summary>
    /// ChartElementCountSliderMaximum プロパティです。
    /// </summary>
    /// <value>ChartElementCountSliderMaximum の値を取得/設定します。</value>
    public double ChartElementCountSliderMaximum => Math.Max(0,this.ResultElementCountOptions.Count-1);

    /// <summary>
    /// IsChartElementSliderVisible プロパティです。
    /// </summary>
    /// <value>IsChartElementSliderVisible の値を取得/設定します。</value>
    public bool IsChartElementSliderVisible => this.ResultElementCountOptions.Count>1&&this.SelectedChartMode.Mode==BenchmarkChartMode.ElementComparison;

    /// <summary>
    /// SelectedChartElementCountText プロパティです。
    /// </summary>
    /// <value>SelectedChartElementCountText の値を取得/設定します。</value>
    public string SelectedChartElementCountText => this.SelectedResultElementCount?.DisplayName??"-";

    /// <summary>
    /// CurrentElementCountText プロパティです。
    /// </summary>
    /// <value>CurrentElementCountText の値を取得/設定します。</value>
    public string CurrentElementCountText {
        get => this.currentElementCountText;
        private set => this.SetProperty(ref this.currentElementCountText,value);
    }

    /// <summary>
    /// ChartHeight プロパティです。
    /// </summary>
    /// <value>ChartHeight の値を取得/設定します。</value>
    public double ChartHeight {
        get => this.SelectedSourceTabViewState.ChartHeight;
        private set {
            if(Math.Abs(this.SelectedSourceTabViewState.ChartHeight-value)>0.001) {
                this.SelectedSourceTabViewState.ChartHeight=value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// GetSourceTabViewState メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    public BenchmarkSourceTabViewState GetSourceTabViewState(BenchmarkSourceTab sourceTab) =>
        this.sourceTabViewStatesByTab.TryGetValue(sourceTab,out var state)
            ? state
            : this.sourceTabViewStates[0];

    private SourceTabOption GetSourceTabOption(BenchmarkSourceTab sourceTab) =>
        this.SourceTabs.FirstOrDefault(option => option.Definition.Tab==sourceTab)??this.SourceTabs[0];

    /// <summary>
    /// GetAllBenchmarkRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<BenchmarkTableRow> GetAllBenchmarkRows() =>
        this.sourceTabViewStates.SelectMany(static state => state.Rows);

    /// <summary>
    /// InitializeAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public async Task InitializeAsync() {
        await this.RefreshAsync();
    }

    /// <summary>
    /// SortBenchmarkRows メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="column">column を指定して実行条件を制御します。</param>
    public void SortBenchmarkRows(BenchmarkGridSortColumn column) {
        var descending = false;
        if(this.CurrentSortColumn==column&&this.BenchmarkSortRules.Count==1) {
            descending=!this.CurrentSortDescending;
        }

        this.SetBenchmarkSortRules([new BenchmarkSortRule(column,descending)],true);
    }

    /// <summary>
    /// AddBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void AddBenchmarkSortRule() {
        var option = this.BenchmarkSortColumnOptions
            .FirstOrDefault(option => this.BenchmarkSortRules.All(rule => rule.Column!=option.Column))
            ??this.BenchmarkSortColumnOptions.FirstOrDefault();
        if(option is null) {
            return;
        }

        var rule = new BenchmarkSortRule(option.Column);
        this.AttachBenchmarkSortRule(rule);
        this.BenchmarkSortRules.Add(rule);
        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// RemoveBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void RemoveBenchmarkSortRule(BenchmarkSortRule rule) {
        this.BenchmarkSortRules.Remove(rule);
        if(this.BenchmarkSortRules.Count==0) {
            this.ResetBenchmarkSortRulesToDefault(true);
            return;
        }

        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// MoveBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="source">source を指定して実行条件を制御します。</param>
    /// <param name="target">target を指定して実行条件を制御します。</param>
    public void MoveBenchmarkSortRule(BenchmarkSortRule source,BenchmarkSortRule target) {
        var oldIndex = this.BenchmarkSortRules.IndexOf(source);
        var newIndex = this.BenchmarkSortRules.IndexOf(target);
        if(oldIndex<0||newIndex<0||oldIndex==newIndex) {
            return;
        }

        this.BenchmarkSortRules.Move(oldIndex,newIndex);
        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// MoveBenchmarkSortRuleUp メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void MoveBenchmarkSortRuleUp(BenchmarkSortRule rule) {
        var index = this.BenchmarkSortRules.IndexOf(rule);
        if(index<=0) {
            return;
        }

        this.BenchmarkSortRules.Move(index,index-1);
        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// MoveBenchmarkSortRuleDown メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void MoveBenchmarkSortRuleDown(BenchmarkSortRule rule) {
        var index = this.BenchmarkSortRules.IndexOf(rule);
        if(index<0||index>=this.BenchmarkSortRules.Count-1) {
            return;
        }

        this.BenchmarkSortRules.Move(index,index+1);
        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// SetBenchmarkSortRuleColumn メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    /// <param name="column">column を指定して実行条件を制御します。</param>
    public void SetBenchmarkSortRuleColumn(BenchmarkSortRule rule,BenchmarkGridSortColumn column) {
        rule.Column=column;
        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// ToggleBenchmarkSortRuleDirection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void ToggleBenchmarkSortRuleDirection(BenchmarkSortRule rule) {
        rule.Descending=!rule.Descending;
        this.ApplySortRuleChanges(true);
    }

    /// <summary>
    /// ResetBenchmarkSortRules メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void ResetBenchmarkSortRules() =>
        this.ResetBenchmarkSortRulesToDefault(true);

    /// <summary>
    /// SetBenchmarkSortRules メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rules">rules を指定して実行条件を制御します。</param>
    /// <param name="save">save を指定して実行条件を制御します。</param>
    private void SetBenchmarkSortRules(IEnumerable<BenchmarkSortRule> rules,bool save) {
        this.isApplyingSortRules=true;
        try {
            this.BenchmarkSortRules.Clear();
            foreach(var rule in rules) {
                this.AttachBenchmarkSortRule(rule);
                this.BenchmarkSortRules.Add(rule);
            }
        }
        finally {
            this.isApplyingSortRules=false;
        }
        this.ApplySortRuleChanges(save);
    }

    /// <summary>
    /// AttachBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    private void AttachBenchmarkSortRule(BenchmarkSortRule rule) {
        rule.PropertyChanged+=(_,args) => {
            if(this.isApplyingSortRules) {
                return;
            }
            if(args.PropertyName==nameof(BenchmarkSortRule.Column)
                ||args.PropertyName==nameof(BenchmarkSortRule.Descending)) {
                this.ApplySortRuleChanges(true);
            }
        };
    }

    /// <summary>
    /// ApplySortRuleChanges メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="save">save を指定して実行条件を制御します。</param>
    private void ApplySortRuleChanges(bool save) {
        this.RefreshCurrentSortIndicator();
        this.OnPropertyChanged(nameof(this.BenchmarkSortSummaryText));
        this.ApplyCurrentSort();
        if(save) {
            this.SaveBenchmarkSortRules();
        }
    }

    /// <summary>
    /// ToggleCpu メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="cpu">cpu を指定して実行条件を制御します。</param>
    public void ToggleCpu(CpuSelectionItem cpu) {
        if(this.IsRefreshing) {
            return;
        }

        cpu.IsSelected=!cpu.IsSelected;
    }

    internal void ToggleCpuChecks(IReadOnlyList<CpuSelectionItem> cpus) {
        if(this.IsRefreshing||cpus.Count==0) {
            return;
        }

        var targets=cpus.Distinct().ToArray();
        var selected=GridCheckToggleLogic.ResolveTargetCheckedState(
            targets.Select(static cpu => (bool?)cpu.IsSelected));
        if(selected is { } value){
            this.ApplyCpuSelection(targets,value);
        }
    }

    /// <summary>
    /// ToggleCpuSection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="section">section を指定して実行条件を制御します。</param>
    public void ToggleCpuSection(CpuTableSection section) {
        if(this.IsRefreshing) {
            return;
        }

        var selected = section.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(section.Cells.Select(static cell => cell.Cpu),selected);
    }

    /// <summary>
    /// ToggleCpuColumn メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="section">section を指定して実行条件を制御します。</param>
    /// <param name="columnIndex">columnIndex を指定して実行条件を制御します。</param>
    public void ToggleCpuColumn(CpuTableSection section,int columnIndex) {
        if(this.IsRefreshing) {
            return;
        }

        var cells = section.Rows
            .Where(row => columnIndex<row.Cells.Count)
            .Select(row => row.Cells[columnIndex])
            .ToArray();
        var selected = section.GetColumnState(columnIndex)!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(cells.Select(static cell => cell.Cpu),selected);
    }

    /// <summary>
    /// ToggleCpuRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    public void ToggleCpuRow(CpuTableRow row) {
        if(this.IsRefreshing) {
            return;
        }

        var selected = row.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(row.Cells.Select(static cell => cell.Cpu),selected);
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    public void ToggleAllBenchmarkSelection(BenchmarkSourceTabDefinition? sourceTab = null) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var rows = sourceTab is { } tab
            ? this.GetBenchmarkRows(tab).Where(static row => row.ProjectCells.Any(static cell => cell.HasCase)).ToArray()
            : this.GetAllBenchmarkRows().Where(static row => row.ProjectCells.Any(static cell => cell.HasCase)).ToArray();
        var state = GetRowSelectionState(rows);
        if(state is null) {
            this.SetBenchmarkRows(rows,true);
            return;
        }

        if(state is true) {
            this.SetBenchmarkRows(rows,false);
            return;
        }

        this.SetBenchmarkRows(rows,true);
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    public void ToggleBenchmarkColumnGroup(BenchmarkColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab = null) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var tab = sourceTab??this.SelectedSourceDefinition;
        var cells = this.GetBenchmarkCells(group.ColumnKeys,tab).Where(static cell => cell.HasCase).ToArray();
        var selected = GetSelectionState(cells) is not true;
        this.SetBenchmarkCells(cells,selected,tab);
    }

    public string 全体ベンチマークシェア概要表示 {
        get => this.全体ベンチマークシェア概要文字列;
        private set{
            if(this.SetProperty(ref this.全体ベンチマークシェア概要文字列,value)){
                this.OnPropertyChanged(nameof(this.OverallSourceProgressSummaryText));
            }
        }
    }

    public string OverallSourceProgressSummaryText =>
        "確定 "+(this.OverallSourceTabProgress.Total<=0 ? "0/0" : this.OverallSourceTabProgress.FractionText)
        +","+this.全体ベンチマークシェア概要短縮文字列;

    /// <summary>
    /// ToggleBenchmarkRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    public void ToggleBenchmarkRow(BenchmarkTableRow row) {
        this.ToggleBenchmarkRows([row]);
    }

    internal void ToggleBenchmarkRows(IEnumerable<BenchmarkTableRow> rows) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var targets = rows
            .Where(static item => item.ProjectCells.Any(static cell => cell.HasCase))
            .Distinct()
            .ToArray();
        var selected = GridCheckToggleLogic.ResolveTargetCheckedState(
            targets.Select(static row => row.IsSelectionChecked));
        if(selected is not { } value) {
            return;
        }

        this.SetBenchmarkRows(targets,value);
    }

    internal void SynchronizeBenchmarkRowUiSelection(
        BenchmarkSourceTabViewState state,
        IReadOnlyList<BenchmarkTableRow> addedRows,
        IReadOnlyList<BenchmarkTableRow> removedRows) {
        var selectionChanged=false;

        foreach(var removed in removedRows) {
            SetRowUiSelection(removed,false);
            if(removed.ChartBar is { } chartBar) {
                state.UiSelectedChartBars.Remove(chartBar);
            }
            selectionChanged=true;
        }

        foreach(var added in addedRows) {
            SetRowUiSelection(added,true);
            if(added.ChartBar is { } chartBar) {
                state.UiSelectedChartBars.Add(chartBar);
            }
            selectionChanged=true;
        }

        if(selectionChanged) {
            UiThreadBudget.Record("chart-selection-series-update-count",0);
            state.NotifyChartSelectionChanged();
        }
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>


    /// <summary>
    /// SelectBenchmarkCell メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>


    /// <summary>
    /// SelectBenchmarkRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>


    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>


    /// <summary>
    /// ToggleBenchmarkCellCheck メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    internal void ToggleBenchmarkCellCheck(BenchmarkTableRow row,string project) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        if(!this.TryGetBenchmarkCellBySelectionKey(row,project,out var cell)) {
            return;
        }

        var state = this.GetSourceTabViewState(row.SourceTab.Tab);
        var selectedCells = state.Rows
            .SelectMany(static row => row.ProjectCells)
            .Where(static cell => cell.IsUiSelected)
            .ToArray();
        var targets = cell.IsUiSelected&&selectedCells.Length>0
            ? selectedCells
            : [cell];
        var targetRows = state.Rows
            .Where(row => row.ProjectCells.Any(targets.Contains))
            .ToArray();
        var selected = targets.Length>1
            ? true
            : !row.IsSelected;
        this.SetBenchmarkRows(targetRows,selected);
    }

    /// <summary>
    /// SelectBenchmarkCellFromChart メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="benchmark">benchmark を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>


    /// <summary>
    /// SelectBenchmarkRowFromChart メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="benchmark">benchmark を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>


    /// <summary>
    /// RefreshAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task RefreshAsync() {
        this.IsRefreshing=true;
        this.StatusText="読み込み中";
        StartupDiagnostics.Write("Benchmark catalog refresh started.");
        var dbUnavailable = false;
        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,true,forceNewSelection:true);
        }
        catch(Exception ex) {
            dbUnavailable=true;
            StartupDiagnostics.Write("Benchmark run list refresh failed.",ex);
        }
        try {
            this.CaptureCurrentBenchmarkSelections();
            var loaded = await this.catalogService.LoadAsync(CancellationToken.None);
            this.catalogCases=loaded;
            this.RefreshBenchmarkCaseIndex(loaded);
            this.RebuildBenchmarkRowsFromCatalog();
            this.RestoreRunSelection(null,null);
            this.RefreshBenchmarkSelectionAggregates();
            if(!this.RestoreCurrentRunDisplayForSelectedTab()) {
                this.RefreshVisibleResults();
                this.ApplySelectedElementCountToTable();
                this.RefreshImprovementPrompt();
            }
            this.RefreshSourceTabProgresses();
            this.ApplyCurrentSort();
            this.StatusText=this.BenchmarkRows.Count+" 件のベンチマーク"+(dbUnavailable ? "(実行履歴DB利用不可)" : "");
            StartupDiagnostics.Write($"Benchmark catalog loaded. Rows={this.BenchmarkRows.Count} DbUnavailable={dbUnavailable}");
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark catalog load failed.",ex);
            this.StatusText="読み込み失敗 "+ex.Message;
        }
        finally {
            this.IsRefreshing=false;
            this.ApplyPendingSourceTabSelection();
        }
    }

    /// <summary>
    /// RebuildBenchmarkRowsFromCatalog メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RebuildBenchmarkRowsFromCatalog() {
        using(this.BeginDataGridUpdateBatch()) {
            var elementCount = this.SelectedResultElementCount?.Value??0;
            var builtRows = this.sourceTabViewStates
                .AsParallel()
                .AsOrdered()
                .WithDegreeOfParallelism(Math.Max(1,Math.Min(Environment.ProcessorCount,this.sourceTabViewStates.Count)))
                .Select(state => (State: state,Rows: this.CreateMethodRows(this.catalogCases,state.Definition,elementCount)))
                .ToArray();
            foreach(var item in builtRows) {
                var rows = item.Rows;
                foreach(var row in rows) {
                    this.SubscribeBenchmarkRow(row);
                    this.ApplyStoredBenchmarkSelection(row);
                }
                item.State.Rows.ReplaceRange(rows);
            }
            this.RebuildBenchmarkRowIndex();
            this.RefreshBenchmarkSortColumns();
        }
    }

    /// <summary>
    /// RebuildRowsForSelectedTab メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RebuildRowsForSelectedTab() {
        if(this.catalogCases.Count==0) {
            _=this.RefreshAsync();
            return;
        }

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();
        this.RebuildBenchmarkRowsFromCatalog();
        var rebuildRowsMilliseconds = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        this.RefreshBenchmarkSelectionAggregates();
        var aggregateMilliseconds = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        var displaySource = "memory";
        if(!this.RestoreCurrentRunDisplayForSelectedTab()) {
            if(this.ShouldLoadSelectedRunFromDatabaseForDisplay()) {
                displaySource="database";
                this.LoadSelectedRunFromDatabase();
            }
            else {
                displaySource="visible";
                this.RefreshVisibleResults();
                this.ApplySelectedElementCountToTable();
                this.RefreshImprovementPrompt();
            }
        }
        var displayMilliseconds = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        this.RefreshSourceTabProgresses();
        var progressMilliseconds = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        this.ApplyCurrentSort();
        var sortMilliseconds = phaseStopwatch.Elapsed;
        totalStopwatch.Stop();
        this.StatusText=this.BenchmarkRows.Count+" 件のベンチマーク";
        StartupDiagnostics.Write(
            $"Source tab rebuild {this.SelectedSourceDefinition.Id} rows={this.BenchmarkRows.Count} total={FormatSwitchMeasureMilliseconds(totalStopwatch.Elapsed)}ms rebuildRows={FormatSwitchMeasureMilliseconds(rebuildRowsMilliseconds)}ms aggregates={FormatSwitchMeasureMilliseconds(aggregateMilliseconds)}ms display={FormatSwitchMeasureMilliseconds(displayMilliseconds)}ms displaySource={displaySource} progress={FormatSwitchMeasureMilliseconds(progressMilliseconds)}ms sort={FormatSwitchMeasureMilliseconds(sortMilliseconds)}ms");
    }

    /// <summary>
    /// FormatSwitchMeasureMilliseconds メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="elapsed">elapsed を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string FormatSwitchMeasureMilliseconds(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// ApplyPendingSourceTabSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyPendingSourceTabSelection() {
        if(this.pendingSourceTabOption is { } pending) {
            this.pendingSourceTabOption=null;
            if(pending!=this.selectedSourceTab) {
                _=this.SelectSourceTabAsync(pending);
            }
        }
    }

    /// <summary>
    /// CreateMethodRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cases">cases を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases) =>
        this.CreateMethodRows(cases,this.SelectedSourceDefinition);

    /// <summary>
    /// CreateMethodRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cases">cases を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases,BenchmarkSourceTabDefinition sourceTab) {
        return this.CreateMethodRows(cases,sourceTab,this.SelectedResultElementCount?.Value??0);
    }

    /// <summary>
    /// CreateMethodRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cases">cases を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases,BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        return BenchmarkRowGroupingLogic.CreateRows(cases,sourceTab,elementCount)
            .Select(row => new BenchmarkTableRow(
                row.RowKey,
                row.MethodName,
                row.BenchmarkName,
                sourceTab,
                row.CasesByProject))
            .ToArray();
    }

    /// <summary>
    /// RefreshBenchmarkCaseIndex メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="cases">cases を指定して実行条件を制御します。</param>
    private void RefreshBenchmarkCaseIndex(IReadOnlyList<BenchmarkCase> cases) {
        this.benchmarkCasesByProjectMethod.Clear();
        foreach(var benchmark in cases) {
            this.benchmarkCasesByProjectMethod[CreateBenchmarkCaseKey(benchmark.MethodName,benchmark.Project,benchmark.TypeFullName,benchmark.ArgumentKey)]=benchmark;
        }
    }

    /// <summary>
    /// RebuildBenchmarkRowIndex メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RebuildBenchmarkRowIndex() {
        this.benchmarkRowsByResultKey.Clear();
        this.benchmarkRowsByProjectMethod.Clear();
        foreach(var row in this.GetAllBenchmarkRows()) {
            foreach(var item in row.CasesByProject) {
                var benchmark = item.Value;
                this.AddBenchmarkRowIndex(row,benchmark.Project,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey);
                if(!IsScalableSourceTab(row.SourceTab.Tab)) {
                    this.AddBenchmarkRowIndex(row,benchmark.Project,string.Empty,benchmark.MethodName,string.Empty);
                }
                this.benchmarkRowsByProjectMethod.TryAdd(CreateBenchmarkRowMethodProjectIndexKey(row.SourceTab.Tab,benchmark.Project,benchmark.MethodName),row);
            }
        }
    }

    /// <summary>
    /// AddBenchmarkRowIndex メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    private void AddBenchmarkRowIndex(BenchmarkTableRow row,string project,string typeFullName,string method,string argumentKey) {
        this.benchmarkRowsByResultKey.TryAdd(CreateBenchmarkRowResultIndexKey(row.SourceTab.Tab,project,typeFullName,method,argumentKey),row);
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    private void SetAllBenchmarkSelection(bool selected,BenchmarkSourceTabDefinition? sourceTab = null) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var rows = sourceTab is { } tab
            ? this.GetBenchmarkRows(tab).Where(static row => row.ProjectCells.Any(static cell => cell.HasCase))
            : this.GetAllBenchmarkRows().Where(static row => row.ProjectCells.Any(static cell => cell.HasCase));
        this.SetBenchmarkRows(rows,selected);
    }

    private void SetBenchmarkRows(IEnumerable<BenchmarkTableRow> rows,bool selected) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var targets = rows.Where(static row => row.ProjectCells.Any(static cell => cell.HasCase)).Distinct().ToArray();
        if(targets.Length==0) {
            this.RefreshBenchmarkSelectionAggregates();
            return;
        }

        this.isApplyingBenchmarkSelection=true;
        try {
            foreach(var row in targets) {
                ApplyRowSelectionToCells(row,selected);
            }
            this.RefreshBenchmarkSelectionAggregates();
        }
        finally {
            this.isApplyingBenchmarkSelection=false;
        }
        this.SaveUiState();
        this.RefreshBenchmarkSelectionDisplay();
    }

    /// <summary>
    /// SubscribeBenchmarkRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    private void SubscribeBenchmarkRow(BenchmarkTableRow row) {
        row.PropertyChanged+=(_,args) => {
            if(args.PropertyName==nameof(BenchmarkTableRow.IsSelected)&&!this.isApplyingUiState&&!this.isApplyingBenchmarkSelection) {
                this.SaveUiState();
            }
        };
        foreach(var cell in row.ProjectCells) {
            cell.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(BenchmarkProjectCell.IsSelected)&&!this.isApplyingUiState&&!this.isApplyingBenchmarkSelection) {
                    this.RefreshBenchmarkSelectionAggregates();
                    this.SaveUiState();
                    this.RefreshBenchmarkSelectionDisplay();
                }
                if(args.PropertyName==nameof(BenchmarkProjectCell.Metrics)) {
                    this.GetSourceTabViewState(row.SourceTab.Tab).DirtyChartRows.Add(row);
                }
            };
        }
    }

    /// <summary>
    /// GetBenchmarkRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<BenchmarkTableRow> GetBenchmarkRows(BenchmarkSourceTabDefinition sourceTab) =>
        this.GetSourceTabViewState(sourceTab.Tab).Rows;

    /// <summary>
    /// GetBenchmarkCells メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="projectKeys">projectKeys を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<BenchmarkProjectCell> GetBenchmarkCells(IReadOnlyList<string> projectKeys,BenchmarkSourceTabDefinition sourceTab) {
        foreach(var row in this.GetBenchmarkRows(sourceTab)) {
            foreach(var project in projectKeys) {
                if(this.TryGetBenchmarkCellBySelectionKey(row,project,out var cell)) {
                    yield return cell;
                }
            }
        }
    }

    // cells は必ず単一の sourceTab に属する行から集めたものを渡すこと。ExpandBenchmarkSelectionCells は
    // その sourceTab 内だけで RowKey+Left/Right の対応を展開するため、他Sourceタブへ選択が漏れない。
    /// <summary>
    /// SetBenchmarkCells メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="cells">cells を指定して実行条件を制御します。</param>
    /// <param name="selected">selected を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    private void SetBenchmarkCells(IEnumerable<BenchmarkProjectCell> cells,bool selected,BenchmarkSourceTabDefinition sourceTab) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var targets = this.ExpandBenchmarkSelectionCells(cells,sourceTab);
        if(targets.Length==0) {
            this.RefreshBenchmarkSelectionAggregates();
            return;
        }

        this.isApplyingBenchmarkSelection=true;
        try {
            foreach(var cell in targets) {
                cell.IsSelected=selected;
            }
            this.RefreshBenchmarkSelectionAggregates();
        }
        finally {
            this.isApplyingBenchmarkSelection=false;
        }
        this.SaveUiState();
        this.RefreshBenchmarkSelectionDisplay();
    }

    /// <summary>
    /// ExpandBenchmarkSelectionCells メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cells">cells を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkProjectCell[] ExpandBenchmarkSelectionCells(IEnumerable<BenchmarkProjectCell> cells,BenchmarkSourceTabDefinition sourceTab) {
        var requested = cells.Where(static cell => cell.HasCase).Distinct().ToArray();
        if(requested.Length==0) {
            return [];
        }

        var rows = this.GetBenchmarkRows(sourceTab).ToArray();
        var requestedSet = requested.ToHashSet();
        var syncKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows) {
            if(requestedSet.Contains(row.LeftCell)) {
                syncKeys.Add(CreateBenchmarkSelectionSyncKey(row.RowKey,"Left"));
            }
            if(requestedSet.Contains(row.RightCell)) {
                syncKeys.Add(CreateBenchmarkSelectionSyncKey(row.RowKey,"Right"));
            }
        }
        if(syncKeys.Count==0) {
            return requested;
        }

        var targets = new List<BenchmarkProjectCell>();
        foreach(var row in rows) {
            if(row.LeftCell.HasCase&&syncKeys.Contains(CreateBenchmarkSelectionSyncKey(row.RowKey,"Left"))) {
                targets.Add(row.LeftCell);
            }
            if(row.RightCell.HasCase&&syncKeys.Contains(CreateBenchmarkSelectionSyncKey(row.RowKey,"Right"))) {
                targets.Add(row.RightCell);
            }
        }
        return targets.Distinct().ToArray();
    }

    /// <summary>
    /// TryGetBenchmarkCellBySelectionKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="key">key を指定して実行条件を制御します。</param>
    /// <param name="cell">cell を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool TryGetBenchmarkCellBySelectionKey(BenchmarkTableRow row,string key,out BenchmarkProjectCell cell) {
        if(string.Equals(key,"Left",StringComparison.OrdinalIgnoreCase)) {
            cell=row.LeftCell;
            return true;
        }
        if(string.Equals(key,"Right",StringComparison.OrdinalIgnoreCase)) {
            cell=row.RightCell;
            return true;
        }
        if(string.Equals(key,row.SourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)) {
            cell=row.LeftCell;
            return true;
        }
        if(string.Equals(key,row.SourceTab.RightProject,StringComparison.OrdinalIgnoreCase)) {
            cell=row.RightCell;
            return true;
        }
        if(row.GetProjectCell(key) is { } projectCell) {
            cell=projectCell;
            return true;
        }

        cell=null!;
        return false;
    }

    /// <summary>
    /// CreateBenchmarkSelectionSyncKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="rowKey">rowKey を指定して実行条件を制御します。</param>
    /// <param name="side">side を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateBenchmarkSelectionSyncKey(string rowKey,string side) =>
        rowKey+"|"+side;



    // 列/プロジェクトヘッダーは現在タブだけを反映する。左上の全体チェックは全Sourceタブを対象にする。
    /// <summary>
    /// RefreshBenchmarkSelectionAggregates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkSelectionAggregates() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.RefreshSelectionState();
        }
        var currentTab = this.SelectedSourceDefinition;
        foreach(var group in this.BenchmarkColumnSelectionGroups) {
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys,currentTab));
        }
        foreach(var group in this.BenchmarkProjectSelectionGroups) {
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys,currentTab));
        }
        foreach(var state in this.SourceTabViewStates) {
            var rows = state.Rows
                .Where(static row => row.ProjectCells.Any(static cell => cell.HasCase))
                .ToArray();
            state.IsBenchmarkSelectionChecked=GetRowSelectionState(rows);
        }
        var allRows = this.GetAllBenchmarkRows()
            .Where(static row => row.ProjectCells.Any(static cell => cell.HasCase))
            .ToArray();
        this.IsAllBenchmarkSelectionChecked=GetRowSelectionState(allRows);
        this.HasSelectedBenchmarkCells=this.HasAnySelectedBenchmarkCell();
        this.RefreshSourceTabProgresses();
    }

    // Run ボタンの活性はライブなチェック状態で判定する。進捗バッジ用の CreateProgressCells は
    // 実行時に凍結した選択スナップショットを優先するため、実行後に選択を変えると実態と乖離し、
    // 「ボタンは有効なのに RunAsync が選択0件で無言中断する」原因になる。
    /// <summary>
    /// HasAnySelectedBenchmarkCell メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool HasAnySelectedBenchmarkCell() =>
        this.SourceTabs.Any(option => this.HasAnyLiveSelectedBenchmarkCell(option.Definition));

    /// <summary>
    /// HasAnyLiveSelectedBenchmarkCell メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool HasAnyLiveSelectedBenchmarkCell(BenchmarkSourceTabDefinition sourceTab) {
        var state = this.GetSourceTabViewState(sourceTab.Tab);
        if(state.Rows.Count>0) {
            return state.Rows.Any(static row => row.IsSelected&&row.ProjectCells.Any(static cell => cell.HasCase));
        }

        return BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,sourceTab)
            .Any(row => row.CasesByProject.Count>0
                &&this.benchmarkRowSelections.TryGetValue(CreateRowSelectionKey(sourceTab,row.RowKey),out var selected)
                &&selected);
    }

    /// <summary>
    /// GetSelectionState メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cells">cells を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool? GetSelectionState(IEnumerable<BenchmarkProjectCell> cells) {
        var targets = cells.Where(static cell => cell.HasCase).ToArray();
        if(targets.Length==0) {
            return false;
        }

        var selected = targets.Count(static cell => cell.IsSelected);
        return selected==0
            ? false
            : selected==targets.Length
                ? true
                : null;
    }

    /// <summary>
    /// rowKey プロパティです。
    /// </summary>
    /// <value>rowKey の値を取得/設定します。</value>
    private static int GetBenchmarkRowIndex(BenchmarkSourceTabViewState state,string benchmark,string rowKey = "") {
        for(var index = 0; index<state.Rows.Count; index++) {
            var row = state.Rows[index];
            if(!string.IsNullOrWhiteSpace(rowKey)
                &&string.Equals(row.RowKey,rowKey,StringComparison.OrdinalIgnoreCase)) {
                return index;
            }

            if(string.IsNullOrWhiteSpace(rowKey)
                &&string.Equals(row.RowKey,benchmark,StringComparison.OrdinalIgnoreCase)) {
                return index;
            }

            if(string.IsNullOrWhiteSpace(rowKey)
                &&string.Equals(row.MethodName,benchmark,StringComparison.OrdinalIgnoreCase)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// GetProjectColumnIndex メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static int GetProjectColumnIndex(BenchmarkSourceTabDefinition sourceTab,string project) =>
        sourceTab.ProjectOrder
            .Select((item,index) => new { item,index })
            .FirstOrDefault(item => string.Equals(item.item,ResolveProjectKey(sourceTab,project),StringComparison.OrdinalIgnoreCase))
            ?.index??-1;

    /// <summary>
    /// ResolveProjectKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string ResolveProjectKey(BenchmarkSourceTabDefinition sourceTab,string project) =>
        project switch {
            "Left" => sourceTab.LeftProject,
            "Right" => sourceTab.RightProject,
            _ => project,
        };

    /// <summary>
    /// LoadImprovementCandidatesChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadImprovementCandidatesChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapImprovementCandidates(this.uiRunStore.QueryAnalysisView("BenchmarkImprovementCandidates",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkImprovementCandidates load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadCategorySummaryChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkCategoryRatioPoint> LoadCategorySummaryChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapCategorySummary(this.uiRunStore.QueryAnalysisView("BenchmarkCategorySummary",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkCategorySummary load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadHistoryTrendChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkTrendSeries> LoadHistoryTrendChartData() =>
        this.時間シェア履歴を読込();

    public IReadOnlyList<BenchmarkTrendSeries> 時間シェア履歴を読込() {
        try {
            var rows=this.選択タブのシェア履歴を読込("履歴時間シェア");
            return BenchmarkAnalysisViewMapper.時間シェア履歴を変換(rows);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("履歴時間シェア load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkTrendSeries> メモリシェア履歴を読込() {
        try {
            var rows=this.選択タブのシェア履歴を読込("履歴メモリシェア");
            return BenchmarkAnalysisViewMapper.メモリシェア履歴を変換(rows);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("履歴メモリシェア load failed.",ex);
            return [];
        }
    }

    private IReadOnlyList<IReadOnlyDictionary<string,object?>> 選択タブのシェア履歴を読込(string viewName) {
        var selectedSourceTabId = this.SelectedSourceDefinition.Id;
        return this.uiRunStore.QueryAnalysisView(viewName,1000)
            .Where(row => 分析行がタブ対象か(row,selectedSourceTabId))
            .ToArray();
    }

    private static bool 分析行がタブ対象か(IReadOnlyDictionary<string,object?> row,string sourceTabId) =>
        row.TryGetValue("タブ名",out var value)
        &&string.Equals(
            Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture),
            sourceTabId,
            StringComparison.OrdinalIgnoreCase);

    private static bool? GetRowSelectionState(IEnumerable<BenchmarkTableRow> rows) {
        var targets = rows.Where(static row => row.ProjectCells.Any(static cell => cell.HasCase)).ToArray();
        if(targets.Length==0) {
            return false;
        }

        var states = targets.Select(static row => row.IsSelectionChecked).ToArray();
        var selected = states.Count(static state => state is true);
        var mixed = states.Any(static state => state is null);
        return selected==0&&!mixed
            ? false
            : selected==targets.Length&&!mixed
                ? true
                : null;
    }

    /// <summary>
    /// LoadScaleTrendChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkTrendSeries> LoadScaleTrendChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapScaleTrend(this.uiRunStore.QueryAnalysisView("BenchmarkScaleTrend",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkScaleTrend load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadMissingFailureMatrixBadges メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkBadgeItem> LoadMissingFailureMatrixBadges() {
        try {
            return BenchmarkAnalysisViewMapper.MapMissingFailureMatrix(this.uiRunStore.QueryAnalysisView("BenchmarkMissingFailureMatrix",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkMissingFailureMatrix load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadMeasurementQualityGauges メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkQualityGaugePoint> LoadMeasurementQualityGauges() {
        try {
            return BenchmarkAnalysisViewMapper.MapMeasurementQuality(this.uiRunStore.QueryAnalysisView("BenchmarkMeasurementQuality",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkMeasurementQuality load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadFallbackDiagnosticsBadges メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkBadgeItem> LoadFallbackDiagnosticsBadges() {
        try {
            return BenchmarkAnalysisViewMapper.MapFallbackDiagnostics(this.uiRunStore.QueryAnalysisView("BenchmarkFallbackDiagnostics",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkFallbackDiagnostics load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// RefreshDbRunListAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cancellationToken">cancellationToken を指定して実行条件を制御します。</param>
    /// <param name="loadSelected">loadSelected を指定して実行条件を制御します。</param>
    private async Task RefreshDbRunListAsync(CancellationToken cancellationToken,bool loadSelected,long? preferredUiRunId = null,bool preserveDisplay = false,bool forceNewSelection = false) {
        var runs = await Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            this.uiRunStore.EnsureSchema();
            return this.uiRunStore.ListRuns(GetUiRunAppKind())
                .Where(run => string.Equals(run.ExecutionProfile,this.SelectedExecutionProfile.Profile.ToString(),StringComparison.OrdinalIgnoreCase))
                .Where(run => this.unsavedUiRunId!=run.RunId)
                .ToArray();
        },cancellationToken);
        if(forceNewSelection) {
            this.ClearRecoveredRunResumeStateForNewSelection();
        }
        else {
            this.TryAutoPauseLatestResumableRun(runs);
        }
        var options = runs.Select(this.CreateRunOption).ToArray();
        var selectedUiRunId = preferredUiRunId??this.SelectedRunOption?.UiRunId;
        var autoPausedRun = runs.FirstOrDefault(run => run.Status is UiRunStatus.Running or UiRunStatus.Queued or UiRunStatus.Paused
            &&this.HasUnfinishedSelectedMeasurements(run.RunId));
        this.RunOptions.Clear();
        this.RunOptions.Add(CreateNewRunOption(this.SelectedExecutionProfile.Profile));
        foreach(var option in options) {
            this.RunOptions.Add(option);
        }
        var newRunOption = this.RunOptions.FirstOrDefault(IsNewRunOption);
        var optionToSelect = forceNewSelection
            ? newRunOption??this.RunOptions.FirstOrDefault()
            : autoPausedRun is not null
            ? this.RunOptions.FirstOrDefault(option => option.UiRunId==autoPausedRun.RunId)??this.RunOptions.FirstOrDefault()
            : this.RunOptions.FirstOrDefault(option => option.UiRunId==selectedUiRunId)??this.RunOptions.FirstOrDefault();
        if(preserveDisplay||forceNewSelection) {
            this.SetSelectedRunOptionPreservingDisplay(optionToSelect,false);
        }
        else {
            this.SetSelectedRunOption(
                optionToSelect,
                loadSelected,
                false);
        }
        this.NotifySelectedRunOptionChangedForPicker();
    }

    private void ClearRecoveredRunResumeStateForNewSelection() {
        this.activeUiRunId=null;
        this.currentUnsavedRunRoot=null;
        this.SetActiveRunRoot(null);
    }

    private async Task RefreshDbRunOptionsOnlyAsync(CancellationToken cancellationToken,long? preferredUiRunId = null) {
        var runs = await Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            this.uiRunStore.EnsureSchema();
            return this.uiRunStore.ListRuns(GetUiRunAppKind())
                .Where(run => string.Equals(run.ExecutionProfile,this.SelectedExecutionProfile.Profile.ToString(),StringComparison.OrdinalIgnoreCase))
                .Where(run => this.unsavedUiRunId!=run.RunId)
                .ToArray();
        },cancellationToken);
        this.TryAutoPauseLatestResumableRun(runs);
        var options = runs.Select(this.CreateRunOption).ToArray();
        var currentSelection = this.SelectedRunOption;
        this.RunOptions.Clear();
        this.RunOptions.Add(CreateNewRunOption(this.SelectedExecutionProfile.Profile));
        foreach(var option in options) {
            this.RunOptions.Add(option);
        }

        var replacement = preferredUiRunId is { } runId
            ? this.RunOptions.FirstOrDefault(option => option.UiRunId==runId)
            : currentSelection is null
                ? null
                : this.RunOptions.FirstOrDefault(option => option.UiRunId==currentSelection.UiRunId)
                    ??(IsNewRunOption(currentSelection) ? this.RunOptions.FirstOrDefault(IsNewRunOption) : null);
        if(replacement is not null&&currentSelection is not null&&replacement.UiRunId==currentSelection.UiRunId) {
            this.SetSelectedRunOptionPreservingDisplay(replacement,false);
            this.NotifySelectedRunOptionChangedForPicker();
        }
    }

    private void TryAutoPauseLatestResumableRun(IReadOnlyList<UiRunRecord> runs){
        var latest = runs.FirstOrDefault();
        if(latest is null||latest.Status is not (UiRunStatus.Running or UiRunStatus.Queued or UiRunStatus.Paused)){
            return;
        }

        if(!this.HasUnfinishedSelectedMeasurements(latest.RunId)){
            return;
        }

        if(latest.Status!=UiRunStatus.Paused){
            this.uiRunStore.SetRunStatus(latest.RunId,UiRunStatus.Paused,"Recovered unfinished benchmark run from DB.");
        }

        if(this.uiRunStore.GetRunSettings(latest.RunId) is { } settings&&!string.IsNullOrWhiteSpace(settings.RunRoot)){
            this.activeUiRunId=latest.RunId;
            this.SetActiveRunRoot(settings.RunRoot);
        }
    }

    private bool HasUnfinishedSelectedMeasurements(long runId){
        foreach(var item in this.uiRunStore.ListBenchmarkSnapshotItems(runId)){
            if(!item.IsSelected){
                continue;
            }
            if(item.Left.Status is UiRunStatus.Queued or UiRunStatus.Running or UiRunStatus.Paused){
                return true;
            }
            if(item.Right.Status is UiRunStatus.Queued or UiRunStatus.Running or UiRunStatus.Paused){
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// CreateRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="run">run を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkRunOption CreateRunOption(UiRunRecord run) {
        var snapshotItems = this.uiRunStore.ListBenchmarkSnapshotItems(run.RunId);
        var sourceTab = Enum.TryParse<BenchmarkSourceTab>(run.SourceTab,true,out var parsedSourceTab)
            ? parsedSourceTab
            : this.SelectedSourceDefinition.Tab;
        var elementCounts = BenchmarkElementCounts.DefaultInitial.ToArray();
        var completedJobs = snapshotItems
            .SelectMany(static item => new[] { item.Left,item.Right })
            .Count(static side => side.Status==UiRunStatus.Completed);
        var selectedJobs = snapshotItems
            .Count(static item => item.IsSelected);
        var executionProfile = Enum.TryParse<BenchmarkExecutionProfile>(run.ExecutionProfile,true,out var parsedProfile)
            ? parsedProfile
            : this.SelectedExecutionProfile.Profile;
        var label = run.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture)
            +" "
            +completedJobs.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"/"
            +selectedJobs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new BenchmarkRunOption(
            unchecked((int)Math.Min(run.RunId,int.MaxValue)),
            run.RunId,
            label,
            string.Empty,
            executionProfile,
            run.UpdatedAt,
            run.UpdatedAt,
            completedJobs,
            snapshotItems.Count*2,
            elementCounts);
    }

    /// <summary>
    /// RefreshRunsForSelectedProfileAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task RefreshRunsForSelectedProfileAsync() {
        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,true,forceNewSelection:true);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run list refresh failed.",ex);
        }
    }

    /// <summary>
    /// ClearRunHistoryAsync メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private async Task ClearRunHistoryAsync() {
        if(!this.CanClearRunHistory) {
            return;
        }

        this.IsRefreshing=true;
        try {
            this.persistenceBatcher.FlushNow();
            await this.uiRunRepository.FlushAsync();
            await Task.Run(this.uiRunStore.DeleteAllRecords);
            this.InvalidateSelectedRunLoadRequests();
            this.selectedRunStateCache=null;
            this.currentRunSession.Clear();
            this.RunOptions.Clear();
            var newRunOption = CreateNewRunOption(this.SelectedExecutionProfile.Profile);
            this.RunOptions.Add(newRunOption);
            this.SetSelectedRunOption(newRunOption,false,false);
            this.ClearAllCurrentRunResultsForFreshRun();
            this.StatusText="履歴を消去しました";
            StartupDiagnostics.Write("Benchmark run history cleared.");
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run history clear failed.",ex);
            this.StatusText="履歴消去失敗 "+ex.Message;
        }
        finally {
            this.IsRefreshing=false;
            this.RefreshToolbarCommandStates();
        }
    }

    /// <summary>
    /// RestoreRunSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="runNumber">runNumber を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    private void RestoreRunSelection(int? runNumber,int? elementCount) {
        this.isApplyingUiState=true;
        try {
            var option = runNumber is { } number
                ? this.RunOptions.FirstOrDefault(option => option.RunNumber==number)??this.RunOptions.FirstOrDefault()
                : this.RunOptions.FirstOrDefault();
            if(IsNewRunOption(option)) {
                this.SetSelectedRunOptionPreservingDisplay(option,false);
            }
            else {
                this.SetSelectedRunOption(option,false,false);
            }
            this.RebuildResultElementCounts(this.SelectedRunOption?.ElementCounts?? []);
        }
        finally {
            this.isApplyingUiState=false;
            this.RefreshBenchmarkSelectionAggregates();
        }
        if(!IsNewRunOption(this.SelectedRunOption)) {
            this.LoadSelectedRunFromDatabase();
        }
    }

    /// <summary>
    /// SelectRunOptionAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    private async Task SelectRunOptionAsync(BenchmarkRunOption? option) {
        var previous = this.SelectedRunOption;
        if(!this.CanSelectRunOption(option)) {
            this.SetSelectedRunOption(previous,false,false);
            this.OnPropertyChanged(nameof(this.SelectedRunOption));
            return;
        }

        this.DiscardUnsavedBenchmarkResults();

        this.SetSelectedRunOption(option,true,true);
    }

    /// <summary>
    /// CanSelectRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool CanSelectRunOption(BenchmarkRunOption? option) {
        if(IsNewRunOption(option)||!this.HasDisplayResults) {
            return true;
        }

        return IsSavedDatabaseRunOption(option);
    }

    /// <summary>
    /// SetSelectedRunOption メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <param name="load">load を指定して実行条件を制御します。</param>
    /// <param name="saveUiState">saveUiState を指定して実行条件を制御します。</param>
    private void SetSelectedRunOption(BenchmarkRunOption? option,bool load,bool saveUiState) {
        this.isApplyingRunOptionSelection=true;
        try {
            if(!ReferenceEquals(this.selectedRunOption,option)) {
                this.selectedRunOption=option;
                this.OnPropertyChanged(nameof(this.SelectedRunOption));
                // Run 選択が変わったらスナップショットのメモリバッファを破棄し、次回読み出しで DB から取り直す。
                this.selectedRunStateCache=null;
                this.ApplySelectedRunOption(option,load);
                if(saveUiState) {
                    this.SaveUiState();
                }
            }
        }
        finally {
            this.isApplyingRunOptionSelection=false;
        }
    }

    /// <summary>
    /// SetTransientRunOption メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    private void SetTransientRunOption(BenchmarkRunOption option) {
        this.RemoveTransientRunOptions();

        var insertIndex = this.RunOptions.Count>0 ? 1 : 0;
        this.RunOptions.Insert(Math.Min(insertIndex,this.RunOptions.Count),option);
        this.SetSelectedRunOption(option,false,true);
    }

    /// <summary>
    /// RemoveTransientRunOptions メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RemoveTransientRunOptions() {
        for(var index = this.RunOptions.Count-1; index>=0; index--) {
            if(this.RunOptions[index].RunNumber<0) {
                this.RunOptions.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// SelectNewRunOptionForFreshRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void SelectNewRunOptionForFreshRun(bool saveUiState = true) {
        var newRunOption = this.RunOptions.FirstOrDefault(IsNewRunOption);
        if(newRunOption is null) {
            newRunOption=CreateNewRunOption(this.SelectedExecutionProfile.Profile);
            this.RunOptions.Insert(0,newRunOption);
        }
        this.SetSelectedRunOptionPreservingDisplay(newRunOption,saveUiState);
        this.NotifySelectedRunOptionChangedForPicker();
        this.RefreshToolbarCommandStates();
    }

    private BenchmarkRunOption? NormalizeSelectedRunOptionReference() {
        var selected = this.selectedRunOption;
        if(selected is null) {
            return null;
        }

        var matchingOption = this.RunOptions.ToArray().FirstOrDefault(option => option is not null
            &&option.UiRunId==selected.UiRunId
            &&option.RunNumber==selected.RunNumber);
        if(matchingOption is not null&&!ReferenceEquals(matchingOption,selected)) {
            this.selectedRunOption=matchingOption;
        }

        return this.selectedRunOption;
    }

    private void NotifySelectedRunOptionChangedForPicker() {
        this.OnPropertyChanged(nameof(this.SelectedRunOption));
        this.uiDispatcher.BeginInvoke(() => this.OnPropertyChanged(nameof(this.SelectedRunOption)));
    }

    /// <summary>
    /// ApplySelectedRunOption メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <param name="load">load を指定して実行条件を制御します。</param>
    private void ApplySelectedRunOption(BenchmarkRunOption? option,bool load) {
        if(IsNewRunOption(option)) {
            if(!this.IsBenchmarkRunActive&&this.currentRunSession.HasAnyResults) {
                this.currentRunSession.Clear();
            }
            if(this.RestoreCurrentRunDisplayForSelectedTab()) {
                return;
            }
            this.ClearSelectedRunDisplay();
            this.loadedExternalState=null;
            return;
        }

        this.RebuildResultElementCounts(option?.ElementCounts?? []);
        if(load&&!IsTransientRunOption(option)) {
            if(option?.UiRunId>0) {
                this.LoadSelectedRunFromDatabase(option);
            }
            else {
                this.LoadSelectedRunFromDatabase();
            }
        }
    }

    /// <summary>
    /// CaptureCurrentBenchmarkSelections メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void CaptureCurrentBenchmarkSelections() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            this.benchmarkRowSelections[CreateRowSelectionKey(row.SourceTab,row.RowKey)]=row.IsSelected;
        }
    }

    private void ApplyStoredBenchmarkSelection(BenchmarkTableRow row) {
        var rowKeys = this.GetBenchmarkSelectionRowKeys(row);
        var rowSelected = rowKeys.Any(key => this.benchmarkRowSelections.GetValueOrDefault(
            CreateRowSelectionKey(row.SourceTab,key),
            false));
        ApplyRowSelectionToCells(row,rowSelected);
    }

    private IReadOnlyList<string> GetBenchmarkSelectionRowKeys(BenchmarkTableRow row) {
        var keys = new List<string> { row.RowKey };
        var groupedRowKey = BenchmarkRowGroupingLogic.CreateRowKey(row.SourceTab,row.MethodName,row.CasesByProject);
        if(!keys.Contains(groupedRowKey,StringComparer.OrdinalIgnoreCase)) {
            keys.Add(groupedRowKey);
        }
        var displayKey = CreateSnapshotRowKey(
            row.BenchmarkTypeText,
            row.MethodName,
            row.CasesByProject.Values.FirstOrDefault()?.ArgumentKey??string.Empty);
        if(!keys.Contains(displayKey,StringComparer.OrdinalIgnoreCase)) {
            keys.Add(displayKey);
        }
        return keys;
    }

    private void RefreshSelectedRunStateSelectionCache() {
        var selectedRunId = this.SelectedRunOption?.UiRunId??0;
        if(selectedRunId<=0
            ||this.selectedRunStateCache is not { } cache
            ||cache.UiRunId!=selectedRunId
            ||cache.State is null) {
            return;
        }

        cache.State.BenchmarkSelection.ProjectCellSelections.Clear();
        cache.State.BenchmarkSelection.RowSelections.Clear();
        foreach(var item in this.benchmarkRowSelections) {
            cache.State.BenchmarkSelection.RowSelections[item.Key]=item.Value;
        }
    }

    /// <summary>
    /// RefreshCurrentRunBenchmarkSelectionSnapshot メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshCurrentRunBenchmarkSelectionSnapshot() {
        Volatile.Write(
            ref this.currentRunSelectedBenchmarkCases,
            this.CreateSelectedBenchmarkCasesFromAllTabs());
    }

    /// <summary>
    /// GetCurrentRunSelectedBenchmarkCases メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkCase> GetCurrentRunSelectedBenchmarkCases() =>
        Volatile.Read(ref this.currentRunSelectedBenchmarkCases);

    private static IReadOnlySet<string> CreateSelectedCaseKeySet(IEnumerable<BenchmarkCase> selected) =>
        selected
            .Select(static benchmark => string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<BenchmarkGridSnapshotItem> CreateBenchmarkGridSnapshotItems() {
        var results = this.GetAllGridResultItems();
        var items = new List<BenchmarkGridSnapshotItem>();
        var order = 0;
        foreach(var row in this.GetAllBenchmarkRows()) {
            if(this.CreateBenchmarkGridSnapshotItem(row,order,results) is { } item) {
                items.Add(item);
                order++;
            }
        }

        return items;
    }

    private BenchmarkGridSnapshotItem? CreateBenchmarkGridSnapshotItem(BenchmarkTableRow row,int order,IReadOnlyList<BenchmarkResultItem> results) {
        row.CasesByProject.TryGetValue(row.SourceTab.LeftProject,out var leftBenchmark);
        row.CasesByProject.TryGetValue(row.SourceTab.RightProject,out var rightBenchmark);
        if(leftBenchmark is null&&rightBenchmark is null) {
            return null;
        }

        var leftResult = leftBenchmark is null ? null : this.FindSnapshotResult(row,leftBenchmark,results);
        var rightResult = rightBenchmark is null ? null : this.FindSnapshotResult(row,rightBenchmark,results);
        var leftCpu = leftResult?.AssignedLogicalCpu??ParseCpuNumber(row.LeftCell.LogicalProcessorText);
        var rightCpu = rightResult?.AssignedLogicalCpu??ParseCpuNumber(row.RightCell.LogicalProcessorText);
        if(leftCpu is { } leftCpuValue&&rightCpu is { } rightCpuValue&&leftCpuValue!=rightCpuValue) {
            StartupDiagnostics.Write(
                "Benchmark snapshot CPU mismatch. Run snapshot stores row common CPU."
                +" SourceTab="+row.SourceTab.Id
                +" Type="+row.BenchmarkTypeText
                +" Benchmark="+row.MethodName
                +" LeftCPU="+leftCpuValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" RightCPU="+rightCpuValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var cpuNumber = leftCpu??rightCpu;
        var typeName = ResolveSnapshotTypeName(row,leftBenchmark,rightBenchmark);
        if(string.IsNullOrWhiteSpace(typeName)) {
            StartupDiagnostics.Write(
                "Benchmark snapshot type display is empty."
                +" SourceTab="+row.SourceTab.Id
                +" Benchmark="+row.MethodName);
        }

        var leftSide = this.CreateBenchmarkGridSnapshotSide(row,row.LeftCell,leftBenchmark,leftResult);
        var rightSide = this.CreateBenchmarkGridSnapshotSide(row,row.RightCell,rightBenchmark,rightResult);

        return new BenchmarkGridSnapshotItem(
            0,
            order,
            row.SourceTab.Id,
            typeName,
            row.MethodName,
            cpuNumber,
            row.IsSelected,
            leftSide.Status==UiRunStatus.Completed ? leftSide : this.CreateEmptyBenchmarkGridSnapshotSide(row,row.LeftCell,leftBenchmark),
            rightSide.Status==UiRunStatus.Completed ? rightSide : this.CreateEmptyBenchmarkGridSnapshotSide(row,row.RightCell,rightBenchmark));
    }

    private static string ResolveSnapshotTypeName(
        BenchmarkTableRow row,
        BenchmarkCase? leftBenchmark,
        BenchmarkCase? rightBenchmark) {
        if(!string.IsNullOrWhiteSpace(row.BenchmarkTypeText)) {
            return row.BenchmarkTypeText;
        }

        var typeName = ScalableBenchmarkNameFormatter.CreateGenericTypeText(leftBenchmark?.TypeFullName??string.Empty);
        if(!string.IsNullOrWhiteSpace(typeName)) {
            return typeName;
        }

        return ScalableBenchmarkNameFormatter.CreateGenericTypeText(rightBenchmark?.TypeFullName??string.Empty);
    }

    private BenchmarkGridSnapshotSide CreateBenchmarkGridSnapshotSide(
        BenchmarkTableRow row,
        BenchmarkProjectCell cell,
        BenchmarkCase? benchmark,
        BenchmarkResultItem? result) {
        var status = this.ResolveSnapshotStatus(cell,result);
        var measuredAt = result?.StartedAt??cell.LastMeasuredAt??cell.LastFailedAt;
        var elementCount = result is { ElementCount: >0 } ? result.ElementCount : cell.LastSuccessfulElementCount??cell.LastFailedElementCount;
        var meanNanoseconds = result?.MeanNanoseconds??cell.Metrics.TimeNanoseconds;
        var allocatedBytes = result?.AllocatedBytesValue??cell.Metrics.AllocatedBytes;
        var resultItem = result??CreateSnapshotResultItem(
            row.SourceTab,
            cell.Project,
            benchmark?.AssemblyName??row.SourceTab.GetAssemblyName(cell.Project),
            benchmark?.TypeFullName??string.Empty,
            benchmark?.MethodName??row.MethodName,
            benchmark?.ArgumentKey??string.Empty,
            benchmark?.Arguments??[],
            status,
            elementCount,
            ParseCpuNumber(cell.LogicalProcessorText),
            measuredAt,
            meanNanoseconds,
            allocatedBytes,
            result?.FailureReason??string.Empty);
        return new(
            status,
            result?.FailureReason??string.Empty,
            measuredAt,
            meanNanoseconds,
            allocatedBytes,
            resultItem,
            benchmark);
    }

    private BenchmarkGridSnapshotSide CreateEmptyBenchmarkGridSnapshotSide(
        BenchmarkTableRow row,
        BenchmarkProjectCell cell,
        BenchmarkCase? benchmark) {
        var resultItem = CreateSnapshotResultItem(
            row.SourceTab,
            cell.Project,
            benchmark?.AssemblyName??row.SourceTab.GetAssemblyName(cell.Project),
            benchmark?.TypeFullName??string.Empty,
            benchmark?.MethodName??row.MethodName,
            benchmark?.ArgumentKey??string.Empty,
            benchmark?.Arguments??[],
            UiRunStatus.Queued,
            BenchmarkElementCounts.BaseElementCount,
            null,
            null,
            null,
            null,
            string.Empty);
        return new(UiRunStatus.Queued,string.Empty,null,null,null,resultItem,benchmark);
    }

    private BenchmarkResultItem? FindSnapshotResult(BenchmarkTableRow row,BenchmarkCase benchmark,IReadOnlyList<BenchmarkResultItem> results) {
        for(var index = results.Count-1;index>=0;index--) {
            var result = results[index];
            if(result.SourceTab==row.SourceTab.Tab
                &&string.Equals(result.Project,benchmark.Project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Method,benchmark.MethodName,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(result.TypeFullName,benchmark.TypeFullName)
                &&IsSameArgument(result.ArgumentKey,benchmark.ArgumentKey)
                &&IsTerminalResult(result)) {
                return result;
            }
        }

        for(var index = results.Count-1;index>=0;index--) {
            var result = results[index];
            if(result.SourceTab==row.SourceTab.Tab
                &&string.Equals(result.Project,benchmark.Project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Method,benchmark.MethodName,StringComparison.OrdinalIgnoreCase)
                &&IsTerminalResult(result)) {
                return result;
            }
        }

        return null;
    }

    private UiRunStatus ResolveSnapshotStatus(BenchmarkProjectCell cell,BenchmarkResultItem? result) {
        if(result is not null) {
            if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)) {
                return UiRunStatus.Completed;
            }
            if(string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)) {
                return UiRunStatus.Failed;
            }
        }
        if(cell.Metrics.HasMeasuredValue) {
            return UiRunStatus.Completed;
        }
        if(string.Equals(cell.StatusImageSource,BenchmarkStatusImageNames.Failed,StringComparison.OrdinalIgnoreCase)) {
            return UiRunStatus.Failed;
        }

        return UiRunStatus.Queued;
    }

    private static int? ParseCpuNumber(string value) =>
        int.TryParse(value,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var parsed) ? parsed : null;

    private static string CreateArgumentDisplay(IReadOnlyList<BenchmarkArgument> arguments) =>
        arguments.Count==0
            ? string.Empty
            : string.Join(",",arguments.OrderBy(static item => item.Name,StringComparer.OrdinalIgnoreCase).Select(static item => item.Name+"="+item.Value));

    private static string CreateSnapshotSide(BenchmarkSourceTabDefinition sourceTab,string project) {
        if(string.Equals(project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)) {
            return "Left";
        }
        if(string.Equals(project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase)) {
            return "Right";
        }

        return string.Empty;
    }

    private static string CreateSnapshotPairDiscriminator(BenchmarkSourceTabDefinition sourceTab,string typeFullName) {
        if(!sourceTab.Id.StartsWith("Scalable",StringComparison.OrdinalIgnoreCase)) {
            return string.Empty;
        }

        var lastDot = typeFullName.LastIndexOf('.');
        return lastDot>=0&&lastDot<typeFullName.Length-1 ? typeFullName[(lastDot+1)..] : typeFullName;
    }

    private static BenchmarkResultItem CreateSnapshotResultItem(
        BenchmarkSourceTabDefinition sourceTab,
        string project,
        string assemblyName,
        string typeFullName,
        string methodName,
        string argumentKey,
        IReadOnlyList<BenchmarkArgument> arguments,
        UiRunStatus status,
        int? elementCount,
        int? cpuNumber,
        DateTimeOffset? measuredAt,
        double? meanNanoseconds,
        long? allocatedBytes,
        string failureReason) {
        return new BenchmarkResultItem(new BenchmarkJob{
            Project=project,
            AssemblyName=assemblyName,
            TypeFullName=typeFullName,
            MethodName=methodName,
            ArgumentKey=argumentKey,
            Arguments=arguments.ToList(),
            SourceTab=sourceTab.Tab,
            ElementCount=elementCount??0,
            LoopCount=BenchmarkWorkloadAxis.DefaultLoopCount,
            Status=ToJobStatusForResume(status),
            Mean=meanNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            MeanNanoseconds=meanNanoseconds,
            AllocatedBytes=allocatedBytes,
            StartedAt=measuredAt,
            FinishedAt=measuredAt,
            AssignedLogicalCpu=cpuNumber,
            FailureReason=failureReason,
        });
    }

    private IEnumerable<BenchmarkCase> FilterBenchmarkCasesWithoutSuccessfulResults(
        IReadOnlyList<BenchmarkCase> selected,
        IReadOnlyList<int> elementCounts) {
        foreach(var benchmark in selected) {
            if(!this.HasSuccessfulResultForAllSelectedAxes(benchmark,elementCounts)) {
                yield return benchmark;
            }
        }
    }

    private bool HasSuccessfulResultForAllSelectedAxes(BenchmarkCase benchmark,IReadOnlyList<int> elementCounts) {
        if(elementCounts.Count==0) {
            return false;
        }

        var results = this.GetAllGridResultItems();
        foreach(var elementCount in elementCounts) {
            var hasSuccess = results.Any(result =>
                string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null
                &&IsResultMatch(result,benchmark.MethodName,benchmark.Project,benchmark.TypeFullName,elementCount,benchmark.ArgumentKey));
            if(!hasSuccess) {
                return false;
            }
        }

        return true;
    }

    // 選択スナップショットとキュー生成の規則は UI.Core の BenchmarkSelectionQueueLogic に集約する。
    // 表示中タブだけの生きた状態(チェックボックス)を優先する。
    /// <summary>
    /// CreateSelectedBenchmarkCasesFromAllTabs メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkCase> CreateSelectedBenchmarkCasesFromAllTabs() {
        this.CaptureCurrentBenchmarkSelections();
        return this.SourceTabs
            .SelectMany(option => this.CreateSelectedBenchmarkCases(option.Definition))
            .GroupBy(
                static benchmark => string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey),
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private IEnumerable<BenchmarkCase> CreateSelectedBenchmarkCases(BenchmarkSourceTabDefinition sourceTab) {
        var state = this.GetSourceTabViewState(sourceTab.Tab);
        if(state.Rows.Count>0) {
            return state.Rows
                .SelectMany((row,index) => row.IsSelected
                    ? row.GetRunCases().Select(benchmark => new BenchmarkCase(
                        benchmark.Project,
                        benchmark.AssemblyName,
                        benchmark.TypeFullName,
                        benchmark.MethodName,
                        benchmark.ArgumentKey,
                        benchmark.Arguments){
                        BindingRowIndex=index,
                    })
                    : []);
        }

        return BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,sourceTab)
            .Where(row => row.CasesByProject.Count>0
                &&this.benchmarkRowSelections.TryGetValue(CreateRowSelectionKey(sourceTab,row.RowKey),out var selected)
                &&selected)
            .SelectMany(row => sourceTab.ProjectOrder
                .Where(project => row.CasesByProject.ContainsKey(project))
                .Select(project => row.CasesByProject[project]));
    }

    /// <summary>
    /// CreateRowSelectionKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="rowKey">rowKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateRowSelectionKey(BenchmarkSourceTabDefinition sourceTab,string rowKey) =>
        BenchmarkSelectionQueueLogic.CreateRowSelectionKey(sourceTab,rowKey);

    private static string CreateAllBenchmarkSelectionSnapshotKey(BenchmarkSourceTabDefinition? sourceTab) =>
        sourceTab is { } tab ? tab.Id : "*";

    /// <summary>
    /// SaveUiState メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void SaveUiState() {
        this.CaptureCurrentBenchmarkSelections();
        this.RefreshSelectedRunStateSelectionCache();
        if(this.IsBenchmarkRunActive) {
            this.RefreshCurrentRunBenchmarkSelectionSnapshot();
        }

        if(this.activeUiRunId is { } runId) {
            var snapshot = this.CreateBenchmarkViewStateSnapshot();
            this.uiRunRepository.QueueSaveUiState(runId,snapshot);
        }
        this.HasPendingCloseStateSave=false;
        if(!this.isApplyingUiState) {
            this.RefreshBenchmarkSelectionDisplay();
        }
    }

    /// <summary>
    /// DiscardPendingCloseStateSave メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void DiscardPendingCloseStateSave() {
        this.HasPendingCloseStateSave=false;
    }

    /// <summary>
    /// RefreshBenchmarkSelectionDisplay メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkSelectionDisplay() {
        if(this.isRefreshingBenchmarkSelectionDisplay||this.BenchmarkRows.Count==0) {
            return;
        }

        if(IsSavedDatabaseRunOption(this.SelectedRunOption)&&this.ResultItems.Count>0&&!this.IsBenchmarkRunActive) {
            this.RefreshVisibleResults();
            this.RefreshSourceTabProgresses();
            this.UpdateChart(false);
            return;
        }

        this.isRefreshingBenchmarkSelectionDisplay=true;
        try {
            this.RefreshVisibleResults();
            this.RefreshComparisonRowsFromDisplayResults();
            this.ApplyComparisonRowsToTable();
            this.ApplySelectedDisplayResultsToTable();
            if(this.SelectedResultElementCount is { } selectedElementCount) {
                this.ApplySkippedCells(selectedElementCount.Value);
            }
            this.RefreshFinalCompletionStates();
            this.UpdateChart(false);
            this.RefreshSourceTabProgresses();
        }
        finally {
            this.isRefreshingBenchmarkSelectionDisplay=false;
        }
    }

    /// <summary>
    /// RefreshComparisonRowsFromDisplayResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshComparisonRowsFromDisplayResults() {
        var selectedElementCount = this.SelectedResultElementCount?.Value;
        if(this.ShouldUseCurrentRunMemoryForDisplay(this.SelectedSourceDefinition)) {
            this.comparisonRows=selectedElementCount is { } currentElementCount
                ? this.BuildCurrentRunComparisonRowsForElementCount(currentElementCount)
                : this.BuildCurrentRunComparisonRows();
            return;
        }

        if(this.ResultItems.Count>0) {
            this.comparisonRows=selectedElementCount is { } displayedElementCount
                ? this.BuildDisplayedRowsForElementCount(this.SelectedSourceDefinition,displayedElementCount)
                : this.BuildDisplayedRows(this.SelectedSourceDefinition);
            return;
        }

        this.comparisonRows= [];
    }

    /// <summary>
    /// comparisonRowsRequestToken フィールドです。
    /// </summary>
    private int comparisonRowsRequestToken;
    /// <summary>
    /// comparisonRowsRefreshInFlight フィールドです。
    /// </summary>
    private bool comparisonRowsRefreshInFlight;
    /// <summary>
    /// comparisonRowsRefreshPending フィールドです。
    /// </summary>
    private bool comparisonRowsRefreshPending;

    /// <summary>
    /// ComparisonRowsTabInput メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="State">State を指定して実行条件を制御します。</param>
    /// <param name="CurrentRunResults">CurrentRunResults を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct ComparisonRowsTabInput(
        BenchmarkSourceTabViewState State,
        IReadOnlyList<BenchmarkResultItem> CurrentRunResults);

    // 全5タブ分の比較行再構築はResultItems全体のLINQ走査+DB参照(uiRunRepository.FindPreviousResult)
    // を伴い重い。currentRunSession/ResultItemsに触れる部分だけをUIスレッドで同期的にスナップショット
    // し、残りの純粋計算(フィルタ・整形・DB参照)はバックグラウンドスレッドで行い、
    // state.ComparisonRows(ObservableCollectionではない単純プロパティ)への代入だけをUIスレッドに戻す。
    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void RefreshAllComparisonRowsFromDisplayResults() {
        if(this.comparisonRowsRefreshInFlight) {
            this.comparisonRowsRefreshPending=true;
            return;
        }

        var selectedElementCount = this.SelectedResultElementCount?.Value;
        var tabInputs = this.sourceTabViewStates
            .Select(state => new ComparisonRowsTabInput(state,this.GetCurrentRunResultItems(state.Definition.Tab)))
            .ToArray();
        var resultItemsSnapshot = this.ResultItems.ToArray();
        var token = ++this.comparisonRowsRequestToken;
        var generation = this.runResultGeneration;
        this.comparisonRowsRefreshInFlight=true;
        var stopwatch = Stopwatch.StartNew();
        Task.Run(() => tabInputs
                .Select(input => this.ComputeComparisonRowsForTab(input,resultItemsSnapshot,selectedElementCount))
                .ToArray())
            .ContinueWith(task => {
                UiThreadBudget.Record("bg:comparison",stopwatch.Elapsed.TotalMilliseconds);
                this.uiDispatcher.BeginInvoke(() => {
                    this.comparisonRowsRefreshInFlight=false;
                    if(token==this.comparisonRowsRequestToken&&generation==this.runResultGeneration&&!task.IsFaulted) {
                        var computed = task.Result;
                        UiThreadBudget.Measure("comparison-apply",() => {
                            for(var index = 0; index<tabInputs.Length; index++) {
                                tabInputs[index].State.ComparisonRows=computed[index];
                                this.ベンチマークシェア概要を更新(tabInputs[index].State);
                            }
                            // 新しいComparisonRowsを行/スキップセル/完了状態/チャートへ反映する。もう次のtickが
                            // 来ない(停止中Run閲覧時など)場合でもここで確定させないと表示が更新されないまま残る。
                            this.ApplyComparisonRowsToAllTables();
                            if(selectedElementCount is { } skippedElementCount) {
                                this.ApplySkippedCells(skippedElementCount);
                            }
                            this.RefreshFinalCompletionStates();
                            this.UpdateChart();
                        });
                    }
                    else if(task.IsFaulted) {
                        StartupDiagnostics.Write("Background RefreshAllComparisonRowsFromDisplayResults failed.",task.Exception);
                    }

                    if(this.comparisonRowsRefreshPending) {
                        this.comparisonRowsRefreshPending=false;
                        this.RefreshAllComparisonRowsFromDisplayResults();
                    }
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// ComputeComparisonRowsForTab メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="input">input を指定して実行条件を制御します。</param>
    /// <param name="resultItemsSnapshot">resultItemsSnapshot を指定して実行条件を制御します。</param>
    /// <param name="selectedElementCount">selectedElementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> ComputeComparisonRowsForTab(
        ComparisonRowsTabInput input,
        IReadOnlyList<BenchmarkResultItem> resultItemsSnapshot,
        int? selectedElementCount) {
        if(input.CurrentRunResults.Count>0&&!IsSavedDatabaseRunOption(this.SelectedRunOption)) {
            return selectedElementCount is { } currentElementCount
                ? this.runProjectionService.BuildComparisonRowsForElementCount(input.CurrentRunResults,input.State.Definition,currentElementCount,this.CreateReportRowsForComparison)
                : this.runProjectionService.BuildLatestComparisonRows(input.CurrentRunResults,input.State.Definition,this.CreateReportRowsForComparison);
        }

        if(resultItemsSnapshot.Count>0) {
            return this.runProjectionService.BuildDisplayedRowsForSelectedElementCount(resultItemsSnapshot,input.State.Definition,selectedElementCount,this.CreateReportRowsForComparison);
        }

        return [];
    }

    private void RefreshAllComparisonRowsFromLoadedResultItems(IReadOnlyList<BenchmarkResultItem> resultItemsSnapshot) {
        var selectedElementCount = this.SelectedResultElementCount?.Value;
        foreach(var state in this.sourceTabViewStates) {
            state.ComparisonRows=this.runProjectionService.BuildDisplayedRowsForSelectedElementCount(
                resultItemsSnapshot,
                state.Definition,
                selectedElementCount,
                this.CreateReportRowsForComparison);
            this.ベンチマークシェア概要を更新(state);
        }

        this.comparisonRows=this.SelectedSourceTabViewState.ComparisonRows;
    }

    /// <summary>
    /// ApplyBenchmarkSelectionsFromProgressState メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    private void ApplyBenchmarkSelectionsFromProgressState(ProgressState? state) {
        if(state is null) {
            return;
        }

        this.isApplyingUiState=true;
        try {
            this.benchmarkRowSelections.Clear();
            foreach(var item in state.BenchmarkSelection.RowSelections) {
                this.benchmarkRowSelections[item.Key]=item.Value;
            }
            foreach(var row in this.GetAllBenchmarkRows()) {
                this.ApplyStoredBenchmarkSelection(row);
            }
        }
        finally {
            this.isApplyingUiState=false;
            this.RefreshBenchmarkSelectionAggregates();
            this.RefreshBenchmarkSelectionDisplay();
        }
    }

    /// <summary>
    /// DiscardUnsavedBenchmarkResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void DiscardUnsavedBenchmarkResults() {
        var discardedRunIds = new HashSet<long>();
        if(this.activeUiRunId is { } activeRunId) {
            discardedRunIds.Add(activeRunId);
        }
        if(this.unsavedUiRunId is { } unsavedRunId) {
            discardedRunIds.Add(unsavedRunId);
        }
        foreach(var discardedRunId in discardedRunIds) {
            this.uiRunRepository.QueueDeleteRun(discardedRunId);
        }
        this.activeUiRunId=null;
        this.unsavedUiRunId=null;
        this.HasUnsavedBenchmarkResults=false;
        this.HasPendingCloseStateSave=false;
        this.currentUnsavedRunRoot=null;
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// GetProgressStateForCurrentUnsavedRun メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState? GetProgressStateForCurrentUnsavedRun() =>
        string.IsNullOrWhiteSpace(this.currentUnsavedRunRoot)
            ? null
            : this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot);

    /// <summary>
    /// GetProgressStateForCurrentDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="targetRoot">targetRoot を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState? GetProgressStateForCurrentDisplay(string targetRoot) {
        if(this.HasUnsavedBenchmarkResults) {
            return this.CreateProgressStateFromAllTabResults(targetRoot);
        }

        if(this.loadedExternalState is not null) {
            return this.loadedExternalState;
        }

        return this.LoadSelectedRunState()
            ??this.CreateProgressStateFromAllTabResults(targetRoot);
    }

    /// <summary>
    /// CreateProgressStateFromDisplayedResults メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState CreateProgressStateFromDisplayedResults(string runRoot) {
        var sourceTab = this.SelectedSourceDefinition;
        var results = this.GetResultSourceForDisplay()
            .Where(result => sourceTab.ContainsProject(result.Project));
        return this.CreateProgressStateFromResults(runRoot,results);
    }

    /// <summary>
    /// CreateProgressStateFromAllTabResults メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState CreateProgressStateFromAllTabResults(string runRoot) {
        var results = this.currentRunSession.HasAnyResults
            ? this.currentRunSession.GetAllResults()
            : this.GetResultSourceForDisplay();
        return this.CreateProgressStateFromResults(runRoot,results);
    }

    /// <summary>
    /// CreateProgressStateFromResults メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <param name="sourceResults">sourceResults を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState CreateProgressStateFromResults(string runRoot,IEnumerable<BenchmarkResultItem> sourceResults) {
        this.CaptureCurrentBenchmarkSelections();
        return BenchmarkRunSession.CreateProgressState(
            runRoot,
            this.SelectedExecutionProfile.Profile,
            this.SelectedSourceDefinition,
            this.GetAdaptiveThreshold(),
            new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase),
            this.benchmarkRowSelections,
            sourceResults,
            result => this.FindBenchmarkCase(result.Method,result.Project,result.TypeFullName,result.ArgumentKey,result.SourceTab));
    }

    /// <summary>
    /// typeFullName プロパティです。
    /// </summary>
    /// <value>typeFullName の値を取得/設定します。</value>
    private BenchmarkCase? FindBenchmarkCase(string method,string project,string typeFullName = "",string argumentKey = "",BenchmarkSourceTab? sourceTab = null) {
        if(this.benchmarkCasesByProjectMethod.TryGetValue(CreateBenchmarkCaseKey(method,project,typeFullName,argumentKey),out var benchmark)) {
            return benchmark;
        }

        if(sourceTab is { } tab&&tab!=this.SelectedSourceDefinition.Tab) {
            return null;
        }

        return this.BenchmarkRows
            .FirstOrDefault(row => row.CasesByProject.TryGetValue(project,out var candidate)
                &&string.Equals(candidate.MethodName,method,StringComparison.OrdinalIgnoreCase)
                &&(string.IsNullOrWhiteSpace(typeFullName)
                    ||string.Equals(candidate.TypeFullName,typeFullName,StringComparison.OrdinalIgnoreCase))
                &&(string.IsNullOrWhiteSpace(argumentKey)
                    ||string.Equals(candidate.ArgumentKey,argumentKey,StringComparison.OrdinalIgnoreCase)))
            ?.CasesByProject
            .GetValueOrDefault(project);
    }

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private static string CreateBenchmarkCaseKey(string method,string project,string typeFullName,string argumentKey = "") =>
        BenchmarkRowGroupingLogic.CreateCaseKey(project,typeFullName,method,argumentKey);

    /// <summary>
    /// ShortTypeName メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string ShortTypeName(string typeFullName) {
        if(string.IsNullOrWhiteSpace(typeFullName)) {
            return string.Empty;
        }

        var index = typeFullName.LastIndexOf('.');
        return index<0||index==typeFullName.Length-1
            ? typeFullName
            : typeFullName[(index+1)..];
    }

    /// <summary>
    /// FindBenchmarkRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkTableRow? FindBenchmarkRow(BenchmarkResultItem result) {
        var state = this.GetSourceTabViewState(result.SourceTab);
        if(result.BindingRowIndex>=0
            &&result.BindingRowIndex<state.Rows.Count
            &&IsResultForRow(state.Rows[result.BindingRowIndex],result)) {
            return state.Rows[result.BindingRowIndex];
        }

        return this.ResolveBenchmarkRowByKey(result.SourceTab,result.Project,result.TypeFullName,result.Method,result.ArgumentKey)
            ??this.BenchmarkRows.FirstOrDefault(candidate => IsResultForRow(candidate,result))
            ??(IsScalableSourceTab(result.SourceTab)||result.SourceTab!=this.SelectedSourceDefinition.Tab ? null : this.BenchmarkRows.FirstOrDefault(candidate => string.Equals(candidate.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
                &&candidate.GetProjectCell(result.Project) is { HasCase: true }))
            ??(IsScalableSourceTab(this.SelectedSourceDefinition.Tab)||!this.SelectedSourceDefinition.ContainsProject(result.Project) ? null : this.BenchmarkRows.FirstOrDefault(candidate => string.Equals(candidate.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
                &&candidate.GetProjectCell(result.Project) is { HasCase: true }));
    }

    /// <summary>
    /// FindBenchmarkRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="job">job を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkTableRow? FindBenchmarkRow(BenchmarkJob job) {
        var state = this.GetSourceTabViewState(job.SourceTab);
        if(job.BindingRowIndex>=0
            &&job.BindingRowIndex<state.Rows.Count
            &&IsJobForRow(state.Rows[job.BindingRowIndex],job)) {
            return state.Rows[job.BindingRowIndex];
        }

        return this.ResolveBenchmarkRowByKey(job.SourceTab,job.Project,job.TypeFullName,job.MethodName,job.ArgumentKey)
            ??this.BenchmarkRows.FirstOrDefault(candidate => IsJobForRow(candidate,job))
            ??(IsScalableSourceTab(job.SourceTab)||job.SourceTab!=this.SelectedSourceDefinition.Tab ? null : this.BenchmarkRows.FirstOrDefault(candidate => string.Equals(candidate.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
                &&candidate.GetProjectCell(job.Project) is { HasCase: true }))
            ??(IsScalableSourceTab(this.SelectedSourceDefinition.Tab)||!this.SelectedSourceDefinition.ContainsProject(job.Project) ? null : this.BenchmarkRows.FirstOrDefault(candidate => string.Equals(candidate.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
                &&candidate.GetProjectCell(job.Project) is { HasCase: true }));
    }

    /// <summary>
    /// ResolveBenchmarkRowByKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkTableRow? ResolveBenchmarkRowByKey(BenchmarkSourceTab sourceTab,string project,string typeFullName,string method,string argumentKey) =>
        this.TryFindBenchmarkRow(sourceTab,project,typeFullName,method,argumentKey,out var row) ? row : null;

    /// <summary>
    /// TryFindBenchmarkRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool TryFindBenchmarkRow(BenchmarkSourceTab sourceTab,string project,string typeFullName,string method,string argumentKey,out BenchmarkTableRow row) {
        if(this.benchmarkRowsByResultKey.TryGetValue(CreateBenchmarkRowResultIndexKey(sourceTab,project,typeFullName,method,argumentKey),out row!)) {
            return true;
        }

        if(!IsScalableSourceTab(sourceTab)
            &&this.benchmarkRowsByResultKey.TryGetValue(CreateBenchmarkRowResultIndexKey(sourceTab,project,string.Empty,method,string.Empty),out row!)) {
            return true;
        }

        return this.benchmarkRowsByProjectMethod.TryGetValue(CreateBenchmarkRowMethodProjectIndexKey(sourceTab,project,method),out row!);
    }

    /// <summary>
    /// CreateBenchmarkRowResultIndexKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateBenchmarkRowResultIndexKey(BenchmarkSourceTab sourceTab,string project,string typeFullName,string method,string argumentKey) =>
        string.Join("|",sourceTab,project,typeFullName,method,argumentKey);

    /// <summary>
    /// CreateBenchmarkRowMethodProjectIndexKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateBenchmarkRowMethodProjectIndexKey(BenchmarkSourceTab sourceTab,string project,string method) =>
        string.Join("|",sourceTab,project,method);

    /// <summary>
    /// IsResultForRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsResultForRow(BenchmarkTableRow row,BenchmarkResultItem result) {
        if(row.CasesByProject.TryGetValue(result.Project,out var benchmark)) {
            return string.Equals(benchmark.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
                &&IsMatchingTypeForRow(row,result.TypeFullName,benchmark.TypeFullName)
                &&IsMatchingArgumentForRow(row.SourceTab.Tab,benchmark.ArgumentKey,result.ArgumentKey);
        }

        return false;
    }

    /// <summary>
    /// IsJobForRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="job">job を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsJobForRow(BenchmarkTableRow row,BenchmarkJob job) {
        if(row.CasesByProject.TryGetValue(job.Project,out var benchmark)) {
            return string.Equals(benchmark.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
                &&IsMatchingTypeForRow(row,job.TypeFullName,benchmark.TypeFullName)
                &&IsMatchingArgumentForRow(row.SourceTab.Tab,benchmark.ArgumentKey,job.ArgumentKey);
        }

        return false;
    }

    /// <summary>
    /// IsMatchingTypeForRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="actualTypeFullName">actualTypeFullName を指定して実行条件を制御します。</param>
    /// <param name="expectedTypeFullName">expectedTypeFullName を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsMatchingTypeForRow(BenchmarkTableRow row,string actualTypeFullName,string expectedTypeFullName) {
        if(IsScalableSourceTab(row.SourceTab.Tab)) {
            return !string.IsNullOrWhiteSpace(actualTypeFullName)
                &&string.Equals(expectedTypeFullName,actualTypeFullName,StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(actualTypeFullName)
            ||string.Equals(expectedTypeFullName,actualTypeFullName,StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// IsMatchingArgumentForRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="expectedArgumentKey">expectedArgumentKey を指定して実行条件を制御します。</param>
    /// <param name="actualArgumentKey">actualArgumentKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsMatchingArgumentForRow(BenchmarkSourceTab sourceTab,string expectedArgumentKey,string actualArgumentKey) =>
        !IsScalableSourceTab(sourceTab)
        ||string.Equals(expectedArgumentKey,actualArgumentKey,StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// IsScalableSourceTab メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsScalableSourceTab(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalableSet
            or BenchmarkSourceTab.ScalableDictionary
            or BenchmarkSourceTab.ScalablePersistedList
            or BenchmarkSourceTab.ScalablePersistedSet
            or BenchmarkSourceTab.ScalableTsavoriteSet
            or BenchmarkSourceTab.ScalablePersistedDictionary
            or BenchmarkSourceTab.ScalableTsavoriteDictionary;

    /// <summary>
    /// CreateUnsavedRunRoot メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="executionProfile">executionProfile を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string CreateUnsavedRunRoot(BenchmarkExecutionProfile executionProfile) {
        var profileRoot = this.GetBenchmarkRoot(executionProfile);
        var root = Path.Combine(profileRoot,".unsaved",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// LoadExternalRows メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="displayName">displayName を指定して実行条件を制御します。</param>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="rowsByElementCount">rowsByElementCount を指定して実行条件を制御します。</param>
    /// <param name="rowsWithoutElementCount">rowsWithoutElementCount を指定して実行条件を制御します。</param>
    /// <param name="selectedElementCount">selectedElementCount を指定して実行条件を制御します。</param>
    private void LoadExternalRows(
        string displayName,
        string runRoot,
        ProgressState? state,
        IReadOnlyDictionary<int,IReadOnlyList<BenchmarkComparisonRow>> rowsByElementCount,
        IReadOnlyList<BenchmarkComparisonRow> rowsWithoutElementCount,
        int selectedElementCount) {
        this.ClearBenchmarkResultDisplays();
        this.currentRunSession.ClearSourceTab(this.SelectedSourceDefinition.Tab);
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.comparisonRows= [];
        foreach(var item in rowsByElementCount.OrderBy(static item => item.Key)) {
            this.AddComparisonRowsIntoResults(item.Value,item.Key,state);
        }
        if(rowsWithoutElementCount.Count>0) {
            this.AddComparisonRowsIntoResults(rowsWithoutElementCount,0,state);
        }

        var elementCounts = rowsByElementCount.Keys
            .Where(static item => item>0)
            .Order()
            .ToArray();
        this.RebuildResultElementCounts(elementCounts);
        if(selectedElementCount>0) {
            this.SelectResultElementCountWithoutReload(selectedElementCount);
            this.comparisonRows=rowsByElementCount.TryGetValue(selectedElementCount,out var selectedRows)
                ? selectedRows
                : [];
        }
        else {
            this.SelectedResultElementCount=null;
            this.CurrentElementCountText="-";
            this.comparisonRows=rowsWithoutElementCount;
        }
        if(state is not null&&selectedElementCount>0) {
            this.ApplySelectedRunStateJobs(state,selectedElementCount);
        }

        this.RefreshVisibleResults();
        this.ApplyCurrentSort();
        this.ApplyComparisonRowsToTable();
        this.UpdateChart(false);
        this.SelectedTab=BenchmarkTab.Chart;
        this.loadedExternalState=state??this.CreateProgressStateFromDisplayedResults(runRoot);
        this.SetTransientRunOption(this.CreateExternalRunOption(displayName,runRoot,elementCounts));
        this.HasUnsavedBenchmarkResults=false;
        this.HasPendingCloseStateSave=false;
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// RunOrResumeAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task RunOrResumeAsync() {
        if(!this.CanRunBenchmark) {
            return;
        }

        if(Interlocked.Exchange(ref this.runCommandGate,1)==1) {
            return;
        }

        try {
            if(this.IsBenchmarkPaused&&this.activeRunRoot is not null) {
                await this.RunAsync(this.activeRunRoot,true);
                return;
            }
            if(this.TryPrepareSelectedDatabaseRunResume(out var runRoot)) {
                await this.RunAsync(runRoot,true);
                return;
            }

            await this.RunAsync(null,this.currentRunSession.HasAnyResults);
        }
        finally {
            if(!this.IsBenchmarkRunActive) {
                Interlocked.Exchange(ref this.runCommandGate,0);
            }
        }
    }

#if DEBUG
    /// <summary>
    /// RunInfiniteAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task RunInfiniteAsync() {
        if(!this.CanRunBenchmark) {
            return;
        }

        if(Interlocked.Exchange(ref this.runCommandGate,1)==1) {
            return;
        }

        try {
            if(this.IsBenchmarkPaused&&this.activeRunRoot is not null) {
                await this.RunAsync(this.activeRunRoot,true,true);
                return;
            }

            await this.RunAsync(null,true,true);
        }
        finally {
            if(!this.IsBenchmarkRunActive) {
                Interlocked.Exchange(ref this.runCommandGate,0);
            }
        }
    }
#endif

    /// <summary>
    /// TryPrepareSelectedDatabaseRunResume メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool TryPrepareSelectedDatabaseRunResume(out string runRoot) {
        runRoot=string.Empty;
        var option = this.SelectedRunOption;
        if(option is null||option.UiRunId<=0) {
            return false;
        }

        var run = this.uiRunStore.ListRuns(GetUiRunAppKind()).FirstOrDefault(item => item.RunId==option.UiRunId);
        if(run is null||run.Status is not (UiRunStatus.Paused or UiRunStatus.Running or UiRunStatus.Queued)) {
            return false;
        }

        var state = this.CreateProgressStateFromDatabaseRun(option.UiRunId,run);
        if(state is null||string.IsNullOrWhiteSpace(state.RunRoot)) {
            return false;
        }

        new BenchmarkStateManager(this.GetBenchmarkRoot(option.ExecutionProfile)).Save(state);
        this.activeUiRunId=option.UiRunId;
        this.SetActiveRunRoot(state.RunRoot);
        runRoot=state.RunRoot;
        return true;
    }

    /// <summary>
    /// useInfiniteTestRunner プロパティです。
    /// </summary>
    /// <value>useInfiniteTestRunner の値を取得/設定します。</value>
    private async Task RunAsync(string? runRootOverride,bool overwriteExistingResults,bool useInfiniteTestRunner = false) {
        var startsFromSavedDatabaseRun = IsSavedDatabaseRunOption(this.SelectedRunOption);
        if(startsFromSavedDatabaseRun) {
            this.RestoreCatalogRowsForFreshRunFromSavedDatabaseRun();
        }
        this.RefreshCurrentRunBenchmarkSelectionSnapshot();
        var allSelected = this.GetCurrentRunSelectedBenchmarkCases().ToArray();
        var selected = overwriteExistingResults
            ? allSelected
            : this.FilterBenchmarkCasesWithoutSuccessfulResults(allSelected,this.GetSelectedElementCounts()).ToArray();
        if(selected.Length==0) {
            this.StatusText=allSelected.Length==0
                ? "ベンチマークが選択されていません"
                : "計測対象はすべて成功済みです";
            StartupDiagnostics.Write("Benchmark run aborted: no benchmark selected or all selected benchmarks already have successful results.");
            return;
        }

        var executionProfile = useInfiniteTestRunner ? BenchmarkExecutionProfile.Test : this.SelectedExecutionProfile.Profile;
        var isResumeRun = runRootOverride is not null;
        this.RemoveTransientRunOptions();
        if(!isResumeRun) {
            this.DiscardUnsavedBenchmarkResults();
            this.SelectNewRunOptionForFreshRun();
            this.loadedExternalState=null;
            this.ClearFinalCompletionStates();
        }
        this.SelectedTab=BenchmarkTab.Cpu;
        this.SelectedLowerTab=BenchmarkLowerTab.Cpu;
        this.runCancellation?.Cancel();
        var runCancellationSource = new CancellationTokenSource();
        this.runCancellation=runCancellationSource;
        this.pendingRunTerminationKind=RunTerminationKind.None;
        var runRoot = runRootOverride??this.CreateUnsavedRunRoot(executionProfile);
        this.currentUnsavedRunRoot=runRoot;
        this.SetActiveRunRoot(runRoot);
        var runGeneration = unchecked(++this.runResultGeneration);
        var runSourceTab = this.SelectedSourceDefinition.Tab;
        string? completedRunRoot = null;
        var completedNormally = false;
        var keepPausedRunRoot = false;
        long? uiRunId = null;
        // DB 直列保存キューが渋滞していても、押した瞬間に実行状態を可視化し Stop でキャンセルできるようにする。
        // (IsRunning/StatusText の反映を DB await より後にすると、キュー待ちの間ボタンが「有効なのに無反応」になる)
        this.IsRunning=true;
        this.ActiveRunSourceTab=runSourceTab;
        this.StatusText="実行準備中";
        try {
            var initialElementCounts = this.GetSelectedElementCounts();
            var initialLogicalCpus = this.GetSelectedLogicalCpus();
            var adaptiveThreshold = this.GetAdaptiveThreshold();
            uiRunId=runRootOverride is not null&&this.activeUiRunId is not null
                ? this.activeUiRunId
                : await this.CreateUiRunAsync(runSourceTab,executionProfile).WaitAsync(runCancellationSource.Token);
            if(uiRunId is { } runId) {
                this.activeUiRunId=runId;
                if(!isResumeRun) {
                    this.unsavedUiRunId=runId;
                }
            }
            else {
                StartupDiagnostics.Write("Benchmark run started without UI DB run id. Results will not be persisted.");
            }
            this.ClearSourceTabRunningStates();
            this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
            this.activeChartMethodName=string.Empty;
            this.UpdateChart();
            if(this.SelectedResultElementCount is null) {
                this.CurrentElementCountText="1";
            }
            this.OnPropertyChanged(nameof(this.ChartElementCountSliderMaximum));
            this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
            this.OnPropertyChanged(nameof(this.SelectedChartElementCountText));
            this.RefreshSourceTabProgresses();
            this.CaptureCurrentBenchmarkSelections();
            var viewState = this.CreateBenchmarkViewStateSnapshot();
            this.uiRunRepository.QueueSaveRunSettings(uiRunId,runRoot,initialElementCounts,adaptiveThreshold,initialLogicalCpus);
            this.uiRunRepository.QueueSaveBenchmarkSnapshotItems(uiRunId,this.CreateBenchmarkGridSnapshotItems());
            this.uiRunRepository.QueueSaveUiState(uiRunId,viewState);
            this.QueueUiRunStatus(uiRunId,UiRunStatus.Running,"Benchmark run started.");
            StartupDiagnostics.Write(
                "Benchmark run requested. "
                +"SourceTab="+runSourceTab+" "
                +"ExecutionProfile="+executionProfile+" "
                +"InfiniteTestRunner="+useInfiniteTestRunner+" "
                +"LogicalCpus=["+string.Join(",",this.CpuItems.Where(static cpu => cpu.IsSelected).Select(static cpu => cpu.LogicalProcessorIndex))+"] "
                +"Benchmarks="+selected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
                +"ElementCounts=["+string.Join(",",this.GetSelectedElementCounts())+"]");
            var report = await this.runService.RunAsync(
                selected,
                initialLogicalCpus,
                this.GetSelectedLogicalCpus,
                initialElementCounts,
                runSourceTab,
                executionProfile,
                adaptiveThreshold,
                !isResumeRun||overwriteExistingResults,
                runRoot,
                this.GetCurrentRunSelectedBenchmarkCases,
                result => this.AcceptRunResult(result,runGeneration,uiRunId),
                this.AcceptRunStatus,
                new BenchmarkRunExecutionOptions(useInfiniteTestRunner),
                this.runCancellation.Token);
            completedRunRoot=report.RunRoot;
            this.currentUnsavedRunRoot=report.RunRoot;
            completedNormally=true;
            // 終了直後の DB 保存は直列キューへ投げるだけにし、SQLite のロックなどで UI の状態復帰が
            // ブロックされないようにする(書き込み順序は QueueSaveResult と同じキューで保証される)。
            this.QueueUiRunStatus(uiRunId,UiRunStatus.Completed,"Benchmark run completed.");
            this.StatusText="計測完了";
            StartupDiagnostics.Write(
                "Benchmark run completed. "
                +"RunRoot="+report.RunRoot);
        }
        catch(OperationCanceledException) {
            if(this.pendingRunTerminationKind==RunTerminationKind.Pause) {
                this.StatusText="一時停止しました";
                this.QueueUiRunStatus(uiRunId,UiRunStatus.Paused,"Benchmark run paused.");
                StartupDiagnostics.Write("Benchmark run paused.");
                this.SetActiveRunRoot(completedRunRoot??runRoot);
                keepPausedRunRoot=true;
            }
            else {
                this.StatusText="キャンセルしました";
                this.QueueUiRunStatus(uiRunId,UiRunStatus.Stopped,"Benchmark run stopped.");
                StartupDiagnostics.Write("Benchmark run canceled.");
                if(ReferenceEquals(this.runCancellation,runCancellationSource)) {
                    this.SetActiveRunRoot(null);
                }
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run failed.",ex);
            this.QueueUiRunStatus(uiRunId,UiRunStatus.Failed,"Benchmark run failed. "+ex.Message);
            this.StatusText="実行失敗 "+ex.Message;
            if(ReferenceEquals(this.runCancellation,runCancellationSource)) {
                this.SetActiveRunRoot(null);
            }
        }
        finally {
            // 10秒タイムアウトの ForceResetRunState 後に新しい実行が始まっている場合、
            // 遅れて目覚めたこの(旧)実行の finally が新しい実行の状態を上書きしないよう、
            // runCancellation の CTS 参照一致で「自分がまだ現行実行か」を判定してから共有状態を触る。
            var isCurrentRun = ReferenceEquals(this.runCancellation,runCancellationSource);
            if(isCurrentRun) {
                if(completedNormally) {
                    this.renderScheduler.FlushNow();
                    await this.DrainRunEventPipelineAsync();
                }
                else {
                    this.runEventPipeline.Clear();
                }
            }
            this.persistenceBatcher.FlushNow();
            if(isCurrentRun&&!completedNormally&&!keepPausedRunRoot) {
                this.runResultGeneration=unchecked(this.runResultGeneration+1);
            }
            if(isCurrentRun) {
                this.IsRunning=false;
                this.activeChartMethodName=string.Empty;
                this.ActiveRunSourceTab=null;
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
            }
            // 強制リセットなどで this.runCancellation が差し替わっていても、この実行の CTS だけを破棄する。
            runCancellationSource.Dispose();
            if(isCurrentRun) {
                this.runCancellation=null;
                this.pendingRunTerminationKind=RunTerminationKind.None;
            }
            this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
            this.OnPropertyChanged(nameof(this.IsSortRuleEditingEnabled));
            this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            this.StopCommand.NotifyCanExecuteChanged();
            this.PauseCommand.NotifyCanExecuteChanged();
            this.RefreshToolbarCommandStates();
            if(isCurrentRun) {
                // パイプラインに残った終端イベントの反映完了後に一度だけ表示最終化を行う。
                // クリーンアップより前に立てる(クリーンアップ末尾でも判定するため)。トークンは
                // クリーンアップ実施前の値を控え、実施後(トークンが進んだ後)のみ最終化を許可する。
                this.pendingPostRunDisplayFinalize=true;
                this.pendingPostRunFinalizeCleanupToken=this.finalLiveVisualCleanupToken;
                var discardPendingRunUpdates = !completedNormally;
                if(completedNormally&&this.runEventPipeline.HasPending) {
                    this.pendingFinalLiveVisualCleanup=true;
                    this.pendingFinalLiveVisualCleanupDiscard=discardPendingRunUpdates;
                    this.SchedulePendingFinalLiveVisualCleanup(runGeneration);
                }
                else {
                    this.ApplyFinalLiveVisualCleanup(discardPendingRunUpdates);
                }
                if(!keepPausedRunRoot) {
                    this.SetActiveRunRoot(null);
                    if(this.activeUiRunId==uiRunId) {
                        this.activeUiRunId=null;
                    }
                }
            }
            this.selectedRunStateCache=null;
            if(completedNormally) {
                this.RefreshUnsavedRunSelection();
            }
            else {
                this.RefreshUnsavedRunSelection();
            }
            this.ClearRunResultHighlights();
            if(completedNormally) {
                this.ApplyFinalBenchmarkSort();
            }
            if(completedNormally) {
                this.ApplySourceTabProgressesSynchronously();
                this.ApplyFinalCompletionStatesSynchronously();
            }
            else {
                this.RefreshSourceTabProgresses();
            }
            this.RefreshImprovementPrompt();
            this.UpdateChart();
            if(isCurrentRun&&uiRunId is { } finalRunId) {
                this.uiRunRepository.QueueSaveBenchmarkSnapshotItems(finalRunId,this.CreateBenchmarkGridSnapshotItems());
                this.uiRunRepository.QueueSaveUiState(finalRunId,this.CreateBenchmarkViewStateSnapshot());
                if(completedNormally) {
                    await this.uiRunRepository.FlushAsync();
                    if(startsFromSavedDatabaseRun) {
                        this.SelectNewRunOptionForFreshRun(false);
                    }
                    else {
                        await this.RefreshDbRunOptionsOnlyAsync(CancellationToken.None);
                    }
                }
            }
        }
    }

    private void RestoreCatalogRowsForFreshRunFromSavedDatabaseRun() {
        var displayedSelections = this.CaptureDisplayedBenchmarkCellSelections();
        this.SelectNewRunOptionForFreshRun(false);
        this.ClearAllCurrentRunResultsForFreshRun();
        this.RebuildBenchmarkRowsFromCatalog();
        this.ApplyDisplayedBenchmarkCellSelections(displayedSelections);
    }

    private IReadOnlyList<DisplayedBenchmarkCellSelection> CaptureDisplayedBenchmarkCellSelections() {
        var selections = new List<DisplayedBenchmarkCellSelection>();
        foreach(var row in this.GetAllBenchmarkRows()) {
            foreach(var cell in row.ProjectCells) {
                if(cell.HasCase&&cell.IsSelected) {
                    selections.Add(new(
                        row.SourceTab.Id,
                        row.BenchmarkTypeText,
                        row.MethodName,
                        cell.Project));
                }
            }
        }

        return selections;
    }

    private void ApplyDisplayedBenchmarkCellSelections(IReadOnlyList<DisplayedBenchmarkCellSelection> selections) {
        var selected = selections.ToHashSet();
        this.isApplyingBenchmarkSelection=true;
        try {
            foreach(var row in this.GetAllBenchmarkRows()) {
                foreach(var cell in row.ProjectCells) {
                    cell.IsSelected=cell.HasCase&&selected.Contains(new(
                        row.SourceTab.Id,
                        row.BenchmarkTypeText,
                        row.MethodName,
                        cell.Project));
                }
                row.RefreshSelectionState();
            }
        }
        finally {
            this.isApplyingBenchmarkSelection=false;
        }
        this.RefreshBenchmarkSelectionAggregates();
        this.CaptureCurrentBenchmarkSelections();
    }

    private readonly record struct DisplayedBenchmarkCellSelection(
        string SourceTabId,
        string BenchmarkTypeText,
        string MethodName,
        string Project);

    public async Task PrepareForCloseAsync() {
        if(this.isClosing) {
            return;
        }

        this.isClosing=true;
        this.logSubscription?.Dispose();
        this.logSubscription=null;
        try {
            await this.uiRunRepository.FlushAsync();
        }
        catch(Exception ex) {
            System.Diagnostics.Debug.WriteLine("Benchmark UI close persistence flush failed: "+ex);
        }
    }

    private void SetSelectedRunOptionPreservingDisplay(BenchmarkRunOption? option,bool saveUiState) {
        this.isApplyingRunOptionSelection=true;
        var changed = false;
        try {
            if(!ReferenceEquals(this.selectedRunOption,option)) {
                this.selectedRunOption=option;
                this.OnPropertyChanged(nameof(this.SelectedRunOption));
                changed=true;
                this.selectedRunStateCache=null;
                if(saveUiState) {
                    this.SaveUiState();
                }
            }
        }
        finally {
            this.isApplyingRunOptionSelection=false;
            if(changed) {
                this.RefreshBenchmarkSelectionAggregates();
                this.RefreshToolbarCommandStates();
            }
        }
    }

    /// <summary>
    /// CreateUiRunAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="runSourceTab">runSourceTab を指定して実行条件を制御します。</param>
    /// <param name="executionProfile">executionProfile を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private async Task<long?> CreateUiRunAsync(BenchmarkSourceTab runSourceTab,BenchmarkExecutionProfile executionProfile) {
        var executionProfileText = executionProfile.ToString();
        var displayName = BenchmarkAppProfile.Title+" "+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
        return await this.uiRunRepository.CreateRunAsync(GetUiRunAppKind(),displayName,executionProfileText,runSourceTab);
    }

    /// <summary>
    /// SaveDatabaseAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task SaveDatabaseAsync() {
        if(!this.CanSaveDatabase) {
            return;
        }

        this.IsRefreshing=true;
        try {
            this.PromoteDisplayedResultsToCurrentRunSession();
            this.CaptureCurrentBenchmarkSelections();
            this.RefreshCurrentRunBenchmarkSelectionSnapshot();
            var runSourceTab = this.SelectedSourceDefinition.Tab;
            var executionProfile = this.SelectedExecutionProfile.Profile;
            var runRoot = this.currentUnsavedRunRoot??this.CreateUnsavedRunRoot(executionProfile);
            var runId = this.unsavedUiRunId??this.activeUiRunId??await this.CreateUiRunAsync(runSourceTab,executionProfile);
            if(runId is null) {
                this.StatusText="DB保存失敗 Run を作成できませんでした";
                return;
            }

            var elementCounts = this.GetSelectedElementCounts();
            var logicalCpus = this.GetSelectedLogicalCpus();
            var viewState = this.CreateBenchmarkViewStateSnapshot();
            var snapshotItems = this.CreateBenchmarkGridSnapshotItems();
            this.uiRunRepository.QueueSaveRunSettings(runId,runRoot,elementCounts,this.GetAdaptiveThreshold(),logicalCpus);
            this.uiRunRepository.QueueSaveBenchmarkSnapshotItems(runId,snapshotItems);
            this.uiRunRepository.QueueSaveUiState(runId,viewState);
            this.QueueUiRunStatus(runId,UiRunStatus.Completed,"Benchmark grid snapshot saved.");
            await this.uiRunRepository.FlushAsync();
            this.unsavedUiRunId=null;
            this.activeUiRunId=null;
            this.selectedRunStateCache=null;
            this.HasUnsavedBenchmarkResults=false;
            this.HasPendingCloseStateSave=false;
            this.currentUnsavedRunRoot=null;
            this.StatusText="DB保存しました";
            await this.RefreshDbRunListAsync(CancellationToken.None,false,runId,true);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark DB save failed.",ex);
            this.StatusText="DB保存失敗 "+ex.Message;
        }
        finally {
            this.IsRefreshing=false;
            this.RefreshToolbarCommandStates();
        }
    }

    /// <summary>
    /// NewGrid メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void NewGrid() {
        if(!this.CanCreateNewGrid) {
            return;
        }

        var newRunOption = this.RunOptions.FirstOrDefault(IsNewRunOption);
        this.DiscardUnsavedBenchmarkResults();
        this.InvalidateSelectedRunLoadRequests();
        this.ClearAllCurrentRunResultsForFreshRun();
        this.HasUnsavedBenchmarkResults=false;
        this.HasPendingCloseStateSave=false;
        this.currentUnsavedRunRoot=null;
        if(newRunOption is not null) {
            this.SetSelectedRunOptionPreservingDisplay(newRunOption,true);
            this.NotifySelectedRunOptionChangedForPicker();
        }
        this.RestoreNewGridCatalogDisplay();
        this.StatusText="新規グリッドにしました";
    }

    private void RestoreNewGridCatalogDisplay() {
        this.benchmarkRowSelections.Clear();
        if(this.catalogCases.Count==0) {
            _=this.RefreshAsync();
            return;
        }

        if(!this.HasCurrentCatalogRowShape()) {
            this.RebuildBenchmarkRowsFromCatalog();
        }
        this.RefreshVisibleResults();
        this.ApplySelectedElementCountToTable();
        this.ClearAllBenchmarkSelectionsForNewGrid();
        this.ClearBenchmarkResultDisplays();
        this.RefreshImprovementPrompt();
        this.RefreshSourceTabProgresses();
        this.ApplyCurrentSort();
        this.OnPropertyChanged(nameof(this.BenchmarkRows));
    }

    private bool HasCurrentCatalogRowShape() {
        var elementCount = this.SelectedResultElementCount?.Value??0;
        foreach(var state in this.sourceTabViewStates) {
            var expectedRows = BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,state.Definition,elementCount).Count;
            if(state.Rows.Count!=expectedRows) {
                return false;
            }
        }

        return true;
    }

    private void ClearAllBenchmarkSelectionsForNewGrid() {
        this.isApplyingBenchmarkSelection=true;
        try {
            foreach(var row in this.GetAllBenchmarkRows()) {
                row.IsSelected=false;
                foreach(var cell in row.ProjectCells) {
                    cell.IsSelected=false;
                }
            }
            this.benchmarkRowSelections.Clear();
        }
        finally {
            this.isApplyingBenchmarkSelection=false;
        }
        this.RefreshBenchmarkSelectionAggregates();
        this.RefreshBenchmarkSelectionDisplay();
    }

    /// <summary>
    /// QueueUiRunStatus メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="runId">runId を指定して実行条件を制御します。</param>
    /// <param name="status">status を指定して実行条件を制御します。</param>
    /// <param name="message">message を指定して実行条件を制御します。</param>
    private void QueueUiRunStatus(long? runId,UiRunStatus status,string message) {
        this.uiRunRepository.QueueRunStatus(runId,status,message);
    }

    /// <summary>
    /// CreateBenchmarkViewStateSnapshot メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkViewStateSnapshot CreateBenchmarkViewStateSnapshot() {
        return new();
    }

    /// <summary>
    /// QueueBenchmarkResultSave メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="runGeneration">runGeneration を指定して実行条件を制御します。</param>
    /// <param name="runId">runId を指定して実行条件を制御します。</param>
    private void QueueBenchmarkResultSave(BenchmarkResultItem result,int runGeneration,long? runId) {
        if(runId is null) {
            return;
        }

        this.persistenceBatcher.Enqueue(new BenchmarkPersistenceItem(result,runGeneration,runId));
    }

    /// <summary>
    /// QueueBenchmarkResultsSave メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="items">items を指定して実行条件を制御します。</param>
    private void QueueBenchmarkResultsSave(IReadOnlyList<BenchmarkPersistenceItem> items) {
        if(items.Count==0) {
            this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
            return;
        }

        UiThreadBudget.Measure("persist-enqueue",() => {
            var snapshot = this.CreateBenchmarkViewStateSnapshot();
            var gridResults = this.CreateGridSnapshotResultsForPersistence(items);
            foreach(var item in items) {
                if(item.RunGeneration!=this.runResultGeneration) {
                    continue;
                }

                var rowKey = this.CreateResultRowKey(item.Result);
                this.uiRunRepository.QueueSaveResult(item.Result,item.UiRunId,rowKey,snapshot);
                var row = this.FindBenchmarkRow(item.Result);
                if(row is not null&&this.CreateBenchmarkGridSnapshotItem(row,0,gridResults) is { } snapshotItem) {
                    this.uiRunRepository.QueueSaveBenchmarkSnapshotItems(item.UiRunId,[snapshotItem]);
                }
            }
        });
        this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
    }

    private IReadOnlyList<BenchmarkResultItem> CreateGridSnapshotResultsForPersistence(IReadOnlyList<BenchmarkPersistenceItem> items) {
        var gridResults = this.GetAllGridResultItems();
        var pendingResults = items
            .Where(item => item.RunGeneration==this.runResultGeneration&&IsTerminalResult(item.Result))
            .Select(static item => item.Result)
            .ToArray();
        if(pendingResults.Length==0) {
            return gridResults;
        }
        if(gridResults.Count==0) {
            return pendingResults;
        }

        return gridResults.Concat(pendingResults).ToArray();
    }

    /// <summary>
    /// GetUiRunAppKind メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private static UiRunAppKind GetUiRunAppKind() {
#if SCALABLE_BENCHMARK
        return UiRunAppKind.ScalableBenchmark;
#else
        return UiRunAppKind.Benchmark;
#endif
    }

    /// <summary>
    /// Cancel メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void Cancel() {
        this.runCancellation?.Cancel();
    }

    /// <summary>
    /// AcceptRunResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="runGeneration">runGeneration を指定して実行条件を制御します。</param>
    /// <param name="uiRunId">uiRunId を指定して実行条件を制御します。</param>
    private void AcceptRunResult(BenchmarkResultItem result,int runGeneration,long? uiRunId) {
        if(runGeneration!=this.runResultGeneration) {
            return;
        }
        if(IsRunningResult(result)&&!this.IsBenchmarkRunActive) {
            return;
        }

        this.pipelineDiagnostics.RecordReceivedEvent(result.Status);
        if(IsTerminalResult(result)) {
            result.MeasuredInRun=true;
        }
        if(IsRunningResult(result)) {
            this.QueueImmediateRunningResult(result,runGeneration);
        }

        this.runEventPipeline.Post(new BenchmarkRunEvent(result,runGeneration,uiRunId,DateTimeOffset.UtcNow));
        this.renderScheduler.RequestRender();
    }

    private void QueueImmediateRunningResult(BenchmarkResultItem result,int runGeneration) {
        this.uiDispatcher.BeginInvoke(() => {
            if(runGeneration!=this.runResultGeneration) {
                return;
            }

            this.ApplyImmediateRunningResult(result);
        });
    }

    /// <summary>
    /// AcceptRunStatus メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="status">status を指定して実行条件を制御します。</param>
    private void AcceptRunStatus(string status) {
        lock(this.runStatusGate) {
            this.pendingRunStatusText=status;
        }
        this.RequestRunStatusUpdate();
    }

    /// <summary>
    /// RequestRunStatusUpdate メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RequestRunStatusUpdate() {
        if(Interlocked.Exchange(ref this.isRunStatusUpdateScheduled,1)==1) {
            return;
        }

        _=Task.Run(async () => {
            await Task.Delay(BenchmarkPipelineConstants.UiRenderIntervalMs);
            this.uiDispatcher.BeginInvoke(() => {
                string text;
                lock(this.runStatusGate) {
                    text=this.pendingRunStatusText;
                    this.pendingRunStatusText=string.Empty;
                }
                if(!string.IsNullOrWhiteSpace(text)) {
                    this.StatusText=text;
                }
                Interlocked.Exchange(ref this.isRunStatusUpdateScheduled,0);
                lock(this.runStatusGate) {
                    if(string.IsNullOrWhiteSpace(this.pendingRunStatusText)) {
                        return;
                    }
                }
                this.RequestRunStatusUpdate();
            });
        });
    }

    /// <summary>
    /// ApplyRunUiSnapshot メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="snapshot">snapshot を指定して実行条件を制御します。</param>
    private void ApplyRunUiSnapshot(BenchmarkUiSnapshot snapshot) {
        if(!snapshot.HasUpdates) {
            this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
            return;
        }

        UiThreadBudget.Measure("running-apply",() => {
            foreach(var item in snapshot.RunningUpdates) {
                this.ApplyRunningSnapshotResult(item);
            }
        });
        UiThreadBudget.Measure("terminal-apply",() => {
            foreach(var item in snapshot.TerminalResults) {
                var dirtyTab = this.ApplyTerminalSnapshotResult(item);
                if(dirtyTab is { } tab) {
                    // チャート再構築はここでは起動しない。パイプラインのRefreshChart(1000ms集約)で
                    // まとめてflushする。tick(100ms)毎に起動するとUIスレッドの反映コスト
                    // (SyncChartPoints+診断行Clear/Add)だけで飽和し、入力を受け付けなくなる。
                    this.pendingDirtyChartTabs.Add(tab);
                }
            }
        });
        if(snapshot.RefreshProgress) {
            UiThreadBudget.Measure("progress-dispatch",this.RefreshSourceTabProgressesForRunningResult);
        }
        if(snapshot.RefreshChart) {
            if(this.IsBenchmarkRunActive) {
                UiThreadBudget.Measure("completion-dispatch",this.RefreshFinalCompletionStates);
                if(this.pendingDirtyChartTabs.Count>0) {
                    var tabs = this.pendingDirtyChartTabs.ToArray();
                    this.pendingDirtyChartTabs.Clear();
                    UiThreadBudget.Measure("chart-dispatch",() => {
                        foreach(var tab in tabs) {
                            this.UpdateChartCore(this.GetSourceTabViewState(tab),false);
                        }
                    });
                }
            }
            else {
                this.pendingDirtyChartTabs.Clear();
                UiThreadBudget.Measure("idle-refresh",() => {
                    this.RefreshResultChecks(this.SelectedSourceDefinition.Tab);
                    this.RefreshVisibleResults();
                    this.ApplySelectedElementCountToTable();
                    this.UpdateChart();
                    // 実行終了後に持ち越された終端イベントの反映後、完全完了アイコンとバッジを
                    // 再計算する。これらは実行中(ACTIVE)分岐でしか更新されないため、ここが無いと
                    // 最終ステップの確定表示が永遠に反映されない。
                    this.RefreshFinalCompletionStates();
                    this.RefreshSourceTabProgresses();
                });
            }
        }
        this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
        this.TryApplyPendingFinalLiveVisualCleanup();
        this.TryApplyPostRunDisplayFinalize();
    }

    /// <summary>
    /// ApplyRunningSnapshotResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    private void ApplyRunningSnapshotResult(BenchmarkRunEvent item) {
        if(item.RunGeneration!=this.runResultGeneration) {
            return;
        }
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        var result = item.Result;
        StartupDiagnostics.AddBreadcrumb(this.CreateResultBreadcrumb("Running snapshot result",result));
        var cpu = this.FindAssignedCpu(result);
        cpu?.ApplyJob(result);
        this.ApplySourceTabRunningResult(result);
        this.MarkActiveRunSourceTab(result.SourceTab);
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            if(result.SourceTab==this.SelectedSourceDefinition.Tab) {
                this.CurrentElementCountText=FormatElementCountText(result.AxisCount);
            }
        }
        if(result.SourceTab!=this.SelectedSourceDefinition.Tab) {
            return;
        }

        this.ApplyVisibleRunningResultToGridCell(
            result,
            cpu,
            DateTimeOffset.Now,
            requestAutoScroll:false,
            updateChart:false,
            retainCompletedHighlights:false);
    }

    /// <summary>
    /// ApplyTerminalSnapshotResult メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkSourceTab? ApplyTerminalSnapshotResult(BenchmarkRunEvent item) {
        if(item.RunGeneration!=this.runResultGeneration) {
            return null;
        }

        var result = item.Result;
        StartupDiagnostics.AddBreadcrumb(this.CreateResultBreadcrumb("Terminal snapshot result",result));
        if(string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)) {
            StartupDiagnostics.Write(BenchmarkFailureDisplayLogic.CreateLogText(
                result.Project,
                result.Method,
                result.AxisCount,
                result.AssignedLogicalCpu,
                result.ArtifactFolder,
                result.FailureReason,
                result.TypeFullName));
        }

        // FlushNow は BeginInvoke 予約であり、終端イベントは MaxTerminalResultsPerTick 件ずつの
        // 分割描画のため、実行終了(finally)後に残りが描画されることがある。その場合はデータ反映のみ
        // 行い、ライブ表示(CPUセル/タブ実行状態/行)には書かない。最終クリーンアップを最後の書き手にするため。
        var isLiveVisualActive = this.IsBenchmarkRunActive;
        var cpu = this.FindAssignedCpu(result);
        if(isLiveVisualActive) {
            cpu?.ApplyJob(result);
            this.ApplySourceTabRunningResult(result);
        }
        BenchmarkSourceTab? dirtyChartTab = null;
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            if(result.SourceTab==this.SelectedSourceDefinition.Tab) {
                this.CurrentElementCountText=FormatElementCountText(result.AxisCount);
                this.UpsertResultItem(result);
                this.RegisterResultElementCount(result.AxisCount);
            }
            if(isLiveVisualActive) {
                var row = this.FindBenchmarkRow(result);
                row?.ApplyRunResult(result,cpu,DateTimeOffset.Now);
                dirtyChartTab=result.SourceTab;
            }
        }
        this.HasPendingCloseStateSave=true;
        this.HasUnsavedBenchmarkResults=true;
        this.QueueBenchmarkResultSave(result,item.RunGeneration,item.UiRunId);
        return dirtyChartTab;
    }

    /// <summary>
    /// ClearCpuJobs メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearCpuJobs() {
        foreach(var cpu in this.CpuItems) {
            cpu.ClearJob();
        }
    }

    /// <summary>
    /// ToggleRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ToggleRun() {
        _=this.RunOrResumeAsync();
    }

    /// <summary>
    /// HandleRunResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="runGeneration">runGeneration を指定して実行条件を制御します。</param>
    /// <param name="uiRunId">uiRunId を指定して実行条件を制御します。</param>
    private void HandleRunResult(BenchmarkResultItem result,int runGeneration,long? uiRunId) {
        if(runGeneration!=this.runResultGeneration) {
            return;
        }

        StartupDiagnostics.AddBreadcrumb(this.CreateResultBreadcrumb("Handle run result",result));
        StartupDiagnostics.Write(
            "Run result received. "
            +"Project="+result.Project+" "
            +"Method="+result.Method+" "
            +"Type="+(string.IsNullOrWhiteSpace(result.TypeFullName) ? "-" : result.TypeFullName)+" "
            +"N="+result.ElementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"Loop="+result.LoopCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"CPU="+(result.AssignedLogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")+" "
            +"Status="+result.Status+" "
            +"Mean="+result.Mean+" "
            +"Allocated="+result.AllocatedBytes);
        var cpu = this.FindAssignedCpu(result);
        var resultSourceTab = result.SourceTab;
        var isResultForVisibleTab = resultSourceTab==this.SelectedSourceDefinition.Tab;
        var isRunningResult = string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        var isCompletedResult = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        var isFailedResult = string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        var isTerminalResult = isCompletedResult||isFailedResult;
        if(isRunningResult&&!this.IsBenchmarkRunActive) {
            return;
        }

        var hasResultElementCount = result.AxisCount>0;
        var shouldRefreshSourceTabProgresses = false;
        if(isRunningResult||isTerminalResult) {
            cpu?.ApplyJob(result);
            this.ApplySourceTabRunningResult(result);
        }
        if(isRunningResult) {
            this.MarkActiveRunSourceTab(resultSourceTab);
        }

        if(isFailedResult) {
            StartupDiagnostics.Write(BenchmarkFailureDisplayLogic.CreateLogText(
                result.Project,
                result.Method,
                result.AxisCount,
                result.AssignedLogicalCpu,
                result.ArtifactFolder,
                result.FailureReason,
                result.TypeFullName));
        }
        if(isResultForVisibleTab&&result.AxisCount>0) {
            this.CurrentElementCountText=FormatElementCountText(result.AxisCount);
        }
        var row = this.FindBenchmarkRow(result);
        if(isRunningResult&&hasResultElementCount) {
            this.UpsertCurrentRunResultItem(result);
            shouldRefreshSourceTabProgresses=true;
        }
        if(isResultForVisibleTab&&isRunningResult) {
            this.RequestBenchmarkAutoScroll(resultSourceTab,result.Method,row?.RowKey??string.Empty);
            this.activeChartMethodName=row?.RowKey??result.Method;
            this.ApplyChartRunningState();
            this.RetainCompletedHighlightsForRunningElement(result.AxisCount);
            row?.ApplyRunResult(result,cpu,DateTimeOffset.Now);
            return;
        }
        if(isRunningResult) {
            return;
        }

        if(isTerminalResult&&hasResultElementCount) {
            this.UpsertCurrentRunResultItem(result);
            this.RefreshResultChecks(resultSourceTab);
            shouldRefreshSourceTabProgresses=true;
        }
        if(isResultForVisibleTab&&isTerminalResult&&hasResultElementCount) {
            this.UpsertResultItem(result);
            this.RegisterResultElementCount(result.AxisCount);
            this.RefreshResultChecks(resultSourceTab);
            this.RefreshVisibleResults();
        }
        var completedAt = DateTimeOffset.Now;
        if(isResultForVisibleTab) {
            row?.ApplyRunResult(result,cpu,completedAt);
            if(isTerminalResult) {
            }
        }
        if(isTerminalResult) {
            this.HasPendingCloseStateSave=true;
            this.HasUnsavedBenchmarkResults=true;
            shouldRefreshSourceTabProgresses=true;
            if(isResultForVisibleTab) {
                this.ApplySelectedElementCountToTable();
                row?.ApplyRunResult(result,cpu,completedAt);
                if(string.Equals(this.activeChartMethodName,row?.RowKey??result.Method,StringComparison.OrdinalIgnoreCase)) {
                    this.activeChartMethodName=string.Empty;
                }
                this.UpdateChart();
                this.RefreshFinalCompletionStates();
            }
            this.QueueBenchmarkResultSave(result,runGeneration,uiRunId);
        }
        if(shouldRefreshSourceTabProgresses) {
            this.RefreshSourceTabProgresses();
        }
    }

    /// <summary>
    /// ApplyImmediateRunningResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void ApplyImmediateRunningResult(BenchmarkResultItem result) {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        StartupDiagnostics.AddBreadcrumb(this.CreateResultBreadcrumb("Immediate running result",result));
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            this.RefreshSourceTabProgressesForRunningResult();
        }

        if(result.SourceTab!=this.SelectedSourceDefinition.Tab) {
            return;
        }

        this.ApplyVisibleRunningResultToGridCell(
            result,
            this.FindAssignedCpu(result),
            DateTimeOffset.Now,
            requestAutoScroll:true,
            updateChart:true,
            retainCompletedHighlights:true);
    }

    private void ApplyVisibleRunningResultToGridCell(
        BenchmarkResultItem result,
        CpuSelectionItem? cpu,
        DateTimeOffset observedAt,
        bool requestAutoScroll,
        bool updateChart,
        bool retainCompletedHighlights) {
        var row = this.FindBenchmarkRow(result);
        if(row is null) {
            this.WriteRunningGridApplyFailure("row-not-found",result,null);
            if(requestAutoScroll) {
                this.RequestBenchmarkAutoScroll(result.SourceTab,result.Method,string.Empty);
            }

            this.activeChartMethodName=result.Method;
            if(updateChart) {
                this.ApplyChartRunningState();
            }

            return;
        }

        if(row.GetProjectCell(result.Project) is null) {
            this.WriteRunningGridApplyFailure("cell-not-found",result,row);
            if(requestAutoScroll) {
                this.RequestBenchmarkAutoScroll(result.SourceTab,result.Method,row.RowKey);
            }

            this.activeChartMethodName=row.RowKey;
            if(updateChart) {
                this.ApplyChartRunningState();
            }

            return;
        }

        if(requestAutoScroll) {
            this.RequestBenchmarkAutoScroll(result.SourceTab,result.Method,row.RowKey);
        }

        this.activeChartMethodName=row.RowKey;
        if(updateChart) {
            this.ApplyChartRunningState();
        }

        if(retainCompletedHighlights) {
            this.RetainCompletedHighlightsForRunningElement(result.AxisCount);
        }

        row.ApplyRunResult(result,cpu,observedAt);
    }

    private void WriteRunningGridApplyFailure(string reason,BenchmarkResultItem result,BenchmarkTableRow? row) {
        StartupDiagnostics.Write(
            "Running grid cell apply failed. "
            +"Reason="+reason+" "
            +"SourceTab="+result.SourceTab+" "
            +"Project="+result.Project+" "
            +"Type="+(string.IsNullOrWhiteSpace(result.TypeFullName) ? "-" : result.TypeFullName)+" "
            +"Method="+result.Method+" "
            +"ArgumentKey="+(string.IsNullOrWhiteSpace(result.ArgumentKey) ? "-" : result.ArgumentKey)+" "
            +"Axis="+result.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"RunItemIndex="+result.RunItemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"Row="+(row?.RowKey??"-")+" "
            +"LeftProject="+(row?.SourceTab.LeftProject??"-")+" "
            +"RightProject="+(row?.SourceTab.RightProject??"-"));
    }

    /// <summary>
    /// discardPendingRunUpdates プロパティです。
    /// </summary>
    /// <value>discardPendingRunUpdates の値を取得/設定します。</value>
    private void TryApplyPendingFinalLiveVisualCleanup() {
        if(!this.pendingFinalLiveVisualCleanup||this.IsBenchmarkRunActive||this.runEventPipeline.HasPending) {
            return;
        }

        var discard = this.pendingFinalLiveVisualCleanupDiscard;
        this.pendingFinalLiveVisualCleanup=false;
        this.pendingFinalLiveVisualCleanupDiscard=false;
        this.ApplyFinalLiveVisualCleanup(discard);
    }

    private async Task DrainRunEventPipelineAsync() {
        for(var retry=0;retry<200;retry++) {
            this.renderScheduler.FlushNow();
            if(!this.runEventPipeline.HasPending) {
                return;
            }

            await Task.Delay(5);
        }
    }

    private void SchedulePendingFinalLiveVisualCleanup(int runGeneration) {
        _=Task.Run(async () => {
            for(var retry=0;retry<200;retry++) {
                await Task.Delay(10);
                if(this.runEventPipeline.HasPending) {
                    continue;
                }

                this.uiDispatcher.BeginInvoke(() => {
                    if(runGeneration!=this.runResultGeneration) {
                        return;
                    }

                    this.TryApplyPendingFinalLiveVisualCleanup();
                });
                return;
            }
        });
    }

    private void ApplyFinalLiveVisualCleanup(bool discardPendingRunUpdates = false) {
        StartupDiagnostics.AddBreadcrumb(
            "Final live visual cleanup apply discard="
            +discardPendingRunUpdates.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" selected="+this.SelectedSourceDefinition.Tab
            +" active="+(this.ActiveRunSourceTab?.ToString()??"-"));
        this.finalLiveVisualCleanupToken=unchecked(this.finalLiveVisualCleanupToken+1);
        if(discardPendingRunUpdates) {
            this.runEventPipeline.Clear();
        }

        this.currentRunSession.ClearRunningResults();
        this.ClearSourceTabRunningStates();
        this.ClearBenchmarkLiveStates();
        this.ApplySelectedElementCountToTable();
        this.RestoreLatestCompletedLiveResult();
        this.ClearCpuJobs();
        // 持ち越し終端イベントが1件も無い場合は ApplyRunUiSnapshot がもう呼ばれないため、ここでも判定する。
        this.TryApplyPostRunDisplayFinalize();
    }

    /// <summary>
    /// 実行終了後、パイプラインの持ち越し終端イベントが全て反映された時点で一度だけ
    /// 表示の最終化(値の再描画+完全完了アイコン+バッジ+チャート)を行います。
    /// 最終ステップの結果は実行終了後に描画されることがあり、実行中(ACTIVE)分岐の
    /// RefreshFinalCompletionStates ではそれらを確定表示へ反映できないためです。
    /// </summary>
    private void TryApplyPostRunDisplayFinalize() {
        if(!this.pendingPostRunDisplayFinalize||this.IsBenchmarkRunActive||this.runEventPipeline.HasPending) {
            return;
        }
        if(this.finalLiveVisualCleanupToken==this.pendingPostRunFinalizeCleanupToken) {
            // 最終クリーンアップ(遅延スケジュール分)がまだ実施されていない。クリーンアップは
            // 行ライブ状態や表示を作り直すため、先に最終化するとその成果が消される。
            // クリーンアップ末尾の呼び出しで改めて判定される。
            return;
        }

        this.pendingPostRunDisplayFinalize=false;
        StartupDiagnostics.AddBreadcrumb(
            "Post-run display finalize apply selected="
            +this.SelectedSourceDefinition.Tab
            +" progress="+this.OverallSourceTabProgress.FractionText);
        StartupDiagnostics.Write("Post-run display finalize applied.");
        UiThreadBudget.Measure("post-run-finalize",() => {
            this.RefreshResultChecks(this.SelectedSourceDefinition.Tab);
            this.RefreshVisibleResults();
            this.ApplySelectedElementCountToTable();
            this.UpdateChart();
            this.ApplySourceTabProgressesSynchronously();
            this.ApplyFinalCompletionStatesSynchronously();
        });
    }

    /// <summary>
    /// ClearBenchmarkLiveStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearBenchmarkLiveStates() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.ClearLiveState();
        }
    }

    private string CreateResultBreadcrumb(string context,BenchmarkResultItem result) =>
        context
        +" source="+result.SourceTab
        +" visible="+this.SelectedSourceDefinition.Tab
        +" active="+(this.ActiveRunSourceTab?.ToString()??"-")
        +" project="+result.Project
        +" method="+result.Method
        +" status="+result.Status
        +" n="+result.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        +" cpu="+(result.AssignedLogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")
        +" running="+this.IsBenchmarkRunActive.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// IsRunningResult メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsRunningResult(BenchmarkResultItem result) =>
        string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// MarkActiveRunSourceTab メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    private void MarkActiveRunSourceTab(BenchmarkSourceTab sourceTab) {
        this.ActiveRunSourceTab=sourceTab;
    }

    /// <summary>
    /// ApplySourceTabRunningResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void ApplySourceTabRunningResult(BenchmarkResultItem result) {
        if(this.sourceTabRunningState.Apply(result)) {
            this.RefreshSourceTabRunningStates();
        }
    }

    /// <summary>
    /// ClearSourceTabRunningStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearSourceTabRunningStates() {
        if(this.sourceTabRunningState.Clear()) {
            this.RefreshSourceTabRunningStates();
            return;
        }

        this.RefreshSourceTabRunningStates();
    }

    /// <summary>
    /// RefreshSourceTabRunningStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshSourceTabRunningStates() {
        foreach(var option in this.SourceTabs) {
            option.SetRunning(this.sourceTabRunningState.IsRunning(option.Tab));
        }
    }

    /// <summary>
    /// RequestBenchmarkAutoScroll メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="methodName">methodName を指定して実行条件を制御します。</param>
    /// <param name="rowKey">rowKey を指定して実行条件を制御します。</param>
    private void RequestBenchmarkAutoScroll(BenchmarkSourceTab sourceTab,string methodName,string rowKey) {
        this.LatestBenchmarkAutoScrollRequest=new BenchmarkAutoScrollRequest(
            sourceTab,
            methodName,
            rowKey,
            unchecked(++this.benchmarkAutoScrollSequence));
        this.OnPropertyChanged(nameof(this.LatestBenchmarkAutoScrollRequest));
    }

    /// <summary>
    /// RestoreLatestCompletedLiveResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RestoreLatestCompletedLiveResult() {
        if(!this.HasCurrentRunResultsForSelectedTab) {
            return;
        }

        var result = this.GetCurrentRunResultItemsForSelectedTab()
            .LastOrDefault(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase));
        if(result is null) {
            return;
        }

        var row = this.FindBenchmarkRow(result);
        row?.ApplyRunResult(result,this.FindAssignedCpu(result),DateTimeOffset.Now,false);
    }

    /// <summary>
    /// FindAssignedCpu メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private CpuSelectionItem? FindAssignedCpu(BenchmarkResultItem result) {
        var logicalCpu = BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(
            result.AssignedLogicalCpu,
            this.CpuItems.Select(static item => item.LogicalProcessorIndex));
        return logicalCpu is { } value
            ? this.CpuItems.FirstOrDefault(item => item.LogicalProcessorIndex==value)
            : null;
    }

    /// <summary>
    /// RefreshUnsavedRunSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshUnsavedRunSelection() {
        if(!this.HasUnsavedBenchmarkResults||!this.currentRunSession.HasAnyResults) {
            this.RefreshToolbarCommandStates();
            return;
        }

        this.SelectNewRunOptionForFreshRun();
        this.RefreshToolbarCommandStates();
    }

    private async Task RefreshCompletedRunSelectionAsync(long? uiRunId) {
        if(uiRunId is not { } completedRunId) {
            this.RefreshToolbarCommandStates();
            return;
        }

        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,false,completedRunId,true);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark completed run selection refresh failed.",ex);
            this.RefreshToolbarCommandStates();
        }
    }

    /// <summary>
    /// UpsertResultItem メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void UpsertResultItem(BenchmarkResultItem result) {
        // 実行が進むほどResultItemsが蓄積するため、RemoveAtを複数回行う実装(旧)は
        // 1件完了するたびにO(n)の走査+シフトが発生し実行後半でUIスレッドが詰まりやすかった。
        // IsResultMatchで一致する項目は識別上高々1件のため、見つかった位置への置換(Replace通知1回)に留める。
        for(var index = 0; index<this.ResultItems.Count; index++) {
            if(IsResultMatch(this.ResultItems[index],result)) {
                this.ResultItems[index]=result;
                return;
            }
        }
        this.ResultItems.Add(result);
    }

    /// <summary>
    /// UpsertCurrentRunResultItem メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void UpsertCurrentRunResultItem(BenchmarkResultItem result) {
        this.currentRunSession.UpsertResult(result);
    }

    /// <summary>
    /// RefreshResultChecks メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="sourceTabValue">sourceTabValue を指定して実行条件を制御します。</param>
    private void RefreshResultChecks(BenchmarkSourceTab sourceTabValue) {
        var sourceTab = BenchmarkSourceTabs.Get(sourceTabValue);
        var results = this.GetCurrentRunResultItems(sourceTab.Tab)
            .Concat(this.ResultItems.Where(item => item.SourceTab==sourceTab.Tab))
            .Where(static item => item.AxisCount>0)
            .GroupBy(item => this.CreateResultRowKey(item)+"|"+item.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparer.OrdinalIgnoreCase);
        foreach(var group in results) {
            var left = group.LastOrDefault(item => string.Equals(item.Project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase));
            var right = group.LastOrDefault(item => string.Equals(item.Project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase));
            if(left is null||right is null
                ||string.IsNullOrWhiteSpace(left.ResultDigest)
                ||string.IsNullOrWhiteSpace(right.ResultDigest)) {
                continue;
            }

            var checkText = string.Equals(left.ResultDigest,right.ResultDigest,StringComparison.Ordinal) ? "OK" : "NG";
            this.ApplyResultCheck(left,checkText);
            this.ApplyResultCheck(right,checkText);
        }
    }

    /// <summary>
    /// CreateResultRowKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string CreateResultRowKey(BenchmarkResultItem result) =>
        this.FindBenchmarkRow(result)?.RowKey??result.Method;

    /// <summary>
    /// ApplyResultCheck メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="checkText">checkText を指定して実行条件を制御します。</param>
    private void ApplyResultCheck(BenchmarkResultItem result,string checkText) {
        result.ResultCheckText=checkText;
        var row = this.FindBenchmarkRow(result);
        row?.ApplyResultCheck(result.Project,checkText,result.ResultSeedText);
        foreach(var item in this.ResultItems.ToArray().Where(item => IsResultMatch(item,result))) {
            item.ResultCheckText=checkText;
        }
        foreach(var item in this.GetCurrentRunResultItems(result.SourceTab).ToArray()
            .Where(item => IsResultMatch(item,result))) {
            item.ResultCheckText=checkText;
        }
    }

    /// <summary>
    /// GetSelectedElementCounts メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<int> GetSelectedElementCounts() {
        return BenchmarkElementCountLogic
            .BuildAdaptiveCandidates(BenchmarkElementCounts.All,BenchmarkElementCounts.All[^1])
            .ToArray();
    }

    /// <summary>
    /// GetBenchmarkRoot メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="profile">profile を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string GetBenchmarkRoot(BenchmarkExecutionProfile profile) =>
        Path.Combine(this.benchmarkRoot,BenchmarkExecutionProfileNames.ToFolderName(profile));

    /// <summary>
    /// CreateThresholdOptions メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<ThresholdOption> CreateThresholdOptions() {
        var settings = new ThresholdOptionSettings();
        ConfigureThresholdOptions(settings);
        return ThresholdOptionSettings.Values
            .Select(value => new ThresholdOption(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "+settings.Unit,
                checked(value*settings.MillisecondsScale)))
            .ToArray();
    }

    static partial void ConfigureThresholdOptions(ThresholdOptionSettings settings);

    /// <summary>
    /// ThresholdOptionSettings プロパティです。
    /// </summary>
    /// <value>ThresholdOptionSettings の値を取得/設定します。</value>
    private sealed class ThresholdOptionSettings {
        public static readonly IReadOnlyList<int> Values = [1,2,4,8,16,32,64,128,256,512,1024,2048,4096,8192];
        public int MillisecondsScale { get; set; } = 1;
        public string Unit { get; set; } = "ms";
    }

    /// <summary>
    /// GetAdaptiveThreshold メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private TimeSpan GetAdaptiveThreshold() {
        return TimeSpan.FromMilliseconds(this.SelectedThreshold.Milliseconds);
    }

    /// <summary>
    /// UpdateComparisonRowsFromResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void UpdateComparisonRowsFromResults() {
        if(this.ResultItems.Count==0) {
            this.comparisonRows= [];
            this.UpdateChart(false);
            this.ApplyComparisonRowsToTable();
            return;
        }

        try {
            this.comparisonRows=this.comparisonService.BuildRows(this.ResultItems);
            this.UpdateChart();
            this.ApplyComparisonRowsToTable();
            this.ApplyCurrentSort();
        }
        catch(InvalidDataException) {
        }
    }

    /// <summary>
    /// RegisterResultElementCount メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    private void RegisterResultElementCount(int elementCount) {
        if(elementCount<=0||this.ResultElementCountOptions.Any(item => item.Value==elementCount)) {
            return;
        }

        var option = new ResultElementCountOption(elementCount,this.FormatResultElementCountDisplayName(elementCount));
        var ordered = this.ResultElementCountOptions
            .Append(option)
            .OrderBy(static item => item.Value)
            .ToArray();
        this.ResultElementCountOptions.Clear();
        foreach(var item in ordered) {
            this.ResultElementCountOptions.Add(item);
        }
        this.OnPropertyChanged(nameof(this.ChartElementCountSliderMaximum));
        this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
        if(!this.IsRunning&&this.SelectedResultElementCount is null) {
            this.SelectedResultElementCount=this.ResultElementCountOptions.FirstOrDefault();
        }
    }

    /// <summary>
    /// FormatElementCountText メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string FormatElementCountText(int elementCount) {
        return elementCount>0
            ? elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "-";
    }

    /// <summary>
    /// FormatResultElementCountDisplayName メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string FormatResultElementCountDisplayName(int elementCount) =>
        BenchmarkWorkloadAxis.UsesLoopCount(this.SelectedSourceDefinition.Tab)
            ? "ループ回数 "+elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : elementCount==BenchmarkElementCounts.BaseElementCount
                ? "N"
                : elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+"N";

    /// <summary>
    /// RebuildResultElementCounts メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="elementCounts">elementCounts を指定して実行条件を制御します。</param>
    private void RebuildResultElementCounts(IReadOnlyList<int> elementCounts) {
        this.ResultElementCountOptions.Clear();
        foreach(var elementCount in elementCounts.Where(static item => item>0).Distinct().Order()) {
            this.ResultElementCountOptions.Add(new ResultElementCountOption(elementCount,this.FormatResultElementCountDisplayName(elementCount)));
        }

        this.OnPropertyChanged(nameof(this.ChartElementCountSliderMaximum));
        this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
        this.SelectedResultElementCount=this.ResultElementCountOptions.LastOrDefault();
        this.SyncSelectedResultElementCountIndex();
    }

    /// <summary>
    /// SelectResultElementCountByIndex メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="index">index を指定して実行条件を制御します。</param>
    private void SelectResultElementCountByIndex(int index) {
        if(this.ResultElementCountOptions.Count==0) {
            if(this.SelectedResultElementCount is not null) {
                this.SelectedResultElementCount=null;
            }
            return;
        }

        var clamped = Math.Clamp(index,0,this.ResultElementCountOptions.Count-1);
        var option = this.ResultElementCountOptions[clamped];
        if(this.SelectedResultElementCount!=option) {
            this.SelectedResultElementCount=option;
        }
    }

    /// <summary>
    /// SyncSelectedResultElementCountIndex メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void SyncSelectedResultElementCountIndex() {
        var index = this.SelectedResultElementCount is null
            ? 0
            : this.ResultElementCountOptions.IndexOf(this.SelectedResultElementCount);
        if(index<0) {
            index=0;
        }

        if(Math.Abs(this.selectedResultElementCountIndex-index)>0.001) {
            this.selectedResultElementCountIndex=index;
            this.OnPropertyChanged(nameof(this.SelectedResultElementCountIndex));
        }
        this.OnPropertyChanged(nameof(this.SelectedChartElementCountText));
        this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
    }

    /// <summary>
    /// RefreshVisibleResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshVisibleResults() {
        this.VisibleResultItems.Clear();
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var item in this.runProjectionService.GetVisibleResults(this.ResultItems.Where(this.IsResultSelectedForDisplay),selectedN)) {
            this.VisibleResultItems.Add(item);
        }
    }

    /// <summary>
    /// HasCurrentRunResultsForSelectedTab プロパティです。
    /// </summary>
    /// <value>HasCurrentRunResultsForSelectedTab の値を取得/設定します。</value>
    private bool HasCurrentRunResultsForSelectedTab =>
        this.currentRunSession.HasResults(this.SelectedSourceDefinition.Tab);

    private bool ShouldUseCurrentRunMemoryForDisplay(BenchmarkSourceTabDefinition sourceTab) =>
        !IsSavedDatabaseRunOption(this.SelectedRunOption)
        &&BenchmarkRunDisplaySourceLogic.ShouldUseCurrentRunMemory(this.currentRunSession.HasResults(sourceTab.Tab));

    /// <summary>
    /// HasDisplayResults プロパティです。
    /// </summary>
    /// <value>HasDisplayResults の値を取得/設定します。</value>
    private bool HasDisplayResults =>
        this.ResultItems.Count>0
        ||this.HasCurrentRunResultsForSelectedTab
        ||this.currentRunSession.HasAnyResults;

    /// <summary>
    /// HasSaveableBenchmarkResults プロパティです。
    /// </summary>
    /// <value>HasSaveableBenchmarkResults の値を取得/設定します。</value>
    private bool HasSaveableBenchmarkResults =>
        this.GetAllGridResultItems().Any(IsTerminalResult);

    /// <summary>
    /// RefreshToolbarCommandStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshToolbarCommandStates() {
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
        this.RunCommand.NotifyCanExecuteChanged();
        this.SaveDatabaseCommand.NotifyCanExecuteChanged();
        this.NewGridCommand.NotifyCanExecuteChanged();
        this.StopCommand.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.CanRunBenchmark));
        this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
        this.OnPropertyChanged(nameof(this.CanSelectRunHistory));
        this.OnPropertyChanged(nameof(this.CanSaveDatabase));
        this.OnPropertyChanged(nameof(this.CanCreateNewGrid));
        this.ClearRunHistoryCommand.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.CanClearRunHistory));
    }

    /// <summary>
    /// GetCurrentRunResultItemsForSelectedTab メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkResultItem> GetCurrentRunResultItemsForSelectedTab() =>
        this.GetCurrentRunResultItems(this.SelectedSourceDefinition.Tab);

    /// <summary>
    /// GetCurrentRunResultItems メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkResultItem> GetCurrentRunResultItems(BenchmarkSourceTab sourceTab) =>
        this.currentRunSession.GetResults(sourceTab);

    private int InvalidateSelectedRunLoadRequests() {
        var requestToken = ++this.selectedRunLoadRequestToken;
        this.IsRunHistoryLoading=false;
        return requestToken;
    }

    private int StartRunHistoryLoad() {
        var requestToken = ++this.selectedRunLoadRequestToken;
        this.IsRunHistoryLoading=true;
        return requestToken;
    }

    private void CompleteRunHistoryLoad(int requestToken) {
        if(requestToken==this.selectedRunLoadRequestToken) {
            this.IsRunHistoryLoading=false;
        }
    }

    private IReadOnlyList<BenchmarkResultItem> GetAllGridResultItems() {
        var currentResults = this.currentRunSession.GetAllResults();
        if(currentResults.Count==0) {
            return this.ResultItems.ToArray();
        }
        if(this.ResultItems.Count==0) {
            return currentResults;
        }

        return this.ResultItems.Concat(currentResults).ToArray();
    }

    private void PromoteDisplayedResultsToCurrentRunSession() {
        foreach(var result in this.ResultItems) {
            this.UpsertCurrentRunResultItem(result);
        }
    }

    private static bool IsTerminalResult(BenchmarkResultItem result) =>
        string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
        ||string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// RestoreCurrentRunDisplayForSelectedTab メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool RestoreCurrentRunDisplayForSelectedTab() {
        var results = this.GetCurrentRunResultItemsForSelectedTab();
        if(results.Count==0) {
            return false;
        }

        var latestElementCount = results
            .Where(static item => item.AxisCount>0)
            .OrderBy(static item => item.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static item => item.AxisCount)
            .LastOrDefault()
            ?.AxisCount;
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        foreach(var item in results) {
            this.ResultItems.Add(item);
        }

        this.RebuildResultElementCounts(results
            .Select(static item => item.AxisCount)
            .Where(static item => item>0)
            .Distinct()
            .Order()
            .ToArray());
        if(latestElementCount is { } latestN) {
            this.SelectedResultElementCount=this.ResultElementCountOptions.FirstOrDefault(item => item.Value==latestN)??this.SelectedResultElementCount;
            this.CurrentElementCountText=FormatElementCountText(latestN);
        }
        this.RefreshVisibleResults();
        this.comparisonRows=this.SelectedResultElementCount is { } selectedElementCount
            ? this.BuildCurrentRunComparisonRowsForElementCount(selectedElementCount.Value)
            : this.BuildCurrentRunComparisonRows();
        this.ApplyComparisonRowsToTable();
        this.ApplySelectedElementCountToTable();
        this.ApplyCurrentRunResultsToTable(results);
        this.RefreshImprovementPrompt();
        this.UpdateChart();
        this.RefreshSourceTabProgresses();
        return true;
    }

    /// <summary>
    /// ApplyCurrentRunResultsToTable メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="results">results を指定して実行条件を制御します。</param>
    private void ApplyCurrentRunResultsToTable(IReadOnlyList<BenchmarkResultItem> results) {
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var result in results.Where(result => selectedN is null||result.AxisCount==selectedN)) {
            var row = this.FindBenchmarkRow(result);
            row?.ApplyRunResult(result,this.FindAssignedCpu(result),result.StartedAt??DateTimeOffset.Now,false);
        }
    }

    /// <summary>
    /// ClearSelectedRunDisplay メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearSelectedRunDisplay() {
        using(this.BeginDataGridUpdateBatch()) {
            this.ClearSourceTabRunningStates();
            this.ClearAllSourceTabComparisonDisplays();
            this.RememberCompletedResultItemsForFallback();
            this.ResultItems.Clear();
            this.VisibleResultItems.Clear();
            this.CurrentElementCountText="-";
            this.ClearBenchmarkResultDisplays();
            this.ImprovementPromptText=string.Empty;
            this.UpdateAllSourceTabCharts(false);
            this.RefreshSourceTabProgresses();
            this.RefreshToolbarCommandStates();
        }
    }

    private void RememberCompletedResultItemsForFallback() {
        var completed = this.ResultItems
            .Where(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)&&item.MeanNanoseconds is not null)
            .ToArray();
        if(completed.Length>0) {
            this.recentCompletedResultItems=completed;
        }
    }

    /// <summary>
    /// ClearAllCurrentRunResultsForFreshRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearAllCurrentRunResultsForFreshRun() {
        this.ClearSourceTabRunningStates();
        this.currentRunSession.Clear();
        this.ClearAllSourceTabComparisonDisplays();
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.ResultElementCountOptions.Clear();
        this.SelectedResultElementCount=null;
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.ImprovementPromptText=string.Empty;
        this.selectedRunStateCache=null;
        this.ClearRunResultHighlights();
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// ClearAllSourceTabComparisonDisplays メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearAllSourceTabComparisonDisplays() {
        foreach(var state in this.sourceTabViewStates) {
            state.ComparisonRows= [];
            state.ベンチマークシェア概要表示=string.Empty;
            this.GetSourceTabOption(state.Definition.Tab).ベンチマークシェア概要短縮表示を設定(BenchmarkImprovementPromptBuilder.概要を作成([]).ShortDisplayText);
            this.SyncBenchmarkChartPoints(state,[]);
            this.UpdateChartEmptyReason(state,[],[]);
            state.LatencyHotspotRows.Clear();
            state.AllocationDiagnosticRows.Clear();
            state.CpuTimelineRows.Clear();
        }
        this.全体ベンチマークシェア概要を更新();
        this.OnPropertyChanged(nameof(this.ベンチマークシェア概要表示));
        this.OnPropertyChanged(nameof(this.ChartPoints));
        this.OnPropertyChanged(nameof(this.ChartEmptyReasonText));
        this.OnPropertyChanged(nameof(this.ChartEmptyReasonTooltipText));
        this.OnPropertyChanged(nameof(this.LatencyHotspotRows));
        this.OnPropertyChanged(nameof(this.AllocationDiagnosticRows));
        this.OnPropertyChanged(nameof(this.CpuTimelineRows));
    }

    /// <summary>
    /// ClearSelectedSourceLiveResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearSelectedSourceLiveResults() {
        this.currentRunSession.ClearSourceTab(this.SelectedSourceDefinition.Tab);
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.ベンチマークシェア概要表示=string.Empty;
        this.GetSourceTabOption(this.SelectedSourceDefinition.Tab).ベンチマークシェア概要短縮表示を設定(BenchmarkImprovementPromptBuilder.概要を作成([]).ShortDisplayText);
        this.全体ベンチマークシェア概要を更新();
        this.RefreshImprovementPrompt();
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// PrepareSelectedSourceLiveResultsForRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void PrepareSelectedSourceLiveResultsForRun() {
        this.currentRunSession.ClearSourceTab(this.SelectedSourceDefinition.Tab);
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.ベンチマークシェア概要表示=string.Empty;
        this.GetSourceTabOption(this.SelectedSourceDefinition.Tab).ベンチマークシェア概要短縮表示を設定(BenchmarkImprovementPromptBuilder.概要を作成([]).ShortDisplayText);
        this.全体ベンチマークシェア概要を更新();
        this.ImprovementPromptText=string.Empty;
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// elementCount プロパティです。
    /// </summary>
    /// <value>elementCount の値を取得/設定します。</value>
    private void LoadComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows,int elementCount = 0) {
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.AddComparisonRowsIntoResults(rows,elementCount,this.LoadSelectedRunState());
    }

    /// <summary>
    /// AddComparisonRowsIntoResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <param name="selectedRunState">selectedRunState を指定して実行条件を制御します。</param>
    private void AddComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows,int elementCount,ProgressState? selectedRunState) {
        foreach(var row in rows) {
            this.AddResult(row.MethodName,row.TypeFullName,row.ArgumentKey,row.Arguments,row.SourceTab.LeftProject,row.Left,elementCount,selectedRunState);
            this.AddResult(row.MethodName,row.TypeFullName,row.ArgumentKey,row.Arguments,row.SourceTab.RightProject,row.Right,elementCount,selectedRunState);
        }
    }

    /// <summary>
    /// AddResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    /// <param name="arguments">arguments を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="metrics">metrics を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <param name="selectedRunState">selectedRunState を指定して実行条件を制御します。</param>
    private void AddResult(
        string method,
        string typeFullName,
        string argumentKey,
        IReadOnlyList<BenchmarkArgument> arguments,
        string project,
        ProjectMetrics metrics,
        int elementCount,
        ProgressState? selectedRunState) {
        if(metrics.HasValue) {
            var job = selectedRunState?.Jobs.LastOrDefault(job => string.Equals(job.MethodName,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(job.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(job.TypeFullName,typeFullName)
                &&IsSameArgument(job.ArgumentKey,argumentKey)
                &&BenchmarkWorkloadAxis.GetAxisCount(job)==elementCount
                &&job.Status==JobStatus.Completed);
            this.ResultItems.Add(job is null
                ? new BenchmarkResultItem(new BenchmarkStoredResult(project,method,typeFullName,BenchmarkWorkloadAxis.ResolveElementCount(this.SelectedSourceDefinition.Tab,elementCount),BenchmarkWorkloadAxis.ResolveLoopCount(this.SelectedSourceDefinition.Tab,elementCount),"Completed",metrics.TimeNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,metrics.TimeNanoseconds,metrics.AllocatedBytes,DateTimeOffset.Now,null,string.Empty) {
                    ArgumentKey=argumentKey,
                    Arguments=arguments,
                })
                : new BenchmarkResultItem(job));
        }
    }

    /// <summary>
    /// ApplyComparisonRowsToTable メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyComparisonRowsToTable() {
        this.ApplyComparisonRowsToTable(this.SelectedSourceTabViewState);
    }

    /// <summary>
    /// ApplyComparisonRowsToAllTables メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyComparisonRowsToAllTables() {
        foreach(var state in this.sourceTabViewStates) {
            this.ApplyComparisonRowsToTable(state);
        }
    }

    /// <summary>
    /// ApplyComparisonRowsToTable メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    private void ApplyComparisonRowsToTable(BenchmarkSourceTabViewState state) {
        var hasCurrentRunResults = this.currentRunSession.HasResults(state.Definition.Tab);
        foreach(var benchmark in state.Rows) {
            benchmark.ApplyComparison(this.FindComparisonRow(state,benchmark),this.IsBenchmarkRunActive||hasCurrentRunResults);
        }
        this.ベンチマークシェア概要を更新(state);
    }

    /// <summary>
    /// ClearUnselectedResultDisplays メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearUnselectedResultDisplays() {
        foreach(var benchmark in this.BenchmarkRows) {
            benchmark.ClearUnselectedResultDisplays();
        }
    }

    /// <summary>
    /// ApplySelectedDisplayResultsToTable メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplySelectedDisplayResultsToTable() {
        var selectedN = this.SelectedResultElementCount?.Value;
        var source = this.GetResultSourceForTableApply();
        var results = selectedN is null
            ? source
            : source.Where(result => result.AxisCount==selectedN).ToArray();
        if(results.Count==0&&source.Count>0) {
            results=source;
        }
        foreach(var result in results) {
            var row = this.FindBenchmarkRow(result);
            row?.ApplyRunResult(result,this.FindAssignedCpu(result),result.StartedAt??DateTimeOffset.Now,false);
        }
    }

    /// <summary>
    /// FindComparisonRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkComparisonRow? FindComparisonRow(BenchmarkTableRow row) {
        return this.FindComparisonRow(this.SelectedSourceTabViewState,row);
    }

    /// <summary>
    /// FindComparisonRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkComparisonRow? FindComparisonRow(BenchmarkSourceTabViewState state,BenchmarkTableRow row) {
        foreach(var item in state.ComparisonRows) {
            if(IsComparisonRowMatch(row,item)) {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// ベンチマークシェア概要を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ベンチマークシェア概要を更新() {
        this.ベンチマークシェア概要を更新(this.SelectedSourceTabViewState);
    }

    /// <summary>
    /// ベンチマークシェア概要を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    private void ベンチマークシェア概要を更新(BenchmarkSourceTabViewState state) {
        var measuredRows=this.BuildMeasuredRunComparisonRows(state.Definition);
        var summary = BenchmarkImprovementPromptBuilder.概要を作成(measuredRows);
        state.ベンチマークシェア概要表示=summary.DisplayText;
        this.GetSourceTabOption(state.Definition.Tab).ベンチマークシェア概要短縮表示を設定(summary.ShortDisplayText);
        this.全体ベンチマークシェア概要を更新();
        if(state==this.SelectedSourceTabViewState) {
            this.OnPropertyChanged(nameof(this.ベンチマークシェア概要表示));
        }
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildMeasuredRunComparisonRows(BenchmarkSourceTabDefinition sourceTab){
        var measured=this.GetResultSourceForDisplay()
            .Where(result => result.MeasuredInRun&&sourceTab.ContainsProject(result.Project))
            .ToArray();
        return measured.Length==0
            ? []
            : this.runProjectionService.BuildLatestComparisonRows(measured,sourceTab,this.CreateReportRowsForComparison);
    }

    private void 全体ベンチマークシェア概要を更新() {
        var rows=this.sourceTabViewStates
            .SelectMany(state => this.BuildMeasuredRunComparisonRows(state.Definition))
            .ToArray();
        var summary = BenchmarkImprovementPromptBuilder.概要を作成(rows);
        this.全体ベンチマークシェア概要表示=summary.OverallDisplayText;
        if(this.全体ベンチマークシェア概要短縮文字列!=summary.ShortDisplayText){
            this.全体ベンチマークシェア概要短縮文字列=summary.ShortDisplayText;
            this.OnPropertyChanged(nameof(this.OverallSourceProgressSummaryText));
        }
    }

    /// <summary>
    /// RefreshImprovementPrompt メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshImprovementPrompt() {
        var candidates = this.BuildImprovementCandidatesFromComparisonRows();
        if(candidates.Count==0) {
            var state = this.GetProgressStateForImprovementPrompt();
            candidates=state is null
                ? []
                : BenchmarkImprovementCandidateBuilder.BuildCandidates(state);
        }
        this.ApplyImprovementCandidates(candidates);
        this.RefreshImprovementPromptText();
    }

    private IReadOnlyList<BenchmarkImprovementCandidate> BuildImprovementCandidatesFromComparisonRows() {
        var rows = this.sourceTabViewStates
            .SelectMany(static state => state.ComparisonRows)
            .ToArray();
        if(rows.Length==0) {
            return [];
        }

        var elementCount = this.SelectedResultElementCount?.Value??0;
        return BenchmarkImprovementCandidateBuilder.BuildCandidatesFromComparisonRows(rows,elementCount);
    }

    /// <summary>
    /// ApplyImprovementCandidates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="candidates">candidates を指定して実行条件を制御します。</param>
    private void ApplyImprovementCandidates(IReadOnlyList<BenchmarkImprovementCandidate> candidates) {
        this.LoadImprovementCandidateSelectionsForCurrentRun();
        var hasStoredSelections = this.hasLoadedImprovementCandidateSelections&&this.improvementCandidateSelections.Count>0;
        var defaultSelected = hasStoredSelections
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : BenchmarkImprovementCandidateBuilder.CreateDefaultSelectedKeys(candidates);
        foreach(var row in this.ImprovementCandidates) {
            row.SelectionChanged-=this.OnImprovementCandidateSelectionChanged;
        }
        this.ImprovementCandidates.Clear();
        this.isApplyingImprovementCandidateSelection=true;
        try {
            foreach(var candidate in candidates) {
                var selected = hasStoredSelections
                    ? this.improvementCandidateSelections.TryGetValue(candidate.Key,out var value)&&value
                    : defaultSelected.Contains(candidate.Key);
                this.improvementCandidateSelections[candidate.Key]=selected;
                var row = new BenchmarkImprovementCandidateRow(candidate,selected);
                row.SelectionChanged+=this.OnImprovementCandidateSelectionChanged;
                this.ImprovementCandidates.Add(row);
            }
        }
        finally {
            this.isApplyingImprovementCandidateSelection=false;
        }
        this.RefreshImprovementCandidateViews();
        this.ImprovementCandidateSummaryText=string.Empty;
    }

    /// <summary>
    /// OnImprovementCandidateSelectionChanged メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="sender">sender を指定して実行条件を制御します。</param>
    /// <param name="args">args を指定して実行条件を制御します。</param>
    private void OnImprovementCandidateSelectionChanged(object? sender,EventArgs args) {
        if(sender is not BenchmarkImprovementCandidateRow row) {
            return;
        }

        this.improvementCandidateSelections[row.Key]=row.IsSelected;
        if(!this.isApplyingImprovementCandidateSelection) {
            this.RefreshImprovementPromptText();
            this.ImprovementCandidateSummaryText=string.Empty;
        }
    }

    /// <summary>
    /// RefreshImprovementPromptText メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshImprovementPromptText() {
        this.ImprovementPromptText=BenchmarkImprovementPromptBuilder.BuildPrompt(
            this.ImprovementCandidates
                .Where(static row => row.IsSelected)
                .Select(static row => row.Candidate));
    }

    private void RefreshImprovementCandidateViews() {
        this.ReplaceImprovementCandidateRows(
            this.TimeImprovementCandidates,
            this.ImprovementCandidates
                .Where(static row => row.Candidate.時間回帰か)
                .OrderByDescending(static row => BenchmarkImprovementCandidateBuilder.順位値を取得(row.Candidate.時間シェア)));
        this.ReplaceImprovementCandidateRows(
            this.AllocatedImprovementCandidates,
            this.ImprovementCandidates
                .Where(static row => row.Candidate.メモリ回帰か)
                .OrderByDescending(static row => BenchmarkImprovementCandidateBuilder.順位値を取得(row.Candidate.メモリシェア)));
    }

    private void ReplaceImprovementCandidateRows(
        ObservableCollection<BenchmarkImprovementCandidateRow> target,
        IEnumerable<BenchmarkImprovementCandidateRow> rows) {
        target.Clear();
        foreach(var row in rows) {
            target.Add(row);
        }
    }

    /// <summary>
    /// CreateImprovementCandidateSummary メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private string CreateImprovementCandidateSummary() {
        var selected = this.ImprovementCandidates.Count(static row => row.IsSelected);
        var total = this.ImprovementCandidates.Count;
        var time = this.ImprovementCandidates.Count(static row => row.Candidate.時間回帰か);
        var allocated = this.ImprovementCandidates.Count(static row => row.Candidate.メモリ回帰か);
        return "選択 "+selected.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"/"+total.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" / 時間 "+time.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" / メモリ "+allocated.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// LoadImprovementCandidateSelectionsForCurrentRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void LoadImprovementCandidateSelectionsForCurrentRun() {
        var runId = this.GetCurrentImprovementSelectionRunId();
        if(this.loadedImprovementSelectionRunId==runId) {
            return;
        }

        this.loadedImprovementSelectionRunId=runId;
        this.improvementCandidateSelections.Clear();
        this.hasLoadedImprovementCandidateSelections=false;
    }

    /// <summary>
    /// GetCurrentImprovementSelectionRunId メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private long? GetCurrentImprovementSelectionRunId() {
        if(this.activeUiRunId is { } active) {
            return active;
        }

        var selectedRun = this.SelectedRunOption;
        return selectedRun is not null&&!IsNewRunOption(selectedRun)&&!IsTransientRunOption(selectedRun)&&selectedRun.UiRunId>0
            ? selectedRun.UiRunId
            : null;
    }

    /// <summary>
    /// CopyImprovementPromptAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task CopyImprovementPromptAsync() {
        if(string.IsNullOrWhiteSpace(this.ImprovementPromptText)) {
            return;
        }

        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(this.ImprovementPromptText);
        this.StatusText="改善プロンプトをコピーしました";
    }

    /// <summary>
    /// chartRefreshBatchDepth フィールドです。
    /// </summary>
    private int chartRefreshBatchDepth;
    /// <summary>
    /// pendingChartUpdate フィールドです。
    /// </summary>
    private bool pendingChartUpdate;
    /// <summary>
    /// pendingChartUpdatePreserveExisting フィールドです。
    /// </summary>
    private bool pendingChartUpdatePreserveExisting = true;
    /// <summary>
    /// pendingProgressRefresh フィールドです。
    /// </summary>
    private bool pendingProgressRefresh;
    /// <summary>
    /// dataGridUpdateBatchDepth フィールドです。
    /// </summary>
    private int dataGridUpdateBatchDepth;
    /// <summary>
    /// lastRunningProgressRefreshAt フィールドです。
    /// </summary>
    private DateTimeOffset lastRunningProgressRefreshAt = DateTimeOffset.MinValue;
    /// <summary>
    /// RunningProgressRefreshInterval フィールドです。
    /// </summary>
    private static readonly TimeSpan RunningProgressRefreshInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// DataGridUpdateBatchChanged イベントです。
    /// </summary>
    internal event EventHandler<BenchmarkDataGridUpdateBatchEventArgs>? DataGridUpdateBatchChanged;

    /// <summary>
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// 必 要 に 応 じ て 副 作 用 と し て 内 部 状 態 や 表 示 へ 反 映 し ま す 。
    /// </summary>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    private IDisposable BeginDataGridUpdateBatch() {
        var shouldNotify = this.dataGridUpdateBatchDepth==0;
        this.dataGridUpdateBatchDepth++;
        if(shouldNotify) {
            this.DataGridUpdateBatchChanged?.Invoke(this,BenchmarkDataGridUpdateBatchEventArgs.Begin);
        }

        return new DataGridUpdateBatchScope(this);
    }

    /// <summary>
    /// EndDataGridUpdateBatch メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void EndDataGridUpdateBatch() {
        if(this.dataGridUpdateBatchDepth<=0) {
            return;
        }

        this.dataGridUpdateBatchDepth--;
        if(this.dataGridUpdateBatchDepth==0) {
            this.DataGridUpdateBatchChanged?.Invoke(this,BenchmarkDataGridUpdateBatchEventArgs.End);
        }
    }

    /// <summary>
    /// IDisposable プロパティです。
    /// </summary>
    /// <value>IDisposable の値を取得/設定します。</value>
    private sealed class DataGridUpdateBatchScope:IDisposable {
        private readonly RunnerViewModel owner;
        private bool disposed;
        public DataGridUpdateBatchScope(RunnerViewModel owner) => this.owner=owner;
        public void Dispose() {
            if(this.disposed) {
                return;
            }
            this.disposed=true;
            this.owner.EndDataGridUpdateBatch();
        }
    }

    /// <summary>
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// 必 要 に 応 じ て 副 作 用 と し て 内 部 状 態 や 表 示 へ 反 映 し ま す 。
    /// </summary>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    private IDisposable BeginChartRefreshBatch() {
        this.chartRefreshBatchDepth++;
        return new ChartRefreshBatchScope(this);
    }

    /// <summary>
    /// EndChartRefreshBatch メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void EndChartRefreshBatch() {
        this.chartRefreshBatchDepth--;
        if(this.chartRefreshBatchDepth>0) {
            return;
        }
        if(this.pendingProgressRefresh) {
            this.pendingProgressRefresh=false;
            this.RefreshSourceTabProgressesCore();
        }
        if(this.pendingChartUpdate) {
            var preserve = this.pendingChartUpdatePreserveExisting;
            this.pendingChartUpdate=false;
            this.pendingChartUpdatePreserveExisting=true;
            this.UpdateAllSourceTabCharts(preserve);
        }
    }

    /// <summary>
    /// IDisposable プロパティです。
    /// </summary>
    /// <value>IDisposable の値を取得/設定します。</value>
    private sealed class ChartRefreshBatchScope:IDisposable {
        private readonly RunnerViewModel owner;
        private bool disposed;
        public ChartRefreshBatchScope(RunnerViewModel owner) => this.owner=owner;
        public void Dispose() {
            if(this.disposed) {
                return;
            }
            this.disposed=true;
            this.owner.EndChartRefreshBatch();
        }
    }

    /// <summary>
    /// RefreshSourceTabProgresses メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshSourceTabProgresses() {
        if(this.chartRefreshBatchDepth>0) {
            this.pendingProgressRefresh=true;
            return;
        }
        this.RefreshSourceTabProgressesCore();
    }

    /// <summary>
    /// RefreshSourceTabProgressesForRunningResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshSourceTabProgressesForRunningResult() {
        if(this.chartRefreshBatchDepth>0) {
            this.pendingProgressRefresh=true;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if(now-this.lastRunningProgressRefreshAt<RunningProgressRefreshInterval) {
            return;
        }

        this.lastRunningProgressRefreshAt=now;
        this.RefreshSourceTabProgressesCore();
    }

    /// <summary>
    /// sourceTabProgressRequestToken フィールドです。
    /// </summary>
    private int sourceTabProgressRequestToken;
    /// <summary>
    /// sourceTabProgressRefreshInFlight フィールドです。
    /// </summary>
    private bool sourceTabProgressRefreshInFlight;
    /// <summary>
    /// sourceTabProgressRefreshPending フィールドです。
    /// </summary>
    private bool sourceTabProgressRefreshPending;

    /// <summary>
    /// SourceTabProgressInput メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="Option">Option を指定して実行条件を制御します。</param>
    /// <param name="Definition">Definition を指定して実行条件を制御します。</param>
    /// <param name="Cells">Cells を指定して実行条件を制御します。</param>
    /// <param name="SessionResults">SessionResults を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct SourceTabProgressInput(
        SourceTabOption Option,
        BenchmarkSourceTabDefinition Definition,
        IReadOnlyList<BenchmarkTabProgressCell> Cells,
        IReadOnlyList<BenchmarkResultItem> SessionResults,
        int MeasuredSelectedCells,
        int SelectedCells);

    // 全5タブ分の進捗計算は実行が進むほど重くなる。UIスレッドに残すのは cells 構築(行数程度)と
    // 生の結果配列の確保(参照コピー)のみで、measurement射影(GroupBy/重複排除)と
    // BenchmarkTabProgressLogic.Calculate(O(cells×measurements)) はバックグラウンドで実行する。
    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void RefreshSourceTabProgressesCore() {
        if(this.sourceTabProgressRefreshInFlight) {
            this.sourceTabProgressRefreshPending=true;
            return;
        }

        if(this.catalogCases.Count==0) {
            foreach(var option in this.SourceTabs) {
                option.SetProgress(new BenchmarkTabProgress(0,0));
            }
            this.OverallSourceTabProgress=new BenchmarkTabProgress(0,0);
            return;
        }

        var inputs = this.SourceTabs
            .Select(option => {
                var measured = this.CountMeasuredSelectedCells(option.Definition);
                return new SourceTabProgressInput(
                    option,
                    option.Definition,
                    this.CreateProgressCells(option.Definition),
                    this.GetCurrentRunResultItems(option.Definition.Tab),
                    measured.Measured,
                    measured.Selected);
            })
            .ToArray();
        var resultItemsSnapshot = this.ResultItems.ToArray();
        var stateJobsSnapshot = this.currentRunSession.HasAnyResults
            ? null
            : this.LoadSelectedRunState()?.Jobs.ToArray();
        var maxElementCount = this.GetSelectedElementCounts().LastOrDefault();
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        var token = ++this.sourceTabProgressRequestToken;
        var generation = this.runResultGeneration;
        this.sourceTabProgressRefreshInFlight=true;
        var stopwatch = Stopwatch.StartNew();
        Task.Run(() => inputs
            .Select(input => CalculateSourceTabProgress(
                    input.Cells,
                    BuildProgressMeasurements(input.SessionResults,resultItemsSnapshot,stateJobsSnapshot,input.Definition.Tab),
                    input.Definition.LeftProject,
                    input.Definition.RightProject,
                    maxElementCount,
                    thresholdNanoseconds,
                    input.MeasuredSelectedCells,
                    input.SelectedCells))
                .ToArray())
            .ContinueWith(task => {
                UiThreadBudget.Record("bg:progress",stopwatch.Elapsed.TotalMilliseconds);
                this.uiDispatcher.BeginInvoke(() => {
                    this.sourceTabProgressRefreshInFlight=false;
                    if(token==this.sourceTabProgressRequestToken&&generation==this.runResultGeneration&&!task.IsFaulted) {
                        var progresses = task.Result;
                        UiThreadBudget.Measure("progress-apply",() => {
                            for(var index = 0; index<inputs.Length; index++) {
                                inputs[index].Option.SetProgress(progresses[index]);
                                if(!this.IsBenchmarkRunActive
                                    &&progresses[index].Total>0
                                    &&progresses[index].Completed>=progresses[index].Total) {
                                    inputs[index].Option.SetFinalCompleted(true);
                                }
                            }
                            this.OverallSourceTabProgress=new BenchmarkTabProgress(
                                progresses.Sum(static progress => progress.Completed),
                                progresses.Sum(static progress => progress.Total));
                        });
                    }
                    else if(task.IsFaulted) {
                        StartupDiagnostics.Write("Background source tab progress refresh failed.",task.Exception);
                    }

                    if(this.sourceTabProgressRefreshPending) {
                        this.sourceTabProgressRefreshPending=false;
                        this.RefreshSourceTabProgressesCore();
                    }
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// BuildProgressMeasurements メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sessionResults">sessionResults を指定して実行条件を制御します。</param>
    /// <param name="resultItems">resultItems を指定して実行条件を制御します。</param>
    /// <param name="stateJobs">stateJobs を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<BenchmarkElementMeasurement> BuildProgressMeasurements(
        IReadOnlyList<BenchmarkResultItem> sessionResults,
        IReadOnlyList<BenchmarkResultItem> resultItems,
        IReadOnlyList<BenchmarkJob>? stateJobs,
        BenchmarkSourceTab sourceTab) {
        IEnumerable<BenchmarkResultItem> EnumerateSource() {
            foreach(var item in sessionResults) {
                yield return item;
            }

            foreach(var item in resultItems) {
                if(item.SourceTab==sourceTab) {
                    yield return item;
                }
            }

            if(stateJobs is null) {
                yield break;
            }

            foreach(var job in stateJobs) {
                if(job.SourceTab==sourceTab) {
                    yield return new BenchmarkResultItem(job);
                }
            }
        }

        return EnumerateSource()
            .Where(static item => item.AxisCount>0)
            .GroupBy(static item => CreateResultIdentityKey(item.Project,item.TypeFullName,item.Method,item.AxisCount,item.ArgumentKey),StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .Select(static item => new BenchmarkElementMeasurement(
                item.Project,
                item.Method,
                item.TypeFullName,
                item.ArgumentKey,
                item.AxisCount,
                item.Status,
                item.MeanNanoseconds,
                item.TotalNanoseconds))
            .ToArray();
    }

    /// <summary>
    /// CreateProgressCells メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkTabProgressCell> CreateProgressCells(BenchmarkSourceTabDefinition sourceTab) {
        var currentRunCells = this.CreateCurrentRunProgressCells(sourceTab);
        if(currentRunCells.Count>0) {
            return currentRunCells;
        }

        var state = this.GetSourceTabViewState(sourceTab.Tab);
        if(state.Rows.Count>0) {
            var rowSnapshot = state.Rows.ToArray();
            return rowSnapshot
                .SelectMany(row => row.ProjectCells.Select(cell => CreateProgressCell(row,cell.Project,cell.HasCase&&cell.IsSelected)))
                .ToArray();
        }

        return BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,sourceTab)
            .SelectMany(row => sourceTab.ProjectOrder.Select(project => CreateProgressCell(
                row,
                project,
                row.CasesByProject.ContainsKey(project)
                    &&this.benchmarkRowSelections.TryGetValue(CreateRowSelectionKey(sourceTab,row.RowKey),out var selected)
                    &&selected)))
            .ToArray();
    }

    /// <summary>
    /// CreateCurrentRunProgressCells メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkTabProgressCell> CreateCurrentRunProgressCells(BenchmarkSourceTabDefinition sourceTab) {
        if(!this.IsBenchmarkRunActive&&!this.currentRunSession.HasAnyResults) {
            return [];
        }

        var selectedCases = this.GetCurrentRunSelectedBenchmarkCases();
        return selectedCases.Count==0
            ? []
            : BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases(selectedCases,sourceTab);
    }

    /// <summary>
    /// CreateProgressCell メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="isSelected">isSelected を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkTabProgressCell CreateProgressCell(BenchmarkRowDefinition row,string project,bool isSelected) {
        row.CasesByProject.TryGetValue(project,out var benchmark);
        return new BenchmarkTabProgressCell(
            row.MethodName,
            project,
            benchmark?.TypeFullName??string.Empty,
            benchmark?.ArgumentKey??string.Empty,
            isSelected,
            row.RowKey);
    }

    /// <summary>
    /// CreateProgressCell メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="isSelected">isSelected を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkTabProgressCell CreateProgressCell(BenchmarkTableRow row,string project,bool isSelected) {
        row.CasesByProject.TryGetValue(project,out var benchmark);
        return new BenchmarkTabProgressCell(
            row.MethodName,
            project,
            benchmark?.TypeFullName??string.Empty,
            benchmark?.ArgumentKey??string.Empty,
            isSelected,
            row.RowKey);
    }

    /// <summary>
    /// GetProgressStateForImprovementPrompt メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState? GetProgressStateForImprovementPrompt() {
        if(this.currentRunSession.HasAnyResults) {
            return this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot??string.Empty);
        }

        return this.LoadSelectedRunState();
    }

    /// <summary>
    /// RetainCompletedHighlightsForRunningElement メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="runningElementCount">runningElementCount を指定して実行条件を制御します。</param>
    private void RetainCompletedHighlightsForRunningElement(int runningElementCount) {
        if(runningElementCount<=0) {
            return;
        }

        foreach(var row in this.GetAllBenchmarkRows()) {
            row.RetainCompletedHighlightsForRunningElement(runningElementCount);
        }
    }

    /// <summary>
    /// ClearRunResultHighlights メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearRunResultHighlights() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.ClearRunHighlights();
        }
    }

    /// <summary>
    /// ClearBenchmarkResultDisplays メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearBenchmarkResultDisplays() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.ClearResultDisplay();
        }
        this.ClearFinalCompletionStates();
    }

    /// <summary>
    /// LoadSelectedRunFromDatabase メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="run">run を指定して実行条件を制御します。</param>
    private void LoadSelectedRunFromDatabase(BenchmarkRunOption run) {
        var requestToken = this.StartRunHistoryLoad();
        this.ClearSelectedRunDisplay();
        Task.Run(() => {
            try {
                this.uiRunRepository.FlushAsync().GetAwaiter().GetResult();
                var items = this.uiRunRepository.LoadBenchmarkSnapshotItems(run.UiRunId);
                return (Items: (IReadOnlyList<BenchmarkGridSnapshotItem>?)items,Error: (Exception?)null);
            }
            catch(Exception ex) {
                return (Items: (IReadOnlyList<BenchmarkGridSnapshotItem>?)null,Error: ex);
            }
        }).ContinueWith(task => {
            this.uiDispatcher.BeginInvoke(() => {
                if(requestToken!=this.selectedRunLoadRequestToken) {
                    return; // より新しい Run/タブ選択で上書きされたため破棄する。
                }
                try {
                    if(task.Result.Error is { } ex||task.Result.Items is not { } results) {
                        StartupDiagnostics.Write("Benchmark run DB load failed.",task.Result.Error);
                        this.StatusText="DB 読み込み失敗 "+(task.Result.Error?.Message??"unknown");
                        return;
                    }
                    this.ApplyBenchmarkSelectionsFromProgressState(this.LoadSelectedRunState());
                    this.ApplyLoadedBenchmarkSnapshotItems(results);
                }
                finally {
                    this.CompleteRunHistoryLoad(requestToken);
                }
            });
        },TaskScheduler.Default);
    }

    /// <summary>
    /// ApplyLoadedRunResultItems メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="results">results を指定して実行条件を制御します。</param>
    private void ApplyLoadedRunResultItems(IReadOnlyList<BenchmarkResultItem> results) {
        using(this.BeginChartRefreshBatch())
        using(this.BeginDataGridUpdateBatch()) {
            this.ResultItems.Clear();
            this.VisibleResultItems.Clear();
            this.currentRunSession.Clear();
            foreach(var result in results) {
                this.ResultItems.Add(result);
            }

            var loadedElementCounts = this.ResultItems
                .Select(static item => item.AxisCount)
                .Where(static item => item>0)
                .Distinct()
                .Order()
                .ToArray();
            this.isApplyingResultElementCount=true;
            try {
                this.RebuildResultElementCounts(loadedElementCounts);
                if(this.SelectedResultElementCount is null
                    ||loadedElementCounts.Length>0&&!loadedElementCounts.Contains(this.SelectedResultElementCount.Value)) {
                    this.SelectedResultElementCount=this.ResultElementCountOptions.LastOrDefault();
                }
            }
            finally {
                this.isApplyingResultElementCount=false;
            }
            this.RefreshVisibleResults();
            this.RefreshAllComparisonRowsFromLoadedResultItems(this.ResultItems.ToArray());
            this.ApplyComparisonRowsToAllTables();
            this.ApplySelectedElementCountToTable();
            this.ApplySelectedDisplayResultsToTable();
            this.RefreshFinalCompletionStates();
            this.RefreshSourceTabProgresses();
            this.RefreshImprovementPrompt();
            this.UpdateAllSourceTabCharts(false);
            this.ApplyCurrentSort();
        }
    }

    // 保存値は全タブ/全行のグリッド表示として復元する。再実行可能なケースだけは、
    // タブ・表示型・メソッド・専用引数列を使って現在のカタログへ一意に接続する。
    // 測定値、CPU、日時はDB行を正とし、カタログから推測補完しない。
    private void ApplyLoadedBenchmarkSnapshotItems(IReadOnlyList<BenchmarkGridSnapshotItem> items) {
        var displayItems = items.ToArray();
        this.runResultGeneration=unchecked(this.runResultGeneration+1);
        this.comparisonRowsRequestToken=unchecked(this.comparisonRowsRequestToken+1);
        this.comparisonRowsRefreshPending=false;
        using(this.BeginChartRefreshBatch())
        using(this.BeginDataGridUpdateBatch()) {
            this.ResultItems.Clear();
            this.VisibleResultItems.Clear();
            this.currentRunSession.Clear();
            this.ClearSourceTabRunningStates();
            this.ClearAllSourceTabComparisonDisplays();
            this.isApplyingBenchmarkSelection=true;
            try {
                foreach(var state in this.sourceTabViewStates) {
                    var stateItems=displayItems
                        .Where(item => string.Equals(item.SourceTabId,state.Definition.Id,StringComparison.OrdinalIgnoreCase))
                        .OrderBy(static item => item.Order)
                        .ToArray();
                    var rows=this.CreateSnapshotRows(state.Definition,stateItems).ToArray();
                    state.Rows.ReplaceRange(rows);
                    for(var index=0;index<rows.Length;index++) {
                        var row=rows[index];
                        var item=stateItems[index];
                        this.SubscribeBenchmarkRow(row);
                        this.ResultItems.Add(this.CreateLoadedSnapshotResultItem(state.Definition,item,state.Definition.LeftProject,item.Left));
                        this.ResultItems.Add(this.CreateLoadedSnapshotResultItem(state.Definition,item,state.Definition.RightProject,item.Right));
                        this.ApplyLoadedBenchmarkSnapshotSide(row,state.Definition.LeftProject,item.CpuNumber,item.Left);
                        this.ApplyLoadedBenchmarkSnapshotSide(row,state.Definition.RightProject,item.CpuNumber,item.Right);
                    }
                }

                this.RebuildBenchmarkRowIndex();
                foreach(var item in displayItems) {
                    if(BenchmarkSourceTabs.TryGetById(item.SourceTabId) is null) {
                        this.LogSnapshotDiagnostic(item,"SourceTabId を解決できません。");
                    }
                }

            }
            finally {
                this.isApplyingBenchmarkSelection=false;
            }

            var loadedElementCounts = this.ResultItems
                .Select(static item => item.AxisCount)
                .Where(static item => item>0)
                .Distinct()
                .Order()
                .ToArray();
            // RebuildResultElementCounts は SelectedResultElementCount を更新する。
            // 保存Runロード中に通常の選択軸変更処理が動くと、非同期の比較行再反映が
            // ClearComparison(false) を通り、直前に復元したスナップショットセルを消す。
            this.isApplyingResultElementCount=true;
            try {
                this.RebuildResultElementCounts(loadedElementCounts);
                if(this.SelectedResultElementCount is null
                    ||loadedElementCounts.Length>0&&!loadedElementCounts.Contains(this.SelectedResultElementCount.Value)) {
                    this.SelectedResultElementCount=this.ResultElementCountOptions.LastOrDefault();
                }
            }
            finally {
                this.isApplyingResultElementCount=false;
            }
            this.RefreshVisibleResults();
            this.RefreshAllComparisonRowsFromLoadedResultItems(this.ResultItems.ToArray());
            this.RefreshSourceTabProgresses();
            this.RefreshImprovementPrompt();
            this.UpdateAllSourceTabCharts(false);
            this.ApplyCurrentSort();
        }
        // チャート/進捗/ソートなどの通常UI更新後、スナップショットのセル値を最後に再適用する。
        // 保存Run表示ではこのDB行単位の復元が正であり、比較表示用の行再計算は正規ロード経路ではない。
        this.isApplyingBenchmarkSelection=true;
        try {
            this.ApplyBenchmarkSnapshotItemsToRows();
            this.ApplyBenchmarkSnapshotFinalCompletionStates();
            this.LogLoadedSnapshotGridDisplayGaps();
        }
        finally {
            this.isApplyingBenchmarkSelection=false;
        }
        if(this.BenchmarkRows.Count==0) {
            var message = "Benchmark saved run snapshot load produced no rows for selected tab "
                +this.SelectedSourceDefinition.Id
                +" items="
                +items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" displayItems="
                +displayItems.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" run="
                +(this.SelectedRunOption?.UiRunId.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-");
            StartupDiagnostics.Write(message);
            this.StatusText="DB 読み込み失敗 表示行が空です";
        }
    }

    private void LogLoadedSnapshotGridDisplayGaps() {
        foreach(var (item,row,sourceTab) in this.EnumerateSnapshotBindings()) {
            this.LogLoadedSnapshotGridDisplayGap(item,row,sourceTab.LeftProject,item.Left,row.LeftCell);
            this.LogLoadedSnapshotGridDisplayGap(item,row,sourceTab.RightProject,item.Right,row.RightCell);
        }
    }

    private void LogLoadedSnapshotGridDisplayGap(
        BenchmarkGridSnapshotItem item,
        BenchmarkTableRow row,
        string project,
        BenchmarkGridSnapshotSide side,
        BenchmarkProjectCell cell) {
        if(side.MeanNanoseconds is null||cell.Metrics.HasMeasuredValue) {
            return;
        }

        StartupDiagnostics.Write(
            "Benchmark saved run snapshot metric was not reflected to grid cell."
            +" RunId="+item.RunId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" SourceTabId="+item.SourceTabId
            +" Project="+project
            +" Type="+item.TypeName
            +" Benchmark="+item.BenchmarkName
            +" RowKey="+row.RowKey
            +" SnapshotMeanNs="+side.MeanNanoseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private BenchmarkResultItem CreateLoadedSnapshotResultItem(
        BenchmarkSourceTabDefinition sourceTab,
        BenchmarkGridSnapshotItem item,
        string project,
        BenchmarkGridSnapshotSide side) {
        return CreateSnapshotResultItem(
            sourceTab,
            project,
            string.Equals(project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase)
                ? sourceTab.LeftAssemblyName
                : sourceTab.RightAssemblyName,
            item.TypeName,
            side.Benchmark?.MethodName??item.BenchmarkName,
            side.Benchmark?.ArgumentKey??string.Empty,
            side.Benchmark?.Arguments??[],
            side.Status,
            side.ResultItem.ElementCount>0 ? side.ResultItem.ElementCount : (int?)null,
            ResolveSnapshotSideCpuNumber(item.CpuNumber,side),
            side.MeasuredAt,
            side.MeanNanoseconds,
            side.AllocatedBytes,
            side.FailureReason);
    }

    private void ApplyBenchmarkSnapshotItemsToRows() {
        foreach(var (item,row,sourceTab) in this.EnumerateSnapshotBindings()) {
            this.ApplyLoadedBenchmarkSnapshotSide(row,sourceTab.LeftProject,item.CpuNumber,item.Left);
            this.ApplyLoadedBenchmarkSnapshotSide(row,sourceTab.RightProject,item.CpuNumber,item.Right);
        }
    }

    private static void ApplyRowSelectionToCells(BenchmarkTableRow row,bool selected) {
        row.IsSelected=selected;
        foreach(var cell in row.ProjectCells) {
            cell.IsSelected=cell.HasCase&&selected;
        }
        row.RefreshSelectionState();
    }

    private static void SetRowUiSelection(BenchmarkTableRow row,bool selected) {
        foreach(var cell in row.ProjectCells) {
            cell.IsUiSelected=cell.HasCase&&selected;
        }
        row.RefreshUiSelectionState();
    }

    private void ApplyBenchmarkSnapshotFinalCompletionStates() {
        foreach(var (item,row,sourceTab) in this.EnumerateSnapshotBindings()) {
            var measurements=new[] {
                CreateSnapshotMeasurement(item.Left.ResultItem),
                CreateSnapshotMeasurement(item.Right.ResultItem),
            };
            var leftCell=CreateProgressCell(row,sourceTab.LeftProject,true);
            var rightCell=CreateProgressCell(row,sourceTab.RightProject,true);
            var thresholdNanoseconds=this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
            var isFinalCompleted=BenchmarkWorkloadAxis.UsesLoopCount(sourceTab.Tab)
                ? BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(measurements,leftCell,rightCell,thresholdNanoseconds)
                : BenchmarkRowCompletionLogic.IsFinalCompleted(
                    measurements,
                    leftCell,
                    rightCell,
                    Math.Max(item.Left.ResultItem.AxisCount,item.Right.ResultItem.AxisCount),
                    thresholdNanoseconds);
            row.SetFinalCompleted(item.IsSelected&&isFinalCompleted);
        }
    }

    private IEnumerable<(BenchmarkGridSnapshotItem Item,BenchmarkTableRow Row,BenchmarkSourceTabDefinition SourceTab)> EnumerateSnapshotBindings() {
        foreach(var sourceTab in BenchmarkSourceTabs.All) {
            var state=this.GetSourceTabViewState(sourceTab.Tab);
            foreach(var row in state.Rows) {
                if(row.LoadedSnapshot is { } item) {
                    yield return (item,row,sourceTab);
                }
            }
        }
    }

    private static BenchmarkElementMeasurement CreateSnapshotMeasurement(BenchmarkResultItem result) =>
        new(
            result.Project,
            result.Method,
            result.TypeFullName,
            result.ArgumentKey,
            result.AxisCount,
            result.Status,
            result.MeanNanoseconds,
            result.TotalNanoseconds);

    private IEnumerable<BenchmarkTableRow> CreateSnapshotRows(BenchmarkSourceTabDefinition sourceTab,IReadOnlyList<BenchmarkGridSnapshotItem> items) {
        return items
            .Where(item => string.Equals(item.SourceTabId,sourceTab.Id,StringComparison.OrdinalIgnoreCase))
            .OrderBy(static item => item.Order)
            .Select(item => {
                var cases = new Dictionary<string,BenchmarkCase>(StringComparer.OrdinalIgnoreCase);
                if(this.ResolveCurrentSnapshotCase(sourceTab,sourceTab.LeftProject,item,item.Left.Benchmark) is { } leftBenchmark){
                    cases[sourceTab.LeftProject]=leftBenchmark;
                }
                if(this.ResolveCurrentSnapshotCase(sourceTab,sourceTab.RightProject,item,item.Right.Benchmark) is { } rightBenchmark){
                    cases[sourceTab.RightProject]=rightBenchmark;
                }
                var benchmarkName=cases.Values.FirstOrDefault() is { } currentCase
                    ? ScalableBenchmarkNameFormatter.CreateBenchmarkName(sourceTab,currentCase.TypeFullName,item.BenchmarkName,0)
                    : item.BenchmarkName;
                return new BenchmarkTableRow(
                    CreateSnapshotRowKey(item.TypeName,item.BenchmarkName,SnapshotArgumentKey(item)),
                    item.BenchmarkName,
                    benchmarkName,
                    sourceTab,
                    cases,
                    item.TypeName){
                    LoadedSnapshot=item,
                };
            });
    }

    private BenchmarkCase? ResolveCurrentSnapshotCase(
        BenchmarkSourceTabDefinition sourceTab,
        string project,
        BenchmarkGridSnapshotItem item,
        BenchmarkCase? storedCase){
        var argumentKey=storedCase?.ArgumentKey??string.Empty;
        var candidates=this.benchmarkCasesByProjectMethod.Values
            .Where(candidate=>
                string.Equals(candidate.Project,project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(candidate.AssemblyName,sourceTab.GetAssemblyName(project),StringComparison.OrdinalIgnoreCase)
                &&string.Equals(candidate.MethodName,item.BenchmarkName,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(candidate.ArgumentKey,argumentKey,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(ScalableBenchmarkNameFormatter.CreateGenericTypeText(candidate.TypeFullName),item.TypeName,StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(2)
            .ToArray();
        return candidates.Length==1 ? candidates[0] : null;
    }

    private void ApplyLoadedBenchmarkSnapshotSide(
        BenchmarkTableRow row,
        string project,
        int? cpuNumber,
        BenchmarkGridSnapshotSide side) {
        var cell = row.GetProjectCell(project);
        if(cell is null) {
            return;
        }

        var sideCpuNumber = ResolveSnapshotSideCpuNumber(cpuNumber,side);
        row.ApplySnapshotResult(project,side.ResultItem,this.FindSnapshotCpu(sideCpuNumber),sideCpuNumber,side.MeasuredAt);
    }

    private static int? ResolveSnapshotSideCpuNumber(int? rowCpuNumber,BenchmarkGridSnapshotSide side) =>
        side.ResultItem.AssignedLogicalCpu??rowCpuNumber;

    private CpuSelectionItem? FindSnapshotCpu(int? cpuNumber) =>
        cpuNumber is { } value
            ? this.CpuItems.FirstOrDefault(item => item.LogicalProcessorIndex==value)
            : null;

    private static string SnapshotArgumentKey(BenchmarkGridSnapshotItem item) =>
        item.Left.Benchmark?.ArgumentKey??item.Right.Benchmark?.ArgumentKey??string.Empty;

    private static string CreateSnapshotRowKey(string typeName,string benchmarkName,string argumentKey) =>
        typeName+"|"+benchmarkName+"|"+argumentKey;

    private void LogSnapshotDiagnostic(BenchmarkGridSnapshotItem item,string reason) {
        StartupDiagnostics.Write(
            "Benchmark snapshot load diagnostic: "+reason
            +" RunId="+item.RunId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +" SourceTabId="+item.SourceTabId
            +" Type="+item.TypeName
            +" Benchmark="+item.BenchmarkName);
    }

    /// <summary>
    /// LoadSelectedRunFromDatabase メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void LoadSelectedRunFromDatabase() {
        if(this.isLoadingSelectedRunDb) {
            return;
        }

        this.isLoadingSelectedRunDb=true;
        try {
            using(this.BeginChartRefreshBatch())
            using(this.BeginDataGridUpdateBatch()) {
                var run = this.SelectedRunOption;
                var isSavedDatabaseRun = IsSavedDatabaseRunOption(run);
                if(!isSavedDatabaseRun&&this.RestoreCurrentRunDisplayForSelectedTab()) {
                    return;
                }

                if(!isSavedDatabaseRun&&this.ShouldKeepCurrentRunMemoryDisplay()) {
                    return;
                }

                if(run is null||IsNewRunOption(run)) {
                    this.ClearSelectedRunDisplay();
                    return;
                }

                if(IsTransientRunOption(run)||run.UiRunId<=0) {
                    this.ClearSelectedRunDisplay();
                    return;
                }

                var selectedRunState = this.LoadSelectedRunState();
                this.ApplyBenchmarkSelectionsFromProgressState(selectedRunState);
                var selectedN = this.SelectedResultElementCount?.Value
                    ??FindLatestTerminalElementCount(selectedRunState,this.SelectedSourceDefinition)
                    ??run.ElementCounts.LastOrDefault();
                if(selectedN<=0) {
                    this.ClearSelectedRunDisplay();
                    return;
                }

                this.SelectResultElementCountWithoutReload(selectedN);
                this.ClearBenchmarkResultDisplays();
                this.comparisonRows= [];
                this.ResultItems.Clear();
                this.VisibleResultItems.Clear();

                this.ApplyComparisonRowsToTable();

                this.ApplySelectedRunStateJobs(selectedRunState,selectedN);
                this.RefreshResultChecks(this.SelectedSourceDefinition.Tab);
                this.ApplySkippedCells(selectedN);
                this.RefreshVisibleResults();
                this.ApplyCurrentSort();
                this.RefreshImprovementPrompt();
                this.UpdateChart(false);
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run DB load failed.",ex);
            this.StatusText="DB 読み込み失敗 "+ex.Message;
        }
        finally {
            this.isLoadingSelectedRunDb=false;
            this.RefreshToolbarCommandStates();
        }
    }

    /// <summary>
    /// SelectResultElementCountWithoutReload メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    private void SelectResultElementCountWithoutReload(int elementCount) {
        var option = this.ResultElementCountOptions.FirstOrDefault(item => item.Value==elementCount);
        if(option is null) {
            option=new ResultElementCountOption(elementCount,this.FormatResultElementCountDisplayName(elementCount));
            var ordered = this.ResultElementCountOptions
                .Append(option)
                .OrderBy(static item => item.Value)
                .ToArray();
            this.ResultElementCountOptions.Clear();
            foreach(var item in ordered) {
                this.ResultElementCountOptions.Add(item);
            }
            this.OnPropertyChanged(nameof(this.ChartElementCountSliderMaximum));
            this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
        }

        this.isApplyingResultElementCount=true;
        try {
            this.SelectedResultElementCount=option;
        }
        finally {
            this.isApplyingResultElementCount=false;
        }
    }

    /// <summary>
    /// FindLatestTerminalElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static int? FindLatestTerminalElementCount(ProgressState? state,BenchmarkSourceTabDefinition sourceTab) {
        return state?.Jobs
            .Where(job => BenchmarkWorkloadAxis.GetAxisCount(job)>0&&IsTerminalJob(job)&&sourceTab.ContainsProject(job.Project))
            .OrderBy(static job => job.FinishedAt??job.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static job => BenchmarkWorkloadAxis.GetAxisCount(job))
            .LastOrDefault()
            is { } job ? BenchmarkWorkloadAxis.GetAxisCount(job) : null;
    }

    /// <summary>
    /// ApplySelectedRunStateJobs メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    private void ApplySelectedRunStateJobs(ProgressState? state,int elementCount) {
        if(state is null) {
            return;
        }

        var jobs = state.Jobs
            .Where(job => this.SelectedSourceDefinition.ContainsProject(job.Project)
                &&IsTerminalJob(job))
            .GroupBy(static job => BenchmarkRowGroupingLogic.CreateCaseKey(job.Project,job.TypeFullName,job.MethodName,job.ArgumentKey),StringComparer.OrdinalIgnoreCase)
            .SelectMany(static group => group
                .GroupBy(static job => BenchmarkWorkloadAxis.GetAxisCount(job))
                .Select(static elementGroup => elementGroup
                    .OrderBy(static job => job.FinishedAt??job.StartedAt??DateTimeOffset.MinValue)
                    .Last()))
            .ToArray();
        foreach(var job in jobs) {
            var item = new BenchmarkResultItem(job);
            this.UpsertResultItem(item);
            if(BenchmarkWorkloadAxis.GetAxisCount(job)!=elementCount) {
                continue;
            }

            var cpu = this.FindAssignedCpu(item);
            var completedAt = job.FinishedAt??job.StartedAt??DateTimeOffset.Now;
            var row = this.FindBenchmarkRow(job);
            row?.ApplyRunResult(item,cpu,completedAt,false);
        }
        this.RefreshSourceTabProgresses();
    }

    /// <summary>
    /// IsTerminalJob メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="job">job を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsTerminalJob(BenchmarkJob job) =>
        job.Status is JobStatus.Completed or JobStatus.Failed;

    /// <summary>
    /// CreateNewRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="executionProfile">executionProfile を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkRunOption CreateNewRunOption(BenchmarkExecutionProfile executionProfile) =>
        NewRunOption with {
            ExecutionProfile=executionProfile,
        };

    /// <summary>
    /// IsNewRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsNewRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber==NewRunNumber;

    /// <summary>
    /// IsUnsavedRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsUnsavedRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber==UnsavedRunNumber;

    /// <summary>
    /// IsExternalRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsExternalRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber==ExternalRunNumber;

    /// <summary>
    /// IsTransientRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsTransientRunOption(BenchmarkRunOption? option) =>
        IsUnsavedRunOption(option)||IsExternalRunOption(option);

    private static bool IsSavedDatabaseRunOption(BenchmarkRunOption? option) =>
        option is not null&&!IsNewRunOption(option)&&!IsTransientRunOption(option)&&option.UiRunId>0;

    /// <summary>
    /// ShouldKeepCurrentRunMemoryDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool ShouldKeepCurrentRunMemoryDisplay() =>
        BenchmarkRunDisplaySourceLogic.ShouldKeepCurrentRunMemoryDisplay(
            this.currentRunSession.HasAnyResults);

    /// <summary>
    /// ShouldLoadSelectedRunFromDatabaseForDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private bool ShouldLoadSelectedRunFromDatabaseForDisplay() =>
        BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
            this.HasCurrentRunResultsForSelectedTab,
            this.currentRunSession.HasAnyResults,
            this.IsBenchmarkRunActive,
            this.isLoadingSelectedRunDb,
            IsExternalRunOption(this.SelectedRunOption),
            IsNewRunOption(this.SelectedRunOption),
            IsTransientRunOption(this.SelectedRunOption),
            IsSavedDatabaseRunOption(this.SelectedRunOption));

    /// <summary>
    /// CreateUnsavedRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <param name="elementCounts">elementCounts を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkRunOption CreateUnsavedRunOption(string runRoot,IReadOnlyList<int> elementCounts) =>
        new(
            UnsavedRunNumber,
            this.activeUiRunId??0,
            "未保存",
            runRoot,
            this.SelectedExecutionProfile.Profile,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            this.currentRunSession.CountCompleted(),
            this.currentRunSession.Count,
            elementCounts);

    /// <summary>
    /// CreateExternalRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="displayName">displayName を指定して実行条件を制御します。</param>
    /// <param name="runRoot">runRoot を指定して実行条件を制御します。</param>
    /// <param name="elementCounts">elementCounts を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkRunOption CreateExternalRunOption(string displayName,string runRoot,IReadOnlyList<int> elementCounts) =>
        new(
            ExternalRunNumber,
            0,
            displayName,
            runRoot,
            this.SelectedExecutionProfile.Profile,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            this.ResultItems.Count(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)),
            this.ResultItems.Count,
            elementCounts);

    /// <summary>
    /// ApplySelectedElementCountToTable メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplySelectedElementCountToTable() {
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.RefreshBenchmarkName(selectedN??0);
        }
        if(!this.IsBenchmarkRunActive&&this.ResultItems.Count==0&&this.ShouldLoadSelectedRunFromDatabaseForDisplay()) {
            this.LoadSelectedRunFromDatabase();
            return;
        }
        this.RefreshAllComparisonRowsFromDisplayResults();
        this.ApplyComparisonRowsToAllTables();
        if(selectedN is { } skippedElementCount) {
            this.ApplySkippedCells(skippedElementCount);
        }
        this.RefreshFinalCompletionStates();
        this.RefreshBenchmarkSelectionAggregates();
    }

    /// <summary>
    /// BuildCurrentRunComparisonRowsForElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRowsForElementCount(int elementCount) {
        return this.BuildCurrentRunComparisonRowsForElementCount(this.SelectedSourceDefinition,elementCount);
    }

    /// <summary>
    /// BuildCurrentRunComparisonRowsForElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRowsForElementCount(BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        return this.runProjectionService.BuildComparisonRowsForElementCount(
            this.GetCurrentRunResultItems(sourceTab.Tab),
            sourceTab,
            elementCount,
            this.CreateReportRowsForComparison);
    }

    /// <summary>
    /// BuildCurrentRunComparisonRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRows() {
        return this.BuildCurrentRunComparisonRows(this.SelectedSourceDefinition);
    }

    /// <summary>
    /// BuildCurrentRunComparisonRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRows(BenchmarkSourceTabDefinition sourceTab) {
        return this.runProjectionService.BuildLatestComparisonRows(
            this.GetCurrentRunResultItems(sourceTab.Tab),
            sourceTab,
            this.CreateReportRowsForComparison);
    }

    /// <summary>
    /// BuildComparisonRowsForElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRowsForElementCount(int elementCount) {
        return this.BuildComparisonRowsForElementCount(this.SelectedSourceDefinition,elementCount);
    }

    /// <summary>
    /// BuildComparisonRowsForElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRowsForElementCount(BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        var currentRunResults = this.GetCurrentRunResultItems(sourceTab.Tab);
        var sourceResults = currentRunResults
            .Where(result => result.AxisCount==elementCount
                &&result.AxisCount>0
                &&sourceTab.ContainsProject(result.Project)
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = this.CreateReportRowsForComparison(sourceTab,sourceResults);
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,sourceTab,elementCount);
    }

    /// <summary>
    /// BuildDisplayedRowsForElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRowsForElementCount(BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        return this.runProjectionService.BuildDisplayedRowsForSelectedElementCount(
            this.ResultItems.ToArray(),
            sourceTab,
            elementCount,
            this.CreateReportRowsForComparison);
    }

    /// <summary>
    /// BuildDisplayedRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRows(BenchmarkSourceTabDefinition sourceTab) {
        return this.runProjectionService.BuildDisplayedRows(
            this.ResultItems,
            sourceTab,
            this.CreateReportRowsForComparison);
    }

    /// <summary>
    /// ApplySkippedCells メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="selectedElementCount">selectedElementCount を指定して実行条件を制御します。</param>
    private void ApplySkippedCells(int selectedElementCount) {
        var stopwatch = Stopwatch.StartNew();
        var displayResults = this.GetResultSourceForDisplay();
        var measurements = this.CreateDisplayMeasurements(displayResults);
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        foreach(var row in this.BenchmarkRows) {
            foreach(var pair in this.BenchmarkPairs()) {
                var left = row.GetProjectCell(pair.LeftProject);
                var right = row.GetProjectCell(pair.RightProject);
                if(left is not { HasCase: true }||right is not { HasCase: true }) {
                    continue;
                }
                if(!left.IsSelected&&!right.IsSelected) {
                    continue;
                }

                var leftTypeFullName = GetRowTypeFullName(row,pair.LeftProject);
                var leftArgumentKey = GetRowArgumentKey(row,pair.LeftProject);
                var rightTypeFullName = GetRowTypeFullName(row,pair.RightProject);
                var rightArgumentKey = GetRowArgumentKey(row,pair.RightProject);
                var hasLeftResult = this.HasResult(displayResults,row.MethodName,pair.LeftProject,leftTypeFullName,selectedElementCount,leftArgumentKey);
                var hasRightResult = this.HasResult(displayResults,row.MethodName,pair.RightProject,rightTypeFullName,selectedElementCount,rightArgumentKey);
                if(hasLeftResult&&hasRightResult) {
                    continue;
                }

                var leftProgressCell = CreateProgressCell(row,pair.LeftProject,left.IsSelected);
                var rightProgressCell = CreateProgressCell(row,pair.RightProject,right.IsSelected);
                var progressCells = new[] { leftProgressCell,rightProgressCell };
                if(BenchmarkPairCompletionLogic.HasSelectedPairThresholdCompletion(
                    measurements,
                    progressCells,
                    leftProgressCell,
                    pair.LeftProject,
                    pair.RightProject,
                    thresholdNanoseconds)) {
                    continue;
                }

                var stoppedAt = BenchmarkSkippedDisplayLogic.FindPairStoppedAt(
                        measurements,
                        leftProgressCell,
                        rightProgressCell,
                        selectedElementCount,
                        thresholdNanoseconds)
                    ??BenchmarkSkippedDisplayLogic.FindLatestCompletedPairElementCountBefore(
                        measurements,
                        leftProgressCell,
                        rightProgressCell,
                        selectedElementCount);
                if(stoppedAt is { } stoppedAtElementCount) {
                    row.ApplyPairStopped(
                        pair.LeftProject,
                        pair.RightProject,
                        stoppedAtElementCount,
                        this.IsBenchmarkRunActive&&this.HasCurrentRunResultsForSelectedTab);
                    if(left.IsSelected&&!hasLeftResult) {
                        this.ApplySkippedCell(displayResults,row,pair.LeftProject,stoppedAtElementCount);
                    }
                    if(right.IsSelected&&!hasRightResult) {
                        this.ApplySkippedCell(displayResults,row,pair.RightProject,stoppedAtElementCount);
                    }
                }
            }
        }

        UiThreadBudget.Record("skipped-cells",stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// finalCompletionRequestToken フィールドです。
    /// </summary>
    private int finalCompletionRequestToken;
    /// <summary>
    /// finalCompletionRefreshInFlight フィールドです。
    /// </summary>
    private bool finalCompletionRefreshInFlight;
    /// <summary>
    /// finalCompletionRefreshPending フィールドです。
    /// </summary>
    private bool finalCompletionRefreshPending;
    /// <summary>
    /// lastFinalCompletionDispatchAt フィールドです。
    /// </summary>
    private DateTimeOffset lastFinalCompletionDispatchAt = DateTimeOffset.MinValue;
    /// <summary>
    /// FinalCompletionDispatchInterval フィールドです。
    /// </summary>
    private static readonly TimeSpan FinalCompletionDispatchInterval = TimeSpan.FromMilliseconds(500);

    // 完了判定は O(行数×measurements件数) で実行が進むほど重くなる(観測: 1tickで700〜1300ms)。
    // UIスレッドに残すのは「生の結果配列の確保(参照コピー)」と「SetFinalCompleted反映」のみで、
    // 選択フィルタ(IsResultSelectedForDisplay)・measurement射影・判定計算は全てバックグラウンド。
    // 行の RowKey/MethodName/CasesByProject/ProjectCells はコンストラクタ後不変、cell.IsSelected は
    // boolのatomic読みなので、バックグラウンドから読んでも安全(Phase Bの比較行構築と同じ前提)。
    private readonly record struct FinalCompletionTabInput(
        SourceTabOption Option,
        BenchmarkSourceTabViewState State,
        IReadOnlyList<BenchmarkResultItem> Source,
        BenchmarkTableRow[] Rows,
        IReadOnlyList<(string LeftProject,string RightProject)> Pairs,
        int MaxElementCount);

    private readonly record struct FinalCompletionTabResult(
        SourceTabOption Option,
        BenchmarkTableRow[] Rows,
        bool[]? Completed,
        bool IsFinalCompleted);

    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void RefreshFinalCompletionStates() {
        if(this.finalCompletionRefreshInFlight) {
            this.finalCompletionRefreshPending=true;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if(this.IsBenchmarkRunActive&&now-this.lastFinalCompletionDispatchAt<FinalCompletionDispatchInterval) {
            // 実行中はRefreshChart(1秒毎)で再度呼ばれるため、スキップしても収束する。
            return;
        }

        this.lastFinalCompletionDispatchAt=now;
        var resultItemsSnapshot = this.ResultItems.ToArray();
        var tabInputs = this.SourceTabs
            .Select(option => {
                var state = this.GetSourceTabViewState(option.Definition.Tab);
                var source = this.ShouldUseCurrentRunMemoryForDisplay(state.Definition)
                    ? this.GetCurrentRunResultItems(state.Definition.Tab).ToArray()
                    : resultItemsSnapshot;
                return new FinalCompletionTabInput(
                option,
                state,
                source,
                state.Rows.ToArray(),
                CreateBenchmarkPairs(state.Definition),
                this.GetFinalCompletionMaxElementCount(state.Definition,source));
            })
            .ToArray();
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        var token = ++this.finalCompletionRequestToken;
        var generation = this.runResultGeneration;
        this.finalCompletionRefreshInFlight=true;
        var stopwatch = Stopwatch.StartNew();
        Task.Run(() => tabInputs
            .Select(input => {
                var measurements = this.CreateDisplayMeasurementsFrom(input.Source,input.State.Definition);
                if(measurements.Length==0){
                    return new FinalCompletionTabResult(input.Option,input.Rows,null,false);
                }

                var completed = ComputeFinalCompletionStates(measurements,input.Rows,input.Pairs,input.MaxElementCount,thresholdNanoseconds);
                return new FinalCompletionTabResult(
                    input.Option,
                    input.Rows,
                    completed,
                    IsTabFinalCompleted(measurements,input.Rows,input.MaxElementCount,thresholdNanoseconds));
            })
            .ToArray())
            .ContinueWith(task => {
                UiThreadBudget.Record("bg:completion",stopwatch.Elapsed.TotalMilliseconds);
                this.uiDispatcher.BeginInvoke(() => {
                    this.finalCompletionRefreshInFlight=false;
                    if(token==this.finalCompletionRequestToken&&generation==this.runResultGeneration&&!task.IsFaulted) {
                        var completedTabs = task.Result;
                        UiThreadBudget.Measure("completion-apply",() => {
                            foreach(var completedTab in completedTabs) {
                                completedTab.Option.SetFinalCompleted(completedTab.IsFinalCompleted);
                                if(completedTab.Completed is null) {
                                    foreach(var row in completedTab.Rows) {
                                        row.SetFinalCompleted(false);
                                    }
                                }
                                else {
                                    for(var index = 0; index<completedTab.Rows.Length; index++) {
                                        completedTab.Rows[index].SetFinalCompleted(completedTab.Completed[index]);
                                    }
                                }
                            }
                        });
                    }
                    else if(task.IsFaulted) {
                        StartupDiagnostics.Write("Background RefreshFinalCompletionStates failed.",task.Exception);
                    }

                    if(this.finalCompletionRefreshPending) {
                        this.finalCompletionRefreshPending=false;
                        this.RefreshFinalCompletionStates();
                    }
                });
            },TaskScheduler.Default);
    }

    private void ApplySourceTabProgressesSynchronously() {
        _=unchecked(++this.sourceTabProgressRequestToken);
        this.sourceTabProgressRefreshPending=false;
        var resultItemsSnapshot = this.ResultItems.ToArray();
        var stateJobsSnapshot = this.currentRunSession.HasAnyResults
            ? null
            : this.LoadSelectedRunState()?.Jobs.ToArray();
        var maxElementCount = this.GetSelectedElementCounts().LastOrDefault();
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        var progresses = this.SourceTabs
            .Select(option => {
                var measured = this.CountMeasuredSelectedCells(option.Definition);
                return new {
                    Option = option,
                    Progress = CalculateSourceTabProgress(
                        this.CreateProgressCells(option.Definition),
                        BuildProgressMeasurements(
                            this.GetCurrentRunResultItems(option.Definition.Tab),
                            resultItemsSnapshot,
                            stateJobsSnapshot,
                            option.Definition.Tab),
                        option.Definition.LeftProject,
                        option.Definition.RightProject,
                        maxElementCount,
                        thresholdNanoseconds,
                        measured.Measured,
                        measured.Selected),
                };
            })
            .ToArray();

        foreach(var item in progresses) {
            item.Option.SetProgress(item.Progress);
        }
        this.OverallSourceTabProgress=new BenchmarkTabProgress(
            progresses.Sum(static item => item.Progress.Completed),
            progresses.Sum(static item => item.Progress.Total));
    }

    private (int Measured,int Selected) CountMeasuredSelectedCells(BenchmarkSourceTabDefinition sourceTab) {
        var measured = 0;
        var selected = 0;
        foreach(var row in this.GetSourceTabViewState(sourceTab.Tab).Rows) {
            foreach(var cell in row.ProjectCells) {
                if(!cell.HasCase||!cell.IsSelected) {
                    continue;
                }

                selected++;
                if(cell.Metrics.HasMeasuredValue) {
                    measured++;
                }
            }
        }

        return (measured,selected);
    }

    private static BenchmarkTabProgress CalculateSourceTabProgress(
        IEnumerable<BenchmarkTabProgressCell> cells,
        IEnumerable<BenchmarkElementMeasurement> measurements,
        string leftProject,
        string rightProject,
        int maxElementCount,
        double thresholdNanoseconds,
        int measuredSelectedCells,
        int selectedCells) {
        var progress = BenchmarkTabProgressLogic.Calculate(
            cells,
            measurements,
            leftProject,
            rightProject,
            maxElementCount,
            thresholdNanoseconds);
        return ApplyMeasuredCellProgressFallback(progress,measuredSelectedCells,selectedCells);
    }

    private static BenchmarkTabProgress ApplyMeasuredCellProgressFallback(
        BenchmarkTabProgress progress,
        int measuredSelectedCells,
        int selectedCells) {
        if(selectedCells==0||measuredSelectedCells<=progress.Completed) {
            return progress;
        }

        var total = progress.Total>0 ? progress.Total : selectedCells;
        return new BenchmarkTabProgress(Math.Min(measuredSelectedCells,total),total);
    }

    private void ApplyFinalCompletionStatesSynchronously() {
        this.finalCompletionRefreshPending=false;
        _=unchecked(++this.finalCompletionRequestToken);
        var resultItemsSnapshot = this.ResultItems.ToArray();
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        var completedTabs = this.SourceTabs
            .Select(option => {
                var state = this.GetSourceTabViewState(option.Definition.Tab);
                var source = this.ShouldUseCurrentRunMemoryForDisplay(state.Definition)
                    ? this.GetCurrentRunResultItems(state.Definition.Tab).ToArray()
                    : resultItemsSnapshot;
                var rows = state.Rows.ToArray();
                var measurements = this.CreateDisplayMeasurementsFrom(source,state.Definition);
                if(measurements.Length==0) {
                    return new FinalCompletionTabResult(
                        option,
                        rows,
                        new bool[rows.Length],
                        false);
                }

                var pairs = CreateBenchmarkPairs(state.Definition);
                var maxElementCount = this.GetFinalCompletionMaxElementCount(state.Definition,source);
                var completed = ComputeFinalCompletionStates(measurements,rows,pairs,maxElementCount,thresholdNanoseconds);
                return new FinalCompletionTabResult(
                    option,
                    rows,
                    completed,
                    IsTabFinalCompleted(measurements,rows,maxElementCount,thresholdNanoseconds));
            })
            .ToArray();
        foreach(var completedTab in completedTabs) {
            completedTab.Option.SetFinalCompleted(completedTab.IsFinalCompleted);
            if(completedTab.Completed is null) {
                foreach(var row in completedTab.Rows) {
                    row.SetFinalCompleted(false);
                }
            }
            else {
                for(var index = 0; index<completedTab.Rows.Length; index++) {
                    completedTab.Rows[index].SetFinalCompleted(completedTab.Completed[index]);
                }
            }
        }
    }

    /// <summary>
    /// CreateDisplayMeasurementsFrom メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="source">source を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkElementMeasurement[] CreateDisplayMeasurementsFrom(IReadOnlyList<BenchmarkResultItem> source,BenchmarkSourceTabDefinition sourceTab) {
        var measurements = new List<BenchmarkElementMeasurement>(source.Count);
        foreach(var result in source) {
            if(!this.IsResultInSourceTab(result,sourceTab)) {
                continue;
            }

            measurements.Add(new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ArgumentKey,
                result.AxisCount,
                result.Status,
                result.MeanNanoseconds,
                result.TotalNanoseconds));
        }

        return measurements.ToArray();
    }

    private bool IsResultInSourceTab(BenchmarkResultItem result,BenchmarkSourceTabDefinition sourceTab) {
        if(!sourceTab.ContainsProject(result.Project)) {
            return false;
        }

        return this.FindBenchmarkRow(result) is not null;
    }

    private void ApplyBenchmarkSelectionsFromDatabaseRun(long runId) {
        this.isApplyingUiState=true;
        try {
            this.benchmarkRowSelections.Clear();

            foreach(var row in this.GetAllBenchmarkRows()) {
                this.ApplyStoredBenchmarkSelection(row);
            }
        }
        finally {
            this.isApplyingUiState=false;
            this.RefreshBenchmarkSelectionAggregates();
        }
    }

    /// <summary>
    /// ComputeFinalCompletionStates メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="measurements">measurements を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <param name="pairs">pairs を指定して実行条件を制御します。</param>
    /// <param name="maxElementCount">maxElementCount を指定して実行条件を制御します。</param>
    /// <param name="thresholdNanoseconds">thresholdNanoseconds を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool[] ComputeFinalCompletionStates(
        BenchmarkElementMeasurement[] measurements,
        BenchmarkTableRow[] rows,
        IReadOnlyList<(string LeftProject,string RightProject)> pairs,
        int maxElementCount,
        double thresholdNanoseconds) {
        var measurementsByMethod = measurements.ToLookup(static m => m.Method,StringComparer.OrdinalIgnoreCase);
        var results = new bool[rows.Length];
        for(var index = 0; index<rows.Length; index++) {
            var row = rows[index];
            var rowMeasurements = measurementsByMethod[row.MethodName].ToArray();
            results[index]=pairs.Any(pair => {
                var leftCell=CreateProgressCell(row,pair.LeftProject,true);
                var rightCell=CreateProgressCell(row,pair.RightProject,true);
                return BenchmarkWorkloadAxis.UsesLoopCount(row.SourceTab.Tab)
                    ? BenchmarkRowCompletionLogic.IsLoopCountFinalCompleted(rowMeasurements,leftCell,rightCell,thresholdNanoseconds)
                    : BenchmarkRowCompletionLogic.IsFinalCompleted(rowMeasurements,leftCell,rightCell,maxElementCount,thresholdNanoseconds);
            });
        }

        return results;
    }

    private static bool IsTabFinalCompleted(BenchmarkElementMeasurement[] measurements,BenchmarkTableRow[] rows,int maxElementCount,double thresholdNanoseconds) {
        var cells=rows.SelectMany(static row => row.ProjectCells
            .Where(static cell => cell.HasCase&&cell.IsSelected)
            .Select(cell => CreateProgressCell(row,cell.Project,true)));
        return rows.FirstOrDefault() is { } first&&BenchmarkWorkloadAxis.UsesLoopCount(first.SourceTab.Tab)
            ? BenchmarkRowCompletionLogic.IsLoopCountTabFinalCompleted(measurements,cells,thresholdNanoseconds)
            : BenchmarkRowCompletionLogic.IsTabFinalCompleted(measurements,cells,maxElementCount,thresholdNanoseconds);
    }

    /// <summary>
    /// CreateDisplayMeasurements メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkElementMeasurement[] CreateDisplayMeasurements() =>
        this.CreateDisplayMeasurements(this.GetResultSourceForDisplay());

    private BenchmarkElementMeasurement[] CreateDisplayMeasurements(IReadOnlyList<BenchmarkResultItem>results) =>
        results
            .Select(static result => new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ArgumentKey,
                result.AxisCount,
                result.Status,
                result.MeanNanoseconds,
                result.TotalNanoseconds))
            .ToArray();

    /// <summary>
    /// ClearFinalCompletionStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearFinalCompletionStates() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.SetFinalCompleted(false);
        }
    }

    /// <summary>
    /// GetFinalCompletionMaxElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private int GetFinalCompletionMaxElementCount() {
        return this.GetFinalCompletionMaxElementCount(this.SelectedSourceDefinition);
    }

    private int GetFinalCompletionMaxElementCount(BenchmarkSourceTabDefinition sourceTab,IReadOnlyList<BenchmarkResultItem>? source=null) {
        var sourceMax = source?
            .Where(result => sourceTab.ContainsProject(result.Project))
            .Select(static result => result.AxisCount)
            .Where(static item => item>0)
            .DefaultIfEmpty(0)
            .Max()??0;
        if(sourceMax>0) {
            return sourceMax;
        }

        if(this.IsBenchmarkRunActive||this.currentRunSession.HasResults(sourceTab.Tab)) {
            return this.GetSelectedElementCounts().LastOrDefault();
        }

        var runElementCounts = this.SelectedRunOption?.ElementCounts
            .Where(static item => item>0)
            .Order()
            .ToArray();
        return runElementCounts is { Length:>0 }
            ? runElementCounts[^1]
            : this.GetSelectedElementCounts().LastOrDefault();
    }

    /// <summary>
    /// ApplySkippedCell メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="stoppedAtElementCount">stoppedAtElementCount を指定して実行条件を制御します。</param>
    private void ApplySkippedCell(IReadOnlyList<BenchmarkResultItem>results,BenchmarkTableRow row,string project,int stoppedAtElementCount) {
        var typeFullName = GetRowTypeFullName(row,project);
        var argumentKey = GetRowArgumentKey(row,project);
        var result = this.GetResultItem(results,row.MethodName,project,typeFullName,stoppedAtElementCount,argumentKey);
        var cpu = result?.AssignedLogicalCpu is { } logicalCpu
            ? this.FindAssignedCpu(result)
            : null;
        var measuredAt = result?.StartedAt;
        var metadataMissing = result is null||cpu is null||measuredAt is null;
        if(metadataMissing) {
            this.ReportMissingBenchmarkMetadata(row.MethodName,project,stoppedAtElementCount,result);
        }

        row.ApplySkipped(
            project,
            stoppedAtElementCount,
            this.GetResultMetrics(results,row.MethodName,project,typeFullName,stoppedAtElementCount,argumentKey),
            cpu,
            measuredAt,
            metadataMissing);
    }

    /// <summary>
    /// IReadOnlyList< メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="LeftProject">LeftProject を指定して実行条件を制御します。</param>
    /// <param name="RightProject">RightProject を指定して実行条件を制御します。</param>
    private IReadOnlyList<(string LeftProject,string RightProject)> BenchmarkPairs() =>
        CreateBenchmarkPairs(this.SelectedSourceDefinition);

    private static IReadOnlyList<(string LeftProject,string RightProject)> CreateBenchmarkPairs(BenchmarkSourceTabDefinition sourceTab) => [
        (sourceTab.LeftProject,sourceTab.RightProject),
    ];

    /// <summary>
    /// GetRowTypeFullName メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string GetRowTypeFullName(BenchmarkTableRow row,string project) =>
        row.CasesByProject.TryGetValue(project,out var benchmark)
            ? benchmark.TypeFullName
            : row.CasesByProject.Values.FirstOrDefault()?.TypeFullName??string.Empty;

    /// <summary>
    /// GetRowArgumentKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string GetRowArgumentKey(BenchmarkTableRow row,string project) =>
        row.CasesByProject.TryGetValue(project,out var benchmark)
            ? benchmark.ArgumentKey
            : row.CasesByProject.Values.FirstOrDefault()?.ArgumentKey??string.Empty;

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private bool HasResult(string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        return this.HasResult(this.GetResultSourceForDisplay(),method,project,typeFullName,elementCount,argumentKey);
    }

    private bool HasResult(IReadOnlyList<BenchmarkResultItem>results,string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        return results.Any(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(result.TypeFullName,typeFullName)
                &&IsSameArgument(result.ArgumentKey,argumentKey)
                &&result.AxisCount==elementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&result.MeanNanoseconds is not null);
    }

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private int? FindPairStoppedAt(string method,string typeFullName,string project,int selectedElementCount,string argumentKey = "") {
        var measurements = this.GetResultSourceForDisplay()
            .Select(static result => new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ArgumentKey,
                result.AxisCount,
                result.Status,
                result.MeanNanoseconds,
                result.TotalNanoseconds));
        return BenchmarkSkippedDisplayLogic.FindPairStoppedAt(
            measurements,
            method,
            typeFullName,
            argumentKey,
            project,
            selectedElementCount,
            this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000);
    }

    /// <summary>
    /// FindLatestCompletedPairElementCountBefore メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="leftProject">leftProject を指定して実行条件を制御します。</param>
    /// <param name="rightProject">rightProject を指定して実行条件を制御します。</param>
    /// <param name="selectedElementCount">selectedElementCount を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private int? FindLatestCompletedPairElementCountBefore(
        string method,
        string typeFullName,
        string leftProject,
        string rightProject,
        int selectedElementCount,
        string argumentKey = "") {
        var measurements = this.GetResultSourceForDisplay()
            .Select(static result => new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ArgumentKey,
                result.AxisCount,
                result.Status,
                result.MeanNanoseconds,
                result.TotalNanoseconds));
        return BenchmarkSkippedDisplayLogic.FindLatestCompletedPairElementCountBefore(
            measurements,
            method,
            typeFullName,
            argumentKey,
            leftProject,
            rightProject,
            selectedElementCount);
    }

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private ProjectMetrics GetResultMetrics(string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        return this.GetResultMetrics(this.GetResultSourceForDisplay(),method,project,typeFullName,elementCount,argumentKey);
    }

    private ProjectMetrics GetResultMetrics(IReadOnlyList<BenchmarkResultItem>results,string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        var selectedRunResult = results
            .LastOrDefault(item => string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(item.Project,project,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(item.TypeFullName,typeFullName)
                &&IsSameArgument(item.ArgumentKey,argumentKey)
                &&item.AxisCount==elementCount
                &&string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&item.MeanNanoseconds is not null);
        return selectedRunResult is not null
            ? ProjectMetrics.FromMeasurement(selectedRunResult.MeanNanoseconds,selectedRunResult.AllocatedBytesValue)
            : ProjectMetrics.Missing;
    }

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private BenchmarkResultItem? GetResultItem(string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        return this.GetResultItem(this.GetResultSourceForDisplay(),method,project,typeFullName,elementCount,argumentKey);
    }

    private BenchmarkResultItem? GetResultItem(IReadOnlyList<BenchmarkResultItem>results,string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        for(var index = results.Count-1; index>=0; index--) {
            var item = results[index];
            if(IsResultMatch(item,method,project,typeFullName,elementCount,argumentKey)
                &&string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)
                &&item.MeanNanoseconds is not null) {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// GetResultSourceForDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkResultItem> GetResultSourceForDisplay() {
        var source = this.ShouldUseCurrentRunMemoryForDisplay(this.SelectedSourceDefinition)
            ? this.GetCurrentRunResultItemsForSelectedTab()
            : this.ResultItems;
        return source.Where(this.IsResultSelectedForDisplay).ToArray();
    }

    private IReadOnlyList<BenchmarkResultItem> GetResultSourceForTableApply() {
        var source = this.ShouldUseCurrentRunMemoryForDisplay(this.SelectedSourceDefinition)
            ? this.GetCurrentRunResultItemsForSelectedTab()
            : this.ResultItems;
        return source
            .Where(result => this.SelectedSourceDefinition.ContainsProject(result.Project))
            .ToArray();
    }

    /// <summary>
    /// IsResultSelectedForDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool IsResultSelectedForDisplay(BenchmarkResultItem result) {
        return this.IsResultSelectedForDisplay(result,this.SelectedSourceDefinition);
    }

    /// <summary>
    /// IsResultSelectedForDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool IsResultSelectedForDisplay(BenchmarkResultItem result,BenchmarkSourceTabDefinition sourceTab) {
        if(!sourceTab.ContainsProject(result.Project)) {
            return false;
        }

        var row = this.FindBenchmarkRow(result);
        if(row is null) {
            return false;
        }

        return row.ProjectCells.Any(cell =>
            cell.HasCase
            &&string.Equals(cell.Project,result.Project,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(row.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
            &&IsSameType(GetRowTypeFullName(row,cell.Project),result.TypeFullName)
            &&IsSameArgument(GetRowArgumentKey(row,cell.Project),result.ArgumentKey));
    }

    /// <summary>
    /// LoadSelectedRunState メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private ProgressState? LoadSelectedRunState() {
        var run = this.SelectedRunOption;
        if(run is null||IsNewRunOption(run)||IsTransientRunOption(run)||run.UiRunId<=0) {
            return null;
        }

        // 過去データの読み込みは Run 選択時の1回だけ DB から行い、以後のタブ切替などはメモリバッファを返す。
        if(this.selectedRunStateCache is { } cache&&cache.UiRunId==run.UiRunId) {
            return cache.State;
        }

        try {
            var runRecord = this.uiRunStore.ListRuns(GetUiRunAppKind()).FirstOrDefault(item => item.RunId==run.UiRunId);
            var state = runRecord is null ? null : this.CreateProgressStateFromDatabaseRun(run.UiRunId,runRecord);
            this.selectedRunStateCache=(run.UiRunId,state);
            return state;
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark DB progress snapshot load failed.",ex);
            if(ex is InvalidDataException) {
                throw;
            }
            return null;
        }
    }

    private ProgressState? CreateProgressStateFromDatabaseRun(long runId,UiRunRecord run){
        var settings = this.uiRunStore.GetRunSettings(runId);
        if(settings is null||string.IsNullOrWhiteSpace(settings.RunRoot)){
            return null;
        }

        if(!Enum.TryParse<BenchmarkSourceTab>(run.SourceTab,true,out var sourceTab)) {
            throw new InvalidDataException("Benchmark DB run SourceTab is unknown. RunId="+runId+" SourceTab="+run.SourceTab);
        }
        var executionProfile = Enum.TryParse<BenchmarkExecutionProfile>(run.ExecutionProfile,true,out var parsedProfile)
            ? parsedProfile
            : this.SelectedExecutionProfile.Profile;
        var cases = this.uiRunStore.ListCases(runId).ToDictionary(static item => item.CaseId);
        var metrics = this.uiRunStore.ListMetrics(runId)
            .GroupBy(static item => item.JobId)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToDictionary(static item => item.MetricName,static item => item,StringComparer.OrdinalIgnoreCase));
        var elementCounts = BenchmarkElementCounts.DefaultInitial.ToList();
        var jobs = new List<BenchmarkJob>();
        var runItemIndex = 0;
        foreach(var storedJob in this.uiRunStore.ListJobs(runId)){
            if(!cases.TryGetValue(storedJob.CaseId,out var testCase)){
                continue;
            }
            var jobSourceTab=BenchmarkSourceTabs.GetById(testCase.SourceTabId).Tab;
            if(jobSourceTab!=sourceTab) {
                throw new InvalidDataException(
                    "Benchmark DB case SourceTab does not match its run. RunId="+runId
                    +" RunSourceTab="+BenchmarkSourceTabs.Get(sourceTab).Id
                    +" CaseSourceTab="+testCase.SourceTabId
                    +" Project="+testCase.Project
                    +" Assembly="+testCase.AssemblyName);
            }
            var resolvedCase = this.FindBenchmarkCase(testCase.Method,testCase.Project,testCase.TypeFullName,testCase.ArgumentKey,jobSourceTab);
            if(resolvedCase is null){
                throw new InvalidDataException(
                    "Benchmark DB case could not be resolved from the current catalog. RunId="+runId
                    +" SourceTab="+testCase.SourceTabId
                    +" Project="+testCase.Project
                    +" Assembly="+testCase.AssemblyName
                    +" Type="+testCase.TypeFullName
                    +" Method="+testCase.Method);
            }
            var sourceTabDefinition=BenchmarkSourceTabs.Get(jobSourceTab);
            if(!string.Equals(resolvedCase.AssemblyName,testCase.AssemblyName,StringComparison.OrdinalIgnoreCase)
                ||!sourceTabDefinition.ContainsCase(resolvedCase)){
                throw new InvalidDataException(
                    "Benchmark DB case identity does not match the current catalog. RunId="+runId
                    +" SourceTab="+testCase.SourceTabId
                    +" Project="+testCase.Project
                    +" StoredAssembly="+testCase.AssemblyName
                    +" ResolvedAssembly="+resolvedCase.AssemblyName
                    +" Type="+testCase.TypeFullName
                    +" Method="+testCase.Method);
            }

            var caseArguments = this.uiRunStore.ListCaseArguments(testCase.CaseId);
            var arguments = caseArguments
                .Where(static item => !string.Equals(item.Key,"ElementCount",StringComparison.OrdinalIgnoreCase)
                    &&!string.Equals(item.Key,"LoopCount",StringComparison.OrdinalIgnoreCase)
                    &&!string.Equals(item.Key,"SourceTab",StringComparison.OrdinalIgnoreCase))
                .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
                .Select(static item => new BenchmarkArgument(item.Key,item.Value))
                .ToList();
            var axisCount = (int)(BenchmarkWorkloadAxis.UsesLoopCount(jobSourceTab)
                ? storedJob.LoopCount??BenchmarkWorkloadAxis.DefaultLoopCount
                : storedJob.ElementCount??0);
            if(axisCount>0&&!elementCounts.Contains(axisCount)){
                elementCounts.Add(axisCount);
            }

            metrics.TryGetValue(storedJob.JobId,out var jobMetrics);
            var meanNanoseconds = GetMetric(jobMetrics,"MeanNanoseconds");
            var allocatedBytes = GetMetric(jobMetrics,"AllocatedBytes");
            jobs.Add(new BenchmarkJob{
                JobId=storedJob.JobKey,
                Project=resolvedCase.Project,
                AssemblyName=resolvedCase.AssemblyName,
                TypeFullName=resolvedCase.TypeFullName,
                MethodName=resolvedCase.MethodName,
                ArgumentKey=resolvedCase.ArgumentKey,
                Arguments=arguments.Count>0 ? arguments : resolvedCase.Arguments.ToList(),
                SourceTab=jobSourceTab,
                ElementCount=(int)(storedJob.ElementCount??BenchmarkWorkloadAxis.ResolveElementCount(jobSourceTab,axisCount)),
                LoopCount=(int)(storedJob.LoopCount??BenchmarkWorkloadAxis.ResolveLoopCount(jobSourceTab,axisCount)),
                PreparedElementCounts=BenchmarkWorkloadAxis.ResolvePreparedElementCounts(jobSourceTab,elementCounts).ToList(),
                Status=ToJobStatusForResume(storedJob.Status),
                AssignedLogicalCpu=storedJob.Status is UiRunStatus.Completed or UiRunStatus.Failed ? storedJob.AssignedLogicalCpu : null,
                Mean=meanNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
                MeanNanoseconds=meanNanoseconds,
                AllocatedBytes=allocatedBytes is null ? null : (long)allocatedBytes.Value,
                StartedAt=storedJob.Status is UiRunStatus.Completed or UiRunStatus.Failed ? storedJob.StartedAt : null,
                FinishedAt=storedJob.Status is UiRunStatus.Completed or UiRunStatus.Failed ? storedJob.FinishedAt : null,
                FailureReason=storedJob.FailureReason,
                RunItemIndex=runItemIndex++,
            });
        }

        elementCounts=elementCounts.Distinct().Order().ToList();
        if(elementCounts.Count==0){
            elementCounts=BenchmarkElementCounts.DefaultInitial.ToList();
        }

        var now = DateTimeOffset.UtcNow;
        var state = new ProgressState{
            RunId=runId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ExecutionProfile=executionProfile,
            RunRoot=settings.RunRoot,
            RunNumber=BenchmarkStateManager.TryParseRunNumber(settings.RunRoot),
            SourceTab=sourceTab,
            ElementCounts=elementCounts,
            AdaptiveThresholdNanoseconds=settings.AdaptiveThresholdNanoseconds,
            Jobs=jobs,
            NextRunItemIndex=jobs.Count==0 ? 0 : jobs.Max(static job => job.RunItemIndex)+1,
            AdaptiveStates=jobs
                .GroupBy(static job => job.MethodName,StringComparer.OrdinalIgnoreCase)
                .Select(static group => new AdaptiveBenchmarkState { MethodName=group.Key })
                .ToList(),
            CreatedAt=run.UpdatedAt,
            UpdatedAt=now,
        };

        return state;
    }

    private static JobStatus ToJobStatusForResume(UiRunStatus status) =>
        status switch{
            UiRunStatus.Completed => JobStatus.Completed,
            UiRunStatus.Failed => JobStatus.Failed,
            _ => JobStatus.Pending,
        };

    private static List<int> ParseIntList(string value) =>
        value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Select(static item => int.TryParse(item,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var parsed) ? parsed : 0)
            .Where(static item => item>0)
            .Distinct()
            .Order()
            .ToList();

    private static double? GetMetric(IReadOnlyDictionary<string,UiMetricValueRecord>? metrics,string key) =>
        metrics is not null&&metrics.TryGetValue(key,out var value)
            ? value.NumericValue
            : null;

    /// <summary>
    /// ReportMissingBenchmarkMetadata メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void ReportMissingBenchmarkMetadata(string method,string project,int elementCount,BenchmarkResultItem? result) {
        var key = method+"|"+project+"|"+elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(!this.reportedMetadataWarnings.Add(key)) {
            return;
        }

        var message = "CPU/time metadata missing: "
            +BenchmarkFormatting.ShortName(method)+" / "
            +project+" / N="
            +elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        this.StatusText=message;
        StartupDiagnostics.Write(
            message+" "
            +"ResultFound="+(result is null ? "false" : "true")+" "
            +"CPU="+(result?.AssignedLogicalCpu?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")+" "
            +"StartedAt="+(result?.StartedAt?.ToString("O",System.Globalization.CultureInfo.InvariantCulture)??"-"));
    }

    /// <summary>
    /// IsResultMatch メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsResultMatch(BenchmarkResultItem item,BenchmarkResultItem result) =>
        IsResultMatch(item,result.Method,result.Project,result.TypeFullName,result.AxisCount,result.ArgumentKey);

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private static bool IsResultMatch(BenchmarkResultItem item,string method,string project,string typeFullName,int elementCount,string argumentKey = "") =>
        string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(item.Project,project,StringComparison.OrdinalIgnoreCase)
        &&IsSameType(item.TypeFullName,typeFullName)
        &&IsSameArgument(item.ArgumentKey,argumentKey)
        &&item.AxisCount==elementCount;

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private static string CreateResultIdentityKey(string project,string typeFullName,string method,int elementCount,string argumentKey = "") =>
        string.Join("|",project,typeFullName,method,argumentKey,elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// IsSameType メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="left">left を指定して実行条件を制御します。</param>
    /// <param name="right">right を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsSameType(string left,string right) =>
        string.IsNullOrWhiteSpace(right)
            ? true
            : !string.IsNullOrWhiteSpace(left)
                &&(string.Equals(left,right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(ShortTypeName(left),right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(left,ShortTypeName(right),StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// IsSameArgument メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="left">left を指定して実行条件を制御します。</param>
    /// <param name="right">right を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsSameArgument(string left,string right) =>
        BenchmarkArgumentKeyMatchLogic.IsExactMatch(left,right);

    /// <summary>
    /// preserveExistingWhenNoMeasuredPoints プロパティです。
    /// </summary>
    /// <value>preserveExistingWhenNoMeasuredPoints の値を取得/設定します。</value>
    private void UpdateChart(bool preserveExistingWhenNoMeasuredPoints = true) {
        this.SelectedSourceTabViewState.RequiresFullChartRebuild=true;
        if(this.chartRefreshBatchDepth>0) {
            this.pendingChartUpdate=true;
            this.pendingChartUpdatePreserveExisting&=preserveExistingWhenNoMeasuredPoints;
            return;
        }
        this.UpdateChartCore(preserveExistingWhenNoMeasuredPoints);
    }

    /// <summary>
    /// preserveExistingWhenNoMeasuredPoints プロパティです。
    /// </summary>
    /// <value>preserveExistingWhenNoMeasuredPoints の値を取得/設定します。</value>
    private void UpdateChartCore(bool preserveExistingWhenNoMeasuredPoints = true) {
        this.UpdateChartCore(this.SelectedSourceTabViewState,preserveExistingWhenNoMeasuredPoints);
    }

    /// <summary>
    /// preserveExistingWhenNoMeasuredPoints プロパティです。
    /// </summary>
    /// <value>preserveExistingWhenNoMeasuredPoints の値を取得/設定します。</value>
    private void UpdateAllSourceTabCharts(bool preserveExistingWhenNoMeasuredPoints = true) {
        foreach(var state in this.sourceTabViewStates) {
            state.RequiresFullChartRebuild=true;
            this.UpdateChartCore(state,preserveExistingWhenNoMeasuredPoints);
        }
        this.OnPropertyChanged(nameof(this.ChartPoints));
        this.OnPropertyChanged(nameof(this.ChartEmptyReasonText));
        this.OnPropertyChanged(nameof(this.ChartEmptyReasonTooltipText));
        this.OnPropertyChanged(nameof(this.ChartHeight));
        this.OnPropertyChanged(nameof(this.LatencyHotspotRows));
        this.OnPropertyChanged(nameof(this.AllocationDiagnosticRows));
        this.OnPropertyChanged(nameof(this.CpuTimelineRows));
    }

    /// <summary>
    /// chartRefreshRequestToken フィールドです。
    /// </summary>
    private int chartRefreshRequestToken;
    /// <summary>
    /// chartRefreshInFlight フィールドです。
    /// </summary>
    private bool chartRefreshInFlight;
    /// <summary>
    /// pendingChartRefreshTabs フィールドです。
    /// </summary>
    private readonly HashSet<BenchmarkSourceTab> pendingChartRefreshTabs = [];
    /// <summary>
    /// pendingDirtyChartTabs フィールドです。
    /// </summary>
    private readonly HashSet<BenchmarkSourceTab> pendingDirtyChartTabs = [];

    /// <summary>
    /// preserveExistingWhenNoMeasuredPoints プロパティです。
    /// </summary>
    /// <value>preserveExistingWhenNoMeasuredPoints の値を取得/設定します。</value>
    private void UpdateChartCore(BenchmarkSourceTabViewState state,bool preserveExistingWhenNoMeasuredPoints = true) {
        if(!this.IsBenchmarkRunActive) {
            // 非実行中は ComparisonRows キャッシュを使う軽い同期パス。
            this.ApplyChartFromComparisonRows(state,this.BuildElementComparisonRows(state));
            return;
        }

        // 実行中の比較行再構築は結果毎の選択判定・整形・DB参照(FindPreviousResult)を含み重い。
        // currentRunSession に触れるスナップショット取得だけをUIスレッドで済ませ、比較行と
        // 診断行の計算はバックグラウンドで行う。チャートポイント構築は行の可変状態
        // (選択・実行中表示)を読むため、反映側(UIスレッド)に残す。
        if(this.chartRefreshInFlight) {
            this.pendingChartRefreshTabs.Add(state.Definition.Tab);
            return;
        }

        var definition = state.Definition;
        var sessionResults = this.GetCurrentRunResultItems(definition.Tab);
        var allResults = this.currentRunSession.GetAllResults();
        var selectedN = this.SelectedResultElementCount?.Value;
        var token = ++this.chartRefreshRequestToken;
        var generation = this.runResultGeneration;
        this.chartRefreshInFlight=true;
        var stopwatch = Stopwatch.StartNew();
        Task.Run(() => {
            var rows = selectedN is { } elementCount
                ? this.runProjectionService.BuildComparisonRowsForElementCount(sessionResults,definition,elementCount,this.CreateReportRowsForComparison)
                : this.runProjectionService.BuildLatestComparisonRows(sessionResults,definition,this.CreateReportRowsForComparison);
            return (
                Rows: rows,
                LatencyRows: this.BuildMetricDiagnosticRowsReadable(definition,rows,BenchmarkMetricKind.Time),
                AllocationRows: this.BuildMetricDiagnosticRowsReadable(definition,rows,BenchmarkMetricKind.Allocated),
                TimelineRows: BuildCpuTimelineRows(allResults));
        })
            .ContinueWith(task => {
                UiThreadBudget.Record("bg:chart",stopwatch.Elapsed.TotalMilliseconds);
                this.uiDispatcher.BeginInvoke(() => {
                    this.chartRefreshInFlight=false;
                    if(token==this.chartRefreshRequestToken&&generation==this.runResultGeneration&&!task.IsFaulted) {
                        var computed = task.Result;
                        UiThreadBudget.Measure("chart-apply",() => {
                            state.ComparisonRows=computed.Rows;
                            this.ベンチマークシェア概要を更新(state);
                            IReadOnlyList<ChartComparisonPoint> points;
                            if(state.RequiresFullChartRebuild) {
                                points=this.BuildBenchmarkChartPoints(state,computed.Rows);
                                this.SyncBenchmarkChartPoints(state,points);
                            }else{
                                this.FlushDirtyChartRows(state,computed.Rows);
                                points=state.ChartPoints;
                            }
                            state.ChartHeight=Math.Max(BenchmarkChartMinimumHeight,points.Count*BenchmarkChartRowHeight+BenchmarkChartVerticalPadding);
                            this.UpdateChartEmptyReason(state,computed.Rows,points);
                            this.SyncDiagnosticRows(state.LatencyHotspotRows,computed.LatencyRows);
                            this.SyncDiagnosticRows(state.AllocationDiagnosticRows,computed.AllocationRows);
                            this.SyncDiagnosticRows(state.CpuTimelineRows,computed.TimelineRows);
                            this.generatedCodeAuditDirty=true;
                            this.RefreshGeneratedCodeAuditIfNeeded();
                        });
                    }
                    else if(task.IsFaulted) {
                        StartupDiagnostics.Write("Background chart refresh failed.",task.Exception);
                    }

                    if(this.pendingChartRefreshTabs.Count>0) {
                        var tabs = this.pendingChartRefreshTabs.ToArray();
                        this.pendingChartRefreshTabs.Clear();
                        foreach(var tab in tabs) {
                            this.UpdateChartCore(this.GetSourceTabViewState(tab),false);
                        }
                    }
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// ApplyChartFromComparisonRows メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    private void ApplyChartFromComparisonRows(BenchmarkSourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        state.ComparisonRows=rows;
        this.ベンチマークシェア概要を更新(state);
        var points = this.BuildBenchmarkChartPoints(state,rows);
        this.SyncBenchmarkChartPoints(state,points);
        state.ChartHeight=Math.Max(BenchmarkChartMinimumHeight,points.Count*BenchmarkChartRowHeight+BenchmarkChartVerticalPadding);
        this.UpdateChartEmptyReason(state,rows,points);
        this.RefreshDiagnosticViews(state,rows);
    }

    private void UpdateChartEmptyReason(BenchmarkSourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows,IReadOnlyList<ChartComparisonPoint> points) {
        var reason = this.CreateChartEmptyReason(state,rows,points);
        state.ChartEmptyReasonText=reason.Text;
        state.ChartEmptyReasonTooltipText=reason.Tooltip;
        if(ReferenceEquals(state,this.SelectedSourceTabViewState)) {
            this.OnPropertyChanged(nameof(this.ChartEmptyReasonText));
            this.OnPropertyChanged(nameof(this.ChartEmptyReasonTooltipText));
        }
    }

    private (string Text,string Tooltip) CreateChartEmptyReason(
        BenchmarkSourceTabViewState state,
        IReadOnlyList<BenchmarkComparisonRow> rows,
        IReadOnlyList<ChartComparisonPoint> points) {
        if(points.Count>0) {
            return (string.Empty,string.Empty);
        }

        var sourceTab = state.Definition;
        var selectedN = this.SelectedResultElementCount?.DisplayName??"-";
        var selectedRun = this.SelectedRunOption?.DisplayName??"-";
        var tooltip = "タブ: "+sourceTab.DisplayName+"\n"
            +"Run: "+selectedRun+"\n"
            +"選択軸: "+selectedN+"\n"
            +"表示結果: "+this.ResultItems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"
            +"比較行: "+rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if(!this.HasAnyLiveSelectedBenchmarkCell(sourceTab)) {
            return ("選択中のベンチマークがありません。",tooltip+"\n原因: このタブで選択済みのベンチマークセルがありません。");
        }

        if(this.IsBenchmarkRunActive) {
            return ("計測結果を待っています。",tooltip+"\n原因: 実行中ですが、まだ比較チャートに使える完了結果がありません。");
        }

        var hasTabResults = this.currentRunSession.HasResults(sourceTab.Tab)||this.ResultItems.Any(result => sourceTab.ContainsProject(result.Project));
        if(!hasTabResults) {
            if(this.SelectedRunOption is null||IsNewRunOption(this.SelectedRunOption)) {
                return ("ベンチマークを実行するか、保存済み DB 実行を選択してください。",tooltip+"\n原因: 現在のタブに表示できる結果がまだありません。");
            }

            return ("保存済み DB 実行に表示できる完了結果がありません。",tooltip+"\n原因: 選択した保存済み実行から、このタブの完了結果を復元できませんでした。");
        }

        if(rows.Count==0) {
            return ("比較できる完了結果がありません。",tooltip+"\n原因: 選択状態、軸、または結果ステータスの条件に合う比較行がありません。");
        }

        return ("比較ペアの両側に有効な値がありません。",tooltip+"\n原因: 比較行はありますが、左右のどちらかまたは両方に計測値がありません。");
    }

    /// <summary>
    /// RefreshDiagnosticViews メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    private void RefreshDiagnosticViews(IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.RefreshDiagnosticViews(this.SelectedSourceTabViewState,rows);
    }

    /// <summary>
    /// RefreshDiagnosticViews メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    private void RefreshDiagnosticViews(BenchmarkSourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.SyncDiagnosticRows(state.LatencyHotspotRows,this.BuildMetricDiagnosticRowsReadable(state.Definition,rows,BenchmarkMetricKind.Time));
        this.SyncDiagnosticRows(state.AllocationDiagnosticRows,this.BuildMetricDiagnosticRowsReadable(state.Definition,rows,BenchmarkMetricKind.Allocated));
        this.SyncDiagnosticRows(state.CpuTimelineRows,this.BuildCpuTimelineRows());
        this.generatedCodeAuditDirty=true;
        this.RefreshGeneratedCodeAuditIfNeeded();
    }

    /// <summary>
    /// BuildMetricDiagnosticRowsReadable メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkDiagnosticRow> BuildMetricDiagnosticRowsReadable(BenchmarkSourceTabDefinition sourceTab,IReadOnlyList<BenchmarkComparisonRow> rows,BenchmarkMetricKind metric) {
        return rows
            .Select(row => {
                var left = row.GetProject(sourceTab.LeftProject);
                var right = row.GetProject(sourceTab.RightProject);
                var leftValue = metric==BenchmarkMetricKind.Time ? left.TimeNanoseconds : left.AllocatedBytes;
                var rightValue = metric==BenchmarkMetricKind.Time ? right.TimeNanoseconds : right.AllocatedBytes;
                var share=指標シェアを作成(leftValue,rightValue,metric);
                return new {
                    Row = row,
                    LeftValue = leftValue,
                    RightValue = rightValue,
                    Share = share,
                };
            })
            .Where(item => item.LeftValue is not null&&item.RightValue is not null)
            .OrderByDescending(item => item.Share.左シェア??double.NegativeInfinity)
            .ThenByDescending(item => item.LeftValue??0)
            .Take(40)
            .Select(item => new BenchmarkDiagnosticRow(
                metric==BenchmarkMetricKind.Time ? "Latency" : "Allocation",
                BenchmarkFormatting.ShortName(item.Row.Benchmark),
                this.SelectedResultElementCount?.DisplayName??"-",
                item.Share.表示文字列,
                FormatMetric(item.LeftValue,metric),
                FormatMetric(item.RightValue,metric),
                sourceTab.LeftProject+" / "+sourceTab.RightProject+" / "+item.Row.MethodName))
            .ToArray();
    }

    /// <summary>
    /// BuildMetricDiagnosticRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkDiagnosticRow> BuildMetricDiagnosticRows(IReadOnlyList<BenchmarkComparisonRow> rows,BenchmarkMetricKind metric) {
        var sourceTab = this.SelectedSourceDefinition;
        return rows
            .Select(row => {
                var left = row.GetProject(sourceTab.LeftProject);
                var right = row.GetProject(sourceTab.RightProject);
                var leftValue = metric==BenchmarkMetricKind.Time ? left.TimeNanoseconds : left.AllocatedBytes;
                var rightValue = metric==BenchmarkMetricKind.Time ? right.TimeNanoseconds : right.AllocatedBytes;
                var share=指標シェアを作成(leftValue,rightValue,metric);
                return new {
                    Row = row,
                    LeftValue = leftValue,
                    RightValue = rightValue,
                    Share = share,
                };
            })
            .Where(item => item.LeftValue is not null&&item.RightValue is not null)
            .OrderByDescending(item => item.Share.左シェア??double.NegativeInfinity)
            .ThenByDescending(item => item.LeftValue??0)
            .Take(40)
            .Select(item => new BenchmarkDiagnosticRow(
                metric==BenchmarkMetricKind.Time ? "Latency" : "Allocation",
                BenchmarkFormatting.ShortName(item.Row.Benchmark),
                this.SelectedResultElementCount?.DisplayName??"-",
                item.Share.表示文字列,
                FormatMetric(item.LeftValue,metric),
                FormatMetric(item.RightValue,metric),
                sourceTab.LeftProject+" / "+sourceTab.RightProject+" / "+item.Row.MethodName))
            .ToArray();
    }

    private static シェア値 指標シェアを作成(double? leftValue,double? rightValue,BenchmarkMetricKind metric) {
        if(leftValue is null||rightValue is null) {
            return シェア値.欠測;
        }
        if(metric==BenchmarkMetricKind.Time) {
            return シェア値.時間から作成(leftValue.Value,rightValue.Value);
        }
        return シェア値.メモリから作成((long)leftValue.Value,(long)rightValue.Value);
    }

    /// <summary>
    /// BuildCpuTimelineRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkDiagnosticRow> BuildCpuTimelineRows() {
        return BuildCpuTimelineRows(this.currentRunSession.GetAllResults());
    }

    /// <summary>
    /// BuildCpuTimelineRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="allResults">allResults を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<BenchmarkDiagnosticRow> BuildCpuTimelineRows(IReadOnlyList<BenchmarkResultItem> allResults) {
        return allResults
            .OrderByDescending(static item => item.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .Select(static item => new BenchmarkDiagnosticRow(
                item.Status,
                BenchmarkFormatting.ShortName(item.Method),
                item.AxisCount>0 ? item.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-",
                item.AssignedLogicalCpu is { } cpu ? "CPU "+cpu.ToString(System.Globalization.CultureInfo.InvariantCulture) : "CPU -",
                item.Mean,
                item.AllocatedBytes,
                item.Project+" / "+(string.IsNullOrWhiteSpace(item.TypeFullName) ? item.Method : item.TypeFullName)))
            .ToArray();
    }

    /// <summary>
    /// SyncDiagnosticRows メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="target">target を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    private void SyncDiagnosticRows(ObservableCollection<BenchmarkDiagnosticRow> target,IReadOnlyList<BenchmarkDiagnosticRow> rows) {
        // 内容が同一ならClear/Addの通知(Syncfusion再レイアウト)自体を発生させない。
        if(target.Count==rows.Count) {
            var identical = true;
            for(var index = 0; index<rows.Count; index++) {
                if(!Equals(target[index],rows[index])) {
                    identical=false;
                    break;
                }
            }
            if(identical) {
                return;
            }
        }

        target.Clear();
        foreach(var row in rows) {
            target.Add(row);
        }
    }

    /// <summary>
    /// generatedCodeAuditDirty フィールドです。
    /// </summary>
    private bool generatedCodeAuditDirty = true;
    /// <summary>
    /// generatedCodeAuditRequestToken フィールドです。
    /// </summary>
    private int generatedCodeAuditRequestToken;

    /// <summary>
    /// RefreshGeneratedCodeAuditIfNeeded メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshGeneratedCodeAuditIfNeeded() {
        if(!this.generatedCodeAuditDirty||!this.IsGeneratedCodeAuditTabVisible) {
            return;
        }
        this.generatedCodeAuditDirty=false;

        var input = this.CaptureGeneratedCodeAuditInput();
        if(input is null) {
            this.GeneratedCodeAuditReport=new BenchmarkGeneratedCodeAuditReport("-","-","Select a target method to inspect generated code.",null,[],"-");
            return;
        }

        var requestToken = ++this.generatedCodeAuditRequestToken;
        Task.Run(() => this.CreateGeneratedCodeAuditReportCore(input.Value))
            .ContinueWith(task => {
                this.uiDispatcher.BeginInvoke(() => {
                    if(requestToken!=this.generatedCodeAuditRequestToken) {
                        return;
                    }
                    var report = task.IsFaulted
                        ? new BenchmarkGeneratedCodeAuditReport(input.Value.MethodName,input.Value.LeftProject+" vs "+input.Value.RightProject,"Load failed",null,[],"Generated code audit failed: "+task.Exception!.GetBaseException().Message)
                        : task.Result;
                    this.GeneratedCodeAuditReport=report;
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// GeneratedCodeAuditInput メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="MethodName">MethodName を指定して実行条件を制御します。</param>
    /// <param name="LeftProject">LeftProject を指定して実行条件を制御します。</param>
    /// <param name="RightProject">RightProject を指定して実行条件を制御します。</param>
    /// <param name="BenchmarkProject">BenchmarkProject を指定して実行条件を制御します。</param>
    /// <param name="AssemblyName">AssemblyName を指定して実行条件を制御します。</param>
    /// <param name="TypeFullName">TypeFullName を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct GeneratedCodeAuditInput(
        string MethodName,string LeftProject,string RightProject,
        string BenchmarkProject,string AssemblyName,string TypeFullName);

    public sealed record BenchmarkGeneratedCodeAuditReport(
        string TargetMethod,string ComparisonPair,string AuditTarget,string? GeneratedFileName,
        IReadOnlyList<BenchmarkBadgeItem> Flags,string Verdict);

    /// <summary>
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// 必 要 に 応 じ て 副 作 用 と し て 内 部 状 態 や 表 示 へ 反 映 し ま す 。
    /// </summary>
    /// <returns>処 理 結 果 を 返 却 し ま す 。</returns>
    private GeneratedCodeAuditInput? CaptureGeneratedCodeAuditInput() {
        var selected = this.BenchmarkRows.FirstOrDefault(static row => row.IsUiSelected)
            ??this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.RowKey,this.activeChartMethodName,StringComparison.OrdinalIgnoreCase))
            ??this.BenchmarkRows.FirstOrDefault();
        if(selected is null) {
            return null;
        }

        var sourceTab = this.SelectedSourceDefinition;
        var benchmark = selected.CasesByProject.TryGetValue(sourceTab.LeftProject,out var leftBenchmark)
            ? leftBenchmark
            : selected.CasesByProject.Values.FirstOrDefault();
        if(benchmark is null) {
            return new GeneratedCodeAuditInput(selected.MethodName,sourceTab.LeftProject,sourceTab.RightProject,"","","");
        }

        return new GeneratedCodeAuditInput(selected.MethodName,sourceTab.LeftProject,sourceTab.RightProject,benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName);
    }

    /// <summary>
    /// CreateGeneratedCodeAuditReportCore メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="input">input を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkGeneratedCodeAuditReport CreateGeneratedCodeAuditReportCore(GeneratedCodeAuditInput input) {
        var comparisonPair = input.LeftProject+" vs "+input.RightProject;
        if(string.IsNullOrEmpty(input.AssemblyName)) {
            return new BenchmarkGeneratedCodeAuditReport(input.MethodName,comparisonPair,"BenchmarkCase was not found.",null,[],"-");
        }

        var auditTarget = input.BenchmarkProject+" / "+input.AssemblyName+" / "+input.TypeFullName;
        var generatedFile = this.FindGeneratedCodeFile(input.AssemblyName,input.MethodName);
        if(generatedFile is null) {
            return new BenchmarkGeneratedCodeAuditReport(input.MethodName,comparisonPair,auditTarget,null,
                [new BenchmarkBadgeItem("generated code","generated-current .g.cs was not found. Build the benchmark project and retry.",BenchmarkBadgeSeverity.Warning)],
                "-");
        }

        var generatedFileName = Path.GetFileName(generatedFile);
        var text = File.ReadAllText(generatedFile);
        var block = ExtractGeneratedMethodBlock(text,input.MethodName);
        if(string.IsNullOrWhiteSpace(block)) {
            return new BenchmarkGeneratedCodeAuditReport(input.MethodName,comparisonPair,auditTarget,generatedFileName,
                [new BenchmarkBadgeItem("intercept","OptimizedLinq_"+input.MethodName+" was not found. Check method name, arguments, or generation target.",BenchmarkBadgeSeverity.Warning)],
                "-");
        }

        var flags = new List<BenchmarkBadgeItem> {
            CreateAuditFlag("intercept","OptimizedLinq_"+input.MethodName+" detected",true),
            CreateAuditFlag("System.Linq fallback","System.Linq.Enumerable call",!block.Contains("System.Linq.Enumerable.",StringComparison.Ordinal)),
            CreateAuditFlag("delegate call",".Invoke / Func",!block.Contains(".Invoke(",StringComparison.Ordinal)&&!block.Contains("Func<",StringComparison.Ordinal)),
            CreateAuditFlag("struct enumerator","struct + IValueEnumerator",block.Contains("private struct ",StringComparison.Ordinal)&&block.Contains("IValueEnumerator",StringComparison.Ordinal)),
            CreateAuditFlag("direct span","GetSpan / TryGetSpan",block.Contains(".GetSpan()",StringComparison.Ordinal)||block.Contains("TryGetSpan",StringComparison.Ordinal)),
            CreateAuditPresence("Dictionary",block.Contains("Dictionary<",StringComparison.Ordinal)),
            CreateAuditPresence("HashSet",block.Contains("HashSet<",StringComparison.Ordinal)),
            CreateAuditPresence("chunk array",block.Contains("new int[",StringComparison.Ordinal)||block.Contains("int[]",StringComparison.Ordinal)),
        };
        var verdict = "Review warning badges first. If all checks are OK, inspect generated code shape as needed.";
        return new BenchmarkGeneratedCodeAuditReport(input.MethodName,comparisonPair,auditTarget,generatedFileName,flags,verdict);
    }

    /// <summary>
    /// CreateAuditFlag メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="title">title を指定して実行条件を制御します。</param>
    /// <param name="label">label を指定して実行条件を制御します。</param>
    /// <param name="ok">ok を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkBadgeItem CreateAuditFlag(string title,string label,bool ok) =>
        new(title,ok ? "OK "+label : "Warning: "+label,ok ? BenchmarkBadgeSeverity.Ok : BenchmarkBadgeSeverity.Warning);

    /// <summary>
    /// CreateAuditPresence メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="title">title を指定して実行条件を制御します。</param>
    /// <param name="exists">exists を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkBadgeItem CreateAuditPresence(string title,bool exists) =>
        new(title,exists ? "present" : "absent",exists ? BenchmarkBadgeSeverity.Info : BenchmarkBadgeSeverity.Ok);

    /// <summary>
    /// FindGeneratedCodeFile メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="assemblyName">assemblyName を指定して実行条件を制御します。</param>
    /// <param name="methodName">methodName を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string? FindGeneratedCodeFile(string assemblyName,string methodName) {
        var solutionRoot = this.FindSolutionRootFromBenchmarkRoot();
        if(solutionRoot is null) {
            return null;
        }

        var projectRoot = Path.Combine(solutionRoot,"Benchmark",assemblyName);
        var objRoot = Path.Combine(projectRoot,"obj");
        if(!Directory.Exists(objRoot)) {
            return null;
        }

        foreach(var file in Directory.EnumerateFiles(objRoot,"*.g.cs",SearchOption.AllDirectories)
            .Where(static path => path.Contains("generated-current",StringComparison.OrdinalIgnoreCase))) {
            try {
                var text = File.ReadAllText(file);
                if(text.Contains("OptimizedLinq_"+methodName,StringComparison.Ordinal)) {
                    return file;
                }
            }
            catch(IOException) {
            }
            catch(UnauthorizedAccessException) {
            }
        }

        return null;
    }

    /// <summary>
    /// FindSolutionRootFromBenchmarkRoot メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private string? FindSolutionRootFromBenchmarkRoot() {
        var directory = new DirectoryInfo(this.benchmarkRoot);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// ExtractGeneratedMethodBlock メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="text">text を指定して実行条件を制御します。</param>
    /// <param name="methodName">methodName を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string ExtractGeneratedMethodBlock(string text,string methodName) {
        var index = text.IndexOf("OptimizedLinq_"+methodName,StringComparison.Ordinal);
        if(index<0) {
            return string.Empty;
        }

        var start = Math.Max(0,index-12000);
        var next = text.IndexOf("public static void OptimizedLinq_",index+1,StringComparison.Ordinal);
        var end = next>index ? Math.Min(text.Length,next+2000) : Math.Min(text.Length,index+40000);
        return text[start..end];
    }

    /// <summary>
    /// SyncChartPoints メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="points">points を指定して実行条件を制御します。</param>
    private void SyncChartPoints(IReadOnlyList<ChartComparisonPoint> points) {
        this.SyncChartPoints(this.ChartPoints,points);
    }

    private void SyncBenchmarkChartPoints(
        BenchmarkSourceTabViewState state,
        IReadOnlyList<ChartComparisonPoint> points) {
        UiThreadBudget.Measure("chart-structural-rebuild",() => {
            foreach(var point in state.ChartPoints) {
                if(point.Row is not { } row) {
                    continue;
                }

                if(ReferenceEquals(row.ChartBar,point)) {
                    row.ChartBar=null;
                    row.ChartPointIndex=-1;
                }
                point.DetachRow(row);
            }

            this.SyncChartPoints(state.ChartPoints,points);
            state.DirtyChartRows.Clear();
            state.RequiresFullChartRebuild=false;
            this.RebuildChartPointIndexes(state);
        });
        UiThreadBudget.Record("chart-structural-rebuild-count",points.Count);
    }

    private void FlushDirtyChartRows(
        BenchmarkSourceTabViewState state,
        IReadOnlyList<BenchmarkComparisonRow> comparisonRows) {
        if(state.DirtyChartRows.Count==0) {
            return;
        }

        var dirtyRows=state.DirtyChartRows.ToArray();
        state.DirtyChartRows.Clear();
        UiThreadBudget.Record("chart-dirty-row-count",dirtyRows.Length);
        UiThreadBudget.Measure("chart-dirty-flush",() => {
            foreach(var row in dirtyRows) {
                if(row.ChartBar is { } chartBar
                    &&(row.ChartPointIndex<0
                        ||row.ChartPointIndex>=state.ChartPoints.Count
                        ||!ReferenceEquals(state.ChartPoints[row.ChartPointIndex],chartBar))) {
                    var points=this.BuildBenchmarkChartPoints(state,comparisonRows);
                    this.SyncBenchmarkChartPoints(state,points);
                    return;
                }

                this.RefreshDirtyChartRow(state,row,comparisonRows);
            }
        });
    }

    private void RefreshDirtyChartRow(
        BenchmarkSourceTabViewState state,
        BenchmarkTableRow row,
        IReadOnlyList<BenchmarkComparisonRow> comparisonRows) {
        var candidate=this.CreateBenchmarkChartPoint(row,comparisonRows,state.Definition);
        if(candidate is null) {
            this.RemoveChartBar(state,row);
            return;
        }

        if(row.ChartBar is { } chartBar) {
            chartBar.ApplyFrom(candidate);
            state.ChartPoints[row.ChartPointIndex]=chartBar;
            UiThreadBudget.Record("chart-dirty-bar-update-count",1);
            return;
        }

        var insertIndex=0;
        while(insertIndex<state.ChartPoints.Count
            &&state.ChartPoints[insertIndex].Row is { } existingRow
            &&existingRow.GridDisplayIndex>row.GridDisplayIndex) {
            insertIndex++;
        }
        state.ChartPoints.Insert(insertIndex,candidate);
        UiThreadBudget.Record("chart-dirty-bar-update-count",1);
        this.ReindexChartPointsFrom(state,insertIndex);
        if(row.IsUiSelected) {
            state.UiSelectedChartBars.Add(candidate);
            state.NotifyChartSelectionChanged();
        }
    }

    private void RemoveChartBar(BenchmarkSourceTabViewState state,BenchmarkTableRow row) {
        if(row.ChartBar is not { } chartBar) {
            return;
        }

        var index=row.ChartPointIndex;
        if(index<0||index>=state.ChartPoints.Count||!ReferenceEquals(state.ChartPoints[index],chartBar)) {
            state.RequiresFullChartRebuild=true;
            return;
        }

        state.UiSelectedChartBars.Remove(chartBar);
        state.ChartPoints.RemoveAt(index);
        UiThreadBudget.Record("chart-dirty-bar-update-count",1);
        state.ChartPointIndexesByRowKey.Remove(row.RowKey);
        row.ChartBar=null;
        row.ChartPointIndex=-1;
        chartBar.DetachRow(row);
        this.ReindexChartPointsFrom(state,index);
        state.NotifyChartSelectionChanged();
    }

    private void ReindexChartPointsFrom(BenchmarkSourceTabViewState state,int startIndex) {
        for(var index=startIndex;index<state.ChartPoints.Count;index++) {
            var point=state.ChartPoints[index];
            state.ChartPointIndexesByRowKey[point.Benchmark]=index;
            if(point.Row is not { } row) {
                continue;
            }

            row.ChartBar=point;
            row.ChartPointIndex=index;
        }
    }

    /// <summary>
    /// SyncChartPoints メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="target">target を指定して実行条件を制御します。</param>
    /// <param name="points">points を指定して実行条件を制御します。</param>
    private void SyncChartPoints(ObservableCollection<ChartComparisonPoint> target,IReadOnlyList<ChartComparisonPoint> points) {
        if(target.Count!=points.Count) {
            target.Clear();
            foreach(var point in points) {
                target.Add(point);
            }

            return;
        }

        for(var index = 0; index<points.Count; index++) {
            var point = points[index];
            var current = target[index];
            if(current is not null
                &&ReferenceEquals(current.Row,point.Row)
                &&current.Benchmark==point.Benchmark
                &&current.DisplayName==point.DisplayName
                &&Math.Abs(current.LeftValue-point.LeftValue)<0.000001
                &&Math.Abs(current.RightValue-point.RightValue)<0.000001
                &&current.LeftText==point.LeftText
                &&current.RightText==point.RightText
                &&current.TooltipText==point.TooltipText
                &&current.IsLeftFallback==point.IsLeftFallback
                &&current.IsRightFallback==point.IsRightFallback
                &&current.IsLeftUiSelected==point.IsLeftUiSelected
                &&current.IsRightUiSelected==point.IsRightUiSelected
                &&current.IsRowUiSelected==point.IsRowUiSelected) {
                continue;
            }

            target[index]=point;
        }
    }

    /// <summary>
    /// BuildBenchmarkChartPoints メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(IReadOnlyList<BenchmarkComparisonRow> rows) {
        return this.BuildBenchmarkChartPoints(this.SelectedSourceTabViewState,rows);
    }

    /// <summary>
    /// BuildBenchmarkChartPoints メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(BenchmarkSourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        var rowSnapshot = state.Rows.ToArray();
        var gridOrderPoints = rowSnapshot
            .Select(row => this.CreateBenchmarkChartPoint(row,rows,state.Definition))
            .Where(static point => point is not null)
            .Select(static point => point!)
            .ToArray();
        return BenchmarkChartDisplayOrderLogic.CreateTransposedSourceOrder(gridOrderPoints);
    }

    /// <summary>
    /// CreateBenchmarkChartPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint? CreateBenchmarkChartPoint(
        BenchmarkTableRow row,
        IReadOnlyList<BenchmarkComparisonRow> rows) =>
        this.CreateBenchmarkChartPoint(row,rows,this.SelectedSourceDefinition);

    /// <summary>
    /// CreateBenchmarkChartPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint? CreateBenchmarkChartPoint(
        BenchmarkTableRow row,
        IReadOnlyList<BenchmarkComparisonRow> rows,
        BenchmarkSourceTabDefinition sourceTab) {
        var comparisonRow = FindComparisonRow(row,rows);
        var left = this.GetChartMetrics(row,sourceTab.LeftProject,comparisonRow);
        var right = this.GetChartMetrics(row,sourceTab.RightProject,comparisonRow);
        var leftValue = GetMetricValue(left,this.SelectedMetric.Kind);
        var rightValue = GetMetricValue(right,this.SelectedMetric.Kind);
        if(leftValue is not null&&rightValue is not null) {
            var measuredPoint = this.ApplyChartUiSelection(row,this.CreateChartComparisonPoint(row,leftValue,rightValue,sourceTab.Pair));
            return this.ApplyChartRunningState(measuredPoint);
        }

        return null;
    }

    /// <summary>
    /// FindComparisonRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkComparisonRow? FindComparisonRow(BenchmarkTableRow row,IReadOnlyList<BenchmarkComparisonRow> rows) {
        foreach(var item in rows) {
            if(IsComparisonRowMatch(row,item)) {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// IsComparisonRowMatch メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool IsComparisonRowMatch(BenchmarkTableRow row,BenchmarkComparisonRow item) =>
        BenchmarkComparisonRowMatchLogic.IsMatch(
            row.MethodName,
            row.BenchmarkName,
            row.SourceTab.Tab,
            row.CasesByProject.Values.Select(static benchmark => new BenchmarkComparisonRowMatchCase(benchmark.TypeFullName,benchmark.ArgumentKey)),
            item);

    /// <summary>
    /// CreateChartComparisonPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="leftValue">leftValue を指定して実行条件を制御します。</param>
    /// <param name="rightValue">rightValue を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint CreateChartComparisonPoint(BenchmarkTableRow row,double? leftValue,double? rightValue) =>
        this.CreateChartComparisonPoint(row.RowKey,row.BenchmarkName,leftValue,rightValue,row,this.SelectedComparisonPair);

    /// <summary>
    /// CreateChartComparisonPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="leftValue">leftValue を指定して実行条件を制御します。</param>
    /// <param name="rightValue">rightValue を指定して実行条件を制御します。</param>
    /// <param name="pair">pair を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint CreateChartComparisonPoint(BenchmarkTableRow row,double? leftValue,double? rightValue,ComparisonPairOption pair) =>
        this.CreateChartComparisonPoint(row.RowKey,row.BenchmarkName,leftValue,rightValue,row,pair);

    /// <summary>
    /// CreateChartComparisonPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="benchmark">benchmark を指定して実行条件を制御します。</param>
    /// <param name="displayName">displayName を指定して実行条件を制御します。</param>
    /// <param name="leftValue">leftValue を指定して実行条件を制御します。</param>
    /// <param name="rightValue">rightValue を指定して実行条件を制御します。</param>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="pair">pair を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint CreateChartComparisonPoint(string benchmark,string displayName,double? leftValue,double? rightValue,BenchmarkTableRow row,ComparisonPairOption pair) {
        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var isLeftFallback = this.IsChartMetricFallback(row,pair.LeftProject,leftValue);
        var isRightFallback = this.IsChartMetricFallback(row,pair.RightProject,rightValue);
        var share=指標シェアを作成(leftValue,rightValue,this.SelectedMetric.Kind);
        var leftShareText=share.左シェア?.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)??"-";
        var rightShareText=share.右シェア?.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)??"-";
        var metricName = this.SelectedMetric.Kind==BenchmarkMetricKind.Time ? "Time" : "Memory";
        var fallbackTooltip = this.CreateChartFallbackTooltip(pair,isLeftFallback,isRightFallback);
        var shortName = BenchmarkFormatting.ShortName(displayName);
        var tooltip = "ベンチマーク: "+displayName
            +(string.Equals(shortName,displayName,StringComparison.OrdinalIgnoreCase) ? string.Empty : "\n表示名: "+shortName)
            +"\n"
            +pair.LeftProject+" "+metricName+": "+FirstText(FormatMetric(leftValue,this.SelectedMetric.Kind),"-")+"\n"
            +pair.RightProject+" "+metricName+": "+FirstText(FormatMetric(rightValue,this.SelectedMetric.Kind),"-")
            +"\nShare: "+pair.LeftProject+" "+leftShareText+" / "+pair.RightProject+" "+rightShareText
            +fallbackTooltip
            +(string.IsNullOrWhiteSpace(BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(row))
                ? string.Empty
                : "\n"+BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(row));
        var point=new ChartComparisonPoint(
            benchmark,
            BenchmarkFormatting.ShortName(displayName),
            share.左シェア??0,
            share.右シェア??0,
            leftShareText,
            rightShareText,
            tooltip,
            share.左シェア)
            .WithFallbackState(isLeftFallback,isRightFallback);
        point.AttachRow(row);
        return point;
    }

    private bool IsChartMetricFallback(BenchmarkTableRow row,string project,double? metricValue) {
        if(metricValue is null) {
            return false;
        }
        if(this.SelectedResultElementCount is not { } selectedElementCount) {
            return false;
        }
        if(this.ShouldUseCurrentRunMemoryForDisplay(this.SelectedSourceDefinition)) {
            return false;
        }

        var typeFullName = GetRowTypeFullName(row,project);
        var argumentKey = GetRowArgumentKey(row,project);
        return !this.HasResult(row.MethodName,project,typeFullName,selectedElementCount.Value,argumentKey);
    }

    private string CreateChartFallbackTooltip(ComparisonPairOption pair,bool isLeftFallback,bool isRightFallback) {
        if(!isLeftFallback&&!isRightFallback) {
            return string.Empty;
        }

        var selectedN = this.SelectedResultElementCount?.DisplayName??"-";
        if(isLeftFallback&&isRightFallback) {
            return "\nFallback: "+pair.LeftProject+" / "+pair.RightProject+" は選択軸 "+selectedN+" ではなく最新完了結果を使用";
        }
        if(isLeftFallback) {
            return "\nFallback: "+pair.LeftProject+" は選択軸 "+selectedN+" ではなく最新完了結果を使用";
        }

        return "\nFallback: "+pair.RightProject+" は選択軸 "+selectedN+" ではなく最新完了結果を使用";
    }

    /// <summary>
    /// CreateChartArgumentTooltip メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string CreateChartArgumentTooltip(BenchmarkTableRow row) {
        return BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(row);
    }

    /// <summary>
    /// ApplyChartRunningState メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="point">point を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint ApplyChartRunningState(ChartComparisonPoint point) =>
        point.WithRunningState(
            this.IsBenchmarkRunActive&&string.Equals(point.Benchmark,this.activeChartMethodName,StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ApplyChartUiSelection メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="point">point を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint ApplyChartUiSelection(BenchmarkTableRow row,ChartComparisonPoint point) =>
        point.WithUiSelection(row.LeftCell.IsUiSelected,row.RightCell.IsUiSelected,row.IsUiSelected);

    /// <summary>
    /// RefreshChartUiSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    private void RefreshChartUiSelection(BenchmarkSourceTabViewState state,IEnumerable<BenchmarkTableRow> changedRows) {
        var selectionChanged=false;
        foreach(var row in changedRows) {
            if(row.ChartBar is not { } chartBar) {
                continue;
            }

            if(row.IsUiSelected) {
                selectionChanged|=state.UiSelectedChartBars.Add(chartBar);
            }else{
                selectionChanged|=state.UiSelectedChartBars.Remove(chartBar);
            }
        }

        if(selectionChanged) {
            state.NotifyChartSelectionChanged();
        }
    }

    private void RebuildChartPointIndexes(BenchmarkSourceTabViewState state) {
        state.ChartPointIndexesByRowKey.Clear();
        state.UiSelectedChartBars.Clear();
        for(var index = 0; index<state.Rows.Count; index++) {
            var row=state.Rows[index];
            row.ChartBar=null;
            row.ChartPointIndex=-1;
            row.GridDisplayIndex=index;
        }
        for(var index = 0; index<state.ChartPoints.Count; index++) {
            var point=state.ChartPoints[index];
            state.ChartPointIndexesByRowKey[point.Benchmark]=index;
            if(point.Row is not { } row) {
                continue;
            }

            row.ChartBar=point;
            row.ChartPointIndex=index;
            if(row.IsUiSelected) {
                state.UiSelectedChartBars.Add(point);
            }
        }
        state.NotifyChartSelectionChanged();
    }

    /// <summary>
    /// ApplyChartRunningState メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyChartRunningState() {
        var state=this.SelectedSourceTabViewState;
        this.ApplyChartRunningState(state,this.appliedActiveChartMethodName,false);
        this.ApplyChartRunningState(state,this.activeChartMethodName,this.IsBenchmarkRunActive);
        this.appliedActiveChartMethodName=this.activeChartMethodName;
    }

    private void ApplyChartRunningState(BenchmarkSourceTabViewState state,string rowKey,bool isRunning) {
        if(string.IsNullOrWhiteSpace(rowKey)
            ||!state.ChartPointIndexesByRowKey.TryGetValue(rowKey,out var index)
            ||index<0
            ||index>=state.ChartPoints.Count) {
            return;
        }

        var point=state.ChartPoints[index];
        var next=point.WithRunningState(isRunning);
        point.ApplyFrom(next);
        state.ChartPoints[index]=point;
    }

    /// <summary>
    /// GetChartMetrics メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="comparisonRow">comparisonRow を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ProjectMetrics GetChartMetrics(
        BenchmarkTableRow row,
        string project,
        BenchmarkComparisonRow? comparisonRow) {
        if(this.SelectedResultElementCount is not null) {
            var comparisonMetrics = comparisonRow?.GetProject(project)??ProjectMetrics.Missing;
            if(comparisonMetrics.HasMeasuredValue) {
                return comparisonMetrics;
            }

            var retainedMetrics = row.GetProjectCell(project)?.Metrics;
            if(retainedMetrics is { HasMeasuredValue: true }) {
                return retainedMetrics.Value;
            }

            return ProjectMetrics.Missing;
        }

        var cellMetrics = row.GetProjectCell(project)?.Metrics;
        if(cellMetrics is { HasMeasuredValue: true }) {
            return cellMetrics.Value;
        }

        return comparisonRow?.GetProject(project)??ProjectMetrics.Missing;
    }

    /// <summary>
    /// BuildElementComparisonRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildElementComparisonRows() {
        return this.BuildElementComparisonRows(this.SelectedSourceTabViewState);
    }

    /// <summary>
    /// BuildElementComparisonRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkComparisonRow> BuildElementComparisonRows(BenchmarkSourceTabViewState state) {
        var sourceTab = state.Definition;
        var selectedN = this.SelectedResultElementCount?.Value;
        if(!this.IsBenchmarkRunActive) {
            return state.ComparisonRows;
        }

        return selectedN is { } elementCount
            ? this.BuildCurrentRunComparisonRowsForElementCount(sourceTab,elementCount)
            : this.BuildCurrentRunComparisonRows(sourceTab);
    }

    /// <summary>
    /// CreateReportRowForSelectedElementCount メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static ReportRow CreateReportRowForSelectedElementCount(BenchmarkResultItem item) =>
        new(
            item.Project,
            item.Method,
            item.TypeFullName,
            item.Method,
            item.Status,
            item.Mean,
            string.Empty,
            string.Empty,
            item.AllocatedBytesValue?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.MeanNanoseconds??0,
            item.ErrorNanoseconds??0,
            item.StdDevNanoseconds??0,
            item.AllocatedBytesValue??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            item.ArtifactFolder,
            item.ExitCode,
            item.ArgumentKey,
            item.Arguments);

    /// <summary>
    /// CreateReportRowsForComparison メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="sourceResults">sourceResults を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ReportRow[] CreateReportRowsForComparison(BenchmarkSourceTabDefinition sourceTab,IReadOnlyList<BenchmarkResultItem> sourceResults) {
        var selectedSourceResults = sourceResults
            .Where(result => this.IsResultInSourceTab(result,sourceTab))
            .ToArray();
        var rows = selectedSourceResults
            .Select(CreateReportRowForSelectedElementCount)
            .ToList();
        if(ScalableBenchmarkNameFormatter.UsesPreviousRightColumn(sourceTab.Tab)&&!string.IsNullOrWhiteSpace(sourceTab.RightProject)) {
            foreach(var result in selectedSourceResults) {
                var previous = this.uiRunRepository.FindPreviousResult(
                    GetUiRunAppKind(),
                    sourceTab,
                    result.Method,
                    result.Project,
                    result.TypeFullName,
                    result.AxisCount,
                    result.StartedAt,
                    result.ArgumentKey,
                    this.activeUiRunId??this.unsavedUiRunId);
                if(previous is null) {
                    continue;
                }

                rows.Add(CreateReportRow(
                    sourceTab.RightProject,
                    result.Method,
                    result.TypeFullName,
                    previous.ToProjectMetrics(),
                    previous.ArgumentKey,
                    previous.Arguments));
            }
        }

        return rows
            .GroupBy(static row => row.Project+"|"+row.TypeFullName+"|"+row.Method+"|"+row.ArgumentKey,StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .ToArray();
    }

    /// <summary>
    /// CreateReportRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="typeFullName">typeFullName を指定して実行条件を制御します。</param>
    /// <param name="metrics">metrics を指定して実行条件を制御します。</param>
    /// <param name="argumentKey">argumentKey を指定して実行条件を制御します。</param>
    /// <param name="arguments">arguments を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static ReportRow CreateReportRow(
        string project,
        string method,
        string typeFullName,
        ProjectMetrics metrics,
        string argumentKey = "",
        IReadOnlyList<BenchmarkArgument>? arguments = null) =>
        new(
            project,
            method,
            typeFullName,
            method,
            metrics.HasMeasuredValue ? "Completed" : "Missing",
            metrics.TimeNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            metrics.AllocatedBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            metrics.TimeNanoseconds??0,
            0,
            0,
            metrics.AllocatedBytes??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            metrics.HasMeasuredValue ? 0 : null,
            argumentKey,
            arguments);

    /// <summary>
    /// BuildScaleTrendPoints メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ChartComparisonPoint> BuildScaleTrendPoints() {
        var selectedRow = this.BenchmarkRows.FirstOrDefault(static item => item.IsSelected);
        var method = selectedRow?.MethodName
            ??this.ResultItems.FirstOrDefault()?.Method
            ??string.Empty;
        if(string.IsNullOrWhiteSpace(method)) {
            return [];
        }

        var typeFullName = selectedRow is null ? string.Empty : GetRowTypeFullName(selectedRow,this.SelectedSourceDefinition.LeftProject);
        var sourceTab = this.SelectedSourceDefinition;
        var currentRunResults = this.GetCurrentRunResultItems(sourceTab.Tab);
        var rows = this.uiRunRepository.LoadResultItems(GetUiRunAppKind(),sourceTab)
            .Concat(currentRunResults.Where(item => sourceTab.ContainsProject(item.Project)))
            .Where(item => item.AxisCount>0
                &&string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase)
                &&IsSameType(item.TypeFullName,typeFullName))
            .GroupBy(static item => item.AxisCount)
            .OrderBy(static group => group.Key)
            .Select(group => CreateScaleTrendPoint(group.Key,group.ToArray(),this.SelectedComparisonPair,this.SelectedMetric.Kind))
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToArray();
        return rows;
    }

    /// <summary>
    /// CreateScaleTrendPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <param name="results">results を指定して実行条件を制御します。</param>
    /// <param name="pair">pair を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static ChartComparisonPoint? CreateScaleTrendPoint(
        int elementCount,
        IReadOnlyList<BenchmarkResultItem> results,
        ComparisonPairOption pair,
        BenchmarkMetricKind metric) {
        var left = results.FirstOrDefault(item => string.Equals(item.Project,pair.LeftProject,StringComparison.OrdinalIgnoreCase));
        var right = results.FirstOrDefault(item => string.Equals(item.Project,pair.RightProject,StringComparison.OrdinalIgnoreCase));
        if(left is null||right is null
            ||!string.Equals(left.Status,"Completed",StringComparison.OrdinalIgnoreCase)
            ||!string.Equals(right.Status,"Completed",StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var leftValue = GetMetricValue(left,metric);
        var rightValue = GetMetricValue(right,metric);
        if(leftValue is null||rightValue is null) {
            return null;
        }
        var leftText = FormatMetric(leftValue,metric);
        var rightText = FormatMetric(rightValue,metric);
        return new ChartComparisonPoint(
            elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),
            elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),
            Math.Max(0,leftValue??0),
            Math.Max(0,rightValue??0),
            leftText,
            rightText,
            $"{pair.LeftProject}: {FirstText(leftText,"-")}\n{pair.RightProject}: {FirstText(rightText,"-")}",
            null);
    }

    /// <summary>
    /// GetMetricValue メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static double? GetMetricValue(BenchmarkResultItem? item,BenchmarkMetricKind metric) {
        if(item is null) {
            return null;
        }

        return metric==BenchmarkMetricKind.Time
            ? item.MeanNanoseconds
            : item.AllocatedBytesValue;
    }

    /// <summary>
    /// GetMetricValue メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="metrics">metrics を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static double? GetMetricValue(ProjectMetrics metrics,BenchmarkMetricKind metric) {
        if(!metrics.HasMeasuredValue) {
            return null;
        }

        return metric==BenchmarkMetricKind.Time
            ? metrics.TimeNanoseconds
            : metrics.AllocatedBytes;
    }

    /// <summary>
    /// FormatMetric メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="value">value を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string FormatMetric(double? value,BenchmarkMetricKind metric) {
        if(value is null) {
            return string.Empty;
        }

        return metric==BenchmarkMetricKind.Time
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : BenchmarkFormatting.FormatBytes((long)Math.Round(value.Value,MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// SetMetric メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="kind">kind を指定して実行条件を制御します。</param>
    private void SetMetric(BenchmarkMetricKind kind) {
        var option = this.Metrics.First(item => item.Kind==kind);
        if(!ReferenceEquals(this.selectedMetric,option)) {
            this.SelectedMetric=option;
        }
    }

    /// <summary>
    /// AppendLogText メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="text">text を指定して実行条件を制御します。</param>
    private void AppendLogText(string text) {
        if(this.isClosing) {
            return;
        }

        this.uiDispatcher.BeginInvoke(() => {
            if(this.isClosing) {
                return;
            }

            try {
                this.LogText+=text;
                this.LogEntries.Add(text);
            }
            catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException) {
                System.Diagnostics.Debug.WriteLine("Benchmark UI log append ignored during close: "+ex);
            }
        });
    }

    // ログ一覧のDataTemplate側でこの接頭辞を検出し、太字/強調色で目立たせる(CreateLogView参照)。
    /// <summary>
    /// HighlightedLogEntryPrefix フィールドです。
    /// </summary>
    internal const string HighlightedLogEntryPrefix = "⚠ ";

    /// <summary>
    /// AppendHighlightedLogEntry メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="message">message を指定して実行条件を制御します。</param>
    private void AppendHighlightedLogEntry(string message) {
        if(this.isClosing) {
            return;
        }

        this.uiDispatcher.BeginInvoke(() => {
            if(this.isClosing) {
                return;
            }

            try {
                this.SelectedLowerTab=BenchmarkLowerTab.Log;
                var text = HighlightedLogEntryPrefix+message;
                this.LogText+=text+Environment.NewLine;
                this.LogEntries.Add(text);
            }
            catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException) {
                System.Diagnostics.Debug.WriteLine("Benchmark UI highlighted log append ignored during close: "+ex);
            }
        });
        StartupDiagnostics.Write(message);
    }

    /// <summary>
    /// FirstText メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="values">values を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string FirstText(params string[] values) {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;
    }

    /// <summary>
    /// GetChartLegendLabel メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string GetChartLegendLabel(string project) =>
        BenchmarkSourceTabs.TryGetByProject(project) is { } tab
            ? string.Equals(project,tab.LeftProject,StringComparison.OrdinalIgnoreCase)
                ? tab.LeftHeader
                : tab.RightHeader
            : project;

    /// <summary>
    /// RefreshBenchmarkSortColumns メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkSortColumns() {
        this.HasBenchmarkTypeColumn=true;
        this.RefreshBenchmarkSortColumnOptions();
        var rules = this.CreateValidSortRules(this.BenchmarkSortRules).ToArray();
        if(rules.Length==0) {
            rules=this.CreateInitialSortRules().ToArray();
        }
        this.SetBenchmarkSortRules(rules,false);
    }

    /// <summary>
    /// RefreshBenchmarkSortColumnOptions メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkSortColumnOptions() {
        var columns = this.CreateAvailableSortColumns().ToArray();
        this.BenchmarkSortColumnOptions.Clear();
        foreach(var column in columns) {
            this.BenchmarkSortColumnOptions.Add(new BenchmarkSortColumnOption(column,BenchmarkSortRule.GetColumnText(column)));
        }
    }

    /// <summary>
    /// CreateAvailableSortColumns メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<BenchmarkGridSortColumn> CreateAvailableSortColumns() {
        if(this.HasBenchmarkTypeColumn) {
            yield return BenchmarkGridSortColumn.BenchmarkType;
        }
        yield return BenchmarkGridSortColumn.BenchmarkMethod;
        yield return BenchmarkGridSortColumn.LogicalCpu;
        yield return BenchmarkGridSortColumn.ループ回数;
        foreach(var column in BenchmarkArgumentColumns.Get(this.SelectedSourceDefinition)) {
            yield return column switch {
                ScalableBenchmarkArgumentColumn.Count => BenchmarkGridSortColumn.ArgumentCount,
                ScalableBenchmarkArgumentColumn.Index => BenchmarkGridSortColumn.ArgumentIndex,
                ScalableBenchmarkArgumentColumn.Page => BenchmarkGridSortColumn.ArgumentPage,
                ScalableBenchmarkArgumentColumn.Segment => BenchmarkGridSortColumn.ArgumentSegment,
                ScalableBenchmarkArgumentColumn.Memory => BenchmarkGridSortColumn.ArgumentMemory,
                ScalableBenchmarkArgumentColumn.Fraction => BenchmarkGridSortColumn.ArgumentFraction,
                ScalableBenchmarkArgumentColumn.Initial => BenchmarkGridSortColumn.ArgumentInitial,
                ScalableBenchmarkArgumentColumn.Data => BenchmarkGridSortColumn.ArgumentData,
                _ => BenchmarkGridSortColumn.BenchmarkMethod,
            };
        }
        yield return BenchmarkGridSortColumn.左日時;
        yield return BenchmarkGridSortColumn.LeftTime;
        yield return BenchmarkGridSortColumn.LeftAllocated;
        yield return BenchmarkGridSortColumn.時間シェア;
        yield return BenchmarkGridSortColumn.メモリシェア;
        yield return BenchmarkGridSortColumn.右日時;
        yield return BenchmarkGridSortColumn.RightTime;
        yield return BenchmarkGridSortColumn.RightAllocated;
    }

    /// <summary>
    /// CreateValidSortRules メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="source">source を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkSortRule> CreateValidSortRules(IEnumerable<BenchmarkSortRule> source) {
        var allowed = this.CreateAvailableSortColumns().ToHashSet();
        var used = new HashSet<BenchmarkGridSortColumn>();
        return source
            .Where(rule => allowed.Contains(rule.Column)&&used.Add(rule.Column))
            .Select(rule => new BenchmarkSortRule(rule.Column,rule.Descending))
            .ToArray();
    }

    /// <summary>
    /// CreateInitialSortRules メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkSortRule> CreateInitialSortRules() {
        var restored = this.CreateValidSortRules(this.savedSortRuleSnapshots.Select(static item => new BenchmarkSortRule(item.Column,item.Descending)));
        if(restored.Count>0) {
            return restored;
        }

        return this.CreateDefaultSortRules();
    }

    /// <summary>
    /// CreateDefaultSortRules メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkSortRule> CreateDefaultSortRules() {
        if(this.HasBenchmarkTypeColumn) {
            return [
                new BenchmarkSortRule(BenchmarkGridSortColumn.BenchmarkType),
                new BenchmarkSortRule(BenchmarkGridSortColumn.BenchmarkMethod),
            ];
        }

        return [new BenchmarkSortRule(BenchmarkGridSortColumn.BenchmarkMethod)];
    }

    /// <summary>
    /// ResetBenchmarkSortRulesToDefault メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="save">save を指定して実行条件を制御します。</param>
    private void ResetBenchmarkSortRulesToDefault(bool save) =>
        this.SetBenchmarkSortRules(this.CreateDefaultSortRules(),save);

    /// <summary>
    /// GetEffectiveSortRules メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkSortRule> GetEffectiveSortRules() {
        var rules = this.CreateValidSortRules(this.BenchmarkSortRules);
        return rules.Count>0 ? rules : this.CreateDefaultSortRules();
    }

    /// <summary>
    /// RefreshCurrentSortIndicator メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshCurrentSortIndicator() {
        var first = this.GetEffectiveSortRules().FirstOrDefault();
        if(first is null) {
            this.CurrentSortColumn=BenchmarkGridSortColumn.BenchmarkMethod;
            this.CurrentSortDescending=false;
            return;
        }

        this.CurrentSortColumn=first.Column;
        this.CurrentSortDescending=first.Descending;
    }

    /// <summary>
    /// LoadSavedBenchmarkSortRules メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkSortRuleSnapshot> LoadSavedBenchmarkSortRules() {
        try {
            var json = this.uiPreferences.Get(BenchmarkSortSettingsPreferenceKey,string.Empty);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<BenchmarkSortRuleSnapshot[]>(json)?? [];
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark sort settings load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// SaveBenchmarkSortRules メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void SaveBenchmarkSortRules() {
        try {
            var snapshot = this.BenchmarkSortRules
                .Select(static rule => new BenchmarkSortRuleSnapshot(rule.Column,rule.Descending))
                .ToArray();
            this.savedSortRuleSnapshots=snapshot;
            this.uiPreferences.Set(BenchmarkSortSettingsPreferenceKey,JsonSerializer.Serialize(snapshot));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark sort settings save failed.",ex);
        }
    }

    /// <summary>
    /// ApplyCurrentSort メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyCurrentSort() {
        if(this.IsBenchmarkRunActive||this.IsBenchmarkPaused) {
            // 実行中/一時停止中は行の並び替えを行わない(index主導更新の前提: 行位置を固定する)。
            return;
        }
        using(this.BeginDataGridUpdateBatch()) {
            var sortRules = this.GetEffectiveSortRules();
            foreach(var state in this.sourceTabViewStates) {
                if(state.Rows.Count<2) {
                    continue;
                }

                var ordered = state.Rows
                    .OrderBy(row => row,Comparer<BenchmarkTableRow>.Create((left,right) => {
                        foreach(var rule in sortRules) {
                            var result = BenchmarkSortValue.Compare(
                                left.GetSortValue(rule.Column),
                                right.GetSortValue(rule.Column),
                                rule.Descending);
                            if(result!=0) {
                                return result;
                            }
                        }

                        var typeResult = string.Compare(left.BenchmarkTypeText,right.BenchmarkTypeText,StringComparison.OrdinalIgnoreCase);
                        if(typeResult!=0) {
                            return typeResult;
                        }
                        var methodResult = string.Compare(left.BenchmarkMethodText,right.BenchmarkMethodText,StringComparison.OrdinalIgnoreCase);
                        return methodResult!=0
                            ? methodResult
                            : string.Compare(left.RowKey,right.RowKey,StringComparison.OrdinalIgnoreCase);
                    }))
                    .ToArray();
                state.Rows.ReplaceRange(ordered);
            }
            this.UpdateAllSourceTabCharts();
        }
    }

    /// <summary>
    /// ApplyFinalBenchmarkSort メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyFinalBenchmarkSort() {
        this.SetBenchmarkSortRules([new BenchmarkSortRule(BenchmarkGridSortColumn.時間シェア,true)],true);
    }

    /// <summary>
    /// BenchmarkSortRuleSnapshot メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="Column">Column を指定して実行条件を制御します。</param>
    /// <param name="Descending">Descending を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct BenchmarkSortRuleSnapshot(BenchmarkGridSortColumn Column,bool Descending);

    /// <summary>
    /// 現 在 の 状 態 を 再 構 築 し 、 表 示 に 反 映 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void RebuildCpuSections() {
        this.CpuSections.Clear();
        this.AddCpuSection("P core",CpuCoreKind.Performance);
        this.AddCpuSection("E core",CpuCoreKind.Efficiency);
        this.AddCpuSection("Unknown core",CpuCoreKind.Unknown);
        this.OnPropertyChanged(nameof(this.PerformanceCpuSections));
        this.OnPropertyChanged(nameof(this.EfficiencyCpuSections));
        this.OnPropertyChanged(nameof(this.UnknownCpuSections));
    }

    /// <summary>
    /// GetCpuSections メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="kind">kind を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<CpuTableSection> GetCpuSections(CpuCoreKind kind) =>
        this.CpuSections.Where(section => section.CoreKind==kind);

    /// <summary>
    /// AddCpuSection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="title">title を指定して実行条件を制御します。</param>
    /// <param name="kind">kind を指定して実行条件を制御します。</param>
    private void AddCpuSection(string title,CpuCoreKind kind) {
        var groups = this.CpuItems
            .Where(cpu => cpu.CoreKind==kind)
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Min(static cpu => cpu.LogicalProcessorIndex))
            .Select(static group => group.OrderBy(static cpu => cpu.LogicalProcessorIndex).ToArray())
            .Where(static group => group.Length>0)
            .ToArray();
        if(groups.Length==0) {
            return;
        }

        var columnCount = groups.Max(static group => group.Length);
        var columns = Enumerable.Range(0,columnCount)
            .Select(static index => new CpuTableColumn(index,"Select"))
            .ToArray();
        var rows = groups
            .Select(group => new CpuTableRow(
                CreateCpuRangeLabel(group),
                group[0].CoreKind,
                group.Select(static cpu => new CpuTableCell(cpu)).ToArray(),
                columnCount))
            .ToArray();
        this.CpuSections.Add(new CpuTableSection(title,kind,columns,rows));
    }

    /// <summary>
    /// CreateCpuRangeLabel メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cpus">cpus を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateCpuRangeLabel(IReadOnlyList<CpuSelectionItem> cpus) {
        var min = cpus.Min(static cpu => cpu.LogicalProcessorIndex);
        var max = cpus.Max(static cpu => cpu.LogicalProcessorIndex);
        return min==max
            ? min.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : min.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+max.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// ApplyCpuSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="cpus">cpus を指定して実行条件を制御します。</param>
    /// <param name="selected">selected を指定して実行条件を制御します。</param>
    private void ApplyCpuSelection(IEnumerable<CpuSelectionItem> cpus,bool selected) {
        if(this.IsRefreshing&&!this.isApplyingCpuSelection) {
            return;
        }

        var targets = cpus.Distinct().ToArray();
        if(targets.Length==0) {
            return;
        }

        this.isApplyingCpuSelection=true;
        try {
            foreach(var cpu in targets) {
                cpu.IsSelected=selected;
            }
        }
        finally {
            this.isApplyingCpuSelection=false;
            this.RefreshSelectedLogicalCpus();
            this.RefreshCpuSectionStates();
        }
    }

    /// <summary>
    /// EnsureAtLeastOneCpuSelected メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="fallback">fallback を指定して実行条件を制御します。</param>
    /// <summary>
    /// GetSelectedLogicalCpus メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<int> GetSelectedLogicalCpus() =>
        Volatile.Read(ref this.selectedLogicalCpus);

    /// <summary>
    /// RefreshSelectedLogicalCpus メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshSelectedLogicalCpus() {
        Volatile.Write(ref this.selectedLogicalCpus,this.CreateSelectedLogicalCpus());
        this.RunCommand.NotifyCanExecuteChanged();
#if DEBUG
        this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
        this.ToggleRunCommand.NotifyCanExecuteChanged();
        this.OnPropertyChanged(nameof(this.CanRunBenchmark));
        this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
    }

    /// <summary>
    /// CreateSelectedLogicalCpus メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private int[] CreateSelectedLogicalCpus() =>
        this.CpuItems
            .Where(static cpu => cpu.IsSelected)
            .Select(static cpu => cpu.LogicalProcessorIndex)
            .ToArray();

    /// <summary>
    /// RefreshCpuSectionStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshCpuSectionStates() {
        foreach(var section in this.CpuSections) {
            section.RefreshState();
            foreach(var column in section.Columns) {
                this.OnPropertyChanged("CpuColumnState"+section.Title+column.Index);
            }
        }
    }

}
