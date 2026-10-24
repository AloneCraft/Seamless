using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
namespace Benchmark.TpcC.Spike;
/// <summary>
/// 実験 4: P 個の区画の並列の開き直しと復旧。
/// --mode open  : 保存済みの P 区画を、順に開く場合と並列に開く場合で時間を比べ、中身を照合する。
/// --mode crash : 子プロセスが P 区画へ書き込み・保存を続け、親が任意の時点で強制終了する。親は P 区画を並列に開き、
///                各区画が自分の保存に書いた要約(meta)と一致すること、子が「保存した」と報告した回より古くないことを確かめる。
/// </summary>
public static class Exp4{
    public static int Run(Args options){
        var mode=options.Str("mode","open");
        if(mode=="open"){
            return RunOpen(options);
        }
        if(mode=="crash"){
            return RunCrash(options);
        }
        throw new ArgumentException("--mode must be open or crash");
    }
    private static int RunOpen(Args options){
        var count=options.Int("partitions",4);
        var scale=Scale.Parse(options.Str("scale","small"));
        var root=Report.NewRoot(options,"exp4-open");
        var partitionOptions=new PartitionOptions{Seed=options.Int("seed",1)};
        var errors=new ConcurrentQueue<string>();
        var saves=new List<SaveRecord>[count];
        var build=Stopwatch.StartNew();
        Exp1.RunThreads(count,index=>{
            using var partition=new Partition(index,Path.Combine(root,"p"+index),scale,partitionOptions);
            partition.Load();
            var first=partition.Save(true);
            for(var txn=0;txn<options.Int("txns",3000);txn++){
                partition.RunTransaction();
            }
            saves[index]=[first,partition.Save(true)];
        });
        Console.WriteLine($"built {count} partitions scale={scale} in {Report.Ms(build.Elapsed)}ms, size={Report.Mb(Report.DirectoryBytes(root))}");
        for(var round=0;round<options.Int("rounds",2);round++){
            ReopenAndVerify(root,count,scale,partitionOptions,saves,errors,parallel:true,checkIntermediate:false);
            ReopenAndVerify(root,count,scale,partitionOptions,saves,errors,parallel:false,checkIntermediate:false);
        }
        ReopenAndVerify(root,count,scale,partitionOptions,saves,errors,parallel:true,checkIntermediate:true);
        foreach(var error in errors){
            Console.WriteLine("ERROR "+error);
        }
        if(errors.IsEmpty){
            Console.WriteLine("PASS exp4-open");
            return 0;
        }
        Console.WriteLine("FAIL exp4-open");
        return 1;
    }
    /// <summary>P 区画を開き直し、最後の保存(と、あれば途中の保存)の要約と照合する。</summary>
    public static void ReopenAndVerify(string root,int count,Scale scale,PartitionOptions options,List<SaveRecord>[] saves,ConcurrentQueue<string> errors,bool parallel,bool checkIntermediate=true){
        var partitions=new Partition[count];
        var watch=Stopwatch.StartNew();
        // 照合のための模型の作り直しは時間に入れない(開くだけの時間を測る)。
        var openOptions=options with{Mirror=false};
        if(parallel){
            Exp1.RunThreads(count,index=>partitions[index]=new Partition(index,Path.Combine(root,"p"+index),scale,openOptions));
        }else{
            for(var index=0;index<count;index++){
                partitions[index]=new Partition(index,Path.Combine(root,"p"+index),scale,openOptions);
            }
        }
        var openElapsed=watch.Elapsed;
        var each=string.Join(" ",partitions.Select(static partition=>Report.Ms(partition.OpenElapsed)));
        Console.WriteLine($"reopen parallel={parallel}: all {Report.Ms(openElapsed)}ms, each [{each}] ms, LightEpoch active={Tsavorite.core.LightEpoch.ActiveInstanceCount()}");
        try{
            for(var index=0;index<count;index++){
                var partition=partitions[index];
                var last=saves[index][^1];
                if(!partition.Recovered){
                    errors.Enqueue($"p{index}: not recovered");
                    continue;
                }
                if(partition.Serial!=last.Serial){
                    errors.Enqueue($"p{index}: serial {partition.Serial} expected {last.Serial}");
                }
                var self=partition.VerifySelf();
                if(self is not null){
                    errors.Enqueue("self "+self);
                }
                if(last.Expected is not null){
                    var problem=partition.Verify(last.Expected);
                    if(problem is not null){
                        errors.Enqueue("latest "+problem);
                    }
                }
                if(checkIntermediate&&saves[index].Count>=3){
                    var middle=saves[index][saves[index].Count/2];
                    partition.ReleaseAll();
                    partition.Store.Load(middle.Token);
                    partition.AcquireAll();
                    var problem=partition.Verify(middle.Expected!);
                    if(problem is not null){
                        errors.Enqueue($"intermediate serial {middle.Serial}: {problem}");
                    }else{
                        Console.WriteLine($"p{index}: latest serial {last.Serial} and intermediate serial {middle.Serial} match");
                    }
                }
            }
        }finally{
            foreach(var partition in partitions){
                partition?.Dispose();
            }
        }
    }
    private static int RunCrash(Args options){
        var count=options.Int("partitions",4);
        var cycles=options.Int("cycles",10);
        var scale=options.Str("scale","tiny");
        var root=Report.NewRoot(options,"exp4-crash");
        var cpu=options.Str("cpu","14");
        var minMs=options.Int("kill-min-ms",500);
        var maxMs=options.Int("kill-max-ms",6000);
        var rng=new Random(options.Int("seed",1));
        var errors=new List<string>();
        var recoverTimes=new List<TimeSpan>();
        var lastSerial=new long[count];
        for(var cycle=1;cycle<=cycles&&errors.Count==0;cycle++){
            var start=new ProcessStartInfo(Environment.ProcessPath!){
                RedirectStandardOutput=true,
                UseShellExecute=false,
            };
            foreach(var argument in new[]{"exp4child","--root",root,"--partitions",count.ToString(CultureInfo.InvariantCulture),"--scale",scale,"--seed",cycle.ToString(CultureInfo.InvariantCulture),"--cpu",cpu,"--save-interval-ms",options.Str("save-interval-ms","300")}){
                start.ArgumentList.Add(argument);
            }
            var maxSaved=new long[count];
            var ready=new ManualResetEventSlim();
            var lines=new ConcurrentQueue<string>();
            using var child=Process.Start(start)!;
            child.OutputDataReceived+=(_,e)=>{
                if(e.Data is null){
                    return;
                }
                lines.Enqueue(e.Data);
                if(e.Data=="ready"){
                    ready.Set();
                }else if(e.Data.StartsWith("saved ",StringComparison.Ordinal)){
                    var parts=e.Data.Split(' ');
                    var partition=int.Parse(parts[1],CultureInfo.InvariantCulture);
                    var serial=long.Parse(parts[2],CultureInfo.InvariantCulture);
                    lock(maxSaved){
                        maxSaved[partition]=Math.Max(maxSaved[partition],serial);
                    }
                }
            };
            child.BeginOutputReadLine();
            if(!ready.Wait(TimeSpan.FromMinutes(5))){
                child.Kill(true);
                errors.Add($"cycle {cycle}: child did not become ready: {string.Join(" | ",lines)}");
                break;
            }
            int delay;
            if(options.Str("kill-mode","time")=="saves"){
                // 保存が戻った回数(全区画の合計)が目標に届いてから、保存の間隔の範囲でランダムに待って殺す。
                // 殺す時刻が「ある区画の保存の直後〜次の保存の途中」に散らばる。
                var target=1+rng.Next(2*count+2);
                var watchSaves=Stopwatch.StartNew();
                while(lines.Count(static line=>line.StartsWith("saved ",StringComparison.Ordinal))<target&&watchSaves.Elapsed<TimeSpan.FromMinutes(5)){
                    Thread.Sleep(20);
                }
                delay=rng.Next(0,options.Int("save-interval-ms",300)*3);
                Thread.Sleep(delay);
            }else{
                delay=rng.Next(minMs,maxMs);
                Thread.Sleep(delay);
            }
            child.Kill(entireProcessTree:true);
            child.WaitForExit();
            var failed=lines.FirstOrDefault(static line=>line.StartsWith("FAIL",StringComparison.Ordinal));
            if(failed is not null){
                errors.Add($"cycle {cycle}: child reported {failed}");
                break;
            }
            long[] snapshot;
            lock(maxSaved){
                snapshot=[..maxSaved];
            }
            var partitions=new Partition[count];
            var watch=Stopwatch.StartNew();
            try{
                Exp1.RunThreads(count,index=>partitions[index]=new Partition(index,Path.Combine(root,"p"+index),Scale.Parse(scale),new PartitionOptions{Mirror=false}));
                var elapsed=watch.Elapsed;
                recoverTimes.Add(elapsed);
                var serials=new long[count];
                for(var index=0;index<count;index++){
                    var partition=partitions[index];
                    serials[index]=partition.Serial;
                    var problem=partition.VerifySelf();
                    if(problem is not null){
                        errors.Add($"cycle {cycle}: {problem}");
                    }
                    if(partition.Serial<snapshot[index]){
                        errors.Add($"cycle {cycle}: p{index} recovered serial {partition.Serial} is older than the acknowledged save {snapshot[index]}");
                    }
                    if(partition.Serial<lastSerial[index]){
                        errors.Add($"cycle {cycle}: p{index} went back from serial {lastSerial[index]} to {partition.Serial}");
                    }
                    lastSerial[index]=partition.Serial;
                }
                var verdict="ok";
                if(errors.Count!=0){
                    verdict="NG";
                }
                Console.WriteLine($"cycle {cycle}: killed after {delay}ms, recovered in {Report.Ms(elapsed)}ms, serials [{string.Join(",",serials)}] acknowledged [{string.Join(",",snapshot)}] {verdict}");
            }catch(Exception error){
                errors.Add($"cycle {cycle}: reopen failed: {error}");
            }finally{
                foreach(var partition in partitions){
                    partition?.Dispose();
                }
            }
        }
        Console.WriteLine($"recovery times {Report.Stats(recoverTimes)} LightEpoch active after dispose={Tsavorite.core.LightEpoch.ActiveInstanceCount()}");
        foreach(var error in errors){
            Console.WriteLine("ERROR "+error);
        }
        if(errors.Count==0){
            Console.WriteLine("PASS exp4-crash");
            return 0;
        }
        Console.WriteLine("FAIL exp4-crash");
        return 1;
    }
    /// <summary>強制終了される子。区画を開き(無ければ作って投入)、書き込みと保存を止まるまで続ける。</summary>
    public static int Child(Args options){
        var count=options.Int("partitions",4);
        var scale=Scale.Parse(options.Str("scale","tiny"));
        var root=options.Str("root","");
        var interval=TimeSpan.FromMilliseconds(options.Int("save-interval-ms",300));
        var partitionOptions=new PartitionOptions{Seed=options.Int("seed",1)};
        var partitions=new Partition[count];
        Exp1.RunThreads(count,index=>{
            var partition=new Partition(index,Path.Combine(root,"p"+index),scale,partitionOptions);
            if(!partition.Recovered){
                partition.Load();
                partition.Save(true);
            }
            partitions[index]=partition;
        });
        var output=TextWriter.Synchronized(Console.Out);
        output.WriteLine("ready");
        output.Flush();
        var threads=new Thread[count];
        for(var index=0;index<count;index++){
            var partition=partitions[index];
            threads[index]=new Thread(()=>{
                try{
                    var last=Stopwatch.StartNew();
                    var release=false;
                    // 区画ごとに間隔をずらし、保存が重なったり離れたりするようにする。
                    var myInterval=interval*(1+partition.Id*0.37);
                    while(true){
                        partition.RunTransaction();
                        if(last.Elapsed>=myInterval){
                            // 操作用オブジェクトを開いたままの保存と、閉じての保存を交互に行う。
                            release=!release;
                            var record=partition.Save(release);
                            output.WriteLine($"saved {partition.Id} {record.Serial}");
                            output.Flush();
                            last.Restart();
                        }
                    }
                }catch(Exception error){
                    output.WriteLine($"FAIL p{partition.Id}: {error.ToString().Replace('\n',' ')}");
                    output.Flush();
                }
            });
            threads[index].Start();
        }
        foreach(var thread in threads){
            thread.Join();
        }
        return 1;
    }
}
