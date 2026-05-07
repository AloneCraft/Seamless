using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableQuery1 {
    private readonly int instanceOffset = 1;

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate() {
        var x = 1;
        Assert.Equal(
            SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x),
            (from value in Source() select value).Aggregate((left,right) => left+right+x)
        );
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Aggregate(Source(),static (left,right) => left+right),
        (from value in Source() select value).Aggregate(static (left,right) => left+right)
    );

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_InstanceDelegate() => Assert.Equal(
        SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1),
        (from value in Source() select value).Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1)
    );

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value),
        (from value in Source() select value).Aggregate(10,static (sum,value) => sum+value)
    );

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_InstanceDelegate() => Assert.Equal(
        SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1),
        (from value in Source() select value).Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1)
    );

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value,static sum => sum*2),
        (from value in Source() select value).Aggregate(10,static (sum,value) => sum+value,static sum => sum*2)
    );

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value),
        (from value in Source() select value).AggregateBy(static value => value%2,0,static (sum,value) => sum+value)
    );

    [Fact, Trait("Category","Unit")]
    public void All_predicate_StaticDelegate() => Assert.Equal(
        SystemEnumerable.All(Source(),static value => value>0),
        (from value in Source() select value).All(static value => value>0)
    );

    [Fact, Trait("Category","Unit")]
    public void Any_NoDelegate() => Assert.Equal(
        SystemEnumerable.Any(Source()),
        (from value in Source() select value).Any()
    );

    [Fact, Trait("Category","Unit")]
    public void Any_predicate_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Any(Source(),static value => value>3),
        (from value in Source() select value).Any(static value => value>3)
    );

    [Fact, Trait("Category","Unit")]
    public void Append_element_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.Append(Source(),5),
        (from value in Source() select value).Append(5)
    );

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.AsEnumerable(Source()),
        (from value in Source() select value).AsEnumerable()
    );

    [Fact, Trait("Category","Unit")]
    public void Average_int_NoDelegate() => Assert.Equal(
        SystemEnumerable.Average(Source()),
        (from value in Source() select value).Average()
    );

    [Fact, Trait("Category","Unit")]
    public void Average_selector_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Average(Rows(),static row => row.Value),
        (from row in Rows() select row).Average(static row => row.Value)
    );

    [Fact, Trait("Category","Unit")]
    public void Cast_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.Cast<int>(CastObjects()),
        (from value in CastObjects().Cast<object>() select value).Cast<int>()
    );

    [Fact, Trait("Category","Unit")]
    public void Chunk_size_NoDelegate() => AssertNestedSequenceEqual(
        SystemEnumerable.Chunk(Source(),2),
        (from value in Source() select value).Chunk(2)
    );

    [Fact, Trait("Category","Unit")]
    public void Concat_second_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.Concat(Source(),Second()),
        (from value in Source() select value).Concat(Second())
    );

    [Fact, Trait("Category","Unit")]
    public void Contains_value_NoDelegate() => Assert.Equal(
        SystemEnumerable.Contains(Source(),3),
        (from value in Source() select value).Contains(3)
    );

    [Fact, Trait("Category","Unit")]
    public void Count_predicate_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Count(Source(),static value => value>2),
        (from value in Source() select value).Count(static value => value>2)
    );

    [Fact, Trait("Category","Unit")]
    public void Distinct_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.Distinct(Duplicates()),
        (from value in Duplicates() select value).Distinct()
    );

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.DistinctBy(Rows(),static row => row.Key),
        (from row in Rows() select row).DistinctBy(static row => row.Key)
    );

    [Fact, Trait("Category","Unit")]
    public void First_predicate_StaticDelegate() => Assert.Equal(
        SystemEnumerable.First(Source(),static value => value>2),
        (from value in Source() select value).First(static value => value>2)
    );

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate() => AssertGroupSequenceEqual(
        SystemEnumerable.GroupBy(Rows(),static row => row.Key),
        from row in Rows() group row by row.Key
    );

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
        from outerValue in Source() join innerValue in Second() on outerValue equals innerValue select outerValue+innerValue
    );

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate() => AssertSequenceEqual(
        SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector),
        from outerValue in Source() join innerValue in Second() on this.InstanceKeySelector(outerValue) equals this.InstanceKeySelector(innerValue) select this.InstanceResultSelector(outerValue,innerValue)
    );

    [Fact, Trait("Category","Unit")]
    public void Max_selector_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Max(Rows(),static row => row.Value),
        (from row in Rows() select row).Max(static row => row.Value)
    );

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.OrderBy(Rows(),static row => row.Key),
        from row in Rows() orderby row.Key select row
    );

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.Select(Source(),static value => value*2),
        from value in Source() select value*2
    );

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate() => AssertSequenceEqual(
        SystemEnumerable.Select(Source(),this.InstanceSelector),
        from value in Source() select this.InstanceSelector(value)
    );

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 }),
        from value in Source() from selected in new[] { value,value+10 } select selected
    );

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.SkipWhile(Source(),static value => value<3),
        (from value in Source() select value).SkipWhile(static value => value<3)
    );

    [Fact, Trait("Category","Unit")]
    public void Sum_selector_StaticDelegate() => Assert.Equal(
        SystemEnumerable.Sum(Rows(),static row => row.Value),
        (from row in Rows() select row).Sum(static row => row.Value)
    );

    [Fact, Trait("Category","Unit")]
    public void Take_count_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.Take(Source(),3),
        (from value in Source() select value).Take(3)
    );

    [Fact, Trait("Category","Unit")]
    public void ToArray_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.ToArray(Source()),
        (from value in Source() select value).ToArray()
    );

    [Fact, Trait("Category","Unit")]
    public void ToList_NoDelegate() => AssertSequenceEqual(
        SystemEnumerable.ToList(Source()),
        (from value in Source() select value).ToList()
    );

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.Where(Source(),static value => value>2),
        from value in Source() where value>2 select value
    );

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate() => AssertSequenceEqual(
        SystemEnumerable.Where(Source(),this.InstancePredicate),
        from value in Source() where this.InstancePredicate(value) select value
    );

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate() => AssertSequenceEqual(
        SystemEnumerable.Zip(Source(),Second(),static (first,second) => first+second),
        (from value in Source() select value).Zip(Second(),static (first,second) => first+second)
    );

    private bool InstancePredicate(int value) => value>this.instanceOffset;
    private int InstanceSelector(int value) => value*this.instanceOffset;
    private int InstanceKeySelector(int value) => value;
    private int InstanceResultSelector(int outerValue,int innerValue) => outerValue+innerValue+this.instanceOffset-1;

    public static int[] Source() => [1,2,3,4];
    public static int[] Second() => [3,4,5];
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
