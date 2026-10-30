using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Benchmark.Dynamic.Core;

/// <summary>実行ID・生成シード・ノードIDから独立した、実行する処理の識別子。</summary>
public static class QueryWorkloadIdentity{
    public static string Compute(QueryIrDocument document){
        var root=JsonSerializer.SerializeToNode(document.Root,QueryIrJson.Options)!;
        RemoveIdentity(root);
        var json=JsonSerializer.Serialize(new{document.IrSchemaVersion,document.ElementType,Root=root});
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
    private static void RemoveIdentity(JsonNode node){
        if(node is JsonObject obj){
            obj.Remove("nodeId");
            foreach(var item in obj){if(item.Value is not null){RemoveIdentity(item.Value);}}
        }else if(node is JsonArray array){
            foreach(var item in array){if(item is not null){RemoveIdentity(item);}}
        }
    }
}
