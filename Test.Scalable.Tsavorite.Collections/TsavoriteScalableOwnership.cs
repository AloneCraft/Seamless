using System.Collections.Concurrent;
using Shared.Scalable.Tsavorite.Collections;
using Seamless.Execution;
using Tsavorite.core;

namespace Test.Scalable.Tsavorite.Collections;

public sealed class TsavoriteScalableOwnership{
    private const int Count=128;
    private const long Index=64;
    private const long Page=4096;
    private const long Memory=131072;

    [Fact,Trait("Category","Unit")]
    public void Add(){
        TrackedSetStrategy.Created.Clear();
        TrackedDictionaryStrategy.Created.Clear();
        using var setBenchmark=new SetBenchmark();
        using var dictionaryBenchmark=new DictionaryBenchmark();
        Assert.Equal(Count,setBenchmark.Add(Count,Index,Page,Page,Memory,1.0));
        Assert.Equal(Count,dictionaryBenchmark.Add(Count,Index,Page,Page,Memory,1.0));
        var set=Assert.Single(TrackedSetStrategy.Created);
        var dictionary=Assert.Single(TrackedDictionaryStrategy.Created);
        Assert.True(set.Handle.Set.IsDisposed);
        Assert.Throws<ObjectDisposedException>(()=>dictionary.Handle.NewDict());
        VerifySettings(set.Tuning,set.Limits);
        VerifySettings(dictionary.Tuning,dictionary.Limits);
    }

    [Fact,Trait("Category","Unit")]
    public void Contains(){
        TrackedSetStrategy.Created.Clear();
        var benchmark=new SetBenchmark();
        try{
            RunThreads(()=>{
                Assert.Equal(Count,benchmark.Contains(Count,Index,Page,Page,Memory,1.0));
                Assert.Equal(Count,benchmark.Contains(Count,Index,Page,Page,Memory,1.0));
            });
            var created=TrackedSetStrategy.Created.ToArray();
            Assert.Equal(2,created.Length);
            Assert.NotEqual(created[0].ThreadId,created[1].ThreadId);
            Assert.NotSame(created[0].Handle.Set,created[1].Handle.Set);
            Assert.All(created,item=>{
                Assert.False(item.Handle.Set.IsDisposed);
                VerifySettings(item.Tuning,item.Limits);
            });
            // 操作スレッドの終了後も値を追跡し、所有インスタンスが別スレッドからすべて破棄する。
            benchmark.Dispose();
            Assert.All(created,item=>Assert.True(item.Handle.Set.IsDisposed));
            Assert.Throws<ObjectDisposedException>(()=>benchmark.Contains(Count,Index,Page,Page,Memory,1.0));
        }finally{
            benchmark.Dispose();
        }
    }

    [Fact,Trait("Category","Unit")]
    public void TryGetValue(){
        TrackedDictionaryStrategy.Created.Clear();
        var benchmark=new DictionaryBenchmark();
        try{
            RunThreads(()=>{
                Assert.Equal(Count,benchmark.TryGetValue(Count,Index,Page,Page,Memory,1.0));
                Assert.Equal(Count,benchmark.TryGetValue(Count,Index,Page,Page,Memory,1.0));
            });
            var created=TrackedDictionaryStrategy.Created.ToArray();
            Assert.Equal(2,created.Length);
            Assert.NotEqual(created[0].ThreadId,created[1].ThreadId);
            Assert.NotSame(created[0].Handle,created[1].Handle);
            Assert.All(created,item=>VerifySettings(item.Tuning,item.Limits));
            benchmark.Dispose();
            Assert.All(created,item=>Assert.Throws<ObjectDisposedException>(()=>item.Handle.NewDict()));
            Assert.Throws<ObjectDisposedException>(()=>benchmark.TryGetValue(Count,Index,Page,Page,Memory,1.0));
        }finally{
            benchmark.Dispose();
        }
    }

