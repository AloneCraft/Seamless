using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class Zip:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void second() {
        var expected=Source().Zip(Second()).ToList();
        secondOptimized(expected);
        secondNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondOptimized(dynamic expected) {
        var actual=new Consumer<(int,int)>();
        Source().Zip(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void secondNoOptimized(dynamic expected) {
        var actual=new Consumer<(int,int)>();
        Source().Zip(Second()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_third() {
        var expected=Source().Zip(Second(),EqualSource()).ToList();
        second_thirdOptimized(expected);
        second_thirdNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_thirdOptimized(dynamic expected) {
        var actual=Source().Zip(Second(),EqualSource()).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_thirdNoOptimized(dynamic expected) {
        var actual=Source().Zip(Second(),EqualSource()).ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Static() {
        var expected=Source().Zip(Second(),static (a,b) => a+b).ToList();
        second_resultSelector_StaticOptimized(expected);
        second_resultSelector_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Zip(Second(),static (a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Zip(Second(),static (a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Lambda() {
        var expected=Source().Zip(Second(),(a,b) => a+b).ToList();
        second_resultSelector_LambdaOptimized(expected);
        second_resultSelector_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Zip(Second(),(a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Zip(Second(),(a,b) => a+b).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Captured() {
        var offset=0;
        var expected=Source().Zip(Second(),(a,b) => a+b+offset).ToList();
        second_resultSelector_CapturedOptimized(expected);
        second_resultSelector_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_CapturedOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().Zip(Second(),(a,b) => a+b+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_CapturedNoOptimized(dynamic expected) {
        var offset=0;
        var actual=new Consumer<int>();
        Source().Zip(Second(),(a,b) => a+b+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_DelegateSyntax() {
        var expected=Source().Zip(Second(),delegate(int a,int b) { return a+b; }).ToList();
        second_resultSelector_DelegateSyntaxOptimized(expected);
        second_resultSelector_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Zip(Second(),delegate(int a,int b) { return a+b; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().Zip(Second(),delegate(int a,int b) { return a+b; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void second_resultSelector_Variable() {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var expected=Source().Zip(Second(),resultSelector).ToList();
        second_resultSelector_VariableOptimized(expected);
        second_resultSelector_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_VariableOptimized(dynamic expected) {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var actual=new Consumer<int>();
        Source().Zip(Second(),resultSelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void second_resultSelector_VariableNoOptimized(dynamic expected) {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var actual=new Consumer<int>();
        Source().Zip(Second(),resultSelector).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
