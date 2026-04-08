using Seamless;
using Seamless.Collections;
using Seamless.Store;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;

public abstract class SymmetricExceptTests<TKey>
    where TKey:IEquatable<TKey> {
    protected abstract TKey Key(int seed);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateMemory();
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void ShiftReadOnly(IDisposable store);
    protected abstract void FlushAndEvict(IDisposable store);

    protected static string CreateDiskFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // ── In-memory (Mutable key) ──────────────────────────────────────
    private void SymmetricExceptWith() {
        var (setA,storeA) = this.CreateMemory();
        var (setB,storeB) = this.CreateMemory();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        setA.Add(Key(1)); setA.Add(Key(2)); setA.Add(Key(3));
        setB.Add(Key(2)); setB.Add(Key(3)); setB.Add(Key(4));
        setA.SymmetricExceptWith(setB);
        Assert.Contains(Key(1),setA);
        Assert.DoesNotContain(Key(2),setA);
        Assert.DoesNotContain(Key(3),setA);
        Assert.Contains(Key(4),setA);
    }
    [Fact,Trait("Category",Unit)] public void InPlaceUpdater() => this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)] public void GetRMWModifiedFieldInfo() => this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)] public void PostRMWOperation() => this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)] public void NeedInitialUpdate() => this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)] public void GetRMWInitialFieldInfo() => this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)] public void InitialUpdater() => this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)] public void PostInitialUpdater() => this.SymmetricExceptWith();

    // ── ReadOnly in-memory (ShiftReadOnlyAddress) ─────────────────────
    private void SymmetricExceptWith_ReadOnly() {
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
        setA.SymmetricExceptWith(setB);
        Assert.Contains(Key(1),setA);
        Assert.DoesNotContain(Key(2),setA);
        Assert.DoesNotContain(Key(3),setA);
        Assert.Contains(Key(4),setA);
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void NeedCopyUpdate() => this.SymmetricExceptWith_ReadOnly();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void CopyUpdater() => this.SymmetricExceptWith_ReadOnly();

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
            if(a%3==0) setB.Add(Key(a));
        }
        setA.SymmetricExceptWith(setB);
        Assert.DoesNotContain(Key(0),setA);
        Assert.Contains(Key(1),setA);
    }

    // ── FlushAndEvict (ConvertOutputToHeap path) ──────────────────────
    private void FlushAndEvictPath() {
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
        setB.Add(Key(2)); setB.Add(Key(3));
        this.FlushAndEvict(storeA);
        setA.SymmetricExceptWith(setB);
        Assert.Contains(Key(1),setA);
        Assert.DoesNotContain(Key(2),setA);
        Assert.DoesNotContain(Key(3),setA);
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void RMWCompletionCallback() => this.FlushAndEvictPath();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void ConvertOutputToHeap() => this.FlushAndEvictPath();
}

public sealed class ManagedSymmetricExcept : SymmetricExceptTests<int> {
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
    protected override void FlushAndEvict(IDisposable store) =>
        ((ManagedHashSetStore<int>)store).Store.Log.FlushAndEvict(true);
}
