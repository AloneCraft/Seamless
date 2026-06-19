using Tsavorite.Collections;
using Tsavorite.Linq;
using Tsavorite.Stores;

namespace Test.Tsavorite.Linq.Intercept;

[Collection("HashSetIntercept")]
public sealed class HashSetMethod1 {
    [Fact, Trait("Category","Intercept")]
    public static void Aggregate_func() => AssertInt(
        static source => source.Aggregate(static (left,right) => left+right),
        Aggregate_funcOptimized,
        Aggregate_funcNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Aggregate_seed_func() => AssertInt(
        static source => source.Aggregate(10,static (sum,value) => sum+value),
        Aggregate_seed_funcOptimized,
        Aggregate_seed_funcNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Aggregate_seed_func_resultSelector() => AssertInt(
        static source => source.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2),
        Aggregate_seed_func_resultSelectorOptimized,
        Aggregate_seed_func_resultSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void AggregateBy_keySelector_seed_func() => AssertPairStringsNormalized(
        static source => FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,0,static (sum,pair) => sum+pair.Value)),
        AggregateBy_keySelector_seed_funcOptimized,
        AggregateBy_keySelector_seed_funcNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void AggregateBy_keySelector_seedSelector_func() => AssertPairStringsNormalized(
        static source => FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,static _ => 1,static (sum,pair) => sum+pair.Value)),
        AggregateBy_keySelector_seedSelector_funcOptimized,
        AggregateBy_keySelector_seedSelector_funcNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void All_predicate() => AssertInt(
        static source => source.All(static value => value>0),
        All_predicateOptimized,
        All_predicateNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Any() => AssertInt(
        static source => source.Any(),
        AnyOptimized,
        AnyNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Any_predicate() => AssertInt(
        static source => source.Any(static value => value>3),
        Any_predicateOptimized,
        Any_predicateNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void AsEnumerable() => AssertIntSequence(
        static source => source.AsEnumerable(),
        AsEnumerableOptimized,
        AsEnumerableNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Average_int() => AssertInt(
        static source => source.Average(),
        Average_intOptimized,
        Average_intNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Average_selector() => AssertPair(
        static source => source.Average(static pair => pair.Value),
        Average_selectorOptimized,
        Average_selectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Contains_value() => AssertInt(
        static source => source.Contains(3),
        Contains_valueOptimized,
        Contains_valueNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Contains_value_comparer() => AssertInt(
        static source => source.Contains(3,EqualityComparer<int>.Default),
        Contains_value_comparerOptimized,
        Contains_value_comparerNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Count() => AssertInt(
        static source => source.Count(),
        CountOptimized,
        CountNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Count_predicate() => AssertInt(
        static source => source.Count(static value => value>2),
        Count_predicateOptimized,
        Count_predicateNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void CountBy_keySelector() => AssertIntStringsNormalized(
        static source => FormatIntKeyValuePairs(source.CountBy(static value => value%2)),
        CountBy_keySelectorOptimized,
        CountBy_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void DefaultIfEmpty() => AssertEmptyIntSequence(
        static source => source.DefaultIfEmpty(),
        DefaultIfEmptyOptimized,
        DefaultIfEmptyNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void DefaultIfEmpty_defaultValue() => AssertEmptyIntSequence(
        static source => source.DefaultIfEmpty(9),
        DefaultIfEmpty_defaultValueOptimized,
        DefaultIfEmpty_defaultValueNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void DistinctBy_keySelector() => AssertPairSequence(
        static source => source.DistinctBy(static pair => pair.Key),
        DistinctBy_keySelectorOptimized,
        DistinctBy_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Except_second() => AssertIntSecondNormalizedSequence(
        static (first,second) => first.Except(second),
        Except_secondOptimized,
        Except_secondNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ExceptBy_second_keySelector() => AssertPairKeyNormalizedSequence(
        static (source,keys) => source.ExceptBy(keys,static pair => pair.Key),
        ExceptBy_second_keySelectorOptimized,
        ExceptBy_second_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void GroupBy_keySelector() => AssertPairStringsNormalized(
        static source => FormatPairGroups(source.GroupBy(static pair => pair.Key,static pair => pair.Value)),
        GroupBy_keySelectorOptimized,
        GroupBy_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void GroupJoin_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => outer.GroupJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValues) => $"{outerValue.Value}:{string.Join(',',innerValues.OrderBy(static value => value.Value).Select(static value => value.Value))}"),
        GroupJoin_keySelectors_resultSelectorOptimized,
        GroupJoin_keySelectors_resultSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Intersect_second() => AssertIntSecondNormalizedSequence(
        static (first,second) => first.Intersect(second),
        Intersect_secondOptimized,
        Intersect_secondNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void IntersectBy_second_keySelector() => AssertPairKeyNormalizedSequence(
        static (source,keys) => source.IntersectBy(keys,static pair => pair.Key),
        IntersectBy_second_keySelectorOptimized,
        IntersectBy_second_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Join_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => outer.Join(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}"),
        Join_keySelectors_resultSelectorOptimized,
        Join_keySelectors_resultSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void LeftJoin_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => outer.LeftJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}"),
        LeftJoin_keySelectors_resultSelectorOptimized,
        LeftJoin_keySelectors_resultSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void LongCount() => AssertInt(
        static source => source.LongCount(),
        LongCountOptimized,
        LongCountNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void LongCount_predicate() => AssertInt(
        static source => source.LongCount(static value => value>2),
        LongCount_predicateOptimized,
        LongCount_predicateNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Max_int() => AssertInt(
        static source => source.Max(),
        Max_intOptimized,
        Max_intNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Max_selector() => AssertPair(
        static source => source.Max(static pair => pair.Value),
        Max_selectorOptimized,
        Max_selectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void MaxBy_keySelector() => AssertPair(
        static source => source.MaxBy(static pair => pair.Value),
        MaxBy_keySelectorOptimized,
        MaxBy_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Min_int() => AssertInt(
        static source => source.Min(),
        Min_intOptimized,
        Min_intNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Min_selector() => AssertPair(
        static source => source.Min(static pair => pair.Value),
        Min_selectorOptimized,
        Min_selectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void MinBy_keySelector() => AssertPair(
        static source => source.MinBy(static pair => pair.Value),
        MinBy_keySelectorOptimized,
        MinBy_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void OrderBy_keySelector() => AssertPairStrings(
        static source => FormatPairSequence(source.OrderBy(static pair => pair.Value)),
        OrderBy_keySelectorOptimized,
        OrderBy_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void OrderByDescending_keySelector() => AssertPairStrings(
        static source => FormatPairSequence(source.OrderByDescending(static pair => pair.Value)),
        OrderByDescending_keySelectorOptimized,
        OrderByDescending_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void RightJoin_keySelectors_resultSelector() => AssertPairPairStringsNormalized(
        static (outer,inner) => outer.RightJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}"),
        RightJoin_keySelectors_resultSelectorOptimized,
        RightJoin_keySelectors_resultSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_selector() => AssertIntSequence(
        static source => source.Select(static value => value*10),
        Select_selectorOptimized,
        Select_selectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Select_index_selector() => AssertIntSequence(
        static source => source.Select(static (value,index) => value+index),
        Select_index_selectorOptimized,
        Select_index_selectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToArray() => AssertIntSequence(
        static source => source.ToArray(),
        ToArrayOptimized,
        ToArrayNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToDictionary_keySelector_elementSelector() => AssertPairStringsNormalized(
        static source => FormatDictionary(source.ToDictionary(static pair => pair.Value,static pair => pair.Key)),
        ToDictionary_keySelector_elementSelectorOptimized,
        ToDictionary_keySelector_elementSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToHashSet() => AssertIntNormalizedSequence(
        static source => source.ToHashSet(),
        ToHashSetOptimized,
        ToHashSetNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToList() => AssertIntSequence(
        static source => source.ToList(),
        ToListOptimized,
        ToListNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void ToLookup_keySelector_elementSelector() => AssertPairStringsNormalized(
        static source => FormatLookup(source.ToLookup(static pair => pair.Key,static pair => pair.Value)),
        ToLookup_keySelector_elementSelectorOptimized,
        ToLookup_keySelector_elementSelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Union_second() => AssertIntSecondNormalizedSequence(
        static (first,second) => first.Union(second),
        Union_secondOptimized,
        Union_secondNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void UnionBy_second_keySelector() => AssertPairPairNormalizedSequence(
        static (first,second) => first.UnionBy(second,static pair => pair.Key),
        UnionBy_second_keySelectorOptimized,
        UnionBy_second_keySelectorNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_predicate() => AssertIntSequence(
        static source => source.Where(static value => value>2),
        Where_predicateOptimized,
        Where_predicateNoOptimized
    );

    [Fact, Trait("Category","Intercept")]
    public static void Where_index_predicate() => AssertIntSequence(
        static source => source.Where(static (value,index) => value>index),
        Where_index_predicateOptimized,
        Where_index_predicateNoOptimized
    );

    private static void AssertInt<TResult>(Func<global::System.Collections.Generic.HashSet<int>,TResult> expectedSelector,Func<object?> optimized,Func<object?> noOptimized) {
        var expected=(object?)expectedSelector(eSource());
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPair<TResult>(Func<global::System.Collections.Generic.HashSet<Pair>,TResult> expectedSelector,Func<object?> optimized,Func<object?> noOptimized) {
        var expected=(object?)expectedSelector(PairSource());
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertIntSequence(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IEnumerable<int>> optimized,Func<IEnumerable<int>> noOptimized) {
        var expected=expectedSelector(eSource()).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertEmptyIntSequence(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IEnumerable<int>> optimized,Func<IEnumerable<int>> noOptimized) {
        var expected=expectedSelector(EmptySource()).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertIntNormalizedSequence(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IEnumerable<int>> optimized,Func<IEnumerable<int>> noOptimized) {
        var expected=NormalizeIntSequence(expectedSelector(eSource())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertIntStringsNormalized(Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<string>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=NormalizeStringSequence(expectedSelector(eSource())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPairSequence(Func<global::System.Collections.Generic.HashSet<Pair>,IEnumerable<Pair>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=FormatPairSequence(expectedSelector(PairSource())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPairStrings(Func<global::System.Collections.Generic.HashSet<Pair>,IEnumerable<string>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=expectedSelector(PairSource()).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPairStringsNormalized(Func<global::System.Collections.Generic.HashSet<Pair>,IEnumerable<string>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=NormalizeStringSequence(expectedSelector(PairSource())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertIntSecondNormalizedSequence(Func<global::System.Collections.Generic.HashSet<int>,global::System.Collections.Generic.HashSet<int>,IEnumerable<int>> expectedSelector,Func<IEnumerable<int>> optimized,Func<IEnumerable<int>> noOptimized) {
        var expected=NormalizeIntSequence(expectedSelector(eSource(),Second())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPairKeyNormalizedSequence(Func<global::System.Collections.Generic.HashSet<Pair>,global::System.Collections.Generic.HashSet<int>,IEnumerable<Pair>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=NormalizePairSequence(expectedSelector(PairSource(),KeySource())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPairPairNormalizedSequence(Func<global::System.Collections.Generic.HashSet<Pair>,global::System.Collections.Generic.HashSet<Pair>,IEnumerable<Pair>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=NormalizePairSequence(expectedSelector(PairSource(),PairSecond())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }

    private static void AssertPairPairStringsNormalized(Func<global::System.Collections.Generic.HashSet<Pair>,global::System.Collections.Generic.HashSet<Pair>,IEnumerable<string>> expectedSelector,Func<IEnumerable<string>> optimized,Func<IEnumerable<string>> noOptimized) {
        var expected=NormalizeStringSequence(expectedSelector(PairSource(),PairSecond())).ToArray();
        Assert.Equal(expected,optimized());
        Assert.Equal(expected,noOptimized());
    }


    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Aggregate_funcOptimized() {
            using var source = aSource();
            return source.Aggregate(static (left,right) => left+right);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Aggregate_seed_funcOptimized() {
            using var source = aSource();
            return source.Aggregate(10,static (sum,value) => sum+value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Aggregate_seed_func_resultSelectorOptimized() {
            using var source = aSource();
            return source.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> AggregateBy_keySelector_seed_funcOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,0,static (sum,pair) => sum+pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> AggregateBy_keySelector_seedSelector_funcOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,static _ => 1,static (sum,pair) => sum+pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? All_predicateOptimized() {
            using var source = aSource();
            return source.All(static value => value>0);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? AnyOptimized() {
            using var source = aSource();
            return source.Any();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Any_predicateOptimized() {
            using var source = aSource();
            return source.Any(static value => value>3);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> AsEnumerableOptimized() {
            using var source = aSource();
            return source.AsEnumerable().ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Average_intOptimized() {
            using var source = aSource();
            return source.Average();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Average_selectorOptimized() {
            using var source = aPairSource();
            return source.Average(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Contains_valueOptimized() {
            using var source = aSource();
            return source.Contains(3);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Contains_value_comparerOptimized() {
            using var source = aSource();
            return source.Contains(3,EqualityComparer<int>.Default);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? CountOptimized() {
            using var source = aSource();
            return source.Count();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Count_predicateOptimized() {
            using var source = aSource();
            return source.Count(static value => value>2);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> CountBy_keySelectorOptimized() {
            using var source = aSource();
            return NormalizeStringSequence(FormatIntKeyValuePairs(source.CountBy(static value => value%2))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> DefaultIfEmptyOptimized() {
            using var source = aEmptySource();
            return source.DefaultIfEmpty().ToArray()!;
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> DefaultIfEmpty_defaultValueOptimized() {
            using var source = aEmptySource();
            return source.DefaultIfEmpty(9).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> DistinctBy_keySelectorOptimized() {
            using var source = aPairSource();
            return FormatPairSequence(source.DistinctBy(static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Except_secondOptimized() {
            using var first = aSource();
            using var second = aSecondSource();
            return NormalizeIntSequence(first.Except(second)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> ExceptBy_second_keySelectorOptimized() {
            using var source = aPairSource();
            using var keys = aKeySource();
            return NormalizePairSequence(source.ExceptBy(keys,static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupBy_keySelectorOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatPairGroups(source.GroupBy(static pair => pair.Key,static pair => pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupJoin_keySelectors_resultSelectorOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.GroupJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValues) => $"{outerValue.Value}:{string.Join(',',innerValues.OrderBy(static value => value.Value).Select(static value => value.Value))}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Intersect_secondOptimized() {
            using var first = aSource();
            using var second = aSecondSource();
            return NormalizeIntSequence(first.Intersect(second)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> IntersectBy_second_keySelectorOptimized() {
            using var source = aPairSource();
            using var keys = aKeySource();
            return NormalizePairSequence(source.IntersectBy(keys,static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> Join_keySelectors_resultSelectorOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.Join(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> LeftJoin_keySelectors_resultSelectorOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.LeftJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? LongCountOptimized() {
            using var source = aSource();
            return source.LongCount();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? LongCount_predicateOptimized() {
            using var source = aSource();
            return source.LongCount(static value => value>2);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Max_intOptimized() {
            using var source = aSource();
            return source.Max();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Max_selectorOptimized() {
            using var source = aPairSource();
            return source.Max(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? MaxBy_keySelectorOptimized() {
            using var source = aPairSource();
            return source.MaxBy(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Min_intOptimized() {
            using var source = aSource();
            return source.Min();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Min_selectorOptimized() {
            using var source = aPairSource();
            return source.Min(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? MinBy_keySelectorOptimized() {
            using var source = aPairSource();
            return source.MinBy(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> OrderBy_keySelectorOptimized() {
            using var source = aPairSource();
            return FormatPairSequence(source.OrderBy(static pair => pair.Value)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> OrderByDescending_keySelectorOptimized() {
            using var source = aPairSource();
            return FormatPairSequence(source.OrderByDescending(static pair => pair.Value)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> RightJoin_keySelectors_resultSelectorOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.RightJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_selectorOptimized() {
            using var source = aSource();
            return source.Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_selectorOptimized() {
            using var source = aSource();
            return source.Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> ToArrayOptimized() {
            using var source = aSource();
            return source.ToArray().ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> ToDictionary_keySelector_elementSelectorOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatDictionary(source.ToDictionary(static pair => pair.Value,static pair => pair.Key))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> ToHashSetOptimized() {
            using var source = aSource();
            return NormalizeIntSequence(source.ToHashSet()).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> ToListOptimized() {
            using var source = aSource();
            return source.ToList().ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> ToLookup_keySelector_elementSelectorOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatLookup(source.ToLookup(static pair => pair.Key,static pair => pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Union_secondOptimized() {
            using var first = aSource();
            using var second = aSecondSource();
            return NormalizeIntSequence(first.Union(second)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> UnionBy_second_keySelectorOptimized() {
            using var first = aPairSource();
            using var second = aPairSecondSource();
            return NormalizePairSequence(first.UnionBy(second,static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_predicateOptimized() {
            using var source = aSource();
            return source.Where(static value => value>2).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_predicateOptimized() {
            using var source = aSource();
            return source.Where(static (value,index) => value>index).ToArray();
        }



    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Aggregate_funcNoOptimized() {
            using var source = aSource();
            return source.Aggregate(static (left,right) => left+right);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Aggregate_seed_funcNoOptimized() {
            using var source = aSource();
            return source.Aggregate(10,static (sum,value) => sum+value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Aggregate_seed_func_resultSelectorNoOptimized() {
            using var source = aSource();
            return source.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> AggregateBy_keySelector_seed_funcNoOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,0,static (sum,pair) => sum+pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> AggregateBy_keySelector_seedSelector_funcNoOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatIntKeyValuePairs(source.AggregateBy(static pair => pair.Key,static _ => 1,static (sum,pair) => sum+pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? All_predicateNoOptimized() {
            using var source = aSource();
            return source.All(static value => value>0);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? AnyNoOptimized() {
            using var source = aSource();
            return source.Any();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Any_predicateNoOptimized() {
            using var source = aSource();
            return source.Any(static value => value>3);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> AsEnumerableNoOptimized() {
            using var source = aSource();
            return source.AsEnumerable().ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Average_intNoOptimized() {
            using var source = aSource();
            return source.Average();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Average_selectorNoOptimized() {
            using var source = aPairSource();
            return source.Average(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Contains_valueNoOptimized() {
            using var source = aSource();
            return source.Contains(3);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Contains_value_comparerNoOptimized() {
            using var source = aSource();
            return source.Contains(3,EqualityComparer<int>.Default);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? CountNoOptimized() {
            using var source = aSource();
            return source.Count();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Count_predicateNoOptimized() {
            using var source = aSource();
            return source.Count(static value => value>2);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> CountBy_keySelectorNoOptimized() {
            using var source = aSource();
            return NormalizeStringSequence(FormatIntKeyValuePairs(source.CountBy(static value => value%2))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> DefaultIfEmptyNoOptimized() {
            using var source = aEmptySource();
            return source.DefaultIfEmpty().ToArray()!;
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> DefaultIfEmpty_defaultValueNoOptimized() {
            using var source = aEmptySource();
            return source.DefaultIfEmpty(9).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> DistinctBy_keySelectorNoOptimized() {
            using var source = aPairSource();
            return FormatPairSequence(source.DistinctBy(static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Except_secondNoOptimized() {
            using var first = aSource();
            using var second = aSecondSource();
            return NormalizeIntSequence(first.Except(second)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> ExceptBy_second_keySelectorNoOptimized() {
            using var source = aPairSource();
            using var keys = aKeySource();
            return NormalizePairSequence(source.ExceptBy(keys,static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupBy_keySelectorNoOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatPairGroups(source.GroupBy(static pair => pair.Key,static pair => pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> GroupJoin_keySelectors_resultSelectorNoOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.GroupJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValues) => $"{outerValue.Value}:{string.Join(',',innerValues.OrderBy(static value => value.Value).Select(static value => value.Value))}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Intersect_secondNoOptimized() {
            using var first = aSource();
            using var second = aSecondSource();
            return NormalizeIntSequence(first.Intersect(second)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> IntersectBy_second_keySelectorNoOptimized() {
            using var source = aPairSource();
            using var keys = aKeySource();
            return NormalizePairSequence(source.IntersectBy(keys,static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> Join_keySelectors_resultSelectorNoOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.Join(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> LeftJoin_keySelectors_resultSelectorNoOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.LeftJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? LongCountNoOptimized() {
            using var source = aSource();
            return source.LongCount();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? LongCount_predicateNoOptimized() {
            using var source = aSource();
            return source.LongCount(static value => value>2);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Max_intNoOptimized() {
            using var source = aSource();
            return source.Max();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Max_selectorNoOptimized() {
            using var source = aPairSource();
            return source.Max(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? MaxBy_keySelectorNoOptimized() {
            using var source = aPairSource();
            return source.MaxBy(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Min_intNoOptimized() {
            using var source = aSource();
            return source.Min();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? Min_selectorNoOptimized() {
            using var source = aPairSource();
            return source.Min(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static object? MinBy_keySelectorNoOptimized() {
            using var source = aPairSource();
            return source.MinBy(static pair => pair.Value);
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> OrderBy_keySelectorNoOptimized() {
            using var source = aPairSource();
            return FormatPairSequence(source.OrderBy(static pair => pair.Value)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> OrderByDescending_keySelectorNoOptimized() {
            using var source = aPairSource();
            return FormatPairSequence(source.OrderByDescending(static pair => pair.Value)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> RightJoin_keySelectors_resultSelectorNoOptimized() {
            using var outer = aPairSource();
            using var inner = aPairSecondSource();
            return NormalizeStringSequence(outer.RightJoin(inner,static outerValue => outerValue.Key,static innerValue => innerValue.Key,static (outerValue,innerValue) => $"{outerValue.Value}:{innerValue.Value}")).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_selectorNoOptimized() {
            using var source = aSource();
            return source.Select(static value => value*10).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Select_index_selectorNoOptimized() {
            using var source = aSource();
            return source.Select(static (value,index) => value+index).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> ToArrayNoOptimized() {
            using var source = aSource();
            return source.ToArray().ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> ToDictionary_keySelector_elementSelectorNoOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatDictionary(source.ToDictionary(static pair => pair.Value,static pair => pair.Key))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> ToHashSetNoOptimized() {
            using var source = aSource();
            return NormalizeIntSequence(source.ToHashSet()).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> ToListNoOptimized() {
            using var source = aSource();
            return source.ToList().ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> ToLookup_keySelector_elementSelectorNoOptimized() {
            using var source = aPairSource();
            return NormalizeStringSequence(FormatLookup(source.ToLookup(static pair => pair.Key,static pair => pair.Value))).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Union_secondNoOptimized() {
            using var first = aSource();
            using var second = aSecondSource();
            return NormalizeIntSequence(first.Union(second)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<string> UnionBy_second_keySelectorNoOptimized() {
            using var first = aPairSource();
            using var second = aPairSecondSource();
            return NormalizePairSequence(first.UnionBy(second,static pair => pair.Key)).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_predicateNoOptimized() {
            using var source = aSource();
            return source.Where(static value => value>2).ToArray();
        }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static IEnumerable<int> Where_index_predicateNoOptimized() {
            using var source = aSource();
            return source.Where(static (value,index) => value>index).ToArray();
        }




    private static IEnumerable<int> NormalizeIntSequence(IEnumerable<int> values) =>
        values.OrderBy(static value => value);

    private static IEnumerable<string> NormalizeStringSequence(IEnumerable<string> values) =>
        values.OrderBy(static value => value,StringComparer.Ordinal);

    private static IEnumerable<string> NormalizePairSequence(IEnumerable<Pair> values) =>
        values.OrderBy(static value => value.Key).ThenBy(static value => value.Value).Select(static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatPairSequence(IEnumerable<Pair> values) =>
        values.Select(static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatIntKeyValuePairs(IEnumerable<KeyValuePair<int,int>> values) =>
        values.Select(static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatPairGroups(IEnumerable<global::System.Linq.IGrouping<int,int>> values) =>
        values.Select(static value => $"{value.Key}:{string.Join(',',value.OrderBy(static element => element))}");

    private static IEnumerable<string> FormatDictionary(Dictionary<int,int> dictionary) =>
        dictionary.OrderBy(static value => value.Key).Select(static value => $"{value.Key}:{value.Value}");

    private static IEnumerable<string> FormatLookup(global::System.Linq.ILookup<int,int> lookup) =>
        lookup.OrderBy(static value => value.Key).Select(static value => $"{value.Key}:{string.Join(',',value.OrderBy(static element => element))}");

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

    public readonly record struct Pair(int Key,int Value);
}

[CollectionDefinition("HashSetIntercept",DisableParallelization = true)]
public sealed class HashSetInterceptCollection;
