using Tsavorite;
using Tsavorite.Stores;
using Tsavorite.core;
namespace Test.Tsavorite.SessionFunctions;

public abstract class RemoveTKeyTValueTests<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    protected abstract TKey Key(int seed);
    protected abstract TValue Value(int seed);
    protected abstract (global::Tsavorite.Collections.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateMemory();
    protected abstract (global::Tsavorite.Collections.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract (global::Tsavorite.Collections.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void FlushAndEvict(IDisposable store);

    protected string CreateDiskFolder() => TestPath.Directory(this);
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    [Fact, Trait("Category","Unit")]
    public void RemoveReturnsValue() {
        var (dict,store)=this.CreateMemory();
        using var _store = store;
        using var _dict = dict;
        dict[this.Key(1)]=this.Value(1);
        Assert.True(dict.Remove(this.Key(1),out var value));
        Assert.Equal(this.Value(1),value);
    }

    [Fact, Trait("Category","Unit")]
    public void RemoveMissingReturnsFalse() {
        var (dict,store)=this.CreateMemory();
        using var _store = store;
        using var _dict = dict;
        Assert.False(dict.Remove(this.Key(999),out _));
    }

    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification = "test-only device creation")]
    public void RemoveAfterDiskSpill() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (dict,store)=this.CreateDiskSpill(logDevice,objDevice);
        using var _store = store;
        using var _dict = dict;
        for(var a = 0; a<300; a++) dict[this.Key(a)]=this.Value(a);
        Assert.True(dict.Remove(this.Key(0),out var value));
        Assert.Equal(this.Value(0),value);
    }

    [Fact, Trait("Category","Unit")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416",Justification = "test-only device creation")]
    public void RemoveAfterFlushAndEvict() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        var (dict,store)=this.CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = dict;
        dict[this.Key(0)]=this.Value(0);
        this.FlushAndEvict(store);
        Assert.True(dict.Remove(this.Key(0),out var value));
        Assert.Equal(this.Value(0),value);
    }
}

public sealed class ManagedRemoveTKeyTValue:RemoveTKeyTValueTests<int,string> {
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (global::Tsavorite.Collections.IDictionary<int,string> Dict,IDisposable Store) CreateMemory() {
        var store = Store.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (global::Tsavorite.Collections.IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (global::Tsavorite.Collections.IDictionary<int,string> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
}

public sealed class UnmanagedRemoveTKeyTValue:RemoveTKeyTValueTests<int,long> {
    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed*100L;
    protected override (global::Tsavorite.Collections.IDictionary<int,long> Dict,IDisposable Store) CreateMemory() {
        var store = Store.Create().Create<int,long>();
        return (store.Create(),store);
    }
    protected override (global::Tsavorite.Collections.IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override (global::Tsavorite.Collections.IDictionary<int,long> Dict,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: Const.MinObjectLogCapacity,MutableFraction: 0).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);
}
