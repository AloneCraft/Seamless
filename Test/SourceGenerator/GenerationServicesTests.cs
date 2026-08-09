using System.Collections.Concurrent;

namespace Test.SourceGenerator;

public sealed class GenerationServicesTests {
    [Fact]
    public void ExplicitOptOutOnContainingMethod_PreventsTargetDiscovery() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public sealed class Container {
                private static int Candidate(int[] values) => values.Contains(1) ? 1 : 0;
                [global::Seamless.FLinq.LinqIntercept(false)]
                private static int Caller(int[] values) => Candidate(values);
                public static int Run(int[] values) => Caller(values);
            }
            """;

        var generated=Generate(("OptOut.cs",source));

        Assert.DoesNotContain("OptimizedLinq_Candidate",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("UnsupportedLinqExpansion(",generated,StringComparison.Ordinal);
    }

    [Fact]
    public void ParallelGeneration_DoesNotLeakTargetState() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public sealed class Container {
                private static int Candidate(int[] values) => values.Where(static value => value > 0).Sum();
                public static int Run(int[] values) => Candidate(values);
            }
            """;
        var results=new ConcurrentBag<string>();

        Parallel.For(0,8,_ => results.Add(Generate(("Parallel.cs",source))));

        Assert.Equal(8,results.Count);
        Assert.Single(results.Distinct(StringComparer.Ordinal));
        Assert.Contains("OptimizedLinq_Candidate",results.First(),StringComparison.Ordinal);
    }

    private static string Generate(params (string fileName,string source)[] sources) {
        var compilation=GeneratorTestHelpers.CreateCompilation(sources);
        var driver=GeneratorTestHelpers.CreateDriver().RunGenerators(compilation);
        return string.Concat(driver.GetRunResult().Results
            .SelectMany(static result => result.GeneratedSources)
            .OrderBy(static source => source.HintName,StringComparer.Ordinal)
            .Select(static source => source.SourceText.ToString()));
    }
}