using global::System.Collections.ObjectModel;
using global::System.Diagnostics;
using global::System.Globalization;
using global::System.Reflection;
using global::System.Text;
using global::System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Microsoft.UI.Xaml.Input;

namespace Benchmark.Runner.Uno;

public sealed partial class MainPage:Page{
    private const string TargetFramework = "net10.0";

    private readonly string repositoryRoot = FindRepositoryRoot();
    private readonly ObservableCollection<BenchmarkItem> benchmarks = [];
    private readonly ObservableCollection<CpuCoreInfo> cpuCores = [];
    private readonly ObservableCollection<CpuRowItem> cpuRows = [];
    private readonly ObservableCollection<BenchmarkRunItem> runningItems = [];
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
    private string? htmlReportPath;
    private string? chartReportPath;
    private string? currentRunRoot;
    private DateTimeOffset? currentRunStartedAt;
    private DateTimeOffset? currentRunFinishedAt;
    private int? selectionAnchorIndex;
    private bool isShiftDown;
    private bool isControlDown;
    private bool isBusy;
    private bool isRunning;
    private bool projectSelectionDirty = true;

    private sealed record RunReportPaths(
        string? CsvPath,
        string? HtmlPath,
        string? ChartHtmlPath);

    public MainPage(){
        this.InitializeComponent();
    }

    private async void OnPageLoaded(object sender,RoutedEventArgs e){
        this.InitializeProjectGrid();
        this.InitializeCpuList();
        this.BenchmarkItemsControl.ItemsSource= this.benchmarks;
        this.BenchmarkProjectHeaderItemsControl.ItemsSource= this.benchmarkProjects;
        this.CpuItemsControl.ItemsSource= this.cpuRows;
        this.RunningItemsControl.ItemsSource= this.runningItems;
        this.UpdateSelectionSummary();
        this.UpdateProgress();
        this.UpdateCommandState();
        await this.LoadSelectedProjectsAsync(CancellationToken.None);
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
                HorizontalAlignment=HorizontalAlignment.Stretch,
                Padding=new Thickness(0),
                MinHeight=20,
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
            this.SetStatus("Select at least one CPU.");
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
    }

    private void OnClearSelectionClick(object sender,RoutedEventArgs e){
        foreach(var benchmark in this.benchmarks){
            benchmark.IsSelected=false;
        }

        this.selectionAnchorIndex=null;
        this.UpdateSelectionSummary();
        this.UpdateCommandState();
    }

    private async void OnRunClick(object sender,RoutedEventArgs e){
        var projects = this.GetSelectedProjects();
        if(projects.Count==0){
            this.SetStatus("Select at least one project.");
            return;
        }

        if(this.projectSelectionDirty|| this.benchmarks.Count==0){
            await this.LoadSelectedProjectsAsync(CancellationToken.None);
        }

        var selected = this.benchmarks.Where(static benchmark => benchmark.IsSelected).ToArray();
        if(selected.Length==0){
            this.SetStatus("Select at least one benchmark.");
            return;
        }

        if(this.cpuCores.All(static cpu => !cpu.IsSelected)){
            this.SetStatus("Select at least one CPU.");
            return;
        }

        await this.RunBenchmarksAsync(projects,selected);
    }

    private void OnCancelClick(object sender,RoutedEventArgs e){
        this.runCancellation?.Cancel();
        this.KillAllRunningProcesses(requeue:false);
    }

    private void OnOpenHtmlReportClick(object sender,RoutedEventArgs e){
        this.OpenReport(this.htmlReportPath);
    }

    private void OnOpenChartReportClick(object sender,RoutedEventArgs e){
        this.OpenReport(this.chartReportPath);
    }

