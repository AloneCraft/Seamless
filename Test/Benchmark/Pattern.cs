#if NET10_0_OR_GREATER && WINDOWS
using System.Collections.Concurrent;
using Seamless.UI.Storage;

namespace Test.Benchmark.Pattern;

public sealed class RunnerViewModel{
    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task Arrayタブの全てのベンチマークにチェックして1msで実行(){
        // UI操作: Arrayタブを前提に「全て選択」し、「Run実行」を押下する。
        // 観測: 選択セルでCPU実行中アイコンが出ること、FinalCompletedへ遷移すること、進捗 Completed/Total が一致することを確認する。
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);
        var running = TrackRunningIcons(targets);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));

        Assert.All(targets,cell => Assert.Contains(cell,running));
        Assert.All(targets,cell => Assert.True(cell.HasCpuDisplay));
        Assert.All(targets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.Equal(targets.Count,viewModel.SelectedSourceTab.Progress.Completed);
        Assert.Equal(targets.Count,viewModel.SelectedSourceTab.Progress.Total);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task ArrayタブのFLinqのベンチマークにチェックして1msで実行(){
        // UI操作: ArrayタブでFLinq側グループを選択し、「Run実行」を押下する。
        // 観測: 左列（FLinq）セルのCPU実行中→FinalCompletedの遷移を確認する。関連する右列（ZLinq）行とのペア表示整合も含めて確認する。
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        SelectProjectGroup(viewModel,"Left");
        var fLinqCells = viewModel.BenchmarkRows.Select(static row => row.LeftCell).Where(static cell => cell.HasCase).ToArray();
        var zLinqCells = viewModel.BenchmarkRows.Select(static row => row.RightCell).Where(static cell => cell.HasCase).ToArray();
        var allObservedCells = fLinqCells.Concat(zLinqCells).ToArray();
        var running = TrackRunningIcons(allObservedCells);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => allObservedCells.All(IsFinalCompleted));

        Assert.All(fLinqCells,cell => Assert.Contains(cell,running));
        Assert.All(fLinqCells,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.All(zLinqCells,cell => Assert.Contains(cell,running));
        Assert.All(zLinqCells,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.Equal(fLinqCells.Length,viewModel.SelectedSourceTab.Progress.Completed);
        Assert.Equal(fLinqCells.Length,viewModel.SelectedSourceTab.Progress.Total);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task ArrayタブのZLinqのベンチマークにチェックして1msで実行(){
        // UI操作: ArrayタブでZLinq側グループを選択し、「Run実行」を押下する。
        // 観測: 右列（ZLinq）セルのCPU実行中アイコンとFinalCompleted遷移、進捗 Completed/Total を確認する。
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        SelectProjectGroup(viewModel,"Right");
        var zLinqCells = viewModel.BenchmarkRows.Select(static row => row.RightCell).Where(static cell => cell.HasCase).ToArray();
        var running = TrackRunningIcons(zLinqCells);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => zLinqCells.All(IsFinalCompleted));

        Assert.All(zLinqCells,cell => Assert.Contains(cell,running));
        Assert.All(zLinqCells,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.Equal(zLinqCells.Length,viewModel.SelectedSourceTab.Progress.Completed);
        Assert.Equal(zLinqCells.Length,viewModel.SelectedSourceTab.Progress.Total);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task IEnumerableタブの全てのベンチマークにチェックして1msで実行(){
        // UI操作: IEnumerable（Range）ベンチマーク一覧で「全て選択」し、「Run実行」を押下する。
        // 観測: 対象セルのCPU状態アイコンとFinalCompleted遷移、進捗 Completed/Total を確認する。
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemRange);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);
        var fLinqCells = viewModel.BenchmarkRows.Select(static row => row.LeftCell).Where(static cell => cell.HasCase).ToArray();
        var running = TrackRunningIcons(targets);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));

        Assert.All(fLinqCells,cell => Assert.Contains(cell,running));
        Assert.All(fLinqCells,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.Equal(targets.Count,viewModel.SelectedSourceTab.Progress.Completed);
        Assert.Equal(targets.Count,viewModel.SelectedSourceTab.Progress.Total);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task SourceTabの列ヘッダーまとめチェックは他タブへ同期しない(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        var arrayState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemArray);
        var rangeState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemRange);
        var arrayTabOption = viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemArray);
        var rangeTabOption = viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemRange);

        //ArrayタブでLeft(FLinq)ヘッダーをまとめチェックしても、Rangeタブの行や進捗ゲージには反映されない
        SelectProjectGroup(viewModel,"Left");

        Assert.All(arrayState.Rows,row => Assert.True(row.LeftCell.IsSelected));
        Assert.All(rangeState.Rows,row => Assert.False(row.LeftCell.IsSelected));
        Assert.True(arrayTabOption.HasProgress);
        Assert.False(rangeTabOption.HasProgress);

        //Rangeタブへ切替えても、LeftCell.IsSelectedはfalseのまま(同期しない)
        viewModel.SelectedSourceTab=rangeTabOption;

        Assert.All(rangeState.Rows,row => Assert.False(row.LeftCell.IsSelected));
        Assert.All(arrayState.Rows,row => Assert.True(row.LeftCell.IsSelected));

        //Rangeタブ側でまとめチェックした場合は、Rangeタブだけに反映される
        SelectProjectGroup(viewModel,"Left");

        Assert.All(rangeState.Rows,row => Assert.True(row.LeftCell.IsSelected));
        Assert.All(arrayState.Rows,row => Assert.True(row.LeftCell.IsSelected));
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task Sourceタブの個別セルチェックは他タブへ同期しない(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        var arrayState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemArray);
        var rangeState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemRange);

        viewModel.ToggleBenchmarkCellCheck(arrayState.Rows[0],"Left");

        Assert.True(arrayState.Rows[0].LeftCell.IsSelected);
        Assert.All(rangeState.Rows,row => Assert.False(row.LeftCell.IsSelected));
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task SourceTab切替でも緑クリック選択をタブ毎に保持する(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        var arrayState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemArray);
        var rangeState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemRange);
        {
            //1タブ目の1行目を緑選択し、2タブ目へ漏れず1タブ目には保持されることを確認する
            viewModel.SelectBenchmarkRow(arrayState.Rows[0],global::UI.BenchmarkCellSelectionGesture.Replace);
            Assert.True(arrayState.Rows[0].IsUiSelected);

            viewModel.SelectedSourceTab=viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemRange);

            Assert.All(rangeState.Rows,static row => Assert.False(row.IsUiSelected));
            Assert.True(arrayState.Rows[0].IsUiSelected);
        }
        {
            //2タブ目の2行目を緑選択しても1タブ目へ混線せず、1タブ目へ戻ると選択が復元されていることを確認する
            viewModel.SelectBenchmarkRow(rangeState.Rows[1],global::UI.BenchmarkCellSelectionGesture.Replace);

            Assert.True(rangeState.Rows[1].IsUiSelected);
            Assert.False(rangeState.Rows[0].IsUiSelected);
            Assert.True(arrayState.Rows[0].IsUiSelected);

            viewModel.SelectedSourceTab=viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemArray);

            Assert.True(arrayState.Rows[0].IsUiSelected);
            Assert.False(arrayState.Rows[1].IsUiSelected);
        }
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task 選択タブとずれた行への緑クリック選択はその行のタブへ適用される(){
        //切替保留などで表示タブとVM選択タブが食い違っても、クリックされた行が属するタブのstateへ適用されることを確認する
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        var arrayState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemArray);
        var rangeState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemRange);

        viewModel.SelectBenchmarkRow(rangeState.Rows[1],global::UI.BenchmarkCellSelectionGesture.Replace);

        Assert.True(rangeState.Rows[1].IsUiSelected);
        Assert.All(arrayState.Rows,static row => Assert.False(row.IsUiSelected));
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task 完了後にもう一度実行できる(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);
        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        {
            //完了後にRunボタンが有効で、2回目の実行が実際に開始(CPU実行中を再通過)して完了することを確認する
            Assert.True(viewModel.RunCommand.CanExecute(null));
            var running = TrackRunningIcons(targets);
            await viewModel.RunCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => targets.All(IsFinalCompleted));
            Assert.All(targets,cell => Assert.Contains(cell,running));
        }
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task 完了後の選択変更にRunボタン活性が追従する(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);
        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        {
            //完了後に選択を全解除するとボタン活性もライブに追従する(凍結スナップショットを見ない)ことを確認する
            viewModel.ClearSelectionCommand.Execute(null);
            Assert.False(viewModel.HasSelectedBenchmarkCells);
            Assert.False(viewModel.RunCommand.CanExecute(null));
        }
        {
            //再選択で活性が復帰し、実行が実際に走ることを確認する
            viewModel.SelectAllCommand.Execute(null);
            Assert.True(viewModel.HasSelectedBenchmarkCells);
            Assert.True(viewModel.RunCommand.CanExecute(null));
            var reselected = GetSelectedCells(viewModel);
            var running = TrackRunningIcons(reselected);
            await viewModel.RunCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => reselected.All(IsFinalCompleted));
            Assert.All(reselected,cell => Assert.Contains(cell,running));
        }
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task フレッシュ実行では全リスタート指定が渡る(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);
        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        {
            //フレッシュ実行(初回)ではtrueが渡ることを確認する
            Assert.Equal([true],fixture.RunService.ObservedRestartFromRequestedElementCount);
        }
    }

