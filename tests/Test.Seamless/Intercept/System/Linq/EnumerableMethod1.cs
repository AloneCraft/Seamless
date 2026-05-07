using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod1 {
    private readonly int instanceOffset = 1;

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate() {
        var x = 1;
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x),
            Source().Aggregate((left,right) => left+right+x)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),static (left,right) => left+right),
            Source().Aggregate(static (left,right) => left+right)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_InstanceDelegate() {
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1),
            Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value),
            Source().Aggregate(10,static (sum,value) => sum+value)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_InstanceDelegate() {
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1),
            Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value,static sum => sum*2),
            Source().Aggregate(10,static (sum,value) => sum+value,static sum => sum*2)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value),
            Source().AggregateBy(static value => value%2,0,static (sum,value) => sum+value));
    }

    [Fact, Trait("Category","Unit")]
    public void All_predicate_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.All(Source(),static value => value>0),
            Source().All(static value => value>0)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Any_NoDelegate() {
        Assert.Equal(
            SystemEnumerable.Any(Source()),
            Source().Any()
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Any_predicate_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Any(Source(),static value => value>3),
            Source().Any(static value => value>3)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Append_element_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Append(Source(),5),
            Source().Append(5));
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.AsEnumerable(Source()),
            Source().AsEnumerable());
    }

    [Fact, Trait("Category","Unit")]
    public void Average_int_NoDelegate() {
        Assert.Equal(
            SystemEnumerable.Average(Source()),
            Source().Average()
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Average_selector_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Average(Rows(),static row => row.Value),
            Rows().Average(static row => row.Value)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Cast_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Cast<int>(CastObjects()),
            CastObjects().Cast<int>());
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk_size_NoDelegate() {
        AssertNestedSequenceEqual(
            SystemEnumerable.Chunk(Source(),2),
            Source().Chunk(2));
    }

    [Fact, Trait("Category","Unit")]
    public void Concat_second_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Concat(Source(),Second()),
            Source().Concat(Second()));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_value_NoDelegate() {
        Assert.Equal(
            SystemEnumerable.Contains(Source(),3),
            Source().Contains(3)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Count_predicate_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Count(Source(),static value => value>2),
            Source().Count(static value => value>2)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Distinct(Duplicates()),
            Duplicates().Distinct());
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.DistinctBy(Rows(),static row => row.Key),
            Rows().DistinctBy(static row => row.Key));
    }

    [Fact, Trait("Category","Unit")]
    public void First_predicate_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.First(Source(),static value => value>2),
            Source().First(static value => value>2)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate() {
        AssertGroupSequenceEqual(
            SystemEnumerable.GroupBy(Rows(),static row => row.Key),
            Rows().GroupBy(static row => row.Key));
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
            Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector),
            Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void Max_selector_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Max(Rows(),static row => row.Value),
            Rows().Max(static row => row.Value)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.OrderBy(Rows(),static row => row.Key),
            Rows().OrderBy(static row => row.Key));
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Select(Source(),static value => value*2),
            Source().Select(static value => value*2));
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Select(Source(),this.InstanceSelector),
            Source().Select(this.InstanceSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 }),
            Source().SelectMany(static value => new[] { value,value+10 }));
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.SkipWhile(Source(),static value => value<3),
            Source().SkipWhile(static value => value<3));
    }

    [Fact, Trait("Category","Unit")]
    public void Sum_selector_StaticDelegate() {
        Assert.Equal(
            SystemEnumerable.Sum(Rows(),static row => row.Value),
            Rows().Sum(static row => row.Value)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Take_count_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Take(Source(),3),
            Source().Take(3));
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.ToArray(Source()),
            Source().ToArray());
    }

    [Fact, Trait("Category","Unit")]
    public void ToList_NoDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.ToList(Source()),
            Source().ToList());
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Where(Source(),static value => value>2),
            Source().Where(static value => value>2));
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Where(Source(),this.InstancePredicate),
            Source().Where(this.InstancePredicate));
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate() {
        AssertSequenceEqual(
            SystemEnumerable.Zip(Source(),Second(),static (first,second) => first+second),
            Source().Zip(Second(),static (first,second) => first+second));
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
        Assert.Equal(
            SystemEnumerable.ToArray(actual),
            SystemEnumerable.ToArray(expected)
        );
    }

    private static void AssertNestedSequenceEqual<T>(IEnumerable<IEnumerable<T>> expected,IEnumerable<IEnumerable<T>> actual) {
        Assert.Equal(
            SystemEnumerable.Select(actual,static source => SystemEnumerable.ToArray(source)),
            SystemEnumerable.Select(expected,static source => SystemEnumerable.ToArray(source))
        );
    }

    private static void AssertGroupSequenceEqual<TKey,TElement>(IEnumerable<IGrouping<TKey,TElement>> expected,IEnumerable<IGrouping<TKey,TElement>> actual) {
        Assert.Equal(
            SystemEnumerable.Select(actual,static group => (group.Key,Values:SystemEnumerable.ToArray(group))),
            SystemEnumerable.Select(expected,static group => (group.Key,Values:SystemEnumerable.ToArray(group)))
        );
    }
}
