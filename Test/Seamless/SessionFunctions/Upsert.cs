using Garnet.common;

using MemoryPack;

using Seamless.Collections.Tsavorite;

using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;


public sealed class Upsert:AddTests<int,string> {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
    private static FixedSpanByteKey MakeKey(byte[] b) => (FixedSpanByteKey)(ReadOnlySpan<byte>)b;
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (global::System.Collections.Generic.IDictionary<int,string> Dict,IDisposable Store) CreateMemory() {
        var store = Store.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (global::System.Collections.Generic.IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store = Store.Create(MemorySize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
    protected override TryAddResult TryAdd(global::System.Collections.Generic.IDictionary<int,string> dict,int key,string value) =>
        ((global::Seamless.Collections.Tsavorite.IDictionary<int,string>)dict).TryAdd(key,value);
    protected override void DoUpsert() {
        using var store = Store.Create().Create<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,string,bool,EqualityEmpty,global::Seamless.Collections.Tsavorite.Upsert<string,global::Seamless.Collections.Serializers.MemoryPack<string>>>(new());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        // Upsert writerの動作を検証する。
        {
            var status = bc.Upsert(key,SerStr("new"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
    protected override void DoInPlaceWriter() {
        using var store = Store.Create().Create<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,string,bool,EqualityEmpty,global::Seamless.Collections.Tsavorite.Upsert<string,global::Seamless.Collections.Serializers.MemoryPack<string>>>(new());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        // Upsert writerの動作を検証する。
        {
            var status = bc.Upsert(key,SerStr("new"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        // Upsert writerの動作を検証する。
        {
            var status = bc.Upsert(key,SerStr("updated"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
    private sealed class DummyHeapObject:IHeapObject {
        public static int MaxSerializedObjectSize => 0;
        public long HeapMemorySize => 0;
        public IHeapObject Clone() => this;
        public void Serialize(BinaryWriter writer) { }
        public void WriteType(BinaryWriter writer,bool includeNamespace) { }
        public void DoSerialize(BinaryWriter writer) { }
        public void CacheSerializedObjectData(ref LogRecord dstLogRecord,ref RMWInfo rmwInfo,bool srcIsOnMemoryLog) { }
        public void ClearSerializedObjectData() { }
        public void Dispose() { }
    }
    private sealed class DummyHeapObjectSerializer:BinaryObjectSerializer<IHeapObject> {
        public override void Serialize(IHeapObject obj) { }
        public override void Deserialize(out IHeapObject obj) => obj=new DummyHeapObject();
    }
    private sealed class HeapObjectReader:SessionFunctionsBase<EqualityEmpty,IHeapObject,EqualityEmpty> {
        public override bool Reader<TSourceLogRecord>(in TSourceLogRecord record,ref EqualityEmpty input,ref IHeapObject output,ref ReadInfo readInfo) {
            output=record.ValueObject;
            return true;
        }
    }
    [Fact, Trait("Category","Unit")]
    public void UpsertHeapObject() {
        // SpanByteAllocatorはinline値専用。object writerはObjectAllocatorで実際に読み戻す。
        using var log=new NullDevice();
        using var objectLog=new NullDevice();
        using var settings=new KVSettings {
            IndexSize=1L<<12,LogMemorySize=1L<<20,PageSize=1L<<12,SegmentSize=1L<<20,
            ObjectLogSegmentSize=1L<<20,LogDevice=log,ObjectLogDevice=objectLog,
        };
        using var store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordTriggers>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordTriggers>>>(
            settings,new(new(),static()=>new DummyHeapObjectSerializer(),new()),static (allocatorSettings,functions)=>new(allocatorSettings,functions));
        using var session=store.NewSession<FixedSpanByteKey,string,bool,EqualityEmpty,global::Seamless.Collections.Tsavorite.Upsert<string,global::Seamless.Collections.Serializers.MemoryPack<string>>>(new());
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(500));
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
        var updated=new DummyHeapObject();
        bc.Upsert(key,(IHeapObject)updated);
        var key2=MakeKey(SerInt(502));
        bc.Upsert(key2,SerStr("seed"));
        var converted=new DummyHeapObject();
        bc.Upsert(key2,(IHeapObject)converted);
        using var reader=store.NewSession<FixedSpanByteKey,EqualityEmpty,IHeapObject,EqualityEmpty,HeapObjectReader>(new());
        IHeapObject value=null!;
        Assert.True(reader.BasicContext.Read(key,ref value).Found);
        Assert.Same(updated,value);
        Assert.True(reader.BasicContext.Read(key2,ref value).Found);
        Assert.Same(converted,value);
    }
    private sealed class ScanAndUpsertFunctions(
        ClientSession<
            FixedSpanByteKey,
            string,//TInput
            bool,
            EqualityEmpty,
            global::Seamless.Collections.Tsavorite.Upsert<string,global::Seamless.Collections.Serializers.MemoryPack<string>>,
            StoreFunctions<SpanByteComparer,DefaultRecordTriggers>,
            SpanByteAllocator<
                StoreFunctions<
                    SpanByteComparer,
                    DefaultRecordTriggers
                >
            >
        >
        session,
        byte[]? externalKeyBytes
    ):IScanIteratorFunctions {
        public bool OnStart(long beginAddress,long endAddress) => true;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            cursorRecordResult=CursorRecordResult.Accept;
            var bc = session.BasicContext;
            var input = default(string)!;
            var output = false;
            var options = default(UpsertOptions);
            if(externalKeyBytes is not null) {
                var extKey = MakeKey(externalKeyBytes);
                bc.Upsert(extKey,ref input,in logRecord,ref output,ref options);
            } else {
                bc.Upsert(ref input,in logRecord,ref output,ref options);
            }
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    [Fact, Trait("Category","Unit")]
    public void TSourceLogRecord() {
        using var store = Store.Create().Create<int,string>();
        using var session = store.Store.NewSession<
            FixedSpanByteKey,
            string,//TInput
            bool,
            EqualityEmpty,
            global::Seamless.Collections.Tsavorite.Upsert<string,global::Seamless.Collections.Serializers.MemoryPack<string>>
        >(new());
        var addr1 = store.Store.Log.TailAddress;
        session.BasicContext.Upsert(MakeKey(SerInt(600)),SerStr("log"));
        var addr2 = store.Store.Log.TailAddress;
        // Upsert 経路はこの処理では使用しない。
        var initFuncs = new ScanAndUpsertFunctions(session,SerInt(9999));
        store.Store.Log.Scan(ref initFuncs,addr1,addr2,DiskScanBufferingMode.NoBuffering);
        // Upsert writerの動作を検証する。
        var inplaceFuncs = new ScanAndUpsertFunctions(session,null);
        store.Store.Log.Scan(ref inplaceFuncs,addr1,addr2,DiskScanBufferingMode.NoBuffering);
    }

    protected override void DoRMW() {
        using var store = Store.Create().Create<int,string>();
        var key = MakeKey(SerInt(1));
        using var session = store.Store.NewSession<FixedSpanByteKey,string,bool,EqualityEmpty,global::Seamless.Collections.Tsavorite.Upsert<string,global::Seamless.Collections.Serializers.MemoryPack<string>>>(new());
        var input0 = "v1";
        var input1 = "v2";
        var bc = session.BasicContext;
        {
            var output = false;
            var status = bc.RMW(key,ref input0,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            var output = false;
            var status = bc.RMW(key,ref input1,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void TryAddAfterFlushReportsExisting_Managed() {
        var folder = this.CreateDiskFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        dict.Add(0,"target");
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    [Fact, Trait("Category","Unit")]
    public void ReadAfterFlush_Managed() {
        var folder = this.CreateDiskFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        dict[0]="target";
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.TryGetValue(0,out var v));
        Assert.Equal("target",v);
    }
    protected override void DoConcurrentWriter() {
    }
    protected override void DoSingleReader() {
    }
    protected override void DoConcurrentReaderEmpty() {
    }
}
