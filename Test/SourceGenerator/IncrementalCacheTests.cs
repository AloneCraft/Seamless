using Microsoft.CodeAnalysis;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class IncrementalCacheTests(ITestOutputHelper output) {
    private const string FileA = """
        using System.Linq;
        namespace SampleA;
        public class ContainerA {
            public virtual int Sum(int[] src) => src.Where(x => x > 0).Sum();
        }
        """;
    private const string FileA2 = """
        using System.Linq;
        namespace SampleA;
        public class ContainerA {
            public virtual int Sum(int[] src) => src.Where(x => x > 1).Sum();
        }
        """;
    private const string FileB = """
        using System.Linq;
        namespace SampleB;
        public class ContainerB {
            public virtual int Count(int[] src) => src.Where(x => x > 0).Count();
        }
        """;

    [Fact]
    public void NoChange_AllStepsCached() {
        var comp = GeneratorTestHelpers.CreateCompilation([("FileA.cs", FileA), ("FileB.cs", FileB)]);
        var driver = GeneratorTestHelpers.CreateDriver().RunGenerators(comp);

        var driver2 = driver.RunGenerators(comp);
        var result = driver2.GetRunResult();

        var steps = result.Results
            .SelectMany(r => r.TrackedSteps)
            .SelectMany(kv => kv.Value)
            .ToArray();
        var generated = string.Concat(result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        Assert.NotEmpty(steps);
        Assert.All(steps, step => {
            Assert.All(step.Outputs, outputStep => {
                GeneratedSourceAssert.True(
                    outputStep.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"Step '{step.Name}' output reason was {outputStep.Reason}, expected Cached or Unchanged.",
                    generated,
                    output);
            });
        });
    }

    [Fact]
    public void OnlyFileAChanged_FileBSourceOutputCached() {
        var comp = GeneratorTestHelpers.CreateCompilation([("FileA.cs", FileA), ("FileB.cs", FileB)]);
        var driver = GeneratorTestHelpers.CreateDriver().RunGenerators(comp);

        var compWithA2 = GeneratorTestHelpers.CreateCompilation([("FileA.cs", FileA2), ("FileB.cs", FileB)]);
        var driver2 = driver.RunGenerators(compWithA2);
        var result = driver2.GetRunResult();

        var fileBSource = result.GeneratedTrees
            .Where(t => t.FilePath.Contains("FileB", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var addedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        var fileBSources = addedSources
            .Where(s => s.HintName.Contains("FileB", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var perFileSteps = result.Results
            .SelectMany(r => r.TrackedSteps)
            .Where(kv => kv.Key == "PerFileInterceptGroup")
            .SelectMany(kv => kv.Value)
            .ToArray();
        var generated = string.Concat(result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        if(perFileSteps.Length > 0) {
            var fileBStep = perFileSteps
                .Where(s => s.Outputs.Any(o => o.Value is { } v && v.ToString()!.Contains("FileB")))
                .ToArray();
            Assert.All(fileBStep, step => {
                Assert.All(step.Outputs, outputStep => {
                    GeneratedSourceAssert.True(
                        outputStep.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                        $"FileB step reason was {outputStep.Reason}, expected Cached or Unchanged.",
                        generated,
                        output);
                });
            });
        }
    }
}
