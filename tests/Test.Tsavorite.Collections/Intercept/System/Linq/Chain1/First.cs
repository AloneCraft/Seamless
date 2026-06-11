using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class First:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.First(Source());
        var actual=Source().First();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.First(Source(),static v => v>2);
        var actual=Source().First(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Nested() => predicate_NestedImpl();

    private static void predicate_NestedImpl() {
        var expected=SystemEnumerable.First(Source(),p => SystemEnumerable.First(Source(),q => q==p)==p);
        var actual=Source().First(p => Source().First(q => q==p)==p);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.First(Source(),v => v>2);
        var actual=Source().First(v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=2;
        var expected=SystemEnumerable.First(Source(),v => v>threshold);
        var actual=Source().First(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.First(Source(),delegate(int v) { return v>2; });
        var actual=Source().First(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.First(Source(),predicate);
        var actual=Source().First(predicate);
        Assert.Equal(expected,actual);
    }

}
