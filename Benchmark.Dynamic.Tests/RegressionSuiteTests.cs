using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

// 共通回帰のランナーをWorkerの明示コマンドと自動検証から使う。
[CollectionDefinition("Dynamic regression sources",DisableParallelization=true)]
public sealed class RegressionSuiteCollection:ICollectionFixture<RegressionSuiteFixture>;

public sealed class RegressionSuiteFixture:IDisposable{
    public RegressionSuite Suite{get;}=RegressionSuite.Compile();
    public void Dispose()=>this.Suite.Dispose();
}

[Collection("Dynamic regression sources")]
public sealed class RegressionSuiteTests(RegressionSuiteFixture fixture){
    [Fact]
    public void BehaviorCases_preserve_assertions_and_never_fallback(){
        var failures=new List<string>();
        foreach(var item in fixture.Suite.Cases){
            var result=fixture.Suite.Run(item.ID);
            MigrationCaseReceipt.Write("behavior",result);
            if(result.Status!="Passed"){failures.Add(result.ID+"\n"+result.Error+"\n"+result.Output);}
        }
        Assert.True(failures.Count==0,string.Join("\n\n",failures));
    }
}

public sealed class GeneratorContractTests{
    public static IEnumerable<object[]> Cases(){
        using var suite=RegressionSuite.Contracts(typeof(GeneratorContractTests).Assembly);
        return suite.Cases.Select(item=>new object[]{item.ID}).ToArray();
    }
    [Theory,MemberData(nameof(Cases))]
    public void Generator_runtime_and_instrumentation_contract(string id){
        using var suite=RegressionSuite.Contracts(typeof(GeneratorContractTests).Assembly);
        var result=suite.Run(id);MigrationCaseReceipt.Write("contract",result);Assert.True(result.Status=="Passed",result.Error+"\n"+result.Output);
    }
}

internal static class MigrationCaseReceipt{
    private static readonly object gate=new();
    internal static void Write(string kind,Benchmark.Dynamic.Core.RegressionCaseResult result){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_RESULT_ROOT");if(string.IsNullOrWhiteSpace(root)){return;}
        lock(gate){Directory.CreateDirectory(root);File.AppendAllText(Path.Combine(root,kind+"-results.jsonl"),System.Text.Json.JsonSerializer.Serialize(result)+"\n");}
    }
}
