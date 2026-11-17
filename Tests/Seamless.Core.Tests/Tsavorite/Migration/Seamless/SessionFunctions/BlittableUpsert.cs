using Seamless.Collections.Tsavorite;

using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;

public sealed class BlittableUpsert:AddTests<int,long> {
    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed*100L;
    protected override (global::System.Collections.Generic.IDictionary<int,long> Dict,IDisposable Store) CreateMemory() {
        var store = Store.Create().Create<int,long>();
        return (store.Create(),store);
    }
    protected override (global::System.Collections.Generic.IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);
    protected override TryAddResult TryAdd(global::System.Collections.Generic.IDictionary<int,long> dict,int key,long value) =>
        ((global::Seamless.Collections.Tsavorite.IDictionary<int,long>)dict).TryAdd(key,value);

    protected override void DoUpsert() {
    }

    protected override void DoInPlaceWriter() {
    }

    protected override void DoRMW() {
    }

    protected override void DoConcurrentWriter() {
    }

    protected override void DoSingleReader() {
    }

    protected override void DoConcurrentReaderEmpty() {
    }
}
