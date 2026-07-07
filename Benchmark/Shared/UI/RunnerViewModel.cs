using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Seamless.UI.Storage;

namespace UI;

public sealed partial class RunnerViewModel:ObservableObject,
    IRunOrchestratorContext,
    IResultPipelineApplierContext,
    ISelectionControllerContext,
    IChartControllerContext,
    ISortControllerContext,
    ICpuTopologyControllerContext,
    IRunStatePersistenceContext {
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
    /// RunningVisualMinimumDuration フィールドです。
    /// </summary>
    private static readonly TimeSpan RunningVisualMinimumDuration = TimeSpan.FromMilliseconds(250);

    private static readonly BenchmarkRunOption NewRunOption = new(
        NewRunNumber,
        0,
        "New",
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
    private readonly ICatalogService catalogService;
    /// <summary>
    /// cpuTopologyService フィールドです。
    /// </summary>
    private readonly ICpuTopologyService cpuTopologyService;
    /// <summary>
    /// runService フィールドです。
    /// </summary>
    private readonly IRunService runService;
    /// <summary>
    /// comparisonService フィールドです。
    /// </summary>
    private readonly IComparisonService comparisonService;
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
    private readonly IUiDispatcher uiDispatcher;
    /// <summary>
    /// uiPreferences フィールドです。
    /// </summary>
    private readonly IUiPreferences uiPreferences;
    /// <summary>
    /// runEventPipeline フィールドです。
    /// </summary>
    private readonly RunEventPipeline runEventPipeline;
    /// <summary>
    /// pipelineDiagnostics フィールドです。
    /// </summary>
    private readonly UiDiagnostics pipelineDiagnostics;
    /// <summary>
    /// renderScheduler フィールドです。
    /// </summary>
    private readonly UiRenderScheduler renderScheduler;
    /// <summary>
    /// persistenceBatcher フィールドです。
    /// </summary>
    private readonly PersistenceBatcher persistenceBatcher;
    private readonly IRunOrchestrator runOrchestrator;
    private readonly IResultPipelineApplier resultPipelineApplier;
    private readonly ISelectionController selectionController;
    private readonly IChartController chartController;
    private readonly ISortController sortController;
    private readonly ICpuTopologyController cpuTopologyController;
    private readonly IRunStatePersistence runStatePersistence;
    private readonly IRunVisualScheduler runVisualScheduler;
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
    private Tab selectedTab = Tab.Cpu;
    /// <summary>
    /// selectedSourceTab フィールドです。
    /// </summary>
    private SourceTabOption selectedSourceTab;
    /// <summary>
    /// sourceTabViewStates フィールドです。
    /// </summary>
    private IReadOnlyList<SourceTabViewState> sourceTabViewStates = [];
    /// <summary>
    /// sourceTabViewStatesByTab フィールドです。
    /// </summary>
    private readonly Dictionary<BenchmarkSourceTab,SourceTabViewState> sourceTabViewStatesByTab = [];
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
    /// <summary>
    /// generatedCodeAuditReport フィールドです。
    /// </summary>
    private GeneratedCodeAuditReport? generatedCodeAuditReport;
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
    /// isLoadingSelectedRunDb フィールドです。
    /// </summary>
    private bool isLoadingSelectedRunDb;
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
    private GridSortColumn currentSortColumn = GridSortColumn.BenchmarkMethod;
    /// <summary>
    /// currentSortDescending フィールドです。
    /// </summary>
    private bool currentSortDescending;
    /// <summary>
    /// benchmarkSelectionAnchors フィールドです。
    /// </summary>
    private readonly Dictionary<BenchmarkSourceTab,BenchmarkCellSelectionAnchor?> benchmarkSelectionAnchors = [];
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
    /// <summary>
    /// benchmarkCellSelections フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> benchmarkCellSelections = new(StringComparer.OrdinalIgnoreCase);
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
    private readonly Dictionary<string,TableRow> benchmarkRowsByResultKey = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// benchmarkRowsByProjectMethod フィールドです。
    /// </summary>
    private readonly Dictionary<string,TableRow> benchmarkRowsByProjectMethod = new(StringComparer.OrdinalIgnoreCase);
    // RunItemIndex -> 行のO(1)照合キャッシュ。実行中は文字列キー照合(上記2つ)を都度使わず、この辞書で更新先行を確定する。
    /// <summary>
    /// benchmarkRowsByRunItemIndex フィールドです。
    /// </summary>
    private readonly Dictionary<int,TableRow> benchmarkRowsByRunItemIndex = new();
    /// <summary>
    /// generatedCodeAuditCache フィールドです。
    /// </summary>
    private readonly Dictionary<string,GeneratedCodeAuditReport> generatedCodeAuditCache = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// runningVisualStartedAt フィールドです。
    /// </summary>
    private readonly Dictionary<string,DateTimeOffset> runningVisualStartedAt = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// sourceTabRunningState フィールドです。
    /// </summary>
    private readonly BenchmarkSourceTabRunningState sourceTabRunningState = new();
    /// <summary>
    /// catalogCases フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkCase> catalogCases = [];
    /// activeRunSourceTab フィールドです。
    /// </summary>
    private BenchmarkSourceTab? activeRunSourceTab;
    /// <summary>
    /// currentRunSelectedBenchmarkCases フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkCase> currentRunSelectedBenchmarkCases = [];
    /// logText フィールドです。
    /// </summary>
    private string logText = string.Empty;
    /// <summary>
    /// selectedLowerTab フィールドです。
    /// </summary>
    private LowerTab selectedLowerTab = LowerTab.Cpu;
    /// <summary>
    /// overallSourceTabProgress フィールドです。
    /// </summary>
    private BenchmarkTabProgress overallSourceTabProgress;
    /// <summary>
    /// logSubscription フィールドです。
    /// </summary>
    private IDisposable? logSubscription;
    /// <summary>
    /// reportedMetadataWarnings フィールドです。
    /// </summary>
    private readonly HashSet<string> reportedMetadataWarnings = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// retainedChartPoints フィールドです。
    /// </summary>
    private readonly Dictionary<string,ChartComparisonPoint> retainedChartPoints = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// runStatusGate フィールドです。
    /// </summary>
    private readonly object runStatusGate = new();
    /// <summary>
    /// activeChartMethodName フィールドです。
    /// </summary>
    private string activeChartMethodName = string.Empty;
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
        ICatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IRunService runService,
        IComparisonService comparisonService,
        UiRunStore uiRunStore,
        string benchmarkRoot,
        IUiDispatcher? uiDispatcher = null,
        IUiPreferences? uiPreferences = null)
        : this(new RunnerViewModelDependencies(uiRunStore,benchmarkRoot) {
            CatalogService=catalogService,
            CpuTopologyService=cpuTopologyService,
            RunService=runService,
            ComparisonService=comparisonService,
            UiDispatcher=uiDispatcher,
            UiPreferences=uiPreferences,
        }) {
    }

    internal RunnerViewModel(RunnerViewModelDependencies dependencies) {
        this.catalogService=dependencies.CreateCatalogService();
        this.cpuTopologyService=dependencies.CreateCpuTopologyService();
        this.comparisonService=dependencies.CreateComparisonService();
        this.runService=dependencies.CreateRunService(this.comparisonService);
        this.uiRunStore=dependencies.UiRunStore;
        this.uiRunRepository=new BenchmarkUiRunRepository(this.uiRunStore,StartupDiagnostics.Write);
        this.uiDispatcher=dependencies.CreateUiDispatcher();
        this.uiPreferences=dependencies.CreateUiPreferences();
        this.runEventPipeline=new RunEventPipeline();
        this.pipelineDiagnostics=new UiDiagnostics(StartupDiagnostics.Write);
        this.runVisualScheduler=dependencies.RunVisualScheduler??new SystemRunVisualScheduler();
        this.resultPipelineApplier=dependencies.CreateResultPipelineApplier(this);
        this.renderScheduler=new UiRenderScheduler(this.uiDispatcher,this.runEventPipeline,this.pipelineDiagnostics,this.resultPipelineApplier.ApplyRunUiSnapshot);
        this.persistenceBatcher=new PersistenceBatcher(this.uiDispatcher,this.pipelineDiagnostics,this.QueueBenchmarkResultsSave);
        this.runOrchestrator=dependencies.CreateRunOrchestrator(this);
        this.selectionController=dependencies.CreateSelectionController(this);
        this.chartController=dependencies.CreateChartController(this);
        this.sortController=dependencies.CreateSortController(this);
        this.cpuTopologyController=dependencies.CreateCpuTopologyController(this,this.cpuTopologyService);
        this.runStatePersistence=dependencies.CreateRunStatePersistence(this);
        this.pipelineDiagnostics.StartHeartbeat(this.uiDispatcher);
        this.benchmarkRoot=dependencies.BenchmarkRoot;
        this.SourceTabs=BenchmarkSourceTabs.All.Select(static item => new SourceTabOption(item)).ToArray();
        this.sourceTabViewStates=this.SourceTabs.Select(static item => new SourceTabViewState(item.Definition)).ToArray();
        foreach(var state in this.sourceTabViewStates) {
            this.sourceTabViewStatesByTab[state.Definition.Tab]=state;
        }
        this.selectedSourceTab=this.SourceTabs[0];
        this.ApplySourceTabVisualSelection();
        this.BenchmarkColumnSelectionGroups=this.CreateBenchmarkColumnSelectionGroups();
        this.BenchmarkProjectSelectionGroups=this.CreateBenchmarkProjectSelectionGroups();
        this.sortController.Initialize();
        this.ExecutionProfiles=CreateExecutionProfiles();
        this.selectedExecutionProfile=this.ExecutionProfiles.First(static item => item.Profile==BenchmarkExecutionProfile.Fast);
        this.ThresholdOptions=CreateThresholdOptions();
        this.selectedThreshold=this.ThresholdOptions[0];
        this.Metrics= [
            new MetricOption("Time",MetricKind.Time),
            new MetricOption("Allocated",MetricKind.Allocated),
        ];
        this.selectedMetric=this.Metrics[0];
        this.ChartModes= [
            new ChartModeOption("N comparison",ChartMode.ElementComparison),
            new ChartModeOption("Scale trend",ChartMode.ScaleTrend),
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
            () => this.selectionController.SetAllBenchmarkSelection(true),
            () => this.CanChangeBenchmarkSelection);
        this.ClearSelectionCommand=new RelayCommand(
            () => this.selectionController.SetAllBenchmarkSelection(false),
            () => this.CanChangeBenchmarkSelection);
        this.RunCommand=new AsyncRelayCommand(this.runOrchestrator.RunOrResumeAsync,() => this.CanUseRunToolbarButton);
#if DEBUG
        this.RunInfiniteCommand=new AsyncRelayCommand(this.runOrchestrator.RunInfiniteAsync,() => this.CanUseRunToolbarButton);
#endif
        this.ToggleRunCommand=new RelayCommand(this.runOrchestrator.ToggleRun,() => !this.IsRefreshing);
        this.StopCommand=new AsyncRelayCommand(this.runOrchestrator.StopAsync,() => this.IsBenchmarkRunActive||this.IsBenchmarkPaused);
        this.PauseCommand=new AsyncRelayCommand(this.runOrchestrator.PauseAsync,() => this.IsBenchmarkRunActive);
        this.CancelCommand=new RelayCommand(this.runOrchestrator.Cancel);
        this.CopyImprovementPromptCommand=new AsyncRelayCommand(this.CopyImprovementPromptAsync,() => this.CanCopyImprovementPrompt);
        this.ShowCpuCommand=new RelayCommand(() => this.SelectedTab=Tab.Cpu);
        this.ShowChartCommand=new RelayCommand(() => this.SelectedTab=Tab.Chart);
        StartupDiagnostics.InitializeFromExistingLog();
        this.logText=StartupDiagnostics.GetLogText();
        this.logSubscription=StartupDiagnostics.Subscribe(this.AppendLogText);
        if(!string.IsNullOrWhiteSpace(this.logText)) {
            this.LogEntries.Add(this.logText);
        }

        this.cpuTopologyController.Initialize();
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
    private IReadOnlyList<ColumnSelectionGroup> CreateBenchmarkColumnSelectionGroups() => [];

    /// <summary>
    /// CreateBenchmarkProjectSelectionGroups メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ColumnSelectionGroup> CreateBenchmarkProjectSelectionGroups() => [
        new ColumnSelectionGroup("Left",this.SelectedSourceDefinition.LeftHeader,["Left"]),
        new ColumnSelectionGroup("Right",this.SelectedSourceDefinition.RightHeader,["Right"]),
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
    public IReadOnlyList<SourceTabViewState> SourceTabViewStates => this.sourceTabViewStates;
    /// <summary>
    /// SelectedSourceTabViewState プロパティです。
    /// </summary>
    /// <value>SelectedSourceTabViewState の値を取得/設定します。</value>
    public SourceTabViewState SelectedSourceTabViewState => this.GetSourceTabViewState(this.SelectedSourceDefinition.Tab);
    /// <summary>
    /// BenchmarkRows プロパティです。
    /// </summary>
    /// <value>BenchmarkRows の値を取得/設定します。</value>
    public ObservableCollection<TableRow> BenchmarkRows => this.SelectedSourceTabViewState.Rows;
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
    public ObservableCollection<ImprovementCandidateRow> ImprovementCandidates { get; } = [];
    /// <summary>
    /// LatencyHotspotRows プロパティです。
    /// </summary>
    /// <value>LatencyHotspotRows の値を取得/設定します。</value>
    public ObservableCollection<DiagnosticRow> LatencyHotspotRows => this.SelectedSourceTabViewState.LatencyHotspotRows;
    /// <summary>
    /// AllocationDiagnosticRows プロパティです。
    /// </summary>
    /// <value>AllocationDiagnosticRows の値を取得/設定します。</value>
    public ObservableCollection<DiagnosticRow> AllocationDiagnosticRows => this.SelectedSourceTabViewState.AllocationDiagnosticRows;
    /// <summary>
    /// CpuTimelineRows プロパティです。
    /// </summary>
    /// <value>CpuTimelineRows の値を取得/設定します。</value>
    public ObservableCollection<DiagnosticRow> CpuTimelineRows => this.SelectedSourceTabViewState.CpuTimelineRows;
    /// <summary>
    /// PipelineDiagnosticRows プロパティです。
    /// </summary>
    /// <value>PipelineDiagnosticRows の値を取得/設定します。</value>
    public ObservableCollection<DiagnosticRow> PipelineDiagnosticRows { get; } = [];
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
    public ObservableCollection<SortRule> BenchmarkSortRules { get; } = [];
    /// <summary>
    /// BenchmarkSortColumnOptions プロパティです。
    /// </summary>
    /// <value>BenchmarkSortColumnOptions の値を取得/設定します。</value>
    public ObservableCollection<SortColumnOption> BenchmarkSortColumnOptions { get; } = [];
    /// <summary>
    /// ChartPoints プロパティです。
    /// </summary>
    /// <value>ChartPoints の値を取得/設定します。</value>
    public ObservableCollection<ChartComparisonPoint> ChartPoints => this.SelectedSourceTabViewState.ChartPoints;
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
    public IReadOnlyList<ColumnSelectionGroup> BenchmarkColumnSelectionGroups { get; }
    /// <summary>
    /// BenchmarkProjectSelectionGroups プロパティです。
    /// </summary>
    /// <value>BenchmarkProjectSelectionGroups の値を取得/設定します。</value>
    public IReadOnlyList<ColumnSelectionGroup> BenchmarkProjectSelectionGroups { get; }
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
        private set => this.SetProperty(ref this.overallSourceTabProgress,value);
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
#if DEBUG
    /// <summary>
    /// RunInfiniteCommand プロパティです。
    /// </summary>
    /// <value>RunInfiniteCommand の値を取得/設定します。</value>
    public IAsyncRelayCommand RunInfiniteCommand { get; }
#endif
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
    public GridSortColumn CurrentSortColumn {
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
    /// BenchmarkRatioSummaryText プロパティです。
    /// </summary>
    /// <value>BenchmarkRatioSummaryText の値を取得/設定します。</value>
    public string BenchmarkRatioSummaryText {
        get => this.SelectedSourceTabViewState.BenchmarkRatioSummaryText;
        /// <summary>
        /// set プロパティを取得/設定します。
        /// </summary>
        /// <value>set の値です。</value>
        private set {
            if(this.SelectedSourceTabViewState.BenchmarkRatioSummaryText!=value) {
                this.SelectedSourceTabViewState.BenchmarkRatioSummaryText=value;
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
        private set {
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
    public GeneratedCodeAuditReport? GeneratedCodeAuditReport {
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
#if DEBUG
                this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
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
                this.OnPropertyChanged(nameof(this.CanRunBenchmark));
                this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
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
#if DEBUG
                this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
                this.ToggleRunCommand.NotifyCanExecuteChanged();
                this.StopCommand.NotifyCanExecuteChanged();
                this.PauseCommand.NotifyCanExecuteChanged();
                this.RefreshToolbarCommandStates();
                this.SelectAllCommand.NotifyCanExecuteChanged();
                this.ClearSelectionCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
                this.OnPropertyChanged(nameof(this.CanRunBenchmark));
                this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            }
        }
    }

    /// <summary>
    /// CanChangeBenchmarkConditions プロパティです。
    /// </summary>
    /// <value>CanChangeBenchmarkConditions の値を取得/設定します。</value>
    public bool CanChangeBenchmarkConditions =>
        !this.IsBenchmarkRunning
        &&!this.IsRefreshing
        &&!this.IsBenchmarkPaused;

    /// <summary>
    /// CanChangeBenchmarkSelection プロパティです。
    /// </summary>
    /// <value>CanChangeBenchmarkSelection の値を取得/設定します。</value>
    public bool CanChangeBenchmarkSelection =>
        !this.IsRefreshing;

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
    public string RunButtonText => this.IsBenchmarkPaused ? "Resume" : "Run";

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
        !this.IsRefreshing&&!this.IsBenchmarkRunning&&this.HasSelectedBenchmarkCells;

    /// <summary>
    /// CanUseRunToolbarButton プロパティです。
    /// </summary>
    /// <value>CanUseRunToolbarButton の値を取得/設定します。</value>
    public bool CanUseRunToolbarButton => this.CanRunBenchmark||this.CanResumeBenchmark;

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
        this.IsBenchmarkPaused||this.HasSelectedBenchmarkCells
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
                this.OnPropertyChanged(nameof(this.CanResumeBenchmark));
                this.OnPropertyChanged(nameof(this.CanUseRunToolbarButton));
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
                this.RefreshToolbarCommandStates();
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
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
        this.CanUseRunToolbarButton,
        this.IsBenchmarkRunActive||this.IsBenchmarkPaused,
        !this.IsRefreshing&&this.IsBenchmarkRunActive);

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
    public Task CancelAndWaitAsync() =>
        this.runOrchestrator.CancelAndWaitAsync();

    private async Task CancelAndWaitAsyncCore() {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        this.Cancel();
        await this.WaitForRunStopAsync();
    }

    /// <summary>
    /// PauseAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public Task PauseAsync() =>
        this.runOrchestrator.PauseAsync();

    private async Task PauseAsyncCore() {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        this.pendingRunTerminationKind=RunTerminationKind.Pause;
        this.Cancel();
        await this.WaitForRunStopAsync();
    }

    /// <summary>
    /// StopAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    public Task StopAsync() =>
        this.runOrchestrator.StopAsync();

    private async Task StopAsyncCore() {
        if(!this.IsBenchmarkRunActive&&!this.IsBenchmarkPaused) {
            return;
        }

        this.pendingRunTerminationKind=RunTerminationKind.Stop;
        this.Cancel();
        await this.WaitForRunStopAsync();
        if(this.IsBenchmarkPaused) {
            this.SetActiveRunRoot(null);
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
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
        this.RunCommand.NotifyCanExecuteChanged();
#if DEBUG
        this.RunInfiniteCommand.NotifyCanExecuteChanged();
#endif
        this.ToggleRunCommand.NotifyCanExecuteChanged();
        this.StopCommand.NotifyCanExecuteChanged();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// SelectedTab プロパティです。
    /// </summary>
    /// <value>SelectedTab の値を取得/設定します。</value>
    public Tab SelectedTab {
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
    public bool IsCpuVisible => this.SelectedTab==Tab.Cpu;
    /// <summary>
    /// IsChartVisible プロパティです。
    /// </summary>
    /// <value>IsChartVisible の値を取得/設定します。</value>
    public bool IsChartVisible => this.SelectedTab==Tab.Chart;

    /// <summary>
    /// SelectedLowerTab プロパティです。
    /// </summary>
    /// <value>SelectedLowerTab の値を取得/設定します。</value>
    public LowerTab SelectedLowerTab {
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
    public bool IsCpuLowerTabVisible => this.SelectedLowerTab==LowerTab.Cpu;
    /// <summary>
    /// IsLogLowerTabVisible プロパティです。
    /// </summary>
    /// <value>IsLogLowerTabVisible の値を取得/設定します。</value>
    public bool IsLogLowerTabVisible => this.SelectedLowerTab==LowerTab.Log;
    /// <summary>
    /// IsImprovementPromptLowerTabVisible プロパティです。
    /// </summary>
    /// <value>IsImprovementPromptLowerTabVisible の値を取得/設定します。</value>
    public bool IsImprovementPromptLowerTabVisible => this.SelectedLowerTab==LowerTab.ImprovementPrompt;

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
    public AutoScrollRequest? LatestBenchmarkAutoScrollRequest { get; private set; }

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
        this.isApplyingSourceTabSelection=true;
        ++this.selectedRunLoadRequestToken;
        try {
            using(this.BeginChartRefreshBatch()) {
                if(this.SetProperty(ref this.selectedSourceTab,option)) {
                    changed=true;
                    this.ApplySourceTabVisualSelection();
                    this.OnPropertyChanged(nameof(this.SelectedSourceTabViewState));
                    this.OnPropertyChanged(nameof(this.BenchmarkRows));
                    this.OnPropertyChanged(nameof(this.ChartPoints));
                    this.OnPropertyChanged(nameof(this.ChartHeight));
                    this.OnPropertyChanged(nameof(this.BenchmarkRatioSummaryText));
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
        finally {
            stopwatch.Stop();
            if(changed) {
                StartupDiagnostics.Write(
                    $"Source tab switch {previousTab}->{nextTab} elapsed={FormatSwitchMeasureMilliseconds(stopwatch.Elapsed)}ms rows={this.BenchmarkRows.Count}");
            }
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
        set => this.SetProperty(ref this.selectedThreshold,value);
    }

    /// <summary>
    /// SelectedRunOption プロパティです。
    /// </summary>
    /// <value>SelectedRunOption の値を取得/設定します。</value>
    public BenchmarkRunOption? SelectedRunOption {
        get => this.selectedRunOption;
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
        get => this.SelectedMetric.Kind==MetricKind.Time;
        set {
            if(value) {
                this.SetMetric(MetricKind.Time);
            }
        }
    }

    /// <summary>
    /// IsAllocatedMetricSelected プロパティです。
    /// </summary>
    /// <value>IsAllocatedMetricSelected の値を取得/設定します。</value>
    public bool IsAllocatedMetricSelected {
        get => this.SelectedMetric.Kind==MetricKind.Allocated;
        set {
            if(value) {
                this.SetMetric(MetricKind.Allocated);
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
    public bool IsChartElementSliderVisible => this.ResultElementCountOptions.Count>1&&this.SelectedChartMode.Mode==ChartMode.ElementComparison;

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
    public SourceTabViewState GetSourceTabViewState(BenchmarkSourceTab sourceTab) =>
        this.sourceTabViewStatesByTab.TryGetValue(sourceTab,out var state)
            ? state
            : this.sourceTabViewStates[0];

    /// <summary>
    /// GetAllBenchmarkRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<TableRow> GetAllBenchmarkRows() =>
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
    public void SortBenchmarkRows(GridSortColumn column) =>
        this.sortController.SortBenchmarkRows(column);

    /// <summary>
    /// AddBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void AddBenchmarkSortRule() =>
        this.sortController.AddBenchmarkSortRule();

    /// <summary>
    /// RemoveBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void RemoveBenchmarkSortRule(SortRule rule) =>
        this.sortController.RemoveBenchmarkSortRule(rule);

    /// <summary>
    /// MoveBenchmarkSortRule メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="source">source を指定して実行条件を制御します。</param>
    /// <param name="target">target を指定して実行条件を制御します。</param>
    public void MoveBenchmarkSortRule(SortRule source,SortRule target) =>
        this.sortController.MoveBenchmarkSortRule(source,target);

    /// <summary>
    /// MoveBenchmarkSortRuleUp メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void MoveBenchmarkSortRuleUp(SortRule rule) =>
        this.sortController.MoveBenchmarkSortRuleUp(rule);

    /// <summary>
    /// MoveBenchmarkSortRuleDown メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void MoveBenchmarkSortRuleDown(SortRule rule) =>
        this.sortController.MoveBenchmarkSortRuleDown(rule);

    /// <summary>
    /// SetBenchmarkSortRuleColumn メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    /// <param name="column">column を指定して実行条件を制御します。</param>
    public void SetBenchmarkSortRuleColumn(SortRule rule,GridSortColumn column) =>
        this.sortController.SetBenchmarkSortRuleColumn(rule,column);

    /// <summary>
    /// ToggleBenchmarkSortRuleDirection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="rule">rule を指定して実行条件を制御します。</param>
    public void ToggleBenchmarkSortRuleDirection(SortRule rule) =>
        this.sortController.ToggleBenchmarkSortRuleDirection(rule);

    /// <summary>
    /// ResetBenchmarkSortRules メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void ResetBenchmarkSortRules() =>
        this.sortController.ResetBenchmarkSortRules();

    /// <summary>
    /// ToggleCpu メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="cpu">cpu を指定して実行条件を制御します。</param>
    public void ToggleCpu(CpuSelectionItem cpu) =>
        this.cpuTopologyController.ToggleCpu(cpu);

    /// <summary>
    /// ToggleCpuSection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="section">section を指定して実行条件を制御します。</param>
    public void ToggleCpuSection(CpuTableSection section) =>
        this.cpuTopologyController.ToggleCpuSection(section);

    /// <summary>
    /// ToggleCpuColumn メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="section">section を指定して実行条件を制御します。</param>
    /// <param name="columnIndex">columnIndex を指定して実行条件を制御します。</param>
    public void ToggleCpuColumn(CpuTableSection section,int columnIndex) =>
        this.cpuTopologyController.ToggleCpuColumn(section,columnIndex);

    /// <summary>
    /// ToggleCpuRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    public void ToggleCpuRow(CpuTableRow row) =>
        this.cpuTopologyController.ToggleCpuRow(row);

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    public void ToggleAllBenchmarkSelection(BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.selectionController.ToggleAllBenchmarkSelection(sourceTab);

    private void ToggleAllBenchmarkSelectionCore(BenchmarkSourceTabDefinition? sourceTab = null) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var tab = sourceTab??this.SelectedSourceDefinition;
        var cells = this.GetBenchmarkRows(tab).SelectMany(static row => row.ProjectCells).ToArray();
        var selected = GetSelectionState(cells) is not true;
        this.SetBenchmarkCells(cells,selected,tab);
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    public void ToggleBenchmarkColumnGroup(ColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.selectionController.ToggleBenchmarkColumnGroup(group,sourceTab);

    private void ToggleBenchmarkColumnGroupCore(ColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab = null) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var tab = sourceTab??this.SelectedSourceDefinition;
        var cells = this.GetBenchmarkCells(group.ColumnKeys,tab).ToArray();
        var selected = GetSelectionState(cells) is not true;
        this.SetBenchmarkCells(cells,selected,tab);
    }

    /// <summary>
    /// ToggleBenchmarkRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    public void ToggleBenchmarkRow(TableRow row) =>
        this.selectionController.ToggleBenchmarkRow(row);

    private void ToggleBenchmarkRowCore(TableRow row) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var state = this.GetSourceTabViewState(row.SourceTab.Tab);
        var selectedRows = state.Rows
            .Where(static item => item.IsUiSelected)
            .ToArray();
        var targetRows = row.IsUiSelected&&selectedRows.Length>0
            ? selectedRows
            : [row];
        var targetCells = targetRows
            .SelectMany(static item => item.ProjectCells)
            .Where(static cell => cell.HasCase)
            .ToArray();
        var selected = GetSelectionState(targetCells) is not true;
        this.SetBenchmarkCells(targetCells,selected,row.SourceTab);
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    internal void SelectAllBenchmarkUiCells(BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.selectionController.SelectAllBenchmarkUiCells(sourceTab);

    private void SelectAllBenchmarkUiCellsCore(BenchmarkSourceTabDefinition? sourceTab = null) {
        var state = this.GetSourceTabViewState((sourceTab??this.SelectedSourceDefinition).Tab);
        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.CornerHeader(),BenchmarkCellSelectionGesture.Replace);
    }

    /// <summary>
    /// SelectBenchmarkCell メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>
    internal void SelectBenchmarkCell(TableRow row,string project,BenchmarkCellSelectionGesture gesture) =>
        this.selectionController.SelectBenchmarkCell(row,project,gesture);

    private void SelectBenchmarkCellCore(TableRow row,string project,BenchmarkCellSelectionGesture gesture) {
        var state = this.GetSourceTabViewState(row.SourceTab.Tab);
        var rowIndex = state.Rows.IndexOf(row);
        var columnIndex = GetProjectColumnIndex(state.Definition,project);
        if(rowIndex<0||columnIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.Cell(rowIndex,columnIndex),gesture);
    }

    /// <summary>
    /// SelectBenchmarkRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>
    internal void SelectBenchmarkRow(TableRow row,BenchmarkCellSelectionGesture gesture) =>
        this.selectionController.SelectBenchmarkRow(row,gesture);

    private void SelectBenchmarkRowCore(TableRow row,BenchmarkCellSelectionGesture gesture) {
        var state = this.GetSourceTabViewState(row.SourceTab.Tab);
        var rowIndex = state.Rows.IndexOf(row);
        if(rowIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.RowHeader(rowIndex),gesture);
    }

    /// <summary>
    /// sourceTab プロパティです。
    /// </summary>
    /// <value>sourceTab の値を取得/設定します。</value>
    internal void SelectBenchmarkColumnGroup(ColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.selectionController.SelectBenchmarkColumnGroup(group,gesture,sourceTab);

    private void SelectBenchmarkColumnGroupCore(ColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab = null) {
        var state = this.GetSourceTabViewState((sourceTab??this.SelectedSourceDefinition).Tab);
        var columns = group.ColumnKeys
            .Select(project => GetProjectColumnIndex(state.Definition,project))
            .Where(static index => index>=0)
            .Order()
            .ToArray();
        if(columns.Length==0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.ColumnHeader(columns[0],columns[^1]),gesture);
    }

    /// <summary>
    /// ToggleBenchmarkCellCheck メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    internal void ToggleBenchmarkCellCheck(TableRow row,string project) =>
        this.selectionController.ToggleBenchmarkCellCheck(row,project);

    private void ToggleBenchmarkCellCheckCore(TableRow row,string project) {
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
        var selected = targets.Length>1
            ? true
            : !cell.IsSelected;
        this.SetBenchmarkCells(targets,selected,row.SourceTab);
    }

    /// <summary>
    /// SelectBenchmarkCellFromChart メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="benchmark">benchmark を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>
    internal void SelectBenchmarkCellFromChart(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture) =>
        this.selectionController.SelectBenchmarkCellFromChart(state,benchmark,project,gesture);

    private void SelectBenchmarkCellFromChartCore(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture) {
        var targetState = state??this.SelectedSourceTabViewState;
        var rowIndex = GetBenchmarkRowIndex(targetState,benchmark);
        var columnIndex = GetProjectColumnIndex(targetState.Definition,project);
        if(rowIndex<0||columnIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(targetState,BenchmarkCellSelectionTarget.Cell(rowIndex,columnIndex),gesture);
    }

    /// <summary>
    /// SelectBenchmarkRowFromChart メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="benchmark">benchmark を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>
    internal void SelectBenchmarkRowFromChart(SourceTabViewState? state,string benchmark,BenchmarkCellSelectionGesture gesture) =>
        this.selectionController.SelectBenchmarkRowFromChart(state,benchmark,gesture);

    private void SelectBenchmarkRowFromChartCore(SourceTabViewState? state,string benchmark,BenchmarkCellSelectionGesture gesture) {
        var targetState = state??this.SelectedSourceTabViewState;
        var rowIndex = GetBenchmarkRowIndex(targetState,benchmark);
        if(rowIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(targetState,BenchmarkCellSelectionTarget.RowHeader(rowIndex),gesture);
    }

    /// <summary>
    /// RefreshAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task RefreshAsync() {
        this.IsRefreshing=true;
        this.StatusText="読み込み中";
        StartupDiagnostics.Write("Benchmark catalog refresh started.");
        var dbUnavailable = false;
        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,true);
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
        this.benchmarkSelectionAnchors.Clear();
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
                row.IsSelected=this.benchmarkRowSelections.GetValueOrDefault(
                    CreateRowSelectionKey(row.SourceTab,row.RowKey),
                    false);
                foreach(var cell in row.ProjectCells) {
                    var key = CreateBenchmarkCellKey(row.SourceTab,row.RowKey,cell.Project);
                    cell.IsSelected=this.benchmarkCellSelections.TryGetValue(key,out var saved)&&saved;
                }
            }
            item.State.Rows.ReplaceRange(rows);
        }
        this.RebuildBenchmarkRowIndex();
        this.RefreshBenchmarkSortColumns();
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
    private IReadOnlyList<TableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases) =>
        this.CreateMethodRows(cases,this.SelectedSourceDefinition);

    /// <summary>
    /// CreateMethodRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cases">cases を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<TableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases,BenchmarkSourceTabDefinition sourceTab) {
        return this.CreateMethodRows(cases,sourceTab,this.SelectedResultElementCount?.Value??0);
    }

    /// <summary>
    /// CreateMethodRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cases">cases を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="elementCount">elementCount を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<TableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases,BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        return BenchmarkRowGroupingLogic.CreateRows(cases,sourceTab,elementCount)
            .Select(row => new TableRow(
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
        // 行インスタンスが再生成されるため、RunItemIndexキャッシュも合わせて破棄する(古いインスタンスへの参照を残さない)。
        this.benchmarkRowsByRunItemIndex.Clear();
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
    private void AddBenchmarkRowIndex(TableRow row,string project,string typeFullName,string method,string argumentKey) {
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

        var tab = sourceTab??this.SelectedSourceDefinition;
        this.SetBenchmarkCells(this.GetBenchmarkRows(tab).SelectMany(static row => row.ProjectCells),selected,tab);
    }

    /// <summary>
    /// SubscribeBenchmarkRow メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    private void SubscribeBenchmarkRow(TableRow row) {
        row.PropertyChanged+=(_,args) => {
            if(args.PropertyName==nameof(TableRow.IsSelected)&&!this.isApplyingUiState&&!this.isApplyingBenchmarkSelection) {
                this.SaveUiState();
            }
        };
        foreach(var cell in row.ProjectCells) {
            cell.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(ProjectCell.IsSelected)&&!this.isApplyingUiState&&!this.isApplyingBenchmarkSelection) {
                    this.RefreshBenchmarkSelectionAggregates();
                    this.SaveUiState();
                    this.RefreshBenchmarkSelectionDisplay();
                }
            };
        }
    }

    /// <summary>
    /// GetBenchmarkRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<TableRow> GetBenchmarkRows(BenchmarkSourceTabDefinition sourceTab) =>
        this.GetSourceTabViewState(sourceTab.Tab).Rows;

    /// <summary>
    /// GetBenchmarkCells メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="projectKeys">projectKeys を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<ProjectCell> GetBenchmarkCells(IReadOnlyList<string> projectKeys,BenchmarkSourceTabDefinition sourceTab) {
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
    private void SetBenchmarkCells(IEnumerable<ProjectCell> cells,bool selected,BenchmarkSourceTabDefinition sourceTab) {
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
    private ProjectCell[] ExpandBenchmarkSelectionCells(IEnumerable<ProjectCell> cells,BenchmarkSourceTabDefinition sourceTab) {
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

        var targets = new List<ProjectCell>();
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
    private bool TryGetBenchmarkCellBySelectionKey(TableRow row,string key,out ProjectCell cell) {
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

    /// <summary>
    /// ApplyBenchmarkCellSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="target">target を指定して実行条件を制御します。</param>
    /// <param name="gesture">gesture を指定して実行条件を制御します。</param>
    private void ApplyBenchmarkCellSelection(SourceTabViewState state,BenchmarkCellSelectionTarget target,BenchmarkCellSelectionGesture gesture) {
        var projectOrder = state.Definition.ProjectOrder;
        var result = BenchmarkCellSelectionLogic.Apply(
            state.Rows.Count,
            projectOrder.Count,
            GetSelectedBenchmarkUiCoordinates(state),
            this.benchmarkSelectionAnchors.GetValueOrDefault(state.Definition.Tab),
            target,
            gesture);
        var selected = result.SelectedCells;
        for(var rowIndex = 0; rowIndex<state.Rows.Count; rowIndex++) {
            var row = state.Rows[rowIndex];
            for(var columnIndex = 0; columnIndex<projectOrder.Count; columnIndex++) {
                if(row.GetProjectCell(projectOrder[columnIndex]) is { } cell) {
                    cell.IsUiSelected=selected.Contains(new BenchmarkCellCoordinate(rowIndex,columnIndex));
                }
            }
        }
        this.benchmarkSelectionAnchors[state.Definition.Tab]=result.Anchor;
        this.RefreshChartUiSelection(state);
    }

    /// <summary>
    /// GetSelectedBenchmarkUiCoordinates メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static IEnumerable<BenchmarkCellCoordinate> GetSelectedBenchmarkUiCoordinates(SourceTabViewState state) {
        for(var rowIndex = 0; rowIndex<state.Rows.Count; rowIndex++) {
            var row = state.Rows[rowIndex];
            for(var columnIndex = 0; columnIndex<state.Definition.ProjectOrder.Count; columnIndex++) {
                if(row.GetProjectCell(state.Definition.ProjectOrder[columnIndex]) is { IsUiSelected: true }) {
                    yield return new BenchmarkCellCoordinate(rowIndex,columnIndex);
                }
            }
        }
    }

    // BenchmarkColumnSelectionGroups/BenchmarkProjectSelectionGroups と IsAllBenchmarkSelectionChecked は
    // 全Sourceタブの見た目ヘッダーで共有されるインスタンスのため、常に「現在表示中のSourceタブ」の
    // チェック状態だけを反映する。他タブの選択状態をここに混ぜると、タブ間でチェック表示が同期して見える。
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
        var currentTabCells = this.GetBenchmarkRows(currentTab).SelectMany(static row => row.ProjectCells).ToArray();
        this.IsAllBenchmarkSelectionChecked=GetSelectionState(currentTabCells);
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
            return state.Rows.Any(static row => row.ProjectCells.Any(static cell => cell.HasCase&&cell.IsSelected));
        }

        return BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,sourceTab)
            .Any(row => sourceTab.ProjectOrder.Any(project =>
                row.CasesByProject.ContainsKey(project)
                &&this.benchmarkCellSelections.TryGetValue(CreateBenchmarkCellKey(sourceTab,row.RowKey,project),out var selected)
                &&selected));
    }

    /// <summary>
    /// GetSelectionState メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="cells">cells を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool? GetSelectionState(IEnumerable<ProjectCell> cells) {
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
    private static int GetBenchmarkRowIndex(SourceTabViewState state,string benchmark,string rowKey = "") {
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
    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadImprovementCandidatesChartData() =>
        this.chartController.LoadImprovementCandidatesChartData();

    private IReadOnlyList<BenchmarkRankedRatioPoint> LoadImprovementCandidatesChartDataCore() {
        try {
            return BenchmarkAnalysisViewMapper.MapImprovementCandidates(this.uiRunStore.QueryAnalysisView("BenchmarkImprovementCandidates",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkImprovementCandidates load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadTimeRatioWorstChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadTimeRatioWorstChartData() =>
        this.chartController.LoadTimeRatioWorstChartData();

    private IReadOnlyList<BenchmarkRankedRatioPoint> LoadTimeRatioWorstChartDataCore() {
        try {
            return BenchmarkAnalysisViewMapper.MapTimeRatioWorst(this.uiRunStore.QueryAnalysisView("BenchmarkTimeRatioWorst",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkTimeRatioWorst load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadAllocationRatioWorstChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadAllocationRatioWorstChartData() =>
        this.chartController.LoadAllocationRatioWorstChartData();

    private IReadOnlyList<BenchmarkRankedRatioPoint> LoadAllocationRatioWorstChartDataCore() {
        try {
            return BenchmarkAnalysisViewMapper.MapAllocationRatioWorst(this.uiRunStore.QueryAnalysisView("BenchmarkAllocationRatioWorst",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkAllocationRatioWorst load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadCategorySummaryChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkCategoryRatioPoint> LoadCategorySummaryChartData() =>
        this.chartController.LoadCategorySummaryChartData();

    private IReadOnlyList<BenchmarkCategoryRatioPoint> LoadCategorySummaryChartDataCore() {
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
        this.chartController.LoadHistoryTrendChartData();

    private IReadOnlyList<BenchmarkTrendSeries> LoadHistoryTrendChartDataCore() {
        try {
            return BenchmarkAnalysisViewMapper.MapHistoryTrend(this.uiRunStore.QueryAnalysisView("BenchmarkHistoryTrend",100));
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkHistoryTrend load failed.",ex);
            return [];
        }
    }

    /// <summary>
    /// LoadScaleTrendChartData メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    public IReadOnlyList<BenchmarkTrendSeries> LoadScaleTrendChartData() =>
        this.chartController.LoadScaleTrendChartData();

    private IReadOnlyList<BenchmarkTrendSeries> LoadScaleTrendChartDataCore() {
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
    public IReadOnlyList<BenchmarkBadgeItem> LoadMissingFailureMatrixBadges() =>
        this.chartController.LoadMissingFailureMatrixBadges();

    private IReadOnlyList<BenchmarkBadgeItem> LoadMissingFailureMatrixBadgesCore() {
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
    public IReadOnlyList<BenchmarkQualityGaugePoint> LoadMeasurementQualityGauges() =>
        this.chartController.LoadMeasurementQualityGauges();

    private IReadOnlyList<BenchmarkQualityGaugePoint> LoadMeasurementQualityGaugesCore() {
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
    public IReadOnlyList<BenchmarkBadgeItem> LoadFallbackDiagnosticsBadges() =>
        this.chartController.LoadFallbackDiagnosticsBadges();

    private IReadOnlyList<BenchmarkBadgeItem> LoadFallbackDiagnosticsBadgesCore() {
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
    private async Task RefreshDbRunListAsync(CancellationToken cancellationToken,bool loadSelected) {
        var options = await Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            this.uiRunStore.EnsureSchema();
            return this.uiRunStore.ListRuns(GetUiRunAppKind())
                .Where(run => string.Equals(run.ExecutionProfile,this.SelectedExecutionProfile.Profile.ToString(),StringComparison.OrdinalIgnoreCase))
                .Select(this.CreateRunOption)
                .ToArray();
        },cancellationToken);
        var selectedUiRunId = this.SelectedRunOption?.UiRunId;
        this.RunOptions.Clear();
        this.RunOptions.Add(CreateNewRunOption(this.SelectedExecutionProfile.Profile));
        foreach(var option in options) {
            this.RunOptions.Add(option);
        }
        this.SetSelectedRunOption(
            this.RunOptions.FirstOrDefault(option => option.UiRunId==selectedUiRunId)??this.RunOptions.FirstOrDefault(),
            loadSelected,
            false);
    }

    /// <summary>
    /// CreateRunOption メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="run">run を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkRunOption CreateRunOption(UiRunRecord run) {
        var jobs = this.uiRunStore.ListJobs(run.RunId);
        var sourceTab = Enum.TryParse<BenchmarkSourceTab>(run.SourceTab,true,out var parsedSourceTab)
            ? parsedSourceTab
            : this.SelectedSourceDefinition.Tab;
        var elementCounts = jobs
            .Select(job => (int)(BenchmarkWorkloadAxis.UsesLoopCount(sourceTab) ? job.LoopCount??0 : job.ElementCount??0))
            .Where(static elementCount => elementCount>0)
            .Distinct()
            .Order()
            .ToArray();
        var completedJobs = jobs.Count(static job => job.Status==UiRunStatus.Completed);
        var executionProfile = Enum.TryParse<BenchmarkExecutionProfile>(run.ExecutionProfile,true,out var parsedProfile)
            ? parsedProfile
            : this.SelectedExecutionProfile.Profile;
        return new BenchmarkRunOption(
            unchecked((int)Math.Min(run.RunId,int.MaxValue)),
            run.RunId,
            run.DisplayName+" "+completedJobs.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+jobs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Empty,
            executionProfile,
            run.CreatedAt,
            run.UpdatedAt,
            completedJobs,
            jobs.Count,
            elementCounts);
    }

    /// <summary>
    /// RefreshRunsForSelectedProfileAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private Task RefreshRunsForSelectedProfileAsync() =>
        this.runStatePersistence.RefreshRunsForSelectedProfileAsync();

    private async Task RefreshRunsForSelectedProfileAsyncCore() {
        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,true);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run list refresh failed.",ex);
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
            this.SetSelectedRunOption(
                runNumber is { } number
                ? this.RunOptions.FirstOrDefault(option => option.RunNumber==number)??this.RunOptions.FirstOrDefault()
                : this.RunOptions.FirstOrDefault(),
                false,
                false);
            this.RebuildResultElementCounts(this.SelectedRunOption?.ElementCounts?? []);
        }
        finally {
            this.isApplyingUiState=false;
            this.RefreshBenchmarkSelectionAggregates();
        }
        this.LoadSelectedRunFromDatabase();
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
    private bool CanSelectRunOption(BenchmarkRunOption? option) =>
        !this.HasDisplayResults||IsNewRunOption(option);

    /// <summary>
    /// SetSelectedRunOption メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <param name="load">load を指定して実行条件を制御します。</param>
    /// <param name="saveUiState">saveUiState を指定して実行条件を制御します。</param>
    private void SetSelectedRunOption(BenchmarkRunOption? option,bool load,bool saveUiState) {
        this.isApplyingRunOptionSelection=true;
        try {
            if(this.SetProperty(ref this.selectedRunOption,option)) {
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
    private void SelectNewRunOptionForFreshRun() {
        var newRunOption = this.RunOptions.FirstOrDefault(IsNewRunOption);
        if(newRunOption is not null) {
            this.SetSelectedRunOption(newRunOption,false,true);
        }
    }

    /// <summary>
    /// ApplySelectedRunOption メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="option">option を指定して実行条件を制御します。</param>
    /// <param name="load">load を指定して実行条件を制御します。</param>
    private void ApplySelectedRunOption(BenchmarkRunOption? option,bool load) {
        this.ClearRetainedChartPoints();
        if(IsNewRunOption(option)) {
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
            foreach(var cell in row.ProjectCells) {
                this.benchmarkCellSelections[CreateBenchmarkCellKey(row.SourceTab,row.RowKey,cell.Project)]=cell.IsSelected;
            }
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

    // 選択スナップショットとキュー生成の規則は UI.Core の BenchmarkSelectionQueueLogic に集約する。
    // 表示中タブだけの生きた状態(チェックボックス)を優先する。
    /// <summary>
    /// CreateSelectedBenchmarkCasesFromAllTabs メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkCase> CreateSelectedBenchmarkCasesFromAllTabs() {
        this.CaptureCurrentBenchmarkSelections();
        return BenchmarkSelectionQueueLogic.CreateSelectedCasesFromAllTabs(
            this.catalogCases,
            this.SourceTabs.Select(static option => option.Definition),
            this.benchmarkCellSelections,
            sourceTab => this.GetSourceTabViewState(sourceTab.Tab).Rows.Count>0
                ? this.GetSourceTabViewState(sourceTab.Tab).Rows.SelectMany(static row => row.GetRunCases())
                : null);
    }

    /// <summary>
    /// CreateRowSelectionKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="rowKey">rowKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateRowSelectionKey(BenchmarkSourceTabDefinition sourceTab,string rowKey) =>
        sourceTab.Id+"|"+rowKey;

    /// <summary>
    /// CreateBenchmarkCellSelectionKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    internal static string CreateBenchmarkCellSelectionKey(BenchmarkSourceTabDefinition sourceTab,string method,string project) =>
        BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,method,project);

    /// <summary>
    /// CreateBenchmarkCellKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateBenchmarkCellKey(BenchmarkSourceTabDefinition sourceTab,string method,string project) =>
        BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,method,project);

    /// <summary>
    /// SaveUiState メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void SaveUiState() =>
        this.runStatePersistence.SaveUiState();

    private void SaveUiStateCore() {
        this.CaptureCurrentBenchmarkSelections();
        if(this.IsBenchmarkRunActive) {
            this.RefreshCurrentRunBenchmarkSelectionSnapshot();
        }

        if(this.activeUiRunId is { } runId) {
            var snapshot = this.CreateBenchmarkViewStateSnapshot();
            var cellSelections = this.benchmarkCellSelections.ToArray();
            var rowSelections = this.benchmarkRowSelections.ToArray();
            this.uiRunRepository.QueueSaveUiState(runId,snapshot,cellSelections,rowSelections);
        }
        this.HasPendingCloseStateSave=false;
        if(!this.isApplyingUiState&&!this.pendingPostRunDisplayFinalize) {
            this.RefreshBenchmarkSelectionDisplay();
        }
    }

    /// <summary>
    /// DiscardPendingCloseStateSave メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    public void DiscardPendingCloseStateSave() =>
        this.runStatePersistence.DiscardPendingCloseStateSave();

    private void DiscardPendingCloseStateSaveCore() {
        this.HasPendingCloseStateSave=false;
    }

    /// <summary>
    /// RefreshBenchmarkSelectionDisplay メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkSelectionDisplay() {
        if(this.isRefreshingBenchmarkSelectionDisplay||this.BenchmarkRows.Count==0) {
            return;
        }

        this.isRefreshingBenchmarkSelectionDisplay=true;
        try {
            this.RefreshVisibleResults();
            this.RefreshComparisonRowsFromDisplayResults();
            this.ApplyComparisonRowsToTable();
            this.ClearUnselectedResultDisplays();
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
        if(this.HasCurrentRunResultsForSelectedTab) {
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
        SourceTabViewState State,
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
        if(input.CurrentRunResults.Count>0) {
            return selectedElementCount is { } currentElementCount
                ? this.runProjectionService.BuildComparisonRowsForElementCount(input.CurrentRunResults,input.State.Definition,currentElementCount,this.CreateReportRowsForComparison)
                : this.runProjectionService.BuildLatestComparisonRows(input.CurrentRunResults,input.State.Definition,this.CreateReportRowsForComparison);
        }

        if(resultItemsSnapshot.Count>0) {
            return selectedElementCount is { } displayedElementCount
                ? this.runProjectionService.BuildDisplayedRowsForElementCount(resultItemsSnapshot,input.State.Definition,displayedElementCount,this.CreateReportRowsForComparison)
                : this.runProjectionService.BuildDisplayedRows(resultItemsSnapshot,input.State.Definition,this.CreateReportRowsForComparison);
        }

        return [];
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
            this.benchmarkCellSelections.Clear();
            foreach(var item in state.BenchmarkSelection.ProjectCellSelections) {
                this.benchmarkCellSelections[item.Key]=item.Value;
            }
            this.benchmarkRowSelections.Clear();
            foreach(var item in state.BenchmarkSelection.RowSelections) {
                this.benchmarkRowSelections[item.Key]=item.Value;
            }
            foreach(var row in this.GetAllBenchmarkRows()) {
                row.IsSelected=this.benchmarkRowSelections.GetValueOrDefault(
                    CreateRowSelectionKey(row.SourceTab,row.RowKey),
                    false);
                foreach(var cell in row.ProjectCells) {
                    var key = CreateBenchmarkCellKey(row.SourceTab,row.RowKey,cell.Project);
                    cell.IsSelected=this.benchmarkCellSelections.TryGetValue(key,out var selected)&&selected;
                }
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
    public void DiscardUnsavedBenchmarkResults() =>
        this.runStatePersistence.DiscardUnsavedBenchmarkResults();

    private void DiscardUnsavedBenchmarkResultsCore() {
        // 結果は QueueSaveResult で SQLite へ保存済みのため、未保存表示を解除する。
        // 実行していない状態で終了確認が出ないようにする。
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
            this.benchmarkCellSelections,
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
    private TableRow? FindBenchmarkRow(BenchmarkResultItem result) {
        if(result.RunItemIndex>=0&&this.benchmarkRowsByRunItemIndex.TryGetValue(result.RunItemIndex,out var indexedRow)) {
            return indexedRow;
        }

        var row = this.ResolveBenchmarkRowByKey(result.SourceTab,result.Project,result.TypeFullName,result.Method,result.ArgumentKey)
            ??this.BenchmarkRows.FirstOrDefault(candidate => IsResultForRow(candidate,result))
            ??(IsScalableSourceTab(result.SourceTab)||result.SourceTab!=this.SelectedSourceDefinition.Tab ? null : this.BenchmarkRows.FirstOrDefault(candidate => string.Equals(candidate.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
                &&candidate.GetProjectCell(result.Project) is { HasCase: true }));
        this.RegisterRunItemIndexMismatch(result.RunItemIndex,result.SourceTab,result.Project,result.Method,row);
        return row;
    }

    /// <summary>
    /// FindBenchmarkRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="job">job を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private TableRow? FindBenchmarkRow(BenchmarkJob job) {
        if(job.RunItemIndex>=0&&this.benchmarkRowsByRunItemIndex.TryGetValue(job.RunItemIndex,out var indexedRow)) {
            return indexedRow;
        }

        var row = this.ResolveBenchmarkRowByKey(job.SourceTab,job.Project,job.TypeFullName,job.MethodName,job.ArgumentKey)
            ??this.BenchmarkRows.FirstOrDefault(candidate => IsJobForRow(candidate,job))
            ??(IsScalableSourceTab(job.SourceTab)||job.SourceTab!=this.SelectedSourceDefinition.Tab ? null : this.BenchmarkRows.FirstOrDefault(candidate => string.Equals(candidate.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
                &&candidate.GetProjectCell(job.Project) is { HasCase: true }));
        this.RegisterRunItemIndexMismatch(job.RunItemIndex,job.SourceTab,job.Project,job.MethodName,row);
        return row;
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
    private TableRow? ResolveBenchmarkRowByKey(BenchmarkSourceTab sourceTab,string project,string typeFullName,string method,string argumentKey) =>
        this.TryFindBenchmarkRow(sourceTab,project,typeFullName,method,argumentKey,out var row) ? row : null;

    /// <summary>
    /// RegisterRunItemIndexMismatch メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="runItemIndex">runItemIndex を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <param name="resolvedRow">resolvedRow を指定して実行条件を制御します。</param>
    private void RegisterRunItemIndexMismatch(int runItemIndex,BenchmarkSourceTab sourceTab,string project,string method,TableRow? resolvedRow) {
        if(runItemIndex<0) {
            // 実行中ジョブに紐づかない結果(履歴表示・比較用など)は元々index管理の対象外のため無視する。
            return;
        }

        var hadPreviousMapping = this.benchmarkRowsByRunItemIndex.TryGetValue(runItemIndex,out var previousRow);
        if(resolvedRow is not null) {
            this.benchmarkRowsByRunItemIndex[runItemIndex]=resolvedRow;
        }

        if(resolvedRow is null) {
            var message = $"[RunItemIndex不一致] index={runItemIndex} tab={sourceTab} project={project} method={method} 解決=失敗 原因(推定)={this.GuessRunItemIndexUnresolvedCause()}";
            this.AppendHighlightedLogEntry(message);
            return;
        }

        if(hadPreviousMapping&&!ReferenceEquals(previousRow,resolvedRow)) {
            var message = $"[RunItemIndex衝突] index={runItemIndex} tab={sourceTab} project={project} method={method} 原因(推定)=タブ間でのRunItemIndex再利用、またはLeft/Right行内の割当衝突の可能性";
            this.AppendHighlightedLogEntry(message);
        }
        // それ以外(初回ブートストラップで文字列キーにより正常解決)はログを出さない。
    }

    /// <summary>
    /// GuessRunItemIndexUnresolvedCause メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private string GuessRunItemIndexUnresolvedCause() {
        if(this.IsBenchmarkPaused&&this.activeRunRoot is not null) {
            return "pause/resume間で行マップが再構築されずに古いindexを参照した可能性";
        }

        if(!this.IsBenchmarkRunActive&&!this.IsBenchmarkPaused) {
            return "アプリ再起動後の再開でインメモリindexマップが失われた可能性(永続化未対応)";
        }

        return "アダプティブ実行中の動的ジョブ生成が行マップの再構築より先行した可能性";
    }

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
    private bool TryFindBenchmarkRow(BenchmarkSourceTab sourceTab,string project,string typeFullName,string method,string argumentKey,out TableRow row) {
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
    private static bool IsResultForRow(TableRow row,BenchmarkResultItem result) {
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
    private static bool IsJobForRow(TableRow row,BenchmarkJob job) {
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
    private static bool IsMatchingTypeForRow(TableRow row,string actualTypeFullName,string expectedTypeFullName) {
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
        this.ClearRetainedChartPoints();
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
        this.SelectedTab=Tab.Chart;
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
        if(this.IsBenchmarkRunning) {
            return;
        }

        if(this.IsBenchmarkPaused&&this.activeRunRoot is not null) {
            await this.RunAsync(this.activeRunRoot);
            return;
        }
        if(this.TryPrepareSelectedDatabaseRunResume(out var runRoot)) {
            await this.RunAsync(runRoot);
            return;
        }

        await this.RunAsync(null);
    }

#if DEBUG
    /// <summary>
    /// RunInfiniteAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task RunInfiniteAsync() {
        if(this.IsBenchmarkRunning) {
            return;
        }

        if(this.IsBenchmarkPaused&&this.activeRunRoot is not null) {
            await this.RunAsync(this.activeRunRoot,true);
            return;
        }

        await this.RunAsync(null,true);
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
        if(run is null||run.Status is not (UiRunStatus.Paused or UiRunStatus.Running)) {
            return false;
        }

        var snapshot = this.uiRunStore.ListUiViewStates(option.UiRunId)
            .FirstOrDefault(static item => string.Equals(item.StateKey,"BenchmarkProgressStateJson",StringComparison.OrdinalIgnoreCase))
            ?.StateValue;
        if(string.IsNullOrWhiteSpace(snapshot)) {
            return false;
        }

        var state = JsonSerializer.Deserialize<ProgressState>(snapshot);
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
    private async Task RunAsync(string? runRootOverride,bool useInfiniteTestRunner = false) {
        this.RefreshCurrentRunBenchmarkSelectionSnapshot();
        var selected = this.GetCurrentRunSelectedBenchmarkCases().ToArray();
        if(selected.Length==0) {
            this.StatusText="ベンチマークが選択されていません";
            StartupDiagnostics.Write("Benchmark run aborted: no benchmark selected.");
            return;
        }

        var executionProfile = useInfiniteTestRunner ? BenchmarkExecutionProfile.Test : this.SelectedExecutionProfile.Profile;
        var isResumeRun = runRootOverride is not null;
        if(!isResumeRun) {
            this.ClearAllCurrentRunResultsForFreshRun();
        }
        this.DiscardUnsavedBenchmarkResults();
        this.RemoveTransientRunOptions();
        if(!isResumeRun) {
            this.SelectNewRunOptionForFreshRun();
            this.loadedExternalState=null;
        }
        this.SelectedTab=Tab.Cpu;
        this.SelectedLowerTab=LowerTab.Cpu;
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
            uiRunId=runRootOverride is not null&&this.activeUiRunId is not null
                ? this.activeUiRunId
                : await this.CreateUiRunAsync(runSourceTab,executionProfile).WaitAsync(runCancellationSource.Token);
            this.activeUiRunId=uiRunId;
            // Running表示への更新は実行開始をブロックする理由がないため、Completed/Paused等と同じくfire-and-forgetにする。
            this.QueueUiRunStatus(uiRunId,UiRunStatus.Running,"Benchmark run started.");
            this.ClearSourceTabRunningStates();
            this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
            this.activeChartMethodName=string.Empty;
            if(!isResumeRun) {
                this.PrepareSelectedSourceLiveResultsForRun();
            }
            this.UpdateChart();
            this.CurrentElementCountText="1";
            this.ResultElementCountOptions.Clear();
            this.SelectedResultElementCount=null;
            this.OnPropertyChanged(nameof(this.ChartElementCountSliderMaximum));
            this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
            this.OnPropertyChanged(nameof(this.SelectedChartElementCountText));
            this.RefreshSourceTabProgresses();
            StartupDiagnostics.Write(
                "Benchmark run requested. "
                +"SourceTab="+runSourceTab+" "
                +"ExecutionProfile="+executionProfile+" "
                +"InfiniteTestRunner="+useInfiniteTestRunner+" "
                +"LogicalCpus=["+string.Join(",",this.CpuItems.Where(static cpu => cpu.IsSelected).Select(static cpu => cpu.LogicalProcessorIndex))+"] "
                +"Benchmarks="+selected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
                +"ElementCounts=["+string.Join(",",this.GetSelectedElementCounts())+"]");
            var cpus = this.CpuItems
                .Where(static cpu => cpu.IsSelected)
                .Select(static cpu => cpu.LogicalProcessorIndex)
                .ToArray();
            var report = await this.runService.RunAsync(
                selected,
                cpus,
                this.GetSelectedLogicalCpus,
                this.GetSelectedElementCounts(),
                runSourceTab,
                executionProfile,
                this.GetAdaptiveThreshold(),
                !isResumeRun,
                runRoot,
                this.GetCurrentRunSelectedBenchmarkCases,
                result => this.resultPipelineApplier.AcceptRunResult(result,runGeneration,uiRunId),
                this.resultPipelineApplier.AcceptRunStatus,
                new BenchmarkRunExecutionOptions(useInfiniteTestRunner),
                this.runCancellation.Token);
            completedRunRoot=report.RunRoot;
            this.currentUnsavedRunRoot=report.RunRoot;
            completedNormally=true;
            // 終了直後の DB 保存は直列キューへ投げるだけにし、SQLite のロックなどで UI の状態復帰が
            // ブロックされないようにする(書き込み順序は QueueSaveResult と同じキューで保証される)。
            this.QueueUiRunStatus(uiRunId,UiRunStatus.Completed,"Benchmark run completed.");
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
                this.QueueUiRunStatus(uiRunId,UiRunStatus.Stopped,"Benchmark run canceled.");
                StartupDiagnostics.Write("Benchmark run canceled.");
                if(ReferenceEquals(this.runCancellation,runCancellationSource)) {
                    this.SetActiveRunRoot(null);
                }
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run failed.",ex);
            this.StatusText="実行失敗 "+ex.Message;
            this.QueueUiRunStatus(uiRunId,UiRunStatus.Failed,ex.Message);
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
                if(completedNormally&&this.TryGetRemainingRunningVisualDelay(out var finalVisualDelay)) {
                    this.ScheduleFinalLiveVisualCleanup(runGeneration,finalVisualDelay);
                }
                else {
                    this.ApplyFinalLiveVisualCleanup(!completedNormally);
                }
                if(!keepPausedRunRoot) {
                    this.SetActiveRunRoot(null);
                    if(this.activeUiRunId==uiRunId) {
                        this.activeUiRunId=null;
                    }
                }
            }
            // 実行が終わった Run のスナップショットは保存で更新されているため、メモリバッファを破棄する。
            this.selectedRunStateCache=null;
            this.RefreshUnsavedRunSelection();
            this.ClearRunResultHighlights();
            if(completedNormally) {
                this.ApplyFinalBenchmarkSort();
            }
            this.RefreshSourceTabProgresses();
            this.RefreshImprovementPrompt();
            this.UpdateChart();
            // 結果は実行中に QueueSaveResult で SQLite へ保存済みのため、「未保存」扱いを解除する。
            // 実行していないときに終了確認ダイアログが出る問題への対策。
            this.HasUnsavedBenchmarkResults=false;
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
        var displayName = AppProfile.Title+" "+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
        return await this.uiRunRepository.CreateRunAsync(GetUiRunAppKind(),displayName,executionProfileText,runSourceTab);
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
        var state = this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot??string.Empty);
        // JSONシリアライズは保存キューのバックグラウンドスレッドで遅延実行する(UIスレッドで
        // 数百msかかっていた)。stateは値コピー済みDTOなので遅延しても内容は変わらない。
        return new(
            new Lazy<string>(() => JsonSerializer.Serialize(state),LazyThreadSafetyMode.ExecutionAndPublication),
            this.SelectedSourceDefinition.Tab.ToString(),
            this.SelectedResultElementCount?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
    }

    /// <summary>
    /// QueueBenchmarkResultSave メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="runGeneration">runGeneration を指定して実行条件を制御します。</param>
    /// <param name="runId">runId を指定して実行条件を制御します。</param>
    private void QueueBenchmarkResultSave(BenchmarkResultItem result,int runGeneration,long? runId) {
        this.persistenceBatcher.Enqueue(new PersistenceItem(result,runGeneration,runId));
    }

    /// <summary>
    /// QueueBenchmarkResultsSave メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="items">items を指定して実行条件を制御します。</param>
    private void QueueBenchmarkResultsSave(IReadOnlyList<PersistenceItem> items) {
        if(items.Count==0) {
            this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
            return;
        }

        UiThreadBudget.Measure("persist-enqueue",() => {
            var snapshot = this.CreateBenchmarkViewStateSnapshot();
            foreach(var item in items) {
                if(item.RunGeneration!=this.runResultGeneration) {
                    continue;
                }

                var rowKey = this.CreateResultRowKey(item.Result);
                this.uiRunRepository.QueueSaveResult(item.Result,item.UiRunId,rowKey,snapshot);
            }
        });
        this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
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
        this.runEventPipeline.Post(new RunEvent(result,runGeneration,uiRunId,DateTimeOffset.UtcNow));
        this.renderScheduler.RequestRender();
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
            await Task.Delay(PipelineConstants.UiRenderIntervalMs);
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
    private void ApplyRunUiSnapshot(UiSnapshot snapshot) {
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
        this.TryApplyPostRunDisplayFinalize();
    }

    /// <summary>
    /// ApplyRunningSnapshotResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    private void ApplyRunningSnapshotResult(RunEvent item) {
        if(item.RunGeneration!=this.runResultGeneration) {
            return;
        }
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        var result = item.Result;
        this.MarkRunningVisualStarted(result);
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

        var row = this.FindBenchmarkRow(result);
        this.activeChartMethodName=row?.RowKey??result.Method;
        row?.ApplyRunResult(result,cpu,DateTimeOffset.Now);
    }

    /// <summary>
    /// ApplyTerminalSnapshotResult メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="item">item を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkSourceTab? ApplyTerminalSnapshotResult(RunEvent item) {
        if(item.RunGeneration!=this.runResultGeneration) {
            return null;
        }

        var result = item.Result;
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
        var terminalVisualDelay = TimeSpan.Zero;
        var shouldDelayTerminalVisual = isLiveVisualActive&&this.TryGetTerminalVisualDelay(result,out terminalVisualDelay);
        var cpu = this.FindAssignedCpu(result);
        if(!shouldDelayTerminalVisual) {
            this.runningVisualStartedAt.Remove(CreateRunningVisualKey(result));
            if(isLiveVisualActive) {
                cpu?.ApplyJob(result);
                this.ApplySourceTabRunningResult(result);
            }
        }
        BenchmarkSourceTab? dirtyChartTab = null;
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            if(result.SourceTab==this.SelectedSourceDefinition.Tab) {
                this.CurrentElementCountText=FormatElementCountText(result.AxisCount);
                this.UpsertResultItem(result);
                this.RegisterResultElementCount(result.AxisCount);
            }
            if(!shouldDelayTerminalVisual&&isLiveVisualActive) {
                this.FindBenchmarkRow(result)?.ApplyRunResult(result,cpu,DateTimeOffset.Now);
                dirtyChartTab=result.SourceTab;
            }
        }
        this.HasPendingCloseStateSave=true;
        this.HasUnsavedBenchmarkResults=true;
        if(shouldDelayTerminalVisual) {
            this.ScheduleTerminalVisualResult(result,item.RunGeneration,terminalVisualDelay);
        }
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
        if(this.IsBenchmarkPaused&&this.activeRunRoot is not null) {
            _=this.RunAsync(this.activeRunRoot);
            return;
        }

        _=this.RunAsync(null);
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
        var terminalVisualDelay = TimeSpan.Zero;
        var shouldDelayTerminalVisual = isTerminalResult&&this.TryGetTerminalVisualDelay(result,out terminalVisualDelay);
        var shouldRefreshSourceTabProgresses = false;
        if(isRunningResult||isTerminalResult&&!shouldDelayTerminalVisual) {
            cpu?.ApplyJob(result);
            this.ApplySourceTabRunningResult(result);
        }
        if(shouldDelayTerminalVisual) {
            this.ScheduleTerminalVisualResult(result,runGeneration,terminalVisualDelay);
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
        if(isResultForVisibleTab&&!shouldDelayTerminalVisual) {
            row?.ApplyRunResult(result,cpu,completedAt);
        }
        if(isTerminalResult) {
            this.HasPendingCloseStateSave=true;
            this.HasUnsavedBenchmarkResults=true;
            shouldRefreshSourceTabProgresses=true;
            if(isResultForVisibleTab) {
                this.ApplySelectedElementCountToTable();
                if(!shouldDelayTerminalVisual) {
                    row?.ApplyRunResult(result,cpu,completedAt);
                }
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

        this.MarkRunningVisualStarted(result);
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            this.RefreshSourceTabProgressesForRunningResult();
        }

        if(result.SourceTab!=this.SelectedSourceDefinition.Tab) {
            return;
        }

        var row = this.FindBenchmarkRow(result);
        this.RequestBenchmarkAutoScroll(result.SourceTab,result.Method,row?.RowKey??string.Empty);
        this.activeChartMethodName=row?.RowKey??result.Method;
        this.ApplyChartRunningState();
        this.RetainCompletedHighlightsForRunningElement(result.AxisCount);
        row?.ApplyRunResult(result,this.FindAssignedCpu(result),DateTimeOffset.Now);
    }

    /// <summary>
    /// MarkRunningVisualStarted メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void MarkRunningVisualStarted(BenchmarkResultItem result) {
        var key = CreateRunningVisualKey(result);
        if(!this.runningVisualStartedAt.ContainsKey(key)) {
            this.runningVisualStartedAt[key]=this.runVisualScheduler.UtcNow;
        }
    }

    /// <summary>
    /// TryGetTerminalVisualDelay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="delay">delay を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool TryGetTerminalVisualDelay(BenchmarkResultItem result,out TimeSpan delay) {
        delay=TimeSpan.Zero;
        if(!this.runningVisualStartedAt.TryGetValue(CreateRunningVisualKey(result),out var startedAt)) {
            return false;
        }

        var elapsed = this.runVisualScheduler.UtcNow-startedAt;
        if(elapsed>=RunningVisualMinimumDuration) {
            return false;
        }

        delay=RunningVisualMinimumDuration-elapsed;
        return true;
    }

    /// <summary>
    /// TryGetRemainingRunningVisualDelay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="delay">delay を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private bool TryGetRemainingRunningVisualDelay(out TimeSpan delay) {
        delay=TimeSpan.Zero;
        if(this.runningVisualStartedAt.Count==0) {
            return false;
        }

        var now = this.runVisualScheduler.UtcNow;
        foreach(var startedAt in this.runningVisualStartedAt.Values) {
            var remaining = RunningVisualMinimumDuration-(now-startedAt);
            if(remaining>delay) {
                delay=remaining;
            }
        }

        return delay>TimeSpan.Zero;
    }

    /// <summary>
    /// ScheduleTerminalVisualResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <param name="runGeneration">runGeneration を指定して実行条件を制御します。</param>
    /// <param name="delay">delay を指定して実行条件を制御します。</param>
    private void ScheduleTerminalVisualResult(BenchmarkResultItem result,int runGeneration,TimeSpan delay) {
        var cleanupToken = this.finalLiveVisualCleanupToken;
        _=Task.Run(async () => {
            await this.runVisualScheduler.DelayAsync(delay);
            this.uiDispatcher.BeginInvoke(() => {
                if(runGeneration!=this.runResultGeneration||cleanupToken!=this.finalLiveVisualCleanupToken) {
                    return;
                }

                this.ApplyTerminalVisualResult(result);
            });
        });
    }

    /// <summary>
    /// ApplyTerminalVisualResult メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    private void ApplyTerminalVisualResult(BenchmarkResultItem result) {
        this.runningVisualStartedAt.Remove(CreateRunningVisualKey(result));
        this.FindAssignedCpu(result)?.ApplyJob(result);
        this.ApplySourceTabRunningResult(result);
        this.FindBenchmarkRow(result)?.ApplyRunResult(result,this.FindAssignedCpu(result),DateTimeOffset.Now,false);
        this.UpdateChartCore(this.GetSourceTabViewState(result.SourceTab),false);
    }

    /// <summary>
    /// ScheduleFinalLiveVisualCleanup メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="runGeneration">runGeneration を指定して実行条件を制御します。</param>
    /// <param name="delay">delay を指定して実行条件を制御します。</param>
    private void ScheduleFinalLiveVisualCleanup(int runGeneration,TimeSpan delay) {
        _=Task.Run(async () => {
            await this.runVisualScheduler.DelayAsync(delay);
            this.uiDispatcher.BeginInvoke(() => {
                if(runGeneration!=this.runResultGeneration) {
                    return;
                }

                this.ApplyFinalLiveVisualCleanup();
            });
        });
    }

    /// <summary>
    /// discardPendingRunUpdates プロパティです。
    /// </summary>
    /// <value>discardPendingRunUpdates の値を取得/設定します。</value>
    private void ApplyFinalLiveVisualCleanup(bool discardPendingRunUpdates = false) {
        this.finalLiveVisualCleanupToken=unchecked(this.finalLiveVisualCleanupToken+1);
        if(discardPendingRunUpdates) {
            this.runEventPipeline.Clear();
        }

        this.currentRunSession.ClearRunningResults();
        this.runningVisualStartedAt.Clear();
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
        StartupDiagnostics.Write("Post-run display finalize applied.");
        UiThreadBudget.Measure("post-run-finalize",() => {
            this.RefreshResultChecks(this.SelectedSourceDefinition.Tab);
            this.RefreshVisibleResults();
            this.ApplySelectedElementCountToTable();
            this.UpdateChart();
            this.RefreshFinalCompletionStates();
            this.RefreshSourceTabProgresses();
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

    /// <summary>
    /// CreateRunningVisualKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="result">result を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string CreateRunningVisualKey(BenchmarkResultItem result) =>
        result.SourceTab.ToString()
        +"|"+result.Project
        +"|"+result.TypeFullName
        +"|"+result.Method
        +"|"+result.ArgumentKey
        +"|"+result.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
        this.LatestBenchmarkAutoScrollRequest=new AutoScrollRequest(
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

        var elementCounts = this.currentRunSession.GetAllResults()
            .Select(static item => item.AxisCount)
            .Where(static item => item>0)
            .Distinct()
            .Order()
            .ToArray();
        this.SetTransientRunOption(this.CreateUnsavedRunOption(this.currentUnsavedRunRoot??string.Empty,elementCounts));
        this.RefreshToolbarCommandStates();
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
        foreach(var item in this.ResultItems.Where(item => IsResultMatch(item,result))) {
            item.ResultCheckText=checkText;
        }
        foreach(var item in this.GetCurrentRunResultItems(result.SourceTab)
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
            ? "Loop "+elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

    /// <summary>
    /// HasDisplayResults プロパティです。
    /// </summary>
    /// <value>HasDisplayResults の値を取得/設定します。</value>
    private bool HasDisplayResults =>
        this.ResultItems.Count>0
        ||this.HasCurrentRunResultsForSelectedTab
        ||this.currentRunSession.HasAnyResults;

    /// <summary>
    /// RefreshToolbarCommandStates メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshToolbarCommandStates() {
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
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
        this.ClearSourceTabRunningStates();
        this.ClearRetainedChartPoints();
        this.ClearAllSourceTabComparisonDisplays();
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.ImprovementPromptText=string.Empty;
        this.UpdateAllSourceTabCharts(false);
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// ClearAllCurrentRunResultsForFreshRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearAllCurrentRunResultsForFreshRun() {
        this.ClearSourceTabRunningStates();
        this.ClearRetainedChartPoints();
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
            state.BenchmarkRatioSummaryText=string.Empty;
            state.ChartPoints.Clear();
            state.LatencyHotspotRows.Clear();
            state.AllocationDiagnosticRows.Clear();
            state.CpuTimelineRows.Clear();
        }
        this.OnPropertyChanged(nameof(this.BenchmarkRatioSummaryText));
        this.OnPropertyChanged(nameof(this.ChartPoints));
        this.OnPropertyChanged(nameof(this.LatencyHotspotRows));
        this.OnPropertyChanged(nameof(this.AllocationDiagnosticRows));
        this.OnPropertyChanged(nameof(this.CpuTimelineRows));
    }

    /// <summary>
    /// ClearSelectedSourceLiveResults メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearSelectedSourceLiveResults() {
        this.ClearRetainedChartPoints();
        this.currentRunSession.ClearSourceTab(this.SelectedSourceDefinition.Tab);
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.BenchmarkRatioSummaryText=string.Empty;
        this.RefreshImprovementPrompt();
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

    /// <summary>
    /// PrepareSelectedSourceLiveResultsForRun メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void PrepareSelectedSourceLiveResultsForRun() {
        this.ClearRetainedChartPoints();
        this.currentRunSession.ClearSourceTab(this.SelectedSourceDefinition.Tab);
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.BenchmarkRatioSummaryText=string.Empty;
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
    private void ApplyComparisonRowsToTable(SourceTabViewState state) {
        var hasCurrentRunResults = this.currentRunSession.HasResults(state.Definition.Tab);
        foreach(var benchmark in state.Rows) {
            benchmark.ApplyComparison(this.FindComparisonRow(state,benchmark),this.IsBenchmarkRunActive||hasCurrentRunResults);
        }
        this.RefreshBenchmarkRatioSummary(state);
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
        foreach(var result in this.GetResultSourceForDisplay().Where(result => selectedN is null||result.AxisCount==selectedN)) {
            var row = this.FindBenchmarkRow(result);
            row?.ApplyRunResult(result,this.FindAssignedCpu(result),result.StartedAt??DateTimeOffset.Now,false);
        }
    }

    /// <summary>
    /// FindComparisonRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkComparisonRow? FindComparisonRow(TableRow row) {
        return this.FindComparisonRow(this.SelectedSourceTabViewState,row);
    }

    /// <summary>
    /// FindComparisonRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkComparisonRow? FindComparisonRow(SourceTabViewState state,TableRow row) {
        foreach(var item in state.ComparisonRows) {
            if(IsComparisonRowMatch(row,item)) {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// RefreshBenchmarkRatioSummary メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkRatioSummary() {
        this.RefreshBenchmarkRatioSummary(this.SelectedSourceTabViewState);
    }

    /// <summary>
    /// RefreshBenchmarkRatioSummary メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    private void RefreshBenchmarkRatioSummary(SourceTabViewState state) {
        state.BenchmarkRatioSummaryText=BenchmarkImprovementPromptBuilder.CreateSummary(state.ComparisonRows).DisplayText;
        if(state==this.SelectedSourceTabViewState) {
            this.OnPropertyChanged(nameof(this.BenchmarkRatioSummaryText));
        }
    }

    /// <summary>
    /// RefreshImprovementPrompt メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshImprovementPrompt() {
        var state = this.GetProgressStateForImprovementPrompt();
        var candidates = state is null
            ? []
            : BenchmarkImprovementCandidateBuilder.BuildCandidates(state);
        this.ApplyImprovementCandidates(candidates);
        this.RefreshImprovementPromptText();
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
                var row = new ImprovementCandidateRow(candidate,selected);
                row.SelectionChanged+=this.OnImprovementCandidateSelectionChanged;
                this.ImprovementCandidates.Add(row);
            }
        }
        finally {
            this.isApplyingImprovementCandidateSelection=false;
        }
        this.ImprovementCandidateSummaryText=this.CreateImprovementCandidateSummary();
    }

    /// <summary>
    /// OnImprovementCandidateSelectionChanged メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="sender">sender を指定して実行条件を制御します。</param>
    /// <param name="args">args を指定して実行条件を制御します。</param>
    private void OnImprovementCandidateSelectionChanged(object? sender,EventArgs args) {
        if(sender is not ImprovementCandidateRow row) {
            return;
        }

        this.improvementCandidateSelections[row.Key]=row.IsSelected;
        if(!this.isApplyingImprovementCandidateSelection) {
            this.SaveImprovementCandidateSelection(row);
            this.RefreshImprovementPromptText();
            this.ImprovementCandidateSummaryText=this.CreateImprovementCandidateSummary();
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

    /// <summary>
    /// CreateImprovementCandidateSummary メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private string CreateImprovementCandidateSummary() {
        var selected = this.ImprovementCandidates.Count(static row => row.IsSelected);
        var total = this.ImprovementCandidates.Count;
        var time = this.ImprovementCandidates.Count(static row => row.Candidate.HasTimeRegression);
        var allocated = this.ImprovementCandidates.Count(static row => row.Candidate.HasAllocatedRegression);
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
        this.hasLoadedImprovementCandidateSelections=false;
        this.improvementCandidateSelections.Clear();
        if(runId is not { } value) {
            return;
        }

        try {
            foreach(var item in this.uiRunStore.ListUiSelections(value).Where(static item => string.Equals(item.Scope,"ImprovementCandidate",StringComparison.OrdinalIgnoreCase))) {
                this.improvementCandidateSelections[item.SelectionKey]=item.IsSelected;
                this.hasLoadedImprovementCandidateSelections=true;
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark improvement candidate selection load failed.",ex);
        }
    }

    /// <summary>
    /// SaveImprovementCandidateSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    private void SaveImprovementCandidateSelection(ImprovementCandidateRow row) {
        if(this.GetCurrentImprovementSelectionRunId() is not { } runId) {
            return;
        }

        try {
            if(!this.hasLoadedImprovementCandidateSelections) {
                foreach(var item in this.ImprovementCandidates) {
                    this.uiRunStore.SaveUiSelection(runId,"ImprovementCandidate",item.Key,item.IsSelected);
                }
            }
            else {
                this.uiRunStore.SaveUiSelection(runId,"ImprovementCandidate",row.Key,row.IsSelected);
            }
            this.hasLoadedImprovementCandidateSelections=true;
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark improvement candidate selection save failed.",ex);
        }
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
    /// lastRunningProgressRefreshAt フィールドです。
    /// </summary>
    private DateTimeOffset lastRunningProgressRefreshAt = DateTimeOffset.MinValue;
    /// <summary>
    /// RunningProgressRefreshInterval フィールドです。
    /// </summary>
    private static readonly TimeSpan RunningProgressRefreshInterval = TimeSpan.FromMilliseconds(250);

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
        IReadOnlyList<BenchmarkResultItem> SessionResults);

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
            .Select(option => new SourceTabProgressInput(
                option,
                option.Definition,
                this.CreateProgressCells(option.Definition),
                this.GetCurrentRunResultItems(option.Definition.Tab)))
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
                .Select(input => BenchmarkTabProgressLogic.Calculate(
                    input.Cells,
                    BuildProgressMeasurements(input.SessionResults,resultItemsSnapshot,stateJobsSnapshot,input.Definition.Tab),
                    input.Definition.LeftProject,
                    input.Definition.RightProject,
                    maxElementCount,
                    thresholdNanoseconds))
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
                item.MeanNanoseconds))
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
            return state.Rows
                .SelectMany(row => row.ProjectCells.Select(cell => CreateProgressCell(row,cell.Project,cell.HasCase&&cell.IsSelected)))
                .ToArray();
        }

        return BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,sourceTab)
            .SelectMany(row => sourceTab.ProjectOrder.Select(project => CreateProgressCell(
                row,
                project,
                row.CasesByProject.ContainsKey(project)
                    &&this.benchmarkCellSelections.TryGetValue(
                    CreateBenchmarkCellKey(sourceTab,row.RowKey,project),
                    out var selected)
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
    private static BenchmarkTabProgressCell CreateProgressCell(TableRow row,string project,bool isSelected) {
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
        this.ClearSelectedRunDisplay();
        var sourceTabs = this.SourceTabs.Select(static option => option.Definition).ToArray();
        var requestToken = ++this.selectedRunLoadRequestToken;
        Task.Run(() => {
            try {
                IReadOnlyList<BenchmarkResultItem>? items = sourceTabs
                    .SelectMany(sourceTab => this.uiRunRepository.LoadResultItems(run.UiRunId,run.UpdatedAt,sourceTab))
                    .ToArray();
                return (Items: items,Error: (Exception?)null);
            }
            catch(Exception ex) {
                return (Items: (IReadOnlyList<BenchmarkResultItem>?)null,Error: ex);
            }
        }).ContinueWith(task => {
            this.uiDispatcher.BeginInvoke(() => {
                if(requestToken!=this.selectedRunLoadRequestToken) {
                    return; // より新しい Run/タブ選択で上書きされたため破棄する。
                }
                if(task.Result.Error is { } ex||task.Result.Items is not { } results) {
                    StartupDiagnostics.Write("Benchmark run DB load failed.",task.Result.Error);
                    this.StatusText="DB 読み込み失敗 "+(task.Result.Error?.Message??"unknown");
                    return;
                }
                this.ApplyLoadedRunResultItems(results);
            });
        },TaskScheduler.Default);
    }

    /// <summary>
    /// ApplyLoadedRunResultItems メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="results">results を指定して実行条件を制御します。</param>
    private void ApplyLoadedRunResultItems(IReadOnlyList<BenchmarkResultItem> results) {
        using(this.BeginChartRefreshBatch()) {
            foreach(var result in results) {
                this.ResultItems.Add(result);
            }

            this.RebuildResultElementCounts(this.ResultItems
                .Select(static item => item.AxisCount)
                .Where(static item => item>0)
                .Distinct()
                .Order()
                .ToArray());
            if(this.SelectedResultElementCount is null) {
                this.SelectedResultElementCount=this.ResultElementCountOptions.LastOrDefault();
            }
            this.RefreshVisibleResults();
            this.RefreshAllComparisonRowsFromDisplayResults();
            this.ApplyComparisonRowsToAllTables();
            this.ApplySelectedElementCountToTable();
            this.RefreshImprovementPrompt();
            this.UpdateAllSourceTabCharts(false);
            this.ApplyCurrentSort();
        }
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
            using(this.BeginChartRefreshBatch()) {
                var run = this.SelectedRunOption;
                if(this.RestoreCurrentRunDisplayForSelectedTab()) {
                    return;
                }

                if(this.ShouldKeepCurrentRunMemoryDisplay()) {
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
                this.ClearRetainedChartPoints();
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

            var cpu = item.AssignedLogicalCpu is { } logicalCpu
                ? this.CpuItems.FirstOrDefault(cpuItem => cpuItem.LogicalProcessorIndex==logicalCpu)
                : null;
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
            this.SelectedRunOption is { UiRunId:>0 });

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
            "Unsaved",
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
        return this.runProjectionService.BuildDisplayedRowsForElementCount(
            this.ResultItems,
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
        using var scope = this.BeginResultSourceForDisplayCache();
        var stopwatch = Stopwatch.StartNew();
        var measurements = this.CreateDisplayMeasurements();
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
                var hasLeftResult = this.HasResult(row.MethodName,pair.LeftProject,leftTypeFullName,selectedElementCount,leftArgumentKey);
                var hasRightResult = this.HasResult(row.MethodName,pair.RightProject,rightTypeFullName,selectedElementCount,rightArgumentKey);
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
                        this.ApplySkippedCell(row,pair.LeftProject,stoppedAtElementCount);
                    }
                    if(right.IsSelected&&!hasRightResult) {
                        this.ApplySkippedCell(row,pair.RightProject,stoppedAtElementCount);
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

    // 完了判定は O(行数×measurements件数) で実行が進むほど重くなる(観測: 1tickで700?1300ms)。
    // UIスレッドに残すのは「生の結果配列の確保(参照コピー)」と「SetFinalCompleted反映」のみで、
    // 選択フィルタ(IsResultSelectedForDisplay)・measurement射影・判定計算は全てバックグラウンド。
    // 行の RowKey/MethodName/CasesByProject/ProjectCells はコンストラクタ後不変、cell.IsSelected は
    // boolのatomic読みなので、バックグラウンドから読んでも安全(Phase Bの比較行構築と同じ前提)。
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
        var sourceTab = this.SelectedSourceDefinition;
        var source = BenchmarkRunDisplaySourceLogic.ShouldUseCurrentRunMemory(this.HasCurrentRunResultsForSelectedTab)
            ? this.GetCurrentRunResultItemsForSelectedTab()
            : this.ResultItems.ToArray();
        var rows = this.BenchmarkRows.ToArray();
        var pairs = this.BenchmarkPairs();
        var maxElementCount = this.GetFinalCompletionMaxElementCount();
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        var token = ++this.finalCompletionRequestToken;
        var generation = this.runResultGeneration;
        this.finalCompletionRefreshInFlight=true;
        var stopwatch = Stopwatch.StartNew();
        Task.Run(() => {
            var measurements = this.CreateDisplayMeasurementsFrom(source,sourceTab);
            return measurements.Length==0
                ? null
                : ComputeFinalCompletionStates(measurements,rows,pairs,maxElementCount,thresholdNanoseconds);
        })
            .ContinueWith(task => {
                UiThreadBudget.Record("bg:completion",stopwatch.Elapsed.TotalMilliseconds);
                this.uiDispatcher.BeginInvoke(() => {
                    this.finalCompletionRefreshInFlight=false;
                    if(token==this.finalCompletionRequestToken&&generation==this.runResultGeneration&&!task.IsFaulted) {
                        var completed = task.Result;
                        UiThreadBudget.Measure("completion-apply",() => {
                            if(completed is null) {
                                this.ClearFinalCompletionStates();
                            }
                            else {
                                for(var index = 0; index<rows.Length; index++) {
                                    rows[index].SetFinalCompleted(completed[index]);
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

    /// <summary>
    /// CreateDisplayMeasurementsFrom メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="source">source を指定して実行条件を制御します。</param>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkElementMeasurement[] CreateDisplayMeasurementsFrom(IReadOnlyList<BenchmarkResultItem> source,BenchmarkSourceTabDefinition sourceTab) {
        var measurements = new List<BenchmarkElementMeasurement>(source.Count);
        foreach(var result in source) {
            if(!this.IsResultSelectedForDisplay(result,sourceTab)) {
                continue;
            }

            measurements.Add(new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ArgumentKey,
                result.AxisCount,
                result.Status,
                result.MeanNanoseconds));
        }

        return measurements.ToArray();
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
        TableRow[] rows,
        IReadOnlyList<(string LeftProject,string RightProject)> pairs,
        int maxElementCount,
        double thresholdNanoseconds) {
        var measurementsByMethod = measurements.ToLookup(static m => m.Method,StringComparer.OrdinalIgnoreCase);
        var results = new bool[rows.Length];
        for(var index = 0; index<rows.Length; index++) {
            var row = rows[index];
            var rowMeasurements = measurementsByMethod[row.MethodName].ToArray();
            results[index]=pairs.Any(pair =>
                BenchmarkRowCompletionLogic.IsFinalCompleted(
                    rowMeasurements,
                    CreateProgressCell(row,pair.LeftProject,true),
                    CreateProgressCell(row,pair.RightProject,true),
                    maxElementCount,
                    thresholdNanoseconds));
        }

        return results;
    }

    /// <summary>
    /// CreateDisplayMeasurements メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private BenchmarkElementMeasurement[] CreateDisplayMeasurements() =>
        this.GetResultSourceForDisplay()
            .Select(static result => new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.TypeFullName,
                result.ArgumentKey,
                result.AxisCount,
                result.Status,
                result.MeanNanoseconds))
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
        if(this.IsBenchmarkRunActive||this.HasCurrentRunResultsForSelectedTab) {
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
    private void ApplySkippedCell(TableRow row,string project,int stoppedAtElementCount) {
        var typeFullName = GetRowTypeFullName(row,project);
        var argumentKey = GetRowArgumentKey(row,project);
        var result = this.GetResultItem(row.MethodName,project,typeFullName,stoppedAtElementCount,argumentKey);
        var cpu = result?.AssignedLogicalCpu is { } logicalCpu
            ? this.CpuItems.FirstOrDefault(item => item.LogicalProcessorIndex==logicalCpu)
            : null;
        var measuredAt = result?.StartedAt;
        var metadataMissing = result is null||cpu is null||measuredAt is null;
        if(metadataMissing) {
            this.ReportMissingBenchmarkMetadata(row.MethodName,project,stoppedAtElementCount,result);
        }

        row.ApplySkipped(
            project,
            stoppedAtElementCount,
            this.GetResultMetrics(row.MethodName,project,typeFullName,stoppedAtElementCount,argumentKey),
            cpu,
            measuredAt,
            metadataMissing);
    }

    /// <summary>
    /// IReadOnlyList< メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="LeftProject">LeftProject を指定して実行条件を制御します。</param>
    /// <param name="RightProject">RightProject を指定して実行条件を制御します。</param>
    private IReadOnlyList<(string LeftProject,string RightProject)> BenchmarkPairs() => [
        (this.SelectedSourceDefinition.LeftProject,this.SelectedSourceDefinition.RightProject),
    ];

    /// <summary>
    /// GetRowTypeFullName メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string GetRowTypeFullName(TableRow row,string project) =>
        row.CasesByProject.TryGetValue(project,out var benchmark)
            ? benchmark.TypeFullName
            : row.CasesByProject.Values.FirstOrDefault()?.TypeFullName??string.Empty;

    /// <summary>
    /// GetRowArgumentKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string GetRowArgumentKey(TableRow row,string project) =>
        row.CasesByProject.TryGetValue(project,out var benchmark)
            ? benchmark.ArgumentKey
            : row.CasesByProject.Values.FirstOrDefault()?.ArgumentKey??string.Empty;

    /// <summary>
    /// argumentKey プロパティです。
    /// </summary>
    /// <value>argumentKey の値を取得/設定します。</value>
    private bool HasResult(string method,string project,string typeFullName,int elementCount,string argumentKey = "") {
        var results = this.GetResultSourceForDisplay();
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
                result.MeanNanoseconds));
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
                result.MeanNanoseconds));
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
        var selectedRunResult = this.GetResultSourceForDisplay()
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
        var results = this.GetResultSourceForDisplay();
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

    // GetResultSourceForDisplay() は呼び出し毎に全結果をLINQ再スキャンする(行/セル毎に全結果を
    // フィルタし直すコストが大きい)。同期的な1回のUI更新tick内では入力(ResultItems/選択タブ)は
    // 変化しないため、tick単位で結果を使い回すための短命キャッシュ。ApplySkippedCells等、
    // 1tickで何十回も呼ばれる箇所の周りを BeginResultSourceForDisplayCache() で囲んで使う。
    /// <summary>
    /// resultSourceForDisplayCache フィールドです。
    /// </summary>
    private IReadOnlyList<BenchmarkResultItem>? resultSourceForDisplayCache;

    /// <summary>
    /// BeginResultSourceForDisplayCache メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IDisposable BeginResultSourceForDisplayCache() {
        this.resultSourceForDisplayCache=null;
        return new ResultSourceForDisplayCacheScope(this);
    }

    /// <summary>
    /// ResultSourceForDisplayCacheScope メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="owner">owner を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private sealed class ResultSourceForDisplayCacheScope(RunnerViewModel owner):IDisposable {
        public void Dispose() => owner.resultSourceForDisplayCache=null;
    }

    /// <summary>
    /// GetResultSourceForDisplay メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<BenchmarkResultItem> GetResultSourceForDisplay() {
        if(this.resultSourceForDisplayCache is { } cached) {
            return cached;
        }

        var source = BenchmarkRunDisplaySourceLogic.ShouldUseCurrentRunMemory(this.HasCurrentRunResultsForSelectedTab)
            ? this.GetCurrentRunResultItemsForSelectedTab()
            : this.ResultItems;
        var results = source.Where(this.IsResultSelectedForDisplay).ToArray();
        this.resultSourceForDisplayCache=results;
        return results;
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

        return BenchmarkResultDisplaySelectionLogic.IsResultSelected(
            result,
            row.ProjectCells.Select(cell => new BenchmarkResultSelectionCell(
                cell.Project,
                row.MethodName,
                GetRowTypeFullName(row,cell.Project),
                GetRowArgumentKey(row,cell.Project),
                cell.HasCase,
                cell.IsSelected)));
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
            var snapshot = this.uiRunStore.ListUiViewStates(run.UiRunId)
                .FirstOrDefault(static item => string.Equals(item.StateKey,"BenchmarkProgressStateJson",StringComparison.OrdinalIgnoreCase))
                ?.StateValue;
            var state = string.IsNullOrWhiteSpace(snapshot)
                ? null
                : JsonSerializer.Deserialize<ProgressState>(snapshot);
            this.selectedRunStateCache=(run.UiRunId,state);
            return state;
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark DB progress snapshot load failed.",ex);
            return null;
        }
    }

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
            this.UpdateChartCore(state,preserveExistingWhenNoMeasuredPoints);
        }
        this.OnPropertyChanged(nameof(this.ChartPoints));
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
    private void UpdateChartCore(SourceTabViewState state,bool preserveExistingWhenNoMeasuredPoints = true) {
        if(!this.IsBenchmarkRunActive) {
            // 非実行中は ComparisonRows キャッシュを使う軽い同期パス。
            this.ApplyChartFromComparisonRows(state,this.BuildElementComparisonRows(state));
            return;
        }

        // 実行中の比較行再構築は結果毎の選択判定・整形・DB参照(FindPreviousResult)を含み重い。
        // currentRunSession に触れるスナップショット取得だけをUIスレッドで済ませ、比較行と
        // 診断行の計算はバックグラウンドで行う。チャートポイント構築は retainedChartPoints
        // 辞書と行の可変状態(選択・実行中表示)を読むため、反映側(UIスレッド)に残す。
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
                LatencyRows: this.BuildMetricDiagnosticRowsReadable(definition,rows,MetricKind.Time),
                AllocationRows: this.BuildMetricDiagnosticRowsReadable(definition,rows,MetricKind.Allocated),
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
                            var points = this.BuildBenchmarkChartPoints(state,computed.Rows);
                            this.SyncChartPoints(state.ChartPoints,points);
                            state.ChartHeight=Math.Max(BenchmarkChartMinimumHeight,points.Count*BenchmarkChartRowHeight+BenchmarkChartVerticalPadding);
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
    private void ApplyChartFromComparisonRows(SourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        var points = this.BuildBenchmarkChartPoints(state,rows);
        this.SyncChartPoints(state.ChartPoints,points);
        state.ChartHeight=Math.Max(BenchmarkChartMinimumHeight,points.Count*BenchmarkChartRowHeight+BenchmarkChartVerticalPadding);
        this.RefreshDiagnosticViews(state,rows);
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
    private void RefreshDiagnosticViews(SourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.SyncDiagnosticRows(state.LatencyHotspotRows,this.BuildMetricDiagnosticRowsReadable(state.Definition,rows,MetricKind.Time));
        this.SyncDiagnosticRows(state.AllocationDiagnosticRows,this.BuildMetricDiagnosticRowsReadable(state.Definition,rows,MetricKind.Allocated));
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
    private IReadOnlyList<DiagnosticRow> BuildMetricDiagnosticRowsReadable(BenchmarkSourceTabDefinition sourceTab,IReadOnlyList<BenchmarkComparisonRow> rows,MetricKind metric) {
        return rows
            .Select(row => {
                var left = row.GetProject(sourceTab.LeftProject);
                var right = row.GetProject(sourceTab.RightProject);
                var leftValue = metric==MetricKind.Time ? left.TimeNanoseconds : left.AllocatedBytes;
                var rightValue = metric==MetricKind.Time ? right.TimeNanoseconds : right.AllocatedBytes;
                var ratio = leftValue is >0&&rightValue is >0 ? leftValue/rightValue : (double?)null;
                return new {
                    Row = row,
                    LeftValue = leftValue,
                    RightValue = rightValue,
                    Ratio = ratio,
                };
            })
            .Where(item => item.LeftValue is not null&&item.RightValue is not null)
            .OrderByDescending(item => item.Ratio??(item.LeftValue>0&&item.RightValue==0 ? double.PositiveInfinity : 0))
            .ThenByDescending(item => item.LeftValue??0)
            .Take(40)
            .Select(item => new DiagnosticRow(
                metric==MetricKind.Time ? "Latency" : "Allocation",
                BenchmarkFormatting.ShortName(item.Row.Benchmark),
                this.SelectedResultElementCount?.DisplayName??"-",
                item.Ratio is { } ratio ? ratio.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"x" : "-",
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
    private IReadOnlyList<DiagnosticRow> BuildMetricDiagnosticRows(IReadOnlyList<BenchmarkComparisonRow> rows,MetricKind metric) {
        var sourceTab = this.SelectedSourceDefinition;
        return rows
            .Select(row => {
                var left = row.GetProject(sourceTab.LeftProject);
                var right = row.GetProject(sourceTab.RightProject);
                var leftValue = metric==MetricKind.Time ? left.TimeNanoseconds : left.AllocatedBytes;
                var rightValue = metric==MetricKind.Time ? right.TimeNanoseconds : right.AllocatedBytes;
                var ratio = leftValue is >0&&rightValue is >0 ? leftValue/rightValue : (double?)null;
                return new {
                    Row = row,
                    LeftValue = leftValue,
                    RightValue = rightValue,
                    Ratio = ratio,
                };
            })
            .Where(item => item.LeftValue is not null&&item.RightValue is not null)
            .OrderByDescending(item => item.Ratio??(item.LeftValue>0&&item.RightValue==0 ? double.PositiveInfinity : 0))
            .ThenByDescending(item => item.LeftValue??0)
            .Take(40)
            .Select(item => new DiagnosticRow(
                metric==MetricKind.Time ? "Latency" : "Allocation",
                BenchmarkFormatting.ShortName(item.Row.Benchmark),
                this.SelectedResultElementCount?.DisplayName??"-",
                item.Ratio is { } ratio ? ratio.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"x" : "-",
                FormatMetric(item.LeftValue,metric),
                FormatMetric(item.RightValue,metric),
                sourceTab.LeftProject+" / "+sourceTab.RightProject+" / "+item.Row.MethodName))
            .ToArray();
    }

    /// <summary>
    /// BuildCpuTimelineRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<DiagnosticRow> BuildCpuTimelineRows() {
        return BuildCpuTimelineRows(this.currentRunSession.GetAllResults());
    }

    /// <summary>
    /// BuildCpuTimelineRows メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="allResults">allResults を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static IReadOnlyList<DiagnosticRow> BuildCpuTimelineRows(IReadOnlyList<BenchmarkResultItem> allResults) {
        return allResults
            .OrderByDescending(static item => item.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .Select(static item => new DiagnosticRow(
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
    private void SyncDiagnosticRows(ObservableCollection<DiagnosticRow> target,IReadOnlyList<DiagnosticRow> rows) {
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
            this.GeneratedCodeAuditReport=new GeneratedCodeAuditReport("-","-","Select a target method to inspect generated code.",null,[],"-");
            return;
        }
        if(this.generatedCodeAuditCache.TryGetValue(input.Value.CacheKey,out var cached)) {
            this.GeneratedCodeAuditReport=cached;
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
                        ? new GeneratedCodeAuditReport(input.Value.MethodName,input.Value.LeftProject+" vs "+input.Value.RightProject,"Load failed",null,[],"Generated code audit failed: "+task.Exception!.GetBaseException().Message)
                        : task.Result;
                    if(!task.IsFaulted) {
                        this.generatedCodeAuditCache[input.Value.CacheKey]=report;
                    }
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
    /// <param name="CacheKey">CacheKey を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct GeneratedCodeAuditInput(
        string MethodName,string LeftProject,string RightProject,
        string BenchmarkProject,string AssemblyName,string TypeFullName,string CacheKey);

    public sealed record GeneratedCodeAuditReport(
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
            return new GeneratedCodeAuditInput(selected.MethodName,sourceTab.LeftProject,sourceTab.RightProject,"","","","missing|"+selected.MethodName);
        }

        var cacheKey = "readable|"+benchmark.AssemblyName+"|"+selected.MethodName+"|"+benchmark.TypeFullName+"|"+benchmark.ArgumentKey;
        return new GeneratedCodeAuditInput(selected.MethodName,sourceTab.LeftProject,sourceTab.RightProject,benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,cacheKey);
    }

    /// <summary>
    /// CreateGeneratedCodeAuditReportCore メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="input">input を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private GeneratedCodeAuditReport CreateGeneratedCodeAuditReportCore(GeneratedCodeAuditInput input) {
        var comparisonPair = input.LeftProject+" vs "+input.RightProject;
        if(string.IsNullOrEmpty(input.AssemblyName)) {
            return new GeneratedCodeAuditReport(input.MethodName,comparisonPair,"BenchmarkCase was not found.",null,[],"-");
        }

        var auditTarget = input.BenchmarkProject+" / "+input.AssemblyName+" / "+input.TypeFullName;
        var generatedFile = this.FindGeneratedCodeFile(input.AssemblyName,input.MethodName);
        if(generatedFile is null) {
            return new GeneratedCodeAuditReport(input.MethodName,comparisonPair,auditTarget,null,
                [new BenchmarkBadgeItem("generated code","generated-current .g.cs was not found. Build the benchmark project and retry.",BenchmarkBadgeSeverity.Warning)],
                "-");
        }

        var generatedFileName = Path.GetFileName(generatedFile);
        var text = File.ReadAllText(generatedFile);
        var block = ExtractGeneratedMethodBlock(text,input.MethodName);
        if(string.IsNullOrWhiteSpace(block)) {
            return new GeneratedCodeAuditReport(input.MethodName,comparisonPair,auditTarget,generatedFileName,
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
        return new GeneratedCodeAuditReport(input.MethodName,comparisonPair,auditTarget,generatedFileName,flags,verdict);
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

    /// <summary>
    /// SyncChartPoints メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="target">target を指定して実行条件を制御します。</param>
    /// <param name="points">points を指定して実行条件を制御します。</param>
    private void SyncChartPoints(ObservableCollection<ChartComparisonPoint> target,IReadOnlyList<ChartComparisonPoint> points) {
        for(var index = 0; index<points.Count; index++) {
            var point = points[index];
            if(index<target.Count) {
                if(target[index].Benchmark==point.Benchmark
                    &&target[index].DisplayName==point.DisplayName
                    &&Math.Abs(target[index].LeftValue-point.LeftValue)<0.000001
                    &&Math.Abs(target[index].RightValue-point.RightValue)<0.000001
                    &&target[index].LeftText==point.LeftText
                    &&target[index].RightText==point.RightText
                    &&target[index].TooltipText==point.TooltipText
                    &&target[index].IsLeftUiSelected==point.IsLeftUiSelected
                    &&target[index].IsRightUiSelected==point.IsRightUiSelected
                    &&target[index].IsRowUiSelected==point.IsRowUiSelected) {
                    continue;
                }

                target[index]=point;
            }
            else {
                target.Add(point);
            }
        }

        while(target.Count>points.Count) {
            target.RemoveAt(target.Count-1);
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
    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(SourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        var gridOrderPoints = state.Rows
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
        TableRow row,
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
        TableRow row,
        IReadOnlyList<BenchmarkComparisonRow> rows,
        BenchmarkSourceTabDefinition sourceTab) {
        var comparisonRow = FindComparisonRow(row,rows);
        var left = this.GetChartMetrics(row,sourceTab.LeftProject,comparisonRow);
        var right = this.GetChartMetrics(row,sourceTab.RightProject,comparisonRow);
        var leftValue = GetMetricValue(left,this.SelectedMetric.Kind);
        var rightValue = GetMetricValue(right,this.SelectedMetric.Kind);
        var cacheKey = this.GetChartPointCacheKey(sourceTab,row.RowKey);
        if(leftValue is not null&&rightValue is not null) {
            var measuredPoint = this.ApplyChartUiSelection(row,this.CreateChartComparisonPoint(row,leftValue,rightValue,sourceTab.Pair));
            this.retainedChartPoints[cacheKey]=measuredPoint;
            return this.ApplyChartRunningState(measuredPoint);
        }

        this.retainedChartPoints.Remove(cacheKey);
        return null;
    }

    /// <summary>
    /// FindComparisonRow メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="rows">rows を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static BenchmarkComparisonRow? FindComparisonRow(TableRow row,IReadOnlyList<BenchmarkComparisonRow> rows) {
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
    private static bool IsComparisonRowMatch(TableRow row,BenchmarkComparisonRow item) =>
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
    private ChartComparisonPoint CreateChartComparisonPoint(TableRow row,double? leftValue,double? rightValue) =>
        this.CreateChartComparisonPoint(row.RowKey,row.BenchmarkName,leftValue,rightValue,row,this.SelectedComparisonPair);

    /// <summary>
    /// CreateChartComparisonPoint メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="leftValue">leftValue を指定して実行条件を制御します。</param>
    /// <param name="rightValue">rightValue を指定して実行条件を制御します。</param>
    /// <param name="pair">pair を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ChartComparisonPoint CreateChartComparisonPoint(TableRow row,double? leftValue,double? rightValue,ComparisonPairOption pair) =>
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
    private ChartComparisonPoint CreateChartComparisonPoint(string benchmark,string displayName,double? leftValue,double? rightValue,TableRow row,ComparisonPairOption pair) {
        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var shareTexts = BenchmarkChartSelectionLayoutLogic.CreateShareTexts(leftNumber,rightNumber);
        double? ratio = BenchmarkChartSelectionLayoutLogic.CreateRatio(leftNumber,rightNumber);
        var ratioText = ratio is { } value
            ? value.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)
            : "-";
        var metricName = this.SelectedMetric.Kind==MetricKind.Time ? "Time" : "Memory";
        var tooltip = BenchmarkFormatting.ShortName(displayName)+"\n"
            +pair.LeftProject+" "+metricName+": "+FirstText(FormatMetric(leftValue,this.SelectedMetric.Kind),"-")+"\n"
            +pair.RightProject+" "+metricName+": "+FirstText(FormatMetric(rightValue,this.SelectedMetric.Kind),"-")
            +"\nRatio(left/right): "+ratioText
            +"\nShare: "+pair.LeftProject+" "+shareTexts.LeftText+" / "+pair.RightProject+" "+shareTexts.RightText
            +(string.IsNullOrWhiteSpace(ArgumentTooltipFormatter.CreateArgumentTooltip(row))
                ? string.Empty
                : "\n"+ArgumentTooltipFormatter.CreateArgumentTooltip(row));
        return new ChartComparisonPoint(
            benchmark,
            BenchmarkFormatting.ShortName(displayName),
            leftNumber,
            rightNumber,
            shareTexts.LeftText,
            shareTexts.RightText,
            tooltip,
            ratio);
    }

    /// <summary>
    /// CreateChartArgumentTooltip メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string CreateChartArgumentTooltip(TableRow row) {
        return ArgumentTooltipFormatter.CreateArgumentTooltip(row);
    }

    /// <summary>
    /// GetChartPointCacheKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string GetChartPointCacheKey(string method) =>
        this.GetChartPointCacheKey(this.SelectedSourceDefinition,method);

    /// <summary>
    /// GetChartPointCacheKey メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="sourceTab">sourceTab を指定して実行条件を制御します。</param>
    /// <param name="method">method を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private string GetChartPointCacheKey(BenchmarkSourceTabDefinition sourceTab,string method) =>
        sourceTab.Id+"|"
        +sourceTab.Pair.LeftProject+"|"
        +sourceTab.Pair.RightProject+"|"
        +this.SelectedMetric.Kind+"|"
        +method;

    /// <summary>
    /// ClearRetainedChartPoints メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ClearRetainedChartPoints() {
        this.retainedChartPoints.Clear();
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
    private ChartComparisonPoint ApplyChartUiSelection(TableRow row,ChartComparisonPoint point) =>
        point.WithUiSelection(row.LeftCell.IsUiSelected,row.RightCell.IsUiSelected,row.IsUiSelected);

    /// <summary>
    /// RefreshChartUiSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="state">state を指定して実行条件を制御します。</param>
    private void RefreshChartUiSelection(SourceTabViewState state) {
        for(var index = 0; index<state.ChartPoints.Count; index++) {
            var point = state.ChartPoints[index];
            var rowIndex = GetBenchmarkRowIndex(state,point.Benchmark);
            if(rowIndex<0) {
                continue;
            }

            var row = state.Rows[rowIndex];
            var next = this.ApplyChartUiSelection(row,point);
            if(next.IsLeftUiSelected==point.IsLeftUiSelected
                &&next.IsRightUiSelected==point.IsRightUiSelected
                &&next.IsRowUiSelected==point.IsRowUiSelected) {
                continue;
            }

            state.ChartPoints[index]=next;
        }
    }

    /// <summary>
    /// ApplyChartRunningState メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyChartRunningState() {
        for(var index = 0; index<this.ChartPoints.Count; index++) {
            this.ChartPoints[index]=this.ApplyChartRunningState(this.ChartPoints[index]);
        }
    }

    /// <summary>
    /// GetChartMetrics メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="row">row を指定して実行条件を制御します。</param>
    /// <param name="project">project を指定して実行条件を制御します。</param>
    /// <param name="comparisonRow">comparisonRow を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ProjectMetrics GetChartMetrics(
        TableRow row,
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
    private IReadOnlyList<BenchmarkComparisonRow> BuildElementComparisonRows(SourceTabViewState state) {
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
            .Where(result => this.IsResultSelectedForDisplay(result,sourceTab))
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
                    result.ArgumentKey);
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
        MetricKind metric) {
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
    private static double? GetMetricValue(BenchmarkResultItem? item,MetricKind metric) {
        if(item is null) {
            return null;
        }

        return metric==MetricKind.Time
            ? item.MeanNanoseconds
            : item.AllocatedBytesValue;
    }

    /// <summary>
    /// GetMetricValue メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="metrics">metrics を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static double? GetMetricValue(ProjectMetrics metrics,MetricKind metric) {
        if(!metrics.HasMeasuredValue) {
            return null;
        }

        return metric==MetricKind.Time
            ? metrics.TimeNanoseconds
            : metrics.AllocatedBytes;
    }

    /// <summary>
    /// FormatMetric メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="value">value を指定して実行条件を制御します。</param>
    /// <param name="metric">metric を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static string FormatMetric(double? value,MetricKind metric) {
        if(value is null) {
            return string.Empty;
        }

        return metric==MetricKind.Time
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : BenchmarkFormatting.FormatBytes((long)Math.Round(value.Value,MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// SetMetric メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="kind">kind を指定して実行条件を制御します。</param>
    private void SetMetric(MetricKind kind) {
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
        this.uiDispatcher.BeginInvoke(() => {
            this.LogText+=text;
            this.LogEntries.Add(text);
        });
    }

    // ログ一覧のDataTemplate側でこの接頭辞を検出し、太字/強調色で目立たせる(CreateLogView参照)。
    /// <summary>
    /// HighlightedLogEntryPrefix フィールドです。
    /// </summary>
    internal const string HighlightedLogEntryPrefix = "? ";

    /// <summary>
    /// AppendHighlightedLogEntry メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="message">message を指定して実行条件を制御します。</param>
    private void AppendHighlightedLogEntry(string message) {
        this.uiDispatcher.BeginInvoke(() => {
            this.SelectedLowerTab=LowerTab.Log;
            var text = HighlightedLogEntryPrefix+message;
            this.LogText+=text+Environment.NewLine;
            this.LogEntries.Add(text);
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
    private void RefreshBenchmarkSortColumns() =>
        this.sortController.RefreshBenchmarkSortColumns();

    /// <summary>
    /// RefreshBenchmarkSortColumnOptions メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshBenchmarkSortColumnOptions() =>
        this.sortController.RefreshBenchmarkSortColumnOptions();

    /// <summary>
    /// ApplyCurrentSort メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyCurrentSort() =>
        this.sortController.ApplyCurrentSort();

    /// <summary>
    /// ApplyFinalBenchmarkSort メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void ApplyFinalBenchmarkSort() =>
        this.sortController.ApplyFinalBenchmarkSort();

    /// <summary>
    /// 現 在 の 状 態 を 再 構 築 し 、 表 示 に 反 映 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void RebuildCpuSections() =>
        this.cpuTopologyController.RebuildCpuSections();

    /// <summary>
    /// GetCpuSections メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="kind">kind を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IEnumerable<CpuTableSection> GetCpuSections(CpuCoreKind kind) =>
        this.CpuSections.Where(section => section.CoreKind==kind);

    /// <summary>
    /// ApplyCpuSelection メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="cpus">cpus を指定して実行条件を制御します。</param>
    /// <param name="selected">selected を指定して実行条件を制御します。</param>
    private void ApplyCpuSelection(IEnumerable<CpuSelectionItem> cpus,bool selected) =>
        this.cpuTopologyController.ApplyCpuSelection(cpus,selected);

    /// <summary>
    /// EnsureAtLeastOneCpuSelected メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="fallback">fallback を指定して実行条件を制御します。</param>
    private void EnsureAtLeastOneCpuSelected(CpuSelectionItem? fallback) =>
        this.cpuTopologyController.EnsureAtLeastOneCpuSelected(fallback);

    /// <summary>
    /// GetSelectedLogicalCpus メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<int> GetSelectedLogicalCpus() =>
        this.cpuTopologyController.GetSelectedLogicalCpus();

    /// <summary>
    /// RefreshSelectedLogicalCpus メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void RefreshSelectedLogicalCpus() =>
        this.cpuTopologyController.RefreshSelectedLogicalCpus();

}


