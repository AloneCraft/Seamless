
using Garnet.common;

using MemoryPack;
using Seamless;
using Seamless.Collections;
using Test.Seamless.Collections;
using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;


public class Add {
    private static string CreateDiskFolder() =>
        Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
#if TSAVORITE2_0BETA3
    private static FixedSpanByteKey MakeKey(byte[] b) => (FixedSpanByteKey)(ReadOnlySpan<byte>)b;
#elif TSAVORITE1_1_1
    private static SpanByte MakeKey(byte[] b) =>SpanByte.FromPinnedSpan(b);
#endif
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    private void Add_TKey_TValue(){
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"one");
        Assert.True(dict.TryGetValue(1,out var value));
    }
    [Fact,Trait("Category",Unit)]public void NeedInitialUpdate()=>this.Add_TKey_TValue();
    [Fact, Trait("Category",Unit)]public void GetRMWModifiedFieldInfo()=>this.Add_TKey_TValue();
    [Fact,Trait("Category",Unit)]public void InitialUpdater()=>this.Add_TKey_TValue();
    [Fact,Trait("Category",Unit)]public void PostInitialUpdater()=>this.Add_TKey_TValue();
    [Fact,Trait("Category",Unit)]public void PostRMWOperation()=>this.Add_TKey_TValue();
    [Fact, Trait("Category",Unit)]public void Reader() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"one");
        Assert.True(dict.TryGetValue(1,out var value));
        Assert.Equal("one",value);
        Assert.False(dict.TryGetValue(99,out _));
    }
    private void Upsert() {
        using var store = Store.Create<int,string>();
#if TSAVORITE2_0BETA3
        using var session = store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new global::Seamless.SessionFunctions.Add());
#elif TSAVORITE1_1_1
        using var session = store.Store.NewSession<SpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new());
#endif
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
#if TSAVORITE2_0BETA3
            var status = bc.Upsert(key,SerStr("new"));
#elif TSAVORITE1_1_1
            var val = MakeKey(SerStr("new"));
            var status = bc.Upsert(ref key,ref val);
#endif
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
    [Fact,Trait("Category",Unit)]public void GetUpsertFieldInfo_TKey_ReadOnlySpan_PInnedSpanByte()=>this.Upsert();
    [Fact,Trait("Category",Unit)]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_ReadOnlySpan_SpanByteAndMemory_UpsertInfo()=>this.Upsert();
    [Fact,Trait("Category",Unit)]public void PostInitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_ReadOnlySpan_SpanByteAndMemory_UpsertInfo()=>this.Upsert();
    [Fact,Trait("Category",Unit)]public void PostUpsertOperation_TKey_PinnedSpanByte_ReadOnlySpan_UpsertInfo_TEpochAccessor()=>this.Upsert();

    [Fact, Trait("Category",Unit)]public void InPlaceWriter() {
        using var store = Store.Create<int,string>();
#if TSAVORITE2_0BETA3
        using var session = store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new global::Seamless.SessionFunctions.Add());
#elif TSAVORITE1_1_1
        using var session = store.Store.NewSession<SpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new());
#endif
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
#if TSAVORITE2_0BETA3
            var status = bc.Upsert(key,SerStr("new"));
#elif TSAVORITE1_1_1
            var val = MakeKey(SerStr("new"));
            var status = bc.Upsert(ref key,ref val);
#endif
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        //InPlaceWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
#if TSAVORITE2_0BETA3
            var status = bc.Upsert(key,SerStr("updated"));
#elif TSAVORITE1_1_1
            var val = MakeKey(SerStr("updated"));
            var status = bc.Upsert(ref key,ref val);
#endif
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
#if TSAVORITE2_0BETA3
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
    [Fact, Trait("Category",Unit)]
    public void UpsertHeapObject() {
        using var store = Store.Create<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new global::Seamless.SessionFunctions.Add());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(500));
        // 新規キー → GetUpsertFieldInfo(IHeapObject), InitialWriter(IHeapObject)
        // SpanByteAllocatorではInitialWriter(IHeapObject)がfalseを返すためPostInitialWriter(IHeapObject)は呼ばれない
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
        // PostUpsertOperation(IHeapObject) → finallyブロックで常に呼ばれる
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
        // スパン値で事前登録→IHeapObjectでUpsert → 既存レコード発見→InPlaceWriter(IHeapObject)が呼ばれる
        // SpanByteAllocatorはValueIsObjectビットをクリアするためInPlaceWriterはfalseを返す
        var key2 = MakeKey(SerInt(502));
        bc.Upsert(key2,SerStr("seed"));
        bc.Upsert(key2,(IHeapObject)new DummyHeapObject());
        // PostInitialWriter(IHeapObject)はSpanByteAllocatorでは到達しないため直接呼び出す（bodyが空なのでdefault引数で安全）
        {
            var f = new global::Seamless.SessionFunctions.Add();
            var lr = default(LogRecord);
            var si = default(RecordSizeInfo);
            var inp = default(PinnedSpanByte);
            var outp = default(SpanByteAndMemory);
            var ui = default(UpsertInfo);
            f.PostInitialWriter(ref lr,in si,ref inp,(IHeapObject)new DummyHeapObject(),ref outp,ref ui);
        }
    }
