using System.Linq;

using Tsavorite.Collections;
using Tsavorite.Linq;
using Tsavorite.Stores;


namespace Test.Tsavorite.Collections.Intercept.Tsavorite.Linq;

[Collection("HashSetIntercept")]
public sealed class HashSetQuery2 {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select() => AssertChain(
        Source().Select(static value => value+1).Select(static value => value*10),
        Select_SelectOptimized,
        Select_SelectNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where() => AssertChain(
        Source().Select(static value => value*10).Where(static value => value>20),
        Select_WhereOptimized,
        Select_WhereNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select() => AssertChain(
        Source().Where(static value => value>1).Select(static value => value*10),
        Where_SelectOptimized,
        Where_SelectNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where() => AssertChain(
        Source().Where(static value => value>1).Where(static value => value<4),
        Where_WhereOptimized,
        Where_WhereNoOptimized
    );

    private static void AssertChain(IEnumerable<int> expected,Func<IEnumerable<int>> optimized,Func<IEnumerable<int>> noOptimized) {
        var expectedArray=expected.OrderBy(static value => value).ToArray();
        Assert.Equal(expectedArray,optimized());
        Assert.Equal(expectedArray,noOptimized());
    }


    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_SelectOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value+1).Select(static selected => selected*10).OrderBy(static value => value).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_WhereOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value*10).Where(static selected => selected>20).OrderBy(static value => value).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_SelectOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Select(static selected => selected*10).OrderBy(static value => value).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_WhereOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Where(static selected => selected<4).OrderBy(static value => value).ToArray();
        }


    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_SelectNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value+1).Select(static selected => selected*10).OrderBy(static value => value).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_WhereNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Select(static value => value*10).Where(static selected => selected>20).OrderBy(static value => value).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_SelectNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Select(static selected => selected*10).OrderBy(static value => value).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_WhereNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return source.Where(static value => value>1).Where(static selected => selected<4).OrderBy(static value => value).ToArray();
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
