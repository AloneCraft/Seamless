using Garnet.common;

using MemoryPack;

using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Seamless.SessionFunctions;


public sealed class Upsert:AddTests<int,string> {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume(){ }
        public bool TrySuspend() => true;
    }
    private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
    private static FixedSpanByteKey MakeKey(byte[] b)=>(FixedSpanByteKey)(ReadOnlySpan<byte>)b;
    protected override int Key(int seed) => seed;
    protected override string Value(int seed) => $"v{seed}";
    protected override (global::System.Collections.Generic.IDictionary<int,string> Dict,IDisposable Store) CreateMemory(){
        var store=Store.Create().Create<int,string>();
        return (store.Create(),store);
    }
    protected override (global::System.Collections.Generic.IDictionary<int,string> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice){
        var store=Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store) =>
        ((CollectionStore<int,string>)store).Store.Log.FlushAndEvict(true);
    protected override TryAddResult TryAdd(global::System.Collections.Generic.IDictionary<int,string> dict,int key,string value) =>
        ((global::Seamless.Tsavorite.Collections.IDictionary<int,string>)dict).TryAdd(key,value);
    protected override void DoUpsert(){
        using var store=Store.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)��closing}��ʂ�
        {
            var status=bc.Upsert(key,SerStr("new"));
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
    protected override void DoInPlaceWriter(){
        using var store=Store.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
        var bc=session.BasicContext;
        var key=MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)��closing}��ʂ�
        {
            var status=bc.Upsert(key,SerStr("new"));
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        //InPlaceWriter(ReadOnlySpan<byte>)��closing}��ʂ�
        {
            var status=bc.Upsert(key,SerStr("updated"));
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
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
        using var store=Store.Create().Create<int,string>();
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
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
            var f=new global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>();
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
    private sealed class ScanAndUpsertFunctions(
        ClientSession<
            FixedSpanByteKey,
            string,//TInput
            bool,
            EqualityEmpty,
            global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>,
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
        using var store=Store.Create().Create<int,string>();
        using var session=store.Store.NewSession<
            FixedSpanByteKey,
            string,//TInput
            bool,
            EqualityEmpty,
            global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>
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

    protected override void DoRMW(){
        using var store=Store.Create().Create<int,string>();
        var key=MakeKey(SerInt(1));
        using var session=store.Store.NewSession<FixedSpanByteKey, string, bool, EqualityEmpty, global::Seamless.Tsavorite.SessionFunctions.Upsert<string,MemoryPackSerializer<string>>>(new());
        var input0="v1";
        var input1="v2";
        var bc=session.BasicContext;
        {
            var output=false;
            var status=bc.RMW(key,ref input0,ref output);
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            var output=false;
            var status=bc.RMW(key,ref input1,ref output);
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
    public void TryAddAfterFlushReportsExisting_Managed(){
        var folder= this.CreateDiskFolder();
        using var LogDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var ObjDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var store=Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict=store.Create();
        dict.Add(0,"target");
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    [Fact, Trait("Category","Unit")]
    public void ReadAfterFlush_Managed(){
        var folder= this.CreateDiskFolder();
        using var LogDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var ObjDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
        using var store=Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity).Create<int,string>(LogDevice,ObjDevice);
        using var dict=store.Create();
        dict[0]="target";
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.TryGetValue(0,out var v));
        Assert.Equal("target",v);
    }
    protected override void DoConcurrentWriter(){
    }
    protected override void DoSingleReader(){
    }
    protected override void DoConcurrentReaderEmpty(){
    }
}
