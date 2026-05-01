using e = System.Linq.Enumerable;
using a = Seamless.Linq.Enumerable;
using Test.Seamless.Collections;

namespace Test.Seamless.Intercept.Seamless.Linq;

public static class EnumerableMethod2 {
    [Fact,Trait("Category","Intercept")]
    public static void Select_Select_ToArrayでこのようにインラインしたいコード(){
        {
            var expected=a.Select(a.Select(Source(),static value=>value+1),static value=>value*10);
        }
        {
            var expected=a.Select(a.Select(Source(),static value=>value+1),static value=>value*10);
        }
        var actual=a.Select(a.Select(Source(),static value=>value+1),static value=>value*10);
    }
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_ToArray() => AssertChain(
        e.Select(e.Select(Source(),static value => value+1),static value => value*10),
        a.Select(a.Select(Source(),static value => value+1),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() => AssertChain(
        e.Select(e.Select(Source(),static value => value+1),static (value,index) => value+index),
        a.Select(a.Select(Source(),static value => value+1),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() => AssertChain(
        e.Where(e.Select(Source(),static value => value*10),static value => value>20),
        a.Where(a.Select(Source(),static value => value*10),static value => value>20)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() => AssertChain(
        e.Where(e.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
        a.Where(a.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() => AssertChain(
        e.Select(e.Select(Source(),static (value,index) => value+index),static value => value*10),
        a.Select(a.Select(Source(),static (value,index) => value+index),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() => AssertChain(
        e.Select(e.Select(Source(),static (value,index) => value+index),static (value,index) => value+index),
        a.Select(a.Select(Source(),static (value,index) => value+index),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() => AssertChain(
        e.Where(e.Select(Source(),static (value,index) => value+index),static value => value>4),
        a.Where(a.Select(Source(),static (value,index) => value+index),static value => value>4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() => AssertChain(
        e.Where(e.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1),
        a.Where(a.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() => AssertChain(
        e.Select(e.Where(Source(),static value => value>1),static value => value*10),
        a.Select(a.Where(Source(),static value => value>1),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() => AssertChain(
        e.Select(e.Where(Source(),static value => value>1),static (value,index) => value+index),
        a.Select(a.Where(Source(),static value => value>1),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() => AssertChain(
        e.Where(e.Where(Source(),static value => value>1),static value => value<4),
        a.Where(a.Where(Source(),static value => value>1),static value => value<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() => AssertChain(
        e.Where(e.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
        a.Where(a.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() => AssertChain(
        e.Select(e.Where(Source(),static (value,index) => value>index),static value => value*10),
        a.Select(a.Where(Source(),static (value,index) => value>index),static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() => AssertChain(
        e.Select(e.Where(Source(),static (value,index) => value>index),static (value,index) => value+index),
        a.Select(a.Where(Source(),static (value,index) => value>index),static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() => AssertChain(
        e.Where(e.Where(Source(),static (value,index) => value>index),static value => value<4),
        a.Where(a.Where(Source(),static (value,index) => value>index),static value => value<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() => AssertChain(
        e.Where(e.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2),
        a.Where(a.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_TypeVariants_ToArray() {
        AssertChain(
            e.Where(e.Select(LongSource(),static value => value*10L),static value => value>20L),
            a.Where(a.Select(LongSource(),static value => value*10L),static value => value>20L)
        );
        AssertChain(
            e.Where(e.Select(StringSource(),static value => value.ToUpperInvariant()),static value => value.Length>1),
            a.Where(a.Select(StringSource(),static value => value.ToUpperInvariant()),static value => value.Length>1)
        );
        AssertChain(
            e.Where(e.Select(ManagedStructSource(),static value => CreateManagedStruct(value)),static value => value.Name.Length>2),
            a.Where(a.Select(ManagedStructSource(),static value => CreateManagedStruct(value)),static value => value.Name.Length>2)
        );
    }

    private static void AssertChain<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        try {
            Assert.Equal(expected,actual);
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    internal static int[] Source() => [1,2,3,4];
    internal static long[] LongSource() => [1L,2L,3L,4L];
    internal static string[] StringSource() => ["a","bb","ccc"];
    internal static ManagedStruct[] ManagedStructSource() => [new() { Name="a" },new() { Name="bb" },new() { Name="ccc" }];
    internal static ManagedStruct CreateManagedStruct(ManagedStruct value) => new() { Name=value.Name+"!" };
}
