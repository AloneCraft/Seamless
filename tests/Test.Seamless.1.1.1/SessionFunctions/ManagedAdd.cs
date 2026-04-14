using MemoryPack;
using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
using ManagedSessionValue=Tsavorite.core.SpanByte;

namespace Test.Seamless.SessionFunctions;

public sealed class ManagedAdd:AddTests<int,string> {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }

    private static byte[] SerInt(int value) => MemoryPackSerializer.Serialize(value);
    private static byte[] SerStr(string value) => MemoryPackSerializer.Serialize(value);
    private static SpanByte MakeKey(byte[] bytes) => SpanByte.FromPinnedSpan(bytes);

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
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(new());

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(200));
        var value=MakeKey(SerStr("new"));
        var status=basicContext.Upsert(ref key,ref value);

        if(status.IsPending) {
            basicContext.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
    }

    protected override void DoInPlaceWriter() {
        using var store=Factory.Create().Create<int,string>();
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(200));

        var firstValue=MakeKey(SerStr("new"));
        var firstStatus=basicContext.Upsert(ref key,ref firstValue);
        if(firstStatus.IsPending) {
            basicContext.CompletePendingWithOutputs(out var firstCompleted,wait: true);
            firstCompleted.Dispose();
        }

        var secondValue=MakeKey(SerStr("updated"));
        var secondStatus=basicContext.Upsert(ref key,ref secondValue);
        if(secondStatus.IsPending) {
            basicContext.CompletePendingWithOutputs(out var secondCompleted,wait: true);
            secondCompleted.Dispose();
        }
    }

    protected override void DoRMW() {
        using var store=Factory.Create().Create<int,string>();

        var key=MakeKey(SerInt(1));
        var input0=SpanByte.FromPinnedSpan(SerStr("v1"));
        var input1=SpanByte.FromPinnedSpan(SerStr("v2"));

        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);
        var basicContext=session.BasicContext;

        {
            var output=false;
            var status=basicContext.RMW(ref key,ref input0,ref output);
            if(status.IsPending) {
                basicContext.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }

        {
            var output=false;
            var status=basicContext.RMW(ref key,ref input1,ref output);
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
        using var session=store.Store.NewSession<SpanByte,bool,EqualityEmpty,global::Seamless.SessionFunctions.ManagedAdd>(default);

        var basicContext=session.BasicContext;
        var key=MakeKey(SerInt(300));
        var longValue=MakeKey(SerStr("hello_world_test"));
        basicContext.Upsert(ref key,ref longValue);

        var shortValue=MakeKey(SerStr("hi"));
        var status=basicContext.Upsert(ref key,ref shortValue);
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
        var collectionStore=(ManagedCollectionStore<int,string>)store;

        dict[400]="single";
        collectionStore.Store.Log.ShiftReadOnlyAddress(collectionStore.Store.Log.TailAddress,wait: true);

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
    public void SingleReader_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var input=default(ManagedSessionValue);
        var value=default(ManagedSessionValue);
        var output=false;
        var readInfo=default(ReadInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.SingleReader(ref key,ref input,ref value,ref output,ref readInfo);
        });
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void ConcurrentReader_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var input=default(ManagedSessionValue);
        var value=default(ManagedSessionValue);
        var output=false;
        var readInfo=default(ReadInfo);
        var recordInfo=default(RecordInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.ConcurrentReader(ref key,ref input,ref value,ref output,ref readInfo,ref recordInfo);
        });
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void ReadCompletionCallback_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var input=default(ManagedSessionValue);
        var output=false;
        var status=default(Status);
        var recordMetadata=default(RecordMetadata);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.ReadCompletionCallback(ref key,ref input,ref output,default,status,recordMetadata);
        });
        Assert.Equal("ManagedAddクラスのRMWでのみ使われる",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void CopyUpdater_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var input=default(ManagedSessionValue);
        var oldValue=default(ManagedSessionValue);
        var newValue=default(ManagedSessionValue);
        var output=false;
        var rmwInfo=default(RMWInfo);
        var recordInfo=default(RecordInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.CopyUpdater(ref key,ref input,ref oldValue,ref newValue,ref output,ref rmwInfo,ref recordInfo);
        });
        Assert.Equal("RMWで更新しない限りない",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void PostCopyUpdater_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var input=default(ManagedSessionValue);
        var oldValue=default(ManagedSessionValue);
        var newValue=default(ManagedSessionValue);
        var output=false;
        var rmwInfo=default(RMWInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.PostCopyUpdater(ref key,ref input,ref oldValue,ref newValue,ref output,ref rmwInfo);
        });
        Assert.Equal("RMWで更新しない限りない",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void GetRMWModifiedValueLength_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var value=default(ManagedSessionValue);
        var input=default(ManagedSessionValue);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.GetRMWModifiedValueLength(ref value,ref input);
        });
        Assert.Equal("NeedCopyUpdate() が常に false 計上デッドコード",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void SingleDeleter_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var value=default(ManagedSessionValue);
        var deleteInfo=default(DeleteInfo);
        var recordInfo=default(RecordInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.SingleDeleter(ref key,ref value,ref deleteInfo,ref recordInfo);
        });
        Assert.Equal("AddはUpsert,RMW",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void PostSingleDeleter_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var deleteInfo=default(DeleteInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.PostSingleDeleter(ref key,ref deleteInfo);
        });
        Assert.Equal("AddはUpsert,RMW",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void ConcurrentDeleter_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var value=default(ManagedSessionValue);
        var deleteInfo=default(DeleteInfo);
        var recordInfo=default(RecordInfo);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.ConcurrentDeleter(ref key,ref value,ref deleteInfo,ref recordInfo);
        });
        Assert.Equal("AddはUpsert,RMW",exception.Message);
    }

    [Fact, Trait("Category","Unit")]
    public void PostDeleteOperation_直接呼び出しは例外() {
        var functions=default(global::Seamless.SessionFunctions.ManagedAdd);
        var key=default(ManagedSessionValue);
        var deleteInfo=default(DeleteInfo);
        var epochAccessor=default(DummyEpochAccessor);

        var exception=Assert.Throws<NotSupportedException>(() => {
            functions.PostDeleteOperation(ref key,ref deleteInfo,epochAccessor);
        });
        Assert.Equal("AddはUpsert,RMW",exception.Message);
    }
}