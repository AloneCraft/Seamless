using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using MemoryPack;

namespace Seamless.Database.Gateway.Hosting;

// MemoryPack 1.x has a process-wide strong Type cache and no public unregister API.
// [版] MemoryPack は常に最新の 1.x を使う。版の番号ではなく、消す対象の内部の入れ物(型を鍵にした辞書)があるかを起動時に確かめ、
// 形が変わっていれば回収できる型の Native クエリを始めずに止める。1.21.4 と 1.22.0 で確かめた。
// The adapter runs only after execution leases end.
internal static class CollectibleFormatterCache{
    private static bool enabled;
    private static class Adapter{internal static readonly FieldInfo[] Fields=Bind();}
    private static FieldInfo[] Bind(){
        var type=typeof(MemoryPackFormatterProvider);var version=type.Assembly.GetName().Version;
        if(version is not{Major:1}){throw new NotSupportedException("Collectible Native queries require a MemoryPack 1.x cache ownership adapter: "+version);}
        return new[]{"formatters","genericFormatterFactory","genericCollectionFormatterFactory"}.Select(name=>type.GetField(name,BindingFlags.Static|BindingFlags.NonPublic) is{} field&&typeof(IDictionary).IsAssignableFrom(field.FieldType)?field:throw new NotSupportedException("MemoryPack formatter cache layout changed: "+name)).ToArray();
    }
    internal static void Validate(){GC.KeepAlive(Adapter.Fields);Volatile.Write(ref enabled,true);}
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Release(AssemblyLoadContext owner){
        if(!Volatile.Read(ref enabled)){return;}
        foreach(var field in Adapter.Fields){
            var dictionary=(IDictionary)field.GetValue(null)!;var keys=new List<object>();
            foreach(DictionaryEntry entry in dictionary){if(entry.Key is Type key&&(BelongsTo(key,owner)||entry.Value is Type factory&&BelongsTo(factory,owner)||entry.Value is not null&&BelongsTo(entry.Value.GetType(),owner))){keys.Add(entry.Key);}}
            foreach(var key in keys){dictionary.Remove(key);}
        }
    }
    private static bool BelongsTo(Type type,AssemblyLoadContext owner){
        if(!type.IsCollectible){return false;}if(ReferenceEquals(AssemblyLoadContext.GetLoadContext(type.Assembly),owner)){return true;}
        if(type.HasElementType){return BelongsTo(type.GetElementType()!,owner);}return type.IsGenericType&&type.GetGenericArguments().Any(argument=>BelongsTo(argument,owner));
    }
}
