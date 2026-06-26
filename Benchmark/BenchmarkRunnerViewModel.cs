using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Benchmark;

public sealed class BenchmarkRunnerViewModel:ObservableObject {
    private static readonly BenchmarkRunOption NewRunOption = new(
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
    private readonly IBenchmarkCsvService csvService;
    private readonly IBenchmarkComparisonService comparisonService;
    private readonly BenchmarkResultStore resultStore;
    private readonly BenchmarkCsvDiscoveryService csvDiscoveryService;
    private readonly BenchmarkUiStateStore uiStateStore;
    private readonly string benchmarkRoot;
    private CancellationTokenSource? runCancellation;
    private string statusText = "Ready";
    private bool isBenchmarkRunning;
    private bool isRefreshing;
    private bool? isAllBenchmarkSelectionChecked;
    private BenchmarkTab selectedTab = BenchmarkTab.Cpu;
    private ComparisonPairOption selectedComparisonPair;
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
    private double chartHeight = 300;
    private bool isApplyingCpuSelection;
    private bool isApplyingUiState;
    private bool isApplyingBenchmarkSelection;
    private bool isLoadingSelectedRunCsv;
    private bool hasPendingCloseStateSave;
    private BenchmarkGridSortColumn currentSortColumn = BenchmarkGridSortColumn.BenchmarkName;
    private bool currentSortDescending;
    private BenchmarkCellSelectionAnchor? benchmarkSelectionAnchor;
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows = [];
    private readonly Dictionary<BenchmarkSourceTab,List<BenchmarkResultItem>> currentRunResultItemsByTab = [];
    private readonly Dictionary<string,bool> benchmarkCellSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,bool> benchmarkRowSelections = new(StringComparer.OrdinalIgnoreCase);
    private BenchmarkSourceTab? activeRunSourceTab;
    private bool hasLoadedUiSelectionCache;
    private int[] selectedLogicalCpus = [];

    public BenchmarkRunnerViewModel(
        IBenchmarkCatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IBenchmarkRunService runService,
        IBenchmarkCsvService csvService,
        IBenchmarkComparisonService comparisonService,
        BenchmarkResultStore resultStore,
        BenchmarkCsvDiscoveryService csvDiscoveryService,
        BenchmarkUiStateStore uiStateStore,
        string benchmarkRoot) {
        this.catalogService=catalogService;
        this.cpuTopologyService=cpuTopologyService;
        this.runService=runService;
        this.csvService=csvService;
        this.comparisonService=comparisonService;
        this.resultStore=resultStore;
        this.csvDiscoveryService=csvDiscoveryService;
        this.uiStateStore=uiStateStore;
        this.benchmarkRoot=benchmarkRoot;
        this.SourceTabs=BenchmarkSourceTabs.All.Select(static item => new SourceTabOption(item)).ToArray();
        this.selectedSourceTab=this.SourceTabs[0];
        this.ComparisonPairs=this.CreateComparisonPairs();
        this.BenchmarkColumnSelectionGroups=this.CreateBenchmarkColumnSelectionGroups();
        this.BenchmarkProjectSelectionGroups=this.CreateBenchmarkProjectSelectionGroups();
        this.selectedComparisonPair=this.ComparisonPairs[0];
        this.ExecutionProfiles= [
            new ExecutionProfileOption("正確",BenchmarkExecutionProfile.Accurate),
            new ExecutionProfileOption("中間",BenchmarkExecutionProfile.Intermediate),
            new ExecutionProfileOption("高速",BenchmarkExecutionProfile.Fast),
        ];
        this.selectedExecutionProfile=this.ExecutionProfiles.First(static item => item.Profile==BenchmarkExecutionProfile.Fast);
        this.ThresholdOptions= [
            new ThresholdOption("   1 ms",   1),
            new ThresholdOption("   2 ms",   2),
            new ThresholdOption("   4 ms",   4),
            new ThresholdOption("   8 ms",   8),
            new ThresholdOption("  16 ms",  16),
            new ThresholdOption("  32 ms",  32),
            new ThresholdOption("  64 ms",  64),
            new ThresholdOption(" 128 ms", 128),
            new ThresholdOption(" 256 ms", 256),
            new ThresholdOption(" 512 ms", 512),
            new ThresholdOption("1024 ms",1024),
            new ThresholdOption("2048 ms",2048),
            new ThresholdOption("4096 ms",4096),
            new ThresholdOption("8192 ms",8192),
        ];
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
        this.SelectAllCommand=new RelayCommand(() => this.SetAllBenchmarkSelection(true));
        this.ClearSelectionCommand=new RelayCommand(() => this.SetAllBenchmarkSelection(false));
        this.RunCommand=new AsyncRelayCommand(this.RunAsync,() => this.CanChangeBenchmarkConditions);
        this.CancelCommand=new RelayCommand(this.Cancel);
        this.LoadCsvCommand=new AsyncRelayCommand(this.LoadCsvAsync);
        this.ShowCpuCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Cpu);
        this.ShowChartCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Chart);

