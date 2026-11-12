using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Stores;
/// <summary>シングル用のコレクションを複数のスレッドが同時に走査すると、Debug ビルドでは例外になる(BUG-0016。Release では検出しない)。</summary>
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
#if DEBUG
    [Fact,Trait("Category","Unit")]public void MultipleThreads_ThrowsInDebug(){
        //複数のスレッドが同時に走査すると、黙って誤った結果を返さず、分かりやすい例外にする
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        using var set=data.Create();
        for(long i=0;i<20_000;i++){
            set.Add(i);
        }
        var errors=new global::System.Collections.Concurrent.ConcurrentBag<Exception>();
        var stop=DateTime.UtcNow.AddSeconds(10);
        var threads=Enumerable.Range(0,4).Select(_=>new Thread(()=>{
            try{
                while(DateTime.UtcNow<stop&&errors.IsEmpty){
                    foreach(var item in set){
                    }
                }
            }catch(Exception error){
                errors.Add(error);
            }
        })).ToList();
        threads.ForEach(thread=>thread.Start());
        threads.ForEach(thread=>thread.Join());
        Assert.NotEmpty(errors);
        Assert.All(errors,error=>Assert.IsType<InvalidOperationException>(error));
    }
#endif
}
