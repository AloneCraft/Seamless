using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Microsoft.UI.Xaml.Input;

namespace Labo.Benchmark.Runner.Uno;

public sealed partial class MainPage:Page{
    private readonly string repositoryRoot = FindRepositoryRoot();
    private readonly ObservableCollection<BenchmarkItem> benchmarks = [];
    private readonly IReadOnlyList<BenchmarkProject> benchmarkProjects = [
        new("System LINQ","Labo.Benchmark.System.Linq.Enumerable.Linq\\Labo.Benchmark.System.Linq.Enumerable.Linq.csproj",true),
        new("System FLinq","Labo.Benchmark.System.Linq.Enumerable.Flinq\\Labo.Benchmark.System.Linq.Enumerable.Flinq.csproj",false),
        new("System ZLinq","Labo.Benchmark.System.Linq.Enumerable.ZLinq\\Labo.Benchmark.System.Linq.Enumerable.ZLinq.csproj",false),
        new("Tsavorite IEnumerable LINQ","Labo.Benchmark.Tsavorite.Linq.Enumerable.IEnumerableLinq\\Labo.Benchmark.Tsavorite.Linq.Enumerable.IEnumerableLinq.csproj",false),
        new("Tsavorite IEnumerable FLinq","Labo.Benchmark.Tsavorite.Linq.Enumerable.IEnumerableFlinq\\Labo.Benchmark.Tsavorite.Linq.Enumerable.IEnumerableFlinq.csproj",false),
        new("Tsavorite IEnumerable ZLinq","Labo.Benchmark.Tsavorite.Linq.Enumerable.IEnumerableZLinq\\Labo.Benchmark.Tsavorite.Linq.Enumerable.IEnumerableZLinq.csproj",false),
        new("Tsavorite IHashSet LINQ","Labo.Benchmark.Tsavorite.Linq.Enumerable.IHashSetLinq\\Labo.Benchmark.Tsavorite.Linq.Enumerable.IHashSetLinq.csproj",false),
        new("Tsavorite IHashSet FLinq","Labo.Benchmark.Tsavorite.Linq.Enumerable.IHashSetFlinq\\Labo.Benchmark.Tsavorite.Linq.Enumerable.IHashSetFlinq.csproj",false),
    ];

    private CancellationTokenSource? runCancellation;
    private string? lastReportPath;
    private int? selectionAnchorIndex;
    private bool isShiftDown;
    private bool isControlDown;
    private bool isBusy;

    public MainPage(){
        this.InitializeComponent();
    }

