using e = System.Linq.Enumerable;
using a = Seamless.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;


public sealed class EnumerableMethod1 {
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_func() => Assert.Equal(
        e.Aggregate(Source,static (left,right) => left+right),
        a.Aggregate(Source,static (left,right) => left+right)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_seed_func() => Assert.Equal(
        e.Aggregate(Source,10,static (sum,value) => sum+value),
        a.Aggregate(Source,10,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Aggregate_seed_func_resultSelector() => Assert.Equal(
        e.Aggregate(Source,10,static (sum,value) => sum+value,static sum => sum*2),
        a.Aggregate(Source,10,static (sum,value) => sum+value,static sum => sum*2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void AggregateBy_keySelector_seed_func() => Assert.Equal(
        e.AggregateBy(Source,static value => value%2,0,static (sum,value) => sum+value),
        a.AggregateBy(Source,static value => value%2,0,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void AggregateBy_keySelector_seedSelector_func() => Assert.Equal(
        e.AggregateBy(Source,static value => value%2,static _ => 1,static (sum,value) => sum+value),
        a.AggregateBy(Source,static value => value%2,static _ => 1,static (sum,value) => sum+value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void All_predicate() => Assert.Equal(
        e.All(Source,static value => value>0),
        a.All(Source,static value => value>0)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Any() => Assert.Equal(
        e.Any(Source),
        a.Any(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Any_predicate() => Assert.Equal(
        e.Any(Source,static value => value>3),
        a.Any(Source,static value => value>3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Append_element() => Assert.Equal(
        e.Append(Source,5),
        a.Append(Source,5)
    );
    [Fact, Trait("Category","Intercept")]
    private static void AsEnumerable() => Assert.Equal(
        e.AsEnumerable(Source),
        a.AsEnumerable(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Average_int() => Assert.Equal(
        e.Average(Source),
        a.Average(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Average_selector() => Assert.Equal(
        e.Average(Rows(),static row => row.Value),
        a.Average(Rows(),static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Cast() => Assert.Equal(
        e.Cast<int>(CastObjects()),
        a.Cast<int>(CastObjects())
    );
    [Fact, Trait("Category","Intercept")]
    private static void Chunk_size() => Assert.Equal(
        e.Chunk(Source,2),
        a.Chunk(Source,2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Concat_second() => Assert.Equal(
        e.Concat(Source,Second),
        a.Concat(Source,Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Contains_value() => Assert.Equal(
        e.Contains(Source,3),
        a.Contains(Source,3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Contains_value_comparer() => Assert.Equal(
        e.Contains(Source,3,EqualityComparer<int>.Default),
        a.Contains(Source,3,EqualityComparer<int>.Default)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Count() => Assert.Equal(
        e.Count(Source),
        a.Count(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Count_predicate() => Assert.Equal(
        e.Count(Source,static value => value>2),
        a.Count(Source,static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void CountBy_keySelector() => Assert.Equal(
        e.CountBy(Source,static value => value%2),
        a.CountBy(Source,static value => value%2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void DefaultIfEmpty() => Assert.Equal(
        e.DefaultIfEmpty(Array.Empty<int>()),
        a.DefaultIfEmpty(Array.Empty<int>())
    );
    [Fact, Trait("Category","Intercept")]
    private static void DefaultIfEmpty_defaultValue() => Assert.Equal(
        e.DefaultIfEmpty(Array.Empty<int>(),9),
        a.DefaultIfEmpty(Array.Empty<int>(),9)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Distinct() => Assert.Equal(
        e.Distinct(Duplicates),
        a.Distinct(Duplicates)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Distinct_comparer() => Assert.Equal(
        e.Distinct(Duplicates,EqualityComparer<int>.Default),
        a.Distinct(Duplicates,EqualityComparer<int>.Default)
    );
    [Fact, Trait("Category","Intercept")]
    private static void DistinctBy_keySelector() => Assert.Equal(
        e.DistinctBy(Rows(),static row => row.Key),
        a.DistinctBy(Rows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ElementAt_index() => Assert.Equal(
        e.ElementAt(Source,2),
        a.ElementAt(Source,2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ElementAtOrDefault_index() => Assert.Equal(
        e.ElementAtOrDefault(Source,20),
        a.ElementAtOrDefault(Source,20)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Empty() => Assert.Equal(
        e.Empty<int>(),
        a.Empty<int>()
    );
    [Fact, Trait("Category","Intercept")]
    private static void Except_second() => Assert.Equal(
        e.Except(Source,Second),
        a.Except(Source,Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ExceptBy_second_keySelector() => Assert.Equal(
        e.ExceptBy(Rows(),new[] { 2 },static row => row.Key),
        a.ExceptBy(Rows(),new[] { 2 },static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void First() => Assert.Equal(
        e.First(Source),
        a.First(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void First_predicate() => Assert.Equal(
        e.First(Source,static value => value>2),
        a.First(Source,static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void FirstOrDefault() => Assert.Equal(
        e.FirstOrDefault(Source),a.FirstOrDefault(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void FirstOrDefault_predicate_defaultValue() => Assert.Equal(
        e.FirstOrDefault(Source,static value => value>9,-1),
        a.FirstOrDefault(Source,static value => value>9,-1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void GroupBy_keySelector() => Assert.Equal(
        e.GroupBy(Rows(),static row => row.Key),
        a.GroupBy(Rows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void GroupJoin_keySelectors_resultSelector() => Assert.Equal(
        e.GroupJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,Values: string.Join(",",innerValues))),
        a.GroupJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,Values: string.Join(",",innerValues)))
    );
    [Fact, Trait("Category","Intercept")]
    private static void Index() => Assert.Equal(
        e.Index(Source),
        a.Index(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void InfiniteSequence_start_step() => Assert.Equal(
        e.Take(e.InfiniteSequence(1, 1),1000),
        a.Take(a.InfiniteSequence(1, 1),1000)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Intersect_second() => Assert.Equal(
        e.Intersect(Source,Second),
        a.Intersect(Source,Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void IntersectBy_second_keySelector() => Assert.Equal(
        e.IntersectBy(Rows(),new[] { 2 },static row => row.Key),
        a.IntersectBy(Rows(),new[] { 2 },static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Join_keySelectors_resultSelector() => Assert.Equal(
        e.Join(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        a.Join(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Last() => Assert.Equal(
        e.Last(Source),
        a.Last(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Last_predicate() => Assert.Equal(
        e.Last(Source,static value => value<4),
        a.Last(Source,static value => value<4)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LastOrDefault() => Assert.Equal(
        e.LastOrDefault(Source),
        a.LastOrDefault(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LastOrDefault_predicate_defaultValue() => Assert.Equal(
        e.LastOrDefault(Source,static value => value>9,-1),
        a.LastOrDefault(Source,static value => value>9,-1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LeftJoin_keySelectors_resultSelector() => Assert.Equal(
        e.LeftJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        a.LeftJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LongCount() => Assert.Equal(
        e.LongCount(Source),
        a.LongCount(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void LongCount_predicate() => Assert.Equal(
        e.LongCount(Source,static value => value>2),
        a.LongCount(Source,static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Max_int() => Assert.Equal(
        e.Max(Source),
        a.Max(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Max_selector() => Assert.Equal(
        e.Max(Rows(),static row => row.Value),
        a.Max(Rows(),static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void MaxBy_keySelector() => Assert.Equal(
        e.MaxBy(Rows(),static row => row.Value),
        a.MaxBy(Rows(),static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Min_int() => Assert.Equal(
        e.Min(Source),
        a.Min(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Min_selector() => Assert.Equal(
        e.Min(Rows(),static row => row.Value),
        a.Min(Rows(),static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void MinBy_keySelector() => Assert.Equal(
        e.MinBy(Rows(),static row => row.Value),
        a.MinBy(Rows(),static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OfType() => Assert.Equal(
        e.OfType<int>(MixedObjects()),
        a.OfType<int>(MixedObjects())
    );
    [Fact, Trait("Category","Intercept")]
    private static void Order() => Assert.Equal(
        e.Order(Source),
        a.Order(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderBy_keySelector() => Assert.Equal(
        e.OrderBy(Rows(),static row => row.Key),
        a.OrderBy(Rows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderByDescending_keySelector() => Assert.Equal(
        e.OrderByDescending(Rows(),static row => row.Key),
        a.OrderByDescending(Rows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void OrderDescending() => Assert.Equal(
        e.OrderDescending(Source),
        a.OrderDescending(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Prepend_element() => Assert.Equal(
        e.Prepend(Source,0),
        a.Prepend(Source,0)
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
        a.Reverse(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void RightJoin_keySelectors_resultSelector() => Assert.Equal(
        e.RightJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        a.RightJoin(Source,Second,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Select_selector() => Assert.Equal(
        e.Select(Source,static value => value*2),
        a.Select(Source,static value => value*2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Select_selector_index() => Assert.Equal(
        e.Select(Source,static (value,index) => value+index),
        a.Select(Source,static (value,index) => value+index)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SelectMany_selector() => Assert.Equal(
        e.SelectMany(Source,static value => new[] { value,value+10 }),
        a.SelectMany(Source,static value => new[] { value,value+10 })
    );
    [Fact, Trait("Category","Intercept")]
    private static void SelectMany_selector_index() => Assert.Equal(
        e.SelectMany(Source,static (value,index) => new[] { value,index }),
        a.SelectMany(Source,static (value,index) => new[] { value,index })
    );
    [Fact, Trait("Category","Intercept")]
    private static void SelectMany_collectionSelector_resultSelector() => Assert.Equal(
        e.SelectMany(Source,static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue),
        a.SelectMany(Source,static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Sequence_start_end_step() => Assert.Equal(
        e.Sequence(1,4,1),
        a.Sequence(1,4,1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SequenceEqual_second() => Assert.Equal(
        e.SequenceEqual(Source,Source),
        a.SequenceEqual(Source,Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Shuffle() {
        e.Shuffle(Source);
        a.Shuffle(Source);
    }
    [Fact, Trait("Category","Intercept")]
    private static void Single() => Assert.Equal(
        e.Single(SingleSource()),
        a.Single(SingleSource())
    );
    [Fact, Trait("Category","Intercept")]
    private static void Single_predicate() => Assert.Equal(
        e.Single(Source,static value => value==2),
        a.Single(Source,static value => value==2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SingleOrDefault() => Assert.Equal(
        e.SingleOrDefault(SingleSource()),
        a.SingleOrDefault(SingleSource())
    );
    [Fact, Trait("Category","Intercept")]
    private static void SingleOrDefault_predicate_defaultValue() => Assert.Equal(
        e.SingleOrDefault(Source,static value => value==9,-1),
        a.SingleOrDefault(Source,static value => value==9,-1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Skip_count() => Assert.Equal(
        e.Skip(Source,1),
        a.Skip(Source,1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SkipLast_count() => Assert.Equal(
        e.SkipLast(Source,1),
        a.SkipLast(Source,1)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SkipWhile_predicate() => Assert.Equal(
        e.SkipWhile(Source,static value => value<3),
        a.SkipWhile(Source,static value => value<3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void SkipWhile_predicate_index() => Assert.Equal(
        e.SkipWhile(Source,static (value,index) => value<3&&index<2),
        a.SkipWhile(Source,static (value,index) => value<3&&index<2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Sum_int() => Assert.Equal(
        e.Sum(Source),
        a.Sum(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Sum_selector() => Assert.Equal(
        e.Sum(Rows(),static row => row.Value),
        a.Sum(Rows(),static row => row.Value)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Take_count() => Assert.Equal(
        e.Take(Source,3),
        a.Take(Source,3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Take_range() => Assert.Equal(
        e.Take(Source,1..3),
        a.Take(Source,1..3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TakeLast_count() => Assert.Equal(
        e.TakeLast(Source,2),
        a.TakeLast(Source,2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TakeWhile_predicate() => Assert.Equal(
        e.TakeWhile(Source,static value => value<4),
        a.TakeWhile(Source,static value => value<4)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TakeWhile_predicate_index() => Assert.Equal(
        e.TakeWhile(Source,static (value,index) => value<4&&index<3),
        a.TakeWhile(Source,static (value,index) => value<4&&index<3)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ThenBy_keySelector() => Assert.Equal(
        e.ThenBy(a.OrderBy(Rows(),static row => row.Key),static row => row.Name),
        a.ThenBy(a.OrderBy(Rows(),static row => row.Key),static row => row.Name)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ThenByDescending_keySelector() => Assert.Equal(
        e.ThenByDescending(a.OrderBy(Rows(),static row => row.Key),static row => row.Name),
        a.ThenByDescending(a.OrderBy(Rows(),static row => row.Key),static row => row.Name)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToArray() => Assert.Equal(
        e.ToArray(Source),
        a.ToArray(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToDictionary_keySelector() => Assert.Equal(
        e.ToDictionary(Rows(),static row => row.Key),
        a.ToDictionary(Rows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToHashSet() => Assert.Equal(
        e.ToHashSet(Source),
        a.ToHashSet(Source)
    );
    [Fact, Trait("Category","Intercept")]
    private static void ToList() {
        var result=new List<int>();
        foreach(var item in Source){
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
        a.ToLookup(Rows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void TryGetNonEnumeratedCount_source() {
        var expected = e.TryGetNonEnumeratedCount(Source,out var expectedCount);
        var actual = a.TryGetNonEnumeratedCount(Source,out var actualCount);
        Assert.Equal(expected,actual);
        Assert.Equal(expectedCount,actualCount);
    }
    [Fact, Trait("Category","Intercept")]
    private static void Union_second() => Assert.Equal(
        e.Union(Source,Second),
        a.Union(Source,Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void UnionBy_second_keySelector() => Assert.Equal(
        e.UnionBy(Rows(),OtherRows(),static row => row.Key),
        a.UnionBy(Rows(),OtherRows(),static row => row.Key)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Where_predicate() => Assert.Equal(
        e.Where(Source,static value => value>2),
        a.Where(Source,static value => value>2)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Where_predicate_index() => Assert.Equal(
        e.Where(Source,static (value,index) => value>index),
        a.Where(Source,static (value,index) => value>index)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Zip_second() => Assert.Equal(
        e.Zip(Source,Second),
        a.Zip(Source,Second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Zip_second_resultSelector() => Assert.Equal(
        e.Zip(Source,Second,static (first,second) => first+second),
        a.Zip(Source,Second,static (first,second) => first+second)
    );
    [Fact, Trait("Category","Intercept")]
    private static void Zip_second_third() => Assert.Equal(
        e.Zip(Source,Second,Third),a.Zip(Source,Second,Third)
    );
    private static readonly int[] Source=[1,2,3,4];
    private static int[] Second=>[3,4,5];
    private static int[] Third=>[10,20,30];
    private static int[] Duplicates=>[1,2,2,3,3,4];
    private static int[] SingleSource() => [2];
    private static global::System.Collections.IEnumerable CastObjects() => new object[] { 1,2,3 };
    private static object[] MixedObjects() => [1,"skip",2,3.0,4];
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];
    private static Row[] OtherRows() => [new(3,300,"gamma-2"),new(4,40,"delta")];
    private sealed record Row(int Key,int Value,string Name);
}