#if DEBUG
    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task 一時停止後に再開すると続きから実行されfalseを渡す(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var runTask = viewModel.RunInfiniteCommand.ExecuteAsync(null);
        //IsBenchmarkRunActiveはDB保存の待ち合わせより前に立つため、実際にFakeRunServiceへ到達するまで待つ
        await WaitUntilAsync(() => fixture.RunService.ObservedRestartFromRequestedElementCount.Count>0);

        await viewModel.PauseCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsBenchmarkPaused);
        await runTask;
        {
            //一時停止からの再開(Resume)が実際に実行を開始・完了することを確認する
            Assert.True(viewModel.RunCommand.CanExecute(null));
            var targets = GetSelectedCells(viewModel);
            var running = TrackRunningIcons(targets);
            await viewModel.RunCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => targets.All(IsFinalCompleted));
            Assert.All(targets,cell => Assert.Contains(cell,running));
        }
        {
            //再開呼び出しではrestartFromRequestedElementCountがfalseで渡ることを確認する(全リスタート防止)
            Assert.Equal([true,false],fixture.RunService.ObservedRestartFromRequestedElementCount);
        }
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task 停止後にもう一度実行できる(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var runTask = viewModel.RunInfiniteCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsBenchmarkRunActive);

        await viewModel.StopCommand.ExecuteAsync(null);
        await runTask;
        {
            //停止後にRunボタンが有効で、再実行が実際に開始・完了することを確認する
            Assert.True(viewModel.RunCommand.CanExecute(null));
            var targets = GetSelectedCells(viewModel);
            var running = TrackRunningIcons(targets);
            await viewModel.RunCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => targets.All(IsFinalCompleted));
            Assert.All(targets,cell => Assert.Contains(cell,running));
        }
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task RunInfiniteCommand_FollowsRunAndPauseState(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;

        Assert.False(viewModel.RunInfiniteCommand.CanExecute(null));

        viewModel.SelectAllCommand.Execute(null);

        Assert.True(viewModel.RunInfiniteCommand.CanExecute(null));

        var runTask = viewModel.RunInfiniteCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsBenchmarkRunActive);

        Assert.False(viewModel.RunInfiniteCommand.CanExecute(null));

        await viewModel.PauseCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsBenchmarkPaused);

        Assert.True(viewModel.RunInfiniteCommand.CanExecute(null));

        await viewModel.StopCommand.ExecuteAsync(null);
        await runTask;
    }
