using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableQuery1 {
    private readonly int instanceOffset = 1;

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate1() {
        Aggregate_func_InstanceDelegate1Impl();
    }
    public void Aggregate_func_InstanceDelegate1Impl() {
        var x = 1;
        var expected=SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x);
        var actual=Source().Aggregate((left,right) => left+right+x);
        Assert.Equal(expected,actual);
    }
    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate() {
        var x = 1;
        var expected=SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x);
        var actual=(from value in Source() select value).Aggregate((left,right) => left+right+x);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_StaticDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),static (left,right) => left+right);
        var actual=(from value in Source() select value).Aggregate(static (left,right) => left+right);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_InstanceDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1);
        var actual=(from value in Source() select value).Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_StaticDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value);
        var actual=(from value in Source() select value).Aggregate(10,static (sum,value) => sum+value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_InstanceDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1);
        var actual=(from value in Source() select value).Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_StaticDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value,static sum => sum*2);
        var actual=(from value in Source() select value).Aggregate(10,static (sum,value) => sum+value,static sum => sum*2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate() {
        var expected=SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value).ToList();
        var actual=(from value in Source() select value).AggregateBy(static value => value%2,0,static (sum,value) => sum+value).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void All_predicate_StaticDelegate() {
        var expected=SystemEnumerable.All(Source(),static value => value>0);
        var actual=(from value in Source() select value).All(static value => value>0);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Any_NoDelegate() {
        var expected=SystemEnumerable.Any(Source());
        var actual=(from value in Source() select value).Any();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Any_predicate_StaticDelegate() {
        var expected=SystemEnumerable.Any(Source(),static value => value>3);
        var actual=(from value in Source() select value).Any(static value => value>3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Append_element_NoDelegate() {
        var expected=SystemEnumerable.Append(Source(),5).ToList();
        var actual=(from value in Source() select value).Append(5).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable_NoDelegate() {
        var expected=SystemEnumerable.AsEnumerable(Source()).ToList();
        var actual=(from value in Source() select value).AsEnumerable().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Average_int_NoDelegate() {
        var expected=SystemEnumerable.Average(Source());
        var actual=(from value in Source() select value).Average();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Average_selector_StaticDelegate() {
        var expected=SystemEnumerable.Average(Rows(),static row => row.Value);
        var actual=(from row in Rows() select row).Average(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Cast_NoDelegate() {
        var expected=SystemEnumerable.Cast<int>(CastObjects()).ToList();
        var actual=(from value in CastObjects().Cast<object>() select value).Cast<int>().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk_size_NoDelegate() {
        var expected=SystemEnumerable.Chunk(Source(),2).ToList();
        var actual=(from value in Source() select value).Chunk(2).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Concat_second_NoDelegate() {
        var expected=SystemEnumerable.Concat(Source(),Second()).ToList();
        var actual=(from value in Source() select value).Concat(Second()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_value_NoDelegate() {
        var expected=SystemEnumerable.Contains(Source(),3);
        var actual=(from value in Source() select value).Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Count_predicate_StaticDelegate() {
        var expected=SystemEnumerable.Count(Source(),static value => value>2);
        var actual=(from value in Source() select value).Count(static value => value>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct_NoDelegate() {
        var expected=SystemEnumerable.Distinct(Duplicates()).ToList();
        var actual=(from value in Duplicates() select value).Distinct().ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate() {
        var expected=SystemEnumerable.DistinctBy(Rows(),static row => row.Key).ToList();
        var actual=(from row in Rows() select row).DistinctBy(static row => row.Key).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void First_predicate_StaticDelegate() {
        var expected=SystemEnumerable.First(Source(),static value => value>2);
        var actual=(from value in Source() select value).First(static value => value>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key).ToList();
        var actual=from row in Rows() group row by row.Key;
        AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate() {
        var expected=SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue).ToList();
        var actual=(from outerValue in Source() join innerValue in Second() on outerValue equals innerValue select outerValue+innerValue).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate() {
        var expected=SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector).ToList();
        var actual=(from outerValue in Source() join innerValue in Second() on this.InstanceKeySelector(outerValue) equals this.InstanceKeySelector(innerValue) select this.InstanceResultSelector(outerValue,innerValue)).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Max_selector_StaticDelegate() {
        var expected=SystemEnumerable.Max(Rows(),static row => row.Value);
        var actual=(from row in Rows() select row).Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate() {
        var expected=SystemEnumerable.OrderBy(Rows(),static row => row.Key).ToList();
        var actual=(from row in Rows() orderby row.Key select row).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate() {
        var expected=SystemEnumerable.Select(Source(),static value => value*2).ToList();
        var actual=(from value in Source() select value*2).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate() {
        var expected=SystemEnumerable.Select(Source(),this.InstanceSelector).ToList();
        var actual=(from value in Source() select this.InstanceSelector(value)).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate() {
        var expected=SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 }).ToList();
        var actual=(from value in Source() from selected in new[] { value,value+10 } select selected).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate() {
        var expected=SystemEnumerable.SkipWhile(Source(),static value => value<3).ToList();
        var actual=(from value in Source() select value).SkipWhile(static value => value<3).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Sum_selector_StaticDelegate() {
        var expected=SystemEnumerable.Sum(Rows(),static row => row.Value);
        var actual=(from row in Rows() select row).Sum(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Take_count_NoDelegate() {
        var expected=SystemEnumerable.Take(Source(),3).ToList();
        var actual=(from value in Source() select value).Take(3).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray_NoDelegate() {
        var expected=SystemEnumerable.ToArray(Source());
        var actual=(from value in Source() select value).ToArray();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ToList_NoDelegate() {
        var expected=SystemEnumerable.ToList(Source());
        var actual=(from value in Source() select value).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_StaticDelegate() {
        var expected=SystemEnumerable.Where(Source(),static value => value>2).ToList();
        var actual=(from value in Source() where value>2 select value).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate() {
        var expected=SystemEnumerable.Where(Source(),this.InstancePredicate).ToList();
        var actual=(from value in Source() where this.InstancePredicate(value) select value).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate() {
        var expected=SystemEnumerable.Zip(Source(),Second(),static (first,second) => first+second).ToList();
        var actual=(from value in Source() select value).Zip(Second(),static (first,second) => first+second).ToList();
        Assert.Equal(expected,actual);
    }

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
        Assert.Equal(expected,actual);
    }

    private static void AssertNestedSequenceEqual<T>(IEnumerable<IEnumerable<T>> expected,IEnumerable<IEnumerable<T>> actual) {
        using var expectedEnumerator = expected.GetEnumerator();
        using var actualEnumerator = actual.GetEnumerator();
        while(true) {
            var hasExpected = expectedEnumerator.MoveNext();
            var hasActual = actualEnumerator.MoveNext();
            Assert.Equal(hasExpected,hasActual);
            if(!hasExpected) {
                return;
            }

            Assert.Equal(expectedEnumerator.Current,actualEnumerator.Current);
        }
    }

    private static void AssertGroupSequenceEqual<TKey,TElement>(IEnumerable<IGrouping<TKey,TElement>> expected,IEnumerable<IGrouping<TKey,TElement>> actual) {
        using var expectedEnumerator = expected.GetEnumerator();
        using var actualEnumerator = actual.GetEnumerator();
        while(true) {
            var hasExpected = expectedEnumerator.MoveNext();
            var hasActual = actualEnumerator.MoveNext();
            Assert.Equal(hasExpected,hasActual);
            if(!hasExpected) {
                return;
            }

            Assert.Equal(expectedEnumerator.Current.Key,actualEnumerator.Current.Key);
            Assert.Equal(expectedEnumerator.Current,actualEnumerator.Current);
        }
    }

}
