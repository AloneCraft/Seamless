using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class EmptySourceIntegration{
    public static TheoryData<SourceTarget> Sources=>new(Enum.GetValues<SourceTarget>());
    [Theory,MemberData(nameof(Sources))]
    public async Task EmptyInputUsesTheActualSourceAndAllFourPaths(SourceTarget source){
        var ir=CorpusChainBuilder.Build(new("empty-"+source,"test",["Where","Count"]),source,0);
        var request=new WorkerRunRequest("empty-"+source,Seed256.Parse("42"),1,2,2,0,OperatorSetMode.Maximum,[source],new("empty-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,QueryCapabilityProfile.CurrentProfileVersion.ToString(),BuiltInOperatorCatalog.OperatorSetVersion.ToString());
        var result=await new DynamicWorkerExecutionRunner().ExecuteAsync(request,ir,0,source,0,CancellationToken.None);
        Assert.True(result.Status==WorkerExecutionStatus.Succeeded,result.DiagnosticsJson);
        Assert.True(result.AssemblyUnloaded);
        Assert.NotNull(result.InputHash);
    }
}
