namespace Test.Tsavorite.SourceGenerator;

public class LinqRewriteAttributeTests {
    // メソッドに [NoLinqRewrite] を付けると、LINQ を含んでいてもインターセプターが生成されない。
    private const string NoRewriteOnMethod = """
        using System.Linq;
        using Tsavorite.SourceGenerator;
        namespace Sample;
        public class Container {
            [NoLinqRewrite]
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    // 属性が無ければ、デフォルトで展開（インターセプター生成）される。
    private const string DefaultRewrite = """
        using System.Linq;
        namespace Sample;
        public class Container {
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    // クラスに [NoLinqRewrite] を付けると、配下のメソッドは展開されない。
    private const string NoRewriteOnClass = """
        using System.Linq;
        using Tsavorite.SourceGenerator;
        namespace Sample;
        [NoLinqRewrite]
        public class Container {
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    // クラスに [NoLinqRewrite] を付けても、メソッドの [LinqRewrite] が優先され展開される。
    private const string MethodOverridesClass = """
        using System.Linq;
        using Tsavorite.SourceGenerator;
        namespace Sample;
        [NoLinqRewrite]
        public class Container {
            [LinqRewrite]
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    // アセンブリ全体に [NoLinqRewrite] を付けても、クラスの [LinqRewrite] が優先され展開される。
    private const string ClassOverridesAssembly = """
        using System.Linq;
        using Tsavorite.SourceGenerator;
        [assembly: NoLinqRewrite]
        namespace Sample;
        [LinqRewrite]
        public class Container {
            private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
            public int Run(int[] src) => SumPositive(src);
        }
        """;

    // ネストしたクラスでは、内側のクラスの属性が外側より優先される。
    private const string InnerClassOverridesOuter = """
        using System.Linq;
        using Tsavorite.SourceGenerator;
        namespace Sample;
        [NoLinqRewrite]
        public class Outer {
            [LinqRewrite]
            public class Inner {
                private static int SumPositive(int[] src) => src.Where(x => x>0).Sum();
                public int Run(int[] src) => SumPositive(src);
            }
        }
        """;

    [Fact]
    public void NoLinqRewrite_OnMethod_NotIntercepted() {
        Assert.False(IsSumPositiveIntercepted(NoRewriteOnMethod));
    }

    [Fact]
    public void NoAttribute_DefaultsToIntercepted() {
        Assert.True(IsSumPositiveIntercepted(DefaultRewrite));
    }

    [Fact]
    public void NoLinqRewrite_OnClass_NotIntercepted() {
        Assert.False(IsSumPositiveIntercepted(NoRewriteOnClass));
    }

    [Fact]
    public void LinqRewrite_OnMethod_OverridesNoRewriteOnClass() {
        Assert.True(IsSumPositiveIntercepted(MethodOverridesClass));
    }

    [Fact]
    public void LinqRewrite_OnClass_OverridesNoRewriteOnAssembly() {
        Assert.True(IsSumPositiveIntercepted(ClassOverridesAssembly));
    }

    [Fact]
    public void LinqRewrite_OnInnerClass_OverridesNoRewriteOnOuterClass() {
        Assert.True(IsSumPositiveIntercepted(InnerClassOverridesOuter));
    }

    private static bool IsSumPositiveIntercepted(string source) {
        var comp=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        var driver=GeneratorTestHelpers.CreateDriver().RunGenerators(comp);
        var result=driver.GetRunResult();

        var generated=string.Concat(result.Results
            .SelectMany(r=>r.GeneratedSources)
            .Select(s=>s.SourceText.ToString()));

        return generated.Contains("SumPositive_",StringComparison.Ordinal);
    }
}
