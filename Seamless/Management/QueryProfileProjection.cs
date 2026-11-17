using System.Globalization;

namespace Seamless.Management;

/// <summary>公開版と実行と呼出し位置が一致した物理消費区間だけを表示する。元クエリの論理ポートへ配分しない。</summary>
public static class QueryProfileProjection{
    public static QueryVisualizationData Apply(QueryVisualizationData data,ObjectExecutionProfile profile){
        ArgumentNullException.ThrowIfNull(data);ArgumentNullException.ThrowIfNull(profile);data.Validate();var identity=data.Identity;
        if(profile.ExecutionId!=identity.ExecutionId||profile.DefinitionId!=identity.DefinitionId||profile.ObjectId!=identity.ObjectId||profile.Version!=identity.Version||profile.ArtifactHash!=identity.ArtifactHash){throw new InvalidDataException("ノード実測の対象・版・成果物・実行が一致しません。");}
        if(profile.StopwatchFrequency<=0||profile.StartedTimestamp<profile.QueryStartedTimestamp||profile.Regions is null||profile.Diagnostics is null||profile.Regions.Length>1024||profile.Diagnostics.Length>128||profile.DroppedRegions<0||profile.Complete&&profile.DroppedRegions>0){throw new InvalidDataException("ノード実測の時計または件数が不正です。");}
        var intervals=data.Intervals.ToList();var quantities=(data.NodeQuantities??[]).ToList();var clocks=data.Clocks.ToList();
        var diagnostic=data.Diagnostics.ToHashSet(StringComparer.Ordinal);var excludedBinding=0;var excludedNode=0;var matched=0;var instances=new HashSet<long>();
        var key=identity.PlanKey;var clockId="execution-relative:"+profile.ExecutionId.ToString("N");
        foreach(var region in profile.Regions){
            if(region is null||region.Target is null||region.Target.CallSite is null||region.InstanceId<=0||!instances.Add(region.InstanceId)||region.StartTimestamp<profile.StartedTimestamp||region.EndTimestamp<region.StartTimestamp||region.ManagedThreadId<=0||region.InputCount<0||region.OutputCount<0||region.Scope!="FusedPipelineInclusive"||region.Outcome is not("Succeeded" or "Failed" or "Cancelled" or "Incomplete")||region.Complete&&region.Outcome!="Succeeded"){throw new InvalidDataException("ノード実測区間の参照または範囲が不正です。");}
            var target=region.Target;var site=data.Plan.CallSite;
            if(target.MethodKey!=data.Plan.MethodKey||target.SourceHash!=data.Plan.SourceHash||target.GeneratedBodyHash!=data.Plan.GeneratedBodyHash||site is null||target.CallSite.Path!=site.Path||target.CallSite.Start!=site.Start||target.CallSite.Length!=site.Length||target.CallSite.Caller!=site.Caller){excludedBinding++;continue;}
            var node=data.Plan.Nodes.SingleOrDefault(item=>item.Id==target.NodeId);
            if(node is null||node.Layer!="Selected"||node.Kind is not("Fragment" or "PipelineStage")){excludedNode++;continue;}
            var instance="profile:"+region.InstanceId.ToString(CultureInfo.InvariantCulture);var lane="managed-thread:"+region.ManagedThreadId.ToString(CultureInfo.InvariantCulture);var nodeRef=new PlanNodeRef(key,node.Id);
            var interval=new VisualizationInterval("profile-region:"+region.InstanceId.ToString(CultureInfo.InvariantCulture),profile.ExecutionId,instance,nodeRef,clockId,lane,"ManagedThread",Ticks(region.StartTimestamp),Ticks(region.EndTimestamp),region.Scope,"Observed",region.Outcome,region.Complete);
            var existing=intervals.SingleOrDefault(item=>item.Id==interval.Id);if(existing is not null&&existing!=interval){throw new InvalidDataException("既存区間とノード実測が衝突しています。");}if(existing is null){intervals.Add(interval);}
            var quantity=new NodeQuantities(nodeRef,instance,Count(region.InputCount,region.InputCountKnown,"ConsumerInput",region.Complete),Count(region.OutputCount,region.OutputCountKnown,"ScalarReturn",region.Complete),region.Scope);
            var old=quantities.SingleOrDefault(item=>item.Node==nodeRef&&item.InstanceId==instance);if(old is not null&&old!=quantity){throw new InvalidDataException("既存数量とノード実測が衝突しています。");}if(old is null){quantities.Add(quantity);}matched++;
        }
        if(matched>0){
            var old=clocks.SingleOrDefault(clock=>clock.Id==clockId);
            if(old is not null&&(old.Origin!="ExecutionRelativeDerived"||old.TicksPerSecond!=TimeSpan.TicksPerSecond)){throw new InvalidDataException("ノード実測と既存時計の原点が一致しません。");}
            if(old is null){clocks.Add(new(clockId,profile.ExecutionId,TimeSpan.TicksPerSecond,"ExecutionRelativeDerived"));}
        }else{diagnostic.Add("ProfileRegionsNotCollected");}
        if(excludedBinding>0){diagnostic.Add("ProfileExcludedBinding="+excludedBinding.ToString(CultureInfo.InvariantCulture));}
        if(excludedNode>0){diagnostic.Add("ProfileExcludedNode="+excludedNode.ToString(CultureInfo.InvariantCulture));}
        if(profile.DroppedRegions>0){diagnostic.Add("ProfileDroppedRegions="+profile.DroppedRegions.ToString(CultureInfo.InvariantCulture));}
        if(!profile.Complete){diagnostic.Add("ProfilePartial");}
        diagnostic.Add("ProfileWorkerNotCollected");diagnostic.Add("ProfileHelperBodiesNotExpanded");
        var result=data with{Intervals=intervals.ToArray(),Clocks=clocks.ToArray(),NodeQuantities=quantities.ToArray(),Diagnostics=diagnostic.Order(StringComparer.Ordinal).ToArray()};result.Validate();return result;
        long Ticks(long timestamp){var value=((decimal)timestamp-profile.QueryStartedTimestamp)*TimeSpan.TicksPerSecond/profile.StopwatchFrequency;if(value<0||value>long.MaxValue){throw new InvalidDataException("ノード実測を表示時計へ変換できません。");}return (long)decimal.Round(value);}
    }
    private static FlowQuantity Count(long value,bool known,string scope,bool complete){if(!known){return FlowQuantity.Unknown(scope);}return new(value.ToString(CultureInfo.InvariantCulture),"Observed",scope,complete);}
}
