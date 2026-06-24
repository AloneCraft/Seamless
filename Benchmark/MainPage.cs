using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Benchmark;

public sealed class MainPage:ContentPage {
    private readonly ObservableCollection<BenchmarkCaseItem> benchmarks = [];
    private readonly ObservableCollection<BenchmarkResultItem> results = [];
    private readonly BenchmarkWorkerClient workerClient = new();
    private readonly Label statusLabel;
    private readonly Entry scaleEntry;
    private CancellationTokenSource? runCancellation;

    public MainPage() {
        Title="Seamless Benchmark";
        BackgroundColor=Color.FromArgb("#F8FAFC");

        statusLabel = new Label {
            Text="Ready",
            FontSize=13,
            TextColor=Color.FromArgb("#475569"),
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        scaleEntry = new Entry {
            Text="1",
            WidthRequest=72,
            Keyboard=Keyboard.Numeric,
            HorizontalTextAlignment=TextAlignment.Center,
        };

        var refreshButton = new Button { Text="Refresh" };
        refreshButton.Clicked+=async (_,_) => await RefreshAsync();

        var selectAllButton = new Button { Text="All" };
        selectAllButton.Clicked+=(_,_) => SetAll(true);

        var clearButton = new Button { Text="Clear" };
        clearButton.Clicked+=(_,_) => SetAll(false);

        var runButton = new Button { Text="Run" };
        runButton.Clicked+=async (_,_) => await RunSelectedAsync();

        var cancelButton = new Button { Text="Cancel" };
        cancelButton.Clicked+=(_,_) => runCancellation?.Cancel();

        var exportButton = new Button { Text="CSV" };
        exportButton.Clicked+=async (_,_) => await ExportCsvAsync();

        var toolbar = new HorizontalStackLayout {
            Spacing=8,
            Padding=new Thickness(12,10),
            Children = {
                refreshButton,
                selectAllButton,
                clearButton,
                new Label { Text="Scale", VerticalTextAlignment=TextAlignment.Center, TextColor=Color.FromArgb("#334155") },
                scaleEntry,
                runButton,
                cancelButton,
                exportButton,
                statusLabel,
            },
        };

        var benchmarkList = new CollectionView {
            ItemsSource=benchmarks,
            SelectionMode=SelectionMode.None,
            ItemTemplate=new DataTemplate(CreateBenchmarkRow),
        };

        var resultList = new CollectionView {
            ItemsSource=results,
            SelectionMode=SelectionMode.None,
            ItemTemplate=new DataTemplate(CreateResultRow),
        };

        var contentGrid = new Grid {
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(0.44,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.56,GridUnitType.Star) },
            },
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Star },
            },
            ColumnSpacing=1,
            BackgroundColor=Color.FromArgb("#CBD5E1"),
        };
        contentGrid.Add(WrapPanel("Benchmarks",benchmarkList),0,0);
        contentGrid.Add(WrapPanel("Results",resultList),1,0);

        Content = new Grid {
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Children = {
                toolbar,
                contentGrid,
            },
        };
        Grid.SetRow(contentGrid,1);

        _ = RefreshAsync();
    }

    private static View WrapPanel(string title,View content) {
        var grid = new Grid {
            BackgroundColor=Colors.White,
            Padding=new Thickness(12),
            RowDefinitions = {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            Children = {
                new Label { Text=title, FontSize=16, FontAttributes=FontAttributes.Bold, TextColor=Color.FromArgb("#0F172A") },
                content,
            },
        };
        grid.SetRow(content,1);
        return grid;
    }

    private static View CreateBenchmarkRow() {
        var grid = new Grid {
            Padding=new Thickness(4,6),
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=8,
        };
        var checkBox = new CheckBox { VerticalOptions=LayoutOptions.Center };
        checkBox.SetBinding(CheckBox.IsCheckedProperty,nameof(BenchmarkCaseItem.IsSelected));

        var label = new Label {
            FontSize=13,
            TextColor=Color.FromArgb("#1E293B"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalTextAlignment=TextAlignment.Center,
        };
        label.SetBinding(Label.TextProperty,nameof(BenchmarkCaseItem.DisplayName));

        grid.Add(checkBox,0,0);
        grid.Add(label,1,0);
        return grid;
    }

    private static View CreateResultRow() {
        var grid = new Grid {
            Padding=new Thickness(4,6),
            ColumnDefinitions = {
                new ColumnDefinition { Width = new GridLength(2.2,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.4,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.1,GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1.0,GridUnitType.Star) },
            },
            ColumnSpacing=10,
        };
        grid.Add(BoundLabel(nameof(BenchmarkResultItem.Name)),0,0);
        grid.Add(BoundLabel(nameof(BenchmarkResultItem.Status)),1,0);
        grid.Add(BoundLabel(nameof(BenchmarkResultItem.Mean)),2,0);
        grid.Add(BoundLabel(nameof(BenchmarkResultItem.AllocatedBytes)),3,0);
        return grid;
    }

    private static Label BoundLabel(string propertyName) {
        var label = new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#1E293B"),
            LineBreakMode=LineBreakMode.TailTruncation,
            VerticalTextAlignment=TextAlignment.Center,
        };
        label.SetBinding(Label.TextProperty,propertyName);
        return label;
    }

    private async Task RefreshAsync() {
        statusLabel.Text="Loading";
        try {
            var loaded = await Task.Run(BenchmarkCatalog.Load);
            benchmarks.Clear();
            foreach(var item in loaded.Select(static item => new BenchmarkCaseItem(item))) {
                benchmarks.Add(item);
            }
            statusLabel.Text=$"{benchmarks.Count} benchmarks";
        } catch(Exception ex) {
            statusLabel.Text="Load failed";
            await DisplayAlert("Load failed",ex.Message,"OK");
        }
    }

    private void SetAll(bool isSelected) {
        foreach(var item in benchmarks) {
            item.IsSelected=isSelected;
        }
    }

    private async Task RunSelectedAsync() {
        var selected = benchmarks.Where(static item => item.IsSelected).Select(static item => item.Case).ToArray();
        if(selected.Length==0) {
            statusLabel.Text="No selection";
            return;
        }

        runCancellation?.Cancel();
        runCancellation = new CancellationTokenSource();
        var cancellationToken = runCancellation.Token;
        var runRoot = CreateRunRoot();
        results.Clear();
        statusLabel.Text=$"Running 0/{selected.Length}";

        for(var i=0;i<selected.Length;i++) {
            cancellationToken.ThrowIfCancellationRequested();
            var benchmark = selected[i];
            statusLabel.Text=$"Running {i+1}/{selected.Length}: {benchmark.MethodName}";
            try {
                var result = await workerClient.RunAsync(benchmark,runRoot,scaleEntry.Text??"1",cancellationToken);
                results.Add(new BenchmarkResultItem(result));
            } catch(Exception ex) {
                results.Add(BenchmarkResultItem.Failed(benchmark,ex));
            }
        }

        var csvPath = WriteCsv(runRoot);
        statusLabel.Text=$"Done: {csvPath}";
    }

    private async Task ExportCsvAsync() {
        if(results.Count==0) {
            statusLabel.Text="No results";
            return;
        }

        var runRoot = CreateRunRoot();
        var csvPath = WriteCsv(runRoot);
        statusLabel.Text=$"CSV: {csvPath}";
        await DisplayAlert("CSV",csvPath,"OK");
    }

    private string WriteCsv(string runRoot) {
        Directory.CreateDirectory(runRoot);
        var path = Path.Combine(runRoot,"results.csv");
        File.WriteAllText(path,BuildCsv(),Encoding.UTF8);
        return path;
    }

    private string BuildCsv() {
        var builder = new StringBuilder();
        builder.AppendLine("Project,Method,Status,Mean,MeanNanoseconds,ErrorNanoseconds,StdDevNanoseconds,AllocatedBytes,ArtifactFolder");
        foreach(var row in results) {
            AppendCsv(builder,row.Project);
            AppendCsv(builder,row.Method);
            AppendCsv(builder,row.Status);
            AppendCsv(builder,row.Mean);
            AppendCsv(builder,row.MeanNanoseconds);
            AppendCsv(builder,row.ErrorNanoseconds);
            AppendCsv(builder,row.StdDevNanoseconds);
            AppendCsv(builder,row.AllocatedBytesValue);
            AppendCsv(builder,row.ArtifactFolder,last: true);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendCsv(StringBuilder builder,object? value,bool last = false) {
        var text = Convert.ToString(value,CultureInfo.InvariantCulture)??string.Empty;
        builder.Append('"');
        builder.Append(text.Replace("\"","\"\"",StringComparison.Ordinal));
        builder.Append('"');
        if(!last) {
            builder.Append(',');
        }
    }

    private static string CreateRunRoot() {
        var root = FindSolutionRoot() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var path = Path.Combine(root,"Work","Benchmark",DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string? FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }
}

public sealed class BenchmarkCaseItem:NotifyObject {
    private bool isSelected;

    public BenchmarkCaseItem(BenchmarkCase benchmarkCase) {
        Case=benchmarkCase;
    }

    public BenchmarkCase Case { get; }
    public string DisplayName => Case.DisplayName;

    public bool IsSelected {
        get => isSelected;
        set => SetProperty(ref isSelected,value);
    }
}

public sealed class BenchmarkResultItem {
    public BenchmarkResultItem(BenchmarkRunResult result) {
        Project=result.Project;
        Method=result.Method;
        Name=$"{result.Project} / {result.Method}";
        Status=result.Status;
        Mean=result.Mean;
        AllocatedBytes=result.AllocatedBytes?.ToString("N0",CultureInfo.InvariantCulture)??string.Empty;
        AllocatedBytesValue=result.AllocatedBytes;
        MeanNanoseconds=result.MeanNanoseconds;
        ErrorNanoseconds=result.ErrorNanoseconds;
        StdDevNanoseconds=result.StdDevNanoseconds;
        ArtifactFolder=result.ArtifactFolder;
    }

    private BenchmarkResultItem(BenchmarkCase benchmark,Exception exception) {
        Project=benchmark.Project;
        Method=benchmark.MethodName;
        Name=$"{benchmark.Project} / {benchmark.MethodName}";
        Status="Failed";
        Mean=exception.GetType().Name;
        AllocatedBytes=string.Empty;
        ArtifactFolder=string.Empty;
    }

    public string Project { get; }
    public string Method { get; }
    public string Name { get; }
    public string Status { get; }
    public string Mean { get; }
    public string AllocatedBytes { get; }
    public long? AllocatedBytesValue { get; }
    public double? MeanNanoseconds { get; }
    public double? ErrorNanoseconds { get; }
    public double? StdDevNanoseconds { get; }
    public string ArtifactFolder { get; }

    public static BenchmarkResultItem Failed(BenchmarkCase benchmark,Exception exception) => new(benchmark,exception);
}

public abstract class NotifyObject:INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field,T value,[CallerMemberName] string? propertyName = null) {
        if(EqualityComparer<T>.Default.Equals(field,value)) {
            return false;
        }

        field=value;
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
