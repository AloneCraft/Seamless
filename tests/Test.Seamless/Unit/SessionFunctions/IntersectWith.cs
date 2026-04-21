using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Unit.SessionFunctions;

public abstract class IntersectWithTests<TKey>
    where TKey : IEquatable<TKey> {
    protected abstract TKey Key(int seed);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateMemory();
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void ShiftReadOnly(IDisposable store);

    protected static string CreateDiskFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // In-memory (Mutable key)
    private void Mutable() {
        var (setA,storeA)=this.CreateMemory();
        var (setB,storeB)=this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
        setA.IntersectWith(setB);
        Assert.DoesNotContain(this.Key(1),setA);
        Assert.Contains(this.Key(2),setA);
        Assert.Contains(this.Key(3),setA);
        Assert.DoesNotContain(this.Key(4),setA);
    }
    [Fact, Trait("Category","Unit")] public void InPlaceUpdater() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void GetRMWModifiedFieldInfo() => this.Mutable();
    [Fact, Trait("Category","Unit")] public void PostRMWOperation() => this.Mutable();

    // ReadOnly in-memory (ShiftReadOnlyAddress)
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
        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
        this.ShiftReadOnly(storeA);
        setA.IntersectWith(setB);
        Assert.DoesNotContain(this.Key(1),setA);
        Assert.Contains(this.Key(2),setA);
        Assert.Contains(this.Key(3),setA);
        Assert.DoesNotContain(this.Key(4),setA);
    }
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void NeedCopyUpdate() => this.ReadOnly();
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性",Justification = "<互換性>")]
    public void CopyUpdater() => this.ReadOnly();

    // Disk (IsPending path)
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
            setA.Add(this.Key(a));
            if(a%2==0) setB.Add(this.Key(a));
        }
        setA.IntersectWith(setB);
        Assert.Contains(this.Key(100),setA);
        Assert.DoesNotContain(this.Key(101),setA);
    }
}

public sealed class ManagedIntersectWith:IntersectWithTests<int> {
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
}

/// <summary>
/// UnmanagedHashSet.IntersectWith のテスト。管理のみ（ストレージバインドなし）。
/// </summary>
public sealed class UnmanagedIntersectWith {
    private static (UnmanagedHashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Factory.Create().Create<int>();
        return (store.Create(),store);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectWith_LeavesCommonItems() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1); setA.Add(2); setA.Add(3);
        setB.Add(2); setB.Add(3); setB.Add(4);
        setA.IntersectWith(setB);
        Assert.False(setA.Contains(1));
        Assert.True(setA.Contains(2));
        Assert.True(setA.Contains(3));
        Assert.False(setA.Contains(4));
    }
    [Fact, Trait("Category","Unit")]
    public void ExceptWith0() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1);
        setA.Add(2);
        setB.Add(2);
        setB.Add(3);
        setA.ExceptWith(setB);
        Assert.Equal(1,setA.Count);
    }
    [Fact, Trait("Category","Unit")]public void IntersectWith0() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1);
        setA.Add(2);
        setB.Add(2);
        setB.Add(3);
        setA.IntersectWith(setB);
        Assert.Equal(0,setA.Count);
    }
    [Fact, Trait("Category","Unit")]
    public void IntersectWith_EmptyOther_ClearsAll() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1); setA.Add(2);
        setA.IntersectWith(setB);
        Assert.Equal(0,setA.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectWith_EmptySelf_Unchanged() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setB.Add(1);
        setA.IntersectWith(setB);
        Assert.Equal(0,setA.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectWith_Null_Throws() {
        var (set,store)=CreateSet();
        using var _store = store;
        using var _set = set;
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectWith_EqualSets_AllRemain() {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(1); setA.Add(2);
        setB.Add(1); setB.Add(2);
        setA.IntersectWith(setB);
        Assert.Equal(2,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
    }
}
