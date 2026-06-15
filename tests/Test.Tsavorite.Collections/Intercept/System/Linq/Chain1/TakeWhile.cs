using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain1;

public sealed class TakeWhile:LinqChain1TestBase {
    [Fact, Trait("Category","Unit")]
    public static void predicate_Static() {
        var expected=Source().TakeWhile(static v => v<3).ToList();
        predicate_StaticOptimized(expected);
        predicate_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(static v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(static v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Nested() {
        var expected=Source().TakeWhile(p => Source().TakeWhile(q => q<=p).Any()).ToList();
        predicate_NestedOptimized(expected);
        predicate_NestedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_NestedOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(p => Source().TakeWhile(q => q<=p).Any()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_NestedNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(p => Source().TakeWhile(q => q<=p).Any()).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Lambda() {
        var expected=Source().TakeWhile(v => v<3).ToList();
        predicate_LambdaOptimized(expected);
        predicate_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(v => v<3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Captured() {
        var threshold=3;
        var expected=Source().TakeWhile(v => v<threshold).ToList();
        predicate_CapturedOptimized(expected);
        predicate_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_CapturedOptimized(dynamic expected) {
        var threshold=3;
        var actual=new Consumer<int>();
        Source().TakeWhile(v => v<threshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_CapturedNoOptimized(dynamic expected) {
        var threshold=3;
        var actual=new Consumer<int>();
        Source().TakeWhile(v => v<threshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_DelegateSyntax() {
        var expected=Source().TakeWhile(delegate(int v) { return v<3; }).ToList();
        predicate_DelegateSyntaxOptimized(expected);
        predicate_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(delegate(int v) { return v<3; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(delegate(int v) { return v<3; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=Source().TakeWhile(predicate).ToList();
        predicate_VariableOptimized(expected);
        predicate_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_VariableOptimized(dynamic expected) {
        Func<int,bool> predicate=v => v<3;
        var actual=new Consumer<int>();
        Source().TakeWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_VariableNoOptimized(dynamic expected) {
        Func<int,bool> predicate=v => v<3;
        var actual=new Consumer<int>();
        Source().TakeWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Static() {
        var expected=Source().TakeWhile(static (v,i) => v>i).ToList();
        predicate_index_StaticOptimized(expected);
        predicate_index_StaticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_StaticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(static (v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(static (v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Lambda() {
        var expected=Source().TakeWhile((v,i) => v>i).ToList();
        predicate_index_LambdaOptimized(expected);
        predicate_index_LambdaNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile((v,i) => v>i).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Captured() {
        var predicate=new IntIndexPredicate(0);
        var expected=Source().TakeWhile(predicate.LessThanThreshold).ToList();
        predicate_index_CapturedOptimized(expected);
        predicate_index_CapturedNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_CapturedOptimized(dynamic expected) {
        var predicate=new IntIndexPredicate(0);
        var actual=new Consumer<int>();
        Source().TakeWhile(predicate.LessThanThreshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_CapturedNoOptimized(dynamic expected) {
        var predicate=new IntIndexPredicate(0);
        var actual=new Consumer<int>();
        Source().TakeWhile(predicate.LessThanThreshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_DelegateSyntax() {
        var expected=Source().TakeWhile(delegate(int v,int i) { return v>i; }).ToList();
        predicate_index_DelegateSyntaxOptimized(expected);
        predicate_index_DelegateSyntaxNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(delegate(int v,int i) { return v>i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
        Source().TakeWhile(delegate(int v,int i) { return v>i; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public static void predicate_index_Variable() {
        Func<int,int,bool> predicate=(v,i) => v>i;
        var expected=Source().TakeWhile(predicate).ToList();
        predicate_index_VariableOptimized(expected);
        predicate_index_VariableNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_VariableOptimized(dynamic expected) {
        Func<int,int,bool> predicate=(v,i) => v>i;
        var actual=new Consumer<int>();
        Source().TakeWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void predicate_index_VariableNoOptimized(dynamic expected) {
        Func<int,int,bool> predicate=(v,i) => v>i;
        var actual=new Consumer<int>();
        Source().TakeWhile(predicate).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
