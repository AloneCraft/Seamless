using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>並列区間(データ本体.BeginParallel)とマルチ用の操作用オブジェクトの仕様。</summary>
public class ParallelRegion{
    [Fact,Trait("Category","Unit")]public void Create(){
        {
            //スレッドごとに作ったマルチ用で書き、区間を閉じると、各マルチ用の増減の合計で件数が確定する(数え直さない)
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using(var region=data.BeginParallel(40_000)){
                Parallel.For(0,4,thread=>{
                    using var set=region.Create();
                    for(long i=0;i<10_000;i++){
                        set.Add(thread*10_000L+i);
                    }
                    //重複した追加と、無い要素の削除
                    Assert.False(set.Add(thread*10_000L));
                    Assert.False(set.Remove(-1));
                });
                //区間の中では件数は分からない(マルチ用は件数を増減しない)
                Assert.False(data.TryGetManagedCount(out _));
            }
            Assert.True(data.TryGetManagedCount(out var managed));
            Assert.Equal(40_000,managed);
            using var reader=data.Create();
            Assert.Equal(40_000,reader.Count);
        }
        {
            //マルチ用の型は件数の窓口を持たない。ScanCount は毎回全件を数える
            Assert.False(typeof(ICollection<long>).IsAssignableFrom(typeof(ParallelHashSet<long>)));
            Assert.False(typeof(IReadOnlyCollection<long>).IsAssignableFrom(typeof(ParallelHashSet<long>)));
            Assert.Null(typeof(ParallelHashSet<long>).GetProperty("Count"));
            Assert.Null(typeof(ParallelHashSet<long>).GetMethod("Clear"));
            Assert.False(typeof(ICollection<KeyValuePair<int,string>>).IsAssignableFrom(typeof(IParallelDictionary<int,string>)));
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using var region=data.BeginParallel();
            using var set=region.Create();
            set.Add(1);
            set.Add(2);
            Assert.Equal(2,set.ScanCount());
            Assert.True(set.Contains(2));
            var items=new List<long>();
            foreach(var item in set){
                items.Add(item);
            }
            items.Sort();
            Assert.Equal([1L,2L],items);
        }
        {
            //辞書: 追加・代入(上書き)・取り出し・削除
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavorite<int,string>("names");
            using(var region=data.BeginParallel()){
                Parallel.For(0,4,thread=>{
                    using var dictionary=region.Create();
                    for(var i=0;i<100;i++){
                        dictionary[thread*100+i]="a";
                    }
                    dictionary[thread*100]="b";
                    Assert.Equal("b",dictionary[thread*100]);
                    Assert.True(dictionary.Remove(thread*100+1,out var removed));
                    Assert.Equal("a",removed);
                    Assert.True(dictionary.ContainsKey(thread*100+2));
                });
                using var check=region.Create();
                Assert.Equal(396,check.ScanCount());
            }
            Assert.Equal(396,data.GetLongCount());
        }
        {
            //値型の辞書(シリアライザを実行時に選ぶ経路)
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavorite<int,int>("numbers");
            using(var region=data.BeginParallel()){
                using var dictionary=region.Create();
                dictionary.Add(1,10);
                dictionary[2]=20;
                Assert.Equal(TryAddResult.KeyAlreadyExists,dictionary.TryAdd(1,11));
                Assert.Equal(2,dictionary.ScanCount());
            }
            Assert.Equal(2,data.GetLongCount());
        }
        {
            //キーを持つレコードの集合
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteSet<int,Score>("scores");
            using(var region=data.BeginParallel()){
                using var set=region.Create();
                Assert.True(set.Add(new(1,10)));
                Assert.False(set.Add(new(1,99)));
                Assert.True(set.TryGetValue(1,out var score));
                Assert.Equal(10,score.Value);
                Assert.Equal(1,set.ScanCount());
            }
            Assert.Equal(1,data.GetLongCount());
        }
    }
    [Fact,Trait("Category","Unit")]public void Dispose(){
        {
            //操作用オブジェクトが残っていても区間を閉じられ(例外にならない)、最後の操作用オブジェクトを閉じたときに仕上げる
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            var region=data.BeginParallel();
            var set=region.Create();
            set.Add(1);
            region.Dispose();
            region.Dispose();
            //閉じ中: 区間からもシングル用も作れない
            Assert.Throws<ObjectDisposedException>(()=>region.Create());
            Assert.Throws<InvalidOperationException>(()=>data.Create());
            set.Dispose();
            using var single=data.Create();
            Assert.Equal(1L,single.GetLongCount());
        }
        {
            //区間の作成と破棄を競わせても、閉じたあとは必ずシングル用を作れる
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            for(var round=0;round<200;round++){
                var region=data.BeginParallel();
                Parallel.Invoke(
                    ()=>{
                        try{
                            using var set=region.Create();
                            set.Add(round);
                        }catch(ObjectDisposedException){
                        }
                    },
                    ()=>region.Dispose());
                using var single=data.Create();
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void BeginParallel(){
        {
            //シングル用が開いている間は区間を開けない。区間が開いている間は2つ目の区間もシングル用も作れない
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using(var single=data.Create()){
                Assert.Throws<InvalidOperationException>(()=>data.BeginParallel());
            }
            using var region=data.BeginParallel();
            Assert.Throws<InvalidOperationException>(()=>data.BeginParallel());
            Assert.Throws<InvalidOperationException>(()=>data.Create());
        }
        {
            //見込み件数に合わせて索引を先に広げる
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            var before=data.GetStatistics().IndexBytes;
            using(data.BeginParallel(1_000_000)){
                Assert.True(data.GetStatistics().IndexBytes>=16L<<20);
            }
            Assert.True(before<16L<<20);
        }
        {
            //区間の中では索引を広げず、閉じたときに混んでいれば広げる
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            var before=data.GetStatistics().IndexBytes;
            using(var region=data.BeginParallel()){
                using(var set=region.Create()){
                    for(long i=0;i<200_000;i++){
                        set.Add(i);
                    }
                    Assert.Equal(before,data.GetStatistics().IndexBytes);
                }
            }
            Assert.True(data.GetStatistics().IndexBytes>before);
            Assert.Equal(200_000,data.GetLongCount());
        }
        {
            //区間の中で保存すると件数は記録しない。開き直すと数え直して正確
            var root=Path.Combine(Path.GetTempPath(),"parallel-region-"+Guid.NewGuid().ToString("N"));
            try{
                using(var store=CommonStore.Open(root)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids");
                    using var region=data.BeginParallel();
                    using var set=region.Create();
                    for(long i=0;i<100;i++){
                        set.Add(i);
                    }
                    store.Save();
                }
                using(var store=CommonStore.Open(root)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids");
                    Assert.False(data.TryGetManagedCount(out _));
                    Assert.Equal(100,data.GetLongCount());
                }
            }finally{
                if(Directory.Exists(root)){
                    Directory.Delete(root,true);
                }
            }
        }
        {
            //区間が開いている間は読み込めない
            var root=Path.Combine(Path.GetTempPath(),"parallel-region-"+Guid.NewGuid().ToString("N"));
            try{
                using var store=CommonStore.Open(root);
                using var data=store.CreateTsavoriteHashSet<long>("ids");
                var token=store.Save();
                using var region=data.BeginParallel();
                Assert.Throws<InvalidOperationException>(()=>store.Load(token));
            }finally{
                if(Directory.Exists(root)){
                    Directory.Delete(root,true);
                }
            }
        }
    }
}
/// <summary>キーを持つレコードの集合の確認用。</summary>
file readonly record struct Score(int Key,int Value):global::Seamless.Collections.IRecord<int>;
