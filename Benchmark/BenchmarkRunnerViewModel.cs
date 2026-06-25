using System.Collections.ObjectModel;
using Benchmark.Worker;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Benchmark;

public sealed class BenchmarkRunnerViewModel:ObservableObject{
    private readonly IBenchmarkCatalogService catalogService;
    private readonly ICpuTopologyService cpuTopologyService;
    private readonly IBenchmarkRunService runService;
    private readonly IBenchmarkCsvService csvService;
    private readonly IBenchmarkComparisonService comparisonService;
    private CancellationTokenSource? runCancellation;
    private string statusText = "Ready";
    private bool isRunning;
    private BenchmarkTab selectedTab = BenchmarkTab.Cpu;
    private ComparisonPairOption selectedComparisonPair;
    private ScaleOption? selectedScale;
    private MetricOption selectedMetric;
    private double chartHeight = 300;
    private bool isApplyingCpuSelection;
    private BenchmarkGridSortColumn currentSortColumn = BenchmarkGridSortColumn.BenchmarkName;
    private bool currentSortDescending;
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows = [];

    public BenchmarkRunnerViewModel(
        IBenchmarkCatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IBenchmarkRunService runService,
        IBenchmarkCsvService csvService,
        IBenchmarkComparisonService comparisonService){
        this.catalogService=catalogService;
        this.cpuTopologyService=cpuTopologyService;
        this.runService=runService;
        this.csvService=csvService;
        this.comparisonService=comparisonService;
        this.ComparisonPairs=[
            new ComparisonPairOption("System","System FLinq/ZLinq",BenchmarkComparisonFormatter.SystemFLinq,BenchmarkComparisonFormatter.SystemZLinq),
            new ComparisonPairOption("Persisted","PersistedFLinq/PersistedLinq",BenchmarkComparisonFormatter.PersistedFLinq,BenchmarkComparisonFormatter.PersistedLinq),
            new ComparisonPairOption("Tsavorite","TsavoriteFLinq/TsavoriteLinq",BenchmarkComparisonFormatter.TsavoriteFLinq,BenchmarkComparisonFormatter.TsavoriteLinq),
            new ComparisonPairOption("PersistedLinqTsavoriteLinq","PersistedLinq/TsavoriteLinq",BenchmarkComparisonFormatter.PersistedLinq,BenchmarkComparisonFormatter.TsavoriteLinq),
            new ComparisonPairOption("PersistedFLinqTsavoriteFLinq","PersistedFLinq/TsavoriteFLinq",BenchmarkComparisonFormatter.PersistedFLinq,BenchmarkComparisonFormatter.TsavoriteFLinq),
        ];
        this.selectedComparisonPair=this.ComparisonPairs[0];
        this.Metrics=[
            new MetricOption("Time",BenchmarkMetricKind.Time),
            new MetricOption("Allocated",BenchmarkMetricKind.Allocated),
        ];
        this.selectedMetric=this.Metrics[0];
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
        this.RefreshCommand=new AsyncRelayCommand(this.RefreshAsync);
        this.SelectAllCommand=new RelayCommand(() => this.SetAllBenchmarkSelection(true));
        this.ClearSelectionCommand=new RelayCommand(() => this.SetAllBenchmarkSelection(false));
        this.RunCommand=new AsyncRelayCommand(this.RunAsync,() => !this.IsRunning);
        this.CancelCommand=new RelayCommand(this.Cancel);
        this.ExportCsvCommand=new AsyncRelayCommand(this.ExportCsvAsync);
        this.LoadCsvCommand=new AsyncRelayCommand(this.LoadCsvAsync);
        this.ShowCpuCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Cpu);
        this.ShowResultsCommand=new RelayCommand(() => this.SelectedTab=BenchmarkTab.Results);
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
    public ObservableCollection<CpuSelectionItem> CpuItems { get; } = [];
    public ObservableCollection<CpuTableSection> CpuSections { get; } = [];
    public ObservableCollection<ChartComparisonPoint> ChartPoints { get; } = [];
    public ObservableCollection<ScaleOption> ScaleOptions { get; } = [];
    public IReadOnlyList<ComparisonPairOption> ComparisonPairs { get; }
    public IReadOnlyList<MetricOption> Metrics { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IAsyncRelayCommand RunCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand ExportCsvCommand { get; }
    public IAsyncRelayCommand LoadCsvCommand { get; }
    public IRelayCommand ShowCpuCommand { get; }
    public IRelayCommand ShowResultsCommand { get; }
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
            }
        }
    }

    public BenchmarkTab SelectedTab{
        get => this.selectedTab;
        set{
            if(this.SetProperty(ref this.selectedTab,value)){
                this.OnPropertyChanged(nameof(this.IsCpuVisible));
                this.OnPropertyChanged(nameof(this.IsResultsVisible));
                this.OnPropertyChanged(nameof(this.IsChartVisible));
            }
        }
    }

    public bool IsCpuVisible => this.SelectedTab==BenchmarkTab.Cpu;
    public bool IsResultsVisible => this.SelectedTab==BenchmarkTab.Results;
    public bool IsChartVisible => this.SelectedTab==BenchmarkTab.Chart;

    public ComparisonPairOption SelectedComparisonPair{
        get => this.selectedComparisonPair;
        set{
            if(this.SetProperty(ref this.selectedComparisonPair,value)){
                this.UpdateChart();
            }
        }
    }

    public ScaleOption? SelectedScale{
        get => this.selectedScale;
        set => this.SetProperty(ref this.selectedScale,value);
    }

    public MetricOption SelectedMetric{
        get => this.selectedMetric;
        set{
            if(this.SetProperty(ref this.selectedMetric,value)){
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

    private async Task RefreshAsync(){
        this.IsRunning=true;
        this.StatusText="Loading";
        try{
            var loaded = await this.catalogService.LoadAsync(CancellationToken.None);
            var existingSelection = this.BenchmarkRows
                .Where(static item => item.IsSelected)
                .Select(static item => item.MethodName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            this.BenchmarkRows.Clear();
            foreach(var row in this.CreateMethodRows(loaded)){
                row.IsSelected=existingSelection.Contains(row.MethodName);
                this.BenchmarkRows.Add(row);
            }
            this.ApplyComparisonRowsToTable();
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
        foreach(var item in this.BenchmarkRows){
            item.IsSelected=selected;
        }
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

        this.SelectedTab=BenchmarkTab.Results;
        this.runCancellation?.Cancel();
        this.runCancellation=new CancellationTokenSource();
        this.IsRunning=true;
        this.ResultItems.Clear();
        this.comparisonRows=[];
        this.ChartPoints.Clear();
        this.ApplyComparisonRowsToTable();
        try{
            var cpus = this.CpuItems
                .Where(static cpu => cpu.IsSelected)
                .Select(static cpu => cpu.LogicalProcessorIndex)
                .ToArray();
            _ = await this.runService.RunAsync(
                selected,
                cpus,
                this.SelectedScale?.RunValue??"1",
                result => MainThread.BeginInvokeOnMainThread(() => {
                    this.ResultItems.Add(result);
                    this.UpdateComparisonRowsFromResults();
                }),
                status => MainThread.BeginInvokeOnMainThread(() => this.StatusText=status),
                this.runCancellation.Token);
        }catch(OperationCanceledException){
            this.StatusText="Canceled";
        }finally{
            this.IsRunning=false;
            this.runCancellation?.Dispose();
            this.runCancellation=null;
            this.UpdateComparisonRowsFromResults();
        }
    }

    private void Cancel(){
        this.runCancellation?.Cancel();
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

    private void LoadComparisonRowsIntoResults(IReadOnlyList<BenchmarkComparisonRow> rows){
        this.ResultItems.Clear();
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
        foreach(var item in this.comparisonService.BuildChartPoints(this.comparisonRows,this.SelectedComparisonPair,this.SelectedMetric.Kind)){
            this.ChartPoints.Add(item);
        }
        this.ChartHeight=Math.Max(300,this.ChartPoints.Count*28+72);
    }

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
            .Select(static group => new CpuTableRow(
                CreateCpuRangeLabel(group),
                group.Select(static cpu => new CpuTableCell(cpu)).ToArray()))
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
