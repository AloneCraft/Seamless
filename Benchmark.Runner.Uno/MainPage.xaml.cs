using System.Collections.ObjectModel;
using global::System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Benchmark.Runner.Uno;

public sealed partial class MainPage:Page{
    private const string TargetFramework = "net10.0";

    private readonly string repositoryRoot = FindRepositoryRoot();
    private readonly ObservableCollection<BenchmarkItem> benchmarks = [];
    private readonly ObservableCollection<CpuCoreInfo> cpuCores = [];
    private readonly ObservableCollection<CpuRowItem> cpuRows = [];
    private readonly ObservableCollection<BenchmarkRunItem> runningItems = [];
    private readonly ObservableCollection<BenchmarkTableColumn> benchmarkTableColumns = [];
    private readonly ObservableCollection<AnalysisRankingItem> rankingItems = [];
    private readonly ObservableCollection<AnalysisHeatmapRow> heatmapRows = [];
    private readonly ObservableCollection<AnalysisDetailItem> detailItems = [];
    private readonly Lock runGate = new();
    private readonly Queue<BenchmarkRunItem> pendingRunItems = new();
    private readonly List<BenchmarkRunItem> allRunItems = [];
    private readonly Dictionary<int,Task> cpuWorkers = [];
    private readonly Dictionary<int,Process> runningProcesses = [];
    private readonly Dictionary<int,BenchmarkRunItem> runningRunItems = [];
    private readonly HashSet<int> requeueOnProcessExit = [];
    private readonly Dictionary<string,IReadOnlyList<BenchmarkProjectMethod>> methodCache = new(StringComparer.Ordinal);
    private readonly List<CpuPresetDefinition> cpuPresets = [];
    private readonly IReadOnlyList<BenchmarkProject> benchmarkProjects = LoadBenchmarkProjects();
    private BenchmarkProject canonicalMethodProject => this.benchmarkProjects[0];

    private CancellationTokenSource? runCancellation;
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows = [];
    private string? currentRunRoot;
    private DateTimeOffset? currentRunStartedAt;
    private DateTimeOffset? currentRunFinishedAt;
    private int? selectionAnchorIndex;
    private bool isShiftDown;
    private bool isControlDown;
    private bool isBusy;
    private bool isRunning;
    private bool projectSelectionDirty = true;
    private BenchmarkSortColumn sortColumn = BenchmarkSortColumn.Name;
    private string? sortColumnKey;
    private bool sortDescending;
    private string selectedAnalysisPairKey = "System";
    private AnalysisMetric selectedAnalysisMetric = AnalysisMetric.Time;
    private AnalysisTab selectedAnalysisTab = AnalysisTab.Ranking;
    private string? selectedAnalysisMethod;
    private bool isChartCollapsed;

    private sealed record RunReportPaths(string? CsvPath);

    private enum BenchmarkSortColumn{
        Name,
        Time,
        Allocated,
    }

    private enum AnalysisMetric{
        Time,
        Allocated,
    }

    private enum AnalysisTab{
        Ranking,
        Heatmap,
        Detail,
    }

    private sealed class BenchmarkSortValueComparer(bool descending):IComparer<object?>{
        public int Compare(object? x,object? y){
            var result = CompareCore(x,y);
            return descending ? -result : result;
        }

        private static int CompareCore(object? x,object? y){
            if(x is string xText&&y is string yText){
                return string.Compare(xText,yText,StringComparison.OrdinalIgnoreCase);
            }

            if(x is IConvertible&&y is IConvertible){
                var xNumber = Convert.ToDouble(x,CultureInfo.InvariantCulture);
                var yNumber = Convert.ToDouble(y,CultureInfo.InvariantCulture);
                return xNumber.CompareTo(yNumber);
            }

            return string.Compare(Convert.ToString(x,CultureInfo.InvariantCulture),Convert.ToString(y,CultureInfo.InvariantCulture),StringComparison.OrdinalIgnoreCase);
        }
    }

    public MainPage(){
        this.InitializeComponent();
    }

    private async void OnPageLoaded(object sender,RoutedEventArgs e){
        this.InitializeBenchmarkTableColumns();
        this.InitializeProjectGrid();
        this.InitializeCpuList();
        this.BenchmarkItemsControl.ItemsSource= this.benchmarks;
        this.BenchmarkTableHeaderItemsControl.ItemsSource= this.benchmarkTableColumns;
        this.CpuItemsControl.ItemsSource= this.cpuRows;
        this.RunningItemsControl.ItemsSource= this.runningItems;
        this.RankingItemsControl.ItemsSource= this.rankingItems;
        this.HeatmapItemsControl.ItemsSource= this.heatmapRows;
        this.DetailItemsControl.ItemsSource= this.detailItems;
        this.UpdateSelectionSummary();
        this.UpdateProgress();
        this.UpdateCommandState();
        await this.LoadSelectedProjectsAsync(CancellationToken.None);
    }

    private void InitializeBenchmarkTableColumns(){
        if(this.benchmarkTableColumns.Count>0){
            return;
        }

        foreach(var pair in BenchmarkComparisonFormatter.PairDefinitions){
            this.benchmarkTableColumns.Add(new BenchmarkTableColumn(
                pair.Key+"Ratio",
                BenchmarkTableColumnKind.Ratio,
                pair.DisplayName,
                pair.TooltipText,
                leftProjectName:pair.LeftProject,
                rightProjectName:pair.RightProject));
            this.benchmarkTableColumns.Add(new BenchmarkTableColumn(
                pair.Key+"Left",
                BenchmarkTableColumnKind.Project,
                pair.LeftProject,
                pair.LeftProject,
                projectName:pair.LeftProject));
            this.benchmarkTableColumns.Add(new BenchmarkTableColumn(
                pair.Key+"Right",
                BenchmarkTableColumnKind.Project,
                pair.RightProject,
                pair.RightProject,
                projectName:pair.RightProject));
        }
    }

    private void InitializeProjectGrid(){
        if(this.ProjectGrid.Children.Count>0){
            return;
        }

        for(var i=0;i< this.benchmarkProjects.Count;i++){
            var project = this.benchmarkProjects[i];
            var checkBox = new CheckBox{
                Content=new TextBlock{
                    Text=project.DisplayName,
                    TextTrimming=TextTrimming.CharacterEllipsis,
                    VerticalAlignment=VerticalAlignment.Center,
                },
                IsChecked=project.IsSelected,
                FontSize=12,
                HorizontalAlignment=HorizontalAlignment.Left,
                Padding=new Thickness(0),
                MinHeight=20,
                MinWidth=120,
                DataContext=project,
            };
            checkBox.Click+= this.OnProjectCheckBoxClick;
            Grid.SetColumn(checkBox,i/4);
            Grid.SetRow(checkBox,i%4);
            this.ProjectGrid.Children.Add(checkBox);
        }
    }

    private void InitializeCpuList(){
        if(this.cpuCores.Count>0){
            return;
        }

        foreach(var cpu in CpuTopologyProvider.Load()){
            this.cpuCores.Add(cpu);
        }

        this.RebuildCpuRows();
        this.InitializeCpuPresetButtons();
        var first = this.cpuCores.OrderBy(static item => item.LogicalProcessorIndex).FirstOrDefault();
        this.ApplyCpuSelection(cpu => cpu==first,updateWorkers:false);
    }

    private void RebuildCpuRows(){
        this.cpuRows.Clear();
        var ordered = this.cpuCores
            .OrderBy(static cpu => cpu.LogicalProcessorIndex)
            .ToArray();
        for(var i=0;i<ordered.Length;i+=4){
            this.cpuRows.Add(new CpuRowItem(
                i<ordered.Length ? ordered[i] : null,
                i+1<ordered.Length ? ordered[i+1] : null,
                i+2<ordered.Length ? ordered[i+2] : null,
                i+3<ordered.Length ? ordered[i+3] : null));
        }
    }

    private void InitializeCpuPresetButtons(){
        this.cpuPresets.Clear();
        this.cpuPresets.AddRange(CpuPresetCatalog.Build(this.cpuCores));

        this.CpuPresetGrid.Children.Clear();
        this.CpuPresetGrid.ColumnDefinitions.Clear();
        for(var i=0;i< this.cpuPresets.Count;i++){
            this.CpuPresetGrid.ColumnDefinitions.Add(new ColumnDefinition{
                Width = GridLength.Auto,
            });

            var preset = this.cpuPresets[i];
            var button = new Button{
                Content=preset.Label,
                Tag=preset,
                MinWidth=48,
            };
            button.Click+= this.OnCpuPresetClick;
            Grid.SetColumn(button,i);
            this.CpuPresetGrid.Children.Add(button);
        }
    }

    private void OnConfigurationSelectionChanged(object sender,SelectionChangedEventArgs e){
        if(this.ConfigurationComboBox is null|| this.isBusy){
            return;
        }

        this.RefreshBenchmarksFromCache();
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
    }

    private async void OnRefreshClick(object sender,RoutedEventArgs e){
        await this.LoadSelectedProjectsAsync(CancellationToken.None);
    }

    private void OnProjectCheckBoxClick(object sender,RoutedEventArgs e){
        if(sender is CheckBox checkBox&&checkBox.DataContext is BenchmarkProject project){
            project.IsSelected=checkBox.IsChecked==true;
        }

        this.UpdateBenchmarkProjectSelectionStates();
        this.RefreshBenchmarksFromCache();
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
    }

    private void OnCpuCheckBoxClick(object sender,RoutedEventArgs e){
        if(sender is not CheckBox checkBox){
            return;
        }

        var cpu = checkBox.Tag as CpuCoreInfo ?? checkBox.DataContext as CpuCoreInfo;
        if(cpu is null){
            return;
        }

        this.SetCpuSelected(cpu,checkBox.IsChecked==true);
    }

    private void OnCpuCellTapped(object sender,TappedRoutedEventArgs e){
        if(sender is not FrameworkElement element||element.Tag is not CpuCoreInfo cpu){
            return;
        }

        this.SetCpuSelected(cpu,!cpu.IsSelected);
    }

