using Seamless;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Unit.SessionFunctions;

public abstract class RemoveKeyValuePairTests<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    protected abstract TKey Key(int seed);
    protected abstract TValue Value(int seed);
    protected abstract (IDictionary<TKey,TValue> Dict,IDisposable Store) CreateMemory();
    protected abstract (IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void FlushAndEvict(IDisposable store);

    protected string CreateDiskFolder() => global::Test.Seamless.TestPath.Directory(this);
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    private void InMemory() {
        var (dict,store)=this.CreateMemory();
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict[this.Key(1)]=this.Value(1);
        Assert.True(dict.Remove(new KeyValuePair<TKey,TValue>(this.Key(1),this.Value(1))));
    }
    [Fact, Trait("Category","Unit")] public void InPlaceUpdater() => this.InMemory();
    [Fact, Trait("Category","Unit")] public void GetRMWModifiedFieldInfo() => this.InMemory();
    [Fact, Trait("Category","Unit")] public void PostRMWOperation() => this.InMemory();

    [Fact, Trait("Category","Unit")]
    public void NeedInitialUpdate() {
        var (dict,store)=this.CreateMemory();
        using var _store = store;
        using var _dict = (IDisposable)dict;
        Assert.False(dict.Remove(new KeyValuePair<TKey,TValue>(this.Key(999),this.Value(1))));
    }

    private void DiskSpill() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (dict,store)=this.CreateDiskSpill(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        for(var a = 0; a<300; a++) dict[this.Key(a)]=this.Value(a);
        Assert.True(dict.Remove(new KeyValuePair<TKey,TValue>(this.Key(0),this.Value(0))));
    }
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:?v???b?g?t?H?[?????????????",Justification = "<?????>")]
    public void NeedCopyUpdate() => this.DiskSpill();
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:?v???b?g?t?H?[?????????????",Justification = "<?????>")]
    public void CopyUpdater() => this.DiskSpill();
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:?v???b?g?t?H?[?????????????",Justification = "<?????>")]
    public void RMWCompletionCallback() => this.DiskSpill();

    private void FlushAndEvictPath() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (dict,store)=this.CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict[this.Key(0)]=this.Value(0);
        this.FlushAndEvict(store);
        Assert.True(dict.Remove(new KeyValuePair<TKey,TValue>(this.Key(0),this.Value(0))));
    }
    [Fact, Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:?v???b?g?t?H?[?????????????",Justification = "<?????>")]
    public void ConvertOutputToHeap() => this.FlushAndEvictPath();
}

public sealed class ManagedRemoveKeyValuePair:RemoveKeyValuePairTests<int,string> {
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (IDictionary<int,string> Dict,IDisposable Store) CreateMemory() {
        var store = HashStore.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (IDictionary<int,string> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
}

public sealed class UnmanagedRemoveKeyValuePair:RemoveKeyValuePairTests<int,long> {
    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed*100L;
    protected override (IDictionary<int,long> Dict,IDisposable Store) CreateMemory() {
        var store = HashStore.Create().Create<int,long>();
        return (store.Create(),store);
    }
    protected override (IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (IDictionary<int,long> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = HashStore.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);
}
