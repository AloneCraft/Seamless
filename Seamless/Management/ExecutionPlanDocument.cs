using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

namespace Seamless.Management;

public sealed record ExecutionPlanNode(string Id,string Kind,string Label,string[] Inputs,string? QueryId,string? SourcePath,int Start,int Length,Dictionary<string,string> Properties){
    public string Layer{get;init;}="Unknown";
    public string? OutputType{get;init;}
    public string Nullability{get;init;}="Unknown";
    public string Measurement{get;init;}="NotCollected";
}
public sealed record ExecutionPlanEdge(string From,string To,string Kind,string? InputRole=null);
public sealed record ExecutionPlanCallSite(string Path,int Start,int Length,string Caller);
public sealed record ExecutionPlanDocument(int SchemaVersion,string MethodKey,string GeneratorVersion,string SourceHash,string GeneratedBodyHash,ExecutionPlanNode[] Nodes,JsonElement Trace,string GeneratorBuild=""){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ExecutionPlanCallSite? CallSite{get;init;}
    public ExecutionPlanEdge[] Edges{get;init;}=[];
    public bool MetadataComplete{get;init;}=true;
    public int OmittedNodeCount{get;init;}
    public string[] Diagnostics{get;init;}=[];
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GeneratedExecutionCode? GeneratedCode{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GeneratedExecutionCode? ProfiledGeneratedCode{get;init;}
    public const int MaximumMetadataBytes=2*1024*1024;
    public const int MaximumNodes=2048;
    public const int MaximumEdges=8192;
    public string ToDot(){
        var result=new StringBuilder("digraph ExecutionPlan {\n  rankdir=TB;\n  node [shape=box];\n");
        foreach(var node in this.Nodes){
            result.Append("  ").Append(Quote(node.Id)).Append(" [label=").Append(Quote(node.Label)).AppendLine("];");
            if(this.Edges.Length==0){foreach(var input in node.Inputs){result.Append("  ").Append(Quote(input)).Append(" -> ").Append(Quote(node.Id)).AppendLine(";");}}
        }
        foreach(var edge in this.Edges){
            result.Append("  ").Append(Quote(edge.From)).Append(" -> ").Append(Quote(edge.To)).Append(" [label=").Append(Quote(edge.InputRole??edge.Kind)).Append(", tooltip=").Append(Quote(edge.Kind)).AppendLine("];");
        }
        return result.AppendLine("}").ToString();
    }
    private static string Quote(string text)=>"\""+text.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal).Replace("\r","\\r",StringComparison.Ordinal).Replace("\n","\\n",StringComparison.Ordinal)+"\"";
    /// <summary>Assembly.Load/属性のコンストラクター呼出しを行わずPEだけを読む。</summary>
    public static ExecutionPlanDocument[] Read(Stream assembly){
        using var pe=new PEReader(assembly,PEStreamOptions.LeaveOpen);
        if(!pe.HasMetadata){throw new BadImageFormatException(".NETメタデータがありません。");}
        var reader=pe.GetMetadataReader();var plans=new List<ExecutionPlanDocument>();
        foreach(var handle in reader.CustomAttributes){
            var attribute=reader.GetCustomAttribute(handle);StringHandle name,ns;
            if(attribute.Constructor.Kind==HandleKind.MethodDefinition){var type=reader.GetTypeDefinition(reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType());name=type.Name;ns=type.Namespace;}
            else if(attribute.Constructor.Kind==HandleKind.MemberReference){var parent=reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;if(parent.Kind!=HandleKind.TypeReference){continue;}var type=reader.GetTypeReference((TypeReferenceHandle)parent);name=type.Name;ns=type.Namespace;}
            else{continue;}
            if(reader.GetString(name)!="ExecutionPlanAttribute"||reader.GetString(ns)!="Seamless.FLinq"){continue;}
            if(attribute.Parent.Kind!=HandleKind.MethodDefinition){throw new InvalidDataException("計画属性の対象がメソッドではありません。");}
            var blob=reader.GetBlobReader(attribute.Value);if(blob.ReadUInt16()!=1){throw new InvalidDataException("計画属性の形式が不正です。");}
            var version=blob.ReadInt32();if(version is not (1 or 2)){throw new NotSupportedException("未対応の計画スキーマ: "+version);}
            var json=blob.ReadSerializedString()??throw new InvalidDataException("計画データがありません。");
            if(version==2&&Encoding.UTF8.GetByteCount(json)>MaximumMetadataBytes){throw new InvalidDataException("計画メタデータが上限を超えています。");}
            var plan=JsonSerializer.Deserialize<ExecutionPlanDocument>(json,ManagementJson.Options)??throw new InvalidDataException("計画が不正です。");
            if(plan.SchemaVersion!=version||plan.Nodes is null||plan.Nodes.Any(node=>node is null||string.IsNullOrEmpty(node.Id)||node.Inputs is null||node.Properties is null)||plan.Nodes.Select(node=>node.Id).Distinct(StringComparer.Ordinal).Count()!=plan.Nodes.Length){throw new InvalidDataException("計画の版またはノード識別子が不正です。");}
            var ids=plan.Nodes.Select(node=>node.Id).ToHashSet(StringComparer.Ordinal);if(plan.Nodes.Any(node=>node.Inputs.Any(input=>!ids.Contains(input)))){throw new InvalidDataException("接続先のない計画です。");}
            if(plan.Edges is null||plan.Diagnostics is null||plan.Edges.Any(edge=>edge is null||!ids.Contains(edge.From)||!ids.Contains(edge.To)||string.IsNullOrEmpty(edge.Kind))){throw new InvalidDataException("意味付き接続が不正です。");}
            if(version==2&&(plan.Nodes.Length>MaximumNodes||plan.Edges.Length>MaximumEdges||plan.OmittedNodeCount<0||plan.MetadataComplete&&plan.OmittedNodeCount>0)){throw new InvalidDataException("計画の収集状態または件数が不正です。");}
            if(plan.CallSite is{} site&&(string.IsNullOrEmpty(site.Path)||string.IsNullOrEmpty(site.Caller)||site.Path.Length>4096||site.Caller.Length>4096||site.Path.Any(char.IsControl)||site.Caller.Any(char.IsControl)||site.Start<0||site.Length<=0||site.Length>int.MaxValue-site.Start)){throw new InvalidDataException("計画の呼び出し位置が不正です。");}
            GeneratedExecutionCodeValidator.Validate(plan.GeneratedCode);
            GeneratedExecutionCodeValidator.Validate(plan.ProfiledGeneratedCode);
            plans.Add(plan);
        }
        return plans.ToArray();
    }
}
