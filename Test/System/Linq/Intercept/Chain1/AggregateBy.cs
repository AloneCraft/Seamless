using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class AggregateBy(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void keySelector_seed_func_Static() {
        var expected=Rows().AggregateBy(static row => row.Key,0,static (total,row) => total+row.Value).ToList();
        this.MeasureOptimized(nameof(keySelector_seed_func_StaticOptimized),()=>keySelector_seed_func_StaticOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_seed_func_StaticNoOptimized),()=>keySelector_seed_func_StaticNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_StaticOptimized(dynamic expected) {
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(static row => row.Key,0,static (total,row) => total+row.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_StaticNoOptimized(dynamic expected) {
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(static row => row.Key,0,static (total,row) => total+row.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_seed_func_Lambda() {
        var expected=Rows().AggregateBy(row => row.Key,0,(total,row) => total+row.Value).ToList();
        this.MeasureOptimized(nameof(keySelector_seed_func_LambdaOptimized),()=>keySelector_seed_func_LambdaOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_seed_func_LambdaNoOptimized),()=>keySelector_seed_func_LambdaNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_LambdaOptimized(dynamic expected) {
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(row => row.Key,0,(total,row) => total+row.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_LambdaNoOptimized(dynamic expected) {
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(row => row.Key,0,(total,row) => total+row.Value).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_seed_func_Captured() {
        var offset=1;
        var expected=Rows().AggregateBy(row => row.Key+offset,0,(total,row) => total+row.Value+offset).ToList();
        this.MeasureOptimized(nameof(keySelector_seed_func_CapturedOptimized),()=>keySelector_seed_func_CapturedOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_seed_func_CapturedNoOptimized),()=>keySelector_seed_func_CapturedNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_CapturedOptimized(dynamic expected) {
        var offset=1;
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(row => row.Key+offset,0,(total,row) => total+row.Value+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_CapturedNoOptimized(dynamic expected) {
        var offset=1;
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(row => row.Key+offset,0,(total,row) => total+row.Value+offset).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_seed_func_DelegateSyntax() {
        var expected=Rows().AggregateBy(delegate(Chain1Row row) { return row.Key; },0,delegate(int total,Chain1Row row) { return total+row.Value; }).ToList();
        this.MeasureOptimized(nameof(keySelector_seed_func_DelegateSyntaxOptimized),()=>keySelector_seed_func_DelegateSyntaxOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_seed_func_DelegateSyntaxNoOptimized),()=>keySelector_seed_func_DelegateSyntaxNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_DelegateSyntaxOptimized(dynamic expected) {
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(delegate(Chain1Row row) { return row.Key; },0,delegate(int total,Chain1Row row) { return total+row.Value; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_DelegateSyntaxNoOptimized(dynamic expected) {
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(delegate(Chain1Row row) { return row.Key; },0,delegate(int total,Chain1Row row) { return total+row.Value; }).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact, Trait("Category","Unit")]
    public void keySelector_seed_func_Variable() {
        Func<Chain1Row,int> keySelector=row => row.Key;
        Func<int,Chain1Row,int> func=(total,row) => total+row.Value;
        var expected=Rows().AggregateBy(keySelector,0,func).ToList();
        this.MeasureOptimized(nameof(keySelector_seed_func_VariableOptimized),()=>keySelector_seed_func_VariableOptimized(expected));
        this.MeasureOptimized(nameof(keySelector_seed_func_VariableNoOptimized),()=>keySelector_seed_func_VariableNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_VariableOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        Func<int,Chain1Row,int> func=(total,row) => total+row.Value;
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(keySelector,0,func).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void keySelector_seed_func_VariableNoOptimized(dynamic expected) {
        Func<Chain1Row,int> keySelector=row => row.Key;
        Func<int,Chain1Row,int> func=(total,row) => total+row.Value;
        var actual=new Consumer<KeyValuePair<int,int>>();
        Rows().AggregateBy(keySelector,0,func).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

}
