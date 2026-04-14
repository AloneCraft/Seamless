using Garnet.common;
using MemoryPack;
using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;

public sealed class ManagedAdd:AddTests<int,string> {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }

    private sealed class DummyHeapObject:IHeapObject {
        public static int MaxSerializedObjectSize => 0;
        public long HeapMemorySize => 0;
        public IHeapObject Clone() => this;
        public void Serialize(BinaryWriter writer) { }
        public void WriteType(BinaryWriter writer,bool includeNamespace) { }
        public void DoSerialize(BinaryWriter writer) { }
        public void CacheSerializedObjectData(ref LogRecord srcLogRecord,ref LogRecord newLogRecord,ref RMWInfo rmwInfo) { }
        public void ClearSerializedObjectData() { }
        public void Dispose() { }
    }

    private static byte[] SerInt(int value) => MemoryPackSerializer.Serialize(value);
    private static byte[] SerStr(string value) => MemoryPackSerializer.Serialize(value);
    private static FixedSpanByteKey MakeKey(byte[] bytes) => (FixedSpanByteKey)(ReadOnlySpan<byte>)bytes;

    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";

    protected override (IDictionary<int,string> Dict,IDisposable Store) CreateMemory() {
        var store=Factory.Create().Create<int,string>();
        return (store.Create(),store);
    }

    protected override (IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store=Factory.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }

    protected override void FlushAndEvict(IDisposable store) =>
        ((ManagedCollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);

    protected override TryAddResult TryAdd(IDictionary<int,string> dict,int key,string value) =>
        ((ISeamlessDictionary<int,string>)dict).TryAdd(key,value);

    protected override void DoUpsert() {
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(200));
        var status=basicContext.Upsert(key,SerStr("new"));

        if(status.IsPending) {
            basicContext.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
    }

    protected override void DoInPlaceWriter() {
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(200));
        var firstStatus=basicContext.Upsert(key,SerStr("new"));
        if(firstStatus.IsPending) {
            basicContext.CompletePendingWithOutputs(out var firstCompleted,wait: true);
            firstCompleted.Dispose();
        }

        var secondStatus=basicContext.Upsert(key,SerStr("updated"));
        if(secondStatus.IsPending) {
            basicContext.CompletePendingWithOutputs(out var secondCompleted,wait: true);
            secondCompleted.Dispose();
        }
    }

    protected override void DoRMW() {
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var key=MakeKey(SerInt(1));
        var input0=PinnedSpanByte.FromPinnedSpan(SerStr("v1"));
        var input1=PinnedSpanByte.FromPinnedSpan(SerStr("v2"));
        var basicContext=session.BasicContext;

        {
            var output=false;
            var status=basicContext.RMW(key,ref input0,ref output);
            if(status.IsPending) {
                basicContext.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }

        {
            var output=false;
            var status=basicContext.RMW(key,ref input1,ref output);
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
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(300));
        basicContext.Upsert(key,SerStr("hello_world_test"));

        var status=basicContext.Upsert(key,SerStr("hi"));
        if(status.IsPending) {
            basicContext.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
    }

    protected override void DoSingleReader() {
        var folder=CreateDiskFolder();
        using var logDevice=Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDevice=Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[400]="single";
        ((ManagedCollectionStore<int,string>)store).Store.Log.ShiftReadOnlyAddress(((ManagedCollectionStore<int,string>)store).Store.Log.TailAddress,wait: true);

        Assert.True(dict.TryGetValue(400,out var actual));
        Assert.Equal("single",actual);
    }

    protected override void DoConcurrentReaderEmpty() {
        var (dict,store)=CreateMemory();
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[500]=string.Empty;

        Assert.True(dict.TryGetValue(500,out var actual));
        Assert.Equal(string.Empty,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void IHeapObject経路() {
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(500));
        basicContext.Upsert(key,(IHeapObject)new DummyHeapObject());
        basicContext.Upsert(key,(IHeapObject)new DummyHeapObject());

        var key2=MakeKey(SerInt(502));
        basicContext.Upsert(key2,SerStr("seed"));
        basicContext.Upsert(key2,(IHeapObject)new DummyHeapObject());

        var functions=new global::Seamless.SessionFunctions.ManagedAdd();
        var logRecord=default(LogRecord);
        var sizeInfo=default(RecordSizeInfo);
        var input=default(PinnedSpanByte);
        var output=false;
        var upsertInfo=default(UpsertInfo);
        functions.PostInitialWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)new DummyHeapObject(),ref output,ref upsertInfo);
    }

    [Fact, Trait("Category","Unit")]
    public void GetUpsertFieldInfo_TKey_IHeapObject_PInnedSpanByte() => this.IHeapObject経路();

    [Fact, Trait("Category","Unit")]
    public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo() => this.IHeapObject経路();

    [Fact, Trait("Category","Unit")]
    public void PostUpsertOperation_TKey_PinnedSpanByte_IHeapObject_UpsertInfo_TEpochAccessor() => this.IHeapObject経路();

    [Fact, Trait("Category","Unit")]
    public void InPlaceWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo() => this.IHeapObject経路();

    [Fact, Trait("Category","Unit")]
    public void PostInitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo() => this.IHeapObject経路();

    private sealed class ScanAndUpsertFunctions(
        ClientSession<
            FixedSpanByteKey,
            PinnedSpanByte,
            bool,
            EqualityEmpty,
            global::Seamless.SessionFunctions.ManagedAdd,
            StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
            SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>
        > session,
        byte[]? externalKeyBytes
    ):IScanIteratorFunctions {
        public bool OnStart(long beginAddress,long endAddress) => true;

        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord:ISourceLogRecord {
            cursorRecordResult=CursorRecordResult.Accept;
            var basicContext=session.BasicContext;
            var input=default(PinnedSpanByte);
            var output=false;
            var options=default(UpsertOptions);

            if(externalKeyBytes is not null) {
                var externalKey=MakeKey(externalKeyBytes);
                basicContext.Upsert(externalKey,ref input,in logRecord,ref output,ref options);
            }
            else {
                basicContext.Upsert(ref input,in logRecord,ref output,ref options);
            }
            return true;
        }

        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    [Fact, Trait("Category","Unit")]
    public void TSourceLogRecord経路() {
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var beginAddress=store.Store.Log.TailAddress;
        session.BasicContext.Upsert(MakeKey(SerInt(600)),SerStr("log"));
        var endAddress=store.Store.Log.TailAddress;

        var initialFunctions=new ScanAndUpsertFunctions(session,SerInt(9999));
        store.Store.Log.Scan(ref initialFunctions,beginAddress,endAddress,DiskScanBufferingMode.NoBuffering);

        var inPlaceFunctions=new ScanAndUpsertFunctions(session,null);
        store.Store.Log.Scan(ref inPlaceFunctions,beginAddress,endAddress,DiskScanBufferingMode.NoBuffering);
    }

    [Fact, Trait("Category","Unit")]
    public void GetUpsertFieldInfo_TKey_TSourceLogRecord_PinnedSpanByte() => this.TSourceLogRecord経路();

    [Fact, Trait("Category","Unit")]
    public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo() => this.TSourceLogRecord経路();

    [Fact, Trait("Category","Unit")]
    public void InPlaceWrit2er_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo() => this.TSourceLogRecord経路();

    [Fact, Trait("Category","Unit")]
    public void CopyUpdater_直接呼び出しは例外() {
        var exception=Assert.Throws<NotSupportedException>(() => ThrowCopyUpdater(default(global::Seamless.SessionFunctions.ManagedAdd)));
        Assert.Equal("NeedCopyUpdateがfalseを返すので呼ばれない",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void PostCopyUpdater_直接呼び出しは例外() {
        var exception=Assert.Throws<NotSupportedException>(() => ThrowPostCopyUpdater(default(global::Seamless.SessionFunctions.ManagedAdd)));
        Assert.Equal("NeedCopyUpdateがfalseを返すので呼ばれない",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void InitialDeleter_直接呼び出しは例外() {
        var exception=Assert.Throws<NotSupportedException>(() => ThrowInitialDeleter(default(global::Seamless.SessionFunctions.ManagedAdd)));
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void PostInitialDeleter_直接呼び出しは例外() {
        var exception=Assert.Throws<NotSupportedException>(() => ThrowPostInitialDeleter(default(global::Seamless.SessionFunctions.ManagedAdd)));
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void InPlaceDeleter_直接呼び出しは例外() {
        var exception=Assert.Throws<NotSupportedException>(() => ThrowInPlaceDeleter(default(global::Seamless.SessionFunctions.ManagedAdd)));
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void PostDeleteOperation_直接呼び出しは例外() {
        var exception=Assert.Throws<NotSupportedException>(() => ThrowPostDeleteOperation(default(global::Seamless.SessionFunctions.ManagedAdd)));
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    private static void ThrowCopyUpdater(global::Seamless.SessionFunctions.ManagedAdd functions) {
        var logRecord=default(DiskLogRecord);
        var dstLogRecord=default(LogRecord);
        var sizeInfo=default(RecordSizeInfo);
        var input=default(PinnedSpanByte);
        var output=false;
        var rmwInfo=default(RMWInfo);
        functions.CopyUpdater(in logRecord,ref dstLogRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
    }

    private static void ThrowPostCopyUpdater(global::Seamless.SessionFunctions.ManagedAdd functions) {
        var logRecord=default(DiskLogRecord);
        var dstLogRecord=default(LogRecord);
        var sizeInfo=default(RecordSizeInfo);
        var input=default(PinnedSpanByte);
        var output=false;
        var rmwInfo=default(RMWInfo);
        functions.PostCopyUpdater(in logRecord,ref dstLogRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
    }

    private static void ThrowInitialDeleter(global::Seamless.SessionFunctions.ManagedAdd functions) {
        var logRecord=default(LogRecord);
        var deleteInfo=default(DeleteInfo);
        functions.InitialDeleter(ref logRecord,ref deleteInfo);
    }

    private static void ThrowPostInitialDeleter(global::Seamless.SessionFunctions.ManagedAdd functions) {
        var logRecord=default(LogRecord);
        var deleteInfo=default(DeleteInfo);
        functions.PostInitialDeleter(ref logRecord,ref deleteInfo);
    }

    private static void ThrowInPlaceDeleter(global::Seamless.SessionFunctions.ManagedAdd functions) {
        var logRecord=default(LogRecord);
        var deleteInfo=default(DeleteInfo);
        functions.InPlaceDeleter(ref logRecord,ref deleteInfo);
    }

    private static void ThrowPostDeleteOperation(global::Seamless.SessionFunctions.ManagedAdd functions) {
        var keyBytes=SerInt(1);
        var key=MakeKey(keyBytes);
        var deleteInfo=default(DeleteInfo);
        var epochAccessor=default(DummyEpochAccessor);
        functions.PostDeleteOperation(key,ref deleteInfo,epochAccessor);
    }
}