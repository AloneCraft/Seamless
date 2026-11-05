using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

namespace Seamless.Management;

public sealed record ExecutionPlanNode(string Id,string Kind,string Label,string[] Inputs,string? QueryId,string? SourcePath,int Start,int Length,Dictionary<string,string> Properties);
public sealed record ExecutionPlanDocument(int SchemaVersion,string MethodKey,string GeneratorVersion,string SourceHash,string GeneratedBodyHash,ExecutionPlanNode[] Nodes,JsonElement Trace,string GeneratorBuild=""){
    public string ToDot(){
        var result=new StringBuilder("digraph ExecutionPlan {\n  rankdir=LR;\n  node [shape=box];\n");
        foreach(var node in this.Nodes){
            result.Append("  ").Append(Quote(node.Id)).Append(" [label=").Append(Quote(node.Label)).AppendLine("];");
            foreach(var input in node.Inputs){result.Append("  ").Append(Quote(input)).Append(" -> ").Append(Quote(node.Id)).AppendLine(";");}
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
            var version=blob.ReadInt32();if(version!=1){throw new NotSupportedException("未対応の計画スキーマ: "+version);}
            var json=blob.ReadSerializedString()??throw new InvalidDataException("計画データがありません。");
            var plan=JsonSerializer.Deserialize<ExecutionPlanDocument>(json,ManagementJson.Options)??throw new InvalidDataException("計画が不正です。");
            if(plan.SchemaVersion!=version||plan.Nodes.Select(node=>node.Id).Distinct(StringComparer.Ordinal).Count()!=plan.Nodes.Length){throw new InvalidDataException("計画の版またはノード識別子が不正です。");}
            var ids=plan.Nodes.Select(node=>node.Id).ToHashSet(StringComparer.Ordinal);if(plan.Nodes.Any(node=>node.Inputs.Any(input=>!ids.Contains(input)))){throw new InvalidDataException("接続先のない計画です。");}
            plans.Add(plan);
        }
        return plans.ToArray();
    }
}
