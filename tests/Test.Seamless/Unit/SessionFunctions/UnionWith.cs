using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Unit.SessionFunctions;

public abstract class UnionWithTests<TKey>
    where TKey : IEquatable<TKey> {
    protected abstract TKey Key(int seed);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateMemory();
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void ShiftReadOnly(IDisposable store);
    protected abstract void FlushAndEvict(IDisposable store);

    protected static string CreateDiskFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    private void Mutable() {
        var (setA,storeA)=this.CreateMemory();
        var (setB,storeB)=this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
        setA.UnionWith(setB);
        Assert.Contains(this.Key(1),setA);
        Assert.Contains(this.Key(2),setA);
        Assert.Contains(this.Key(3),setA);
        Assert.Contains(this.Key(4),setA);
        Assert.Equal(4,setA.Count);
    }
    [Fact, Trait("Category","Unit")] public void NeedInitialUpdate() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void GetRMWInitialFieldInfo() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void InitialUpdater() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void PostInitialUpdater() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void GetRMWModifiedFieldInfo() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void InPlaceUpdater() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void PostRMWOperation() => this.Mutable();

    private void ReadOnly() {
        var folder = CreateDiskFolder();
        using var logDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (setA,storeA)=this.CreateDisk(logDevice,objDevice);
        var (setB,storeB)=this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2));
        setB.Add(this.Key(2)); setB.Add(this.Key(3));
        this.ShiftReadOnly(storeA);
        setA.UnionWith(setB);
        Assert.Contains(this.Key(1),setA);
        Assert.Contains(this.Key(2),setA);
        Assert.Contains(this.Key(3),setA);
        Assert.Equal(3,setA.Count);
    }
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void ReadOnly_Works() => this.ReadOnly();

    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void IsPendingPath_Works() {
        var folderA = CreateDiskFolder();
        var folderB = CreateDiskFolder();
        using var logDeviceA = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceA = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var logDeviceB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceB = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (setA,storeA)=this.CreateDiskSpill(logDeviceA,objDeviceA);
        var (setB,storeB)=this.CreateDiskSpill(logDeviceB,objDeviceB);
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        for(var a = 0; a<300; a++) {
            if(a%2==0) setA.Add(this.Key(a));
            if(a%3==0) setB.Add(this.Key(a));
        }
        setA.UnionWith(setB);
        Assert.Contains(this.Key(0),setA);
        Assert.DoesNotContain(this.Key(1),setA);
        Assert.Contains(this.Key(3),setA);
        Assert.Contains(this.Key(298),setA);
    }

    private void FlushAndEvictPath() {
        var folder = CreateDiskFolder();
        using var logDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (setA,storeA)=this.CreateDisk(logDevice,objDevice);
        var (setB,storeB)=this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2));
        setB.Add(this.Key(2)); setB.Add(this.Key(3));
        this.FlushAndEvict(storeA);
        setA.UnionWith(setB);
        Assert.Contains(this.Key(1),setA);
        Assert.Contains(this.Key(2),setA);
        Assert.Contains(this.Key(3),setA);
        Assert.Equal(3,setA.Count);
    }
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void RMWCompletionCallback() => this.FlushAndEvictPath();
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void ConvertOutputToHeap() => this.FlushAndEvictPath();
}

public sealed class ManagedUnionWith:UnionWithTests<int> {
    protected override int Key(int seed) => seed;
    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateMemory() {
        var store = Factory.Create().Create<int>(new ManagedTag());
        return (store.Create(),store);
    }
    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize: Const.MinObjectLogCapacity).Create<int>(logDevice,objDevice,new ManagedTag());
        return (store.Create(),store);
    }
    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDevice,objDevice,new ManagedTag());
        return (store.Create(),store);
    }
    protected override void ShiftReadOnly(IDisposable store) {
        var s = (ManagedHashSetStore<int>)store;
        s.Store.Log.ShiftReadOnlyAddress(s.Store.Log.TailAddress,true);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((ManagedHashSetStore<int>)store).Store.Log.FlushAndEvict(true);

}

public sealed class UnmanagedUnionWith {
    private static (UnmanagedHashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Factory.Create().Create<int>();
        return (store.Create(),store);
    }

    private static (UnmanagedHashSet<int> Set,IDisposable Store) CreateSmallMutableSet() {
        var store = Factory.Create(MutableFraction: 0.01).Create<int>();
        return (store.Create(),store);
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_LeavesUnion() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1); setA.Add(2); setA.Add(3);
        setB.Add(2); setB.Add(3); setB.Add(4);
        setA.UnionWith(setB);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
        Assert.True(setA.Contains(3));
        Assert.True(setA.Contains(4));
        Assert.Equal(4,setA.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_EmptyOther_Unchanged() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1); setA.Add(2);
        setA.UnionWith(setB);
        Assert.Equal(2,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_EmptySelf_AddsAllFromOther() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setB.Add(1); setB.Add(2);
        setA.UnionWith(setB);
        Assert.Equal(2,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_EqualSets_AllRemain() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1); setA.Add(2);
        setB.Add(1); setB.Add(2);
        setA.UnionWith(setB);
        Assert.Equal(2,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_Null_Throws() {
        var (set,store)=CreateSet();
        using var _store = store;
        using var _set = set;
        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
    }
}
