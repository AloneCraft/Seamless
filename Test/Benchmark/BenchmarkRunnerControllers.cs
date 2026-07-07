#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class BenchmarkRunnerControllers{
    [Fact,Trait("Category","BenchmarkUI")]
    public void DefaultSortController(){
        var context = new SortContext();
        var controller = new global::UI.DefaultSortController(context);

        controller.Initialize();
        controller.SortBenchmarkRows(global::UI.GridSortColumn.PairRatioTime);
        controller.AddBenchmarkSortRule();
        controller.ResetBenchmarkSortRules();

        Assert.NotEmpty(context.BenchmarkSortRules);
        Assert.Contains(context.BenchmarkSortColumnOptions,option => option.Column==global::UI.GridSortColumn.PairRatioTime);
        Assert.Equal(global::UI.GridSortColumn.BenchmarkType,context.CurrentSortColumn);
        Assert.True(context.SummaryChangedCount>0);
        Assert.True(context.ChartRefreshCount>0);
        Assert.Contains(context.PreferenceValues,entry => entry.Key=="Seamless.Benchmark.SortRules.v1");
    }

    [Fact,Trait("Category","BenchmarkUI")]
    public void DefaultCpuTopologyController(){
        var context = new CpuContext();
        var controller = new global::UI.DefaultCpuTopologyController(context,new CpuService());

        controller.Initialize();
        controller.ApplyCpuSelection(context.CpuItems,false);

        Assert.NotEmpty(context.CpuSections);
        Assert.Single(controller.GetSelectedLogicalCpus());
        Assert.Contains(context.CpuItems,item => item.IsSelected);
        Assert.Contains(nameof(global::UI.RunnerViewModel.PerformanceCpuSections),context.ChangedProperties);
    }

    [Fact,Trait("Category","BenchmarkUI")]
    public void RunVisualScheduler(){
        var scheduler = new ImmediateRunVisualScheduler();
        var started = scheduler.UtcNow;

        scheduler.Advance(TimeSpan.FromMilliseconds(250));
        var completed = scheduler.DelayAsync(TimeSpan.FromSeconds(10));

        Assert.True(completed.IsCompletedSuccessfully);
        Assert.Equal(started+TimeSpan.FromMilliseconds(250),scheduler.UtcNow);
    }

    private sealed class SortContext:global::UI.ISortControllerContext,global::UI.IUiPreferences{
        public System.Collections.ObjectModel.ObservableCollection<global::UI.SortRule> BenchmarkSortRules { get; } = [];
        public System.Collections.ObjectModel.ObservableCollection<global::UI.SortColumnOption> BenchmarkSortColumnOptions { get; } = [];
        public IReadOnlyList<global::UI.SourceTabViewState> SourceTabViewStates { get; } = [new(global::UI.BenchmarkSourceTabs.Default)];
        public global::UI.BenchmarkSourceTabDefinition SelectedSourceDefinition => global::UI.BenchmarkSourceTabs.Default;
        public bool HasBenchmarkTypeColumn { get; set; } = true;
        public global::UI.GridSortColumn CurrentSortColumn { get; set; }
        public bool CurrentSortDescending { get; set; }
        public bool IsBenchmarkRunActive => false;
        public bool IsBenchmarkPaused => false;
        public global::UI.IUiPreferences UiPreferences => this;
        public Dictionary<string,string> PreferenceValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int SummaryChangedCount { get; private set; }
        public int ChartRefreshCount { get; private set; }

        public void NotifyBenchmarkSortSummaryChanged() =>
            ++this.SummaryChangedCount;

        public void UpdateAllSourceTabChartsForSort() =>
            ++this.ChartRefreshCount;

        public string Get(string key,string defaultValue) =>
            this.PreferenceValues.TryGetValue(key,out var value) ? value : defaultValue;

        public void Set(string key,string value) =>
            this.PreferenceValues[key]=value;
    }

    private sealed class CpuContext:global::UI.ICpuTopologyControllerContext{
        public System.Collections.ObjectModel.ObservableCollection<global::UI.CpuSelectionItem> CpuItems { get; } = [];
        public System.Collections.ObjectModel.ObservableCollection<global::UI.CpuTableSection> CpuSections { get; } = [];
        public bool IsRefreshing => false;
        public HashSet<string> ChangedProperties { get; } = [];

        public void NotifyCpuTopologyPropertyChanged(string propertyName) =>
            this.ChangedProperties.Add(propertyName);
    }

    private sealed class CpuService:global::UI.ICpuTopologyService{
        public IReadOnlyList<global::UI.CpuSelectionItem> Load() => [
            new(0,global::UI.CpuCoreKind.Performance,0,0,true),
            new(1,global::UI.CpuCoreKind.Performance,0,0,true),
        ];
    }

    private sealed class ImmediateRunVisualScheduler:global::UI.IRunVisualScheduler{
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;

        public Task DelayAsync(TimeSpan delay,CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Advance(TimeSpan elapsed) =>
            this.UtcNow+=elapsed;
    }
}
#endif


