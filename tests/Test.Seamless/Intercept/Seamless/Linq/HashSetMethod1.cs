using Seamless.Collections;
using Seamless.Linq;
using Seamless.Stores;
using SystemEnumerable = System.Linq.Enumerable;
namespace Test.Seamless.Intercept.Seamless.Linq.HashSetSample;

[Collection("HashSetIntercept")]
public sealed class HashSetMethod1 {
    [Fact, Trait("Category","Intercept")]
    public static void Aggregate_func() => AssertInt(
        static source => SystemEnumerable.Aggregate(source,static (left,right) => left+right),
        static source => source.Aggregate(static (left,right) => left+right)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Aggregate_seed_func() => AssertInt(
        static source => SystemEnumerable.Aggregate(source,10,static (sum,value) => sum+value),
        static source => source.Aggregate(10,static (sum,value) => sum+value)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Aggregate_seed_func_resultSelector() => AssertInt(
        static source => SystemEnumerable.Aggregate(source,10,static (sum,value) => sum+value,static sum => sum*2),
        static source => source.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void AggregateBy_keySelector_seed_func() => AssertPairStringsNormalized(
        static source => FormatIntKeyValuePairs(SystemEnumerable.AggregateBy(source,static pair => pair.Key,0,static (sum,pair) => sum+pair.Value)),
        static source => FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,0,static (sum,pair) => sum+pair.Value))
    );

    [Fact, Trait("Category","Intercept")]
    public static void AggregateBy_keySelector_seedSelector_func() => AssertPairStringsNormalized(
        static source => FormatIntKeyValuePairs(SystemEnumerable.AggregateBy(source,static pair => pair.Key,static _ => 1,static (sum,pair) => sum+pair.Value)),
        static source => FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,static _ => 1,static (sum,pair) => sum+pair.Value))
    );

    [Fact, Trait("Category","Intercept")]
    public static void All_predicate() => AssertInt(
        static source => SystemEnumerable.All(source,static value => value>0),
        static source => source.All(static value => value>0)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Any() => AssertInt(
        static source => SystemEnumerable.Any(source),
        static source => source.Any()
    );

    [Fact, Trait("Category","Intercept")]
    public static void Any_predicate() => AssertInt(
        static source => SystemEnumerable.Any(source,static value => value>3),
        static source => source.Any(static value => value>3)
    );

    [Fact, Trait("Category","Intercept")]
    public static void AsEnumerable() => AssertIntSequence(
        static source => SystemEnumerable.AsEnumerable(source),
        static source => source.AsEnumerable()
    );

    [Fact, Trait("Category","Intercept")]
    public static void Average_int() => AssertInt(
        static source => SystemEnumerable.Average(source),
        static source => source.Average()
    );

    [Fact, Trait("Category","Intercept")]
    public static void Average_selector() => AssertPair(
        static source => SystemEnumerable.Average(source,static pair => pair.Value),
        static source => source.Average(static pair => pair.Value)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Contains_value() => AssertInt(
        static source => SystemEnumerable.Contains(source,3),
        static source => source.Contains(3)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Contains_value_comparer() => AssertInt(
        static source => SystemEnumerable.Contains(source,3,EqualityComparer<int>.Default),
        static source => source.Contains(3,EqualityComparer<int>.Default)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Count() => AssertInt(
        static source => SystemEnumerable.Count(source),
        static source => source.Count()
    );

    [Fact, Trait("Category","Intercept")]
    public static void Count_predicate() => AssertInt(
        static source => SystemEnumerable.Count(source,static value => value>2),
        static source => source.Count(static value => value>2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void CountBy_keySelector() => AssertIntStringsNormalized(
        static source => FormatIntKeyValuePairs(SystemEnumerable.CountBy(source,static value => value%2)),
        static source => FormatIntKeyValuePairs(source.CountBy(static value => value%2))
    );

    [Fact, Trait("Category","Intercept")]
    public static void DefaultIfEmpty() => AssertEmptyIntSequence(
        static source => SystemEnumerable.DefaultIfEmpty(source),
        static source => source.DefaultIfEmpty()
    );

    [Fact, Trait("Category","Intercept")]
    public static void DefaultIfEmpty_defaultValue() => AssertEmptyIntSequence(
        static source => SystemEnumerable.DefaultIfEmpty(source,9),
        static source => source.DefaultIfEmpty(9)
    );

    [Fact, Trait("Category","Intercept")]
    public static void DistinctBy_keySelector() => AssertPairSequence(
        static source => SystemEnumerable.DistinctBy(source,static pair => pair.Key),
        static source => source.DistinctBy(static pair => pair.Key)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Except_second() => AssertIntSecondNormalizedSequence(
        static (first,second) => SystemEnumerable.Except(first,second),
        static (first,second) => first.Except(second)
    );

    [Fact, Trait("Category","Intercept")]
    public static void ExceptBy_second_keySelector() => AssertPairKeyNormalizedSequence(
        static (source,keys) => SystemEnumerable.ExceptBy(source,keys,static pair => pair.Key),
        static (source,keys) => source.ExceptBy(keys,static pair => pair.Key)
    );

    [Fact, Trait("Category","Intercept")]
    public static void GroupBy_keySelector() => AssertPairStringsNormalized(
        static source => FormatPairGroups(SystemEnumerable.GroupBy(source,static pair => pair.Key,static pair => pair.Value)),
        static source => FormatPairGroups(source.GroupBy(static pair => pair.Key,static pair => pair.Value))
    );

    [Fact, Trait("Category","Intercept")]
    public static void GroupJoin_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => SystemEnumerable.GroupJoin(outer,inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValues) => $"{outerValue.Value}:{string.Join(',',SystemEnumerable.Select(SystemEnumerable.OrderBy(innerValues,static value => value.Value),static value => value.Value))}"),
        static (outer,inner) => outer.GroupJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValues) => $"{outerValue.Value}:{string.Join(',',SystemEnumerable.Select(SystemEnumerable.OrderBy(innerValues,static value => value.Value),static value => value.Value))}")
    );

    [Fact, Trait("Category","Intercept")]
    public static void Intersect_second() => AssertIntSecondNormalizedSequence(
        static (first,second) => SystemEnumerable.Intersect(first,second),
        static (first,second) => first.Intersect(second)
    );

    [Fact, Trait("Category","Intercept")]
    public static void IntersectBy_second_keySelector() => AssertPairKeyNormalizedSequence(
        static (source,keys) => SystemEnumerable.IntersectBy(source,keys,static pair => pair.Key),
        static (source,keys) => source.IntersectBy(keys,static pair => pair.Key)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Join_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => SystemEnumerable.Join(outer,inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}"),
        static (outer,inner) => outer.Join(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")
    );

    [Fact, Trait("Category","Intercept")]
    public static void LeftJoin_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => SystemEnumerable.LeftJoin(outer,inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}"),
        static (outer,inner) => outer.LeftJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")
    );

    [Fact, Trait("Category","Intercept")]
    public static void LongCount() => AssertInt(
        static source => SystemEnumerable.LongCount(source),
        static source => source.LongCount()
    );

    [Fact, Trait("Category","Intercept")]
    public static void LongCount_predicate() => AssertInt(
        static source => SystemEnumerable.LongCount(source,static value => value>2),
        static source => source.LongCount(static value => value>2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Max_int() => AssertInt(
        static source => SystemEnumerable.Max(source),
        static source => source.Max()
    );

    [Fact, Trait("Category","Intercept")]
    public static void Max_selector() => AssertPair(
        static source => SystemEnumerable.Max(source,static pair => pair.Value),
        static source => source.Max(static pair => pair.Value)
    );

    [Fact, Trait("Category","Intercept")]
    public static void MaxBy_keySelector() => AssertPair(
        static source => SystemEnumerable.MaxBy(source,static pair => pair.Value),
        static source => source.MaxBy(static pair => pair.Value)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Min_int() => AssertInt(
        static source => SystemEnumerable.Min(source),
        static source => source.Min()
    );

    [Fact, Trait("Category","Intercept")]
    public static void Min_selector() => AssertPair(
        static source => SystemEnumerable.Min(source,static pair => pair.Value),
        static source => source.Min(static pair => pair.Value)
    );

    [Fact, Trait("Category","Intercept")]
    public static void MinBy_keySelector() => AssertPair(
        static source => SystemEnumerable.MinBy(source,static pair => pair.Value),
        static source => source.MinBy(static pair => pair.Value)
    );

    [Fact, Trait("Category","Intercept")]
    public static void OrderBy_keySelector() => AssertPairStrings(
        static source => FormatPairSequence(SystemEnumerable.OrderBy(source,static pair => pair.Value)),
        static source => FormatPairSequence(source.OrderBy(static pair => pair.Value))
    );

    [Fact, Trait("Category","Intercept")]
    public static void OrderByDescending_keySelector() => AssertPairStrings(
        static source => FormatPairSequence(SystemEnumerable.OrderByDescending(source,static pair => pair.Value)),
        static source => FormatPairSequence(source.OrderByDescending(static pair => pair.Value))
    );

    [Fact, Trait("Category","Intercept")]
    public static void RightJoin_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => SystemEnumerable.RightJoin(outer,inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}"),
        static (outer,inner) => outer.RightJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_selector() => AssertIntSequence(
        static source => SystemEnumerable.Select(source,static value => value*10),
        static source => source.Select(static value => value*10)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_selector() => AssertIntSequence(
        static source => SystemEnumerable.Select(source,static (value,index) => value+index),
        static source => source.Select(static (value,index) => value+index)
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToArray() => AssertIntSequence(
        static source => SystemEnumerable.ToArray(source),
        static source => source.ToArray()
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToDictionary_keySelector_elementSelector() => AssertPairStringsNormalized(
        static source => FormatDictionary(SystemEnumerable.ToDictionary(source,static pair => pair.Value,static pair => pair.Key)),
        static source => FormatDictionary(source.ToDictionary(static pair => pair.Value,static pair => pair.Key))
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToHashSet() => AssertIntNormalizedSequence(
        static source => SystemEnumerable.ToHashSet(source),
        static source => source.ToHashSet()
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToList() => AssertIntSequence(
        static source => SystemEnumerable.ToList(source),
        static source => source.ToList()
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToLookup_keySelector_elementSelector() => AssertPairStringsNormalized(
        static source => FormatLookup(SystemEnumerable.ToLookup(source,static pair => pair.Key,static pair => pair.Value)),
        static source => FormatLookup(source.ToLookup(static pair => pair.Key,static pair => pair.Value))
    );

    [Fact, Trait("Category","Intercept")]
    public static void Union_second() => AssertIntSecondNormalizedSequence(
        static (first,second) => SystemEnumerable.Union(first,second),
        static (first,second) => first.Union(second)
    );

    [Fact, Trait("Category","Intercept")]
    public static void UnionBy_second_keySelector() => AssertPairPairNormalizedSequence(
        static (first,second) => SystemEnumerable.UnionBy(first,second,static pair => pair.Key),
        static (first,second) => first.UnionBy(second,static pair => pair.Key)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_predicate() => AssertIntSequence(
        static source => SystemEnumerable.Where(source,static value => value>2),
        static source => source.Where(static value => value>2)
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_predicate() => AssertIntSequence(
        static source => SystemEnumerable.Where(source,static (value,index) => value>index),
        static source => source.Where(static (value,index) => value>index)
    );

    private static void AssertInt<TResult>(Func<global::System.Collections.Generic.HashSet<int>,TResult> expectedSelector,Func<IHashSet<int>,TResult> actualSelector) {
        using var source = aSource();
        Assert.Equal(expectedSelector(eSource()),actualSelector(source));
    }

    private static void AssertPair<TResult>(Func<global::System.Collections.Generic.HashSet<Pair>,TResult> expectedSelector,Func<IHashSet<Pair>,TResult> actualSelector) {
        using var source = aPairSource();
        Assert.Equal(expectedSelector(PairSource()),actualSelector(source));
    }

    private static void AssertIntSequence(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IHashSet<int>,IEnumerable<int>> actualSelector) {
        using var source = aSource();
        Assert.Equal(expectedSelector(eSource()),actualSelector(source));
    }

    private static void AssertEmptyIntSequence(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IHashSet<int>,IEnumerable<int>> actualSelector) {
        using var source = aEmptySource();
        Assert.Equal(expectedSelector(EmptySource()),actualSelector(source));
    }

    private static void AssertIntNormalizedSequence(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IHashSet<int>,IEnumerable<int>> actualSelector) {
        using var source = aSource();
        Assert.Equal(NormalizeIntSequence(expectedSelector(eSource())),NormalizeIntSequence(actualSelector(source)));
    }

    private static void AssertIntStringsNormalized(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<string>> expectedSelector,Func<IHashSet<int>,IEnumerable<string>> actualSelector) {
        using var source = aSource();
        Assert.Equal(NormalizeStringSequence(expectedSelector(eSource())),NormalizeStringSequence(actualSelector(source)));
    }

    private static void AssertPairSequence(Func<global::System.Collections.Generic.HashSet<Pair>,IEnumerable<Pair>> expectedSelector,Func<IHashSet<Pair>,IEnumerable<Pair>> actualSelector) {
        using var source = aPairSource();
        Assert.Equal(FormatPairSequence(expectedSelector(PairSource())),FormatPairSequence(actualSelector(source)));
    }

    private static void AssertPairStrings(Func<global::System.Collections.Generic.HashSet<Pair>,IEnumerable<string>> expectedSelector,Func<IHashSet<Pair>,IEnumerable<string>> actualSelector) {
        using var source = aPairSource();
        Assert.Equal(expectedSelector(PairSource()),actualSelector(source));
    }

    private static void AssertPairStringsNormalized(Func<global::System.Collections.Generic.HashSet<Pair>,IEnumerable<string>> expectedSelector,Func<IHashSet<Pair>,IEnumerable<string>> actualSelector) {
        using var source = aPairSource();
        Assert.Equal(NormalizeStringSequence(expectedSelector(PairSource())),NormalizeStringSequence(actualSelector(source)));
    }

    private static void AssertIntSecondNormalizedSequence(Func<global::System.Collections.Generic.HashSet<int>,global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IHashSet<int>,IHashSet<int>,IEnumerable<int>> actualSelector) {
        using var first = aSource();
        using var second = aSecondSource();
        Assert.Equal(NormalizeIntSequence(expectedSelector(eSource(),Second())),NormalizeIntSequence(actualSelector(first,second)));
    }

    private static void AssertPairKeyNormalizedSequence(Func<global::System.Collections.Generic.HashSet<Pair>,global::System.Collections.Generic.HashSet<int>,IEnumerable<Pair>> expectedSelector,Func<IHashSet<Pair>,IHashSet<int>,IEnumerable<Pair>> actualSelector) {
        using var source = aPairSource();
        using var keys = aKeySource();
        Assert.Equal(NormalizePairSequence(expectedSelector(PairSource(),KeySource())),NormalizePairSequence(actualSelector(source,keys)));
    }

    private static void AssertPairPairNormalizedSequence(Func<global::System.Collections.Generic.HashSet<Pair>,global::System.Collections.Generic.HashSet<Pair>,IEnumerable<Pair>> expectedSelector,Func<IHashSet<Pair>,IHashSet<Pair>,IEnumerable<Pair>> actualSelector) {
        using var first = aPairSource();
        using var second = aPairSecondSource();
        Assert.Equal(NormalizePairSequence(expectedSelector(PairSource(),PairSecond())),NormalizePairSequence(actualSelector(first,second)));
    }

    private static void AssertPairPairStringsNormalized(Func<global::System.Collections.Generic.HashSet<Pair>,global::System.Collections.Generic.HashSet<Pair>,IEnumerable<string>> expectedSelector,Func<IHashSet<Pair>,IHashSet<Pair>,IEnumerable<string>> actualSelector) {
        using var first = aPairSource();
        using var second = aPairSecondSource();
        Assert.Equal(NormalizeStringSequence(expectedSelector(PairSource(),PairSecond())),NormalizeStringSequence(actualSelector(first,second)));
    }

    private static IEnumerable<int> NormalizeIntSequence(IEnumerable<int> values) =>
        SystemEnumerable.OrderBy(values,static value => value);

    private static IEnumerable<string> NormalizeStringSequence(IEnumerable<string> values) =>
        SystemEnumerable.OrderBy(values,static value => value,StringComparer.Ordinal);

    private static IEnumerable<string> NormalizePairSequence(IEnumerable<Pair> values) =>
        SystemEnumerable.Select(SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(values,static value => value.Key),static value => value.Value),static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatPairSequence(IEnumerable<Pair> values) =>
        SystemEnumerable.Select(values,static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatIntKeyValuePairs(IEnumerable<KeyValuePair<int,int>> values) =>
        SystemEnumerable.Select(values,static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatPairGroups(IEnumerable<global::System.Linq.IGrouping<int,int>> values) =>
        SystemEnumerable.Select(values,static value => $"{value.Key}:{string.Join(',',SystemEnumerable.OrderBy(value,static element => element))}");

    private static IEnumerable<string> FormatDictionary(Dictionary<int,int> dictionary) =>
        SystemEnumerable.Select(SystemEnumerable.OrderBy(dictionary,static value => value.Key),static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatLookup(global::System.Linq.ILookup<int,int> lookup) =>
        SystemEnumerable.Select(SystemEnumerable.OrderBy(lookup,static value => value.Key),static value => $"{value.Key}:{string.Join(',',SystemEnumerable.OrderBy(value,static element => element))}");

    private static global::System.Collections.Generic.HashSet<int> eSource() => [1,2,3,4];
    private static global::System.Collections.Generic.HashSet<int> Second() => [3,4,5];
    private static global::System.Collections.Generic.HashSet<int> EmptySource() => [];
    private static global::System.Collections.Generic.HashSet<int> KeySource() => [1,3];
    private static global::System.Collections.Generic.HashSet<Pair> PairSource() => [new(1,10),new(1,11),new(2,20),new(3,30)];
    private static global::System.Collections.Generic.HashSet<Pair> PairSecond() => [new(1,100),new(1,101),new(3,300),new(4,400)];

    private static IHashSet<int> aSource() => CreateSet(eSource());
    private static IHashSet<int> aSecondSource() => CreateSet(Second());
    private static IHashSet<int> aEmptySource() => CreateSet(EmptySource());
    private static IHashSet<int> aKeySource() => CreateSet(KeySource());
    private static IHashSet<Pair> aPairSource() => CreateSet(PairSource());
    private static IHashSet<Pair> aPairSecondSource() => CreateSet(PairSecond());

    private static IHashSet<T> CreateSet<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var store = HashStore.Create().Create<T>();
        var result = store.CreateOwned();
        foreach(var value in values) {
            result.Add(value);
        }
        return result;
    }

    private readonly record struct Pair(int Key,int Value);
}

[CollectionDefinition("HashSetIntercept",DisableParallelization = true)]
public sealed class HashSetInterceptCollection;