#endif
#if TSAVORITE2_0BETA3
    [Fact,Trait("Category",Unit)]public void GetUpsertFieldInfo_TKey_IHeapObject_PInnedSpanByte()=>this.UpsertHeapObject();
    [Fact,Trait("Category",Unit)]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=>this.UpsertHeapObject();
    [Fact,Trait("Category",Unit)]public void PostUpsertOperation_TKey_PinnedSpanByte_IHeapObject_UpsertInfo_TEpochAccessor()=>this.UpsertHeapObject();
    [Fact,Trait("Category",Unit)]public void InPlaceWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=>this.UpsertHeapObject();
    [Fact,Trait("Category",Unit)]public void PostInitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=>this.UpsertHeapObject();

    private sealed class ScanAndUpsertFunctions(
        ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>
        session,
        byte[]? externalKeyBytes
    ) : IScanIteratorFunctions {
        public bool OnStart(long beginAddress,long endAddress) => true;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            cursorRecordResult = CursorRecordResult.Accept;
            var bc = session.BasicContext;
            var input = default(PinnedSpanByte);
            var output = default(SpanByteAndMemory);
            var options = default(UpsertOptions);
            if(externalKeyBytes is not null) {
                var extKey = MakeKey(externalKeyBytes);
                bc.Upsert<FixedSpanByteKey,TSourceLogRecord>(extKey,ref input,in logRecord,ref output,ref options);
            } else {
                bc.Upsert<TSourceLogRecord>(ref input,in logRecord,ref output,ref options);
            }
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    [Fact, Trait("Category",Unit)]
    public void TSourceLogRecord() {
        using var store = Store.Create<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new());
        var addr1 = store.Store.Log.TailAddress;
        session.BasicContext.Upsert(MakeKey(SerInt(600)),SerStr("log"));
        var addr2 = store.Store.Log.TailAddress;
        // 新規キー(9999) → GetUpsertFieldInfo<TKey,TSourceLogRecord>, InitialWriter<TSourceLogRecord>, PostInitialWriter<TSourceLogRecord>
        var initFuncs = new ScanAndUpsertFunctions(session,SerInt(9999));
        store.Store.Log.Scan(ref initFuncs,addr1,addr2,DiskScanBufferingMode.NoBuffering);
        // 既存キー(600) → GetUpsertFieldInfo<TSourceLogRecord,TSourceLogRecord>, InPlaceWriter<TSourceLogRecord>
        var inplaceFuncs = new ScanAndUpsertFunctions(session,null);
        store.Store.Log.Scan(ref inplaceFuncs,addr1,addr2,DiskScanBufferingMode.NoBuffering);
    }
    [Fact,Trait("Category",Unit)]public void GetUpsertFieldInfo_TKey_TSourceLogRecord_PinnedSpanByte()=>this.TSourceLogRecord();
    [Fact,Trait("Category",Unit)]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo()=>this.TSourceLogRecord();
    [Fact,Trait("Category",Unit)]public void InPlaceWrit2er_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo()=>this.TSourceLogRecord();