#endif

    private static void SelectProjectGroup(global::UI.ViewModels.RunnerViewModel viewModel,string groupId){
        viewModel.ClearSelectionCommand.Execute(null);
        var group = viewModel.BenchmarkProjectSelectionGroups.First(group => string.Equals(group.Id,groupId,StringComparison.OrdinalIgnoreCase));
        viewModel.ToggleBenchmarkColumnGroup(group);
    }

    private static IReadOnlyList<global::UI.BenchmarkProjectCell> GetSelectedCells(global::UI.ViewModels.RunnerViewModel viewModel) =>
        viewModel.BenchmarkRows
            .SelectMany(static row => row.ProjectCells)
            .Where(static cell => cell.HasCase&&cell.IsSelected)
            .ToArray();

    private static ConcurrentDictionary<global::UI.BenchmarkProjectCell,byte> TrackRunningIcons(IEnumerable<global::UI.BenchmarkProjectCell> cells){
        var running = new ConcurrentDictionary<global::UI.BenchmarkProjectCell,byte>();
        foreach(var cell in cells){
            if(cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.CpuRunning){
                running.TryAdd(cell,0);
            }
            cell.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(global::UI.BenchmarkProjectCell.StatusImageSource)
                    &&cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.CpuRunning){
                    running.TryAdd(cell,0);
                }
            };
        }

        return running;
    }

    private static bool IsFinalCompleted(global::UI.BenchmarkProjectCell cell) =>
        cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.FinalCompleted;

    private static async Task WaitUntilAsync(Func<bool> condition){
        for(var index=0;index<60;index++){
            if(condition()){
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition());
    }

    private sealed class ViewModelFixture{
        private ViewModelFixture(global::UI.ViewModels.RunnerViewModel viewModel,FakeRunService runService){
            this.ViewModel=viewModel;
            this.RunService=runService;
        }

        public global::UI.ViewModels.RunnerViewModel ViewModel { get; }
        public FakeRunService RunService { get; }

        public static async Task<ViewModelFixture> CreateAsync(global::UI.BenchmarkSourceTab sourceTab){
            var cases = CreateCatalogCases();
            var storePath = Path.Combine(Path.GetTempPath(),"Seamless-Test-Benchmark-UI-"+Guid.NewGuid().ToString("N")+".db");
            var store = new UiRunStore(storePath);
            store.EnsureSchema();
            var runService = new FakeRunService(cases);
            var viewModel = new global::UI.ViewModels.RunnerViewModel(
                new FakeCatalogService(cases),
                new FakeCpuTopologyService(),
                runService,
                new global::UI.BenchmarkComparisonService(new global::UI.BenchmarkResultService()),
                store,
                Path.Combine(Path.GetTempPath(),"Seamless-Test-Benchmark-UI-"+Guid.NewGuid().ToString("N")),
                new ImmediateDispatcher(),
                new InMemoryPreferences());
            await viewModel.InitializeAsync();
            viewModel.SelectedSourceTab=viewModel.SourceTabs.First(option => option.Definition.Tab==sourceTab);
            viewModel.SelectedExecutionProfile=viewModel.ExecutionProfiles.First(option => option.Profile==global::UI.BenchmarkExecutionProfile.Test);
            viewModel.SelectedThreshold=viewModel.ThresholdOptions.First(option => option.Milliseconds==1);
            viewModel.ClearSelectionCommand.Execute(null);
            return new(viewModel,runService);
        }

        private static IReadOnlyList<global::UI.BenchmarkCase> CreateCatalogCases(){
            var array = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var enumerable = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemRange);
            return [
                CreateCase(array.LeftProject,array.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                CreateCase(array.RightProject,array.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","AggregateBy_keySelector"),
                CreateCase(array.LeftProject,array.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","Select_selector"),
                CreateCase(array.RightProject,array.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","Select_selector"),
                CreateCase(enumerable.LeftProject,enumerable.LeftAssemblyName,"DotNet.Linq.Enumerable.Range.FLinqInt32","AggregateBy_keySelector"),
                CreateCase(enumerable.RightProject,enumerable.RightAssemblyName,"DotNet.Linq.Enumerable.Range.ZLinqInt32","AggregateBy_keySelector"),
                CreateCase(enumerable.LeftProject,enumerable.LeftAssemblyName,"DotNet.Linq.Enumerable.Range.FLinqInt32","Select_selector"),
                CreateCase(enumerable.RightProject,enumerable.RightAssemblyName,"DotNet.Linq.Enumerable.Range.ZLinqInt32","Select_selector"),
            ];
        }

        private static global::UI.BenchmarkCase CreateCase(string project,string assemblyName,string typeFullName,string methodName) =>
            new(project,assemblyName,typeFullName,methodName);
    }

    private sealed class FakeCatalogService(IReadOnlyList<global::UI.BenchmarkCase> cases):global::UI.IBenchmarkCatalogService{
        public Task<IReadOnlyList<global::UI.BenchmarkCase>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(cases);
    }

    private sealed class FakeCpuTopologyService:global::UI.ICpuTopologyService{
        public IReadOnlyList<global::UI.CpuSelectionItem> Load() => [
            new(0,global::UI.CpuCoreKind.Performance,0,0,true),
        ];
    }

    private sealed class FakeRunService(IReadOnlyList<global::UI.BenchmarkCase> allCases):global::UI.IBenchmarkRunService{
        public List<bool> ObservedRestartFromRequestedElementCount { get; } = [];

        public async Task<global::UI.BenchmarkRunReport> RunAsync(
            IReadOnlyList<global::UI.BenchmarkCase> benchmarks,
            IReadOnlyList<int> logicalCpus,
            Func<IReadOnlyList<int>> logicalCpuProvider,
            IReadOnlyList<int> elementCounts,
            global::UI.BenchmarkSourceTab sourceTab,
            global::UI.BenchmarkExecutionProfile executionProfile,
            TimeSpan adaptiveThreshold,
            bool restartFromRequestedElementCount,
            string? runRootOverride,
            Func<IReadOnlyList<global::UI.BenchmarkCase>>? benchmarkProvider,
            Action<global::UI.BenchmarkResultItem> resultReceived,
            Action<string> statusChanged,
            global::UI.BenchmarkRunExecutionOptions? executionOptions,
            CancellationToken cancellationToken){
            this.ObservedRestartFromRequestedElementCount.Add(restartFromRequestedElementCount);
            var emitCases = this.CreateEmitCases(benchmarks,sourceTab);
            var logicalCpu = logicalCpus.FirstOrDefault();
            foreach(var benchmark in emitCases){
                cancellationToken.ThrowIfCancellationRequested();
                resultReceived(CreateResult(benchmark,sourceTab,global::UI.JobStatus.Running,logicalCpu,null));
                if(executionOptions?.UseInfiniteTestRunner==true){
                    await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);
                }
                resultReceived(CreateResult(benchmark,sourceTab,global::UI.JobStatus.Completed,logicalCpu,adaptiveThreshold.TotalMilliseconds*1_000_000));
            }
            statusChanged("Completed");
            return new global::UI.BenchmarkRunReport(runRootOverride);
        }

        private IReadOnlyList<global::UI.BenchmarkCase> CreateEmitCases(
            IReadOnlyList<global::UI.BenchmarkCase> selected,
            global::UI.BenchmarkSourceTab sourceTab){
            var source = global::UI.BenchmarkSourceTabs.Get(sourceTab);
            var map = new Dictionary<string,global::UI.BenchmarkCase>(StringComparer.OrdinalIgnoreCase);
            foreach(var benchmark in selected){
                Add(benchmark);
                var pairProject = string.Equals(benchmark.Project,source.LeftProject,StringComparison.OrdinalIgnoreCase)
                    ? source.RightProject
                    : source.LeftProject;
                var pair = allCases.FirstOrDefault(candidate =>
                    string.Equals(candidate.Project,pairProject,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(candidate.MethodName,benchmark.MethodName,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(candidate.ArgumentKey,benchmark.ArgumentKey,StringComparison.OrdinalIgnoreCase));
                if(pair is not null){
                    Add(pair);
                }
            }

            return map.Values.ToArray();

            void Add(global::UI.BenchmarkCase benchmark){
                map.TryAdd(benchmark.Project+"|"+benchmark.TypeFullName+"|"+benchmark.MethodName+"|"+benchmark.ArgumentKey,benchmark);
            }
        }

        private static global::UI.BenchmarkResultItem CreateResult(
            global::UI.BenchmarkCase benchmark,
            global::UI.BenchmarkSourceTab sourceTab,
            global::UI.JobStatus status,
            int logicalCpu,
            double? meanNanoseconds){
            var job = new global::UI.BenchmarkJob{
                Project=benchmark.Project,
                AssemblyName=benchmark.AssemblyName,
                TypeFullName=benchmark.TypeFullName,
                MethodName=benchmark.MethodName,
                ArgumentKey=benchmark.ArgumentKey,
                Arguments=benchmark.Arguments.ToList(),
                SourceTab=sourceTab,
                ElementCount=1,
                LoopCount=1,
                PreparedElementCounts=[1],
                Status=status,
                AssignedLogicalCpu=logicalCpu,
                StartedAt=DateTimeOffset.Now,
                FinishedAt=status==global::UI.JobStatus.Completed ? DateTimeOffset.Now : null,
                MeanNanoseconds=meanNanoseconds,
                AllocatedBytes=status==global::UI.JobStatus.Completed ? 0 : null,
                ExitCode=status==global::UI.JobStatus.Completed ? 0 : null,
                ResultDigest=status==global::UI.JobStatus.Completed ? "test" : string.Empty,
                ResultCheckText=status==global::UI.JobStatus.Completed ? "OK" : string.Empty,
            };
            return new global::UI.BenchmarkResultItem(job);
        }
    }

    private sealed class ImmediateDispatcher:global::UI.IBenchmarkUiDispatcher{
        public void BeginInvoke(Action action) => action();
    }

    private sealed class InMemoryPreferences:global::UI.IBenchmarkUiPreferences{
        private readonly Dictionary<string,string> values = new(StringComparer.OrdinalIgnoreCase);

        public string Get(string key,string defaultValue) =>
            this.values.TryGetValue(key,out var value) ? value : defaultValue;

        public void Set(string key,string value) => this.values[key]=value;
    }
}
#endif
