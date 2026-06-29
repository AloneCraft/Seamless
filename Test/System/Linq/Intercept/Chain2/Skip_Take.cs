namespace Test.System.Linq.Intercept.Chain2;

public sealed class Skip_Take(ITestOutputHelper output):LinqChain2TestBase(output) {
    [Fact, Trait("Category","Unit")]
    public void NoDelegate() {
        var expected=Source().Skip(2).Take(3).ToList();
        this.MeasureOptimized(nameof(NoDelegateOptimized),()=>NoDelegateOptimized(expected));
        this.MeasureOptimized(nameof(NoDelegateNoOptimized),()=>NoDelegateNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Skip(2).Take(3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void NoDelegateNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Source().Skip(2).Take(3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
