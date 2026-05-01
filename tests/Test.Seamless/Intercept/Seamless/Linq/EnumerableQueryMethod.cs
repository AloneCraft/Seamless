using Seamless.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class EnumerableQueryMethod {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
        (from value in Source() select value+1).Select(static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index),
        (from value in Source() select value+1).Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
        (from value in Source() select value*10).Where(static value => value>20)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
        (from value in Source() select value*10).Where(static (value,index) => value>20&&index>0)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
        (from value in Source() where value>1 select value).Select(static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index),
        (from value in Source() where value>1 select value).Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
        (from value in Source() where value>1 select value).Where(static value => value<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
        (from value in Source() where value>1 select value).Where(static (value,index) => value<4&&index<2)
    );

    private static int[] Source() => [1,2,3,4];
}
