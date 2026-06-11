using Garnet.common;

using MemoryPack;

using Tsavorite;
using Tsavorite.Collections;
using Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Tsavorite.Collections.Unit.SessionFunctions;


public sealed class Upsert:AddTests<int,string> {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume(){ }
        public bool TrySuspend() => true;
    }
    private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
#if TSAVORITE2_0BETA3
    private static FixedSpanByteKey MakeKey(byte[] b)=>(FixedSpanByteKey)(ReadOnlySpan<byte>)b;
#elif TSAVORITE1_1_1
    private static SpanByte MakeKey(byte[] b) => SpanByte.FromPinnedSpan(b);
#endif
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (System.Collections.Generic.IDictionary<int,string> Dict,IDisposable Store) CreateMemory(){
        var store=HashStore.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (System.Collections.Generic.IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice){
        var store=HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
    protected override TryAddResult TryAdd(System.Collections.Generic.IDictionary<int,string> dict,int key,string value) =>
        ((global::Tsavorite.Collections.IDictionary<int,string>)dict).TryAdd(key,value);
    protected override void DoUpsert(){
        using var store=HashStore.Create().Create<int,string>();
#if TSAVORITE2_0BETA3
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
#elif TSAVORITE1_1_1
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Tsavorite.SessionFunctions.Upsert>(new());
#endif
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)��closing}��ʂ�
        {
#if TSAVORITE2_0BETA3
            var status=bc.Upsert(key,SerStr("new"));
#elif TSAVORITE1_1_1
            var val=MakeKey(SerStr("new"));
            var status=bc.Upsert(ref key,ref val);
#endif
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
    protected override void DoInPlaceWriter(){
        using var store=HashStore.Create().Create<int,string>();
#if TSAVORITE2_0BETA3
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
#elif TSAVORITE1_1_1
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Tsavorite.SessionFunctions.Upsert>(default);
#endif
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)��closing}��ʂ�
        {
#if TSAVORITE2_0BETA3
            var status=bc.Upsert(key,SerStr("new"));
#elif TSAVORITE1_1_1
            var val=MakeKey(SerStr("new"));
            var status=bc.Upsert(ref key,ref val);
#endif
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        //InPlaceWriter(ReadOnlySpan<byte>)��closing}��ʂ�
        {
#if TSAVORITE2_0BETA3
            var status=bc.Upsert(key,SerStr("updated"));
#elif TSAVORITE1_1_1
            var val=MakeKey(SerStr("updated"));
            var status=bc.Upsert(ref key,ref val);
#endif
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
#if TSAVORITE2_0BETA3
    private sealed class DummyHeapObject:IHeapObject {
        public static int MaxSerializedObjectSize=>0;
        public long HeapMemorySize=>0;
        public IHeapObject Clone()=>this;
        public void Serialize(BinaryWriter writer){ }
        public void WriteType(BinaryWriter writer,bool includeNamespace){ }
        public void DoSerialize(BinaryWriter writer){ }
        public void CacheSerializedObjectData(ref LogRecord dstLogRecord,ref RMWInfo rmwInfo,bool srcIsOnMemoryLog){ }
        public void ClearSerializedObjectData(){ }
        public void Dispose(){ }
    }
    [Fact, Trait("Category","Unit")]
    public void UpsertHeapObject(){
        using var store=HashStore.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(500));
        // �V�K�L�[ �� GetUpsertFieldInfo(IHeapObject), InitialWriter(IHeapObject)
        // SpanByteAllocator�ł�InitialWriter(IHeapObject)��false��Ԃ�����PostInitialWriter(IHeapObject)�͌Ă΂�Ȃ�
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
        // PostUpsertOperation(IHeapObject) �� finally�u���b�N�ŏ�ɌĂ΂��
        bc.Upsert(key,(IHeapObject)new DummyHeapObject());
        // �X�p���l�Ŏ��O�o�^��IHeapObject��Upsert �� �������R�[�h������InPlaceWriter(IHeapObject)���Ă΂��
        // SpanByteAllocator��ValueIsObject�r�b�g��N���A���邽��InPlaceWriter��false��Ԃ�
        var key2=MakeKey(SerInt(502));
        bc.Upsert(key2,SerStr("seed"));
        bc.Upsert(key2,(IHeapObject)new DummyHeapObject());
        // PostInitialWriter(IHeapObject)��SpanByteAllocator�ł͓��B���Ȃ����ߒ��ڌĂяo���ibody����Ȃ̂�default�����ň��S�j
        {
            var f=new global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>();
            var lr=default(LogRecord);
            var si=default(RecordSizeInfo);
            string inp=null!;
            var outp=false;
            var ui=default(UpsertInfo);
#pragma warning disable CS8601
            f.PostInitialWriter(ref lr,in si,ref inp,(IHeapObject)new DummyHeapObject(),ref outp,ref ui);
#pragma warning restore CS8601
        }
    }
#endif
#if TSAVORITE2_0BETA3
    [Fact,Trait("Category","Unit")]public void GetUpsertFieldInfo_TKey_IHeapObject_PInnedSpanByte()=> this.UpsertHeapObject();
    [Fact,Trait("Category","Unit")]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=> this.UpsertHeapObject();
    [Fact,Trait("Category","Unit")]public void PostUpsertOperation_TKey_PinnedSpanByte_IHeapObject_UpsertInfo_TEpochAccessor()=> this.UpsertHeapObject();
    [Fact,Trait("Category","Unit")]public void InPlaceWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=> this.UpsertHeapObject();
    [Fact,Trait("Category","Unit")]public void PostInitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_IHeapObject_SpanByteAndMemory_UpsertInfo()=> this.UpsertHeapObject();

    private sealed class ScanAndUpsertFunctions(
        ClientSession<
            FixedSpanByteKey,
            string,//TInput
            bool,
            EqualityEmpty,
            global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>,
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
        public bool OnStart(long beginAddress,long endAddress)=>true;
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord:ISourceLogRecord {
            cursorRecordResult=CursorRecordResult.Accept;
            var bc=session.BasicContext;
            var input=default(string)!;
            var output=false;
            var options=default(UpsertOptions);
            if(externalKeyBytes is not null){
                var extKey=MakeKey(externalKeyBytes);
                bc.Upsert(extKey,ref input,in logRecord,ref output,ref options);
            } else {
                bc.Upsert(ref input,in logRecord,ref output,ref options);
            }
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords){ }
        public void OnException(Exception exception,long numberOfRecords){ }
    }

    [Fact, Trait("Category","Unit")]
    public void TSourceLogRecord(){
        using var store=HashStore.Create().Create<int,string>();
        using var session=store.Store.NewSession<
            FixedSpanByteKey,
            string,//TInput
            bool,
            EqualityEmpty,
            global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>
        >(new());
        var addr1=store.Store.Log.TailAddress;
        session.BasicContext.Upsert(MakeKey(SerInt(600)),SerStr("log"));
        var addr2=store.Store.Log.TailAddress;
        // �V�K�L�[(9999) �� GetUpsertFieldInfo<TKey,TSourceLogRecord>, InitialWriter<TSourceLogRecord>, PostInitialWriter<TSourceLogRecord>
        var initFuncs=new ScanAndUpsertFunctions(session,SerInt(9999));
        store.Store.Log.Scan(ref initFuncs,addr1,addr2,DiskScanBufferingMode.NoBuffering);
        // �����L�[(600) �� GetUpsertFieldInfo<TSourceLogRecord,TSourceLogRecord>, InPlaceWriter<TSourceLogRecord>
        var inplaceFuncs=new ScanAndUpsertFunctions(session,null);
        store.Store.Log.Scan(ref inplaceFuncs,addr1,addr2,DiskScanBufferingMode.NoBuffering);
    }
    [Fact,Trait("Category","Unit")]public void GetUpsertFieldInfo_TKey_TSourceLogRecord_PinnedSpanByte()=> this.TSourceLogRecord();
    [Fact,Trait("Category","Unit")]public void InitialWriter_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo()=> this.TSourceLogRecord();
    [Fact,Trait("Category","Unit")]public void InPlaceWrit2er_LogRecord_RecordSizeInfo_PinnedSpanByte_TSourceLogRecord_SpanByteAndMemory_UpsertInfo()=> this.TSourceLogRecord();
#endif

    protected override void DoRMW(){
        using var store=HashStore.Create().Create<int,string>();
        var key=MakeKey(SerInt(1));
#if TSAVORITE2_0BETA3
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
        var input0="v1";
        var input1="v2";
#elif TSAVORITE1_1_1
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Tsavorite.SessionFunctions.Upsert>(default);
        var input0=SpanByte.FromPinnedSpan(SerStr("v1"));
        var input1=SpanByte.FromPinnedSpan(SerStr("v2"));
#endif
        var bc=session.BasicContext;
        {
            var output=false;
#if TSAVORITE2_0BETA3
            var status=bc.RMW(key,ref input0,ref output);
#elif TSAVORITE1_1_1
            var status=bc.RMW(ref key,ref input0,ref output);
#endif
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            var output=false;
#if TSAVORITE2_0BETA3
            var status=bc.RMW(key,ref input1,ref output);
#elif TSAVORITE1_1_1
            var status=bc.RMW(ref key,ref input1,ref output);
#endif
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void RMWCompletionCallback_Managed(){
        var folder= this.CreateDiskFolder();
        using var LogDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store=HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict=store.Create();
        dict.Add(0,"target");
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    [Fact, Trait("Category","Unit")]
    public void ReadCompletionCallback_Managed(){
        var folder= this.CreateDiskFolder();
        using var LogDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store=HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict=store.Create();
        dict[0]="target";
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.TryGetValue(0,out var v));
        Assert.Equal("target",v);
    }
    [Fact, Trait("Category","Unit")]
    public void ConvertOutputToHeap_Managed(){
        var folder= this.CreateDiskFolder();
        using var LogDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store=HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict=store.Create();
        dict.Add(0,"target");
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    protected override void DoConcurrentWriter(){
#if TSAVORITE1_1_1
        // �����l�ŏ���Upsert��A�Z���l�ō�Upsert �� src.Length<=dst.Length �� in-place �R�s�[�����ʂ�
        using var store=HashStore.Create().Create<int,string>();
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Tsavorite.SessionFunctions.Upsert>(default);
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(300));
        var valLong=MakeKey(SerStr("hello_world_test"));
        bc.Upsert(ref key,ref valLong);
        var valShort=MakeKey(SerStr("hi"));
        var status=bc.Upsert(ref key,ref valShort);
        if(status.IsPending){
            bc.CompletePendingWithOutputs(out var c,wait: true);
            c.Dispose();
        }
#endif
    }
    protected override void DoSingleReader(){
#if TSAVORITE1_1_1
        // ShiftReadOnlyAddress �̓y�[�W�t���b�V���𔺂����� ManagedLocalStorageDevice ���K�v
        var folder=CreateDiskFolder();
        using var LogDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store=HashStore.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Tsavorite.SessionFunctions.Upsert>(default);
        var bc=session.BasicContext;
        var key1=MakeKey(SerInt(400));
        var val1=MakeKey(SerStr("single"));
        bc.Upsert(ref key1,ref val1);
        // ReadOnlyAddress ��TailAddress�܂Ői�߂� �� ���������R�[�h�� in-memory read-only �̈�� �� SingleReader ���Ă΂��
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);
        var status1=bc.Read(key1,out _);
        if(status1.IsPending){
            bc.CompletePendingWithOutputs(out var c1,wait: true);
            c1.Dispose();
        }
        Assert.True(status1.Found);
        // IsEmpty ����: ��l�� read-only �̈�ɒu���� Read �� Span.IsEmpty=true �p�X��ʂ�
        var key2=MakeKey(SerInt(401));
        var empty=default(SpanByte);
        bc.Upsert(ref key2,ref empty);
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);
        var status2=bc.Read(key2,out _);
        if(status2.IsPending){
            bc.CompletePendingWithOutputs(out var c2,wait: true);
            c2.Dispose();
        }
        Assert.True(status2.Found);
#endif
    }
    protected override void DoConcurrentReaderEmpty(){
#if TSAVORITE1_1_1
        // mutable �̈�ɋ�l��u���� Read �� ConcurrentReader �� Span.IsEmpty=true �����ʂ�
        using var store=HashStore.Create().Create<int,string>();
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Tsavorite.SessionFunctions.Upsert>(default);
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(500));
        var empty=default(SpanByte);
        bc.Upsert(ref key,ref empty);
        var status=bc.Read(key,out _);
        Assert.True(status.Found);
#endif
    }
}
