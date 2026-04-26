using Azure;

using Seamless;
using Seamless.Collections;
using Seamless.Stores;

using Tsavorite.core;
namespace Test.Seamless.Unit.SessionFunctions;

public abstract class IntersectWith<TKey, TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : ISeamlessHashSet<TKey>, IDisposable {
    [Fact, Trait("Category","Unit")]
    public void IntersectWithAB() => this.RunWithAB();

    [Fact, Trait("Category","Unit")]
    public void IntersectWithAA() => this.RunWithAA();
}

public sealed class ManagedIntersectWith:IntersectWith<string,ManagedHashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (ManagedHashSet<string> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<string>(new ManagedTag());
        return (store.Create(),store);
    }
    protected override void With(ManagedHashSet<string> set,ManagedHashSet<string> other) => set.IntersectWith(other);
    protected override void ExpectedWith(HashSet<string> set,HashSet<string> other) => set.IntersectWith(other);

#if TSAVORITE2_0BETA3
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification="Storage-backed Tsavorite test path is Windows-only in this workspace")]
    public void FlushAndEvict後もIntersectWithで共通要素だけ残る() {
        var folderA = global::Test.Seamless.TestPath.Directory(this,"A");
        var folderB = global::Test.Seamless.TestPath.Directory(this,"B");
        using var logDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var logDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var storeA = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<string>(logDeviceA,objDeviceA,new ManagedTag());
        using var storeB = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<string>(logDeviceB,objDeviceB,new ManagedTag());
        using var setA = storeA.Create();
        using var setB = storeB.Create();

        for(var i = 0;i<64;i++) {
            Assert.True(setA.Add(this.CreateKey(i)));
            if((i&1)==0) {
                Assert.True(setB.Add(this.CreateKey(i)));
            }
        }
        for(var i = 64;i<96;i++) {
            Assert.True(setB.Add(this.CreateKey(i)));
        }

        storeA.Store.Log.FlushAndEvict(wait: true);
        storeB.Store.Log.FlushAndEvict(wait: true);

        setA.IntersectWith(setB);

        Assert.Equal(32,setA.Count);
        for(var i = 0;i<64;i++) {
            Assert.Equal((i&1)==0,setA.Contains(this.CreateKey(i)));
        }
    }
#endif
}

public sealed class UnmanagedIntersectWith:IntersectWith<int,UnmanagedHashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (UnmanagedHashSet<int> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(UnmanagedHashSet<int> set,UnmanagedHashSet<int> other) => set.IntersectWith(other);
    protected override void ExpectedWith(HashSet<int> set,HashSet<int> other) => set.IntersectWith(other);

#if TSAVORITE2_0BETA3
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification="Storage-backed Tsavorite test path is Windows-only in this workspace")]
    public void FlushAndEvict後もIntersectWithで共通要素だけ残る() {
        var folderA = global::Test.Seamless.TestPath.Directory(this,"A");
        var folderB = global::Test.Seamless.TestPath.Directory(this,"B");
        using var logDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var logDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var storeA = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDeviceA,objDeviceA,new UnmanagedTag());
        using var storeB = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDeviceB,objDeviceB,new UnmanagedTag());
        using var setA = storeA.Create();
        using var setB = storeB.Create();

        for(var i = 0;i<64;i++) {
            Assert.True(setA.Add(this.CreateKey(i)));
            if((i&1)==0) {
                Assert.True(setB.Add(this.CreateKey(i)));
            }
        }
        for(var i = 64;i<96;i++) {
            Assert.True(setB.Add(this.CreateKey(i)));
        }

        storeA.Store.Log.FlushAndEvict(wait: true);
        storeB.Store.Log.FlushAndEvict(wait: true);

        setA.IntersectWith(setB);

        Assert.Equal(32,setA.Count);
        for(var i = 0;i<64;i++) {
            Assert.Equal((i&1)==0,setA.Contains(this.CreateKey(i)));
        }
    }
#endif
}
