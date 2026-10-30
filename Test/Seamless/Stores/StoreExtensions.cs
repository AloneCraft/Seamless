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
                //固定した索引は混雑しても拡張せず、要素はすべて読み取れる
                using var store=CommonStore.CreateMemory();
                using var data=store.CreateTsavoriteHashSet<long>("fixed",Small with{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>(),LogMemorySize=1L<<21});
                using var set=data.Create();
                var initialOverflowBuckets=data.Store.OverflowBucketCount;
                for(long i=0;i<10_000;i++){
                    Assert.True(set.Add(i));
                }
                Assert.Equal(64,data.Store.IndexSize);
                Assert.True(data.Store.OverflowBucketCount>initialOverflowBuckets);
                for(long i=0;i<10_000;i++){
                    Assert.True(set.Contains(i));
                }
            }
            {
                //不正な設定は資源を予約する前に拒否する
                using var store=CommonStore.CreateMemory();
                TsavoriteTuning[] invalid=[Small with{IndexSize=65},Small with{PageSize=4097},Small with{SegmentSize=8193},Small with{LogMemorySize=65537},Small with{SegmentSize=2048},Small with{MutableFraction=0},Small with{MutableFraction=-1},Small with{MutableFraction=1.01},Small with{MutableFraction=double.NaN},Small with{Storage=new()}];
                foreach(var tuning in invalid){
                    Assert.ThrowsAny<ArgumentException>(()=>store.CreateTsavoriteHashSet<long>("invalid",tuning));
                    Assert.Equal(0,store.Budget.MemoryBytes);
                }
                Assert.Throws<ArgumentException>(()=>store.CreateTsavoriteHashSet<long>("too-small",Small with{LogMemorySize=4096}));
            }
            {
                //保存済みの索引と異なる固定指定は、小さくても大きくても拒否する
                var directory=Path.Combine(root,"fixed-index");
                using(var store=CommonStore.Open(directory)){
                    var data=store.CreateTsavoriteHashSet<long>("ids",Small with{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()});
                    using(var set=data.Create()){
                        set.Add(123);
                    }
                    store.Save();
                }
                using(var store=CommonStore.Open(directory)){
                    foreach(var index in new long[]{2048,8192}){
                        Assert.Throws<InvalidOperationException>(()=>store.CreateTsavoriteHashSet<long>("ids",Small with{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>(),IndexSize=index}));
                    }
                    using var data=store.CreateTsavoriteHashSet<long>("ids",Small with{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()});
                    using var set=data.Create();
                    Assert.True(set.Contains(123));
                }
            }
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
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small with{LogMemorySize=null});
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
                using(var data=store.CreateTsavoriteHashSet<long>("ids",Small)){
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
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
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
                    using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
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
                    using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
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
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
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
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small with{LogMemorySize=null});
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
                    using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
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
                    using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                    Assert.Equal(grownIndexSize,data.Store.IndexSize);
                    using var set=data.Create();
                    Assert.True(set.Contains(29_999));
                }
            }
            {
                //Random/Unbuffered の指定で一時ストアの保存・復元と永続ストアの開き直しができる
                StorageTuning[] modes=[new(FileAccess.ReadWrite,StorageAccessPattern.Random,StorageBufferingMode.Buffered),new(FileAccess.ReadWrite,StorageAccessPattern.Sequential,StorageBufferingMode.Unbuffered)];
                foreach(var mode in modes){
                    var tuning=Small with{Storage=mode};
                    using(var temporary=CommonStore.CreateTemporary(temporaryDirectory:root)){
                        var data=temporary.CreateTsavoriteHashSet<long>("ids",tuning);
                        using(var set=data.Create()){
                            for(long i=0;i<4000;i++){set.Add(i);}
                        }
                        var checkpoint=temporary.Save();
                        temporary.Load(checkpoint);
                        using var restored=data.Create();
                        Assert.True(restored.Contains(3999));
                    }
                    var directory=Path.Combine(root,mode.AccessPattern+"-"+mode.BufferingMode);
                    using(var persistent=CommonStore.Open(directory)){
                        var data=persistent.CreateTsavorite<int,string>("names",tuning,expectedCount:10);
                        var records=persistent.CreateTsavoriteSet<int,Score>("records",tuning);
                        using(var dictionary=data.Create()){
                            dictionary.Add(7,"seven");
                        }
                        using(var set=records.Create()){
                            set.Add(new(7,70));
                        }
                        persistent.Save();
                    }
                    using(var persistent=CommonStore.Open(directory)){
                        using var dictionary=persistent.CreateTsavorite<int,string>("names",tuning).Create();
                        using var records=persistent.CreateTsavoriteSet<int,Score>("records",tuning).Create();
                        Assert.Equal("seven",dictionary[7]);
                        Assert.True(records.ContainsKey(7));
                    }
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
    [Fact,Trait("Category","Unit")]public void Save_RandomOperations_ReopenRestoresSavedState(){
        //追加・上書き・削除・保存・開き直し・追い出しをランダムに繰り返し、標準の辞書(モデル)と照合する。開き直すと、最後に保存した時点へ戻る。
        //各シードは、修正前に失敗していたもの。(1) 保存後の書き出しが末尾を含むセクターを書き直し、その間に更新・削除で無効化された古いレコードが復元で見えなくなる(欠落)。
        //(2) 削除した最新の墓標が無効化され、ログデバイス上の古い有効なレコードだけが復元で見える(復活)。
        foreach(var seed in new[]{1105,1107,1143,1586,1606,1613,1998,2063,2066,2373,2424}){
            var failure=RunSaveModel(seed);
            Assert.True(failure is null,$"seed={seed}: {failure}");
        }
    }
    private static string? RunSaveModel(int seed){
        const long Page=4096;
        var rng=new Random(seed);
        var memoryPages=1<<rng.Next(2,6);
        var keySpace=rng.Next(20,3000);
        var tuning=new TsavoriteTuning{PageSize=Page,SegmentSize=1L<<20,LogMemorySize=memoryPages*Page};
        var root=NewRoot();
        var model=new Dictionary<long,long>();
        var saved=new Dictionary<long,long>();
        CommonStore? store=null;
        DictionaryStore<long,long>? data=null;
        void Open(){
            store=CommonStore.Open(root);
            data=store.CreateTsavorite<long,long>("d",tuning);
        }
        void Close(){
            data?.Dispose();
            store?.Dispose();
            data=null;
            store=null;
        }
        string? Verify(string when){
            using var accessor=data!.Create();
            var missing=0;
            foreach(var pair in model){
                if(!accessor.TryGetValue(pair.Key,out var value)||value!=pair.Value){
                    missing++;
                }
            }
            var extra=0;
            for(long key=0;key<keySpace;key++){
                if(!model.ContainsKey(key)&&accessor.ContainsKey(key)){
                    extra++;
                }
            }
            var count=data.GetLongCount();
            return missing==0&&extra==0&&count==model.Count ? null : $"{when}: 欠落または値違い={missing} 余分={extra} 件数={count}(モデル{model.Count})";
        }
        try{
            Open();
            for(var operation=0;operation<100;operation++){
                var kind=rng.Next(100);
                if(kind<55){
                    using var accessor=data!.Create();
                    var burst=rng.Next(1,40);
                    for(var i=0;i<burst;i++){
                        var key=(long)rng.Next(keySpace);
                        var value=rng.NextInt64();
                        accessor[key]=value;
                        model[key]=value;
                    }
                }else if(kind<75){
                    using var accessor=data!.Create();
                    var burst=rng.Next(1,30);
                    for(var i=0;i<burst;i++){
                        var key=(long)rng.Next(keySpace);
                        accessor.Remove(key);
                        model.Remove(key);
                    }
                }else if(kind<85){
                    store!.Save();
                    saved=new Dictionary<long,long>(model);
                }else if(kind<89){
                    //(検証だけ。古い保存への Load は、より新しい保存を無効にするので使わない)
                    var problem=Verify($"操作{operation} 途中確認");
                    if(problem is not null){
                        return problem;
                    }
                }else if(kind<94){
                    Close();
                    Open();
                    model=new Dictionary<long,long>(saved);
                    var problem=Verify($"操作{operation} 開き直し");
                    if(problem is not null){
                        return problem;
                    }
                }else if(kind<97){
                    data!.Store.Log.FlushAndEvict(true);
                }else{
                    var problem=Verify($"操作{operation} 途中確認");
                    if(problem is not null){
                        return problem;
                    }
                }
            }
            return Verify("最終");
        }finally{
            Close();
            Directory.Delete(root,true);
        }
    }
}
file readonly record struct Score(int Key,long Value):global::Seamless.Collections.IRecord<int>;
