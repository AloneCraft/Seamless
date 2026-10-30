using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Infrastructure;

/// <summary>Workerの完了イベントを通常実行と共通のチェックポイント経路へ保存する。</summary>
public sealed class DynamicHistoryRecorder:IAsyncDisposable{
    private readonly DynamicSingleWriterQueue writer;
    private readonly DynamicRepository repository;
    private DynamicRunRecord run=null!;
    private WorkerRunRequest request=null!;
    public DynamicHistoryRecorder(string databasePath){
        var database=new DynamicDatabase(databasePath);
        this.writer=new(database);
        this.repository=new(database,this.writer);
    }
    public async Task BeginAsync(WorkerRunRequest request,CancellationToken cancellationToken=default){
        this.request=request;
        var now=DateTimeOffset.UtcNow;
        this.run=new(request.RunId,"Running",0,request.MeasurementProfile.ProfileId,JsonSerializer.Serialize(request,DynamicWorkerProtocol.JsonOptions),now,now,StartedAt:now);
        await this.repository.SaveRunPlanAsync(this.run,request.SourceTargets.Select((source,index)=>new DynamicRunSourceRecord(request.RunId,source.ToString(),true,index)).ToArray(),[],cancellationToken);
    }
    public async Task RecordAsync(WorkerQueryStartedEvent query,WorkerSourceExecutionCompletedEvent result,CancellationToken cancellationToken=default){
        var ir=QueryIrJson.Deserialize(query.IrJson);
        var executionID=$"{result.QueryId}-{result.SourceTarget}-a1";
        var queryRecord=new DynamicQueryRecord(result.QueryId,this.run.RunID,result.QueryIndex,"live",0,query.IrJson,result.SourceText,QueryIrJson.ComputeHash(ir),result.Status.ToString(),IrSchemaVersion:ir.IrSchemaVersion,OperatorCount:ir.ActualOperatorCount,Depth:ir.ActualDepth,ElementType:ir.ElementType.ToString());
        var execution=new DynamicSourceExecutionRecord(executionID,result.QueryId,result.SourceTarget.ToString(),1,result.QueryIndex,result.Status.ToString(),CompletedAt:result.CompletedAtUtc,ResultDigest:DynamicResultDigest.Compute(result.ResultJson),Error:result.DiagnosticsJson??"",Partial:result.Status==WorkerExecutionStatus.Canceled,GeneratedSource:result.SourceText,AssemblyUnloaded:result.AssemblyUnloaded);
        var measurements=new List<DynamicMeasurementBundle>();
        if(!string.IsNullOrEmpty(result.MeasurementsJson)){
            using var artifact=JsonDocument.Parse(result.MeasurementsJson);
            if(artifact.RootElement.TryGetProperty("measurement",out var container)&&container.ValueKind==JsonValueKind.Object&&container.TryGetProperty("measurements",out var items)){
                foreach(var item in items.EnumerateArray()){
                    var id=Guid.NewGuid().ToString("N");
                    var samples=new List<DynamicRawSampleRecord>();
                    foreach(var sample in item.GetProperty("samples").EnumerateArray()){
                        samples.Add(new(id,"Measurement",sample.GetProperty("sampleIndex").GetInt32(),sample.GetProperty("elapsedNanoseconds").GetDouble(),sample.GetProperty("operationCount").GetInt64(),sample.GetProperty("allocatedBytes").GetInt64(),sample.GetProperty("gen0Collections").GetInt64(),sample.GetProperty("gen1Collections").GetInt64(),sample.GetProperty("gen2Collections").GetInt64(),result.CompletedAtUtc));
                    }
                    var name=item.GetProperty("implementation").GetString()!;
                    var implementation=new[]{"System","FLinqUnoptimized","FLinq","ZLinq","Ideal"}.Single(kind=>kind.Equals(name,StringComparison.OrdinalIgnoreCase));
                    measurements.Add(new(new(id,executionID,implementation,item.GetProperty("operationCount").GetInt64(),samples.Count),samples));
                }
            }
        }
        var metadata=new DynamicMeasurementConditions(result.InputHash??"",result.EnvironmentJSON??"",result.ParametersJSON??"{}",result.GrowthJSON??"{}",result.StoreKind??"Unknown",this.request.OptimizationFlags.ToString(),result.CodeFingerprint??"",result.StorageChangesJSON,result.IO,result.StorageKey);
        var diagnostic=new DynamicDiagnosticRecord(Guid.NewGuid().ToString("N"),executionID,"MeasurementConditions",JsonSerializer.Serialize(metadata),result.CompletedAtUtc);
        await this.repository.SaveCheckpointAsync(new(this.run with{UpdatedAt=result.CompletedAtUtc},Queries:[queryRecord],SourceExecutions:[execution],Measurements:measurements,Diagnostics:[diagnostic]),cancellationToken);
    }
    public Task CompleteAsync(string status,CancellationToken cancellationToken=default){
        this.run=this.run with{Status=status,UpdatedAt=DateTimeOffset.UtcNow,CompletedAt=DateTimeOffset.UtcNow};
        return this.repository.SaveRunAsync(this.run,cancellationToken);
    }
    public ValueTask DisposeAsync()=>this.writer.DisposeAsync();
}
