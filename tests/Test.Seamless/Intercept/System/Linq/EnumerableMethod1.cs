using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod1 {
    private readonly int instanceOffset = 1;

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_InstanceDelegate() {
        var x = 1;
        var expected=SystemEnumerable.Aggregate(Source(),(left,right) => left+right+x);
        var actual=Source().Aggregate((left,right) => left+right+x);
        Assert.Equal(expected,actual);
        AssertInterceptorGenerated();
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_func_StaticDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),static (left,right) => left+right);
        var actual=Source().Aggregate(static (left,right) => left+right);
        Assert.Equal(expected,actual);

        var generated = AssertInterceptorGenerated();
        Assert.Contains("public static global::System.Int32 func(global::System.Int32 left, global::System.Int32 right) => left+right;",generated);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_InstanceDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1);
        var actual=Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_StaticDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value);
        var actual=Source().Aggregate(10,static (sum,value) => sum+value);
        Assert.Equal(expected,actual);

        var generated = AssertInterceptorGenerated();
        Assert.Contains("public static global::System.Int32 func(global::System.Int32 sum, global::System.Int32 value) => sum+value;",generated);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_InstanceDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1);
        var actual=Source().Aggregate(10,(sum,value) => sum+value+this.instanceOffset-1,sum => sum*2+this.instanceOffset-1);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Aggregate_seed_func_resultSelector_StaticDelegate() {
        var expected=SystemEnumerable.Aggregate(Source(),10,static (sum,value) => sum+value,static sum => sum*2);
        var actual=Source().Aggregate(10,static (sum,value) => sum+value,static sum => sum*2);
        Assert.Equal(expected,actual);

        var generated = AssertInterceptorGenerated();
        Assert.Contains("public static global::System.Int32 resultSelector(global::System.Int32 sum) => sum*2;",generated);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate() {
        var expected=SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value);
        var actual=Source().AggregateBy(static value => value%2,0,static (sum,value) => sum+value);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void All_predicate_StaticDelegate() {
        var expected=SystemEnumerable.All(Source(),static value => value>0);
        var actual=Source().All(static value => value>0);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Any_NoDelegate() {
        var expected=SystemEnumerable.Any(Source());
        var actual=Source().Any();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Any_predicate_StaticDelegate() {
        var expected=SystemEnumerable.Any(Source(),static value => value>3);
        var actual=Source().Any(static value => value>3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Append_element_NoDelegate() {
        var expected=SystemEnumerable.Append(Source(),5);
        var actual=Source().Append(5);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable_NoDelegate() {
        var expected=SystemEnumerable.AsEnumerable(Source());
        var actual=Source().AsEnumerable();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Average_int_NoDelegate() {
        var expected=SystemEnumerable.Average(Source());
        var actual=Source().Average();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Average_selector_StaticDelegate() {
        var expected=SystemEnumerable.Average(Rows(),static row => row.Value);
        var actual=Rows().Average(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Cast_NoDelegate() {
        var expected=SystemEnumerable.Cast<int>(CastObjects());
        var actual=CastObjects().Cast<int>();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk_size_NoDelegate() {
        var expected=SystemEnumerable.Chunk(Source(),2);
        var actual=Source().Chunk(2);
        AssertNestedSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Concat_second_NoDelegate() {
        var expected=SystemEnumerable.Concat(Source(),Second());
        var actual=Source().Concat(Second());
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_value_NoDelegate() {
        var expected=SystemEnumerable.Contains(Source(),3);
        var actual=Source().Contains(3);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Count_predicate_StaticDelegate() {
        var expected=SystemEnumerable.Count(Source(),static value => value>2);
        var actual=Source().Count(static value => value>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct_NoDelegate() {
        var expected=SystemEnumerable.Distinct(Duplicates());
        var actual=Duplicates().Distinct();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate() {
        var expected=SystemEnumerable.DistinctBy(Rows(),static row => row.Key);
        var actual=Rows().DistinctBy(static row => row.Key);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void First_predicate_StaticDelegate() {
        var expected=SystemEnumerable.First(Source(),static value => value>2);
        var actual=Source().First(static value => value>2);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate() {
        var expected=SystemEnumerable.GroupBy(Rows(),static row => row.Key);
        var actual=Rows().GroupBy(static row => row.Key);
        AssertGroupSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate() {
        var expected=SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue);
        var actual=Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate() {
        var expected=SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actual=Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Max_selector_StaticDelegate() {
        var expected=SystemEnumerable.Max(Rows(),static row => row.Value);
        var actual=Rows().Max(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate() {
        var expected=SystemEnumerable.OrderBy(Rows(),static row => row.Key);
        var actual=Rows().OrderBy(static row => row.Key);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate() {
        var expected=SystemEnumerable.Select(Source(),static value => value*2);
        var actual=Source().Select(static value => value*2);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate() {
        var expected=SystemEnumerable.Select(Source(),this.InstanceSelector);
        var actual=Source().Select(this.InstanceSelector);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate() {
        var expected=SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 });
        var actual=Source().SelectMany(static value => new[] { value,value+10 });
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate() {
        var expected=SystemEnumerable.SkipWhile(Source(),static value => value<3);
        var actual=Source().SkipWhile(static value => value<3);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Sum_selector_StaticDelegate() {
        var expected=SystemEnumerable.Sum(Rows(),static row => row.Value);
        var actual=Rows().Sum(static row => row.Value);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Take_count_NoDelegate() {
        var expected=SystemEnumerable.Take(Source(),3);
        var actual=Source().Take(3);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ToArray_NoDelegate() {
        var expected=SystemEnumerable.ToArray(Source());
        var actual=Source().ToArray();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ToList_NoDelegate() {
        var expected=SystemEnumerable.ToList(Source());
        var actual=Source().ToList();
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_StaticDelegate() {
        var expected=SystemEnumerable.Where(Source(),static value => value>2);
        var actual=Source().Where(static value => value>2);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate() {
        var expected=SystemEnumerable.Where(Source(),this.InstancePredicate);
        var actual=Source().Where(this.InstancePredicate);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate() {
        var expected=SystemEnumerable.Zip(Source(),Second(),static (first,second) => first+second);
        var actual=Source().Zip(Second(),static (first,second) => first+second);
        AssertSequenceEqual(expected,actual);
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

    private static string AssertInterceptorGenerated([global::System.Runtime.CompilerServices.CallerMemberName] string methodName = "") {
        var generated = GeneratedInterceptorAssert.SourceFor<EnumerableMethod1>(methodName);
        Assert.Contains(methodName,generated);
        return generated;
    }
}
