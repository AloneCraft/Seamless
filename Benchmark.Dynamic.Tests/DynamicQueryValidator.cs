using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;
using Benchmark.Infrastructure;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicQueryValidator{
    private static readonly DynamicImplementationKind[] Implementations=[DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq];

    [Theory,Trait("Category","FixedReference")]
    [InlineData("value","value","value",true,WorkerExecutionStatus.Succeeded,true)]
    [InlineData("exception","exception","exception",true,WorkerExecutionStatus.Succeeded,false)]
    [InlineData("exception","exception","exception",false,WorkerExecutionStatus.ExecutionFailed,false)]
    [InlineData("exception","other-exception","exception",true,WorkerExecutionStatus.SemanticMismatch,false)]
    [InlineData("value","exception","value",true,WorkerExecutionStatus.SemanticMismatch,false)]
    [InlineData("exception","value","exception",true,WorkerExecutionStatus.SemanticMismatch,false)]
    [InlineData("value","fallback","value",true,WorkerExecutionStatus.ExpansionIncomplete,false)]
    [InlineData("value","fallback-mismatch","value",true,WorkerExecutionStatus.SemanticMismatch,false)]
    [InlineData("exception","fallback-exception","exception",true,WorkerExecutionStatus.Succeeded,false)]
    [InlineData("exception","fallback","exception",true,WorkerExecutionStatus.SemanticMismatch,false)]
    public void Validate(string system,string flinq,string unoptimized,bool expectExceptions,WorkerExecutionStatus expectedStatus,bool shouldMeasure){
        var loaded=Compile(system,flinq,unoptimized);
        try{
            var result=global::Benchmark.Dynamic.Worker.DynamicQueryValidator.Validate(loaded,new[]{1},Array.Empty<int>(),Implementations,expectExceptions);
            Assert.Equal(expectedStatus,result.Status);
            Assert.Equal(shouldMeasure,result.ShouldMeasure);
            if(expectedStatus==WorkerExecutionStatus.SemanticMismatch){Assert.Contains(result.Comparisons.Values,comparison=>!comparison.IsMatch);}
            if(expectedStatus==WorkerExecutionStatus.Succeeded){Assert.All(result.Comparisons.Values,comparison=>Assert.True(comparison.IsMatch));}
            if(flinq=="fallback-exception"){
                Assert.Empty(result.Fallbacks);
                Assert.Equal(3,result.Exceptions.Count);
                Assert.All(result.Exceptions.Values,exception=>Assert.Equal(typeof(InvalidOperationException).FullName,exception.Type));
            }
            if(flinq is "fallback" or "fallback-mismatch"){Assert.Equal(1,result.Fallbacks[DynamicImplementationKind.FLinq]);}
        }finally{Assert.True(loaded.UnloadAndVerify().Unloaded);}
    }

    [Fact,Trait("Category","FixedReference")]
    public void ValidatePropagatesCancellation(){
        foreach(var duringExecution in new[]{false,true}){
            var behavior="value";
            if(duringExecution){behavior="cancel";}
            var loaded=Compile(behavior,"value","value");
            try{VerifyCancellation(loaded,duringExecution);}
            finally{Assert.True(loaded.UnloadAndVerify().Unloaded);}
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VerifyCancellation(LoadedDynamicQuery loaded,bool duringExecution){
        using var cancellation=new CancellationTokenSource();
        if(!duringExecution){cancellation.Cancel();}
        Assert.Throws<OperationCanceledException>(()=>global::Benchmark.Dynamic.Worker.DynamicQueryValidator.Validate(loaded,new[]{1},Array.Empty<int>(),Implementations,true,cancellation.Token));
    }

    private static LoadedDynamicQuery Compile(string system,string flinq,string unoptimized){
        var item=new Core.FixedCase("validation-contract",["Count"],"source.Count()",null,"Int","Scalar",string.Empty);
        var source=Core.FixedCaseCatalog.Emit(item);
        foreach(var (method,behavior) in new[]{("RunSystem",system),("RunFLinqCore",flinq),("RunFLinqUnoptimizedCore",unoptimized)}){
            var methodStart=source.IndexOf("DynamicQueryValue "+method+"(",StringComparison.Ordinal);
            var resultStart=source.IndexOf("var result=",methodStart,StringComparison.Ordinal);
            var action=behavior switch{
                "value"=>string.Empty,
                "exception"=>"if(source.Length>=0){throw new global::System.InvalidOperationException(\""+method+"\");}",
                "other-exception"=>"if(source.Length>=0){throw new global::System.ArgumentException(\"different\");}",
                "cancel"=>"if(source.Length>=0){throw new global::System.OperationCanceledException();}",
                "fallback"=>"global::Seamless.Linq.LinqCounterScope.Counter.IncrementEnumerableFallback(\"validation-contract\");",
                "fallback-mismatch"=>"global::Seamless.Linq.LinqCounterScope.Counter.IncrementEnumerableFallback(\"validation-contract\");source=new[]{1,2};",
                "fallback-exception"=>"global::Seamless.Linq.LinqCounterScope.Counter.IncrementEnumerableFallback(\"validation-contract\");if(source.Length>=0){throw new global::System.InvalidOperationException(\"after fallback\");}",
                _=>throw new ArgumentOutOfRangeException(nameof(behavior))
            };
            source=source.Insert(resultStart,action);
        }
        var compilation=new DynamicQueryCompiler().Compile(new("validation-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,"validation-contract.cs"));
        Assert.True(compilation.Success,string.Join("\n",compilation.Diagnostics.Where(diagnostic=>diagnostic.Severity=="Error").Select(diagnostic=>diagnostic.ID+": "+diagnostic.Message)));
        return compilation.LoadedQuery!;
    }

    [Fact,Trait("Category","FixedReference")]
    public void FixedRunSavesValidationAndRejectsOnlyUnknownCatalogEntries(){
        var directory=Path.Combine(Path.GetTempPath(),"fixed-validation-"+Guid.NewGuid().ToString("N"));
        try{
            var repository=new BenchmarkRepository(new BenchmarkDatabase(Path.Combine(directory,"history.db")));
            var runner=new FixedCaseRunner(repository);
            var measurements=runner.Run(count:0,caseID:"chain-01");
            Assert.Equal(4,measurements.Count);
            var run=Assert.Single(repository.GetRuns("Dynamic.Fixed"));
            Assert.Equal("Passed",run.Status);
            using(var settings=JsonDocument.Parse(run.SettingsJSON)){
                Assert.Equal(1,settings.RootElement.GetProperty("MatchedCaseCount").GetInt32());
                var validation=Assert.Single(settings.RootElement.GetProperty("Validation").EnumerateArray());
                Assert.Equal("chain-01",validation.GetProperty("ID").GetString());
                Assert.Equal("Succeeded",validation.GetProperty("Status").GetString());
                Assert.False(validation.GetProperty("MeasurementSkipped").GetBoolean());
                Assert.Empty(validation.GetProperty("Exceptions").EnumerateObject());
                Assert.Equal(3,validation.GetProperty("Comparisons").EnumerateObject().Count());
            }
            Assert.Throws<ArgumentException>(()=>runner.Run(count:0,caseID:"unknown-case"));
            var failed=Assert.Single(repository.GetRuns("Dynamic.Fixed"),candidate=>candidate.Status=="Failed");
            using var failedSettings=JsonDocument.Parse(failed.SettingsJSON);
            Assert.Equal(0,failedSettings.RootElement.GetProperty("MatchedCaseCount").GetInt32());
            Assert.Empty(repository.GetMeasurements(failed.ID));
            using var cancellation=new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(()=>runner.Run(count:0,caseID:"chain-01",cancellationToken:cancellation.Token));
        }finally{if(Directory.Exists(directory)){Directory.Delete(directory,true);}}
    }
}
