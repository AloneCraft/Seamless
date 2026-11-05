using System.Text.Json;

namespace Seamless.Database.Client;

/// <summary>C#の実行テキストではなく、サーバーが再検証する有限ノードのプログラム。</summary>
public sealed record NativeLambdaNode(string Kind,string? Text=null,NativeLambdaNode[]? Children=null){
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeLambdaNode[] Nodes=>this.Children??[];
}

public sealed record NativeLambdaProgram(int Version,string DatabaseType,string DatabaseContract,string ArgumentsType,string ResultType,string ArgumentsContract,string ResultContract,NativeLambdaNode Body){
    public const int CurrentVersion=1;
    public const int MaximumBytes=128*1024;
    public const int MaximumNodes=2048;
    public const int MaximumDepth=48;
    private static readonly JsonSerializerOptions Json=new(){MaxDepth=MaximumDepth*2+8};

    public byte[] Encode(){
        this.ValidateBounds();
        var bytes=JsonSerializer.SerializeToUtf8Bytes(this,Json);
        if(bytes.Length>MaximumBytes){throw new InvalidDataException("The lambda program is too large.");}
        return bytes;
    }

    public static NativeLambdaProgram Decode(ReadOnlySpan<byte> bytes){
        if(bytes.Length is 0 or >MaximumBytes){throw new InvalidDataException("Invalid lambda program size.");}
        var program=JsonSerializer.Deserialize<NativeLambdaProgram>(bytes,Json)??throw new InvalidDataException("The lambda program is empty.");
        program.ValidateBounds();return program;
    }

    public void ValidateBounds(){
        if(this.Version!=CurrentVersion||this.Body is null){throw new InvalidDataException("Unsupported lambda program version.");}
        foreach(var name in new[]{this.DatabaseType,this.DatabaseContract,this.ArgumentsType,this.ResultType,this.ArgumentsContract,this.ResultContract}){
            if(string.IsNullOrWhiteSpace(name)||name.Length>2048){throw new InvalidDataException("Invalid lambda contract identifier.");}
        }
        var count=0;var active=new HashSet<NativeLambdaNode>(ReferenceEqualityComparer.Instance);
        void Visit(NativeLambdaNode node,int depth){
            if(node is null||++count>MaximumNodes||depth>MaximumDepth||!active.Add(node)){throw new InvalidDataException("The lambda program exceeds its structural bounds.");}
            try{
                if(node.Kind is null||node.Kind.Length is 0 or >32||node.Text?.Length>8192||node.Nodes.Length>MaximumNodes){throw new InvalidDataException("Invalid lambda node.");}
                foreach(var child in node.Nodes){Visit(child,depth+1);}
            }finally{active.Remove(node);}
        }
        Visit(this.Body,0);
    }
}

public static class NativeLambdaContracts{
    public static string Surface<TDatabase>()=>Surface(typeof(TDatabase));
    public static string Surface(Type type){
        var text=type.FullName+"|"+string.Join("|",type.GetProperties().OrderBy(static property=>property.Name,StringComparer.Ordinal).Select(property=>property.Name+":"+string.Join(",",property.PropertyType.GenericTypeArguments.Select(NativeWireContract.For))));
        return Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(text)));
    }
}

public sealed record NativePreparedLambda(Guid Epoch,ulong Handle);

/// <summary>ローカルと通信の入口。準備と実行を分離し、準備でDB処理を実行しない。</summary>
public interface INativeLambdaTransport{
    Task<NativePreparedLambda> PrepareAsync(NativeLambdaProgram program,CancellationToken cancellationToken);
    Task<byte[]> ExecuteAsync(NativePreparedLambda query,ReadOnlyMemory<byte> arguments,CancellationToken cancellationToken);
}

public sealed class NativeLambdaException(string code,string message,bool executionNotStarted=false):InvalidOperationException(message){
    public string Code{get;}=code;
    public bool ExecutionNotStarted{get;}=executionNotStarted;
}
