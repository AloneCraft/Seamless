using Seamless;
using Seamless.Collections;
using Seamless.Store;
using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;

public sealed class UnmanagedAdd : AddTests<int, long> {
    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed * 100L;
    protected override (IDictionary<int, long> Dict, IDisposable Store) CreateMemory() {
        var store = Factory.CreateUnmanaged<int, long>();
        return (store.Create(), store);
    }
    protected override (IDictionary<int, long> Dict, IDisposable Store) CreateDisk(IDevice logDevice, IDevice objDevice) {
        var store = Factory.CreateUnmanaged<int, long>(logDevice, objDevice, MemorySize: Const.MinObjectLogCapacity);
        return (store.Create(), store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((UnmanagedCollectionStore<int, long>)store).Store.Log.FlushAndEvict(true);
    protected override TryAddResult TryAdd(IDictionary<int, long> dict, int key, long value) =>
        ((UnmanagedDictionary<int, long>)dict).TryAdd(key, value);

    protected override void DoUpsert() {
#if TSAVORITE1_1_1
        using var store = Factory.CreateUnmanaged<int, long>();
        using var session = store.Store.NewSession<long, long, Tsavorite.core.Empty, global::Seamless.SessionFunctions.UnmanagedAdd<int, long>>(new());
        var bc = session.BasicContext;
        var key = 200;
        var val = 200L;
        var status = bc.Upsert(ref key, ref val);
        if(status.IsPending) {
            bc.CompletePendingWithOutputs(out var completed, wait: true);
            completed.Dispose();
        }
#endif
    }

    protected override void DoInPlaceWriter() {
#if TSAVORITE1_1_1
        using var store = Factory.CreateUnmanaged<int, long>();
        using var session = store.Store.NewSession<long, long, Tsavorite.core.Empty, global::Seamless.SessionFunctions.UnmanagedAdd<int, long>>(new());
        var bc = session.BasicContext;
        var key = 200;
        var valFirst = 200L;
        var s1 = bc.Upsert(ref key, ref valFirst);
        if(s1.IsPending) {
            bc.CompletePendingWithOutputs(out var c1, wait: true);
            c1.Dispose();
        }
        var valSecond = 201L;
        var s2 = bc.Upsert(ref key, ref valSecond);
        if(s2.IsPending) {
            bc.CompletePendingWithOutputs(out var c2, wait: true);
            c2.Dispose();
        }
#endif
    }

    protected override void DoRMW() {
#if TSAVORITE1_1_1
        using var store = Factory.CreateUnmanaged<int, long>();
        using var session = store.Store.NewSession<long, long, Tsavorite.core.Empty, global::Seamless.SessionFunctions.UnmanagedAdd<int, long>>(new());
        var key = 1;
        var input0 = 100L;
        var input1 = 200L;
        var bc = session.BasicContext;
        {
            long output = default;
            var status = bc.RMW(ref key, ref input0, ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed, wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            long output = default;
            var status = bc.RMW(ref key, ref input1, ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed, wait: true);
                completed.Next();
                status = completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
#endif
    }

    protected override void DoConcurrentWriter() {
#if TSAVORITE1_1_1
        using var store = Factory.CreateUnmanaged<int, long>();
        using var session = store.Store.NewSession<long, long, Tsavorite.core.Empty, global::Seamless.SessionFunctions.UnmanagedAdd<int, long>>(new());
        var bc = session.BasicContext;
        var key = 300;
        var valFirst = 300L;
        bc.Upsert(ref key, ref valFirst);
        var valSecond = 301L;
        var status = bc.Upsert(ref key, ref valSecond);
        if(status.IsPending) {
            bc.CompletePendingWithOutputs(out var c, wait: true);
            c.Dispose();
        }
#endif
    }

    protected override void DoSingleReader() {
#if TSAVORITE1_1_1
        var folder = CreateDiskFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder, "log.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder + "_obj", "obj.data"), deleteOnClose: true, capacity: Const.MinObjectLogCapacity);
        using var store = Factory.CreateUnmanaged<int, long>(logDevice, objDevice, MemorySize: Const.MinObjectLogCapacity);
        using var session = store.Store.NewSession<long, long, Tsavorite.core.Empty, global::Seamless.SessionFunctions.UnmanagedAdd<int, long>>(new());
        var bc = session.BasicContext;
        var key1 = 400;
        var val1 = 100L;
        bc.Upsert(ref key1, ref val1);
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress, wait: true);
        long output1 = default;
        var status1 = bc.Read(ref key1, ref output1);
        if(status1.IsPending) {
            bc.CompletePendingWithOutputs(out var c1, wait: true);
            c1.Dispose();
        }
        Assert.True(status1.Found);
#endif
    }

    protected override void DoConcurrentReaderEmpty() {
#if TSAVORITE1_1_1
        using var store = Factory.CreateUnmanaged<int, long>();
        using var session = store.Store.NewSession<long, long, Tsavorite.core.Empty, global::Seamless.SessionFunctions.UnmanagedAdd<int, long>>(new());
        var bc = session.BasicContext;
        var key = 500;
        var val = 0L;
        bc.Upsert(ref key, ref val);
        long output = default;
        var status = bc.Read(ref key, ref output);
        Assert.True(status.Found);
#endif
    }
}
