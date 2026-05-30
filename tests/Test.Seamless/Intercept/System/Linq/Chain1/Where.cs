using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain1;

public sealed class Where:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),static v => v<3).ToList();
        var actual=LinqChain1TestBase.Source().Where(static v => v<3).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),v => v<3).ToList();
        var actual=LinqChain1TestBase.Source().Where(v => v<3).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=3;
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),v => v<threshold).ToList();
        var actual=LinqChain1TestBase.Source().Where(v => v<threshold).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),delegate(int v) { return v<3; }).ToList();
        var actual=LinqChain1TestBase.Source().Where(delegate(int v) { return v<3; }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),predicate).ToList();
        var actual=LinqChain1TestBase.Source().Where(predicate).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Static() => predicate_index_StaticImpl();

    private static void predicate_index_StaticImpl() {
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),static (v,i) => v>i).ToList();
        var actual=LinqChain1TestBase.Source().Where(static (v,i) => v>i).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Lambda() => predicate_index_LambdaImpl();

    private static void predicate_index_LambdaImpl() {
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),(v,i) => v>i).ToList();
        var actual=LinqChain1TestBase.Source().Where((v,i) => v>i).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Captured() => predicate_index_CapturedImpl();

    private static void predicate_index_CapturedImpl() {
        var predicate=new LinqChain1TestBase.IntIndexPredicate(0);
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),predicate.LessThanThreshold).ToList();
        var actual=LinqChain1TestBase.Source().Where(predicate.LessThanThreshold).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_DelegateSyntax() => predicate_index_DelegateSyntaxImpl();

    private static void predicate_index_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),delegate(int v,int i) { return v>i; }).ToList();
        var actual=LinqChain1TestBase.Source().Where(delegate(int v,int i) { return v>i; }).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Variable() => predicate_index_VariableImpl();

    private static void predicate_index_VariableImpl() {
        Func<int,int,bool> predicate=(v,i) => v>i;
        var expected=SystemEnumerable.Where(LinqChain1TestBase.Source(),predicate).ToList();
        var actual=LinqChain1TestBase.Source().Where(predicate).ToList();
        LinqChain1TestBase.AssertSequenceEqual(expected,actual);
    }

}