    private void OpenReport(string? path){
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)){
            this.SetStatus("Report file was not found.");
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
            this.SetStatus("Select at least one project.");
            return Task.CompletedTask;
        }

        this.SetBusy(true,allowCpuChanges:false);
        var selectedMethodNames = this.GetSelectedMethodNames();
        this.benchmarks.Clear();
        this.selectionAnchorIndex=null;
        this.SetReportPaths(null,null);
        this.LogTextBox.Text=string.Empty;
        this.SetStatus("Loading canonical method list...");

        try{
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = this.GetConfiguration();
            var canonicalCacheKey = GetMethodCacheKey(configuration, this.canonicalMethodProject);
            if(!this.methodCache.ContainsKey(canonicalCacheKey)){
                this.SetStatus($"Loading methods from {this.canonicalMethodProject.DisplayName}...");
                this.methodCache[canonicalCacheKey]=LoadBenchmarkMethods(this.canonicalMethodProject.BenchmarkType);
            }

            this.RefreshBenchmarksFromCache(selectedMethodNames);
            this.SetStatus($"Loaded {this.benchmarks.Count} common methods for {projects.Count} selected project(s).");
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
        this.SetReportPaths(null,null);
        this.UpdateRunTimeDisplay();
        this.AppendLog($"Benchmark started: {FormatRunTimestamp(this.currentRunStartedAt.Value)}");
        this.SetBusy(true,allowCpuChanges:true);
        this.SetStatus($"Preparing {projects.Count} project(s) x {selectedMethods.Count} method(s) = {projects.Count*selectedMethods.Count} task(s), element scale {this.GetSelectedElementScale()}...");
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
            var reportPaths = this.CreateRunReport(this.currentRunRoot,includeHtml:true);
            this.SetReportPaths(reportPaths.HtmlPath,reportPaths.ChartHtmlPath);
            var failed = this.allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Failed);
            this.RecordRunFinished(failed==0?"Completed":$"Completed with {failed} failed task(s)",reportPaths);
            this.SetStatus(failed==0
                ? "Benchmarks completed."
                : $"Benchmarks completed with {failed} failed task(s).");
        }catch(OperationCanceledException){
            var reportPaths = this.CreateRunReport(this.currentRunRoot,includeHtml:true);
            this.SetReportPaths(reportPaths.HtmlPath,reportPaths.ChartHtmlPath);
            this.RecordRunFinished("Canceled",reportPaths);
            this.SetStatus("Benchmark canceled. Partial report was written.");
            this.AppendLog("Canceled.");
        }catch(Exception ex){
            var reportPaths = this.CreateRunReport(this.currentRunRoot,includeHtml:true);
            this.SetReportPaths(reportPaths.HtmlPath,reportPaths.ChartHtmlPath);
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
            this.SetStatus("Select at least one project.");
            return;
        }

        if(!this.methodCache.TryGetValue(GetMethodCacheKey(this.GetConfiguration(), this.canonicalMethodProject),out var canonicalMethods)){
            this.projectSelectionDirty=true;
            this.SetStatus("Loading method list...");
            return;
        }

        foreach(var item in CreateBenchmarkItemsFromCanonicalMethods(this.benchmarkProjects,canonicalMethods)){
            item.IsSelected=selectedMethodNames.Contains(item.MethodName);
            this.benchmarks.Add(item);
        }

        this.projectSelectionDirty=false;
        this.SetStatus($"Loaded {this.benchmarks.Count} common methods for {projects.Count} selected project(s).");
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
                return new BenchmarkItem(method.MethodName,method.MethodName,method.Categories,methodsByProject,projectColumns);
            })
            .ToArray();
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

    private static void SetRunItemStatus(BenchmarkRunItem item,BenchmarkRunStatus status){
        item.Status=status;
        item.Method.SetProjectStatus(item.Project.ProjectKey,status);
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
        var message = new WorkerTaskMessage(
            "run",
            taskId,
            item.Project.AssemblyName,
            item.Project.TypeFullName,
            item.MethodName, this.GetSelectedElementScale(),
            artifactFolder);
        WorkerResultMessage? result = null;
        Exception? workerException = null;
        var requeue = false;
        try{
            lock(this.runGate){
                this.runningRunItems[cpu.LogicalProcessorIndex]=item;
            }
            result = await this.SendWorkerTaskAsync(worker,message,cancellationToken);
        }catch(OperationCanceledException){
            await this.RunOnUiAsync(() => {
                this.runningItems.Remove(item);
                item.ProcessId=0;
                item.FinishedAt=DateTimeOffset.Now;
                SetRunItemStatus(item,BenchmarkRunStatus.Canceled);
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

        item.ExitCode=result.ExitCode;
        await this.RunOnUiAsync(() => {
            item.ExitCode=result.ExitCode;
            item.Mean=result.Mean;
            item.Error=result.Error;
            item.StdDev=result.StdDev;
            item.FinishedAt=DateTimeOffset.Now;
            item.ProcessId=0;
            SetRunItemStatus(item,result.ExitCode==0 ? BenchmarkRunStatus.Completed : BenchmarkRunStatus.Failed);
        });
        if(!string.IsNullOrWhiteSpace(result.Log)){
            this.AppendLog(result.Log);
        }

        this.UpdateProgress();
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
                this.SetStatus("Paused. Select at least one CPU to resume.");
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
        }catch(global::System.ComponentModel.Win32Exception){
        }
    }

    private static IReadOnlyList<BenchmarkProject> LoadBenchmarkProjects(){
        // Collect candidate assembly files from the base directory and a small number of parent directories
    var paths = new List<string>();
        try{
            paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "Benchmark.*.dll", global::System.IO.SearchOption.TopDirectoryOnly));
        }catch{
        }

        var parent = Directory.GetParent(AppContext.BaseDirectory);
        for(var depth = 0; parent is not null && depth < 6; depth++, parent = parent.Parent){
            try{
                // Search subdirectories under each parent to find built benchmark assemblies in sibling project output folders
                paths.AddRange(Directory.GetFiles(parent.FullName, "Benchmark.*.dll", global::System.IO.SearchOption.AllDirectories));
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
        return type.Name.Equals("Linq",StringComparison.Ordinal)
            &&(type.Namespace?.Contains(".DotNet.",StringComparison.OrdinalIgnoreCase)??false);
    }

    private static int GetBenchmarkProjectOrder(Type type){
        var sourceOrder = GetBenchmarkSource(type) switch{
            BenchmarkSource.DotNet => 0,
            BenchmarkSource.Persisted => 1,
            BenchmarkSource.Tsavorite => 2,
            _ => 9,
        };
        var kindOrder = type.Name.EndsWith("Linq",StringComparison.Ordinal)&&!type.Name.EndsWith("FLinq",StringComparison.Ordinal) ? 0
            : type.Name.EndsWith("FLinq",StringComparison.Ordinal) ? 1
            : type.Name.EndsWith("ZLinq",StringComparison.Ordinal) ? 2
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
            : name.EndsWith("Linq",StringComparison.Ordinal) ? "LINQ"
            : name;
        var prefix = suffix switch{
            "FLinq" => name[..^5],
            "ZLinq" => name[..^5],
            "LINQ" => name[..^4],
            _ => string.Empty,
        };
        return string.IsNullOrWhiteSpace(prefix)
            ? source+" "+suffix
            : source+" "+prefix+" "+suffix;
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
            }catch(global::System.ComponentModel.Win32Exception){
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

    private static (string Mean,string Error,string StdDev) ReadResultValues(string artifactFolder,string methodName){
        var csvFile = Directory.Exists(artifactFolder)
            ? Directory.GetFiles(artifactFolder,"*-report.csv",global::System.IO.SearchOption.AllDirectories)
                .Select(static path => new FileInfo(path))
                .OrderByDescending(static file => file.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if(csvFile is null){
            return (string.Empty,string.Empty,string.Empty);
        }

        using var parser = new TextFieldParser(csvFile.FullName,Encoding.UTF8){
            TextFieldType=FieldType.Delimited,
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false,
        };
        parser.SetDelimiters(",");
        if(parser.EndOfData){
            return (string.Empty,string.Empty,string.Empty);
        }

        var header = parser.ReadFields()??[];
        var methodIndex = Array.IndexOf(header,"Method");
        var meanIndex = Array.IndexOf(header,"Mean");
        var errorIndex = Array.IndexOf(header,"Error");
        var stdDevIndex = Array.IndexOf(header,"StdDev");
        while(!parser.EndOfData){
            var row = parser.ReadFields()??[];
            if(methodIndex>=0
                &&row.Length>methodIndex
                &&string.Equals(row[methodIndex],methodName,StringComparison.OrdinalIgnoreCase)){
                return (GetCell(row,meanIndex),GetCell(row,errorIndex),GetCell(row,stdDevIndex));
            }
        }

        return (string.Empty,string.Empty,string.Empty);
    }

    private RunReportPaths CreateRunReport(string? runRoot,bool includeHtml){
        if(string.IsNullOrWhiteSpace(runRoot)){
            return new RunReportPaths(null,null,null);
        }

        Directory.CreateDirectory(runRoot);
        var rows = this.allRunItems
            .Where(static item => item.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
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
                item.AssignedCpu?.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture)??string.Empty,
                item.AssignedCpu?.CoreLabel??string.Empty,
                item.AssignedCpu?.CpuDisplayText??string.Empty,
                item.StartedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
                item.FinishedAt?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
                item.ArtifactFolder??string.Empty,
                item.ExitCode))
            .ToArray();

        var csvPath = Path.Combine(runRoot,"BenchmarkComparison.csv");
        WriteRunCsv(csvPath,rows);
        if(!includeHtml){
            this.AppendLog($"Comparison CSV: {csvPath}");
            return new RunReportPaths(csvPath,null,null);
        }

        var htmlPath = Path.Combine(runRoot,"BenchmarkComparison.html");
        var chartHtmlPath = Path.Combine(runRoot,"BenchmarkComparisonCharts.html");
        WriteRunHtml(htmlPath,rows);
        WriteRunChartsHtml(chartHtmlPath,rows);
        this.AppendLog($"Comparison report: {htmlPath}");
        this.AppendLog($"Comparison charts: {chartHtmlPath}");
        return new RunReportPaths(csvPath,htmlPath,chartHtmlPath);
    }

    private static void WriteRunCsv(string path,IReadOnlyList<ReportRow> rows){
        File.WriteAllText(path,BenchmarkReportFormatter.BuildCsv(rows),Encoding.UTF8);
    }

    private static void WriteRunHtml(string path,IReadOnlyList<ReportRow> rows){
        File.WriteAllText(path,BenchmarkReportFormatter.BuildHtml(rows,DateTimeOffset.Now),Encoding.UTF8);
    }

    private static void WriteRunChartsHtml(string path,IReadOnlyList<ReportRow> rows){
        File.WriteAllText(path,BenchmarkReportFormatter.BuildChartsHtml(rows,DateTimeOffset.Now),Encoding.UTF8);
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
        builder.AppendLine($"CsvReport: {reportPaths.CsvPath??string.Empty}");
        builder.AppendLine($"HtmlReport: {reportPaths.HtmlPath??string.Empty}");
        builder.AppendLine($"ChartReport: {reportPaths.ChartHtmlPath??string.Empty}");
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

    private void SetReportPaths(string? htmlPath,string? chartHtmlPath){
        this.RunOnUi(() => {
            this.htmlReportPath=htmlPath;
            this.chartReportPath=chartHtmlPath;
            this.HtmlReportLinkButton.Visibility=string.IsNullOrWhiteSpace(htmlPath)
                ? Visibility.Collapsed
                : Visibility.Visible;
            this.ChartReportLinkButton.Visibility=string.IsNullOrWhiteSpace(chartHtmlPath)
                ? Visibility.Collapsed
                : Visibility.Visible;
        });
    }

    private void UpdateCommandState(){
        var hasSelectedBenchmark = this.benchmarks.Any(static benchmark => benchmark.IsSelected);
        var hasSelectedProject = this.benchmarkProjects.Any(static project => project.IsSelected);
        this.RunButton.IsEnabled=!this.isBusy&&hasSelectedProject&&hasSelectedBenchmark;
    }

    private void UpdateSelectionSummary(){
        var selectedCount = this.benchmarks.Count(static benchmark => benchmark.IsSelected);
        this.SelectionSummaryText.Text=$"{selectedCount} / {this.benchmarks.Count} selected";
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
        this.CpuSummaryText.Text=$"{selected} / {this.cpuCores.Count} CPU selected";
    }

    private void UpdateRunTimeDisplay(){
        this.RunOnUi(() => {
            this.RunTimeText.Text=this.currentRunStartedAt is null
                ? "Run time: -"
                : this.currentRunFinishedAt is null
                    ? $"Run time: {FormatRunTimestamp(this.currentRunStartedAt.Value)} -> running"
                    : $"Run time: {FormatRunTimestamp(this.currentRunStartedAt.Value)} -> {FormatRunTimestamp(this.currentRunFinishedAt.Value)} ({FormatRunElapsed(this.currentRunStartedAt,this.currentRunFinishedAt)})";
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
}
