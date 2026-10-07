using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>件数の不変条件(INV-06)。シングル用は集合演算のあとも件数を「不明」にせず、1 件ずつの増減で正確に保つ。</summary>
public class CountInvariant{
    public enum SetOperation{UnionWith,ExceptWith,IntersectWith,SymmetricExceptWith}
    private static long CountByEnumeration(global::Seamless.Collections.Tsavorite.HashSet<long> set){
        var count=0L;
        foreach(var _ in set){
            count++;
        }
        return count;
    }
    private static long Expected(SetOperation operation,IEnumerable<long> left,IEnumerable<long> right){
        var expected=new global::System.Collections.Generic.HashSet<long>(left);
        switch(operation){
            case SetOperation.UnionWith:expected.UnionWith(right);break;
            case SetOperation.ExceptWith:expected.ExceptWith(right);break;
            case SetOperation.IntersectWith:expected.IntersectWith(right);break;
            case SetOperation.SymmetricExceptWith:expected.SymmetricExceptWith(right);break;
        }
        return expected.Count;
    }
    private static void Apply(SetOperation operation,global::Seamless.Collections.Tsavorite.HashSet<long> set,global::Seamless.Collections.Tsavorite.HashSet<long> other){
        switch(operation){
            case SetOperation.UnionWith:set.UnionWith(other);break;
            case SetOperation.ExceptWith:set.ExceptWith(other);break;
            case SetOperation.IntersectWith:set.IntersectWith(other);break;
            case SetOperation.SymmetricExceptWith:set.SymmetricExceptWith(other);break;
        }
    }
    [Theory,Trait("Category","Unit")]
    [InlineData(SetOperation.UnionWith,false)]
    [InlineData(SetOperation.ExceptWith,false)]
    [InlineData(SetOperation.IntersectWith,false)]
    [InlineData(SetOperation.SymmetricExceptWith,false)]
    [InlineData(SetOperation.UnionWith,true)]
    [InlineData(SetOperation.ExceptWith,true)]
    [InlineData(SetOperation.IntersectWith,true)]
    [InlineData(SetOperation.SymmetricExceptWith,true)]
    public void SetOperation_KeepsManagedCount(SetOperation operation,bool readOnlyAndDisk){
        //集合演算のあとも件数は分かったままで、列挙した件数・BCL の結果と一致する。
        //readOnlyAndDisk では元の要素を読み取り専用の領域とディスクへ追い出し、上書き・削除が新しいレコードになる経路と、保留(Pending)の経路も通す
        var left=Enumerable.Range(0,2000).Select(i=>(long)i).ToArray();
        var right=Enumerable.Range(1000,2000).Select(i=>(long)i).ToArray();
        using var storeA=readOnlyAndDisk?CommonStore.CreateTemporary():CommonStore.CreateMemory();
        using var a=readOnlyAndDisk
            ?storeA.CreateTsavoriteHashSet<long>("a",new TsavoriteTuning{PageSize=4096,LogMemorySize=1L<<14})
            :storeA.CreateTsavoriteHashSet<long>("a");
        using var storeB=CommonStore.CreateMemory();
        using var b=storeB.CreateTsavoriteHashSet<long>("b");
        using var set=a.Create();
        using var other=b.Create();
        foreach(var item in left){
            set.Add(item);
        }
        foreach(var item in right){
            other.Add(item);
        }
        if(readOnlyAndDisk){
            Assert.True(a.GetStatistics().DiskLogBytes>0);
            a.Store.Log.ShiftReadOnlyAddress(a.Store.Log.TailAddress,true);
        }
        Apply(operation,set,other);
        Assert.True(a.TryGetManagedCount(out var managed));
        var expected=Expected(operation,left,right);
        Assert.Equal(expected,managed);
        Assert.Equal(expected,CountByEnumeration(set));
        //同じ演算をもう一度(和・差・積は変化なし、対称差は元へ戻る)
        Apply(operation,set,other);
        Assert.True(a.TryGetManagedCount(out managed));
        var twice=operation==SetOperation.SymmetricExceptWith?left.Length:expected;
        Assert.Equal(twice,managed);
        Assert.Equal(twice,CountByEnumeration(set));
    }
    [Fact,Trait("Category","Unit")]
    public void SetOperation_FailedMidway_CountMatchesContents(){
        //和集合が満杯の判定で途中で止まっても、それまでに足した分だけ件数が増えている
        using var storeA=CommonStore.CreateMemory();
        using var a=storeA.CreateTsavoriteHashSet<long>("a",new TsavoriteTuning{PageSize=1L<<12,SegmentSize=1L<<16,LogMemorySize=1L<<16,IndexSize=1L<<14});
        using var storeB=CommonStore.CreateMemory();
        using var b=storeB.CreateTsavoriteHashSet<long>("b");
        using var set=a.Create();
        using var other=b.Create();
        for(long i=0;i<500;i++){
            set.Add(i);
        }
        for(long i=0;i<10_000;i++){
            other.Add(i);
        }
        Assert.Throws<OutOfMemoryException>(()=>set.UnionWith(other));
        Assert.True(a.TryGetManagedCount(out var managed));
        Assert.True(managed>500);
        Assert.Equal(CountByEnumeration(set),managed);
    }
    [Fact,Trait("Category","Unit")]
    public void ParallelRegion_SameKeysFromManyThreads_CountIsSumOfDeltas(){
        //複数のスレッドが同じキーを同時に足し・消しても、新規・削除になるのはそれぞれ 1 つのスレッドだけなので、
        //区間を閉じると、走査せずに件数が確定し、走査した件数と一致する(何度も繰り返して競合を起こす)
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        using(var set=data.Create()){
            for(long i=0;i<1000;i++){
                set.Add(i);
            }
        }
        for(var round=0;round<20;round++){
            using(var region=data.BeginParallel()){
                Parallel.For(0,8,thread=>{
                    using var set=region.Create();
                    for(long i=0;i<3000;i++){
                        //全スレッドが同じキーを足す。半分のスレッドは別の範囲を消す
                        set.Add(i);
                        if(thread%2==0){
                            set.Remove(round%2==0?i+1500:i);
                        }
                    }
                });
                Assert.False(data.TryGetManagedCount(out _));
            }
            Assert.True(data.TryGetManagedCount(out var managed));
            using var reader=data.Create();
            Assert.Equal(CountByEnumeration(reader),managed);
        }
    }
    [Fact,Trait("Category","Unit")]
    public void ParallelRegion_DictionaryAssignment_CountsOnlyNewKeys(){
        //マルチ用の辞書の代入は、新規のときだけ数える(同じキーを複数のスレッドが代入しても 1 件)
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavorite<int,int>("values");
        using(var region=data.BeginParallel()){
            Parallel.For(0,8,thread=>{
                using var dictionary=region.Create();
                for(var i=0;i<5000;i++){
                    dictionary[i]=thread;
                }
                for(var i=0;i<1000;i++){
                    dictionary.Remove(i,out _);
                }
            });
        }
        Assert.True(data.TryGetManagedCount(out var managed));
        Assert.Equal(4000,managed);
        using var reader=data.Create();
        Assert.Equal(4000,reader.Count);
        var enumerated=0;
        foreach(var _ in reader){
            enumerated++;
        }
        Assert.Equal(4000,enumerated);
    }
    [Fact,Trait("Category","Unit")]
    public void ParallelRegion_RecordsOnDisk_CountIsSumOfDeltas(){
        //元の要素がディスクにあり、削除・追加が保留(Pending)や新しいレコードになる経路でも、合計が件数と一致する
        using var store=CommonStore.CreateTemporary();
        using var data=store.CreateTsavoriteHashSet<long>("ids",new TsavoriteTuning{PageSize=4096,LogMemorySize=1L<<14});
        using(var set=data.Create()){
            for(long i=0;i<4000;i++){
                set.Add(i);
            }
        }
        Assert.True(data.GetStatistics().DiskLogBytes>0);
        data.Store.Log.ShiftReadOnlyAddress(data.Store.Log.TailAddress,true);
        using(var region=data.BeginParallel()){
            Parallel.For(0,4,thread=>{
                using var set=region.Create();
                for(long i=0;i<4000;i+=2){
                    set.Remove(i);
                }
                for(long i=3000;i<6000;i++){
                    set.Add(i);
                }
            });
        }
        Assert.True(data.TryGetManagedCount(out var managed));
        var expected=Enumerable.Range(0,6000).Count(i=>i>=3000||i%2==1);
        Assert.Equal(expected,managed);
        using var reader=data.Create();
        Assert.Equal(expected,CountByEnumeration(reader));
    }
    [Fact,Trait("Category","Unit")]
    public void ParallelRegion_UnknownBaseCount_StaysUnknownAndRescans(){
        //区間を開いたときに件数が不明なら、閉じたあとも不明のまま。次に求めたときに数え直す
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        using(var set=data.Create()){
            for(long i=0;i<100;i++){
                set.Add(i);
            }
        }
        data.InvalidateManagedCount();
        using(var region=data.BeginParallel()){
            using var set=region.Create();
            set.Add(1000);
        }
        Assert.False(data.TryGetManagedCount(out _));
        Assert.Equal(101,data.GetLongCount());
    }
}
