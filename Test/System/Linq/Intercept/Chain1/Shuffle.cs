using System.Linq;


namespace Test.System.Linq.Intercept.Chain1;

public sealed class Shuffle(ITestOutputHelper output):LinqChain1TestBase(output) {
    [Fact,Trait("Category","Unit")]
    public void noArg() {
        var expected=SourceWithDuplicates().Shuffle().Order().ToList();
        this.MeasureOptimized(nameof(noArgOptimized),()=>noArgOptimized(expected));
        this.MeasureOptimized(nameof(noArgNoOptimized),()=>noArgNoOptimized(expected));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void noArgOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Shuffle().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void noArgNoOptimized(dynamic expected) {
        var actual=SourceWithDuplicates().Shuffle().Order().ToList();
        AssertSequenceEqual(expected,actual);
    }
}
