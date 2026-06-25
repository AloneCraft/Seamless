using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Benchmark;

public sealed class BenchmarkRunnerViewModel:ObservableObject{
    private static readonly HashSet<string> DisplayBenchmarkProjects = new(StringComparer.OrdinalIgnoreCase){
        BenchmarkComparisonFormatter.SystemFLinq,
        BenchmarkComparisonFormatter.SystemZLinq,
        BenchmarkComparisonFormatter.PersistedFLinq,
        BenchmarkComparisonFormatter.PersistedLinq,
        BenchmarkComparisonFormatter.TsavoriteFLinq,
        BenchmarkComparisonFormatter.TsavoriteLinq,
    };

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
    private bool isRunning;
    private bool? isAllBenchmarkSelectionChecked;
    private BenchmarkTab selectedTab = BenchmarkTab.Cpu;
    private ComparisonPairOption selectedComparisonPair;
    private ScaleOption? selectedScale;
    private RunModeOption selectedRunMode;
    private ThresholdOption selectedThreshold;
    private CsvDateOption? selectedCsvDateOption;
    private DateTime? selectedCalendarDate;
    private string adaptiveThresholdSecondsText = "1";
    private MetricOption selectedMetric;
    private ChartModeOption selectedChartMode;
    private ResultElementCountOption? selectedResultElementCount;
    private double chartHeight = 300;
    private bool isApplyingCpuSelection;
    private bool isApplyingUiState;
    private bool isApplyingBenchmarkSelection;
    private bool hasPendingCloseStateSave;
    private BenchmarkGridSortColumn currentSortColumn = BenchmarkGridSortColumn.BenchmarkName;
    private bool currentSortDescending;
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows = [];

