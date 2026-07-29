using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Collections.Concurrent;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicEndToEndTests{
    [Fact,Trait("Category","Integration")]
    public async Task 全8ソースをWorker実行してDBとUI行へ保存する(){
        var temporaryRoot=DynamicDatabase.一時ルートを作成();
        var databasePath=Path.Combine(temporaryRoot,DynamicInfrastructureSchema.DatabaseFileName);
        var workerPath=Path.Combine(AppContext.BaseDirectory,"Benchmark.Dynamic.Worker.exe");
        var runID="e2e-"+Guid.NewGuid().ToString("N");
        try{
            Assert.True(File.Exists(workerPath),$"Worker was not found: {workerPath}");
            var rows=new ConcurrentBag<global::Benchmark.Dynamic.動的結果行>();
            var progress=new ConcurrentBag<global::Benchmark.Dynamic.動的実行進捗>();
            await using(var service=new global::Benchmark.Dynamic.動的実行調停サービス(
                databasePath,
                workerPath,
                new WorkerMeasurementProfile("E2E-v1",0,1,0,1))){
                var history=await service.実行Async(
                    new global::Benchmark.Dynamic.動的実行設定(
                        runID,
                        1,
                        1,
                        8,
                        1,
                        Seed256.Parse("0x5EED"),
                        OperatorSetMode.Common,
                        Enum.GetValues<SourceTarget>()),
                    rows.Add,
                    progress.Add);
                Assert.Equal("Completed",history.状態);
            }

            var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(databasePath);
            await using var writer=new DynamicSingleWriterQueue(database);
            var repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer);
            var snapshot=repository.GetRunSnapshot(runID);
            Assert.NotNull(snapshot);
            Assert.Equal("Completed",snapshot.Run.Status);
            Assert.Equal(8,snapshot.Sources.Count);
            Assert.Single(snapshot.Queries);
            Assert.Equal(8,snapshot.SourceExecutions.Count);
            Assert.All(snapshot.SourceExecutions,static execution=>Assert.Equal("Succeeded",execution.Status));
            Assert.Equal(8,snapshot.Validations.Count);
            Assert.All(snapshot.Validations,static validation=>Assert.Equal("Match",validation.Status));
            Assert.Equal(24,snapshot.Measurements.Count);
            Assert.Equal(
                ["FLinq","System","ZLinq"],
                snapshot.Measurements.Select(static measurement=>measurement.ImplementationKind).Distinct().Order().ToArray());
            Assert.Equal(24,snapshot.RawSamples.Count);
            Assert.Equal(8,rows.Count);
            Assert.All(rows,static row=>{
                Assert.Equal("Succeeded",row.状態);
                Assert.Equal("一致",row.意味一致);
                Assert.Equal("到達",row.FixedPoint);
            });
            Assert.Contains(progress,static item=>item.完了数==8);
        }finally{
            DynamicDatabase.一時ルートを削除(temporaryRoot);
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task キャンセル時に処理中と未開始のSourceExecutionをDBへ保持する(){
        var temporaryRoot=DynamicDatabase.一時ルートを作成();
        var databasePath=Path.Combine(temporaryRoot,DynamicInfrastructureSchema.DatabaseFileName);
        var workerPath=Path.Combine(AppContext.BaseDirectory,"Benchmark.Dynamic.Worker.exe");
        var runID="cancel-"+Guid.NewGuid().ToString("N");
        try{
            var sourceStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using(var service=new global::Benchmark.Dynamic.動的実行調停サービス(
                databasePath,
                workerPath,
                new WorkerMeasurementProfile("Cancel-v1",2,7,25,1_048_576))){
                var runTask=service.実行Async(
                    new global::Benchmark.Dynamic.動的実行設定(
                        runID,
                        3,
                        8,
                        1_024,
                        8,
                        Seed256.Parse("0xCA11CE1"),
                        OperatorSetMode.Common,
                        [SourceTarget.SystemArray,SourceTarget.SystemList]),
                    _=>{},
                    _=>sourceStarted.TrySetResult());
                await sourceStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await service.キャンセルAsync();
                var history=await runTask.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal("Canceled",history.状態);
            }

            var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(databasePath);
            await using var writer=new DynamicSingleWriterQueue(database);
            var repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer);
            var snapshot=repository.GetRunSnapshot(runID);
            Assert.NotNull(snapshot);
            Assert.Equal("Canceled",snapshot.Run.Status);
            Assert.Equal(3,snapshot.Queries.Count);
            Assert.Equal(6,snapshot.SourceExecutions.Count);
            Assert.Contains(snapshot.SourceExecutions,static execution=>execution.Status=="NotStarted");
            Assert.All(snapshot.SourceExecutions,static execution=>
                Assert.Contains(execution.Status,new[]{"Succeeded","Canceled","NotStarted"}));
        }finally{
            DynamicDatabase.一時ルートを削除(temporaryRoot);
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task 保存IRでRun全体とQuery単体を子Runとして再実行する(){
        var temporaryRoot=DynamicDatabase.一時ルートを作成();
        var databasePath=Path.Combine(temporaryRoot,DynamicInfrastructureSchema.DatabaseFileName);
        var workerPath=Path.Combine(AppContext.BaseDirectory,"Benchmark.Dynamic.Worker.exe");
        var parentRunID="replay-parent-"+Guid.NewGuid().ToString("N");
        try{
            await using var service=new global::Benchmark.Dynamic.動的実行調停サービス(
                databasePath,
                workerPath,
                new WorkerMeasurementProfile("Replay-v1",0,1,0,1));
            var parentHistory=await service.実行Async(
                new global::Benchmark.Dynamic.動的実行設定(
                    parentRunID,
                    1,
                    1,
                    8,
                    1,
                    Seed256.Parse("0xBEEF"),
                    OperatorSetMode.Common,
                    [SourceTarget.SystemArray]),
                _=>{},
                _=>{});
            Assert.Equal("Completed",parentHistory.状態);

            var runReplay=await service.Runを再実行Async(parentRunID,_=>{},_=>{});
            var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(databasePath);
            await using var writer=new DynamicSingleWriterQueue(database);
            var repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer);
            var parent=repository.GetRunSnapshot(parentRunID);
            var replayedRun=repository.GetRunSnapshot(runReplay.実行ID);
            Assert.NotNull(parent);
            Assert.NotNull(replayedRun);
            Assert.Equal(parentRunID,replayedRun.Run.ReplayedFromRunID);
            Assert.Equal(parent.Queries[0].QueryID,replayedRun.Queries[0].ReplayedFromQueryID);
            Assert.Equal(parent.Queries[0].CanonicalIRJSON,replayedRun.Queries[0].CanonicalIRJSON);
            Assert.Equal(parent.Queries[0].QueryHash,replayedRun.Queries[0].QueryHash);
            Assert.Equal(3,replayedRun.Measurements.Count);

            var queryReplay=await service.Queryを再実行Async(parent.Queries[0].QueryID,_=>{},_=>{});
            var replayedQuery=repository.GetRunSnapshot(queryReplay.実行ID);
            Assert.NotNull(replayedQuery);
            Assert.Equal(parentRunID,replayedQuery.Run.ReplayedFromRunID);
            Assert.Equal(parent.Queries[0].QueryID,replayedQuery.Queries[0].ReplayedFromQueryID);
            Assert.Equal(parent.Queries[0].CanonicalIRJSON,replayedQuery.Queries[0].CanonicalIRJSON);
            Assert.Equal(3,replayedQuery.Measurements.Count);
        }finally{
            DynamicDatabase.一時ルートを削除(temporaryRoot);
        }
    }
}
