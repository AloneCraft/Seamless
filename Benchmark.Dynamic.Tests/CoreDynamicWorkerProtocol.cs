using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicWorkerProtocolTests{
    [Fact,Trait("Category","Unit")]public void Serialize(){
        var request=new WorkerRunRequest(
            "run-1",
            Core.Seed256.Parse("0x2A"),
            10,
            8,
            8,
            1_024,
            OperatorSetMode.Common,
            [SourceTarget.SystemArray],
            new(),
            "generator-v1",
            "capability-v1",
            "operators-v1",
            [new WorkerReplayQuery(0,"{}","parent",null,7,"Shrink")]);
        var envelope=Core.DynamicWorkerProtocol.Create("request-1",WorkerMessageTypes.StartRun,request);
        var line=Core.DynamicWorkerProtocol.Serialize(envelope);
        Assert.DoesNotContain("\r",line,StringComparison.Ordinal);
        Assert.DoesNotContain("\n",line,StringComparison.Ordinal);
        var parsed=Core.DynamicWorkerProtocol.Deserialize(line);
        var payload=Core.DynamicWorkerProtocol.DeserializePayload<WorkerRunRequest>(parsed);
        Assert.Equal(request with{
            SourceTargets=payload.SourceTargets,
            ReplayQueries=payload.ReplayQueries,
        },payload);
        Assert.Equal(request.SourceTargets,payload.SourceTargets);
        Assert.Equal("Shrink",Assert.Single(payload.ReplayQueries!).DerivationKind);
        Assert.Equal(Core.DynamicWorkerProtocol.CurrentVersion,parsed.ProtocolVersion);
    }

    [Fact,Trait("Category","Unit")]public void Deserialize(){
        {
            var payload=JsonSerializer.SerializeToElement(new{});
            var incompatible=new WorkerMessageEnvelope(
                Core.DynamicWorkerProtocol.CurrentVersion+1,
                "request-1",
                WorkerMessageTypes.Ping,
                payload);
            var json=JsonSerializer.Serialize(incompatible,Core.DynamicWorkerProtocol.JsonOptions);
            Assert.Throws<DynamicWorkerProtocolVersionException>(()=>Core.DynamicWorkerProtocol.Deserialize(json));
        }
        {
            Assert.ThrowsAny<DynamicWorkerProtocolException>(()=>Core.DynamicWorkerProtocol.Deserialize("{}"));
            Assert.Throws<DynamicWorkerProtocolException>(()=>Core.DynamicWorkerProtocol.Deserialize("{}\n{}"));
            Assert.Throws<ArgumentException>(()=>Core.DynamicWorkerProtocol.Create("request\n1",WorkerMessageTypes.Ping,new{}));
        }
    }
}
