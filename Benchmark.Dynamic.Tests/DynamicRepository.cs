using Benchmark.Dynamic.Infrastructure;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicRepository {
    [Fact,Trait("Category","Integration")]
    public async Task SaveCheckpointAsync() {
        var 一時ルート=DynamicDatabase.一時ルートを作成();
        try {
            var DB=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(Path.Combine(一時ルート,"Benchmark.Dynamic.db"));
            await using var Writer=new DynamicSingleWriterQueue(DB);
            var Repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(DB,Writer);
            var 現在=DateTimeOffset.UtcNow;
            var Run=new DynamicRunRecord(
                "run-1",
                "Canceled",
                1234,
                "Balanced-v1",
                "{\"n\":3}",
                現在,
                現在,
                AppVersion:"1",
                WorkerVersion:"1",
                StartedAt:現在,
                CancelledAt:現在);
            var Source=new DynamicRunSourceRecord("run-1","SystemArray",true,0);
            var Query=new DynamicQueryRecord(
                "query-1",
                "run-1",
                0,
                "SystemArray",
                42,
                "{\"ir\":1}",
                "source",
                "hash",
                "Canceled",
                IrSchemaVersion:1,
                OperatorCount:3,
                Depth:2,
                ResultType:"Int",
                OrderSemantics:"TotalOrdered",
                IrGenerationNanoseconds:123);
            var Execution=new DynamicSourceExecutionRecord(
                "execution-1",
                "query-1",
                "SystemArray",
                1,
                0,
                "Canceled",
                LoopCount:8,
                WarmupCount:2,
                StartedAt:現在,
                CompletedAt:現在,
                ResultDigest:"digest",
                Partial:true,
                CapabilityProfileID:"profile-id",
                CapabilityProfileJSON:"{\"profileVersion\":14}",
                GeneratedSource:"internal static class GeneratedQuery {}",
                SourceHash:"source-hash",
                SourceEmissionNanoseconds:10,
                SourceTextNanoseconds:20,
                SyntaxTreeNanoseconds:30,
                CompilationNanoseconds:40,
                GeneratorNanoseconds:50,
                EmitNanoseconds:60,
                LoadNanoseconds:70,
                BindNanoseconds:80,
                RewriteSummaryJSON:"{\"rewriteCount\":2,\"reachedFixedPoint\":true}",
                FixedPointTermination:"FixedPoint",
                AssemblyUnloaded:true);
            var Validation=new DynamicValidationRecord(
                "validation-1",
                "execution-1",
                "System",
                "Success",
                現在,
                "summary",
                "{\"value\":1}",
                "null",
                "[]");
            var Measurement=new DynamicMeasurementRecord(
                "measurement-1",
                "execution-1",
                "System",
                8,
                1,
                MedianNanoseconds:100,
                MeanNanoseconds:100,
                MinimumNanoseconds:100,
                MaximumNanoseconds:100,
                StandardDeviationNanoseconds:0,
                StandardErrorNanoseconds:0,
                AllocatedBytesPerOperation:12,
                StartedAt:現在,
                CompletedAt:現在);
            var Sample=new DynamicRawSampleRecord(
                "measurement-1",
                "measure",
                0,
                800,
                8,
                96,
                0,
                0,
                0,
                現在);
            var Diagnostic=new DynamicDiagnosticRecord(
                "diagnostic-1",
                "execution-1",
                "Compile",
                "{}",
                現在,
                TextValue:"ok");
            var Optimization=new DynamicOptimizationEventRecord(
                "optimization-1",
                "query-1",
                "SystemArray",
                0,
                "Rewrite",
                "{}",
                現在,
                ExecutionID:"execution-1",
                Layer:"Syntax",
                Phase:"Algebraic",
                RuleID:"CountComparisonToAny",
                Round:2,
                Pass:3,
                Visit:4,
                NodeID:"S1:/node[0]",
                NodePath:"/node[0]",
                Depth:1,
                ParentKind:"InvocationExpression",
                IsLeft:true,
                IsInner:true,
                Before:"source.Count()>0",
                After:"source.Any()",
                Changed:true,
                Revisited:true);
            var RunEvent=new DynamicRunEventRecord(
                "event-1",
                "run-1",
                0,
                "Info",
                "Canceled",
                "canceled",
                "{}",
                現在,
                "query-1",
                "execution-1");

            await Repository.SaveCheckpointAsync(new DynamicRunCheckpoint(
                Run,
                [Source],
                [Query],
                [Execution],
                [Validation],
                [new DynamicMeasurementBundle(Measurement,[Sample])],
                [Diagnostic],
                [Optimization],
                [RunEvent]));
            await Writer.FlushAsync();

            var Snapshot=Repository.GetRunSnapshot("run-1");
            Assert.NotNull(Snapshot);
            Assert.Single(Snapshot.Sources);
            Assert.Single(Snapshot.Queries);
            Assert.Equal(3,Snapshot.Queries[0].OperatorCount);
            Assert.Equal(2,Snapshot.Queries[0].Depth);
            Assert.Equal(123,Snapshot.Queries[0].IrGenerationNanoseconds);
            Assert.Single(Snapshot.SourceExecutions);
            Assert.True(Snapshot.SourceExecutions[0].Partial);
            Assert.Equal("profile-id",Snapshot.SourceExecutions[0].CapabilityProfileID);
            Assert.Equal("{\"profileVersion\":14}",Snapshot.SourceExecutions[0].CapabilityProfileJSON);
            Assert.Equal("internal static class GeneratedQuery {}",Snapshot.SourceExecutions[0].GeneratedSource);
            Assert.Equal("source-hash",Snapshot.SourceExecutions[0].SourceHash);
            Assert.Equal(10,Snapshot.SourceExecutions[0].SourceEmissionNanoseconds);
            Assert.Equal(20,Snapshot.SourceExecutions[0].SourceTextNanoseconds);
            Assert.Equal(30,Snapshot.SourceExecutions[0].SyntaxTreeNanoseconds);
            Assert.Equal(40,Snapshot.SourceExecutions[0].CompilationNanoseconds);
            Assert.Equal(50,Snapshot.SourceExecutions[0].GeneratorNanoseconds);
            Assert.Equal(60,Snapshot.SourceExecutions[0].EmitNanoseconds);
            Assert.Equal(70,Snapshot.SourceExecutions[0].LoadNanoseconds);
            Assert.Equal(80,Snapshot.SourceExecutions[0].BindNanoseconds);
            Assert.Equal("{\"rewriteCount\":2,\"reachedFixedPoint\":true}",Snapshot.SourceExecutions[0].RewriteSummaryJSON);
            Assert.Equal("FixedPoint",Snapshot.SourceExecutions[0].FixedPointTermination);
            Assert.True(Snapshot.SourceExecutions[0].AssemblyUnloaded);
            Assert.Single(Snapshot.Validations);
            Assert.Single(Snapshot.Measurements);
            Assert.Single(Snapshot.RawSamples);
            Assert.Single(Snapshot.Diagnostics);
            Assert.Single(Snapshot.OptimizationEvents);
            Assert.Equal("execution-1",Snapshot.OptimizationEvents[0].ExecutionID);
            Assert.Equal("Syntax",Snapshot.OptimizationEvents[0].Layer);
            Assert.Equal("Algebraic",Snapshot.OptimizationEvents[0].Phase);
            Assert.Equal("CountComparisonToAny",Snapshot.OptimizationEvents[0].RuleID);
            Assert.Equal(2,Snapshot.OptimizationEvents[0].Round);
            Assert.Equal(3,Snapshot.OptimizationEvents[0].Pass);
            Assert.Equal(4,Snapshot.OptimizationEvents[0].Visit);
            Assert.Equal("S1:/node[0]",Snapshot.OptimizationEvents[0].NodeID);
            Assert.Equal("/node[0]",Snapshot.OptimizationEvents[0].NodePath);
            Assert.Equal(1,Snapshot.OptimizationEvents[0].Depth);
            Assert.Null(Snapshot.OptimizationEvents[0].StageIndex);
            Assert.True(Snapshot.OptimizationEvents[0].IsLeft);
            Assert.False(Snapshot.OptimizationEvents[0].IsRight);
            Assert.True(Snapshot.OptimizationEvents[0].IsInner);
            Assert.Equal("source.Count()>0",Snapshot.OptimizationEvents[0].Before);
            Assert.Equal("source.Any()",Snapshot.OptimizationEvents[0].After);
            Assert.True(Snapshot.OptimizationEvents[0].Changed);
            Assert.True(Snapshot.OptimizationEvents[0].Revisited);
            Assert.Single(Snapshot.RunEvents);

            Assert.True(await Repository.SoftDeleteRunAsync("run-1",現在,"test"));
            Assert.Empty(Repository.GetRuns());
            Assert.Single(Repository.GetRuns(trashOnly:true));
            Assert.True(await Repository.RestoreRunAsync("run-1",現在));
            Assert.Single(Repository.GetRuns());
            Assert.True(await Repository.SoftDeleteRunAsync("run-1",現在,"purge"));
            Assert.True(await Repository.PurgeRunAsync("run-1"));
            Assert.Null(Repository.GetRunSnapshot("run-1"));
        } finally {
            DynamicDatabase.一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task SaveRunEventAsync() {
        var 一時ルート=DynamicDatabase.一時ルートを作成();
        try {
            var DB=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(Path.Combine(一時ルート,"Benchmark.Dynamic.db"));
            await using var Writer=new DynamicSingleWriterQueue(DB);
            var Repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(DB,Writer);
            var 現在=DateTimeOffset.UtcNow;
            await Repository.SaveRunAsync(new DynamicRunRecord(
                "run-events",
                "Running",
                1,
                "Balanced-v1",
                "{}",
                現在,
                現在));
            var Tasks=Enumerable.Range(0,32).Select(Index=>Repository.SaveRunEventAsync(
                new DynamicRunEventRecord(
                    $"event-{Index:D2}",
                    "run-events",
                    Index,
                    "Info",
                    "Progress",
                    Index.ToString(),
                    "{}",
                    現在))).ToArray();
            await Task.WhenAll(Tasks);
            await Writer.FlushAsync();
            var Snapshot=Repository.GetRunSnapshot("run-events");
            Assert.NotNull(Snapshot);
            Assert.Equal(32,Snapshot.RunEvents.Count);
            Assert.Equal(Enumerable.Range(0,32),Snapshot.RunEvents.Select(Event=>Event.Sequence));
        } finally {
            DynamicDatabase.一時ルートを削除(一時ルート);
        }
    }
}
