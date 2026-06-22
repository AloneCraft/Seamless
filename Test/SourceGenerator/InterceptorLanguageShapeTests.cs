using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class InterceptorLanguageShapeTests(ITestOutputHelper output)
{
    private string lastGeneratedSource = string.Empty;

    private static void SumA() { }
    private static void SumB() { }
    private static void SumWithBaseCall() { }
    private static void SumWithPrivateOverloads() { }
    private static void SumSealedOverride() { }
    private static void SumInStaticClass() { }
    private static void SumWithBaseProperty() { }
    private static void SumWithShadowedField() { }
    private static void SumWithNamedExpandedParams() { }
    private static void SumWithPrivateInstanceHelper() { }
    private static void SumWithPrivateStaticMethodGroup() { }
    private static void SumWithPrivateInstanceMethodGroup() { }
    private static void SumWithBlockPrivateHelper() { }
    private static void SumStructInstance() { }
    private static void SumGeneratedHelper() { }
    private static void ToArrayConsume() { }

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

        GeneratedSourceAssert.True(
            IsIntercepted(result.GeneratedSource, nameof(SumSealedOverride)) || !result.GeneratedSource.Contains(nameof(SumSealedOverride), StringComparison.Ordinal),
            "sealed override should either be intercepted or omitted from generated source.",
            result.GeneratedSource,
            output);
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

        GeneratedSourceAssert.True(
            !result.GeneratedSource.Contains("LinqInterceptorFailure", StringComparison.Ordinal),
            "Generated source should not contain LinqInterceptorFailure.",
            result.GeneratedSource,
            output);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void ScalarMaterializeConsume_ExpandsWithoutForwardingFailure()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer {
                public List<object?> Items { get; } = [];
                public void Consume<T>(T item) => Items.Add(item);
            }
            public static class ConsumerExtensions {
                public static void Consume<T>(this IEnumerable<T> source, Consumer consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private readonly Consumer consumer = new();
                private void ToArrayConsume() => new[] { 1, 2, 3 }.ToArray().Consume(this.consumer);
                public void Run() => ToArrayConsume();
                public static string TargetName => nameof(ToArrayConsume);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(ToArrayConsume));
        GeneratedSourceAssert.True(
            !result.GeneratedSource.Contains("LinqInterceptorFailure", StringComparison.Ordinal)
            && result.GeneratedSource.Contains("foreach(var __item in __linq", StringComparison.Ordinal)
            && result.GeneratedSource.Contains(".Consume(__item);", StringComparison.Ordinal),
            "Scalar materialize terminal followed by Consume should expand to direct item consumption.",
            result.GeneratedSource,
            output);
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
        GeneratedSourceAssert.True(
            result.GeneratedSource.Contains("UnsafeAccessorKind.StaticMethod", StringComparison.Ordinal)
            && result.GeneratedSource.Contains("__StaticMethod_Container_AddAll", StringComparison.Ordinal),
            "Generated source should call private static AddAll through UnsafeAccessor.",
            result.GeneratedSource,
            output);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void PrivateInstanceHelper_InSelector_UsesUnsafeAccessor()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private int Map(int value) => value+1;
                private int SumWithPrivateInstanceHelper(int[] src) =>
                    src.Select(x => Map(x)).Sum();
                public int Run(int[] src) => SumWithPrivateInstanceHelper(src);
                public static string TargetName => nameof(SumWithPrivateInstanceHelper);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithPrivateInstanceHelper));
        GeneratedSourceAssert.True(
            result.GeneratedSource.Contains("UnsafeAccessorKind.Method", StringComparison.Ordinal)
            && result.GeneratedSource.Contains("__Method_Container_Map", StringComparison.Ordinal),
            "Generated source should call private instance Map through UnsafeAccessor.",
            result.GeneratedSource,
            output);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void PrivateStaticMethodGroup_InSelector_UsesUnsafeAccessor()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int MapStatic(int value) => value+1;
                private static int SumWithPrivateStaticMethodGroup(int[] src) =>
                    src.Select(MapStatic).Sum();
                public int Run(int[] src) => SumWithPrivateStaticMethodGroup(src);
                public static string TargetName => nameof(SumWithPrivateStaticMethodGroup);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithPrivateStaticMethodGroup));
        GeneratedSourceAssert.True(
            result.GeneratedSource.Contains("UnsafeAccessorKind.StaticMethod", StringComparison.Ordinal)
            && result.GeneratedSource.Contains("__StaticMethod_Container_MapStatic", StringComparison.Ordinal),
            "Generated source should bind private static method group through UnsafeAccessor.",
            result.GeneratedSource,
            output);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void PrivateInstanceMethodGroup_InSelector_UsesUnsafeAccessor()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private int MapInstance(int value) => value+1;
                private int SumWithPrivateInstanceMethodGroup(int[] src) =>
                    src.Select(MapInstance).Sum();
                public int Run(int[] src) => SumWithPrivateInstanceMethodGroup(src);
                public static string TargetName => nameof(SumWithPrivateInstanceMethodGroup);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithPrivateInstanceMethodGroup));
        GeneratedSourceAssert.True(
            result.GeneratedSource.Contains("UnsafeAccessorKind.Method", StringComparison.Ordinal)
            && result.GeneratedSource.Contains("__DelegateAccess_Container_MapInstance", StringComparison.Ordinal),
            "Generated source should bind private instance method group through UnsafeAccessor.",
            result.GeneratedSource,
            output);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void BlockLambda_PrivateHelper_UsesUnsafeAccessor()
    {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int MapBlock(int value) => value+1;
                private static int SumWithBlockPrivateHelper(int[] src) =>
                    src.Select(x => {
                        var y = MapBlock(x);
                        if(MapBlock(y) > 0) {
                            return y;
                        }
                        return MapBlock(0);
                    }).Sum();
                public int Run(int[] src) => SumWithBlockPrivateHelper(src);
                public static string TargetName => nameof(SumWithBlockPrivateHelper);
            }
            """;

        var result = RunGeneratorAndCompile(("Container.cs", source));

        AssertIntercepted(result.GeneratedSource, nameof(SumWithBlockPrivateHelper));
        GeneratedSourceAssert.True(
            result.GeneratedSource.Contains("UnsafeAccessorKind.StaticMethod", StringComparison.Ordinal)
            && result.GeneratedSource.Contains("__StaticMethod_Container_MapBlock", StringComparison.Ordinal),
            "Generated source should call private helper in block lambda through UnsafeAccessor.",
            result.GeneratedSource,
            output);
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

        GeneratedSourceAssert.True(
            !IsIntercepted(result.GeneratedSource, nameof(SumStructInstance)),
            "Struct instance method should not be intercepted.",
            result.GeneratedSource,
            output);
        AssertNoErrors(result.Diagnostics);
    }

    [Fact]
    public void OtherSourceGeneratorMethod_IsExplicitlyNotIntercepted()
    {
        var comp = GeneratorTestHelpers.CreateCompilation([]);
        GeneratorDriver driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
            generators: [
                new GeneratedHelperGenerator(),
                new global::Seamless.FLinq.TsavoriteIncrementalGenerator().AsSourceGenerator()
            ],
            parseOptions: GeneratorTestHelpers.ParseOptions);
        driver = driver.RunGenerators(comp);
        var runResult = driver.GetRunResult();
        var generated = string.Concat(runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        GeneratedSourceAssert.True(
            !IsIntercepted(generated, nameof(SumGeneratedHelper)),
            "Other source generator method should not be intercepted.",
            generated,
            output);
    }

    private GeneratorCompilationResult RunGeneratorAndCompile(params (string fileName, string source)[] sources)
    {
        var comp = GeneratorTestHelpers.CreateCompilation(sources);
        GeneratorDriver driver = GeneratorTestHelpers.CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();
        var generated = string.Concat(runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        lastGeneratedSource = generated;
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

    private void AssertIntercepted(string generatedSource, string methodName) =>
        GeneratedSourceAssert.True(
            IsIntercepted(generatedSource, methodName),
            $"{methodName} interceptor was not generated.",
            generatedSource,
            output);

    private void AssertNoErrors(IReadOnlyCollection<Diagnostic> diagnostics)
    {
        GeneratedSourceAssert.NoErrors(diagnostics, lastGeneratedSource, output);
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