    private void OnPageLoaded(object sender,RoutedEventArgs e){
        InitializeProjectGrid();
        BenchmarkItemsControl.ItemsSource=benchmarks;

        UpdateSelectionSummary();
        UpdateCommandState();
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

    private async void OnLoadClick(object sender,RoutedEventArgs e){
        await LoadSelectedProjectsAsync();
    }

    private void OnProjectCheckBoxClick(object sender,RoutedEventArgs e){
        if(sender is CheckBox checkBox&&checkBox.DataContext is BenchmarkProject project){
            project.IsSelected=checkBox.IsChecked==true;
        }

        UpdateCommandState();
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

        var selected = benchmarks.Where(static benchmark => benchmark.IsSelected).ToArray();
        if(selected.Length==0){
            SetStatus("Select at least one benchmark.");
            return;
        }

        var filter = selected.Length==benchmarks.Count
            ? "*"
            : string.Join("|",selected.Select(static benchmark => $"*{benchmark.MethodName}*"));

        var benchmarkArgs = new List<string>{"--filter",filter};
        if(SingleFileCheckBox.IsChecked==true){
            benchmarkArgs.Add("--single-file");
        }

        await RunBenchmarksAsync(projects,benchmarkArgs);
    }

    private void OnCancelClick(object sender,RoutedEventArgs e){
        runCancellation?.Cancel();
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

    private async Task LoadSelectedProjectsAsync(){
        var projects = GetSelectedProjects();
        if(projects.Count==0){
            SetStatus("Select at least one project.");
            return;
        }

        var project = projects[0];
        SetBusy(true);
        benchmarks.Clear();
        selectionAnchorIndex=null;
        SetReportPath(null);
        LogTextBox.Text=string.Empty;
        SetStatus($"Loading methods from {project.DisplayName}...");

        try{
            var args = BuildDotnetRunArgs(project,GetConfiguration(),["--list-json"]);
            var result = await RunProcessAsync(args,AppendLog,CancellationToken.None);
            if(result.ExitCode!=0){
                SetStatus($"List failed: exit code {result.ExitCode}");
                return;
            }

            foreach(var item in ParseBenchmarkItems(result.StandardOutput)){
                benchmarks.Add(item);
            }

            SetStatus($"Loaded {benchmarks.Count} methods for {projects.Count} selected projects.");
        }catch(Exception ex){
            SetStatus(ex.Message);
            AppendLog(ex.ToString());
        }finally{
            SetBusy(false);
            UpdateSelectionSummary();
            UpdateCommandState();
        }
    }

    private async Task RunBenchmarksAsync(IReadOnlyList<BenchmarkProject> projects,IReadOnlyList<string> benchmarkArgs){
        runCancellation=new CancellationTokenSource();
        SetBusy(true);
        SetStatus($"Running {projects.Count} projects...");

        try{
            var exitCode = 0;
            foreach(var project in projects){
                SetStatus($"Running {project.DisplayName}...");
                var args = BuildDotnetRunArgs(project,GetConfiguration(),benchmarkArgs);
                var result = await RunProcessAsync(args,AppendLog,runCancellation.Token);
                if(result.ExitCode!=0){
                    exitCode=result.ExitCode;
                    break;
                }
            }

            SetReportPath(CreateComparisonReport(GetConfiguration()));
            SetStatus(exitCode==0
                ? "Benchmarks completed."
                : $"Benchmark failed: exit code {exitCode}");
        }catch(OperationCanceledException){
            SetStatus("Benchmark canceled.");
            AppendLog("Canceled.");
        }catch(Exception ex){
            SetStatus(ex.Message);
            AppendLog(ex.ToString());
        }finally{
            runCancellation?.Dispose();
            runCancellation=null;
            SetBusy(false);
            UpdateCommandState();
        }
    }

    private IReadOnlyList<BenchmarkProject> GetSelectedProjects(){
        return benchmarkProjects.Where(static project => project.IsSelected).ToArray();
    }

    private string GetConfiguration(){
        return (ConfigurationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()??"Release";
    }

    private string GetProjectPath(BenchmarkProject project){
        return Path.Combine(repositoryRoot,project.RelativeProjectPath);
    }

    private string GetProjectDirectory(BenchmarkProject project){
        return Path.GetDirectoryName(GetProjectPath(project))??repositoryRoot;
    }

    private IReadOnlyList<string> BuildDotnetRunArgs(BenchmarkProject project,string configuration,IReadOnlyList<string> benchmarkArgs){
        var args = new List<string>{
            "run",
            "--project",
            GetProjectPath(project),
            "-c",
            configuration,
            "--",
        };
        args.AddRange(benchmarkArgs);
        return args;
    }

    private async Task<ProcessResult> RunProcessAsync(IReadOnlyList<string> arguments,Action<string> output,CancellationToken cancellationToken){
        AppendLog("> dotnet "+string.Join(" ",arguments.Select(QuoteArgument)));

        var standardOutput = new StringBuilder();
        using var process = new Process{
            StartInfo = new ProcessStartInfo{
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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
            if(e.Data is not null){
                output(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try{
            await process.WaitForExitAsync(cancellationToken);
        }catch(OperationCanceledException){
            if(!process.HasExited){
                process.Kill(entireProcessTree:true);
            }
            throw;
        }

        return new ProcessResult(process.ExitCode,standardOutput.ToString());
    }

    private static IReadOnlyList<BenchmarkItem> ParseBenchmarkItems(string output){
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
            .Select(static item => new BenchmarkItem(
                item.TypeName,
                item.TypeFullName,
                item.MethodName,
                item.DisplayName,
                item.Filter,
                item.Categories??[]))
            .ToArray();
    }

    private void SetBusy(bool value){
        isBusy=value;
        BusyRing.IsActive=value;
        LoadButton.IsEnabled=!value;
        foreach(var child in ProjectGrid.Children.OfType<Control>()){
            child.IsEnabled=!value;
        }
        ConfigurationComboBox.IsEnabled=!value;
        SingleFileCheckBox.IsEnabled=!value;
        CancelButton.IsEnabled=value&&runCancellation is not null;
        UpdateCommandState();
    }

    private void SetReportPath(string? path){
        lastReportPath=path;
        ReportLinkButton.Visibility=string.IsNullOrWhiteSpace(path)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private string? CreateComparisonReport(string configuration){
        var reports = LoadReportData(configuration);
        if(reports.Count==0){
            return null;
        }

        var methodNames = reports
            .SelectMany(static report => report.Means.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static method => method,StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var outputDirectory = Path.Combine(repositoryRoot,"Benchmark",configuration,"net10.0");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory,"BenchmarkComparison.html");

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
        writer.AppendLine("    th,td{border:1px solid #d5d1c4;padding:5px 7px;text-align:right;white-space:nowrap;}");
        writer.AppendLine("    th{background:#f0ede4;font-weight:600;}");
        writer.AppendLine("    th:first-child,td:first-child{text-align:left;position:sticky;left:0;background:#fffefa;}");
        writer.AppendLine("    tbody tr:nth-child(even) td{background:#faf8f0;}");
        writer.AppendLine("    tbody tr:nth-child(even) td:first-child{background:#faf8f0;}");
        writer.AppendLine("  </style>");
        writer.AppendLine("</head>");
        writer.AppendLine("<body>");
        var generatedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        writer.AppendLine("  <h1>Benchmark Comparison</h1>");
        writer.AppendLine($"  <p>Generated: {EscapeHtml(generatedAt)}</p>");
        writer.AppendLine("  <table>");
        writer.Append("    <thead><tr><th>Method</th>");
        foreach(var report in reports){
            writer.Append("<th>");
            writer.Append(EscapeHtml(report.Label));
            writer.Append("</th>");
        }
        writer.AppendLine("</tr></thead>");
        writer.AppendLine("    <tbody>");
        foreach(var methodName in methodNames){
            writer.Append("      <tr><td>");
            writer.Append(EscapeHtml(methodName));
            writer.Append("</td>");
            foreach(var report in reports){
                writer.Append("<td>");
                if(report.Means.TryGetValue(methodName,out var mean)){
                    writer.Append(EscapeHtml(mean));
                }
                writer.Append("</td>");
            }
            writer.AppendLine("</tr>");
        }
        writer.AppendLine("    </tbody>");
        writer.AppendLine("  </table>");
        writer.AppendLine("</body>");
        writer.AppendLine("</html>");

        File.WriteAllText(outputPath,writer.ToString(),Encoding.UTF8);
        AppendLog($"Comparison report: {outputPath}");
        return outputPath;
    }

    private IReadOnlyList<ReportData> LoadReportData(string configuration){
        var reports = new List<ReportData>();
        foreach(var project in benchmarkProjects){
            var root = Path.Combine(GetProjectDirectory(project),"Benchmark",configuration,"net10.0");
            if(!Directory.Exists(root)){
                continue;
            }

            var reportFiles = Directory.GetFiles(root,"*-report.csv",System.IO.SearchOption.AllDirectories)
                .Select(static path => new FileInfo(path))
                .GroupBy(static file => GetReportLabel(file.FullName),StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.OrderByDescending(static file => file.LastWriteTimeUtc).First())
                .OrderBy(static file => GetReportLabel(file.FullName),StringComparer.OrdinalIgnoreCase);

            foreach(var reportFile in reportFiles){
                var means = ReadMeanValues(reportFile.FullName);
                if(means.Count>0){
                    reports.Add(new ReportData(GetReportLabel(reportFile.FullName),means));
                }
            }
        }

        return reports;
    }

    private static Dictionary<string,string> ReadMeanValues(string path){
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        using var parser = new TextFieldParser(path,Encoding.UTF8){
            TextFieldType=FieldType.Delimited,
            HasFieldsEnclosedInQuotes=true,
            TrimWhiteSpace=false,
        };
        parser.SetDelimiters(",");

        if(parser.EndOfData){
            return result;
        }

        var header = parser.ReadFields()??[];
        var methodIndex = Array.IndexOf(header,"Method");
        var meanIndex = Array.IndexOf(header,"Mean");
        if(methodIndex<0||meanIndex<0){
            return result;
        }

        while(!parser.EndOfData){
            var row = parser.ReadFields()??[];
            if(row.Length<=methodIndex||row.Length<=meanIndex){
                continue;
            }

            result[row[methodIndex]]=row[meanIndex];
        }

        return result;
    }

    private static string GetReportLabel(string path){
        var name = Path.GetFileNameWithoutExtension(path);
        if(name.EndsWith("-report",StringComparison.OrdinalIgnoreCase)){
            name=name[..^7];
        }

        var lastDot = name.LastIndexOf('.');
        return lastDot>=0&&lastDot+1<name.Length
            ? name[(lastDot+1)..]
            : name;
    }

    private static string EscapeHtml(string value){
        return value
            .Replace("&","&amp;",StringComparison.Ordinal)
            .Replace("<","&lt;",StringComparison.Ordinal)
            .Replace(">","&gt;",StringComparison.Ordinal)
            .Replace("\"","&quot;",StringComparison.Ordinal);
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

    private void SetStatus(string text){
        if(DispatcherQueue.HasThreadAccess){
            StatusText.Text=text;
        }else{
            DispatcherQueue.TryEnqueue(() => StatusText.Text=text);
        }
    }

    private void AppendLog(string text){
        if(DispatcherQueue.HasThreadAccess){
            AppendLogCore(text);
        }else{
            DispatcherQueue.TryEnqueue(() => AppendLogCore(text));
        }
    }

    private void AppendLogCore(string text){
        LogTextBox.Text+=text+Environment.NewLine;
        LogTextBox.SelectionStart=LogTextBox.Text.Length;
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
        return argument.Contains(' ',StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
    }

    private sealed record BenchmarkListItem(
        string TypeName,
        string TypeFullName,
        string MethodName,
        string DisplayName,
        string Filter,
        string[]? Categories);

    private sealed record ProcessResult(int ExitCode,string StandardOutput);

    private sealed record ReportData(string Label,Dictionary<string,string> Means);
}

public sealed class BenchmarkProject:INotifyPropertyChanged{
    private bool isSelected;

    public BenchmarkProject(string displayName,string relativeProjectPath,bool isSelected){
        DisplayName=displayName;
        RelativeProjectPath=relativeProjectPath;
        this.isSelected=isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }
    public string RelativeProjectPath { get; }

    public bool IsSelected{
        get => isSelected;
        set{
            if(isSelected==value){
                return;
            }

            isSelected=value;
            PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class BenchmarkItem:INotifyPropertyChanged{
    private bool isSelected;

    public BenchmarkItem(string typeName,string typeFullName,string methodName,string displayName,string filter,string[] categories){
        TypeName=typeName;
        TypeFullName=typeFullName;
        MethodName=methodName;
        DisplayName=displayName;
        Filter=filter;
        Categories=categories;
        CategoryText=categories.Length==0?"-":string.Join(", ",categories);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TypeName { get; }
    public string TypeFullName { get; }
    public string MethodName { get; }
    public string DisplayName { get; }
    public string Filter { get; }
    public string[] Categories { get; }
    public string CategoryText { get; }

    public bool IsSelected{
        get => isSelected;
        set{
            if(isSelected==value){
                return;
            }

            isSelected=value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CheckGlyph));
        }
    }

    public string CheckGlyph => isSelected?"X":string.Empty;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null){
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName));
    }
}
