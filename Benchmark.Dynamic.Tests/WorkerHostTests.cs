using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

public sealed class WorkerHostTests{
    [Fact,Trait("Category","Integration")]
    public async Task RunAsync_PingAndShutdownProduceVersionedJsonLines(){
        var input=string.Join(
            Environment.NewLine,
            DynamicWorkerProtocol.Serialize(DynamicWorkerProtocol.Create(
                "ping-1",
                WorkerMessageTypes.Ping,
                new WorkerPingRequest(DateTimeOffset.UnixEpoch))),
            DynamicWorkerProtocol.Serialize(DynamicWorkerProtocol.Create(
                "shutdown-1",
                WorkerMessageTypes.Shutdown,
                new WorkerControlRequest(string.Empty,DateTimeOffset.UnixEpoch))),
            string.Empty);
        using var reader=new StringReader(input);
        using var writer=new StringWriter();
        using var error=new StringWriter();
        var host=new WorkerHost(reader,writer,error);

        var exitCode=await host.RunAsync();

        Assert.Equal(0,exitCode);
        var messages=writer.ToString()
            .Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries)
            .Select(DynamicWorkerProtocol.Deserialize)
            .ToArray();
        Assert.Equal(2,messages.Length);
        Assert.Equal(WorkerMessageTypes.WorkerStarted,messages[0].MessageType);
        Assert.Equal(WorkerMessageTypes.Pong,messages[1].MessageType);
        Assert.Empty(error.ToString());
    }

    [Fact,Trait("Category","Integration")]
    public async Task RunAsync_SourceFailureDoesNotStopFollowingSource(){
        var request=new WorkerRunRequest(
            "run-1",
            Seed256.FromUInt64(10),
            1,
            1,
            1,
            8,
            OperatorSetMode.Common,
            [SourceTarget.SystemArray,SourceTarget.SystemList],
            new WorkerMeasurementProfile("test",0,1,0,1),
            "test",
            "test",
            "test");
        var input=string.Join(
            Environment.NewLine,
            DynamicWorkerProtocol.Serialize(DynamicWorkerProtocol.Create(
                "run-request",
                WorkerMessageTypes.StartRun,
                request)),
            string.Empty);
        using var reader=new StringReader(input);
        using var writer=new StringWriter();
        using var error=new StringWriter();
        var executor=new FakeExecutionRunner();
        var host=new WorkerHost(reader,writer,error,executor);

        var exitCode=await host.RunAsync();

        Assert.Equal(0,exitCode);
        var messages=writer.ToString()
            .Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries)
            .Select(DynamicWorkerProtocol.Deserialize)
            .ToArray();
        var completed=messages
            .Where(message=>message.MessageType==WorkerMessageTypes.SourceExecutionCompleted)
            .Select(DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>)
            .ToArray();
        Assert.Equal(2,completed.Length);
        Assert.Equal(WorkerExecutionStatus.ExecutionFailed,completed[0].Status);
        Assert.Equal(WorkerExecutionStatus.Succeeded,completed[1].Status);
    }

    [Fact,Trait("Category","Integration")]
    public async Task RunAsync_ResumeSkipsCompletedSourceAndCarriesCounters(){
        var request=new WorkerRunRequest(
            "resume-run-1",
            Seed256.FromUInt64(10),
            1,
            1,
            1,
            8,
            OperatorSetMode.Common,
            [SourceTarget.SystemArray,SourceTarget.SystemList],
            new WorkerMeasurementProfile("test",0,1,0,1),
            "test",
            "test",
            "test",
            ResumeState:new WorkerRunResumeState(
                [new(0,SourceTarget.SystemArray)],
                FailedSourceExecutions:1,
                ExecutionFailures:1));
        var input=string.Join(
            Environment.NewLine,
            DynamicWorkerProtocol.Serialize(DynamicWorkerProtocol.Create(
                "resume-request",
                WorkerMessageTypes.StartRun,
                request)),
            string.Empty);
        using var reader=new StringReader(input);
        using var writer=new StringWriter();
        using var error=new StringWriter();
        var host=new WorkerHost(reader,writer,error,new FakeExecutionRunner());

        var exitCode=await host.RunAsync();

        Assert.Equal(0,exitCode);
        var messages=writer.ToString()
            .Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries)
            .Select(DynamicWorkerProtocol.Deserialize)
            .ToArray();
        var completed=Assert.Single(messages
            .Where(message=>message.MessageType==WorkerMessageTypes.SourceExecutionCompleted)
            .Select(DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>));
        Assert.Equal(SourceTarget.SystemList,completed.SourceTarget);
        var runCompleted=Assert.Single(messages
            .Where(message=>message.MessageType==WorkerMessageTypes.RunCompleted)
            .Select(DynamicWorkerProtocol.DeserializePayload<WorkerRunCompletedEvent>));
        Assert.Equal(2,runCompleted.CompletedSourceExecutions);
        Assert.Equal(1,runCompleted.FailedSourceExecutions);
    }

    private static async Task<(int ExitCode,WorkerMessageEnvelope[] Messages)> RunExhaustiveAsync(WorkerRunRequest request){
        var input=string.Join(
            Environment.NewLine,
            DynamicWorkerProtocol.Serialize(DynamicWorkerProtocol.Create("run-request",WorkerMessageTypes.StartRun,request)),
            string.Empty);
        using var reader=new StringReader(input);
        using var writer=new StringWriter();
        using var error=new StringWriter();
        var host=new WorkerHost(reader,writer,error,new SucceedingExecutionRunner());
        var exitCode=await host.RunAsync();
        var messages=writer.ToString()
            .Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries)
            .Select(DynamicWorkerProtocol.Deserialize)
            .ToArray();
        return (exitCode,messages);
    }

    [Fact,Trait("Category","Integration")]
    public async Task RunAsync_全組み合わせモードは中間演算子と終端の組み合わせを番号順に決定的に生成する(){
        WorkerRunRequest Create()=>new(
            "run-exhaustive",
            Seed256.FromUInt64(77),
            6,
            2,
            2,
            8,
            OperatorSetMode.Common,
            [SourceTarget.SystemArray],
            new WorkerMeasurementProfile("test",0,1,0,1),
            "test",
            "test",
            "test",
            GenerationMode:QueryGenerationMode.Exhaustive,
            ExhaustiveIntermediateCount:1,
            ExhaustiveTerminalIds:[QueryOperatorIds.ToArray,QueryOperatorIds.Count]);

        var first=await RunExhaustiveAsync(Create());
        var second=await RunExhaustiveAsync(Create());

        Assert.Equal(0,first.ExitCode);
        string[] Signatures((int ExitCode,WorkerMessageEnvelope[] Messages) result)=>result.Messages
            .Where(static message=>message.MessageType==WorkerMessageTypes.QueryStarted)
            .Select(DynamicWorkerProtocol.DeserializePayload<WorkerQueryStartedEvent>)
            .Select(static started=>{
                var document=QueryIrJson.Deserialize(started.IrJson);
                var unary=Assert.IsAssignableFrom<QueryNode>(document.Root.Input);
                return $"{unary.OperatorId}>{document.Root.OperatorId}";
            })
            .ToArray();
        var signatures=Signatures(first);
        Assert.Equal(6,signatures.Length);
        Assert.Equal(6,signatures.Distinct().Count());
        Assert.All(signatures,static signature=>Assert.Matches(@"^[^>]+>(toArray|count)$",signature));
        Assert.Equal(signatures,Signatures(second));
        Assert.Contains(first.Messages,static message=>message.MessageType==WorkerMessageTypes.RunCompleted);
    }

    [Fact,Trait("Category","Integration")]
    public async Task RunAsync_全組み合わせモードは演算子数が合わない要求を実行しない(){
        var request=new WorkerRunRequest(
            "run-exhaustive-invalid",
            Seed256.FromUInt64(77),
            2,
            3,
            3,
            8,
            OperatorSetMode.Common,
            [SourceTarget.SystemArray],
            new WorkerMeasurementProfile("test",0,1,0,1),
            "test",
            "test",
            "test",
            GenerationMode:QueryGenerationMode.Exhaustive,
            ExhaustiveIntermediateCount:1);

        var result=await RunExhaustiveAsync(request);

        Assert.NotEqual(0,result.ExitCode);
        Assert.DoesNotContain(result.Messages,static message=>message.MessageType==WorkerMessageTypes.QueryStarted);
    }

    private sealed class SucceedingExecutionRunner:IWorkerExecutionRunner{
        public Task<WorkerSourceExecutionCompletedEvent> ExecuteAsync(
            WorkerRunRequest request,
            QueryIrDocument query,
            int queryIndex,
            SourceTarget target,
            int sourceOrdinal,
            CancellationToken cancellationToken){
            return Task.FromResult(new WorkerSourceExecutionCompletedEvent(
                request.RunId,
                query.QueryId,
                queryIndex,
                target,
                WorkerExecutionStatus.Succeeded,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                true,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeExecutionRunner:IWorkerExecutionRunner{
        public Task<WorkerSourceExecutionCompletedEvent> ExecuteAsync(
            WorkerRunRequest request,
            QueryIrDocument query,
            int queryIndex,
            SourceTarget target,
            int sourceOrdinal,
            CancellationToken cancellationToken){
            var status=target==SourceTarget.SystemArray
                ?WorkerExecutionStatus.ExecutionFailed
                :WorkerExecutionStatus.Succeeded;
            return Task.FromResult(new WorkerSourceExecutionCompletedEvent(
                request.RunId,
                query.QueryId,
                queryIndex,
                target,
                status,
                string.Empty,
                string.Empty,
                JsonSerializer.Serialize(new{status},DynamicWorkerProtocol.JsonOptions),
                null,
                null,
                null,
                true,
                DateTimeOffset.UtcNow));
        }
    }
}
