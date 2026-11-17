using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

public sealed class CoreGroupingRegressionTests{
    [Fact]
    public void GroupingAndLazyEvaluationKeepInternalTuplesWithoutWarnings(){
        var paths=new[]{"GroupByOrder.cs","LazyEvaluationCounts.cs","OrderingComparers.cs"};
        var documents=RegressionCaseSources.Legacy.Documents
            .Where(document=>paths.Contains(Path.GetFileName(document.Path),StringComparer.Ordinal))
            .Select(document=>document with{Text="#nullable enable\n"+document.Text}).ToArray();
        Assert.Equal(3,documents.Length);
        var debug=false;
#if DEBUG
        debug=true;
#endif
        using var suite=RegressionSuite.Compile(new("grouping-generation-contract",documents),default,debug);
        var results=Environment.GetEnvironmentVariable("SEAMLESS_TEST_RESULT_ROOT");
        if(!string.IsNullOrWhiteSpace(results)){
            Directory.CreateDirectory(results);
            File.WriteAllText(Path.Combine(results,"grouping-generated.cs"),suite.ExpandedSource);
        }
        Assert.Equal(30,suite.Cases.Count);
        Assert.DoesNotContain(suite.Diagnostics,diagnostic=>diagnostic.Contains("warning ",StringComparison.Ordinal));
        Assert.Contains("OptimizedLinq_SelectOrderBy_Optimized",suite.ExpandedSource);
        Assert.Contains("OptimizedLinq_JoinEmptyOuterImpl",suite.ExpandedSource);
        Assert.Contains("OptimizedLinq_CustomFirst",suite.ExpandedSource);
        Assert.Contains("OptimizedLinq_SelectAnonymousOrderBy_Optimized",suite.ExpandedSource);
        Assert.DoesNotContain("__TsavoriteAnonRecords_",suite.ExpandedSource);
        foreach(var item in suite.Cases){
            var result=suite.Run(item.ID);
            Assert.True(result.Status=="Passed",result.ID+"\n"+result.Error+"\n"+result.Output);
            Assert.Equal(0,result.FallbackCount);
        }
    }
}
