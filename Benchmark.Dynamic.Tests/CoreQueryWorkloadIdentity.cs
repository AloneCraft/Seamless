using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class QueryWorkloadIdentityTests{
    [Fact]
    public void WorkloadIgnoresRunIdentityButPreservesProcessing(){
        var first=CorpusChainBuilder.Build(new("identity","test",["Where","Count"]),SourceTarget.SystemArray,16);
        var otherRun=first with{QueryId="other-run-query",Seed=Seed256.Parse("999"),Root=first.Root with{NodeId="other-node"}};
        Assert.NotEqual(QueryIrJson.ComputeHash(first),QueryIrJson.ComputeHash(otherRun));
        Assert.Equal(QueryWorkloadIdentity.Compute(first),QueryWorkloadIdentity.Compute(otherRun));
        var changed=CorpusChainBuilder.Build(new("changed","test",["Where","Sum"]),SourceTarget.SystemArray,16);
        Assert.NotEqual(QueryWorkloadIdentity.Compute(first),QueryWorkloadIdentity.Compute(changed));
    }
}
