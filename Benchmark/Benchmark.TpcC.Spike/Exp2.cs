using System.Diagnostics;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// 実験 2: 1 つの区画(18 個のデータ本体、シングル用の操作用オブジェクト)を、所有するスレッドを替えながら順に使う。
/// --mode thread : T 本の専用スレッドが SemaphoreSlim で順番を受け渡す。次の所有者は毎回、今と違うスレッドにする。
/// --mode async  : 同時に T 個の作業項目(Parallel.ForEachAsync)が 1 つの SemaphoreSlim を WaitAsync で取り合う(続きはスレッドプールのどのスレッドでも動く)。
/// --mode pool   : スレッドプールの作業項目が順に次の作業項目を別のプールスレッドで始めさせる(毎回プールスレッドが替わる)。
/// 毎回の読み取りを模型と照合する。途中で保存(開いたまま/閉じて)・全走査の照合・操作用オブジェクトを別のスレッドで閉じて開く、を挟む。
/// --small-memory でログのメモリを 4 ページにし、読み取りの多くをディスクへ行かせる(待ちの読み取りの完了を別スレッドで起こす)。
/// </summary>
public static class Exp2{
    public static int Run(Args options){
        var handoffs=options.Long("handoffs",1_000_000);
        var threadCount=options.Int("threads",4);
        var mode=options.Str("mode","thread");
        var saveEvery=options.Long("save-every",100_000);
        var verifyEvery=options.Long("verify-every",100_000);
        var reacquireEvery=options.Long("reacquire-every",50_000);
        var txnEvery=options.Long("txn-every",100);
        var scale=Scale.Parse(options.Str("scale","tiny"));
        var root=Report.NewRoot(options,"exp2");
        var partitionOptions=new PartitionOptions{Seed=options.Int("seed",1),SmallMemory=options.Bool("small-memory",true)};
        var partition=new Partition(0,Path.Combine(root,"p0"),scale,partitionOptions);
        partition.Load();
        var saves=new List<SaveRecord>{partition.Save(true)};
        Console.WriteLine($"root={root} handoffs={handoffs} threads={threadCount} mode={mode} small-memory={partitionOptions.SmallMemory}");
        var state=new HandoffState();
        // 1 回の受け渡しで行う仕事。呼ぶのは常に今の所有者だけ(順番は SemaphoreSlim が保証する)。
        void Work(){
            var me=Environment.CurrentManagedThreadId;
            if(me!=state.LastThread){
                state.Switches++;
                state.LastThread=me;
            }
            state.Threads.Add(me);
            if(state.Released){
                // 前の所有者が閉じた操作用オブジェクトを、別のスレッドで開き直す。
                partition.AcquireAll();
                state.Released=false;
            }
            var n=++state.Count;
            partition.RunSmallUnit();
            if(n%txnEvery==0){
                partition.RunTransaction();
            }
            if(n%saveEvery==0){
                // 開いたままの保存と、閉じての保存を交互に行う。
                saves.Add(partition.Save((n/saveEvery)%2==0));
            }
            if(n%verifyEvery==0){
                var expected=partition.Parts.Select(static part=>part.Expected).ToArray();
                var problem=partition.Verify(expected);
                if(problem is not null){
                    throw new SpikeFailure($"handoff {n}: {problem}");
                }
            }
            if(n%reacquireEvery==0){
                partition.ReleaseAll();
                state.Released=true;
            }
        }
        var watch=Stopwatch.StartNew();
        Exception? failure=null;
        if(mode=="thread"){
            var gates=new SemaphoreSlim[threadCount];
            for(var index=0;index<threadCount;index++){
                gates[index]=new SemaphoreSlim(0,1);
            }
            var done=false;
            var threads=new Thread[threadCount];
            for(var index=0;index<threadCount;index++){
                var self=index;
                var rng=new Random(1000+index);
                threads[index]=new Thread(()=>{
                    while(true){
                        gates[self].Wait();
                        if(done){
                            break;
                        }
                        try{
                            Work();
                        }catch(Exception error){
                            failure??=error;
                            done=true;
                        }
                        if(state.Count>=handoffs){
                            done=true;
                        }
                        if(done){
                            for(var other=0;other<threadCount;other++){
                                if(other!=self){
                                    gates[other].Release();
                                }
                            }
                            break;
                        }
                        var next=(self+1+rng.Next(threadCount-1))%threadCount;
                        gates[next].Release();
                    }
                }){Name="owner"+index};
            }
            foreach(var thread in threads){
                thread.Start();
            }
            gates[0].Release();
            foreach(var thread in threads){
                thread.Join();
            }
        }else if(mode=="async"){
            // 受付スレッドが受信箱へ振り分ける形の模擬: 同時に T 個の作業項目が区画の関所を WaitAsync で取り合い、続きはスレッドプールのどれかで動く。
            ThreadPool.GetMinThreads(out var workers,out var ports);
            ThreadPool.SetMinThreads(Math.Max(workers,threadCount+2),ports);
            var gate=new SemaphoreSlim(1,1);
            Parallel.ForEachAsync(LongRange(handoffs),new ParallelOptions{MaxDegreeOfParallelism=threadCount},async(_,cancel)=>{
                await gate.WaitAsync(cancel).ConfigureAwait(false);
                try{
                    if(failure is null){
                        Work();
                    }
                }catch(Exception error){
                    failure??=error;
                }finally{
                    gate.Release();
                }
            }).GetAwaiter().GetResult();
        }else if(mode=="pool"){
            // スレッドプールの上で受け渡す: 今の作業項目が次の作業項目をプールの全体の待ち行列へ入れ、次が別のプールスレッドで始まるまで待ってから抜ける。
            // 今のスレッドは待っているので、次は必ず別のプールスレッドで動く(どのスレッドかはプールが決める)。
            ThreadPool.GetMinThreads(out var workers,out var ports);
            ThreadPool.SetMinThreads(Math.Max(workers,threadCount+2),ports);
            var finished=new ManualResetEventSlim();
            void Step(ManualResetEventSlim? previousStarted){
                previousStarted?.Set();
                try{
                    Work();
                }catch(Exception error){
                    failure??=error;
                }
                if(failure is not null||state.Count>=handoffs){
                    finished.Set();
                    return;
                }
                var started=new ManualResetEventSlim();
                ThreadPool.UnsafeQueueUserWorkItem(static item=>item.Step(item.Started),(Step:(Action<ManualResetEventSlim?>)Step,Started:started),preferLocal:false);
                started.Wait();
                started.Dispose();
            }
            ThreadPool.UnsafeQueueUserWorkItem(static item=>item(null),(Action<ManualResetEventSlim?>)Step,preferLocal:false);
            finished.Wait();
        }else{
            throw new ArgumentException("--mode must be thread, async or pool");
        }
        var elapsed=watch.Elapsed;
        Console.WriteLine($"handoffs={state.Count} owner switches={state.Switches} distinct threads={state.Threads.Count} txns(incl. units)={partition.Transactions} saves={saves.Count} in {Report.Ms(elapsed)}ms ({state.Count/elapsed.TotalSeconds:0}/s)");
        foreach(var part in partition.Parts){
            var address=part.Address;
            var onDisk=0.0;
            if(address.Tail>address.Begin){
                onDisk=(double)(address.Head-address.Begin)/(address.Tail-address.Begin);
            }
            Console.WriteLine($"  {part.Name}: count={part.StoreCount()} log={Report.Mb(address.Tail-address.Begin)} disk-only={onDisk:P0}");
        }
        if(failure is not null){
            Console.WriteLine("ERROR "+failure);
            partition.Dispose();
            Console.WriteLine("FAIL exp2");
            return 1;
        }
        if(state.Released){
            partition.AcquireAll();
        }
        saves.Add(partition.Save(true));
        partition.Dispose();
        var errors=new System.Collections.Concurrent.ConcurrentQueue<string>();
        Exp4.ReopenAndVerify(root,1,scale,partitionOptions,[saves],errors,parallel:false);
        foreach(var error in errors){
            Console.WriteLine("ERROR "+error);
        }
        if(errors.IsEmpty){
            Console.WriteLine("PASS exp2");
            return 0;
        }
        Console.WriteLine("FAIL exp2");
        return 1;
    }
    private static IEnumerable<long> LongRange(long count){
        for(long index=0;index<count;index++){
            yield return index;
        }
    }
    private sealed class HandoffState{
        public long Count;
        public long Switches;
        public int LastThread=-1;
        public bool Released;
        public readonly HashSet<int> Threads=[];
    }
}
