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
                "Canceled");
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
                Partial:true);
            var Validation=new DynamicValidationRecord(
                "validation-1",
                "query-1",
                "SystemArray",
                "Match",
                現在,
                "expected",
                "actual");
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
                現在);
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
            Assert.Single(Snapshot.SourceExecutions);
            Assert.True(Snapshot.SourceExecutions[0].Partial);
            Assert.Single(Snapshot.Validations);
            Assert.Single(Snapshot.Measurements);
            Assert.Single(Snapshot.RawSamples);
            Assert.Single(Snapshot.Diagnostics);
            Assert.Single(Snapshot.OptimizationEvents);
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
