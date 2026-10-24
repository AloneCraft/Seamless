using System.Diagnostics;
using System.Globalization;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// 実験 6: 行数 約 100 万の区画で、保存 1 回あたりのディスクの増分と、保存の間(区画を押さえて止めている時間)を測る。
/// 古いチェックポイントは消えない(DeviceLogCommitCheckpointManager の removeOutdated:false)ので、保存のたびに何が増えるかを分類する。
/// </summary>
public static class Exp6{
    public static int Run(Args options){
        var scale=Scale.Parse(options.Str("scale","million"));
        var saves=options.Int("saves",6);
        var txns=options.Int("txns",3000);
        var release=options.Bool("release",true);
        var root=Report.NewRoot(options,"exp6");
        var directory=Path.Combine(root,"p0");
        var partitionOptions=new PartitionOptions{Mirror=false,MemoryBytes=options.Long("memory-mb",4096)*1024*1024};
        Console.WriteLine($"root={root} scale={scale} saves={saves} txns between saves={txns} release={release}");
        var partition=new Partition(0,directory,scale,partitionOptions);
        var load=Stopwatch.StartNew();
        partition.Load();
        Console.WriteLine($"load {Report.Ms(load.Elapsed)}ms");
        PrintRows(partition);
        var breakdown=Breakdown(directory);
        Console.WriteLine("before first save: "+Format(breakdown));
        var times=new List<TimeSpan>();
        for(var round=0;round<=saves;round++){
            if(round>0){
                var run=Stopwatch.StartNew();
                for(var txn=0;txn<txns;txn++){
                    partition.RunTransaction();
                }
                Console.WriteLine($"round {round}: {txns} txns in {Report.Ms(run.Elapsed)}ms ({txns/run.Elapsed.TotalSeconds:0} txn/s)");
            }
            var record=partition.Save(release);
            var after=Breakdown(directory);
            Console.WriteLine($"save {round}: {Report.Ms(record.Elapsed)}ms, added {Format(Delta(after,breakdown))}, total {Report.Mb(after.Values.Sum())}");
            if(round>0){
                times.Add(record.Elapsed);
            }
            breakdown=after;
        }
        // 変更なしで続けて保存したときの固定の費用。
        var idle=partition.Save(release);
        var idleAfter=Breakdown(directory);
        Console.WriteLine($"save without changes: {Report.Ms(idle.Elapsed)}ms, added {Format(Delta(idleAfter,breakdown))}");
        Console.WriteLine($"save times after txns {Report.Stats(times)}");
        PrintIndexSizes(directory);
        var rowsBefore=partition.Parts.Sum(static part=>part.StoreCount());
        partition.Dispose();
        var reopen=Stopwatch.StartNew();
        using(var again=new Partition(0,directory,scale,partitionOptions)){
            var opened=reopen.Elapsed;
            var counting=Stopwatch.StartNew();
            var total=again.Parts.Sum(static part=>part.StoreCount());
            Console.WriteLine($"reopen {Report.Ms(opened)}ms, counts {Report.Ms(counting.Elapsed)}ms (rows={total}, before close {rowsBefore})");
            if(total!=rowsBefore){
                Console.WriteLine("FAIL exp6: row count changed across reopen");
                return 1;
            }
        }
        Console.WriteLine("PASS exp6");
        return 0;
    }
    private static void PrintRows(Partition partition){
        long total=0;
        var parts=new List<string>();
        foreach(var part in partition.Parts){
            var count=part.StoreCount();
            total+=count;
            parts.Add($"{part.Name}={count}");
        }
        Console.WriteLine($"rows total={total}: {string.Join(" ",parts)}");
    }
    /// <summary>分類ごとの大きさ: 索引のチェックポイント・ログのチェックポイント(スナップショット)・ログ本体・その他。</summary>
    private static Dictionary<string,long> Breakdown(string directory){
        var result=new Dictionary<string,long>{["index-ckpt"]=0,["log-ckpt"]=0,["hlog"]=0,["other"]=0};
        foreach(var file in Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories)){
            long length;
            try{
                length=new FileInfo(file).Length;
            }catch(FileNotFoundException){
                continue;
            }
            var normalized=file.Replace('\\','/');
            string category;
            if(normalized.Contains("/index-checkpoints/",StringComparison.Ordinal)){
                category="index-ckpt";
            }else if(normalized.Contains("/cpr-checkpoints/",StringComparison.Ordinal)){
                category="log-ckpt";
            }else if(Path.GetFileName(file).Contains("hlog",StringComparison.Ordinal)){
                category="hlog";
            }else{
                category="other";
            }
            result[category]+=length;
        }
        return result;
    }
    private static Dictionary<string,long> Delta(Dictionary<string,long> after,Dictionary<string,long> before){
        return after.ToDictionary(static pair=>pair.Key,pair=>pair.Value-before.GetValueOrDefault(pair.Key));
    }
    private static string Format(Dictionary<string,long> sizes){
        return string.Join(" ",sizes.Select(static pair=>$"{pair.Key}={Report.Mb(pair.Value)}"))+$" (sum {Report.Mb(sizes.Values.Sum())})";
    }
    /// <summary>データ本体ごとの、1 回分の索引のチェックポイントの大きさ(最新の保存)。</summary>
    private static void PrintIndexSizes(string directory){
        var parts=new List<string>();
        foreach(var component in Directory.EnumerateDirectories(Path.Combine(directory,"data"))){
            var indexRoot=Path.Combine(component,"checkpoints","index-checkpoints");
            if(!Directory.Exists(indexRoot)){
                continue;
            }
            var latest=Directory.EnumerateDirectories(indexRoot).OrderBy(static path=>Directory.GetCreationTimeUtc(path)).LastOrDefault();
            if(latest is null){
                continue;
            }
            parts.Add($"{Path.GetFileName(component)}={Report.Mb(Report.DirectoryBytes(latest))}(x{Directory.EnumerateDirectories(indexRoot).Count()})");
        }
        Console.WriteLine("index checkpoint per save (count kept): "+string.Join(" ",parts));
    }
}
