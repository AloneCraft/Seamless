using Seamless.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public static class EnumerableMethodQuery {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Select(Source,static value => value+1),static value => value*10),
        from value in Source.Select(static value => value+1) select value*10
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Select(Source,static value => value*10),static value => value>20),
        from value in Source.Select(static value => value*10) where value>20 select value
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Select(Source,static (value,index) => value+index),static value => value*10),
        from value in Source.Select(static (value,index) => value+index) select value*10
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Select(Source,static (value,index) => value+index),static value => value>4),
        from value in Source.Select(static (value,index) => value+index) where value>4 select value
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Where(Source,static value => value>1),static value => value*10),
        from value in Source.Where(static value => value>1) select value*10
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Where(Source,static value => value>1),static value => value<4),
        from value in Source.Where(static value => value>1) where value<4 select value
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select() => Assert.Equal(
        SystemEnumerable.Select(SystemEnumerable.Where(Source,static (value,index) => value>index),static value => value*10),
        from value in Source.Where(static (value,index) => value>index) select value*10
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where() => Assert.Equal(
        SystemEnumerable.Where(SystemEnumerable.Where(Source,static (value,index) => value>index),static value => value<4),
        from value in Source.Where(static (value,index) => value>index) where value<4 select value
    );
    private static readonly int[] Source=[1,2,3,4];
}
