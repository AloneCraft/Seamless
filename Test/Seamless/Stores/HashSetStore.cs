using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>共通ストアから作った Tsavorite データ本体の件数管理(件数を管理する操作用オブジェクトと、保持しない操作用オブジェクト)の仕様。</summary>
public class HashSetStore{
    [Fact,Trait("Category","Unit")]public void Create(){
        {
            //件数を管理する操作用オブジェクトを複数スレッドで使っても、データ本体の件数は正確
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            Parallel.For(0,4,thread=>{
                using var set=data.Create();
                for(long i=0;i<10_000;i++){
                    set.Add(thread*10_000L+i);
                }
                //重複した追加と、無い要素の削除は件数を変えない
                set.Add(thread*10_000L);
                set.Remove(-1);
            });
            Assert.Equal(40_000,data.GetLongCount());
            using var reader=data.Create();
            Assert.Equal(40_000,reader.GetLongCount());
            Assert.Equal(40_000,reader.Count);
        }
        {
            //削除と全削除で件数が変わり、別の操作用オブジェクトからも同じ件数が見える
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using var first=data.Create();
            using var second=data.Create();
            for(long i=0;i<100;i++){
                first.Add(i);
            }
            Assert.True(second.Remove(5));
            Assert.Equal(99,first.GetLongCount());
            second.Clear();
            Assert.Equal(0,first.GetLongCount());
        }
    }
    [Fact,Trait("Category","Unit")]public void CreateUncounted(){
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        long delta=0;
        {
            //件数を保持しない操作用オブジェクトの追加は、反映するまでデータ本体の件数に入らない
            Parallel.For(0,4,thread=>{
                using var set=data.CreateUncounted();
                long local=0;
                for(long i=0;i<1_000;i++){
                    if(set.Add(thread*1_000L+i)){
                        local++;
                    }
                }
                Interlocked.Add(ref delta,local);
            });
            Assert.Equal(0,data.GetLongCount());
        }
        {
            //集計した増減を 1 回で反映すると、件数が正確になる
            data.ApplyCountDelta(delta);
            Assert.Equal(4_000,data.GetLongCount());
            using var reader=data.Create();
            Assert.Equal(4_000,reader.GetLongCount());
        }
    }
    [Fact,Trait("Category","Unit")]public void GetLongCount(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-hashsetstore-tests",Guid.NewGuid().ToString("N"));
        try{
            {
                //挿入か上書きか分からない書き込み(辞書の代入)のあとは、全走査で数え直して正確な件数を返す
                using var store=CommonStore.CreateMemory();
                using var data=store.CreateTsavorite<int,string>("names");
                using var dictionary=data.Create();
                for(var i=0;i<100;i++){
                    dictionary[i]="a";
                }
                dictionary[0]="b";
                Assert.Equal(100,data.GetLongCount());
                dictionary.Add(100,"c");
                Assert.Equal(101,dictionary.GetLongCount());
            }
            {
                //保存した件数は、開き直したあとも保たれる
                var directory=Path.Combine(root,"count");
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids");
                    using(var set=data.Create()){
                        for(long i=0;i<500;i++){
                            set.Add(i);
                        }
                    }
                    store.Save();
                }
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids");
                    Assert.Equal(500,data.GetLongCount());
                }
            }
            {
                //旧 API で作ったデータ本体では件数の反映はできない
                using var legacy=Store.Create(MemorySize:1L<<20).Create<long>();
                Assert.Throws<NotSupportedException>(()=>legacy.ApplyCountDelta(1));
            }
        }finally{
            if(Directory.Exists(root)){
                Directory.Delete(root,true);
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void Enumerator_AfterTheEnd_KeepsReturningFalse(){
        //使い切ったあとの MoveNext は false を返し続ける(IEnumerator の約束)。以前は最後のチャンクを読み直し、終わりを確かめずに何度も呼ぶ利用者が無限に回った
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        using var set=data.Create();
        set.Add(3);
        set.Add(5);
        using var enumerator=set.GetEnumerator();
        var seen=0;
        while(enumerator.MoveNext()){
            seen++;
        }
        Assert.Equal(2,seen);
        for(var repeat=0;repeat<3;repeat++){
            Assert.False(enumerator.MoveNext());
        }
    }
}
