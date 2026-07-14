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

    private static string GeneratedSource(string source) {
        var comp = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        var driver = GeneratorTestHelpers.CreateDriver().RunGenerators(comp);
        var result = driver.GetRunResult();

        return string.Concat(result.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));
    }
}
