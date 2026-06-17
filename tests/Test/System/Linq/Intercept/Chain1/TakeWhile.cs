using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class TakeWhile(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void predicate_Static() {
        var expected=Source().TakeWhile(static v => v<3).ToList();
        MeasureOptimized(nameof(predicate_StaticOptimized),()=>predicate_StaticOptimized(expected));
        MeasureOptimized(nameof(predicate_StaticNoOptimized),()=>predicate_StaticNoOptimized(expected));
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
    public void predicate_Nested() {
        var expected=Source().TakeWhile(p => Source().TakeWhile(q => q<=p).Any()).ToList();
        MeasureOptimized(nameof(predicate_NestedOptimized),()=>predicate_NestedOptimized(expected));
        MeasureOptimized(nameof(predicate_NestedNoOptimized),()=>predicate_NestedNoOptimized(expected));
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
    public void predicate_Lambda() {
        var expected=Source().TakeWhile(v => v<3).ToList();
        MeasureOptimized(nameof(predicate_LambdaOptimized),()=>predicate_LambdaOptimized(expected));
        MeasureOptimized(nameof(predicate_LambdaNoOptimized),()=>predicate_LambdaNoOptimized(expected));
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
    public void predicate_Captured() {
        var threshold=3;
        var expected=Source().TakeWhile(v => v<threshold).ToList();
        MeasureOptimized(nameof(predicate_CapturedOptimized),()=>predicate_CapturedOptimized(expected));
        MeasureOptimized(nameof(predicate_CapturedNoOptimized),()=>predicate_CapturedNoOptimized(expected));
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
    public void predicate_DelegateSyntax() {
        var expected=Source().TakeWhile(delegate(int v) { return v<3; }).ToList();
        MeasureOptimized(nameof(predicate_DelegateSyntaxOptimized),()=>predicate_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(predicate_DelegateSyntaxNoOptimized),()=>predicate_DelegateSyntaxNoOptimized(expected));
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
    public void predicate_Variable() {
        Func<int,bool> predicate=v => v<3;
        var expected=Source().TakeWhile(predicate).ToList();
        MeasureOptimized(nameof(predicate_VariableOptimized),()=>predicate_VariableOptimized(expected));
        MeasureOptimized(nameof(predicate_VariableNoOptimized),()=>predicate_VariableNoOptimized(expected));
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
    public void predicate_index_Static() {
        var expected=Source().TakeWhile(static (v,i) => v>i).ToList();
        MeasureOptimized(nameof(predicate_index_StaticOptimized),()=>predicate_index_StaticOptimized(expected));
        MeasureOptimized(nameof(predicate_index_StaticNoOptimized),()=>predicate_index_StaticNoOptimized(expected));
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
    public void predicate_index_Lambda() {
        var expected=Source().TakeWhile((v,i) => v>i).ToList();
        MeasureOptimized(nameof(predicate_index_LambdaOptimized),()=>predicate_index_LambdaOptimized(expected));
        MeasureOptimized(nameof(predicate_index_LambdaNoOptimized),()=>predicate_index_LambdaNoOptimized(expected));
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
    public void predicate_index_Captured() {
        var predicate=new IntIndexPredicate(0);
        var expected=Source().TakeWhile(predicate.LessThanThreshold).ToList();
        MeasureOptimized(nameof(predicate_index_CapturedOptimized),()=>predicate_index_CapturedOptimized(expected));
        MeasureOptimized(nameof(predicate_index_CapturedNoOptimized),()=>predicate_index_CapturedNoOptimized(expected));
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
    public void predicate_index_DelegateSyntax() {
        var expected=Source().TakeWhile(delegate(int v,int i) { return v>i; }).ToList();
        MeasureOptimized(nameof(predicate_index_DelegateSyntaxOptimized),()=>predicate_index_DelegateSyntaxOptimized(expected));
        MeasureOptimized(nameof(predicate_index_DelegateSyntaxNoOptimized),()=>predicate_index_DelegateSyntaxNoOptimized(expected));
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
    public void predicate_index_Variable() {
        Func<int,int,bool> predicate=(v,i) => v>i;
        var expected=Source().TakeWhile(predicate).ToList();
        MeasureOptimized(nameof(predicate_index_VariableOptimized),()=>predicate_index_VariableOptimized(expected));
        MeasureOptimized(nameof(predicate_index_VariableNoOptimized),()=>predicate_index_VariableNoOptimized(expected));
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
