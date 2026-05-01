using Seamless.Collections;
using Seamless.Stores;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SeamlessHashSet = Seamless.Linq.HashSet;
using SystemEnumerable = System.Linq.Enumerable;
using Test.Seamless.Collections;

namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

[Collection("HashSetIntercept")]
public sealed class HashSetMethod2 {
    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10),
            SeamlessEnumerable.Select(SeamlessHashSet.Select(source,static value => value+1),static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index),
            SeamlessEnumerable.Select(SeamlessHashSet.Select(source,static value => value+1),static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(source,static value => value*10),static value => value>20)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(source,static value => value*10),static (value,index) => value>20&&index>0)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value*10),
            SeamlessEnumerable.Select(SeamlessHashSet.Select(source,static (value,index) => value+index),static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value+index),
            SeamlessEnumerable.Select(SeamlessHashSet.Select(source,static (value,index) => value+index),static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value>4),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(source,static (value,index) => value+index),static value => value>4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(source,static (value,index) => value+index),static (value,index) => value>3&&index>1)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10),
            SeamlessEnumerable.Select(SeamlessHashSet.Where(source,static value => value>1),static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index),
            SeamlessEnumerable.Select(SeamlessHashSet.Where(source,static value => value>1),static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4),
            SeamlessEnumerable.Where(SeamlessHashSet.Where(source,static value => value>1),static value => value<4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2),
            SeamlessEnumerable.Where(SeamlessHashSet.Where(source,static value => value>1),static (value,index) => value<4&&index<2)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value*10),
            SeamlessEnumerable.Select(SeamlessHashSet.Where(source,static (value,index) => value>index),static value => value*10)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Select_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value+index),
            SeamlessEnumerable.Select(SeamlessHashSet.Where(source,static (value,index) => value>index),static (value,index) => value+index)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value<4),
            SeamlessEnumerable.Where(SeamlessHashSet.Where(source,static (value,index) => value>index),static value => value<4)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_Where_index_ToArray() {
        using var source = CreateUnmanagedSource(Source());
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2),
            SeamlessEnumerable.Where(SeamlessHashSet.Where(source,static (value,index) => value>index),static (value,index) => value<4&&index<2)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select_Where_TypeVariants_ToArray() {
        using var longSource = CreateUnmanagedSource(LongSource());
        using var stringSource = CreateManagedSource(StringSource());
        using var managedStructSource = CreateManagedSource(ManagedStructSource());

        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(LongSource(),static value => value*10L),static value => value>20L),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(longSource,static value => value*10L),static value => value>20L)
        );
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(StringSource(),static value => value.ToUpperInvariant()),static value => value.Length>1),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(stringSource,static value => value.ToUpperInvariant()),static value => value.Length>1)
        );
        AssertChain(
            SystemEnumerable.Where(SystemEnumerable.Select(ManagedStructSource(),static value => CreateManagedStruct(value)),static value => value.Name.Length>2),
            SeamlessEnumerable.Where(SeamlessHashSet.Select(managedStructSource,static value => CreateManagedStruct(value)),static value => value.Name.Length>2)
        );
    }

    private static void AssertChain<T>(IEnumerable<T> expected,IEnumerable<T> actual)
        where T : IComparable<T> {
        try {
            Assert.Equal(SystemEnumerable.OrderBy(expected,static value => value),SystemEnumerable.OrderBy(actual,static value => value));
        }
        finally {
            if(actual is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }

    internal static HashSet<int> Source() => [1,2,3,4];
    internal static HashSet<long> LongSource() => [1L,2L,3L,4L];
    internal static HashSet<string> StringSource() => ["a","bb","ccc"];
    internal static HashSet<ManagedStruct> ManagedStructSource() => [new() { Name="a" },new() { Name="bb" },new() { Name="ccc" }];
    internal static ManagedStruct CreateManagedStruct(ManagedStruct value) => new() { Name=value.Name+"!" };

    private static IHashSet<T> CreateUnmanagedSource<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var store = HashStore.Create().Create<T>();
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }

    private static IHashSet<T> CreateManagedSource<T>(IEnumerable<T> values)
        where T : IEquatable<T> {
        var store = HashStore.Create().Create<T>(new ManagedTag());
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }
}
