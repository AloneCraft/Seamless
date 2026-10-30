using System.Diagnostics;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// 実験 7(報告の「限界」を埋める追加の試し):
/// --mode checkpoints : 保存を N 回積み重ねた区画の、開き直し時間・保存時間・ディスクの大きさ(古いチェックポイントが消えないので、開き直しが伸びるか)。
/// --mode storecount  : 1 ストアのデータ本体の数 K を変えたときの、変更なしの保存の時間(保存時間が行数でなくデータ本体の数で決まるか)。行は各本体 --rows 件。
/// --mode setoverhead : Set 型のデータ本体 1 個あたりの固定メモリ(管理ヒープ・private)。
/// </summary>
public static class Exp7{
    public static int Run(Args options){
        var mode=options.Str("mode","checkpoints");
        var root=Report.NewRoot(options,"exp7-"+mode);
        Console.WriteLine($"root={root} mode={mode}");
        return mode switch{
            "checkpoints"=>Checkpoints(root,options),
            "storecount"=>StoreCount(root,options),
            "setoverhead"=>SetOverhead(root),
            _=>throw new ArgumentException("--mode must be checkpoints, storecount or setoverhead"),
        };
    }
    private static int Checkpoints(string root,Args options){
        var scale=Scale.Parse(options.Str("scale","small"));
        var steps=options.Str("steps","1,5,10,20,40,80").Split(',').Select(int.Parse).ToArray();
        var txns=options.Int("txns",50);
        var directory=Path.Combine(root,"p0");
        var partitionOptions=new PartitionOptions{Mirror=false,MemoryBytes=4096L*1024*1024};
        var saved=0;
        Partition? partition=new Partition(0,directory,scale,partitionOptions);
        partition.Load();
        foreach(var target in steps){
            while(saved<target){
                for(var txn=0;txn<txns;txn++){
                    partition!.RunTransaction();
                }
                partition!.Save(true);
                saved++;
            }
            partition!.Dispose();
            partition=null;
            var checkpointDirectories=Directory.EnumerateDirectories(directory,"*",SearchOption.AllDirectories).Count(static path=>Guid.TryParse(Path.GetFileName(path),out _));
            var size=Report.DirectoryBytes(directory);
            var open=Stopwatch.StartNew();
            partition=new Partition(0,directory,scale,partitionOptions);
            var opened=open.Elapsed;
            var perPart=partition.OpenElapsed;
            var next=Stopwatch.StartNew();
            var manifest=new FileInfo(Path.Combine(directory,"store.json")).Length;
            Console.WriteLine($"saves={saved}: reopen {Report.Ms(opened)}ms (ctor {Report.Ms(perPart)}ms) checkpoint folders={checkpointDirectories} store.json={manifest}B directory={Report.Mb(size)}");
            var one=partition.Save(true);
            Console.WriteLine($"  next save {Report.Ms(one.Elapsed)}ms");
        }
        partition!.Dispose();
        Console.WriteLine("PASS exp7 checkpoints");
        return 0;
    }
    private static int StoreCount(string root,Args options){
        var rows=options.Int("rows",20000);
        var counts=options.Str("counts","1,2,4,9,18,36").Split(',').Select(int.Parse).ToArray();
        var tuning=TsavoriteTuning.Default with{LogMemorySize=1L<<22};
        foreach(var k in counts){
            var directory=Path.Combine(root,"k"+k);
            using var store=Store.Open(directory,new StoreLimits{MemoryBytes=8L*1024*1024*1024,DiskBytes=64L*1024*1024*1024});
            var handles=new List<IDisposable>();
            for(var index=0;index<k;index++){
                var data=store.CreateTsavorite<K4,V24>("d"+index,tuning,rows);
                var handle=data.Create();
                handles.Add(handle);
                for(var row=0;row<rows;row++){
                    handle[new K4(row,index,0,0)]=new V24{A=row};
                }
            }
            foreach(var handle in handles){
                handle.Dispose();
            }
            store.Save();
            var times=new List<TimeSpan>();
            for(var round=0;round<5;round++){
                var watch=Stopwatch.StartNew();
                store.Save();
                times.Add(watch.Elapsed);
            }
            Console.WriteLine($"stores={k} rows/store={rows} total rows={(long)k*rows}: no-change save {Report.Stats(times)}");
        }
        Console.WriteLine("PASS exp7 storecount");
        return 0;
    }
    private static int SetOverhead(string root){
        const int Count=100;
        foreach(var label in new[]{"set-default","set-tiny","dict-default"}){
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var before=GC.GetTotalMemory(true);
            var directory=Path.Combine(root,label);
            using var store=Store.Open(directory,new StoreLimits{MemoryBytes=64L*1024*1024*1024});
            var tuning=TsavoriteTuning.Default;
            if(label=="set-tiny"){
                tuning=TsavoriteTuning.Default with{PageSize=4096,LogMemorySize=8192,IndexSize=64,Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
            }
            for(var index=0;index<Count;index++){
                if(label=="dict-default"){
                    store.CreateTsavorite<K4,V24>("c"+index,tuning);
                }else{
                    store.CreateTsavoriteSet<K4,Row<Pad8>>("c"+index,tuning);
                }
            }
            var after=GC.GetTotalMemory(true);
            Console.WriteLine($"{label}: per component managed heap={Report.Mb((after-before)/Count)}");
        }
        Console.WriteLine("PASS exp7 setoverhead");
        return 0;
    }
}
