using Benchmark.Shared;

namespace Benchmark.Tsavorite.Linq.Enumerable;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public sealed class TsavoriteEnumerableCatalog():Base(80_000) {
    private TsavoriteEnumerableScenario.SystemSources? systemSources;
    private TsavoriteEnumerableScenario.TsavoriteSources? tsavoriteSources;

    public static IEnumerable<string> MethodNames => TsavoriteEnumerableScenario.MethodNames;

    [ParamsSource(nameof(MethodNames))]
    public string MethodName { get; set; } = TsavoriteEnumerableScenario.MethodNames[0];

    [GlobalSetup]
    public void Setup() {
        var length = this.CountForTestTiming(80_000);
        this.systemSources=TsavoriteEnumerableScenario.SystemSources.Create(length);
        this.tsavoriteSources=TsavoriteEnumerableScenario.TsavoriteSources.Create(this.systemSources);
    }

    [GlobalCleanup]
    public void Cleanup() {
        this.tsavoriteSources?.Dispose();
        this.tsavoriteSources=null;
        this.systemSources=null;
    }

    [Benchmark(Baseline=true),BenchmarkCategory(Categories.Linq)]
    public void Linq() => this.ConsumeResult(TsavoriteEnumerableScenario.RunSystem(this.MethodName,this.RequireSystemSources()));

    [Benchmark,BenchmarkCategory(Categories.Tsavorite)]
    public void Tsavorite() => this.ConsumeResult(TsavoriteEnumerableScenario.RunTsavorite(this.MethodName,this.RequireTsavoriteSources()));

#if BENCH_AS_TEST
    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match() {
        foreach(var methodName in TsavoriteEnumerableScenario.MethodNames) {
            Xunit.Assert.Equal(
                TsavoriteEnumerableScenario.RunSystemForTest(methodName),
                TsavoriteEnumerableScenario.RunTsavoriteForTest(methodName));
        }
    }
#endif

    private TsavoriteEnumerableScenario.SystemSources RequireSystemSources() =>
        this.systemSources??throw new InvalidOperationException("GlobalSetup was not called.");

    private TsavoriteEnumerableScenario.TsavoriteSources RequireTsavoriteSources() =>
        this.tsavoriteSources??throw new InvalidOperationException("GlobalSetup was not called.");

    private void ConsumeResult(object? result) {
        this.Result=result;
        if(result is global::System.Collections.IEnumerable enumerable&&result is not string) {
            foreach(var item in enumerable) {
                this.consumer.Consume(item);
            }
            return;
        }

        this.consumer.Consume(result);
    }
}

internal static class TsavoriteEnumerableScenario {
    public static readonly string[] MethodNames = [
        "Aggregate_func",
        "Aggregate_seed_func",
        "Aggregate_seed_func_resultSelector",
        "AggregateBy_keySelector",
        "All_predicate",
        "Any",
        "Any_predicate",
        "Append_element",
        "AsEnumerable",
        "Average",
        "Average_selector",
        "Chunk_size",
        "Concat_second",
        "Contains_value",
        "Count",
        "Count_predicate",
        "CountBy_keySelector",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy_keySelector",
        "ElementAt_index",
        "ElementAtOrDefault_index",
        "Except_second",
        "ExceptBy_keySelector",
        "First",
        "FirstOrDefault",
        "FirstOrDefault_predicate",
        "GroupBy_elementSelector",
        "GroupBy_elementSelector_resultSelector",
        "GroupBy_keySelector",
        "GroupBy_resultSelector",
        "GroupJoin_inner",
        "Index",
        "Intersect_second",
        "IntersectBy_keySelector",
        "Join_inner",
        "Last",
        "LastOrDefault",
        "LastOrDefault_predicate",
        "LongCount",
        "LongCount_predicate",
        "Max",
        "Max_selector",
        "MaxBy_keySelector",
        "Min",
        "Min_selector",
        "MinBy_keySelector",
        "Order",
        "OrderBy_keySelector",
        "OrderByDescending_keySelector",
        "OrderDescending",
        "Prepend_element",
        "Reverse",
        "Select_indexSelector",
        "Select_selector",
        "SelectMany_indexSelector",
        "SelectMany_indexSelector_resultSelector",
        "SelectMany_resultSelector",
        "SelectMany_selector",
        "SequenceEqual_second",
        "Single",
        "Single_predicate",
        "SingleOrDefault",
        "SingleOrDefault_predicate",
        "Skip_count",
        "SkipLast_count",
        "SkipWhile_indexPredicate",
        "SkipWhile_predicate",
        "Sum",
        "Sum_selector",
        "Take_count",
        "TakeLast_count",
        "TakeWhile_indexPredicate",
        "TakeWhile_predicate",
        "ThenBy_keySelector",
        "ThenByDescending_keySelector",
        "ToArray",
        "ToDictionary_elementSelector",
        "ToDictionary_keySelector",
        "ToHashSet",
        "ToList",
        "ToLookup_elementSelector",
        "ToLookup_keySelector",
        "TryGetNonEnumeratedCount",
        "Union_second",
        "UnionBy_keySelector",
        "Where_indexPredicate",
        "Where_predicate",
        "Zip_second",
        "Zip_tuple",
    ];

