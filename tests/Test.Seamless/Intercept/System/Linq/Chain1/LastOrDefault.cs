using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class LastOrDefault:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.Source());
        var actual=LinqChain1TestBase.Source().LastOrDefault();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void defaultValue() => defaultValueImpl();

    private static void defaultValueImpl() {
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.EmptySource(),42);
        var actual=LinqChain1TestBase.EmptySource().LastOrDefault(42);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.Source(),static v => v>2);
        var actual=LinqChain1TestBase.Source().LastOrDefault(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.Source(),v => v>2);
        var actual=LinqChain1TestBase.Source().LastOrDefault(v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=2;
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.Source(),v => v>threshold);
        var actual=LinqChain1TestBase.Source().LastOrDefault(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.Source(),delegate(int v) { return v>2; });
        var actual=LinqChain1TestBase.Source().LastOrDefault(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.LastOrDefault(LinqChain1TestBase.Source(),predicate);
        var actual=LinqChain1TestBase.Source().LastOrDefault(predicate);
        Assert.Equal(expected,actual);
    }

}
