using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Seamless.UI.Storage;

namespace UI;

public sealed partial class BenchmarkRunnerViewModel:ObservableObject {
    private const int NewRunNumber = 0;
    private const int UnsavedRunNumber = -1;
    private const int ExternalRunNumber = -2;
    private const double BenchmarkChartRowHeight = 24;
    private const double BenchmarkChartVerticalPadding = 82;
    private const double BenchmarkChartMinimumHeight = 120;
    private const string BenchmarkSortSettingsPreferenceKey = "Seamless.Benchmark.SortRules.v1";
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

    private readonly IBenchmarkCatalogService catalogService;
    private readonly ICpuTopologyService cpuTopologyService;
    private readonly IBenchmarkRunService runService;
    private readonly IBenchmarkComparisonService comparisonService;
    private readonly UiRunStore uiRunStore;
    private readonly BenchmarkUiRunRepository uiRunRepository;
    private readonly IBenchmarkUiDispatcher uiDispatcher;
    private readonly IBenchmarkUiPreferences uiPreferences;
    private readonly BenchmarkRunEventPipeline runEventPipeline;
    private readonly BenchmarkUiDiagnostics pipelineDiagnostics;
    private readonly BenchmarkUiRenderScheduler renderScheduler;
    private readonly BenchmarkPersistenceBatcher persistenceBatcher;
    private readonly BenchmarkRunProjectionService runProjectionService = new();
    private readonly string benchmarkRoot;
    private CancellationTokenSource? runCancellation;
    private string? activeRunRoot;
    private RunTerminationKind pendingRunTerminationKind;
    private string statusText = "Ready";
    private bool isBenchmarkRunning;
    private bool isRefreshing;
    private bool? isAllBenchmarkSelectionChecked;
    private bool hasSelectedBenchmarkCells;
    private BenchmarkTab selectedTab = BenchmarkTab.Cpu;
    private SourceTabOption selectedSourceTab;
    private IReadOnlyList<BenchmarkSourceTabViewState> sourceTabViewStates = [];
    private readonly Dictionary<BenchmarkSourceTab,BenchmarkSourceTabViewState> sourceTabViewStatesByTab = [];
    private ExecutionProfileOption selectedExecutionProfile;
    private ScaleOption? selectedScale;
    private ThresholdOption selectedThreshold;
    private BenchmarkRunOption? selectedRunOption;
    private string adaptiveThresholdSecondsText = "1";
    private MetricOption selectedMetric;
    private ChartModeOption selectedChartMode;
    private ResultElementCountOption? selectedResultElementCount;
    private double selectedResultElementCountIndex;
    private string currentElementCountText = "1";
    private string improvementPromptText = string.Empty;
    private string improvementCandidateSummaryText = string.Empty;
    private BenchmarkGeneratedCodeAuditReport? generatedCodeAuditReport;
    private bool isApplyingCpuSelection;
    private bool isApplyingUiState;
    private bool isApplyingBenchmarkSelection;
    private bool isApplyingResultElementCount;
    private bool isApplyingRunOptionSelection;
    private bool isApplyingSourceTabSelection;
    private bool isApplyingSortRules;
    private bool isLoadingSelectedRunDb;
    private int selectedRunLoadRequestToken;
    private bool isRefreshingBenchmarkSelectionDisplay;
    private bool hasPendingCloseStateSave;
    private bool hasUnsavedBenchmarkResults;
    private bool hasBenchmarkTypeColumn = true;
    private BenchmarkGridSortColumn currentSortColumn = BenchmarkGridSortColumn.BenchmarkMethod;
    private bool currentSortDescending;
    private readonly Dictionary<BenchmarkSourceTab,BenchmarkCellSelectionAnchor?> benchmarkSelectionAnchors = [];
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows{
        get => this.SelectedSourceTabViewState.ComparisonRows;
        set => this.SelectedSourceTabViewState.ComparisonRows=value;
    }
    private readonly BenchmarkRunSession currentRunSession = new();
    private readonly Dictionary<string,bool> benchmarkCellSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,bool> benchmarkRowSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,bool> improvementCandidateSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,BenchmarkCase> benchmarkCasesByProjectMethod = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,BenchmarkTableRow> benchmarkRowsByResultKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,BenchmarkTableRow> benchmarkRowsByProjectMethod = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,BenchmarkGeneratedCodeAuditReport> generatedCodeAuditCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,DateTimeOffset> runningVisualStartedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly BenchmarkSourceTabRunningState sourceTabRunningState = new();
    private IReadOnlyList<BenchmarkCase> catalogCases = [];
    private IReadOnlyList<BenchmarkSortRuleSnapshot> savedSortRuleSnapshots = [];
    private BenchmarkSourceTab? activeRunSourceTab;
    private IReadOnlyList<BenchmarkCase> currentRunSelectedBenchmarkCases = [];
    private int[] selectedLogicalCpus = [];
    private string logText = string.Empty;
    private BenchmarkLowerTab selectedLowerTab = BenchmarkLowerTab.Cpu;
    private BenchmarkTabProgress overallSourceTabProgress;
    private IDisposable? logSubscription;
    private readonly HashSet<string> reportedMetadataWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,ChartComparisonPoint> retainedChartPoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly object runStatusGate = new();
    private string activeChartMethodName = string.Empty;
    private string pendingRunStatusText = string.Empty;
    private int runResultGeneration;
    private int benchmarkAutoScrollSequence;
    private int isRunStatusUpdateScheduled;
    private string? currentUnsavedRunRoot;
    private ProgressState? loadedExternalState;
    private long? activeUiRunId;
    private SourceTabOption? pendingSourceTabOption;
    // 選択中の保存済み Run の ProgressState スナップショット。
    // タブ切替時に DB を再読み込みしないため、Run 選択の変更と実行終了時に破棄する。
    private (long UiRunId,ProgressState? State)? selectedRunStateCache;
    private long? loadedImprovementSelectionRunId;
    private bool hasLoadedImprovementCandidateSelections;
    private bool isApplyingImprovementCandidateSelection;

    private enum RunTerminationKind {
        None,
        Stop,
        Pause,
    }


