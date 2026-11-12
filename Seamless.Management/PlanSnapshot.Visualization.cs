using System.Text.Json;

namespace Seamless.Management;

public sealed record PlanSnapshotSankeySettings(string Unit="Rows",string Basis="Observed",string Scale="Linear",bool Fit=true);
public sealed record PlanSnapshotHeatSettings(string Scope="Host",string Kind="",string Palette="Continuous",bool ShowNumbers=false,double Zoom=1,int Cursor=0){
    public bool Overlay{get;init;}
    public double PlaybackSpeed{get;init;}=1;
    public long? RangeStart{get;init;}
    public long? RangeEnd{get;init;}
}
public sealed record PlanSnapshotViewState(string Mode="DAG",string Search="",string? SelectedNodeId=null){
    public PlanSnapshotSankeySettings Sankey{get;init;}=new();
    public PlanSnapshotHeatSettings Heat{get;init;}=new();
    public double GanttZoom{get;init;}=1;
}
/// <summary>ポートと接続は保存計画から再投影する。演算の量をクエリ全体の件数から補完しない。</summary>
public sealed record PlanSnapshotExtension(Guid? ExecutionId,PortQuantities[] PortQuantities,LinkQuantities[] LinkQuantities,
    VisualizationClock[] Clocks,VisualizationInterval[] Intervals,VisualizationEvent[] Events,ResourceHeatTrace? ResourceHeat,PlanSnapshotViewState View){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NodeQuantities[]? NodeQuantities{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ProjectionDiagnostics{get;init;}
}

public sealed partial record PlanSnapshot{
    public static PlanSnapshot CreateVisualization(QueryPlanResult target,ExecutionPlanDocument plan,QueryVisualizationData visualization,
        ResourceHeatTrace? heat=null,PlanSnapshotViewState? view=null,ObjectExecutionStatistics? statistics=null,
        PlanSnapshotDisplaySettings? display=null,bool includeCode=false,QueryExecutionObservation? observation=null){
        visualization.Validate();
        if(visualization.Identity.DefinitionId!=target.DefinitionId||visualization.Identity.ObjectId!=target.Selection.ObjectId||visualization.Identity.Version!=target.Version||visualization.Identity.ArtifactHash!=target.ArtifactHash||visualization.Identity.SourceHash!=target.SourceHash){throw new InvalidDataException("保存対象と可視化の対象・版が一致しません。");}
        if(JsonSerializer.Serialize(visualization.Plan,Options)!=JsonSerializer.Serialize(plan,Options)){throw new InvalidDataException("保存計画と可視化の計画が一致しません。");}
        var structure=QueryVisualizationData.FromPlan(target,plan,visualization.Identity.ExecutionId);
        if(JsonSerializer.Serialize(structure.Ports,Options)!=JsonSerializer.Serialize(visualization.Ports,Options)||JsonSerializer.Serialize(structure.Links,Options)!=JsonSerializer.Serialize(visualization.Links,Options)){throw new InvalidDataException("保存計画から復元できないポートまたは接続があります。");}
        // 生の計画のJSONサイズで、コード除外後の保存まで拒否しない。最終投影のサイズを検査する。
        var original=Create(target,plan,statistics,display,false,observation);
        var extension=new PlanSnapshotExtension(visualization.Identity.ExecutionId,visualization.PortQuantities,visualization.LinkQuantities,
            visualization.Clocks,visualization.Intervals,visualization.Events,heat,view??new()){NodeQuantities=visualization.NodeQuantities};
        var diagnostics=visualization.Diagnostics.Where(ProfileDiagnostic).ToArray();if(diagnostics.Length>0){extension=extension with{ProjectionDiagnostics=diagnostics};}
        var data=original.Data with{Plan=Project(plan,true),Observation=observation,Extension=extension,IncludesCode=includeCode};
        Validate(data);
        if(!includeCode){data=ProjectData(data);}
        var result=Seal(data);_=result.ToJson();return result;
    }
    public QueryVisualizationData? Visualization(){
        _=this.ToJson();if(this.Data.Extension is not{} extension){return null;}
        return Visualization(this.Data,extension);
    }
    private static QueryVisualizationData Visualization(PlanSnapshotData data,PlanSnapshotExtension extension){
        var target=new QueryPlanResult(data.DefinitionId,data.Version,data.ArtifactHash,data.SourceHash,data.FromDraft,new(data.ObjectId,[],[data.Plan]),data.DraftRevision);
        var projected=QueryVisualizationData.FromPlan(target,data.Plan,extension.ExecutionId);
        return projected with{
            PortQuantities=extension.PortQuantities,LinkQuantities=extension.LinkQuantities,Clocks=extension.Clocks,Intervals=extension.Intervals,Events=extension.Events,NodeQuantities=extension.NodeQuantities,
            Diagnostics=projected.Diagnostics.Concat(extension.ProjectionDiagnostics??[]).Distinct(StringComparer.Ordinal).ToArray()
        };
    }
    private static void ValidateFormat(int format,PlanSnapshotData data){
        if(data is null||format is not(1 or 2)||format==1&&data.Extension is not null||format==2&&data.Extension is null){throw new InvalidDataException("保存形式と拡張データが一致しません。");}
    }
    private static void ValidateExtension(PlanSnapshotData data){
        if(data.Extension is not{} extension){return;}
        if(extension.View is null||extension.View.Mode is not("DAG" or "Sankey" or "Gantt" or "Heat" or "Tree" or "DOT")){throw new InvalidDataException("保存表示の種類が不正です。");}
        Bound(extension.View.Search,4096);
        var view=extension.View;
        if(view.Sankey is null||view.Heat is null||view.Sankey.Unit is not("Rows" or "Bytes")||view.Sankey.Basis is not("Observed" or "Estimated" or "Compare")||view.Sankey.Scale is not("Linear" or "Log1p")||view.Heat.Scope is not("Host" or "Process" or "Query" or "Node")||view.Heat.Palette is not("Continuous" or "Steps" or "Mono" or "Accessible")||!double.IsFinite(view.Heat.Zoom)||view.Heat.Zoom is <1 or >8||view.Heat.Cursor is <0 or >1000||!double.IsFinite(view.GanttZoom)||view.GanttZoom is <1 or >8){throw new InvalidDataException("保存表示の設定範囲が不正です。");}
        Bound(view.Heat.Kind,128);
        if(extension.ProjectionDiagnostics is{} diagnostics&&(diagnostics.Length>128||diagnostics.Any(value=>!ProfileDiagnostic(value)))){throw new InvalidDataException("保存した投影診断が不正です。");}
        if(view.Heat.PlaybackSpeed is not(0.25 or 0.5 or 1 or 2 or 4 or 8)||view.Heat.RangeStart.HasValue!=view.Heat.RangeEnd.HasValue||view.Heat.RangeEnd<view.Heat.RangeStart){throw new InvalidDataException("ヒート図の再生速度または選択時間が不正です。");}
        if(extension.View.SelectedNodeId is{} selected&&!data.Plan.Nodes.Any(node=>node.Id==selected)){throw new InvalidDataException("選択ノードが保存計画にありません。");}
        var visualization=Visualization(data,extension);visualization.Validate();
        using(var json=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(extension,Options))){ValidateJson(json.RootElement);}
        if(data.Statistics is{} statistics&&extension.ExecutionId!=statistics.ExecutionId||data.Observation is{} observation&&extension.ExecutionId!=observation.ExecutionId){throw new InvalidDataException("保存資料に別の実行が混在しています。");}
        if(extension.ResourceHeat is{} heat){
            heat.Validate();
            if(data.FromDraft||!heat.IdentityResolved||heat.ExecutionId!=extension.ExecutionId||heat.DefinitionId!=data.DefinitionId||heat.ObjectId!=data.ObjectId||heat.Version!=data.Version||heat.ArtifactHash!=data.ArtifactHash){throw new InvalidDataException("資源記録の対象・版・実行が一致しません。");}
            foreach(var sample in heat.Samples){if(sample.Node is{} node&&(node.PlanKey!=visualization.Identity.PlanKey||!data.Plan.Nodes.Any(item=>item.Id==node.NodeId))){throw new InvalidDataException("資源記録のノード参照が一致しません。");}}
            ValidateHeatRange(heat,visualization,view.Heat);
        }
    }
    private static void ValidateHeatRange(ResourceHeatTrace heat,QueryVisualizationData visualization,PlanSnapshotHeatSettings view){
        if(view.RangeStart is not{} start||view.RangeEnd is not{} end){return;}
        var minimum=heat.Clock.AnchorTimestamp;var maximum=minimum;
        foreach(var sample in heat.Samples){minimum=Math.Min(minimum,sample.StartTimestamp);maximum=Math.Max(maximum,sample.EndTimestamp);}
        foreach(var interval in visualization.Intervals){
            if(interval.Evidence!="Observed"){continue;}
            var clock=visualization.Clocks.Single(item=>item.Id==interval.ClockId);decimal from,to;
            if(clock.Id==heat.Clock.Id&&clock.TicksPerSecond==heat.Clock.TicksPerSecond){from=interval.StartTicks;to=interval.EndTicks;}
            else if(clock.Origin=="ExecutionRelativeDerived"&&heat.Clock.AnchorKind=="QueryStart"){
                try{
                    from=heat.Clock.AnchorTimestamp+(decimal)interval.StartTicks*heat.Clock.TicksPerSecond/clock.TicksPerSecond;
                    to=heat.Clock.AnchorTimestamp+(decimal)interval.EndTicks*heat.Clock.TicksPerSecond/clock.TicksPerSecond;
                }catch(OverflowException){continue;}
            }else{continue;}
            if(from<long.MinValue||to>long.MaxValue){continue;}
            minimum=Math.Min(minimum,(long)decimal.Round(from));maximum=Math.Max(maximum,(long)decimal.Round(to));
        }
        if(start<minimum||end>maximum){throw new InvalidDataException("保存した表示時間が記録の時間範囲外です。");}
    }
    private static PlanSnapshotData ProjectData(PlanSnapshotData data){
        var projected=Project(data.Plan,false);
        var result=data with{Plan=projected,IncludesCode=false,Observation=ProjectObservation(data.Observation,false)};
        if(data.Extension is not{} extension){return result;}
        var original=Visualization(data,extension);var next=Visualization(result,extension with{PortQuantities=[],LinkQuantities=[],Clocks=[],Intervals=[],Events=[]});
        var nodes=data.Plan.Nodes.Select((node,index)=>(node.Id,Safe:projected.Nodes[index].Id)).ToDictionary(item=>item.Id,item=>item.Safe,StringComparer.Ordinal);
        var ports=original.Ports.Zip(next.Ports).ToDictionary(pair=>pair.First.Id,pair=>pair.Second.Id,StringComparer.Ordinal);
        var links=original.Links.Zip(next.Links).ToDictionary(pair=>pair.First.Id,pair=>pair.Second.Id,StringComparer.Ordinal);
        var lanes=extension.Intervals.Select(item=>item.LaneId).Concat(extension.Events.Select(item=>item.LaneId)).Distinct(StringComparer.Ordinal).Select((id,index)=>(id,Safe:"lane"+index)).ToDictionary(item=>item.id,item=>item.Safe,StringComparer.Ordinal);
        var instances=extension.Intervals.Select(item=>item.InstanceId).Concat(extension.Events.Select(item=>item.InstanceId)).Concat((extension.NodeQuantities??[]).Select(item=>item.InstanceId)).Distinct(StringComparer.Ordinal).Select((id,index)=>(id,Safe:"instance"+index)).ToDictionary(item=>item.id,item=>item.Safe,StringComparer.Ordinal);
        var clocks=extension.Clocks.Select((clock,index)=>(clock.Id,Safe:ClockId(clock,index))).ToDictionary(item=>item.Id,item=>item.Safe,StringComparer.Ordinal);
        PlanNodeRef? Node(PlanNodeRef? node){if(node is null){return null;}return new(next.Identity.PlanKey,nodes[node.NodeId]);}
        string Safe(string value,params string[] allowed){if(allowed.Contains(value,StringComparer.Ordinal)){return value;}return "Unknown";}
        FlowQuantity Quantity(FlowQuantity value)=>value with{Scope=Safe(value.Scope,"Unknown","Logical","Physical","LogicalOutput","PhysicalTransfer","Port","Link","ConsumerInput","ScalarReturn"),MissingReason=Reason(value.MissingReason),ByteFormat=ByteFormat(value.ByteFormat)};
        ResourceHeatTrace? heat=null;
        if(extension.ResourceHeat is{} source){
            var heatLanes=source.Lanes.Select((lane,index)=>(lane.Id,Safe:"resource"+index)).ToDictionary(item=>item.Id,item=>item.Safe,StringComparer.Ordinal);
            var heatClock="resource-clock";if(clocks.TryGetValue(source.Clock.Id,out var mappedClock)){heatClock=mappedClock;}
            heat=source with{Clock=source.Clock with{Id=heatClock,AnchorKind=Safe(source.Clock.AnchorKind,"QueryStart","HostStart","Unknown")},
                Lanes=source.Lanes.Select(lane=>lane with{Id=heatLanes[lane.Id],Label=heatLanes[lane.Id],ParentId=Map(lane.ParentId,heatLanes),PhysicalResourceId=null,Topology=null,
                    Kind=Safe(lane.Kind,"CPU","LogicalCPU","ProcessCPU","Memory","NIC","Storage","Disk","NUMA","Unknown"),Metric=Safe(lane.Metric,"Utilization","CpuUtilization","CpuTime","WorkingSet","WorkingSetBytes","UsedMemory","AvailableMemory","FreeMemory","BytesPerSecond","ReadBytesPerSecond","WriteBytesPerSecond","Unknown"),Unit=Safe(lane.Unit,"Percent","Bytes","BytesPerSecond","CpuSecondsPerSecond","Unknown"),
                    PercentBasis=MaskBasis(lane.PercentBasis)}).ToArray(),
                Samples=source.Samples.Select(sample=>sample with{LaneId=heatLanes[sample.LaneId],Node=Node(sample.Node),MissingReason=Reason(sample.MissingReason)}).ToArray(),Diagnostics=[],Persistence=MaskPersistence(source.Persistence)};
        }
        result=result with{Extension=extension with{
            PortQuantities=extension.PortQuantities.Select(value=>new PortQuantities(ports[value.PortId],Quantity(value.EstimatedRows),Quantity(value.ObservedRows),Quantity(value.EstimatedBytes),Quantity(value.ObservedBytes))).ToArray(),
            LinkQuantities=extension.LinkQuantities.Select(value=>new LinkQuantities(links[value.LinkId],Quantity(value.EstimatedRows),Quantity(value.ObservedRows),Quantity(value.EstimatedBytes),Quantity(value.ObservedBytes))).ToArray(),
            NodeQuantities=extension.NodeQuantities?.Select(item=>new NodeQuantities(Node(item.Node)!,instances[item.InstanceId],Quantity(item.InputRows),Quantity(item.OutputRows),Safe(item.Scope,"FusedPipelineInclusive"))).ToArray(),
            Clocks=extension.Clocks.Select(clock=>clock with{Id=clocks[clock.Id],Origin=Safe(clock.Origin,"ExecutionRelativeDerived","Stopwatch","Unknown")}).ToArray(),
            Intervals=extension.Intervals.Select((item,index)=>item with{Id="interval"+index,InstanceId=instances[item.InstanceId],LaneId=lanes[item.LaneId],ClockId=clocks[item.ClockId],Node=Node(item.Node),LaneKind=Safe(item.LaneKind,"Query","Wait","Worker","Task","ManagedThread","Unknown"),Scope=Safe(item.Scope,"QueryInclusive","DatabaseGateWait","TaskExecution","FusedPipelineInclusive","Unknown"),Outcome=Outcome(item.Outcome)}).ToArray(),
            Events=extension.Events.Select((item,index)=>item with{Id="event"+index,InstanceId=instances[item.InstanceId],LaneId=lanes[item.LaneId],ClockId=clocks[item.ClockId],Node=Node(item.Node),Kind=Safe(item.Kind,"Started","GateAcquired","FirstOutput","Completed","Unknown")}).ToArray(),
            ResourceHeat=heat,View=extension.View with{Search="",SelectedNodeId=Map(extension.View.SelectedNodeId,nodes),Heat=extension.View.Heat with{Kind=MaskResourceKind(extension.View.Heat.Kind)}}
        }};
        Validate(result);return result;
    }
    private static string ClockId(VisualizationClock clock,int index){if(clock.Origin=="ExecutionRelativeDerived"){return "execution-relative:"+clock.ExecutionId.ToString("N");}return "clock"+index;}
    private static string? Map(string? value,Dictionary<string,string> names){if(value is null){return null;}return names[value];}
    private static string? Reason(string? value){if(value is null){return null;}return "NotCollected";}
    private static string? ByteFormat(string? value){if(value is null){return null;}if(value is "Serialized" or "Managed" or "Wire" or "Logical"){return value;}return "Unknown";}
    private static string? Outcome(string? value){if(value is null){return null;}if(value is "Succeeded" or "Failed" or "Cancelled"){return value;}return "Unknown";}
    private static ResourcePercentBasis? MaskBasis(ResourcePercentBasis? value){if(value is null){return null;}return new("Capacity",value.Capacity,"Unknown","ObservedProvider");}
    private static ResourceHeatPersistence? MaskPersistence(ResourceHeatPersistence? value){if(value is null){return null;}return value with{Reason=null};}
    private static string MaskResourceKind(string value){if(value.Length==0||value is "CPU" or "LogicalCPU" or "ProcessCPU" or "Memory" or "NIC" or "Storage" or "Disk" or "NUMA"){return value;}return "Unknown";}
    private static bool ProfileDiagnostic(string value){
        if(value is "ProfileRegionsNotCollected" or "ProfilePartial" or "ProfileWorkerNotCollected" or "ProfileHelperBodiesNotExpanded"){return true;}
        foreach(var prefix in new[]{"ProfileExcludedBinding=","ProfileExcludedNode=","ProfileDroppedRegions="}){
            if(value is not null&&value.StartsWith(prefix,StringComparison.Ordinal)){var count=value[prefix.Length..];return count.Length is >0 and <=19&&count.All(character=>character is >='0' and <='9')&&long.TryParse(count,out _);}
        }
        return false;
    }
}