    public static object? RunSystem(string methodName,SystemSources source) =>
        methodName switch {
            "Aggregate_func" => global::System.Linq.Enumerable.Aggregate(source.Int,static (acc,value) => acc+value),
            "Aggregate_seed_func" => global::System.Linq.Enumerable.Aggregate(source.Int,10,static (acc,value) => acc+value),
            "Aggregate_seed_func_resultSelector" => global::System.Linq.Enumerable.Aggregate(source.Int,10,static (acc,value) => acc+value,static value => value*2),
            "AggregateBy_keySelector" => global::System.Linq.Enumerable.AggregateBy(source.Int,static value => value%16,0,static (acc,value) => acc+value),
            "All_predicate" => global::System.Linq.Enumerable.All(source.Int,static value => value>0),
            "Any" => global::System.Linq.Enumerable.Any(source.Int),
            "Any_predicate" => global::System.Linq.Enumerable.Any(source.Int,static value => value>1),
            "Append_element" => global::System.Linq.Enumerable.Append(source.Int,-1),
            "AsEnumerable" => global::System.Linq.Enumerable.AsEnumerable(source.Int),
            "Average" => global::System.Linq.Enumerable.Average(source.Int),
            "Average_selector" => global::System.Linq.Enumerable.Average(source.Pairs,static value => value.Value),
            "Chunk_size" => global::System.Linq.Enumerable.Chunk(source.Int,128),
            "Concat_second" => global::System.Linq.Enumerable.Concat(source.Int,source.Second),
            "Contains_value" => global::System.Linq.Enumerable.Contains(source.Int,1),
            "Count" => global::System.Linq.Enumerable.Count(source.Int),
            "Count_predicate" => global::System.Linq.Enumerable.Count(source.Int,static value => (value&1)==0),
            "CountBy_keySelector" => global::System.Linq.Enumerable.CountBy(source.Int,static value => value%16),
            "DefaultIfEmpty" => global::System.Linq.Enumerable.DefaultIfEmpty(source.Empty),
            "Distinct" => global::System.Linq.Enumerable.Distinct(source.Int),
            "DistinctBy_keySelector" => global::System.Linq.Enumerable.DistinctBy(source.Pairs,static value => value.Key),
            "ElementAt_index" => global::System.Linq.Enumerable.ElementAt(source.Single,0),
            "ElementAtOrDefault_index" => global::System.Linq.Enumerable.ElementAtOrDefault(source.Single,0),
            "Except_second" => global::System.Linq.Enumerable.Except(source.Int,source.Second),
            "ExceptBy_keySelector" => global::System.Linq.Enumerable.ExceptBy(source.Pairs,source.Keys,static value => value.Key),
            "First" => global::System.Linq.Enumerable.First(source.Single),
            "FirstOrDefault" => global::System.Linq.Enumerable.FirstOrDefault(source.Single),
            "FirstOrDefault_predicate" => global::System.Linq.Enumerable.FirstOrDefault(source.Int,static value => value>0),
            "GroupBy_elementSelector" => global::System.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key,static value => value.Value),
            "GroupBy_elementSelector_resultSelector" => global::System.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key,static value => value.Value,static (key,values) => new Pair(key,global::System.Linq.Enumerable.Sum(values))),
            "GroupBy_keySelector" => global::System.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key),
            "GroupBy_resultSelector" => global::System.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key,static (key,values) => new Pair(key,global::System.Linq.Enumerable.Count(values))),
            "GroupJoin_inner" => global::System.Linq.Enumerable.GroupJoin(source.Pairs,source.PairSecond,static outer => outer.Key,static inner => inner.Key,static (outer,inners) => new Pair(outer.Value,global::System.Linq.Enumerable.Count(inners))),
            "Index" => global::System.Linq.Enumerable.Index(source.Int),
            "Intersect_second" => global::System.Linq.Enumerable.Intersect(source.Int,source.Second),
            "IntersectBy_keySelector" => global::System.Linq.Enumerable.IntersectBy(source.Pairs,source.Keys,static value => value.Key),
            "Join_inner" => global::System.Linq.Enumerable.Join(source.Pairs,source.PairSecond,static outer => outer.Key,static inner => inner.Key,static (outer,inner) => new Pair(outer.Value,inner.Value)),
            "Last" => global::System.Linq.Enumerable.Last(source.Single),
            "LastOrDefault" => global::System.Linq.Enumerable.LastOrDefault(source.Single),
            "LastOrDefault_predicate" => global::System.Linq.Enumerable.LastOrDefault(source.Int,static value => value>0),
            "LongCount" => global::System.Linq.Enumerable.LongCount(source.Int),
            "LongCount_predicate" => global::System.Linq.Enumerable.LongCount(source.Int,static value => (value&1)==0),
            "Max" => global::System.Linq.Enumerable.Max(source.Int),
            "Max_selector" => global::System.Linq.Enumerable.Max(source.Pairs,static value => value.Value),
            "MaxBy_keySelector" => global::System.Linq.Enumerable.MaxBy(source.Pairs,static value => value.Value),
            "Min" => global::System.Linq.Enumerable.Min(source.Int),
            "Min_selector" => global::System.Linq.Enumerable.Min(source.Pairs,static value => value.Value),
            "MinBy_keySelector" => global::System.Linq.Enumerable.MinBy(source.Pairs,static value => value.Value),
            "Order" => global::System.Linq.Enumerable.Order(source.Int),
            "OrderBy_keySelector" => global::System.Linq.Enumerable.OrderBy(source.Pairs,static value => value.Value),
            "OrderByDescending_keySelector" => global::System.Linq.Enumerable.OrderByDescending(source.Pairs,static value => value.Value),
            "OrderDescending" => global::System.Linq.Enumerable.OrderDescending(source.Int),
            "Prepend_element" => global::System.Linq.Enumerable.Prepend(source.Int,-1),
            "Reverse" => global::System.Linq.Enumerable.Reverse(source.Single),
            "Select_indexSelector" => global::System.Linq.Enumerable.Select(source.Int,static (value,index) => value+index),
            "Select_selector" => global::System.Linq.Enumerable.Select(source.Int,static value => value*2),
            "SelectMany_indexSelector" => global::System.Linq.Enumerable.SelectMany(source.Int,static (value,index) => new[] { value+index }),
            "SelectMany_indexSelector_resultSelector" => global::System.Linq.Enumerable.SelectMany(source.Int,static (value,index) => new[] { value,index },static (value,item) => value+item),
            "SelectMany_resultSelector" => global::System.Linq.Enumerable.SelectMany(source.Int,static value => new[] { value,value+1 },static (value,item) => value+item),
            "SelectMany_selector" => global::System.Linq.Enumerable.SelectMany(source.Int,static value => new[] { value,value+1 }),
            "SequenceEqual_second" => global::System.Linq.Enumerable.SequenceEqual(source.Single,source.SingleSecond),
            "Single" => global::System.Linq.Enumerable.Single(source.Single),
            "Single_predicate" => global::System.Linq.Enumerable.Single(source.Single,static value => value>0),
            "SingleOrDefault" => global::System.Linq.Enumerable.SingleOrDefault(source.Single),
            "SingleOrDefault_predicate" => global::System.Linq.Enumerable.SingleOrDefault(source.Single,static value => value>0),
            "Skip_count" => global::System.Linq.Enumerable.Skip(source.Int,1),
            "SkipLast_count" => global::System.Linq.Enumerable.SkipLast(source.Single,1),
            "SkipWhile_indexPredicate" => global::System.Linq.Enumerable.SkipWhile(source.Int,static (value,index) => index<1&&value>0),
            "SkipWhile_predicate" => global::System.Linq.Enumerable.SkipWhile(source.Int,static value => value<2),
            "Sum" => global::System.Linq.Enumerable.Sum(source.Int),
            "Sum_selector" => global::System.Linq.Enumerable.Sum(source.Pairs,static value => value.Value),
            "Take_count" => global::System.Linq.Enumerable.Take(source.Int,16),
            "TakeLast_count" => global::System.Linq.Enumerable.TakeLast(source.Single,1),
            "TakeWhile_indexPredicate" => global::System.Linq.Enumerable.TakeWhile(source.Int,static (value,index) => index<16&&value>0),
            "TakeWhile_predicate" => global::System.Linq.Enumerable.TakeWhile(source.Int,static value => value<16),
            "ThenBy_keySelector" => global::System.Linq.Enumerable.ThenBy(global::System.Linq.Enumerable.OrderBy(source.Pairs,static value => value.Key),static value => value.Value),
            "ThenByDescending_keySelector" => global::System.Linq.Enumerable.ThenByDescending(global::System.Linq.Enumerable.OrderBy(source.Pairs,static value => value.Key),static value => value.Value),
            "ToArray" => global::System.Linq.Enumerable.ToArray(source.Int),
            "ToDictionary_elementSelector" => global::System.Linq.Enumerable.ToDictionary(source.Int,static value => value,static value => value*2),
            "ToDictionary_keySelector" => global::System.Linq.Enumerable.ToDictionary(source.Int,static value => value),
            "ToHashSet" => global::System.Linq.Enumerable.ToHashSet(source.Int),
            "ToList" => global::System.Linq.Enumerable.ToList(source.Int),
            "ToLookup_elementSelector" => global::System.Linq.Enumerable.ToLookup(source.Pairs,static value => value.Key,static value => value.Value),
            "ToLookup_keySelector" => global::System.Linq.Enumerable.ToLookup(source.Pairs,static value => value.Key),
            "TryGetNonEnumeratedCount" => TryGetNonEnumeratedCount(source.Int),
            "Union_second" => global::System.Linq.Enumerable.Union(source.Int,source.Second),
            "UnionBy_keySelector" => global::System.Linq.Enumerable.UnionBy(source.Pairs,source.PairSecond,static value => value.Key),
            "Where_indexPredicate" => global::System.Linq.Enumerable.Where(source.Int,static (value,index) => value>index),
            "Where_predicate" => global::System.Linq.Enumerable.Where(source.Int,static value => (value&1)==0),
            "Zip_second" => global::System.Linq.Enumerable.Zip(source.Int,source.Second),
            "Zip_tuple" => global::System.Linq.Enumerable.Zip(source.Int,source.Second,source.Third),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName),methodName,null),
        };

    public static object? RunTsavorite(string methodName,TsavoriteSources source) =>
        methodName switch {
            "Aggregate_func" => global::Tsavorite.Linq.Enumerable.Aggregate(source.Int,static (acc,value) => acc+value),
            "Aggregate_seed_func" => global::Tsavorite.Linq.Enumerable.Aggregate(source.Int,10,static (acc,value) => acc+value),
            "Aggregate_seed_func_resultSelector" => global::Tsavorite.Linq.Enumerable.Aggregate(source.Int,10,static (acc,value) => acc+value,static value => value*2),
            "AggregateBy_keySelector" => global::Tsavorite.Linq.Enumerable.AggregateBy(source.Int,static value => value%16,0,static (acc,value) => acc+value),
            "All_predicate" => global::Tsavorite.Linq.Enumerable.All(source.Int,static value => value>0),
            "Any" => global::Tsavorite.Linq.Enumerable.Any(source.Int),
            "Any_predicate" => global::Tsavorite.Linq.Enumerable.Any(source.Int,static value => value>1),
            "Append_element" => global::Tsavorite.Linq.Enumerable.Append(source.Int,-1),
            "AsEnumerable" => global::Tsavorite.Linq.Enumerable.AsEnumerable(source.Int),
            "Average" => global::Tsavorite.Linq.Enumerable.Average(source.Int),
            "Average_selector" => global::Tsavorite.Linq.Enumerable.Average(source.Pairs,static value => value.Value),
            "Chunk_size" => global::Tsavorite.Linq.Enumerable.Chunk(source.Int,128),
            "Concat_second" => global::Tsavorite.Linq.Enumerable.Concat(source.Int,source.Second),
            "Contains_value" => global::Tsavorite.Linq.Enumerable.Contains(source.Int,1),
            "Count" => global::Tsavorite.Linq.Enumerable.Count(source.Int),
            "Count_predicate" => global::Tsavorite.Linq.Enumerable.Count(source.Int,static value => (value&1)==0),
            "CountBy_keySelector" => global::Tsavorite.Linq.Enumerable.CountBy(source.Int,static value => value%16),
            "DefaultIfEmpty" => global::Tsavorite.Linq.Enumerable.DefaultIfEmpty(source.Empty),
            "Distinct" => global::Tsavorite.Linq.Enumerable.Distinct(source.Int),
            "DistinctBy_keySelector" => global::Tsavorite.Linq.Enumerable.DistinctBy(source.Pairs,static value => value.Key),
            "ElementAt_index" => global::Tsavorite.Linq.Enumerable.ElementAt(source.Single,0),
            "ElementAtOrDefault_index" => global::Tsavorite.Linq.Enumerable.ElementAtOrDefault(source.Single,0),
            "Except_second" => global::Tsavorite.Linq.Enumerable.Except(source.Int,source.Second),
            "ExceptBy_keySelector" => global::Tsavorite.Linq.Enumerable.ExceptBy(source.Pairs,source.Keys,static value => value.Key),
            "First" => global::Tsavorite.Linq.Enumerable.First(source.Single),
            "FirstOrDefault" => global::Tsavorite.Linq.Enumerable.FirstOrDefault(source.Single),
            "FirstOrDefault_predicate" => global::Tsavorite.Linq.Enumerable.FirstOrDefault(source.Int,static value => value>0),
            "GroupBy_elementSelector" => global::Tsavorite.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key,static value => value.Value),
            "GroupBy_elementSelector_resultSelector" => global::Tsavorite.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key,static value => value.Value,static (key,values) => new Pair(key,global::System.Linq.Enumerable.Sum(values))),
            "GroupBy_keySelector" => global::Tsavorite.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key),
            "GroupBy_resultSelector" => global::Tsavorite.Linq.Enumerable.GroupBy(source.Pairs,static value => value.Key,static (key,values) => new Pair(key,global::System.Linq.Enumerable.Count(values))),
            "GroupJoin_inner" => global::Tsavorite.Linq.Enumerable.GroupJoin(source.Pairs,source.PairSecond,static outer => outer.Key,static inner => inner.Key,static (outer,inners) => new Pair(outer.Value,global::System.Linq.Enumerable.Count(inners))),
            "Index" => global::Tsavorite.Linq.Enumerable.Index(source.Int),
            "Intersect_second" => global::Tsavorite.Linq.Enumerable.Intersect(source.Int,source.Second),
            "IntersectBy_keySelector" => global::Tsavorite.Linq.Enumerable.IntersectBy(source.Pairs,source.Keys,static value => value.Key),
            "Join_inner" => global::Tsavorite.Linq.Enumerable.Join(source.Pairs,source.PairSecond,static outer => outer.Key,static inner => inner.Key,static (outer,inner) => new Pair(outer.Value,inner.Value)),
            "Last" => global::Tsavorite.Linq.Enumerable.Last(source.Single),
            "LastOrDefault" => global::Tsavorite.Linq.Enumerable.LastOrDefault(source.Single),
            "LastOrDefault_predicate" => global::Tsavorite.Linq.Enumerable.LastOrDefault(source.Int,static value => value>0),
            "LongCount" => global::Tsavorite.Linq.Enumerable.LongCount(source.Int),
            "LongCount_predicate" => global::Tsavorite.Linq.Enumerable.LongCount(source.Int,static value => (value&1)==0),
            "Max" => global::Tsavorite.Linq.Enumerable.Max(source.Int),
            "Max_selector" => global::Tsavorite.Linq.Enumerable.Max(source.Pairs,static value => value.Value),
            "MaxBy_keySelector" => global::Tsavorite.Linq.Enumerable.MaxBy(source.Pairs,static value => value.Value),
            "Min" => global::Tsavorite.Linq.Enumerable.Min(source.Int),
            "Min_selector" => global::Tsavorite.Linq.Enumerable.Min(source.Pairs,static value => value.Value),
            "MinBy_keySelector" => global::Tsavorite.Linq.Enumerable.MinBy(source.Pairs,static value => value.Value),
            "Order" => global::Tsavorite.Linq.Enumerable.Order(source.Int),
            "OrderBy_keySelector" => global::Tsavorite.Linq.Enumerable.OrderBy(source.Pairs,static value => value.Value),
            "OrderByDescending_keySelector" => global::Tsavorite.Linq.Enumerable.OrderByDescending(source.Pairs,static value => value.Value),
            "OrderDescending" => global::Tsavorite.Linq.Enumerable.OrderDescending(source.Int),
            "Prepend_element" => global::Tsavorite.Linq.Enumerable.Prepend(source.Int,-1),
            "Reverse" => global::Tsavorite.Linq.Enumerable.Reverse(source.Single),
            "Select_indexSelector" => global::Tsavorite.Linq.Enumerable.Select(source.Int,static (value,index) => value+index),
            "Select_selector" => global::Tsavorite.Linq.Enumerable.Select(source.Int,static value => value*2),
            "SelectMany_indexSelector" => global::Tsavorite.Linq.Enumerable.SelectMany(source.Int,static (value,index) => new[] { value+index }),
            "SelectMany_indexSelector_resultSelector" => global::Tsavorite.Linq.Enumerable.SelectMany(source.Int,static (value,index) => new[] { value,index },static (value,item) => value+item),
            "SelectMany_resultSelector" => global::Tsavorite.Linq.Enumerable.SelectMany(source.Int,static value => new[] { value,value+1 },static (value,item) => value+item),
            "SelectMany_selector" => global::Tsavorite.Linq.Enumerable.SelectMany(source.Int,static value => new[] { value,value+1 }),
            "SequenceEqual_second" => global::Tsavorite.Linq.Enumerable.SequenceEqual(source.Single,source.SingleSecond),
            "Single" => global::Tsavorite.Linq.Enumerable.Single(source.Single),
            "Single_predicate" => global::Tsavorite.Linq.Enumerable.Single(source.Single,static value => value>0),
            "SingleOrDefault" => global::Tsavorite.Linq.Enumerable.SingleOrDefault(source.Single),
            "SingleOrDefault_predicate" => global::Tsavorite.Linq.Enumerable.SingleOrDefault(source.Single,static value => value>0),
            "Skip_count" => global::Tsavorite.Linq.Enumerable.Skip(source.Int,1),
            "SkipLast_count" => global::Tsavorite.Linq.Enumerable.SkipLast(source.Single,1),
            "SkipWhile_indexPredicate" => global::Tsavorite.Linq.Enumerable.SkipWhile(source.Int,static (value,index) => index<1&&value>0),
            "SkipWhile_predicate" => global::Tsavorite.Linq.Enumerable.SkipWhile(source.Int,static value => value<2),
            "Sum" => global::Tsavorite.Linq.Enumerable.Sum(source.Int),
            "Sum_selector" => global::Tsavorite.Linq.Enumerable.Sum(source.Pairs,static value => value.Value),
            "Take_count" => global::Tsavorite.Linq.Enumerable.Take(source.Int,16),
            "TakeLast_count" => global::Tsavorite.Linq.Enumerable.TakeLast(source.Single,1),
            "TakeWhile_indexPredicate" => global::Tsavorite.Linq.Enumerable.TakeWhile(source.Int,static (value,index) => index<16&&value>0),
            "TakeWhile_predicate" => global::Tsavorite.Linq.Enumerable.TakeWhile(source.Int,static value => value<16),
            "ThenBy_keySelector" => global::System.Linq.Enumerable.ThenBy(global::Tsavorite.Linq.Enumerable.OrderBy(source.Pairs,static value => value.Key),static value => value.Value),
            "ThenByDescending_keySelector" => global::System.Linq.Enumerable.ThenByDescending(global::Tsavorite.Linq.Enumerable.OrderBy(source.Pairs,static value => value.Key),static value => value.Value),
            "ToArray" => global::Tsavorite.Linq.Enumerable.ToArray(source.Int),
            "ToDictionary_elementSelector" => global::Tsavorite.Linq.Enumerable.ToDictionary(source.Int,static value => value,static value => value*2),
            "ToDictionary_keySelector" => global::Tsavorite.Linq.Enumerable.ToDictionary(source.Int,static value => value),
            "ToHashSet" => global::Tsavorite.Linq.Enumerable.ToHashSet(source.Int),
            "ToList" => global::Tsavorite.Linq.Enumerable.ToList(source.Int),
            "ToLookup_elementSelector" => global::Tsavorite.Linq.Enumerable.ToLookup(source.Pairs,static value => value.Key,static value => value.Value),
            "ToLookup_keySelector" => global::Tsavorite.Linq.Enumerable.ToLookup(source.Pairs,static value => value.Key),
            "TryGetNonEnumeratedCount" => TryGetNonEnumeratedCount(source.Int),
            "Union_second" => global::Tsavorite.Linq.Enumerable.Union(source.Int,source.Second),
            "UnionBy_keySelector" => global::Tsavorite.Linq.Enumerable.UnionBy(source.Pairs,source.PairSecond,static value => value.Key),
            "Where_indexPredicate" => global::Tsavorite.Linq.Enumerable.Where(source.Int,static (value,index) => value>index),
            "Where_predicate" => global::Tsavorite.Linq.Enumerable.Where(source.Int,static value => (value&1)==0),
            "Zip_second" => global::Tsavorite.Linq.Enumerable.Zip(source.Int,source.Second),
            "Zip_tuple" => global::Tsavorite.Linq.Enumerable.Zip(source.Int,source.Second,source.Third),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName),methodName,null),
        };

    public static string RunSystemForTest(string methodName) =>
        Normalize(RunSystem(methodName,SystemSources.CreateForTest()));

    public static string RunTsavoriteForTest(string methodName) {
        var systemSources = SystemSources.CreateForTest();
        using var tsavoriteSources = TsavoriteSources.Create(systemSources);
        return Normalize(RunTsavorite(methodName,tsavoriteSources));
    }

    private static int TryGetNonEnumeratedCount<T>(IEnumerable<T> source) =>
        global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var count) ? count : -1;

    private static int TryGetNonEnumeratedCount<T>(global::Tsavorite.Collections.IHashSet<T> source)
        where T : IEquatable<T> {
        return global::Tsavorite.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var count) ? count : -1;
    }

    private static string Normalize(object? value) {
        if(value is null) {
            return "<null>";
        }
        if(value is string text) {
            return text;
        }
        if(TryGetKey(value,out var key)&&value is global::System.Collections.IEnumerable keyedEnumerable) {
            return "{"+Normalize(key)+":"+NormalizeEnumerable(keyedEnumerable)+"}";
        }
        if(value is global::System.Collections.IDictionary dictionary) {
            return NormalizeDictionary(dictionary);
        }
        if(value is global::System.Collections.IEnumerable enumerable) {
            return NormalizeEnumerable(enumerable);
        }
        return value.ToString()??string.Empty;
    }

    private static string NormalizeEnumerable(global::System.Collections.IEnumerable enumerable) {
        var values = new List<string>();
        foreach(var item in enumerable) {
            values.Add(Normalize(item));
        }
        values.Sort(StringComparer.Ordinal);
        return "["+string.Join(",",values)+"]";
    }

    private static string NormalizeDictionary(global::System.Collections.IDictionary dictionary) {
        var values = new List<string>();
        foreach(global::System.Collections.DictionaryEntry entry in dictionary) {
            values.Add(Normalize(entry.Key)+"="+Normalize(entry.Value));
        }
        values.Sort(StringComparer.Ordinal);
        return "["+string.Join(",",values)+"]";
    }

    private static bool TryGetKey(object instance,out object? key) {
        var property = instance.GetType().GetProperty("Key");
        if(property is null) {
            key=null;
            return false;
        }

        key=property.GetValue(instance);
        return true;
    }

    public sealed class SystemSources {
        private SystemSources(
            global::System.Collections.Generic.HashSet<int> intSource,
            global::System.Collections.Generic.HashSet<int> second,
            global::System.Collections.Generic.HashSet<int> third,
            global::System.Collections.Generic.HashSet<int> empty,
            global::System.Collections.Generic.HashSet<int> single,
            global::System.Collections.Generic.HashSet<int> singleSecond,
            global::System.Collections.Generic.HashSet<int> keys,
            global::System.Collections.Generic.HashSet<Pair> pairs,
            global::System.Collections.Generic.HashSet<Pair> pairSecond) {
            this.Int=intSource;
            this.Second=second;
            this.Third=third;
            this.Empty=empty;
            this.Single=single;
            this.SingleSecond=singleSecond;
            this.Keys=keys;
            this.Pairs=pairs;
            this.PairSecond=pairSecond;
        }

        public global::System.Collections.Generic.HashSet<int> Int { get; }
        public global::System.Collections.Generic.HashSet<int> Second { get; }
        public global::System.Collections.Generic.HashSet<int> Third { get; }
        public global::System.Collections.Generic.HashSet<int> Empty { get; }
        public global::System.Collections.Generic.HashSet<int> Single { get; }
        public global::System.Collections.Generic.HashSet<int> SingleSecond { get; }
        public global::System.Collections.Generic.HashSet<int> Keys { get; }
        public global::System.Collections.Generic.HashSet<Pair> Pairs { get; }
        public global::System.Collections.Generic.HashSet<Pair> PairSecond { get; }

        public static SystemSources Create(int length) {
            length=Math.Max(length,32);
            return new SystemSources(
                CreateIntSource(1,length),
                CreateIntSource((length/2)+1,length/2),
                CreateIntSource((length/3)+1,length/2),
                [],
                [1],
                [1],
                CreateIntSource(1,Math.Min(16,length)),
                CreatePairSource(length,0),
                CreatePairSource(Math.Max(32,length/2),100_000));
        }

        public static SystemSources CreateForTest() =>
            new(
                [1],
                [1],
                [1],
                [],
                [1],
                [1],
                [1],
                [new Pair(1,10)],
                [new Pair(1,100)]);

        private static global::System.Collections.Generic.HashSet<int> CreateIntSource(int start,int length) {
            var result = new global::System.Collections.Generic.HashSet<int>();
            for(var i = 0;i<length;i++) {
                result.Add(start+i);
            }
            return result;
        }

        private static global::System.Collections.Generic.HashSet<Pair> CreatePairSource(int length,int offset) {
            var result = new global::System.Collections.Generic.HashSet<Pair>();
            for(var i = 1;i<=length;i++) {
                result.Add(new Pair(i%1024,offset+i));
            }
            return result;
        }
    }

    public sealed class TsavoriteSources:IDisposable {
        private TsavoriteSources(
            SetHandle<int> intSource,
            SetHandle<int> second,
            SetHandle<int> third,
            SetHandle<int> empty,
            SetHandle<int> single,
            SetHandle<int> singleSecond,
            SetHandle<int> keys,
            SetHandle<Pair> pairs,
            SetHandle<Pair> pairSecond) {
            this.intSource=intSource;
            this.second=second;
            this.third=third;
            this.empty=empty;
            this.single=single;
            this.singleSecond=singleSecond;
            this.keys=keys;
            this.pairs=pairs;
            this.pairSecond=pairSecond;
        }

        private readonly SetHandle<int> intSource;
        private readonly SetHandle<int> second;
        private readonly SetHandle<int> third;
        private readonly SetHandle<int> empty;
        private readonly SetHandle<int> single;
        private readonly SetHandle<int> singleSecond;
        private readonly SetHandle<int> keys;
        private readonly SetHandle<Pair> pairs;
        private readonly SetHandle<Pair> pairSecond;

        public global::Tsavorite.Collections.IHashSet<int> Int => this.intSource.Set;
        public global::Tsavorite.Collections.IHashSet<int> Second => this.second.Set;
        public global::Tsavorite.Collections.IHashSet<int> Third => this.third.Set;
        public global::Tsavorite.Collections.IHashSet<int> Empty => this.empty.Set;
        public global::Tsavorite.Collections.IHashSet<int> Single => this.single.Set;
        public global::Tsavorite.Collections.IHashSet<int> SingleSecond => this.singleSecond.Set;
        public global::Tsavorite.Collections.IHashSet<int> Keys => this.keys.Set;
        public global::Tsavorite.Collections.IHashSet<Pair> Pairs => this.pairs.Set;
        public global::Tsavorite.Collections.IHashSet<Pair> PairSecond => this.pairSecond.Set;

        public static TsavoriteSources Create(SystemSources source) =>
            new(
                CreateSet(source.Int),
                CreateSet(source.Second),
                CreateSet(source.Third),
                CreateSet(source.Empty),
                CreateSet(source.Single),
                CreateSet(source.SingleSecond),
                CreateSet(source.Keys),
                CreateSet(source.Pairs),
                CreateSet(source.PairSecond));

        public void Dispose() {
            this.pairSecond.Dispose();
            this.pairs.Dispose();
            this.keys.Dispose();
            this.singleSecond.Dispose();
            this.single.Dispose();
            this.empty.Dispose();
            this.third.Dispose();
            this.second.Dispose();
            this.intSource.Dispose();
        }

        private static SetHandle<T> CreateSet<T>(IEnumerable<T> values)
            where T : unmanaged, IEquatable<T> {
            var store = global::Tsavorite.Stores.HashStore.Create().Create<T>();
            var result = store.Create();
            foreach(var value in values) {
                result.Add(value);
            }
            return new SetHandle<T>(store,result);
        }

        private sealed class SetHandle<T>(
            IDisposable store,
            global::Tsavorite.Collections.HashSet<T> set):IDisposable
            where T : unmanaged, IEquatable<T> {
            public global::Tsavorite.Collections.HashSet<T> Set { get; } = set;

            public void Dispose() {
                this.Set.Dispose();
                store.Dispose();
            }
        }
    }

    public readonly record struct Pair(int Key,int Value);
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
