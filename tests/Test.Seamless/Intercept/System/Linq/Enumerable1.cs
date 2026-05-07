using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class Enumerable1 {
    private readonly int instanceOffset = 1;

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate() {
        var x = 1;
        Assert.Equal(
            Source().Aggregate((left,right) => left+right+x),
            SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x));
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_StaticDelegate() {
        Assert.Equal(
            Source().Aggregate(static (left,right) => left+right),
            SystemEnumerable.Aggregate(Source(),static (left,right) => left+right));
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_InstanceDelegate() {
        Assert.Equal(
            Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1),
            SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1));
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_StaticDelegate() {
        Assert.Equal(
            Source().Aggregate(10,static (sum,value) => sum+value),
            SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value));
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_InstanceDelegate() {
        Assert.Equal(
            Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1),
            SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1));
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_StaticDelegate() {
        Assert.Equal(
            Source().Aggregate(10,static (sum,value) => sum+value,static sum => sum*2),
            SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value,static sum => sum*2));
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate() {
        AssertSequenceEqual(
            Source().AggregateBy(static value => value%2,0,static (sum,value) => sum+value),
            SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value));
    }

    [Fact, Trait("Category","Unit")]
    public void All_predicate_StaticDelegate() {
        Assert.Equal(
            Source().All(static value => value>0),
            SystemEnumerable.All(Source(),static value => value>0));
    }

    [Fact, Trait("Category","Unit")]
    public void Any_NoDelegate() {
        Assert.Equal(
            Source().Any(),
            SystemEnumerable.Any(Source()));
    }

    [Fact, Trait("Category","Unit")]
    public void Any_predicate_StaticDelegate() {
        Assert.Equal(
            Source().Any(static value => value>3),
            SystemEnumerable.Any(Source(),static value => value>3));
    }

    [Fact, Trait("Category","Unit")]
    public void Append_element_NoDelegate() {
        AssertSequenceEqual(
            Source().Append(5),
            SystemEnumerable.Append(Source(),5));
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable_NoDelegate() {
        AssertSequenceEqual(
            Source().AsEnumerable(),
            SystemEnumerable.AsEnumerable(Source()));
    }

    [Fact, Trait("Category","Unit")]
    public void Average_int_NoDelegate() {
        Assert.Equal(
            Source().Average(),
            SystemEnumerable.Average(Source()));
    }

    [Fact, Trait("Category","Unit")]
    public void Average_selector_StaticDelegate() {
        Assert.Equal(
            Rows().Average(static row => row.Value),
            SystemEnumerable.Average(Rows(),static row => row.Value));
    }

    [Fact, Trait("Category","Unit")]
    public void Cast_NoDelegate() {
        AssertSequenceEqual(
            CastObjects().Cast<int>(),
            SystemEnumerable.Cast<int>(CastObjects()));
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk_size_NoDelegate() {
        AssertNestedSequenceEqual(
            Source().Chunk(2),
            SystemEnumerable.Chunk(Source(),2));
    }

    [Fact, Trait("Category","Unit")]
    public void Concat_second_NoDelegate() {
        AssertSequenceEqual(
            Source().Concat(Second()),
            SystemEnumerable.Concat(Source(),Second()));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_value_NoDelegate() {
        Assert.Equal(
            Source().Contains(3),
            SystemEnumerable.Contains(Source(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void Count_predicate_StaticDelegate() {
        Assert.Equal(
            Source().Count(static value => value>2),
            SystemEnumerable.Count(Source(),static value => value>2));
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct_NoDelegate() {
        AssertSequenceEqual(
            Duplicates().Distinct(),
            SystemEnumerable.Distinct(Duplicates()));
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate() {
        AssertSequenceEqual(
            Rows().DistinctBy(static row => row.Key),
            SystemEnumerable.DistinctBy(Rows(),static row => row.Key));
    }

    [Fact, Trait("Category","Unit")]
    public void First_predicate_StaticDelegate() {
        Assert.Equal(
            Source().First(static value => value>2),
            SystemEnumerable.First(Source(),static value => value>2));
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate() {
        AssertGroupSequenceEqual(
            Rows().GroupBy(static row => row.Key),
            SystemEnumerable.GroupBy(Rows(),static row => row.Key));
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate() {
        AssertSequenceEqual(
            Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
            SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate() {
        AssertSequenceEqual(
            Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector),
            SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void Max_selector_StaticDelegate() {
        Assert.Equal(
            Rows().Max(static row => row.Value),
            SystemEnumerable.Max(Rows(),static row => row.Value));
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate() {
        AssertSequenceEqual(
            Rows().OrderBy(static row => row.Key),
            SystemEnumerable.OrderBy(Rows(),static row => row.Key));
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate() {
        AssertSequenceEqual(
            Source().Select(static value => value*2),
            SystemEnumerable.Select(Source(),static value => value*2));
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate() {
        AssertSequenceEqual(
            Source().Select(this.InstanceSelector),
            SystemEnumerable.Select(Source(),this.InstanceSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate() {
        AssertSequenceEqual(
            Source().SelectMany(static value => new[] { value,value+10 }),
            SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 }));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate() {
        AssertSequenceEqual(
            Source().SkipWhile(static value => value<3),
            SystemEnumerable.SkipWhile(Source(),static value => value<3));
    }

    [Fact, Trait("Category","Unit")]
    public void Sum_selector_StaticDelegate() {
        Assert.Equal(
            Rows().Sum(static row => row.Value),
            SystemEnumerable.Sum(Rows(),static row => row.Value));
    }

    [Fact, Trait("Category","Unit")]
    public void Take_count_NoDelegate() {
        AssertSequenceEqual(
            Source().Take(3),
            SystemEnumerable.Take(Source(),3));
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray_NoDelegate() {
        AssertSequenceEqual(
            Source().ToArray(),
            SystemEnumerable.ToArray(Source()));
    }

    [Fact, Trait("Category","Unit")]
    public void ToList_NoDelegate() {
        AssertSequenceEqual(
            Source().ToList(),
            SystemEnumerable.ToList(Source()));
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_StaticDelegate() {
        AssertSequenceEqual(
            Source().Where(static value => value>2),
            SystemEnumerable.Where(Source(),static value => value>2));
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate() {
        AssertSequenceEqual(
            Source().Where(this.InstancePredicate),
            SystemEnumerable.Where(Source(),this.InstancePredicate));
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate() {
        AssertSequenceEqual(
            Source().Zip(Second(),static (first,second) => first+second),
            SystemEnumerable.Zip(Source(),Second(),static (first,second) => first+second));
    }

    private bool InstancePredicate(int value) => value>this.instanceOffset;
    private int InstanceSelector(int value) => value*this.instanceOffset;
    private int InstanceKeySelector(int value) => value;
    private int InstanceResultSelector(int outerValue,int innerValue) => outerValue+innerValue+this.instanceOffset-1;

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
    public static int[] Third() => [10,20,30];
    public static int[] Duplicates() => [1,2,2,3,3,4];
    public static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    public static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    public sealed record Row(int Key,int Value,string Name);

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        Assert.Equal(SystemEnumerable.ToArray(expected),SystemEnumerable.ToArray(actual));
    }

    private static void AssertNestedSequenceEqual<T>(IEnumerable<IEnumerable<T>> expected,IEnumerable<IEnumerable<T>> actual) {
        Assert.Equal(
            SystemEnumerable.Select(expected,static source => SystemEnumerable.ToArray(source)),
            SystemEnumerable.Select(actual,static source => SystemEnumerable.ToArray(source)));
    }

    private static void AssertGroupSequenceEqual<TKey,TElement>(IEnumerable<IGrouping<TKey,TElement>> expected,IEnumerable<IGrouping<TKey,TElement>> actual) {
        Assert.Equal(
            SystemEnumerable.Select(expected,static group => (group.Key,Values:SystemEnumerable.ToArray(group))),
            SystemEnumerable.Select(actual,static group => (group.Key,Values:SystemEnumerable.ToArray(group))));
    }
}
