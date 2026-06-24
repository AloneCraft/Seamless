using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Collections;
using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;

public abstract class AddTests<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {

    protected abstract TKey Key(int seed);
    protected abstract TValue Value(int seed);
    protected abstract (global::System.Collections.Generic.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateMemory();
    protected abstract (global::System.Collections.Generic.IDictionary<TKey,TValue> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract void FlushAndEvict(IDisposable store);
    protected abstract TryAddResult TryAdd(global::System.Collections.Generic.IDictionary<TKey,TValue> dict,TKey key,TValue value);

    protected abstract void DoUpsert();
    protected abstract void DoInPlaceWriter();
    protected abstract void DoRMW();
    protected abstract void DoConcurrentWriter();
    protected abstract void DoSingleReader();
    protected abstract void DoConcurrentReaderEmpty();

    protected string CreateDiskFolder() =>
        TestPath.Directory(this);

    [Fact, Trait("Category","Unit")]
    public void AddAndRead() {
        var (dict,store)=CreateMemory();
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict.Add(Key(1),Value(1));
        Assert.True(dict.TryGetValue(Key(1),out var value));
        Assert.Equal(Value(1),value);
        Assert.False(dict.TryGetValue(Key(99),out _));
    }

    [Fact, Trait("Category","Unit")] public void Upsert() => DoUpsert();
    [Fact, Trait("Category","Unit")] public void InPlaceWriter() => DoInPlaceWriter();
    [Fact, Trait("Category","Unit")] public void RMW() => DoRMW();
    [Fact, Trait("Category","Unit")] public void ConcurrentWriter() => DoConcurrentWriter();
    [Fact, Trait("Category","Unit")] public void SingleReader() => DoSingleReader();
    [Fact, Trait("Category","Unit")] public void ConcurrentReaderEmpty() => DoConcurrentReaderEmpty();

    [Fact, Trait("Category","Unit")]
    public void TryAddAfterFlushReportsExisting() {
        var folder = CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict.Add(Key(0),Value(0));
        FlushAndEvict(store);
        Assert.Equal(TryAddResult.KeyAlreadyExists,TryAdd(dict,Key(0),Value(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void ReadAfterFlush() {
        var folder = CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict[Key(0)]=Value(0);
        FlushAndEvict(store);
        Assert.True(dict.TryGetValue(Key(0),out var v));
        Assert.Equal(Value(0),v);
    }
}
