using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Seamless.Transactions;

namespace TransactionModes;

public static class Acceptance{
    public static void Check(bool value,string message){if(!value){throw new InvalidOperationException(message);}}
    private static void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new InvalidOperationException("Expected "+typeof(T).Name);}
    public static void Run(string root,string report){
        Directory.CreateDirectory(root);var results=new List<object>();
        foreach(var disk in new[]{false,true}){
            foreach(var mode in new[]{Mode.Deferred,Mode.Immediate}){
                foreach(var test in new (string,Action<Engine>)[]{("Visibility",Visibility),("Conflict",Conflict),("WriteSkew",WriteSkew),("Phantom",Phantom),("Queries",QueryResults),("ParallelSnapshot",ParallelSnapshot),("AtomicPublication",AtomicPublication),("Retention",Retention),("AbortedRetention",AbortedRetention),("Budget",Budget)}){
                    var path=Path.Combine(root,$"{mode}-{disk}-{test.Item1}");var clock=Stopwatch.StartNew();
                    try{using var engine=new Engine(mode,path,disk,64);test.Item2(engine);results.Add(new{mode,disk,test=test.Item1,outcome="Passed",ms=clock.Elapsed.TotalMilliseconds});Console.WriteLine($"PASS {mode} disk={disk} {test.Item1}");}
                    catch(Exception error){results.Add(new{mode,disk,test=test.Item1,outcome="Failed",error=error.ToString()});Console.WriteLine($"FAIL {mode} disk={disk} {test.Item1}: {error}");}
                }
            }
        }
        foreach(var disk in new[]{false,true}){
            try{Collision(Path.Combine(root,"collision-"+disk),disk);results.Add(new{test="Collision",disk,outcome="Passed"});}catch(Exception error){results.Add(new{test="Collision",disk,outcome="Failed",error=error.ToString()});}
        }
        foreach(var mode in new[]{Mode.Deferred,Mode.Immediate}){
            try{SyncFence(mode,Path.Combine(root,"sync-"+mode));results.Add(new{mode,test="SyncFence",outcome="Passed"});}catch(Exception error){results.Add(new{mode,test="SyncFence",outcome="Failed",error=error.ToString()});}
        }
        try{using var single=new Engine(Mode.Single,Path.Combine(root,"single"),false,64);using(var tx=single.Begin()){Check(Queries.Sum(tx)==3200,"Single native sum");tx.Set(0,200);Check(Queries.Sum(tx)==3300,"Single read your writes");}using(var tx=single.Begin()){Check(tx.Read(0).Value==100,"Single undo");}results.Add(new{test="SingleNative",outcome="Passed"});}catch(Exception error){results.Add(new{test="SingleNative",outcome="Failed",error=error.ToString()});}
        foreach(var mode in new[]{Mode.Deferred,Mode.Immediate}){
            foreach(var phase in Enum.GetValues<Phase>()){
                if(mode==Mode.Immediate&&phase==Phase.FirstTableApplied){continue;}
                var path=Path.Combine(root,$"fault-{mode}-{phase}");
                try{Fault(mode,phase,path);results.Add(new{mode,test="Fault-"+phase,outcome="Passed"});Console.WriteLine($"PASS {mode} Fault-{phase}");}
                catch(Exception error){results.Add(new{mode,test="Fault-"+phase,outcome="Failed",error=error.ToString()});Console.WriteLine("FAIL "+error);}
            }
        }
        foreach(var mode in new[]{Mode.Deferred,Mode.Immediate}){foreach(var phase in new[]{Phase.BeforeWal,Phase.WalDurable,Phase.BeforePublish}){
            try{var seconds=Crash(mode,phase,Path.Combine(root,$"crash-{mode}-{phase}"));results.Add(new{mode,test="ProcessCrash-"+phase,outcome="Passed",recoverySeconds=seconds});Console.WriteLine($"PASS {mode} ProcessCrash-{phase}");}
            catch(Exception error){results.Add(new{mode,test="ProcessCrash-"+phase,outcome="Failed",error=error.ToString()});}
        }}
        try{NegativeControl(Path.Combine(root,"negative"));results.Add(new{test="NegativeControl",outcome="Passed"});}catch(Exception error){results.Add(new{test="NegativeControl",outcome="Failed",error=error.ToString()});}
        File.WriteAllText(report,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        if(results.Any(row=>JsonSerializer.Serialize(row).Contains("\"Failed\"",StringComparison.Ordinal))){throw new InvalidOperationException("Acceptance failed. See "+report);}
    }
    private static void Visibility(Engine engine){
        using var before=engine.Begin();using(var pending=engine.Begin()){
            pending.Set(3,200);pending.Set(3,300);Check(pending.Read(3).Value==300,"Read your writes");Check(before.Read(3).Value==100,"Dirty read");
        }
        Check(before.Read(3).Value==100,"Aborted version leaked");
        using(var update=engine.Begin()){update.Set(3,200);update.Set(3,0,deleted:true);Check(update.Commit(),"Commit rejected");}
        Check(before.Read(3).Value==100,"Old version lost after delete");
        using(var update=engine.Begin()){update.Set(3,400);Check(update.Commit(),"Reinsert rejected");}
        if(engine.Tables[0].Data.Store.Log.BeginAddress<engine.Tables[0].Data.Store.Log.TailAddress){
            // ファイル構成の実行でも同じスナップショットを検証する。退避は別の専用試験に含める。
            Check(before.Read(3).Value==100,"Snapshot changed after reinsert");
        }
        using var now=engine.Begin();Check(now.Read(3).Value==400,"Latest version missing");
    }
    private static void Conflict(Engine engine){
        using var a=engine.Begin();using var b=engine.Begin();var av=a.Read(1).Value;var bv=b.Read(1).Value;
        a.Set(1,av+1);b.Set(1,bv+1);Check(a.Commit(),"First commit failed");Check(!b.Commit(),"Lost update was accepted");
        using var verify=engine.Begin();Check(verify.Read(1).Value==101,"Lost update or aborted value");
    }
    private static void WriteSkew(Engine engine){
        using var a=engine.Begin();using var b=engine.Begin();Check(a.Read(1).Value+a.Read(2).Value==200,"Initial sum");Check(b.Read(1).Value+b.Read(2).Value==200,"Initial sum");
        a.Set(1,20);b.Set(2,20);Check(a.Commit(),"First commit failed");Check(!b.Commit(),"Write skew accepted");
    }
    private static void Phantom(Engine engine){
        using var a=engine.Begin();Check(Queries.Count(a)==64,"Seed count");Check(a.Read(1000).Deleted==1,"Missing key exists");
        using(var b=engine.Begin()){b.Set(1000,10);Check(b.Commit(),"Insert failed");}
        a.Set(2,99);Check(!a.Commit(),"Phantom not detected");
    }
    private static void QueryResults(Engine engine){
        using var old=engine.Begin();
        using(var writer=engine.Begin()){writer.Set(3,777);writer.Set(4,0,deleted:true);writer.Set(5,999,group:2);Check(writer.Commit(),"Update failed");}
        var count=Queries.Count(old);var contains=Queries.Contains(old,4);var first=Queries.First(old);
        Check(count==64&&contains&&first==100,$"Old snapshot query mismatch: count={count} contains={contains} first={first}");
        Check(Queries.Sum(old)==3200&&Queries.Sum(old)==Queries.SumLinq(old),"Old sum mismatch");Check(Queries.Join(old)==12800&&Queries.Join(old)==Queries.JoinLinq(old),"Join mismatch");var groups=Queries.Groups(old);Check(groups.Length==64&&groups.All(static group=>group.Total==100)&&groups.SequenceEqual(Queries.GroupsLinq(old)),"Group mismatch");
        using var now=engine.Begin();Check(Queries.Count(now)==63&&!Queries.Contains(now,4)&&Queries.First(now)==777,"Current query mismatch");Check(Queries.Sum(now)==Queries.SumLinq(now),"Current sum mismatch");
        now.Set(1001,123,group:2);Check(Queries.Count(now)==64,"Own insert count");Check(Queries.Sum(now)==Queries.SumLinq(now),"Own insert sum");
        Check(Queries.Join(now)==14399&&Queries.Join(now)==Queries.JoinLinq(now),"Own insert join");
        Check(Queries.Groups(now).Sum(static group=>group.Total)==7999&&Queries.Groups(now).SequenceEqual(Queries.GroupsLinq(now)),"Own insert group");
    }
    private static void ParallelSnapshot(Engine engine){
        using var snapshot=engine.Begin();var errors=new ConcurrentQueue<Exception>();using var start=new Barrier(5);
        var threads=Enumerable.Range(0,4).Select(worker=>new Thread(()=>{
            try{start.SignalAndWait();for(var i=0;i<30;i++){using var tx=engine.Begin();var row=tx.Read(worker);tx.Set(worker,row.Value+1);Check(tx.Commit(),"Independent update conflicted");}}
            catch(Exception error){errors.Enqueue(error);}
        })).ToArray();
        foreach(var thread in threads){thread.Start();}start.SignalAndWait();
        try{for(var i=0;i<30;i++){Check(Queries.Count(snapshot)==64,"Concurrent scan lost keys");Check(Queries.Sum(snapshot)==3200,"Snapshot sum changed during writes");}}
        catch(Exception error){errors.Enqueue(error);}
        foreach(var thread in threads){Check(thread.Join(TimeSpan.FromSeconds(20)),"Worker timed out");}
        if(errors.TryDequeue(out var failure)){throw new AggregateException(errors.Prepend(failure));}
        using var verify=engine.Begin();for(var i=0;i<4;i++){Check(verify.Read(i).Value==130,"Independent writer lost update");}
    }
    private static void Retention(Engine engine){
        using(var old=engine.Begin()){
            for(var i=0;i<15;i++){using var tx=engine.Begin();tx.Set(0,200+i);Check(tx.Commit(),"History update failed");}
            Throws<InvalidOperationException>(()=>engine.Collect());Check(old.Read(0).Value==100,"Retained version lost");
        }
        for(var round=0;round<32;round++){using var batch=engine.Begin();for(var id=0;id<64;id++){batch.Set(id,214);}Check(batch.Commit(),"Compaction preparation failed");}
        Check(engine.Collect()>0,"Compaction did not reclaim history");using var verify=engine.Begin();Check(verify.Read(0).Value==214,"Compaction lost latest value");
    }
    private static void Budget(Engine engine){
        var root=Path.Combine(engine.Root,"budget-"+Guid.NewGuid().ToString("N"));
        using var small=new Engine(engine.Mode,root,false,1,budget:8192);
        Throws<HistoryBudgetException>(()=>{using var old=small.Begin();for(var i=0;i<500;i++){using var tx=small.Begin();tx.Set(0,i);tx.Commit();}});
    }
    private static void AbortedRetention(Engine engine){
        using(var aborted=engine.Begin()){aborted.Set(1,999);}
        if(engine.Mode==Mode.Immediate){Throws<InvalidOperationException>(()=>engine.Collect());}else{engine.Collect();}
        using var read=engine.Begin();Check(read.Read(1).Value==100,"Collection of aborted latest value lost the committed row");
    }
    private static void Fault(Mode mode,Phase phase,string path){
        var committed=false;
        using(var engine=new Engine(mode,path,true,64)){
            engine.Inject=current=>{if(current==phase){throw new IOException("injected "+phase);}};
            try{using var tx=engine.Begin();tx.Set(1,55);tx.Set(1,55,table:1);tx.Commit();throw new InvalidOperationException("Injection missed");}
            catch(CommitFailureException error){committed=error.Committed;}
            catch(IOException) when(phase==Phase.BeforeWal){}
        }
        var records=FileWal.Read(Path.Combine(path,"transactions.wal")).ToArray();
        if(phase==Phase.BeforeWal){Check(records.Length==0,"Pre-WAL failure committed");return;}
        if(mode==Mode.Immediate&&phase==Phase.FirstTableApplied){return;}
        Check(committed&&records.Length==1&&records[0].Draft.Entries.Count==2,"Durable commit not recoverable");
        using var replay=new Engine(Mode.Deferred,path+"-replay",true,64);
        foreach(var record in records){using var tx=replay.Begin();foreach(var entry in record.Draft.Entries){var row=Seamless.Collections.Serializers.Auto<Row>.Deserialize(entry.Row!);tx.Set(row.Id,row.Value,entry.TableId-1,row.Group,row.Deleted!=0);}Check(tx.Commit(),"Replay failed");}
        using var verify=replay.Begin();Check(verify.Read(1).Value==55&&verify.Read(1,1).Value==55,"Replay was partial");
    }
    private static void NegativeControl(string path){
        using var engine=new Engine(Mode.Deferred,path,false,64){DisableValidation=true};
        Throws<InvalidOperationException>(()=>Conflict(engine));Console.WriteLine("PASS NegativeControl: missing validation is detected");
    }
    private static void Collision(string path,bool disk){
        CollisionProbe.Run(path,disk);
    }
    private static void AtomicPublication(Engine engine){
        using var old=engine.Begin();using var reached=new ManualResetEventSlim();using var release=new ManualResetEventSlim();Exception? error=null;
        engine.Inject=phase=>{if(phase==Phase.BeforePublish){reached.Set();if(!release.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException("Publication barrier");}}};
        var thread=new Thread(()=>{try{using var tx=engine.Begin();tx.Set(1,55);tx.Set(1,55,table:1);Check(tx.Commit(),"Writer conflict");}catch(Exception failure){error=failure;}});
        thread.Start();try{Check(reached.Wait(TimeSpan.FromSeconds(10)),"Publish was not reached");Check(old.Read(1).Value==100&&old.Read(1,1).Value==100,"Partial publication");}finally{release.Set();Check(thread.Join(TimeSpan.FromSeconds(10)),"Publish worker stuck");engine.Inject=null;}
        if(error is not null){throw error;}using var current=engine.Begin();Check(current.Read(1).Value==55&&current.Read(1,1).Value==55,"Multi-table publication failed");
    }
    private static void SyncFence(Mode mode,string path){
        using var device=new FakeWalDevice();using var engine=new Engine(mode,path,false,64,device);using var old=engine.Begin();device.BlockSync();Exception? error=null;var completed=0;
        var thread=new Thread(()=>{try{using var tx=engine.Begin();tx.Set(1,55);Check(tx.Commit(),"Sync test conflict");Volatile.Write(ref completed,1);}catch(Exception failure){error=failure;}});
        thread.Start();try{Check(device.SyncEntered.Wait(TimeSpan.FromSeconds(10)),"Sync not reached");Check(Volatile.Read(ref completed)==0&&old.Read(1).Value==100,"Commit visible before durable");}finally{device.ReleaseSync();Check(thread.Join(TimeSpan.FromSeconds(10)),"Sync worker stuck");}
        if(error is not null){throw error;}
        Check(Volatile.Read(ref completed)==1&&old.Read(1).Value==100,"Fence or old snapshot failed");
    }
    private static double Crash(Mode mode,Phase phase,string path){
        Directory.CreateDirectory(path);var executable=Environment.ProcessPath!;var assembly=typeof(Program).Assembly.Location;
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{assembly,"crash-child",path,mode.ToString(),phase.ToString()}){start.ArgumentList.Add(argument);}
        using var process=Process.Start(start)!;var creation=process.StartTime.ToUniversalTime();var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(15_000)){if(process.StartTime.ToUniversalTime()==creation){process.Kill(true);process.WaitForExit();}throw new TimeoutException("Owned crash child did not exit");}
        File.WriteAllText(Path.Combine(path,"child.stdout.log"),stdout.GetAwaiter().GetResult());File.WriteAllText(Path.Combine(path,"child.stderr.log"),stderr.GetAwaiter().GetResult());
        Check(process.ExitCode!=0&&File.Exists(Path.Combine(path,"crash-marker.json")),"Child did not reach its crash marker");
        var watch=Stopwatch.StartNew();var records=FileWal.Read(Path.Combine(path,"transactions.wal")).ToArray();
        if(phase==Phase.BeforeWal){Check(records.Length==0,"Pre-WAL crash has a committed record");return watch.Elapsed.TotalSeconds;}
        Check(records.Length==1&&records[0].Draft.Entries.Count==2,"Durable crash lost the transaction");
        using var replay=new Engine(Mode.Deferred,path+"-recovery",true,64);foreach(var record in records){using var tx=replay.Begin();foreach(var entry in record.Draft.Entries){var row=Seamless.Collections.Serializers.Auto<Row>.Deserialize(entry.Row!);tx.Set(row.Id,row.Value,entry.TableId-1,row.Group,row.Deleted!=0);}Check(tx.Commit(),"Crash replay failed");}
        using var read=replay.Begin();Check(read.Read(1).Value==55&&read.Read(1,1).Value==55,"Crash replay did not restore all participants");return watch.Elapsed.TotalSeconds;
    }
}
