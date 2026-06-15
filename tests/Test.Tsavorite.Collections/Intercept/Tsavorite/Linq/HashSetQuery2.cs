using System.Linq;

using Tsavorite.Collections;
using Tsavorite.Linq;
using Tsavorite.Stores;


namespace Test.Tsavorite.Collections.Intercept.Tsavorite.Linq;

[Collection("HashSetIntercept")]
public sealed class HashSetQuery2 {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static value => value+1).Select(static value => value*10),
            source.Select(static value => value+1).Select(static selected => selected*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Select(static value => value*10).Where(static value => value>20),
            source.Select(static value => value*10).Where(static selected => selected>20)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static value => value>1).Select(static value => value*10),
            source.Where(static value => value>1).Select(static selected => selected*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            Source().Where(static value => value>1).Where(static value => value<4),
            source.Where(static value => value>1).Where(static selected => selected<4)
        );
    }

    private static void AssertChain(IEnumerable<int> expected,IEnumerable<int> actual) {
        try {
            Assert.Equal(expected.OrderBy(static value => value),actual.OrderBy(static value => value));
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    private static global::System.Collections.Generic.HashSet<int> Source() => [1,2,3,4];

    private static IHashSet<T> CreateUnmanagedSource<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var store = HashStore.Create().Create<T>();
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }
}
