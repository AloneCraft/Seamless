using LinqInstrumentation=Seamless.FLinq.LinqInstrumentation;
using Seamless.Linq;
using LinqInterceptAttribute=Seamless.FLinq.LinqInterceptAttribute;
using ClassInt32=Seamless.TestKit.ClassInt32;

namespace Test.FLinqBackends.TsavoriteEnumerable;

public sealed class TsavoriteEnumerableRootTests {
    private static global::Seamless.Collections.Tsavorite.HashSet<int>? source;

    [Fact,Trait("Category","Unit")]
    public void TsavoriteHashSetRoute_UsesIEnumerableRootWithoutNativeIterateState() {
        using var store=global::Seamless.Collections.Tsavorite.Store.Create().CreateHashSet<int>();
        using var set=store.Create();
        foreach(var value in new[]{1,2,3,4})
            set.Add(value);
        source=set;
        try {
            Assert.Equal(2,CountEvenImpl());
            var generated=GeneratedFor(nameof(CountEvenImpl));
            Assert.DoesNotContain("TsavoriteHashSetIterateState",generated);
            Assert.DoesNotContain("Seamless.Collections.Iteration.IStrategy",generated);
            Assert.DoesNotContain(".Iterate(ref __TsavoriteHashSetIterateState",generated);
        } finally {
            source=null;
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl()=>Source().AsX().Count(static value=>value%2==0);

    private static global::Seamless.Collections.Tsavorite.HashSet<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
    private static string GeneratedFor(string methodName)=>Test.System.Linq.Intercept.GeneratedInterceptorAssert.SourceFor<TsavoriteEnumerableRootTests>(methodName);
}

internal static class Erasure{
    public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::Seamless.Collections.Tsavorite.HashSet<T> source){
        global::System.Collections.Generic.IEnumerable<T> result=source;
        return result;
    }
}
