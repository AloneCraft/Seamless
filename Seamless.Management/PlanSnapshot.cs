using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.Management;

public sealed record PlanSnapshotDisplaySettings(string Layer="All",string[]? HiddenEdgeKinds=null,bool ShowInputRoles=false,bool ShowDisplayResult=true){
    public bool ShowSummaryEdges{get;init;}=true;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StructureLevel{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? GeneratedVariant{get;init;}
}
public sealed record PlanSnapshotData(string DefinitionId,string ObjectId,long Version,string ArtifactHash,string SourceHash,bool FromDraft,long? DraftRevision,
    ExecutionPlanDocument Plan,ObjectExecutionStatistics? Statistics,PlanSnapshotDisplaySettings Display,bool IncludesCode){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public QueryExecutionObservation? Observation{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PlanSnapshotExtension? Extension{get;init;}
}
public sealed record PlanSnapshotComparison(bool SameTarget,bool SameArtifact,bool SamePlan,bool NodeCorrespondenceKnown,PlanSnapshotCountDifference[] NodeKinds,PlanSnapshotCountDifference[] EdgeKinds,int? LeftRewrites,int? RightRewrites);
public sealed record PlanSnapshotCountDifference(string Kind,int Left,int Right);

/// <summary>閲覧専用の交換形式。内容ハッシュは改変検出用で、署名や成果物DLLの真正性を証明しない。</summary>
public sealed partial record PlanSnapshot(int Format,PlanSnapshotData Data,string ContentHash){
    public const int CurrentFormat=1;
    public const int MaximumBytes=ExecutionPlanDocument.MaximumMetadataBytes;
    private static readonly JsonSerializerOptions Options=new(ManagementJson.Options){MaxDepth=64,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    private static readonly UTF8Encoding StrictUtf8=new(false,true);
    private static readonly HashSet<string> Kinds=new(StringComparer.Ordinal){"SourceOperator","Fragment","PipelineStage","Expression","LoweringDecision","RuntimeCondition","Unknown"};
    private static readonly HashSet<string> Layers=new(StringComparer.Ordinal){"Unknown","Original","Selected","Optimization","GeneratedControl","OriginalExpression"};
    private static readonly HashSet<string> Edges=new(StringComparer.Ordinal){"OriginalDataFlow","ExpressionDependency","SelectedReference","SourceMapping","Unknown"};
    private static readonly HashSet<string> DisplayLayers=new(StringComparer.Ordinal){"All","Selected","Original","OriginalExpression","Logical","Optimization","Unknown"};

    public static PlanSnapshot Create(QueryPlanResult target,ExecutionPlanDocument plan,ObjectExecutionStatistics? statistics=null,PlanSnapshotDisplaySettings? display=null,bool includeCode=false,QueryExecutionObservation? observation=null){
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(plan);
        if(!target.Selection.Plans.Any(candidate=>ReferenceEquals(candidate,plan)||candidate.MethodKey==plan.MethodKey&&candidate.SourceHash==plan.SourceHash&&candidate.GeneratedBodyHash==plan.GeneratedBodyHash&&candidate.CallSite==plan.CallSite)){
            throw new InvalidDataException("計画と選択対象が一致しません。");
        }
        if(statistics is not null&&(target.FromDraft||statistics.DefinitionId!=target.DefinitionId||statistics.ObjectId!=target.Selection.ObjectId||statistics.Version!=target.Version||statistics.ArtifactHash!=target.ArtifactHash)){
            throw new InvalidDataException("実測と計画の対象・版が一致しません。");
        }
        var data=new PlanSnapshotData(target.DefinitionId,target.Selection.ObjectId,target.Version,target.ArtifactHash,target.SourceHash,target.FromDraft,target.DraftRevision,
            Project(plan,includeCode),statistics,display??new(),includeCode){Observation=ProjectObservation(observation,includeCode)};
        Validate(data);
        var snapshot=Seal(data);_=snapshot.ToJson();return snapshot;
    }
    public byte[] ToJson(){
        ValidateFormat(this.Format,this.Data);
        Validate(this.Data);
        if(!HashEquals(this.ContentHash,Hash(this.Data))){throw new InvalidDataException("保存内容のハッシュが一致しません。");}
        var bytes=JsonSerializer.SerializeToUtf8Bytes(this,Options);
        if(bytes.Length>MaximumBytes){throw new InvalidDataException("計画の保存サイズ上限を超えています。");}
        return bytes;
    }
    public static PlanSnapshot Read(ReadOnlySpan<byte> bytes){
        if(bytes.Length>MaximumBytes){throw new InvalidDataException("計画の読込サイズ上限を超えています。");}
        try{
            using var document=JsonDocument.Parse(bytes.ToArray(),new(){MaxDepth=64});
            ValidateJson(document.RootElement);
            var snapshot=JsonSerializer.Deserialize<PlanSnapshot>(bytes,Options)??throw new InvalidDataException("計画の保存内容がありません。");
            ValidateFormat(snapshot.Format,snapshot.Data);
            Validate(snapshot.Data);
            if(!HashEquals(snapshot.ContentHash,Hash(snapshot.Data))){throw new InvalidDataException("保存内容のハッシュが一致しません。");}
            // マスク済みと宣言したファイルにも、外部から追加された任意の本文を残さない。
            if(!snapshot.Data.IncludesCode){return Seal(ProjectData(snapshot.Data));}
            return snapshot;
        }catch(JsonException exception){throw new InvalidDataException("計画JSONの型または形式が不正です。",exception);}
    }
    public PlanSnapshot WithoutCode(){_=this.ToJson();return Seal(ProjectData(this.Data));}
    public static PlanSnapshotComparison Compare(PlanSnapshot left,PlanSnapshot right){
        _=left.ToJson();_=right.ToJson();
        var sameTarget=left.Data.DefinitionId==right.Data.DefinitionId&&left.Data.ObjectId==right.Data.ObjectId;
        var sameArtifact=sameTarget&&left.Data.Version==right.Data.Version&&left.Data.ArtifactHash==right.Data.ArtifactHash;
        var samePlan=sameArtifact&&left.Data.Plan.SourceHash==right.Data.Plan.SourceHash&&left.Data.Plan.GeneratedBodyHash==right.Data.Plan.GeneratedBodyHash&&left.Data.Plan.GeneratorBuild==right.Data.Plan.GeneratorBuild;
        var nodeKinds=Counts(left.Data.Plan.Nodes.Select(node=>node.Layer+" / "+node.Kind),right.Data.Plan.Nodes.Select(node=>node.Layer+" / "+node.Kind));
        var edgeKinds=Counts(left.Data.Plan.Edges.Select(edge=>edge.Kind),right.Data.Plan.Edges.Select(edge=>edge.Kind));
        var sameNodes=samePlan&&JsonSerializer.Serialize(left.Data.Plan,Options)==JsonSerializer.Serialize(right.Data.Plan,Options);
        return new(sameTarget,sameArtifact,samePlan,sameNodes,nodeKinds,edgeKinds,
            OptimizationTraceReader.Read(left.Data.Plan.Trace).Summary?.RewriteCount,OptimizationTraceReader.Read(right.Data.Plan.Trace).Summary?.RewriteCount);
    }
    private static PlanSnapshotCountDifference[] Counts(IEnumerable<string> left,IEnumerable<string> right){
        var a=left.GroupBy(value=>value,StringComparer.Ordinal).ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
        var b=right.GroupBy(value=>value,StringComparer.Ordinal).ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.Ordinal);
        return a.Keys.Concat(b.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(kind=>new PlanSnapshotCountDifference(kind,a.GetValueOrDefault(kind),b.GetValueOrDefault(kind))).ToArray();
    }
    private static PlanSnapshot Seal(PlanSnapshotData data){var format=CurrentFormat;if(data.Extension is not null){format=2;}return new(format,data,Hash(data));}
    private static string Hash(PlanSnapshotData data)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data,Options)));
    private static bool HashEquals(string? left,string right)=>left is not null&&string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
    private static string Known(string value,HashSet<string> allowed){if(allowed.Contains(value)){return value;}return "Unknown";}
    private static QueryExecutionObservation? ProjectObservation(QueryExecutionObservation? value,bool includeCode){
        if(value is null||includeCode){return value;}
        return value with{ErrorCode=null};
    }
    private static ExecutionPlanDocument Project(ExecutionPlanDocument plan,bool includeCode){
        ValidatePlan(plan);
        if(includeCode){return plan with{Trace=OptimizationTraceReader.Project(plan.Trace,true)};}
        var names=plan.Nodes.Select((node,index)=>(node.Id,Safe:"node"+index)).ToDictionary(item=>item.Id,item=>item.Safe,StringComparer.Ordinal);
        var nodes=plan.Nodes.Select(node=>new ExecutionPlanNode(names[node.Id],Known(node.Kind,Kinds),Known(node.Kind,Kinds),node.Inputs.Select(input=>names[input]).ToArray(),null,null,0,0,new()){
            Layer=Known(node.Layer,Layers),OutputType=SafeType(node.OutputType),Nullability=node.Nullability,Measurement=node.Measurement
        }).ToArray();
        return plan with{MethodKey="",CallSite=null,Nodes=nodes,Trace=OptimizationTraceReader.Project(plan.Trace,false),
            Edges=plan.Edges.Select(edge=>new ExecutionPlanEdge(names[edge.From],names[edge.To],Known(edge.Kind,Edges))).ToArray(),Diagnostics=[],GeneratedCode=null,ProfiledGeneratedCode=null};
    }
    private static void Validate(PlanSnapshotData data){
        if(data is null||data.Plan is null||data.Display is null){throw new InvalidDataException("保存対象がありません。");}
        Identifier(data.DefinitionId);Identifier(data.ObjectId);
        if(data.Version<1||data.DraftRevision<0||data.FromDraft!=(data.DraftRevision is not null)){throw new InvalidDataException("公開版または下書き版が不正です。");}
        HashValue(data.ArtifactHash);HashValue(data.SourceHash);ValidatePlan(data.Plan);
        ValidateExtension(data);
        if(!DisplayLayers.Contains(data.Display.Layer)||data.Display.HiddenEdgeKinds is{Length:>32}||data.Display.HiddenEdgeKinds?.Any(kind=>!Edges.Contains(kind))==true){throw new InvalidDataException("表示設定が不正です。");}
        if(data.Display.StructureLevel is not (null or "Relational" or "Logical" or "Generated")){throw new InvalidDataException("構造表示の段階が不正です。");}
        if(data.Display.GeneratedVariant is not(null or "Normal" or "Profiled")){throw new InvalidDataException("生成コードの経路が不正です。");}
        if(data.Statistics is{} statistics){
            if(data.FromDraft||statistics.DefinitionId!=data.DefinitionId||statistics.ObjectId!=data.ObjectId||statistics.Version!=data.Version||statistics.ArtifactHash!=data.ArtifactHash||statistics.ExecutionId==Guid.Empty||statistics.Elapsed<TimeSpan.Zero||statistics.DatabaseGateWait<TimeSpan.Zero||statistics.DatabaseGateWait>statistics.Elapsed||statistics.GateWaitStartedOffset<TimeSpan.Zero||statistics.GateWaitStartedOffset>statistics.Elapsed-statistics.DatabaseGateWait||statistics.TimeToFirstOutput<TimeSpan.Zero||statistics.TimeToFirstOutput>statistics.Elapsed||statistics.ReturnedOutputCount<0||statistics.ObservedOutputCount<statistics.ReturnedOutputCount){throw new InvalidDataException("実測の対象または範囲が不正です。");}
        }
        if(data.Observation is{} observation){
            if(data.FromDraft||!observation.IdentityResolved||observation.DefinitionId!=data.DefinitionId||observation.ObjectId!=data.ObjectId||observation.Version!=data.Version||observation.ArtifactHash!=data.ArtifactHash||observation.ExecutionId==Guid.Empty||observation.Outcome is not ("Succeeded" or "Failed" or "Cancelled")||observation.Phase is not ("Execution" or "DatabaseGate" or "VersionBinding")||observation.Elapsed<TimeSpan.Zero||observation.DatabaseGateWait<TimeSpan.Zero||observation.DatabaseGateWait>observation.Elapsed||observation.GateWaitStartedOffset<TimeSpan.Zero||observation.GateWaitStartedOffset>observation.Elapsed-observation.DatabaseGateWait||observation.TimeToFirstOutput<TimeSpan.Zero||observation.TimeToFirstOutput>observation.Elapsed||observation.ReturnedOutputCount<0||observation.ObservedOutputCount<observation.ReturnedOutputCount){throw new InvalidDataException("実績記録の対象または観測範囲が不正です。");}
            if(observation.ErrorCode is{} code){Bound(code,4096);}
            if(data.Statistics is{} measured&&(measured.ExecutionId!=observation.ExecutionId||measured.Elapsed!=observation.Elapsed||measured.DatabaseGateWait!=observation.DatabaseGateWait||measured.GateWaitStartedOffset!=observation.GateWaitStartedOffset||measured.ObservedOutputCount!=observation.ObservedOutputCount||measured.ReturnedOutputCount!=observation.ReturnedOutputCount)){throw new InvalidDataException("実測と終端記録が一致しません。");}
        }
    }
    private static void ValidatePlan(ExecutionPlanDocument plan){
        if(plan.SchemaVersion is not (1 or 2)||plan.Nodes is null||plan.Edges is null||plan.Diagnostics is null||plan.Nodes.Length>ExecutionPlanDocument.MaximumNodes||plan.Edges.Length>ExecutionPlanDocument.MaximumEdges||plan.OmittedNodeCount<0||plan.MetadataComplete&&plan.OmittedNodeCount>0){throw new InvalidDataException("計画の版、件数または収集状態が不正です。");}
        GeneratedExecutionCodeValidator.Validate(plan.GeneratedCode);
        GeneratedExecutionCodeValidator.Validate(plan.ProfiledGeneratedCode);
        HashValue(plan.SourceHash);HashValue(plan.GeneratedBodyHash);Bound(plan.MethodKey,4096);Bound(plan.GeneratorVersion,256);Bound(plan.GeneratorBuild,256);
        if(plan.CallSite is{} site){
            Bound(site.Path,4096);Bound(site.Caller,4096);
            if(site.Path.Length==0||site.Caller.Length==0||site.Start<0||site.Length<=0||site.Length>int.MaxValue-site.Start||site.Path.Any(char.IsControl)||site.Caller.Any(char.IsControl)){throw new InvalidDataException("呼び出し位置が不正です。");}
            try{_=StrictUtf8.GetByteCount(site.Path);_=StrictUtf8.GetByteCount(site.Caller);}catch(EncoderFallbackException exception){throw new InvalidDataException("呼び出し位置の文字列が不正です。",exception);}
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var node in plan.Nodes){
            if(node is null||node.Inputs is null||node.Properties is null||!ids.Add(node.Id)||node.Start<0||node.Length<0){throw new InvalidDataException("計画ノードが不正です。");}
            Identifier(node.Id);Bound(node.Kind,256);Bound(node.Label,65536);Bound(node.Layer,256);
            if(node.Nullability is not ("Unknown" or "Nullable" or "NotNullable")||node.Measurement is not ("NotCollected" or "NotIndependentlyMeasured")){throw new InvalidDataException("型または計測状態が不正です。");}
            if(node.OutputType is{} type){
                if(type.Length>4096||type.Any(char.IsControl)){throw new InvalidDataException("出力型の表示文字列が不正です。");}
                try{_=StrictUtf8.GetByteCount(type);}catch(EncoderFallbackException exception){throw new InvalidDataException("出力型の表示文字列が不正です。",exception);}
            }
            if(node.Properties.Count>256||node.Inputs.Length>ExecutionPlanDocument.MaximumEdges){throw new InvalidDataException("ノード情報が上限を超えています。");}
            foreach(var property in node.Properties){Bound(property.Key,1024);Bound(property.Value,65536);}
        }
        if(plan.Nodes.Any(node=>node.Inputs.Any(input=>input is null||!ids.Contains(input)))||plan.Edges.Any(edge=>edge is null||!ids.Contains(edge.From)||!ids.Contains(edge.To)||string.IsNullOrWhiteSpace(edge.Kind))){throw new InvalidDataException("接続先のない計画です。");}
        foreach(var edge in plan.Edges){Bound(edge.Kind,256);if(edge.InputRole is{} role){Bound(role,1024);}}
        if(plan.Diagnostics.Length>256){throw new InvalidDataException("診断数が上限を超えています。");}
        foreach(var diagnostic in plan.Diagnostics){Bound(diagnostic,65536);}
        if(plan.Trace.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined)){throw new InvalidDataException("最適化追跡の型が不正です。");}
    }
    private static void Identifier(string? value){if(string.IsNullOrWhiteSpace(value)||value.Length>4096){throw new InvalidDataException("識別子が不正です。");}}
    private static string? SafeType(string? value){
        if(value is null){return null;}var type=SyntaxFactory.ParseTypeName(value);
        if(type.ContainsDiagnostics){return null;}return string.Join(" ",type.DescendantTokens().Select(token=>token.Text));
    }
    private static void HashValue(string? value){if(value is null||value.Length!=64||value.Any(character=>!Uri.IsHexDigit(character))){throw new InvalidDataException("ハッシュの形式が不正です。");}}
    private static void Bound(string? value,int length){if(value is null||value.Length>length){throw new InvalidDataException("計画の文字列が上限を超えています。");}}
    private static void ValidateJson(JsonElement value,bool dictionary=false){
        if(value.ValueKind==JsonValueKind.Object){
            var names=new HashSet<string>(dictionary?StringComparer.Ordinal:StringComparer.OrdinalIgnoreCase);
            foreach(var property in value.EnumerateObject()){
                if(!names.Add(property.Name)){throw new InvalidDataException("JSONのプロパティが重複しています。");}
                ValidateJson(property.Value,string.Equals(property.Name,"properties",StringComparison.OrdinalIgnoreCase));
            }
        }else if(value.ValueKind==JsonValueKind.Array){foreach(var item in value.EnumerateArray()){ValidateJson(item);}}
        else if(value.ValueKind==JsonValueKind.String&&value.GetString()!.Length>65536){throw new InvalidDataException("計画の文字列が上限を超えています。");}
    }
}
