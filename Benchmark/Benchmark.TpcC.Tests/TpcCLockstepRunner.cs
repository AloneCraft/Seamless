using System.Text.Json;
namespace Benchmark.TpcC.Tests;
public sealed class TpcCLockstepRunner{
    internal static Core.TpcCRunOptions Mini=>new(){Warehouses=2,Partitions=2,Items=200,CustomersPerDistrict=30,Terminals=4,Transactions=80,Warmup=5,SaveEvery=20,ReopenEvery=1,KeepStores=true};
    [Fact,Trait("Category","Integration")]public void Run(){
        foreach(var hopping in new[]{false,true}){
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                InDirectory(directory=>{
                    var options=Mini with{Mode=mode,HopThreads=hopping};
                    using var log=new StringWriter();
                    var runner=new Core.TpcCLockstepRunner(options,TestDatabases.Variants,directory,log);
                    var outcome=runner.Run();
                    File.WriteAllText(Path.Combine(directory,"runner.log"),log.ToString());
                    Assert.True(outcome.ExitCode==0,Describe(outcome));
                    Assert.Equal(options.Transactions,outcome.FinalState!.TxnIndex);
                    Assert.Equal(options.Transactions,outcome.FinalState.Counts.Values.Sum());
                    Assert.Equal(5,runner.Sessions.Count);
                    foreach(var session in runner.Sessions){
                        Assert.True(session.PhaseSeconds.ContainsKey("save"));
                        Assert.True(session.PhaseSeconds.ContainsKey("close"));
                        Assert.True(Directory.Exists(session.Directory));
                        Assert.Equal(options.Transactions-options.Warmup,session.Timings.Values.Sum(histogram=>histogram.Count));
                    }
                    using var summary=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"summary.json")));
                    Assert.Equal("ok",summary.RootElement.GetProperty("Result").GetString());
                    Assert.True(File.Exists(Path.Combine(directory,"txn-stats.csv")));
                    Assert.Equal(hopping,log.ToString().Contains("取引所有thread終了:",StringComparison.Ordinal));
                });
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_DetectsFaults(){
        foreach(var hopping in new[]{false,true}){
            foreach(var fault in new[]{"Output","DropWrite","RouteWrong","SkipUndo","ReopenTamper"}){
                InDirectory(directory=>{
                    var index=FindInput(fault=="SkipUndo");
                    var options=Mini with{Transactions=index,SaveEvery=0,InjectFault=$"PF:{index}:{fault}",HopThreads=hopping};
                    if(fault=="ReopenTamper"){
                        options=options with{SaveEvery=index,ReopenEvery=1};
                    }
                    var outcome=new Core.TpcCLockstepRunner(options,TestDatabases.Variants,directory,TextWriter.Null).Run();
                    Assert.Equal(1,outcome.ExitCode);
                    var failure=Assert.IsType<Core.TpcCFailure>(outcome.Failure);
                    var expected=fault switch{
                        "Output"=>Core.TpcCFailureKind.Output,
                        "RouteWrong"=>Core.TpcCFailureKind.State,
                        "ReopenTamper"=>Core.TpcCFailureKind.Reopen,
                        _=>Core.TpcCFailureKind.ReadBack,
                    };
                    Assert.True(failure.Kind==expected,$"{fault}: {Describe(outcome)}");
                    Assert.Equal(index,failure.TxnIndex);
                    Assert.True(Directory.Exists(Path.Combine(directory,"stores")));
                    using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"failure","failure.json")));
                    Assert.Equal(index,report.RootElement.GetProperty("TxnIndex").GetInt64());
                    var lastAgreed=index-1;
                    if(fault is "RouteWrong" or "ReopenTamper"){
                        lastAgreed=index;
                    }
                    Assert.Equal(lastAgreed,report.RootElement.GetProperty("LastAgreedTxn").GetInt64());
                });
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_HopLifecycle(){
        InDirectory(directory=>{
            var trace=new OwnerTrace();
            var variants=TestDatabases.Variants.Select(variant=>(Core.ITpcCVariant)new OwnerVariant(variant,trace)).ToArray();
            using var log=new StringWriter();
            var options=Mini with{HopThreads=true,Transactions=4,Warmup=1,SaveEvery=1,ReopenEvery=1};
            var runner=new Core.TpcCLockstepRunner(options,variants,directory,log);
            var outcome=runner.Run();
            Assert.True(outcome.ExitCode==0,Describe(outcome));
            var worker=Assert.Single(trace.Workers);
            Assert.NotEqual(trace.RunThread,worker.ManagedThreadId);
            Assert.False(worker.IsAlive);
            Assert.Contains("取引所有thread終了:",log.ToString());
            Assert.All(trace.Events.Where(item=>item.Operation is "open" or "save" or "dispose" or "store-dispose"),item=>Assert.Equal(trace.RunThread,item.Thread));
            var transfers=trace.Events.Where(item=>item.Operation is "release" or "acquire" or "execute").ToArray();
            var workerEvents=transfers.Where(item=>item.Thread==worker.ManagedThreadId).ToArray();
            Assert.Equal(4*5*3,workerEvents.Length);
            for(var txn=0;txn<4;txn++){
                var start=Array.FindIndex(transfers,item=>ReferenceEquals(item,workerEvents[txn*15]));
                Assert.All(transfers.Skip(start-5).Take(5),item=>{Assert.Equal("release",item.Operation);Assert.Equal(trace.RunThread,item.Thread);});
                Assert.All(workerEvents.Skip(txn*15).Take(5),item=>Assert.Equal("acquire",item.Operation));
                Assert.All(workerEvents.Skip(txn*15+5).Take(5),item=>Assert.Equal("execute",item.Operation));
                Assert.All(workerEvents.Skip(txn*15+10).Take(5),item=>Assert.Equal("release",item.Operation));
                Assert.All(transfers.Skip(start+15).Take(5),item=>{Assert.Equal("acquire",item.Operation);Assert.Equal(trace.RunThread,item.Thread);});
            }
            Assert.All(runner.Sessions,session=>{
                Assert.True(session.PhaseSeconds.ContainsKey("hop-acquire"));
                Assert.True(session.PhaseSeconds.ContainsKey("hop-release"));
                Assert.Equal(3,session.Timings.Values.Sum(histogram=>histogram.Count));
            });
        });
    }
    [Fact,Trait("Category","Integration")]public void Run_HopFailures(){
        foreach(var stage in new[]{"ARelease","BAcquire","BAcquireAndRelease","BExecute","BRelease","AReacquire","BExecuteAndRelease"}){
            InDirectory(directory=>{
                var trace=new OwnerTrace{FailureStage=stage};
                Core.ITpcCVariant[] variants=[new OwnerVariant(TestDatabases.Variants[2],trace),new OwnerVariant(TestDatabases.Variants[0],trace),new OwnerVariant(TestDatabases.Variants[3],trace)];
                using var log=new OwnerLogWriter(trace);
                var outcome=new Core.TpcCLockstepRunner(Mini with{HopThreads=true,Transactions=3,SaveEvery=0,Shadow=false},variants,directory,log).Run();
                Assert.Equal(2,outcome.ExitCode);
                Assert.Contains("injected "+stage,outcome.Failure!.ToString());
                Assert.False(Assert.Single(trace.Workers).IsAlive);
                Assert.Contains("取引所有thread終了:",log.ToString());
                Assert.True(Directory.Exists(Path.Combine(directory,"stores")));
                if(stage is "BExecuteAndRelease" or "BAcquireAndRelease"){
                    Assert.Contains("injected release cleanup",outcome.Failure.ToString());
                    Assert.Contains("injected disposal cleanup",outcome.Failure.ToString());
                }
                var disposals=trace.Events.Where(item=>item.Operation=="dispose").ToArray();
                Assert.Equal(3,disposals.Length);
                var closeThread=trace.RunThread;
                if(stage is "BRelease" or "BExecuteAndRelease" or "BAcquireAndRelease"){
                    closeThread=Assert.Single(trace.Workers).ManagedThreadId;
                }
                Assert.All(disposals,item=>Assert.Equal(closeThread,item.Thread));
                using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"failure","failure.json")));
                Assert.Equal(0,report.RootElement.GetProperty("LastAgreedTxn").GetInt64());
                Assert.Equal(1,outcome.Failure.TxnIndex);
                var fault=trace.Events.FindIndex(item=>item.Operation=="fault");
                Assert.True(fault>=0);
                Assert.DoesNotContain(trace.Events.Skip(fault+1),item=>item.Operation=="execute");
                Assert.DoesNotContain(trace.Events.Skip(fault+1),item=>item.Operation=="acquire");
                var executes=trace.Events.Count(item=>item.Operation=="execute");
                var expectedExecutes=3;
                if(stage is "ARelease" or "BAcquire" or "BAcquireAndRelease"){expectedExecutes=0;}
                if(stage is "BExecute" or "BExecuteAndRelease"){expectedExecutes=1;}
                Assert.Equal(expectedExecutes,executes);
                if(stage=="ARelease"){
                    Assert.DoesNotContain(trace.Events,item=>item.Operation=="acquire"&&item.Thread!=trace.RunThread);
                }
                if(stage is "BAcquire" or "BAcquireAndRelease"){
                    Assert.DoesNotContain(trace.Events,item=>item.Operation=="acquire"&&item.Instance==3);
                    Assert.Contains(trace.Events,item=>item.Operation=="partial-acquire"&&item.Instance==2);
                }
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_HopStopsAndJoins(){
        foreach(var stop in new[]{false,true}){
            InDirectory(directory=>{
                var trace=new OwnerTrace();
                using var log=new OwnerLogWriter(trace);
                var transactions=0L;
                if(stop){transactions=4;}
                var runner=new Core.TpcCLockstepRunner(Mini with{HopThreads=true,Transactions=transactions,SaveEvery=0,Shadow=false},[new OwnerVariant(TestDatabases.Variants[2],trace)],directory,log);
                using var entered=new ManualResetEventSlim();
                using var release=new ManualResetEventSlim();
                Thread? control=null;
                if(stop){
                    trace.OnExecute=()=>{entered.Set();release.Wait();};
                    control=new Thread(()=>{entered.Wait();runner.RequestStop();release.Set();});
                    control.Start();
                }
                Core.TpcCRunOutcome outcome;
                try{
                    outcome=runner.Run();
                }finally{
                    entered.Set();
                    release.Set();
                    control?.Join();
                }
                var expectedExit=0;
                var expectedCount=0L;
                if(stop){expectedExit=130;expectedCount=1;}
                Assert.True(outcome.ExitCode==expectedExit,Describe(outcome));
                Assert.Equal(expectedCount,outcome.FinalState!.TxnIndex);
                Assert.False(Assert.Single(trace.Workers).IsAlive);
                if(control is not null){
                    Assert.NotEqual(trace.RunThread,control.ManagedThreadId);
                    Assert.NotEqual(Assert.Single(trace.Workers).ManagedThreadId,control.ManagedThreadId);
                    Assert.False(control.IsAlive);
                }
                Assert.Contains("取引所有thread終了:",log.ToString());
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_HopMatchesDirect(){
        InDirectory(directory=>{
            var traces=new List<OwnerTrace>();
            var outcomes=new List<Core.TpcCRunOutcome>();
            foreach(var hopping in new[]{false,true}){
                var trace=new OwnerTrace{CaptureSemantics=true};
                traces.Add(trace);
                var variants=TestDatabases.Variants.Select(variant=>(Core.ITpcCVariant)new OwnerVariant(variant,trace)).ToArray();
                var path=Path.Combine(directory,hopping.ToString());
                Directory.CreateDirectory(path);
                using var log=new OwnerLogWriter(trace);
                var outcome=new Core.TpcCLockstepRunner(Mini with{HopThreads=hopping},variants,path,log).Run();
                Assert.True(outcome.ExitCode==0,Describe(outcome));
                outcomes.Add(outcome);
            }
            Assert.Equal(80*5,traces[0].Semantics.Count);
            Assert.Equal(traces[0].Semantics,traces[1].Semantics);
            Assert.Equal(outcomes[0].FinalState!.Counts.OrderBy(pair=>pair.Key),outcomes[1].FinalState!.Counts.OrderBy(pair=>pair.Key));
            Assert.Equal(5,traces[0].FinalDigests.Count);
            Assert.Equal(traces[0].FinalDigests.Keys.Order(),traces[1].FinalDigests.Keys.Order());
            foreach(var pair in traces[0].FinalDigests){
                Assert.Empty(pair.Value.Differences(traces[1].FinalDigests[pair.Key]));
            }
        });
    }
    [Fact,Trait("Category","Integration")]public void Run_HopCleanupLoggingFailures(){
        foreach(var scenario in new[]{"join","worker-close","direct-close"}){
            InDirectory(directory=>{
                var stage="";
                var message="取引所有thread終了:";
                var hopping=true;
                var transactions=1L;
                if(scenario=="worker-close"){stage="BExecuteAndRelease";message="エンジンを閉じる途中の例外:";}
                if(scenario=="direct-close"){stage="DisposeOnly";message="エンジンを閉じる途中の例外:";hopping=false;transactions=0;}
                var trace=new OwnerTrace{FailureStage=stage};
                using var log=new OwnerLogWriter(trace,message);
                Core.ITpcCVariant[] variants=[new OwnerVariant(TestDatabases.Variants[2],trace),new OwnerVariant(TestDatabases.Variants[0],trace),new OwnerVariant(TestDatabases.Variants[3],trace)];
                var runner=new Core.TpcCLockstepRunner(Mini with{HopThreads=hopping,Transactions=transactions,SaveEvery=0,Shadow=false},variants,directory,log);
                var outcome=runner.Run();
                Assert.Equal(2,outcome.ExitCode);
                Assert.Contains("injected diagnostic failure",outcome.Failure!.ToString());
                if(hopping){
                    Assert.False(Assert.Single(trace.Workers).IsAlive);
                }else{
                    Assert.Empty(trace.Workers);
                }
                Assert.Equal(3,trace.Events.Count(item=>item.Operation=="dispose"));
                Assert.Equal(3,trace.Events.Count(item=>item.Operation=="store-dispose"));
                Assert.All(runner.Sessions,session=>{Assert.Null(session.Engine);Assert.Null(session.Store);});
                var owner=trace.RunThread;
                if(scenario=="worker-close"){
                    owner=Assert.Single(trace.Workers).ManagedThreadId;
                    Assert.Contains("injected BExecuteAndRelease",outcome.Failure.ToString());
                    Assert.Contains("injected release cleanup",outcome.Failure.ToString());
                    Assert.Contains("injected disposal cleanup",outcome.Failure.ToString());
                }
                Assert.All(trace.Events.Where(item=>item.Operation is "dispose" or "store-dispose"),item=>Assert.Equal(owner,item.Thread));
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_DetectsMissingOutput(){
        InDirectory(directory=>{
            Core.ITpcCVariant[] variants=[TestDatabases.Variants[0],new MissingOutputVariant(TestDatabases.Variants[3])];
            var outcome=new Core.TpcCLockstepRunner(Mini with{Transactions=1,SaveEvery=0,Shadow=false},variants,directory,TextWriter.Null).Run();
            Assert.Equal(Core.TpcCFailureKind.Output,outcome.Failure!.Kind);
            Assert.Equal(1,outcome.Failure.TxnIndex);
        });
    }
    [Fact,Trait("Category","Integration")]public void Run_DetectsDisposeFailure(){
        InDirectory(directory=>{
            Core.ITpcCVariant[] variants=[new DisposeFailureVariant(TestDatabases.Variants[0])];
            var outcome=new Core.TpcCLockstepRunner(Mini with{Transactions=0,SaveEvery=0,Shadow=false},variants,directory,TextWriter.Null).Run();
            Assert.Equal(2,outcome.ExitCode);
            Assert.Equal(Core.TpcCFailureKind.Exception,outcome.Failure!.Kind);
            Assert.Contains("injected disposal failure",outcome.Failure.ToString());
            Assert.True(File.Exists(Path.Combine(directory,"failure","failure.json")));
        });
    }
    private static long FindInput(bool rollback){
        var options=Mini;
        var driver=new Core.TpcCDriver(options.Scale,options.Mode,options.Seed,options.Terminals,Core.TpcCDriver.Initial(options.Seed));
        for(var index=1;index<=10_000;index++){
            if(driver.Next() is Core.NewOrderRequest request&&request.Lines.Any(line=>line.ItemId>options.Items)==rollback){
                return index;
            }
        }
        throw new InvalidOperationException("The deterministic stream did not contain the required New-Order.");
    }
    internal static void InDirectory(Action<string> action){
        var directory=Path.Combine(Path.GetTempPath(),"Seamless-TpcC-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            action(directory);
        }catch(Exception error){
            throw new InvalidOperationException("Test failed; evidence retained at "+directory,error);
        }
        try{
            Directory.Delete(directory,true);
        }catch(Exception error){
            throw new InvalidOperationException("Test action passed, but directory cleanup failed at "+directory,error);
        }
    }
    private static string Describe(Core.TpcCRunOutcome outcome)=>outcome.Failure?.ToString()+" "+string.Join(";",outcome.Failure?.Details??[]);
    private sealed record OwnerEvent(string Operation,int Thread,int Instance);
    private sealed record SemanticEvent(string Variant,string Request,string Result);
    private sealed class OwnerTrace{
        public int RunThread{get;}=Environment.CurrentManagedThreadId;
        public List<OwnerEvent> Events{get;}=[];
        public List<Thread> Workers{get;}=[];
        public string FailureStage{get;init;}="";
        public Action? OnExecute{get;set;}
        public int Instances{get;set;}
        public bool CaptureSemantics{get;init;}
        public List<SemanticEvent> Semantics{get;}=[];
        public Dictionary<string,DatabaseDigest> FinalDigests{get;}=new(StringComparer.Ordinal);
        public void Record(string operation,int instance){
            var thread=Thread.CurrentThread;
            this.Events.Add(new(operation,thread.ManagedThreadId,instance));
            if(thread.ManagedThreadId!=this.RunThread&&!this.Workers.Contains(thread)){
                this.Workers.Add(thread);
            }
        }
        public bool Fails(string stage,int instance)=>instance==2&&this.FailureStage==stage;
        public void ThrowFault(string message,int instance){
            this.Record("fault",instance);
            throw new InvalidOperationException("injected "+message);
        }
    }
    private sealed class OwnerLogWriter(OwnerTrace trace,string? failMessage=null):StringWriter{
        private bool thrown;
        public override void WriteLine(string? value){
            if(value is not null&&value.Contains("取引所有thread稼働:",StringComparison.Ordinal)){
                // 実workerからの公開ログを観測し、zero txnでもThread実体の終端を検査する。
                trace.Record("worker-start",0);
            }
            if(!this.thrown&&value is not null&&failMessage is not null&&value.Contains(failMessage,StringComparison.Ordinal)){
                this.thrown=true;
                throw new IOException("injected diagnostic failure");
            }
            base.WriteLine(value);
        }
    }
    private sealed class OwnerComponent(OwnerTrace trace,int instance):Seamless.Execution.IStoreComponent{
        public string Name=>"owner-lifecycle";
        public string Save(){trace.Record("save",instance);return "owner";}
        public void Flush(){}
        public void Load(string state){}
        public void Dispose()=>trace.Record("store-dispose",instance);
    }
    private sealed class OwnerVariant(Core.ITpcCVariant inner,OwnerTrace trace,string? name=null):Core.ITpcCVariant{
        public string Name=>name??inner.Name;
        public string ShortName=>inner.ShortName;
        public Core.ITpcCEngine Open(Seamless.Execution.Store store,Core.TpcCScale scale,Core.TpcCMode mode){
            var instance=++trace.Instances;
            trace.Record("open",instance);
            store.Register(new OwnerComponent(trace,instance));
            return new OwnerEngine(inner.Open(store,scale,mode),trace,instance,this.Name+"/p"+scale.PartitionCount);
        }
        public Core.ITpcCEngine Open(IReadOnlyList<Seamless.Execution.Store> stores,Core.TpcCScale scale,Core.TpcCMode mode){
            var instance=++trace.Instances;
            trace.Record("open",instance);
            foreach(var store in stores){
                store.Register(new OwnerComponent(trace,instance));
            }
            return new OwnerEngine(inner.Open(stores,scale,mode),trace,instance,this.Name+"/p"+scale.PartitionCount);
        }
    }
    private sealed class OwnerEngine:Core.ITpcCEngine{
        private readonly Core.ITpcCEngine inner;
        private readonly OwnerTrace trace;
        private readonly int instance;
        private readonly OwnerDatabase database;
        private readonly string name;
        public OwnerEngine(Core.ITpcCEngine inner,OwnerTrace trace,int instance,string name){
            this.inner=inner;
            this.trace=trace;
            this.instance=instance;
            this.database=new(inner.Database,trace,instance);
            this.name=name;
        }
        public Core.TpcCMode Mode=>this.inner.Mode;
        public Core.ITpcCDatabase Database=>this.database;
        public Core.ITpcCLoadSink Loader=>this.inner.Loader;
        public bool IsFaulted=>this.inner.IsFaulted;
        public Core.TpcCResult Execute(Core.TpcCRequest request){
            this.database.AssertOwner();
            this.trace.Record("execute",this.instance);
            this.trace.OnExecute?.Invoke();
            if(this.trace.Fails("BExecute",this.instance)||this.trace.Fails("BExecuteAndRelease",this.instance)){
                this.trace.ThrowFault(this.trace.FailureStage,this.instance);
            }
            var result=this.inner.Execute(request);
            if(this.trace.CaptureSemantics){
                // derived型の公開値を全て含め、時刻や再現command等の診断値は比較しない。
                this.trace.Semantics.Add(new(this.name,JsonSerializer.Serialize(request,request.GetType()),JsonSerializer.Serialize(result,result.GetType())));
            }
            return result;
        }
        public long SumNextOrderIds()=>this.inner.SumNextOrderIds();
        public void Dispose(){
            this.database.AssertCanDispose();
            this.trace.Record("dispose",this.instance);
            if(this.trace.CaptureSemantics){
                this.trace.FinalDigests[this.name]=Core.TpcCStateDigest.Of(this.inner.Database);
            }
            this.inner.Dispose();
            if(this.trace.Fails("BExecuteAndRelease",this.instance)||this.trace.Fails("BAcquireAndRelease",this.instance)||this.trace.Fails("DisposeOnly",this.instance)){
                throw new InvalidOperationException("injected disposal cleanup");
            }
        }
    }
    private sealed class OwnerDatabase(Core.ITpcCDatabase inner,OwnerTrace trace,int instance):Core.ITpcCDatabase{
        private int owner=trace.RunThread;
        private bool acquired=true;
        public Core.TpcCScale Scale=>inner.Scale;
        public IReadOnlyList<Core.TpcCPartitionBase> PartitionBases=>inner.PartitionBases;
        public void ReleaseHandles(){
            this.AssertCanDispose();
            trace.Record("release",instance);
            if(Environment.CurrentManagedThreadId!=trace.RunThread){
                if(trace.Fails("BRelease",instance)){
                    trace.ThrowFault("BRelease",instance);
                }
                if(trace.Fails("BExecuteAndRelease",instance)||trace.Fails("BAcquireAndRelease",instance)){
                    throw new InvalidOperationException("injected release cleanup");
                }
            }
            inner.ReleaseHandles();
            this.acquired=false;
            if(Environment.CurrentManagedThreadId==trace.RunThread&&trace.Fails("ARelease",instance)){
                trace.ThrowFault("ARelease",instance);
            }
        }
        public void AcquireHandles(){
            Assert.False(this.acquired);
            trace.Record("acquire",instance);
            this.owner=Environment.CurrentManagedThreadId;
            if(this.owner!=trace.RunThread&&(trace.Fails("BAcquire",instance)||trace.Fails("BAcquireAndRelease",instance))){
                this.acquired=true;
                inner.PartitionBases[0].AcquireHandles();
                trace.Record("partial-acquire",instance);
                trace.ThrowFault(trace.FailureStage,instance);
            }
            inner.AcquireHandles();
            this.acquired=true;
            if(this.owner==trace.RunThread&&trace.Fails("AReacquire",instance)){
                trace.ThrowFault("AReacquire",instance);
            }
        }
        public void AssertOwner(){
            Assert.True(this.acquired);
            Assert.Equal(this.owner,Environment.CurrentManagedThreadId);
        }
        public void AssertCanDispose(){
            if(this.acquired){
                Assert.Equal(this.owner,Environment.CurrentManagedThreadId);
            }
        }
        public void BeginBulkLoad()=>inner.BeginBulkLoad();
        public void EndBulkLoad()=>inner.EndBulkLoad();
        public void Dispose(){
            this.AssertCanDispose();
            inner.Dispose();
        }
    }
    private sealed class DisposeFailureVariant(Core.ITpcCVariant inner):Core.ITpcCVariant{
        public string Name=>inner.Name;
        public string ShortName=>inner.ShortName;
        public Core.ITpcCEngine Open(Seamless.Execution.Store store,Core.TpcCScale scale,Core.TpcCMode mode)=>new DisposeFailureEngine(inner.Open(store,scale,mode));
        public Core.ITpcCEngine Open(IReadOnlyList<Seamless.Execution.Store> stores,Core.TpcCScale scale,Core.TpcCMode mode)=>new DisposeFailureEngine(inner.Open(stores,scale,mode));
    }
    private sealed class DisposeFailureEngine(Core.ITpcCEngine inner):Core.ITpcCEngine{
        public Core.TpcCMode Mode=>inner.Mode;
        public Core.ITpcCDatabase Database=>inner.Database;
        public Core.ITpcCLoadSink Loader=>inner.Loader;
        public bool IsFaulted=>inner.IsFaulted;
        public Core.TpcCResult Execute(Core.TpcCRequest request)=>inner.Execute(request);
        public long SumNextOrderIds()=>inner.SumNextOrderIds();
        public void Dispose(){
            inner.Dispose();
            throw new InvalidOperationException("injected disposal failure");
        }
    }
    private sealed class MissingOutputVariant(Core.ITpcCVariant inner):Core.ITpcCVariant{
        public string Name=>inner.Name;
        public string ShortName=>inner.ShortName;
        public Core.ITpcCEngine Open(Seamless.Execution.Store store,Core.TpcCScale scale,Core.TpcCMode mode)=>new MissingOutputEngine(inner.Open(store,scale,mode));
        public Core.ITpcCEngine Open(IReadOnlyList<Seamless.Execution.Store> stores,Core.TpcCScale scale,Core.TpcCMode mode)=>new MissingOutputEngine(inner.Open(stores,scale,mode));
    }
    private sealed class MissingOutputEngine(Core.ITpcCEngine inner):Core.ITpcCEngine{
        public Core.TpcCMode Mode=>inner.Mode;
        public Core.ITpcCDatabase Database=>inner.Database;
        public Core.ITpcCLoadSink Loader=>inner.Loader;
        public bool IsFaulted=>inner.IsFaulted;
        public Core.TpcCResult Execute(Core.TpcCRequest request){
            inner.Execute(request);
            return null!;
        }
        public long SumNextOrderIds()=>inner.SumNextOrderIds();
        public void Dispose()=>inner.Dispose();
    }
}
