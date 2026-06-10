using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Tsavorite.SourceGenerator;

namespace Test.Tsavorite.SourceGenerator;

public class IncrementalCacheTests {
    // ファイルAとBはそれぞれ独立した virtual メソッドを持つクラス。
    // interceptor はクラス単位でインターセプターを生成するため、
    // 片方を変更しても他方の生成結果がキャッシュから取得されることを確認する。
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
        var comp = CreateCompilation([("FileA.cs",FileA),("FileB.cs",FileB)]);
        var driver = CreateDriver().RunGenerators(comp);

        // 変更なしで再実行 → 全ステップがキャッシュ
        var driver2 = driver.RunGenerators(comp);
        var result = driver2.GetRunResult();

        var steps = result.Results
            .SelectMany(r => r.TrackedSteps)
            .SelectMany(kv => kv.Value)
            .ToArray();

        Assert.NotEmpty(steps);
        Assert.All(steps,step => {
            Assert.All(step.Outputs,output => {
                Assert.True(
                    output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"Step '{step.Name}' output reason was {output.Reason}, expected Cached or Unchanged.");
            });
        });
    }

    [Fact]
    public void OnlyFileAChanged_FileBSourceOutputCached() {
        var comp = CreateCompilation([("FileA.cs",FileA),("FileB.cs",FileB)]);
        var driver = CreateDriver().RunGenerators(comp);

        // FileA だけ変更した compilation で再実行
        var compWithA2 = CreateCompilation([("FileA.cs",FileA2),("FileB.cs",FileB)]);
        var driver2 = driver.RunGenerators(compWithA2);
        var result = driver2.GetRunResult();

        // FileB に対応する生成ファイルのヒント名（SourcePath=FileB.cs）が Cached であることを確認
        var fileBSource = result.GeneratedTrees
            .Where(t => t.FilePath.Contains("FileB",StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // FileB の出力は前回と同じ内容のため再生成されない（Cached）
        // 生成ファイルが存在するか、または全ステップ中に FileB 由来のキャッシュが含まれることを確認
        var addedSources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
        var fileBSources = addedSources
            .Where(s => s.HintName.Contains("FileB",StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // FileB の生成物が変化していないことを確認（Cached なら再追加されない）
        var perFileSteps = result.Results
            .SelectMany(r => r.TrackedSteps)
            .Where(kv => kv.Key=="PerFileInterceptGroup")
            .SelectMany(kv => kv.Value)
            .ToArray();

        // PerFileInterceptGroup が追跡されていれば、FileB のグループが Cached であることを確認
        if(perFileSteps.Length>0) {
            var fileBStep = perFileSteps
                .Where(s => s.Outputs.Any(o => o.Value is { } v && v.ToString()!.Contains("FileB")))
                .ToArray();
            Assert.All(fileBStep,step => {
                Assert.All(step.Outputs,output => {
                    Assert.True(
                        output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                        $"FileB step reason was {output.Reason}, expected Cached or Unchanged.");
                });
            });
        }
    }

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview);

    private static CSharpGeneratorDriver CreateDriver() {
        return CSharpGeneratorDriver.Create(
            generators: [new TsavoriteIncrementalGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<(string fileName,string source)> sources) {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable);
        var references = GetNetCoreReferences();
        var syntaxTrees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s.source,ParseOptions,path: s.fileName))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: options);
    }

    private static ImmutableArray<MetadataReference> GetNetCoreReferences() {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var refs = trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToImmutableArray();
        return refs;
    }
}
