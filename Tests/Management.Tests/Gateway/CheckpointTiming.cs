using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Seamless.Database.Gateway.Tests;

internal sealed class CheckpointTiming:IDisposable{
    private readonly ActivityListener listener;
    private readonly ConcurrentQueue<Stage> stages=new();
    private readonly string output;

    internal static CheckpointTiming? Capture(string root,string output){
        if(Environment.GetEnvironmentVariable("SEAMLESS_TEST_RECOVERY_TRACE")!="1"){return null;}
        return new(Path.Combine(root,"transactions"),output);
    }

    private CheckpointTiming(string transactionDirectory,string output){
        this.output=output;
        this.listener=new(){
            ShouldListenTo=static source=>source.Name.StartsWith("Seamless.Checkpoint.",StringComparison.Ordinal),
            Sample=static (ref ActivityCreationOptions<ActivityContext> options)=>ActivitySamplingResult.AllData,
            ActivityStarted=static activity=>activity.SetCustomProperty("PendingWorkerItems",ThreadPool.PendingWorkItemCount),
            ActivityStopped=activity=>{
                var checkpoint=activity;
                var databaseId=activity.GetTagItem("database.id");
                while(checkpoint.Parent is{} parent&&checkpoint.GetCustomProperty("TransactionDirectory") is null){
                    checkpoint=parent;
                    databaseId??=checkpoint.GetTagItem("database.id");
                }
                if(!Equals(checkpoint.GetCustomProperty("TransactionDirectory"),transactionDirectory)){return;}
                this.stages.Enqueue(new(activity.OperationName,activity.Id,activity.ParentId,activity.StartTimeUtc,
                    activity.Duration.TotalMilliseconds,checkpoint.GetTagItem("maintenance.sequence"),databaseId,activity.GetTagItem("store.component.ordinal"),
                    (long?)activity.GetCustomProperty("PendingWorkerItems"),ThreadPool.PendingWorkItemCount,ThreadPool.ThreadCount));
            }
        };
        ActivitySource.AddActivityListener(this.listener);
    }

    public void Dispose(){
        this.listener.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(this.output)!);
        File.WriteAllText(this.output,JsonSerializer.Serialize(this.stages.ToArray()));
    }

    private sealed record Stage(string Name,string? Id,string? ParentId,DateTime StartUtc,double DurationMs,object? Sequence,object? DatabaseId,object? ComponentOrdinal,long? PendingWorkerItemsStart,long PendingWorkerItemsEnd,int WorkerThreadCount);
}
