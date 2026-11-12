using FactAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
namespace Test.SourceGenerator;
public sealed class ArchitectureContract{
    [Fact, Trait("Category","Architecture")]
    public void ProductCodeDoesNotDependOnBenchmarkIdentifiers() {
        var root = FindRepositoryRoot();
        var productFiles = new[] { "Seamless.FLinq.Generator","Seamless.Linq" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root,folder),"*.cs",SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",StringComparison.Ordinal)
                &&!path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",StringComparison.Ordinal));
        var forbidden = new[]{
            "Benchmark.System.Linq.Enumerable",
            "ExecutionPlan_FilterJoinAggregate",
            "ExecutionPlan_GroupJoinMaterialize",
            "AggregateBy_keySelector",
            "ToLookup_elementSelector",
            "Join_innerKey_Hoisting",
        };
        foreach(var file in productFiles) {
            var source = File.ReadAllText(file);
            foreach(var token in forbidden) {
                Assert.DoesNotContain(token,source,StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepositoryRoot() {
        var current = new DirectoryInfo(Environment.GetEnvironmentVariable("SEAMLESS_TEST_REPOSITORY")??Environment.CurrentDirectory);
        while(current is not null&&!File.Exists(Path.Combine(current.FullName,"Seamless.slnx"))) {
            current=current.Parent;
        }
        return current?.FullName??throw new DirectoryNotFoundException("Repository root was not found.");
    }

}
