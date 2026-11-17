namespace Seamless.Management;

public sealed record ResourceHeatOptions(TimeSpan? SampleInterval=null,int MaximumSamples=4096){
    [System.Text.Json.Serialization.JsonIgnore]public TimeSpan EffectiveSampleInterval=>this.SampleInterval??TimeSpan.FromMilliseconds(100);
    public void Validate(){if(this.EffectiveSampleInterval<TimeSpan.FromMilliseconds(100)||this.EffectiveSampleInterval>TimeSpan.FromMinutes(1)||this.MaximumSamples is <1 or >65536){throw new ManagementException("InvalidRequest","資源計測の間隔または件数上限が不正です。");}}
}
public sealed record ResourceHeatClock(string Id,long TicksPerSecond,long AnchorTimestamp,DateTimeOffset AnchorUtc,string AnchorKind="QueryStart",TimeSpan? AlignmentUncertainty=null);
/// <summary>百分率の100%を定義する容量・単位・出典。Capacityはその分母であり、実測値ではない。</summary>
public sealed record ResourcePercentBasis(string Description,double? Capacity,string Unit,string Source);
public sealed record ResourceLane(string Id,string Label,string Kind,string Scope,string Metric,string Unit,string? ParentId=null,string? PhysicalResourceId=null,ResourcePercentBasis? PercentBasis=null,Dictionary<string,string>? Topology=null);
/// <summary>WindowAggregateは実際に差分を取った窓、Instantは時点観測。時計はtrace.Clockのraw ticks。</summary>
public sealed record ResourceSample(string LaneId,long StartTimestamp,long EndTimestamp,double? Value,double? Percent,string State="Available",string Evidence="Observed",Guid? ExecutionId=null,PlanNodeRef? Node=null,string? MissingReason=null,string Aggregation="WindowAggregate");
public sealed record ResourceHeatReading(ResourceLane[] Lanes,ResourceSample[] Samples,string[] Diagnostics);
public sealed record ResourceHeatTrace(Guid ExecutionId,string DefinitionId,string ObjectId,long? Version,string? ArtifactHash,bool IdentityResolved,ResourceHeatClock Clock,ResourceLane[] Lanes,ResourceSample[] Samples,TimeSpan RequestedInterval,long DroppedSamples,string[] Diagnostics){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ResourceHeatPersistence? Persistence{get;init;}
    public const int MaximumLanes=1024,MaximumSamples=65536;
    public void Validate(){
        if(this.ExecutionId==Guid.Empty||this.Clock is null||this.Lanes is null||this.Samples is null||this.Diagnostics is null){throw new InvalidDataException("資源記録の識別または本文がありません。");}
        if(this.Persistence is{} persistence){if(persistence.State is not("Queued" or "Saved" or "Dropped" or "Failed" or "Unavailable")){throw new InvalidDataException("資源履歴の保存状態が不正です。");}if(persistence.Reason is{} reason){Text(reason,4096);}}
        Text(this.DefinitionId,4096);Text(this.ObjectId,4096);Text(this.Clock.Id,4096);Text(this.Clock.AnchorKind,128);
        if(this.Clock.TicksPerSecond<=0||this.Clock.AlignmentUncertainty<TimeSpan.Zero||this.RequestedInterval<=TimeSpan.Zero||this.RequestedInterval>TimeSpan.FromMinutes(1)||this.DroppedSamples<0||this.Lanes.Length>MaximumLanes||this.Samples.Length>MaximumSamples||this.Diagnostics.Length>128){throw new InvalidDataException("資源記録の時計・容量・収集間隔が不正です。");}
        if(this.IdentityResolved&&(this.Version is null or <1||this.ArtifactHash is not{Length:64}||!this.ArtifactHash.All(Uri.IsHexDigit))){throw new InvalidDataException("資源記録の版または成果物が不正です。");}
        var lanes=new Dictionary<string,ResourceLane>(StringComparer.Ordinal);
        foreach(var lane in this.Lanes){
            if(lane is null){throw new InvalidDataException("資源レーンがありません。");}
            Text(lane.Id,4096);Text(lane.Label,4096);Text(lane.Kind,128);Text(lane.Metric,128);Text(lane.Unit,128);
            if(!lanes.TryAdd(lane.Id,lane)||lane.Scope is not("Host" or "Process" or "Query" or "Node")||lane.Topology?.Count>32){throw new InvalidDataException("資源レーンの識別または帰属範囲が不正です。");}
            if(lane.PhysicalResourceId is{} physical){Text(physical,4096);}
            if(lane.PercentBasis is{} basis){Text(basis.Description,4096);Text(basis.Unit,128);Text(basis.Source,4096);if(basis.Capacity is{} capacity&&(!double.IsFinite(capacity)||capacity<=0)){throw new InvalidDataException("百分率の分母が不正です。");}}
            foreach(var pair in lane.Topology??[]){Text(pair.Key,128);Text(pair.Value,4096);}
        }
        foreach(var lane in this.Lanes){
            var ancestors=new HashSet<string>(StringComparer.Ordinal){lane.Id};var parent=lane.ParentId;
            while(parent is not null){if(!lanes.TryGetValue(parent,out var value)||!ancestors.Add(parent)){throw new InvalidDataException("資源レーンの親参照が不正です。");}parent=value.ParentId;}
        }
        foreach(var sample in this.Samples){
            if(sample is null||!lanes.TryGetValue(sample.LaneId,out var lane)||sample.EndTimestamp<sample.StartTimestamp||sample.Aggregation is not("WindowAggregate" or "Instant")||sample.Aggregation=="Instant"&&sample.EndTimestamp!=sample.StartTimestamp){throw new InvalidDataException("資源サンプルの参照または窓が不正です。");}
            if(sample.State is not("Available" or "Unsupported" or "Missing" or "Gap" or "NoData")||sample.Evidence is not("Observed" or "Correlated" or "Estimated" or "Unknown")){throw new InvalidDataException("資源サンプルの収集状態が不正です。");}
            if(sample.Value is{} value&&!double.IsFinite(value)||sample.Percent is{} percent&&(!double.IsFinite(percent)||percent<0)){throw new InvalidDataException("資源サンプルには有限の値を指定してください。");}
            if(sample.State=="Available"){
                if(sample.Value is null||sample.Evidence=="Unknown"||sample.Percent is not null&&lane.PercentBasis is null){throw new InvalidDataException("既知の資源値または百分率の分母がありません。");}
            }else if(sample.Value is not null||sample.Percent is not null||sample.Evidence!="Unknown"||string.IsNullOrWhiteSpace(sample.MissingReason)){throw new InvalidDataException("欠測状態には理由を指定し、値を未知にしてください。");}
            if(sample.MissingReason is{} reason){Text(reason,4096);}
            if(sample.ExecutionId is{} execution&&execution!=this.ExecutionId||lane.Scope is "Query" or "Node"&&sample.ExecutionId!=this.ExecutionId||sample.Node is not null&&lane.Scope!="Node"){throw new InvalidDataException("資源のクエリ帰属が不正です。");}
            if(lane.Scope=="Node"&&sample.State=="Available"&&sample.Node is null){throw new InvalidDataException("ノード帰属には対応する計画ノードを指定してください。");}
            if(sample.Node is{} node){Text(node.PlanKey,4096);Text(node.NodeId,4096);}
        }
        foreach(var diagnostic in this.Diagnostics){Text(diagnostic,4096);}
    }
    private static void Text(string value,int maximum){if(string.IsNullOrWhiteSpace(value)||value.Length>maximum){throw new InvalidDataException("資源記録の文字列が不正です。");}}
}
