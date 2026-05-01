using e = System.Linq.Enumerable;
using a = Seamless.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class EnumerableMethod2 {
    [Fact, Trait("Category","Intercept")]
    private static void Where_Select_Aggregate_func() => Assert.Equal(
        e.Aggregate(e.Select(e.Where(Source(),static value => value>1),static value => value*10),static (left,right) => left+right),
        a.Aggregate(a.Select(a.Where(Source(),static value => value>1),static value => value*10),static (left,right) => left+right)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Select_index_Where_ToArray() => Assert.Equal(
        e.ToArray(e.Where(e.Select(Source(),static (value,index) => value+index),static value => value>4)),
        a.ToArray(a.Where(a.Select(Source(),static (value,index) => value+index),static value => value>4))
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderBy_Select_Take_ToArray() => Assert.Equal(
        e.ToArray(e.Take(e.Select(e.OrderBy(Source(),static value => -value),static value => value+1),3)),
        a.ToArray(a.Take(a.Select(a.OrderBy(Source(),static value => -value),static value => value+1),3))
    );
    private static int[] Source() => [1,2,3,4];
}
