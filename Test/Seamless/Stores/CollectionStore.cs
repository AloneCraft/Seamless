namespace Test.Seamless.Stores;
/// <summary>
/// CollectionStoreの境界条件を検証する。
/// CollectionStoreの境界条件を検証する。
/// </summary>

public class CollectionStore {
    [Fact,Trait("Category","Unit")]
    public void Save_SectorBoundaryInsideRecord_Reopens(){
        var directory=Path.Combine(Path.GetTempPath(),"sector-boundary-record-"+Guid.NewGuid().ToString("N"));
        var tuning=new TsavoriteTuning{PageSize=64L<<10,SegmentSize=1L<<20,LogMemorySize=256L<<10,IndexSize=64};
        try{
            using(var owner=global::Seamless.Execution.Store.Open(directory)){
                var data=owner.CreateTsavorite<RecoveryKey,RecoveryValue>("records",tuning);
                using(var dictionary=data.Create()){
                    for(var index=0;index<19;index++){
                        dictionary[new(index,~(long)index)]=new(index,index+1,index+2);
                    }
                    owner.Save();
                    Assert.Equal(1128,data.Store.Log.TailAddress);
                    owner.Save();
                }
            }
            using(var owner=global::Seamless.Execution.Store.Open(directory)){
                using var dictionary=owner.CreateTsavorite<RecoveryKey,RecoveryValue>("records",tuning).Create();
                Assert.Equal(19,dictionary.Count);
                for(var index=0;index<19;index++){
                    Assert.Equal(new RecoveryValue(index,index+1,index+2),dictionary[new(index,~(long)index)]);
                }
            }
        }finally{
            if(Directory.Exists(directory)){
                Directory.Delete(directory,true);
            }
        }
    }
    [Fact,Trait("Category","Unit")]
    public void GetStatistics(){
        var tuning=new TsavoriteTuning{IndexSize=4096,PageSize=4096,SegmentSize=65536,LogMemorySize=65536,Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
        {
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            using var data=owner.CreateTsavoriteHashSet<long>("ids",tuning with{LogMemorySize=1L<<20});
            using var set=data.Create();
            var before=data.GetStatistics();
            for(long i=0;i<10_000;i++){set.Add(i);}
            var statistics=data.GetStatistics();
            Assert.Equal(64,statistics.IndexSize);
            Assert.Equal(4096,statistics.IndexBytes);
            Assert.InRange(statistics.EntryCount,1,data.GetLongCount());
            Assert.True(statistics.OverflowBucketCount>before.OverflowBucketCount);
            Assert.True(statistics.OverflowBucketAllocations>before.OverflowBucketAllocations);
            Assert.True(statistics.HighWaterAllocatedPageCount>0);
            Assert.Equal(0,statistics.DiskLogBytes);
            Assert.True(statistics.MemoryLogBytes>0);
            Assert.Equal(statistics.TailAddress-statistics.HeadAddress,statistics.MemoryLogBytes);
            var previousTail=statistics.TailAddress;
            set.Add(10_000);
            Assert.True(data.GetStatistics().TailAddress>previousTail);
            Assert.Equal(previousTail,statistics.TailAddress);
        }
        {
            using var owner=global::Seamless.Execution.Store.CreateTemporary();
            using var data=owner.CreateTsavoriteHashSet<long>("ids",tuning);
            using var set=data.Create();
            for(long i=0;i<20_000;i++){set.Add(i);}
            var statistics=data.GetStatistics();
            Assert.True(statistics.DiskLogBytes>0);
            Assert.True(statistics.MemoryLogBytes>0);
            Assert.Equal(statistics.HeadAddress-statistics.BeginAddress,statistics.DiskLogBytes);
            Assert.Equal(statistics.TailAddress-statistics.HeadAddress,statistics.MemoryLogBytes);
            Assert.True(statistics.ReadOnlyAddress>=statistics.HeadAddress);
            Assert.True(set.Contains(0));
            Assert.True(set.Contains(19_999));
        }
    }
    private readonly record struct RecoveryKey(long High,long Low);
    private readonly record struct RecoveryValue(long First,long Second,long Third);
    [Fact,Trait("Category","Unit")]
    public void AccessorCount(){
        {
            //破棄したデータ本体からは作れない。作成に失敗した操作用オブジェクトは、ファイナライザで数を減らさない
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            var data=owner.CreateTsavoriteHashSet<long>("ids");
            data.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>data.Create());
            GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking:true);
            GC.WaitForPendingFinalizers();
            Assert.Equal(0,data.AccessorCount);
        }
        {
            //破棄し忘れた操作用オブジェクトは、ファイナライザが例外を出さずに数を戻す
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            using var data=owner.CreateTsavoriteHashSet<long>("ids");
            CreateOrphan(data);
            Assert.Equal(1,data.AccessorCount);
            GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking:true);
            GC.WaitForPendingFinalizers();
            Assert.Equal(0,data.AccessorCount);
            using var set=data.Create();
            Assert.Equal(1,data.AccessorCount);
        }
        {
            //内部の件数の走査(データ本体の件数・旧 API の保存)は、操作用オブジェクトを作らない
            var root=Path.Combine(Path.GetTempPath(),"accessor-count-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try{
                using var logDevice=global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(root,"log"));
                using var objDevice=global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(root,"log_obj"));
                using var store=Store.Create(CheckpointDir:Path.Combine(root,"checkpoint")).Create<int,int>(logDevice,objDevice);
                using var dictionary=store.Create();
                dictionary[1]=1;
                store.Save();
                Assert.Equal(1,store.AccessorCount);
            }finally{
                Directory.Delete(root,true);
            }
        }
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void CreateOrphan(global::Seamless.Collections.Tsavorite.HashSetStore<long> data){
            var set=data.Create();
            set.Add(1);
        }
    }
    [Fact,Trait("Category","Unit")]
    public void OnAccessorCreated(){
        {
            //シングル用は同時に1つだけ。2つ目は例外で、破棄すれば作れる
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            using var data=owner.CreateTsavoriteHashSet<long>("ids");
            var first=data.Create();
            Assert.Throws<InvalidOperationException>(()=>data.Create());
            Assert.Throws<InvalidOperationException>(()=>data.CreateSet());
            //作れなかった操作用オブジェクトのファイナライザが、1つ目の権利を返してしまわない
            GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking:true);
            GC.WaitForPendingFinalizers();
            Assert.Throws<InvalidOperationException>(()=>data.Create());
            Assert.Equal(1,data.AccessorCount);
            first.Dispose();
            using var second=data.Create();
            Assert.Equal(1,data.AccessorCount);
        }
        {
            //辞書はシリアライザの組み合わせで作り方が違う(実行時に型を組み立てる経路を含む)。どれも2つ目は例外
            CheckDictionary<int,int>();
            CheckDictionary<string,string>();
            CheckDictionary<int,string>();
            CheckDictionary<string,int>();
        }
        {
            //キーを持つレコードの集合と、旧 API のデータ本体も同じ
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            using var records=owner.CreateTsavoriteSet<int,ScoreRecord>("scores");
            using(records.Create()){
                Assert.Throws<InvalidOperationException>(()=>records.Create());
            }
            using var legacy=Store.Create(MemorySize:1L<<20).Create<long>();
            using(legacy.Create()){
                Assert.Throws<InvalidOperationException>(()=>legacy.Create());
            }
        }
        static void CheckDictionary<TKey,TValue>() where TKey:notnull{
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            using var data=owner.CreateTsavorite<TKey,TValue>("names");
            using(data.Create()){
                Assert.Throws<InvalidOperationException>(()=>data.Create());
            }
            using var again=data.Create();
        }
    }
    [Fact,Trait("Category","Unit")]
    public async Task SaveWhileGrowingIndex(){
        //索引を広げる書き込みと、別のスレッドの保存が重なっても、どちらも失敗しない(保存と拡大は同時に走れない)
        var root=Path.Combine(Path.GetTempPath(),"save-while-growing-"+Guid.NewGuid().ToString("N"));
        try{
            using var owner=global::Seamless.Execution.Store.Open(root);
            using var data=owner.CreateTsavoriteHashSet<long>("ids",new TsavoriteTuning{IndexSize=4096,PageSize=4096,LogMemorySize=1L<<20});
            using var stop=new CancellationTokenSource();
            var saves=0;
            var saver=Task.Run(()=>{
                while(!stop.IsCancellationRequested){
                    owner.Save();
                    saves++;
                }
            });
            using(var set=data.Create()){
                for(long i=0;i<200_000;i++){
                    set.Add(i);
                }
            }
            stop.Cancel();
            await saver;
            Assert.True(saves>0);
            Assert.True(data.GetStatistics().IndexBytes>4096);
            Assert.Equal(200_000,data.GetLongCount());
        }finally{
            if(Directory.Exists(root)){
                Directory.Delete(root,true);
            }
        }
    }
    [Fact,Trait("Category","Unit")]
    public void TryGetManagedCount(){
        {
            //辞書の代入は、新規か上書きかを見分けて件数を保つ(数え直しにしない)
            using var owner=global::Seamless.Execution.Store.CreateMemory();
            using var data=owner.CreateTsavorite<int,string>("names");
            using var dictionary=data.Create();
            for(var i=0;i<100;i++){
                dictionary[i]="a";
            }
            dictionary[0]="b";
            //値が伸びる上書き
            dictionary[1]=new string('x',1000);
            Assert.True(data.TryGetManagedCount(out var count));
            Assert.Equal(100,count);
            Assert.Equal("b",dictionary[0]);
            Assert.Equal(1000,dictionary[1].Length);
        }
        {
            //読み取り専用の領域とディスク上のレコードへの代入も、上書きとして数える
            using var owner=global::Seamless.Execution.Store.CreateTemporary();
            using var data=owner.CreateTsavorite<int,string>("names",new TsavoriteTuning{PageSize=4096,LogMemorySize=1L<<14});
            using var dictionary=data.Create();
            for(var i=0;i<2000;i++){
                dictionary[i]="v"+i;
            }
            Assert.True(data.GetStatistics().DiskLogBytes>0);
            data.Store.Log.ShiftReadOnlyAddress(data.Store.Log.TailAddress,true);
            for(var i=0;i<2000;i+=7){
                dictionary[i]="w"+i;
            }
            dictionary[5000]="new";
            Assert.True(data.TryGetManagedCount(out var count));
            Assert.Equal(2001,count);
            Assert.Equal("w0",dictionary[0]);
            Assert.Equal("v1",dictionary[1]);
            Assert.Equal("new",dictionary[5000]);
        }
        {
            //旧 API で作ったデータ本体も、件数を管理する
            using var legacy=Store.Create(MemorySize:1L<<20).Create<long>();
            using var set=legacy.Create();
            for(long i=0;i<100;i++){
                set.Add(i);
            }
            set.Remove(5);
            Assert.True(legacy.TryGetManagedCount(out var count));
            Assert.Equal(99,count);
        }
        {
            //旧 API の読み込みは件数を「不明」に戻し、次に数え直す
            var root=Path.Combine(Path.GetTempPath(),"managed-count-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try{
                var logPath=Path.Combine(root,"log");
                var checkpointDir=Path.Combine(root,"checkpoint");
                Guid token;
                {
                    using var logDevice=global::Tsavorite.core.Devices.CreateLogDevice(logPath);
                    using var objDevice=global::Tsavorite.core.Devices.CreateLogDevice(logPath+"_obj");
                    using var store=Store.Create(CheckpointDir:checkpointDir).Create<int,int>(logDevice,objDevice);
                    using(var dictionary=store.Create()){
                        for(var i=0;i<10;i++){
                            dictionary.Add(i,i);
                        }
                    }
                    token=store.Save();
                }
                {
                    using var logDevice=global::Tsavorite.core.Devices.CreateLogDevice(logPath);
                    using var objDevice=global::Tsavorite.core.Devices.CreateLogDevice(logPath+"_obj");
                    using var store=Store.Create(CheckpointDir:checkpointDir).Create<int,int>(logDevice,objDevice);
                    store.Load(token);
                    Assert.False(store.TryGetManagedCount(out _));
                    Assert.Equal(10,store.GetLongCount());
                }
            }finally{
                Directory.Delete(root,true);
            }
        }
        {
            //操作用オブジェクトを開いたまま保存すると、件数は記録しない(開き直したあとに数え直す)
            var root=Path.Combine(Path.GetTempPath(),"managed-count-"+Guid.NewGuid().ToString("N"));
            try{
                using(var owner=global::Seamless.Execution.Store.Open(root)){
                    using var data=owner.CreateTsavoriteHashSet<long>("ids");
                    using var set=data.Create();
                    for(long i=0;i<50;i++){
                        set.Add(i);
                    }
                    owner.Save();
                }
                using(var owner=global::Seamless.Execution.Store.Open(root)){
                    using var data=owner.CreateTsavoriteHashSet<long>("ids");
                    Assert.False(data.TryGetManagedCount(out _));
                    Assert.Equal(50,data.GetLongCount());
                    Assert.True(data.TryGetManagedCount(out var count));
                    Assert.Equal(50,count);
                }
            }finally{
                if(Directory.Exists(root)){
                    Directory.Delete(root,true);
                }
            }
        }
    }
    [Fact,Trait("Category","Unit")]
    public void DumpIndexDistribution(){
        using var owner=global::Seamless.Execution.Store.CreateMemory();
        using var data=owner.CreateTsavoriteHashSet<int>("ids");
        using var set=data.Create();
        set.Add(1);
        Assert.False(string.IsNullOrWhiteSpace(data.DumpIndexDistribution()));
    }
    private static string CreateFolder() => TestPath.Directory<CollectionStore>();
    private const long TestMemorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity;
    // CollectionStoreの境界条件を検証する。
    // CollectionStoreの境界条件を検証する。
    [Fact, Trait("Category","Unit")]
    public void Constructor_ObjectLogCapacity_Negative() {
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        // CollectionStoreの境界条件を検証する。
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true);
        // CollectionStoreの境界条件を検証する。
        using var store = Store.Create(MemorySize: TestMemorySize).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Equal("a",dict[1]);
    }
    // CollectionStoreの境界条件を検証する。
    // CollectionStoreの境界条件を検証する。
    // Read 経路はこの処理では使用しない。
    [Fact, Trait("Category","Unit")]
    public void CompactWithLookup_EarlyReturn() {
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0.1).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        // CollectionStoreの境界条件を検証する。
        for(var a = 0;a<1000;a++) {
            dict.Add(a,$"v{a}");
        }
        {
            var before = store.Address;
            store.CompactWithLookup();
            var after = store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        // CollectionStoreの境界条件を検証する。
        // Read 経路はこの処理では使用しない。
        {
            var beforeSecond = store.Address;
            store.CompactWithLookup();
            var afterSecond = store.Address;
            // CollectionStoreの境界条件を検証する。
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
        // CollectionStoreの境界条件を検証する。
        store.CompactWithLookup();
        store.CompactWithLookup();
    }
    // Read 経路はこの処理では使用しない。
    [Fact, Trait("Category","Unit")]
    public void CompactWithLookup() {
        // CollectionStoreの境界条件を検証する。
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0.1).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        // CollectionStoreの境界条件を検証する。
        for(var a = 0;a<1000;a++) {
            dict.Add(a,$"v{a}");
        }
        // CollectionStoreの境界条件を検証する。
        // CollectionStoreの境界条件を検証する。
        // CollectionStoreの境界条件を検証する。
        // CollectionStoreの境界条件を検証する。
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
        {
            var before = store.Address;
            store.CompactWithLookup();
            var after = store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        // CollectionStoreの境界条件を検証する。
        // Read 経路はこの処理では使用しない。
        {
            var beforeSecond = store.Address;
            store.CompactWithLookup();
            var afterSecond = store.Address;
            // CollectionStoreの境界条件を検証する。
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
    }
    // Read 経路はこの処理では使用しない。
    [Fact, Trait("Category","Unit")]
    public void CompactWithLookup_ClipToSafeReadOnly() {
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        // CollectionStoreの境界条件を検証する。
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        // Read 経路はこの処理では使用しない。
        dict.Add(1,"a");
        dict.Add(2,"b");
        var address = store.Address;
        // Read 経路はこの処理では使用しない。
        store.CompactWithLookup();
        // CollectionStoreの境界条件を検証する。
        Assert.True(store.Address.BeginAddress>=address.BeginAddress);
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose_DoubleCall_Idempotent() {
        var store = Store.Create().Create<int>();
        store.Dispose();
        store.Dispose();
    }
    [Fact, Trait("Category","Unit")]
    public void Finalize_GcAsyncDispose() {
        static void CreateOrphan() {
            _=Store.Create().Create<int,string>();
        }
        for(var i = 0;i<16;i++) {
            CreateOrphan();
        }
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        using var store = Store.Create().Create<int,string>();
    }
    [Fact, Trait("Category","Unit")]
    public void GetFlushTargets() {
        var root = Path.Combine(Path.GetTempPath(),"flush-targets-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var logPath = Path.Combine(root,"log");
            // 名前が似ているだけの別のファイルは、書き出す対象に含めない。
            File.WriteAllText(logPath+".bak","x");
            File.WriteAllText(logPath+"ger.0","x");
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath+"_obj");
            using var store = Store.Create(CheckpointDir: Path.Combine(root,"checkpoint")).Create<int,int>(logDevice,objDevice);
            using var set = store.Create();
            set[1]=11;
            // メモリ上のログをデバイスへ書き出し、ログのセグメントのファイルを作る。
            store.Store.Log.FlushAndEvict(true);
            var first = store.Save();
            set[2]=22;
            var second = store.Save();
            {
                // 指定した保存番号のチェックポイントのファイル・索引サイズのメタデータ・ログのセグメントが対象。別の保存番号は含めない。
                var names = store.GetFlushTargets([second]).Select(path => Path.GetRelativePath(root,path)).ToList();
                Assert.Contains(Path.Combine("checkpoint","cpr-checkpoints",second.ToString(),"info.dat.0"),names);
                Assert.Contains(Path.Combine("checkpoint","index-checkpoints",second.ToString(),"ht.dat.0"),names);
                Assert.Contains(Path.Combine("checkpoint",".Tsavorite",$"{second:N}.index-size.json"),names);
                Assert.Contains("log.0",names);
                Assert.DoesNotContain(names,name => name.Contains(first.ToString())||name.Contains($"{first:N}"));
                Assert.DoesNotContain("log.bak",names);
                Assert.DoesNotContain("logger.0",names);
            }
            {
                // 保存番号を渡さなければ、ログのデバイスのファイルだけ。
                var names = store.GetFlushTargets([]).Select(path => Path.GetRelativePath(root,path)).ToList();
                Assert.Contains("log.0",names);
                Assert.DoesNotContain(names,name => name.StartsWith("checkpoint"));
            }
        } finally {
            Directory.Delete(root,true);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ThrowIfRecoveredTailCollidesWithHead() {
        var root = Path.Combine(Path.GetTempPath(),"collide-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var logPath = Path.Combine(root,"log");
            var checkpointDir = Path.Combine(root,"checkpoint");
            Guid token;
            {
                using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath);
                using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath+"_obj");
                using var store = Store.Create(MemorySize: 65536,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
                using var set = store.Create();
                // ログの末尾がページ境界ちょうどで、16 ページのバッファが満杯になるまで追加して、チェックポイントを取る。
                long count = 0;
                while(store.Store.Log.TailAddress%4096!=0||store.Store.Log.TailAddress<65536) {
                    set.Add(count++);
                }
                var (success,checkpointToken) = store.Store.TakeFullCheckpointAsync(global::Tsavorite.core.CheckpointType.Snapshot).AsTask().GetAwaiter().GetResult();
                store.Store.CompleteCheckpointAsync().AsTask().GetAwaiter().GetResult();
                Assert.True(success);
                token=checkpointToken;
                store.Store.Log.Flush(true);
            }
            {
                using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath);
                using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath+"_obj");
                using var store = Store.Create(MemorySize: 65536,PageSize: 4096,SegmentSize: 1<<20,CheckpointDir: checkpointDir).Create<long>(logDevice,objDevice);
                {
                    // 追跡つきの復元(Load)なら、衝突は起きず、例外にもならない。
                    store.Load(token);
                    store.ThrowIfRecoveredTailCollidesWithHead();
                }
                {
                    // 【カナリア】Garnet の素の復元(追跡なし)は、末尾ページのスロットが先頭ページのスロットと重なる(Garnet 2.0.1-beta.6 以降、2.1.8 でも確認)。
                    // 検知が例外にすることを確かめる。このケースが失敗する(重ならなくなった)のは、Garnet が直った合図。
                    // CollectionStore.RecoverWithSizeTrackerAsync と ThrowIfRecoveredTailCollidesWithHead を外してよいか、
                    // 境界の件数の掃引とメモリを縮めた開き直し(docs の不変条件15)で確かめて、外す。
                    store.Store.RecoverAsync(Guid.Empty,token).AsTask().GetAwaiter().GetResult();
                    var collided = false;
                    try {
                        store.ThrowIfRecoveredTailCollidesWithHead();
                    } catch(InvalidDataException) {
                        collided=true;
                    }
                    Assert.True(collided,"Garnet の素の復元が、末尾ページと先頭ページの衝突を起こさなくなった。RecoverWithSizeTrackerAsync を外せるか確かめること。");
                }
            }
        } finally {
            Directory.Delete(root,true);
        }
    }
}
/// <summary>キーを持つレコードの集合の確認用。</summary>
file readonly record struct ScoreRecord(int Key,int Value):global::Seamless.Collections.IRecord<int>;
