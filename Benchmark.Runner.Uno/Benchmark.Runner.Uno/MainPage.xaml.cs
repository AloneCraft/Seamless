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
    private readonly ObservableCollection<BenchmarkRunItem> runningItems = [];
    private readonly object runGate = new();
    private readonly Queue<BenchmarkRunItem> pendingRunItems = new();
    private readonly List<BenchmarkRunItem> allRunItems = [];
    private readonly Dictionary<int,Task> cpuWorkers = [];
    private readonly Dictionary<int,Process> runningProcesses = [];
    private readonly Dictionary<int,BenchmarkRunItem> runningRunItems = [];
    private readonly HashSet<int> requeueOnProcessExit = [];
    private readonly Dictionary<string,IReadOnlyList<BenchmarkProjectMethod>> methodCache = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<BenchmarkProject> benchmarkProjects = LoadBenchmarkProjects();
    private BenchmarkProject canonicalMethodProject => benchmarkProjects[0];

    private CancellationTokenSource? runCancellation;
    private string? lastReportPath;
    private string? currentRunRoot;
    private int? selectionAnchorIndex;
    private bool isShiftDown;
    private bool isControlDown;
    private bool isBusy;
    private bool isRunning;
    private bool projectSelectionDirty = true;

    public MainPage(){
        InitializeComponent();
    }

    private async void OnPageLoaded(object sender,RoutedEventArgs e){
        InitializeProjectGrid();
        InitializeCpuList();
        BenchmarkItemsControl.ItemsSource=benchmarks;
        CpuItemsControl.ItemsSource=cpuCores;
        RunningItemsControl.ItemsSource=runningItems;
        UpdateSelectionSummary();
        UpdateProgress();
        UpdateCommandState();
        await LoadSelectedProjectsAsync(CancellationToken.None);
    }

    private void InitializeProjectGrid(){
        if(ProjectGrid.Children.Count>0){
            return;
        }

        for(var i=0;i<benchmarkProjects.Count;i++){
            var project = benchmarkProjects[i];
            var checkBox = new CheckBox{
                Content=project.DisplayName,
                IsChecked=project.IsSelected,
                FontSize=12,
                Padding=new Thickness(0),
                MinHeight=20,
                DataContext=project,
            };
            checkBox.Click+=OnProjectCheckBoxClick;
            Grid.SetColumn(checkBox,i/4);
            Grid.SetRow(checkBox,i%4);
            ProjectGrid.Children.Add(checkBox);
        }
    }

    private void InitializeCpuList(){
        if(cpuCores.Count>0){
            return;
        }

        foreach(var cpu in CpuTopologyProvider.Load()){
            cpuCores.Add(cpu);
        }
    }

    private void OnConfigurationSelectionChanged(object sender,SelectionChangedEventArgs e){
        if(ConfigurationComboBox is null||isBusy){
            return;
        }

        RefreshBenchmarksFromCache();
        UpdateSelectionSummary();
        UpdateCommandState();
    }

    private void OnProjectCheckBoxClick(object sender,RoutedEventArgs e){
        if(sender is CheckBox checkBox&&checkBox.DataContext is BenchmarkProject project){
            project.IsSelected=checkBox.IsChecked==true;
        }

        RefreshBenchmarksFromCache();
        UpdateSelectionSummary();
        UpdateCommandState();
    }

    private void OnCpuCheckBoxClick(object sender,RoutedEventArgs e){
        if(!isRunning||sender is not CheckBox checkBox||checkBox.DataContext is not CpuCoreInfo cpu){
            UpdateCpuSummary();
            return;
        }

        if(checkBox.IsChecked==true){
            StartWorkerForCpu(cpu);
        }else{
            StopCpuAndRequeue(cpu.LogicalProcessorIndex);
        }

        UpdateCpuSummary();
        UpdateProgress();
    }

    private void OnSelectAllClick(object sender,RoutedEventArgs e){
        foreach(var benchmark in benchmarks){
            benchmark.IsSelected=true;
        }

        selectionAnchorIndex=benchmarks.Count>0?0:null;
        UpdateSelectionSummary();
        UpdateCommandState();
    }

    private void OnClearSelectionClick(object sender,RoutedEventArgs e){
        foreach(var benchmark in benchmarks){
            benchmark.IsSelected=false;
        }

        selectionAnchorIndex=null;
        UpdateSelectionSummary();
        UpdateCommandState();
    }

    private async void OnRunClick(object sender,RoutedEventArgs e){
        var projects = GetSelectedProjects();
        if(projects.Count==0){
            SetStatus("Select at least one project.");
            return;
        }

        if(projectSelectionDirty||benchmarks.Count==0){
            await LoadSelectedProjectsAsync(CancellationToken.None);
        }

        var selected = benchmarks.Where(static benchmark => benchmark.IsSelected).ToArray();
        if(selected.Length==0){
            SetStatus("Select at least one benchmark.");
            return;
        }

        if(cpuCores.All(static cpu => !cpu.IsSelected)){
            SetStatus("Select at least one CPU.");
            return;
        }

        await RunBenchmarksAsync(projects,selected);
    }

    private void OnCancelClick(object sender,RoutedEventArgs e){
        runCancellation?.Cancel();
        KillAllRunningProcesses(requeue:false);
    }

    private void OnOpenReportClick(object sender,RoutedEventArgs e){
        if(string.IsNullOrWhiteSpace(lastReportPath)||!File.Exists(lastReportPath)){
            SetStatus("Report file was not found.");
            return;
        }

        Process.Start(new ProcessStartInfo{
            FileName = lastReportPath,
            UseShellExecute = true,
        });
    }

    private void OnBenchmarkRowTapped(object sender,TappedRoutedEventArgs e){
        if(sender is not FrameworkElement element||element.Tag is not BenchmarkItem benchmark){
            return;
        }

        var index = benchmarks.IndexOf(benchmark);
        if(index<0){
            return;
        }

        var isSelected = !benchmark.IsSelected;
        if(isShiftDown&&selectionAnchorIndex is { } anchorIndex){
            var start = Math.Min(anchorIndex,index);
            var end = Math.Max(anchorIndex,index);
            for(var i=start;i<=end;i++){
                benchmarks[i].IsSelected=isSelected;
            }
        }else{
            benchmark.IsSelected=isSelected;
            selectionAnchorIndex=index;
        }

        if(isControlDown){
            selectionAnchorIndex=index;
        }

        UpdateSelectionSummary();
        UpdateCommandState();
    }

    private void OnPageKeyDown(object sender,KeyRoutedEventArgs e){
        if(e.Key==Windows.System.VirtualKey.Shift){
            isShiftDown=true;
        }else if(e.Key==Windows.System.VirtualKey.Control){
            isControlDown=true;
        }
    }

    private void OnPageKeyUp(object sender,KeyRoutedEventArgs e){
        if(e.Key==Windows.System.VirtualKey.Shift){
            isShiftDown=false;
        }else if(e.Key==Windows.System.VirtualKey.Control){
            isControlDown=false;
        }
    }

    private Task LoadSelectedProjectsAsync(CancellationToken cancellationToken){
        var projects = GetSelectedProjects();
        if(projects.Count==0){
            SetStatus("Select at least one project.");
            return Task.CompletedTask;
        }

        SetBusy(true,allowCpuChanges:false);
        var selectedMethodNames = GetSelectedMethodNames();
        benchmarks.Clear();
        selectionAnchorIndex=null;
        SetReportPath(null);
        LogTextBox.Text=string.Empty;
        SetStatus("Loading canonical method list...");

        try{
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = GetConfiguration();
            var canonicalCacheKey = GetMethodCacheKey(configuration,canonicalMethodProject);
            if(!methodCache.ContainsKey(canonicalCacheKey)){
                SetStatus($"Loading methods from {canonicalMethodProject.DisplayName}...");
                methodCache[canonicalCacheKey]=LoadBenchmarkMethods(canonicalMethodProject.BenchmarkType);
            }

            RefreshBenchmarksFromCache(selectedMethodNames);
            SetStatus($"Loaded {benchmarks.Count} common methods for {projects.Count} selected project(s).");
        }catch(OperationCanceledException){
            SetStatus("Load canceled.");
            AppendLog("Canceled.");
        }catch(Exception ex){
            SetStatus(ex.Message);
            AppendLog(ex.ToString());
        }finally{
            SetBusy(false,allowCpuChanges:false);
            UpdateSelectionSummary();
            UpdateCommandState();
        }

        return Task.CompletedTask;
    }

    private async Task RunBenchmarksAsync(IReadOnlyList<BenchmarkProject> projects,IReadOnlyList<BenchmarkItem> selectedMethods){
        runCancellation=new CancellationTokenSource();
        currentRunRoot=CreateRunRoot(GetConfiguration());
        SetReportPath(null);
        SetBusy(true,allowCpuChanges:true);
        SetStatus($"Preparing {projects.Count} project(s) x {selectedMethods.Count} method(s) = {projects.Count*selectedMethods.Count} task(s)...");
        runningItems.Clear();

        lock(runGate){
            pendingRunItems.Clear();
            allRunItems.Clear();
            runningProcesses.Clear();
            runningRunItems.Clear();
            requeueOnProcessExit.Clear();
            cpuWorkers.Clear();
        }

        try{
            PrepareRunQueue(projects,selectedMethods);
            UpdateProgress();
            StartWorkersForSelectedCpus();
            await WaitForRunCompletionAsync(runCancellation.Token);
            var reportPath = CreateRunReport(currentRunRoot,includeHtml:true);
            SetReportPath(reportPath);
            var failed = allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Failed);
            SetStatus(failed==0
                ? "Benchmarks completed."
                : $"Benchmarks completed with {failed} failed task(s).");
        }catch(OperationCanceledException){
            var reportPath = CreateRunReport(currentRunRoot,includeHtml:true);
            SetReportPath(reportPath);
            SetStatus("Benchmark canceled. Partial report was written.");
            AppendLog("Canceled.");
        }catch(Exception ex){
            var reportPath = CreateRunReport(currentRunRoot,includeHtml:true);
            SetReportPath(reportPath);
            SetStatus(ex.Message);
            AppendLog(ex.ToString());
        }finally{
            KillAllRunningProcesses(requeue:false);
            runCancellation?.Dispose();
            runCancellation=null;
            isRunning=false;
            SetBusy(false,allowCpuChanges:false);
            UpdateCommandState();
            UpdateProgress();
        }
    }

    private void PrepareRunQueue(IReadOnlyList<BenchmarkProject> projects,IReadOnlyList<BenchmarkItem> selectedMethods){
        if(currentRunRoot is null){
            throw new InvalidOperationException("Run root was not created.");
        }

        lock(runGate){
            pendingRunItems.Clear();
            allRunItems.Clear();
            foreach(var method in selectedMethods){
                foreach(var project in projects){
                    if(!method.ProjectMethods.TryGetValue(project.ProjectKey,out var projectMethod)){
                        throw new InvalidOperationException($"Benchmark method '{method.MethodName}' was not found in {project.DisplayName}.");
                    }

                    var item = new BenchmarkRunItem(project,method,projectMethod);
                    pendingRunItems.Enqueue(item);
                    allRunItems.Add(item);
                }
            }
        }
    }

    private void RefreshBenchmarksFromCache(IReadOnlySet<string>? selectedMethodNames = null){
        var projects = GetSelectedProjects();
        selectedMethodNames ??= GetSelectedMethodNames();
        benchmarks.Clear();
        selectionAnchorIndex=null;
        if(projects.Count==0){
            projectSelectionDirty=true;
            SetStatus("Select at least one project.");
            return;
        }

        if(!methodCache.TryGetValue(GetMethodCacheKey(GetConfiguration(),canonicalMethodProject),out var canonicalMethods)){
            projectSelectionDirty=true;
            SetStatus("Loading method list...");
            return;
        }

        foreach(var item in CreateBenchmarkItemsFromCanonicalMethods(projects,canonicalMethods)){
            item.IsSelected=selectedMethodNames.Contains(item.MethodName);
            benchmarks.Add(item);
        }

        projectSelectionDirty=false;
        SetStatus($"Loaded {benchmarks.Count} common methods for {projects.Count} selected project(s).");
    }

    private HashSet<string> GetSelectedMethodNames(){
        return benchmarks
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
        IReadOnlyList<BenchmarkProject> projects,
        IReadOnlyList<BenchmarkProjectMethod> canonicalMethods){
        if(projects.Count==0){
            return [];
        }

        return canonicalMethods
            .GroupBy(static method => method.MethodName,StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static method => method.MethodName,StringComparer.OrdinalIgnoreCase)
            .Select(method => {
                var methodsByProject = projects.ToDictionary(
                    static project => project.ProjectKey,
                    project => new BenchmarkProjectMethod(
                        project.BenchmarkType.Name,
                        project.TypeFullName,
                        method.MethodName,
                        method.MethodName,
                        method.Categories),
                    StringComparer.Ordinal);
                return new BenchmarkItem(method.MethodName,method.MethodName,method.Categories,methodsByProject);
            })
            .ToArray();
    }

    private void StartWorkersForSelectedCpus(){
        isRunning=true;
        foreach(var cpu in cpuCores.Where(static cpu => cpu.IsSelected)){
            StartWorkerForCpu(cpu);
        }
        UpdateCpuSummary();
    }

    private void StartWorkerForCpu(CpuCoreInfo cpu){
        if(runCancellation is null){
            return;
        }

        lock(runGate){
            if(cpuWorkers.TryGetValue(cpu.LogicalProcessorIndex,out var existing)&&!existing.IsCompleted){
                return;
            }

            cpuWorkers[cpu.LogicalProcessorIndex]=Task.Run(() => CpuWorkerLoopAsync(cpu,runCancellation.Token));
        }
    }

    private async Task CpuWorkerLoopAsync(CpuCoreInfo cpu,CancellationToken cancellationToken){
        Process? worker = null;
        try{
            worker=StartBenchmarkWorker(cpu);
            lock(runGate){
                runningProcesses[cpu.LogicalProcessorIndex]=worker;
            }

            while(!cancellationToken.IsCancellationRequested&&cpu.IsSelected){
                BenchmarkRunItem? item;
                lock(runGate){
                    item=pendingRunItems.Count>0 ? pendingRunItems.Dequeue() : null;
                }

                if(item is null){
                    return;
                }

                try{
                    await RunSingleBenchmarkAsync(worker,item,cpu,cancellationToken);
                }catch(OperationCanceledException){
                    throw;
                }catch(Exception ex){
                    AppendLog(ex.ToString());
                    await RunOnUiAsync(() => {
                        runningItems.Remove(item);
                        item.ProcessId=0;
                        item.FinishedAt=DateTimeOffset.Now;
                        item.Status=BenchmarkRunStatus.Failed;
                    });
                    UpdateProgress();
                    if(worker is null||worker.HasExited){
                        return;
                    }
                }
            }

            await StopWorkerGracefullyAsync(worker);
        }finally{
            lock(runGate){
                cpuWorkers.Remove(cpu.LogicalProcessorIndex);
                runningProcesses.Remove(cpu.LogicalProcessorIndex);
                runningRunItems.Remove(cpu.LogicalProcessorIndex);
                requeueOnProcessExit.Remove(cpu.LogicalProcessorIndex);
            }
            KillProcess(worker);
        }
    }

    private async Task RunSingleBenchmarkAsync(Process worker,BenchmarkRunItem item,CpuCoreInfo cpu,CancellationToken cancellationToken){
        if(currentRunRoot is null){
            throw new InvalidOperationException("Run root was not created.");
        }

        var attempt = item.Attempt+1;
        var artifactFolder = Path.Combine(
            currentRunRoot,
            "artifacts",
            BenchmarkText.SanitizePathPart(item.Project.AssemblyName),
            BenchmarkText.SanitizePathPart(item.MethodName),
            $"cpu{cpu.LogicalProcessorIndex}-attempt{attempt}");
        Directory.CreateDirectory(artifactFolder);

        await RunOnUiAsync(() => {
            item.Attempt=attempt;
            item.ArtifactFolder=artifactFolder;
            item.AssignedCpu=cpu;
            item.ProcessId=worker.Id;
            item.StartedAt=DateTimeOffset.Now;
            item.FinishedAt=null;
            item.Status=BenchmarkRunStatus.Running;
            runningItems.Add(item);
        });
        UpdateProgress();

        var taskId = Guid.NewGuid().ToString("N");
        var message = new WorkerTaskMessage(
            "run",
            taskId,
            item.Project.AssemblyName,
            item.Project.TypeFullName,
            item.MethodName,
            artifactFolder);
        WorkerResultMessage? result = null;
        Exception? workerException = null;
        var requeue = false;
        try{
            lock(runGate){
                runningRunItems[cpu.LogicalProcessorIndex]=item;
            }
            result = await SendWorkerTaskAsync(worker,message,cancellationToken);
        }catch(OperationCanceledException){
            await RunOnUiAsync(() => {
                runningItems.Remove(item);
                item.ProcessId=0;
                item.FinishedAt=DateTimeOffset.Now;
                item.Status=BenchmarkRunStatus.Canceled;
            });
            throw;
        }catch(Exception ex){
            workerException=ex;
        }finally{
            lock(runGate){
                runningRunItems.Remove(cpu.LogicalProcessorIndex);
                requeue=requeueOnProcessExit.Remove(cpu.LogicalProcessorIndex);
            }
        }

        await RunOnUiAsync(() => runningItems.Remove(item));

        if(requeue&&!cancellationToken.IsCancellationRequested){
            await RunOnUiAsync(() => {
                item.Status=BenchmarkRunStatus.Pending;
                item.ProcessId=0;
                item.AssignedCpu=null;
                item.FinishedAt=null;
            });
            lock(runGate){
                pendingRunItems.Enqueue(item);
            }
            UpdateProgress();
            return;
        }

        if(workerException is not null){
            throw workerException;
        }

        if(result is null){
            throw new InvalidOperationException("Benchmark worker did not return a result.");
        }

        item.ExitCode=result.ExitCode;
        await RunOnUiAsync(() => {
            item.ExitCode=result.ExitCode;
            item.Mean=result.Mean;
            item.Error=result.Error;
            item.StdDev=result.StdDev;
            item.FinishedAt=DateTimeOffset.Now;
            item.ProcessId=0;
            item.Status=result.ExitCode==0 ? BenchmarkRunStatus.Completed : BenchmarkRunStatus.Failed;
        });
        if(!string.IsNullOrWhiteSpace(result.Log)){
            AppendLog(result.Log);
        }
        UpdateProgress();
    }

    private Process StartBenchmarkWorker(CpuCoreInfo cpu){
        if(currentRunRoot is null){
            throw new InvalidOperationException("Run root was not created.");
        }

        var workerDirectory = AppContext.BaseDirectory;
        var workerExe = Path.Combine(workerDirectory,OperatingSystem.IsWindows()?"Benchmark.Runner.Uno.exe":"Benchmark.Runner.Uno");
        var workerDll = Path.Combine(workerDirectory,"Benchmark.Runner.Uno.dll");
        var startInfo = new ProcessStartInfo{
            WorkingDirectory = Directory.Exists(workerDirectory)?workerDirectory:repositoryRoot,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        if(File.Exists(workerExe)){
            startInfo.FileName=workerExe;
        }else if(File.Exists(workerDll)){
            startInfo.FileName="dotnet";
            startInfo.ArgumentList.Add(workerDll);
        }else{
            throw new FileNotFoundException($"Benchmark worker mode host was not found. Build Benchmark.Runner.Uno first: {workerDll}",workerDll);
        }

        startInfo.ArgumentList.Add("--worker");
        startInfo.ArgumentList.Add("--logical-cpu");
        startInfo.ArgumentList.Add(cpu.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--run-root");
        startInfo.ArgumentList.Add(currentRunRoot);

        var process = new Process{
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        process.ErrorDataReceived+=(sender,e) => {
            if(!string.IsNullOrWhiteSpace(e.Data)){
                AppendLog(e.Data);
            }
        };
        process.Start();
        process.BeginErrorReadLine();
        AppendLog($"> worker CPU {cpu.LogicalProcessorIndex}: {process.Id}");
        return process;
    }

    private static async Task StopWorkerGracefullyAsync(Process worker){
        try{
            if(!worker.HasExited){
                var message = JsonSerializer.Serialize(new WorkerTaskMessage("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty));
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
            var line = await worker.StandardOutput.ReadLineAsync(cancellationToken);
            if(line is null){
                throw new InvalidOperationException($"Benchmark worker exited before returning result. ExitCode: {(worker.HasExited?worker.ExitCode:null)}");
            }

            try{
                var result = JsonSerializer.Deserialize<WorkerResultMessage>(line);
                if(result is not null&&string.Equals(result.TaskId,message.TaskId,StringComparison.Ordinal)){
                    return result;
                }
            }catch(JsonException){
                AppendLog(line);
            }
        }
    }

    private async Task WaitForRunCompletionAsync(CancellationToken cancellationToken){
        while(true){
            cancellationToken.ThrowIfCancellationRequested();
            Task[] workers;
            lock(runGate){
                if(pendingRunItems.Count==0&&runningProcesses.Count==0&&cpuWorkers.Count==0){
                    return;
                }

                workers=cpuWorkers.Values.ToArray();
            }

            if(cpuCores.All(static cpu => !cpu.IsSelected)){
                SetStatus("Paused. Select at least one CPU to resume.");
            }else{
                foreach(var cpu in cpuCores.Where(static cpu => cpu.IsSelected)){
                    StartWorkerForCpu(cpu);
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
        lock(runGate){
            if(runningProcesses.TryGetValue(logicalProcessorIndex,out process)){
                if(runningRunItems.ContainsKey(logicalProcessorIndex)){
                    requeueOnProcessExit.Add(logicalProcessorIndex);
                }
            }
        }

        KillProcess(process);
    }

    private void KillAllRunningProcesses(bool requeue){
        Process[] processes;
        lock(runGate){
            if(requeue){
                foreach(var key in runningRunItems.Keys){
                    requeueOnProcessExit.Add(key);
                }
            }
            processes=runningProcesses.Values.ToArray();
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
            &&(type.Namespace?.Contains(".System.",StringComparison.OrdinalIgnoreCase)??false);
    }

    private static int GetBenchmarkProjectOrder(Type type){
        var sourceOrder = type.Namespace?.Contains(".Tsavorite.",StringComparison.OrdinalIgnoreCase)==true ? 1 : 0;
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
        var source = type.Namespace?.Contains(".Tsavorite.",StringComparison.OrdinalIgnoreCase)==true
            ? "Tsavorite"
            : "System";
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

    private IReadOnlyList<BenchmarkProject> GetSelectedProjects(){
        return benchmarkProjects.Where(static project => project.IsSelected).ToArray();
    }

    private string GetConfiguration(){
        return (ConfigurationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()??"Release";
    }

    private string CreateRunRoot(string configuration){
        var runId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
        var path = Path.Combine(repositoryRoot,"Benchmark",configuration,TargetFramework,"UnoRuns",runId);
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
        AppendLog("> dotnet "+string.Join(" ",arguments.Select(QuoteArgument)));

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

    private string? CreateRunReport(string? runRoot,bool includeHtml){
        if(string.IsNullOrWhiteSpace(runRoot)){
            return null;
        }

        Directory.CreateDirectory(runRoot);
        var rows = allRunItems
            .Where(static item => item.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed)
            .Select(static item => new ReportRow(
                item.ProjectDisplayName,
                item.MethodName,
                item.StatusText,
                item.Mean??string.Empty,
                item.Error??string.Empty,
                item.StdDev??string.Empty,
                item.AssignedCpu?.LogicalProcessorIndex.ToString(CultureInfo.InvariantCulture)??string.Empty,
                item.AssignedCpu?.CoreLabel??string.Empty,
                item.ArtifactFolder??string.Empty,
                item.ExitCode))
            .ToArray();

        var csvPath = Path.Combine(runRoot,"BenchmarkComparison.csv");
        WriteRunCsv(csvPath,rows);
        if(!includeHtml){
            AppendLog($"Comparison CSV: {csvPath}");
            return csvPath;
        }

        var htmlPath = Path.Combine(runRoot,"BenchmarkComparison.html");
        WriteRunHtml(htmlPath,rows);
        AppendLog($"Comparison report: {htmlPath}");
        return htmlPath;
    }

    private static void WriteRunCsv(string path,IReadOnlyList<ReportRow> rows){
        var writer = new StringBuilder();
        writer.AppendLine("Project,Method,Status,Mean,Error,StdDev,LogicalCpu,Core,ArtifactFolder,ExitCode");
        foreach(var row in rows){
            writer.AppendCsv(row.Project).Append(',');
            writer.AppendCsv(row.Method).Append(',');
            writer.AppendCsv(row.Status).Append(',');
            writer.AppendCsv(row.Mean).Append(',');
            writer.AppendCsv(row.Error).Append(',');
            writer.AppendCsv(row.StdDev).Append(',');
            writer.AppendCsv(row.LogicalCpu).Append(',');
            writer.AppendCsv(row.Core).Append(',');
            writer.AppendCsv(row.ArtifactFolder).Append(',');
            writer.AppendCsv(row.ExitCode?.ToString(CultureInfo.InvariantCulture)??string.Empty).AppendLine();
        }

        File.WriteAllText(path,writer.ToString(),Encoding.UTF8);
    }

    private static void WriteRunHtml(string path,IReadOnlyList<ReportRow> rows){
        var writer = new StringBuilder();
        writer.AppendLine("<!DOCTYPE html>");
        writer.AppendLine("<html lang=\"en\">");
        writer.AppendLine("<head>");
        writer.AppendLine("  <meta charset=\"utf-8\">");
        writer.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        writer.AppendLine("  <title>Benchmark Comparison</title>");
        writer.AppendLine("  <style>");
        writer.AppendLine("    body{font-family:Segoe UI,Arial,sans-serif;margin:20px;color:#202020;background:#fffefa;}");
        writer.AppendLine("    h1{font-size:22px;margin:0 0 8px;}");
        writer.AppendLine("    p{margin:0 0 16px;color:#555;}");
        writer.AppendLine("    table{border-collapse:collapse;width:100%;font-size:13px;}");
        writer.AppendLine("    th,td{border:1px solid #d5d1c4;padding:5px 7px;text-align:left;white-space:nowrap;}");
        writer.AppendLine("    th{background:#f0ede4;font-weight:600;}");
        writer.AppendLine("    td.num{text-align:right;}");
        writer.AppendLine("    tbody tr:nth-child(even) td{background:#faf8f0;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        writer.AppendLine("  <h1>Benchmark Comparison</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz",CultureInfo.InvariantCulture))}</p>");
        writer.AppendLine("  <table>");
        writer.AppendLine("    <thead><tr><th>Project</th><th>Method</th><th>Status</th><th>Mean</th><th>Error</th><th>StdDev</th><th>Logical CPU</th><th>Core</th><th>Artifact</th><th>Exit</th></tr></thead>");
        writer.AppendLine("    <tbody>");
        foreach(var row in rows){
            writer.Append("      <tr><td>").Append(EscapeHtml(row.Project)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Method)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Status)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Mean)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.Error)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.StdDev)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.LogicalCpu)).Append("</td><td>");
            writer.Append(EscapeHtml(row.Core)).Append("</td><td>");
            writer.Append(EscapeHtml(row.ArtifactFolder)).Append("</td><td class=\"num\">");
            writer.Append(EscapeHtml(row.ExitCode?.ToString(CultureInfo.InvariantCulture)??string.Empty)).AppendLine("</td></tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");
        File.WriteAllText(path,writer.ToString(),Encoding.UTF8);
    }

    private void SetBusy(bool value,bool allowCpuChanges){
        isBusy=value;
        BusyRing.IsActive=value;
        foreach(var child in ProjectGrid.Children.OfType<Control>()){
            child.IsEnabled=!value;
        }
        ConfigurationComboBox.IsEnabled=!value;
        CpuItemsControl.IsEnabled=!value||allowCpuChanges;
        CancelButton.IsEnabled=value&&runCancellation is not null;
        UpdateCommandState();
    }

    private void SetReportPath(string? path){
        RunOnUi(() => {
            lastReportPath=path;
            var extension = string.IsNullOrWhiteSpace(path)?string.Empty:Path.GetExtension(path);
            ReportLinkButton.Content=extension.Equals(".html",StringComparison.OrdinalIgnoreCase)
                ? "Open HTML report"
                : "Open report";
            ReportLinkButton.Visibility=string.IsNullOrWhiteSpace(path)
                ? Visibility.Collapsed
                : Visibility.Visible;
        });
    }

    private void UpdateCommandState(){
        var hasSelectedBenchmark = benchmarks.Any(static benchmark => benchmark.IsSelected);
        var hasSelectedProject = benchmarkProjects.Any(static project => project.IsSelected);
        RunButton.IsEnabled=!isBusy&&hasSelectedProject&&hasSelectedBenchmark;
    }

    private void UpdateSelectionSummary(){
        var selectedCount = benchmarks.Count(static benchmark => benchmark.IsSelected);
        SelectionSummaryText.Text=$"{selectedCount} / {benchmarks.Count} selected";
    }

    private void UpdateProgress(){
        RunOnUi(() => {
            var total = allRunItems.Count;
            var done = allRunItems.Count(static item => item.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed);
            var totalMethods = allRunItems
                .Select(static item => item.MethodName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var doneMethods = allRunItems
                .GroupBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
                .Count(static group => group.All(static item => item.Status is BenchmarkRunStatus.Completed or BenchmarkRunStatus.Failed));
            var failed = allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Failed);
            var running = allRunItems.Count(static item => item.Status==BenchmarkRunStatus.Running);
            var remaining = Math.Max(0,total-done);
            ProgressBar.Maximum=Math.Max(1,totalMethods);
            ProgressBar.Value=doneMethods;
            ProgressText.Text=total==0
                ? "No run queued"
                : $"{doneMethods} / {totalMethods} methods done ({done} / {total} tasks, {remaining} remaining, {running} running, {failed} failed)";
            UpdateCpuSummary();
        });
    }

    private void UpdateCpuSummary(){
        var selected = cpuCores.Count(static cpu => cpu.IsSelected);
        CpuSummaryText.Text=$"{selected} / {cpuCores.Count} CPU selected";
    }

    private void SetStatus(string text){
        RunOnUi(() => StatusText.Text=text);
    }

    private void AppendLog(string text){
        RunOnUi(() => AppendLogCore(text));
    }

    private void AppendLogCore(string text){
        LogTextBox.Text+=text+Environment.NewLine;
        LogTextBox.SelectionStart=LogTextBox.Text.Length;
    }

    private void RunOnUi(Action action){
        if(DispatcherQueue.HasThreadAccess){
            action();
        }else{
            DispatcherQueue.TryEnqueue(() => action());
        }
    }

    private Task RunOnUiAsync(Action action){
        if(DispatcherQueue.HasThreadAccess){
            action();
            return Task.CompletedTask;
        }

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!DispatcherQueue.TryEnqueue(() => {
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

    private static string EscapeHtml(string value){
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
    }

    private static string GetCell(string[] row,int index){
        return index>=0&&index<row.Length ? row[index] : string.Empty;
    }
}

internal static class CsvStringBuilderExtensions{
    public static StringBuilder AppendCsv(this StringBuilder builder,string value){
        if(value.Contains('"',StringComparison.Ordinal)
            ||value.Contains(',',StringComparison.Ordinal)
            ||value.Contains('\r',StringComparison.Ordinal)
            ||value.Contains('\n',StringComparison.Ordinal)){
            builder.Append('"');
            builder.Append(value.Replace("\"","\"\"",StringComparison.Ordinal));
            builder.Append('"');
            return builder;
        }

        builder.Append(value);
        return builder;
    }
}
