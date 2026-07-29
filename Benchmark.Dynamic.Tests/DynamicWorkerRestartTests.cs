using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Runtime.CompilerServices;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicWorkerRestartTests{
    [Fact,Trait("Category","Integration")]
    public async Task ALC解放失敗3件後に新Workerで未完了SourceExecutionだけを継続する(){
        var temporaryRoot=DynamicDatabase.一時ルートを作成();
        var databasePath=Path.Combine(temporaryRoot,DynamicInfrastructureSchema.DatabaseFileName);
        var runID="restart-"+Guid.NewGuid().ToString("N");
        var client=new 再起動確認Client();
        try{
            await using(var service=new global::Benchmark.Dynamic.動的実行調停サービス(
                databasePath,
                typeof(DynamicWorkerRestartTests).Assembly.Location,
                new WorkerMeasurementProfile("Restart-v1",0,1,0,1),
                client)){
                var history=await service.実行Async(
                    new global::Benchmark.Dynamic.動的実行設定(
                        runID,
                        4,
                        1,
                        8,
                        1,
                        Seed256.Parse("0xA1C"),
                        OperatorSetMode.Common,
                        [SourceTarget.SystemArray]),
                    _=>{},
                    _=>{});

                Assert.Equal("Completed",history.状態);
            }

            Assert.Equal(2,client.StartCount);
            Assert.Equal(2,client.Requests.Count);
            Assert.Null(client.Requests[0].ResumeState);
            var resume=Assert.IsType<WorkerRunResumeState>(client.Requests[1].ResumeState);
            Assert.Equal(3,resume.CompletedSourceExecutions.Count);
            Assert.Equal(3,resume.FailedSourceExecutions);
            Assert.Equal([0,1,2],resume.CompletedSourceExecutions.Select(static item=>item.QueryIndex).Order());

            var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(databasePath);
            await using var writer=new DynamicSingleWriterQueue(database);
            var repository=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer);
            var snapshot=repository.GetRunSnapshot(runID);
            Assert.NotNull(snapshot);
            Assert.Equal("Completed",snapshot.Run.Status);
            Assert.Equal(4,snapshot.SourceExecutions.Count);
            Assert.Equal(
                ["AssemblyUnloadFailed","AssemblyUnloadFailed","AssemblyUnloadFailed","Succeeded"],
                snapshot.SourceExecutions.OrderBy(static execution=>execution.ExecutionOrder)
                    .Select(static execution=>execution.Status));
            Assert.Single(snapshot.RunEvents,static runEvent=>runEvent.Kind=="WorkerRestart");
        }finally{
            DynamicDatabase.一時ルートを削除(temporaryRoot);
        }
    }

    private sealed class 再起動確認Client:IWorkerProcessClient{
        private int startCount;

        public int StartCount=>this.startCount;
        public List<WorkerRunRequest> Requests{get;}=[];

        public IWorkerProcessSession Start(
            string workerPath,
            IReadOnlyList<string>? arguments=null,
            string? workingDirectory=null){
            var generation=Interlocked.Increment(ref this.startCount);
            return new 再起動確認Session(generation,this.Requests);
        }
    }

    private sealed class 再起動確認Session:IWorkerProcessSession{
        private const string MatchingResultJson="{\"exceptions\":{},\"comparisons\":{\"fLinqUnoptimized\":{\"isMatch\":true},\"fLinq\":{\"isMatch\":true},\"zLinq\":{\"isMatch\":true}}}";
        private readonly int generation;
        private readonly List<WorkerRunRequest> requests;
        private WorkerRunRequest? request;
        private string requestID=string.Empty;

        public 再起動確認Session(int generation,List<WorkerRunRequest> requests){
            this.generation=generation;
            this.requests=requests;
        }

        public int ProcessID=>10_000+this.generation;
        public bool HasExited{get;private set;}

        public Task SendAsync<T>(
            string requestID,
            string messageType,
            T payload,
            CancellationToken cancellationToken=default){
            cancellationToken.ThrowIfCancellationRequested();
            if(messageType==WorkerMessageTypes.StartRun){
                this.request=payload as WorkerRunRequest
                    ??throw new InvalidDataException("StartRun payload is not WorkerRunRequest.");
                this.requestID=requestID;
                this.requests.Add(this.request);
            }
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<WorkerMessageEnvelope> ReadAllAsync(
            [EnumeratorCancellation]CancellationToken cancellationToken=default){
            var run=this.request??throw new InvalidOperationException("StartRun was not sent.");
            await Task.Yield();
            yield return Event(WorkerMessageTypes.WorkerStarted,new WorkerStartedEvent(
                this.ProcessID,
                "fake-worker-v"+this.generation,
                Environment.Version.ToString(),
                DateTimeOffset.UtcNow));
            yield return Event(WorkerMessageTypes.RunStarted,new WorkerRunStartedEvent(
                run.RunId,
                DateTimeOffset.UtcNow,
                run.QueryCount*run.SourceTargets.Count));

            int[] queryIndexes=this.generation==1?[0,1,2]:[3];
            var completed=this.generation==1?0:3;
            foreach(var queryIndex in queryIndexes){
                cancellationToken.ThrowIfCancellationRequested();
                var query=CreateQuery(run,queryIndex);
                var irJson=QueryIrJson.Serialize(query);
                yield return Event(WorkerMessageTypes.QueryStarted,new WorkerQueryStartedEvent(
                    run.RunId,
                    query.QueryId,
                    queryIndex,
                    query.Seed,
                    irJson,
                    1));
                yield return Event(WorkerMessageTypes.SourceExecutionStarted,new WorkerSourceExecutionStartedEvent(
                    run.RunId,
                    query.QueryId,
                    queryIndex,
                    SourceTarget.SystemArray,
                    0,
                    DateTimeOffset.UtcNow));
                var succeeded=this.generation!=1;
                yield return Event(WorkerMessageTypes.SourceExecutionCompleted,new WorkerSourceExecutionCompletedEvent(
                    run.RunId,
                    query.QueryId,
                    queryIndex,
                    SourceTarget.SystemArray,
                    succeeded?WorkerExecutionStatus.Succeeded:WorkerExecutionStatus.AssemblyUnloadFailed,
                    string.Empty,
                    string.Empty,
                    MatchingResultJson,
                    "[]",
                    null,
                    null,
                    succeeded,
                    DateTimeOffset.UtcNow));
                completed++;
                yield return Event(WorkerMessageTypes.Progress,new WorkerProgressEvent(
                    run.RunId,
                    completed,
                    4,
                    0,
                    0,
                    0,
                    0,
                    false));
            }

            if(this.generation==1){
                yield return Event(WorkerMessageTypes.Fatal,new WorkerFatalEvent(
                    run.RunId,
                    WorkerFatalErrorTypes.AssemblyUnloadFailure,
                    "simulated three consecutive unload failures",
                    null,
                    DateTimeOffset.UtcNow));
            }else{
                yield return Event(WorkerMessageTypes.RunCompleted,new WorkerRunCompletedEvent(
                    run.RunId,
                    WorkerExecutionStatus.Succeeded,
                    4,
                    3,
                    DateTimeOffset.UtcNow));
            }

            WorkerMessageEnvelope Event<T>(string messageType,T payload){
                return DynamicWorkerProtocol.Create(this.requestID,messageType,payload);
            }
        }

        public Task<bool> CancelAsync(string requestID){
            this.HasExited=true;
            return Task.FromResult(false);
        }

        public Task<bool> ShutdownAsync(string requestID){
            this.HasExited=true;
            return Task.FromResult(false);
        }

        public Task<string> GetStandardErrorAsync(){
            return Task.FromResult(string.Empty);
        }

        public ValueTask DisposeAsync(){
            this.HasExited=true;
            return ValueTask.CompletedTask;
        }

        private static QueryIrDocument CreateQuery(WorkerRunRequest run,int queryIndex){
            return new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=run.BaseSeed,
                QueryIndex=queryIndex,
                QueryId=$"{run.RunId}-q{queryIndex:D6}",
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=1,
                MaximumDepth=1,
                InputElementCount=run.InputElementCount,
                EnableBinaryOperators=false,
                EnableNestedOperators=false,
                AllowedOperatorIds=[QueryOperatorIds.Count],
            });
        }
    }
}
