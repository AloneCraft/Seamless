using Seamless.Tsavorite.Stores;

using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;

public abstract class DeleteTests<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    protected abstract TKey Key(int seed);
    protected abstract TValue Value(int seed);
    protected abstract (global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateMemory();
    protected abstract (global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void FlushAndEvict(IDisposable store);

    protected string CreateDiskFolder() => TestPath.Directory(this);
    private const long TestMemorySize = global::Seamless.Tsavorite.Const.MinObjectLogCapacity;

    [Fact, Trait("Category","Unit")]
    public void RemoveInMemory() {
        var (dict,store)=this.CreateMemory();
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict[this.Key(1)]=this.Value(1);
        Assert.True(dict.Remove(this.Key(1)));
        Assert.False(dict.Remove(this.Key(1)));
    }

    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification = "test-only device creation")]
    public void RemoveAfterDiskSpill() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (dict,store)=this.CreateDiskSpill(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        for(var a = 0;a<300;a++) dict[this.Key(a)]=this.Value(a);
        Assert.True(dict.Remove(this.Key(0)));
        Assert.False(dict.Remove(this.Key(99999)));
    }

    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification = "test-only device creation")]
    public void RemoveAfterFlushAndEvict() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (dict,store)=this.CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict.Add(this.Key(0),this.Value(0));
        this.FlushAndEvict(store);
        dict.Remove(this.Key(0));
    }
}

public sealed class ManagedDelete:DeleteTests<int,string> {
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (global::Seamless.Tsavorite.Collections.IDictionary<int,string> Dict,IDisposable Store) CreateMemory() {
        var store = Store.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (global::Seamless.Tsavorite.Collections.IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (global::Seamless.Tsavorite.Collections.IDictionary<int,string> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
}

public sealed class UnmanagedDelete:DeleteTests<int,long> {
    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed*100L;
    protected override (global::Seamless.Tsavorite.Collections.IDictionary<int,long> Dict,IDisposable Store) CreateMemory() {
        var store = Store.Create().Create<int,long>();
        return (store.Create(),store);
    }
    protected override (global::Seamless.Tsavorite.Collections.IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (global::Seamless.Tsavorite.Collections.IDictionary<int,long> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);
}
