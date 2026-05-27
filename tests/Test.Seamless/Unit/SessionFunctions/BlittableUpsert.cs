using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Unit.SessionFunctions;

public sealed class BlittableUpsert:AddTests<int,long>{
    protected override int Key(int seed)=>seed;
    protected override long Value(int seed)=>(long)seed*100L;
    protected override(System.Collections.Generic.IDictionary<int,long> Dict,IDisposable Store) CreateMemory(){
        var store=HashStore.Create().Create<int,long>();
        return(store.Create(),store);
    }
    protected override(System.Collections.Generic.IDictionary<int,long> Dict,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice){
        var store=HashStore.Create(MemorySize:Const.MinObjectLogCapacity).Create<int,long>(logDevice,objDevice);
        return(store.Create(),store);
    }
    protected override void FlushAndEvict(IDisposable store)=>
        ((CollectionStore<int,long>)store).Store.Log.FlushAndEvict(true);
    protected override TryAddResult TryAdd(System.Collections.Generic.IDictionary<int,long> dict,int key,long value)=>
        ((global::Seamless.Collections.IDictionary<int,long>)dict).TryAdd(key,value);

    protected override void DoUpsert(){
#if TSAVORITE1_1_1
        using var store=HashStore.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.Upsert<long,BlittableSerializer<long>>>(new());
        var bc=session.BasicContext;
        var key=200;
        var val=200L;
        var status=bc.Upsert(ref key,ref val);
        if(status.IsPending){
            bc.CompletePendingWithOutputs(out var completed,wait: true);
            completed.Dispose();
        }
#endif
    }

    protected override void DoInPlaceWriter(){
#if TSAVORITE1_1_1
        using var store=HashStore.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.Upsert<long,BlittableSerializer<long>>>(new());
        var bc=session.BasicContext;
        var key=200;
        var valFirst=200L;
        var s1=bc.Upsert(ref key,ref valFirst);
        if(s1.IsPending){
            bc.CompletePendingWithOutputs(out var c1,wait: true);
            c1.Dispose();
        }
        var valSecond=201L;
        var s2=bc.Upsert(ref key,ref valSecond);
        if(s2.IsPending){
            bc.CompletePendingWithOutputs(out var c2,wait: true);
            c2.Dispose();
        }
#endif
    }

    protected override void DoRMW(){
#if TSAVORITE1_1_1
        using var store=HashStore.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.Upsert<long,BlittableSerializer<long>>>(new());
        var key=1;
        var input0=100L;
        var input1=200L;
        var bc=session.BasicContext;
        {
            bool output=default;
            var status=bc.RMW(ref key,ref input0,ref output);
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            bool output=default;
            var status=bc.RMW(ref key,ref input1,ref output);
            if(status.IsPending){
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
#endif
    }

    protected override void DoConcurrentWriter(){
#if TSAVORITE1_1_1
        using var store=HashStore.Create().Create<int,long>();
        using var session=store.Store.NewSession<long,bool,EqualityEmpty,global::Seamless.SessionFunctions.Upsert<long,BlittableSerializer<long>>>(new());
        var bc=session.BasicContext;
        var key=300;
        var valFirst=300L;
        bc.Upsert(ref key,ref valFirst);
        var valSecond=301L;
        var status=bc.Upsert(ref key,ref valSecond);
        if(status.IsPending){
            bc.CompletePendingWithOutputs(out var c,wait: true);
            c.Dispose();
        }
#endif
    }

    protected override void DoSingleReader(){
#if TSAVORITE1_1_1
        var folder=CreateDiskFolder();
        using var logDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDevice= global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        var (dict,store)=this.CreateDisk(logDevice,objDevice);
        using var _store=store;
        using var _dict=(IDisposable)dict;
        var collectionStore=(CollectionStore<int,long>)store;

        dict[400]=100L;
        collectionStore.Store.Log.ShiftReadOnlyAddress(collectionStore.Store.Log.TailAddress,wait: true);

        Assert.True(dict.TryGetValue(400,out var actual));
        Assert.Equal(100L,actual);
#endif
    }

    protected override void DoConcurrentReaderEmpty(){
#if TSAVORITE1_1_1
        var (dict,store)=this.CreateMemory();
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[500]=0L;

        Assert.True(dict.TryGetValue(500,out var actual));
        Assert.Equal(0L,actual);
#endif
    }
}
