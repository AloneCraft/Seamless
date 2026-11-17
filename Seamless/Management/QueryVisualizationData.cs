using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Seamless.Management;

public sealed record QueryVisualizationIdentity(string DefinitionId,string ObjectId,long Version,string ArtifactHash,string SourceHash,string PlanKey,Guid? ExecutionId);
public sealed record PlanNodeRef(string PlanKey,string NodeId);
public sealed record DataFlowPort(string Id,PlanNodeRef Node,string Direction,string? Role,int Index);
public sealed record DataFlowLink(string Id,string SourcePortId,string TargetPortId,string Relation,string? TransferId=null);
public sealed record FlowQuantity(string? DecimalValue,string Evidence,string Scope,bool Complete,string? MissingReason=null,string? ByteFormat=null){
    public static FlowQuantity Unknown(string scope="Unknown")=>new(null,"Unknown",scope,false,"NotCollected");
    public void Validate(bool bytes=false){
        if(this.Evidence is not("Unknown" or "Observed" or "Estimated" or "Derived")||string.IsNullOrWhiteSpace(this.Scope)||this.Scope.Length>128||this.MissingReason?.Length>1024||this.ByteFormat?.Length>128){throw new InvalidDataException("流量の出所または範囲が不正です。");}
        if(!bytes&&this.ByteFormat is not null){throw new InvalidDataException("行数にはバイト表現を指定できません。");}
        if(this.Evidence=="Unknown"){
            if(this.DecimalValue is not null||this.Complete||string.IsNullOrWhiteSpace(this.MissingReason)){throw new InvalidDataException("未知の流量に値または完全性を指定できません。");}
            return;
        }
        var value=this.DecimalValue;
        if(string.IsNullOrEmpty(value)||value.Length>256||value.Any(character=>character is not(>='0' and <='9')&&character!='.')){throw new InvalidDataException("流量には256文字以内の非負十進数を指定してください。");}
        var point=value.IndexOf('.');
        if(point>=0&&(point==0||point==value.Length-1||value.LastIndexOf('.')!=point||value[^1]=='0'||this.Evidence=="Observed")){throw new InvalidDataException("流量の小数表現が不正です。");}
        if(value.Length>1&&value[0]=='0'&&point!=1){throw new InvalidDataException("流量は先頭のゼロを除いて正規化してください。");}
    }
}
public sealed record PortQuantities(string PortId,FlowQuantity EstimatedRows,FlowQuantity ObservedRows,FlowQuantity EstimatedBytes,FlowQuantity ObservedBytes);
public sealed record LinkQuantities(string LinkId,FlowQuantity EstimatedRows,FlowQuantity ObservedRows,FlowQuantity EstimatedBytes,FlowQuantity ObservedBytes);
public sealed record NodeQuantities(PlanNodeRef Node,string InstanceId,FlowQuantity InputRows,FlowQuantity OutputRows,string Scope);
public sealed record VisualizationClock(string Id,Guid ExecutionId,long TicksPerSecond,string Origin,DateTimeOffset? StartedAtUtc=null);
public sealed record VisualizationInterval(string Id,Guid ExecutionId,string InstanceId,PlanNodeRef? Node,string ClockId,string LaneId,string LaneKind,long StartTicks,long EndTicks,string Scope,string Evidence="Observed",string? Outcome=null,bool Complete=true);
public sealed record VisualizationEvent(string Id,Guid ExecutionId,string InstanceId,PlanNodeRef? Node,string ClockId,string LaneId,long AtTicks,string Kind,string Evidence="Observed");

