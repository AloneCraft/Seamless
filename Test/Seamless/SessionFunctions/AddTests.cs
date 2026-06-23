using Test.Seamless.Collections;

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
        var (dict,store)=this.CreateMemory();
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict.Add(this.Key(1),this.Value(1));
        Assert.True(dict.TryGetValue(this.Key(1),out var value));
        Assert.Equal(this.Value(1),value);
        Assert.False(dict.TryGetValue(this.Key(99),out _));
    }

    [Fact, Trait("Category","Unit")] public void Upsert() => this.DoUpsert();
    [Fact, Trait("Category","Unit")] public void InPlaceWriter() => this.DoInPlaceWriter();
    [Fact, Trait("Category","Unit")] public void RMW() => this.DoRMW();
    [Fact, Trait("Category","Unit")] public void ConcurrentWriter() => this.DoConcurrentWriter();
    [Fact, Trait("Category","Unit")] public void SingleReader() => this.DoSingleReader();
    [Fact, Trait("Category","Unit")] public void ConcurrentReaderEmpty() => this.DoConcurrentReaderEmpty();

    [Fact, Trait("Category","Unit")]
    public void TryAddAfterFlushReportsExisting() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        var (dict,store)=this.CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict.Add(this.Key(0),this.Value(0));
        this.FlushAndEvict(store);
        Assert.Equal(TryAddResult.KeyAlreadyExists,this.TryAdd(dict,this.Key(0),this.Value(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void ReadAfterFlush() {
        var folder = this.CreateDiskFolder();
        using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        var (dict,store)=this.CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;
        dict[this.Key(0)]=this.Value(0);
        this.FlushAndEvict(store);
        Assert.True(dict.TryGetValue(this.Key(0),out var v));
        Assert.Equal(this.Value(0),v);
    }
}
