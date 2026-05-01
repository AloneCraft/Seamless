using e = System.Linq.Enumerable;
using a = Seamless.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class EnumerableMethod2 {
    [Fact, Trait("Category","Intercept")]private static void Select_selector_Aggregate_func()=>Assert.Equal(
        e.Aggregate(e.Select(Source(),static (left,right) => left+right),static (left,right) => left+right),
        a.Aggregate(a.Select(Source(),static (left,right) => left+right),static (left,right) => left+right)
    );
    [Fact, Trait("Category","Intercept")]private static void Aggregate_func()=>Assert.Equal(
        e.Aggregate(Source(),static (left,right) => left+right),
        a.Aggregate(Source(),static (left,right) => left+right)
    );
    private static int[] Source() => [1,2,3,4];
}
