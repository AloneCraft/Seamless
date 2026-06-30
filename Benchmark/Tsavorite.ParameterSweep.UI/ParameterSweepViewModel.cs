using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tsavorite.ParameterSweep;

namespace Tsavorite.ParameterSweep.UI;

public sealed class ParameterSweepViewModel:INotifyPropertyChanged {
    private readonly ParameterSweepRunner runner = new();
    private readonly ParameterSweepRunStore store;
    private ParameterSweepRunSummary? selectedBaselineRun;
    private bool isRunning;
    private string statusText = "Ready";
    private string maxSuccessfulCountText = "65536";
    private string maxSweepStepsText = "8";

    public ParameterSweepViewModel() {
        var root = Path.Combine(
            FindSolutionRoot()??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Work",
            "Tsavorite.ParameterSweep");
        this.store=new(root);
        this.RefreshRuns();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ParameterSweepComparisonViewRow> Rows { get; } = [];
    public ObservableCollection<ParameterSweepRunSummary> RunHistory { get; } = [];

    public ParameterSweepRunSummary? SelectedBaselineRun {
        get => this.selectedBaselineRun;
        set {
            if(this.selectedBaselineRun==value) {
                return;
            }
            this.selectedBaselineRun=value;
            this.OnPropertyChanged();
            this.ApplyBaselineComparison();
        }
    }

    public bool IsRunning {
        get => this.isRunning;
        private set {
            if(this.isRunning==value) {
                return;
            }
            this.isRunning=value;
            this.OnPropertyChanged();
            this.OnPropertyChanged(nameof(this.CanRun));
        }
    }

    public bool CanRun => !this.IsRunning;

    public string StatusText {
        get => this.statusText;
        private set {
            if(this.statusText==value) {
                return;
            }
            this.statusText=value;
            this.OnPropertyChanged();
        }
    }

    public string MaxSuccessfulCountText {
        get => this.maxSuccessfulCountText;
        set {
            if(this.maxSuccessfulCountText==value) {
                return;
            }
            this.maxSuccessfulCountText=value;
            this.OnPropertyChanged();
        }
    }

    public string MaxSweepStepsText {
        get => this.maxSweepStepsText;
        set {
            if(this.maxSweepStepsText==value) {
                return;
            }
            this.maxSweepStepsText=value;
            this.OnPropertyChanged();
        }
    }

    public async Task RunFullAsync() => await this.RunAsync(smoke:false);

    public async Task RunSmokeAsync() => await this.RunAsync(smoke:true);

    public void RefreshRuns() {
        var selected = this.SelectedBaselineRun?.RunId;
        this.RunHistory.Clear();
        foreach(var item in this.store.ListRuns()) {
            this.RunHistory.Add(item);
        }
        this.SelectedBaselineRun=this.RunHistory.FirstOrDefault(item => item.RunId==selected)??this.RunHistory.FirstOrDefault();
    }

    private async Task RunAsync(bool smoke) {
        if(this.IsRunning) {
            return;
        }
        this.IsRunning=true;
        this.Rows.Clear();
        try {
            var options = this.CreateOptions(smoke);
            var cases = smoke
                ? ParameterSweepPlan.CreateSmokeCases(options)
                : ParameterSweepPlan.CreateDefaultCases(options);
            var progress = new Progress<ParameterSweepResult>(result => {
                this.Rows.Add(new(result,null));
                this.StatusText="Running "+this.Rows.Count+"/"+cases.Count+" "+result.CaseName;
            });
            var results = await this.runner.RunAsync(cases,options,progress);
            var summary = this.store.Save(results);
            this.RefreshRuns();
            this.SelectedBaselineRun=this.RunHistory.FirstOrDefault(item => item.RunId==summary.RunId);
            this.StatusText="Completed "+results.Count+" cases. Saved: "+summary.RunRoot;
        }
        catch(Exception ex) {
            this.StatusText="Failed: "+ex.Message;
        }
        finally {
            this.IsRunning=false;
        }
    }

    private ParameterSweepRunOptions CreateOptions(bool smoke) {
        var maxSuccessfulCount = int.TryParse(this.MaxSuccessfulCountText,out var parsedMax)&&parsedMax>0
            ? parsedMax
            : 65536;
        var maxSweepSteps = int.TryParse(this.MaxSweepStepsText,out var parsedSteps)&&parsedSteps>0
            ? parsedSteps
            : 8;
        if(smoke) {
            maxSuccessfulCount=Math.Min(maxSuccessfulCount,256);
            maxSweepSteps=Math.Min(maxSweepSteps,2);
        }
        return new() {
            MaxSuccessfulCount=maxSuccessfulCount,
            MaxSweepSteps=maxSweepSteps,
            LookupProbeCount=Math.Min(4096,maxSuccessfulCount),
            WorkDirectory=Path.Combine(this.store.RootDirectory,"Temp"),
        };
    }

    private void ApplyBaselineComparison() {
        var baselineRun = this.SelectedBaselineRun;
        if(baselineRun is null||this.Rows.Count==0) {
            return;
        }
        IReadOnlyList<ParameterSweepResult> baseline;
        try {
            baseline=this.store.Load(baselineRun);
        }
        catch {
            return;
        }
        var compared = ParameterSweepComparer.Compare(this.Rows.Select(static row => row.Current).ToArray(),baseline);
        this.Rows.Clear();
        foreach(var row in compared) {
            this.Rows.Add(new(row.Current,row));
        }
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        this.PropertyChanged?.Invoke(this,new(propertyName));
}

public sealed class ParameterSweepComparisonViewRow(ParameterSweepResult current,ParameterSweepComparisonRow? comparison) {
    public ParameterSweepResult Current { get; } = current;
    public string CaseName => this.Current.CaseName;
    public string Collection => this.Current.Collection.ToString();
    public string Generic => this.Current.GenericName;
    public string Backend => this.Current.Backend.ToString();
    public string Sweep => this.Current.Sweep.ToString();
    public string IndexSize => ParameterSweepFormatting.FormatSize(this.Current.IndexSize);
    public string PageSize => ParameterSweepFormatting.FormatSize(this.Current.PageSize);
    public string SegmentSize => ParameterSweepFormatting.FormatSize(this.Current.SegmentSize);
    public string MemorySize => ParameterSweepFormatting.FormatSize(this.Current.MemorySize);
    public string MutableFraction => this.Current.MutableFraction.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
    public string MaxSuccessfulCount => this.Current.MaxSuccessfulCount.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
    public string FailureKind => this.Current.FailureKind.ToString();
    public string AddElapsed => this.Current.AddElapsedMilliseconds.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" ms";
    public string LookupMean => this.Current.LookupMeanNanoseconds?.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+" ns"??"-";
    public string MaxCountDelta => comparison?.MaxCountDelta?.ToString("+0;-0;0",System.Globalization.CultureInfo.InvariantCulture)??"-";
    public string AddElapsedRatio => comparison?.AddElapsedRatio?.ToString("0.###x",System.Globalization.CultureInfo.InvariantCulture)??"-";
    public string LookupRatio => comparison?.LookupRatio?.ToString("0.###x",System.Globalization.CultureInfo.InvariantCulture)??"-";
    public string FailureChanged => comparison is { FailureKindChanged: true } ? "Changed" : "";
}
