using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Unit.SessionFunctions;

//public abstract class SymmetricExceptTests<TKey>
//    where TKey : IEquatable<TKey> {
//    protected abstract TKey Key(int seed);
//    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateMemory();
//    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
//    protected abstract (ManagedHashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
//    protected abstract void ShiftReadOnly(IDisposable store);
//    protected abstract void FlushAndEvict(IDisposable store);
//    private static string CreateDiskFolder() => Path.Combine(Environment.CurrentDirectory,"SeamlessTest_"+Guid.NewGuid().ToString("N"));
//    private const long TestMemorySize = Const.MinObjectLogCapacity;

//    // ���� In-memory (Mutable key) ����������������������������������������������������������������������������
//    private void SymmetricExceptWith() {
//        var (setA,storeA)=this.CreateMemory();
//        var (setB,storeB)=this.CreateMemory();
//        using var _storeA = storeA;
//        using var _storeB = storeB;
//        using var _setA = setA;
//        using var _setB = setB;
//        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
//        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
//        setA.SymmetricExceptWith(setB);
//        Assert.Contains(this.Key(1),setA);
//        Assert.DoesNotContain(this.Key(2),setA);
//        Assert.DoesNotContain(this.Key(3),setA);
//        Assert.Contains(this.Key(4),setA);
//    }
//    [Fact, Trait("Category","Unit")] public void InPlaceUpdater() => this.SymmetricExceptWith();
//    [Fact, Trait("Category","Unit")] public void GetRMWModifiedFieldInfo() => this.SymmetricExceptWith();
//    [Fact, Trait("Category","Unit")] public void PostRMWOperation() => this.SymmetricExceptWith();
//    [Fact, Trait("Category","Unit")] public void NeedInitialUpdate() => this.SymmetricExceptWith();
//    [Fact, Trait("Category","Unit")] public void GetRMWInitialFieldInfo() => this.SymmetricExceptWith();
//    [Fact, Trait("Category","Unit")] public void InitialUpdater() => this.SymmetricExceptWith();
//    [Fact, Trait("Category","Unit")] public void PostInitialUpdater() => this.SymmetricExceptWith();

//    // ���� ReadOnly in-memory (ShiftReadOnlyAddress) ������������������������������������������
//    private void SymmetricExceptWith_ReadOnly() {
//        var folder = CreateDiskFolder();
//        using var logDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
//        using var objDevice = Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
//        var (setA,storeA)=this.CreateDisk(logDevice,objDevice);
//        var (setB,storeB)=this.CreateMemory();
//        using var _storeA = storeA;
//        using var _storeB = storeB;
//        using var _setA = setA;
//        using var _setB = setB;
//        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
//        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
//        this.ShiftReadOnly(storeA);
//        setA.SymmetricExceptWith(setB);
//        Assert.Contains(this.Key(1),setA);
//        Assert.DoesNotContain(this.Key(2),setA);
//        Assert.DoesNotContain(this.Key(3),setA);
//        Assert.Contains(this.Key(4),setA);
//    }
//    [Fact, Trait("Category","Unit")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
//    public void NeedCopyUpdate() => this.SymmetricExceptWith_ReadOnly();
//    [Fact, Trait("Category","Unit")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
//    public void CopyUpdater() => this.SymmetricExceptWith_ReadOnly();

//    // ���� Disk (IsPending path) ����������������������������������������������������������������������������������
//    [Fact, Trait("Category","Unit")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
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
//            setA.Add(this.Key(a));
//            if(a%3==0) setB.Add(this.Key(a));
//        }
//        setA.SymmetricExceptWith(setB);
//        Assert.DoesNotContain(this.Key(0),setA);
//        Assert.Contains(this.Key(1),setA);
//    }

//    // ���� FlushAndEvict (ConvertOutputToHeap path) ��������������������������������������������
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
//        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
//        setB.Add(this.Key(2)); setB.Add(this.Key(3));
//        this.FlushAndEvict(storeA);
//        setA.SymmetricExceptWith(setB);
//        Assert.Contains(this.Key(1),setA);
//        Assert.DoesNotContain(this.Key(2),setA);
//        Assert.DoesNotContain(this.Key(3),setA);
//    }
//    [Fact, Trait("Category","Unit")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
//    public void RMWCompletionCallback() => this.FlushAndEvictPath();
//    [Fact, Trait("Category","Unit")]
//    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
//    public void ConvertOutputToHeap() => this.FlushAndEvictPath();
//}

//public sealed class ManagedSymmetricExcept:SymmetricExceptTests<int> {
//    protected override int Key(int seed) => seed;
//    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateMemory() {
//        var store = HashStore.Create().Create<int>(new ManagedTag());
//        return (store.Create(),store);
//    }
//    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
//        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int>(logDevice,objDevice,new ManagedTag());
//        return (store.Create(),store);
//    }
//    protected override (ManagedHashSet<int> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
//        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDevice,objDevice,new ManagedTag());
//        return (store.Create(),store);
//    }
//    protected override void ShiftReadOnly(IDisposable store) {
//        var s = (ManagedHashSetStore<int>)store;
//        s.Store.Log.ShiftReadOnlyAddress(s.Store.Log.TailAddress,true);
//    }
//    protected override void FlushAndEvict(IDisposable store) =>
//        ((ManagedHashSetStore<int>)store).Store.Log.FlushAndEvict(true);

//}

public abstract class SymmetricExceptWith<TKey,TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : ISeamlessHashSet<TKey>,IDisposable {
    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWithAB() => this.RunWithAB();

    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWithAA() => this.RunWithAA();
}

public sealed class ManagedSymmetricExceptWith:SymmetricExceptWith<string,ManagedHashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (ManagedHashSet<string> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<string>(new ManagedTag());
        return (store.Create(),store);
    }
    protected override void With(ManagedHashSet<string> set,ManagedHashSet<string> other) => set.SymmetricExceptWith(other);
    protected override void ExpectedWith(HashSet<string> set,HashSet<string> other) => set.SymmetricExceptWith(other);
}

public sealed class UnmanagedSymmetricExceptWith:SymmetricExceptWith<int,UnmanagedHashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (UnmanagedHashSet<int> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(UnmanagedHashSet<int> set,UnmanagedHashSet<int> other) => set.SymmetricExceptWith(other);
    protected override void ExpectedWith(HashSet<int> set,HashSet<int> other) => set.SymmetricExceptWith(other);
}
