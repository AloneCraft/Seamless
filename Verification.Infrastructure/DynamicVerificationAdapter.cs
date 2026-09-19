using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using Verification.Core;
namespace Verification.Infrastructure;
public sealed class DynamicVerificationAdapter:IVerificationAdapter{
    public string Name=>"dynamic-worker";
    private readonly DotnetVerificationAdapter builder=new();
    public Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifacts,CancellationToken cancellationToken)=>this.builder.PrepareAsync(item,repositoryRoot,artifacts,cancellationToken);
    public async Task<CaseResult> ExecuteAsync(VerificationCase item,string root,string artifacts,CancellationToken cancellationToken){
        var request=ReadRequest(item) with{RunId=Guid.NewGuid().ToString("N")};
        var worker=Path.Combine(root,"Benchmark","Benchmark.Dynamic.Worker","bin","Release","net10.0","Benchmark.Dynamic.Worker.dll");
        await File.WriteAllTextAsync(Path.Combine(artifacts,"request.json"),JsonSerializer.Serialize(request,DynamicWorkerProtocol.JsonOptions),cancellationToken).ConfigureAwait(false);
        await using var session=new VersionedJsonLineWorkerProcessClient().Start(worker,workingDirectory:root);
        await using var output=new StreamWriter(Path.Combine(artifacts,"events.jsonl"),false,new UTF8Encoding(false)){AutoFlush=true};
        var status=CaseStatus.Interrupted;
        var samples=new List<RawMeasurement>();
        var times=new List<double>();
        string? digest=null,diagnostic="Worker exited without completing the case.";
        long peak=0;
        var completed=0;
        using var process=Process.GetProcessById(session.ProcessID);
        try{
            await session.SendAsync(request.RunId,WorkerMessageTypes.StartRun,request,cancellationToken).ConfigureAwait(false);
            await foreach(var envelope in session.ReadAllAsync(cancellationToken).ConfigureAwait(false)){
                await output.WriteLineAsync(DynamicWorkerProtocol.Serialize(envelope)).ConfigureAwait(false);
                try{process.Refresh();peak=Math.Max(peak,process.PrivateMemorySize64);}catch(InvalidOperationException){}
                if(envelope.MessageType==WorkerMessageTypes.QueryStarted){
                    var query=DynamicWorkerProtocol.DeserializePayload<WorkerQueryStartedEvent>(envelope);
                    await File.WriteAllTextAsync(Path.Combine(artifacts,"query.json"),query.IrJson,cancellationToken).ConfigureAwait(false);
                    var replay=new WorkerReplayQuery(0,query.IrJson,query.QueryId,
                        request.FixtureSeedOverride??SeedDerivation.Derive(request.BaseSeed,"fixture",query.QueryIndex),query.QueryIndex,"Replay");
                    await File.WriteAllTextAsync(Path.Combine(artifacts,"replay-request.json"),JsonSerializer.Serialize(request with{ReplayQueries=[replay]},DynamicWorkerProtocol.JsonOptions),cancellationToken).ConfigureAwait(false);
                }
                if(envelope.MessageType==WorkerMessageTypes.SourceExecutionCompleted){
                    var result=DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>(envelope);
                    completed++;
                    await File.WriteAllTextAsync(Path.Combine(artifacts,"source.cs"),result.SourceText,cancellationToken).ConfigureAwait(false);
                    await File.WriteAllTextAsync(Path.Combine(artifacts,"execution.json"),JsonSerializer.Serialize(result,DynamicWorkerProtocol.JsonOptions),cancellationToken).ConfigureAwait(false);
                    if(result.MeasurementsJson is not null){
                        await File.WriteAllTextAsync(Path.Combine(artifacts,"measurements.json"),result.MeasurementsJson,cancellationToken).ConfigureAwait(false);
                        ParseMeasurements(result.MeasurementsJson,samples,times);
                    }
                    if(result.ResultJson is not null){
                        await File.WriteAllTextAsync(Path.Combine(artifacts,"results.json"),result.ResultJson,cancellationToken).ConfigureAwait(false);
                        digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.ResultJson)));
                    }
                    diagnostic=result.Status.ToString();
                    if(result.Status==WorkerExecutionStatus.Succeeded){status=CaseStatus.Passed;}
                    else if(result.Status==WorkerExecutionStatus.Canceled){status=CaseStatus.Cancelled;}
                    else{status=CaseStatus.Failed;}
                }
                if(envelope.MessageType==WorkerMessageTypes.Fatal){
                    diagnostic=DynamicWorkerProtocol.DeserializePayload<WorkerFatalEvent>(envelope).Message;
                    status=CaseStatus.Failed;break;
                }
                if(envelope.MessageType==WorkerMessageTypes.RunCanceled){status=CaseStatus.Cancelled;break;}
                if(envelope.MessageType==WorkerMessageTypes.RunCompleted){
                    var done=DynamicWorkerProtocol.DeserializePayload<WorkerRunCompletedEvent>(envelope);
                    if(done.Status!=WorkerExecutionStatus.Succeeded||done.FailedSourceExecutions>0||completed!=1){status=CaseStatus.Failed;}
                    break;
                }
            }
        }catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){
            status=CaseStatus.Cancelled;diagnostic="Cancelled. Saved events and replay input are available.";
            await session.CancelAsync(request.RunId).ConfigureAwait(false);
        }finally{
            if(!session.HasExited){await session.ShutdownAsync(request.RunId).ConfigureAwait(false);}
            var standardError=await session.GetStandardErrorAsync().ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(artifacts,"stderr.log"),standardError,CancellationToken.None).ConfigureAwait(false);
            if(status==CaseStatus.Interrupted&&!string.IsNullOrWhiteSpace(standardError)){diagnostic=standardError;}
        }
        return new(item.Id,status,item.Conditions,times.ToArray(),null,digest,diagnostic,peak,samples.ToArray());
    }
    internal static WorkerRunRequest ReadRequest(VerificationCase item){
        if(item.Arguments.Length!=1){throw new InvalidDataException("A Dynamic case must contain one serialized request.");}
        var request=JsonSerializer.Deserialize<WorkerRunRequest>(item.Arguments[0],DynamicWorkerProtocol.JsonOptions)??throw new InvalidDataException("Dynamic request is missing.");
        if(request.QueryCount!=1||request.SourceTargets.Count!=1){throw new InvalidDataException("Each Dynamic verification case must contain one query and one source.");}
        return request;
    }
    private static void ParseMeasurements(string json,List<RawMeasurement> samples,List<double> times){
        using var document=JsonDocument.Parse(json);
        if(!document.RootElement.TryGetProperty("measurement",out var measurement)||measurement.ValueKind!=JsonValueKind.Object){return;}
        if(!measurement.TryGetProperty("executionOrderedSamples",out var raw)){return;}
        foreach(var sample in raw.EnumerateArray()){
            var implementation=sample.GetProperty("implementation").GetString()??"unknown";
            var elapsed=sample.GetProperty("elapsedNanoseconds").GetInt64();
            var operations=sample.GetProperty("operationCount").GetInt64();
            if(operations<=0||elapsed<0){throw new InvalidDataException("Invalid Dynamic measurement.");}
            samples.Add(new(implementation,sample.GetProperty("sampleIndex").GetInt32(),elapsed,operations,sample.GetProperty("allocatedBytes").GetInt64(),
                sample.GetProperty("gen0Collections").GetInt32(),sample.GetProperty("gen1Collections").GetInt32(),sample.GetProperty("gen2Collections").GetInt32()));
            if(implementation.Equals("fLinq",StringComparison.OrdinalIgnoreCase)){times.Add((double)elapsed/operations);}
        }
    }
}
