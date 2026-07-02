using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Seamless.UI.Storage;
using Tsavorite.ParameterSweep;

namespace Tsavorite.ParameterSweep.UI;

public sealed class ParameterSweepViewModel:INotifyPropertyChanged {
    private readonly ParameterSweepRunner runner = new();
    private readonly UiRunStore store;
    private UiRunRecord? selectedBaselineRun;
    private bool isRunning;
    private string statusText = "Ready";
    private string maxSuccessfulCountText = "65536";
    private string maxSweepStepsText = "8";

    public ParameterSweepViewModel() {
        this.store=new(UiRunDatabasePathResolver.Resolve(FindSolutionRoot()));
        this.store.EnsureSchema();
        this.RefreshRuns();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ParameterSweepComparisonViewRow> Rows { get; } = [];
    public ObservableCollection<UiRunRecord> RunHistory { get; } = [];

    public UiRunRecord? SelectedBaselineRun {
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
        foreach(var item in this.store.ListRuns(UiRunAppKind.ParameterSweep)) {
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
        UiRunRecord? run = null;
        try {
            var options = this.CreateOptions(smoke);
            var cases = smoke
                ? ParameterSweepPlan.CreateSmokeCases(options)
                : ParameterSweepPlan.CreateDefaultCases(options);
            run=this.store.CreateRun(new(
                UiRunAppKind.ParameterSweep,
                (smoke?"Smoke ":"Full ")+DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm:ss",CultureInfo.InvariantCulture),
                smoke?"Smoke":"Full",
                "ParameterSweep",
                DateTimeOffset.UtcNow));
            this.store.SetRunStatus(run.RunId,UiRunStatus.Running,"Parameter sweep started.");
            var progress = new Progress<ParameterSweepResult>(result => {
                this.SaveResult(run.RunId,result);
                this.Rows.Add(new(result,null));
                this.StatusText="Running "+this.Rows.Count+"/"+cases.Count+" "+result.CaseName;
            });
            var results = await this.runner.RunAsync(cases,options,progress);
            foreach(var result in results) {
                this.SaveResult(run.RunId,result);
            }
            this.store.SetRunStatus(run.RunId,UiRunStatus.Completed,"Completed "+results.Count.ToString(CultureInfo.InvariantCulture)+" cases.");
            this.RefreshRuns();
            this.SelectedBaselineRun=this.RunHistory.FirstOrDefault(item => item.RunId==run.RunId);
            this.StatusText="Completed "+results.Count+" cases. Saved to UI DB run "+run.RunId.ToString(CultureInfo.InvariantCulture)+".";
        }
        catch(Exception ex) {
            if(run is not null) {
                this.store.SetRunStatus(run.RunId,UiRunStatus.Failed,ex.Message);
            }
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
            WorkDirectory=Path.Combine(Path.GetDirectoryName(this.store.DatabasePath)??Path.GetTempPath(),"Temp"),
        };
    }

    private void ApplyBaselineComparison() {
        var baselineRun = this.SelectedBaselineRun;
        if(baselineRun is null||this.Rows.Count==0) {
            return;
        }
        IReadOnlyList<ParameterSweepResult> baseline;
        try {
            baseline=this.LoadResults(baselineRun);
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

    private void SaveResult(long runId,ParameterSweepResult result) {
        var testCase = this.store.UpsertCase(
            runId,
            result.CaseName,
            result.CaseName,
            result.Collection.ToString(),
            result.Sweep.ToString(),
            result.GenericName,
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
                ["Collection"]=result.Collection.ToString(),
                ["Generic"]=result.GenericName,
                ["Backend"]=result.Backend.ToString(),
                ["Sweep"]=result.Sweep.ToString(),
                ["IndexSize"]=result.IndexSize.ToString(CultureInfo.InvariantCulture),
                ["PageSize"]=result.PageSize.ToString(CultureInfo.InvariantCulture),
                ["SegmentSize"]=result.SegmentSize.ToString(CultureInfo.InvariantCulture),
                ["MemorySize"]=result.MemorySize.ToString(CultureInfo.InvariantCulture),
                ["MutableFraction"]=result.MutableFraction.ToString("R",CultureInfo.InvariantCulture),
            });
        var status = result.FailureKind==ParameterSweepFailureKind.Completed
            ? UiRunStatus.Completed
            : UiRunStatus.Failed;
        var job = this.store.UpsertJob(
            runId,
            testCase.CaseId,
            result.CaseName,
            status,
            result.MaxSuccessfulCount,
            1,
            null,
            result.MeasuredAt,
            DateTimeOffset.UtcNow,
            result.FailureKind.ToString(),
            result.FailureMessage);
        var metrics = new List<UiMetricValueWrite> {
            new("MaxSuccessfulCount",result.MaxSuccessfulCount,"count"),
            new("EntryCount",result.EntryCount,"count"),
            new("AddElapsedMilliseconds",result.AddElapsedMilliseconds,"ms"),
        };
        if(result.LookupMeanNanoseconds is { } lookupMeanNanoseconds) {
            metrics.Add(new("LookupMeanNanoseconds",lookupMeanNanoseconds,"ns"));
        }

        this.store.SaveMeasurement(job.JobId,metrics,result.MeasuredAt);
    }

    private IReadOnlyList<ParameterSweepResult> LoadResults(UiRunRecord run) {
        var cases = this.store.ListCases(run.RunId).ToDictionary(static item => item.CaseId);
        var jobs = this.store.ListJobs(run.RunId);
        var metrics = this.store.ListMetrics(run.RunId)
            .GroupBy(static item => item.JobId)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .GroupBy(static item => item.MetricName,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(static item => item.Key,static item => item.Last(),StringComparer.OrdinalIgnoreCase));
        var results = new List<ParameterSweepResult>();
        foreach(var job in jobs) {
            if(!cases.TryGetValue(job.CaseId,out var testCase)) {
                continue;
            }

            var arguments = this.store.ListCaseArguments(testCase.CaseId);
            metrics.TryGetValue(job.JobId,out var jobMetrics);
            results.Add(new(
                run.RunId.ToString(CultureInfo.InvariantCulture),
                job.FinishedAt??job.StartedAt??run.UpdatedAt,
                testCase.Name,
                ParseEnum(arguments,"Collection",ParameterSweepCollectionKind.HashSet),
                GetArgument(arguments,"Generic",testCase.TypeFullName),
                ParseEnum(arguments,"Backend",ParameterSweepBackend.Memory),
                ParseEnum(arguments,"Sweep",ParameterSweepAxis.IndexSize),
                ParseLong(arguments,"IndexSize"),
                ParseLong(arguments,"PageSize"),
                ParseLong(arguments,"SegmentSize"),
                ParseLong(arguments,"MemorySize"),
                ParseDouble(arguments,"MutableFraction"),
                GetMetric(jobMetrics,"MaxSuccessfulCount",job.ElementCount??0),
                GetMetric(jobMetrics,"EntryCount",0),
                Enum.TryParse<ParameterSweepFailureKind>(job.FailureKind,true,out var failureKind)
                    ? failureKind
                    : job.Status==UiRunStatus.Completed
                        ? ParameterSweepFailureKind.Completed
                        : ParameterSweepFailureKind.Exception,
                job.FailureReason,
                GetMetric(jobMetrics,"AddElapsedMilliseconds",0),
                GetNullableMetric(jobMetrics,"LookupMeanNanoseconds")));
        }

        return results;
    }

    private static string GetArgument(IReadOnlyDictionary<string,string> arguments,string key,string fallback) =>
        arguments.TryGetValue(key,out var value) ? value : fallback;

    private static long ParseLong(IReadOnlyDictionary<string,string> arguments,string key) =>
        long.TryParse(GetArgument(arguments,key,"0"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
            ? value
            : 0;

    private static double ParseDouble(IReadOnlyDictionary<string,string> arguments,string key) =>
        double.TryParse(GetArgument(arguments,key,"0"),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)
            ? value
            : 0;

    private static T ParseEnum<T>(IReadOnlyDictionary<string,string> arguments,string key,T fallback) where T:struct =>
        Enum.TryParse<T>(GetArgument(arguments,key,string.Empty),true,out var value)
            ? value
            : fallback;

    private static long GetMetric(IReadOnlyDictionary<string,UiMetricValueRecord>? metrics,string key,long fallback) =>
        metrics is not null&&metrics.TryGetValue(key,out var value)
            ? (long)value.NumericValue
            : fallback;

    private static double GetMetric(IReadOnlyDictionary<string,UiMetricValueRecord>? metrics,string key,double fallback) =>
        metrics is not null&&metrics.TryGetValue(key,out var value)
            ? value.NumericValue
            : fallback;

    private static double? GetNullableMetric(IReadOnlyDictionary<string,UiMetricValueRecord>? metrics,string key) =>
        metrics is not null&&metrics.TryGetValue(key,out var value)
            ? value.NumericValue
            : null;
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
