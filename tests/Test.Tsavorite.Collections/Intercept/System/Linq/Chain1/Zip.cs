using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Zip:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() => secondImpl();

    private static void secondImpl() {
        var expected=SystemEnumerable.Zip(Source(),Second()).ToList();
        var actual=new Consumer<(int,int)>();
        Source().Zip(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_third() => second_thirdImpl();

    private static void second_thirdImpl() {
        var expected=SystemEnumerable.Zip(Source(),Second(),EqualSource()).ToList();
        var actual=Source().Zip(Second(),EqualSource()).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Static() => second_resultSelector_StaticImpl();

    private static void second_resultSelector_StaticImpl() {
        var expected=SystemEnumerable.Zip(Source(),Second(),static (a,b) => a+b).ToList();
        var actual=new Consumer<int>();
        Source().Zip(Second(),static (a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Lambda() => second_resultSelector_LambdaImpl();

    private static void second_resultSelector_LambdaImpl() {
        var expected=SystemEnumerable.Zip(Source(),Second(),(a,b) => a+b).ToList();
        var actual=new Consumer<int>();
        Source().Zip(Second(),(a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Captured() => second_resultSelector_CapturedImpl();

    private static void second_resultSelector_CapturedImpl() {
        var offset=0;
        var expected=SystemEnumerable.Zip(Source(),Second(),(a,b) => a+b+offset).ToList();
        var actual=new Consumer<int>();
        Source().Zip(Second(),(a,b) => a+b+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_DelegateSyntax() => second_resultSelector_DelegateSyntaxImpl();

    private static void second_resultSelector_DelegateSyntaxImpl() {
        var expected=SystemEnumerable.Zip(Source(),Second(),delegate(int a,int b) { return a+b; }).ToList();
        var actual=new Consumer<int>();
        Source().Zip(Second(),delegate(int a,int b) { return a+b; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Variable() => second_resultSelector_VariableImpl();

    private static void second_resultSelector_VariableImpl() {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var expected=SystemEnumerable.Zip(Source(),Second(),resultSelector).ToList();
        var actual=new Consumer<int>();
        Source().Zip(Second(),resultSelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
