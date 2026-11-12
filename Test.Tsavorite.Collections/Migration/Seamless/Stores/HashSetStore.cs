using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>共通ストアから作った Tsavorite データ本体の件数管理(シングル用の操作用オブジェクト)の仕様。並列区間の仕様は ParallelRegion。</summary>
public class HashSetStore{
    [Fact,Trait("Category","Unit")]public void Create(){
        {
            //シングル用で追加・重複した追加・無い要素の削除をしても、データ本体の件数は正確
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using(var set=data.Create()){
                for(long i=0;i<40_000;i++){
                    set.Add(i);
                }
                set.Add(0);
                set.Remove(-1);
            }
            Assert.Equal(40_000,data.GetLongCount());
            using var reader=data.Create();
            Assert.Equal(40_000,reader.GetLongCount());
            Assert.Equal(40_000,reader.Count);
        }
        {
            //削除と全削除で件数が変わり、あとで作った操作用オブジェクトからも同じ件数が見える
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using(var first=data.Create()){
                for(long i=0;i<100;i++){
                    first.Add(i);
                }
            }
            using(var second=data.Create()){
                Assert.True(second.Remove(5));
                Assert.Equal(99,second.GetLongCount());
            }
            Assert.Equal(99,data.GetLongCount());
            using(var third=data.Create()){
                third.Clear();
            }
            Assert.Equal(0,data.GetLongCount());
        }
    }
    [Fact,Trait("Category","Unit")]public void GetLongCount(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-hashsetstore-tests",Guid.NewGuid().ToString("N"));
        try{
            {
                //辞書の代入(新規と上書き)のあとも、件数は正確
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
