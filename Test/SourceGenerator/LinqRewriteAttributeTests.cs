namespace Test.SourceGenerator;

public class LinqRewriteAttributeTests(ITestOutputHelper output) {
    private const string NoRewriteOnMethod = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(false)]
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    private const string DefaultRewrite = """
        using System.Linq;
        namespace Sample;
        public class Container {
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    private const string GenericContainingTypeRewrite = """
        using System.Linq;
        namespace Sample;
        public static class Route {
            public static T[] AsX<T>(this T[]source) => source;
        }
        public class Container<T> where T:notnull {
            internal readonly T[]src=[];
            internal static bool Accept(T value) => value is not null;
            private int CountItems() => this.src.AsX<T>().Count(static value => Accept(value));
            public int Run() => this.CountItems();
        }
        """;

    private const string NestedGenericContainingTypeRewrite = """
        using System.Linq;
        namespace Sample;
        public static class Route {
            public static T[] AsX<T>(this T[] source) => source;
        }
        public class Outer<T> where T:notnull {
            public class Inner<U> where U:class,new() {
                private readonly T[] source=[];
                private int CountItems() => this.source.AsX<T>().Count(static value => value is not null);
                public int Run(U value) => value is null ? 0 : this.CountItems();
            }
        }
        """;

    private const string PrivateConstraintRewrite = """
        using System.Linq;
        namespace Sample;
        public class Host {
            private class Constraint { }
            private int CountItems<T>(T[] source) where T:Constraint => source.Count();
            public int Run() => this.CountItems(System.Array.Empty<Constraint>());
        }
        """;

    private const string PrivateConstraintOptOut = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Host {
            private class Constraint { }
            [LinqIntercept(false)]
            private int CountItems<T>(T[] source) where T:Constraint => source.Count();
            public int Run() => this.CountItems(System.Array.Empty<Constraint>());
        }
        """;

    private const string GenericFragmentFailure = """
        using System.Collections;
        using System.Collections.Generic;
        using System.Linq;
        namespace Sample;
        public sealed class Weird<T>:IEnumerable<T> {
            public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)System.Array.Empty<T>()).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        public class Container<T> where T:notnull {
            private static bool Accept(T value) => value is not null;
            private int CountItems(Weird<T> source) => source.Where(Accept).Count();
            public int Run(Weird<T> source) => this.CountItems(source);
        }
        """;
    private const string CounterWithoutOptimization = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(false,LinqInstrumentation.Counter)]
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    private const string NoRewriteOnClass = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        [LinqIntercept(false)]
        public class Container {
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    private const string MethodOverridesClass = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        [LinqIntercept(false)]
        public class Container {
            [LinqIntercept(true)]
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    private const string ClassOverridesAssembly = """
        using System.Linq;
        using Seamless.FLinq;
        [assembly: LinqIntercept(false)]
        namespace Sample;
        [LinqIntercept(true)]
        public class Container {
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    private const string InnerClassOverridesOuter = """
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        [LinqIntercept(false)]
        public class Outer {
            [LinqIntercept(true)]
            public class Inner {
                private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
                public int Run(int[] src) => SumPositive(src);
            }
        }
        """;

    [Fact]
    public void NoLinqRewrite_OnMethod_NotIntercepted() {
        this.AssertNotIntercepted(NoRewriteOnMethod);
    }

    [Fact]
    public void NoAttribute_DefaultsToIntercepted() {
        this.AssertIntercepted(DefaultRewrite);
    }

    [Fact]
    public void GenericContainingType_WithAccessibleLambda_IsIntercepted() {
        var generated = GeneratedSource(GenericContainingTypeRewrite);
        GeneratedSourceAssert.True(
            generated.Contains("CountItems_",StringComparison.Ordinal),
            "Expected generic-containing CountItems interceptor was not generated.",
            generated,
            output);
    }

    [Fact]
    public void NestedGenericContainingType_ConstraintsCompileAfterGeneration() {
        var (result,outputCompilation)=RunGenerator(NestedGenericContainingTypeRewrite);
        var generated=GeneratedText(result);
        Assert.Contains("CountItems_",generated,StringComparison.Ordinal);
        Assert.DoesNotContain(
            outputCompilation.GetDiagnostics().Where(static diagnostic => diagnostic.Id!="CS9137"),
            static diagnostic => diagnostic.Severity==global::Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void NonGenericMethodOnClosedGenericOwnerCompilesAndRuns(){
        const string source="""
            #nullable disable
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            [LinqIntercept(false)]public sealed class Entry{
                public static int Run(){return new Box<string>().CountItems(new[]{"a",null})+new Box<int>().CountItems(new[]{0,1});}
            }
            [LinqIntercept(false)]public sealed class Box<T>{
                [LinqIntercept(true)]public int CountItems(T[] values){return values.Where(value=>value is not null).Count();}
            }
            """;
        var(compilation,generated)=ExecutableGeneration.Generate(source,"ClosedGenericOwnerProbe",minimumInterceptions:2);
        Assert.DoesNotContain("UnsupportedLinqExpansion(",generated,StringComparison.Ordinal);
        var actual=ExecutableGeneration.Run(compilation,"Sample.Entry",static type=>(int)type.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal(3,actual);
    }

    [Fact]
    public void PrivateConstraint_ReportsNotConfigurableFLINQ005() {
        var (result,_)=RunGenerator(PrivateConstraintRewrite);
        var diagnostic=Assert.Single(result.Diagnostics.Where(static diagnostic => diagnostic.Id=="FLINQ005"));
        Assert.Equal(global::Microsoft.CodeAnalysis.DiagnosticSeverity.Error,diagnostic.Severity);
        Assert.Contains(global::Microsoft.CodeAnalysis.WellKnownDiagnosticTags.NotConfigurable,diagnostic.Descriptor.CustomTags);
    }

    [Fact]
    public void PrivateConstraint_WithOptOut_DoesNotReportFLINQ005() {
        var (result,_)=RunGenerator(PrivateConstraintOptOut);
        Assert.DoesNotContain(result.Diagnostics,static diagnostic => diagnostic.Id=="FLINQ005");
        Assert.DoesNotContain("CountItems_",GeneratedText(result),StringComparison.Ordinal);
    }

    [Fact]
    public void GenericFragmentFailure_ReportsFLINQ002AndEmitsThrowStub() {
        var (result,outputCompilation)=RunGenerator(GenericFragmentFailure);
        var diagnostic=Assert.Single(result.Diagnostics.Where(static diagnostic => diagnostic.Id=="FLINQ002"));
        Assert.Equal(global::Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,diagnostic.Severity);
        Assert.Contains("UnsupportedLinqExpansion",GeneratedText(result),StringComparison.Ordinal);
        Assert.DoesNotContain(
            outputCompilation.GetDiagnostics().Where(static diagnostic => diagnostic.Id!="CS9137"),
            static diagnostic => diagnostic.Severity==global::Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }
    [Fact]
    public void CounterInstrumentation_WithNoLinqOptimization_IsIntercepted() {
        var generated = GeneratedSource(CounterWithoutOptimization);
        GeneratedSourceAssert.True(
            generated.Contains("MeasuredLinq_SumPositive",StringComparison.Ordinal),
            "Measured interceptor was not generated.",
            generated,
            output);
        GeneratedSourceAssert.True(
            !generated.Contains("OptimizedLinq_SumPositive",StringComparison.Ordinal),
            "Optimized interceptor should not be generated.",
            generated,
            output);
    }

    [Fact]
    public void NoLinqRewrite_OnClass_NotIntercepted() {
        this.AssertNotIntercepted(NoRewriteOnClass);
    }

    [Fact]
    public void LinqRewrite_OnMethod_OverridesNoRewriteOnClass() {
        this.AssertIntercepted(MethodOverridesClass);
    }

    [Fact]
    public void LinqRewrite_OnClass_OverridesNoRewriteOnAssembly() {
        this.AssertIntercepted(ClassOverridesAssembly);
    }

    [Fact]
    public void LinqRewrite_OnInnerClass_OverridesNoRewriteOnOuterClass() {
        this.AssertIntercepted(InnerClassOverridesOuter);
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void IteratorFallbackWarningIsLimitedToLinqMethods(bool containsLinq,bool enabled) {
        var body=containsLinq
            ? "yield return values.Where(static value=>value>0).First();"
            : "foreach(var value in values){yield return value;}";
        var source=$$"""
using System.Collections.Generic;
using System.Linq;
[Seamless.FLinq.LinqIntercept({{enabled.ToString().ToLowerInvariant()}})]
public class Container {
    public IEnumerable<int> Source(int[] values){ {{body}} }
    public IEnumerable<int> Run(int[] values)=>this.Source(values);
}
""";
        var (result,compilation)=RunGenerator(source);
        Assert.DoesNotContain(compilation.GetDiagnostics(),static diagnostic=>diagnostic.Severity==global::Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(containsLinq&&enabled,result.Diagnostics.Any(static diagnostic=>diagnostic.Id=="FLINQ004"));
    }

    private void AssertIntercepted(string source) {
        var generated = GeneratedSource(source);
        GeneratedSourceAssert.True(
            generated.Contains("SumPositive_",StringComparison.Ordinal),
            "Expected SumPositive interceptor was not generated.",
            generated,
            output);
    }

    private void AssertNotIntercepted(string source) {
        var generated = GeneratedSource(source);
        GeneratedSourceAssert.True(
            !generated.Contains("SumPositive_",StringComparison.Ordinal),
            "SumPositive interceptor should not be generated.",
            generated,
            output);
    }

    private static (global::Microsoft.CodeAnalysis.GeneratorDriverRunResult Result,global::Microsoft.CodeAnalysis.Compilation OutputCompilation) RunGenerator(string source) {
        var compilation=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        global::Microsoft.CodeAnalysis.GeneratorDriver driver=GeneratorTestHelpers.CreateDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out _);
        return (driver.GetRunResult(),outputCompilation);
    }

    private static string GeneratedText(global::Microsoft.CodeAnalysis.GeneratorDriverRunResult result) =>
        string.Concat(result.Results
            .SelectMany(static generatorResult => generatorResult.GeneratedSources)
            .Select(static generatedSource => generatedSource.SourceText.ToString()));
    private static string GeneratedSource(string source) {
        var comp = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        var driver = GeneratorTestHelpers.CreateDriver().RunGenerators(comp);
        var result = driver.GetRunResult();

        return string.Concat(result.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));
    }
}