        foreach(var cpu in this.cpuTopologyService.Load()) {
            this.CpuItems.Add(cpu);
            cpu.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(CpuSelectionItem.IsSelected)&&!this.isApplyingCpuSelection) {
                    if(!this.CanChangeBenchmarkConditions) {
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

    private IReadOnlyList<ComparisonPairOption> CreateComparisonPairs() => [
        new(
            this.SelectedSourceDefinition.Id,
            this.SelectedSourceDefinition.LeftHeader+"/"+this.SelectedSourceDefinition.RightHeader,
            this.SelectedSourceDefinition.LeftProject,
            this.SelectedSourceDefinition.RightProject),
    ];

    private IReadOnlyList<BenchmarkColumnSelectionGroup> CreateBenchmarkColumnSelectionGroups() => [
        new BenchmarkColumnSelectionGroup("Active","Active",["Left","Right"]),
    ];

    private IReadOnlyList<BenchmarkColumnSelectionGroup> CreateBenchmarkProjectSelectionGroups() => [
        new BenchmarkColumnSelectionGroup("Left","Left",["Left"]),
        new BenchmarkColumnSelectionGroup("Right","Right",["Right"]),
    ];

    public ObservableCollection<BenchmarkTableRow> BenchmarkRows { get; } = [];
    public ObservableCollection<BenchmarkResultItem> ResultItems { get; } = [];
    public ObservableCollection<BenchmarkResultItem> VisibleResultItems { get; } = [];
    public ObservableCollection<CpuSelectionItem> CpuItems { get; } = [];
    public ObservableCollection<CpuTableSection> CpuSections { get; } = [];
    public ObservableCollection<ChartComparisonPoint> ChartPoints { get; } = [];
    public ObservableCollection<ScaleOption> ScaleOptions { get; } = [];
    public ObservableCollection<ResultElementCountOption> ResultElementCountOptions { get; } = [];
    public ObservableCollection<BenchmarkRunOption> RunOptions { get; } = [];
    public ObservableCollection<string> ChartWarnings { get; } = [];
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkColumnSelectionGroups { get; }
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkProjectSelectionGroups { get; }
    public IReadOnlyList<ComparisonPairOption> ComparisonPairs { get; }
    public IReadOnlyList<SourceTabOption> SourceTabs { get; }
    public IReadOnlyList<ExecutionProfileOption> ExecutionProfiles { get; }
    public IReadOnlyList<MetricOption> Metrics { get; }
    public IReadOnlyList<ChartModeOption> ChartModes { get; }
    public IReadOnlyList<ThresholdOption> ThresholdOptions { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RunCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand LoadCsvCommand { get; }
    public IRelayCommand ShowCpuCommand { get; }
    public IRelayCommand ShowChartCommand { get; }

    public string StatusText {
        get => this.statusText;
        set => this.SetProperty(ref this.statusText,value);
    }

    public bool IsBenchmarkRunning {
        get => this.isBenchmarkRunning;
        private set {
            if(this.SetProperty(ref this.isBenchmarkRunning,value)) {
                this.RunCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.IsRunning));
                this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
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
                this.OnPropertyChanged(nameof(this.CanChangeBenchmarkConditions));
            }
        }
    }

    public bool CanChangeBenchmarkConditions => !this.IsBenchmarkRunning&&!this.IsRefreshing;

    public bool IsBenchmarkRunActive => this.runCancellation is not null&&this.IsBenchmarkRunning;

    public bool HasPendingCloseStateSave {
        get => this.hasPendingCloseStateSave;
        private set => this.SetProperty(ref this.hasPendingCloseStateSave,value);
    }

    public bool? IsAllBenchmarkSelectionChecked {
        get => this.isAllBenchmarkSelectionChecked;
        private set => this.SetProperty(ref this.isAllBenchmarkSelectionChecked,value);
    }

    public async Task CancelAndWaitAsync() {
        if(!this.IsBenchmarkRunActive) {
            return;
        }

        this.Cancel();
        while(this.IsBenchmarkRunActive) {
            await Task.Delay(100);
        }
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

    public ComparisonPairOption SelectedComparisonPair {
        get => this.selectedComparisonPair;
        set {
            if(this.SetProperty(ref this.selectedComparisonPair,value)) {
                this.OnPropertyChanged(nameof(this.ChartLeftLegendLabel));
                this.OnPropertyChanged(nameof(this.ChartRightLegendLabel));
                this.UpdateChart();
            }
        }
    }

    public SourceTabOption SelectedSourceTab {
        get => this.selectedSourceTab;
        set {
            if(this.IsRefreshing) {
                return;
            }

            if(this.SetProperty(ref this.selectedSourceTab,value)) {
                this.selectedComparisonPair=this.CreateComparisonPairs()[0];
                this.OnPropertyChanged(nameof(this.SelectedComparisonPair));
                this.OnPropertyChanged(nameof(this.SelectedSourceDefinition));
                this.ClearSelectedRunDisplay();
                this.UpdateChart(false);
                _=this.RefreshAsync();
            }
        }
    }

    public BenchmarkSourceTabDefinition SelectedSourceDefinition => this.SelectedSourceTab.Definition;

    public string ChartLeftLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.LeftProject);
    public string ChartRightLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.RightProject);

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
            if(this.SetProperty(ref this.selectedRunOption,value)) {
                if(!this.isApplyingUiState) {
                    if(IsNewRunOption(value)) {
                        this.ClearSelectedSourceLiveResults();
                    }
                    this.RebuildResultElementCounts(value?.ElementCounts?? []);
                    this.LoadSelectedRunCsv();
                    this.SaveUiState();
                }
            }
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
                this.RefreshVisibleResults();
                this.ApplySelectedElementCountToTable();
                this.UpdateChart();
                if(!this.isApplyingUiState&&!this.IsBenchmarkRunning) {
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

    public string SelectedChartElementCountText => this.SelectedResultElementCount?.DisplayName??"N=-";

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
        if(this.currentSortColumn==column) {
            this.currentSortDescending=!this.currentSortDescending;
        }
        else {
            this.currentSortColumn=column;
            this.currentSortDescending=false;
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
        if(this.IsRefreshing) {
            return;
        }

        this.SetBenchmarkCells(this.BenchmarkRows.SelectMany(static row => row.ProjectCells),this.IsAllBenchmarkSelectionChecked is false);
    }

    public void ToggleBenchmarkColumnGroup(BenchmarkColumnSelectionGroup group) {
        if(this.IsRefreshing) {
            return;
        }

        var selected = group.IsChecked is false;
        this.SetBenchmarkCells(this.GetBenchmarkCells(group.ColumnKeys),selected);
    }

    public void ToggleBenchmarkRow(BenchmarkTableRow row) {
        if(this.IsRefreshing) {
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
        if(this.IsRefreshing) {
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

    private async Task RefreshAsync() {
        this.IsRefreshing=true;
        this.StatusText="Loading";
        try {
            await this.RefreshCsvScanAsync(CancellationToken.None);
            var savedState = this.uiStateStore.Load();
            this.EnsureUiSelectionCache(savedState);
            this.CaptureCurrentBenchmarkSelections();
            var loaded = await this.catalogService.LoadAsync(CancellationToken.None);
            this.BenchmarkRows.Clear();
            this.benchmarkSelectionAnchor=null;
            foreach(var row in this.CreateMethodRows(loaded)) {
                this.SubscribeBenchmarkRow(row);
                row.IsSelected=this.benchmarkRowSelections.GetValueOrDefault(
                    CreateRowSelectionKey(row.SourceTab,row.MethodName),
                    savedState.RowSelections.GetValueOrDefault(row.MethodName,false));
                foreach(var cell in row.ProjectCells) {
                    var key = BenchmarkUiStateStore.CreateCellKey(row.MethodName,cell.Project);
                    if(this.benchmarkCellSelections.TryGetValue(key,out var saved)) {
                        cell.IsSelected=saved;
                    }
                    else {
                        cell.IsSelected=false;
                    }
                }
                this.BenchmarkRows.Add(row);
            }
            this.RestoreRunSelection(savedState.SelectedRunNumber,savedState.SelectedResultElementCount);
            this.RefreshBenchmarkSelectionAggregates();
            this.ApplyCurrentSort();
            this.StatusText=this.BenchmarkRows.Count+" benchmarks";
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark catalog load failed.",ex);
            this.StatusText="Load failed: "+ex.Message;
        }
        finally {
            this.IsRefreshing=false;
        }
    }

    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases) {
        return cases
            .Where(item => this.SelectedSourceDefinition.ProjectOrder.Contains(item.Project,StringComparer.OrdinalIgnoreCase))
            .GroupBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key,StringComparer.OrdinalIgnoreCase)
            .Select(group => new BenchmarkTableRow(
                group.Key,
                this.SelectedSourceDefinition,
                group
                    .GroupBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(static item => item.Key,static item => item.First(),StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private void SetAllBenchmarkSelection(bool selected) {
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
        if(this.IsRefreshing) {
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
        this.IsAllBenchmarkSelectionChecked=GetSelectionState(this.BenchmarkRows.SelectMany(static row => row.ProjectCells));
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

    private async Task RefreshCsvScanAsync(CancellationToken cancellationToken) {
        var scan = await Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            return this.csvDiscoveryService.Scan(this.benchmarkRoot,this.SelectedExecutionProfile.Profile,this.SelectedSourceDefinition.Tab);
        },cancellationToken);
        this.resultStore.UpsertMany(scan.Results);
        this.resultStore.Save();
        var selectedRunNumber = this.SelectedRunOption?.RunNumber;
        this.RunOptions.Clear();
        this.RunOptions.Add(CreateNewRunOption(this.SelectedExecutionProfile.Profile));
        foreach(var option in scan.RunOptions) {
            this.RunOptions.Add(option);
        }
        this.SelectedRunOption=this.RunOptions.FirstOrDefault(option => option.RunNumber==selectedRunNumber)??this.RunOptions.FirstOrDefault();
        this.RebuildResultElementCounts(this.SelectedRunOption?.ElementCounts?? []);
        this.LoadSelectedRunCsv();
    }

    private async Task RefreshRunsForSelectedProfileAsync() {
        try {
            await this.RefreshCsvScanAsync(CancellationToken.None);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run list refresh failed.",ex);
        }
    }

    private void RestoreRunSelection(int? runNumber,int? elementCount) {
        this.isApplyingUiState=true;
        try {
            this.SelectedRunOption=runNumber is { } number
                ? this.RunOptions.FirstOrDefault(option => option.RunNumber==number)??this.RunOptions.FirstOrDefault()
                : this.RunOptions.FirstOrDefault();
            this.RebuildResultElementCounts(this.SelectedRunOption?.ElementCounts?? []);
        }
        finally {
            this.isApplyingUiState=false;
            this.RefreshBenchmarkSelectionAggregates();
        }
        this.LoadSelectedRunCsv();
    }

    private void EnsureUiSelectionCache(BenchmarkUiState savedState) {
        if(this.hasLoadedUiSelectionCache) {
            return;
        }

        foreach(var item in savedState.ProjectCellSelections) {
            this.benchmarkCellSelections[item.Key]=item.Value;
        }
        foreach(var item in savedState.RowSelections) {
            this.benchmarkRowSelections[item.Key]=item.Value;
        }
        this.hasLoadedUiSelectionCache=true;
    }

    private void CaptureCurrentBenchmarkSelections() {
        foreach(var row in this.BenchmarkRows) {
            this.benchmarkRowSelections[CreateRowSelectionKey(row.SourceTab,row.MethodName)]=row.IsSelected;
            foreach(var cell in row.ProjectCells) {
                this.benchmarkCellSelections[BenchmarkUiStateStore.CreateCellKey(row.MethodName,cell.Project)]=cell.IsSelected;
            }
        }
    }

    private static string CreateRowSelectionKey(BenchmarkSourceTabDefinition sourceTab,string method) =>
        sourceTab.Id+"|"+method;

    private void SelectRunByRoot(string runRoot) {
        var option = this.RunOptions.FirstOrDefault(item => string.Equals(item.RunRoot,runRoot,StringComparison.OrdinalIgnoreCase));
        if(option is null) {
            return;
        }

        this.SelectedRunOption=option;
    }

    public void SaveUiState() {
        this.CaptureCurrentBenchmarkSelections();
        var state = new BenchmarkUiState(
            null,
            this.SelectedResultElementCount?.Value,
            new Dictionary<string,bool>(this.benchmarkCellSelections,StringComparer.OrdinalIgnoreCase),
            new Dictionary<string,bool>(this.benchmarkRowSelections,StringComparer.OrdinalIgnoreCase));
        this.uiStateStore.Save(state);
        this.HasPendingCloseStateSave=false;
    }

    public void DiscardPendingCloseStateSave() {
        this.HasPendingCloseStateSave=false;
    }

    private async Task RunAsync() {
        var selected = this.BenchmarkRows
            .Where(static item => item.IsSelected)
            .SelectMany(static item => item.GetRunCases())
            .ToArray();
        if(selected.Length==0) {
            this.StatusText="No benchmark selected";
            return;
        }

        this.SelectedTab=BenchmarkTab.Cpu;
        this.runCancellation?.Cancel();
        this.runCancellation=new CancellationTokenSource();
        this.IsRunning=true;
        var runSourceTab = this.SelectedSourceDefinition.Tab;
        this.activeRunSourceTab=runSourceTab;
        this.CurrentElementCountText="1";
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.currentRunResultItemsByTab.Remove(runSourceTab);
        this.ResultElementCountOptions.Clear();
        this.SelectedResultElementCount=null;
        this.OnPropertyChanged(nameof(this.ChartElementCountSliderMaximum));
        this.OnPropertyChanged(nameof(this.IsChartElementSliderVisible));
        this.OnPropertyChanged(nameof(this.SelectedChartElementCountText));
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
                IsNewRunOption(this.SelectedRunOption) ? null : this.SelectedRunOption?.RunRoot,
                result => MainThread.BeginInvokeOnMainThread(() => {
                    this.HandleRunResult(result);
                }),
                status => MainThread.BeginInvokeOnMainThread(() => this.StatusText=status),
                this.runCancellation.Token);
            await this.RefreshCsvScanAsync(this.runCancellation.Token);
            if(report.RunRoot is not null) {
                this.SelectRunByRoot(report.RunRoot);
            }
        }
        catch(OperationCanceledException) {
            this.StatusText="Canceled";
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run failed.",ex);
            this.StatusText="Run failed: "+ex.Message;
        }
        finally {
            this.IsRunning=false;
            this.activeRunSourceTab=null;
            this.runCancellation?.Dispose();
            this.runCancellation=null;
            this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
            this.ApplySelectedElementCountToTable();
            this.RestoreLatestCompletedLiveResult();
            this.ClearRunResultHighlights();
            this.UpdateChart();
        }
    }

    private void Cancel() {
        this.runCancellation?.Cancel();
    }

    private void HandleRunResult(BenchmarkResultItem result) {
        var cpu = result.AssignedLogicalCpu is { } logicalCpu
            ? this.CpuItems.FirstOrDefault(item => item.LogicalProcessorIndex==logicalCpu)
            : null;
        cpu?.ApplyJob(result);
        var resultSourceTab = this.activeRunSourceTab??this.SelectedSourceDefinition.Tab;
        var isResultForVisibleTab = resultSourceTab==this.SelectedSourceDefinition.Tab;
        if(isResultForVisibleTab&&result.ElementCount>0) {
            this.CurrentElementCountText=FormatElementCountText(result.ElementCount);
        }
        if(isResultForVisibleTab&&string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase)) {
            this.RetainCompletedHighlightsForRunningElement(result.ElementCount);
        }
        this.UpsertCurrentRunResultItem(result);
        if(isResultForVisibleTab) {
            this.UpsertResultItem(result);
            this.RegisterResultElementCount(result.ElementCount);
            this.RefreshVisibleResults();
        }
        var row = this.BenchmarkRows.FirstOrDefault(item => string.Equals(item.MethodName,result.Method,StringComparison.OrdinalIgnoreCase));
        if(isResultForVisibleTab&&string.Equals(result.Status,JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase)) {
            foreach(var benchmarkRow in this.BenchmarkRows) {
                benchmarkRow.ClearLiveState();
            }
        }
        var completedAt = DateTimeOffset.Now;
        if(isResultForVisibleTab) {
            row?.ApplyRunResult(result,cpu,completedAt);
        }
        if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)) {
            this.resultStore.Upsert(result,completedAt,null,string.Empty);
            this.resultStore.Save();
            this.HasPendingCloseStateSave=true;
            if(isResultForVisibleTab) {
                this.ApplySelectedElementCountToTable();
                row?.ApplyRunResult(result,cpu,completedAt);
                this.UpdateChart();
            }
        }
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

        var row = this.BenchmarkRows.FirstOrDefault(item => string.Equals(item.MethodName,result.Method,StringComparison.OrdinalIgnoreCase));
        row?.ApplyRunResult(result,null,DateTimeOffset.Now,false);
    }

    private void UpsertResultItem(BenchmarkResultItem result) {
        for(var index = this.ResultItems.Count-1; index>=0; index--) {
            var item = this.ResultItems[index];
            if(string.Equals(item.Project,result.Project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(item.Method,result.Method,StringComparison.OrdinalIgnoreCase)
                &&item.ElementCount==result.ElementCount) {
                this.ResultItems.RemoveAt(index);
            }
        }
        this.ResultItems.Add(result);
    }

    private void UpsertCurrentRunResultItem(BenchmarkResultItem result) {
        var sourceTab = this.activeRunSourceTab??this.SelectedSourceDefinition.Tab;
        var results = this.GetOrCreateCurrentRunResultItems(sourceTab);
        for(var index = results.Count-1; index>=0; index--) {
            var item = results[index];
            if(string.Equals(item.Project,result.Project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(item.Method,result.Method,StringComparison.OrdinalIgnoreCase)
                &&item.ElementCount==result.ElementCount) {
                results.RemoveAt(index);
            }
        }
        results.Add(result);
    }

    private IReadOnlyList<int> GetSelectedElementCounts() {
        return BenchmarkElementCountLogic
            .BuildAdaptiveCandidates(BenchmarkElementCounts.All,BenchmarkElementCounts.All[^1])
            .ToArray();
    }

    private string GetBenchmarkRoot(BenchmarkExecutionProfile profile) =>
        Path.Combine(this.benchmarkRoot,BenchmarkExecutionProfileNames.ToFolderName(profile));

    private TimeSpan GetAdaptiveThreshold() {
        return TimeSpan.FromMilliseconds(this.SelectedThreshold.Milliseconds);
    }

    private async Task LoadCsvAsync() {
        var file = await FilePicker.PickAsync(new PickOptions {
            PickerTitle="BenchmarkComparison.csv",
            FileTypes=new FilePickerFileType(new Dictionary<DevicePlatform,IEnumerable<string>>{
                { DevicePlatform.WinUI, [".csv"] },
                { DevicePlatform.macOS, ["csv"] },
                { DevicePlatform.MacCatalyst, ["public.comma-separated-values-text"] },
            }),
        });
        if(file is null) {
            return;
        }

        try {
            this.ClearBenchmarkResultDisplays();
            this.comparisonRows=this.csvService.ReadComparisonCsv(file.FullPath);
            this.LoadComparisonRowsIntoResults(this.comparisonRows);
            this.ApplyCurrentSort();
            this.ApplyComparisonRowsToTable();
            this.UpdateChart(false);
            this.SelectedTab=BenchmarkTab.Chart;
            this.StatusText="CSV loaded: "+file.FullPath;
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("CSV load failed.",ex);
            this.StatusText="CSV load failed: "+ex.Message;
        }
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
            if(elementCount>0&&this.IsRunning) {
                this.SelectedResultElementCount=this.ResultElementCountOptions.FirstOrDefault(item => item.Value==elementCount)??this.SelectedResultElementCount;
            }
            return;
        }

        var option = new ResultElementCountOption(elementCount,"N="+elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture));
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
        this.SelectedResultElementCount=this.IsRunning
            ? this.ResultElementCountOptions.FirstOrDefault(item => item.Value==elementCount)
            : this.SelectedResultElementCount??this.ResultElementCountOptions.FirstOrDefault();
    }

    private static string FormatElementCountText(int elementCount) {
        return elementCount>0
            ? elementCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "-";
    }

    private void RebuildResultElementCounts(IReadOnlyList<int> elementCounts) {
        this.ResultElementCountOptions.Clear();
        foreach(var elementCount in elementCounts.Where(static item => item>0).Distinct().Order()) {
            this.ResultElementCountOptions.Add(new ResultElementCountOption(elementCount,"N="+elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)));
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
        foreach(var item in this.ResultItems.Where(item => selectedN is null||item.ElementCount==0||item.ElementCount==selectedN)) {
            this.VisibleResultItems.Add(item);
        }
    }

    private bool HasCurrentRunResultsForSelectedTab =>
        this.GetCurrentRunResultItemsForSelectedTab().Count>0;

    private IReadOnlyList<BenchmarkResultItem> GetCurrentRunResultItemsForSelectedTab() =>
        this.currentRunResultItemsByTab.TryGetValue(this.SelectedSourceDefinition.Tab,out var results)
            ? results
            : [];

    private List<BenchmarkResultItem> GetOrCreateCurrentRunResultItems(BenchmarkSourceTab sourceTab) {
        if(!this.currentRunResultItemsByTab.TryGetValue(sourceTab,out var results)) {
            results=[];
            this.currentRunResultItemsByTab.Add(sourceTab,results);
        }

        return results;
    }

    private bool RestoreCurrentRunDisplayForSelectedTab() {
        var results = this.GetCurrentRunResultItemsForSelectedTab();
        if(results.Count==0) {
            return false;
        }

        var latestElementCount = results
            .Where(static item => item.ElementCount>0)
            .OrderBy(static item => item.StartedAt??DateTimeOffset.MinValue)
            .ThenBy(static item => item.ElementCount)
            .LastOrDefault()
            ?.ElementCount;
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        foreach(var item in results) {
            this.ResultItems.Add(item);
        }

        this.RebuildResultElementCounts(results
            .Select(static item => item.ElementCount)
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
        this.UpdateChart();
        return true;
    }

    private void ClearSelectedRunDisplay() {
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
        this.UpdateChart(false);
    }

    private void ClearSelectedSourceLiveResults() {
        this.currentRunResultItemsByTab.Remove(this.SelectedSourceDefinition.Tab);
        this.comparisonRows= [];
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.CurrentElementCountText="-";
        this.ClearBenchmarkResultDisplays();
    }

    private void LoadComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows,int elementCount = 0) {
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        foreach(var row in rows) {
            this.AddResult(row.Benchmark,row.SourceTab.LeftProject,row.Left,elementCount);
            this.AddResult(row.Benchmark,row.SourceTab.RightProject,row.Right,elementCount);
        }
    }

    private void AddResult(string method,string project,ProjectMetrics metrics,int elementCount) {
        if(metrics.HasValue) {
            this.ResultItems.Add(new BenchmarkResultItem(project,method,metrics,elementCount));
        }
    }

    private void ApplyComparisonRowsToTable() {
        var rowsByMethod = this.comparisonRows.ToDictionary(static item => item.Benchmark,StringComparer.OrdinalIgnoreCase);
        var preserveRetainedDisplay = this.HasCurrentRunResultsForSelectedTab;
        foreach(var benchmark in this.BenchmarkRows) {
            benchmark.ApplyComparison(rowsByMethod.TryGetValue(benchmark.MethodName,out var row) ? row : null,preserveRetainedDisplay);
        }
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
    }

    private void LoadSelectedRunCsv() {
        if(this.isLoadingSelectedRunCsv) {
            return;
        }

        this.isLoadingSelectedRunCsv=true;
        try {
            var run = this.SelectedRunOption;
            var selectedN = this.SelectedResultElementCount?.Value;
            if(run is null||IsNewRunOption(run)||selectedN is null) {
                this.ClearSelectedRunDisplay();
                return;
            }

            if(this.RestoreCurrentRunDisplayForSelectedTab()) {
                return;
            }

            var path = Path.Combine(
                run.RunRoot,
                this.SelectedSourceDefinition.Id,
                "N"+selectedN.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+".csv");
            if(!File.Exists(path)) {
                this.ClearSelectedRunDisplay();
                return;
            }

            this.comparisonRows=this.csvService.ReadComparisonCsv(path);
            this.LoadComparisonRowsIntoResults(this.comparisonRows,selectedN.Value);
            this.ApplyComparisonRowsToTable();
            this.RefreshVisibleResults();
            this.ApplyCurrentSort();
            this.UpdateChart(false);
        }
        catch(Exception ex) {
            StartupDiagnostics.Write("Benchmark run CSV load failed.",ex);
            this.StatusText="CSV load failed: "+ex.Message;
        }
        finally {
            this.isLoadingSelectedRunCsv=false;
        }
    }

    private static BenchmarkRunOption CreateNewRunOption(BenchmarkExecutionProfile executionProfile) =>
        NewRunOption with {
            ExecutionProfile=executionProfile,
        };

    private static bool IsNewRunOption(BenchmarkRunOption? option) =>
        option is not null&&option.RunNumber<=0;

    private void ApplySelectedElementCountToTable() {
        var selectedN = this.SelectedResultElementCount?.Value;
        if(this.HasCurrentRunResultsForSelectedTab) {
            this.comparisonRows=selectedN is { } currentElementCount
                ? this.BuildCurrentRunComparisonRowsForElementCount(currentElementCount)
                : this.BuildCurrentRunComparisonRows();
        }
        else {
            if(!this.isLoadingSelectedRunCsv) {
                this.LoadSelectedRunCsv();
                return;
            }
        }
        this.ApplyComparisonRowsToTable();
        if(selectedN is { } skippedElementCount) {
            this.ApplySkippedCells(skippedElementCount);
        }
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRowsForElementCount(int elementCount) {
        var currentRunResults = this.GetCurrentRunResultItemsForSelectedTab();
        var rows = currentRunResults
            .Where(result => result.ElementCount==elementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .Select(CreateReportRowForSelectedElementCount)
            .GroupBy(static row => row.Project+"|"+row.Method,StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .ToArray();
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,this.SelectedSourceDefinition);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildCurrentRunComparisonRows() {
        var currentRunResults = this.GetCurrentRunResultItemsForSelectedTab();
        var rows = currentRunResults
            .Where(static result => string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .GroupBy(static result => result.Method,StringComparer.OrdinalIgnoreCase)
            .SelectMany(static group => group
                .GroupBy(static result => result.Project,StringComparer.OrdinalIgnoreCase)
                .Select(static projectGroup => CreateReportRowForSelectedElementCount(projectGroup
                    .OrderByDescending(static result => result.StartedAt)
                    .ThenByDescending(static result => result.ElementCount)
                    .First())))
            .ToArray();
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,this.SelectedSourceDefinition);
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildComparisonRowsForElementCount(int elementCount) {
        var currentRunResults = this.GetCurrentRunResultItemsForSelectedTab();
        var rows = this.resultStore.Results
            .Where(result => result.ElementCount==elementCount
                &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .Select(static result => CreateReportRow(result.Project,result.Method,result.ToProjectMetrics()))
            .Concat(currentRunResults
                .Where(result => result.ElementCount==elementCount
                    &&string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase))
                .Select(CreateReportRowForSelectedElementCount))
            .GroupBy(static row => row.Project+"|"+row.Method,StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .ToArray();
        return rows.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(rows,this.SelectedSourceDefinition);
    }

    private void ApplySkippedCells(int selectedElementCount) {
        foreach(var row in this.BenchmarkRows) {
            foreach(var pair in this.BenchmarkPairs()) {
                var left = row.GetProjectCell(pair.LeftProject);
                var right = row.GetProjectCell(pair.RightProject);
                if(left is not { HasCase: true }||right is not { HasCase: true }) {
                    continue;
                }

                if(this.HasResult(row.MethodName,pair.LeftProject,selectedElementCount)
                    ||this.HasResult(row.MethodName,pair.RightProject,selectedElementCount)) {
                    continue;
                }

                var stoppedAt = this.FindPairStoppedAt(row.MethodName,pair.LeftProject,selectedElementCount);
                if(stoppedAt is { } stoppedAtElementCount) {
                    row.ApplyPairStopped(pair.LeftProject,pair.RightProject,stoppedAtElementCount,this.HasCurrentRunResultsForSelectedTab);
                    row.ApplySkipped(pair.LeftProject,stoppedAtElementCount,this.GetResultMetrics(row.MethodName,pair.LeftProject,stoppedAtElementCount));
                    row.ApplySkipped(pair.RightProject,stoppedAtElementCount,this.GetResultMetrics(row.MethodName,pair.RightProject,stoppedAtElementCount));
                }
            }
        }
    }

    private IReadOnlyList<(string LeftProject,string RightProject)> BenchmarkPairs() => [
        (this.SelectedSourceDefinition.LeftProject,this.SelectedSourceDefinition.RightProject),
    ];

    private bool HasResult(string method,string project,int elementCount) {
        var currentRunResults = this.GetCurrentRunResultItemsForSelectedTab();
        return this.HasCurrentRunResultsForSelectedTab&&currentRunResults.Any(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
                &&result.ElementCount==elementCount)
            ||(!this.HasCurrentRunResultsForSelectedTab&&this.ResultItems.Any(result => string.Equals(result.Method,method,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(result.Project,project,StringComparison.OrdinalIgnoreCase)
            &&result.ElementCount==elementCount));
    }

    private int? FindPairStoppedAt(string method,string project,int selectedElementCount) {
        var currentRunResults = this.GetCurrentRunResultItemsForSelectedTab();
        var measurements = this.HasCurrentRunResultsForSelectedTab
            ? currentRunResults.Select(static result => new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.ElementCount,
                result.Status,
                result.MeanNanoseconds))
            : this.ResultItems.Select(static result => new BenchmarkElementMeasurement(
                result.Project,
                result.Method,
                result.ElementCount,
                result.Status,
                result.MeanNanoseconds));
        return BenchmarkSkippedDisplayLogic.FindPairStoppedAt(
            measurements,
            method,
            project,
            selectedElementCount,
            this.GetAdaptiveThreshold().TotalMilliseconds*1_000_000);
    }

    private ProjectMetrics GetResultMetrics(string method,string project,int elementCount) {
        if(this.HasCurrentRunResultsForSelectedTab) {
            var currentRunResults = this.GetCurrentRunResultItemsForSelectedTab();
            for(var index = currentRunResults.Count-1; index>=0; index--) {
                var item = currentRunResults[index];
                if(string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(item.Project,project,StringComparison.OrdinalIgnoreCase)
                    &&item.ElementCount==elementCount
                    &&string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)) {
                    return ProjectMetrics.FromMeasurement(item.MeanNanoseconds,item.AllocatedBytesValue);
                }
            }
        }

        var selectedRunResult = !this.HasCurrentRunResultsForSelectedTab
            ? this.ResultItems.LastOrDefault(item => string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(item.Project,project,StringComparison.OrdinalIgnoreCase)
                &&item.ElementCount==elementCount
                &&string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            : null;
        return selectedRunResult is not null
            ? ProjectMetrics.FromMeasurement(selectedRunResult.MeanNanoseconds,selectedRunResult.AllocatedBytesValue)
            : ProjectMetrics.Missing;
    }

    private void UpdateChart(bool preserveExistingWhenNoMeasuredPoints = true) {
        var elementComparisonRows = this.BuildElementComparisonRows();
        var points = this.BuildBenchmarkChartPoints(elementComparisonRows);
        var hasMeasuredPoint = points.Any(static point => point.LeftValue>0||point.RightValue>0);
        if(preserveExistingWhenNoMeasuredPoints&&this.ChartPoints.Count>0&&!hasMeasuredPoint) {
            return;
        }

        this.ChartPoints.Clear();
        this.ChartWarnings.Clear();
        foreach(var item in points.Reverse()) {
            this.ChartPoints.Add(item);
        }
        foreach(var warning in this.BuildChartWarnings(elementComparisonRows)) {
            this.ChartWarnings.Add(warning);
        }
        this.ChartHeight=Math.Max(300,this.ChartPoints.Count*28+72);
    }

    private IReadOnlyList<ChartComparisonPoint> BuildBenchmarkChartPoints(IReadOnlyList<BenchmarkComparisonRow> rows) {
        var rowsByBenchmark = rows.ToDictionary(static item => item.Benchmark,StringComparer.OrdinalIgnoreCase);
        return this.BenchmarkRows
            .Select(row => this.CreateBenchmarkChartPoint(row,rowsByBenchmark))
            .ToArray();
    }

    private ChartComparisonPoint CreateBenchmarkChartPoint(
        BenchmarkTableRow row,
        IReadOnlyDictionary<string,BenchmarkComparisonRow> rowsByBenchmark) {
        BenchmarkComparisonRow? comparisonRow = rowsByBenchmark.TryGetValue(row.MethodName,out var found)
            ? found
            : null;
        var left = this.GetChartMetrics(row,this.SelectedComparisonPair.LeftProject,comparisonRow);
        var right = this.GetChartMetrics(row,this.SelectedComparisonPair.RightProject,comparisonRow);
        var leftValue = GetMetricValue(left,this.SelectedMetric.Kind);
        var rightValue = GetMetricValue(right,this.SelectedMetric.Kind);
        var leftNumber = Math.Max(0,leftValue??0);
        var rightNumber = Math.Max(0,rightValue??0);
        var total = leftNumber+rightNumber;
        var leftText = total>0&&leftValue is not null
            ? (leftNumber/total).ToString("P0",System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        var rightText = total>0&&rightValue is not null
            ? (rightNumber/total).ToString("P0",System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        double? ratio = leftNumber>0&&rightNumber>0
            ? leftNumber/rightNumber
            : null;
        var metricName = this.SelectedMetric.Kind==BenchmarkMetricKind.Time?"Time":"Allocated";
        var tooltip = row.MethodName+"\n"
            +this.SelectedComparisonPair.LeftProject+" "+metricName+": "+FirstText(FormatMetric(leftValue,this.SelectedMetric.Kind),"-")+"\n"
            +this.SelectedComparisonPair.RightProject+" "+metricName+": "+FirstText(FormatMetric(rightValue,this.SelectedMetric.Kind),"-");
        return new ChartComparisonPoint(
            row.MethodName,
            BenchmarkFormatting.ShortName(row.MethodName),
            leftNumber,
            rightNumber,
            leftText,
            rightText,
            tooltip,
            ratio);
    }

    private ProjectMetrics GetChartMetrics(
        BenchmarkTableRow row,
        string project,
        BenchmarkComparisonRow? comparisonRow) {
        var cellMetrics = row.GetProjectCell(project)?.Metrics;
        if(cellMetrics is { HasMeasuredValue: true }) {
            return cellMetrics.Value;
        }

        return comparisonRow?.GetProject(project)??ProjectMetrics.Missing;
    }

    private IReadOnlyList<BenchmarkComparisonRow> BuildElementComparisonRows() {
        var selectedN = this.SelectedResultElementCount?.Value;
        if(selectedN is null) {
            return this.comparisonRows;
        }

        var results = this.ResultItems
            .Where(item => item.ElementCount==selectedN.Value)
            .ToArray();
        return results.Length==0
            ? []
            : BenchmarkComparisonFormatter.BuildComparisonRows(results.Select(CreateReportRowForSelectedElementCount).ToArray(),this.SelectedSourceDefinition);
    }

    private static ReportRow CreateReportRowForSelectedElementCount(BenchmarkResultItem item) =>
        new(
            item.Project,
            item.Method,
            string.Empty,
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
            item.ExitCode);

    private static ReportRow CreateReportRow(string project,string method,ProjectMetrics metrics) =>
        new(
            project,
            method,
            string.Empty,
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
            metrics.HasMeasuredValue ? 0 : null);

    private IEnumerable<string> BuildChartWarnings(IReadOnlyList<BenchmarkComparisonRow> rows) {
        foreach(var row in rows) {
            var left = row.GetProject(this.SelectedComparisonPair.LeftProject);
            var right = row.GetProject(this.SelectedComparisonPair.RightProject);
            if(left.HasValue==right.HasValue) {
                continue;
            }

            var missing = left.HasValue ? this.SelectedComparisonPair.RightProject : this.SelectedComparisonPair.LeftProject;
            yield return BenchmarkFormatting.ShortName(row.Benchmark)+" / "+missing+" 未計測";
        }
    }

    private IReadOnlyList<ChartComparisonPoint> BuildScaleTrendPoints() {
        var method = this.BenchmarkRows.FirstOrDefault(static item => item.IsSelected)?.MethodName
            ??this.ResultItems.FirstOrDefault()?.Method
            ??string.Empty;
        if(string.IsNullOrWhiteSpace(method)) {
            return [];
        }

        var rows = this.ResultItems
            .Where(item => item.ElementCount>0&&string.Equals(item.Method,method,StringComparison.OrdinalIgnoreCase))
            .GroupBy(static item => item.ElementCount)
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
        if(left is null&&right is null) {
            return null;
        }

        var leftValue = GetMetricValue(left,metric);
        var rightValue = GetMetricValue(right,metric);
        var leftText = FormatMetric(leftValue,metric);
        var rightText = FormatMetric(rightValue,metric);
        return new ChartComparisonPoint(
            "N="+elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),
            "N="+elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),
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
                    left.GetSortValue(this.currentSortColumn),
                    right.GetSortValue(this.currentSortColumn),
                    this.currentSortDescending);
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

    private void RebuildCpuSections() {
        this.CpuSections.Clear();
        this.AddCpuSection("P core",CpuCoreKind.Performance);
        this.AddCpuSection("E core",CpuCoreKind.Efficiency);
        this.AddCpuSection("Unknown core",CpuCoreKind.Unknown);
    }

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
