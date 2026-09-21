using CommonStore=global::Seamless.Execution.Store;
using ResourceLimitException=global::Seamless.Execution.ResourceLimitException;
using StoreKind=global::Seamless.Execution.StoreKind;
namespace Test.Seamless.Stores;
/// <summary>共通ストアから Tsavorite のデータ本体を作る拡張メソッドの仕様。</summary>
public class StoreExtensions{
    private static string NewRoot(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-tsavorite-store-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    // メモリ上のログを小さくして、少ない件数でディスク退避や上限を起こすための詳細設定。
    private static readonly TsavoriteTuning Small=new(){PageSize=1L<<12,SegmentSize=1L<<16,LogMemorySize=1L<<16,IndexSize=1L<<12};
    [Fact,Trait("Category","Unit")]public void CreateTsavorite(){
        var root=NewRoot();
        try{
            {
                //メモリストア: 追加・上書き・読み取りができる
                using var store=CommonStore.CreateMemory();
                using var data=store.CreateTsavorite<int,string>("names");
                using var dictionary=data.Create();
                dictionary.Add(1,"a");
                dictionary[2]="b";
                dictionary[2]="c";
                Assert.Equal("a",dictionary[1]);
                Assert.True(dictionary.TryGetValue(2,out var value));
                Assert.Equal("c",value);
                Assert.Equal(2,dictionary.GetLongCount());
            }
            {
                //永続ストア: 保存した内容だけが開き直したあとに残る
                var directory=Path.Combine(root,"dictionary");
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavorite<int,string>("names");
                    using(var dictionary=data.Create()){
                        for(var i=0;i<1000;i++){
                            dictionary.Add(i,"v"+i);
                        }
                    }
                    store.Save();
                    using(var dictionary=data.Create()){
                        dictionary.Add(5000,"unsaved");
                    }
                }
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavorite<int,string>("names");
                    using var dictionary=data.Create();
                    Assert.Equal(1000,dictionary.GetLongCount());
                    Assert.Equal("v999",dictionary[999]);
                    Assert.False(dictionary.ContainsKey(5000));
                }
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void CreateTsavoriteHashSet(){
        var root=NewRoot();
        try{
            {
                //メモリストア: データ本体はストアの子資源になり、予算に索引とログの分が計上される
                using var store=CommonStore.CreateMemory();
                var before=store.Budget.MemoryBytes;
                using var data=store.CreateTsavoriteHashSet<long>("ids");
                using(var set=data.Create()){
                    for(long i=0;i<10_000;i++){
                        Assert.True(set.Add(i));
                    }
                    Assert.False(set.Add(1));
                    for(long i=0;i<10_000;i++){
                        Assert.True(set.Contains(i));
                    }
                }
                Assert.True(store.Budget.MemoryBytes>before);
            }
            {
                //メモリストア: 上限を超える追加は ResourceLimitException。それまでに入れた要素は消えない
                using var store=CommonStore.CreateMemory(new(){MemoryBytes=1L<<20,DiskBytes=0});
                using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small with{LogMemorySize=null});
                using var set=data.Create();
                long added=0;
                Assert.Throws<ResourceLimitException>(()=>{
                    for(;added<100_000_000;added++){
                        set.Add(added);
                    }
                });
                Assert.True(added>1000);
                for(long i=0;i<added;i++){
                    Assert.True(set.Contains(i));
                }
            }
            {
                //一時ストア: メモリ上のログを超えた分はディスクへ退避し、すべて読める。破棄でフォルダごと消える
                var store=CommonStore.CreateTemporary(temporaryDirectory:root);
                var directory=store.DirectoryPath!;
                using(var data=store.CreateTsavoriteHashSet<long>("ids",0,Small)){
                    using var set=data.Create();
                    for(long i=0;i<50_000;i++){
                        set.Add(i);
                    }
                    Assert.True(data.Address.HeadAddress>data.Address.BeginAddress);
                    for(long i=0;i<50_000;i++){
                        Assert.True(set.Contains(i));
                    }
                    Assert.True(store.Budget.TemporaryStorageBytes>0);
                }
                store.Dispose();
                Assert.False(Directory.Exists(directory));
            }
            {
                //一時ストア: ディスクの上限を超える追加は ResourceLimitException
                using var store=CommonStore.CreateTemporary(new(){MemoryBytes=1L<<24,DiskBytes=1L<<18},root);
                using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small);
                using var set=data.Create();
                var error=Assert.Throws<ResourceLimitException>(()=>{
                    for(long i=0;i<100_000_000;i++){
                        set.Add(i);
                    }
                });
                Assert.Equal(global::Seamless.Execution.ResourceLimitKind.TemporaryStorage,error.Kind);
            }
            {
                //永続ストア: 保存した時点へ開き直せる。保存後の追加は確定しない
                var directory=Path.Combine(root,"set");
                using(var store=CommonStore.Open(directory)){
                    Assert.Equal(StoreKind.Persistent,store.Kind);
                    using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small);
                    using(var set=data.Create()){
                        for(long i=0;i<20_000;i++){
                            set.Add(i);
                        }
                    }
                    store.Save();
                    //索引の大きさはストアの保存に記録するので、checkpoint フォルダ内へのメタデータ(.Tsavorite)は作らない
                    Assert.False(Directory.Exists(Path.Combine(store.GetComponentDirectory("ids"),"checkpoints",".Tsavorite")));
                    using(var set=data.Create()){
                        set.Add(-1);
                    }
                }
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small);
                    using var set=data.Create();
                    Assert.Equal(20_000,set.GetLongCount());
                    Assert.True(set.Contains(19_999));
                    Assert.False(set.Contains(-1));
                }
            }
            {
                //操作用オブジェクトが残っている間は復元できない
                var directory=Path.Combine(root,"busy");
                using var store=CommonStore.Open(directory);
                using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small);
                using(var set=data.Create()){
                    set.Add(1);
                }
                var token=store.Save();
                using var alive=data.Create();
                Assert.Throws<InvalidOperationException>(()=>store.Load(token));
            }
            {
                //索引は件数に応じて自動で拡張され、拡張分のメモリも予算に計上される
                using var store=CommonStore.CreateMemory();
                using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small with{LogMemorySize=null});
                Assert.Equal(64,data.Store.IndexSize);
                using(var set=data.Create()){
                    for(long i=0;i<60_000;i++){
                        set.Add(i);
                    }
                    for(long i=0;i<60_000;i++){
                        Assert.True(set.Contains(i));
                    }
                }
                Assert.True(data.Store.IndexSize>=8192);
                Assert.True(store.Budget.MemoryBytes>=data.Store.IndexSize*64);
            }
            {
                //索引を拡張したデータ本体も、保存して開き直すと拡張後の大きさで復元される
                var directory=Path.Combine(root,"grown");
                long grownIndexSize;
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small);
                    using(var set=data.Create()){
                        for(long i=0;i<30_000;i++){
                            set.Add(i);
                        }
                    }
                    grownIndexSize=data.Store.IndexSize;
                    Assert.True(grownIndexSize>64);
                    store.Save();
                }
                using(var store=CommonStore.Open(directory)){
                    using var data=store.CreateTsavoriteHashSet<long>("ids",0,Small);
                    Assert.Equal(grownIndexSize,data.Store.IndexSize);
                    using var set=data.Create();
                    Assert.True(set.Contains(29_999));
                }
            }
            {
                //同じ名前は二重に作れない
                using var store=CommonStore.CreateMemory();
                using var data=store.CreateTsavoriteHashSet<long>("ids");
                Assert.Throws<ArgumentException>(()=>store.CreateTsavoriteHashSet<long>("ids"));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void CreateTsavoriteSet(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"records");
            using(var store=CommonStore.Open(directory)){
                //キーで重複を判定し、キーで引ける。件数はデータ本体が管理する
                using var data=store.CreateTsavoriteSet<int,Score>("scores");
                using(var set=data.Create()){
                    Assert.True(set.Add(new(1,10)));
                    Assert.True(set.Add(new(2,20)));
                    Assert.False(set.Add(new(1,99)));
                    Assert.True(set.ContainsKey(2));
                    Assert.Equal(2,data.GetLongCount());
                }
                store.Save();
                using(var set=data.Create()){
                    set.Add(new(3,30));
                }
            }
            using(var store=CommonStore.Open(directory)){
                //保存した時点へ開き直せる
                using var data=store.CreateTsavoriteSet<int,Score>("scores");
                using var set=data.Create();
                Assert.Equal(2,data.GetLongCount());
                Assert.True(set.ContainsKey(1));
                Assert.False(set.ContainsKey(3));
            }
        }finally{Directory.Delete(root,true);}
    }
}
file readonly record struct Score(int Key,long Value):global::Seamless.Collections.IRecord<int>;
