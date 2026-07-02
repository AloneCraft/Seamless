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
    private static readonly TimeSpan RunningVisualMinimumDuration = TimeSpan.FromMilliseconds(250);

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

    private readonly IBenchmarkCatalogService catalogService;
    private readonly ICpuTopologyService cpuTopologyService;
    private readonly IBenchmarkRunService runService;
    private readonly IBenchmarkComparisonService comparisonService;
    private readonly UiRunStore uiRunStore;
    private readonly BenchmarkUiRunRepository uiRunRepository;
    private readonly BenchmarkRunProjectionService runProjectionService = new();
    private readonly string benchmarkRoot;
    private CancellationTokenSource? runCancellation;
    private string? activeRunRoot;
    private RunTerminationKind pendingRunTerminationKind;
    private string statusText = "準備完了";
    private bool isBenchmarkRunning;
    private bool isRefreshing;
    private bool? isAllBenchmarkSelectionChecked;
    private bool hasSelectedBenchmarkCells;
    private BenchmarkTab selectedTab = BenchmarkTab.Cpu;
    private SourceTabOption selectedSourceTab;
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
    private string benchmarkRatioSummaryText = string.Empty;
    private string improvementPromptText = string.Empty;
    private string improvementCandidateSummaryText = string.Empty;
    private string generatedCodeAuditText = string.Empty;
    private double chartHeight = 300;
    private bool isApplyingCpuSelection;
    private bool isApplyingUiState;
    private bool isApplyingBenchmarkSelection;
    private bool isApplyingResultElementCount;
    private bool isApplyingRunOptionSelection;
    private bool isApplyingSourceTabSelection;
    private bool isLoadingSelectedRunDb;
    private bool hasPendingCloseStateSave;
    private bool hasUnsavedBenchmarkResults;
    private BenchmarkGridSortColumn currentSortColumn = BenchmarkGridSortColumn.BenchmarkName;
    private bool currentSortDescending;
    private BenchmarkCellSelectionAnchor? benchmarkSelectionAnchor;
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows = [];
    private readonly BenchmarkRunSession currentRunSession = new();
    private readonly Dictionary<string,bool> benchmarkCellSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,bool> benchmarkRowSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,bool> improvementCandidateSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,BenchmarkCase> benchmarkCasesByProjectMethod = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> generatedCodeAuditCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,DateTimeOffset> runningVisualStartedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly BenchmarkSourceTabRunningState sourceTabRunningState = new();
    private readonly Queue<PendingRunResult> pendingRunResults = new();
    private IReadOnlyList<BenchmarkCase> catalogCases = [];
    private BenchmarkSourceTab? activeRunSourceTab;
    private IReadOnlyList<BenchmarkCase> currentRunSelectedBenchmarkCases = [];
    private int[] selectedLogicalCpus = [];
    private string logText = string.Empty;
    private BenchmarkLowerTab selectedLowerTab = BenchmarkLowerTab.Cpu;
    private BenchmarkTabProgress overallSourceTabProgress;
    private IDisposable? logSubscription;
    private readonly HashSet<string> reportedMetadataWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,ChartComparisonPoint> retainedChartPoints = new(StringComparer.OrdinalIgnoreCase);
    private string activeChartMethodName = string.Empty;
    private int runResultGeneration;
    private int benchmarkAutoScrollSequence;
    private bool isRunResultFlushScheduled;
    private string? currentUnsavedRunRoot;
    private ProgressState? loadedExternalState;
    private long? activeUiRunId;
    private long? loadedImprovementSelectionRunId;
    private bool hasLoadedImprovementCandidateSelections;
    private bool isApplyingImprovementCandidateSelection;

    private enum RunTerminationKind {
        None,
        Stop,
        Pause,
    }

    private sealed record PendingRunResult(BenchmarkResultItem Result,int RunGeneration,long? UiRunId);

    internal BenchmarkRunnerViewModel(
        IBenchmarkCatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IBenchmarkRunService runService,
        IBenchmarkComparisonService comparisonService,
        UiRunStore uiRunStore,
        string benchmarkRoot) {
        this.catalogService=catalogService;
        this.cpuTopologyService=cpuTopologyService;
        this.runService=runService;
        this.comparisonService=comparisonService;
        this.uiRunStore=uiRunStore;
        this.uiRunRepository=new BenchmarkUiRunRepository(uiRunStore,StartupDiagnostics.Write);
        this.benchmarkRoot=benchmarkRoot;
        this.SourceTabs=BenchmarkSourceTabs.All.Select(static item => new SourceTabOption(item)).ToArray();
        this.selectedSourceTab=this.SourceTabs[0];
        this.ApplySourceTabVisualSelection();
        this.BenchmarkColumnSelectionGroups=this.CreateBenchmarkColumnSelectionGroups();
        this.BenchmarkProjectSelectionGroups=this.CreateBenchmarkProjectSelectionGroups();
        this.ExecutionProfiles= [
            new ExecutionProfileOption("正確",BenchmarkExecutionProfile.Accurate),
            new ExecutionProfileOption("バランス",BenchmarkExecutionProfile.Intermediate),
            new ExecutionProfileOption("高速",BenchmarkExecutionProfile.Fast),
        ];
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

    public ObservableCollection<BenchmarkTableRow> BenchmarkRows { get; } = [];
    public ObservableCollection<BenchmarkResultItem> ResultItems { get; } = [];
    public ObservableCollection<BenchmarkResultItem> VisibleResultItems { get; } = [];
    public ObservableCollection<CpuSelectionItem> CpuItems { get; } = [];
    public ObservableCollection<CpuTableSection> CpuSections { get; } = [];
    public ObservableCollection<BenchmarkImprovementCandidateRow> ImprovementCandidates { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> LatencyHotspotRows { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> AllocationDiagnosticRows { get; } = [];
    public ObservableCollection<BenchmarkDiagnosticRow> CpuTimelineRows { get; } = [];
    public IEnumerable<CpuTableSection> PerformanceCpuSections => this.GetCpuSections(CpuCoreKind.Performance);
    public IEnumerable<CpuTableSection> EfficiencyCpuSections => this.GetCpuSections(CpuCoreKind.Efficiency);
    public IEnumerable<CpuTableSection> UnknownCpuSections => this.GetCpuSections(CpuCoreKind.Unknown);
    public ObservableCollection<ChartComparisonPoint> ChartPoints { get; } = [];
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

    public string BenchmarkRatioSummaryText {
        get => this.benchmarkRatioSummaryText;
        private set => this.SetProperty(ref this.benchmarkRatioSummaryText,value);
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

    public string GeneratedCodeAuditText {
        get => this.generatedCodeAuditText;
        private set => this.SetProperty(ref this.generatedCodeAuditText,value);
    }

    public bool CanCopyImprovementPrompt => !string.IsNullOrWhiteSpace(this.ImprovementPromptText);

    public bool IsBenchmarkRunning {
        get => this.isBenchmarkRunning;
        private set {
            if(this.SetProperty(ref this.isBenchmarkRunning,value)) {
                this.RunCommand.NotifyCanExecuteChanged();
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

    public string RunButtonText => this.IsBenchmarkPaused ? "再開" : "実行";

    public bool CanResumeBenchmark => !this.IsRefreshing&&this.IsBenchmarkPaused;

    public bool CanRunBenchmark =>
        !this.IsRefreshing&&!this.IsBenchmarkRunning&&this.HasSelectedBenchmarkCells;

    public bool CanUseRunToolbarButton => this.CanRunBenchmark||this.CanResumeBenchmark;

    public bool HasSelectedBenchmarkCells {
        get => this.hasSelectedBenchmarkCells;
        private set {
            if(this.SetProperty(ref this.hasSelectedBenchmarkCells,value)) {
                this.RunCommand.NotifyCanExecuteChanged();
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
        while(this.IsBenchmarkRunActive) {
            await Task.Delay(100);
        }
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
            if(this.IsRefreshing||value==this.selectedSourceTab) {
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
        this.isApplyingSourceTabSelection=true;
        try {
            if(this.SetProperty(ref this.selectedSourceTab,option)) {
                this.ApplySourceTabVisualSelection();
                this.OnPropertyChanged(nameof(this.SelectedComparisonPair));
                this.OnPropertyChanged(nameof(this.SelectedSourceDefinition));
                this.OnPropertyChanged(nameof(this.ChartLeftLegendLabel));
                this.OnPropertyChanged(nameof(this.ChartRightLegendLabel));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
                this.RefreshBenchmarkProjectHeaderTexts();
                this.ClearRetainedChartPoints();
                this.UpdateChart(false);
                _=this.RefreshAsync();
                this.RefreshSourceTabProgresses();
            }
        }
        finally {
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
                this.UpdateChart();
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
                this.UpdateChart();
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
        get => this.chartHeight;
        private set => this.SetProperty(ref this.chartHeight,value);
    }

    public async Task InitializeAsync() {
        await this.RefreshAsync();
    }

    public void SortBenchmarkRows(BenchmarkGridSortColumn column) {
        if(this.CurrentSortColumn==column) {
            this.CurrentSortDescending=!this.CurrentSortDescending;
        }
        else {
            this.CurrentSortColumn=column;
            this.CurrentSortDescending=false;
        }

        this.ApplyCurrentSort();
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

        this.SetBenchmarkCells(this.BenchmarkRows.SelectMany(static row => row.ProjectCells),this.IsAllBenchmarkSelectionChecked is false);
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

        var selectedRows = this.BenchmarkRows
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

    internal void SelectAllBenchmarkUiCells() {
        this.ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget.CornerHeader(),BenchmarkCellSelectionGesture.Replace);
    }

    internal void SelectBenchmarkCell(BenchmarkTableRow row,string project,BenchmarkCellSelectionGesture gesture) {
        var rowIndex = this.BenchmarkRows.IndexOf(row);
        var columnIndex = this.GetProjectColumnIndex(project);
        if(rowIndex<0||columnIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget.Cell(rowIndex,columnIndex),gesture);
    }

    internal void SelectBenchmarkRow(BenchmarkTableRow row,BenchmarkCellSelectionGesture gesture) {
        var rowIndex = this.BenchmarkRows.IndexOf(row);
        if(rowIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget.RowHeader(rowIndex),gesture);
    }

    internal void SelectBenchmarkColumnGroup(BenchmarkColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture) {
        var columns = group.ColumnKeys
            .Select(this.GetProjectColumnIndex)
            .Where(static index => index>=0)
            .Order()
            .ToArray();
        if(columns.Length==0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget.ColumnHeader(columns[0],columns[^1]),gesture);
    }

    internal void ToggleBenchmarkCellCheck(BenchmarkTableRow row,string project) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        if(row.GetProjectCell(project) is not { } cell) {
            return;
        }

        var selectedCells = this.BenchmarkRows
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

    internal void SelectBenchmarkCellFromChart(string benchmark,string project,BenchmarkCellSelectionGesture gesture) {
        var rowIndex = this.GetBenchmarkRowIndex(benchmark);
        var columnIndex = this.GetProjectColumnIndex(project);
        if(rowIndex<0||columnIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget.Cell(rowIndex,columnIndex),gesture);
    }

    internal void SelectBenchmarkRowFromChart(string benchmark,BenchmarkCellSelectionGesture gesture) {
        var rowIndex = this.GetBenchmarkRowIndex(benchmark);
        if(rowIndex<0) {
            return;
        }

        this.ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget.RowHeader(rowIndex),gesture);
    }

    private async Task RefreshAsync() {
        this.IsRefreshing=true;
        this.StatusText="読み込み中";
        StartupDiagnostics.Write("Benchmark catalog refresh started.");
        try {
            await this.RefreshDbRunListAsync(CancellationToken.None,true);
            this.CaptureCurrentBenchmarkSelections();
            var loaded = await this.catalogService.LoadAsync(CancellationToken.None);
            this.catalogCases=loaded;
            this.RefreshBenchmarkCaseIndex(loaded);
            this.BenchmarkRows.Clear();
            this.benchmarkSelectionAnchor=null;
            foreach(var row in this.CreateMethodRows(loaded)) {
                this.SubscribeBenchmarkRow(row);
                row.IsSelected=this.benchmarkRowSelections.GetValueOrDefault(
                    CreateRowSelectionKey(row.SourceTab,row.RowKey),
                    false);
                foreach(var cell in row.ProjectCells) {
                    var key = CreateBenchmarkCellKey(row.SourceTab,row.RowKey,cell.Project);
                    if(this.benchmarkCellSelections.TryGetValue(key,out var saved)) {
                        cell.IsSelected=saved;
                    }
                    else {
                        cell.IsSelected=false;
                    }
                }
                this.BenchmarkRows.Add(row);
            }
            this.RestoreRunSelection(null,null);
            this.RefreshBenchmarkSelectionAggregates();
            if(!this.RestoreCurrentRunDisplayForSelectedTab()) {
                this.RefreshVisibleResults();
                this.ApplySelectedElementCountToTable();
                this.RefreshImprovementPrompt();
            }
            this.RefreshSourceTabProgresses();
            this.ApplyCurrentSort();
            this.StatusText=this.BenchmarkRows.Count+" 件のベンチマーク";
            StartupDiagnostics.Write($"Benchmark catalog loaded. Rows={this.BenchmarkRows.Count}");
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark catalog load failed.",ex);
            this.StatusText="読み込み失敗: "+ex.Message;
        }
        finally {
            this.IsRefreshing=false;
        }
    }

    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases) {
        return BenchmarkRowGroupingLogic.CreateRows(cases,this.SelectedSourceDefinition,this.SelectedResultElementCount?.Value??0)
            .Select(row => new BenchmarkTableRow(
                row.RowKey,
                row.MethodName,
                row.BenchmarkName,
                this.SelectedSourceDefinition,
                row.CasesByProject))
            .ToArray();
    }

    private void RefreshBenchmarkCaseIndex(IReadOnlyList<BenchmarkCase> cases) {
        this.benchmarkCasesByProjectMethod.Clear();
        foreach(var benchmark in cases) {
            this.benchmarkCasesByProjectMethod[CreateBenchmarkCaseKey(benchmark.MethodName,benchmark.Project,benchmark.TypeFullName,benchmark.ArgumentKey)]=benchmark;
        }
    }

    private void SetAllBenchmarkSelection(bool selected) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        this.SetBenchmarkCells(this.BenchmarkRows.SelectMany(static row => row.ProjectCells),selected);
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
                }
            };
        }
    }

    private IEnumerable<BenchmarkProjectCell> GetBenchmarkCells(IReadOnlyList<string> projectKeys) {
        foreach(var row in this.BenchmarkRows) {
            foreach(var project in projectKeys) {
                var resolvedProject = this.ResolveProjectKey(project);
                if(row.GetProjectCell(resolvedProject) is { } cell) {
                    yield return cell;
                }
            }
        }
    }

    private void SetBenchmarkCells(IEnumerable<BenchmarkProjectCell> cells,bool selected) {
        if(!this.CanChangeBenchmarkSelection) {
            return;
        }

        var targets = cells.Where(static cell => cell.HasCase).Distinct().ToArray();
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
    }

    private void ApplyBenchmarkCellSelection(BenchmarkCellSelectionTarget target,BenchmarkCellSelectionGesture gesture) {
        var result = BenchmarkCellSelectionLogic.Apply(
            this.BenchmarkRows.Count,
            this.SelectedSourceDefinition.ProjectOrder.Count,
            this.GetSelectedBenchmarkUiCoordinates(),
            this.benchmarkSelectionAnchor,
            target,
            gesture);
        var selected = result.SelectedCells;
        for(var rowIndex = 0; rowIndex<this.BenchmarkRows.Count; rowIndex++) {
            var row = this.BenchmarkRows[rowIndex];
            for(var columnIndex = 0; columnIndex<this.SelectedSourceDefinition.ProjectOrder.Count; columnIndex++) {
                if(row.GetProjectCell(this.SelectedSourceDefinition.ProjectOrder[columnIndex]) is { } cell) {
                    cell.IsUiSelected=selected.Contains(new BenchmarkCellCoordinate(rowIndex,columnIndex));
                }
            }
        }
        this.benchmarkSelectionAnchor=result.Anchor;
        this.RefreshChartUiSelection();
    }

    private IEnumerable<BenchmarkCellCoordinate> GetSelectedBenchmarkUiCoordinates() {
        for(var rowIndex = 0; rowIndex<this.BenchmarkRows.Count; rowIndex++) {
            var row = this.BenchmarkRows[rowIndex];
            for(var columnIndex = 0; columnIndex<this.SelectedSourceDefinition.ProjectOrder.Count; columnIndex++) {
                if(row.GetProjectCell(this.SelectedSourceDefinition.ProjectOrder[columnIndex]) is { IsUiSelected: true }) {
                    yield return new BenchmarkCellCoordinate(rowIndex,columnIndex);
                }
            }
        }
    }

    private void RefreshBenchmarkSelectionAggregates() {
        foreach(var row in this.BenchmarkRows) {
            row.RefreshSelectionState();
        }
        foreach(var group in this.BenchmarkColumnSelectionGroups) {
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys));
        }
        foreach(var group in this.BenchmarkProjectSelectionGroups) {
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys));
        }
        var allCells = this.BenchmarkRows.SelectMany(static row => row.ProjectCells).ToArray();
        this.IsAllBenchmarkSelectionChecked=GetSelectionState(allCells);
        this.HasSelectedBenchmarkCells=this.HasAnySelectedBenchmarkCell();
        this.RefreshSourceTabProgresses();
    }

    private bool HasAnySelectedBenchmarkCell() =>
        this.SourceTabs.Any(option => this.CreateProgressCells(option.Definition).Any(static cell => cell.IsSelected));

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

    private int GetBenchmarkRowIndex(string benchmark,string rowKey = "") {
        for(var index=0;index<this.BenchmarkRows.Count;index++) {
            var row = this.BenchmarkRows[index];
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

    private int GetProjectColumnIndex(string project) =>
        this.SelectedSourceDefinition.ProjectOrder
            .Select((item,index) => new { item,index })
            .FirstOrDefault(item => string.Equals(item.item,this.ResolveProjectKey(project),StringComparison.OrdinalIgnoreCase))
            ?.index??-1;

    private string ResolveProjectKey(string project) =>
        project switch {
            "Left" => this.SelectedSourceDefinition.LeftProject,
            "Right" => this.SelectedSourceDefinition.RightProject,
            _ => project,
        };

    private async Task RefreshDbRunListAsync(CancellationToken cancellationToken,bool loadSelected) {
        var options = await Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            this.uiRunStore.Initialize();
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
        foreach(var row in this.BenchmarkRows) {
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

    private IReadOnlyList<BenchmarkCase> CreateSelectedBenchmarkCasesFromAllTabs() {
        this.CaptureCurrentBenchmarkSelections();
        return this.SourceTabs
            .SelectMany(option => this.CreateSelectedBenchmarkCases(option.Definition))
            .GroupBy(static benchmark => string.Join("|",benchmark.Project,benchmark.AssemblyName,benchmark.TypeFullName,benchmark.MethodName,benchmark.ArgumentKey),StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private IEnumerable<BenchmarkCase> CreateSelectedBenchmarkCases(BenchmarkSourceTabDefinition sourceTab) {
        if(sourceTab.Tab==this.SelectedSourceDefinition.Tab&&this.BenchmarkRows.Count>0) {
            return this.BenchmarkRows.SelectMany(static row => row.GetRunCases());
        }

        return BenchmarkRowGroupingLogic.CreateRows(this.catalogCases,sourceTab)
            .SelectMany(row => sourceTab.ProjectOrder.Select(project => new {
                Row=row,
                Project=project,
            }))
            .Where(item =>
                item.Row.CasesByProject.ContainsKey(item.Project)
                &&this.benchmarkCellSelections.TryGetValue(
                    CreateBenchmarkCellKey(sourceTab,item.Row.RowKey,item.Project),
                    out var selected)
                &&selected)
            .Select(item => item.Row.CasesByProject[item.Project]);
    }

    private static string CreateRowSelectionKey(BenchmarkSourceTabDefinition sourceTab,string rowKey) =>
        sourceTab.Id+"|"+rowKey;

    internal static string CreateBenchmarkCellSelectionKey(BenchmarkSourceTabDefinition sourceTab,string method,string project) =>
        sourceTab.Id+"|"+method+"|"+project;

    private static string CreateBenchmarkCellKey(BenchmarkSourceTabDefinition sourceTab,string method,string project) =>
        CreateBenchmarkCellSelectionKey(sourceTab,method,project);

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
    }

    public void DiscardPendingCloseStateSave() {
        this.HasPendingCloseStateSave=false;
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
            foreach(var row in this.BenchmarkRows) {
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
        }
    }

    public void DiscardUnsavedBenchmarkResults() {
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
            result => this.FindBenchmarkCase(result.Method,result.Project,result.TypeFullName,result.ArgumentKey));
    }

    private BenchmarkCase? FindBenchmarkCase(string method,string project,string typeFullName = "",string argumentKey = "") {
        if(this.benchmarkCasesByProjectMethod.TryGetValue(CreateBenchmarkCaseKey(method,project,typeFullName,argumentKey),out var benchmark)) {
            return benchmark;
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

    private BenchmarkTableRow? FindBenchmarkRow(BenchmarkResultItem result) =>
        this.BenchmarkRows.FirstOrDefault(row => IsResultForRow(row,result))
        ??(IsScalableSourceTab(result.SourceTab) ? null : this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.MethodName,result.Method,StringComparison.OrdinalIgnoreCase)
            &&row.GetProjectCell(result.Project) is { HasCase: true }));

    private BenchmarkTableRow? FindBenchmarkRow(BenchmarkJob job) =>
        this.BenchmarkRows.FirstOrDefault(row => IsJobForRow(row,job))
        ??(IsScalableSourceTab(job.SourceTab) ? null : this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.MethodName,job.MethodName,StringComparison.OrdinalIgnoreCase)
            &&row.GetProjectCell(job.Project) is { HasCase: true }));

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

    private string CreateUnsavedRunRoot() {
        var profileRoot = this.GetBenchmarkRoot(this.SelectedExecutionProfile.Profile);
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

    private async Task RunAsync(string? runRootOverride) {
        this.RefreshCurrentRunBenchmarkSelectionSnapshot();
        var selected = this.GetCurrentRunSelectedBenchmarkCases().ToArray();
        if(selected.Length==0) {
            this.StatusText="ベンチマークが選択されていません";
            StartupDiagnostics.Write("Benchmark run aborted: no benchmark selected.");
            return;
        }

        this.DiscardUnsavedBenchmarkResults();
        this.RemoveTransientRunOptions();
        this.loadedExternalState=null;
        this.SelectedTab=BenchmarkTab.Cpu;
        this.SelectedLowerTab=BenchmarkLowerTab.Cpu;
        this.runCancellation?.Cancel();
        this.runCancellation=new CancellationTokenSource();
        this.pendingRunTerminationKind=RunTerminationKind.None;
        var runRoot = runRootOverride??this.CreateUnsavedRunRoot();
        this.currentUnsavedRunRoot=runRoot;
        this.SetActiveRunRoot(runRoot);
        var runGeneration = unchecked(++this.runResultGeneration);
        var runSourceTab = this.SelectedSourceDefinition.Tab;
        var uiRunId = runRootOverride is not null&&this.activeUiRunId is not null
            ? this.activeUiRunId
            : await this.CreateUiRunAsync(runSourceTab);
        this.activeUiRunId=uiRunId;
        await this.SetUiRunStatusAsync(uiRunId,UiRunStatus.Running,"Benchmark run started.");
        string? completedRunRoot = null;
        var completedNormally = false;
        var keepPausedRunRoot = false;
        this.IsRunning=true;
        this.ActiveRunSourceTab=runSourceTab;
        this.ClearSourceTabRunningStates();
        this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
        this.activeChartMethodName=string.Empty;
        this.PrepareSelectedSourceLiveResultsForRun();
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
            +"ExecutionProfile="+this.SelectedExecutionProfile.Profile+" "
            +"LogicalCpus=["+string.Join(",",this.CpuItems.Where(static cpu => cpu.IsSelected).Select(static cpu => cpu.LogicalProcessorIndex))+"] "
            +"Benchmarks="+selected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "
            +"ElementCounts=["+string.Join(",",this.GetSelectedElementCounts())+"]");
        try {
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
                BenchmarkRunMode.Adaptive,
                this.SelectedExecutionProfile.Profile,
                this.GetAdaptiveThreshold(),
                true,
                runRoot,
                this.GetCurrentRunSelectedBenchmarkCases,
                result => MainThread.BeginInvokeOnMainThread(() => {
                    this.BufferRunResult(result,runGeneration,uiRunId);
                }),
                status => {
                    StartupDiagnostics.Write(status);
                    MainThread.BeginInvokeOnMainThread(() => this.StatusText=status);
                },
                this.runCancellation.Token);
            completedRunRoot=report.RunRoot;
            this.currentUnsavedRunRoot=report.RunRoot;
            completedNormally=true;
            await this.SetUiRunStatusAsync(uiRunId,UiRunStatus.Completed,"Benchmark run completed.");
            StartupDiagnostics.Write(
                "Benchmark run completed. "
                +"RunRoot="+report.RunRoot);
        }
        catch(OperationCanceledException) {
            if(this.pendingRunTerminationKind==RunTerminationKind.Pause) {
                this.StatusText="一時停止";
                await this.SetUiRunStatusAsync(uiRunId,UiRunStatus.Paused,"Benchmark run paused.");
                StartupDiagnostics.Write("Benchmark run paused.");
                this.SetActiveRunRoot(completedRunRoot ?? runRoot);
                keepPausedRunRoot=true;
            }else{
                this.StatusText="キャンセルしました";
                await this.SetUiRunStatusAsync(uiRunId,UiRunStatus.Stopped,"Benchmark run canceled.");
                StartupDiagnostics.Write("Benchmark run canceled.");
                this.SetActiveRunRoot(null);
            }
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run failed.",ex);
            this.StatusText="実行失敗: "+ex.Message;
            await this.SetUiRunStatusAsync(uiRunId,UiRunStatus.Failed,ex.Message);
            this.SetActiveRunRoot(null);
        }
        finally {
            this.FlushBufferedRunResults();
            if(!completedNormally&&!keepPausedRunRoot) {
                this.runResultGeneration=unchecked(this.runResultGeneration+1);
            }
            this.IsRunning=false;
            this.activeChartMethodName=string.Empty;
            this.ActiveRunSourceTab=null;
            this.OnPropertyChanged(nameof(this.CanChangeBenchmarkSelection));
            this.runCancellation?.Dispose();
            this.runCancellation=null;
            this.pendingRunTerminationKind=RunTerminationKind.None;
            this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
            if(completedNormally&&this.TryGetRemainingRunningVisualDelay(out var finalVisualDelay)) {
                this.ScheduleFinalLiveVisualCleanup(runGeneration,finalVisualDelay);
            }else{
                this.ApplyFinalLiveVisualCleanup();
            }
            if(!keepPausedRunRoot) {
                this.SetActiveRunRoot(null);
                if(this.activeUiRunId==uiRunId) {
                    this.activeUiRunId=null;
                }
            }
            this.RefreshUnsavedRunSelection();
            this.ClearRunResultHighlights();
            if(completedNormally) {
                this.ApplyFinalBenchmarkSort();
            }
            this.RefreshImprovementPrompt();
            this.UpdateChart();
        }
    }

    private async Task<long?> CreateUiRunAsync(BenchmarkSourceTab runSourceTab) {
        var executionProfile = this.SelectedExecutionProfile.Profile.ToString();
        var displayName = BenchmarkAppProfile.Title+" "+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture);
        return await this.uiRunRepository.CreateRunAsync(GetUiRunAppKind(),displayName,executionProfile,runSourceTab);
    }

    private async Task SetUiRunStatusAsync(long? runId,UiRunStatus status,string message) {
        await this.uiRunRepository.SetRunStatusAsync(runId,status,message);
    }

    private BenchmarkViewStateSnapshot CreateBenchmarkViewStateSnapshot() {
        var state = this.CreateProgressStateFromAllTabResults(this.currentUnsavedRunRoot??string.Empty);
        return new(
            JsonSerializer.Serialize(state),
            this.SelectedSourceDefinition.Tab.ToString(),
            this.SelectedResultElementCount?.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
    }

    private void QueueBenchmarkResultSave(BenchmarkResultItem result,long? runId) {
        var rowKey = this.CreateResultRowKey(result);
        this.uiRunRepository.QueueSaveResult(result,runId,rowKey,this.CreateBenchmarkViewStateSnapshot());
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

    private void BufferRunResult(BenchmarkResultItem result,int runGeneration,long? uiRunId) {
        if(runGeneration!=this.runResultGeneration) {
            return;
        }

        var isRunningResult = string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase);
        var isCompletedResult = string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase);
        var isFailedResult = string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase);
        if(isRunningResult) {
            this.FindAssignedCpu(result)?.ApplyJob(result);
            this.ApplySourceTabRunningResult(result);
            this.ApplyImmediateRunningResult(result);
        }else if(isCompletedResult||isFailedResult){
            // Terminal display is applied by the 250ms flush so very short jobs still show the running spinner.
        }
        if(isRunningResult) {
            this.MarkActiveRunSourceTab(result.SourceTab);
        }

        this.pendingRunResults.Enqueue(new PendingRunResult(result,runGeneration,uiRunId));
        if(this.isRunResultFlushScheduled) {
            return;
        }

        this.isRunResultFlushScheduled=true;
        _=Task.Run(async () => {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            MainThread.BeginInvokeOnMainThread(this.FlushBufferedRunResults);
        });
    }

    private void FlushBufferedRunResults() {
        if(this.pendingRunResults.Count==0) {
            this.isRunResultFlushScheduled=false;
            return;
        }

        var pending = this.pendingRunResults.ToArray();
        this.pendingRunResults.Clear();
        this.isRunResultFlushScheduled=false;
        foreach(var item in pending) {
            this.HandleRunResult(item.Result,item.RunGeneration,item.UiRunId);
        }
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
        var hasResultElementCount = result.AxisCount>0;
        var terminalVisualDelay = TimeSpan.Zero;
        var shouldDelayTerminalVisual = isTerminalResult&&this.TryGetTerminalVisualDelay(result,out terminalVisualDelay);
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
            this.RefreshSourceTabProgresses();
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
            this.RefreshSourceTabProgresses();
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
            this.RefreshSourceTabProgresses();
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
            this.QueueBenchmarkResultSave(result,uiRunId);
        }
    }

    private void ApplyImmediateRunningResult(BenchmarkResultItem result) {
        this.MarkRunningVisualStarted(result);
        if(result.AxisCount>0) {
            this.UpsertCurrentRunResultItem(result);
            this.RefreshSourceTabProgresses();
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
            MainThread.BeginInvokeOnMainThread(() => {
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
        if(result.SourceTab==this.SelectedSourceDefinition.Tab) {
            this.FindBenchmarkRow(result)?.ApplyRunResult(result,this.FindAssignedCpu(result),DateTimeOffset.Now,false);
        }
    }

    private void ScheduleFinalLiveVisualCleanup(int runGeneration,TimeSpan delay) {
        _=Task.Run(async () => {
            await Task.Delay(delay);
            MainThread.BeginInvokeOnMainThread(() => {
                if(runGeneration!=this.runResultGeneration) {
                    return;
                }

                this.ApplyFinalLiveVisualCleanup();
            });
        });
    }

    private void ApplyFinalLiveVisualCleanup() {
        this.runningVisualStartedAt.Clear();
        this.ClearSourceTabRunningStates();
        this.ApplySelectedElementCountToTable();
        this.RestoreLatestCompletedLiveResult();
        this.ClearCpuJobs();
    }

    private static string CreateRunningVisualKey(BenchmarkResultItem result) =>
        result.SourceTab.ToString()
        +"|"+result.Project
        +"|"+result.TypeFullName
        +"|"+result.Method
        +"|"+result.ArgumentKey
        +"|"+result.AxisCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
            .Concat(this.ResultItems.Where(item => sourceTab.ContainsProject(item.Project)))
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
        foreach(var item in this.GetCurrentRunResultItems(BenchmarkSourceTabs.TryGetByProject(result.Project)?.Tab??this.SelectedSourceDefinition.Tab)
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
        foreach(var item in this.runProjectionService.GetVisibleResults(this.ResultItems,selectedN)) {
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
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.BenchmarkRatioSummaryText=string.Empty;
        this.ImprovementPromptText=string.Empty;
        this.UpdateChart(false);
        this.RefreshSourceTabProgresses();
        this.RefreshToolbarCommandStates();
    }

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
        foreach(var benchmark in this.BenchmarkRows) {
            benchmark.ApplyComparison(this.FindComparisonRow(benchmark),this.IsBenchmarkRunActive||this.HasCurrentRunResultsForSelectedTab);
        }
        this.RefreshBenchmarkRatioSummary();
    }

    private BenchmarkComparisonRow? FindComparisonRow(BenchmarkTableRow row) {
        foreach(var item in this.comparisonRows) {
            if(IsComparisonRowMatch(row,item)) {
                return item;
            }
        }

        return null;
    }

    private void RefreshBenchmarkRatioSummary() {
        this.BenchmarkRatioSummaryText=BenchmarkImprovementPromptBuilder.CreateSummary(this.comparisonRows).DisplayText;
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
        return "候補 "+selected.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

    private void RefreshSourceTabProgresses() {
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

        if(sourceTab.Tab==this.SelectedSourceDefinition.Tab&&this.BenchmarkRows.Count>0) {
            return this.BenchmarkRows
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
        return this.GetCurrentRunResultItems(sourceTab.Tab)
            .Concat(this.ResultItems.Where(item => sourceTab.ContainsProject(item.Project)))
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

        foreach(var row in this.BenchmarkRows) {
            row.RetainCompletedHighlightsForRunningElement(runningElementCount);
        }
    }

    private void ClearRunResultHighlights() {
        foreach(var row in this.BenchmarkRows) {
            row.ClearRunHighlights();
        }
    }

    private void ClearBenchmarkResultDisplays() {
        foreach(var row in this.BenchmarkRows) {
            row.ClearResultDisplay();
        }
        this.ClearFinalCompletionStates();
    }

    private void LoadSelectedRunFromDatabase(BenchmarkRunOption run) {
        try {
            this.ClearSelectedRunDisplay();
            foreach(var result in this.uiRunRepository.LoadResultItems(run.UiRunId,run.UpdatedAt,this.SelectedSourceDefinition)) {
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
            this.comparisonRows=this.SelectedResultElementCount is { } selectedElementCount
                ? this.BuildDisplayedRowsForElementCount(this.SelectedSourceDefinition,selectedElementCount.Value)
                : this.BuildDisplayedRows(this.SelectedSourceDefinition);
            this.ApplyComparisonRowsToTable();
            this.ApplySelectedElementCountToTable();
            this.RefreshImprovementPrompt();
            this.UpdateChart(false);
            this.ApplyCurrentSort();
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run DB load failed.",ex);
            this.StatusText="DB 読み込み失敗: "+ex.Message;
        }
    }

    private void LoadSelectedRunFromDatabase() {
        if(this.isLoadingSelectedRunDb) {
            return;
        }

        this.isLoadingSelectedRunDb=true;
        try {
            var run = this.SelectedRunOption;
            if(this.RestoreCurrentRunDisplayForSelectedTab()) {
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
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run DB load failed.",ex);
            this.StatusText="DB 読み込み失敗: "+ex.Message;
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
        foreach(var row in this.BenchmarkRows) {
            row.RefreshBenchmarkName(selectedN??0);
        }
        if(this.HasCurrentRunResultsForSelectedTab) {
            this.comparisonRows=selectedN is { } currentElementCount
                ? this.BuildCurrentRunComparisonRowsForElementCount(currentElementCount)
                : this.BuildCurrentRunComparisonRows();
        }
        else if(IsExternalRunOption(this.SelectedRunOption)) {
            this.comparisonRows=selectedN is { } externalElementCount
                ? this.BuildDisplayedRowsForElementCount(this.SelectedSourceDefinition,externalElementCount)
                : [];
        }
        else {
            if(this.IsBenchmarkRunActive) {
                this.ApplyComparisonRowsToTable();
                return;
            }

            if(!this.isLoadingSelectedRunDb) {
                this.LoadSelectedRunFromDatabase();
                return;
            }
        }
        this.ApplyComparisonRowsToTable();
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

                var leftTypeFullName = GetRowTypeFullName(row,pair.LeftProject);
                var leftArgumentKey = GetRowArgumentKey(row,pair.LeftProject);
                var rightTypeFullName = GetRowTypeFullName(row,pair.RightProject);
                var rightArgumentKey = GetRowArgumentKey(row,pair.RightProject);
                var hasLeftResult = this.HasResult(row.MethodName,pair.LeftProject,leftTypeFullName,selectedElementCount,leftArgumentKey);
                var hasRightResult = this.HasResult(row.MethodName,pair.RightProject,rightTypeFullName,selectedElementCount,rightArgumentKey);
                if(hasLeftResult&&hasRightResult) {
                    continue;
                }

                var leftProgressCell = CreateProgressCell(row,pair.LeftProject,true);
                var rightProgressCell = CreateProgressCell(row,pair.RightProject,true);
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
                    if(!hasLeftResult) {
                        this.ApplySkippedCell(row,pair.LeftProject,stoppedAtElementCount);
                    }
                    if(!hasRightResult) {
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
        foreach(var row in this.BenchmarkRows) {
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

    private IReadOnlyList<BenchmarkResultItem> GetResultSourceForDisplay() =>
        (this.IsBenchmarkRunActive||IsUnsavedRunOption(this.SelectedRunOption))&&this.HasCurrentRunResultsForSelectedTab
            ? this.GetCurrentRunResultItemsForSelectedTab()
            : this.ResultItems;

    private ProgressState? LoadSelectedRunState() {
        var run = this.SelectedRunOption;
        if(run is null||IsNewRunOption(run)||IsTransientRunOption(run)||run.UiRunId<=0) {
            return null;
        }

        try {
            var snapshot = this.uiRunStore.ListUiViewStates(run.UiRunId)
                .FirstOrDefault(static item => string.Equals(item.StateKey,"BenchmarkProgressStateJson",StringComparison.OrdinalIgnoreCase))
                ?.StateValue;
            return string.IsNullOrWhiteSpace(snapshot)
                ? null
                : JsonSerializer.Deserialize<ProgressState>(snapshot);
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
        var elementComparisonRows = this.BuildElementComparisonRows();
        var points = this.BuildBenchmarkChartPoints(elementComparisonRows);
        this.SyncChartPoints(points);
        this.ChartHeight=Math.Max(BenchmarkChartMinimumHeight,points.Count*BenchmarkChartRowHeight+BenchmarkChartVerticalPadding);
        this.RefreshDiagnosticViews(elementComparisonRows);
    }

    private void RefreshDiagnosticViews(IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.SyncDiagnosticRows(this.LatencyHotspotRows,this.BuildMetricDiagnosticRowsReadable(rows,BenchmarkMetricKind.Time));
        this.SyncDiagnosticRows(this.AllocationDiagnosticRows,this.BuildMetricDiagnosticRowsReadable(rows,BenchmarkMetricKind.Allocated));
        this.SyncDiagnosticRows(this.CpuTimelineRows,this.BuildCpuTimelineRows());
        this.GeneratedCodeAuditText=this.CreateGeneratedCodeAuditTextReadable();
    }

    private IReadOnlyList<BenchmarkDiagnosticRow> BuildMetricDiagnosticRowsReadable(IReadOnlyList<BenchmarkComparisonRow> rows,BenchmarkMetricKind metric) {
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
                metric==BenchmarkMetricKind.Time ? "遅延" : "割当",
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
                metric==BenchmarkMetricKind.Time ? "遅延" : "割当",
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

    private string CreateGeneratedCodeAuditTextReadable() {
        var selected = this.BenchmarkRows.FirstOrDefault(static row => row.IsUiSelected)
            ??this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.RowKey,this.activeChartMethodName,StringComparison.OrdinalIgnoreCase))
            ??this.BenchmarkRows.FirstOrDefault();
        if(selected is null) {
            return "対象メソッドを選択すると、生成コード監査を表示します。";
        }

        var sourceTab = this.SelectedSourceDefinition;
        var benchmark = selected.CasesByProject.TryGetValue(sourceTab.LeftProject,out var leftBenchmark)
            ? leftBenchmark
            : selected.CasesByProject.Values.FirstOrDefault();
        if(benchmark is null) {
            return "対象メソッド: "+selected.MethodName+"\n比較ペア: "+sourceTab.LeftProject+" vs "+sourceTab.RightProject+"\n監査対象のBenchmarkCaseが見つかりません。";
        }

        var cacheKey = "readable|"+benchmark.AssemblyName+"|"+selected.MethodName+"|"+benchmark.TypeFullName+"|"+benchmark.ArgumentKey;
        if(this.generatedCodeAuditCache.TryGetValue(cacheKey,out var cached)) {
            return cached;
        }

        var audit = this.CreateGeneratedCodeAuditTextReadable(selected,benchmark);
        this.generatedCodeAuditCache[cacheKey]=audit;
        return audit;
    }

    private string CreateGeneratedCodeAuditTextReadable(BenchmarkTableRow row,BenchmarkCase benchmark) {
        var lines = new List<string>();
        lines.Add("対象メソッド: "+row.MethodName);
        lines.Add("比較ペア: "+this.SelectedSourceDefinition.LeftProject+" vs "+this.SelectedSourceDefinition.RightProject);
        lines.Add("監査対象: "+benchmark.Project+" / "+benchmark.AssemblyName+" / "+benchmark.TypeFullName);
        var generatedFile = this.FindGeneratedCodeFile(benchmark.AssemblyName,row.MethodName);
        if(generatedFile is null) {
            lines.Add("生成コード: 注意 generated-current の .g.cs が見つかりません。");
            lines.Add("確認: BenchmarkプロジェクトをReleaseまたはDebugでビルドしてから再確認してください。");
            return string.Join(Environment.NewLine,lines);
        }

        var text = File.ReadAllText(generatedFile);
        var block = ExtractGeneratedMethodBlock(text,row.MethodName);
        lines.Add("生成ファイル: "+Path.GetFileName(generatedFile));
        if(string.IsNullOrWhiteSpace(block)) {
            lines.Add("intercept: 注意 OptimizedLinq_"+row.MethodName+" が見つかりません。");
            lines.Add("次の確認: メソッド名、引数違い、または生成対象外になっていないか確認してください。");
            return string.Join(Environment.NewLine,lines);
        }

        lines.Add("intercept: OK OptimizedLinq_"+row.MethodName+" を検出");
        lines.Add("System.Linq fallback: "+FormatAuditFlagReadable(!block.Contains("System.Linq.Enumerable.",StringComparison.Ordinal),"System.Linq.Enumerable 呼び出し"));
        lines.Add("delegate呼び出し: "+FormatAuditFlagReadable(!block.Contains(".Invoke(",StringComparison.Ordinal)&&!block.Contains("Func<",StringComparison.Ordinal),".Invoke / Func"));
        lines.Add("struct enumerator: "+FormatAuditFlagReadable(block.Contains("private struct ",StringComparison.Ordinal)&&block.Contains("IValueEnumerator",StringComparison.Ordinal),"struct + IValueEnumerator"));
        lines.Add("direct span: "+FormatAuditFlagReadable(block.Contains(".GetSpan()",StringComparison.Ordinal)||block.Contains("TryGetSpan",StringComparison.Ordinal),"GetSpan / TryGetSpan"));
        lines.Add("Dictionary: "+FormatAuditPresenceReadable(block.Contains("Dictionary<",StringComparison.Ordinal),"Dictionary"));
        lines.Add("HashSet: "+FormatAuditPresenceReadable(block.Contains("HashSet<",StringComparison.Ordinal),"HashSet"));
        lines.Add("chunk配列生成: "+FormatAuditPresenceReadable(block.Contains("new int[",StringComparison.Ordinal)||block.Contains("int[]",StringComparison.Ordinal),"int[]"));
        lines.Add("不変式外出し: Anchor()以外の式は生成形状テストで追加確認してください。");
        lines.Add("判定: OKが不足している項目、または注意が付いた項目を遅延ホットスポット上位から優先調査します。");
        return string.Join(Environment.NewLine,lines);
    }

    private static string FormatAuditFlagReadable(bool ok,string label) =>
        ok ? "OK "+label : "注意 "+label+" が残っています";

    private static string FormatAuditPresenceReadable(bool exists,string label) =>
        exists ? "あり "+label : "なし "+label;

    private string CreateGeneratedCodeAuditText() {
        var selected = this.BenchmarkRows.FirstOrDefault(static row => row.IsUiSelected)
            ??this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.RowKey,this.activeChartMethodName,StringComparison.OrdinalIgnoreCase))
            ??this.BenchmarkRows.FirstOrDefault();
        if(selected is null) {
            return "対象メソッドを選択すると、生成コード監査を表示します。";
        }

        var sourceTab = this.SelectedSourceDefinition;
        var benchmark = selected.CasesByProject.TryGetValue(sourceTab.LeftProject,out var leftBenchmark)
            ? leftBenchmark
            : selected.CasesByProject.Values.FirstOrDefault();
        if(benchmark is null) {
            return "対象メソッド: "+selected.MethodName+"\n比較ペア: "+sourceTab.LeftProject+" vs "+sourceTab.RightProject+"\n監査対象のBenchmarkCaseが見つかりません。";
        }

        var cacheKey = benchmark.AssemblyName+"|"+selected.MethodName+"|"+benchmark.TypeFullName+"|"+benchmark.ArgumentKey;
        if(this.generatedCodeAuditCache.TryGetValue(cacheKey,out var cached)) {
            return cached;
        }

        var audit = this.CreateGeneratedCodeAuditText(selected,benchmark);
        this.generatedCodeAuditCache[cacheKey]=audit;
        return audit;
    }

    private string CreateGeneratedCodeAuditText(BenchmarkTableRow row,BenchmarkCase benchmark) {
        var lines = new List<string>();
        lines.Add("対象メソッド: "+row.MethodName);
        lines.Add("比較ペア: "+this.SelectedSourceDefinition.LeftProject+" vs "+this.SelectedSourceDefinition.RightProject);
        lines.Add("監査対象: "+benchmark.Project+" / "+benchmark.AssemblyName+" / "+benchmark.TypeFullName);
        var generatedFile = this.FindGeneratedCodeFile(benchmark.AssemblyName,row.MethodName);
        if(generatedFile is null) {
            lines.Add("生成コード: 注意 generated-current の .g.cs が見つかりません。");
            lines.Add("確認: BenchmarkプロジェクトをReleaseまたはDebugでビルドしてから再確認してください。");
            return string.Join(Environment.NewLine,lines);
        }

        var text = File.ReadAllText(generatedFile);
        var block = ExtractGeneratedMethodBlock(text,row.MethodName);
        lines.Add("生成ファイル: "+Path.GetFileName(generatedFile));
        if(string.IsNullOrWhiteSpace(block)) {
            lines.Add("intercept: 注意 OptimizedLinq_"+row.MethodName+" が見つかりません。");
            lines.Add("次の確認: メソッド名、引数違い、または生成対象外になっていないか確認してください。");
            return string.Join(Environment.NewLine,lines);
        }

        lines.Add("intercept: OK OptimizedLinq_"+row.MethodName+" を検出");
        lines.Add("System.Linq fallback: "+FormatAuditFlag(!block.Contains("System.Linq.Enumerable.",StringComparison.Ordinal),"System.Linq.Enumerable 呼び出し"));
        lines.Add("delegate呼び出し: "+FormatAuditFlag(!block.Contains(".Invoke(",StringComparison.Ordinal)&&!block.Contains("Func<",StringComparison.Ordinal),".Invoke / Func"));
        lines.Add("struct enumerator: "+FormatAuditFlag(block.Contains("private struct ",StringComparison.Ordinal)&&block.Contains("IValueEnumerator",StringComparison.Ordinal),"struct + IValueEnumerator"));
        lines.Add("direct span: "+FormatAuditFlag(block.Contains(".GetSpan()",StringComparison.Ordinal)||block.Contains("TryGetSpan",StringComparison.Ordinal),"GetSpan / TryGetSpan"));
        lines.Add("Dictionary: "+FormatAuditPresence(block.Contains("Dictionary<",StringComparison.Ordinal),"Dictionary"));
        lines.Add("HashSet: "+FormatAuditPresence(block.Contains("HashSet<",StringComparison.Ordinal),"HashSet"));
        lines.Add("chunk配列生成: "+FormatAuditPresence(block.Contains("new int[",StringComparison.Ordinal)||block.Contains("int[]",StringComparison.Ordinal),"int[]"));
        lines.Add("不変式外出し: Anchor()以外の式は生成形状テストで追加確認してください。");
        lines.Add("判定: OKが不足している項目、または注意が付いた項目を遅延ホットスポット上位から優先調査します。");
        return string.Join(Environment.NewLine,lines);
    }

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

    private static string FormatAuditFlag(bool ok,string label) =>
        ok ? "OK "+label : "注意 "+label+" が残っています";

    private static string FormatAuditPresence(bool exists,string label) =>
        exists ? "あり "+label : "なし "+label;

    private string CreateGeneratedCodeAuditTextLegacy() {
        var selected = this.BenchmarkRows.FirstOrDefault(static row => row.IsUiSelected)
            ??this.BenchmarkRows.FirstOrDefault(row => string.Equals(row.RowKey,this.activeChartMethodName,StringComparison.OrdinalIgnoreCase))
            ??this.BenchmarkRows.FirstOrDefault();
        if(selected is null) {
            return "対象メソッドを選択すると、生成コード監査の検索ヒントを表示します。";
        }

        var method = selected.MethodName;
        var sourceTab = this.SelectedSourceDefinition;
        return "対象メソッド: "+method+"\n"
            +"比較ペア: "+sourceTab.LeftProject+" vs "+sourceTab.RightProject+"\n"
            +"検索ヒント: "+method+" / "+selected.BenchmarkName+" / System.Linq.Enumerable / Func.Invoke / IValueEnumerator / GetSpan / TryGetSpan\n"
            +"確認観点: intercept済みメソッド、Enumerable fallback、ラムダのインライン化、delegate呼び出し、struct enumerator、array/list direct span path。\n"
            +"次の作業: generated .g.cs と形状テストを照合し、遅延ホットスポット上位から原因を確定してください。";
    }

    private void SyncChartPoints(IReadOnlyList<ChartComparisonPoint> points) {
        for(var index=0;index<points.Count;index++) {
            var point = points[index];
            if(index<this.ChartPoints.Count) {
                if(this.ChartPoints[index].Benchmark==point.Benchmark
                    &&this.ChartPoints[index].DisplayName==point.DisplayName
                    &&Math.Abs(this.ChartPoints[index].LeftValue-point.LeftValue)<0.000001
                    &&Math.Abs(this.ChartPoints[index].RightValue-point.RightValue)<0.000001
                    &&this.ChartPoints[index].LeftText==point.LeftText
                    &&this.ChartPoints[index].RightText==point.RightText
                    &&this.ChartPoints[index].TooltipText==point.TooltipText
                    &&this.ChartPoints[index].IsLeftUiSelected==point.IsLeftUiSelected
                    &&this.ChartPoints[index].IsRightUiSelected==point.IsRightUiSelected
                    &&this.ChartPoints[index].IsRowUiSelected==point.IsRowUiSelected) {
                    continue;
                }

                this.ChartPoints[index]=point;
            }else {
                this.ChartPoints.Add(point);
            }
        }

        while(this.ChartPoints.Count>points.Count) {
            this.ChartPoints.RemoveAt(this.ChartPoints.Count-1);
        }
    }

    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(IReadOnlyList<BenchmarkComparisonRow> rows) {
        return this.BenchmarkRows
            .Select(row => this.CreateBenchmarkChartPoint(row,rows))
            .Where(static point => point is not null)
            .Select(static point => point!)
            .ToArray();
    }

    private ChartComparisonPoint? CreateBenchmarkChartPoint(
        BenchmarkTableRow row,
        IReadOnlyList<BenchmarkComparisonRow> rows) {
        var comparisonRow = FindComparisonRow(row,rows);
        var left = this.GetChartMetrics(row,this.SelectedComparisonPair.LeftProject,comparisonRow);
        var right = this.GetChartMetrics(row,this.SelectedComparisonPair.RightProject,comparisonRow);
        var leftValue = GetMetricValue(left,this.SelectedMetric.Kind);
        var rightValue = GetMetricValue(right,this.SelectedMetric.Kind);
        var cacheKey = this.GetChartPointCacheKey(row.RowKey);
        if(leftValue is not null&&rightValue is not null) {
            var measuredPoint = this.ApplyChartUiSelection(row,this.CreateChartComparisonPoint(row,leftValue,rightValue));
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
        this.CreateChartComparisonPoint(row.RowKey,row.BenchmarkName,leftValue,rightValue,row);

    private ChartComparisonPoint CreateChartComparisonPoint(string benchmark,string displayName,double? leftValue,double? rightValue,BenchmarkTableRow row) {
        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var shareTexts = BenchmarkChartSelectionLayoutLogic.CreateShareTexts(leftNumber,rightNumber);
        double? ratio = leftNumber>0&&rightNumber>0
            ? leftNumber/rightNumber
            : null;
        var metricName = this.SelectedMetric.Kind==BenchmarkMetricKind.Time?"Time":"Memory";
        var tooltip = BenchmarkFormatting.ShortName(displayName)+"\n"
            +this.SelectedComparisonPair.LeftProject+" "+metricName+": "+FirstText(FormatMetric(leftValue,this.SelectedMetric.Kind),"-")+"\n"
            +this.SelectedComparisonPair.RightProject+" "+metricName+": "+FirstText(FormatMetric(rightValue,this.SelectedMetric.Kind),"-")
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
        this.SelectedSourceDefinition.Id+"|"
        +this.SelectedComparisonPair.LeftProject+"|"
        +this.SelectedComparisonPair.RightProject+"|"
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

    private void RefreshChartUiSelection() {
        for(var index=0;index<this.ChartPoints.Count;index++) {
            var point = this.ChartPoints[index];
            var rowIndex = this.GetBenchmarkRowIndex(point.Benchmark);
            if(rowIndex<0) {
                continue;
            }

            var row = this.BenchmarkRows[rowIndex];
            var next = this.ApplyChartUiSelection(row,point);
            if(next.IsLeftUiSelected==point.IsLeftUiSelected
                &&next.IsRightUiSelected==point.IsRightUiSelected
                &&next.IsRowUiSelected==point.IsRowUiSelected) {
                continue;
            }

            this.ChartPoints[index]=next;
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
        var sourceTab = this.SelectedSourceDefinition;
        var selectedN = this.SelectedResultElementCount?.Value;
        if(!this.IsBenchmarkRunActive) {
            return this.comparisonRows;
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
        var rows = sourceResults
            .Select(CreateReportRowForSelectedElementCount)
            .ToList();
        if(ScalableBenchmarkNameFormatter.UsesPreviousRightColumn(sourceTab.Tab)&&!string.IsNullOrWhiteSpace(sourceTab.RightProject)) {
            foreach(var result in sourceResults) {
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
        MainThread.BeginInvokeOnMainThread(() => {
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

    private void ApplyCurrentSort() {
        if(this.BenchmarkRows.Count<2) {
            return;
        }

        var ordered = this.BenchmarkRows
            .OrderBy(row => row,Comparer<BenchmarkTableRow>.Create((left,right) => {
                var result = BenchmarkSortValue.Compare(
                    left.GetSortValue(this.CurrentSortColumn),
                    right.GetSortValue(this.CurrentSortColumn),
                    this.CurrentSortDescending);
                return result!=0
                    ? result
                    : string.Compare(left.BenchmarkName,right.BenchmarkName,StringComparison.OrdinalIgnoreCase);
            }))
            .ToArray();
        this.BenchmarkRows.Clear();
        foreach(var row in ordered) {
            this.BenchmarkRows.Add(row);
        }
        this.UpdateChart();
    }

    private void ApplyFinalBenchmarkSort() {
        this.CurrentSortColumn=BenchmarkGridSortColumn.PairRatioTime;
        this.CurrentSortDescending=true;
        this.ApplyCurrentSort();
    }

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