    private static void VerifySettings(TsavoriteTuning tuning,StoreLimits limits){
        Assert.Equal(Index,tuning.IndexSize);
        Assert.Equal(Page,tuning.PageSize);
        Assert.Equal(Page,tuning.SegmentSize);
        Assert.Equal(Memory,tuning.LogMemorySize);
        Assert.Equal(1.0,tuning.MutableFraction);
        Assert.False(tuning.AutoGrowIndex);
        Assert.Null(tuning.Storage);
        Assert.Equal(checked(Index+Memory+2*Page),limits.MemoryBytes);
    }

    private static void RunThreads(Action action){
        using var barrier=new Barrier(2);
        var errors=new ConcurrentQueue<Exception>();
        var threads=Enumerable.Range(0,2).Select(_=>new Thread(()=>{
            try{
                Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                action();
            }catch(Exception error){
                errors.Enqueue(error);
            }
        }){IsBackground=true}).ToArray();
        foreach(var thread in threads){
            thread.Start();
        }
        foreach(var thread in threads){
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)),"The collection operation did not finish.");
        }
        Assert.Empty(errors);
    }

    private static TunedHashSetHandle<int> CreateSet<TStrategy>(TsavoriteTuning tuning,StoreLimits limits)
        where TStrategy:IHashSetTestStrategy<int>
        =>TStrategy.CreateTunedSet(tuning,limits);
    private static TunedDictionaryHandle<int,int> CreateDictionary<TStrategy>(TsavoriteTuning tuning,StoreLimits limits)
        where TStrategy:IDictTestStrategy<int,int>
        =>TStrategy.CreateTunedStore(tuning,limits);

    private sealed class SetBenchmark:TsavoriteSetScalable<TrackedSetStrategy,int>;
    private sealed class DictionaryBenchmark:TsavoriteDictionaryScalable<TrackedDictionaryStrategy,int,int>;

    private readonly struct TrackedSetStrategy:IHashSetTestStrategy<int>{
        public static readonly ConcurrentQueue<(int ThreadId,TunedHashSetHandle<int> Handle,TsavoriteTuning Tuning,StoreLimits Limits)> Created=new();
        public static int Elem(int i)=>i;
        public static TunedHashSetHandle<int> CreateTunedSet(TsavoriteTuning tuning,StoreLimits limits){
            var handle=CreateSet<BlittableHashSetStrategy<Int32HashSetValueProvider,int>>(tuning,limits);
            Created.Enqueue((Environment.CurrentManagedThreadId,handle,tuning,limits));
            return handle;
        }
        public static HashSetHandle<int> CreateMemorySet(long memorySize,double mutableFraction,long indexSize,long pageSize,long segmentSize)=>throw new NotSupportedException("The legacy factory must remain unused by Scalable.");
    }

    private readonly struct TrackedDictionaryStrategy:IDictTestStrategy<int,int>{
        public static readonly ConcurrentQueue<(int ThreadId,TunedDictionaryHandle<int,int> Handle,TsavoriteTuning Tuning,StoreLimits Limits)> Created=new();
        public static int K(int i)=>i;
        public static int V(int i)=>i+1;
        public static int VWrong()=>-1;
        public static TunedDictionaryHandle<int,int> CreateTunedStore(TsavoriteTuning tuning,StoreLimits limits){
            var handle=CreateDictionary<UnmanagedStoreDictionaryStrategy<IntDictTypeProvider,IntDictTypeProvider,int,int>>(tuning,limits);
            Created.Enqueue((Environment.CurrentManagedThreadId,handle,tuning,limits));
            return handle;
        }
        public static StoreHandle<int,int> CreateMemoryStore(long memorySize,double mutableFraction,long indexSize,long pageSize,long segmentSize)=>throw new NotSupportedException("The legacy factory must remain unused by Scalable.");
        public static StoreHandle<int,int> CreateStorageStore(IDevice logDevice,IDevice objDevice,long memorySize,double mutableFraction,string? checkpointDir,bool recoverDevice)=>throw new NotSupportedException();
    }
}
