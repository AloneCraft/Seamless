using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using MemoryPack;

namespace Seamless.Database.Client;

[MemoryPackable]
public sealed partial record NativeQueryRequest(string ModuleId,string QueryId,string Database,string SchemaFingerprint,byte[] Arguments,Guid TransactionId=default);
public sealed record NativeQueryDefinition(string Id,string Database,string SchemaFingerprint,string ArgumentsType,string ResultType,bool IsReadOnly=true);
public sealed record NativeModuleManifest(string ModuleId,string AssemblyPath,string AssemblySha256,string EntryType,string ProductVersion,IReadOnlyList<NativeQueryDefinition> Queries);
public interface INativeQueryContext{
    object Database{get;}
    string DatabaseName{get;}
    CancellationToken CancellationToken{get;}
}
/// <summary>ビルド済みモジュールの入口。ネットワークから実行コードは受け取らない。</summary>
public interface INativeQueryModule{
    IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context);
}
public sealed class NativeQueryException(string message):InvalidOperationException(message);

public static class NativeFrame{
    public const int MaximumBytes=16*1024*1024;
    public const byte Request=1,Row=2,Completed=3,Error=4;
    public static async ValueTask WriteAsync(Stream stream,byte kind,ReadOnlyMemory<byte> payload,CancellationToken cancellationToken=default){
        if(payload.Length>MaximumBytes-1){throw new InvalidDataException("Native frame exceeds the maximum size.");}
        var header=new byte[5];BinaryPrimitives.WriteInt32LittleEndian(header,payload.Length+1);header[4]=kind;
        await stream.WriteAsync(header,cancellationToken).ConfigureAwait(false);
        if(!payload.IsEmpty){await stream.WriteAsync(payload,cancellationToken).ConfigureAwait(false);}
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
    public static async ValueTask<(byte Kind,byte[] Payload)?> ReadAsync(Stream stream,CancellationToken cancellationToken=default){
        var header=new byte[4];var first=await stream.ReadAsync(header,cancellationToken).ConfigureAwait(false);if(first==0){return null;}
        await stream.ReadExactlyAsync(header.AsMemory(first),cancellationToken).ConfigureAwait(false);
        var length=BinaryPrimitives.ReadInt32LittleEndian(header);if(length<1||length>MaximumBytes){throw new InvalidDataException("Invalid native frame length.");}
        var bytes=new byte[length];await stream.ReadExactlyAsync(bytes,cancellationToken).ConfigureAwait(false);return(bytes[0],bytes[1..]);
    }
}
