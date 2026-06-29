using Seamless.Tsavorite.Stores;
namespace Test.Seamless.Stores;
/// <summary>
/// CollectionStoreの境界条件を検証する。
/// CollectionStoreの境界条件を検証する。
/// </summary>

public class CollectionStore {
    private static string CreateFolder() => TestPath.Directory<CollectionStore>();
    private const long TestMemorySize = global::Seamless.Tsavorite.Const.MinObjectLogCapacity;
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
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0.1).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        // CollectionStoreの境界条件を検証する。
        for(var a = 0; a<1000; a++) {
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
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0.1).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        // CollectionStoreの境界条件を検証する。
        for(var a = 0; a<1000; a++) {
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
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
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
        for(var i = 0; i<16; i++) {
            CreateOrphan();
        }
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        using var store = Store.Create().Create<int,string>();
    }
}
