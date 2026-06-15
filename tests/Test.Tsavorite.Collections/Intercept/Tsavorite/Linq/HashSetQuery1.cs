using System.Linq;

using Tsavorite.Collections;
using Tsavorite.Linq;
using Tsavorite.Stores;


namespace Test.Tsavorite.Collections.Intercept.Tsavorite.Linq;

[Collection("HashSetIntercept")]
public sealed class HashSetQuery1 {
    [Fact, Trait("Category","Intercept")]
    public static void GroupBy() => AssertStringNormalizedSequence(
        FormatGroups(Rows().GroupBy(static row => row.Key,static row => row.Value)),
        GroupByOptimized,
        GroupByNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void GroupJoin() => AssertStringNormalizedSequence(
        FormatGroupJoin(Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues))),
        GroupJoinOptimized,
        GroupJoinNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Join() => AssertIntNormalizedSequence(
        Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        JoinOptimized,
        JoinNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void OrderBy() => AssertRowSequence(
        Rows().OrderBy(static row => row.Key),
        OrderByOptimized,
        OrderByNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void OrderByDescending() => AssertRowSequence(
        Rows().OrderByDescending(static row => row.Key),
        OrderByDescendingOptimized,
        OrderByDescendingNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select() {
        AssertIntNormalizedSequence(
            Source().Select(static value => value*2),
            SelectOptimized,
            SelectNoOptimized
        );
        AssertIntNormalizedSequence(
            Source().Select(static value => value*2+1),
            Select_AddOneOptimized,
            Select_AddOneNoOptimized
        );
        AssertIntNormalizedSequence(
            Source().Select(static value => value*2).Select(static selected => selected+1),
            Select_SelectOptimized,
            Select_SelectNoOptimized
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void SelectMany() => AssertIntNormalizedSequence(
        Source().SelectMany(static _ => Second(),static (outerValue,innerValue) => outerValue+innerValue),
        SelectManyOptimized,
        SelectManyNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ThenBy() => AssertRowSequence(
        OrderedRows().OrderBy(static row => row.Key).ThenBy(static row => row.Rank),
        ThenByOptimized,
        ThenByNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ThenByDescending() => AssertRowSequence(
        OrderedRows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Rank),
        ThenByDescendingOptimized,
        ThenByDescendingNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where() {
        AssertIntNormalizedSequence(
            Source().Where(static value => value>2),
            WhereOptimized,
            WhereNoOptimized
        );
        AssertIntNormalizedSequence(
            Source().Select(static value => value*2).Where(static selected => selected>4),
            Select_WhereOptimized,
            Select_WhereNoOptimized
        );
    }

    private static void AssertIntNormalizedSequence(IEnumerable<int> expected,Func<IEnumerable<int>> optimized,Func<IEnumerable<int>> noOptimized) {
        var expectedArray=NormalizeIntSequence(expected).ToArray();
        Assert.Equal(expectedArray,optimized());
        Assert.Equal(expectedArray,noOptimized());
    }

    private static void AssertStringNormalizedSequence(IEnumerable<string> expected,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expectedArray=NormalizeStringSequence(expected).ToArray();
        Assert.Equal(expectedArray,optimized());
        Assert.Equal(expectedArray,noOptimized());
    }

    private static void AssertRowSequence(IEnumerable<Row> expected,Func<IEnumerable<Row>> optimized,Func<IEnumerable<Row>> noOptimized) {
        var expectedArray=expected.ToArray();
        Assert.Equal(expectedArray,optimized());
        Assert.Equal(expectedArray,noOptimized());
    }


    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupByOptimized() {
            using var rows = CreateUnmanagedSource(Rows());
            return NormalizeStringSequence(FormatGroups(rows.GroupBy(static row => row.Key,static row => row.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupJoinOptimized() {
            using var source = CreateUnmanagedSource(Source());
            using var second = CreateUnmanagedSource(Second());
            return NormalizeStringSequence(FormatGroupJoin(source.GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues)))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> JoinOptimized() {
            using var source = CreateUnmanagedSource(Source());
            using var second = CreateUnmanagedSource(Second());
            return NormalizeIntSequence(source.Join(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> OrderByOptimized() {
            using var rows = CreateUnmanagedSource(Rows());
            return rows.OrderBy(static row => row.Key).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> OrderByDescendingOptimized() {
            using var rows = CreateUnmanagedSource(Rows());
            return rows.OrderByDescending(static row => row.Key).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> SelectOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_AddOneOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2+1)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_SelectOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2).Select(static selected => selected+1)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> SelectManyOptimized() {
            using var source = CreateUnmanagedSource(Source());
            using var second = CreateUnmanagedSource(Second());
            return NormalizeIntSequence(source.SelectMany(_ => second,static (outerValue,innerValue) => outerValue+innerValue)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> ThenByOptimized() {
            using var rows = CreateUnmanagedSource(OrderedRows());
            return rows.OrderBy(static row => row.Key).ThenBy(static row => row.Rank).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> ThenByDescendingOptimized() {
            using var rows = CreateUnmanagedSource(OrderedRows());
            return rows.OrderBy(static row => row.Key).ThenByDescending(static row => row.Rank).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> WhereOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Where(static value => value>2)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_WhereOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2).Where(static selected => selected>4)).ToArray();
        }


    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupByNoOptimized() {
            using var rows = CreateUnmanagedSource(Rows());
            return NormalizeStringSequence(FormatGroups(rows.GroupBy(static row => row.Key,static row => row.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupJoinNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            using var second = CreateUnmanagedSource(Second());
            return NormalizeStringSequence(FormatGroupJoin(source.GroupJoin(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues)))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> JoinNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            using var second = CreateUnmanagedSource(Second());
            return NormalizeIntSequence(source.Join(second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> OrderByNoOptimized() {
            using var rows = CreateUnmanagedSource(Rows());
            return rows.OrderBy(static row => row.Key).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> OrderByDescendingNoOptimized() {
            using var rows = CreateUnmanagedSource(Rows());
            return rows.OrderByDescending(static row => row.Key).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> SelectNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_AddOneNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2+1)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_SelectNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2).Select(static selected => selected+1)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> SelectManyNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            using var second = CreateUnmanagedSource(Second());
            return NormalizeIntSequence(source.SelectMany(_ => second,static (outerValue,innerValue) => outerValue+innerValue)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> ThenByNoOptimized() {
            using var rows = CreateUnmanagedSource(OrderedRows());
            return rows.OrderBy(static row => row.Key).ThenBy(static row => row.Rank).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<Row> ThenByDescendingNoOptimized() {
            using var rows = CreateUnmanagedSource(OrderedRows());
            return rows.OrderBy(static row => row.Key).ThenByDescending(static row => row.Rank).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> WhereNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Where(static value => value>2)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_WhereNoOptimized() {
            using var source = CreateUnmanagedSource(Source());
            return NormalizeIntSequence(source.Select(static value => value*2).Where(static selected => selected>4)).ToArray();
        }



    private static IEnumerable<int> NormalizeIntSequence(IEnumerable<int> values) =>
        values.OrderBy(static value => value);

    private static IEnumerable<string> NormalizeStringSequence(IEnumerable<string> values) =>
        values.OrderBy(static value => value,StringComparer.Ordinal);

    private static IEnumerable<string> FormatGroups(IEnumerable<IGrouping<int,int>> groups) =>
        groups.Select(static group => group.Key+":"+string.Join(",",group.OrderBy(static value => value)));

    private static IEnumerable<string> FormatGroupJoin(IEnumerable<(int outerValue,IEnumerable<int> innerValues)> rows) =>
        rows.Select(static row => row.outerValue+":"+string.Join(",",row.innerValues.OrderBy(static value => value)));

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

    public readonly record struct Row(int Key,int Value,int Rank);
}