    private void SetCpuSelected(CpuCoreInfo cpu,bool shouldSelect){
        if(!shouldSelect&& this.cpuCores.All(item => item==cpu||!item.IsSelected)){
            cpu.IsSelected=true;
            this.SetStatus("CPU繧・縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            this.UpdateCpuSummary();
            return;
        }

        cpu.IsSelected=shouldSelect;
        if(this.isRunning&&shouldSelect){
            this.StartWorkerForCpu(cpu);
        }else if(this.isRunning){
            this.StopCpuAndRequeue(cpu.LogicalProcessorIndex);
        }

        this.UpdateCpuSummary();
        this.UpdateProgress();
    }

    private void OnCpuPresetClick(object sender,RoutedEventArgs e){
        if(sender is not Button button||button.Tag is not CpuPresetDefinition preset){
            return;
        }

        var selected = preset.LogicalProcessorIndexes.ToHashSet();
        this.ApplyCpuSelection(cpu => selected.Contains(cpu.LogicalProcessorIndex),updateWorkers:true);
    }

    private void ApplyCpuSelection(Func<CpuCoreInfo,bool> shouldSelect,bool updateWorkers){
        if(this.cpuCores.Count==0){
            return;
        }

        var selected = this.cpuCores.Where(shouldSelect).ToHashSet();

        foreach(var cpu in this.cpuCores){
            var wasSelected = cpu.IsSelected;
            var isSelected = selected.Contains(cpu);
            if(wasSelected==isSelected){
                continue;
            }

            cpu.IsSelected=isSelected;
            if(updateWorkers&& this.isRunning){
                if(isSelected){
                    this.StartWorkerForCpu(cpu);
                }else{
                    this.StopCpuAndRequeue(cpu.LogicalProcessorIndex);
                }
            }
        }

        this.UpdateCpuSummary();
        this.UpdateProgress();
    }

    private void OnSelectAllClick(object sender,RoutedEventArgs e){
        foreach(var benchmark in this.benchmarks){
            benchmark.IsSelected=true;
        }

        this.selectionAnchorIndex= this.benchmarks.Count>0?0:null;
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
    }

    private void OnClearSelectionClick(object sender,RoutedEventArgs e){
        foreach(var benchmark in this.benchmarks){
            benchmark.IsSelected=false;
        }

        this.selectionAnchorIndex=null;
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
    }

    private async void OnRunClick(object sender,RoutedEventArgs e){
        var projects = this.GetSelectedProjects();
        if(projects.Count==0){
            this.SetStatus("繝励Ο繧ｸ繧ｧ繧ｯ繝医ｒ1縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            return;
        }

        if(this.projectSelectionDirty|| this.benchmarks.Count==0){
            await this.LoadSelectedProjectsAsync(CancellationToken.None);
        }

        var selected = this.benchmarks.Where(static benchmark => benchmark.IsSelected).ToArray();
        if(selected.Length==0){
            this.SetStatus("繝吶Φ繝√・繝ｼ繧ｯ繧・縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            return;
        }

        if(this.cpuCores.All(static cpu => !cpu.IsSelected)){
            this.SetStatus("CPU繧・縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            return;
        }

        await this.RunBenchmarksAsync(projects,selected);
    }

    private void OnCancelClick(object sender,RoutedEventArgs e){
        this.runCancellation?.Cancel();
        this.KillAllRunningProcesses(requeue:false);
    }

    private void OnBenchmarkHeaderClick(object sender,RoutedEventArgs e){
        this.ApplyBenchmarkSort(BenchmarkSortColumn.Name,null);
    }

    private void OnTableColumnTimeHeaderClick(object sender,RoutedEventArgs e){
        if(sender is Button { Tag:BenchmarkTableColumn column }){
            this.ApplyBenchmarkSort(BenchmarkSortColumn.Time,column.Key);
        }
    }

    private void OnTableColumnAllocatedHeaderClick(object sender,RoutedEventArgs e){
        if(sender is Button { Tag:BenchmarkTableColumn column }){
            this.ApplyBenchmarkSort(BenchmarkSortColumn.Allocated,column.Key);
        }
    }

    private void OnSelectTop10Click(object sender,RoutedEventArgs e){
        var selected = this.benchmarks
            .Take(10)
            .ToHashSet();

        foreach(var benchmark in this.benchmarks){
            benchmark.IsSelected=selected.Contains(benchmark);
        }

        this.selectionAnchorIndex=this.benchmarks.Count>0?0:null;
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
        this.SetStatus($"迴ｾ蝨ｨ縺ｮ荳ｦ縺ｳ鬆・°繧我ｸ贋ｽ・{selected.Count} 莉ｶ繧帝∈謚槭＠縺ｾ縺励◆縲・);
    }

    private void OnSelectionHeaderClick(object sender,RoutedEventArgs e){
        var selectAll = this.benchmarks.Any(static item => !item.IsSelected);
        foreach(var benchmark in this.benchmarks){
            benchmark.IsSelected=selectAll;
        }

        this.selectionAnchorIndex=this.benchmarks.Count>0?0:null;
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
    }

    private void OnChartToggleClick(object sender,RoutedEventArgs e){
        this.isChartCollapsed=!this.isChartCollapsed;
        this.AnalysisPanel.Visibility=this.isChartCollapsed ? Visibility.Collapsed : Visibility.Visible;
        this.ChartToggleButton.Content=this.isChartCollapsed ? "分析を表示" : "分析を隠す";
    }

    private void OnAnalysisPairClick(object sender,RoutedEventArgs e){
        if(sender is Button { Tag:string key }){
            this.selectedAnalysisPairKey=key;
            this.selectedAnalysisTab=AnalysisTab.Ranking;
            this.UpdateAnalysis();
        }
    }

    private void OnAnalysisMetricClick(object sender,RoutedEventArgs e){
        if(sender is Button { Tag:string key }){
            this.selectedAnalysisMetric=string.Equals(key,"Allocated",StringComparison.Ordinal)
                ? AnalysisMetric.Allocated
                : AnalysisMetric.Time;
            this.UpdateAnalysis();
        }
    }

    private void OnCheckedOnlyClick(object sender,RoutedEventArgs e){
        this.UpdateAnalysis();
    }

    private void OnAnalysisTabClick(object sender,RoutedEventArgs e){
        if(sender is not Button { Tag:string key }){
            return;
        }

        this.selectedAnalysisTab=key switch{
            "Heatmap" => AnalysisTab.Heatmap,
            "Detail" => AnalysisTab.Detail,
            _ => AnalysisTab.Ranking,
        };
        this.UpdateAnalysis();
    }

    private void OnAnalysisRankingTapped(object sender,TappedRoutedEventArgs e){
        if(sender is FrameworkElement { Tag:AnalysisRankingItem item }){
            this.SelectAnalysisMethod(item.Benchmark);
        }
    }

    private void OnAnalysisAddBenchmarkClick(object sender,RoutedEventArgs e){
        if(sender is Button { Tag:AnalysisRankingItem item }){
            this.AddBenchmarkToRerun(item.Benchmark);
        }
    }

    private void OnAnalysisHeatmapCellClick(object sender,RoutedEventArgs e){
        if(sender is Button { Tag:AnalysisHeatmapCell cell }){
            this.selectedAnalysisPairKey=cell.PairKey;
            this.SelectAnalysisMethod(cell.Benchmark);
        }
    }

    private void OnLoadCsvClick(object sender,RoutedEventArgs e){
        var path = this.CsvPathTextBox.Text?.Trim().Trim('"');
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)){
            this.SetStatus("螳滓ｸｬ蛟､CSV繝輔ぃ繧､繝ｫ縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ縲・);
            return;
        }

        try{
            var rows = BenchmarkComparisonFormatter.ReadCsv(File.ReadAllText(path,Encoding.UTF8));
            this.LoadComparisonRowsIntoBenchmarkTable(rows);
            this.SetStatus($"螳滓ｸｬ蛟､CSV繧定ｪｭ縺ｿ霎ｼ縺ｿ縺ｾ縺励◆: {path}");
        }catch(Exception ex){
            this.SetStatus(ex.Message);
            this.AppendLog(ex.ToString());
        }
    }

    private void LoadComparisonRowsIntoBenchmarkTable(IReadOnlyList<BenchmarkComparisonRow> rows){
        var rowByMethod = rows.ToDictionary(static row => row.Benchmark,StringComparer.OrdinalIgnoreCase);
        var missingMethods = rowByMethod.Keys
            .Where(method => this.benchmarks.All(item => !string.Equals(item.MethodName,method,StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if(missingMethods.Length>0){
            throw new InvalidDataException("CSV蜀・・繝｡繧ｽ繝・ラ縺檎樟蝨ｨ縺ｮ繝吶Φ繝√・繝ｼ繧ｯ荳隕ｧ縺ｫ蟄伜惠縺励∪縺帙ｓ: "+string.Join(", ",missingMethods));
        }

        foreach(var benchmark in this.benchmarks){
            benchmark.IsSelected=false;
            foreach(var status in benchmark.ProjectStatuses){
                status.Status=BenchmarkProjectRunStatus.NotSelected;
                status.ClearResult();
            }

            if(!rowByMethod.TryGetValue(benchmark.MethodName,out var row)){
                benchmark.RefreshDisplayCells();
                continue;
            }

            this.ApplyMetricsToStatus(benchmark,BenchmarkComparisonFormatter.SystemFLinq,row.SystemFLinq);
            this.ApplyMetricsToStatus(benchmark,BenchmarkComparisonFormatter.SystemZLinq,row.SystemZLinq);
            this.ApplyMetricsToStatus(benchmark,BenchmarkComparisonFormatter.PersistedFLinq,row.PersistedFLinq);
            this.ApplyMetricsToStatus(benchmark,BenchmarkComparisonFormatter.PersistedLinq,row.PersistedLinq);
            this.ApplyMetricsToStatus(benchmark,BenchmarkComparisonFormatter.TsavoriteFLinq,row.TsavoriteFLinq);
            this.ApplyMetricsToStatus(benchmark,BenchmarkComparisonFormatter.TsavoriteLinq,row.TsavoriteLinq);
            benchmark.RefreshDisplayCells();
        }

        this.comparisonRows=rows;
        this.SortBenchmarkItems();
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
    }

    private void ApplyMetricsToStatus(BenchmarkItem benchmark,string projectName,ProjectMetrics metrics){
        var status = benchmark.ProjectStatuses.FirstOrDefault(cell => string.Equals(cell.DisplayName,projectName,StringComparison.OrdinalIgnoreCase));
        if(status is null||!metrics.HasValue){
            return;
        }

        status.IsProjectSelected=true;
        status.Status=BenchmarkProjectRunStatus.Completed;
        status.SetResult(
            FormatShortDuration(metrics.TimeNanoseconds),
            FormatNanoseconds(metrics.TimeNanoseconds),
            metrics.TimeNanoseconds,
            FormatShortBytes(metrics.AllocatedBytes),
            metrics.AllocatedBytes is { } bytes ? bytes.ToString("N0",CultureInfo.InvariantCulture)+" B" : string.Empty,
            metrics.AllocatedBytes);
    }

    private void AddBenchmarkToRerun(string benchmarkName){
        var benchmark = this.benchmarks.FirstOrDefault(item => string.Equals(item.MethodName,benchmarkName,StringComparison.OrdinalIgnoreCase));
        if(benchmark is null){
            return;
        }

        benchmark.IsSelected=true;
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
        this.UpdateAnalysis();
        this.SetStatus($"{benchmarkName} 繧貞・險域ｸｬ蟇ｾ雎｡縺ｫ霑ｽ蜉縺励∪縺励◆縲・);
    }

    private void SelectAnalysisMethod(string benchmarkName){
        this.selectedAnalysisMethod=benchmarkName;
        this.selectedAnalysisTab=AnalysisTab.Detail;
        this.UpdateAnalysis();
    }

    private void OnRerunNextScaleClick(object sender,RoutedEventArgs e){
        if(!this.benchmarks.Any(static benchmark => benchmark.IsSelected)){
            this.SetStatus("蜀崎ｨ域ｸｬ縺吶ｋ繝吶Φ繝√・繝ｼ繧ｯ繧・縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            return;
        }

        this.SelectNextElementScale();
        this.OnRunClick(sender,e);
    }

    private void ApplyBenchmarkSort(BenchmarkSortColumn column,string? columnKey){
        if(this.sortColumn==column&&string.Equals(this.sortColumnKey,columnKey,StringComparison.Ordinal)){
            this.sortDescending=!this.sortDescending;
        }else{
            this.sortColumn=column;
            this.sortColumnKey=columnKey;
            this.sortDescending=column!=BenchmarkSortColumn.Name;
        }

        this.UpdateBenchmarkSortHeaders();
        this.SortBenchmarkItems();
        this.UpdateAnalysis();
    }

    private void OpenReport(string? path){
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)){
            this.SetStatus("繝ｬ繝昴・繝医ヵ繧｡繧､繝ｫ縺瑚ｦ九▽縺九ｊ縺ｾ縺帙ｓ縲・);
            return;
        }

        Process.Start(new ProcessStartInfo{
            FileName = path,
            UseShellExecute = true,
        });
    }

    private void OnBenchmarkRowTapped(object sender,TappedRoutedEventArgs e){
        if(sender is not FrameworkElement element||element.Tag is not BenchmarkItem benchmark){
            return;
        }

        var index = this.benchmarks.IndexOf(benchmark);
        if(index<0){
            return;
        }

        var isSelected = !benchmark.IsSelected;
        if(this.isShiftDown&& this.selectionAnchorIndex is { } anchorIndex){
            var start = Math.Min(anchorIndex,index);
            var end = Math.Max(anchorIndex,index);
            for(var i=start;i<=end;i++){
                this.benchmarks[i].IsSelected=isSelected;
            }
        }else{
            benchmark.IsSelected=isSelected;
            this.selectionAnchorIndex=index;
        }

        if(this.isControlDown){
            this.selectionAnchorIndex=index;
        }

        this.UpdateSelectionSummary();
        this.UpdateCommandState();
    }

    private void OnPageKeyDown(object sender,KeyRoutedEventArgs e){
        if(e.Key==Windows.System.VirtualKey.Shift){
            this.isShiftDown=true;
        }else if(e.Key==Windows.System.VirtualKey.Control){
            this.isControlDown=true;
        }
    }

    private void OnPageKeyUp(object sender,KeyRoutedEventArgs e){
        if(e.Key==Windows.System.VirtualKey.Shift){
            this.isShiftDown=false;
        }else if(e.Key==Windows.System.VirtualKey.Control){
            this.isControlDown=false;
        }
    }

    private Task LoadSelectedProjectsAsync(CancellationToken cancellationToken){
        var projects = this.GetSelectedProjects();
        if(projects.Count==0){
            this.SetStatus("繝励Ο繧ｸ繧ｧ繧ｯ繝医ｒ1縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            return Task.CompletedTask;
        }

        this.SetBusy(true,allowCpuChanges:false);
        var selectedMethodNames = this.GetSelectedMethodNames();
        this.benchmarks.Clear();
        this.selectionAnchorIndex=null;
        this.SetReportPaths(null);
        this.LogTextBox.Text=string.Empty;
        this.SetStatus("蝓ｺ貅悶Γ繧ｽ繝・ラ荳隕ｧ繧定ｪｭ縺ｿ霎ｼ繧薙〒縺・∪縺・..");

        try{
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = this.GetConfiguration();
            var canonicalCacheKey = GetMethodCacheKey(configuration, this.canonicalMethodProject);
            if(!this.methodCache.ContainsKey(canonicalCacheKey)){
                this.SetStatus($"{this.canonicalMethodProject.DisplayName} 縺九ｉ繝｡繧ｽ繝・ラ繧定ｪｭ縺ｿ霎ｼ繧薙〒縺・∪縺・..");
                this.methodCache[canonicalCacheKey]=LoadBenchmarkMethods(this.canonicalMethodProject.BenchmarkType);
            }

            this.RefreshBenchmarksFromCache(selectedMethodNames);
            this.SetStatus($"{projects.Count} 蛟九・驕ｸ謚槭・繝ｭ繧ｸ繧ｧ繧ｯ繝医〒蜈ｱ騾壹☆繧九Γ繧ｽ繝・ラ繧・{this.benchmarks.Count} 莉ｶ隱ｭ縺ｿ霎ｼ縺ｿ縺ｾ縺励◆縲・);
        }catch(OperationCanceledException){
            this.SetStatus("Load canceled.");
            this.AppendLog("Canceled.");
        }catch(Exception ex){
            this.SetStatus(ex.Message);
            this.AppendLog(ex.ToString());
        }finally{
            this.SetBusy(false,allowCpuChanges:false);
            this.UpdateSelectionSummary();
            this.UpdateCommandState();
        }

        return Task.CompletedTask;
    }

    private async Task RunBenchmarksAsync(IReadOnlyList<BenchmarkProject> projects,IReadOnlyList<BenchmarkItem> selectedMethods){
        this.runCancellation=new CancellationTokenSource();
        this.currentRunRoot= this.CreateRunRoot(this.GetConfiguration());
        this.currentRunStartedAt=DateTimeOffset.Now;
        this.currentRunFinishedAt=null;
            this.SetReportPaths(null);
        this.UpdateRunTimeDisplay();
        this.AppendLog($"Benchmark started: {FormatRunTimestamp(this.currentRunStartedAt.Value)}");
        this.SetBusy(true,allowCpuChanges:true);
        this.SetStatus($"{projects.Count} 繝励Ο繧ｸ繧ｧ繧ｯ繝・x {selectedMethods.Count} 繝｡繧ｽ繝・ラ = {projects.Count*selectedMethods.Count} 繧ｿ繧ｹ繧ｯ繧呈ｺ門ｙ荳ｭ縺ｧ縺吶・lement Scale: {this.GetSelectedElementScale()}");
        this.runningItems.Clear();

        lock(this.runGate){
            this.pendingRunItems.Clear();
            this.allRunItems.Clear();
            this.runningProcesses.Clear();
            this.runningRunItems.Clear();
            this.requeueOnProcessExit.Clear();
            this.cpuWorkers.Clear();
        }

        try{
            this.PrepareRunQueue(projects,selectedMethods);
            this.UpdateProgress();
            this.StartWorkersForSelectedCpus();
            await this.WaitForRunCompletionAsync(this.runCancellation.Token);
            var reportPaths = this.CreateRunReport(this.currentRunRoot);
            this.SetReportPaths(reportPaths.CsvPath);
            var failed = this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Failed);
            this.RecordRunFinished(failed==0?"Completed":$"Completed with {failed} failed task(s)",reportPaths);
            this.SetStatus(failed==0
                ? "繝吶Φ繝√・繝ｼ繧ｯ縺悟ｮ御ｺ・＠縺ｾ縺励◆縲・
                : $"繝吶Φ繝√・繝ｼ繧ｯ縺悟ｮ御ｺ・＠縺ｾ縺励◆縲ょ､ｱ謨・ {failed} 莉ｶ");
        }catch(OperationCanceledException){
            var reportPaths = this.CreateRunReport(this.currentRunRoot);
            this.SetReportPaths(reportPaths.CsvPath);
            this.RecordRunFinished("Canceled",reportPaths);
            this.SetStatus("繝吶Φ繝√・繝ｼ繧ｯ繧剃ｸｭ豁｢縺励∪縺励◆縲る比ｸｭ縺ｾ縺ｧ縺ｮCSV繧呈嶌縺榊・縺励∪縺励◆縲・);
            this.AppendLog("Canceled.");
        }catch(Exception ex){
            var reportPaths = this.CreateRunReport(this.currentRunRoot);
            this.SetReportPaths(reportPaths.CsvPath);
            this.RecordRunFinished("Failed",reportPaths);
            this.SetStatus(ex.Message);
            this.AppendLog(ex.ToString());
        }finally{
            this.KillAllRunningProcesses(requeue:false);
            this.runCancellation?.Dispose();
            this.runCancellation=null;
            this.isRunning=false;
            this.SetBusy(false,allowCpuChanges:false);
            this.UpdateCommandState();
            this.UpdateProgress();
        }
    }

    private void RecordRunFinished(string status,RunReportPaths reportPaths){
        this.currentRunFinishedAt=DateTimeOffset.Now;
        this.UpdateRunTimeDisplay();
        this.AppendLog($"Benchmark finished: {FormatRunTimestamp(this.currentRunFinishedAt.Value)}");
        this.AppendLog($"Elapsed: {FormatRunElapsed(this.currentRunStartedAt,this.currentRunFinishedAt)}");
        this.WriteRunSummary(status,reportPaths);
    }

    private void PrepareRunQueue(IReadOnlyList<BenchmarkProject> projects,IReadOnlyList<BenchmarkItem> selectedMethods){
        if(this.currentRunRoot is null){
            throw new InvalidOperationException("Run root was not created.");
        }

        foreach(var benchmark in this.benchmarks){
            benchmark.ResetProjectStatuses(this.benchmarkProjects);
        }

        lock(this.runGate){
            this.pendingRunItems.Clear();
            this.allRunItems.Clear();
            foreach(var method in selectedMethods){
                foreach(var project in projects){
                    if(!method.ProjectMethods.TryGetValue(project.ProjectKey,out var projectMethod)){
                        throw new InvalidOperationException($"Benchmark method '{method.MethodName}' was not found in {project.DisplayName}.");
                    }

                    var item = new BenchmarkRunItem(project,method,projectMethod);
                    this.pendingRunItems.Enqueue(item);
                    this.allRunItems.Add(item);
                }
            }
        }
    }

    private void RefreshBenchmarksFromCache(IReadOnlySet<string>? selectedMethodNames = null){
        var projects = this.GetSelectedProjects();
        selectedMethodNames ??= this.GetSelectedMethodNames();
        this.benchmarks.Clear();
        this.selectionAnchorIndex=null;
        if(projects.Count==0){
            this.projectSelectionDirty=true;
            this.SetStatus("繝励Ο繧ｸ繧ｧ繧ｯ繝医ｒ1縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            return;
        }

        if(!this.methodCache.TryGetValue(GetMethodCacheKey(this.GetConfiguration(), this.canonicalMethodProject),out var canonicalMethods)){
            this.projectSelectionDirty=true;
            this.SetStatus("繝｡繧ｽ繝・ラ荳隕ｧ繧定ｪｭ縺ｿ霎ｼ繧薙〒縺・∪縺・..");
            return;
        }

        foreach(var item in CreateBenchmarkItemsFromCanonicalMethods(this.benchmarkProjects,canonicalMethods)){
            item.IsSelected=selectedMethodNames.Contains(item.MethodName);
            this.benchmarks.Add(item);
        }

        this.UpdateBenchmarkSortHeaders();
        this.SortBenchmarkItems();
        this.projectSelectionDirty=false;
        this.SetStatus($"{projects.Count} 蛟九・驕ｸ謚槭・繝ｭ繧ｸ繧ｧ繧ｯ繝医〒蜈ｱ騾壹☆繧九Γ繧ｽ繝・ラ繧・{this.benchmarks.Count} 莉ｶ隱ｭ縺ｿ霎ｼ縺ｿ縺ｾ縺励◆縲・);
    }

    private void SortBenchmarkItems(){
        if(this.benchmarks.Count<=1){
            return;
        }

        var ordered = this.benchmarks
            .OrderBy(item => GetBenchmarkSortMissingRank(item,this.sortColumn,this.sortColumnKey))
            .ThenBy(item => GetBenchmarkSortValue(item,this.sortColumn,this.sortColumnKey),new BenchmarkSortValueComparer(this.sortDescending))
            .ThenBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        this.benchmarks.Clear();
        foreach(var item in ordered){
            this.benchmarks.Add(item);
        }
    }

    private void UpdateBenchmarkSortHeaders(){
        if(this.BenchmarkHeaderButton is not null){
            this.BenchmarkHeaderButton.Content=this.sortColumn==BenchmarkSortColumn.Name
                ? "繝吶Φ繝√・繝ｼ繧ｯ "+GetSortGlyphText(this.sortDescending)
                : "繝吶Φ繝√・繝ｼ繧ｯ";
        }

        foreach(var column in this.benchmarkTableColumns){
            var isTime = this.sortColumn==BenchmarkSortColumn.Time&&string.Equals(this.sortColumnKey,column.Key,StringComparison.Ordinal);
            var isAllocated = this.sortColumn==BenchmarkSortColumn.Allocated&&string.Equals(this.sortColumnKey,column.Key,StringComparison.Ordinal);
            column.TimeHeaderText=isTime
                ? "譎る俣 "+GetSortGlyphText(this.sortDescending)
                : "譎る俣";
            column.AllocatedHeaderText=isAllocated
                ? "蜑ｲ蠖・"+GetSortGlyphText(this.sortDescending)
                : "蜑ｲ蠖・;
        }
    }

    private static int GetBenchmarkSortMissingRank(BenchmarkItem item,BenchmarkSortColumn column,string? columnKey){
        return GetBenchmarkSortValue(item,column,columnKey) is null
            ? 1
            : 0;
    }

    private static object? GetBenchmarkSortValue(BenchmarkItem item,BenchmarkSortColumn column,string? columnKey){
        if(column==BenchmarkSortColumn.Name){
            return item.MethodName;
        }

        if(string.IsNullOrWhiteSpace(columnKey)){
            return null;
        }

        var cell = item.DisplayCells.FirstOrDefault(cell => string.Equals(cell.ColumnKey,columnKey,StringComparison.Ordinal));
        return column switch{
            BenchmarkSortColumn.Time => cell?.TimeSortValue,
            BenchmarkSortColumn.Allocated => cell?.AllocatedSortValue,
            _ => item.MethodName,
        };
    }

    private static string GetSortGlyph(bool descending){
        return descending ? "笆ｼ" : "笆ｲ";
    }

    private static string GetSortGlyphText(bool descending){
        return descending ? "v" : "^";
    }

    private void SelectNextElementScale(){
        if(this.ElementScaleComboBox.SelectedIndex+1<this.ElementScaleComboBox.Items.Count){
            this.ElementScaleComboBox.SelectedIndex++;
        }

        this.SetStatus($"Element Scale 繧・{this.GetSelectedElementScale()} 縺ｫ縺励∪縺励◆縲・);
    }

    private HashSet<string> GetSelectedMethodNames(){
        return this.benchmarks
            .Where(static benchmark => benchmark.IsSelected)
            .Select(static benchmark => benchmark.MethodName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetMethodCacheKey(string configuration,BenchmarkProject project){
        return GetMethodCacheKey(configuration,project.ProjectKey);
    }

    private static string GetMethodCacheKey(string configuration,string projectKey){
        return projectKey;
    }

    private static IReadOnlyList<BenchmarkItem> CreateBenchmarkItemsFromCanonicalMethods(
        IReadOnlyList<BenchmarkProject> projectColumns,
        IReadOnlyList<BenchmarkProjectMethod> canonicalMethods){
        if(projectColumns.Count==0){
            return [];
        }

        var displayColumns = CreateBenchmarkTableColumns();
        return canonicalMethods
            .GroupBy(static method => method.MethodName,StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static method => method.MethodName,StringComparer.OrdinalIgnoreCase)
            .Select(method => {
                var methodsByProject = projectColumns.ToDictionary(
                    static project => project.ProjectKey,
                    project => new BenchmarkProjectMethod(
                        project.BenchmarkType.Name,
                        project.TypeFullName,
                        method.MethodName,
                        method.MethodName,
                        method.Categories),
                    StringComparer.Ordinal);
                return new BenchmarkItem(method.MethodName,method.MethodName,method.Categories,methodsByProject,displayColumns,projectColumns);
            })
            .ToArray();
    }

    private static IReadOnlyList<BenchmarkTableColumn> CreateBenchmarkTableColumns(){
        var columns = new List<BenchmarkTableColumn>();
        foreach(var pair in BenchmarkComparisonFormatter.PairDefinitions){
            columns.Add(new BenchmarkTableColumn(pair.Key+"Ratio",BenchmarkTableColumnKind.Ratio,pair.DisplayName,pair.TooltipText,leftProjectName:pair.LeftProject,rightProjectName:pair.RightProject));
            columns.Add(new BenchmarkTableColumn(pair.Key+"Left",BenchmarkTableColumnKind.Project,pair.LeftProject,pair.LeftProject,projectName:pair.LeftProject));
            columns.Add(new BenchmarkTableColumn(pair.Key+"Right",BenchmarkTableColumnKind.Project,pair.RightProject,pair.RightProject,projectName:pair.RightProject));
        }

        return columns;
    }

    private void StartWorkersForSelectedCpus(){
        this.isRunning=true;
        foreach(var cpu in this.cpuCores.Where(static cpu => cpu.IsSelected)){
            this.StartWorkerForCpu(cpu);
        }

        this.UpdateCpuSummary();
    }

    private void UpdateBenchmarkProjectSelectionStates(){
        foreach(var benchmark in this.benchmarks){
            foreach(var project in this.benchmarkProjects){
                benchmark.SetProjectSelected(project.ProjectKey,project.IsSelected);
            }
        }
    }

    private void SetRunItemStatus(BenchmarkRunItem item,BenchmarkRunStatus status){
        item.Status=status;
        item.Method.SetProjectStatus(item.Project.ProjectKey,status);
        if(status is BenchmarkRunStatus.Pending or BenchmarkRunStatus.Running or BenchmarkRunStatus.Failed){
            this.SetProjectCellResult(item.Method,item.Project.ProjectKey,null,null,null);
        }
    }

    private void SetProjectCellResult(BenchmarkItem method,string projectKey,double? meanNanoseconds,string? meanText,long? allocatedBytes){
        var cell = method.ProjectStatuses.FirstOrDefault(cell => string.Equals(cell.ProjectKey,projectKey,StringComparison.Ordinal));
        if(cell is null){
            return;
        }

        if(meanNanoseconds is null&&allocatedBytes is null){
            cell.ClearResult();
            method.RefreshDisplayCells();
            return;
        }

        cell.SetResult(
            FormatShortDuration(meanNanoseconds),
            FirstText(meanText,FormatNanoseconds(meanNanoseconds)),
            meanNanoseconds,
            FormatShortBytes(allocatedBytes),
            allocatedBytes is { } bytes ? bytes.ToString("N0",CultureInfo.InvariantCulture)+" B" : string.Empty,
            allocatedBytes);
        method.RefreshDisplayCells();
    }

    private void ApplyCanceledResult(BenchmarkRunItem item){
        this.SetProjectCellResult(item.Method,item.Project.ProjectKey,null,null,null);
        this.SortBenchmarkItems();
    }

    private void StartWorkerForCpu(CpuCoreInfo cpu){
        if(this.runCancellation is null){
            return;
        }

        lock(this.runGate){
            if(this.cpuWorkers.TryGetValue(cpu.LogicalProcessorIndex,out var existing)&&!existing.IsCompleted){
                return;
            }

            this.cpuWorkers[cpu.LogicalProcessorIndex]=Task.Run(() => this.CpuWorkerLoopAsync(cpu, this.runCancellation.Token));
        }
    }

    private async Task CpuWorkerLoopAsync(CpuCoreInfo cpu,CancellationToken cancellationToken){
        Process? worker = null;
        try{
            worker= this.StartBenchmarkWorker(cpu);
            lock(this.runGate){
                this.runningProcesses[cpu.LogicalProcessorIndex]=worker;
            }

            while(!cancellationToken.IsCancellationRequested&&cpu.IsSelected){
                BenchmarkRunItem? item;
                lock(this.runGate){
                    item= this.pendingRunItems.Count>0 ? this.pendingRunItems.Dequeue() : null;
                }

                if(item is null){
                    return;
                }

                try{
                    await this.RunSingleBenchmarkAsync(worker,item,cpu,cancellationToken);
                }catch(OperationCanceledException){
                    throw;
                }catch(Exception ex){
                    this.AppendLog(ex.ToString());
                    await this.RunOnUiAsync(() => {
                        this.runningItems.Remove(item);
                        item.ProcessId=0;
                        item.FinishedAt=DateTimeOffset.Now;
            SetRunItemStatus(item,BenchmarkRunStatus.Failed);
                    });
                    this.UpdateProgress();
                    if(worker is null||worker.HasExited){
                        return;
                    }
                }
            }

            await StopWorkerGracefullyAsync(worker);
        }finally{
            lock(this.runGate){
                this.cpuWorkers.Remove(cpu.LogicalProcessorIndex);
                this.runningProcesses.Remove(cpu.LogicalProcessorIndex);
                this.runningRunItems.Remove(cpu.LogicalProcessorIndex);
                this.requeueOnProcessExit.Remove(cpu.LogicalProcessorIndex);
            }
            KillProcess(worker);
        }
    }

    private async Task RunSingleBenchmarkAsync(Process worker,BenchmarkRunItem item,CpuCoreInfo cpu,CancellationToken cancellationToken){
        if(this.currentRunRoot is null){
            throw new InvalidOperationException("Run root was not created.");
        }

        var attempt = item.Attempt+1;
        var artifactFolder = Path.Combine(this.currentRunRoot,
            "artifacts",
            BenchmarkText.SanitizePathPart(item.Project.AssemblyName),
            BenchmarkText.SanitizePathPart(item.MethodName),
            $"cpu{cpu.LogicalProcessorIndex}-attempt{attempt}");
        Directory.CreateDirectory(artifactFolder);

        await this.RunOnUiAsync(() => {
            item.Attempt=attempt;
            item.ArtifactFolder=artifactFolder;
            item.AssignedCpu=cpu;
            item.ProcessId=worker.Id;
            item.StartedAt=DateTimeOffset.Now;
            item.FinishedAt=null;
            SetRunItemStatus(item,BenchmarkRunStatus.Running);
            this.runningItems.Add(item);
        });
        this.UpdateProgress();

        var taskId = Guid.NewGuid().ToString("N");
        var selectedCpuCount = Math.Max(1,this.cpuCores.Count(static cpu => cpu.IsSelected));
        var message = new WorkerTaskMessage(
            "run",
            taskId,
            item.Project.AssemblyName,
            item.Project.TypeFullName,
            item.MethodName, this.GetSelectedElementScale(),
            artifactFolder);
        WorkerResultMessage? result = null;
        Exception? workerException = null;
        var peakRssBytes = 0L;
        var requeue = false;
        try{
            lock(this.runGate){
                this.runningRunItems[cpu.LogicalProcessorIndex]=item;
            }
            var resultTask = this.SendWorkerTaskAsync(worker,message,cancellationToken);
            peakRssBytes=await MeasurePeakWorkingSetUntilCompletedAsync(worker,resultTask,cancellationToken);
            result=await resultTask;
        }catch(OperationCanceledException){
            await this.RunOnUiAsync(() => {
                this.runningItems.Remove(item);
                item.ProcessId=0;
                item.FinishedAt=DateTimeOffset.Now;
                SetRunItemStatus(item,BenchmarkRunStatus.Canceled);
                this.ApplyCanceledResult(item);
            });
            throw;
        }catch(Exception ex){
            workerException=ex;
        }finally{
            lock(this.runGate){
                this.runningRunItems.Remove(cpu.LogicalProcessorIndex);
                requeue= this.requeueOnProcessExit.Remove(cpu.LogicalProcessorIndex);
            }
        }

        await this.RunOnUiAsync(() => this.runningItems.Remove(item));

        if(requeue&&!cancellationToken.IsCancellationRequested){
            await this.RunOnUiAsync(() => {
                SetRunItemStatus(item,BenchmarkRunStatus.Pending);
                item.ProcessId=0;
                item.AssignedCpu=null;
                item.FinishedAt=null;
            });
            lock(this.runGate){
                this.pendingRunItems.Enqueue(item);
            }

            this.UpdateProgress();
            return;
        }

        if(workerException is not null){
            throw workerException;
        }

        if(result is null){
            throw new InvalidOperationException("Benchmark worker did not return a result.");
        }

        var resultValues = ReadResultValues(result.ArtifactFolder,item.MethodName);
        var meanNanoseconds = result.MeanNanoseconds??ParseNanoseconds(result.Mean)??ParseNanoseconds(resultValues.Mean);
        var errorNanoseconds = result.ErrorNanoseconds??ParseNanoseconds(result.Error)??ParseNanoseconds(resultValues.Error);
        var stdDevNanoseconds = result.StdDevNanoseconds??ParseNanoseconds(result.StdDev)??ParseNanoseconds(resultValues.StdDev);
        var allocatedBytesValue = ParseMemoryBytes(resultValues.AllocatedBytes)??result.AllocatedBytes;
        var peakRssBytesValue = peakRssBytes>0 ? peakRssBytes : (long?)null;
        var estimatedPeakRssBytesValue = peakRssBytesValue is { } peak
            ? peak*selectedCpuCount
            : (long?)null;
        item.ExitCode=result.ExitCode;
        await this.RunOnUiAsync(() => {
            item.ExitCode=result.ExitCode;
            item.Mean=FirstText(result.Mean,resultValues.Mean,FormatNanoseconds(meanNanoseconds));
            item.Error=FirstText(result.Error,resultValues.Error,FormatNanoseconds(errorNanoseconds));
            item.StdDev=FirstText(result.StdDev,resultValues.StdDev,FormatNanoseconds(stdDevNanoseconds));
            item.AllocatedBytes=FormatLong(allocatedBytesValue);
            item.PeakRssBytes=FormatLong(peakRssBytesValue);
            item.EstimatedPeakRssBytes=FormatLong(estimatedPeakRssBytesValue);
            item.MeanNanoseconds=meanNanoseconds;
            item.ErrorNanoseconds=errorNanoseconds;
            item.StdDevNanoseconds=stdDevNanoseconds;
            item.AllocatedBytesValue=allocatedBytesValue;
            item.PeakRssBytesValue=peakRssBytesValue;
            item.EstimatedPeakRssBytesValue=estimatedPeakRssBytesValue;
            item.FinishedAt=DateTimeOffset.Now;
            item.ProcessId=0;
            SetRunItemStatus(item,result.ExitCode==0 ? BenchmarkRunStatus.Completed : BenchmarkRunStatus.Failed);
            if(result.ExitCode==0){
                this.SetProjectCellResult(item.Method,item.Project.ProjectKey,meanNanoseconds,item.Mean,allocatedBytesValue);
                this.SortBenchmarkItems();
                this.RefreshComparisonRowsFromBenchmarks();
            }
        });
        if(!string.IsNullOrWhiteSpace(result.Log)){
            this.AppendLog(result.Log);
        }

        this.UpdateProgress();
    }

    private static async Task<long> MeasurePeakWorkingSetUntilCompletedAsync(Process process,Task observedTask,CancellationToken cancellationToken){
        var peak = ReadWorkingSet(process);
        while(!observedTask.IsCompleted){
            cancellationToken.ThrowIfCancellationRequested();
            peak=Math.Max(peak,ReadWorkingSet(process));
            await Task.Delay(100,cancellationToken);
        }

        return Math.Max(peak,ReadWorkingSet(process));
    }

    private static long ReadWorkingSet(Process process){
        try{
            if(process.HasExited){
                return 0;
            }

            process.Refresh();
            return process.WorkingSet64;
        }catch(InvalidOperationException){
            return 0;
        }catch(System.ComponentModel.Win32Exception){
            return 0;
        }
    }

    private Process StartBenchmarkWorker(CpuCoreInfo cpu){
        if(this.currentRunRoot is null){
            throw new InvalidOperationException("Run root was not created.");
        }

        var (workerDirectory,workerExe,workerDll) = this.ResolveBenchmarkWorkerHostPaths();
        var startInfo = new ProcessStartInfo{
            WorkingDirectory = Directory.Exists(workerDirectory)?workerDirectory: this.repositoryRoot,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            CreateNoWindow = true,
        };

        if(File.Exists(workerExe)){
            startInfo.FileName=workerExe;
        }else if(File.Exists(workerDll)){
            startInfo.FileName="dotnet";
            startInfo.ArgumentList.Add(workerDll);
        }else{
            throw new FileNotFoundException($"Benchmark worker host was not found. Searched: {workerExe}, {workerDll}",workerDll);
        }

        startInfo.ArgumentList.Add("--logical-cpu");
        startInfo.ArgumentList.Add(cpu.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--run-root");
        startInfo.ArgumentList.Add(this.currentRunRoot);

        var process = new Process{
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        process.ErrorDataReceived+=(sender,e) => {
            if(!string.IsNullOrWhiteSpace(e.Data)){
                this.AppendLog(e.Data);
            }
        };
        process.Start();
        process.BeginErrorReadLine();
        WaitForWorkerReady(process);
        this.AppendLog($"> worker CPU {cpu.LogicalProcessorIndex}: {process.Id}");
        return process;
    }

    private (string Directory,string ExePath,string DllPath) ResolveBenchmarkWorkerHostPaths(){
        foreach(var workerDirectory in this.EnumerateBenchmarkWorkerDirectories()){
            var workerExe = Path.Combine(workerDirectory,OperatingSystem.IsWindows()?"Benchmark.Worker.exe":"Benchmark.Worker");
            var workerDll = Path.Combine(workerDirectory,"Benchmark.Worker.dll");
            if(File.Exists(workerExe)||File.Exists(workerDll)){
                return (workerDirectory,workerExe,workerDll);
            }
        }

        var defaultDirectory = AppContext.BaseDirectory;
        return (
            defaultDirectory,
            Path.Combine(defaultDirectory,OperatingSystem.IsWindows()?"Benchmark.Worker.exe":"Benchmark.Worker"),
            Path.Combine(defaultDirectory,"Benchmark.Worker.dll"));
    }

    private IEnumerable<string> EnumerateBenchmarkWorkerDirectories(){
        yield return AppContext.BaseDirectory;

        var configuration = this.GetConfiguration();
        yield return Path.Combine(this.repositoryRoot,"Benchmark.Worker","bin",configuration,TargetFramework);

        if(!string.Equals(configuration,"Debug",StringComparison.OrdinalIgnoreCase)){
            yield return Path.Combine(this.repositoryRoot,"Benchmark.Worker","bin","Debug",TargetFramework);
        }

        if(!string.Equals(configuration,"Release",StringComparison.OrdinalIgnoreCase)){
            yield return Path.Combine(this.repositoryRoot,"Benchmark.Worker","bin","Release",TargetFramework);
        }
    }

    private static void WaitForWorkerReady(Process worker){
        try{
            var line = worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            if(!string.Equals(line,"ready",StringComparison.Ordinal)){
                throw new InvalidOperationException($"Benchmark worker did not complete startup handshake. Received: {line??"<null>"}");
            }
        }catch{
            KillProcess(worker);
            throw;
        }
    }

    private static async Task StopWorkerGracefullyAsync(Process worker){
        try{
            if(!worker.HasExited){
                var message = JsonSerializer.Serialize(new WorkerTaskMessage("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty));
                await worker.StandardInput.WriteLineAsync(message);
                await worker.StandardInput.FlushAsync();
                await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            }
        }catch(InvalidOperationException){
        }catch(IOException){
        }catch(TimeoutException){
        }
    }

    private async Task<WorkerResultMessage> SendWorkerTaskAsync(Process worker,WorkerTaskMessage message,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
        await worker.StandardInput.FlushAsync(cancellationToken);
        while(true){
            cancellationToken.ThrowIfCancellationRequested();
            var lineTask = worker.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
            var line = await lineTask;
            if(line is null){
                throw new InvalidOperationException($"Benchmark worker exited before returning result. ExitCode: {(worker.HasExited?worker.ExitCode:null)}");
            }

            try{
                var result = JsonSerializer.Deserialize<WorkerResultMessage>(line);
                if(result is not null){
                    if(!string.Equals(result.TaskId,message.TaskId,StringComparison.Ordinal)){
                        this.AppendLog($"> worker task id mismatch. expected={message.TaskId} actual={result.TaskId}");
                    }
                    return result;
                }
            }catch(JsonException){
                this.AppendLog(line);
            }
        }
    }

    private async Task WaitForRunCompletionAsync(CancellationToken cancellationToken){
        while(true){
            cancellationToken.ThrowIfCancellationRequested();
            Task[] workers;
            lock(this.runGate){
                if(this.pendingRunItems.Count==0&& this.runningProcesses.Count==0&& this.cpuWorkers.Count==0){
                    return;
                }

                workers= this.cpuWorkers.Values.ToArray();
            }

            if(this.cpuCores.All(static cpu => !cpu.IsSelected)){
                this.SetStatus("荳譎ょ●豁｢荳ｭ縺ｧ縺吶ょ・髢九☆繧九↓縺ｯCPU繧・縺､莉･荳企∈謚槭＠縺ｦ縺上□縺輔＞縲・);
            }else{
                foreach(var cpu in this.cpuCores.Where(static cpu => cpu.IsSelected)){
                    this.StartWorkerForCpu(cpu);
                }
            }

            if(workers.Length>0){
                await Task.WhenAny(Task.WhenAll(workers),Task.Delay(500,cancellationToken));
            }else{
                await Task.Delay(500,cancellationToken);
            }
        }
    }

    private void StopCpuAndRequeue(int logicalProcessorIndex){
        Process? process = null;
        lock(this.runGate){
            if(this.runningProcesses.TryGetValue(logicalProcessorIndex,out process)){
                if(this.runningRunItems.ContainsKey(logicalProcessorIndex)){
                    this.requeueOnProcessExit.Add(logicalProcessorIndex);
                }
            }
        }

        KillProcess(process);
    }

    private void KillAllRunningProcesses(bool requeue){
        Process[] processes;
        lock(this.runGate){
            if(requeue){
                foreach(var key in this.runningRunItems.Keys){
                    this.requeueOnProcessExit.Add(key);
                }
            }
            processes= this.runningProcesses.Values.ToArray();
        }

        foreach(var process in processes){
            KillProcess(process);
        }
    }

    private static void KillProcess(Process? process){
        try{
            if(process is not null&&!process.HasExited){
                process.Kill(entireProcessTree:true);
            }
        }catch(InvalidOperationException){
        }catch(System.ComponentModel.Win32Exception){
        }
    }

    private static IReadOnlyList<BenchmarkProject> LoadBenchmarkProjects(){
        // Collect candidate assembly files from the base directory and a small number of parent directories
    var paths = new List<string>();
        try{
            paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "Benchmark.*.dll", System.IO.SearchOption.TopDirectoryOnly));
        }catch{
        }

        var parent = Directory.GetParent(AppContext.BaseDirectory);
        for(var depth = 0; parent is not null && depth < 6; depth++, parent = parent.Parent){
            try{
                // Search subdirectories under each parent to find built benchmark assemblies in sibling project output folders
                paths.AddRange(Directory.GetFiles(parent.FullName, "Benchmark.*.dll", System.IO.SearchOption.AllDirectories));
            }catch{
                // ignore inaccessible folders
            }
        }

        var assemblies = paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(LoadAssembly)
            .Where(static assembly => assembly is not null)
            .Cast<Assembly>()
            .DistinctBy(static assembly => assembly.FullName)
            .ToArray();

        var projects = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(static type => type is { IsAbstract:false, IsClass:true })
            .Where(static type => type.GetMethods(BindingFlags.Instance|BindingFlags.Public)
                .Any(IsBenchmarkMethod))
            .Where(static type => !string.Equals(CreateBenchmarkDisplayName(type),"SystemLinq",StringComparison.OrdinalIgnoreCase))
            .Select(type => new BenchmarkProject(CreateBenchmarkDisplayName(type),type,IsDefaultBenchmarkType(type)))
            .OrderBy(static project => GetBenchmarkProjectOrder(project.BenchmarkType))
            .ThenBy(static project => project.DisplayName,StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if(projects.Length==0){
            throw new InvalidOperationException("No benchmark types were found in referenced Benchmark assemblies.");
        }

        if(projects.All(static project => !project.IsSelected)){
            projects[0].IsSelected=true;
        }

        return projects;
    }

    private static Assembly? LoadAssembly(string path){
        try{
            return Assembly.LoadFrom(path);
        }catch(BadImageFormatException){
            return null;
        }catch(FileLoadException){
            return null;
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly){
        try{
            return assembly.GetTypes();
        }catch(ReflectionTypeLoadException ex){
            return ex.Types.Where(static type => type is not null).Cast<Type>();
        }
    }

    private static IReadOnlyList<BenchmarkProjectMethod> LoadBenchmarkMethods(Type benchmarkType){
        return benchmarkType
            .GetMethods(BindingFlags.Instance|BindingFlags.Public)
            .Where(IsBenchmarkMethod)
            .Select(method => new BenchmarkProjectMethod(
                benchmarkType.Name,
                benchmarkType.FullName??benchmarkType.Name,
                method.Name,
                method.Name,
                GetBenchmarkCategories(benchmarkType)
                    .Concat(GetBenchmarkCategories(method))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static category => category,StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(static method => method.MethodName,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsBenchmarkMethod(MethodInfo method){
        return method.GetCustomAttributes(false).Any(static attribute =>
            string.Equals(attribute.GetType().FullName,"BenchmarkDotNet.Attributes.BenchmarkAttribute",StringComparison.Ordinal));
    }

    private static IEnumerable<string> GetBenchmarkCategories(MemberInfo member){
        foreach(var attribute in member.GetCustomAttributes(false)){
            if(!string.Equals(attribute.GetType().FullName,"BenchmarkDotNet.Attributes.BenchmarkCategoryAttribute",StringComparison.Ordinal)){
                continue;
            }

            if(attribute.GetType().GetProperty("Categories")?.GetValue(attribute) is IEnumerable<string> categories){
                foreach(var category in categories){
                    if(!string.IsNullOrWhiteSpace(category)){
                        yield return category;
                    }
                }
            }
        }
    }

    private static bool IsDefaultBenchmarkType(Type type){
        return GetBenchmarkProjectOrder(type)<int.MaxValue;
    }

    private static int GetBenchmarkProjectOrder(Type type){
        var sourceOrder = GetBenchmarkSource(type) switch{
            BenchmarkSource.DotNet => 0,
            BenchmarkSource.Persisted => 1,
            BenchmarkSource.Tsavorite => 2,
            _ => 9,
        };
        var kindOrder = type.Name.EndsWith("FLinq",StringComparison.Ordinal) ? 0
            : type.Name.EndsWith("ZLinq",StringComparison.Ordinal) ? 1
            : type.Name.EndsWith("Linq",StringComparison.Ordinal)&&!type.Name.EndsWith("FLinq",StringComparison.Ordinal) ? 2
            : 9;
        var shapeOrder = type.Name.StartsWith("IEnumerable",StringComparison.Ordinal) ? 1
            : type.Name.StartsWith("IHashSet",StringComparison.Ordinal) ? 2
            : 0;
        return sourceOrder*100+shapeOrder*10+kindOrder;
    }

    private static string CreateBenchmarkDisplayName(Type type){
        var source = GetBenchmarkSource(type) switch{
            BenchmarkSource.DotNet => "System",
            BenchmarkSource.Persisted => "Persisted",
            BenchmarkSource.Tsavorite => "Tsavorite",
            _ => "System",
        };
        var name = type.Name;
        var suffix = name.EndsWith("FLinq",StringComparison.Ordinal) ? "FLinq"
            : name.EndsWith("ZLinq",StringComparison.Ordinal) ? "ZLinq"
            : name.EndsWith("Linq",StringComparison.Ordinal) ? "Linq"
            : name;
        var prefix = suffix switch{
            "FLinq" => name[..^5],
            "ZLinq" => name[..^5],
            "Linq" => name[..^4],
            _ => string.Empty,
        };
        return string.IsNullOrWhiteSpace(prefix)
            ? source+suffix
            : source+prefix+suffix;
    }

    private static BenchmarkSource GetBenchmarkSource(Type type){
        var ns = type.Namespace??string.Empty;
        if(ns.Contains(".Persisted.",StringComparison.OrdinalIgnoreCase)){
            return BenchmarkSource.Persisted;
        }

        if(ns.Contains(".Tsavorite.",StringComparison.OrdinalIgnoreCase)){
            return BenchmarkSource.Tsavorite;
        }

        if(ns.Contains(".DotNet.",StringComparison.OrdinalIgnoreCase)){
            return BenchmarkSource.DotNet;
        }

        return BenchmarkSource.Unknown;
    }

    private enum BenchmarkSource{
        Unknown,
        DotNet,
        Persisted,
        Tsavorite,
    }

    private IReadOnlyList<BenchmarkProject> GetSelectedProjects(){
        return this.benchmarkProjects.Where(static project => project.IsSelected).ToArray();
    }

    private string GetConfiguration(){
        return (this.ConfigurationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()??"Release";
    }

    private string GetSelectedElementScale(){
        return (this.ElementScaleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()??"1";
    }

    private string CreateRunRoot(string configuration){
        var runId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
        var path = Path.Combine(this.repositoryRoot,"Benchmark",configuration,TargetFramework,"UnoRuns",runId);
        Directory.CreateDirectory(path);
        return path;
    }

    private async Task<ProcessResult> RunDotnetProcessAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        Action<string> output,
        CancellationToken cancellationToken,
        long? processorAffinity = null,
        Action<Process>? onStarted = null){
        this.AppendLog("> dotnet "+string.Join(" ",arguments.Select(QuoteArgument)));

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        using var process = new Process{
            StartInfo = new ProcessStartInfo{
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        foreach(var argument in arguments){
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.OutputDataReceived+=(sender,e) => {
            if(e.Data is null){
                return;
            }

            lock(standardOutput){
                standardOutput.AppendLine(e.Data);
            }
            output(e.Data);
        };
        process.ErrorDataReceived+=(sender,e) => {
            if(e.Data is null){
                return;
            }

            lock(standardError){
                standardError.AppendLine(e.Data);
            }
            output(e.Data);
        };

        process.Start();
        if(processorAffinity is { } affinity&&(OperatingSystem.IsWindows()||OperatingSystem.IsLinux())){
            try{
                process.ProcessorAffinity=new IntPtr(affinity);
            }catch(PlatformNotSupportedException){
            }catch(InvalidOperationException){
            }catch(System.ComponentModel.Win32Exception){
            }
        }

        onStarted?.Invoke(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try{
            await process.WaitForExitAsync(cancellationToken);
        }catch(OperationCanceledException){
            KillProcess(process);
            throw;
        }

        return new ProcessResult(process.ExitCode,standardOutput.ToString(),standardError.ToString());
    }

    private static IReadOnlyList<BenchmarkProjectMethod> ParseBenchmarkMethods(string output){
        var start = output.IndexOf("[{",StringComparison.Ordinal);
        var end = output.LastIndexOf(']');
        if(start<0||end<start){
            return [];
        }

        var json = output[start..(end+1)];
        var items = JsonSerializer.Deserialize<BenchmarkListItem[]>(json,new JsonSerializerOptions{
            PropertyNameCaseInsensitive=true,
        })??[];

        return items
            .Select(static item => new BenchmarkProjectMethod(
                item.TypeName,
                item.TypeFullName,
                item.MethodName,
                item.DisplayName,
                item.Categories??[]))
            .ToArray();
    }

    private static (string Mean,string Error,string StdDev,string AllocatedBytes) ReadResultValues(string artifactFolder,string methodName){
        var csvFile = Directory.Exists(artifactFolder)
            ? Directory.GetFiles(artifactFolder,"*-report.csv",System.IO.SearchOption.AllDirectories)
                .Select(static path => new FileInfo(path))
                .OrderByDescending(static file => file.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if(csvFile is null){
            return (string.Empty,string.Empty,string.Empty,string.Empty);
        }

        using var parser = new TextFieldParser(csvFile.FullName,Encoding.UTF8){
            TextFieldType=FieldType.Delimited,
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false,
        };
        parser.SetDelimiters(",");
        if(parser.EndOfData){
            return (string.Empty,string.Empty,string.Empty,string.Empty);
        }

        var header = parser.ReadFields()??[];
        var methodIndex = Array.IndexOf(header,"Method");
        var meanIndex = Array.IndexOf(header,"Mean");
        var errorIndex = Array.IndexOf(header,"Error");
        var stdDevIndex = Array.IndexOf(header,"StdDev");
        var allocatedIndex = Array.IndexOf(header,"Allocated");
        while(!parser.EndOfData){
            var row = parser.ReadFields()??[];
            if(methodIndex>=0
                &&row.Length>methodIndex
                &&string.Equals(row[methodIndex],methodName,StringComparison.OrdinalIgnoreCase)){
                return (
                    GetCell(row,meanIndex),
                    GetCell(row,errorIndex),
                    GetCell(row,stdDevIndex),
                    FormatLong(ParseMemoryBytes(GetCell(row,allocatedIndex))));
            }
        }

        return (string.Empty,string.Empty,string.Empty,string.Empty);
    }

    private RunReportPaths CreateRunReport(string? runRoot){
        if(string.IsNullOrWhiteSpace(runRoot)){
            return new RunReportPaths(null);
        }

        Directory.CreateDirectory(runRoot);
        var rows = this.allRunItems
            .Where(static item => item.Status is BenchmarkRunStatus.Completed
                or BenchmarkRunStatus.Failed
                or BenchmarkRunStatus.Canceled
                or BenchmarkRunStatus.Running)
            .Select(static item => new ReportRow(
                item.ProjectDisplayName,
                item.MethodName,
                item.ProjectMethod.TypeFullName,
                string.IsNullOrWhiteSpace(item.ProjectMethod.TypeFullName)
                    ? item.MethodName
                    : item.ProjectMethod.TypeFullName+"."+item.ProjectMethod.MethodName,
                item.StatusText,
                item.Mean??string.Empty,
                item.Error??string.Empty,
                item.StdDev??string.Empty,
                item.AllocatedBytes??string.Empty,
                item.PeakRssBytes??string.Empty,
                item.EstimatedPeakRssBytes??string.Empty,
                item.MeanNanoseconds??0,
                item.ErrorNanoseconds??0,
                item.StdDevNanoseconds??0,
                item.AllocatedBytesValue??0,
                item.PeakRssBytesValue??0,
                item.EstimatedPeakRssBytesValue??0,
                item.AssignedCpu?.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture)??string.Empty,
                item.AssignedCpu?.CoreLabel??string.Empty,
                item.AssignedCpu?.CpuDisplayText??string.Empty,
                item.StartedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
                item.FinishedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
                item.ArtifactFolder??string.Empty,
                item.ExitCode))
            .ToArray();

        this.comparisonRows=BenchmarkComparisonFormatter.BuildComparisonRows(rows);
        this.UpdateAnalysis();
        var csvPath = Path.Combine(runRoot,"BenchmarkComparison.csv");
        WriteRunCsv(csvPath,this.comparisonRows);
        this.AppendLog($"螳滓ｸｬ蛟､CSV: {csvPath}");
        return new RunReportPaths(csvPath);
    }

    private static void WriteRunCsv(string path,IReadOnlyList<BenchmarkComparisonRow> rows){
        File.WriteAllText(path,BenchmarkComparisonFormatter.WriteCsv(rows),new UTF8Encoding(true));
    }

    private void WriteRunSummary(string status,RunReportPaths reportPaths){
        if(string.IsNullOrWhiteSpace(this.currentRunRoot)){
            return;
        }

        Directory.CreateDirectory(this.currentRunRoot);
        var summaryPath = Path.Combine(this.currentRunRoot,"BenchmarkRunSummary.txt");
        var builder = new StringBuilder();
        builder.AppendLine($"Status: {status}");
        builder.AppendLine($"StartedAt: {this.currentRunStartedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty}");
        builder.AppendLine($"FinishedAt: {this.currentRunFinishedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty}");
        builder.AppendLine($"Elapsed: {FormatRunElapsed(this.currentRunStartedAt,this.currentRunFinishedAt)}");
        builder.AppendLine($"TotalTasks: {this.allRunItems.Count}");
        builder.AppendLine($"CompletedTasks: {this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Completed)}");
        builder.AppendLine($"FailedTasks: {this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Failed)}");
        builder.AppendLine($"CanceledTasks: {this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Canceled)}");
        builder.AppendLine($"MaxPeakRssBytes: {MaxLongText(this.allRunItems.Select(static item => item.PeakRssBytes))}");
        builder.AppendLine($"MaxEstimatedPeakRssBytes: {MaxLongText(this.allRunItems.Select(static item => item.EstimatedPeakRssBytes))}");
        builder.AppendLine($"CsvReport: {reportPaths.CsvPath??string.Empty}");
        File.WriteAllText(summaryPath,builder.ToString(),Encoding.UTF8);
        this.AppendLog($"Run summary: {summaryPath}");
    }

    private void SetBusy(bool value,bool allowCpuChanges){
        this.isBusy=value;
        this.BusyRing.IsActive=value;
        foreach(var child in this.ProjectGrid.Children.OfType<Control>()){
            child.IsEnabled=!value;
        }

        this.ConfigurationComboBox.IsEnabled=!value;
        this.ElementScaleComboBox.IsEnabled=!value;
        foreach(var child in this.CpuPresetGrid.Children.OfType<Control>()){
            child.IsEnabled=!value||allowCpuChanges;
        }

        this.CpuItemsControl.IsEnabled=!value||allowCpuChanges;
        this.CancelButton.IsEnabled=value&& this.runCancellation is not null;
        this.UpdateCommandState();
    }

    private void SetReportPaths(string? csvPath){
        this.RunOnUi(() => {
            if(!string.IsNullOrWhiteSpace(csvPath)){
                this.SetStatus($"CSV繧呈嶌縺榊・縺励∪縺励◆: {csvPath}");
            }
        });
    }

    private void UpdateCommandState(){
        var hasSelectedBenchmark = this.benchmarks.Any(static benchmark => benchmark.IsSelected);
        var hasSelectedProject = this.benchmarkProjects.Any(static project => project.IsSelected);
        this.RunButton.IsEnabled=!this.isBusy&&hasSelectedProject&&hasSelectedBenchmark;
    }

    private void UpdateSelectionSummary(){
        var selectedCount = this.benchmarks.Count(static benchmark => benchmark.IsSelected);
        this.SelectionSummaryText.Text=$"{selectedCount} / {this.benchmarks.Count} 莉ｶ繧帝∈謚樔ｸｭ";
        if(this.SelectionHeaderButton is not null){
            this.SelectionHeaderButton.Content=selectedCount==0
                ? "笘・
                : selectedCount==this.benchmarks.Count
                    ? "笘・
                    : "笆｣";
        }
    }

    private void RefreshComparisonRowsFromBenchmarks(){
        this.comparisonRows=BenchmarkComparisonFormatter.BuildComparisonRows(this.CreateReportRowsFromBenchmarkItems());
        this.UpdateAnalysis();
    }

    private IReadOnlyList<ReportRow> CreateReportRowsFromBenchmarkItems(){
        var rows = new List<ReportRow>();
        foreach(var benchmark in this.benchmarks){
            foreach(var cell in benchmark.ProjectStatuses){
                rows.Add(new ReportRow(
                    cell.DisplayName,
                    benchmark.MethodName,
                    string.Empty,
                    benchmark.MethodName,
                    cell.Status==BenchmarkProjectRunStatus.Completed ? "Completed" : cell.Status.ToString(),
                    cell.TimeFullText,
                    string.Empty,
                    string.Empty,
                    cell.AllocatedFullText,
                    string.Empty,
                    string.Empty,
                    cell.TimeSortValue??0,
                    0,
                    0,
                    cell.AllocatedSortValue??0,
                    0,
                    0,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    null));
            }
        }

        return rows;
    }

    private void UpdateAnalysis(){
        if(this.RankingItemsControl is null){
            return;
        }

        var rows = this.GetAnalysisRows();
        this.RankingItemsControl.Visibility=this.selectedAnalysisTab==AnalysisTab.Ranking ? Visibility.Visible : Visibility.Collapsed;
        this.HeatmapItemsControl.Visibility=this.selectedAnalysisTab==AnalysisTab.Heatmap ? Visibility.Visible : Visibility.Collapsed;
        this.DetailItemsControl.Visibility=this.selectedAnalysisTab==AnalysisTab.Detail ? Visibility.Visible : Visibility.Collapsed;
        this.UpdateAnalysisSummary(rows);
        this.UpdateRanking(rows);
        this.UpdateHeatmap(rows);
        this.UpdateDetail(rows);
    }

    private IReadOnlyList<BenchmarkComparisonRow> GetAnalysisRows(){
        var rows = this.comparisonRows;
        if(this.CheckedOnlyCheckBox?.IsChecked==true){
            var selected = this.benchmarks
                .Where(static item => item.IsSelected)
                .Select(static item => item.MethodName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows=rows.Where(row => selected.Contains(row.Benchmark)).ToArray();
        }

        return rows;
    }

    private void UpdateAnalysisSummary(IReadOnlyList<BenchmarkComparisonRow> rows){
        if(this.CheckedOnlyCheckBox?.IsChecked==true&&rows.Count==0){
            this.AnalysisSummaryText.Text="繝√ぉ繝・け貂医∩鬆・岼縺後≠繧翫∪縺帙ｓ";
            return;
        }

        var pair = this.GetSelectedPairDefinition();
        var ratios = rows
            .Select(row => this.GetRatio(row,pair))
            .Where(static ratio => ratio.Judgement!=RatioJudgement.None)
            .ToArray();
        var wins = ratios.Count(static ratio => ratio.Judgement==RatioJudgement.LeftWin);
        var losses = ratios.Count(static ratio => ratio.Judgement==RatioJudgement.LeftLose);
        var numeric = ratios
            .Select(static ratio => ratio.NumericValue)
            .Where(static value => value is not null)
            .Select(static value => value!.Value)
            .OrderBy(static value => value)
            .ToArray();
        var median = numeric.Length==0 ? "-" : numeric[numeric.Length/2].ToString("0.00",CultureInfo.InvariantCulture);
        var worst = rows
            .Select(row => new { Row = row, Ratio = this.GetRatio(row,pair), Score = GetSortScore(this.GetRatio(row,pair)) })
            .Where(static item => item.Score is not null)
            .OrderByDescending(static item => item.Score)
            .FirstOrDefault();
        this.AnalysisSummaryText.Text=rows.Count==0
            ? "陦ｨ遉ｺ縺ｧ縺阪ｋ豈碑ｼ・ョ繝ｼ繧ｿ縺後≠繧翫∪縺帙ｓ"
            : $"蟇ｾ雎｡ {rows.Count} 莉ｶ / 蜍昴■ {wins} / 雋縺・{losses} / 荳ｭ螟ｮ蛟､ {median} / 譛螟ｧ謔ｪ蛹・{(worst is null ? "-" : worst.Row.Benchmark+" "+worst.Ratio.DisplayText)}";
    }

    private void UpdateRanking(IReadOnlyList<BenchmarkComparisonRow> rows){
        this.rankingItems.Clear();
        if(this.selectedAnalysisTab!=AnalysisTab.Ranking){
            return;
        }

        var pair = this.GetSelectedPairDefinition();
        foreach(var item in rows
            .Select(row => new { Row = row, Ratio = this.GetRatio(row,pair), Score = GetSortScore(this.GetRatio(row,pair)) })
            .Where(static item => item.Score is not null)
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Row.Benchmark,StringComparer.OrdinalIgnoreCase)){
            this.rankingItems.Add(new AnalysisRankingItem(
                item.Row.Benchmark,
                pair.Key,
                item.Ratio.DisplayText,
                GetBarWidth(item.Ratio),
                GetRatioBrush(item.Ratio),
                GetRatioBorderBrush(item.Ratio),
                GetRatioBorderThickness(item.Ratio)));
        }
    }

    private void UpdateHeatmap(IReadOnlyList<BenchmarkComparisonRow> rows){
        this.heatmapRows.Clear();
        if(this.selectedAnalysisTab!=AnalysisTab.Heatmap){
            return;
        }

        foreach(var row in rows.OrderBy(static row => row.Benchmark,StringComparer.OrdinalIgnoreCase)){
            var cells = BenchmarkComparisonFormatter.PairDefinitions
                .Select(pair => CreateHeatmapCell(row,pair,this.GetRatio(row,pair)))
                .ToArray();
            this.heatmapRows.Add(new AnalysisHeatmapRow(row.Benchmark,cells[0],cells[1],cells[2],cells[3],cells[4]));
        }
    }

    private void UpdateDetail(IReadOnlyList<BenchmarkComparisonRow> rows){
        this.detailItems.Clear();
        if(this.selectedAnalysisTab!=AnalysisTab.Detail){
            return;
        }

        if(string.IsNullOrWhiteSpace(this.selectedAnalysisMethod)){
            this.AnalysisSummaryText.Text="繝ｩ繝ｳ繧ｭ繝ｳ繧ｰ縺ｾ縺溘・繝偵・繝医・繝・・縺九ｉ繝｡繧ｽ繝・ラ繧帝∈謚槭＠縺ｦ縺上□縺輔＞";
            return;
        }

        var row = rows.FirstOrDefault(row => string.Equals(row.Benchmark,this.selectedAnalysisMethod,StringComparison.OrdinalIgnoreCase));
        if(string.IsNullOrWhiteSpace(row.Benchmark)){
            this.AnalysisSummaryText.Text="繝ｩ繝ｳ繧ｭ繝ｳ繧ｰ縺ｾ縺溘・繝偵・繝医・繝・・縺九ｉ繝｡繧ｽ繝・ラ繧帝∈謚槭＠縺ｦ縺上□縺輔＞";
            return;
        }

        foreach(var pair in BenchmarkComparisonFormatter.PairDefinitions){
            var comparison = row.GetPair(pair.Key);
            this.detailItems.Add(new AnalysisDetailItem(
                pair.TooltipText,
                comparison.TimeRatio.DisplayText,
                comparison.AllocatedRatio.DisplayText,
                FormatDetailMetrics(comparison.Left),
                FormatDetailMetrics(comparison.Right)));
        }
    }

    private ComparisonPairDefinition GetSelectedPairDefinition(){
        var pair = BenchmarkComparisonFormatter.PairDefinitions.FirstOrDefault(pair => string.Equals(pair.Key,this.selectedAnalysisPairKey,StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(pair.Key)
            ? BenchmarkComparisonFormatter.PairDefinitions[0]
            : pair;
    }

    private RatioValue GetRatio(BenchmarkComparisonRow row,ComparisonPairDefinition pair){
        var comparison = row.GetPair(pair.Key);
        return this.selectedAnalysisMetric==AnalysisMetric.Allocated
            ? comparison.AllocatedRatio
            : comparison.TimeRatio;
    }

    private static AnalysisHeatmapCell CreateHeatmapCell(BenchmarkComparisonRow row,ComparisonPairDefinition pair,RatioValue ratio){
        return new AnalysisHeatmapCell(row.Benchmark,pair.Key,ratio.DisplayText,GetRatioBrush(ratio));
    }

    private static double? GetSortScore(RatioValue ratio){
        return ratio.Kind switch{
            RatioValueKind.LeftOnly => 2.0,
            RatioValueKind.RightOnly => 0.5,
            RatioValueKind.BothZero => 1.0,
            RatioValueKind.Numeric => ratio.NumericValue,
            _ => null,
        };
    }

    private static double GetBarWidth(RatioValue ratio){
        var score = GetSortScore(ratio)??0;
        var clamped = Math.Clamp(score,0.5,2.0);
        return 20+((clamped-0.5)/1.5*200);
    }

    private static Brush GetRatioBrush(RatioValue ratio){
        var score = GetSortScore(ratio);
        if(score is null){
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,235,235,235));
        }

        if(score>1){
            var t = Math.Clamp((score.Value-1.0)/1.0,0,1);
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,(byte)(245),(byte)(210-(60*t)),(byte)(205-(70*t))));
        }

        if(score<1){
            var t = Math.Clamp((1.0-score.Value)/0.5,0,1);
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,(byte)(215-(55*t)),(byte)(232-(20*t)),(byte)(222+(10*t))));
        }

        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,240,238,230));
    }

    private static Brush GetRatioBorderBrush(RatioValue ratio){
        return ratio.IsSaturatedHigh||ratio.IsSaturatedLow
            ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,60,60,60))
            : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,200,195,185));
    }

    private static Thickness GetRatioBorderThickness(RatioValue ratio){
        return ratio.IsSaturatedHigh||ratio.IsSaturatedLow
            ? new Thickness(3)
            : new Thickness(1);
    }

    private static string FormatDetailMetrics(ProjectMetrics metrics){
        if(!metrics.HasValue){
            return "-";
        }

        return $"{FormatShortDuration(metrics.TimeNanoseconds)} / {FormatShortBytes(metrics.AllocatedBytes)}";
    }

    private void UpdateProgress(){
        this.RunOnUi(() => {
            var total = this.allRunItems.Count;
            var done = this.allRunItems.Count(static item => item.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed);
            var totalMethods = this.allRunItems
                .Select(static item => item.MethodName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var doneMethods = this.allRunItems
                .GroupBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
                .Count(static group => group.All(static item => item.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed));
            var failed = this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Failed);
            var running = this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Running);
            var remaining = Math.Max(0,total-done);
            this.ProgressBar.Maximum=Math.Max(1,totalMethods);
            this.ProgressBar.Value=doneMethods;
            this.ProgressText.Text=total==0
                ? "No run queued"
                : $"{doneMethods} / {totalMethods} methods done ({done} / {total} tasks, {remaining} remaining, {running} running, {failed} failed)";
            this.UpdateCpuSummary();
        });
    }

    private void UpdateCpuSummary(){
        var selected = this.cpuCores.Count(static cpu => cpu.IsSelected);
        this.CpuSummaryText.Text=$"{selected} / {this.cpuCores.Count} CPU繧帝∈謚樔ｸｭ";
    }

    private void UpdateRunTimeDisplay(){
        this.RunOnUi(() => {
            this.RunTimeText.Text=this.currentRunStartedAt is null
                ? "螳溯｡梧凾髢・ -"
                : this.currentRunFinishedAt is null
                    ? $"螳溯｡梧凾髢・ {FormatRunTimestamp(this.currentRunStartedAt.Value)} -> 螳溯｡御ｸｭ"
                    : $"螳溯｡梧凾髢・ {FormatRunTimestamp(this.currentRunStartedAt.Value)} -> {FormatRunTimestamp(this.currentRunFinishedAt.Value)} ({FormatRunElapsed(this.currentRunStartedAt,this.currentRunFinishedAt)})";
        });
    }

    private static string FormatRunTimestamp(DateTimeOffset timestamp){
        return timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture);
    }

    private static string FormatRunElapsed(DateTimeOffset? startedAt,DateTimeOffset? finishedAt){
        if(startedAt is null||finishedAt is null){
            return string.Empty;
        }

        return (finishedAt.Value-startedAt.Value).ToString(@"hh\:mm\:ss",CultureInfo.InvariantCulture);
    }

    private void SetStatus(string text){
        this.RunOnUi(() => this.StatusText.Text=text);
    }

    private void AppendLog(string text){
        this.RunOnUi(() => this.AppendLogCore(text));
    }

    private void AppendLogCore(string text){
        this.LogTextBox.Text+=text+Environment.NewLine;
        this.LogTextBox.SelectionStart= this.LogTextBox.Text.Length;
    }

    private void RunOnUi(Action action){
        if(this.DispatcherQueue.HasThreadAccess){
            action();
        }else{
            this.DispatcherQueue.TryEnqueue(() => action());
        }
    }

    private Task RunOnUiAsync(Action action){
        if(this.DispatcherQueue.HasThreadAccess){
            action();
            return Task.CompletedTask;
        }

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!this.DispatcherQueue.TryEnqueue(() => {
            try{
                action();
                source.SetResult();
            }catch(Exception ex){
                source.SetException(ex);
            }
        })){
            source.SetException(new InvalidOperationException("Failed to enqueue UI work."));
        }

        return source.Task;
    }

    private static string FindRepositoryRoot(){
        for(var directory = new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent){
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string QuoteArgument(string argument){
        return argument.Contains(' ',StringComparison.Ordinal)||argument.Contains('|',StringComparison.Ordinal)
            ? $"\"{argument}\""
            : argument;
    }

    private static string GetCell(string[] row,int index){
        return index>=0&&index<row.Length ? row[index] : string.Empty;
    }

    private static string FirstText(params string?[] values){
        foreach(var value in values){
            if(!string.IsNullOrWhiteSpace(value)){
                return value;
            }
        }

        return string.Empty;
    }

    private static string FormatNanoseconds(double? value){
        return value is { } number&&double.IsFinite(number)
            ? number.ToString("N3",CultureInfo.InvariantCulture)+" ns"
            : string.Empty;
    }

    private static string FormatShortDuration(double? nanoseconds){
        if(nanoseconds is not { } value||!double.IsFinite(value)||value<0){
            return string.Empty;
        }

        if(value>=1_000_000_000){
            return (value/1_000_000_000).ToString("0.##",CultureInfo.InvariantCulture)+" s";
        }

        if(value>=1_000_000){
            return (value/1_000_000).ToString("0.##",CultureInfo.InvariantCulture)+" ms";
        }

        if(value>=1_000){
            return (value/1_000).ToString("0.##",CultureInfo.InvariantCulture)+" us";
        }

        return value.ToString("0.##",CultureInfo.InvariantCulture)+" ns";
    }

    private static string FormatShortBytes(long? bytes){
        if(bytes is not { } value||value<0){
            return string.Empty;
        }

        if(value>=1024L*1024L*1024L){
            return (value/(1024d*1024d*1024d)).ToString("0.##",CultureInfo.InvariantCulture)+" GB";
        }

        if(value>=1024L*1024L){
            return (value/(1024d*1024d)).ToString("0.##",CultureInfo.InvariantCulture)+" MB";
        }

        if(value>=1024L){
            return (value/1024d).ToString("0.##",CultureInfo.InvariantCulture)+" KB";
        }

        return value.ToString(CultureInfo.InvariantCulture)+" B";
    }

    private static double? ParseNanoseconds(string value){
        var text = value.Trim();
        if(text.EndsWith("ns",StringComparison.OrdinalIgnoreCase)){
            text=text[..^2].Trim();
        }

        return double.TryParse(
            text,
            NumberStyles.AllowThousands|NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number)
            &&number>=0
            ? number
            : null;
    }

    private static string FormatLong(long? value){
        return value is { } number&&number>=0
            ? number.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string MaxLongText(IEnumerable<string?> values){
        var max = values
            .Select(static value => long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return max>0
            ? max.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static long? ParseMemoryBytes(string value){
        var text = value.Trim();
        if(string.IsNullOrWhiteSpace(text)||text=="-"){
            return null;
        }

        var parts = text.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(parts.Length==0){
            return null;
        }

        var numberText = parts[0].Replace(",","",StringComparison.Ordinal);
        if(!double.TryParse(numberText,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||number<0){
            return null;
        }

        var unit = parts.Length>1 ? parts[1] : "B";
        var multiplier = unit.ToUpperInvariant() switch{
            "B" => 1d,
            "BYTE" => 1d,
            "BYTES" => 1d,
            "KB" => 1024d,
            "KIB" => 1024d,
            "MB" => 1024d*1024d,
            "MIB" => 1024d*1024d,
            "GB" => 1024d*1024d*1024d,
            "GIB" => 1024d*1024d*1024d,
            _ => 1d,
        };
        return (long)Math.Round(number*multiplier,MidpointRounding.AwayFromZero);
    }
}
