namespace Test.Seamless.Stores;
/// <summary>
/// CollectionStoreの境界条件を検証する。
/// CollectionStoreの境界条件を検証する。
/// </summary>

public class CollectionStore {
    [Fact,Trait("Category","Unit")]
    public void GetStatistics(){
        var tuning=new TsavoriteTuning{IndexSize=4096,PageSize=4096,SegmentSize=65536,LogMemorySize=65536,AutoGrowIndex=false};
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
}
