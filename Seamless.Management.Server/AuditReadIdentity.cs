using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Seamless.Management.Server;

internal static class AuditReadIdentity{
    internal static byte[] KeyHash(AuditReadKey key){using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);Text(hash,"Seamless.Audit.ReadKey.v2");Key(hash,key);return hash.GetHashAndReset();}
    internal static Guid Id(AuditReadKey key)=>new(KeyHash(key).AsSpan(0,16));
    internal static string BatchHash(AuditReadDelta[] deltas){using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);Text(hash,"Seamless.Audit.ReadBatch.v2");Number(hash,deltas.Length);foreach(var delta in deltas){Key(hash,delta.Key);Number(hash,delta.Count);}return "v2:"+Convert.ToHexString(hash.GetHashAndReset());}
    private static void Key(IncrementalHash hash,AuditReadKey key){Number(hash,key.WindowStart.UtcTicks);Number(hash,key.WindowEnd.UtcTicks);Span<byte> guid=stackalloc byte[16];key.AuthorityId.TryWriteBytes(guid);hash.AppendData(guid);Text(hash,key.SubjectId);Text(hash,key.Database);Text(hash,key.QueryId);Text(hash,key.CodeVersion);if(key.PolicyVersion.HasValue){Number(hash,1);Number(hash,key.PolicyVersion.Value);}else{Number(hash,0);}}
    private static void Number(IncrementalHash hash,long value){Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteInt64LittleEndian(bytes,value);hash.AppendData(bytes);}
    private static void Text(IncrementalHash hash,string? value){if(value is null){Number(hash,-1);return;}Number(hash,value.Length);Span<byte> bytes=stackalloc byte[256];for(var offset=0;offset<value.Length;){var count=Math.Min(128,value.Length-offset);for(var index=0;index<count;index++){BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(index*2,2),value[offset+index]);}hash.AppendData(bytes[..(count*2)]);offset+=count;}}
}
