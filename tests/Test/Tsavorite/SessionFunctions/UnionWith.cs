using Tsavorite.Collections;
using Tsavorite.Stores;
namespace Test.Tsavorite.SessionFunctions;

//public abstract class UnionWithTests<TKey>
//    where TKey : IEquatable<TKey> {
//    protected abstract TKey Key(int seed);
//    protected abstract (global::Tsavorite.Collections.HashSet<TKey> Set,IDisposable Store) CreateMemory();
//    protected abstract (global::Tsavorite.Collections.HashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
//    protected abstract (global::Tsavorite.Collections.HashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
//    protected abstract void ShiftReadOnly(IDisposable store);
//    protected abstract void FlushAndEvict(IDisposable store);

//    protected static string CreateDiskFolder() => Path.Combine(Environment.CurrentDirectory,"TsavoriteTest_"+Guid.NewGuid().ToString("N"));
//    private const long TestMemorySize = Const.MinObjectLogCapacity;

//    private void Mutable() {
//        var (setA,storeA)=this.CreateMemory();
//        var (setB,storeB)=this.CreateMemory();
//        using var _storeA = storeA;
//        using var _storeB = storeB;
//        using var _setA = setA;
//        using var _setB = setB;
//        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
//        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
//        setA.UnionWith(setB);
//        Assert.Contains(this.Key(1),setA);
//        Assert.Contains(this.Key(2),setA);
//        Assert.Contains(this.Key(3),setA);
//        Assert.Contains(this.Key(4),setA);
//        Assert.Equal(4,setA.Count);
//    }
//    [Fact, Trait("Category","Unit")] public void NeedInitialUpdate() => this.Mutable();
//    [Fact, Trait("Category","Unit")] public void GetRMWInitialFieldInfo() => this.Mutable();
//    [Fact, Trait("Category","Unit")] public void InitialUpdater() => this.Mutable();
//    [Fact, Trait("Category","Unit")] public void PostInitialUpdater() => this.Mutable();
//    [Fact, Trait("Category","Unit")] public void GetRMWModifiedFieldInfo() => this.Mutable();
//    [Fact, Trait("Category","Unit")] public void InPlaceUpdater() => this.Mutable();
//    [Fact, Trait("Category","Unit")] public void PostRMWOperation() => this.Mutable();

//    private void ReadOnly() {
//        var folder = CreateDiskFolder();
//        using var logDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var objDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
//        var (setA,storeA)=this.CreateDisk(logDevice,objDevice);
//        var (setB,storeB)=this.CreateMemory();
//        using var _storeA = storeA;
//        using var _storeB = storeB;
//        using var _setA = setA;
//        using var _setB = setB;
//        setA.Add(this.Key(1)); setA.Add(this.Key(2));
//        setB.Add(this.Key(2)); setB.Add(this.Key(3));
//        this.ShiftReadOnly(storeA);
//        setA.UnionWith(setB);
//        Assert.Contains(this.Key(1),setA);
//        Assert.Contains(this.Key(2),setA);
//        Assert.Contains(this.Key(3),setA);
//        Assert.Equal(3,setA.Count);
//    }
//    [Fact, Trait("Category","Unit")]
//    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
//    public void ReadOnly_Works() => this.ReadOnly();

//    [Fact, Trait("Category","Unit")]
//    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
//    public void IsPendingPath_Works() {
//        var folderA = CreateDiskFolder();
//        var folderB = CreateDiskFolder();
//        using var logDeviceA = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var objDeviceA = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var logDeviceB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var objDeviceB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
//        var (setA,storeA)=this.CreateDiskSpill(logDeviceA,objDeviceA);
//        var (setB,storeB)=this.CreateDiskSpill(logDeviceB,objDeviceB);
//        using var _storeA = storeA;
//        using var _storeB = storeB;
//        using var _setA = setA;
//        using var _setB = setB;
//        for(var a = 0; a<300; a++) {
//            if(a%2==0) setA.Add(this.Key(a));
//            if(a%3==0) setB.Add(this.Key(a));
//        }
//        setA.UnionWith(setB);
//        Assert.Contains(this.Key(0),setA);
//        Assert.DoesNotContain(this.Key(1),setA);
//        Assert.Contains(this.Key(3),setA);
//        Assert.Contains(this.Key(298),setA);
//    }

//    private void FlushAndEvictPath() {
//        var folder = CreateDiskFolder();
//        using var logDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var objDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
//        var (setA,storeA)=this.CreateDisk(logDevice,objDevice);
//        var (setB,storeB)=this.CreateMemory();
//        using var _storeA = storeA;
//        using var _storeB = storeB;
//        using var _setA = setA;
//        using var _setB = setB;
//        setA.Add(this.Key(1)); setA.Add(this.Key(2));
//        setB.Add(this.Key(2)); setB.Add(this.Key(3));
//        this.FlushAndEvict(storeA);
//        setA.UnionWith(setB);
//        Assert.Contains(this.Key(1),setA);
//        Assert.Contains(this.Key(2),setA);
//        Assert.Contains(this.Key(3),setA);
//        Assert.Equal(3,setA.Count);
//    }
//    [Fact, Trait("Category","Unit")]
//    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
//    public void RMWCompletionCallback() => this.FlushAndEvictPath();
//    [Fact, Trait("Category","Unit")]
//    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
//    public void ConvertOutputToHeap() => this.FlushAndEvictPath();
//}

//public sealed class ManagedUnionWith:UnionWithTests<int> {
//    protected override int Key(int seed) => seed;
//    protected override (global::Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateMemory() {
//        var store = HashStore.Create().Create<int>(new MemoryPackSerializerStrategy());
//        return (store.Create(),store);
//    }
//    protected override (global::Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
//        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int>(logDevice,objDevice,new MemoryPackSerializerStrategy());
//        return (store.Create(),store);
//    }
//    protected override (global::Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
//        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDevice,objDevice,new MemoryPackSerializerStrategy());
//        return (store.Create(),store);
//    }
//    protected override void ShiftReadOnly(IDisposable store) {
//        var s = (HashSetStore<int>)store;
//        s.Store.Log.ShiftReadOnlyAddress(s.Store.Log.TailAddress,true);
//    }
//    protected override void FlushAndEvict(IDisposable store) =>
//        ((HashSetStore<int>)store).Store.Log.FlushAndEvict(true);

//}

public abstract class UnionWith<TKey, TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : IHashSet<TKey>, IDisposable {
    [Fact, Trait("Category","Unit")]
    public void UnionWithAB() => this.RunWithAB();

    [Fact, Trait("Category","Unit")]
    public void UnionWithAA() => this.RunWithAA();
}

public sealed class ManagedUnionWith:UnionWith<string,global::Tsavorite.Collections.HashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (global::Tsavorite.Collections.HashSet<string> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<string>(new MemoryPackSerializerStrategy());
        return (store.Create(),store);
    }
    protected override void With(global::Tsavorite.Collections.HashSet<string> set,global::Tsavorite.Collections.HashSet<string> other) => set.UnionWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<string> set,global::System.Collections.Generic.HashSet<string> other) => set.UnionWith(other);
}

public sealed class UnmanagedUnionWith:UnionWith<int,global::Tsavorite.Collections.HashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (global::Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(global::Tsavorite.Collections.HashSet<int> set,global::Tsavorite.Collections.HashSet<int> other) => set.UnionWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<int> set,global::System.Collections.Generic.HashSet<int> other) => set.UnionWith(other);
}
