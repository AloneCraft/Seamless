using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>まとめて書き換える集合演算(UnionWith・SymmetricExceptWith)も、1 件ずつの追加と同じ容量の確認を通る(BUG-0017)。</summary>
public class SetOperationCapacity{
    private static readonly TsavoriteTuning Small=new(){PageSize=1L<<12,SegmentSize=1L<<16,LogMemorySize=1L<<16,IndexSize=1L<<14,Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
    private static void AssertOriginalKept(global::Seamless.Collections.Tsavorite.HashSet<long> set,int count){
        var missing=0;
        for(long i=0;i<count;i++){
            if(!set.Contains(i)){
                missing++;
            }
        }
        Assert.Equal(0,missing);
    }
    [Fact,Trait("Category","Unit")]public void UnionWith_FullMemoryStore_ThrowsAndKeepsElements(){
        //小さいメモリストアへ、入りきらない大きさの集合を足すと、満杯の判定で例外になり、元の要素を失わない。以前は何も言わずに戻り、元の 500 件が全部消えた
        using var storeA=CommonStore.CreateMemory();
        using var a=storeA.CreateTsavoriteHashSet<long>("a",Small);
        using var storeB=CommonStore.CreateMemory();
        using var b=storeB.CreateTsavoriteHashSet<long>("b");
        using(var setB=b.Create()){
            for(long i=1_000_000;i<1_010_000;i++){
                setB.Add(i);
            }
        }
        using var setA=a.Create();
        for(long i=0;i<500;i++){
            setA.Add(i);
        }
        using var other=b.Create();
        Assert.Throws<OutOfMemoryException>(()=>setA.UnionWith(other));
        AssertOriginalKept(setA,500);
        Assert.True(storeA.Budget.MemoryBytes<=storeA.Budget.MemoryLimitBytes);
    }
    [Fact,Trait("Category","Unit")]public void SymmetricExceptWith_FullMemoryStore_ThrowsAndKeepsElements(){
        using var storeA=CommonStore.CreateMemory();
        using var a=storeA.CreateTsavoriteHashSet<long>("a",Small);
        using var storeB=CommonStore.CreateMemory();
        using var b=storeB.CreateTsavoriteHashSet<long>("b");
        using(var setB=b.Create()){
            for(long i=1_000_000;i<1_010_000;i++){
                setB.Add(i);
            }
        }
        using var setA=a.Create();
        for(long i=0;i<500;i++){
            setA.Add(i);
        }
        using var other=b.Create();
        Assert.Throws<OutOfMemoryException>(()=>setA.SymmetricExceptWith(other));
        AssertOriginalKept(setA,500);
    }
    [Fact,Trait("Category","Unit")]public void UnionWith_FitsInMemory_StillWorks(){
        //入る大きさなら、これまでどおり和集合になる
        using var storeA=CommonStore.CreateMemory();
        using var a=storeA.CreateTsavoriteHashSet<long>("a");
        using var storeB=CommonStore.CreateMemory();
        using var b=storeB.CreateTsavoriteHashSet<long>("b");
        using var setA=a.Create();
        using var setB=b.Create();
        for(long i=0;i<1000;i++){
            setA.Add(i);
            setB.Add(i+500);
        }
        setA.UnionWith(setB);
        Assert.Equal(1500,setA.GetLongCount());
        setA.SymmetricExceptWith(setB);
        Assert.Equal(500,setA.GetLongCount());
    }
}
