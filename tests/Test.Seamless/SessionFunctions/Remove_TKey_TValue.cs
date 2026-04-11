using Seamless;
using Seamless.Collections;
using Seamless.Store;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;

public abstract class RemoveTKeyTValueTests<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    protected abstract TKey Key(int seed);
    protected abstract TValue Value(int seed);
    protected abstract (ISeamlessDictionary<TKey,TValue> Dict,IDisposable Store) CreateMemory();
    protected abstract (ISeamlessDictionary<TKey,TValue> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (ISeamlessDictionary<TKey,TValue> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void FlushAndEvict(IDisposable store);

    protected static string CreateDiskFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    private void InMemory() {
        var (dict,store) = this.CreateMemory();
        using var _store = store;
        using var _dict = dict;
        dict[Key(1)] = Value(1);
        Assert.True(dict.Remove(Key(1),out var value));
        Assert.Equal(Value(1),value);
    }
    [Fact,Trait("Category","Unit")] public void InPlaceUpdater() => this.InMemory();
    [Fact,Trait("Category","Unit")] public void GetRMWModifiedFieldInfo() => this.InMemory();
    [Fact,Trait("Category","Unit")] public void PostRMWOperation() => this.InMemory();

    [Fact,Trait("Category","Unit")]
    public void NeedInitialUpdate() {
        var (dict,store) = this.CreateMemory();
        using var _store = store;
        using var _dict = dict;
        Assert.False(dict.Remove(Key(999),out _));
    }

    private void DiskSpill() {
        var folder = CreateDiskFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var (dict,store) = this.CreateDiskSpill(logDevice,objDevice);
        using var _store = store;
        using var _dict = dict;
        for(var a = 0;a<300;a++) dict[Key(a)] = Value(a);
        Assert.True(dict.Remove(Key(0),out var value));
        Assert.Equal(Value(0),value);
    }
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification="<�ۗ���>")]
    public void NeedCopyUpdate() => this.DiskSpill();
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification="<�ۗ���>")]
    public void CopyUpdater() => this.DiskSpill();
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification="<�ۗ���>")]
    public void RMWCompletionCallback() => this.DiskSpill();
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification="<�ۗ���>")]
    public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte() => this.DiskSpill();

    private void FlushAndEvictPath() {
        var folder = CreateDiskFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var (dict,store) = this.CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = dict;
        dict[Key(0)] = Value(0);
        this.FlushAndEvict(store);
        Assert.True(dict.Remove(Key(0),out var value));
        Assert.Equal(Value(0),value);
    }
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification="<�ۗ���>")]
    public void ConvertOutputToHeap() => this.FlushAndEvictPath();
}

public sealed class ManagedRemoveTKeyTValue : RemoveTKeyTValueTests<int,string> {
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (ISeamlessDictionary<int,string> Dict,IDisposable Store) CreateMemory() {
        var store = Factory.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (ISeamlessDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize:Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (ISeamlessDictionary<int,string> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize:Const.MinObjectLogCapacity,MutableFraction:0).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((ManagedCollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
}

public sealed class UnmanagedRemoveTKeyTValue : RemoveTKeyTValueTests<int,long> {
    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed*100L;
    protected override (ISeamlessDictionary<int,long> Dict,IDisposable Store) CreateMemory() {
        var store = Factory.Create().Create<int,long>();
        return (store.Create(),store);
    }
    protected override (ISeamlessDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize:Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (ISeamlessDictionary<int,long> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize:Const.MinObjectLogCapacity,MutableFraction:0).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((UnmanagedCollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);
}
