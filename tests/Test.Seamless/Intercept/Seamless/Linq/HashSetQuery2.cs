using System.Collections.Generic;
using Seamless.Collections;
using Seamless.Linq;
using Seamless.Stores;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

[Collection("HashSetIntercept")]
public sealed class HashSetQuery2 {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
            from value in source select value+1 into selected select selected*10
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
            from value in source select value*10 into selected where selected>20 select selected
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
            from value in source where value>1 select value into selected select selected*10
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
            from value in source where value>1 select value into selected where selected<4 select selected
        );
    }

    private static void AssertChain(IEnumerable<int> expected,IEnumerable<int> actual) {
        try {
            Assert.Equal(SystemEnumerable.OrderBy(expected,static value => value),SystemEnumerable.OrderBy(actual,static value => value));
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    private static HashSet<int> Source() => [1,2,3,4];

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
