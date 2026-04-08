using Seamless;
using Seamless.Collections;
using Seamless.Store;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;

public abstract class IntersectWithTests<TKey>
    where TKey:IEquatable<TKey> {
    protected abstract TKey Key(int seed);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateMemory();
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void ShiftReadOnly(IDisposable store);

    protected static string CreateDiskFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // ── In-memory (Mutable key) ──────────────────────────────────────
    private void Mutable() {
        var (setA,storeA) = this.CreateMemory();
        var (setB,storeB) = this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(Key(1)); setA.Add(Key(2)); setA.Add(Key(3));
        setB.Add(Key(2)); setB.Add(Key(3)); setB.Add(Key(4));
        setA.IntersectWith(setB);
        Assert.DoesNotContain(Key(1),setA);
        Assert.Contains(Key(2),setA);
        Assert.Contains(Key(3),setA);
        Assert.DoesNotContain(Key(4),setA);
    }
    [Fact,Trait("Category",Unit)] public void InPlaceUpdater() => this.Mutable();
    [Fact,Trait("Category",Unit)] public void GetRMWModifiedFieldInfo() => this.Mutable();
    [Fact,Trait("Category",Unit)] public void PostRMWOperation() => this.Mutable();

    // ── ReadOnly in-memory (ShiftReadOnlyAddress) ─────────────────────
    private void ReadOnly() {
        var folder = CreateDiskFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var (setA,storeA) = this.CreateDisk(logDevice,objDevice);
        var (setB,storeB) = this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(Key(1)); setA.Add(Key(2)); setA.Add(Key(3));
        setB.Add(Key(2)); setB.Add(Key(3)); setB.Add(Key(4));
        this.ShiftReadOnly(storeA);
        setA.IntersectWith(setB);
        Assert.DoesNotContain(Key(1),setA);
        Assert.Contains(Key(2),setA);
        Assert.Contains(Key(3),setA);
        Assert.DoesNotContain(Key(4),setA);
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void NeedCopyUpdate() => this.ReadOnly();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void CopyUpdater() => this.ReadOnly();

    // ── Disk (IsPending path) ─────────────────────────────────────────
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void IsPending経路でも動作する() {
        var folderA = CreateDiskFolder();
        var folderB = CreateDiskFolder();
        using var logDeviceA = Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDeviceA = Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var logDeviceB = Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDeviceB = Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var (setA,storeA) = this.CreateDiskSpill(logDeviceA,objDeviceA);
        var (setB,storeB) = this.CreateDiskSpill(logDeviceB,objDeviceB);
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        for(var a = 0;a<300;a++) {
            setA.Add(Key(a));
            if(a%2==0) setB.Add(Key(a));
        }
        setA.IntersectWith(setB);
        Assert.Contains(Key(100),setA);
        Assert.DoesNotContain(Key(101),setA);
    }
}

public sealed class ManagedIntersectWith : IntersectWithTests<int> {
    protected override int Key(int seed) => seed;
    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateMemory() {
        var store = Factory.Create<int>();
        return (store.Create(),store);
    }
    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create<int>(logDevice,objDevice,MemorySize:Const.MinObjectLogCapacity);
        return (store.Create(),store);
    }
    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create<int>(logDevice,objDevice,MemorySize:Const.MinObjectLogCapacity,MutableFraction:0);
        return (store.Create(),store);
    }
    protected override void ShiftReadOnly(IDisposable store) {
        var s = (ManagedHashSetStore<int>)store;
        s.Store.Log.ShiftReadOnlyAddress(s.Store.Log.TailAddress,true);
    }
}
