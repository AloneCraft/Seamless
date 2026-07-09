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

        Assert.Contains(viewModel.BenchmarkRows,row => row.HasCpuDisplay);
        Assert.All(targets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.Equal(targets.Count,viewModel.SelectedSourceTab.Progress.Completed);
        Assert.Equal(targets.Count,viewModel.SelectedSourceTab.Progress.Total);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task MultipleSourceTabs_RunOnce_FinalCompletedForAllSelectedTabs(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        fixture.RunService.EmitAllElementCounts=true;
        var arrayTab = viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemArray);
        var enumerableTab = viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemRange);
        var listTab = viewModel.SourceTabs.First(option => option.Definition.Tab==global::UI.BenchmarkSourceTab.SystemList);
        var arrayState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemArray);
        var enumerableState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemRange);
        var listState = viewModel.GetSourceTabViewState(global::UI.BenchmarkSourceTab.SystemList);

        viewModel.SelectedSourceTab=arrayTab;
        viewModel.SelectAllCommand.Execute(null);
        var arrayTargets = GetSelectedCells(arrayState);

        viewModel.SelectedSourceTab=enumerableTab;
        viewModel.SelectAllCommand.Execute(null);
        var enumerableTargets = GetSelectedCells(enumerableState);

        viewModel.SelectedSourceTab=listTab;
        viewModel.SelectAllCommand.Execute(null);
        var listTargets = GetSelectedCells(listState);
        var allTargets = arrayTargets.Concat(enumerableTargets).Concat(listTargets).ToArray();

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => allTargets.All(IsFinalCompleted));

        Assert.All(arrayTargets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.All(enumerableTargets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.All(listTargets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
        Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,arrayTab.CompletionIconSource);
        Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,enumerableTab.CompletionIconSource);
        Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,listTab.CompletionIconSource);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public void SourceTabCompletionIcon_UsesCompletedUntilFinalCompleted(){
        var option = new global::UI.SourceTabOption(global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray));

        option.SetProgress(new global::UI.BenchmarkTabProgress(1,1));
        option.SetFinalCompleted(false);

        Assert.True(option.IsCompletionIconVisible);
        Assert.Equal(global::UI.BenchmarkStatusImageNames.Completed,option.CompletionIconSource);

        option.SetFinalCompleted(true);

        Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,option.CompletionIconSource);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task Initialize_WithRecoveredPausedRunAndNewSelection_AllowsRunHistorySelection(){
        var fixture = await ViewModelFixture.CreateAsync(
            global::UI.BenchmarkSourceTab.SystemArray,
            store => SeedPausedSelectedRun(store));
        var viewModel = fixture.ViewModel;

        Assert.Equal(0,viewModel.SelectedRunOption?.UiRunId);
        Assert.Equal(0,viewModel.SelectedRunOption?.RunNumber);
        Assert.False(viewModel.IsBenchmarkPaused);
        Assert.True(viewModel.CanSelectRunHistory);
        Assert.True(viewModel.CanChangeBenchmarkConditions);
        Assert.True(viewModel.CanCreateNewGrid);
    }

    private static void SeedPausedSelectedRun(UiRunStore store){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Recovered",
            global::UI.BenchmarkExecutionProfile.Test.ToString(),
            sourceTab.Tab.ToString(),
            DateTimeOffset.UtcNow));
        store.SetRunStatus(run.RunId,UiRunStatus.Paused,"Recovered test run.");
        store.SaveRunSettings(run.RunId,"RecoveredRoot",[4096],1_000_000,[]);
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new(
                sourceTab.Id,
                "int",
                "AggregateBy_keySelector",
                0,
                true,
                new(UiRunStatus.Queued,string.Empty,null,null,null),
                new(UiRunStatus.Queued,string.Empty,null,null,null)),
        ]);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public void SnapshotTableRow_UsesStoredDisplayType(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var cases = new Dictionary<string,global::UI.BenchmarkCase>(StringComparer.OrdinalIgnoreCase) {
            [sourceTab.LeftProject]=new(sourceTab.LeftProject,sourceTab.LeftAssemblyName,"int","AggregateBy_keySelector"),
            [sourceTab.RightProject]=new(sourceTab.RightProject,sourceTab.RightAssemblyName,"int","AggregateBy_keySelector"),
        };
        var row = new global::UI.BenchmarkTableRow(
            "int|AggregateBy_keySelector",
            "AggregateBy_keySelector",
            "AggregateBy_keySelector",
            sourceTab,
            cases,
            "int");

        Assert.Equal("int",row.BenchmarkTypeText);
        Assert.Equal("AggregateBy_keySelector",row.BenchmarkMethodText);

        var measuredAt = new DateTimeOffset(2026,7,8,13,39,31,TimeSpan.Zero);
        var result = new global::UI.BenchmarkResultItem(new global::UI.BenchmarkJob{
            Project=sourceTab.LeftProject,
            AssemblyName=sourceTab.LeftAssemblyName,
            TypeFullName="int",
            MethodName="AggregateBy_keySelector",
            SourceTab=sourceTab.Tab,
            ElementCount=4096,
            LoopCount=global::UI.BenchmarkWorkloadAxis.DefaultLoopCount,
            Status=global::UI.JobStatus.Completed,
            AssignedLogicalCpu=0,
            StartedAt=measuredAt,
            FinishedAt=measuredAt,
            MeanNanoseconds=1_000_000,
            AllocatedBytes=1024,
            ExitCode=0,
        });
        row.ApplySnapshotResult(sourceTab.LeftProject,result,0,measuredAt);

        Assert.Equal("4096",row.LeftCell.ElementCountText);
        Assert.NotEmpty(row.LeftCell.ExecutionText);
        Assert.True(row.LeftCell.HasCpuDisplay);
        Assert.Equal("0",row.LeftCell.LogicalProcessorText);
        Assert.NotEmpty(row.LeftTimeText);

        row.RefreshBenchmarkName(4096);

        Assert.Equal("int",row.BenchmarkTypeText);
        Assert.Equal("AggregateBy_keySelector",row.BenchmarkMethodText);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public void RowSelectionHeader_ReflectsPartialCellSelection(){
        var row = CreateArrayBenchmarkRow("AggregateBy_keySelector");

        row.LeftCell.IsSelected=true;
        row.RightCell.IsSelected=true;
        row.RefreshSelectionState();

        Assert.True(row.IsSelectionChecked);
        Assert.True(row.IsSelected);

        row.RightCell.IsSelected=false;
        row.RefreshSelectionState();

        Assert.Null(row.IsSelectionChecked);
        Assert.True(row.IsSelected);

        row.LeftCell.IsSelected=false;
        row.RefreshSelectionState();

        Assert.False(row.IsSelectionChecked);
        Assert.False(row.IsSelected);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public void RunningResult_AppliesToRightZLinqCell(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var row = CreateArrayBenchmarkRow("Aggregate_seed_func_resultSelector");
        var result = CreateArrayResult(
            sourceTab.RightProject,
            "Aggregate_seed_func_resultSelector",
            global::UI.JobStatus.Running,
            runItemIndex:1);

        row.ApplyRunResult(result,null,DateTimeOffset.Now);

        Assert.True(row.RightCell.IsRunningVisual);
        Assert.False(row.LeftCell.IsRunningVisual);
        Assert.Equal(global::UI.BenchmarkStatusImageNames.CpuRunning,row.RightCell.StatusImageSource);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public void RunningResult_AppliesToLeftFLinqCell(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var row = CreateArrayBenchmarkRow("Aggregate_seed_func_resultSelector");
        var result = CreateArrayResult(
            sourceTab.LeftProject,
            "Aggregate_seed_func_resultSelector",
            global::UI.JobStatus.Running,
            runItemIndex:0);

        row.ApplyRunResult(result,null,DateTimeOffset.Now);

        Assert.True(row.LeftCell.IsRunningVisual);
        Assert.False(row.RightCell.IsRunningVisual);
        Assert.Equal(global::UI.BenchmarkStatusImageNames.CpuRunning,row.LeftCell.StatusImageSource);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task RunEventPipeline_KeepsZLinqRunningBeforeImmediateTerminal(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var pipeline = new global::UI.BenchmarkRunEventPipeline();
        var running = CreateArrayResult(sourceTab.RightProject,"Aggregate_seed_func_resultSelector",global::UI.JobStatus.Running,runItemIndex:1);
        var completed = CreateArrayResult(sourceTab.RightProject,"Aggregate_seed_func_resultSelector",global::UI.JobStatus.Completed,runItemIndex:1);

        pipeline.Post(new global::UI.BenchmarkRunEvent(running,1,null,DateTimeOffset.UtcNow));
        pipeline.Post(new global::UI.BenchmarkRunEvent(completed,1,null,DateTimeOffset.UtcNow));
        var snapshot = await WaitForPipelineSnapshotAsync(pipeline);

        Assert.Contains(snapshot.RunningUpdates,item =>
            string.Equals(item.Result.Project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(item.Result.Status,global::UI.JobStatus.Running.ToString(),StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshot.TerminalResults,item =>
            string.Equals(item.Result.Project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(item.Result.Status,"Completed",StringComparison.OrdinalIgnoreCase));
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task RunEventPipeline_SeparatesFLinqAndZLinqRunningKeys(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var pipeline = new global::UI.BenchmarkRunEventPipeline();
        var left = CreateArrayResult(sourceTab.LeftProject,"Aggregate_seed_func_resultSelector",global::UI.JobStatus.Running,runItemIndex:0);
        var right = CreateArrayResult(sourceTab.RightProject,"Aggregate_seed_func_resultSelector",global::UI.JobStatus.Running,runItemIndex:1);

        pipeline.Post(new global::UI.BenchmarkRunEvent(left,1,null,DateTimeOffset.UtcNow));
        pipeline.Post(new global::UI.BenchmarkRunEvent(right,1,null,DateTimeOffset.UtcNow));
        var snapshot = await WaitForPipelineSnapshotAsync(pipeline);

        Assert.Contains(snapshot.RunningUpdates,item => string.Equals(item.Result.Project,sourceTab.LeftProject,StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshot.RunningUpdates,item => string.Equals(item.Result.Project,sourceTab.RightProject,StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2,snapshot.RunningUpdates.Count);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task CompletedRun_SelectsSavedDatabaseRunWithoutUnsavedOption(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);

        var savedRun = viewModel.SelectedRunOption!;
        Assert.True(savedRun.UiRunId>0);
        Assert.Contains(viewModel.RunOptions,option => ReferenceEquals(option,savedRun));
        Assert.NotEqual("未保存",savedRun.DisplayName);
        Assert.Contains("/",savedRun.DisplayName);
        Assert.True(savedRun.TotalJobs>0);
        Assert.All(viewModel.RunOptions,option => Assert.NotEqual("未保存",option.DisplayName));
        Assert.All(targets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.FinalCompleted,cell.StatusImageSource));
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task CompletedRun_CanReloadSavedDatabaseRunAfterNewSelection(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);
        var savedRun = viewModel.SelectedRunOption!;
        var dataGridBatchEvents = new List<bool>();
        viewModel.DataGridUpdateBatchChanged+=(_,args) => dataGridBatchEvents.Add(args.IsBeginning);

        viewModel.NewGridCommand.Execute(null);
        await WaitUntilAsync(() => targets.All(static cell => cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.Waiting));
        Assert.Equal("新規",viewModel.SelectedRunOption?.DisplayName);

        AssertNewGridCatalogDisplay(viewModel);
        viewModel.SelectedRunOption=savedRun;
        Assert.True(viewModel.IsRunHistoryLoading);
        Assert.False(viewModel.CanSelectRunHistory);
        await WaitUntilAsync(() => viewModel.ResultItems.Any(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)));
        await WaitUntilAsync(() => !viewModel.IsRunHistoryLoading&&viewModel.CanSelectRunHistory);

        Assert.NotEmpty(viewModel.BenchmarkRows);
        Assert.True(viewModel.ResultItems.Count>0);
        var completedResults = viewModel.ResultItems
            .Where(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.All(completedResults,item => Assert.True(item.ElementCount>0));
        Assert.All(completedResults,item => Assert.NotNull(item.AssignedLogicalCpu));
        Assert.Contains(viewModel.ResultItems,item => viewModel.SelectedSourceTab.Definition.ContainsProject(item.Project));
        await WaitUntilOrContinueAsync(() => GetSelectedCells(viewModel).All(HasLoadedCellMetadata));
        var loadedTargets = GetSelectedCells(viewModel);
        Assert.All(loadedTargets,cell => Assert.True(HasLoadedCellMetadata(cell),CreateCellMetadataMessage(viewModel,cell)));
        var loadedRows = viewModel.BenchmarkRows
            .Where(static row => row.ProjectCells.Any(static cell => cell.HasCase&&cell.IsSelected))
            .ToArray();
        Assert.All(loadedRows,row => Assert.Equal("int",row.BenchmarkTypeText));
        Assert.All(loadedRows,AssertLoadedRowHasPairMetrics);

        Assert.Equal(savedRun.UiRunId,viewModel.SelectedRunOption?.UiRunId);
        Assert.NotEmpty(viewModel.ResultItems);
        Assert.True(dataGridBatchEvents.Count>=2);
        Assert.Equal(0,dataGridBatchEvents.Aggregate(0,(depth,isBeginning) => isBeginning ? depth+1 : depth-1));
        Assert.True(dataGridBatchEvents.First());
        Assert.False(dataGridBatchEvents.Last());
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task LoadedSavedDatabaseRun_RunAgain_PreservesSavedCheckState(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        SelectProjectGroup(viewModel,"Left");
        var targets = GetSelectedCells(viewModel);
        var savedSelectedCount = targets.Count;
        Assert.True(savedSelectedCount>0);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);
        var savedRun = viewModel.SelectedRunOption!;
        var runCount = fixture.RunService.ObservedRestartFromRequestedElementCount.Count;

        viewModel.NewGridCommand.Execute(null);
        await WaitUntilAsync(() => targets.All(static cell => cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.Waiting));
        viewModel.SelectedRunOption=savedRun;
        await WaitUntilAsync(() => !viewModel.IsRunHistoryLoading
            &&viewModel.ResultItems.Any(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)));

        Assert.True(viewModel.RunCommand.CanExecute(null),$"Status={viewModel.StatusText} CanRun={viewModel.CanRunBenchmark} Selected={GetSelectedCells(viewModel).Count} Option={viewModel.SelectedRunOption?.DisplayName}");
        await viewModel.RunCommand.ExecuteAsync(null);

        await WaitUntilOrContinueAsync(() => fixture.RunService.ObservedRestartFromRequestedElementCount.Count==runCount+1);
        Assert.True(
            fixture.RunService.ObservedRestartFromRequestedElementCount.Count==runCount+1,
            $"Status={viewModel.StatusText} CanRun={viewModel.CanRunBenchmark} Selected={GetSelectedCells(viewModel).Count} Option={viewModel.SelectedRunOption?.DisplayName}");
        Assert.Equal("新規",viewModel.SelectedRunOption?.DisplayName);
        Assert.Equal(savedSelectedCount,GetSelectedCells(viewModel).Count);
        Assert.Equal(savedSelectedCount,fixture.RunService.ObservedBenchmarkCounts[^1]);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task RowCheck_RunUsesOnlyCheckedRowPairs(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        var row = viewModel.BenchmarkRows.First(static row => row.LeftCell.HasCase&&row.RightCell.HasCase);

        viewModel.ToggleBenchmarkRow(row);

        Assert.True(viewModel.HasSelectedBenchmarkCells);
        Assert.True(row.IsSelected);
        Assert.True(row.LeftCell.IsSelected);
        Assert.True(row.RightCell.IsSelected);
        Assert.Equal(2,GetSelectedCells(viewModel).Count);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilOrContinueAsync(() => fixture.RunService.ObservedBenchmarkCounts.Count>0);

        Assert.Equal(2,fixture.RunService.ObservedBenchmarkCounts[^1]);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task LoadedSavedDatabaseRun_ShowsOnlySelectedRowsAndRestoresCpuDisplay(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        var selectedRows = viewModel.BenchmarkRows.Take(2).ToArray();
        Assert.Equal(2,selectedRows.Length);
        foreach(var row in selectedRows){
            viewModel.ToggleBenchmarkRow(row);
        }

        var selectedCells = GetSelectedCells(viewModel);
        Assert.Equal(4,selectedCells.Count);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => selectedCells.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);
        var savedRun = viewModel.SelectedRunOption!;
        var storedSelections = fixture.Store.ListUiSelections(savedRun.UiRunId);
        Assert.Contains(storedSelections,selection => selection.Scope=="BenchmarkCell"
            &&selection.SelectionKey.EndsWith("|"+viewModel.SelectedSourceDefinition.LeftProject,StringComparison.OrdinalIgnoreCase)
            &&selection.IsSelected);
        Assert.Contains(storedSelections,selection => selection.Scope=="BenchmarkCell"
            &&selection.SelectionKey.EndsWith("|"+viewModel.SelectedSourceDefinition.RightProject,StringComparison.OrdinalIgnoreCase)
            &&!selection.IsSelected);

        viewModel.NewGridCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.BenchmarkRows.Count>0&&GetSelectedCells(viewModel).Count==0);
        viewModel.SelectedRunOption=savedRun;
        await WaitUntilAsync(() => !viewModel.IsRunHistoryLoading
            &&viewModel.ResultItems.Any(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)));

        var loadedRows = viewModel.BenchmarkRows.ToArray();
        Assert.Equal(2,loadedRows.Length);
        Assert.DoesNotContain(loadedRows,row => !row.IsSelected);
        Assert.All(loadedRows,row => {
            AssertLoadedRowHasPairMetrics(row);
            Assert.True(row.LeftCell.HasCpuDisplay,CreateCellMetadataMessage(viewModel,row.LeftCell));
            Assert.True(row.RightCell.HasCpuDisplay,CreateCellMetadataMessage(viewModel,row.RightCell));
            Assert.False(string.IsNullOrWhiteSpace(row.LeftCell.CoreKindImageSource),CreateCellMetadataMessage(viewModel,row.LeftCell));
            Assert.False(string.IsNullOrWhiteSpace(row.RightCell.CoreKindImageSource),CreateCellMetadataMessage(viewModel,row.RightCell));
        });
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task LoadedSavedDatabaseRun_ResultElementCountChange_KeepsPartialSelectionAndSavedRun(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        fixture.RunService.EmitAllElementCounts=true;
        SelectProjectGroup(viewModel,"Left");
        Assert.All(viewModel.BenchmarkRows,row => Assert.Null(row.IsSelectionChecked));
        Assert.Null(viewModel.IsAllBenchmarkSelectionChecked);
        var targets = GetSelectedCells(viewModel);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.ResultItems.Any(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)));
        Assert.All(viewModel.BenchmarkRows,row => {
            Assert.True(row.LeftCell.IsSelected);
            Assert.False(row.RightCell.IsSelected);
            Assert.Null(row.IsSelectionChecked);
        });
        await SaveCurrentRunAsync(viewModel);
        var savedRun = viewModel.SelectedRunOption!;

        viewModel.NewGridCommand.Execute(null);
        await WaitUntilAsync(() => GetSelectedCells(viewModel).Count==0);
        viewModel.SelectedRunOption=savedRun;
        await WaitUntilAsync(() => !viewModel.IsRunHistoryLoading
            &&viewModel.ResultItems.Any(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase))
            &&viewModel.ResultElementCountOptions.Count>0);

        Assert.Equal(savedRun.UiRunId,viewModel.SelectedRunOption?.UiRunId);
        Assert.All(viewModel.BenchmarkRows,row => {
            Assert.True(row.LeftCell.IsSelected);
            Assert.False(row.RightCell.IsSelected);
            Assert.Null(row.IsSelectionChecked);
            Assert.True(row.IsSelected);
        });
        Assert.Null(viewModel.IsAllBenchmarkSelectionChecked);

        viewModel.SelectedResultElementCount=viewModel.ResultElementCountOptions.First();

        Assert.Equal(savedRun.UiRunId,viewModel.SelectedRunOption?.UiRunId);
        Assert.All(viewModel.BenchmarkRows,row => Assert.Null(row.IsSelectionChecked));
        Assert.Null(viewModel.IsAllBenchmarkSelectionChecked);

        viewModel.ToggleBenchmarkCellCheck(viewModel.BenchmarkRows[0],"Right");

        Assert.Equal(savedRun.UiRunId,viewModel.SelectedRunOption?.UiRunId);
    }

    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task CompletedRun_CanSelectEarlierSavedDatabaseRunWithoutRestart(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);
        var firstRunId = viewModel.SelectedRunOption!.UiRunId;

        viewModel.NewGridCommand.Execute(null);
        await WaitUntilAsync(() => targets.All(static cell => cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.Waiting));
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => targets.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);
        await WaitUntilAsync(() => viewModel.SelectedRunOption is { UiRunId:>0 }&&viewModel.SelectedRunOption.UiRunId!=firstRunId);
        var firstRun = viewModel.RunOptions.First(option => option.UiRunId==firstRunId);

        viewModel.SelectedRunOption=firstRun;
        await WaitUntilAsync(() => viewModel.SelectedRunOption?.UiRunId==firstRunId
            &&viewModel.ResultItems.Any(static item => string.Equals(item.Status,"Completed",StringComparison.OrdinalIgnoreCase)));
        await WaitUntilAsync(() => !viewModel.IsRunHistoryLoading&&viewModel.CanSelectRunHistory);

        Assert.Equal(firstRunId,viewModel.SelectedRunOption?.UiRunId);
        Assert.NotEmpty(viewModel.ResultItems);
    }

#if DEBUG
    [Fact,Trait("Category","BenchmarkUI"),Trait("Source","Pattern")]
    public async Task ClearRunHistoryCommand_DeletesHistoryAndResetsDisplay(){
        var fixture = await ViewModelFixture.CreateAsync(global::UI.BenchmarkSourceTab.SystemArray);
        var viewModel = fixture.ViewModel;
        viewModel.SelectAllCommand.Execute(null);
        var targets = GetSelectedCells(viewModel);

        await viewModel.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.ResultItems.Count>0&&targets.All(IsFinalCompleted));
        await SaveCurrentRunAsync(viewModel);

        Assert.True(viewModel.ClearRunHistoryCommand.CanExecute(null));
        await viewModel.ClearRunHistoryCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.RunOptions.Count==1
            &&viewModel.SelectedRunOption?.DisplayName=="新規"
            &&viewModel.ResultItems.Count==0
            &&targets.All(static cell => cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.Waiting));

        Assert.Single(viewModel.RunOptions);
        Assert.Equal("新規",viewModel.SelectedRunOption?.DisplayName);
        Assert.Empty(viewModel.ResultItems);
        Assert.All(targets,cell => Assert.Equal(global::UI.BenchmarkStatusImageNames.Waiting,cell.StatusImageSource));
    }
#endif

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
            Assert.Contains(viewModel.BenchmarkRows,row => row.HasCpuDisplay);
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
    public async Task 一時停止後に計測すると再実行されtrueを渡す(){
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
            //一時停止後の計測が実際に実行を開始・完了することを確認する
            Assert.True(viewModel.RunCommand.CanExecute(null));
            var targets = GetSelectedCells(viewModel);
            var running = TrackRunningIcons(targets);
            await viewModel.RunCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => targets.All(IsFinalCompleted));
            Assert.All(targets,cell => Assert.Contains(cell,running));
        }
        {
            //新仕様では再開導線を使わず、計測は再実行としてrestartFromRequestedElementCount=trueで渡ることを確認する
            Assert.Equal([true,true],fixture.RunService.ObservedRestartFromRequestedElementCount);
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

    private static IReadOnlyList<global::UI.BenchmarkProjectCell> GetSelectedCells(global::UI.BenchmarkSourceTabViewState state) =>
        state.Rows
            .SelectMany(static row => row.ProjectCells)
            .Where(static cell => cell.HasCase&&cell.IsSelected)
            .ToArray();

    private static void AssertNewGridCatalogDisplay(global::UI.ViewModels.RunnerViewModel viewModel){
        Assert.NotEmpty(viewModel.BenchmarkRows);
        Assert.Empty(viewModel.ResultItems);
        Assert.Empty(viewModel.VisibleResultItems);
        Assert.Empty(viewModel.ResultElementCountOptions);
        Assert.Empty(GetSelectedCells(viewModel));
        var cells = viewModel.BenchmarkRows
            .SelectMany(static row => row.ProjectCells)
            .Where(static cell => cell.HasCase)
            .ToArray();
        Assert.NotEmpty(cells);
        Assert.All(cells,static cell => {
            Assert.False(cell.IsSelected);
            Assert.False(cell.HasCpuDisplay);
            Assert.True(string.IsNullOrEmpty(cell.StatusImageSource)||cell.StatusImageSource==global::UI.BenchmarkStatusImageNames.Waiting);
        });
    }

    private static void AssertLoadedRowHasPairMetrics(global::UI.BenchmarkTableRow row){
        Assert.False(string.IsNullOrWhiteSpace(row.LeftTimeText),"Left time was not restored for "+row.RowKey);
        Assert.False(string.IsNullOrWhiteSpace(row.RightTimeText),"Right time was not restored for "+row.RowKey);
        Assert.True(row.LeftCell.Metrics.HasMeasuredValue,"Left metrics were not restored for "+row.RowKey);
        Assert.True(row.RightCell.Metrics.HasMeasuredValue,"Right metrics were not restored for "+row.RowKey);
    }

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

    private static bool HasLoadedCellMetadata(global::UI.BenchmarkProjectCell cell) =>
        !string.IsNullOrWhiteSpace(cell.ElementCountText)
        &&!string.IsNullOrWhiteSpace(cell.ExecutionText)
        &&cell.HasCpuDisplay
        &&!string.IsNullOrWhiteSpace(cell.LogicalProcessorText);

    private static string CreateCellMetadataMessage(global::UI.ViewModels.RunnerViewModel viewModel,global::UI.BenchmarkProjectCell cell) =>
        "Cell metadata was not restored. ElementCount='"+cell.ElementCountText
        +"' Execution='"+cell.ExecutionText
        +"' HasCpu='"+cell.HasCpuDisplay
        +"' Cpu='"+cell.LogicalProcessorText
        +"' Time='"+cell.TimeText
        +"' Memory='"+cell.AllocatedText
        +"' Status='"+cell.StatusImageSource
        +"' CellProject='"+cell.Project
        +"' RowMethod='"+(viewModel.BenchmarkRows.FirstOrDefault(row => row.ProjectCells.Contains(cell))?.MethodName??"-")
        +"' RowType='"+(viewModel.BenchmarkRows.FirstOrDefault(row => row.ProjectCells.Contains(cell))?.BenchmarkTypeText??"-")
        +"' RowKey='"+(viewModel.BenchmarkRows.FirstOrDefault(row => row.ProjectCells.Contains(cell))?.RowKey??"-")
        +"' RowLeftTime='"+(viewModel.BenchmarkRows.FirstOrDefault(row => row.ProjectCells.Contains(cell))?.LeftTimeText??"-")
        +"' RowRightTime='"+(viewModel.BenchmarkRows.FirstOrDefault(row => row.ProjectCells.Contains(cell))?.RightTimeText??"-")
        +"' Results='"+string.Join(";",viewModel.ResultItems.Take(8).Select(static item => item.Project+"/"+item.Method+"/Status="+item.Status+"/N="+item.AxisCount+"/CPU="+(item.AssignedLogicalCpu?.ToString()??"-")+"/Mean="+(item.MeanNanoseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")))+"'";

    private static async Task<global::UI.BenchmarkUiSnapshot> WaitForPipelineSnapshotAsync(global::UI.BenchmarkRunEventPipeline pipeline){
        var metrics = new global::UI.BenchmarkPipelineMetrics(0,0,0,0,0,0,0,0,0,0,0,0,0,0);
        for(var index=0;index<50;index++){
            if(pipeline.TryCreateUiSnapshot(metrics,out var snapshot)){
                return snapshot;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Pipeline snapshot was not produced.");
    }

    private static global::UI.BenchmarkTableRow CreateArrayBenchmarkRow(string methodName){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var cases = new Dictionary<string,global::UI.BenchmarkCase>(StringComparer.OrdinalIgnoreCase) {
            [sourceTab.LeftProject]=new(sourceTab.LeftProject,sourceTab.LeftAssemblyName,CreateArrayTypeFullName(sourceTab.LeftProject),methodName),
            [sourceTab.RightProject]=new(sourceTab.RightProject,sourceTab.RightAssemblyName,CreateArrayTypeFullName(sourceTab.RightProject),methodName),
        };
        return new global::UI.BenchmarkTableRow("int|"+methodName,methodName,methodName,sourceTab,cases,"int");
    }

    private static global::UI.BenchmarkResultItem CreateArrayResult(
        string project,
        string methodName,
        global::UI.JobStatus status,
        int runItemIndex){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var now = DateTimeOffset.UtcNow;
        return new global::UI.BenchmarkResultItem(new global::UI.BenchmarkJob{
            Project=project,
            AssemblyName=sourceTab.GetAssemblyName(project),
            TypeFullName=CreateArrayTypeFullName(project),
            MethodName=methodName,
            SourceTab=sourceTab.Tab,
            ElementCount=4096,
            LoopCount=global::UI.BenchmarkWorkloadAxis.DefaultLoopCount,
            Status=status,
            AssignedLogicalCpu=0,
            StartedAt=now,
            FinishedAt=status==global::UI.JobStatus.Completed ? now : null,
            MeanNanoseconds=status==global::UI.JobStatus.Completed ? 1_000_000 : null,
            AllocatedBytes=status==global::UI.JobStatus.Completed ? 1024 : null,
            ExitCode=status==global::UI.JobStatus.Completed ? 0 : null,
            RunItemIndex=runItemIndex,
        });
    }

    private static string CreateArrayTypeFullName(string project) =>
        project.Contains("ZLinq",StringComparison.OrdinalIgnoreCase)
            ? "DotNet.Linq.Enumerable.ZLinq.ZLinqInt32"
            : "DotNet.Linq.Enumerable.FLinq.FLinqInt32";

    private static async Task SaveCurrentRunAsync(global::UI.ViewModels.RunnerViewModel viewModel){
        if(viewModel.SelectedRunOption is { UiRunId:>0 }&&!viewModel.IsRefreshing&&viewModel.CanSelectRunHistory){
            return;
        }

        await WaitUntilAsync(() => viewModel.CanSaveDatabase&&viewModel.SaveDatabaseCommand.CanExecute(null));
        await viewModel.SaveDatabaseCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.SelectedRunOption is { UiRunId:>0 }
            &&!viewModel.IsRefreshing
            &&viewModel.CanSelectRunHistory);
    }

    private static async Task WaitUntilAsync(Func<bool> condition){
        for(var index=0;index<60;index++){
            if(condition()){
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition());
    }

    private static async Task WaitUntilOrContinueAsync(Func<bool> condition){
        for(var index=0;index<60;index++){
            if(condition()){
                return;
            }

            await Task.Delay(50);
        }
    }

    private sealed class ViewModelFixture{
        private ViewModelFixture(global::UI.ViewModels.RunnerViewModel viewModel,FakeRunService runService,UiRunStore store){
            this.ViewModel=viewModel;
            this.RunService=runService;
            this.Store=store;
        }

        public global::UI.ViewModels.RunnerViewModel ViewModel { get; }
        public FakeRunService RunService { get; }
        public UiRunStore Store { get; }

        public static async Task<ViewModelFixture> CreateAsync(global::UI.BenchmarkSourceTab sourceTab,Action<UiRunStore>? configureStore = null){
            var cases = CreateCatalogCases();
            var storePath = Path.Combine(Path.GetTempPath(),"Seamless-Test-Benchmark-UI-"+Guid.NewGuid().ToString("N")+".db");
            var store = new UiRunStore(storePath);
            store.EnsureSchema();
            configureStore?.Invoke(store);
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
            viewModel.SelectedExecutionProfile=viewModel.ExecutionProfiles.FirstOrDefault(option => option.Profile==global::UI.BenchmarkExecutionProfile.Test)
                ??viewModel.ExecutionProfiles.First();
            viewModel.SelectedThreshold=viewModel.ThresholdOptions.First(option => option.Milliseconds==1);
            viewModel.ClearSelectionCommand.Execute(null);
            return new(viewModel,runService,store);
        }

        private static IReadOnlyList<global::UI.BenchmarkCase> CreateCatalogCases(){
            var array = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
            var enumerable = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemRange);
            var list = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemList);
            return [
                CreateCase(array.LeftProject,array.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                CreateCase(array.RightProject,array.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","AggregateBy_keySelector"),
                CreateCase(array.LeftProject,array.LeftAssemblyName,"DotNet.Linq.Enumerable.FLinqInt32","Select_selector"),
                CreateCase(array.RightProject,array.RightAssemblyName,"DotNet.Linq.Enumerable.ZLinqInt32","Select_selector"),
                CreateCase(enumerable.LeftProject,enumerable.LeftAssemblyName,"DotNet.Linq.Enumerable.Range.FLinqInt32","AggregateBy_keySelector"),
                CreateCase(enumerable.RightProject,enumerable.RightAssemblyName,"DotNet.Linq.Enumerable.Range.ZLinqInt32","AggregateBy_keySelector"),
                CreateCase(enumerable.LeftProject,enumerable.LeftAssemblyName,"DotNet.Linq.Enumerable.Range.FLinqInt32","Select_selector"),
                CreateCase(enumerable.RightProject,enumerable.RightAssemblyName,"DotNet.Linq.Enumerable.Range.ZLinqInt32","Select_selector"),
                CreateCase(list.LeftProject,list.LeftAssemblyName,"DotNet.Linq.Enumerable.List.FLinqInt32","AggregateBy_keySelector"),
                CreateCase(list.RightProject,list.RightAssemblyName,"DotNet.Linq.Enumerable.List.ZLinqInt32","AggregateBy_keySelector"),
                CreateCase(list.LeftProject,list.LeftAssemblyName,"DotNet.Linq.Enumerable.List.FLinqInt32","Select_selector"),
                CreateCase(list.RightProject,list.RightAssemblyName,"DotNet.Linq.Enumerable.List.ZLinqInt32","Select_selector"),
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
        public List<int> ObservedBenchmarkCounts { get; } = [];
        public bool EmitAllElementCounts { get; set; }

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
            this.ObservedBenchmarkCounts.Add(benchmarks.Count);
            var emitCases = this.CreateEmitCases(benchmarks,sourceTab);
            var logicalCpu = logicalCpus.FirstOrDefault();
            var preparedElementCounts = this.EmitAllElementCounts&&elementCounts.Count>0 ? elementCounts.ToArray() : [1];
            var emittedElementCounts = preparedElementCounts;
            foreach(var elementCount in emittedElementCounts){
                foreach(var benchmark in emitCases){
                    var benchmarkSourceTab = ResolveSourceTab(benchmark,sourceTab);
                    cancellationToken.ThrowIfCancellationRequested();
                    resultReceived(CreateResult(benchmark,benchmarkSourceTab,global::UI.JobStatus.Running,logicalCpu,null,elementCount,preparedElementCounts));
                    if(executionOptions?.UseInfiniteTestRunner==true){
                        await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);
                    }
                    resultReceived(CreateResult(benchmark,benchmarkSourceTab,global::UI.JobStatus.Completed,logicalCpu,adaptiveThreshold.TotalMilliseconds*1_000_000,elementCount,preparedElementCounts));
                }
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

        private static global::UI.BenchmarkSourceTab ResolveSourceTab(global::UI.BenchmarkCase benchmark,global::UI.BenchmarkSourceTab fallback) =>
            global::UI.BenchmarkSourceTabs.TryGetByProjectAndAssembly(benchmark.Project,benchmark.AssemblyName)?.Tab??fallback;

        private static global::UI.BenchmarkResultItem CreateResult(
            global::UI.BenchmarkCase benchmark,
            global::UI.BenchmarkSourceTab sourceTab,
            global::UI.JobStatus status,
            int logicalCpu,
            double? meanNanoseconds,
            int elementCount = 1,
            IReadOnlyList<int>? preparedElementCounts = null){
            var job = new global::UI.BenchmarkJob{
                Project=benchmark.Project,
                AssemblyName=benchmark.AssemblyName,
                TypeFullName=benchmark.TypeFullName,
                MethodName=benchmark.MethodName,
                ArgumentKey=benchmark.ArgumentKey,
                Arguments=benchmark.Arguments.ToList(),
                SourceTab=sourceTab,
                ElementCount=global::UI.BenchmarkWorkloadAxis.ResolveElementCount(sourceTab,elementCount),
                LoopCount=global::UI.BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab,elementCount),
                PreparedElementCounts=preparedElementCounts?.ToList()??[elementCount],
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
        private readonly object gate = new();

        public void BeginInvoke(Action action) {
            lock(this.gate){
                action();
            }
        }
    }

    private sealed class InMemoryPreferences:global::UI.IBenchmarkUiPreferences{
        private readonly Dictionary<string,string> values = new(StringComparer.OrdinalIgnoreCase);

        public string Get(string key,string defaultValue) =>
            this.values.TryGetValue(key,out var value) ? value : defaultValue;

        public void Set(string key,string value) => this.values[key]=value;
    }
}
#endif
