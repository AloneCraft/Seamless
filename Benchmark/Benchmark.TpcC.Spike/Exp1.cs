using System.Collections.Concurrent;
using System.Diagnostics;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// 実験 1: P 個の区画(別々の共通ストア、各 18 個のデータ本体)へ P 本のスレッドから同時に書き込み、同時に保存する。
/// --save-by owner : 書き込むスレッド自身が、間隔ごとに自分の区画を保存する(他の区画は書き込み中)。
/// --save-by other : 別のスレッドが区画を順に押さえて保存する(保存するスレッドと書き込むスレッドが違う)。
/// --save-by all   : 区画ごとの保存スレッドが、足並みをそろえて一斉に保存する(Store.Save が P 個同時に走る)。
/// 終わったら開き直し(並列)、最後の保存と途中の保存の両方を、模型の要約と照合する。
/// </summary>
public static class Exp1{
    public static int Run(Args options){
        var count=options.Int("partitions",4);
        var seconds=options.Double("seconds",20);
        var saveBy=options.Str("save-by","owner");
        var interval=TimeSpan.FromMilliseconds(options.Int("save-interval-ms",500));
        var release=options.Bool("release",true);
        var scale=Scale.Parse(options.Str("scale","tiny"));
        var seed=options.Int("seed",1);
        var root=Report.NewRoot(options,"exp1");
        var partitionOptions=new PartitionOptions{Seed=seed,SmallMemory=options.Bool("small-memory",false),MemoryBytes=options.Long("memory-mb",512)*1024*1024};
        Console.WriteLine($"root={root} partitions={count} scale={scale} save-by={saveBy} interval={interval.TotalMilliseconds}ms release={release} seconds={seconds}");
        var partitions=new Partition[count];
        Overlap.Reset();
        var loadWatch=Stopwatch.StartNew();
        // 作成と初期投入も区画ごとのスレッドで同時に行う(同時の書き込みの一部)。
        RunThreads(count,index=>{
            partitions[index]=new Partition(index,Path.Combine(root,"p"+index),scale,partitionOptions);
            partitions[index].Load();
        });
        Console.WriteLine($"created+loaded {count} partitions in {Report.Ms(loadWatch.Elapsed)}ms (LightEpoch active={Tsavorite.core.LightEpoch.ActiveInstanceCount()})");
        var saves=new List<SaveRecord>[count];
        for(var index=0;index<count;index++){
            saves[index]=[partitions[index].Save(release)];
        }
        var errors=new ConcurrentQueue<string>();
        var stop=0;
        var saveTimes=new ConcurrentBag<TimeSpan>();
        var threads=new List<Thread>();
        for(var index=0;index<count;index++){
            var partition=partitions[index];
            var mine=saves[index];
            threads.Add(new Thread(()=>{
                var last=Stopwatch.StartNew();
                try{
                    while(Volatile.Read(ref stop)==0){
                        partition.Gate.Wait();
                        try{
                            partition.RunTransaction();
                            if(saveBy=="owner"&&last.Elapsed>=interval){
                                var record=partition.Save(release);
                                lock(mine){
                                    mine.Add(record);
                                }
                                saveTimes.Add(record.Elapsed);
                                last.Restart();
                            }
                        }finally{
                            partition.Gate.Release();
                        }
                    }
                }catch(Exception error){
                    errors.Enqueue($"writer p{partition.Id}: {error}");
                    Volatile.Write(ref stop,1);
                }
            }){Name="writer"+index});
        }
        if(saveBy=="other"){
            threads.Add(new Thread(()=>{
                var next=0;
                try{
                    while(Volatile.Read(ref stop)==0){
                        Thread.Sleep(interval/count);
                        var partition=partitions[next];
                        partition.Gate.Wait();
                        try{
                            var record=partition.Save(release);
                            lock(saves[next]){
                                saves[next].Add(record);
                            }
                            saveTimes.Add(record.Elapsed);
                        }finally{
                            partition.Gate.Release();
                        }
                        next=(next+1)%count;
                    }
                }catch(Exception error){
                    errors.Enqueue($"saver: {error}");
                    Volatile.Write(ref stop,1);
                }
            }){Name="saver"});
        }else if(saveBy=="all"){
            var barrier=new Barrier(count);
            for(var index=0;index<count;index++){
                var partition=partitions[index];
                var mine=saves[index];
                threads.Add(new Thread(()=>{
                    try{
                        while(true){
                            Thread.Sleep(interval);
                            // 全員がそろってから一斉に保存する。止めるときも全員が同じ回で抜ける。
                            barrier.SignalAndWait();
                            if(Volatile.Read(ref stop)!=0){
                                break;
                            }
                            partition.Gate.Wait();
                            try{
                                var record=partition.Save(release);
                                lock(mine){
                                    mine.Add(record);
                                }
                                saveTimes.Add(record.Elapsed);
                            }finally{
                                partition.Gate.Release();
                            }
                            barrier.SignalAndWait();
                        }
                    }catch(Exception error){
                        errors.Enqueue($"saver p{partition.Id}: {error}");
                        Volatile.Write(ref stop,1);
                        barrier.RemoveParticipant();
                    }
                }){Name="saver"+index});
            }
        }else if(saveBy!="owner"){
            throw new ArgumentException("--save-by must be owner, other or all");
        }
        var run=Stopwatch.StartNew();
        foreach(var thread in threads){
            thread.Start();
        }
        while(run.Elapsed.TotalSeconds<seconds&&Volatile.Read(ref stop)==0){
            Thread.Sleep(100);
        }
        Volatile.Write(ref stop,1);
        foreach(var thread in threads){
            thread.Join();
        }
        var elapsed=run.Elapsed;
        long total=0;
        for(var index=0;index<count;index++){
            var partition=partitions[index];
            total+=partition.Transactions;
            if(errors.IsEmpty){
                saves[index].Add(partition.Save(release));
                var problem=partition.Verify(saves[index][^1].Expected!);
                if(problem is not null){
                    errors.Enqueue("before reopen: "+problem);
                }
            }
            Console.WriteLine($"p{index}: txns={partition.Transactions} saves={saves[index].Count} serial={partition.Serial}");
        }
        Console.WriteLine("overlap: "+Overlap.Summary);
        Console.WriteLine($"run {Report.Ms(elapsed)}ms total txns={total} ({total/elapsed.TotalSeconds:0} txn/s) save times {Report.Stats(saveTimes)}");
        foreach(var partition in partitions){
            partition.Dispose();
        }
        if(errors.IsEmpty){
            Exp4.ReopenAndVerify(root,count,scale,partitionOptions,saves,errors,parallel:true);
        }
        foreach(var error in errors){
            Console.WriteLine("ERROR "+error);
        }
        if(errors.IsEmpty){
            Console.WriteLine("PASS exp1");
            return 0;
        }
        Console.WriteLine("FAIL exp1");
        return 1;
    }
    public static void RunThreads(int count,Action<int> body){
        var errors=new ConcurrentQueue<Exception>();
        var threads=new Thread[count];
        for(var index=0;index<count;index++){
            var captured=index;
            threads[index]=new Thread(()=>{
                try{
                    body(captured);
                }catch(Exception error){
                    errors.Enqueue(error);
                }
            });
            threads[index].Start();
        }
        foreach(var thread in threads){
            thread.Join();
        }
        if(!errors.IsEmpty){
            throw new AggregateException(errors);
        }
    }
}
