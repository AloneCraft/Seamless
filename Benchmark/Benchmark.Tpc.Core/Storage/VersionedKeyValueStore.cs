using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Seamless.Collections.Tsavorite;
using Seamless.Transactions.Collections;
namespace Benchmark.Tpc.Core;

/// <summary>索引の見出しと全32要素のchunkを同じMVCC domainに登録する。</summary>
public sealed class VersionedKeyValueStore<TKey,TValue>:IKeyValueStore<TKey,TValue> where TKey:unmanaged where TValue:unmanaged{
    private readonly VersionedTpcDomain domain;
    private readonly VersionedTransactionalTable<TKey,(TKey Key,TValue Value)> participant;
    public VersionedKeyValueStore(VersionedTpcDomain domain,string name,TsavoriteTuning tuning){
        this.domain=domain;this.Name=name;
        this.participant=domain.CreateTable<TKey,(TKey Key,TValue Value)>(name,static row=>row.Key,tuning,configure:static codecs=>codecs.Register<TValue>(new UnmanagedCodec()));
    }
    public string Name{get;}
    private TransactionCollection<TKey,(TKey Key,TValue Value)> Rows=>this.participant.For(this.domain.Current);
    public bool TryGet(TKey key,out TValue value){if(this.Rows.TryGetValue(key,out var row)){value=row.Value;return true;}value=default;return false;}
    public void Put(TKey key,TValue value)=>this.Rows[key]=(key,value);
    public bool Remove(TKey key)=>this.Rows.Remove(key);
    public IEnumerable<KeyValuePair<TKey,TValue>> Enumerate(){foreach(var row in this.Rows.Rows){yield return new(row.Key,row.Value);}}
    public void Dispose(){}
    // InlineArrayをfield一つとして符号化せず、従来の索引chunkと同じunmanaged全領域を保持する。
    private sealed class UnmanagedCodec:global::Seamless.Execution.Codecs.IAdapter<TValue>{
        private static readonly string identity="tpc-unmanaged-v1|"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Layout(typeof(TValue),new global::System.Collections.Generic.HashSet<Type>())+"|bytes="+Unsafe.SizeOf<TValue>()+"|little-endian="+BitConverter.IsLittleEndian)));
        public string FormatId=>identity;
        public int GetEncodedLength(TValue value)=>Unsafe.SizeOf<TValue>();
        public void Encode(TValue value,Span<byte> destination)=>MemoryMarshal.Write(destination,in value);
        public TValue Decode(ReadOnlySpan<byte> source){if(source.Length!=Unsafe.SizeOf<TValue>()){throw new InvalidDataException("Index value size mismatch.");}return MemoryMarshal.Read<TValue>(source);}
        private static string Layout(Type type,global::System.Collections.Generic.HashSet<Type> visiting){
            if(type.IsPrimitive||type.IsEnum){return type.AssemblyQualifiedName!;}
            if(!visiting.Add(type)){throw new NotSupportedException("Recursive unmanaged layout.");}
            try{
                var layout=type.StructLayoutAttribute;var array=type.GetCustomAttribute<InlineArrayAttribute>()?.Length??0;
                var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(static field=>field.MetadataToken)
                    .Select(field=>field.Name+"@"+field.GetCustomAttribute<FieldOffsetAttribute>()?.Value+":"+Layout(field.FieldType,visiting));
                return type.AssemblyQualifiedName+"|layout="+layout?.Value+"|pack="+layout?.Pack+"|size="+layout?.Size+"|inline="+array+"{"+string.Join(";",fields)+"}";
            }finally{visiting.Remove(type);}
        }
    }
}
