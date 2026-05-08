using Seamless.Linq;
using e = System.Linq.Enumerable;
using a = Seamless.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;


public sealed class EnumerableMethod1 {
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_func() => Assert.Equal(
        e.Aggregate(Source,static (left,right) => left+right),
        Source.Aggregate(static (left,right) => left+right)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_seed_func() => Assert.Equal(
        e.Aggregate(Source,10,static (sum,value) => sum+value),
        Source.Aggregate(10,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_seed_func_resultSelector() => Assert.Equal(
        e.Aggregate(Source,10,static (sum,value) => sum+value,static sum => sum*2),
        Source.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void AggregateBy_keySelector_seed_func() => Assert.Equal(
        e.AggregateBy(Source,static value => value%2,0,static (sum,value) => sum+value),
        Source.AggregateBy(static value => value%2,0,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void AggregateBy_keySelector_seedSelector_func() => Assert.Equal(
        e.AggregateBy(Source,static value => value%2,static _ => 1,static (sum,value) => sum+value),
        Source.AggregateBy(static value => value%2,static _ => 1,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void All_predicate() => Assert.Equal(
        e.All(Source,static value => value>0),
        Source.All(static value => value>0)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Any() => Assert.Equal(
        e.Any(Source),
        Source.Any()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Any_predicate() => Assert.Equal(
        e.Any(Source,static value => value>3),
        Source.Any(static value => value>3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Append_element() => Assert.Equal(
        e.Append(Source,5),
        Source.Append(5)
    );
    [Fact, Trait("Category","Intercept")]
    private static void AsEnumerable() => Assert.Equal(
        e.AsEnumerable(Source),
        Source.AsEnumerable()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Average_int() => Assert.Equal(
        e.Average(Source),
        Source.Average()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Average_selector() => Assert.Equal(
        e.Average(Rows(),static row => row.Value),
        Rows().Average(static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Cast() => Assert.Equal(
        e.Cast<int>(CastObjects()),
        CastObjects().Cast<int>()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Chunk_size() => Assert.Equal(
        e.Chunk(Source,2),
        Source.Chunk(2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Concat_second() => Assert.Equal(
        e.Concat(Source,Second),
        Source.Concat(Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Contains_value() => Assert.Equal(
        e.Contains(Source,3),
        Source.Contains(3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Contains_value_comparer() => Assert.Equal(
        e.Contains(Source,3,EqualityComparer<int>.Default),
        Source.Contains(3,EqualityComparer<int>.Default)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Count() => Assert.Equal(
        e.Count(Source),
        Source.Count()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Count_predicate() => Assert.Equal(
        e.Count(Source,static value => value>2),
        Source.Count(static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void CountBy_keySelector() => Assert.Equal(
        e.CountBy(Source,static value => value%2),
        Source.CountBy(static value => value%2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void DefaultIfEmpty() => Assert.Equal(
        e.DefaultIfEmpty(Array.Empty<int>()),
        Array.Empty<int>().DefaultIfEmpty()
    );
    [Fact, Trait("Category","Intercept")]
    private static void DefaultIfEmpty_defaultValue() => Assert.Equal(
        e.DefaultIfEmpty(Array.Empty<int>(),9),
        Array.Empty<int>().DefaultIfEmpty(9)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Distinct() => Assert.Equal(
        e.Distinct(Duplicates),
        Duplicates.Distinct()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Distinct_comparer() => Assert.Equal(
        e.Distinct(Duplicates,EqualityComparer<int>.Default),
        Duplicates.Distinct(EqualityComparer<int>.Default)
    );
    [Fact, Trait("Category","Intercept")]
    private static void DistinctBy_keySelector() => Assert.Equal(
        e.DistinctBy(Rows(),static row => row.Key),
        Rows().DistinctBy(static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ElementAt_index() => Assert.Equal(
        e.ElementAt(Source,2),
        Source.ElementAt(2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ElementAtOrDefault_index() => Assert.Equal(
        e.ElementAtOrDefault(Source,20),
        Source.ElementAtOrDefault(20)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Empty() => Assert.Equal(
        e.Empty<int>(),
        a.Empty<int>()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Except_second() => Assert.Equal(
        e.Except(Source,Second),
        Source.Except(Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ExceptBy_second_keySelector() => Assert.Equal(
        e.ExceptBy(Rows(),new[] { 2 },static row => row.Key),
        Rows().ExceptBy(new[] { 2 },static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void First() => Assert.Equal(
        e.First(Source),
        Source.First()
    );
    [Fact, Trait("Category","Intercept")]
    private static void First_predicate() => Assert.Equal(
        e.First(Source,static value => value>2),
        Source.First(static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void FirstOrDefault() => Assert.Equal(
        e.FirstOrDefault(Source),Source.FirstOrDefault()
    );
    [Fact, Trait("Category","Intercept")]
    private static void FirstOrDefault_predicate_defaultValue() => Assert.Equal(
        e.FirstOrDefault(Source,static value => value>9,-1),
        Source.FirstOrDefault(static value => value>9,-1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void GroupBy_keySelector() => Assert.Equal(
        e.GroupBy(Rows(),static row => row.Key),
        Rows().GroupBy(static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void GroupJoin_keySelectors_resultSelector() => Assert.Equal(
        e.GroupJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,Values: string.Join(",",innerValues))),
        Source.GroupJoin(Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,Values: string.Join(",",innerValues)))
    );
    [Fact, Trait("Category","Intercept")]
    private static void Index() => Assert.Equal(
        e.Index(Source),
        Source.Index()
    );
    [Fact, Trait("Category","Intercept")]
    private static void InfiniteSequence_start_step() => Assert.Equal(
        e.Take(e.InfiniteSequence(1,1),1000),
        a.InfiniteSequence(1,1).Take(1000)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Intersect_second() => Assert.Equal(
        e.Intersect(Source,Second),
        Source.Intersect(Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void IntersectBy_second_keySelector() => Assert.Equal(
        e.IntersectBy(Rows(),new[] { 2 },static row => row.Key),
        Rows().IntersectBy(new[] { 2 },static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Join_keySelectors_resultSelector() => Assert.Equal(
        e.Join(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        Source.Join(Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Last() => Assert.Equal(
        e.Last(Source),
        Source.Last()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Last_predicate() => Assert.Equal(
        e.Last(Source,static value => value<4),
        Source.Last(static value => value<4)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LastOrDefault() => Assert.Equal(
        e.LastOrDefault(Source),
        Source.LastOrDefault()
    );
    [Fact, Trait("Category","Intercept")]
    private static void LastOrDefault_predicate_defaultValue() => Assert.Equal(
        e.LastOrDefault(Source,static value => value>9,-1),
        Source.LastOrDefault(static value => value>9,-1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LeftJoin_keySelectors_resultSelector() => Assert.Equal(
        e.LeftJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        Source.LeftJoin(Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LongCount() => Assert.Equal(
        e.LongCount(Source),
        Source.LongCount()
    );
    [Fact, Trait("Category","Intercept")]
    private static void LongCount_predicate() => Assert.Equal(
        e.LongCount(Source,static value => value>2),
        Source.LongCount(static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Max_int() => Assert.Equal(
        e.Max(Source),
        Source.Max()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Max_selector() => Assert.Equal(
        e.Max(Rows(),static row => row.Value),
        Rows().Max(static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void MaxBy_keySelector() => Assert.Equal(
        e.MaxBy(Rows(),static row => row.Value),
        Rows().MaxBy(static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Min_int() => Assert.Equal(
        e.Min(Source),
        Source.Min()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Min_selector() => Assert.Equal(
        e.Min(Rows(),static row => row.Value),
        Rows().Min(static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void MinBy_keySelector() => Assert.Equal(
        e.MinBy(Rows(),static row => row.Value),
        Rows().MinBy(static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OfType() => Assert.Equal(
        e.OfType<int>(MixedObjects()),
        MixedObjects().OfType<int>()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Order() => Assert.Equal(
        e.Order(Source),
        Source.Order()
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderBy_keySelector() => Assert.Equal(
        e.OrderBy(Rows(),static row => row.Key),
        Rows().OrderBy(static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderByDescending_keySelector() => Assert.Equal(
        e.OrderByDescending(Rows(),static row => row.Key),
        Rows().OrderByDescending(static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderDescending() => Assert.Equal(
        e.OrderDescending(Source),
        Source.OrderDescending()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Prepend_element() => Assert.Equal(
        e.Prepend(Source,0),
        Source.Prepend(0)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Range_start_count() => Assert.Equal(
        e.Range(1,4),
        a.Range(1,4)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Repeat_element_count() => Assert.Equal(
        e.Repeat(2,3),
        a.Repeat(2,3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Reverse() => Assert.Equal(
        e.Reverse(Source),
        Source.Reverse()
    );
    [Fact, Trait("Category","Intercept")]
    private static void RightJoin_keySelectors_resultSelector() => Assert.Equal(
        e.RightJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        Source.RightJoin(Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Select_selector() => Assert.Equal(
        e.Select(Source,static value => value*2),
        Source.Select(static value => value*2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Select_selector_index() => Assert.Equal(
        e.Select(Source,static (value,index) => value+index),
        Source.Select(static (value,index) => value+index)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SelectMany_selector() => Assert.Equal(
        e.SelectMany(Source,static value => new[] { value,value+10 }),
        Source.SelectMany(static value => new[] { value,value+10 })
    );
    [Fact, Trait("Category","Intercept")]
    private static void SelectMany_selector_index() => Assert.Equal(
        e.SelectMany(Source,static (value,index) => new[] { value,index }),
        Source.SelectMany(static (value,index) => new[] { value,index })
    );
    [Fact, Trait("Category","Intercept")]
    private static void SelectMany_collectionSelector_resultSelector() => Assert.Equal(
        e.SelectMany(Source,static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue),
        Source.SelectMany(static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Sequence_start_end_step() => Assert.Equal(
        e.Sequence(1,4,1),
        a.Sequence(1,4,1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SequenceEqual_second() => Assert.Equal(
        e.SequenceEqual(Source,Source),
        Source.SequenceEqual(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Shuffle() {
        e.Shuffle(Source);
        Source.Shuffle();
    }
    [Fact, Trait("Category","Intercept")]
    private static void Single() => Assert.Equal(
        e.Single(SingleSource()),
        SingleSource().Single()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Single_predicate() => Assert.Equal(
        e.Single(Source,static value => value==2),
        Source.Single(static value => value==2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SingleOrDefault() => Assert.Equal(
        e.SingleOrDefault(SingleSource()),
        SingleSource().SingleOrDefault()
    );
    [Fact, Trait("Category","Intercept")]
    private static void SingleOrDefault_predicate_defaultValue() => Assert.Equal(
        e.SingleOrDefault(Source,static value => value==9,-1),
        Source.SingleOrDefault(static value => value==9,-1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Skip_count() => Assert.Equal(
        e.Skip(Source,1),
        Source.Skip(1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SkipLast_count() => Assert.Equal(
        e.SkipLast(Source,1),
        Source.SkipLast(1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SkipWhile_predicate() => Assert.Equal(
        e.SkipWhile(Source,static value => value<3),
        Source.SkipWhile(static value => value<3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SkipWhile_predicate_index() => Assert.Equal(
        e.SkipWhile(Source,static (value,index) => value<3&&index<2),
        Source.SkipWhile(static (value,index) => value<3&&index<2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Sum_int() => Assert.Equal(
        e.Sum(Source),
        Source.Sum()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Sum_selector() => Assert.Equal(
        e.Sum(Rows(),static row => row.Value),
        Rows().Sum(static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Take_count() => Assert.Equal(
        e.Take(Source,3),
        Source.Take(3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Take_range() => Assert.Equal(
        e.Take(Source,1..3),
        Source.Take(1..3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TakeLast_count() => Assert.Equal(
        e.TakeLast(Source,2),
        Source.TakeLast(2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TakeWhile_predicate() => Assert.Equal(
        e.TakeWhile(Source,static value => value<4),
        Source.TakeWhile(static value => value<4)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TakeWhile_predicate_index() => Assert.Equal(
        e.TakeWhile(Source,static (value,index) => value<4&&index<3),
        Source.TakeWhile(static (value,index) => value<4&&index<3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ThenBy_keySelector() => Assert.Equal(
        e.ThenBy(Rows().OrderBy(static row => row.Key),static row => row.Name),
        Rows().OrderBy(static row => row.Key).ThenBy(static row => row.Name)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ThenByDescending_keySelector() => Assert.Equal(
        e.ThenByDescending(Rows().OrderBy(static row => row.Key),static row => row.Name),
        Rows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToArray() => Assert.Equal(
        e.ToArray(Source),
        Source.ToArray()
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToDictionary_keySelector() => Assert.Equal(
        e.ToDictionary(Rows(),static row => row.Key),
        Rows().ToDictionary(static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToHashSet() => Assert.Equal(
        e.ToHashSet(Source),
        Source.ToHashSet()
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToList() {
        var result = new List<int>();
        foreach(var item in Source) {
            result.Add(item);
        }
        Assert.Equal(
            e.ToList(Source),
            result
        );
    }
    [Fact, Trait("Category","Intercept")]
    private static void ToLookup_keySelector() => Assert.Equal(
        e.ToLookup(Rows(),static row => row.Key),
        Rows().ToLookup(static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TryGetNonEnumeratedCount_source() {
        var expected = e.TryGetNonEnumeratedCount(Source,out var expectedCount);
        var actual = Source.TryGetNonEnumeratedCount(out var actualCount);
        Assert.Equal(expected,actual);
        Assert.Equal(expectedCount,actualCount);
    }
    [Fact, Trait("Category","Intercept")]
    private static void Union_second() => Assert.Equal(
        e.Union(Source,Second),
        Source.Union(Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void UnionBy_second_keySelector() => Assert.Equal(
        e.UnionBy(Rows(),OtherRows(),static row => row.Key),
        Rows().UnionBy(OtherRows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Where_predicate() => Assert.Equal(
        e.Where(Source,static value => value>2),
        Source.Where(static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Where_predicate_index() => Assert.Equal(
        e.Where(Source,static (value,index) => value>index),
        Source.Where(static (value,index) => value>index)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Zip_second() => Assert.Equal(
        e.Zip(Source,Second),
        Source.Zip(Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Zip_second_resultSelector() => Assert.Equal(
        e.Zip(Source,Second,static (first,second) => first+second),
        Source.Zip(Second,static (first,second) => first+second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Zip_second_third() => Assert.Equal(
        e.Zip(Source,Second,Third),Source.Zip(Second,Third)
    );
    private static readonly int[] Source = [1,2,3,4];
    private static int[] Second => [3,4,5];
    private static int[] Third => [10,20,30];
    private static int[] Duplicates => [1,2,2,3,3,4];
    private static int[] SingleSource() => [2];
    private static global::System.Collections.IEnumerable CastObjects() => new object[] { 1,2,3 };
    private static object[] MixedObjects() => [1,"skip",2,3.0,4];
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];
    private static Row[] OtherRows() => [new(3,300,"gamma-2"),new(4,40,"delta")];
    private sealed record Row(int Key,int Value,string Name);
}
