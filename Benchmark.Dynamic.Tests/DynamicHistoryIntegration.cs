using Benchmark.Dynamic.Infrastructure;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;
using Benchmark.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicHistoryIntegration{
    [Theory]
    [InlineData("Memory")]
    [InlineData("Temporary")]
    public async Task ActualStoreGrowthIsSavedWithLiveMeasurement(string kind){
        var directory=DynamicDatabase.一時ルートを作成();
        try{
            var databasePath=Path.Combine(directory,"history.db");
            var ir=CorpusChainBuilder.Build(new("growth-history","test",["Where","Count"]),SourceTarget.SeamlessTsavorite,4096);
            var request=new WorkerRunRequest("growth-history",Seed256.Parse("42"),1,2,2,4096,OperatorSetMode.Maximum,[SourceTarget.SeamlessTsavorite],
                new("test",0,1,0,1),RandomQueryGenerator.GeneratorVersion,QueryCapabilityProfile.CurrentProfileVersion.ToString(),BuiltInOperatorCatalog.OperatorSetVersion.ToString(),StorageSettings:new(Kind:kind));
            var completed=await new DynamicWorkerExecutionRunner().ExecuteAsync(request,ir,0,SourceTarget.SeamlessTsavorite,0,CancellationToken.None);
            Assert.True(completed.Status==WorkerExecutionStatus.Succeeded,completed.DiagnosticsJson);
            await using(var recorder=new DynamicHistoryRecorder(databasePath)){
                await recorder.BeginAsync(request);
                await recorder.RecordAsync(new(request.RunId,ir.QueryId,0,ir.Seed,QueryIrJson.Serialize(ir),0),completed);
                await recorder.CompleteAsync("Completed");
            }
            var repository=new BenchmarkRepository(new BenchmarkDatabase(databasePath));
            var changes=repository.GetChanges(request.RunId);
            Assert.NotEmpty(changes);
            Assert.Contains(changes,change=>change.Applied&&change.TransitionNanoseconds>=0);
            Assert.All(changes,change=>Assert.Equal(request.RunId,change.RunID));
            var measurements=repository.GetMeasurements(request.RunId);
            Assert.Equal(4,measurements.Count);
            Assert.All(measurements,measurement=>Assert.Equal(kind,measurement.Conditions.StoreKind));
            string? storageDirectory=null;
            if(kind=="Temporary"){storageDirectory=Path.GetTempPath();}
            Assert.All(measurements,measurement=>Assert.Equal(Seamless.Execution.ProfileEnvironment.Storage(storageDirectory),measurement.Conditions.StorageKey));
            var parameters=System.Text.Json.JsonSerializer.Deserialize<ProfileParameters>(measurements[0].Conditions.ParametersJSON)!;
            Assert.Equal(64,parameters.Initial!.IndexBytes);Assert.True(parameters.Effective.IndexBytes>64);
        }finally{Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task DetailedAndCommonCheckpointCommitOrRollbackTogether(){
        var directory=DynamicDatabase.一時ルートを作成();
        try{
            var path=Path.Combine(directory,"history.db");
            var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(path);
            database.InitializeIfMissing();
            using(var connection=database.OpenWritableConnection()){
                using var command=connection.CreateCommand();
                command.CommandText="CREATE TRIGGER fail_common BEFORE INSERT ON BenchmarkMeasurements BEGIN SELECT RAISE(ABORT,'forced projection failure');END;";
                command.ExecuteNonQuery();
            }
            var now=DateTimeOffset.UtcNow;
            var run=new DynamicRunRecord("atomic","Completed",1,"Balanced-v1","{}",now,now);
            var query=new DynamicQueryRecord("query",run.RunID,0,"SystemArray",1,"{}","source","hash","Succeeded");
            var execution=new DynamicSourceExecutionRecord("execution",query.QueryID,"SystemArray",1,0,"Succeeded",ResultDigest:"digest");
            var measurement=new DynamicMeasurementBundle(new("measurement",execution.ExecutionID,"System",1,1),[new("measurement","Measurement",0,10,1,0,0,0,0,now)]);
            var checkpoint=new DynamicRunCheckpoint(run,Queries:[query],SourceExecutions:[execution],Measurements:[measurement]);
            await using(var writer=new DynamicSingleWriterQueue(database)){
                var repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer);
                await Assert.ThrowsAsync<SqliteException>(()=>repository.SaveCheckpointAsync(checkpoint));
                using(var connection=database.OpenWritableConnection()){
                    using var command=connection.CreateCommand();
                    command.CommandText="SELECT (SELECT COUNT(*) FROM DynamicRuns)+(SELECT COUNT(*) FROM BenchmarkRuns);";
                    Assert.Equal(0L,command.ExecuteScalar());
                    command.CommandText="DROP TRIGGER fail_common;";command.ExecuteNonQuery();
                }
                await repository.SaveCheckpointAsync(checkpoint);
            }
            var history=new BenchmarkRepository(new BenchmarkDatabase(path));
            Assert.Equal("Passed",Assert.Single(history.GetRuns()).Status);
            Assert.False(Assert.Single(history.GetMeasurements(run.RunID)).Conditions.Known);
        }finally{Directory.Delete(directory,true);}
    }
}