public sealed record QueryVisualizationData(QueryVisualizationIdentity Identity,ExecutionPlanDocument Plan,DataFlowPort[] Ports,DataFlowLink[] Links,
    PortQuantities[] PortQuantities,LinkQuantities[] LinkQuantities,VisualizationInterval[] Intervals,VisualizationEvent[] Events,string[] Diagnostics){
    public VisualizationClock[] Clocks{get;init;}=[];
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NodeQuantities[]? NodeQuantities{get;init;}
    public const int MaximumNodes=2048,MaximumLinks=8192,MaximumPorts=16384,MaximumIntervals=1024,MaximumEvents=1024;
    public static string PlanKey(ExecutionPlanDocument plan)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{plan.MethodKey,plan.SourceHash,plan.GeneratedBodyHash,plan.CallSite}))));
    public static QueryVisualizationData FromPlan(QueryPlanResult target,ExecutionPlanDocument plan,Guid? executionId=null){
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(plan);
        if(plan.Nodes is null||plan.Edges is null||plan.Nodes.Length>MaximumNodes||plan.Edges.Length>MaximumLinks||plan.Nodes.Any(node=>node is null)||plan.Edges.Any(edge=>edge is null)){throw new InvalidDataException("元の計画構造が不正です。");}
        if(!target.Selection.Plans.Any(candidate=>candidate.MethodKey==plan.MethodKey&&candidate.SourceHash==plan.SourceHash&&candidate.GeneratedBodyHash==plan.GeneratedBodyHash&&candidate.CallSite==plan.CallSite)){
            throw new InvalidDataException("表示対象と計画が一致しません。");
        }
        var key=PlanKey(plan);
        var nodes=plan.Nodes.Where(node=>node.Kind=="SourceOperator").ToDictionary(node=>node.Id,StringComparer.Ordinal);
        var ports=nodes.Values.Select(node=>new DataFlowPort("out:"+node.Id,new(key,node.Id),"Output",null,0)).ToList();
        var links=new List<DataFlowLink>();var indexes=new Dictionary<string,int>(StringComparer.Ordinal);var diagnostics=new List<string>();
        foreach(var edge in plan.Edges.Where(edge=>edge.Kind=="OriginalDataFlow")){
            if(!nodes.ContainsKey(edge.From)||!nodes.ContainsKey(edge.To)){if(diagnostics.Count<128){diagnostics.Add("OriginalDataFlowEndpointUnavailable");}continue;}
            indexes.TryGetValue(edge.To,out var index);indexes[edge.To]=index+1;
            var input="in:"+edge.To+":"+index;
            ports.Add(new(input,new(key,edge.To),"Input",edge.InputRole,index));
            links.Add(new("flow:"+links.Count,"out:"+edge.From,input,"LogicalReference"));
        }
        // 実行全体の出力件数を演算や転送へ割り振らない。量配列の未登録はUnknownとして扱う。
        var result=new QueryVisualizationData(new(target.DefinitionId,target.Selection.ObjectId,target.Version,target.ArtifactHash,target.SourceHash,key,executionId),plan,ports.ToArray(),links.ToArray(),[],[],[],[],diagnostics.ToArray());
        result.Validate();return result;
    }
    public void Validate(){
        if(this.Identity is null||this.Plan is null||this.Ports is null||this.Links is null||this.PortQuantities is null||this.LinkQuantities is null||this.Intervals is null||this.Events is null||this.Clocks is null||this.Diagnostics is null){throw new InvalidDataException("可視化データがありません。");}
        Text(this.Identity.DefinitionId,4096);Text(this.Identity.ObjectId,4096);
        if(this.Plan.Nodes is null||this.Plan.Nodes.Any(node=>node is null)||this.Ports.Any(port=>port is null)||this.Links.Any(link=>link is null)||this.PortQuantities.Any(value=>value is null)||this.LinkQuantities.Any(value=>value is null)||this.Clocks.Any(value=>value is null)||this.Intervals.Any(value=>value is null)||this.Events.Any(value=>value is null)){throw new InvalidDataException("可視化データの要素がありません。");}
        if(this.Identity.Version<1||!Hash(this.Identity.ArtifactHash)||!Hash(this.Identity.SourceHash)||this.Identity.PlanKey!=PlanKey(this.Plan)||this.Identity.ExecutionId==Guid.Empty){throw new InvalidDataException("可視化の対象または計画識別が不正です。");}
        if(this.Plan.Nodes.Length>MaximumNodes||this.Ports.Length>MaximumPorts||this.Links.Length>MaximumLinks||this.PortQuantities.Length>MaximumPorts||this.LinkQuantities.Length>MaximumLinks||this.Intervals.Length>MaximumIntervals||this.Events.Length>MaximumEvents||this.Clocks.Length>MaximumIntervals||this.Diagnostics.Length>128){throw new InvalidDataException("可視化データの件数上限を超えています。");}
        var nodes=Unique(this.Plan.Nodes.Select(node=>node.Id));var ports=Unique(this.Ports.Select(port=>port.Id));var links=Unique(this.Links.Select(link=>link.Id));
        foreach(var port in this.Ports){Node(port.Node);if(port.Direction is not("Input" or "Output")||port.Index<0||port.Role?.Length>1024){throw new InvalidDataException("データポートが不正です。");}}
        var portMap=this.Ports.ToDictionary(port=>port.Id,StringComparer.Ordinal);
        foreach(var link in this.Links){
            if(!portMap.TryGetValue(link.SourcePortId,out var source)||!portMap.TryGetValue(link.TargetPortId,out var target)||source.Direction!="Output"||target.Direction!="Input"||link.Relation is not("LogicalReference" or "PhysicalTransfer" or "ReuseReference")||link.TransferId?.Length>4096){throw new InvalidDataException("データ接続の参照または種類が不正です。");}
        }
        _=Unique(this.PortQuantities.Select(value=>value.PortId));_=Unique(this.LinkQuantities.Select(value=>value.LinkId));
        foreach(var value in this.PortQuantities){if(!ports.Contains(value.PortId)){throw new InvalidDataException("数量のポートがありません。");}Quantities(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes);}
        foreach(var value in this.LinkQuantities){if(!links.Contains(value.LinkId)){throw new InvalidDataException("数量の接続がありません。");}Quantities(value.EstimatedRows,value.ObservedRows,value.EstimatedBytes,value.ObservedBytes);}
        if(this.NodeQuantities is{} quantities){
            if(quantities.Length>MaximumIntervals||quantities.Length>0&&this.Identity.ExecutionId is null){throw new InvalidDataException("ノード数量の件数または実行識別が不正です。");}
            var keys=new HashSet<(PlanNodeRef Node,string Instance)>();
            foreach(var item in quantities){
                if(item is null||item.Node is null||!keys.Add((item.Node,item.InstanceId))||item.InputRows is null||item.OutputRows is null){throw new InvalidDataException("ノード数量の識別が不正です。");}
                Node(item.Node);Text(item.InstanceId,4096);Text(item.Scope,128);item.InputRows.Validate();item.OutputRows.Validate();
                if(item.Scope!="FusedPipelineInclusive"||item.InputRows.Scope!="ConsumerInput"||item.OutputRows.Scope!="ScalarReturn"||item.InputRows.Evidence is not("Unknown" or "Observed")||item.OutputRows.Evidence is not("Unknown" or "Observed")){throw new InvalidDataException("ノード数量の消費境界が不正です。");}
            }
        }
        var clocks=Unique(this.Clocks.Select(value=>value.Id));_=Unique(this.Intervals.Select(value=>value.Id));_=Unique(this.Events.Select(value=>value.Id));
        foreach(var clock in this.Clocks){
            Execution(clock.ExecutionId);Text(clock.Origin,128);
            if(clock.TicksPerSecond<=0||clock.Origin=="ExecutionRelativeDerived"&&(clock.TicksPerSecond!=TimeSpan.TicksPerSecond||clock.Id!="execution-relative:"+clock.ExecutionId.ToString("N"))){throw new InvalidDataException("時計の周波数または原点が不正です。");}
        }
        foreach(var interval in this.Intervals){Execution(interval.ExecutionId);if(interval.Node is{} node){Node(node);}if(!clocks.Contains(interval.ClockId)||interval.StartTicks<0||interval.EndTicks<interval.StartTicks){throw new InvalidDataException("実行区間の時計または範囲が不正です。");}Text(interval.InstanceId,4096);Text(interval.LaneId,4096);Text(interval.LaneKind,128);Text(interval.Scope,128);Evidence(interval.Evidence);}
        foreach(var item in this.Events){Execution(item.ExecutionId);if(item.Node is{} node){Node(node);}if(!clocks.Contains(item.ClockId)||item.AtTicks<0){throw new InvalidDataException("イベントの時計または時刻が不正です。");}Text(item.InstanceId,4096);Text(item.LaneId,4096);Text(item.Kind,128);Evidence(item.Evidence);}
        foreach(var item in this.Diagnostics){Text(item,4096);}
        void Node(PlanNodeRef node){if(node is null||node.PlanKey!=this.Identity.PlanKey||!nodes.Contains(node.NodeId)){throw new InvalidDataException("別の計画のノードが混在しています。");}}
        void Execution(Guid id){if(id==Guid.Empty||this.Identity.ExecutionId!=id){throw new InvalidDataException("別の実行の観測が混在しています。");}}
    }
    private static void Quantities(FlowQuantity estimatedRows,FlowQuantity observedRows,FlowQuantity estimatedBytes,FlowQuantity observedBytes){
        if(estimatedRows is null||observedRows is null||estimatedBytes is null||observedBytes is null){throw new InvalidDataException("数量の項目がありません。");}
        estimatedRows.Validate();observedRows.Validate();estimatedBytes.Validate(true);observedBytes.Validate(true);
        if(estimatedRows.Evidence is not("Unknown" or "Estimated" or "Derived")||estimatedBytes.Evidence is not("Unknown" or "Estimated" or "Derived")||observedRows.Evidence is not("Unknown" or "Observed")||observedBytes.Evidence is not("Unknown" or "Observed")){throw new InvalidDataException("推定値と実測値の欄が一致しません。");}
    }
    private static HashSet<string> Unique(IEnumerable<string> values){var result=new HashSet<string>(StringComparer.Ordinal);foreach(var value in values){Text(value,8192);if(!result.Add(value)){throw new InvalidDataException("可視化の識別子が重複しています。");}}return result;}
    private static void Text(string value,int limit){if(string.IsNullOrWhiteSpace(value)||value.Length>limit){throw new InvalidDataException("可視化の文字列が不正です。");}}
    private static bool Hash(string value)=>value is{Length:64}&&value.All(Uri.IsHexDigit);
    private static void Evidence(string value){if(value is not("Observed" or "Estimated" or "Derived")){throw new InvalidDataException("観測の出所が不正です。");}}
}
