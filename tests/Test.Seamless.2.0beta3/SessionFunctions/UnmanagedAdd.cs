using Garnet.common;
using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
using EqualityEmpty = Seamless.Collections.EqualityEmpty;

namespace Test.Seamless.SessionFunctions;

public sealed class UnmanagedAdd:AddTests<int,long> {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }

    private static FixedSpanByteKey MakeKey(int value) => (FixedSpanByteKey)BitConverter.GetBytes(value);
    private static byte[] ValueBytes(long value) => BitConverter.GetBytes(value);

    protected override int Key(int seed) => seed;
    protected override long Value(int seed) => (long)seed*100L;

    protected override (IDictionary<int,long> Dict,IDisposable Store) CreateMemory() {
        var store = Factory.Create().Create<int,long>();
        return (store.Create(),store);
    }

    protected override (IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Factory.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return (store.Create(),store);
    }

    protected override void FlushAndEvict(IDisposable store) =>
        ((UnmanagedCollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);

    protected override TryAddResult TryAdd(IDictionary<int,long> dict,int key,long value) =>
        ((ISeamlessDictionary<int,long>)dict).TryAdd(key,value);

    protected override void DoUpsert() {
        using var store = Factory.Create().Create<int,long>();
        using var session = store.Store.NewSession<FixedSpanByteKey,long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(default);

        var status = session.BasicContext.Upsert(MakeKey(200),ValueBytes(200L));
        if(status.IsPending) {
            session.BasicContext.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
    }

    protected override void DoInPlaceWriter() {
        using var store = Factory.Create().Create<int,long>();
        using var session = store.Store.NewSession<FixedSpanByteKey,long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(default);

        var basicContext = session.BasicContext;
        var key = MakeKey(200);
        basicContext.Upsert(key,ValueBytes(200L));
        var status = basicContext.Upsert(key,ValueBytes(201L));
        if(status.IsPending) {
            basicContext.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
    }

    protected override void DoRMW() {
        using var store = Factory.Create().Create<int,long>();
        using var session = store.Store.NewSession<FixedSpanByteKey,long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(default);

        var basicContext = session.BasicContext;
        var key = MakeKey(1);
        var input0 = 100L;
        var input1 = 200L;

        {
            var output = false;
            var status = basicContext.RMW(key,ref input0,ref output);
            if(status.IsPending) {
                basicContext.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }

        {
            var output = false;
            var status = basicContext.RMW(key,ref input1,ref output);
            if(status.IsPending) {
                basicContext.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }

    protected override void DoConcurrentWriter() {
        using var store = Factory.Create().Create<int,long>();
        using var session = store.Store.NewSession<FixedSpanByteKey,long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(default);

        var basicContext = session.BasicContext;
        var key = MakeKey(300);
        basicContext.Upsert(key,ValueBytes(300L));
        var status = basicContext.Upsert(key,ValueBytes(301L));
        if(status.IsPending) {
            basicContext.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
    }

    protected override void DoSingleReader() {
        var folder = CreateDiskFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store = store;
        using var _dict = (IDisposable)dict;

        dict[400]=100L;
        ((UnmanagedCollectionStore<int,long>)store).Store.Log.ShiftReadOnlyAddress(((UnmanagedCollectionStore<int,long>)store).Store.Log.TailAddress,wait: true);

        Assert.True(dict.TryGetValue(400,out var actual));
        Assert.Equal(100L,actual);
    }

    protected override void DoConcurrentReaderEmpty() {
        var (dict,store)=CreateMemory();
        using var _store = store;
        using var _dict = (IDisposable)dict;

        dict[500]=0L;

        Assert.True(dict.TryGetValue(500,out var actual));
        Assert.Equal(0L,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void 未使用ハンドラは例外を送出する() {
        var copyUpdater = Assert.Throws<NotSupportedException>(() => ThrowCopyUpdater(default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>)));
        Assert.Equal("NeedCopyUpdate が false を返すので呼ばれない",copyUpdater.Message);

        var postCopyUpdater = Assert.Throws<NotSupportedException>(() => ThrowPostCopyUpdater(default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>)));
        Assert.Equal("NeedCopyUpdate が false を返すので呼ばれない",postCopyUpdater.Message);

        var initialDeleter = Assert.Throws<NotSupportedException>(() => ThrowInitialDeleter(default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>)));
        Assert.Equal("Add は Delete を使用しない",initialDeleter.Message);

        var postInitialDeleter = Assert.Throws<NotSupportedException>(() => ThrowPostInitialDeleter(default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>)));
        Assert.Equal("Add は Delete を使用しない",postInitialDeleter.Message);

        var inPlaceDeleter = Assert.Throws<NotSupportedException>(() => ThrowInPlaceDeleter(default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>)));
        Assert.Equal("Add は Delete を使用しない",inPlaceDeleter.Message);

        var postDeleteOperation = Assert.Throws<NotSupportedException>(() => ThrowPostDeleteOperation(default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>)));
        Assert.Equal("Add は Delete を使用しない",postDeleteOperation.Message);
    }

    private static void ThrowCopyUpdater(global::Seamless.SessionFunctions.UnmanagedAdd<int,long> functions) {
        var logRecord = default(DiskLogRecord);
        var dstLogRecord = default(LogRecord);
        var sizeInfo = default(RecordSizeInfo);
        var input = 0L;
        var output = false;
        var rmwInfo = default(RMWInfo);
        functions.CopyUpdater(in logRecord,ref dstLogRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
    }

    private static void ThrowPostCopyUpdater(global::Seamless.SessionFunctions.UnmanagedAdd<int,long> functions) {
        var logRecord = default(DiskLogRecord);
        var dstLogRecord = default(LogRecord);
        var sizeInfo = default(RecordSizeInfo);
        var input = 0L;
        var output = false;
        var rmwInfo = default(RMWInfo);
        functions.PostCopyUpdater(in logRecord,ref dstLogRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
    }

    private static void ThrowInitialDeleter(global::Seamless.SessionFunctions.UnmanagedAdd<int,long> functions) {
        var logRecord = default(LogRecord);
        var deleteInfo = default(DeleteInfo);
        functions.InitialDeleter(ref logRecord,ref deleteInfo);
    }

    private static void ThrowPostInitialDeleter(global::Seamless.SessionFunctions.UnmanagedAdd<int,long> functions) {
        var logRecord = default(LogRecord);
        var deleteInfo = default(DeleteInfo);
        functions.PostInitialDeleter(ref logRecord,ref deleteInfo);
    }

    private static void ThrowInPlaceDeleter(global::Seamless.SessionFunctions.UnmanagedAdd<int,long> functions) {
        var logRecord = default(LogRecord);
        var deleteInfo = default(DeleteInfo);
        functions.InPlaceDeleter(ref logRecord,ref deleteInfo);
    }

    private static void ThrowPostDeleteOperation(global::Seamless.SessionFunctions.UnmanagedAdd<int,long> functions) {
        var key = MakeKey(1);
        var deleteInfo = default(DeleteInfo);
        var epochAccessor = default(DummyEpochAccessor);
        functions.PostDeleteOperation(key,ref deleteInfo,epochAccessor);
    }
}