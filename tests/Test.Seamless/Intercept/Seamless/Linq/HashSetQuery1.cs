using System.Linq;
using Seamless.Collections;
using Seamless.Linq;
using Seamless.Stores;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

[Collection("HashSetIntercept")]
public sealed class HashSetQuery1 {
    [Fact, Trait("Category","Intercept")]
    public static void GroupBy() {
        using var rows = CreateUnmanagedSource(Rows());
        AssertStringNormalizedSequence(
            FormatGroups(SystemEnumerable.GroupBy(Rows(),static row => row.Key,static row => row.Value)),
            FormatGroups(rows.GroupBy(static row => row.Key,static row => row.Value))
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void GroupJoin() {
        using var source = CreateUnmanagedSource(Source());
        using var second = CreateUnmanagedSource(Second());
        AssertStringNormalizedSequence(
            FormatGroupJoin(SystemEnumerable.GroupJoin(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues))),
            FormatGroupJoin(source.GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues)))
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Join() {
        using var source = CreateUnmanagedSource(Source());
        using var second = CreateUnmanagedSource(Second());
        AssertIntNormalizedSequence(
            SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
            source.Join(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void OrderBy() {
        using var rows = CreateUnmanagedSource(Rows());
        AssertRowSequence(
            SystemEnumerable.OrderBy(Rows(),static row => row.Key),
            rows.OrderBy(static row => row.Key)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void OrderByDescending() {
        using var rows = CreateUnmanagedSource(Rows());
        AssertRowSequence(
            SystemEnumerable.OrderByDescending(Rows(),static row => row.Key),
            rows.OrderByDescending(static row => row.Key)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select() {
        using var source = CreateUnmanagedSource(Source());

        AssertIntNormalizedSequence(
            SystemEnumerable.Select(Source(),static value => value*2),
            source.Select(static value => value*2)
        );
        AssertIntNormalizedSequence(
            SystemEnumerable.Select(Source(),static value => value*2+1),
            source.Select(static value => value*2+1)
        );
        AssertIntNormalizedSequence(
            SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value*2),static selected => selected+1),
            source.Select(static value => value*2).Select(static selected => selected+1)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void SelectMany() {
        using var source = CreateUnmanagedSource(Source());
        using var second = CreateUnmanagedSource(Second());
        AssertIntNormalizedSequence(
            SystemEnumerable.SelectMany(Source(),static _ => Second(),static (outerValue,innerValue) => outerValue+innerValue),
            source.SelectMany(_ => second,static (outerValue,innerValue) => outerValue+innerValue)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void ThenBy() {
        using var rows = CreateUnmanagedSource(OrderedRows());
        AssertRowSequence(
            SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(OrderedRows(),static row => row.Key),static row => row.Rank),
            rows.OrderBy(static row => row.Key).ThenBy(static row => row.Rank)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void ThenByDescending() {
        using var rows = CreateUnmanagedSource(OrderedRows());
        AssertRowSequence(
            SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(OrderedRows(),static row => row.Key),static row => row.Rank),
            rows.OrderBy(static row => row.Key).ThenByDescending(static row => row.Rank)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where() {
        using var source = CreateUnmanagedSource(Source());

        AssertIntNormalizedSequence(
            SystemEnumerable.Where(Source(),static value => value>2),
            source.Where(static value => value>2)
        );
        AssertIntNormalizedSequence(
            SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*2),static selected => selected>4),
            source.Select(static value => value*2).Where(static selected => selected>4)
        );
    }

    private static void AssertIntNormalizedSequence(IEnumerable<int> expected,IEnumerable<int> actual) {
        try {
            Assert.Equal(NormalizeIntSequence(expected),NormalizeIntSequence(actual));
        }
        finally {
            DisposeActual(actual);
        }
    }

    private static void AssertStringNormalizedSequence(IEnumerable<string> expected,IEnumerable<string> actual) {
        try {
            Assert.Equal(NormalizeStringSequence(expected),NormalizeStringSequence(actual));
        }
        finally {
            DisposeActual(actual);
        }
    }

    private static void AssertRowSequence(IEnumerable<Row> expected,IEnumerable<Row> actual) {
        try {
            Assert.Equal(expected,actual);
        }
        finally {
            DisposeActual(actual);
        }
    }

    private static void DisposeActual<T>(IEnumerable<T> actual) {
        if(actual is IDisposable disposable) {
            disposable.Dispose();
        }
    }

    private static IEnumerable<int> NormalizeIntSequence(IEnumerable<int> values) =>
        SystemEnumerable.OrderBy(values,static value => value);

    private static IEnumerable<string> NormalizeStringSequence(IEnumerable<string> values) =>
        SystemEnumerable.OrderBy(values,static value => value,StringComparer.Ordinal);

    private static IEnumerable<string> FormatGroups(IEnumerable<global::System.Linq.IGrouping<int,int>> groups) =>
        SystemEnumerable.Select(groups,static group => group.Key+":"+string.Join(",",SystemEnumerable.OrderBy(group,static value => value)));

    private static IEnumerable<string> FormatGroupJoin(IEnumerable<(int outerValue,IEnumerable<int> innerValues)> rows) =>
        SystemEnumerable.Select(rows,static row => row.outerValue+":"+string.Join(",",SystemEnumerable.OrderBy(row.innerValues,static value => value)));

    private static global::System.Collections.Generic.HashSet<int> Source() => [1,2,3,4];
    private static global::System.Collections.Generic.HashSet<int> Second() => [3,4,5];
    private static global::System.Collections.Generic.HashSet<Row> Rows() => [new(1,10,2),new(2,20,1),new(3,30,3)];
    private static global::System.Collections.Generic.HashSet<Row> OrderedRows() => [new(1,30,3),new(1,10,1),new(2,20,2)];

    private static IHashSet<T> CreateUnmanagedSource<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var store = HashStore.Create().Create<T>();
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }

    private readonly record struct Row(int Key,int Value,int Rank);
}