#endif

    [Fact, Trait("Category",Unit)]
    public void RMW() {
        using var store = Store.Create<int,string>();
        var key = MakeKey(SerInt(1));
#if TSAVORITE2_0BETA3
        using var session = store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, global::Seamless.Collections.Empty, global::Seamless.SessionFunctions.Add>(new());
        var input0 = PinnedSpanByte.FromPinnedSpan(SerStr("v1"));
        var input1 = PinnedSpanByte.FromPinnedSpan(SerStr("v2"));
#elif TSAVORITE1_1_1
        using var session = store.Store.NewSession<SpanByte,SpanByteAndMemory,global::Seamless.Collections.Empty,global::Seamless.SessionFunctions.Add>(new());
        var input0 = SpanByte.FromPinnedSpan(SerStr("v1"));
        var input1 = SpanByte.FromPinnedSpan(SerStr("v2"));
#endif
        var bc = session.BasicContext;

        {
            SpanByteAndMemory output = default;
#if TSAVORITE2_0BETA3
            var status = bc.RMW(key,ref input0,ref output);
#elif TSAVORITE1_1_1
            var status = bc.RMW(ref key,ref input0,ref output);
#endif
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            SpanByteAndMemory output = default;
#if TSAVORITE2_0BETA3
            var status = bc.RMW(key,ref input1,ref output);
#elif TSAVORITE1_1_1
            var status = bc.RMW(ref key,ref input1,ref output);
#endif
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }
    [Fact,Trait("Category",Unit)]public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.RMW();
    [Fact,Trait("Category",Unit)]public void InPlaceUpdater()=>this.RMW();
    [Fact,Trait("Category",Unit)]public void NeedCopyUpdate()=>this.RMW();
    [Fact,Trait("Category",Unit)]public void RMWCompletionCallback(){
        var folder = CreateDiskFolder();
        using var LogDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のRMWでIsPending=true
        // TryAdd on existing key: AddSessionFunctionsのRMWがPENDING → Pending(Session,Status) IsPending=true 分岐を通す
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    [Fact,Trait("Category",Unit)]public void ReadCompletionCallback(){
        var folder = CreateDiskFolder();
        using var LogDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict[0]="target";
        // 全レコードをディスクに追い出す → 次のReadでIsPending=true
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.TryGetValue(0,out var v));
        Assert.Equal("target",v);
    }
    [Fact, Trait("Category",Unit)]public void ConvertOutputToHeap() {
        var folder = CreateDiskFolder();
        using var LogDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のRMWでIsPending=true
        // TryAdd on existing key: AddSessionFunctionsのRMWがPENDING → Pending(Session,Status) IsPending=true 分岐を通す
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    [Fact,Trait("Category",Unit)]
    public void ConcurrentWriter() {
#if TSAVORITE1_1_1
        // 長い値で初回Upsert後、短い値で再Upsert → src.Length<=dst.Length → in-place コピー分岐を通す
        using var store = Store.Create<int,string>();
        using var session = store.Store.NewSession<SpanByte,SpanByteAndMemory,global::Seamless.Collections.Empty,global::Seamless.SessionFunctions.Add>(new());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(300));
        var valLong = MakeKey(SerStr("hello_world_test"));
        bc.Upsert(ref key,ref valLong);
        var valShort = MakeKey(SerStr("hi"));
        var status = bc.Upsert(ref key,ref valShort);
        if(status.IsPending) {
            bc.CompletePendingWithOutputs(out var c,wait: true);
            c.Dispose();
        }
#endif
    }
    [Fact,Trait("Category",Unit)]
    public void SingleReader() {
#if TSAVORITE1_1_1
        // ShiftReadOnlyAddress はページフラッシュを伴うため ManagedLocalStorageDevice が必要
        var folder = CreateDiskFolder();
        using var LogDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var session = store.Store.NewSession<SpanByte,SpanByteAndMemory,global::Seamless.Collections.Empty,global::Seamless.SessionFunctions.Add>(new());
        var bc = session.BasicContext;
        var key1 = MakeKey(SerInt(400));
        var val1 = MakeKey(SerStr("single"));
        bc.Upsert(ref key1,ref val1);
        // ReadOnlyAddress をTailAddressまで進める → 書いたレコードが in-memory read-only 領域へ → SingleReader が呼ばれる
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);
        var status1 = bc.Read(key1,out var output1);
        if(status1.IsPending) {
            bc.CompletePendingWithOutputs(out var c1,wait: true);
            c1.Dispose();
        }
        Assert.True(status1.Found);
        output1.Memory?.Dispose();
        // IsEmpty 分岐: 空値を read-only 領域に置いて Read → Span.IsEmpty=true パスを通す
        var key2 = MakeKey(SerInt(401));
        var empty = default(SpanByte);
        bc.Upsert(ref key2,ref empty);
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);
        var status2 = bc.Read(key2,out _);
        if(status2.IsPending) {
            bc.CompletePendingWithOutputs(out var c2,wait: true);
            c2.Dispose();
        }
        Assert.True(status2.Found);
#endif
    }
    [Fact,Trait("Category",Unit)]
    public void ConcurrentReader_Empty() {
#if TSAVORITE1_1_1
        // mutable 領域に空値を置いて Read → ConcurrentReader の Span.IsEmpty=true 分岐を通す
        using var store = Store.Create<int,string>();
        using var session = store.Store.NewSession<SpanByte,SpanByteAndMemory,global::Seamless.Collections.Empty,global::Seamless.SessionFunctions.Add>(new());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(500));
        var empty = default(SpanByte);
        bc.Upsert(ref key,ref empty);
        var status = bc.Read(key,out _);
        Assert.True(status.Found);
#endif
    }
}
