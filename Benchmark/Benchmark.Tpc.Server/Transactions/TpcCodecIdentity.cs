using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Benchmark.Tpc.Core;
namespace Benchmark.Tpc.Server;
/// <summary>行・キーの構造とMemoryPack版をWALのcodec契約へ固定する。</summary>
internal static class TpcCodecIdentity{
    private static readonly ConcurrentDictionary<Type,string> Schemas=new();
    public static string For(ITableView table)=>table.Name+"|"+Schema(table.KeyType)+"|"+Schema(table.RowType)+"|memorypack:"+typeof(MemoryPack.MemoryPackSerializer).Assembly.GetName().Version+"|pointer:"+IntPtr.Size;
    private static string Schema(Type type)=>Schemas.GetOrAdd(type,static key=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Describe(key,[])))));
    private static string Describe(Type type,HashSet<Type> visiting){
        var name=type.FullName??type.Name;
        if(type.IsPrimitive||type==typeof(string)||type==typeof(decimal)){return name;}
        if(type.IsArray){return "array:"+type.GetArrayRank()+":"+Describe(type.GetElementType()!,visiting);}
        if(!visiting.Add(type)){return "ref:"+name;}
        try{
            var layout=type.StructLayoutAttribute;
            var attributes=type.GetCustomAttributesData().Where(static value=>value.AttributeType.Namespace=="MemoryPack"||value.AttributeType.FullName=="System.Runtime.CompilerServices.InlineArrayAttribute").OrderBy(static value=>value.AttributeType.FullName).Select(static value=>value.ToString());
            var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(static field=>field.MetadataToken).Select(field=>field.Name+":"+Describe(field.FieldType,visiting));
            return type.Assembly.GetName().Name+":"+name+"|"+layout?.Value+":"+layout?.Pack+":"+layout?.Size+"|"+string.Join(";",attributes)+"{"+string.Join(";",fields)+"}";
        }finally{visiting.Remove(type);}
    }
}
