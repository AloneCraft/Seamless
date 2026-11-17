using System.Text.Json;
using Seamless.Execution;

namespace Seamless.Management.Server;

internal static class ObjectExecutionProfiling{
    // 3つの最大4096文字のmetadataをJSONエスケープする最悪値も含め、1MiB枠に収める。
    internal const int MaximumRegions=8,MaximumResponseBytes=1024*1024;
    internal static ObjectExecutionProfile Complete(QueryProfiler profiler,ObjectExecutionMeasurement measurement,string definitionId,string objectId,DefinitionVersion version){
        var snapshot=profiler.Complete();
        var regions=snapshot.Regions.Select(region=>new ObjectExecutionProfileRegion(region.InstanceId,
            new(region.Target.MethodKey,region.Target.SourceHash,region.Target.GeneratedBodyHash,new(region.Target.CallSite.Path,region.Target.CallSite.Start,region.Target.CallSite.Length,region.Target.CallSite.Caller),region.Target.NodeId),
            region.StartTimestamp,region.EndTimestamp,region.ManagedThreadId,region.InputCount,region.OutputCount,region.InputCountKnown,region.OutputCountKnown,region.Complete,region.Outcome,region.Scope)).ToArray();
        var diagnostics=snapshot.Diagnostics.Select(value=>value.ToString()).Append("NodeProfileNotPersisted").Append("MaximumRegions=8").ToArray();
        var result=new ObjectExecutionProfile(snapshot.ExecutionId,definitionId,objectId,version.Version,version.ArtifactHash,snapshot.StopwatchFrequency,snapshot.StartedAtUtc,snapshot.StartedTimestamp,regions,snapshot.DroppedRegions,snapshot.Complete,diagnostics){QueryStartedTimestamp=measurement.StartedTimestamp};
        if(JsonSerializer.SerializeToUtf8Bytes(result,ManagementJson.Options).Length>MaximumResponseBytes){throw new ManagementException("Limit","ノード実測の応答サイズ上限を超えています。");}
        return result;
    }
}
