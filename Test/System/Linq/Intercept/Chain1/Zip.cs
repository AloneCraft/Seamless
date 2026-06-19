using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Zip(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void second() {
        var expected=Source().Zip(Second()).ToList();
        this.MeasureOptimized(nameof(secondOptimized),()=>secondOptimized(expected));
        this.MeasureOptimized(nameof(secondNoOptimized),()=>secondNoOptimized(expected));
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
    public void second_third() {
        var expected=Source().Zip(Second(),EqualSource()).ToList();
        this.MeasureOptimized(nameof(second_thirdOptimized),()=>second_thirdOptimized(expected));
        this.MeasureOptimized(nameof(second_thirdNoOptimized),()=>second_thirdNoOptimized(expected));
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
    public void second_resultSelector_Static() {
        var expected=Source().Zip(Second(),static (a,b) => a+b).ToList();
        this.MeasureOptimized(nameof(second_resultSelector_StaticOptimized),()=>second_resultSelector_StaticOptimized(expected));
        this.MeasureOptimized(nameof(second_resultSelector_StaticNoOptimized),()=>second_resultSelector_StaticNoOptimized(expected));
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
    public void second_resultSelector_Lambda() {
        var expected=Source().Zip(Second(),(a,b) => a+b).ToList();
        this.MeasureOptimized(nameof(second_resultSelector_LambdaOptimized),()=>second_resultSelector_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(second_resultSelector_LambdaNoOptimized),()=>second_resultSelector_LambdaNoOptimized(expected));
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
    public void second_resultSelector_Captured() {
        var offset=0;
        var expected=Source().Zip(Second(),(a,b) => a+b+offset).ToList();
        this.MeasureOptimized(nameof(second_resultSelector_CapturedOptimized),()=>second_resultSelector_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(second_resultSelector_CapturedNoOptimized),()=>second_resultSelector_CapturedNoOptimized(expected));
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
    public void second_resultSelector_DelegateSyntax() {
        var expected=Source().Zip(Second(),delegate(int a,int b) { return a+b; }).ToList();
        this.MeasureOptimized(nameof(second_resultSelector_DelegateSyntaxOptimized),()=>second_resultSelector_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(second_resultSelector_DelegateSyntaxNoOptimized),()=>second_resultSelector_DelegateSyntaxNoOptimized(expected));
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
    public void second_resultSelector_Variable() {
        Func<int,int,int> resultSelector=(a,b) => a+b;
        var expected=Source().Zip(Second(),resultSelector).ToList();
        this.MeasureOptimized(nameof(second_resultSelector_VariableOptimized),()=>second_resultSelector_VariableOptimized(expected));
        this.MeasureOptimized(nameof(second_resultSelector_VariableNoOptimized),()=>second_resultSelector_VariableNoOptimized(expected));
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