    public BenchmarkRunnerViewModel(
        IBenchmarkCatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IBenchmarkRunService runService,
        IBenchmarkCsvService csvService,
        IBenchmarkComparisonService comparisonService,
        BenchmarkResultStore resultStore,
        BenchmarkCsvDiscoveryService csvDiscoveryService,
        BenchmarkUiStateStore uiStateStore,
        string benchmarkRoot){
        this.catalogService=catalogService;
        this.cpuTopologyService=cpuTopologyService;
        this.runService=runService;
        this.csvService=csvService;
        this.comparisonService=comparisonService;
        this.resultStore=resultStore;
        this.csvDiscoveryService=csvDiscoveryService;
        this.uiStateStore=uiStateStore;
        this.benchmarkRoot=benchmarkRoot;
        this.ComparisonPairs=[
            new ComparisonPairOption("System","System FLinq/ZLinq",BenchmarkComparisonFormatter.SystemFLinq,BenchmarkComparisonFormatter.SystemZLinq),
            new ComparisonPairOption("Persisted","PersistedFLinq/PersistedLinq",BenchmarkComparisonFormatter.PersistedFLinq,BenchmarkComparisonFormatter.PersistedLinq),
            new ComparisonPairOption("Tsavorite","TsavoriteFLinq/TsavoriteLinq",BenchmarkComparisonFormatter.TsavoriteFLinq,BenchmarkComparisonFormatter.TsavoriteLinq),
        ];
        this.BenchmarkColumnSelectionGroups=[
            new BenchmarkColumnSelectionGroup("System","System",[
                BenchmarkComparisonFormatter.SystemFLinq,
                BenchmarkComparisonFormatter.SystemZLinq,
            ]),
            new BenchmarkColumnSelectionGroup("Persisted","Persisted",[
                BenchmarkComparisonFormatter.PersistedFLinq,
                BenchmarkComparisonFormatter.PersistedLinq,
            ]),
            new BenchmarkColumnSelectionGroup("Tsavorite","Tsavorite",[
                BenchmarkComparisonFormatter.TsavoriteFLinq,
                BenchmarkComparisonFormatter.TsavoriteLinq,
            ]),
        ];
        this.BenchmarkProjectSelectionGroups=[
            new BenchmarkColumnSelectionGroup(BenchmarkComparisonFormatter.SystemFLinq,"FLinq",[
                BenchmarkComparisonFormatter.SystemFLinq,
            ]),
            new BenchmarkColumnSelectionGroup(BenchmarkComparisonFormatter.SystemZLinq,"ZLinq",[
                BenchmarkComparisonFormatter.SystemZLinq,
            ]),
            new BenchmarkColumnSelectionGroup(BenchmarkComparisonFormatter.PersistedFLinq,"FLinq",[
                BenchmarkComparisonFormatter.PersistedFLinq,
            ]),
            new BenchmarkColumnSelectionGroup(BenchmarkComparisonFormatter.PersistedLinq,"Linq",[
                BenchmarkComparisonFormatter.PersistedLinq,
            ]),
            new BenchmarkColumnSelectionGroup(BenchmarkComparisonFormatter.TsavoriteFLinq,"FLinq",[
                BenchmarkComparisonFormatter.TsavoriteFLinq,
            ]),
            new BenchmarkColumnSelectionGroup(BenchmarkComparisonFormatter.TsavoriteLinq,"Linq",[
                BenchmarkComparisonFormatter.TsavoriteLinq,
            ]),
        ];
        this.selectedComparisonPair=this.ComparisonPairs[0];
        this.RunModes=[
            new RunModeOption("Adaptive",BenchmarkRunMode.Adaptive),
            new RunModeOption("Fixed",BenchmarkRunMode.Fixed),
        ];
        this.selectedRunMode=this.RunModes[0];
        this.ThresholdOptions=[
            new ThresholdOption("1 ms",1),
            new ThresholdOption("2 ms",2),
            new ThresholdOption("4 ms",4),
            new ThresholdOption("8 ms",8),
            new ThresholdOption("16 ms",16),
            new ThresholdOption("32 ms",32),
            new ThresholdOption("64 ms",64),
            new ThresholdOption("128 ms",128),
            new ThresholdOption("256 ms",256),
            new ThresholdOption("512 ms",512),
            new ThresholdOption("1024 ms",1024),
        ];
        this.selectedThreshold=this.ThresholdOptions[0];
        this.Metrics=[
            new MetricOption("Time",BenchmarkMetricKind.Time),
            new MetricOption("Allocated",BenchmarkMetricKind.Allocated),
        ];
        this.selectedMetric=this.Metrics[0];
        this.ChartModes=[
            new ChartModeOption("N comparison",BenchmarkChartMode.ElementComparison),
            new ChartModeOption("Scale trend",BenchmarkChartMode.ScaleTrend),
        ];
        this.selectedChartMode=this.ChartModes[0];
        this.ScaleOptions=[
            new ScaleOption("1/8","0.125"),
            new ScaleOption("1/4","0.25"),
            new ScaleOption("1/2","0.5"),
            new ScaleOption("1","1"),
            new ScaleOption("2","2"),
            new ScaleOption("4","4"),
            new ScaleOption("8","8"),
        ];
        this.selectedScale=this.ScaleOptions.First(static item => item.DisplayName=="1");
        foreach(var value in BenchmarkElementCounts.All){
            this.ElementCountOptions.Add(new ElementCountOption(value,BenchmarkElementCounts.DefaultInitial.Contains(value)));
        }
        this.RefreshCommand=new AsyncRelayCommand(this.RefreshAsync);
        this.SelectAllCommand=new RelayCommand(() => this.SetAllBenchmarkSelection(true));
        this.ClearSelectionCommand=new RelayCommand(() => this.SetAllBenchmarkSelection(false));
        this.RunCommand=new AsyncRelayCommand(this.RunAsync,() => !this.IsRunning);
        this.CancelCommand=new RelayCommand(this.Cancel);
        this.ExportCsvCommand=new AsyncRelayCommand(this.ExportCsvAsync);
        this.LoadCsvCommand=new AsyncRelayCommand(this.LoadCsvAsync);
        this.ShowCpuCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Cpu);
        this.ShowChartCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Chart);

        foreach(var cpu in this.cpuTopologyService.Load()){
            this.CpuItems.Add(cpu);
            cpu.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(CpuSelectionItem.IsSelected)&&!this.isApplyingCpuSelection){
                    this.EnsureAtLeastOneCpuSelected(cpu);
                    this.RefreshCpuSectionStates();
                }
            };
        }
        this.RebuildCpuSections();
    }

    public ObservableCollection<BenchmarkTableRow> BenchmarkRows { get; } = [];
    public ObservableCollection<BenchmarkResultItem> ResultItems { get; } = [];
    public ObservableCollection<BenchmarkResultItem> VisibleResultItems { get; } = [];
    public ObservableCollection<CpuSelectionItem> CpuItems { get; } = [];
    public ObservableCollection<CpuTableSection> CpuSections { get; } = [];
    public ObservableCollection<ChartComparisonPoint> ChartPoints { get; } = [];
    public ObservableCollection<ScaleOption> ScaleOptions { get; } = [];
    public ObservableCollection<ElementCountOption> ElementCountOptions { get; } = [];
    public ObservableCollection<ResultElementCountOption> ResultElementCountOptions { get; } = [];
    public ObservableCollection<CsvDateOption> CsvDateOptions { get; } = [];
    public ObservableCollection<DateTime> CsvCalendarDates { get; } = [];
    public ObservableCollection<string> ChartWarnings { get; } = [];
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkColumnSelectionGroups { get; }
    public IReadOnlyList<BenchmarkColumnSelectionGroup> BenchmarkProjectSelectionGroups { get; }
    public IReadOnlyList<RunModeOption> RunModes { get; }
    public IReadOnlyList<ComparisonPairOption> ComparisonPairs { get; }
    public IReadOnlyList<MetricOption> Metrics { get; }
    public IReadOnlyList<ChartModeOption> ChartModes { get; }
    public IReadOnlyList<ThresholdOption> ThresholdOptions { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RunCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand ExportCsvCommand { get; }
    public IAsyncRelayCommand LoadCsvCommand { get; }
    public IRelayCommand ShowCpuCommand { get; }
    public IRelayCommand ShowChartCommand { get; }

    public string StatusText{
        get => this.statusText;
        set => this.SetProperty(ref this.statusText,value);
    }

    public bool IsRunning{
        get => this.isRunning;
        private set{
            if(this.SetProperty(ref this.isRunning,value)){
                this.RunCommand.NotifyCanExecuteChanged();
                this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
            }
        }
    }

    public bool IsBenchmarkRunActive => this.runCancellation is not null&&this.IsRunning;

    public bool HasPendingCloseStateSave{
        get => this.hasPendingCloseStateSave;
        private set => this.SetProperty(ref this.hasPendingCloseStateSave,value);
    }

    public bool? IsAllBenchmarkSelectionChecked{
        get => this.isAllBenchmarkSelectionChecked;
        private set => this.SetProperty(ref this.isAllBenchmarkSelectionChecked,value);
    }

    public async Task CancelAndWaitAsync(){
        if(!this.IsBenchmarkRunActive){
            return;
        }

        this.Cancel();
        while(this.IsBenchmarkRunActive){
            await Task.Delay(100);
        }
    }

    public BenchmarkTab SelectedTab{
        get => this.selectedTab;
        set{
            if(this.SetProperty(ref this.selectedTab,value)){
                this.OnPropertyChanged(nameof(this.IsCpuVisible));
                this.OnPropertyChanged(nameof(this.IsChartVisible));
            }
        }
    }

    public bool IsCpuVisible => this.SelectedTab==BenchmarkTab.Cpu;
    public bool IsChartVisible => this.SelectedTab==BenchmarkTab.Chart;

    public ComparisonPairOption SelectedComparisonPair{
        get => this.selectedComparisonPair;
        set{
            if(this.SetProperty(ref this.selectedComparisonPair,value)){
                this.OnPropertyChanged(nameof(this.ChartLeftLegendLabel));
                this.OnPropertyChanged(nameof(this.ChartRightLegendLabel));
                this.UpdateChart();
            }
        }
    }

    public string ChartLeftLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.LeftProject);
    public string ChartRightLegendLabel => GetChartLegendLabel(this.SelectedComparisonPair.RightProject);

    public ScaleOption? SelectedScale{
        get => this.selectedScale;
        set => this.SetProperty(ref this.selectedScale,value);
    }

    public RunModeOption SelectedRunMode{
        get => this.selectedRunMode;
        set => this.SetProperty(ref this.selectedRunMode,value);
    }

    public ThresholdOption SelectedThreshold{
        get => this.selectedThreshold;
        set => this.SetProperty(ref this.selectedThreshold,value);
    }

    public CsvDateOption? SelectedCsvDateOption{
        get => this.selectedCsvDateOption;
        set{
            if(this.SetProperty(ref this.selectedCsvDateOption,value)){
                this.selectedCalendarDate=value?.Timestamp.ToLocalTime().Date;
                this.OnPropertyChanged(nameof(this.SelectedCalendarDate));
                if(!this.isApplyingUiState){
                    this.ApplyCsvDateFilter(value?.Timestamp);
                    this.SaveUiState();
                }
            }
        }
    }

    public DateTime? SelectedCalendarDate{
        get => this.selectedCalendarDate;
        set{
            if(this.SetProperty(ref this.selectedCalendarDate,value)){
                if(value is { } date){
                    this.SelectedCsvDateOption=this.CsvDateOptions
                        .Where(option => option.Timestamp.ToLocalTime().Date==date.Date)
                        .OrderByDescending(static option => option.Timestamp)
                        .FirstOrDefault();
                }
            }
        }
    }

    public string AdaptiveThresholdSecondsText{
        get => this.adaptiveThresholdSecondsText;
        set => this.SetProperty(ref this.adaptiveThresholdSecondsText,value);
    }

    public MetricOption SelectedMetric{
        get => this.selectedMetric;
        set{
            if(this.SetProperty(ref this.selectedMetric,value)){
                this.UpdateChart();
            }
        }
    }

    public ChartModeOption SelectedChartMode{
        get => this.selectedChartMode;
        set{
            if(this.SetProperty(ref this.selectedChartMode,value)){
                this.UpdateChart();
            }
        }
    }

    public ResultElementCountOption? SelectedResultElementCount{
        get => this.selectedResultElementCount;
        set{
            if(this.SetProperty(ref this.selectedResultElementCount,value)){
                this.RefreshVisibleResults();
                this.UpdateChart();
            }
        }
    }

    public double ChartHeight{
        get => this.chartHeight;
        private set => this.SetProperty(ref this.chartHeight,value);
    }

    public async Task InitializeAsync(){
        await this.RefreshAsync();
    }

    public void SortBenchmarkRows(BenchmarkGridSortColumn column){
        if(this.currentSortColumn==column){
            this.currentSortDescending=!this.currentSortDescending;
        }else{
            this.currentSortColumn=column;
            this.currentSortDescending=false;
        }

        this.ApplyCurrentSort();
    }

    public void ToggleCpu(CpuSelectionItem cpu){
        cpu.IsSelected=!cpu.IsSelected;
    }

    public void ToggleCpuSection(CpuTableSection section){
        var selected = section.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(section.Cells.Select(static cell => cell.Cpu),selected);
    }

    public void ToggleCpuColumn(CpuTableSection section,int columnIndex){
        var cells = section.Rows
            .Where(row => columnIndex<row.Cells.Count)
            .Select(row => row.Cells[columnIndex])
            .ToArray();
        var selected = section.GetColumnState(columnIndex)!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(cells.Select(static cell => cell.Cpu),selected);
    }

    public void ToggleCpuRow(CpuTableRow row){
        var selected = row.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(row.Cells.Select(static cell => cell.Cpu),selected);
    }

    public void ToggleAllBenchmarkSelection(){
        this.SetBenchmarkCells(this.BenchmarkRows.SelectMany(static row => row.ProjectCells),this.IsAllBenchmarkSelectionChecked is false);
    }

    public void ToggleBenchmarkColumnGroup(BenchmarkColumnSelectionGroup group){
        var selected = group.IsChecked is false;
        this.SetBenchmarkCells(this.GetBenchmarkCells(group.ColumnKeys),selected);
    }

    public void ToggleBenchmarkRow(BenchmarkTableRow row){
        this.SetBenchmarkCells(row.ProjectCells,row.IsSelectionChecked is false);
    }

    private async Task RefreshAsync(){
        this.IsRunning=true;
        this.StatusText="Loading";
        try{
            await this.RefreshCsvScanAsync(CancellationToken.None);
            var savedState = this.uiStateStore.Load();
            var loaded = await this.catalogService.LoadAsync(CancellationToken.None);
            var existingSelection = this.BenchmarkRows
                .Where(static item => item.IsSelected)
                .Select(static item => item.MethodName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingCellSelection = this.BenchmarkRows
                .SelectMany(static row => row.ProjectCells.Select(cell => new {
                    Key=BenchmarkUiStateStore.CreateCellKey(row.MethodName,cell.Project),
                    cell.IsSelected,
                }))
                .ToDictionary(static item => item.Key,static item => item.IsSelected,StringComparer.OrdinalIgnoreCase);
            this.BenchmarkRows.Clear();
            foreach(var row in this.CreateMethodRows(loaded)){
                this.SubscribeBenchmarkRow(row);
                row.IsSelected=existingSelection.Count==0
                    ? savedState.RowSelections.GetValueOrDefault(row.MethodName,true)
                    : existingSelection.Contains(row.MethodName);
                foreach(var cell in row.ProjectCells){
                    var key = BenchmarkUiStateStore.CreateCellKey(row.MethodName,cell.Project);
                    if(existingCellSelection.TryGetValue(key,out var existing)){
                        cell.IsSelected=existing;
                    }else if(savedState.ProjectCellSelections.TryGetValue(key,out var saved)){
                        cell.IsSelected=saved;
                    }
                }
                this.BenchmarkRows.Add(row);
            }
            this.ApplyComparisonRowsToTable();
            this.ApplyStoredResultsToTable();
            this.RestoreCsvDateSelection(savedState.SelectedCsvTimestamp);
            this.RefreshBenchmarkSelectionAggregates();
            this.ApplyCurrentSort();
            this.StatusText=this.BenchmarkRows.Count+" benchmarks";
        }catch(Exception ex){
            StartupDiagnostics.Write("Benchmark catalog load failed.",ex);
            this.StatusText="Load failed: "+ex.Message;
        }finally{
            this.IsRunning=false;
        }
    }

    private IReadOnlyList<BenchmarkTableRow> CreateMethodRows(IReadOnlyList<BenchmarkCase> cases){
        return cases
            .Where(static item => DisplayBenchmarkProjects.Contains(item.Project))
            .GroupBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static group => new BenchmarkTableRow(
                group.Key,
                group
                    .GroupBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(static item => item.Key,static item => item.First(),StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private void SetAllBenchmarkSelection(bool selected){
        this.SetBenchmarkCells(this.BenchmarkRows.SelectMany(static row => row.ProjectCells),selected);
    }

    private void SubscribeBenchmarkRow(BenchmarkTableRow row){
        row.PropertyChanged+=(_,args) => {
            if(args.PropertyName==nameof(BenchmarkTableRow.IsSelected)&&!this.isApplyingUiState&&!this.isApplyingBenchmarkSelection){
                this.SaveUiState();
            }
        };
        foreach(var cell in row.ProjectCells){
            cell.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(BenchmarkProjectCell.IsSelected)&&!this.isApplyingUiState&&!this.isApplyingBenchmarkSelection){
                    this.RefreshBenchmarkSelectionAggregates();
                    this.SaveUiState();
                }
            };
        }
    }

    private IEnumerable<BenchmarkProjectCell> GetBenchmarkCells(IReadOnlyList<string> projectKeys){
        foreach(var row in this.BenchmarkRows){
            foreach(var project in projectKeys){
                if(row.GetProjectCell(project) is { } cell){
                    yield return cell;
                }
            }
        }
    }

    private void SetBenchmarkCells(IEnumerable<BenchmarkProjectCell> cells,bool selected){
        var targets = cells.Where(static cell => cell.HasCase).Distinct().ToArray();
        if(targets.Length==0){
            this.RefreshBenchmarkSelectionAggregates();
            return;
        }

        this.isApplyingBenchmarkSelection=true;
        try{
            foreach(var cell in targets){
                cell.IsSelected=selected;
            }
            this.RefreshBenchmarkSelectionAggregates();
        }finally{
            this.isApplyingBenchmarkSelection=false;
        }
        this.SaveUiState();
    }

    private void RefreshBenchmarkSelectionAggregates(){
        foreach(var row in this.BenchmarkRows){
            row.RefreshSelectionState();
        }
        foreach(var group in this.BenchmarkColumnSelectionGroups){
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys));
        }
        foreach(var group in this.BenchmarkProjectSelectionGroups){
            group.IsChecked=GetSelectionState(this.GetBenchmarkCells(group.ColumnKeys));
        }
        this.IsAllBenchmarkSelectionChecked=GetSelectionState(this.BenchmarkRows.SelectMany(static row => row.ProjectCells));
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

    private async Task RefreshCsvScanAsync(CancellationToken cancellationToken){
        var scan = await Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            return this.csvDiscoveryService.Scan(this.benchmarkRoot);
        },cancellationToken);
        this.resultStore.UpsertMany(scan.Results);
        this.resultStore.Save();
        this.CsvDateOptions.Clear();
        foreach(var option in scan.DateOptions){
            this.CsvDateOptions.Add(option);
        }
        this.CsvCalendarDates.Clear();
        foreach(var date in scan.CalendarDates){
            this.CsvCalendarDates.Add(date);
        }
        this.comparisonRows=this.resultStore.BuildLatestComparisonRows();
        this.UpdateChart();
    }

    private void RestoreCsvDateSelection(DateTimeOffset? timestamp){
        if(timestamp is null){
            return;
        }

        this.isApplyingUiState=true;
        try{
            this.SelectedCsvDateOption=this.CsvDateOptions.FirstOrDefault(option => option.Timestamp==timestamp.Value);
            if(this.SelectedCsvDateOption is { } selected){
                this.selectedCalendarDate=selected.Timestamp.ToLocalTime().Date;
                this.OnPropertyChanged(nameof(this.SelectedCalendarDate));
            }
        }finally{
            this.isApplyingUiState=false;
            this.RefreshBenchmarkSelectionAggregates();
        }
    }

    private void ApplyStoredResultsToTable(){
        foreach(var row in this.BenchmarkRows){
            foreach(var cell in row.ProjectCells){
                var stored = this.resultStore.GetLatest(row.MethodName,cell.Project);
                if(stored is not null){
                    row.ApplyStoredResult(stored);
                }
            }
        }
    }

    private void ApplyCsvDateFilter(DateTimeOffset? selectedTimestamp){
        if(selectedTimestamp is null){
            return;
        }

        this.isApplyingUiState=true;
        try{
            foreach(var row in this.BenchmarkRows){
                var anySelected = false;
                foreach(var cell in row.ProjectCells){
                    if(!cell.HasCase){
                        continue;
                    }

                    var lastMeasuredAt = this.resultStore.GetLatestMeasuredAt(row.MethodName,cell.Project);
                    var shouldRun = lastMeasuredAt is null||lastMeasuredAt.Value<selectedTimestamp.Value;
                    cell.IsSelected=shouldRun;
                    anySelected|=shouldRun;
                }
                row.IsSelected=anySelected;
            }
        }finally{
            this.isApplyingUiState=false;
        }
    }

    public void SaveUiState(){
        var state = new BenchmarkUiState(
            this.SelectedCsvDateOption?.Timestamp,
            this.BenchmarkRows
                .SelectMany(static row => row.ProjectCells.Select(cell => new {
                    Key=BenchmarkUiStateStore.CreateCellKey(row.MethodName,cell.Project),
                    cell.IsSelected,
                }))
                .ToDictionary(static item => item.Key,static item => item.IsSelected,StringComparer.OrdinalIgnoreCase),
            this.BenchmarkRows.ToDictionary(static row => row.MethodName,static row => row.IsSelected,StringComparer.OrdinalIgnoreCase));
        this.uiStateStore.Save(state);
        this.HasPendingCloseStateSave=false;
    }

    public void DiscardPendingCloseStateSave(){
        this.HasPendingCloseStateSave=false;
    }

    private async Task RunAsync(){
        var selected = this.BenchmarkRows
            .Where(static item => item.IsSelected)
            .SelectMany(static item => item.GetRunCases())
            .ToArray();
        if(selected.Length==0){
            this.StatusText="No benchmark selected";
            return;
        }

        this.SelectedTab=BenchmarkTab.Cpu;
        this.runCancellation?.Cancel();
        this.runCancellation=new CancellationTokenSource();
        this.IsRunning=true;
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        this.ResultElementCountOptions.Clear();
        this.SelectedResultElementCount=null;
        try{
            var cpus = this.CpuItems
                .Where(static cpu => cpu.IsSelected)
                .Select(static cpu => cpu.LogicalProcessorIndex)
                .ToArray();
            _ = await this.runService.RunAsync(
                selected,
                cpus,
                this.GetSelectedElementCounts(),
                this.SelectedRunMode.Mode,
                this.GetAdaptiveThreshold(),
                result => MainThread.BeginInvokeOnMainThread(() => {
                    this.HandleRunResult(result);
                }),
                status => MainThread.BeginInvokeOnMainThread(() => this.StatusText=status),
                this.runCancellation.Token);
            await this.RefreshCsvScanAsync(this.runCancellation.Token);
        }catch(OperationCanceledException){
            this.StatusText="Canceled";
        }finally{
            this.IsRunning=false;
            this.runCancellation?.Dispose();
            this.runCancellation=null;
            this.OnPropertyChanged(nameof(this.IsBenchmarkRunActive));
            this.comparisonRows=this.resultStore.BuildLatestComparisonRows();
            this.ApplyComparisonRowsToTable();
            this.ApplyStoredResultsToTable();
            this.UpdateChart();
        }
    }

    private void Cancel(){
        this.runCancellation?.Cancel();
    }

    private void HandleRunResult(BenchmarkResultItem result){
        var cpu = result.AssignedLogicalCpu is { } logicalCpu
            ? this.CpuItems.FirstOrDefault(item => item.LogicalProcessorIndex==logicalCpu)
            : null;
        cpu?.ApplyJob(result);
        this.UpsertResultItem(result);
        this.RegisterResultElementCount(result.ElementCount);
        this.RefreshVisibleResults();
        var row = this.BenchmarkRows.FirstOrDefault(item => string.Equals(item.MethodName,result.Method,StringComparison.OrdinalIgnoreCase));
        row?.ApplyRunResult(result,cpu);
        if(string.Equals(result.Status,"Completed",StringComparison.OrdinalIgnoreCase)
            ||string.Equals(result.Status,"Failed",StringComparison.OrdinalIgnoreCase)){
            this.resultStore.Upsert(result,DateTimeOffset.Now,null,string.Empty);
            this.resultStore.Save();
            this.HasPendingCloseStateSave=true;
            this.comparisonRows=this.resultStore.BuildLatestComparisonRows();
            this.ApplyComparisonRowsToTable();
            this.ApplyStoredResultsToTable();
            this.UpdateChart();
        }
    }

    private void UpsertResultItem(BenchmarkResultItem result){
        for(var index=this.ResultItems.Count-1;index>=0;index--){
            var item = this.ResultItems[index];
            if(string.Equals(item.Project,result.Project,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(item.Method,result.Method,StringComparison.OrdinalIgnoreCase)
                &&item.ElementCount==result.ElementCount){
                this.ResultItems.RemoveAt(index);
            }
        }
        this.ResultItems.Add(result);
    }

    private IReadOnlyList<int> GetSelectedElementCounts(){
        var selected = this.ElementCountOptions
            .Where(static item => item.IsSelected)
            .Select(static item => item.Value)
            .ToArray();
        return selected.Length==0 ? BenchmarkElementCounts.DefaultInitial : selected;
    }

    private TimeSpan GetAdaptiveThreshold(){
        return TimeSpan.FromMilliseconds(this.SelectedThreshold.Milliseconds);
    }

    private async Task ExportCsvAsync(){
        await Task.Yield();
        if(this.ResultItems.Count==0&&this.comparisonRows.Count==0){
            this.StatusText="No results";
            return;
        }

        var root = Path.Combine(
            this.FindSolutionRoot()??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Work",
            "Benchmark",
            DateTime.Now.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture));
        var rows = this.comparisonRows.Count>0
            ? this.comparisonRows
            : this.comparisonService.BuildRows(this.ResultItems);
        var path = this.csvService.WriteComparisonCsv(root,rows);
        this.StatusText="CSV saved: "+path;
    }

    private async Task LoadCsvAsync(){
        var file = await FilePicker.PickAsync(new PickOptions{
            PickerTitle="BenchmarkComparison.csv",
            FileTypes=new FilePickerFileType(new Dictionary<DevicePlatform,IEnumerable<string>>{
                { DevicePlatform.WinUI, [".csv"] },
                { DevicePlatform.macOS, ["csv"] },
                { DevicePlatform.MacCatalyst, ["public.comma-separated-values-text"] },
            }),
        });
        if(file is null){
            return;
        }

        try{
            this.comparisonRows=this.csvService.ReadComparisonCsv(file.FullPath);
            this.LoadComparisonRowsIntoResults(this.comparisonRows);
            this.ApplyComparisonRowsToTable();
            this.UpdateChart();
            this.ApplyCurrentSort();
            this.SelectedTab=BenchmarkTab.Chart;
            this.StatusText="CSV loaded: "+file.FullPath;
        }catch(Exception ex){
            StartupDiagnostics.Write("CSV load failed.",ex);
            this.StatusText="CSV load failed: "+ex.Message;
        }
    }

    private void UpdateComparisonRowsFromResults(){
        if(this.ResultItems.Count==0){
            this.comparisonRows=[];
            this.UpdateChart();
            this.ApplyComparisonRowsToTable();
            return;
        }

        try{
            this.comparisonRows=this.comparisonService.BuildRows(this.ResultItems);
            this.UpdateChart();
            this.ApplyComparisonRowsToTable();
            this.ApplyCurrentSort();
        }catch(InvalidDataException){
        }
    }

    private void RegisterResultElementCount(int elementCount){
        if(elementCount<=0||this.ResultElementCountOptions.Any(item => item.Value==elementCount)){
            return;
        }

        var option = new ResultElementCountOption(elementCount,"N="+elementCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture));
        var ordered = this.ResultElementCountOptions
            .Append(option)
            .OrderBy(static item => item.Value)
            .ToArray();
        this.ResultElementCountOptions.Clear();
        foreach(var item in ordered){
            this.ResultElementCountOptions.Add(item);
        }
        this.SelectedResultElementCount??=this.ResultElementCountOptions.FirstOrDefault();
    }

    private void RefreshVisibleResults(){
        this.VisibleResultItems.Clear();
        var selectedN = this.SelectedResultElementCount?.Value;
        foreach(var item in this.ResultItems.Where(item => selectedN is null||item.ElementCount==0||item.ElementCount==selectedN)){
            this.VisibleResultItems.Add(item);
        }
    }

    private void LoadComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows){
        this.ResultItems.Clear();
        this.VisibleResultItems.Clear();
        foreach(var row in rows){
            this.AddResult(row.Benchmark,BenchmarkComparisonFormatter.SystemFLinq,row.SystemFLinq);
            this.AddResult(row.Benchmark,BenchmarkComparisonFormatter.SystemZLinq,row.SystemZLinq);
            this.AddResult(row.Benchmark,BenchmarkComparisonFormatter.PersistedFLinq,row.PersistedFLinq);
            this.AddResult(row.Benchmark,BenchmarkComparisonFormatter.PersistedLinq,row.PersistedLinq);
            this.AddResult(row.Benchmark,BenchmarkComparisonFormatter.TsavoriteFLinq,row.TsavoriteFLinq);
            this.AddResult(row.Benchmark,BenchmarkComparisonFormatter.TsavoriteLinq,row.TsavoriteLinq);
        }
    }

    private void AddResult(string method,string project,ProjectMetrics metrics){
        if(metrics.HasValue){
            this.ResultItems.Add(new BenchmarkResultItem(project,method,metrics));
        }
    }

    private void ApplyComparisonRowsToTable(){
        var rowsByMethod = this.comparisonRows.ToDictionary(static item => item.Benchmark,StringComparer.OrdinalIgnoreCase);
        foreach(var benchmark in this.BenchmarkRows){
            benchmark.ApplyComparison(rowsByMethod.TryGetValue(benchmark.MethodName,out var row) ? row : null);
        }
    }

    private void UpdateChart(){
        this.ChartPoints.Clear();
        this.ChartWarnings.Clear();
        var points = this.SelectedChartMode.Mode==BenchmarkChartMode.ScaleTrend
            ? this.BuildScaleTrendPoints()
            : this.comparisonService.BuildChartPoints(this.comparisonRows,this.SelectedComparisonPair,this.SelectedMetric.Kind);
        foreach(var item in points){
            this.ChartPoints.Add(item);
        }
        if(this.SelectedChartMode.Mode==BenchmarkChartMode.ElementComparison){
            foreach(var warning in this.BuildChartWarnings()){
                this.ChartWarnings.Add(warning);
            }
        }
        this.ChartHeight=Math.Max(300,this.ChartPoints.Count*28+72);
    }

    private IEnumerable<string> BuildChartWarnings(){
        foreach(var row in this.comparisonRows){
            var left = row.GetProject(this.SelectedComparisonPair.LeftProject);
            var right = row.GetProject(this.SelectedComparisonPair.RightProject);
            if(left.HasValue==right.HasValue){
                continue;
            }

            var missing = left.HasValue ? this.SelectedComparisonPair.RightProject : this.SelectedComparisonPair.LeftProject;
            yield return BenchmarkFormatting.ShortName(row.Benchmark)+" / "+missing+" 未計測";
        }
    }

    private IReadOnlyList<ChartComparisonPoint> BuildScaleTrendPoints(){
        var method = this.BenchmarkRows.FirstOrDefault(static item => item.IsSelected)?.MethodName
            ??this.ResultItems.FirstOrDefault()?.Method
            ??string.Empty;
        if(string.IsNullOrWhiteSpace(method)){
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
        BenchmarkMetricKind metric){
        var left = results.FirstOrDefault(item => string.Equals(item.Project,pair.LeftProject,StringComparison.OrdinalIgnoreCase));
        var right = results.FirstOrDefault(item => string.Equals(item.Project,pair.RightProject,StringComparison.OrdinalIgnoreCase));
        if(left is null&&right is null){
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

    private static double? GetMetricValue(BenchmarkResultItem? item,BenchmarkMetricKind metric){
        if(item is null){
            return null;
        }

        return metric==BenchmarkMetricKind.Time
            ? item.MeanNanoseconds
            : item.AllocatedBytesValue;
    }

    private static string FormatMetric(double? value,BenchmarkMetricKind metric){
        if(value is null){
            return string.Empty;
        }

        return metric==BenchmarkMetricKind.Time
            ? BenchmarkFormatting.FormatNanoseconds(value)
            : BenchmarkFormatting.FormatBytes((long)Math.Round(value.Value,MidpointRounding.AwayFromZero));
    }

    private static string FirstText(params string[] values){
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;
    }

    private static string GetChartLegendLabel(string project) =>
        project switch{
            BenchmarkComparisonFormatter.SystemFLinq => "FLinq",
            BenchmarkComparisonFormatter.SystemZLinq => "ZLinq",
            BenchmarkComparisonFormatter.PersistedFLinq => "FLinq",
            BenchmarkComparisonFormatter.PersistedLinq => "Linq",
            BenchmarkComparisonFormatter.TsavoriteFLinq => "FLinq",
            BenchmarkComparisonFormatter.TsavoriteLinq => "Linq",
            _ => project,
        };

    private void ApplyCurrentSort(){
        if(this.BenchmarkRows.Count<2){
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
        foreach(var row in ordered){
            this.BenchmarkRows.Add(row);
        }
    }

    private void RebuildCpuSections(){
        this.CpuSections.Clear();
        this.AddCpuSection("P core",CpuCoreKind.Performance);
        this.AddCpuSection("E core",CpuCoreKind.Efficiency);
        this.AddCpuSection("Unknown core",CpuCoreKind.Unknown);
    }

    private void AddCpuSection(string title,CpuCoreKind kind){
        var groups = this.CpuItems
            .Where(cpu => cpu.CoreKind==kind)
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Min(static cpu => cpu.LogicalProcessorIndex))
            .Select(static group => group.OrderBy(static cpu => cpu.LogicalProcessorIndex).ToArray())
            .Where(static group => group.Length>0)
            .ToArray();
        if(groups.Length==0){
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

    private static string CreateCpuRangeLabel(IReadOnlyList<CpuSelectionItem> cpus){
        var min = cpus.Min(static cpu => cpu.LogicalProcessorIndex);
        var max = cpus.Max(static cpu => cpu.LogicalProcessorIndex);
        return min==max
            ? min.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : min.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+max.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ApplyCpuSelection(IEnumerable<CpuSelectionItem> cpus,bool selected){
        var targets = cpus.Distinct().ToArray();
        if(targets.Length==0){
            return;
        }

        this.isApplyingCpuSelection=true;
        try{
            if(!selected){
                var selectedOutsideTargets = this.CpuItems.Any(cpu => cpu.IsSelected&&!targets.Contains(cpu));
                if(!selectedOutsideTargets&&this.CpuItems.Any(static cpu => cpu.IsSelected)){
                    var keep = targets.FirstOrDefault(static cpu => cpu.IsSelected)??targets[0];
                    foreach(var cpu in targets){
                        cpu.IsSelected=ReferenceEquals(cpu,keep);
                    }
                    return;
                }
            }

            foreach(var cpu in targets){
                cpu.IsSelected=selected;
            }
        }finally{
            this.isApplyingCpuSelection=false;
            this.EnsureAtLeastOneCpuSelected(null);
            this.RefreshCpuSectionStates();
        }
    }

    private void EnsureAtLeastOneCpuSelected(CpuSelectionItem? fallback){
        if(this.CpuItems.Count==0||this.CpuItems.Any(static cpu => cpu.IsSelected)){
            return;
        }

        (fallback??this.CpuItems[0]).IsSelected=true;
    }

    private void RefreshCpuSectionStates(){
        foreach(var section in this.CpuSections){
            section.RefreshState();
            foreach(var column in section.Columns){
                this.OnPropertyChanged("CpuColumnState"+section.Title+column.Index);
            }
        }
    }

    private string? FindSolutionRoot(){
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null){
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }
}
