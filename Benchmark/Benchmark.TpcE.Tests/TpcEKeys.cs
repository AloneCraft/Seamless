using System.Reflection;
using System.Runtime.CompilerServices;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
/// <summary>表と索引のキーの形の検査(対象は Benchmark.TpcE.Core の *Key 型と索引の持ち主)。</summary>
public sealed class TpcEKeys{
    [Fact,Trait("Category","Unit")]public void NoPadding(){
        {
            //Tsavorite と Persisted はキーをバイト列で比べるので、パディングがあると同じキーが別物になる。全キーで大きさ=項目の大きさの合計
            var keyTypes=typeof(TpcEDatabaseBase).Assembly.GetTypes().Where(static type=>type.IsValueType&&type.Name.EndsWith("Key",StringComparison.Ordinal)&&type.Namespace=="Benchmark.TpcE.Core").ToList();
            keyTypes.Add(typeof(NameHash));
            Assert.True(keyTypes.Count>=33);
            foreach(var type in keyTypes){
                Assert.Equal(FieldBytes(type),SizeOf(type));
            }
        }
    }
    private static int FieldBytes(Type type){
        if(type.IsPrimitive){
            return SizeOf(type);
        }
        var total=0;
        foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){
            total+=FieldBytes(field.FieldType);
        }
        return total;
    }
    private static int SizeOf(Type type)=>(int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null,null)!;
}
