using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

public sealed class 動的実行調停サービス{
    private static readonly TimeSpan HangTimeout=TimeSpan.FromSeconds(30);

    [Fact,Trait("Category","Integration")]
    public async Task 実行Async(){
        {
            // 成功したsessionだけを除去し、終了後の再取消では呼び出さない。
            await VerifySessionOwnershipAsync(failStop:false,shrink:false);
        }
        {
            // 停止失敗後は元Taskと再取消の双方が、保持した同じsessionの失敗を返す。
            await VerifySessionOwnershipAsync(failStop:true,shrink:false);
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task Queryを縮小Async(){
        {
            // 縮小候補の停止成功後も、終了したsessionを再取消しない。
            await VerifySessionOwnershipAsync(failStop:false,shrink:true);
        }
        {
            // 縮小の外側finallyでも、失敗したsessionを除去しない。
            await VerifySessionOwnershipAsync(failStop:true,shrink:true);
        }
    }

    private static async Task VerifySessionOwnershipAsync(bool failStop,bool shrink){
        var temporaryRoot=DynamicDatabase.一時ルートを作成();
        var databasePath=Path.Combine(temporaryRoot,DynamicInfrastructureSchema.DatabaseFileName);
        var client=new ControlledWorkerClient(failStop);
        global::Benchmark.Dynamic.UI.動的実行調停サービス? service=null;
        Task? run=null;
        try{
            string? parentQueryID=null;
            if(shrink){
                parentQueryID=await SaveShrinkParentAsync(databasePath);
            }
            service=new(
                databasePath,
                Path.Combine(AppContext.BaseDirectory,"controlled-worker.exe"),
                new WorkerMeasurementProfile("Ownership-boundary-v1",0,1,0,1),
                WorkerClient:client);
            if(shrink){
                run=service.Queryを縮小Async(parentQueryID!,_=>{},_=>{});
            }else{
                run=service.実行Async(
                    new global::Benchmark.Dynamic.UI.動的実行設定(
                        "ownership-"+Guid.NewGuid().ToString("N"),
                        1,1,4,1,Seed256.FromUInt64(42),OperatorSetMode.Common,[SourceTarget.SystemArray]),
                    _=>{},_=>{});
            }
            await client.Session.ReaderStarted.Task.WaitAsync(HangTimeout);
            var cancelFailure=await Record.ExceptionAsync(()=>service.キャンセルAsync().WaitAsync(HangTimeout));
            var runFailure=await Record.ExceptionAsync(()=>run.WaitAsync(HangTimeout));
            var callsBeforeRepeat=client.Session.CancelCalls;
            var repeatFailure=await Record.ExceptionAsync(()=>service.キャンセルAsync().WaitAsync(HangTimeout));
            Assert.Equal(1,client.StartCalls);
            if(failStop){
                Assert.Equal(1,callsBeforeRepeat);
                Assert.Same(client.Session.StopFailure,cancelFailure);
                Assert.Same(client.Session.StopFailure,runFailure);
                Assert.Same(client.Session.StopFailure,repeatFailure);
                Assert.Equal(callsBeforeRepeat+1,client.Session.CancelCalls);
            }else{
                Assert.Null(cancelFailure);
                if(shrink){
                    Assert.IsAssignableFrom<OperationCanceledException>(runFailure);
                    Assert.InRange(callsBeforeRepeat,0,1);
                }else{
                    Assert.Null(runFailure);
                    Assert.Equal(1,callsBeforeRepeat);
                }
                Assert.Null(repeatFailure);
                Assert.Equal(callsBeforeRepeat,client.Session.CancelCalls);
            }
        }finally{
            // fakeはOS Processを持たない。登録保持を検証した後のテスト後処理だけで解放する。
            client.Session.ReleaseForCleanup();
            if(run is not null){
                _=await Record.ExceptionAsync(()=>run.WaitAsync(HangTimeout));
            }
            try{
                if(service is not null){
                    await service.DisposeAsync().AsTask().WaitAsync(HangTimeout);
                }
            }finally{
                DynamicDatabase.一時ルートを削除(temporaryRoot);
            }
        }
    }

    private static async Task<string> SaveShrinkParentAsync(string databasePath){
        var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(databasePath);
        database.InitializeIfMissing();
        await using var writer=new DynamicSingleWriterQueue(database);
        var repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer);
        var seed=Seed256.FromUInt64(42);
        const string runID="ownership-parent";
        const string queryID="ownership-parent-q000000";
        var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=seed,
            QueryIndex=0,
            QueryId=queryID,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=2,
            MaximumDepth=2,
            InputElementCount=4,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.ToArray],
        });
        var settings=new global::Benchmark.Dynamic.UI.動的実行条件Snapshot(
            1,1,2,4,2,seed.ToString(),OperatorSetMode.Common,[SourceTarget.SystemArray],
            new WorkerMeasurementProfile("Ownership-parent-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion,
            BuiltInOperatorCatalog.OperatorSetVersion,
            "ownership-parent");
        var now=DateTimeOffset.UtcNow;
        await repository.SaveCheckpointAsync(new DynamicRunCheckpoint(
            new DynamicRunRecord(runID,"Failed",42,"Ownership-parent-v1",
                JsonSerializer.Serialize(settings,DynamicWorkerProtocol.JsonOptions),now,now),
            Sources:[new DynamicRunSourceRecord(runID,nameof(SourceTarget.SystemArray),true,0)],
            Queries:[new DynamicQueryRecord(queryID,runID,0,"SharedIR",42,
                QueryIrJson.Serialize(query),string.Empty,QueryIrJson.ComputeHash(query),"Failed")],
            SourceExecutions:[new DynamicSourceExecutionRecord("ownership-parent-execution",queryID,
                nameof(SourceTarget.SystemArray),1,0,nameof(WorkerExecutionStatus.CompilationFailed),Error:"[]")]));
        return queryID;
    }

    private sealed class ControlledWorkerClient(bool failStop):IWorkerProcessClient{
        public ControlledWorkerSession Session{get;}=new(failStop);
        public int StartCalls{get;private set;}

        public IWorkerProcessSession Start(string workerPath,IReadOnlyList<string>? arguments=null,string? workingDirectory=null){
            this.StartCalls++;
            return this.Session;
        }
    }

    private sealed class ControlledWorkerSession:IWorkerProcessSession{
        private readonly bool failStop;
        private readonly Task<bool> failedStop;
        private readonly TaskCompletionSource cancelled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int cancelCalls;
        private int releasedForCleanup;
        private string runID=string.Empty;

        public ControlledWorkerSession(bool failStop){
            this.failStop=failStop;
            if(failStop){
                this.failedStop=Task.FromException<bool>(this.StopFailure);
            }else{
                this.failedStop=Task.FromResult(false);
            }
        }

        public TaskCompletionSource ReaderStarted{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IOException StopFailure{get;}=new("Controlled Worker stop failure.");
        public int CancelCalls=>Volatile.Read(ref this.cancelCalls);
        public int ProcessID=>10001;
        public bool HasExited{get;private set;}

        public Task SendAsync<T>(string requestID,string messageType,T payload,CancellationToken cancellationToken=default){
            if(payload is WorkerRunRequest request){
                this.runID=request.RunId;
            }
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<WorkerMessageEnvelope> ReadAllAsync(
            [EnumeratorCancellation]CancellationToken cancellationToken=default){
            this.ReaderStarted.TrySetResult();
            await this.cancelled.Task.WaitAsync(cancellationToken);
            yield return DynamicWorkerProtocol.Create(
                "controlled-run",
                WorkerMessageTypes.RunCanceled,
                new WorkerRunCompletedEvent(this.runID,WorkerExecutionStatus.Canceled,0,0,DateTimeOffset.UtcNow));
        }

        public Task<bool> CancelAsync(string requestID){
            Interlocked.Increment(ref this.cancelCalls);
            this.cancelled.TrySetResult();
            return this.Stop();
        }

        public Task<bool> ShutdownAsync(string requestID)=>this.Stop();
        public Task<string> GetStandardErrorAsync()=>Task.FromResult(string.Empty);
        public ValueTask DisposeAsync()=>new(this.Stop());

        private Task<bool> Stop(){
            if(this.failStop&&Volatile.Read(ref this.releasedForCleanup)==0){
                return this.failedStop;
            }
            this.HasExited=true;
            return Task.FromResult(false);
        }

        public void ReleaseForCleanup(){
            Volatile.Write(ref this.releasedForCleanup,1);
            this.cancelled.TrySetResult();
            _=this.failedStop.Exception;
        }
    }
}
