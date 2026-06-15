using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Test.Tsavorite.SourceGenerator;

public class InterceptorLanguageShapeTests
{
    private static void SumA() { }
    private static void SumB() { }
    private static void SumWithBaseCall() { }
    private static void SumWithPrivateOverloads() { }
    private static void SumSealedOverride() { }
    private static void SumInStaticClass() { }
    private static void SumWithBaseProperty() { }
    private static void SumWithShadowedField() { }
    private static void SumWithNamedExpandedParams() { }
    private static void SumStructInstance() { }
    private static void SumGeneratedHelper() { }

    [Fact]
    public void PartialClass_TwoFiles_DoesNotDuplicateGeneratedHelperType()
    {
        const string fileA = """
            using System.Linq;
            namespace Sample;
            public partial class Container {
                private static int SumA(int[] src) => src.Where(x => x>0).Sum();
                public int RunA(int[] src) => SumA(src);
                public static string SumAName => nameof(SumA);
            }
            """;
        const string fileB = """
            using System.Linq;
            namespace Sample;
            public partial class Container {
                private static int SumB(int[] src) => src.Where(x => x<10).Sum();
                public int RunB(int[] src) => SumB(src);
                public static string SumBName => nameof(SumB);
            }
            """;

        var result = RunGeneratorAndCompile(("A.cs", fileA), ("B.cs", fileB));

        AssertIntercepted(result.GeneratedSource, nameof(SumA));
        AssertIntercepted(result.GeneratedSource, nameof(SumB));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void InstanceMethod_WithBaseCall_GeneratedCodeCompiles()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class BaseContainer {
                protected int Adjust(int value) => value+1;
            }
            public class Container : BaseContainer {
                private int SumWithBaseCall(int[] src) => src.Select(x => base.Adjust(x)).Sum();
                public int Run(int[] src) => SumWithBaseCall(src);
                public static string TargetName => nameof(SumWithBaseCall);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithBaseCall));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void PrivateOverloads_UnsafeAccessorNamesAreUnique()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private int Map(int value) => value+1;
                private long Map(long value) => value+2;
                private long SumWithPrivateOverloads(int[] src) =>
                    src.Select(x => (long)Map(x)+Map((long)x)).Sum();
                public long Run(int[] src) => SumWithPrivateOverloads(src);
                public static string TargetName => nameof(SumWithPrivateOverloads);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithPrivateOverloads));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void SealedOverride_IsEitherInterceptedOrExplicitlyNotIntercepted()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class BaseContainer {
                public virtual int SumSealedOverride(int[] src) => src.Sum();
            }
            public sealed class Container : BaseContainer {
                public sealed override int SumSealedOverride(int[] src) => src.Where(x => x>0).Sum();
                public int Run(int[] src) => SumSealedOverride(src);
                public static string TargetName => nameof(SumSealedOverride);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        Assert.True(
            IsIntercepted(result.GeneratedSource, nameof(SumSealedOverride)) || !result.GeneratedSource.Contains(nameof(SumSealedOverride), StringComparison.Ordinal),
            "sealed override はインターセプト対象にするか、生成コード側に中途半端な痕跡を残さず明示的に対象外にする。");
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void StaticClass_StaticHelper_SpecIsFixed()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public static class Helpers {
                private static int SumInStaticClass(int[] src) => src.Where(x => x>0).Sum();
                public static int Run(int[] src) => SumInStaticClass(src);
                public static string TargetName => nameof(SumInStaticClass);
            }
            """;

        var result = RunGeneratorAndCompile(("Helpers.cs", source));

        Assert.DoesNotContain("LinqInterceptorFailure", result.GeneratedSource, StringComparison.Ordinal);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void DerivedClass_ProtectedBaseProperty_GeneratedCodeCompiles()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class BaseContainer {
                protected int Offset => 3;
            }
            public class Container : BaseContainer {
                private int SumWithBaseProperty(int[] src) => src.Select(x => Offset+x).Sum();
                public int Run(int[] src) => SumWithBaseProperty(src);
                public static string TargetName => nameof(SumWithBaseProperty);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithBaseProperty));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void NewShadowedPrivateMember_UsesCorrectDeclaringType()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class BaseContainer {
                private int value = 1;
                protected int BaseValue() => value;
            }
            public class Container : BaseContainer {
                private new int value = 2;
                private int SumWithShadowedField(int[] src) => src.Select(x => value+x).Sum();
                public int Run(int[] src) => SumWithShadowedField(src);
                public static string TargetName => nameof(SumWithShadowedField);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithShadowedField));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void NamedArguments_WithExpandedParams_FallsBackOrCompilesPredictably()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int AddAll(int seed, params int[] values) => seed+values.Sum();
                private static int SumWithNamedExpandedParams(int[] src) =>
                    src.Select(x => AddAll(seed: x, values: [1, 2])).Sum();
                public int Run(int[] src) => SumWithNamedExpandedParams(src);
                public static string TargetName => nameof(SumWithNamedExpandedParams);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithNamedExpandedParams));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void StructInstanceMethod_IsExplicitlyNotIntercepted()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public struct Container {
                private int SumStructInstance(int[] src) => src.Where(x => x>0).Sum();
                public int Run(int[] src) => SumStructInstance(src);
                public static string TargetName => nameof(SumStructInstance);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        Assert.False(IsIntercepted(result.GeneratedSource, nameof(SumStructInstance)));
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void OtherSourceGeneratorMethod_IsExplicitlyNotIntercepted()
    {
        var comp = GeneratorTestHelpers.CreateCompilation([]);
        GeneratorDriver driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
            generators: [
                new GeneratedHelperGenerator(),
                new global::Tsavorite.SourceGenerator.TsavoriteIncrementalGenerator().AsSourceGenerator()
            ],
            parseOptions: GeneratorTestHelpers.ParseOptions);
        driver = driver.RunGenerators(comp);
        var runResult = driver.GetRunResult();
        var generated = string.Concat(runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        Assert.False(IsIntercepted(generated, nameof(SumGeneratedHelper)));
    }

    private static GeneratorCompilationResult RunGeneratorAndCompile(params (string fileName, string source)[] sources)
    {
        var comp = GeneratorTestHelpers.CreateCompilation(sources);
        GeneratorDriver driver = GeneratorTestHelpers.CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();
        var generated = string.Concat(runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        var diagnostics = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Where(static d => d.Id != "CS9137")
            .ToArray();
        return new GeneratorCompilationResult(generated, diagnostics);
    }

    private static bool IsIntercepted(string generatedSource, string methodName) =>
        generatedSource.Contains($"OptimizedLinq_{methodName}_", StringComparison.Ordinal)
        || generatedSource.Contains($"MeasuredLinq_{methodName}_", StringComparison.Ordinal);

    private static void AssertIntercepted(string generatedSource, string methodName) =>
        Assert.True(IsIntercepted(generatedSource, methodName), $"{methodName} のインターセプターが生成されていません。");

    private static void AssertNoErrors(IReadOnlyCollection<Diagnostic> diagnostics)
    {
        Assert.True(
            diagnostics.Count == 0,
            string.Join(Environment.NewLine, diagnostics.Select(static d => d.ToString())));
    }

    private sealed record GeneratorCompilationResult(string GeneratedSource, IReadOnlyCollection<Diagnostic> Diagnostics);

#pragma warning disable RS1042
    private sealed class GeneratedHelperGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("GeneratedHelper.g.cs", SourceText.From("""
                using System.Linq;
                namespace Sample;
                public partial class Container {
                    private static int SumGeneratedHelper(int[] src) => src.Where(x => x>0).Sum();
                    public int Run(int[] src) => SumGeneratedHelper(src);
                    public static string TargetName => nameof(SumGeneratedHelper);
                }
                """, Encoding.UTF8));
        }
    }
#pragma warning restore RS1042
}
