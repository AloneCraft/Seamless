using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>共有の読み取りセッションを排他し、複数スレッドの走査結果を保つ。</summary>
[global::Seamless.FLinq.LinqIntercept(false)]
public class ConcurrentScan{
    [Fact,Trait("Category","Unit")]public void SingleThread_NeverThrows(){
        //1 つのスレッドが順に何度走査しても、入れ子の走査(同じスレッド)でも例外にならない
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        using var set=data.Create();
        for(long i=0;i<2000;i++){
            set.Add(i);
        }
        for(var round=0;round<5;round++){
            var count=0;
            foreach(var outer in set){
                if(count++==0){
                    foreach(var inner in set){
                    }
                }
            }
            Assert.Equal(2000,count);
        }
    }
    [Fact,Trait("Category","Unit")]public void MultipleThreads_KeepIndependentScans(){
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        using var set=data.Create();
        for(long i=0;i<20_000;i++){
            set.Add(i);
        }
        var errors=new global::System.Collections.Concurrent.ConcurrentBag<Exception>();
        using var start=new Barrier(4);
        var threads=Enumerable.Range(0,4).Select(_=>new Thread(()=>{
            try{
                start.SignalAndWait();
                for(var round=0;round<5;round++){
                    var seen=new System.Collections.Generic.HashSet<long>();
                    foreach(var item in set){
                        Assert.InRange(item,0,19_999);
                        Assert.True(seen.Add(item),$"重複した要素 {item}");
                    }
                    Assert.Equal(20_000,seen.Count);
                }
            }catch(Exception error){
                errors.Add(error);
            }
        })).ToList();
        threads.ForEach(thread=>thread.Start());
        threads.ForEach(thread=>thread.Join());
        Assert.Empty(errors);
    }
}
