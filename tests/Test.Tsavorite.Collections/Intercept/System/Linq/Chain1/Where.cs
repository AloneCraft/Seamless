using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.Chain1;

public sealed class Where:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() => predicate_StaticImpl();

    private static void predicate_StaticImpl() {
        var expected=SystemEnumerable.Where(Source(),static v => v<3).ToList();
        var actual=new Consumer<int>();
        Source().Where(static v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() => predicate_LambdaImpl();

    private static void predicate_LambdaImpl() {
        var expected=SystemEnumerable.Where(Source(),v => v<3).ToList();
        var actual=new Consumer<int>();
        Source().Where(v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() => predicate_CapturedImpl();

    private static void predicate_CapturedImpl() {
        var threshold=3;
        var expected=SystemEnumerable.Where(Source(),v => v<threshold).ToList();
        var actual=new Consumer<int>();
        Source().Where(v => v<threshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() => predicate_DelegateSyntaxImpl();

    private static void predicate_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Where(Source(),delegate(int v) { return v<3; }).ToList();
        var actual=new Consumer<int>();
        Source().Where(delegate(int v) { return v<3; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() => predicate_VariableImpl();

    private static void predicate_VariableImpl() {
        Func<int,bool> predicate=v => v<3;
        var expected=SystemEnumerable.Where(Source(),predicate).ToList();
        var actual=new Consumer<int>();
        Source().Where(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Nested() => predicate_NestedImpl();

    private static void predicate_NestedImpl() {
        var expected=SystemEnumerable.Where(Source(),p => SystemEnumerable.Any(SystemEnumerable.Where(Source(),q => q==p))).ToList();
        var actual=new Consumer<int>();
        Source().Where(p => Source().Where(q => q==p).Any()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Query() => predicate_QueryImpl();

    private static void predicate_QueryImpl() {
        var expected=SystemEnumerable.Where(Source(),v => v<3).ToList();
        var actual=new Consumer<int>();
        (from v in Source() where v<3 select v).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Static() => predicate_index_StaticImpl();

    private static void predicate_index_StaticImpl() {
        var expected=SystemEnumerable.Where(Source(),static (v,i) => v>i).ToList();
        var actual=new Consumer<int>();
        Source().Where(static (v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Lambda() => predicate_index_LambdaImpl();

    private static void predicate_index_LambdaImpl() {
        var expected=SystemEnumerable.Where(Source(),(v,i) => v>i).ToList();
        var actual=new Consumer<int>();
        Source().Where((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Captured() => predicate_index_CapturedImpl();

    private static void predicate_index_CapturedImpl() {
        var predicate=new IntIndexPredicate(0);
        var expected=SystemEnumerable.Where(Source(),predicate.LessThanThreshold).ToList();
        var actual=new Consumer<int>();
        Source().Where(predicate.LessThanThreshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_DelegateSyntax() => predicate_index_DelegateSyntaxImpl();

    private static void predicate_index_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Where(Source(),delegate(int v,int i) { return v>i; }).ToList();
        var actual=new Consumer<int>();
        Source().Where(delegate(int v,int i) { return v>i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Variable() => predicate_index_VariableImpl();

    private static void predicate_index_VariableImpl() {
        Func<int,int,bool> predicate=(v,i) => v>i;
        var expected=SystemEnumerable.Where(Source(),predicate).ToList();
        var actual=new Consumer<int>();
        Source().Where(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
