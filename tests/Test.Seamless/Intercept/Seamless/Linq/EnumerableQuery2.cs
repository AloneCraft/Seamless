using Seamless.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class EnumerableQuery2 {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
        Source().Select(static value => value+1).Select(static selected => selected*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index),
        Source().Select(static value => value+1).Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
        Source().Select(static value => value*10).Where(static selected => selected>20)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
        Source().Select(static value => value*10).Where(static (value,index) => value>20&&index>0)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value*10),
        Source().Select(static (value,index) => value+index).Select(static selected => selected*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value+index),
        Source().Select(static (value,index) => value+index).Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value>4),
        Source().Select(static (value,index) => value+index).Where(static selected => selected>4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1),
        Source().Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
        Source().Where(static value => value>1).Select(static selected => selected*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index),
        Source().Where(static value => value>1).Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
        Source().Where(static value => value>1).Where(static selected => selected<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
        Source().Where(static value => value>1).Where(static (value,index) => value<4&&index<2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value*10),
        Source().Where(static (value,index) => value>index).Select(static selected => selected*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value+index),
        Source().Where(static (value,index) => value>index).Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value<4),
        Source().Where(static (value,index) => value>index).Where(static selected => selected<4)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() => AssertChain(
        SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2),
        Source().Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Take_Select_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Take(SystemEnumerable.Select(Source(),static value => value+1),2),static value => value*10),
        Source().Select(static value => value+1).Take(2).Select(static selected => selected*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Take_zero_Select_does_not_enumerate_source_ToArray() => AssertChain(
        SystemEnumerable.Select(SystemEnumerable.Take(ThrowingSource(),0),static value => value*10),
        ThrowingSource().Take(0).Select(static selected => selected*10)
    );

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

    private static readonly int[] SourceValue=[1,2,3,4];
    private static int[] Source() => SourceValue;
    private static IEnumerable<int> ThrowingSource() => new ThrowingEnumerable();

    private sealed class ThrowingEnumerable:IEnumerable<int> {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("Take(0) must not enumerate the source.");
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
