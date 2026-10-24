using System.Diagnostics;
using System.Reflection;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Tsavorite.core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// 実験 3: LightEpoch の上限(1 プロセスで同時に生きている TsavoriteKV の数)。
/// (1) データ本体 1 つ・保存・読み込みで、いくつ使うか。(2) P×18 個(P=1,2,4,8,16)を作って使い、保存し、破棄して戻るか。
/// (3) 上限まで作ったときの例外・漏れ・作り直し・上限近くでの保存と読み込み。(4) 複数スレッドから同時に作ったときの上限。
/// </summary>
public static class Exp3{
    private static readonly TsavoriteTuning Tiny=TsavoriteTuning.Default with{PageSize=4096,LogMemorySize=8192,IndexSize=64,AutoGrowIndex=false};
    public static int Run(Args options){
        var root=Report.NewRoot(options,"exp3");
        var baseline=LightEpoch.ActiveInstanceCount();
        Console.WriteLine($"Garnet {typeof(LightEpoch).Assembly.GetName().Version} MaxInstances(reflection)={ReadMaxInstances()} active at start={baseline}");
        var errors=new List<string>();
        PerComponent(root,baseline,errors);
        Overhead(root,"tiny(page 4KB,log 8KB,index 64B)",Tiny);
        Overhead(root,"default(page 64KB,log 64MB,index 64KB)",TsavoriteTuning.Default);
        Overhead(root,"page 4KB,log 64KB,index 64KB",TsavoriteTuning.Default with{PageSize=4096,LogMemorySize=1L<<16});
        foreach(var count in options.Str("partitions","1,2,4,8,16").Split(',').Select(int.Parse)){
            Partitions(root,count,baseline,errors);
        }
        Limit(root,baseline,errors);
        ConcurrentLimit(root,baseline,options.Int("threads",8),errors);
        foreach(var error in errors){
            Console.WriteLine("ERROR "+error);
        }
        if(errors.Count==0){
            Console.WriteLine("PASS exp3");
            return 0;
        }
        Console.WriteLine("FAIL exp3");
        return 1;
    }
    private static int Active()=>LightEpoch.ActiveInstanceCount();
    private static object? ReadMaxInstances(){
        foreach(var type in AllNested(typeof(LightEpoch))){
            var field=type.GetField("MaxInstances",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            if(field is not null){
                return field.GetValue(null);
            }
        }
        return null;
    }
    private static IEnumerable<Type> AllNested(Type type){
        yield return type;
        foreach(var nested in type.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic)){
            foreach(var inner in AllNested(nested)){
                yield return inner;
            }
        }
    }
    /// <summary>動作中に LightEpoch の数の最大を見張る。</summary>
    private static (T Result,int Peak) WithPeak<T>(Func<T> action){
        var peak=Active();
        var stop=0;
        var sampler=new Thread(()=>{
            while(Volatile.Read(ref stop)==0){
                var now=Active();
                if(now>Volatile.Read(ref peak)){
                    Volatile.Write(ref peak,now);
                }
                Thread.Yield();
            }
        });
        sampler.Start();
        try{
            var result=action();
            return (result,Math.Max(peak,Active()));
        }finally{
            Volatile.Write(ref stop,1);
            sampler.Join();
        }
    }
    private static void PerComponent(string root,int baseline,List<string> errors){
        var steps=new List<string>();
        using(var store=Store.Open(Path.Combine(root,"one"))){
            steps.Add($"Store.Open={Active()-baseline}");
            var set=store.CreateTsavoriteSet<K4,Row<Pad8>>("set",Tiny);
            steps.Add($"+SetStore={Active()-baseline}");
            var handle=set.Create();
            handle.Add(new Row<Pad8>{K=new(1,0,0,0)});
            steps.Add($"+Create()+Add={Active()-baseline}");
            var dictionary=store.CreateTsavorite<K4,V24>("dict",Tiny);
            steps.Add($"+DictionaryStore={Active()-baseline}");
            var region=dictionary.BeginParallel();
            var parallel=region.Create();
            parallel[new K4(1,0,0,0)]=new V24{A=1};
            steps.Add($"+BeginParallel().Create()={Active()-baseline}");
            parallel.Dispose();
            region.Dispose();
            var (_,savePeak)=WithPeak(()=>store.Save());
            steps.Add($"Save(peak)={savePeak-baseline} after={Active()-baseline}");
            handle.Dispose();
            var (_,loadPeak)=WithPeak(()=>{
                store.Load();
                return 0;
            });
            steps.Add($"Load(peak)={loadPeak-baseline} after={Active()-baseline}");
        }
        steps.Add($"Store.Dispose={Active()-baseline}");
        using(var memory=Store.CreateMemory()){
            memory.CreateTsavoriteSet<K4,Row<Pad8>>("set",Tiny with{LogMemorySize=1L<<16});
            steps.Add($"memory store+SetStore={Active()-baseline}");
        }
        using(var temporary=Store.CreateTemporary(null,Path.Combine(root,"tmp"))){
            temporary.CreateTsavoriteSet<K4,Row<Pad8>>("set",Tiny);
            steps.Add($"temporary store+SetStore={Active()-baseline}");
        }
        steps.Add($"all disposed={Active()-baseline}");
        Console.WriteLine("per component (LightEpoch instances above the start): "+string.Join(", ",steps));
        if(Active()!=baseline){
            errors.Add($"per component: {Active()-baseline} instances leaked");
        }
    }
    /// <summary>データ本体 1 つあたりの固定のメモリ(空のまま 100 個作った差分)。</summary>
    private static void Overhead(string root,string label,TsavoriteTuning tuning){
        const int Count=100;
        var process=Process.GetCurrentProcess();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        var privateBefore=process.PrivateMemorySize64;
        var workingBefore=process.WorkingSet64;
        var managedBefore=GC.GetTotalMemory(true);
        var directory=Path.Combine(root,"overhead-"+Guid.NewGuid().ToString("N")[..8]);
        using(var store=Store.Open(directory,new StoreLimits{MemoryBytes=64L*1024*1024*1024})){
            for(var index=0;index<Count;index++){
                store.CreateTsavorite<K4,V24>("c"+index,tuning);
            }
            process.Refresh();
            var managed=GC.GetTotalMemory(true);
            Console.WriteLine($"overhead {label}: per component private={Report.Mb((process.PrivateMemorySize64-privateBefore)/Count)} working set={Report.Mb((process.WorkingSet64-workingBefore)/Count)} managed heap={Report.Mb((managed-managedBefore)/Count)}");
        }
    }
    private static void Partitions(string root,int count,int baseline,List<string> errors){
        var directory=Path.Combine(root,"p"+count);
        var partitions=new Partition[count];
        var watch=Stopwatch.StartNew();
        Exp1.RunThreads(count,index=>{
            partitions[index]=new Partition(index,Path.Combine(directory,"p"+index),Scale.Tiny,new PartitionOptions{Mirror=false});
            partitions[index].Load();
        });
        var created=watch.Elapsed;
        var active=Active()-baseline;
        var process=Process.GetCurrentProcess();
        process.Refresh();
        var components=partitions.Sum(static partition=>partition.Parts.Count+1);
        Exp1.RunThreads(count,index=>{
            for(var txn=0;txn<200;txn++){
                partitions[index].RunTransaction();
            }
        });
        var (_,savePeak)=WithPeak(()=>{
            Exp1.RunThreads(count,index=>partitions[index].Save(true));
            return 0;
        });
        foreach(var partition in partitions){
            partition.Dispose();
        }
        var after=Active()-baseline;
        Console.WriteLine($"P={count}: components={components} LightEpoch={active} (save peak {savePeak-baseline}) after dispose={after}; create+load {Report.Ms(created)}ms working set={Report.Mb(process.WorkingSet64)} private={Report.Mb(process.PrivateMemorySize64)} handles={process.HandleCount}");
        if(active!=components){
            errors.Add($"P={count}: {active} instances for {components} components");
        }
        if(after!=0){
            errors.Add($"P={count}: {after} instances remain after dispose");
        }
    }
    private static void Limit(string root,int baseline,List<string> errors){
        var limits=new StoreLimits{MemoryBytes=8L*1024*1024*1024,DiskBytes=64L*1024*1024*1024};
        var store=Store.Open(Path.Combine(root,"limit"),limits);
        var components=new List<DictionaryStore<K4,V24>>();
        var watch=Stopwatch.StartNew();
        Exception? failure=null;
        var beforeFailure=0;
        try{
            while(true){
                beforeFailure=Active();
                components.Add(store.CreateTsavorite<K4,V24>("c"+components.Count,Tiny));
            }
        }catch(Exception error){
            failure=error;
        }
        var afterFailure=Active();
        var process=Process.GetCurrentProcess();
        process.Refresh();
        Console.WriteLine($"limit: created {components.Count} components in {Report.Ms(watch.Elapsed)}ms then {failure?.GetType().Name}: {failure?.Message}");
        Console.WriteLine($"limit: active before the failing call={beforeFailure} after={afterFailure} (leak={afterFailure-beforeFailure}) working set={Report.Mb(process.WorkingSet64)} private={Report.Mb(process.PrivateMemorySize64)} gc-committed={Report.Mb(GC.GetGCMemoryInfo().TotalCommittedBytes)} managed={Report.Mb(GC.GetTotalMemory(false))} handles={process.HandleCount}");
        if(afterFailure!=beforeFailure){
            errors.Add($"limit: the failed creation leaked {afterFailure-beforeFailure} instances");
        }
        if(components.Count+baseline!=1024){
            errors.Add($"limit: {components.Count} components + {baseline} at start != 1024");
        }
        // 上限に達したあとも、作れたものは使え、保存と読み込みもできるか。
        using(var handle=components[0].Create()){
            handle[new K4(1,2,3,4)]=new V24{A=42};
            if(!handle.TryGetValue(new K4(1,2,3,4),out var value)||value.A!=42){
                errors.Add("limit: read back failed at the limit");
            }
        }
        try{
            var saveWatch=Stopwatch.StartNew();
            var (token,peak)=WithPeak(()=>store.Save());
            Console.WriteLine($"limit: Save of {components.Count} components at the limit took {Report.Ms(saveWatch.Elapsed)}ms (peak active={peak})");
            var load=Stopwatch.StartNew();
            store.Load(token);
            Console.WriteLine($"limit: Load at the limit {Report.Ms(load.Elapsed)}ms");
        }catch(Exception error){
            errors.Add("limit: save/load at the limit failed: "+error.Message);
        }
        // 1 つ破棄すれば、もう 1 つ作れる。
        components[^1].Dispose();
        components.RemoveAt(components.Count-1);
        try{
            components.Add(store.CreateTsavorite<K4,V24>("again",Tiny));
            Console.WriteLine("limit: after disposing one, creating one more succeeded");
        }catch(Exception error){
            errors.Add("limit: could not create after disposing one: "+error.Message);
        }
        try{
            store.CreateTsavorite<K4,V24>("over",Tiny);
            errors.Add("limit: creating beyond the limit succeeded unexpectedly");
        }catch(InvalidOperationException){
        }
        // 失敗した名前は登録されていないので、保存の記録にも残らない。
        var dispose=Stopwatch.StartNew();
        store.Dispose();
        Console.WriteLine($"limit: Store.Dispose {Report.Ms(dispose.Elapsed)}ms, active after={Active()-baseline}");
        if(Active()!=baseline){
            errors.Add($"limit: {Active()-baseline} instances remain after dispose");
        }
        // 開き直し: 保存した 1023+1 個の状態を戻せるか(上限ちょうど)。
        var reopen=Stopwatch.StartNew();
        using(var again=Store.Open(Path.Combine(root,"limit"),limits)){
            var opened=0;
            try{
                for(var index=0;index<1024;index++){
                    again.CreateTsavorite<K4,V24>("c"+index,Tiny);
                    opened++;
                }
            }catch(InvalidOperationException error){
                Console.WriteLine($"limit reopen: {opened} reopened, then {error.Message}");
            }
            Console.WriteLine($"limit reopen: {opened} components in {Report.Ms(reopen.Elapsed)}ms");
        }
    }
    private static void ConcurrentLimit(string root,int baseline,int threads,List<string> errors){
        var stores=new Store[threads];
        var counts=new int[threads];
        Exp1.RunThreads(threads,index=>{
            stores[index]=Store.Open(Path.Combine(root,"concurrent","s"+index),new StoreLimits{MemoryBytes=4L*1024*1024*1024});
            try{
                while(true){
                    stores[index].CreateTsavorite<K4,V24>("c"+counts[index],Tiny);
                    counts[index]++;
                }
            }catch(InvalidOperationException){
            }
        });
        var total=counts.Sum();
        Console.WriteLine($"concurrent: {threads} threads created {total} ({string.Join("+",counts)}) active={Active()}");
        if(total+baseline!=1024){
            errors.Add($"concurrent: {total} created + {baseline} != 1024");
        }
        foreach(var store in stores){
            store.Dispose();
        }
        if(Active()!=baseline){
            errors.Add($"concurrent: {Active()-baseline} remain after dispose");
        }
    }
}