    internal BenchmarkRunnerViewModel(
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
        this.ExecutionProfiles= CreateExecutionProfiles();
        this.selectedExecutionProfile=this.ExecutionProfiles.First(static item => item.Profile==BenchmarkExecutionProfile.Fast);
        this.ThresholdOptions=CreateThresholdOptions();
        this.selectedThreshold=this.ThresholdOptions[0];
        this.Metrics= [
            new MetricOption("Time",BenchmarkMetricKind.Time),
            new MetricOption("Allocated",BenchmarkMetricKind.Allocated),
        ];
        this.selectedMetric=this.Metrics[0];
        this.ChartModes= [
            new ChartModeOption("N comparison",BenchmarkChartMode.ElementComparison),
            new ChartModeOption("Scale trend",BenchmarkChartMode.ScaleTrend),
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
        this.RunCommand=new AsyncRelayCommand(this.RunOrResumeAsync,() => this.CanUseRunToolbarButton);
#if DEBUG
        this.RunInfiniteCommand=new AsyncRelayCommand(this.RunInfiniteAsync,() => this.CanUseRunToolbarButton);
#endif
        this.ToggleRunCommand=new RelayCommand(this.ToggleRun,() => !this.IsRefreshing);
        this.StopCommand=new AsyncRelayCommand(this.StopAsync,() => this.IsBenchmarkRunActive||this.IsBenchmarkPaused);
        this.PauseCommand=new AsyncRelayCommand(this.PauseAsync,() => this.IsBenchmarkRunActive);
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

                    this.EnsureAtLeastOneCpuSelected(cpu);
                    this.RefreshSelectedLogicalCpus();
                    this.RefreshCpuSectionStates();
                }
            };
        }
        this.RebuildCpuSections();
        this.RefreshSelectedLogicalCpus();
    }

    private static IReadOnlyList<ExecutionProfileOption> CreateExecutionProfiles() => [
            new ExecutionProfileOption("高精度",BenchmarkExecutionProfile.Accurate),
            new ExecutionProfileOption("バランス",BenchmarkExecutionProfile.Intermediate),
            new ExecutionProfileOption("Fast",BenchmarkExecutionProfile.Fast),
#if DEBUG
            new ExecutionProfileOption("Test",BenchmarkExecutionProfile.Test),
#endif
        ];

    private IReadOnlyList<BenchmarkColumnSelectionGroup> CreateBenchmarkColumnSelectionGroups() => [];

    private IReadOnlyList<BenchmarkColumnSelectionGroup> CreateBenchmarkProjectSelectionGroups() => [
        new BenchmarkColumnSelectionGroup("Left",this.SelectedSourceDefinition.LeftHeader,["Left"]),
        new BenchmarkColumnSelectionGroup("Right",this.SelectedSourceDefinition.RightHeader,["Right"]),
    ];

    private void RefreshBenchmarkProjectHeaderTexts() {
        foreach(var group in this.BenchmarkProjectSelectionGroups) {
            if(group.Id=="Left") {
                group.SetHeaderText(this.SelectedSourceDefinition.LeftHeader);
            }else if(group.Id=="Right") {
                group.SetHeaderText(this.SelectedSourceDefinition.RightHeader);
            }
        }
    }

    public IReadOnlyList<BenchmarkSourceTabViewState> SourceTabViewStates => this.sourceTabViewStates;
    public BenchmarkSourceTabViewState SelectedSourceTabViewState => this.GetSourceTabViewState(this.SelectedSourceDefinition.Tab);
    public ObservableCollection<BenchmarkTableRow> BenchmarkRows => this.SelectedSourceTabViewState.Rows;
    public ObservableCollection<BenchmarkResultItem> ResultItems { get; } = [];
    public ObservableCollection<BenchmarkResultItem> VisibleResultItems { get; } = [];
    public ObservableCollection<CpuSelectionItem> CpuItems { get; } = [];
    public ObservableCollection<CpuTableSection> CpuSections { get; } = [];
    public ObservableCollection<BenchmarkImprovementCandidateRow> ImprovementCandidates { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> LatencyHotspotRows => this.SelectedSourceTabViewState.LatencyHotspotRows;
    public ObservableCollection<BenchmarkDiagnosticRow> AllocationDiagnosticRows => this.SelectedSourceTabViewState.AllocationDiagnosticRows;
    public ObservableCollection<BenchmarkDiagnosticRow> CpuTimelineRows => this.SelectedSourceTabViewState.CpuTimelineRows;
    public ObservableCollection<BenchmarkDiagnosticRow> PipelineDiagnosticRows { get; } = [];
    public IEnumerable<CpuTableSection> PerformanceCpuSections => this.GetCpuSections(CpuCoreKind.Performance);
    public IEnumerable<CpuTableSection> EfficiencyCpuSections => this.GetCpuSections(CpuCoreKind.Efficiency);
    public IEnumerable<CpuTableSection> UnknownCpuSections => this.GetCpuSections(CpuCoreKind.Unknown);
    public ObservableCollection<BenchmarkSortRule> BenchmarkSortRules { get; } = [];
    public ObservableCollection<BenchmarkSortColumnOption> BenchmarkSortColumnOptions { get; } = [];
    public ObservableCollection<ChartComparisonPoint> ChartPoints => this.SelectedSourceTabViewState.ChartPoints;
    public ObservableCollection<ScaleOption> ScaleOptions { get; } = [];
    public ObservableCollection<ResultElementCountOption> ResultElementCountOptions { get; } = [];
    public ObservableCollection<BenchmarkRunOption> RunOptions { get; } = [];
    public ObservableCollection<string> LogEntries { get; } = [];
    public string LogText {
        get => this.logText;
        private set => this.SetProperty(ref this.logText,value);
    }
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkColumnSelectionGroups { get; }
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkProjectSelectionGroups { get; }
    public IReadOnlyList<SourceTabOption> SourceTabs { get; }
    public BenchmarkTabProgress OverallSourceTabProgress{
        get => this.overallSourceTabProgress;
        private set => this.SetProperty(ref this.overallSourceTabProgress,value);
    }
    public IReadOnlyList<ExecutionProfileOption> ExecutionProfiles { get; }
    public IReadOnlyList<MetricOption> Metrics { get; }
    public IReadOnlyList<ChartModeOption> ChartModes { get; }
    public IReadOnlyList<ThresholdOption> ThresholdOptions { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RunCommand { get; }
#if DEBUG
    public IAsyncRelayCommand RunInfiniteCommand { get; }
#endif
    public IRelayCommand ToggleRunCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand PauseCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand CopyImprovementPromptCommand { get; }
    public IRelayCommand ShowCpuCommand { get; }
    public IRelayCommand ShowChartCommand { get; }

    public string StatusText {
        get => this.statusText;
        set => this.SetProperty(ref this.statusText,value);
    }

    public BenchmarkGridSortColumn CurrentSortColumn {
        get => this.currentSortColumn;
        private set => this.SetProperty(ref this.currentSortColumn,value);
    }

    public bool CurrentSortDescending {
        get => this.currentSortDescending;
        private set => this.SetProperty(ref this.currentSortDescending,value);
    }

    public bool HasBenchmarkTypeColumn{
        get => this.hasBenchmarkTypeColumn;
        private set => this.SetProperty(ref this.hasBenchmarkTypeColumn,value);
    }

    public string BenchmarkSortSummaryText =>
        this.BenchmarkSortRules.Count==0
            ? "Default"
            : string.Join(" > ",this.BenchmarkSortRules.Select(static rule => rule.ColumnText+" "+rule.DirectionText));

    public string BenchmarkRatioSummaryText {
        get => this.SelectedSourceTabViewState.BenchmarkRatioSummaryText;
        private set {
            if(this.SelectedSourceTabViewState.BenchmarkRatioSummaryText!=value) {
                this.SelectedSourceTabViewState.BenchmarkRatioSummaryText=value;
                this.OnPropertyChanged();
            }
        }
    }

    public string ImprovementPromptText {
        get => this.improvementPromptText;
        private set {
            if(this.SetProperty(ref this.improvementPromptText,value)){
                this.OnPropertyChanged(nameof(this.CanCopyImprovementPrompt));
                this.CopyImprovementPromptCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ImprovementCandidateSummaryText {
        get => this.improvementCandidateSummaryText;
        private set => this.SetProperty(ref this.improvementCandidateSummaryText,value);
    }

    public BenchmarkGeneratedCodeAuditReport? GeneratedCodeAuditReport {
        get => this.generatedCodeAuditReport;
        private set => this.SetProperty(ref this.generatedCodeAuditReport,value);
    }

    private bool isGeneratedCodeAuditTabVisible;
    public bool IsGeneratedCodeAuditTabVisible {
        get => this.isGeneratedCodeAuditTabVisible;
        private set {
            if(this.SetProperty(ref this.isGeneratedCodeAuditTabVisible,value)&&value) {
                this.RefreshGeneratedCodeAuditIfNeeded();
            }
        }
    }

    public void SetGeneratedCodeAuditTabVisible(bool visible) => this.IsGeneratedCodeAuditTabVisible=visible;

    public bool CanCopyImprovementPrompt => !string.IsNullOrWhiteSpace(this.ImprovementPromptText);

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

    public bool IsRunning {
        get => this.IsBenchmarkRunning;
        private set => this.IsBenchmarkRunning=value;
    }

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

    public bool CanChangeBenchmarkConditions =>
        !this.IsBenchmarkRunning
        &&!this.IsRefreshing
        &&!this.IsBenchmarkPaused;

    public bool CanChangeBenchmarkSelection =>
        !this.IsRefreshing;

    public bool IsBenchmarkRunActive => this.runCancellation is not null&&this.IsBenchmarkRunning;

    public bool IsBenchmarkPaused => this.runCancellation is null&&this.activeRunRoot is not null;

    public string RunButtonText => this.IsBenchmarkPaused ? "Resume" : "Run";

    public bool CanResumeBenchmark => !this.IsRefreshing&&this.IsBenchmarkPaused;

    public bool CanRunBenchmark =>
        !this.IsRefreshing&&!this.IsBenchmarkRunning&&this.HasSelectedBenchmarkCells;

    public bool CanUseRunToolbarButton => this.CanRunBenchmark||this.CanResumeBenchmark;

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

    public string RunButtonTooltipText =>
        this.IsBenchmarkPaused||this.HasSelectedBenchmarkCells
            ? this.RunButtonText
            : "ベンチマークを1つ以上選択してください";

    public BenchmarkSourceTab? ActiveRunSourceTab {
        get => this.activeRunSourceTab;
        private set {
            if(this.SetProperty(ref this.activeRunSourceTab,value)) {
                this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
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

    public bool HasPendingCloseStateSave {
        get => this.hasPendingCloseStateSave;
        private set => this.SetProperty(ref this.hasPendingCloseStateSave,value);
    }

    public bool HasUnsavedBenchmarkResults {
        get => this.hasUnsavedBenchmarkResults;
        private set {
            if(this.SetProperty(ref this.hasUnsavedBenchmarkResults,value)) {
                this.RefreshToolbarCommandStates();
                this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            }
        }
    }

    internal BenchmarkToolbarStateInput ToolbarStateInput => new(
        this.IsRefreshing,
        this.IsBenchmarkRunActive,
        this.IsBenchmarkPaused,
        this.CanUseRunToolbarButton,
        this.IsBenchmarkRunActive||this.IsBenchmarkPaused,
        !this.IsRefreshing&&this.IsBenchmarkRunActive);

    internal BenchmarkToolbarButtonState GetToolbarButtonState(BenchmarkToolbarButtonRole role) =>
        BenchmarkToolbarStateLogic.CreateState(role,this.ToolbarStateInput);

    public bool? IsAllBenchmarkSelectionChecked {
        get => this.isAllBenchmarkSelectionChecked;
        private set => this.SetProperty(ref this.isAllBenchmarkSelectionChecked,value);
    }

    public async Task CancelAndWaitAsync() {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        this.Cancel();
        await this.WaitForRunStopAsync();
    }

    public async Task PauseAsync() {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        this.pendingRunTerminationKind=RunTerminationKind.Pause;
        this.Cancel();
        await this.WaitForRunStopAsync();
    }

    public async Task StopAsync() {
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
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
        this.StopCommand.NotifyCanExecuteChanged();
        this.PauseCommand.NotifyCanExecuteChanged();
        this.RefreshToolbarCommandStates();
    }

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

    public BenchmarkTab SelectedTab {
        get => this.selectedTab;
        set {
            if(this.SetProperty(ref this.selectedTab,value)) {
                this.OnPropertyChanged(nameof(this.IsCpuVisible));
                this.OnPropertyChanged(nameof(this.IsChartVisible));
            }
        }
    }

    public bool IsCpuVisible => this.SelectedTab==BenchmarkTab.Cpu;
    public bool IsChartVisible => this.SelectedTab==BenchmarkTab.Chart;

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

    public bool IsCpuLowerTabVisible => this.SelectedLowerTab==BenchmarkLowerTab.Cpu;
    public bool IsLogLowerTabVisible => this.SelectedLowerTab==BenchmarkLowerTab.Log;
    public bool IsImprovementPromptLowerTabVisible => this.SelectedLowerTab==BenchmarkLowerTab.ImprovementPrompt;

    public ComparisonPairOption SelectedComparisonPair => this.SelectedSourceDefinition.Pair;

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

    public BenchmarkSourceTabDefinition SelectedSourceDefinition => this.SelectedSourceTab.Definition;

    public BenchmarkAutoScrollRequest? LatestBenchmarkAutoScrollRequest { get; private set; }

    public string ChartLeftLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.LeftProject);
    public string ChartRightLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.RightProject);

    private async Task SelectSourceTabAsync(SourceTabOption option) {
        await Task.CompletedTask;
        this.CaptureCurrentBenchmarkSelections();
        this.ApplySourceTabSelection(option);
    }

    private void ApplySourceTabSelection(SourceTabOption option) {
        var previousTab=this.selectedSourceTab.Definition.Id;
        var nextTab=option.Definition.Id;
        var stopwatch=System.Diagnostics.Stopwatch.StartNew();
        var changed=false;
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

    private void ApplySourceTabVisualSelection() {
        foreach(var item in this.SourceTabs) {
            item.SetSelected(ReferenceEquals(item,this.selectedSourceTab));
        }
    }

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

    public ScaleOption? SelectedScale {
        get => this.selectedScale;
        set => this.SetProperty(ref this.selectedScale,value);
    }

    public ThresholdOption SelectedThreshold {
        get => this.selectedThreshold;
        set => this.SetProperty(ref this.selectedThreshold,value);
    }

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

    public string AdaptiveThresholdSecondsText {
        get => this.adaptiveThresholdSecondsText;
        set => this.SetProperty(ref this.adaptiveThresholdSecondsText,value);
    }

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

    public bool IsTimeMetricSelected {
        get => this.SelectedMetric.Kind==BenchmarkMetricKind.Time;
        set {
            if(value) {
                this.SetMetric(BenchmarkMetricKind.Time);
            }
        }
    }

    public bool IsAllocatedMetricSelected {
        get => this.SelectedMetric.Kind==BenchmarkMetricKind.Allocated;
        set {
            if(value) {
                this.SetMetric(BenchmarkMetricKind.Allocated);
            }
        }
    }

    public ChartModeOption SelectedChartMode {
        get => this.selectedChartMode;
        set {
            if(this.SetProperty(ref this.selectedChartMode,value)) {
                this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
                this.UpdateAllSourceTabCharts();
            }
        }
    }

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

    public double ChartElementCountSliderMaximum => Math.Max(0,this.ResultElementCountOptions.Count-1);

    public bool IsChartElementSliderVisible => this.ResultElementCountOptions.Count>1&&this.SelectedChartMode.Mode==BenchmarkChartMode.ElementComparison;

    public string SelectedChartElementCountText => this.SelectedResultElementCount?.DisplayName??"-";

    public string CurrentElementCountText {
        get => this.currentElementCountText;
        private set => this.SetProperty(ref this.currentElementCountText,value);
    }

    public double ChartHeight {
        get => this.SelectedSourceTabViewState.ChartHeight;
        private set {
            if(Math.Abs(this.SelectedSourceTabViewState.ChartHeight-value)>0.001) {
                this.SelectedSourceTabViewState.ChartHeight=value;
                this.OnPropertyChanged();
            }
        }
    }

    public BenchmarkSourceTabViewState GetSourceTabViewState(BenchmarkSourceTab sourceTab) =>
        this.sourceTabViewStatesByTab.TryGetValue(sourceTab,out var state)
            ? state
            : this.sourceTabViewStates[0];

    private IEnumerable<BenchmarkTableRow> GetAllBenchmarkRows() =>
        this.sourceTabViewStates.SelectMany(static state => state.Rows);

    public async Task InitializeAsync() {
        await this.RefreshAsync();
    }

    public void SortBenchmarkRows(BenchmarkGridSortColumn column) {
        var descending = false;
        if(this.CurrentSortColumn==column&&this.BenchmarkSortRules.Count==1) {
            descending=!this.CurrentSortDescending;
        }

        this.SetBenchmarkSortRules([new BenchmarkSortRule(column,descending)],true);
    }

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

    public void RemoveBenchmarkSortRule(BenchmarkSortRule rule) {
        this.BenchmarkSortRules.Remove(rule);
        if(this.BenchmarkSortRules.Count==0) {
            this.ResetBenchmarkSortRulesToDefault(true);
            return;
        }

        this.ApplySortRuleChanges(true);
    }

    public void MoveBenchmarkSortRule(BenchmarkSortRule source,BenchmarkSortRule target) {
        var oldIndex = this.BenchmarkSortRules.IndexOf(source);
        var newIndex = this.BenchmarkSortRules.IndexOf(target);
        if(oldIndex<0||newIndex<0||oldIndex==newIndex) {
            return;
        }

        this.BenchmarkSortRules.Move(oldIndex,newIndex);
        this.ApplySortRuleChanges(true);
    }

    public void MoveBenchmarkSortRuleUp(BenchmarkSortRule rule) {
        var index = this.BenchmarkSortRules.IndexOf(rule);
        if(index<=0) {
            return;
        }

        this.BenchmarkSortRules.Move(index,index-1);
        this.ApplySortRuleChanges(true);
    }

    public void MoveBenchmarkSortRuleDown(BenchmarkSortRule rule) {
        var index = this.BenchmarkSortRules.IndexOf(rule);
        if(index<0||index>=this.BenchmarkSortRules.Count-1) {
            return;
        }

        this.BenchmarkSortRules.Move(index,index+1);
        this.ApplySortRuleChanges(true);
    }

    public void SetBenchmarkSortRuleColumn(BenchmarkSortRule rule,BenchmarkGridSortColumn column) {
        rule.Column=column;
        this.ApplySortRuleChanges(true);
    }

    public void ToggleBenchmarkSortRuleDirection(BenchmarkSortRule rule) {
        rule.Descending=!rule.Descending;
        this.ApplySortRuleChanges(true);
    }

    public void ResetBenchmarkSortRules() =>
        this.ResetBenchmarkSortRulesToDefault(true);

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

    private void ApplySortRuleChanges(bool save) {
        this.RefreshCurrentSortIndicator();
        this.OnPropertyChanged(nameof(this.BenchmarkSortSummaryText));
        this.ApplyCurrentSort();
        if(save) {
            this.SaveBenchmarkSortRules();
        }
    }

    public void ToggleCpu(CpuSelectionItem cpu) {
        if(this.IsRefreshing) {
            return;
        }

        cpu.IsSelected=!cpu.IsSelected;
    }

    public void ToggleCpuSection(CpuTableSection section) {
        if(this.IsRefreshing) {
            return;
        }

        var selected = section.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(section.Cells.Select(static cell => cell.Cpu),selected);
    }

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

    public void ToggleCpuRow(CpuTableRow row) {
        if(this.IsRefreshing) {
            return;
        }

        var selected = row.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(row.Cells.Select(static cell => cell.Cpu),selected);
    }

    public void ToggleAllBenchmarkSelection() {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        this.SetBenchmarkCells(this.GetAllBenchmarkRows().SelectMany(static row => row.ProjectCells),this.IsAllBenchmarkSelectionChecked is false);
    }

    public void ToggleBenchmarkColumnGroup(BenchmarkColumnSelectionGroup group) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var selected = group.IsChecked is false;
        this.SetBenchmarkCells(this.GetBenchmarkCells(group.ColumnKeys),selected);
    }

    public void ToggleBenchmarkRow(BenchmarkTableRow row) {
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
        this.SetBenchmarkCells(targetCells,selected);
    }

    internal void SelectAllBenchmarkUiCells(BenchmarkSourceTabDefinition? sourceTab = null) {
        var state = this.GetSourceTabViewState((sourceTab??this.SelectedSourceDefinition).Tab);
        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.CornerHeader(),BenchmarkCellSelectionGesture.Replace);
    }

    internal void SelectBenchmarkCell(BenchmarkTableRow row,string project,BenchmarkCellSelectionGesture gesture) {
        var state = this.GetSourceTabViewState(row.SourceTab.Tab);
        var rowIndex = state.Rows.IndexOf(row);
        var columnIndex = GetProjectColumnIndex(state.Definition,project);
        if(rowIndex<0||columnIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.Cell(rowIndex,columnIndex),gesture);
    }

    internal void SelectBenchmarkRow(BenchmarkTableRow row,BenchmarkCellSelectionGesture gesture) {
        var state = this.GetSourceTabViewState(row.SourceTab.Tab);
        var rowIndex = state.Rows.IndexOf(row);
        if(rowIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(state,BenchmarkCellSelectionTarget.RowHeader(rowIndex),gesture);
    }

    internal void SelectBenchmarkColumnGroup(BenchmarkColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab = null) {
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
        var selected = targets.Length>1
            ? true
            : !cell.IsSelected;
        this.SetBenchmarkCells(targets,selected);
    }

    internal void SelectBenchmarkCellFromChart(BenchmarkSourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture) {
        var targetState = state??this.SelectedSourceTabViewState;
        var rowIndex = GetBenchmarkRowIndex(targetState,benchmark);
        var columnIndex = GetProjectColumnIndex(targetState.Definition,project);
        if(rowIndex<0||columnIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(targetState,BenchmarkCellSelectionTarget.Cell(rowIndex,columnIndex),gesture);
    }

    internal void SelectBenchmarkRowFromChart(BenchmarkSourceTabViewState? state,string benchmark,BenchmarkCellSelectionGesture gesture) {
        var targetState = state??this.SelectedSourceTabViewState;
        var rowIndex = GetBenchmarkRowIndex(targetState,benchmark);
        if(rowIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(targetState,BenchmarkCellSelectionTarget.RowHeader(rowIndex),gesture);
    }

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

    /// <summary>選択セル状態を保持したまま、読み込み済みカタログから表示中タブの行を再構築する。</summary>
    private void RebuildBenchmarkRowsFromCatalog() {
        this.benchmarkSelectionAnchors.Clear();
        var elementCount=this.SelectedResultElementCount?.Value??0;
        var builtRows=this.sourceTabViewStates
            .AsParallel()
            .AsOrdered()
            .WithDegreeOfParallelism(Math.Max(1,Math.Min(Environment.ProcessorCount,this.sourceTabViewStates.Count)))
            .Select(state => (State:state,Rows:this.CreateMethodRows(this.catalogCases,state.Definition,elementCount)))
            .ToArray();
        foreach(var item in builtRows) {
            var rows=item.Rows;
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
    /// タブ切替用の軽量パス。計測中は DB への書き込みはしても読み込みは避ける方針のため、
    /// カタログ再読み込みや Run 一覧の再読み込みを行わず、メモリ上のカタログと結果バッファから表示を再構築する。
    /// 保存済み Run の状態は <see cref="LoadSelectedRunState"/> のメモリキャッシュから適用される。
    /// </summary>
    private void RebuildRowsForSelectedTab() {
        if(this.catalogCases.Count==0) {
            _=this.RefreshAsync();
            return;
        }

        var totalStopwatch=System.Diagnostics.Stopwatch.StartNew();
        var phaseStopwatch=System.Diagnostics.Stopwatch.StartNew();
        this.RebuildBenchmarkRowsFromCatalog();
        var rebuildRowsMilliseconds=phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        this.RefreshBenchmarkSelectionAggregates();
        var aggregateMilliseconds=phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        var displaySource="memory";
        if(!this.RestoreCurrentRunDisplayForSelectedTab()) {
            if(this.ShouldLoadSelectedRunFromDatabaseForDisplay()) {
                displaySource="database";
                this.LoadSelectedRunFromDatabase();
            }else{
                displaySource="visible";
                this.RefreshVisibleResults();
                this.ApplySelectedElementCountToTable();
                this.RefreshImprovementPrompt();
            }
        }
        var displayMilliseconds=phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        this.RefreshSourceTabProgresses();
        var progressMilliseconds=phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();
        this.ApplyCurrentSort();
        var sortMilliseconds=phaseStopwatch.Elapsed;
        totalStopwatch.Stop();
        this.StatusText=this.BenchmarkRows.Count+" 件のベンチマーク";
        StartupDiagnostics.Write(
            $"Source tab rebuild {this.SelectedSourceDefinition.Id} rows={this.BenchmarkRows.Count} total={FormatSwitchMeasureMilliseconds(totalStopwatch.Elapsed)}ms rebuildRows={FormatSwitchMeasureMilliseconds(rebuildRowsMilliseconds)}ms aggregates={FormatSwitchMeasureMilliseconds(aggregateMilliseconds)}ms display={FormatSwitchMeasureMilliseconds(displayMilliseconds)}ms displaySource={displaySource} progress={FormatSwitchMeasureMilliseconds(progressMilliseconds)}ms sort={FormatSwitchMeasureMilliseconds(sortMilliseconds)}ms");
    }

    private static string FormatSwitchMeasureMilliseconds(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);

    private void ApplyPendingSourceTabSelection() {
        if(this.pendingSourceTabOption is { } pending) {
            this.pendingSourceTabOption=null;
            if(pending!=this.selectedSourceTab) {
                _=this.SelectSourceTabAsync(pending);
            }
        }
    }

    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases) =>
        this.CreateMethodRows(cases,this.SelectedSourceDefinition);

    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases,BenchmarkSourceTabDefinition sourceTab) {
        return this.CreateMethodRows(cases,sourceTab,this.SelectedResultElementCount?.Value??0);
    }

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

    private void RefreshBenchmarkCaseIndex(IReadOnlyList<BenchmarkCase> cases) {
        this.benchmarkCasesByProjectMethod.Clear();
        foreach(var benchmark in cases) {
            this.benchmarkCasesByProjectMethod[CreateBenchmarkCaseKey(benchmark.MethodName,benchmark.Project,benchmark.TypeFullName,benchmark.ArgumentKey)]=benchmark;
        }
    }

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

    private void AddBenchmarkRowIndex(BenchmarkTableRow row,string project,string typeFullName,string method,string argumentKey) {
        this.benchmarkRowsByResultKey.TryAdd(CreateBenchmarkRowResultIndexKey(row.SourceTab.Tab,project,typeFullName,method,argumentKey),row);
    }

    private void SetAllBenchmarkSelection(bool selected) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        this.SetBenchmarkCells(this.GetAllBenchmarkRows().SelectMany(static row => row.ProjectCells),selected);
    }

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
            };
        }
    }

    private IEnumerable<BenchmarkProjectCell> GetBenchmarkCells(IReadOnlyList<string> projectKeys) {
        foreach(var row in this.GetAllBenchmarkRows()) {
            foreach(var project in projectKeys) {
                if(this.TryGetBenchmarkCellBySelectionKey(row,project,out var cell)) {
                    yield return cell;
                }
            }
        }
    }

    private void SetBenchmarkCells(IEnumerable<BenchmarkProjectCell> cells,bool selected) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var targets = this.ExpandBenchmarkSelectionCells(cells);
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

    private BenchmarkProjectCell[] ExpandBenchmarkSelectionCells(IEnumerable<BenchmarkProjectCell> cells) {
        var requested = cells.Where(static cell => cell.HasCase).Distinct().ToArray();
        if(requested.Length==0) {
            return [];
        }

        var requestedSet = requested.ToHashSet();
        var syncKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in this.GetAllBenchmarkRows()) {
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
        foreach(var row in this.GetAllBenchmarkRows()) {
            if(row.LeftCell.HasCase&&syncKeys.Contains(CreateBenchmarkSelectionSyncKey(row.RowKey,"Left"))) {
                targets.Add(row.LeftCell);
            }
            if(row.RightCell.HasCase&&syncKeys.Contains(CreateBenchmarkSelectionSyncKey(row.RowKey,"Right"))) {
                targets.Add(row.RightCell);
            }
        }
        return targets.Distinct().ToArray();
    }

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

    private static string CreateBenchmarkSelectionSyncKey(string rowKey,string side) =>
        rowKey+"|"+side;

    private void ApplyBenchmarkCellSelection(BenchmarkSourceTabViewState state,BenchmarkCellSelectionTarget target,BenchmarkCellSelectionGesture gesture) {
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

    private static IEnumerable<BenchmarkCellCoordinate> GetSelectedBenchmarkUiCoordinates(BenchmarkSourceTabViewState state) {
        for(var rowIndex = 0; rowIndex<state.Rows.Count; rowIndex++) {
            var row = state.Rows[rowIndex];
            for(var columnIndex = 0; columnIndex<state.Definition.ProjectOrder.Count; columnIndex++) {
                if(row.GetProjectCell(state.Definition.ProjectOrder[columnIndex]) is { IsUiSelected: true }) {
                    yield return new BenchmarkCellCoordinate(rowIndex,columnIndex);
                }
            }
        }
    }

    private void RefreshBenchmarkSelectionAggregates() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.RefreshSelectionState();
        }
        foreach(var group in this.BenchmarkColumnSelectionGroups) {
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys));
        }
        foreach(var group in this.BenchmarkProjectSelectionGroups) {
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys));
        }
        var allCells = this.GetAllBenchmarkRows().SelectMany(static row => row.ProjectCells).ToArray();
        this.IsAllBenchmarkSelectionChecked=GetSelectionState(allCells);
        this.HasSelectedBenchmarkCells=this.HasAnySelectedBenchmarkCell();
        this.RefreshSourceTabProgresses();
    }

    // Run ボタンの活性はライブなチェック状態で判定する。進捗バッジ用の CreateProgressCells は
    // 実行時に凍結した選択スナップショットを優先するため、実行後に選択を変えると実態と乖離し、
    // 「ボタンは有効なのに RunAsync が選択0件で無言中断する」原因になる。
    private bool HasAnySelectedBenchmarkCell() =>
        this.SourceTabs.Any(option => this.HasAnyLiveSelectedBenchmarkCell(option.Definition));

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

    private static int GetBenchmarkRowIndex(BenchmarkSourceTabViewState state,string benchmark,string rowKey = "") {
        for(var index=0;index<state.Rows.Count;index++) {
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

    private static int GetProjectColumnIndex(BenchmarkSourceTabDefinition sourceTab,string project) =>
        sourceTab.ProjectOrder
            .Select((item,index) => new { item,index })
            .FirstOrDefault(item => string.Equals(item.item,ResolveProjectKey(sourceTab,project),StringComparison.OrdinalIgnoreCase))
            ?.index??-1;

    private static string ResolveProjectKey(BenchmarkSourceTabDefinition sourceTab,string project) =>
        project switch {
            "Left" => sourceTab.LeftProject,
            "Right" => sourceTab.RightProject,
            _ => project,
        };

    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadImprovementCandidatesChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapImprovementCandidates(this.uiRunStore.QueryAnalysisView("BenchmarkImprovementCandidates",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkImprovementCandidates load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadTimeRatioWorstChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapTimeRatioWorst(this.uiRunStore.QueryAnalysisView("BenchmarkTimeRatioWorst",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkTimeRatioWorst load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkRankedRatioPoint> LoadAllocationRatioWorstChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapAllocationRatioWorst(this.uiRunStore.QueryAnalysisView("BenchmarkAllocationRatioWorst",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkAllocationRatioWorst load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkCategoryRatioPoint> LoadCategorySummaryChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapCategorySummary(this.uiRunStore.QueryAnalysisView("BenchmarkCategorySummary",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkCategorySummary load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkTrendSeries> LoadHistoryTrendChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapHistoryTrend(this.uiRunStore.QueryAnalysisView("BenchmarkHistoryTrend",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkHistoryTrend load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkTrendSeries> LoadScaleTrendChartData() {
        try {
            return BenchmarkAnalysisViewMapper.MapScaleTrend(this.uiRunStore.QueryAnalysisView("BenchmarkScaleTrend",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkScaleTrend load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkBadgeItem> LoadMissingFailureMatrixBadges() {
        try {
            return BenchmarkAnalysisViewMapper.MapMissingFailureMatrix(this.uiRunStore.QueryAnalysisView("BenchmarkMissingFailureMatrix",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkMissingFailureMatrix load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkQualityGaugePoint> LoadMeasurementQualityGauges() {
        try {
            return BenchmarkAnalysisViewMapper.MapMeasurementQuality(this.uiRunStore.QueryAnalysisView("BenchmarkMeasurementQuality",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkMeasurementQuality load failed.",ex);
            return [];
        }
    }

    public IReadOnlyList<BenchmarkBadgeItem> LoadFallbackDiagnosticsBadges() {
        try {
            return BenchmarkAnalysisViewMapper.MapFallbackDiagnostics(this.uiRunStore.QueryAnalysisView("BenchmarkFallbackDiagnostics",100));
        } catch(Exception ex) {
            StartupDiagnostics.Write("BenchmarkFallbackDiagnostics load failed.",ex);
            return [];
        }
    }

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

    private async Task RefreshRunsForSelectedProfileAsync() {
        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,true);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run list refresh failed.",ex);
        }
    }

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

    private bool CanSelectRunOption(BenchmarkRunOption? option) =>
        !this.HasDisplayResults||IsNewRunOption(option);

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

    private void SetTransientRunOption(BenchmarkRunOption option) {
        this.RemoveTransientRunOptions();

        var insertIndex = this.RunOptions.Count>0 ? 1 : 0;
        this.RunOptions.Insert(Math.Min(insertIndex,this.RunOptions.Count),option);
        this.SetSelectedRunOption(option,false,true);
    }

    private void RemoveTransientRunOptions() {
        for(var index = this.RunOptions.Count-1; index>=0; index--) {
            if(this.RunOptions[index].RunNumber<0) {
                this.RunOptions.RemoveAt(index);
            }
        }
    }

    private void SelectNewRunOptionForFreshRun() {
        var newRunOption = this.RunOptions.FirstOrDefault(IsNewRunOption);
        if(newRunOption is not null) {
            this.SetSelectedRunOption(newRunOption,false,true);
        }
    }

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
            }else{
                this.LoadSelectedRunFromDatabase();
            }
        }
    }

    private void CaptureCurrentBenchmarkSelections() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            this.benchmarkRowSelections[CreateRowSelectionKey(row.SourceTab,row.RowKey)]=row.IsSelected;
            foreach(var cell in row.ProjectCells) {
                this.benchmarkCellSelections[CreateBenchmarkCellKey(row.SourceTab,row.RowKey,cell.Project)]=cell.IsSelected;
            }
        }
    }

    private void RefreshCurrentRunBenchmarkSelectionSnapshot() {
        Volatile.Write(
            ref this.currentRunSelectedBenchmarkCases,
            this.CreateSelectedBenchmarkCasesFromAllTabs());
    }

    private IReadOnlyList<BenchmarkCase> GetCurrentRunSelectedBenchmarkCases() =>
        Volatile.Read(ref this.currentRunSelectedBenchmarkCases);

    // 選択スナップショットとキュー生成の規則は UI.Core の BenchmarkSelectionQueueLogic に集約する。
    // 表示中タブだけの生きた状態(チェックボックス)を優先する。
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

    private static string CreateRowSelectionKey(BenchmarkSourceTabDefinition sourceTab,string rowKey) =>
        sourceTab.Id+"|"+rowKey;

    internal static string CreateBenchmarkCellSelectionKey(BenchmarkSourceTabDefinition sourceTab,string method,string project) =>
        BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,method,project);

    private static string CreateBenchmarkCellKey(BenchmarkSourceTabDefinition sourceTab,string method,string project) =>
        BenchmarkSelectionQueueLogic.CreateCellSelectionKey(sourceTab,method,project);

    public void SaveUiState() {
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
        if(!this.isApplyingUiState) {
            this.RefreshBenchmarkSelectionDisplay();
        }
    }

    public void DiscardPendingCloseStateSave() {
        this.HasPendingCloseStateSave=false;
    }

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

        this.comparisonRows=[];
    }

    private void RefreshAllComparisonRowsFromDisplayResults() {
        var selectedElementCount = this.SelectedResultElementCount?.Value;
        foreach(var state in this.sourceTabViewStates) {
            if(this.GetCurrentRunResultItems(state.Definition.Tab).Count>0) {
                state.ComparisonRows=selectedElementCount is { } currentElementCount
                    ? this.BuildCurrentRunComparisonRowsForElementCount(state.Definition,currentElementCount)
                    : this.BuildCurrentRunComparisonRows(state.Definition);
            }else if(this.ResultItems.Count>0) {
                state.ComparisonRows=selectedElementCount is { } displayedElementCount
                    ? this.BuildDisplayedRowsForElementCount(state.Definition,displayedElementCount)
                    : this.BuildDisplayedRows(state.Definition);
            }else{
                state.ComparisonRows=[];
            }
        }
    }

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

    public void DiscardUnsavedBenchmarkResults() {
        // 結果は QueueSaveResult で SQLite へ保存済みのため、未保存表示を解除する。
        // 実行していない状態で終了確認が出ないようにする。
        this.HasUnsavedBenchmarkResults=false;
        this.HasPendingCloseStateSave=false;
        this.currentUnsavedRunRoot=null;
        this.RefreshToolbarCommandStates();
    }

    private ProgressState? GetProgressStateForCurrentUnsavedRun() =>
        string.IsNullOrWhiteSpace(this.currentUnsavedRunRoot)
            ? null
            : this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot);

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

    private ProgressState CreateProgressStateFromDisplayedResults(string runRoot) {
        var sourceTab = this.SelectedSourceDefinition;
        var results = this.GetResultSourceForDisplay()
            .Where(result => sourceTab.ContainsProject(result.Project));
        return this.CreateProgressStateFromResults(runRoot,results);
    }

    private ProgressState CreateProgressStateFromAllTabResults(string runRoot) {
        var results = this.currentRunSession.HasAnyResults
            ? this.currentRunSession.GetAllResults()
            : this.GetResultSourceForDisplay();
        return this.CreateProgressStateFromResults(runRoot,results);
    }

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

    private static string CreateBenchmarkCaseKey(string method,string project,string typeFullName,string argumentKey = "") =>
        BenchmarkRowGroupingLogic.CreateCaseKey(project,typeFullName,method,argumentKey);

    private static string ShortTypeName(string typeFullName) {
        if(string.IsNullOrWhiteSpace(typeFullName)) {
            return string.Empty;
        }

        var index = typeFullName.LastIndexOf('.');
        return index<0||index==typeFullName.Length-1
            ? typeFullName
            : typeFullName[(index+1)..];
    }

    private BenchmarkTableRow? FindBenchmarkRow(BenchmarkResultItem result) {
        if(this.TryFindBenchmarkRow(result.SourceTab,result.Project,result.TypeFullName,result.Method,result.ArgumentKey,out var row)) {
            return row;
        }

        return this.BenchmarkRows.FirstOrDefault(row => IsResultForRow(row,result))
            ??(IsScalableSourceTab(result.SourceTab)||result.SourceTab!=this.SelectedSourceDefinition.Tab ? null : this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
                &&row.GetProjectCell(result.Project) is { HasCase: true }));
    }

    private BenchmarkTableRow? FindBenchmarkRow(BenchmarkJob job) {
        if(this.TryFindBenchmarkRow(job.SourceTab,job.Project,job.TypeFullName,job.MethodName,job.ArgumentKey,out var row)) {
            return row;
        }

        return this.BenchmarkRows.FirstOrDefault(row => IsJobForRow(row,job))
            ??(IsScalableSourceTab(job.SourceTab)||job.SourceTab!=this.SelectedSourceDefinition.Tab ? null : this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
                &&row.GetProjectCell(job.Project) is { HasCase: true }));
    }

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

    private static string CreateBenchmarkRowResultIndexKey(BenchmarkSourceTab sourceTab,string project,string typeFullName,string method,string argumentKey) =>
        string.Join("|",sourceTab,project,typeFullName,method,argumentKey);

    private static string CreateBenchmarkRowMethodProjectIndexKey(BenchmarkSourceTab sourceTab,string project,string method) =>
        string.Join("|",sourceTab,project,method);

    private static bool IsResultForRow(BenchmarkTableRow row,BenchmarkResultItem result) {
        if(row.CasesByProject.TryGetValue(result.Project,out var benchmark)) {
            return string.Equals(benchmark.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
                &&IsMatchingTypeForRow(row,result.TypeFullName,benchmark.TypeFullName)
                &&IsMatchingArgumentForRow(row.SourceTab.Tab,benchmark.ArgumentKey,result.ArgumentKey);
        }

        return false;
    }

    private static bool IsJobForRow(BenchmarkTableRow row,BenchmarkJob job) {
        if(row.CasesByProject.TryGetValue(job.Project,out var benchmark)) {
            return string.Equals(benchmark.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
                &&IsMatchingTypeForRow(row,job.TypeFullName,benchmark.TypeFullName)
                &&IsMatchingArgumentForRow(row.SourceTab.Tab,benchmark.ArgumentKey,job.ArgumentKey);
        }

        return false;
    }

    private static bool IsMatchingTypeForRow(BenchmarkTableRow row,string actualTypeFullName,string expectedTypeFullName) {
        if(IsScalableSourceTab(row.SourceTab.Tab)) {
            return !string.IsNullOrWhiteSpace(actualTypeFullName)
                &&string.Equals(expectedTypeFullName,actualTypeFullName,StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(actualTypeFullName)
            ||string.Equals(expectedTypeFullName,actualTypeFullName,StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMatchingArgumentForRow(BenchmarkSourceTab sourceTab,string expectedArgumentKey,string actualArgumentKey) =>
        !IsScalableSourceTab(sourceTab)
        ||string.Equals(expectedArgumentKey,actualArgumentKey,StringComparison.OrdinalIgnoreCase);

    private static bool IsScalableSourceTab(BenchmarkSourceTab sourceTab) =>
        sourceTab is BenchmarkSourceTab.ScalableSet
            or BenchmarkSourceTab.ScalableDictionary
            or BenchmarkSourceTab.ScalablePersistedList
            or BenchmarkSourceTab.ScalablePersistedSet
            or BenchmarkSourceTab.ScalableTsavoriteSet
            or BenchmarkSourceTab.ScalablePersistedDictionary
            or BenchmarkSourceTab.ScalableTsavoriteDictionary;

    private string CreateUnsavedRunRoot(BenchmarkExecutionProfile executionProfile) {
        var profileRoot = this.GetBenchmarkRoot(executionProfile);
        var root = Path.Combine(profileRoot,".unsaved",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

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
        }else{
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
            uiRunId=runRootOverride is not null&&this.activeUiRunId is not null
                ? this.activeUiRunId
                : await this.CreateUiRunAsync(runSourceTab,executionProfile).WaitAsync(runCancellationSource.Token);
            this.activeUiRunId=uiRunId;
            await this.SetUiRunStatusAsync(uiRunId,UiRunStatus.Running,"Benchmark run started.").WaitAsync(runCancellationSource.Token);
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
                true,
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
            StartupDiagnostics.Write(
                "Benchmark run completed. "
                +"RunRoot="+report.RunRoot);
        }
        catch(OperationCanceledException) {
            if(this.pendingRunTerminationKind==RunTerminationKind.Pause) {
                this.StatusText="一時停止しました";
                this.QueueUiRunStatus(uiRunId,UiRunStatus.Paused,"Benchmark run paused.");
                StartupDiagnostics.Write("Benchmark run paused.");
                this.SetActiveRunRoot(completedRunRoot ?? runRoot);
                keepPausedRunRoot=true;
            }else{
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
                }else{
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
            this.OnPropertyChanged(nameof(this.ToolbarStateInput));
            this.StopCommand.NotifyCanExecuteChanged();
            this.PauseCommand.NotifyCanExecuteChanged();
            this.RefreshToolbarCommandStates();
            if(isCurrentRun) {
                if(completedNormally&&this.TryGetRemainingRunningVisualDelay(out var finalVisualDelay)) {
                    this.ScheduleFinalLiveVisualCleanup(runGeneration,finalVisualDelay);
                }else{
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

    private async Task<long?> CreateUiRunAsync(BenchmarkSourceTab runSourceTab,BenchmarkExecutionProfile executionProfile) {
        var executionProfileText = executionProfile.ToString();
        var displayName = BenchmarkAppProfile.Title+" "+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
        return await this.uiRunRepository.CreateRunAsync(GetUiRunAppKind(),displayName,executionProfileText,runSourceTab);
    }

    private async Task SetUiRunStatusAsync(long? runId,UiRunStatus status,string message) {
        await this.uiRunRepository.SetRunStatusAsync(runId,status,message);
    }

    private void QueueUiRunStatus(long? runId,UiRunStatus status,string message) {
        this.uiRunRepository.QueueRunStatus(runId,status,message);
    }

    private BenchmarkViewStateSnapshot CreateBenchmarkViewStateSnapshot() {
        var state = this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot??string.Empty);
        return new(
            JsonSerializer.Serialize(state),
            this.SelectedSourceDefinition.Tab.ToString(),
            this.SelectedResultElementCount?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
    }

    private void QueueBenchmarkResultSave(BenchmarkResultItem result,int runGeneration,long? runId) {
        this.persistenceBatcher.Enqueue(new BenchmarkPersistenceItem(result,runGeneration,runId));
    }

    private void QueueBenchmarkResultsSave(IReadOnlyList<BenchmarkPersistenceItem> items) {
        if(items.Count==0) {
            this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
            return;
        }

        var snapshot = this.CreateBenchmarkViewStateSnapshot();
        foreach(var item in items) {
            if(item.RunGeneration!=this.runResultGeneration) {
                continue;
            }

            var rowKey = this.CreateResultRowKey(item.Result);
            this.uiRunRepository.QueueSaveResult(item.Result,item.UiRunId,rowKey,snapshot);
        }
        this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
    }

    private static UiRunAppKind GetUiRunAppKind() {
#if SCALABLE_BENCHMARK
        return UiRunAppKind.ScalableBenchmark;
#else
        return UiRunAppKind.Benchmark;
#endif
    }

    private void Cancel() {
        this.runCancellation?.Cancel();
    }

    private void AcceptRunResult(BenchmarkResultItem result,int runGeneration,long? uiRunId) {
        if(runGeneration!=this.runResultGeneration) {
            return;
        }
        if(IsRunningResult(result)&&!this.IsBenchmarkRunActive) {
            return;
        }

        this.pipelineDiagnostics.RecordReceivedEvent(result.Status);
        this.runEventPipeline.Post(new BenchmarkRunEvent(result,runGeneration,uiRunId,DateTimeOffset.UtcNow));
        this.renderScheduler.RequestRender();
    }

    private void AcceptRunStatus(string status) {
        lock(this.runStatusGate) {
            this.pendingRunStatusText=status;
        }
        this.RequestRunStatusUpdate();
    }

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

    private void ApplyRunUiSnapshot(BenchmarkUiSnapshot snapshot) {
        if(!snapshot.HasUpdates) {
            this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
            return;
        }

        foreach(var item in snapshot.RunningUpdates) {
            this.ApplyRunningSnapshotResult(item);
        }
        foreach(var item in snapshot.TerminalResults) {
            this.ApplyTerminalSnapshotResult(item);
        }
        if(snapshot.RefreshProgress) {
            this.RefreshSourceTabProgressesForRunningResult();
        }
        if(snapshot.RefreshChart) {
            if(this.IsBenchmarkRunActive) {
                this.RefreshFinalCompletionStates();
            }else{
                this.RefreshResultChecks(this.SelectedSourceDefinition.Tab);
                this.RefreshVisibleResults();
                this.ApplySelectedElementCountToTable();
                this.UpdateChart();
                this.RefreshFinalCompletionStates();
            }
        }
        this.SyncDiagnosticRows(this.PipelineDiagnosticRows,this.pipelineDiagnostics.CreateRows());
    }

    private void ApplyRunningSnapshotResult(BenchmarkRunEvent item) {
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

    private void ApplyTerminalSnapshotResult(BenchmarkRunEvent item) {
        if(item.RunGeneration!=this.runResultGeneration) {
            return;
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

        var shouldDelayTerminalVisual = this.TryGetTerminalVisualDelay(result,out var terminalVisualDelay);
        var cpu = this.FindAssignedCpu(result);
        if(!shouldDelayTerminalVisual) {
            this.runningVisualStartedAt.Remove(CreateRunningVisualKey(result));
            cpu?.ApplyJob(result);
            this.ApplySourceTabRunningResult(result);
        }
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            if(result.SourceTab==this.SelectedSourceDefinition.Tab) {
                this.CurrentElementCountText=FormatElementCountText(result.AxisCount);
                this.UpsertResultItem(result);
                this.RegisterResultElementCount(result.AxisCount);
            }
            if(!shouldDelayTerminalVisual) {
                this.FindBenchmarkRow(result)?.ApplyRunResult(result,cpu,DateTimeOffset.Now);
                this.UpdateChartCore(this.GetSourceTabViewState(result.SourceTab),false);
            }
        }
        this.HasPendingCloseStateSave=true;
        this.HasUnsavedBenchmarkResults=true;
        if(shouldDelayTerminalVisual) {
            this.ScheduleTerminalVisualResult(result,item.RunGeneration,terminalVisualDelay);
        }
        this.QueueBenchmarkResultSave(result,item.RunGeneration,item.UiRunId);
    }

    private void ClearCpuJobs() {
        foreach(var cpu in this.CpuItems) {
            cpu.ClearJob();
        }
    }

    private void ToggleRun() {
        if(this.IsBenchmarkPaused && this.activeRunRoot is not null) {
            _=this.RunAsync(this.activeRunRoot);
            return;
        }

        _=this.RunAsync(null);
    }

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
        if(isRunningResult||isTerminalResult&&!shouldDelayTerminalVisual){
            cpu?.ApplyJob(result);
            this.ApplySourceTabRunningResult(result);
        }
        if(shouldDelayTerminalVisual){
            this.ScheduleTerminalVisualResult(result,runGeneration,terminalVisualDelay);
        }
        if(isRunningResult) {
            this.MarkActiveRunSourceTab(resultSourceTab);
        }

        if(isFailedResult){
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

    private void MarkRunningVisualStarted(BenchmarkResultItem result) {
        var key = CreateRunningVisualKey(result);
        if(!this.runningVisualStartedAt.ContainsKey(key)) {
            this.runningVisualStartedAt[key]=DateTimeOffset.UtcNow;
        }
    }

    private bool TryGetTerminalVisualDelay(BenchmarkResultItem result,out TimeSpan delay) {
        delay=TimeSpan.Zero;
        if(!this.runningVisualStartedAt.TryGetValue(CreateRunningVisualKey(result),out var startedAt)) {
            return false;
        }

        var elapsed = DateTimeOffset.UtcNow-startedAt;
        if(elapsed>=RunningVisualMinimumDuration) {
            return false;
        }

        delay=RunningVisualMinimumDuration-elapsed;
        return true;
    }

    private bool TryGetRemainingRunningVisualDelay(out TimeSpan delay) {
        delay=TimeSpan.Zero;
        if(this.runningVisualStartedAt.Count==0) {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        foreach(var startedAt in this.runningVisualStartedAt.Values) {
            var remaining = RunningVisualMinimumDuration-(now-startedAt);
            if(remaining>delay) {
                delay=remaining;
            }
        }

        return delay>TimeSpan.Zero;
    }

    private void ScheduleTerminalVisualResult(BenchmarkResultItem result,int runGeneration,TimeSpan delay) {
        _=Task.Run(async () => {
            await Task.Delay(delay);
            this.uiDispatcher.BeginInvoke(() => {
                if(runGeneration!=this.runResultGeneration) {
                    return;
                }

                this.ApplyTerminalVisualResult(result);
            });
        });
    }

    private void ApplyTerminalVisualResult(BenchmarkResultItem result) {
        this.runningVisualStartedAt.Remove(CreateRunningVisualKey(result));
        this.FindAssignedCpu(result)?.ApplyJob(result);
        this.ApplySourceTabRunningResult(result);
        this.FindBenchmarkRow(result)?.ApplyRunResult(result,this.FindAssignedCpu(result),DateTimeOffset.Now,false);
        this.UpdateChartCore(this.GetSourceTabViewState(result.SourceTab),false);
    }

    private void ScheduleFinalLiveVisualCleanup(int runGeneration,TimeSpan delay) {
        _=Task.Run(async () => {
            await Task.Delay(delay);
            this.uiDispatcher.BeginInvoke(() => {
                if(runGeneration!=this.runResultGeneration) {
                    return;
                }

                this.ApplyFinalLiveVisualCleanup();
            });
        });
    }

    private void ApplyFinalLiveVisualCleanup(bool discardPendingRunUpdates = false) {
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
    }

    private void ClearBenchmarkLiveStates() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.ClearLiveState();
        }
    }

    private static string CreateRunningVisualKey(BenchmarkResultItem result) =>
        result.SourceTab.ToString()
        +"|"+result.Project
        +"|"+result.TypeFullName
        +"|"+result.Method
        +"|"+result.ArgumentKey
        +"|"+result.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsRunningResult(BenchmarkResultItem result) =>
        string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);

    private void MarkActiveRunSourceTab(BenchmarkSourceTab sourceTab) {
        this.ActiveRunSourceTab=sourceTab;
    }

    private void ApplySourceTabRunningResult(BenchmarkResultItem result){
        if(this.sourceTabRunningState.Apply(result)){
            this.RefreshSourceTabRunningStates();
        }
    }

    private void ClearSourceTabRunningStates(){
        if(this.sourceTabRunningState.Clear()){
            this.RefreshSourceTabRunningStates();
            return;
        }

        this.RefreshSourceTabRunningStates();
    }

    private void RefreshSourceTabRunningStates(){
        foreach(var option in this.SourceTabs){
            option.SetRunning(this.sourceTabRunningState.IsRunning(option.Tab));
        }
    }

    private void RequestBenchmarkAutoScroll(BenchmarkSourceTab sourceTab,string methodName,string rowKey) {
        this.LatestBenchmarkAutoScrollRequest=new BenchmarkAutoScrollRequest(
            sourceTab,
            methodName,
            rowKey,
            unchecked(++this.benchmarkAutoScrollSequence));
        this.OnPropertyChanged(nameof(this.LatestBenchmarkAutoScrollRequest));
    }

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

    private CpuSelectionItem? FindAssignedCpu(BenchmarkResultItem result) {
        var logicalCpu = BenchmarkResultCpuDisplayLogic.ResolveAssignedLogicalCpu(
            result.AssignedLogicalCpu,
            this.CpuItems.Select(static item => item.LogicalProcessorIndex));
        return logicalCpu is { } value
            ? this.CpuItems.FirstOrDefault(item => item.LogicalProcessorIndex==value)
            : null;
    }

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

    private void UpsertResultItem(BenchmarkResultItem result) {
        for(var index = this.ResultItems.Count-1; index>=0; index--) {
            var item = this.ResultItems[index];
            if(IsResultMatch(item,result)) {
                this.ResultItems.RemoveAt(index);
            }
        }
        this.ResultItems.Add(result);
    }

    private void UpsertCurrentRunResultItem(BenchmarkResultItem result) {
        this.currentRunSession.UpsertResult(result);
    }

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

    private string CreateResultRowKey(BenchmarkResultItem result) =>
        this.FindBenchmarkRow(result)?.RowKey??result.Method;

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

    private IReadOnlyList<int> GetSelectedElementCounts() {
        return BenchmarkElementCountLogic
            .BuildAdaptiveCandidates(BenchmarkElementCounts.All,BenchmarkElementCounts.All[^1])
            .ToArray();
    }

    private string GetBenchmarkRoot(BenchmarkExecutionProfile profile) =>
        Path.Combine(this.benchmarkRoot,BenchmarkExecutionProfileNames.ToFolderName(profile));

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

    private sealed class ThresholdOptionSettings {
        public static readonly IReadOnlyList<int> Values = [1,2,4,8,16,32,64,128,256,512,1024,2048,4096,8192];
        public int MillisecondsScale { get; set; } = 1;
        public string Unit { get; set; } = "ms";
    }

    private TimeSpan GetAdaptiveThreshold() {
        return TimeSpan.FromMilliseconds(this.SelectedThreshold.Milliseconds);
    }

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

    private static string FormatElementCountText(int elementCount) {
        return elementCount>0
            ? elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "-";
    }

    private string FormatResultElementCountDisplayName(int elementCount) =>
        BenchmarkWorkloadAxis.UsesLoopCount(this.SelectedSourceDefinition.Tab)
            ? "Loop "+elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : elementCount==BenchmarkElementCounts.BaseElementCount
                ? "N"
                : elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)+"N";

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

    private void RefreshVisibleResults() {
        this.VisibleResultItems.Clear();
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var item in this.runProjectionService.GetVisibleResults(this.ResultItems.Where(this.IsResultSelectedForDisplay),selectedN)) {
            this.VisibleResultItems.Add(item);
        }
    }

    private bool HasCurrentRunResultsForSelectedTab =>
        this.GetCurrentRunResultItemsForSelectedTab().Count>0;

    private bool HasDisplayResults =>
        this.ResultItems.Count>0
        ||this.HasCurrentRunResultsForSelectedTab
        ||this.currentRunSession.HasAnyResults;

    private void RefreshToolbarCommandStates() {
        this.OnPropertyChanged(nameof(this.ToolbarStateInput));
    }

    private IReadOnlyList<BenchmarkResultItem> GetCurrentRunResultItemsForSelectedTab() =>
        this.GetCurrentRunResultItems(this.SelectedSourceDefinition.Tab);

    private IReadOnlyList<BenchmarkResultItem> GetCurrentRunResultItems(BenchmarkSourceTab sourceTab) =>
        this.currentRunSession.GetResults(sourceTab);

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

    private void ApplyCurrentRunResultsToTable(IReadOnlyList<BenchmarkResultItem> results) {
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var result in results.Where(result => selectedN is null||result.AxisCount==selectedN)) {
            var row = this.FindBenchmarkRow(result);
            row?.ApplyRunResult(result,this.FindAssignedCpu(result),result.StartedAt??DateTimeOffset.Now,false);
        }
    }

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

    private void ClearAllSourceTabComparisonDisplays() {
        foreach(var state in this.sourceTabViewStates) {
            state.ComparisonRows=[];
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

    private void ClearSelectedSourceLiveResults() {
        this.ClearRetainedChartPoints();
        this.currentRunSession.ClearSourceTab(this.SelectedSourceDefinition.Tab);
        this.comparisonRows=[];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.BenchmarkRatioSummaryText=string.Empty;
        this.RefreshImprovementPrompt();
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

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

    private void LoadComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows,int elementCount = 0) {
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.AddComparisonRowsIntoResults(rows,elementCount,this.LoadSelectedRunState());
    }

    private void AddComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows,int elementCount,ProgressState? selectedRunState) {
        foreach(var row in rows) {
            this.AddResult(row.MethodName,row.TypeFullName,row.ArgumentKey,row.Arguments,row.SourceTab.LeftProject,row.Left,elementCount,selectedRunState);
            this.AddResult(row.MethodName,row.TypeFullName,row.ArgumentKey,row.Arguments,row.SourceTab.RightProject,row.Right,elementCount,selectedRunState);
        }
    }

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

    private void ApplyComparisonRowsToTable() {
        this.ApplyComparisonRowsToTable(this.SelectedSourceTabViewState);
    }

    private void ApplyComparisonRowsToAllTables() {
        foreach(var state in this.sourceTabViewStates) {
            this.ApplyComparisonRowsToTable(state);
        }
    }

    private void ApplyComparisonRowsToTable(BenchmarkSourceTabViewState state) {
        var hasCurrentRunResults = this.GetCurrentRunResultItems(state.Definition.Tab).Count>0;
        foreach(var benchmark in state.Rows) {
            benchmark.ApplyComparison(this.FindComparisonRow(state,benchmark),this.IsBenchmarkRunActive||hasCurrentRunResults);
        }
        this.RefreshBenchmarkRatioSummary(state);
    }

    private void ClearUnselectedResultDisplays() {
        foreach(var benchmark in this.BenchmarkRows) {
            benchmark.ClearUnselectedResultDisplays();
        }
    }

    private void ApplySelectedDisplayResultsToTable() {
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var result in this.GetResultSourceForDisplay().Where(result => selectedN is null||result.AxisCount==selectedN)) {
            var row = this.FindBenchmarkRow(result);
            row?.ApplyRunResult(result,this.FindAssignedCpu(result),result.StartedAt??DateTimeOffset.Now,false);
        }
    }

    private BenchmarkComparisonRow? FindComparisonRow(BenchmarkTableRow row) {
        return this.FindComparisonRow(this.SelectedSourceTabViewState,row);
    }

    private BenchmarkComparisonRow? FindComparisonRow(BenchmarkSourceTabViewState state,BenchmarkTableRow row) {
        foreach(var item in state.ComparisonRows) {
            if(IsComparisonRowMatch(row,item)) {
                return item;
            }
        }

        return null;
    }

    private void RefreshBenchmarkRatioSummary() {
        this.RefreshBenchmarkRatioSummary(this.SelectedSourceTabViewState);
    }

    private void RefreshBenchmarkRatioSummary(BenchmarkSourceTabViewState state) {
        state.BenchmarkRatioSummaryText=BenchmarkImprovementPromptBuilder.CreateSummary(state.ComparisonRows).DisplayText;
        if(state==this.SelectedSourceTabViewState) {
            this.OnPropertyChanged(nameof(this.BenchmarkRatioSummaryText));
        }
    }

    private void RefreshImprovementPrompt() {
        var state = this.GetProgressStateForImprovementPrompt();
        var candidates = state is null
            ? []
            : BenchmarkImprovementCandidateBuilder.BuildCandidates(state);
        this.ApplyImprovementCandidates(candidates);
        this.RefreshImprovementPromptText();
    }

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
        this.ImprovementCandidateSummaryText=this.CreateImprovementCandidateSummary();
    }

    private void OnImprovementCandidateSelectionChanged(object? sender,EventArgs args) {
        if(sender is not BenchmarkImprovementCandidateRow row) {
            return;
        }

        this.improvementCandidateSelections[row.Key]=row.IsSelected;
        if(!this.isApplyingImprovementCandidateSelection) {
            this.SaveImprovementCandidateSelection(row);
            this.RefreshImprovementPromptText();
            this.ImprovementCandidateSummaryText=this.CreateImprovementCandidateSummary();
        }
    }

    private void RefreshImprovementPromptText() {
        this.ImprovementPromptText=BenchmarkImprovementPromptBuilder.BuildPrompt(
            this.ImprovementCandidates
                .Where(static row => row.IsSelected)
                .Select(static row => row.Candidate));
    }

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

    private void SaveImprovementCandidateSelection(BenchmarkImprovementCandidateRow row) {
        if(this.GetCurrentImprovementSelectionRunId() is not { } runId) {
            return;
        }

        try {
            if(!this.hasLoadedImprovementCandidateSelections) {
                foreach(var item in this.ImprovementCandidates) {
                    this.uiRunStore.SaveUiSelection(runId,"ImprovementCandidate",item.Key,item.IsSelected);
                }
            }else{
                this.uiRunStore.SaveUiSelection(runId,"ImprovementCandidate",row.Key,row.IsSelected);
            }
            this.hasLoadedImprovementCandidateSelections=true;
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark improvement candidate selection save failed.",ex);
        }
    }

    private long? GetCurrentImprovementSelectionRunId() {
        if(this.activeUiRunId is { } active) {
            return active;
        }

        var selectedRun = this.SelectedRunOption;
        return selectedRun is not null&&!IsNewRunOption(selectedRun)&&!IsTransientRunOption(selectedRun)&&selectedRun.UiRunId>0
            ? selectedRun.UiRunId
            : null;
    }

    private async Task CopyImprovementPromptAsync() {
        if(string.IsNullOrWhiteSpace(this.ImprovementPromptText)) {
            return;
        }

        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(this.ImprovementPromptText);
        this.StatusText="改善プロンプトをコピーしました";
    }

    private int chartRefreshBatchDepth;
    private bool pendingChartUpdate;
    private bool pendingChartUpdatePreserveExisting=true;
    private bool pendingProgressRefresh;
    private DateTimeOffset lastRunningProgressRefreshAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan RunningProgressRefreshInterval = TimeSpan.FromMilliseconds(250);

    private IDisposable BeginChartRefreshBatch() {
        this.chartRefreshBatchDepth++;
        return new ChartRefreshBatchScope(this);
    }

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

    private sealed class ChartRefreshBatchScope:IDisposable{
        private readonly BenchmarkRunnerViewModel owner;
        private bool disposed;
        public ChartRefreshBatchScope(BenchmarkRunnerViewModel owner) => this.owner=owner;
        public void Dispose() {
            if(this.disposed) {
                return;
            }
            this.disposed=true;
            this.owner.EndChartRefreshBatch();
        }
    }

    private void RefreshSourceTabProgresses() {
        if(this.chartRefreshBatchDepth>0) {
            this.pendingProgressRefresh=true;
            return;
        }
        this.RefreshSourceTabProgressesCore();
    }

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

    private void RefreshSourceTabProgressesCore() {
        foreach(var option in this.SourceTabs) {
            option.SetProgress(this.CreateSourceTabProgress(option.Definition));
        }
        this.OverallSourceTabProgress=new BenchmarkTabProgress(
            this.SourceTabs.Sum(static option => option.Progress.Completed),
            this.SourceTabs.Sum(static option => option.Progress.Total));
    }

    private BenchmarkTabProgress CreateSourceTabProgress(BenchmarkSourceTabDefinition sourceTab) {
        if(this.catalogCases.Count==0) {
            return new BenchmarkTabProgress(0,0);
        }

        var cells = this.CreateProgressCells(sourceTab);
        var measurements = this.CreateProgressMeasurements(sourceTab);
        return BenchmarkTabProgressLogic.Calculate(
            cells,
            measurements,
            sourceTab.LeftProject,
            sourceTab.RightProject,
            this.GetSelectedElementCounts().LastOrDefault(),
            this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000);
    }

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

    private IReadOnlyList<BenchmarkTabProgressCell> CreateCurrentRunProgressCells(BenchmarkSourceTabDefinition sourceTab) {
        if(!this.IsBenchmarkRunActive&&!this.currentRunSession.HasAnyResults) {
            return [];
        }

        var selectedCases = this.GetCurrentRunSelectedBenchmarkCases();
        return selectedCases.Count==0
            ? []
            : BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases(selectedCases,sourceTab);
    }

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

    private IReadOnlyList<BenchmarkElementMeasurement> CreateProgressMeasurements(BenchmarkSourceTabDefinition sourceTab) {
        return this.CreateProgressResultItems(sourceTab)
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

    private IEnumerable<BenchmarkResultItem> CreateProgressResultItems(BenchmarkSourceTabDefinition sourceTab) {
        foreach(var item in this.GetCurrentRunResultItems(sourceTab.Tab)){
            yield return item;
        }

        foreach(var item in this.ResultItems.Where(item => item.SourceTab==sourceTab.Tab)){
            yield return item;
        }

        if(this.currentRunSession.HasAnyResults){
            yield break;
        }

        var selectedRunState=this.LoadSelectedRunState();
        if(selectedRunState is null){
            yield break;
        }

        foreach(var job in selectedRunState.Jobs.Where(job => job.SourceTab==sourceTab.Tab)){
            yield return new BenchmarkResultItem(job);
        }
    }

    private ProgressState? GetProgressStateForImprovementPrompt() {
        if(this.currentRunSession.HasAnyResults) {
            return this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot??string.Empty);
        }

        return this.LoadSelectedRunState();
    }

    private void RetainCompletedHighlightsForRunningElement(int runningElementCount) {
        if(runningElementCount<=0) {
            return;
        }

        foreach(var row in this.GetAllBenchmarkRows()) {
            row.RetainCompletedHighlightsForRunningElement(runningElementCount);
        }
    }

    private void ClearRunResultHighlights() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.ClearRunHighlights();
        }
    }

    private void ClearBenchmarkResultDisplays() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.ClearResultDisplay();
        }
        this.ClearFinalCompletionStates();
    }

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
        } finally {
            this.isApplyingResultElementCount=false;
        }
    }

    private static int? FindLatestTerminalElementCount(ProgressState? state,BenchmarkSourceTabDefinition sourceTab) {
        return state?.Jobs
            .Where(job => BenchmarkWorkloadAxis.GetAxisCount(job)>0&&IsTerminalJob(job)&&sourceTab.ContainsProject(job.Project))
            .OrderBy(static job => job.FinishedAt??job.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static job => BenchmarkWorkloadAxis.GetAxisCount(job))
            .LastOrDefault()
            is { } job ? BenchmarkWorkloadAxis.GetAxisCount(job) : null;
    }

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

    private static bool IsTerminalJob(BenchmarkJob job) =>
        job.Status is JobStatus.Completed or JobStatus.Failed;

    private static BenchmarkRunOption CreateNewRunOption(BenchmarkExecutionProfile executionProfile) =>
        NewRunOption with {
            ExecutionProfile=executionProfile,
        };

    private static bool IsNewRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber==NewRunNumber;

    private static bool IsUnsavedRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber==UnsavedRunNumber;

    private static bool IsExternalRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber==ExternalRunNumber;

    private static bool IsTransientRunOption(BenchmarkRunOption? option) =>
        IsUnsavedRunOption(option)||IsExternalRunOption(option);

    private bool ShouldKeepCurrentRunMemoryDisplay() =>
        BenchmarkRunDisplaySourceLogic.ShouldKeepCurrentRunMemoryDisplay(
            this.currentRunSession.HasAnyResults);

    private bool ShouldLoadSelectedRunFromDatabaseForDisplay() =>
        BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
            this.HasCurrentRunResultsForSelectedTab,
            this.currentRunSession.HasAnyResults,
            this.IsBenchmarkRunActive,
            this.isLoadingSelectedRunDb,
            IsExternalRunOption(this.SelectedRunOption),
            IsNewRunOption(this.SelectedRunOption),
            IsTransientRunOption(this.SelectedRunOption),
            this.SelectedRunOption is { UiRunId: >0 });

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

    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRowsForElementCount(int elementCount) {
        return this.BuildCurrentRunComparisonRowsForElementCount(this.SelectedSourceDefinition,elementCount);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRowsForElementCount(BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        return this.runProjectionService.BuildComparisonRowsForElementCount(
            this.currentRunSession,
            sourceTab,
            elementCount,
            this.CreateReportRowsForComparison);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRows() {
        return this.BuildCurrentRunComparisonRows(this.SelectedSourceDefinition);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRows(BenchmarkSourceTabDefinition sourceTab) {
        return this.runProjectionService.BuildLatestComparisonRows(
            this.currentRunSession,
            sourceTab,
            this.CreateReportRowsForComparison);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRowsForElementCount(int elementCount) {
        return this.BuildComparisonRowsForElementCount(this.SelectedSourceDefinition,elementCount);
    }

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

    private IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRowsForElementCount(BenchmarkSourceTabDefinition sourceTab,int elementCount) {
        return this.runProjectionService.BuildDisplayedRowsForElementCount(
            this.ResultItems,
            sourceTab,
            elementCount,
            this.CreateReportRowsForComparison);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildDisplayedRows(BenchmarkSourceTabDefinition sourceTab) {
        return this.runProjectionService.BuildDisplayedRows(
            this.ResultItems,
            sourceTab,
            this.CreateReportRowsForComparison);
    }

    private void ApplySkippedCells(int selectedElementCount) {
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
    }

    private void RefreshFinalCompletionStates() {
        var measurements = this.CreateDisplayMeasurements();
        if(measurements.Length==0) {
            this.ClearFinalCompletionStates();
            return;
        }

        var maxElementCount = this.GetFinalCompletionMaxElementCount();
        var thresholdNanoseconds = this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000;
        foreach(var row in this.BenchmarkRows) {
            var isFinalCompleted = this.BenchmarkPairs().Any(pair =>
                BenchmarkRowCompletionLogic.IsFinalCompleted(
                    measurements,
                    CreateProgressCell(row,pair.LeftProject,true),
                    CreateProgressCell(row,pair.RightProject,true),
                    maxElementCount,
                    thresholdNanoseconds));
            row.SetFinalCompleted(isFinalCompleted);
        }
    }

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

    private void ClearFinalCompletionStates() {
        foreach(var row in this.GetAllBenchmarkRows()) {
            row.SetFinalCompleted(false);
        }
    }

    private int GetFinalCompletionMaxElementCount() {
        if(this.IsBenchmarkRunActive||this.HasCurrentRunResultsForSelectedTab) {
            return this.GetSelectedElementCounts().LastOrDefault();
        }

        var runElementCounts = this.SelectedRunOption?.ElementCounts
            .Where(static item => item>0)
            .Order()
            .ToArray();
        return runElementCounts is { Length: > 0 }
            ? runElementCounts[^1]
            : this.GetSelectedElementCounts().LastOrDefault();
    }

    private void ApplySkippedCell(BenchmarkTableRow row,string project,int stoppedAtElementCount) {
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

    private IReadOnlyList<(string LeftProject,string RightProject)> BenchmarkPairs() => [
        (this.SelectedSourceDefinition.LeftProject,this.SelectedSourceDefinition.RightProject),
    ];

    private static string GetRowTypeFullName(BenchmarkTableRow row,string project) =>
        row.CasesByProject.TryGetValue(project,out var benchmark)
            ? benchmark.TypeFullName
            : row.CasesByProject.Values.FirstOrDefault()?.TypeFullName??string.Empty;

    private static string GetRowArgumentKey(BenchmarkTableRow row,string project) =>
        row.CasesByProject.TryGetValue(project,out var benchmark)
            ? benchmark.ArgumentKey
            : row.CasesByProject.Values.FirstOrDefault()?.ArgumentKey??string.Empty;

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

    private IReadOnlyList<BenchmarkResultItem> GetResultSourceForDisplay() {
        var source = BenchmarkRunDisplaySourceLogic.ShouldUseCurrentRunMemory(this.HasCurrentRunResultsForSelectedTab)
            ? this.GetCurrentRunResultItemsForSelectedTab()
            : this.ResultItems;
        return source.Where(this.IsResultSelectedForDisplay).ToArray();
    }

    private bool IsResultSelectedForDisplay(BenchmarkResultItem result) {
        return this.IsResultSelectedForDisplay(result,this.SelectedSourceDefinition);
    }

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

    private static bool IsResultMatch(BenchmarkResultItem item,BenchmarkResultItem result) =>
        IsResultMatch(item,result.Method,result.Project,result.TypeFullName,result.AxisCount,result.ArgumentKey);

    private static bool IsResultMatch(BenchmarkResultItem item,string method,string project,string typeFullName,int elementCount,string argumentKey = "") =>
        string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(item.Project,project,StringComparison.OrdinalIgnoreCase)
        &&IsSameType(item.TypeFullName,typeFullName)
        &&IsSameArgument(item.ArgumentKey,argumentKey)
        &&item.AxisCount==elementCount;

    private static string CreateResultIdentityKey(string project,string typeFullName,string method,int elementCount,string argumentKey = "") =>
        string.Join("|",project,typeFullName,method,argumentKey,elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static bool IsSameType(string left,string right) =>
        string.IsNullOrWhiteSpace(right)
            ? true
            : !string.IsNullOrWhiteSpace(left)
                &&(string.Equals(left,right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(ShortTypeName(left),right,StringComparison.OrdinalIgnoreCase)
                    ||string.Equals(left,ShortTypeName(right),StringComparison.OrdinalIgnoreCase));

    private static bool IsSameArgument(string left,string right) =>
        BenchmarkArgumentKeyMatchLogic.IsExactMatch(left,right);

    private void UpdateChart(bool preserveExistingWhenNoMeasuredPoints = true) {
        if(this.chartRefreshBatchDepth>0) {
            this.pendingChartUpdate=true;
            this.pendingChartUpdatePreserveExisting&=preserveExistingWhenNoMeasuredPoints;
            return;
        }
        this.UpdateChartCore(preserveExistingWhenNoMeasuredPoints);
    }

    private void UpdateChartCore(bool preserveExistingWhenNoMeasuredPoints = true) {
        this.UpdateChartCore(this.SelectedSourceTabViewState,preserveExistingWhenNoMeasuredPoints);
    }

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

    private void UpdateChartCore(BenchmarkSourceTabViewState state,bool preserveExistingWhenNoMeasuredPoints = true) {
        var elementComparisonRows = this.BuildElementComparisonRows(state);
        var points = this.BuildBenchmarkChartPoints(state,elementComparisonRows);
        this.SyncChartPoints(state.ChartPoints,points);
        state.ChartHeight=Math.Max(BenchmarkChartMinimumHeight,points.Count*BenchmarkChartRowHeight+BenchmarkChartVerticalPadding);
        this.RefreshDiagnosticViews(state,elementComparisonRows);
    }

    private void RefreshDiagnosticViews(IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.RefreshDiagnosticViews(this.SelectedSourceTabViewState,rows);
    }

    private void RefreshDiagnosticViews(BenchmarkSourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.SyncDiagnosticRows(state.LatencyHotspotRows,this.BuildMetricDiagnosticRowsReadable(state.Definition,rows,BenchmarkMetricKind.Time));
        this.SyncDiagnosticRows(state.AllocationDiagnosticRows,this.BuildMetricDiagnosticRowsReadable(state.Definition,rows,BenchmarkMetricKind.Allocated));
        this.SyncDiagnosticRows(state.CpuTimelineRows,this.BuildCpuTimelineRows());
        this.generatedCodeAuditDirty=true;
        this.RefreshGeneratedCodeAuditIfNeeded();
    }

    private IReadOnlyList<BenchmarkDiagnosticRow> BuildMetricDiagnosticRowsReadable(BenchmarkSourceTabDefinition sourceTab,IReadOnlyList<BenchmarkComparisonRow> rows,BenchmarkMetricKind metric) {
        return rows
            .Select(row => {
                var left = row.GetProject(sourceTab.LeftProject);
                var right = row.GetProject(sourceTab.RightProject);
                var leftValue = metric==BenchmarkMetricKind.Time ? left.TimeNanoseconds : left.AllocatedBytes;
                var rightValue = metric==BenchmarkMetricKind.Time ? right.TimeNanoseconds : right.AllocatedBytes;
                var ratio = leftValue is >0&&rightValue is >0 ? leftValue/rightValue : (double?)null;
                return new {
                    Row=row,
                    LeftValue=leftValue,
                    RightValue=rightValue,
                    Ratio=ratio,
                };
            })
            .Where(item => item.LeftValue is not null&&item.RightValue is not null)
            .OrderByDescending(item => item.Ratio??(item.LeftValue>0&&item.RightValue==0 ? double.PositiveInfinity : 0))
            .ThenByDescending(item => item.LeftValue??0)
            .Take(40)
            .Select(item => new BenchmarkDiagnosticRow(
                metric==BenchmarkMetricKind.Time ? "Latency" : "Allocation",
                BenchmarkFormatting.ShortName(item.Row.Benchmark),
                this.SelectedResultElementCount?.DisplayName??"-",
                item.Ratio is { } ratio ? ratio.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"x" : "-",
                FormatMetric(item.LeftValue,metric),
                FormatMetric(item.RightValue,metric),
                sourceTab.LeftProject+" / "+sourceTab.RightProject+" / "+item.Row.MethodName))
            .ToArray();
    }

    private IReadOnlyList<BenchmarkDiagnosticRow> BuildMetricDiagnosticRows(IReadOnlyList<BenchmarkComparisonRow> rows,BenchmarkMetricKind metric) {
        var sourceTab = this.SelectedSourceDefinition;
        return rows
            .Select(row => {
                var left = row.GetProject(sourceTab.LeftProject);
                var right = row.GetProject(sourceTab.RightProject);
                var leftValue = metric==BenchmarkMetricKind.Time ? left.TimeNanoseconds : left.AllocatedBytes;
                var rightValue = metric==BenchmarkMetricKind.Time ? right.TimeNanoseconds : right.AllocatedBytes;
                var ratio = leftValue is >0&&rightValue is >0 ? leftValue/rightValue : (double?)null;
                return new {
                    Row=row,
                    LeftValue=leftValue,
                    RightValue=rightValue,
                    Ratio=ratio,
                };
            })
            .Where(item => item.LeftValue is not null&&item.RightValue is not null)
            .OrderByDescending(item => item.Ratio??(item.LeftValue>0&&item.RightValue==0 ? double.PositiveInfinity : 0))
            .ThenByDescending(item => item.LeftValue??0)
            .Take(40)
            .Select(item => new BenchmarkDiagnosticRow(
                metric==BenchmarkMetricKind.Time ? "Latency" : "Allocation",
                BenchmarkFormatting.ShortName(item.Row.Benchmark),
                this.SelectedResultElementCount?.DisplayName??"-",
                item.Ratio is { } ratio ? ratio.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"x" : "-",
                FormatMetric(item.LeftValue,metric),
                FormatMetric(item.RightValue,metric),
                sourceTab.LeftProject+" / "+sourceTab.RightProject+" / "+item.Row.MethodName))
            .ToArray();
    }

    private IReadOnlyList<BenchmarkDiagnosticRow> BuildCpuTimelineRows() {
        return this.currentRunSession.GetAllResults()
            .OrderByDescending(static item => item.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .Select(item => new BenchmarkDiagnosticRow(
                item.Status,
                BenchmarkFormatting.ShortName(item.Method),
                item.AxisCount>0 ? item.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-",
                item.AssignedLogicalCpu is { } cpu ? "CPU "+cpu.ToString(System.Globalization.CultureInfo.InvariantCulture) : "CPU -",
                item.Mean,
                item.AllocatedBytes,
                item.Project+" / "+(string.IsNullOrWhiteSpace(item.TypeFullName) ? item.Method : item.TypeFullName)))
            .ToArray();
    }

    private void SyncDiagnosticRows(ObservableCollection<BenchmarkDiagnosticRow> target,IReadOnlyList<BenchmarkDiagnosticRow> rows) {
        target.Clear();
        foreach(var row in rows) {
            target.Add(row);
        }
    }

    private bool generatedCodeAuditDirty = true;
    private int generatedCodeAuditRequestToken;

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
                        ? new BenchmarkGeneratedCodeAuditReport(input.Value.MethodName,input.Value.LeftProject+" vs "+input.Value.RightProject,"Load failed",null,[],"Generated code audit failed: "+task.Exception!.GetBaseException().Message)
                        : task.Result;
                    if(!task.IsFaulted) {
                        this.generatedCodeAuditCache[input.Value.CacheKey]=report;
                    }
                    this.GeneratedCodeAuditReport=report;
                });
            },TaskScheduler.Default);
    }

    private readonly record struct GeneratedCodeAuditInput(
        string MethodName,string LeftProject,string RightProject,
        string BenchmarkProject,string AssemblyName,string TypeFullName,string CacheKey);

    public sealed record BenchmarkGeneratedCodeAuditReport(
        string TargetMethod,string ComparisonPair,string AuditTarget,string? GeneratedFileName,
        IReadOnlyList<BenchmarkBadgeItem> Flags,string Verdict);

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

    private static BenchmarkBadgeItem CreateAuditFlag(string title,string label,bool ok) =>
        new(title,ok ? "OK "+label : "Warning: "+label,ok ? BenchmarkBadgeSeverity.Ok : BenchmarkBadgeSeverity.Warning);

    private static BenchmarkBadgeItem CreateAuditPresence(string title,bool exists) =>
        new(title,exists ? "present" : "absent",exists ? BenchmarkBadgeSeverity.Info : BenchmarkBadgeSeverity.Ok);

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

    private void SyncChartPoints(IReadOnlyList<ChartComparisonPoint> points) {
        this.SyncChartPoints(this.ChartPoints,points);
    }

    private void SyncChartPoints(ObservableCollection<ChartComparisonPoint> target,IReadOnlyList<ChartComparisonPoint> points) {
        for(var index=0;index<points.Count;index++) {
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
            }else {
                target.Add(point);
            }
        }

        while(target.Count>points.Count) {
            target.RemoveAt(target.Count-1);
        }
    }

    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(IReadOnlyList<BenchmarkComparisonRow> rows) {
        return this.BuildBenchmarkChartPoints(this.SelectedSourceTabViewState,rows);
    }

    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(BenchmarkSourceTabViewState state,IReadOnlyList<BenchmarkComparisonRow> rows) {
        var gridOrderPoints = state.Rows
            .Select(row => this.CreateBenchmarkChartPoint(row,rows,state.Definition))
            .Where(static point => point is not null)
            .Select(static point => point!)
            .ToArray();
        return BenchmarkChartDisplayOrderLogic.CreateTransposedSourceOrder(gridOrderPoints);
    }

    private ChartComparisonPoint? CreateBenchmarkChartPoint(
        BenchmarkTableRow row,
        IReadOnlyList<BenchmarkComparisonRow> rows) =>
        this.CreateBenchmarkChartPoint(row,rows,this.SelectedSourceDefinition);

    private ChartComparisonPoint? CreateBenchmarkChartPoint(
        BenchmarkTableRow row,
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

    private static BenchmarkComparisonRow? FindComparisonRow(BenchmarkTableRow row,IReadOnlyList<BenchmarkComparisonRow> rows) {
        foreach(var item in rows) {
            if(IsComparisonRowMatch(row,item)) {
                return item;
            }
        }

        return null;
    }

    private static bool IsComparisonRowMatch(BenchmarkTableRow row,BenchmarkComparisonRow item) =>
        BenchmarkComparisonRowMatchLogic.IsMatch(
            row.MethodName,
            row.BenchmarkName,
            row.SourceTab.Tab,
            row.CasesByProject.Values.Select(static benchmark => new BenchmarkComparisonRowMatchCase(benchmark.TypeFullName,benchmark.ArgumentKey)),
            item);

    private ChartComparisonPoint CreateChartComparisonPoint(BenchmarkTableRow row,double? leftValue,double? rightValue) =>
        this.CreateChartComparisonPoint(row.RowKey,row.BenchmarkName,leftValue,rightValue,row,this.SelectedComparisonPair);

    private ChartComparisonPoint CreateChartComparisonPoint(BenchmarkTableRow row,double? leftValue,double? rightValue,ComparisonPairOption pair) =>
        this.CreateChartComparisonPoint(row.RowKey,row.BenchmarkName,leftValue,rightValue,row,pair);

    private ChartComparisonPoint CreateChartComparisonPoint(string benchmark,string displayName,double? leftValue,double? rightValue,BenchmarkTableRow row,ComparisonPairOption pair) {
        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var shareTexts = BenchmarkChartSelectionLayoutLogic.CreateShareTexts(leftNumber,rightNumber);
        double? ratio = BenchmarkChartSelectionLayoutLogic.CreateRatio(leftNumber,rightNumber);
        var ratioText = ratio is { } value
            ? value.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)
            : "-";
        var metricName = this.SelectedMetric.Kind==BenchmarkMetricKind.Time?"Time":"Memory";
        var tooltip = BenchmarkFormatting.ShortName(displayName)+"\n"
            +pair.LeftProject+" "+metricName+": "+FirstText(FormatMetric(leftValue,this.SelectedMetric.Kind),"-")+"\n"
            +pair.RightProject+" "+metricName+": "+FirstText(FormatMetric(rightValue,this.SelectedMetric.Kind),"-")
            +"\nRatio(left/right): "+ratioText
            +"\nShare: "+pair.LeftProject+" "+shareTexts.LeftText+" / "+pair.RightProject+" "+shareTexts.RightText
            +(string.IsNullOrWhiteSpace(BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(row))
                ? string.Empty
                : "\n"+BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(row));
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

    private string CreateChartArgumentTooltip(BenchmarkTableRow row) {
        return BenchmarkArgumentTooltipFormatter.CreateArgumentTooltip(row);
    }

    private string GetChartPointCacheKey(string method) =>
        this.GetChartPointCacheKey(this.SelectedSourceDefinition,method);

    private string GetChartPointCacheKey(BenchmarkSourceTabDefinition sourceTab,string method) =>
        sourceTab.Id+"|"
        +sourceTab.Pair.LeftProject+"|"
        +sourceTab.Pair.RightProject+"|"
        +this.SelectedMetric.Kind+"|"
        +method;

    private void ClearRetainedChartPoints() {
        this.retainedChartPoints.Clear();
    }

    private ChartComparisonPoint ApplyChartRunningState(ChartComparisonPoint point) =>
        point.WithRunningState(
            this.IsBenchmarkRunActive&&string.Equals(point.Benchmark,this.activeChartMethodName,StringComparison.OrdinalIgnoreCase));

    private ChartComparisonPoint ApplyChartUiSelection(BenchmarkTableRow row,ChartComparisonPoint point) =>
        point.WithUiSelection(row.LeftCell.IsUiSelected,row.RightCell.IsUiSelected,row.IsUiSelected);

    private void RefreshChartUiSelection(BenchmarkSourceTabViewState state) {
        for(var index=0;index<state.ChartPoints.Count;index++) {
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

    private void ApplyChartRunningState() {
        for(var index=0;index<this.ChartPoints.Count;index++) {
            this.ChartPoints[index]=this.ApplyChartRunningState(this.ChartPoints[index]);
        }
    }

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

    private IReadOnlyList<BenchmarkComparisonRow> BuildElementComparisonRows() {
        return this.BuildElementComparisonRows(this.SelectedSourceTabViewState);
    }

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

    private static double? GetMetricValue(BenchmarkResultItem? item,BenchmarkMetricKind metric) {
        if(item is null) {
            return null;
        }

        return metric==BenchmarkMetricKind.Time
            ? item.MeanNanoseconds
            : item.AllocatedBytesValue;
    }

    private static double? GetMetricValue(ProjectMetrics metrics,BenchmarkMetricKind metric) {
        if(!metrics.HasMeasuredValue) {
            return null;
        }

        return metric==BenchmarkMetricKind.Time
            ? metrics.TimeNanoseconds
            : metrics.AllocatedBytes;
    }

    private static string FormatMetric(double? value,BenchmarkMetricKind metric) {
        if(value is null) {
            return string.Empty;
        }

        return metric==BenchmarkMetricKind.Time
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : BenchmarkFormatting.FormatBytes((long)Math.Round(value.Value,MidpointRounding.AwayFromZero));
    }

    private void SetMetric(BenchmarkMetricKind kind) {
        var option = this.Metrics.First(item => item.Kind==kind);
        if(!ReferenceEquals(this.selectedMetric,option)) {
            this.SelectedMetric=option;
        }
    }

    private void AppendLogText(string text) {
        this.uiDispatcher.BeginInvoke(() => {
            this.LogText+=text;
            this.LogEntries.Add(text);
        });
    }

    private static string FirstText(params string[] values) {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;
    }

    private static string GetChartLegendLabel(string project) =>
        BenchmarkSourceTabs.TryGetByProject(project) is { } tab
            ? string.Equals(project,tab.LeftProject,StringComparison.OrdinalIgnoreCase)
                ? tab.LeftHeader
                : tab.RightHeader
            : project;

    private void RefreshBenchmarkSortColumns() {
        this.HasBenchmarkTypeColumn=true;
        this.RefreshBenchmarkSortColumnOptions();
        var rules = this.CreateValidSortRules(this.BenchmarkSortRules).ToArray();
        if(rules.Length==0) {
            rules=this.CreateInitialSortRules().ToArray();
        }
        this.SetBenchmarkSortRules(rules,false);
    }

    private void RefreshBenchmarkSortColumnOptions() {
        var columns = this.CreateAvailableSortColumns().ToArray();
        this.BenchmarkSortColumnOptions.Clear();
        foreach(var column in columns) {
            this.BenchmarkSortColumnOptions.Add(new BenchmarkSortColumnOption(column,BenchmarkSortRule.GetColumnText(column)));
        }
    }

    private IEnumerable<BenchmarkGridSortColumn> CreateAvailableSortColumns() {
        if(this.HasBenchmarkTypeColumn) {
            yield return BenchmarkGridSortColumn.BenchmarkType;
        }
        yield return BenchmarkGridSortColumn.BenchmarkMethod;
        yield return BenchmarkGridSortColumn.ElementCount;
        foreach(var column in BenchmarkArgumentColumns.Get(this.SelectedSourceDefinition)) {
            yield return column switch{
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
        yield return BenchmarkGridSortColumn.PairRatioTime;
        yield return BenchmarkGridSortColumn.PairRatioAllocated;
        yield return BenchmarkGridSortColumn.LeftTime;
        yield return BenchmarkGridSortColumn.LeftAllocated;
        yield return BenchmarkGridSortColumn.RightTime;
        yield return BenchmarkGridSortColumn.RightAllocated;
    }

    private IReadOnlyList<BenchmarkSortRule> CreateValidSortRules(IEnumerable<BenchmarkSortRule> source) {
        var allowed = this.CreateAvailableSortColumns().ToHashSet();
        var used = new HashSet<BenchmarkGridSortColumn>();
        return source
            .Where(rule => allowed.Contains(rule.Column)&&used.Add(rule.Column))
            .Select(rule => new BenchmarkSortRule(rule.Column,rule.Descending))
            .ToArray();
    }

    private IReadOnlyList<BenchmarkSortRule> CreateInitialSortRules() {
        var restored = this.CreateValidSortRules(this.savedSortRuleSnapshots.Select(static item => new BenchmarkSortRule(item.Column,item.Descending)));
        if(restored.Count>0) {
            return restored;
        }

        return this.CreateDefaultSortRules();
    }

    private IReadOnlyList<BenchmarkSortRule> CreateDefaultSortRules() {
        if(this.HasBenchmarkTypeColumn) {
            return [
                new BenchmarkSortRule(BenchmarkGridSortColumn.BenchmarkType),
                new BenchmarkSortRule(BenchmarkGridSortColumn.BenchmarkMethod),
            ];
        }

        return [new BenchmarkSortRule(BenchmarkGridSortColumn.BenchmarkMethod)];
    }

    private void ResetBenchmarkSortRulesToDefault(bool save) =>
        this.SetBenchmarkSortRules(this.CreateDefaultSortRules(),save);

    private IReadOnlyList<BenchmarkSortRule> GetEffectiveSortRules() {
        var rules = this.CreateValidSortRules(this.BenchmarkSortRules);
        return rules.Count>0 ? rules : this.CreateDefaultSortRules();
    }

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

    private IReadOnlyList<BenchmarkSortRuleSnapshot> LoadSavedBenchmarkSortRules() {
        try {
            var json = this.uiPreferences.Get(BenchmarkSortSettingsPreferenceKey,string.Empty);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<BenchmarkSortRuleSnapshot[]>(json)??[];
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark sort settings load failed.",ex);
            return [];
        }
    }

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

    private void ApplyCurrentSort() {
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

    private void ApplyFinalBenchmarkSort() {
        this.SetBenchmarkSortRules([new BenchmarkSortRule(BenchmarkGridSortColumn.PairRatioTime,true)],true);
    }

    private readonly record struct BenchmarkSortRuleSnapshot(BenchmarkGridSortColumn Column,bool Descending);

    private void RebuildCpuSections() {
        this.CpuSections.Clear();
        this.AddCpuSection("P core",CpuCoreKind.Performance);
        this.AddCpuSection("E core",CpuCoreKind.Efficiency);
        this.AddCpuSection("Unknown core",CpuCoreKind.Unknown);
        this.OnPropertyChanged(nameof(this.PerformanceCpuSections));
        this.OnPropertyChanged(nameof(this.EfficiencyCpuSections));
        this.OnPropertyChanged(nameof(this.UnknownCpuSections));
    }

    private IEnumerable<CpuTableSection> GetCpuSections(CpuCoreKind kind) =>
        this.CpuSections.Where(section => section.CoreKind==kind);

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

    private static string CreateCpuRangeLabel(IReadOnlyList<CpuSelectionItem> cpus) {
        var min = cpus.Min(static cpu => cpu.LogicalProcessorIndex);
        var max = cpus.Max(static cpu => cpu.LogicalProcessorIndex);
        return min==max
            ? min.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : min.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+max.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

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
            if(!selected) {
                var selectedOutsideTargets = this.CpuItems.Any(cpu => cpu.IsSelected&&!targets.Contains(cpu));
                if(!selectedOutsideTargets&&this.CpuItems.Any(static cpu => cpu.IsSelected)) {
                    var keep = targets.FirstOrDefault(static cpu => cpu.IsSelected)??targets[0];
                    foreach(var cpu in targets) {
                        cpu.IsSelected=ReferenceEquals(cpu,keep);
                    }
                    return;
                }
            }

            foreach(var cpu in targets) {
                cpu.IsSelected=selected;
            }
        }
        finally {
            this.isApplyingCpuSelection=false;
            this.EnsureAtLeastOneCpuSelected(null);
            this.RefreshSelectedLogicalCpus();
            this.RefreshCpuSectionStates();
        }
    }

    private void EnsureAtLeastOneCpuSelected(CpuSelectionItem? fallback) {
        if(this.CpuItems.Count==0||this.CpuItems.Any(static cpu => cpu.IsSelected)) {
            return;
        }

        (fallback??this.CpuItems[0]).IsSelected=true;
    }

    private IReadOnlyList<int> GetSelectedLogicalCpus() =>
        Volatile.Read(ref this.selectedLogicalCpus);

    private void RefreshSelectedLogicalCpus() =>
        Volatile.Write(ref this.selectedLogicalCpus,this.CreateSelectedLogicalCpus());

    private int[] CreateSelectedLogicalCpus() =>
        this.CpuItems
            .Where(static cpu => cpu.IsSelected)
            .Select(static cpu => cpu.LogicalProcessorIndex)
            .ToArray();

    private void RefreshCpuSectionStates() {
        foreach(var section in this.CpuSections) {
            section.RefreshState();
            foreach(var column in section.Columns) {
                this.OnPropertyChanged("CpuColumnState"+section.Title+column.Index);
            }
        }
    }

}
