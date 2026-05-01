using System.Collections.Generic;
using Seamless.Collections;
using Seamless.Linq;
using Seamless.Stores;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

[Collection("HashSetIntercept")]
public sealed class HashSetQueryMethod {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
            (from value in source select value+1).Select(static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index),
            (from value in source select value+1).Select(static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
            (from value in source select value*10).Where(static value => value>20)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
            (from value in source select value*10).Where(static (value,index) => value>20&&index>0)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
            (from value in source where value>1 select value).Select(static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index),
            (from value in source where value>1 select value).Select(static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
            (from value in source where value>1 select value).Where(static value => value<4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
            (from value in source where value>1 select value).Where(static (value,index) => value<4&&index<2)
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
