using System.Reflection;
using System.Runtime.CompilerServices;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
/// <summary>表と索引のキーの形の検査(対象は Benchmark.TpcE.Core の *Key 型と索引の持ち主)。</summary>
public sealed class TpcEKeys{
    [Fact,Trait("Category","Unit")]
    public void CopyWithKey_UpdatesCompositeKeyAndPreservesOtherColumns(){
        var original=new FinancialRow{CompanyId=1,Year=2026,Quarter=1,Revenue=123.45m};
        var updated=CopyWithKey(original,new FinancialKey(2,2027,2));

        Assert.Equal(original with{CompanyId=2,Year=2027,Quarter=2},updated);
        Assert.Equal(new FinancialKey(1,2026,1),GetKey<FinancialKey,FinancialRow>(original));
        Assert.Equal(new FinancialKey(2,2027,2),GetKey<FinancialKey,FinancialRow>(updated));
    }

    [Fact,Trait("Category","Unit")]
    public void CopyWithKey_MapsKeyColumnsInKeyOrder(){
        var original=new DailyMarketRow{Date=1,Symbol=new(2,3),Close=123.45m};
        var key=new DailyMarketKey(new(4,5),6);
        var updated=CopyWithKey(original,key);

        Assert.Equal(original with{Symbol=key.Symbol,Date=key.Date},updated);
        Assert.Equal(key,GetKey<DailyMarketKey,DailyMarketRow>(updated));
        Assert.Equal(1,original.Date);
        Assert.Equal(new(2,3),original.Symbol);
    }

    [Fact,Trait("Category","Unit")]
    public void CopyWithKey_UpdatesManagedRecordAndPreservesOtherColumns(){
        var original=new CompanyRow{Id=1,Name="alpha",Ceo="ceo",Description="detail"};
        var updated=CopyWithKey(original,new CompanyKey(2));

        Assert.Equal(original with{Id=2},updated);
        Assert.Equal(1,original.Id);
        Assert.Equal(new CompanyKey(2),GetKey<CompanyKey,CompanyRow>(updated));
    }

    private static TRecord CopyWithKey<TKey,TRecord>(TRecord record,TKey key)
        where TRecord:struct,Seamless.Collections.IRecord<TKey>{
        return record with{Key=key};
    }

    private static TKey GetKey<TKey,TRecord>(TRecord record)
        where TRecord:struct,Seamless.Collections.IRecord<TKey>{
        return record.Key;
    }

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
