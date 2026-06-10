using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class FirstOrDefault:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void NoDelegate() => NoDelegateImpl();

    private static void NoDelegateImpl() {
        var expected=SystemEnumerable.FirstOrDefault(Source());
        var actual=Source().FirstOrDefault();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void defaultValue() => defaultValueImpl();

    private static void defaultValueImpl() {
        var expected=SystemEnumerable.FirstOrDefault(EmptySource(),42);
        var actual=EmptySource().FirstOrDefault(42);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),static v => v>2);
        var actual=Source().FirstOrDefault(static v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Nested() => predicate_NestedImpl();

    private static void predicate_NestedImpl() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),p => SystemEnumerable.FirstOrDefault(Source(),q => q==p)==p);
        var actual=Source().FirstOrDefault(p => Source().FirstOrDefault(q => q==p)==p);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),v => v>2);
        var actual=Source().FirstOrDefault(v => v>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=2;
        var expected=SystemEnumerable.FirstOrDefault(Source(),v => v>threshold);
        var actual=Source().FirstOrDefault(v => v>threshold);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.FirstOrDefault(Source(),delegate(int v) { return v>2; });
        var actual=Source().FirstOrDefault(delegate(int v) { return v>2; });
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v>2;
        var expected=SystemEnumerable.FirstOrDefault(Source(),predicate);
        var actual=Source().FirstOrDefault(predicate);
        Assert.Equal(expected,actual);
    }

}
