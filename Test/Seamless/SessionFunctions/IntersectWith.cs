using Test.Seamless.Collections;

using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Stores;
namespace Test.Seamless.SessionFunctions;

public abstract class IntersectWith<TKey, TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : IHashSet<TKey>, IDisposable {
    [Fact, Trait("Category","Unit")]
    public void IntersectWithAB() => this.RunWithAB();

    [Fact, Trait("Category","Unit")]
    public void IntersectWithAA() => this.RunWithAA();
}

public sealed class ManagedIntersectWith:IntersectWith<string,global::Seamless.Tsavorite.Collections.HashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (global::Seamless.Tsavorite.Collections.HashSet<string> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<string>(new MemoryPackSerializerStrategy());
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Tsavorite.Collections.HashSet<string> set,global::Seamless.Tsavorite.Collections.HashSet<string> other) => set.IntersectWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<string> set,global::System.Collections.Generic.HashSet<string> other) => set.IntersectWith(other);

#if TSAVORITE2_0BETA3
    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification="Storage-backed Tsavorite test path is Windows-only in this workspace")]
    public void FlushAndEvict後もIntersectWithで共通要素だけ残る() {
        var folderA = TestPath.Directory(this,"A");
        var folderB = TestPath.Directory(this,"B");
        using var logDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var logDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var storeA = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<string>(logDeviceA,objDeviceA,new MemoryPackSerializerStrategy());
        using var storeB = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<string>(logDeviceB,objDeviceB,new MemoryPackSerializerStrategy());
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

public sealed class UnmanagedIntersectWith:IntersectWith<int,global::Seamless.Tsavorite.Collections.HashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (global::Seamless.Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Tsavorite.Collections.HashSet<int> set,global::Seamless.Tsavorite.Collections.HashSet<int> other) => set.IntersectWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<int> set,global::System.Collections.Generic.HashSet<int> other) => set.IntersectWith(other);

#if TSAVORITE2_0BETA3
    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification="Storage-backed Tsavorite test path is Windows-only in this workspace")]
    public void FlushAndEvict後もIntersectWithで共通要素だけ残る() {
        var folderA = TestPath.Directory(this,"A");
        var folderB = TestPath.Directory(this,"B");
        using var logDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDeviceA = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var logDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDeviceB = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var storeA = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDeviceA,objDeviceA,new BlittableStrategy());
        using var storeB = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDeviceB,objDeviceB,new BlittableStrategy());
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
