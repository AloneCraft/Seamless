using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
using EqualityEmpty = Seamless.Collections.EqualityEmpty;

namespace Test.Seamless.SessionFunctions;

public sealed class UnmanagedAdd:AddTests<int,long>{
    private readonly struct DummyEpochAccessor:IEpochAccessor{
        public void Resume(){}
        public bool TrySuspend()=>true;
    }

    protected override int Key(int seed)=>seed;
    protected override long Value(int seed)=>(long)seed*100L;

    protected override(IDictionary<int,long> Dict,IDisposable Store) CreateMemory(){
        var store=Factory.Create().Create<int,long>();
        return(store.Create(),store);
    }

    protected override(IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice){
        var store=Factory.Create(MemorySize:Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return(store.Create(),store);
    }

    protected override void FlushAndEvict(IDisposable store)=>
        ((UnmanagedCollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);

    protected override TryAddResult TryAdd(IDictionary<int,long> dict,int key,long value)=>
        ((ISeamlessDictionary<int,long>)dict).TryAdd(key,value);

    protected override void DoUpsert(){
        using var store=Factory.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(new());

        var basicContext=session.BasicContext;
        var key=200;
        var value=200L;
        var status=basicContext.Upsert(ref key,ref value);

        if(status.IsPending){
            basicContext.CompletePendingWithOutputs(out var completed,wait:true);
            completed.Dispose();
        }
    }

    protected override void DoInPlaceWriter(){
        using var store=Factory.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(new());

        var basicContext=session.BasicContext;
        var key=200;
        var firstValue=200L;
        var firstStatus=basicContext.Upsert(ref key,ref firstValue);
        if(firstStatus.IsPending){
            basicContext.CompletePendingWithOutputs(out var firstCompleted,wait:true);
            firstCompleted.Dispose();
        }

        var secondValue=201L;
        var secondStatus=basicContext.Upsert(ref key,ref secondValue);
        if(secondStatus.IsPending){
            basicContext.CompletePendingWithOutputs(out var secondCompleted,wait:true);
            secondCompleted.Dispose();
        }
    }

    protected override void DoRMW(){
        using var store=Factory.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(new());

        var key=1;
        var input0=100L;
        var input1=200L;
        var basicContext=session.BasicContext;

        {
            var output=false;
            var status=basicContext.RMW(ref key,ref input0,ref output);
            if(status.IsPending){
                basicContext.CompletePendingWithOutputs(out var completed,wait:true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }

        {
            var output=false;
            var status=basicContext.RMW(ref key,ref input1,ref output);
            if(status.IsPending){
                basicContext.CompletePendingWithOutputs(out var completed,wait:true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }

    protected override void DoConcurrentWriter(){
        using var store=Factory.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.UnmanagedAdd<int,long>>(new());

        var basicContext=session.BasicContext;
        var key=300;
        var firstValue=300L;
        basicContext.Upsert(ref key,ref firstValue);

        var secondValue=301L;
        var status=basicContext.Upsert(ref key,ref secondValue);
        if(status.IsPending){
            basicContext.CompletePendingWithOutputs(out var completed,wait:true);
            completed.Dispose();
        }
    }

    protected override void DoSingleReader(){
        var folder=CreateDiskFolder();
        using var logDevice=Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose:true,capacity:Const.MinObjectLogCapacity);
        using var objDevice=Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose:true,capacity:Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store=store;
        using var _dict=(IDisposable)dict;
        var collectionStore=(UnmanagedCollectionStore<int,long>)store;

        dict[400]=100L;
        collectionStore.Store.Log.ShiftReadOnlyAddress(collectionStore.Store.Log.TailAddress,wait:true);

        Assert.True(dict.TryGetValue(400,out var actual));
        Assert.Equal(100L,actual);
    }

    protected override void DoConcurrentReaderEmpty(){
        var (dict,store)=CreateMemory();
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[500]=0L;

        Assert.True(dict.TryGetValue(500,out var actual));
        Assert.Equal(0L,actual);
    }

    [Fact,Trait("Category","Unit")]
    public void 未使用ハンドラは例外を送出する(){
        var functions=default(global::Seamless.SessionFunctions.UnmanagedAdd<int,long>);
        var key=1;
        var input=2L;
        var oldValue=3L;
        var newValue=4L;
        var value=5L;
        var output=false;
        var rmwInfo=default(RMWInfo);
        var deleteInfo=default(DeleteInfo);
        var recordInfo=default(RecordInfo);
        var epochAccessor=default(DummyEpochAccessor);

        var copyUpdater=Assert.Throws<NotSupportedException>(()=>{
            functions.CopyUpdater(ref key,ref input,ref oldValue,ref newValue,ref output,ref rmwInfo,ref recordInfo);
        });
        Assert.Equal("NeedCopyUpdate が false を返すので呼ばれない",copyUpdater.Message);

        var postCopyUpdater=Assert.Throws<NotSupportedException>(()=>{
            functions.PostCopyUpdater(ref key,ref input,ref oldValue,ref newValue,ref output,ref rmwInfo);
        });
        Assert.Equal("CopyUpdater が false を返すので呼ばれない",postCopyUpdater.Message);

        var singleDeleter=Assert.Throws<NotSupportedException>(()=>{
            functions.SingleDeleter(ref key,ref value,ref deleteInfo,ref recordInfo);
        });
        Assert.Equal("Add は Delete を使用しない",singleDeleter.Message);

        var postSingleDeleter=Assert.Throws<NotSupportedException>(()=>{
            functions.PostSingleDeleter(ref key,ref deleteInfo);
        });
        Assert.Equal("Add は Delete を使用しない",postSingleDeleter.Message);

        var concurrentDeleter=Assert.Throws<NotSupportedException>(()=>{
            functions.ConcurrentDeleter(ref key,ref value,ref deleteInfo,ref recordInfo);
        });
        Assert.Equal("Add は Delete を使用しない",concurrentDeleter.Message);

        var postDeleteOperation=Assert.Throws<NotSupportedException>(()=>{
            functions.PostDeleteOperation(ref key,ref deleteInfo,epochAccessor);
        });
        Assert.Equal("Add は Delete を使用しない",postDeleteOperation.Message);
    }
}
