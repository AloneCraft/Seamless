using Common;

using Garnet.common;

using MemoryPack;

using System.Diagnostics.CodeAnalysis;

using Tsavorite.core;

using Xunit;

using OA = Tsavorite.core.ObjectAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordDisposer>>;
using StoreFunc = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordDisposer>;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class AddSessionFunctions {
    private static string CreateDiskFolder() =>
        Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
    private static FixedSpanByteKey MakeKey(byte[] b) => (FixedSpanByteKey)(ReadOnlySpan<byte>)b;
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    private void Add(){
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"one");
        Assert.True(dict.TryGetValue(1,out var value));
    }
    [Fact,Trait("Category","Coverage")]public void NeedInitialUpdate()=>this.Add();
    [Fact, Trait("Category","Coverage")]public void GetRMWModifiedFieldInfo()=>this.Add();
    [Fact,Trait("Category","Coverage")]public void InitialUpdater()=>this.Add();
    [Fact,Trait("Category","Coverage")]public void PostInitialUpdater()=>this.Add();
    [Fact,Trait("Category","Coverage")]public void PostRMWOperation()=>this.Add();
    [Fact, Trait("Category","Coverage")]public void Reader() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"one");
        Assert.True(dict.TryGetValue(1,out var value));
        Assert.Equal("one",value);
        Assert.False(dict.TryGetValue(99,out _));
    }
    private void Upsert() {
        using var store = new DictionaryStore<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.Add_SessionFunctions>(new Seamless.Collections.Add_SessionFunctions());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
            var status = bc.Upsert(key,SerStr("new"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
    [Fact,Trait("Category","Coverage")]public void GetUpsertFieldInfo_TKey_ReadOnlySpan_PInnedSpanByte()=>this.Upsert();
    [Fact,Trait("Category","Coverage")]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_ReadOnlySpan_SpanByteAndMemory_UpsertInfo()=>this.Upsert();
    [Fact,Trait("Category","Coverage")]public void PostInitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_ReadOnlySpan_SpanByteAndMemory_UpsertInfo()=>this.Upsert();
    [Fact,Trait("Category","Coverage")]public void PostUpsertOperation_TKey_PinnedSpanByte_ReadOnlySpan_UpsertInfo_TEpochAccessor()=>this.Upsert();

    [Fact, Trait("Category","Coverage")]public void InPlaceWriter() {
        using var store = new DictionaryStore<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.Add_SessionFunctions>(new Seamless.Collections.Add_SessionFunctions());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
            var status = bc.Upsert(key,SerStr("new"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        //InPlaceWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
            var status = bc.Upsert(key,SerStr("updated"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }

    [Fact, Trait("Category","Coverage")]
    public void UpsertHeapObject() {
        using var store = new DictionaryStore<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.Add_SessionFunctions>(new Seamless.Collections.Add_SessionFunctions());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(500));
        // 新規キー → GetUpsertFieldInfo(IHeapObject), InitialWriter(IHeapObject), PostInitialWriter(IHeapObject)
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
        // 既存キー → GetUpsertFieldInfo(IHeapObject), InPlaceWriter(IHeapObject), PostUpsertOperation(IHeapObject)
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
    }
    [Fact,Trait("Category","Coverage")]public void GetUpsertFieldInfo_TKey_IHeapObject_PInnedSpanByte()=>this.UpsertHeapObject();
    [Fact,Trait("Category","Coverage")]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=>this.UpsertHeapObject();
    //public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    [Fact,Trait("Category","Coverage")]public void PostUpsertOperation_TKey_PinnedSpanByte_IHeapObject_UpsertInfo_TEpochAccessor()=>this.UpsertHeapObject();
    [Fact,Trait("Category","Coverage")]public void InPlaceWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=>this.UpsertHeapObject();

    private sealed class ScanAndUpsertFunctions(
        ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.Add_SessionFunctions,StoreFunc,OA> session,
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
                var extKey = AddSessionFunctions.MakeKey(externalKeyBytes);
                bc.Upsert<FixedSpanByteKey,TSourceLogRecord>(extKey,ref input,in logRecord,ref output,ref options);
            } else {
                bc.Upsert<TSourceLogRecord>(ref input,in logRecord,ref output,ref options);
            }
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }

    [Fact, Trait("Category","Coverage")]
    public void TSourceLogRecord() {
        using var store = new DictionaryStore<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.Add_SessionFunctions>(new Seamless.Collections.Add_SessionFunctions());
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
    [Fact,Trait("Category","Coverage")]public void GetUpsertFieldInfo_TKey_TSourceLogRecord_PinnedSpanByte()=>this.TSourceLogRecord();
    [Fact,Trait("Category","Coverage")]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo()=>this.TSourceLogRecord();
    [Fact,Trait("Category","Coverage")]public void InPlaceWrit2er_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo()=>this.TSourceLogRecord();

    [Fact, Trait("Category","Coverage")]
    public void RMW() {
        using var store = new DictionaryStore<int,string>();
        var key = MakeKey(SerInt(1));
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.Add_SessionFunctions>(new Seamless.Collections.Add_SessionFunctions());
        var bc = session.BasicContext;

        {
            var input = PinnedSpanByte.FromPinnedSpan(SerStr("v1"));
            SpanByteAndMemory output = default;
            var status = bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            var input = PinnedSpanByte.FromPinnedSpan(SerStr("v2"));
            SpanByteAndMemory output = default;
            var status = bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }
    [Fact,Trait("Category","Coverage")]public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.RMW();
    [Fact,Trait("Category","Coverage")]public void InPlaceUpdater()=>this.RMW();
    [Fact,Trait("Category","Coverage")]public void NeedCopyUpdate()=>this.RMW();

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void AddSessionFunctions_IsPendingパス_RMWCompletionCallbackとReadCompletionCallbackを通す() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }
        // ディスク上のキーにTryAdd → NeedCopyUpdate(→false)、IsPending → RMWCompletionCallback
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(1,"x"));
        // ディスク上のキーにTryGetValue → IsPending → ReadCompletionCallback
        Assert.True(dict.TryGetValue(1,out var value));
        Assert.Equal("v1",value);
    }
    [Fact,Trait("Category","Coverage")]public void RMWCompletionCallback()=>this.AddSessionFunctions_IsPendingパス_RMWCompletionCallbackとReadCompletionCallbackを通す();
    [Fact,Trait("Category","Coverage")]public void ReadCompletionCallback()=>this.AddSessionFunctions_IsPendingパス_RMWCompletionCallbackとReadCompletionCallbackを通す();
    //AddSessionはDelete與羽h内

    //[Fact, Trait("Category","Coverage")]
    //public void AddSessionFunctions_Delete() {
    //    using var store = new DictionaryStore<int,string>();
    //    using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.AddSessionFunctions>(new Seamless.Collections.AddSessionFunctions());
    //    var bc = session.BasicContext;
    //    var key = MakeKey(SerInt(400));
    //    // キー未存在 → InitialDeleter, PostInitialDeleter, PostDeleteOperation
    //    var status1 = bc.Delete(key);
    //    if(status1.IsPending) { bc.CompletePending(wait: true); }
    //    Assert.True(status1.NotFound);
    //    // 既存キー → InPlaceDeleter, PostDeleteOperation
    //    bc.Upsert(key,SerStr("value"));
    //    var status2 = bc.Delete(key);
    //    if(status2.IsPending) { bc.CompletePending(wait: true); }
    //    Assert.False(status2.NotFound);
    //}
    [Fact, Trait("Category","Coverage")]public void ConvertOutputToHeap() {
        var folder = CreateDiskFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: Const.MinObjectLogCapacity);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のRMWでIsPending=true
        // TryAdd on existing key: AddSessionFunctionsのRMWがPENDING → Pending(Session,Status) IsPending=true 分岐を通す
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
}
