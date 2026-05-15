using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod2 {
    private readonly int instanceOffset = 1;

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_InstanceDelegate__Aggregate_func_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),this.InstanceKeySelector,0,this.InstanceAccumulator));
        var actualSource = Normalize(Source().AggregateBy(this.InstanceKeySelector,0,this.InstanceAccumulator));
        var expected=Snapshot(SystemEnumerable.Aggregate(expectedSource,this.InstanceAggregateFunc));
        var actual=Snapshot(actualSource.Aggregate(this.InstanceAggregateFunc));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate__Aggregate_func_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value));
        var actualSource = Normalize(Source().AggregateBy(static value => value%2,0,static (sum,value) => sum+value));
        var expected=Snapshot(SystemEnumerable.Aggregate(expectedSource,static (left,right) => left+right));
        var actual=Snapshot(actualSource.Aggregate(static (left,right) => left+right));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seedSelector_func_InstanceDelegate__Aggregate_seed_func_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),this.InstanceKeySelector,this.InstanceSeedSelector,this.InstanceAccumulator));
        var actualSource = Normalize(Source().AggregateBy(this.InstanceKeySelector,this.InstanceSeedSelector,this.InstanceAccumulator));
        var expected=Snapshot(SystemEnumerable.Aggregate(expectedSource,10,this.InstanceAccumulator));
        var actual=Snapshot(actualSource.Aggregate(10,this.InstanceAccumulator));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seedSelector_func_StaticDelegate__Aggregate_seed_func_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),static value => value%2,static _ => 1,static (sum,value) => sum+value));
        var actualSource = Normalize(Source().AggregateBy(static value => value%2,static _ => 1,static (sum,value) => sum+value));
        var expected=Snapshot(SystemEnumerable.Aggregate(expectedSource,10,static (sum,value) => sum+value));
        var actual=Snapshot(actualSource.Aggregate(10,static (sum,value) => sum+value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Append_element__Aggregate_seed_func_resultSelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Append(Source(),5);
        var actualSource = Source().Append(5);
        var expected=Snapshot(SystemEnumerable.Aggregate(expectedSource,10,this.InstanceAccumulator,this.InstanceResultValueSelector));
        var actual=Snapshot(actualSource.Aggregate(10,this.InstanceAccumulator,this.InstanceResultValueSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable__Aggregate_seed_func_resultSelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.AsEnumerable(Source());
        var actualSource = Source().AsEnumerable();
        var expected=Snapshot(SystemEnumerable.Aggregate(expectedSource,10,static (sum,value) => sum+value,static sum => sum*2));
        var actual=Snapshot(actualSource.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Cast__AggregateBy_keySelector_seed_func_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Cast<int>(SystemEnumerable.Select(Source(),static value => (object)value));
        var actualSource = SystemEnumerable.Select(Source(),static value => (object)value).Cast<int>();
        var expected=Snapshot(SystemEnumerable.AggregateBy(expectedSource,this.InstanceModuloKeySelector,0,this.InstanceAccumulator));
        var actual=Snapshot(actualSource.AggregateBy(this.InstanceModuloKeySelector,0,this.InstanceAccumulator));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk_size__AggregateBy_keySelector_seed_func_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.Chunk(Source(),2));
        var actualSource = Normalize(Source().Chunk(2));
        var expected=Snapshot(SystemEnumerable.AggregateBy(expectedSource,static value => value%2,0,static (sum,value) => sum+value));
        var actual=Snapshot(actualSource.AggregateBy(static value => value%2,0,static (sum,value) => sum+value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Concat_second__AggregateBy_keySelector_seedSelector_func_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Concat(Source(),Second());
        var actualSource = Source().Concat(Second());
        var expected=Snapshot(SystemEnumerable.AggregateBy(expectedSource,this.InstanceModuloKeySelector,this.InstanceSeedSelector,this.InstanceAccumulator));
        var actual=Snapshot(actualSource.AggregateBy(this.InstanceModuloKeySelector,this.InstanceSeedSelector,this.InstanceAccumulator));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy_keySelector_InstanceDelegate__AggregateBy_keySelector_seedSelector_func_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.CountBy(Source(),this.InstanceKeySelector));
        var actualSource = Normalize(Source().CountBy(this.InstanceKeySelector));
        var expected=Snapshot(SystemEnumerable.AggregateBy(expectedSource,static value => value%2,static _ => 1,static (sum,value) => sum+value));
        var actual=Snapshot(actualSource.AggregateBy(static value => value%2,static _ => 1,static (sum,value) => sum+value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy_keySelector_StaticDelegate__All_predicate_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.CountBy(Source(),static value => value%2));
        var actualSource = Normalize(Source().CountBy(static value => value%2));
        var expected=Snapshot(SystemEnumerable.All(expectedSource,this.InstancePositivePredicate));
        var actual=Snapshot(actualSource.All(this.InstancePositivePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty__All_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.DefaultIfEmpty(Source());
        var actualSource = Source().DefaultIfEmpty();
        var expected=Snapshot(SystemEnumerable.All(expectedSource,static value => value>0));
        var actual=Snapshot(actualSource.All(static value => value>0));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty_defaultValue__Any() {
        var expectedSource = SystemEnumerable.DefaultIfEmpty(Source(),9);
        var actualSource = Source().DefaultIfEmpty(9);
        var expected=Snapshot(SystemEnumerable.Any(expectedSource));
        var actual=Snapshot(actualSource.Any());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct__Any_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Distinct(Source());
        var actualSource = Source().Distinct();
        var expected=Snapshot(SystemEnumerable.Any(expectedSource,this.InstancePredicate));
        var actual=Snapshot(actualSource.Any(this.InstancePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct_comparer__Any_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.Distinct(Source(),EqualityComparer<int>.Default);
        var actualSource = Source().Distinct(EqualityComparer<int>.Default);
        var expected=Snapshot(SystemEnumerable.Any(expectedSource,static value => value>2));
        var actual=Snapshot(actualSource.Any(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_InstanceDelegate__Append_element() {
        var expectedSource = SystemEnumerable.DistinctBy(Source(),this.InstanceKeySelector);
        var actualSource = Source().DistinctBy(this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.Append(expectedSource,5));
        var actual=Snapshot(actualSource.Append(5));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate__AsEnumerable() {
        var expectedSource = SystemEnumerable.DistinctBy(Source(),static value => value%2);
        var actualSource = Source().DistinctBy(static value => value%2);
        var expected=Snapshot(SystemEnumerable.AsEnumerable(expectedSource));
        var actual=Snapshot(actualSource.AsEnumerable());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Empty__Average_int() {
        var expectedSource = SystemEnumerable.Empty<int>();
        var actualSource = SystemEnumerable.Empty<int>();
        var expected=Snapshot(SystemEnumerable.Average(EnsureNonEmpty(expectedSource)));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Average());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Except_second__Average_selector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Except(Source(),Second());
        var actualSource = Source().Except(Second());
        var expected=Snapshot(SystemEnumerable.Average(expectedSource,this.InstanceKeySelector));
        var actual=Snapshot(actualSource.Average(this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy_second_keySelector_InstanceDelegate__Average_selector_StaticDelegate() {
        var expectedSource = SystemEnumerable.ExceptBy(Source(),new[] { 2 },this.InstanceKeySelector);
        var actualSource = Source().ExceptBy(new[] { 2 },this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.Average(expectedSource,static value => value));
        var actual=Snapshot(actualSource.Average(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy_second_keySelector_StaticDelegate__Cast() {
        var expectedSource = SystemEnumerable.ExceptBy(Source(),new[] { 2 },static value => value%2);
        var actualSource = Source().ExceptBy(new[] { 2 },static value => value%2);
        var expected=Snapshot(SystemEnumerable.Cast<int>(SystemEnumerable.Select(expectedSource,static value => (object)value)));
        var actual=Snapshot(SystemEnumerable.Select(actualSource,static value => (object)value).Cast<int>());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_InstanceDelegate__Chunk_size() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),this.InstanceKeySelector));
        var actualSource = Normalize(Source().GroupBy(this.InstanceKeySelector));
        var expected=Snapshot(SystemEnumerable.Chunk(expectedSource,2));
        var actual=Snapshot(actualSource.Chunk(2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate__Concat_second() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),static value => value%2));
        var actualSource = Normalize(Source().GroupBy(static value => value%2));
        var expected=Snapshot(SystemEnumerable.Concat(expectedSource,Second()));
        var actual=Snapshot(actualSource.Concat(Second()));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_Query_InstanceDelegate__Contains_value() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),this.InstanceKeySelector,this.InstanceSelector));
        var actualSource = Normalize(Source().GroupBy(this.InstanceKeySelector,this.InstanceSelector));
        var expected=Snapshot(SystemEnumerable.Contains(expectedSource,3));
        var actual=Snapshot(actualSource.Contains(3));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_Query_StaticDelegate__Contains_value_comparer() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),static value => value%2,static value => value*2));
        var actualSource = Normalize(Source().GroupBy(static value => value%2,static value => value*2));
        var expected=Snapshot(SystemEnumerable.Contains(expectedSource,3,EqualityComparer<int>.Default));
        var actual=Snapshot(actualSource.Contains(3,EqualityComparer<int>.Default));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupJoin_keySelectors_resultSelector_InstanceDelegate__Count() {
        var expectedSource = Normalize(SystemEnumerable.GroupJoin(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValues) => (outerValue,innerValues)));
        var actualSource = Normalize(Source().GroupJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValues) => (outerValue,innerValues)));
        var expected=Snapshot(SystemEnumerable.Count(expectedSource));
        var actual=Snapshot(actualSource.Count());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupJoin_keySelectors_resultSelector_StaticDelegate__Count_predicate_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.GroupJoin(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValues) => (outerValue,innerValues)));
        var actualSource = Normalize(Source().GroupJoin(Second(),static value => value%2,static value => value%2,static (outerValue,innerValues) => (outerValue,innerValues)));
        var expected=Snapshot(SystemEnumerable.Count(expectedSource,this.InstancePredicate));
        var actual=Snapshot(actualSource.Count(this.InstancePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Index__Count_predicate_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.Index(Source()));
        var actualSource = Normalize(Source().Index());
        var expected=Snapshot(SystemEnumerable.Count(expectedSource,static value => value>2));
        var actual=Snapshot(actualSource.Count(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Intersect_second__CountBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Intersect(Source(),Second());
        var actualSource = Source().Intersect(Second());
        var expected=Snapshot(SystemEnumerable.CountBy(expectedSource,this.InstanceModuloKeySelector));
        var actual=Snapshot(actualSource.CountBy(this.InstanceModuloKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy_second_keySelector_InstanceDelegate__CountBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.IntersectBy(Source(),new[] { 2 },this.InstanceKeySelector);
        var actualSource = Source().IntersectBy(new[] { 2 },this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.CountBy(expectedSource,static value => value%2));
        var actual=Snapshot(actualSource.CountBy(static value => value%2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy_second_keySelector_StaticDelegate__DefaultIfEmpty() {
        var expectedSource = SystemEnumerable.IntersectBy(Source(),new[] { 2 },static value => value%2);
        var actualSource = Source().IntersectBy(new[] { 2 },static value => value%2);
        var expected=Snapshot(SystemEnumerable.DefaultIfEmpty(expectedSource));
        var actual=Snapshot(actualSource.DefaultIfEmpty());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate__DefaultIfEmpty_defaultValue() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.DefaultIfEmpty(expectedSource,9));
        var actual=Snapshot(actualSource.DefaultIfEmpty(9));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate__Distinct() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().Join(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Distinct(expectedSource));
        var actual=Snapshot(actualSource.Distinct());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_Query_InstanceDelegate__Distinct_comparer() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Distinct(expectedSource,EqualityComparer<int>.Default));
        var actual=Snapshot(actualSource.Distinct(EqualityComparer<int>.Default));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_Query_StaticDelegate__DistinctBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().Join(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.DistinctBy(expectedSource,this.InstanceModuloKeySelector));
        var actual=Snapshot(actualSource.DistinctBy(this.InstanceModuloKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin_keySelectors_resultSelector_InstanceDelegate__DistinctBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.LeftJoin(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().LeftJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.DistinctBy(expectedSource,static value => value%2));
        var actual=Snapshot(actualSource.DistinctBy(static value => value%2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin_keySelectors_resultSelector_StaticDelegate__ElementAt_index() {
        var expectedSource = SystemEnumerable.LeftJoin(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().LeftJoin(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.ElementAt(EnsureAtLeast3(expectedSource),2));
        var actual=Snapshot(EnsureAtLeast3(actualSource).ElementAt(2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OfType__ElementAtOrDefault_index() {
        var expectedSource = SystemEnumerable.OfType<int>(SystemEnumerable.Select(Source(),static value => (object)value));
        var actualSource = SystemEnumerable.Select(Source(),static value => (object)value).OfType<int>();
        var expected=Snapshot(SystemEnumerable.ElementAtOrDefault(expectedSource,20));
        var actual=Snapshot(actualSource.ElementAtOrDefault(20));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Order__Empty() {
        var expectedSource = SystemEnumerable.Order(Source());
        var actualSource = Source().Order();
        var expected=Snapshot(SystemEnumerable.Empty<int>());
        var actual=Snapshot(SystemEnumerable.Empty<int>());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_InstanceDelegate__Except_second() {
        var expectedSource = SystemEnumerable.OrderBy(Source(),this.InstanceKeySelector);
        var actualSource = Source().OrderBy(this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.Except(expectedSource,Second()));
        var actual=Snapshot(actualSource.Except(Second()));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate__ExceptBy_second_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.OrderBy(Source(),static value => value%2);
        var actualSource = Source().OrderBy(static value => value%2);
        var expected=Snapshot(SystemEnumerable.ExceptBy(expectedSource,new[] { 2 },this.InstanceKeySelector));
        var actual=Snapshot(actualSource.ExceptBy(new[] { 2 },this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending_keySelector_InstanceDelegate__ExceptBy_second_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.OrderByDescending(Source(),this.InstanceKeySelector);
        var actualSource = Source().OrderByDescending(this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.ExceptBy(expectedSource,new[] { 2 },static value => value));
        var actual=Snapshot(actualSource.ExceptBy(new[] { 2 },static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending_keySelector_StaticDelegate__First() {
        var expectedSource = SystemEnumerable.OrderByDescending(Source(),static value => value%2);
        var actualSource = Source().OrderByDescending(static value => value%2);
        var expected=Snapshot(SystemEnumerable.First(EnsureNonEmpty(expectedSource)));
        var actual=Snapshot(EnsureNonEmpty(actualSource).First());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderDescending__First_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.OrderDescending(Source());
        var actualSource = Source().OrderDescending();
        var expected=Snapshot(SystemEnumerable.First(EnsureAtLeast3(expectedSource),static value => value>2));
        var actual=Snapshot(EnsureAtLeast3(actualSource).First(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Prepend_element__First_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.Prepend(Source(),0);
        var actualSource = Source().Prepend(0);
        var expected=Snapshot(SystemEnumerable.First(EnsureAtLeast3(expectedSource),static value => value>2));
        var actual=Snapshot(EnsureAtLeast3(actualSource).First(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Range_start_count__FirstOrDefault() {
        var expectedSource = SystemEnumerable.Range(1,4);
        var actualSource = SystemEnumerable.Range(1,4);
        var expected=Snapshot(SystemEnumerable.FirstOrDefault(expectedSource));
        var actual=Snapshot(actualSource.FirstOrDefault());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Repeat_element_count__FirstOrDefault_predicate_defaultValue_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Repeat(2,3);
        var actualSource = SystemEnumerable.Repeat(2,3);
        var expected=Snapshot(SystemEnumerable.FirstOrDefault(expectedSource,this.InstanceNeverPredicate,-1));
        var actual=Snapshot(actualSource.FirstOrDefault(this.InstanceNeverPredicate,-1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Reverse__FirstOrDefault_predicate_defaultValue_StaticDelegate() {
        var expectedSource = SystemEnumerable.Reverse(Source());
        var actualSource = Source().Reverse();
        var expected=Snapshot(SystemEnumerable.FirstOrDefault(expectedSource,static value => value>9,-1));
        var actual=Snapshot(actualSource.FirstOrDefault(static value => value>9,-1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin_keySelectors_resultSelector_InstanceDelegate__GroupBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.RightJoin(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().RightJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.GroupBy(expectedSource,this.InstanceModuloKeySelector));
        var actual=Snapshot(actualSource.GroupBy(this.InstanceModuloKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin_keySelectors_resultSelector_StaticDelegate__GroupBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.RightJoin(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().RightJoin(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.GroupBy(expectedSource,static value => value%2));
        var actual=Snapshot(actualSource.GroupBy(static value => value%2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate__GroupBy_keySelector_Query_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),this.InstanceSelector);
        var actualSource = Source().Select(this.InstanceSelector);
        var expected=Snapshot(SystemEnumerable.GroupBy(expectedSource,this.InstanceModuloKeySelector,this.InstanceSelector));
        var actual=Snapshot(actualSource.GroupBy(this.InstanceModuloKeySelector,this.InstanceSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate__GroupBy_keySelector_Query_StaticDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),static value => value*2);
        var actualSource = Source().Select(static value => value*2);
        var expected=Snapshot(SystemEnumerable.GroupBy(expectedSource,static value => value%2,static value => value*2));
        var actual=Snapshot(actualSource.GroupBy(static value => value%2,static value => value*2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_index_InstanceDelegate__GroupJoin_keySelectors_resultSelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),this.InstanceIndexSelector);
        var actualSource = Source().Select(this.InstanceIndexSelector);
        var expected=Snapshot(SystemEnumerable.GroupJoin(expectedSource,Second(),this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValues) => (outerValue,innerValues)));
        var actual=Snapshot(actualSource.GroupJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValues) => (outerValue,innerValues)));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_index_StaticDelegate__GroupJoin_keySelectors_resultSelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),static (value,index) => value+index);
        var actualSource = Source().Select(static (value,index) => value+index);
        var expected=Snapshot(SystemEnumerable.GroupJoin(expectedSource,Second(),static value => value,static value => value,static (outerValue,innerValues) => (outerValue,innerValues)));
        var actual=Snapshot(actualSource.GroupJoin(Second(),static value => value,static value => value,static (outerValue,innerValues) => (outerValue,innerValues)));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_Query_InstanceDelegate__Index() {
        var expectedSource = SystemEnumerable.Select(Source(),this.InstanceSelector);
        var actualSource = Source().Select(this.InstanceSelector);
        var expected=Snapshot(SystemEnumerable.Index(expectedSource));
        var actual=Snapshot(actualSource.Index());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_Query_StaticDelegate__Intersect_second() {
        var expectedSource = SystemEnumerable.Select(Source(),static value => value*2);
        var actualSource = Source().Select(static value => value*2);
        var expected=Snapshot(SystemEnumerable.Intersect(expectedSource,Second()));
        var actual=Snapshot(actualSource.Intersect(Second()));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_InstanceDelegate__IntersectBy_second_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),this.InstanceCollectionSelector);
        var actualSource = Source().SelectMany(this.InstanceCollectionSelector);
        var expected=Snapshot(SystemEnumerable.IntersectBy(expectedSource,new[] { 2 },this.InstanceKeySelector));
        var actual=Snapshot(actualSource.IntersectBy(new[] { 2 },this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate__IntersectBy_second_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 });
        var actualSource = Source().SelectMany(static value => new[] { value,value+10 });
        var expected=Snapshot(SystemEnumerable.IntersectBy(expectedSource,new[] { 2 },static value => value));
        var actual=Snapshot(actualSource.IntersectBy(new[] { 2 },static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_index_InstanceDelegate__Join_keySelectors_resultSelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),this.InstanceIndexCollectionSelector);
        var actualSource = Source().SelectMany(this.InstanceIndexCollectionSelector);
        var expected=Snapshot(SystemEnumerable.Join(expectedSource,Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        var actual=Snapshot(actualSource.Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_index_StaticDelegate__Join_keySelectors_resultSelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static (value,index) => new[] { value,index });
        var actualSource = Source().SelectMany(static (value,index) => new[] { value,index });
        var expected=Snapshot(SystemEnumerable.Join(expectedSource,Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.Join(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_InstanceDelegate__Join_keySelectors_resultSelector_Query_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),this.InstanceCollectionSelector,this.InstanceResultSelector);
        var actualSource = Source().SelectMany(this.InstanceCollectionSelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Join(expectedSource,Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        var actual=Snapshot(actualSource.Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_StaticDelegate__Join_keySelectors_resultSelector_Query_StaticDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().SelectMany(static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Join(expectedSource,Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.Join(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_Query_InstanceDelegate__Last() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static outerValue => Second(),this.InstanceResultSelector);
        var actualSource = Source().SelectMany(static outerValue => Second(),this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Last(EnsureNonEmpty(expectedSource)));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Last());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_Query_StaticDelegate__Last_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().SelectMany(static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Last(EnsureAtLeast4(expectedSource),static value => value<4));
        var actual=Snapshot(EnsureAtLeast4(actualSource).Last(static value => value<4));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Skip_count__Last_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.Skip(Source(),1);
        var actualSource = Source().Skip(1);
        var expected=Snapshot(SystemEnumerable.Last(EnsureAtLeast4(expectedSource),static value => value<4));
        var actual=Snapshot(EnsureAtLeast4(actualSource).Last(static value => value<4));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipLast_count__LastOrDefault() {
        var expectedSource = SystemEnumerable.SkipLast(Source(),1);
        var actualSource = Source().SkipLast(1);
        var expected=Snapshot(SystemEnumerable.LastOrDefault(expectedSource));
        var actual=Snapshot(actualSource.LastOrDefault());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_InstanceDelegate__LastOrDefault_predicate_defaultValue_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SkipWhile(Source(),this.InstancePredicate);
        var actualSource = Source().SkipWhile(this.InstancePredicate);
        var expected=Snapshot(SystemEnumerable.LastOrDefault(expectedSource,this.InstanceNeverPredicate,-1));
        var actual=Snapshot(actualSource.LastOrDefault(this.InstanceNeverPredicate,-1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_StaticDelegate__LastOrDefault_predicate_defaultValue_StaticDelegate() {
        var expectedSource = SystemEnumerable.SkipWhile(Source(),static value => value>2);
        var actualSource = Source().SkipWhile(static value => value>2);
        var expected=Snapshot(SystemEnumerable.LastOrDefault(expectedSource,static value => value>9,-1));
        var actual=Snapshot(actualSource.LastOrDefault(static value => value>9,-1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_index_InstanceDelegate__LeftJoin_keySelectors_resultSelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SkipWhile(Source(),this.InstanceIndexPredicate);
        var actualSource = Source().SkipWhile(this.InstanceIndexPredicate);
        var expected=Snapshot(SystemEnumerable.LeftJoin(expectedSource,Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        var actual=Snapshot(actualSource.LeftJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SkipWhile_predicate_index_StaticDelegate__LeftJoin_keySelectors_resultSelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.SkipWhile(Source(),static (value,index) => value>index);
        var actualSource = Source().SkipWhile(static (value,index) => value>index);
        var expected=Snapshot(SystemEnumerable.LeftJoin(expectedSource,Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.LeftJoin(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Take_count__LongCount() {
        var expectedSource = SystemEnumerable.Take(Source(),3);
        var actualSource = Source().Take(3);
        var expected=Snapshot(SystemEnumerable.LongCount(expectedSource));
        var actual=Snapshot(actualSource.LongCount());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Take_range__LongCount_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Take(Source(),1..3);
        var actualSource = Source().Take(1..3);
        var expected=Snapshot(SystemEnumerable.LongCount(expectedSource,this.InstancePredicate));
        var actual=Snapshot(actualSource.LongCount(this.InstancePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeLast_count__LongCount_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.TakeLast(Source(),2);
        var actualSource = Source().TakeLast(2);
        var expected=Snapshot(SystemEnumerable.LongCount(expectedSource,static value => value>2));
        var actual=Snapshot(actualSource.LongCount(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile_predicate_InstanceDelegate__Max_int() {
        var expectedSource = SystemEnumerable.TakeWhile(Source(),this.InstanceTakePredicate);
        var actualSource = Source().TakeWhile(this.InstanceTakePredicate);
        var expected=Snapshot(SystemEnumerable.Max(EnsureNonEmpty(expectedSource)));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Max());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile_predicate_StaticDelegate__Max_selector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.TakeWhile(Source(),static value => value<4);
        var actualSource = Source().TakeWhile(static value => value<4);
        var expected=Snapshot(SystemEnumerable.Max(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Max(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile_predicate_index_InstanceDelegate__Max_selector_StaticDelegate() {
        var expectedSource = SystemEnumerable.TakeWhile(Source(),this.InstanceTakeIndexPredicate);
        var actualSource = Source().TakeWhile(this.InstanceTakeIndexPredicate);
        var expected=Snapshot(SystemEnumerable.Max(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Max(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void TakeWhile_predicate_index_StaticDelegate__MaxBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.TakeWhile(Source(),static (value,index) => value<4&&index<3);
        var actualSource = Source().TakeWhile(static (value,index) => value<4&&index<3);
        var expected=Snapshot(SystemEnumerable.MaxBy(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).MaxBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ThenBy_keySelector_InstanceDelegate__MaxBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Source(),static value => value),static value => value);
        var actualSource = Source().OrderBy(static value => value).ThenBy(static value => value);
        var expected=Snapshot(SystemEnumerable.MaxBy(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).MaxBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ThenBy_keySelector_StaticDelegate__Min_int() {
        var expectedSource = SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(Source(),static value => value),static value => value);
        var actualSource = Source().OrderBy(static value => value).ThenBy(static value => value);
        var expected=Snapshot(SystemEnumerable.Min(EnsureNonEmpty(expectedSource)));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Min());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ThenByDescending_keySelector_InstanceDelegate__Min_selector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Source(),static value => value),static value => value);
        var actualSource = Source().OrderBy(static value => value).ThenByDescending(static value => value);
        var expected=Snapshot(SystemEnumerable.Min(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Min(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ThenByDescending_keySelector_StaticDelegate__Min_selector_StaticDelegate() {
        var expectedSource = SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(Source(),static value => value),static value => value);
        var actualSource = Source().OrderBy(static value => value).ThenByDescending(static value => value);
        var expected=Snapshot(SystemEnumerable.Min(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).Min(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Union_second__MinBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Union(Source(),Second());
        var actualSource = Source().Union(Second());
        var expected=Snapshot(SystemEnumerable.MinBy(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).MinBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void UnionBy_second_keySelector_InstanceDelegate__MinBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.UnionBy(Source(),Second(),this.InstanceKeySelector);
        var actualSource = Source().UnionBy(Second(),this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.MinBy(EnsureNonEmpty(expectedSource),static value => value));
        var actual=Snapshot(EnsureNonEmpty(actualSource).MinBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void UnionBy_second_keySelector_StaticDelegate__OfType() {
        var expectedSource = SystemEnumerable.UnionBy(Source(),Second(),static value => value%2);
        var actualSource = Source().UnionBy(Second(),static value => value%2);
        var expected=Snapshot(SystemEnumerable.OfType<int>(SystemEnumerable.Select(expectedSource,static value => (object)value)));
        var actual=Snapshot(SystemEnumerable.Select(actualSource,static value => (object)value).OfType<int>());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_InstanceDelegate__Order() {
        var expectedSource = SystemEnumerable.Where(Source(),this.InstancePredicate);
        var actualSource = Source().Where(this.InstancePredicate);
        var expected=Snapshot(SystemEnumerable.Order(expectedSource));
        var actual=Snapshot(actualSource.Order());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_StaticDelegate__OrderBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Where(Source(),static value => value>2);
        var actualSource = Source().Where(static value => value>2);
        var expected=Snapshot(SystemEnumerable.OrderBy(expectedSource,this.InstanceKeySelector));
        var actual=Snapshot(actualSource.OrderBy(this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_index_InstanceDelegate__OrderBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Where(Source(),this.InstanceIndexPredicate);
        var actualSource = Source().Where(this.InstanceIndexPredicate);
        var expected=Snapshot(SystemEnumerable.OrderBy(expectedSource,static value => value));
        var actual=Snapshot(actualSource.OrderBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_index_StaticDelegate__OrderByDescending_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Where(Source(),static (value,index) => value>index);
        var actualSource = Source().Where(static (value,index) => value>index);
        var expected=Snapshot(SystemEnumerable.OrderByDescending(expectedSource,this.InstanceKeySelector));
        var actual=Snapshot(actualSource.OrderByDescending(this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_Query_InstanceDelegate__OrderByDescending_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Where(Source(),this.InstancePredicate);
        var actualSource = Source().Where(this.InstancePredicate);
        var expected=Snapshot(SystemEnumerable.OrderByDescending(expectedSource,static value => value));
        var actual=Snapshot(actualSource.OrderByDescending(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_predicate_Query_StaticDelegate__OrderDescending() {
        var expectedSource = SystemEnumerable.Where(Source(),static value => value>2);
        var actualSource = Source().Where(static value => value>2);
        var expected=Snapshot(SystemEnumerable.OrderDescending(expectedSource));
        var actual=Snapshot(actualSource.OrderDescending());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second__Prepend_element() {
        var expectedSource = Normalize(SystemEnumerable.Zip(Source(),Second()));
        var actualSource = Normalize(Source().Zip(Second()));
        var expected=Snapshot(SystemEnumerable.Prepend(expectedSource,0));
        var actual=Snapshot(actualSource.Prepend(0));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_InstanceDelegate__Range_start_count() {
        var expectedSource = SystemEnumerable.Zip(Source(),Second(),this.InstanceResultSelector);
        var actualSource = Source().Zip(Second(),this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Range(1,4));
        var actual=Snapshot(SystemEnumerable.Range(1,4));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_resultSelector_StaticDelegate__Repeat_element_count() {
        var expectedSource = SystemEnumerable.Zip(Source(),Second(),static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().Zip(Second(),static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Repeat(2,3));
        var actual=Snapshot(SystemEnumerable.Repeat(2,3));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Zip_second_third__Reverse() {
        var expectedSource = Normalize(SystemEnumerable.Zip(Source(),Second(),Third()));
        var actualSource = Normalize(Source().Zip(Second(),Third()));
        var expected=Snapshot(SystemEnumerable.Reverse(expectedSource));
        var actual=Snapshot(actualSource.Reverse());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_InstanceDelegate__RightJoin_keySelectors_resultSelector_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),this.InstanceKeySelector,0,this.InstanceAccumulator));
        var actualSource = Normalize(Source().AggregateBy(this.InstanceKeySelector,0,this.InstanceAccumulator));
        var expected=Snapshot(SystemEnumerable.RightJoin(expectedSource,Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        var actual=Snapshot(actualSource.RightJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seed_func_StaticDelegate__RightJoin_keySelectors_resultSelector_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),static value => value%2,0,static (sum,value) => sum+value));
        var actualSource = Normalize(Source().AggregateBy(static value => value%2,0,static (sum,value) => sum+value));
        var expected=Snapshot(SystemEnumerable.RightJoin(expectedSource,Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.RightJoin(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seedSelector_func_InstanceDelegate__Select_selector_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),this.InstanceKeySelector,this.InstanceSeedSelector,this.InstanceAccumulator));
        var actualSource = Normalize(Source().AggregateBy(this.InstanceKeySelector,this.InstanceSeedSelector,this.InstanceAccumulator));
        var expected=Snapshot(SystemEnumerable.Select(expectedSource,this.InstanceSelector));
        var actual=Snapshot(actualSource.Select(this.InstanceSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AggregateBy_keySelector_seedSelector_func_StaticDelegate__Select_selector_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.AggregateBy(Source(),static value => value%2,static _ => 1,static (sum,value) => sum+value));
        var actualSource = Normalize(Source().AggregateBy(static value => value%2,static _ => 1,static (sum,value) => sum+value));
        var expected=Snapshot(SystemEnumerable.Select(expectedSource,static value => value*2));
        var actual=Snapshot(actualSource.Select(static value => value*2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Append_element__Select_selector_index_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Append(Source(),5);
        var actualSource = Source().Append(5);
        var expected=Snapshot(SystemEnumerable.Select(expectedSource,this.InstanceIndexSelector));
        var actual=Snapshot(actualSource.Select(this.InstanceIndexSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void AsEnumerable__Select_selector_index_StaticDelegate() {
        var expectedSource = SystemEnumerable.AsEnumerable(Source());
        var actualSource = Source().AsEnumerable();
        var expected=Snapshot(SystemEnumerable.Select(expectedSource,static (value,index) => value+index));
        var actual=Snapshot(actualSource.Select(static (value,index) => value+index));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Cast__Select_selector_Query_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Cast<int>(SystemEnumerable.Select(Source(),static value => (object)value));
        var actualSource = SystemEnumerable.Select(Source(),static value => (object)value).Cast<int>();
        var expected=Snapshot(SystemEnumerable.Select(expectedSource,this.InstanceSelector));
        var actual=Snapshot(actualSource.Select(this.InstanceSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Chunk_size__Select_selector_Query_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.Chunk(Source(),2));
        var actualSource = Normalize(Source().Chunk(2));
        var expected=Snapshot(SystemEnumerable.Select(expectedSource,static value => value*2));
        var actual=Snapshot(actualSource.Select(static value => value*2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Concat_second__SelectMany_selector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Concat(Source(),Second());
        var actualSource = Source().Concat(Second());
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,this.InstanceCollectionSelector));
        var actual=Snapshot(actualSource.SelectMany(this.InstanceCollectionSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy_keySelector_InstanceDelegate__SelectMany_selector_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.CountBy(Source(),this.InstanceKeySelector));
        var actualSource = Normalize(Source().CountBy(this.InstanceKeySelector));
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,static value => new[] { value,value+10 }));
        var actual=Snapshot(actualSource.SelectMany(static value => new[] { value,value+10 }));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void CountBy_keySelector_StaticDelegate__SelectMany_selector_index_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.CountBy(Source(),static value => value%2));
        var actualSource = Normalize(Source().CountBy(static value => value%2));
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,this.InstanceIndexCollectionSelector));
        var actual=Snapshot(actualSource.SelectMany(this.InstanceIndexCollectionSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty__SelectMany_selector_index_StaticDelegate() {
        var expectedSource = SystemEnumerable.DefaultIfEmpty(Source());
        var actualSource = Source().DefaultIfEmpty();
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,static (value,index) => new[] { value,index }));
        var actual=Snapshot(actualSource.SelectMany(static (value,index) => new[] { value,index }));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DefaultIfEmpty_defaultValue__SelectMany_collectionSelector_resultSelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.DefaultIfEmpty(Source(),9);
        var actualSource = Source().DefaultIfEmpty(9);
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,this.InstanceCollectionSelector,this.InstanceResultSelector));
        var actual=Snapshot(actualSource.SelectMany(this.InstanceCollectionSelector,this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct__SelectMany_collectionSelector_resultSelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Distinct(Source());
        var actualSource = Source().Distinct();
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.SelectMany(static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Distinct_comparer__SelectMany_collectionSelector_resultSelector_Query_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Distinct(Source(),EqualityComparer<int>.Default);
        var actualSource = Source().Distinct(EqualityComparer<int>.Default);
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,static outerValue => Second(),this.InstanceResultSelector));
        var actual=Snapshot(actualSource.SelectMany(static outerValue => Second(),this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_InstanceDelegate__SelectMany_collectionSelector_resultSelector_Query_StaticDelegate() {
        var expectedSource = SystemEnumerable.DistinctBy(Source(),this.InstanceKeySelector);
        var actualSource = Source().DistinctBy(this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.SelectMany(expectedSource,static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.SelectMany(static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void DistinctBy_keySelector_StaticDelegate__SequenceEqual_second() {
        var expectedSource = SystemEnumerable.DistinctBy(Source(),static value => value%2);
        var actualSource = Source().DistinctBy(static value => value%2);
        var expected=Snapshot(SystemEnumerable.SequenceEqual(expectedSource,expectedSource));
        var actual=Snapshot(actualSource.SequenceEqual(actualSource));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Empty__Single() {
        var expectedSource = SystemEnumerable.Empty<int>();
        var actualSource = SystemEnumerable.Empty<int>();
        var expected=Snapshot(SystemEnumerable.Single(EnsureSingle(expectedSource)));
        var actual=Snapshot(EnsureSingle(actualSource).Single());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Except_second__Single_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Except(Source(),Second());
        var actualSource = Source().Except(Second());
        var expected=Snapshot(SystemEnumerable.Single(new[] { 2 },static value => value==2));
        var actual=Snapshot(new[] { 2 }.Single(static value => value==2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy_second_keySelector_InstanceDelegate__Single_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.ExceptBy(Source(),new[] { 2 },this.InstanceKeySelector);
        var actualSource = Source().ExceptBy(new[] { 2 },this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.Single(new[] { 2 },static value => value==2));
        var actual=Snapshot(new[] { 2 }.Single(static value => value==2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptBy_second_keySelector_StaticDelegate__SingleOrDefault() {
        var expectedSource = SystemEnumerable.ExceptBy(Source(),new[] { 2 },static value => value%2);
        var actualSource = Source().ExceptBy(new[] { 2 },static value => value%2);
        var expected=Snapshot(SystemEnumerable.SingleOrDefault(EnsureSingle(expectedSource)));
        var actual=Snapshot(EnsureSingle(actualSource).SingleOrDefault());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_InstanceDelegate__SingleOrDefault_predicate_defaultValue_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),this.InstanceKeySelector));
        var actualSource = Normalize(Source().GroupBy(this.InstanceKeySelector));
        var expected=Snapshot(SystemEnumerable.SingleOrDefault(expectedSource,this.InstanceNeverPredicate,-1));
        var actual=Snapshot(actualSource.SingleOrDefault(this.InstanceNeverPredicate,-1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_StaticDelegate__SingleOrDefault_predicate_defaultValue_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),static value => value%2));
        var actualSource = Normalize(Source().GroupBy(static value => value%2));
        var expected=Snapshot(SystemEnumerable.SingleOrDefault(expectedSource,static value => value==9,-1));
        var actual=Snapshot(actualSource.SingleOrDefault(static value => value==9,-1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_Query_InstanceDelegate__Skip_count() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),this.InstanceKeySelector,this.InstanceSelector));
        var actualSource = Normalize(Source().GroupBy(this.InstanceKeySelector,this.InstanceSelector));
        var expected=Snapshot(SystemEnumerable.Skip(expectedSource,1));
        var actual=Snapshot(actualSource.Skip(1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupBy_keySelector_Query_StaticDelegate__SkipLast_count() {
        var expectedSource = Normalize(SystemEnumerable.GroupBy(Source(),static value => value%2,static value => value*2));
        var actualSource = Normalize(Source().GroupBy(static value => value%2,static value => value*2));
        var expected=Snapshot(SystemEnumerable.SkipLast(expectedSource,1));
        var actual=Snapshot(actualSource.SkipLast(1));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupJoin_keySelectors_resultSelector_InstanceDelegate__SkipWhile_predicate_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.GroupJoin(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValues) => (outerValue,innerValues)));
        var actualSource = Normalize(Source().GroupJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValues) => (outerValue,innerValues)));
        var expected=Snapshot(SystemEnumerable.SkipWhile(expectedSource,this.InstanceSkipPredicate));
        var actual=Snapshot(actualSource.SkipWhile(this.InstanceSkipPredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void GroupJoin_keySelectors_resultSelector_StaticDelegate__SkipWhile_predicate_StaticDelegate() {
        var expectedSource = Normalize(SystemEnumerable.GroupJoin(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValues) => (outerValue,innerValues)));
        var actualSource = Normalize(Source().GroupJoin(Second(),static value => value%2,static value => value%2,static (outerValue,innerValues) => (outerValue,innerValues)));
        var expected=Snapshot(SystemEnumerable.SkipWhile(expectedSource,static value => value<3));
        var actual=Snapshot(actualSource.SkipWhile(static value => value<3));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Index__SkipWhile_predicate_index_InstanceDelegate() {
        var expectedSource = Normalize(SystemEnumerable.Index(Source()));
        var actualSource = Normalize(Source().Index());
        var expected=Snapshot(SystemEnumerable.SkipWhile(expectedSource,this.InstanceSkipIndexPredicate));
        var actual=Snapshot(actualSource.SkipWhile(this.InstanceSkipIndexPredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Intersect_second__SkipWhile_predicate_index_StaticDelegate() {
        var expectedSource = SystemEnumerable.Intersect(Source(),Second());
        var actualSource = Source().Intersect(Second());
        var expected=Snapshot(SystemEnumerable.SkipWhile(expectedSource,static (value,index) => value<3&&index<2));
        var actual=Snapshot(actualSource.SkipWhile(static (value,index) => value<3&&index<2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy_second_keySelector_InstanceDelegate__Sum_int() {
        var expectedSource = SystemEnumerable.IntersectBy(Source(),new[] { 2 },this.InstanceKeySelector);
        var actualSource = Source().IntersectBy(new[] { 2 },this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.Sum(expectedSource));
        var actual=Snapshot(actualSource.Sum());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectBy_second_keySelector_StaticDelegate__Sum_selector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.IntersectBy(Source(),new[] { 2 },static value => value%2);
        var actualSource = Source().IntersectBy(new[] { 2 },static value => value%2);
        var expected=Snapshot(SystemEnumerable.Sum(expectedSource,this.InstanceKeySelector));
        var actual=Snapshot(actualSource.Sum(this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_InstanceDelegate__Sum_selector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Sum(expectedSource,static value => value));
        var actual=Snapshot(actualSource.Sum(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_StaticDelegate__Take_count() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().Join(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Take(expectedSource,3));
        var actual=Snapshot(actualSource.Take(3));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_Query_InstanceDelegate__Take_range() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().Join(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Take(EnsureAtLeast3(expectedSource),1..3));
        var actual=Snapshot(EnsureAtLeast3(actualSource).Take(1..3));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Join_keySelectors_resultSelector_Query_StaticDelegate__TakeLast_count() {
        var expectedSource = SystemEnumerable.Join(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().Join(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.TakeLast(expectedSource,2));
        var actual=Snapshot(actualSource.TakeLast(2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin_keySelectors_resultSelector_InstanceDelegate__TakeWhile_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.LeftJoin(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().LeftJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.TakeWhile(expectedSource,this.InstanceTakePredicate));
        var actual=Snapshot(actualSource.TakeWhile(this.InstanceTakePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void LeftJoin_keySelectors_resultSelector_StaticDelegate__TakeWhile_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.LeftJoin(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().LeftJoin(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.TakeWhile(expectedSource,static value => value<4));
        var actual=Snapshot(actualSource.TakeWhile(static value => value<4));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OfType__TakeWhile_predicate_index_InstanceDelegate() {
        var expectedSource = SystemEnumerable.OfType<int>(SystemEnumerable.Select(Source(),static value => (object)value));
        var actualSource = SystemEnumerable.Select(Source(),static value => (object)value).OfType<int>();
        var expected=Snapshot(SystemEnumerable.TakeWhile(expectedSource,this.InstanceTakeIndexPredicate));
        var actual=Snapshot(actualSource.TakeWhile(this.InstanceTakeIndexPredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Order__TakeWhile_predicate_index_StaticDelegate() {
        var expectedSource = SystemEnumerable.Order(Source());
        var actualSource = Source().Order();
        var expected=Snapshot(SystemEnumerable.TakeWhile(expectedSource,static (value,index) => value<4&&index<3));
        var actual=Snapshot(actualSource.TakeWhile(static (value,index) => value<4&&index<3));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_InstanceDelegate__ThenBy_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.OrderBy(Source(),this.InstanceKeySelector);
        var actualSource = Source().OrderBy(this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(expectedSource,static value => value),static value => value));
        var actual=Snapshot(actualSource.OrderBy(static value => value).ThenBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderBy_keySelector_StaticDelegate__ThenBy_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.OrderBy(Source(),static value => value%2);
        var actualSource = Source().OrderBy(static value => value%2);
        var expected=Snapshot(SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(expectedSource,static value => value),static value => value));
        var actual=Snapshot(actualSource.OrderBy(static value => value).ThenBy(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending_keySelector_InstanceDelegate__ThenByDescending_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.OrderByDescending(Source(),this.InstanceKeySelector);
        var actualSource = Source().OrderByDescending(this.InstanceKeySelector);
        var expected=Snapshot(SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(expectedSource,static value => value),static value => value));
        var actual=Snapshot(actualSource.OrderBy(static value => value).ThenByDescending(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderByDescending_keySelector_StaticDelegate__ThenByDescending_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.OrderByDescending(Source(),static value => value%2);
        var actualSource = Source().OrderByDescending(static value => value%2);
        var expected=Snapshot(SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(expectedSource,static value => value),static value => value));
        var actual=Snapshot(actualSource.OrderBy(static value => value).ThenByDescending(static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void OrderDescending__ToArray() {
        var expectedSource = SystemEnumerable.OrderDescending(Source());
        var actualSource = Source().OrderDescending();
        var expected=Snapshot(SystemEnumerable.ToArray(expectedSource));
        var actual=Snapshot(actualSource.ToArray());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Prepend_element__ToDictionary_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Prepend(Source(),0);
        var actualSource = Source().Prepend(0);
        var expected=Snapshot(SystemEnumerable.ToDictionary(SystemEnumerable.Select(expectedSource,static (value,index) => new KeyValuePair<int,int>(index,value)),static pair => pair.Key,static pair => pair.Value));
        var actual=Snapshot(SystemEnumerable.Select(actualSource,static (value,index) => new KeyValuePair<int,int>(index,value)).ToDictionary(static pair => pair.Key,static pair => pair.Value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Range_start_count__ToDictionary_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Range(1,4);
        var actualSource = SystemEnumerable.Range(1,4);
        var expected=Snapshot(SystemEnumerable.ToDictionary(SystemEnumerable.Select(expectedSource,static (value,index) => new KeyValuePair<int,int>(index,value)),static pair => pair.Key,static pair => pair.Value));
        var actual=Snapshot(SystemEnumerable.Select(actualSource,static (value,index) => new KeyValuePair<int,int>(index,value)).ToDictionary(static pair => pair.Key,static pair => pair.Value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Repeat_element_count__ToHashSet() {
        var expectedSource = SystemEnumerable.Repeat(2,3);
        var actualSource = SystemEnumerable.Repeat(2,3);
        var expected=Snapshot(SystemEnumerable.ToHashSet(expectedSource));
        var actual=Snapshot(actualSource.ToHashSet());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Reverse__ToList() {
        var expectedSource = SystemEnumerable.Reverse(Source());
        var actualSource = Source().Reverse();
        var expected=Snapshot(SystemEnumerable.ToList(expectedSource));
        var actual=Snapshot(actualSource.ToList());
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin_keySelectors_resultSelector_InstanceDelegate__ToLookup_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.RightJoin(Source(),Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var actualSource = Source().RightJoin(Second(),this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.ToLookup(expectedSource,this.InstanceModuloKeySelector));
        var actual=Snapshot(actualSource.ToLookup(this.InstanceModuloKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void RightJoin_keySelectors_resultSelector_StaticDelegate__ToLookup_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.RightJoin(Source(),Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().RightJoin(Second(),static value => value%2,static value => value%2,static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.ToLookup(expectedSource,static value => value%2));
        var actual=Snapshot(actualSource.ToLookup(static value => value%2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_InstanceDelegate__TryGetNonEnumeratedCount_source() {
        var expectedSource = SystemEnumerable.Select(Source(),this.InstanceSelector);
        var actualSource = Source().Select(this.InstanceSelector);
        var expected=Snapshot(TryGetNonEnumeratedCountResult_System(expectedSource));
        var actual=Snapshot(TryGetNonEnumeratedCountResult_Extension(actualSource));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_StaticDelegate__Union_second() {
        var expectedSource = SystemEnumerable.Select(Source(),static value => value*2);
        var actualSource = Source().Select(static value => value*2);
        var expected=Snapshot(SystemEnumerable.Union(expectedSource,Second()));
        var actual=Snapshot(actualSource.Union(Second()));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_index_InstanceDelegate__UnionBy_second_keySelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),this.InstanceIndexSelector);
        var actualSource = Source().Select(this.InstanceIndexSelector);
        var expected=Snapshot(SystemEnumerable.UnionBy(expectedSource,Second(),this.InstanceKeySelector));
        var actual=Snapshot(actualSource.UnionBy(Second(),this.InstanceKeySelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_index_StaticDelegate__UnionBy_second_keySelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),static (value,index) => value+index);
        var actualSource = Source().Select(static (value,index) => value+index);
        var expected=Snapshot(SystemEnumerable.UnionBy(expectedSource,Second(),static value => value));
        var actual=Snapshot(actualSource.UnionBy(Second(),static value => value));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_Query_InstanceDelegate__Where_predicate_InstanceDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),this.InstanceSelector);
        var actualSource = Source().Select(this.InstanceSelector);
        var expected=Snapshot(SystemEnumerable.Where(expectedSource,this.InstancePredicate));
        var actual=Snapshot(actualSource.Where(this.InstancePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_selector_Query_StaticDelegate__Where_predicate_StaticDelegate() {
        var expectedSource = SystemEnumerable.Select(Source(),static value => value*2);
        var actualSource = Source().Select(static value => value*2);
        var expected=Snapshot(SystemEnumerable.Where(expectedSource,static value => value>2));
        var actual=Snapshot(actualSource.Where(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_InstanceDelegate__Where_predicate_index_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),this.InstanceCollectionSelector);
        var actualSource = Source().SelectMany(this.InstanceCollectionSelector);
        var expected=Snapshot(SystemEnumerable.Where(expectedSource,this.InstanceIndexPredicate));
        var actual=Snapshot(actualSource.Where(this.InstanceIndexPredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_StaticDelegate__Where_predicate_index_StaticDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 });
        var actualSource = Source().SelectMany(static value => new[] { value,value+10 });
        var expected=Snapshot(SystemEnumerable.Where(expectedSource,static (value,index) => value>index));
        var actual=Snapshot(actualSource.Where(static (value,index) => value>index));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_index_InstanceDelegate__Where_predicate_Query_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),this.InstanceIndexCollectionSelector);
        var actualSource = Source().SelectMany(this.InstanceIndexCollectionSelector);
        var expected=Snapshot(SystemEnumerable.Where(expectedSource,this.InstancePredicate));
        var actual=Snapshot(actualSource.Where(this.InstancePredicate));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_selector_index_StaticDelegate__Where_predicate_Query_StaticDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static (value,index) => new[] { value,index });
        var actualSource = Source().SelectMany(static (value,index) => new[] { value,index });
        var expected=Snapshot(SystemEnumerable.Where(expectedSource,static value => value>2));
        var actual=Snapshot(actualSource.Where(static value => value>2));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_InstanceDelegate__Zip_second() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),this.InstanceCollectionSelector,this.InstanceResultSelector);
        var actualSource = Source().SelectMany(this.InstanceCollectionSelector,this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Zip(expectedSource,Second()));
        var actual=Snapshot(actualSource.Zip(Second()));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_StaticDelegate__Zip_second_resultSelector_InstanceDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().SelectMany(static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Zip(expectedSource,Second(),this.InstanceResultSelector));
        var actual=Snapshot(actualSource.Zip(Second(),this.InstanceResultSelector));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_Query_InstanceDelegate__Zip_second_resultSelector_StaticDelegate() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static outerValue => Second(),this.InstanceResultSelector);
        var actualSource = Source().SelectMany(static outerValue => Second(),this.InstanceResultSelector);
        var expected=Snapshot(SystemEnumerable.Zip(expectedSource,Second(),static (outerValue,innerValue) => outerValue+innerValue));
        var actual=Snapshot(actualSource.Zip(Second(),static (outerValue,innerValue) => outerValue+innerValue));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void SelectMany_collectionSelector_resultSelector_Query_StaticDelegate__Zip_second_third() {
        var expectedSource = SystemEnumerable.SelectMany(Source(),static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue);
        var actualSource = Source().SelectMany(static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue);
        var expected=Snapshot(SystemEnumerable.Zip(expectedSource,Second(),Third()));
        var actual=Snapshot(actualSource.Zip(Second(),Third()));
        Assert.Equal(expected,actual);
    }

    private bool InstancePredicate(int value) => value>2;
    private bool InstancePositivePredicate(int value) => value>0;
    private bool InstanceNeverPredicate(int value) => value>9;
    private bool InstanceSkipPredicate(int value) => value<3;
    private bool InstanceTakePredicate(int value) => value<4;
    private bool InstanceIndexPredicate(int value,int index) => value>index;
    private bool InstanceSkipIndexPredicate(int value,int index) => value<3&&index<2;
    private bool InstanceTakeIndexPredicate(int value,int index) => value<4&&index<3;
    private int InstanceSelector(int value) => value*2;
    private int InstanceIndexSelector(int value,int index) => value+index;
    private int InstanceKeySelector(int value) => value;
    private int InstanceModuloKeySelector(int value) => value%2;
    private int InstanceSeedSelector(int value) => 1;
    private int InstanceAccumulator(int sum,int value) => sum+value+this.instanceOffset-1;
    private int InstanceAggregateFunc(int left,int right) => left+right+this.instanceOffset-1;
    private int InstanceResultValueSelector(int value) => value*2+this.instanceOffset-1;
    private int InstanceResultSelector(int outerValue,int innerValue) => outerValue+innerValue+this.instanceOffset-1;
    private IEnumerable<int> InstanceCollectionSelector(int value) => new[] { value,value+10 };
    private IEnumerable<int> InstanceIndexCollectionSelector(int value,int index) => new[] { value,index };

    private static int[] Source() => [1,2,3,4];
    private static int[] Second() => [3,4,5];
    private static int[] Third() => [10,20,30];

    private static IEnumerable<int> Normalize<T>(IEnumerable<T> source) => SystemEnumerable.Select(source,static _ => 1);
    private static IEnumerable<int> EnsureNonEmpty(IEnumerable<int> source) => SystemEnumerable.DefaultIfEmpty(source,0);
    private static IEnumerable<int> EnsureAtLeast3(IEnumerable<int> source) => SystemEnumerable.Concat(source,new[] { 1,2,3 });
    private static IEnumerable<int> EnsureAtLeast4(IEnumerable<int> source) => SystemEnumerable.Concat(source,new[] { 1,2,3,4 });
    private static IEnumerable<int> EnsureSingle(IEnumerable<int> source) => SystemEnumerable.Take(EnsureNonEmpty(source),1);

    private static int TryGetNonEnumeratedCountResult_System(IEnumerable<int> source) {
        SystemEnumerable.TryGetNonEnumeratedCount(source,out var count);
        return count;
    }

    private static int TryGetNonEnumeratedCountResult_Extension(IEnumerable<int> source) {
        source.TryGetNonEnumeratedCount(out var count);
        return count;
    }

    private static string Snapshot(object? value) {
        if(value is null) {
            return "<null>";
        }
        if(value is string text) {
            return text;
        }
        if(value is global::System.Runtime.CompilerServices.ITuple tuple) {
            return "("+string.Join(",",SystemEnumerable.Select(SystemEnumerable.Range(0,tuple.Length),index => Snapshot(tuple[index])))+")";
        }
        if(value is NonGenericEnumerable enumerable) {
            return "["+string.Join(",",SystemEnumerable.Select(SystemEnumerable.Cast<object?>(enumerable),Snapshot))+"]";
        }
        return value.ToString()??string.Empty;
    }
}

internal static class EnumerableChainHelpers {
    internal static int[] Source() => [1,2,3,4];
    internal static int[] Second() => [3,4,5];
    internal static int[] Third() => [10,20,30];
    internal static IEnumerable<int> Normalize<T>(IEnumerable<T> source) => SystemEnumerable.Select(source,static _ => 1);
    internal static IEnumerable<int> EnsureNonEmpty(IEnumerable<int> source) => SystemEnumerable.DefaultIfEmpty(source,0);
    internal static IEnumerable<int> EnsureAtLeast3(IEnumerable<int> source) => SystemEnumerable.Concat(source,new[] { 1,2,3 });
    internal static IEnumerable<int> EnsureAtLeast4(IEnumerable<int> source) => SystemEnumerable.Concat(source,new[] { 1,2,3,4 });
    internal static IEnumerable<int> EnsureSingle(IEnumerable<int> source) => SystemEnumerable.Take(EnsureNonEmpty(source),1);
    internal static int TryGetNonEnumeratedCountResult_Extension(IEnumerable<int> source) {
        source.TryGetNonEnumeratedCount(out var count);
        return count;
    }
    internal static void Consume(object? value) {
        if(value is NonGenericEnumerable enumerable&&value is not string) {
            foreach(var item in enumerable) {
                if(item is NonGenericEnumerable nested&&item is not string) {
                    foreach(var _ in nested) { }
                }
            }
        }
    }
    internal static IEnumerable<int> Source_M066_Select_selector(IEnumerable<int> source) => source.Select(static value => value*2);
    internal static IEnumerable<int> Source_M099_Where_predicate(IEnumerable<int> source) => source.Where(static value => value>2);
    internal static object? Outer_M000_Aggregate_func(IEnumerable<int> source) => source.Aggregate(static (left,right) => left+right);
    internal static object? Outer_M001_Aggregate_func(IEnumerable<int> source) => source.Aggregate(static (left,right) => left+right);
    internal static object? Outer_M002_Aggregate_seed_func(IEnumerable<int> source) => source.Aggregate(10,static (sum,value) => sum+value);
    internal static object? Outer_M003_Aggregate_seed_func_resultSelector(IEnumerable<int> source) => source.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2);
    internal static object? Outer_M004_AggregateBy_keySelector_seed_func(IEnumerable<int> source) => source.AggregateBy(static value => value%2,0,static (sum,value) => sum+value);
    internal static object? Outer_M005_AggregateBy_keySelector_seedSelector_func(IEnumerable<int> source) => source.AggregateBy(static value => value%2,static _ => 1,static (sum,value) => sum+value);
    internal static object? Outer_M006_All_predicate(IEnumerable<int> source) => source.All(static value => value>0);
    internal static object? Outer_M007_Any(IEnumerable<int> source) => source.Any();
    internal static object? Outer_M008_Any_predicate(IEnumerable<int> source) => source.Any(static value => value>2);
    internal static object? Outer_M009_Append_element(IEnumerable<int> source) => source.Append(5);
    internal static object? Outer_M010_AsEnumerable(IEnumerable<int> source) => source.AsEnumerable();
    internal static object? Outer_M011_Average_int(IEnumerable<int> source) => source.Average();
    internal static object? Outer_M012_Average_selector(IEnumerable<int> source) => source.Average(static value => value);
    internal static object? Outer_M013_Cast(IEnumerable<int> source) => SystemEnumerable.Select(source,static value => (object)value).Cast<int>();
    internal static object? Outer_M014_Chunk_size(IEnumerable<int> source) => source.Chunk(2);
    internal static object? Outer_M015_Concat_second(IEnumerable<int> source) => source.Concat(Second());
    internal static object? Outer_M016_Contains_value(IEnumerable<int> source) => source.Contains(3);
    internal static object? Outer_M017_Contains_value_comparer(IEnumerable<int> source) => source.Contains(3,EqualityComparer<int>.Default);
    internal static object? Outer_M018_Count(IEnumerable<int> source) => source.Count();
    internal static object? Outer_M019_Count_predicate(IEnumerable<int> source) => source.Count(static value => value>2);
    internal static object? Outer_M020_CountBy_keySelector(IEnumerable<int> source) => source.CountBy(static value => value%2);
    internal static object? Outer_M021_DefaultIfEmpty(IEnumerable<int> source) => source.DefaultIfEmpty();
    internal static object? Outer_M022_DefaultIfEmpty_defaultValue(IEnumerable<int> source) => source.DefaultIfEmpty(9);
    internal static object? Outer_M023_Distinct(IEnumerable<int> source) => source.Distinct();
    internal static object? Outer_M024_Distinct_comparer(IEnumerable<int> source) => source.Distinct(EqualityComparer<int>.Default);
    internal static object? Outer_M025_DistinctBy_keySelector(IEnumerable<int> source) => source.DistinctBy(static value => value%2);
    internal static object? Outer_M026_ElementAt_index(IEnumerable<int> source) => EnsureAtLeast3(source).ElementAt(2);
    internal static object? Outer_M027_ElementAtOrDefault_index(IEnumerable<int> source) => source.ElementAtOrDefault(20);
    internal static object? Outer_M028_Empty(IEnumerable<int> source) => SystemEnumerable.Empty<int>();
    internal static object? Outer_M029_Except_second(IEnumerable<int> source) => source.Except(Second());
    internal static object? Outer_M030_ExceptBy_second_keySelector(IEnumerable<int> source) => source.ExceptBy(new[] { 2 },static value => value);
    internal static object? Outer_M031_First(IEnumerable<int> source) => EnsureNonEmpty(source).First();
    internal static object? Outer_M032_First_predicate(IEnumerable<int> source) => EnsureAtLeast3(source).First(static value => value>2);
    internal static object? Outer_M033_FirstOrDefault(IEnumerable<int> source) => source.FirstOrDefault();
    internal static object? Outer_M034_FirstOrDefault_predicate_defaultValue(IEnumerable<int> source) => source.FirstOrDefault(static value => value>9,-1);
    internal static object? Outer_M035_GroupBy_keySelector(IEnumerable<int> source) => source.GroupBy(static value => value%2);
    internal static object? Outer_M036_GroupBy_keySelector_Query(IEnumerable<int> source) => source.GroupBy(static value => value%2,static value => value*2);
    internal static object? Outer_M037_GroupJoin_keySelectors_resultSelector(IEnumerable<int> source) => source.GroupJoin(Second(),static value => value,static value => value,static (outerValue,innerValues) => (outerValue,innerValues));
    internal static object? Outer_M038_Index(IEnumerable<int> source) => source.Index();
    internal static object? Outer_M039_Intersect_second(IEnumerable<int> source) => source.Intersect(Second());
    internal static object? Outer_M040_IntersectBy_second_keySelector(IEnumerable<int> source) => source.IntersectBy(new[] { 2 },static value => value);
    internal static object? Outer_M041_Join_keySelectors_resultSelector(IEnumerable<int> source) => source.Join(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M042_Join_keySelectors_resultSelector_Query(IEnumerable<int> source) => source.Join(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M043_Last(IEnumerable<int> source) => EnsureNonEmpty(source).Last();
    internal static object? Outer_M044_Last_predicate(IEnumerable<int> source) => EnsureAtLeast4(source).Last(static value => value<4);
    internal static object? Outer_M045_LastOrDefault(IEnumerable<int> source) => source.LastOrDefault();
    internal static object? Outer_M046_LastOrDefault_predicate_defaultValue(IEnumerable<int> source) => source.LastOrDefault(static value => value>9,-1);
    internal static object? Outer_M047_LeftJoin_keySelectors_resultSelector(IEnumerable<int> source) => source.LeftJoin(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M048_LongCount(IEnumerable<int> source) => source.LongCount();
    internal static object? Outer_M049_LongCount_predicate(IEnumerable<int> source) => source.LongCount(static value => value>2);
    internal static object? Outer_M050_Max_int(IEnumerable<int> source) => EnsureNonEmpty(source).Max();
    internal static object? Outer_M051_Max_selector(IEnumerable<int> source) => EnsureNonEmpty(source).Max(static value => value);
    internal static object? Outer_M052_MaxBy_keySelector(IEnumerable<int> source) => EnsureNonEmpty(source).MaxBy(static value => value);
    internal static object? Outer_M053_Min_int(IEnumerable<int> source) => EnsureNonEmpty(source).Min();
    internal static object? Outer_M054_Min_selector(IEnumerable<int> source) => EnsureNonEmpty(source).Min(static value => value);
    internal static object? Outer_M055_MinBy_keySelector(IEnumerable<int> source) => EnsureNonEmpty(source).MinBy(static value => value);
    internal static object? Outer_M056_OfType(IEnumerable<int> source) => SystemEnumerable.Select(source,static value => (object)value).OfType<int>();
    internal static object? Outer_M057_Order(IEnumerable<int> source) => source.Order();
    internal static object? Outer_M058_OrderBy_keySelector(IEnumerable<int> source) => source.OrderBy(static value => value);
    internal static object? Outer_M059_OrderByDescending_keySelector(IEnumerable<int> source) => source.OrderByDescending(static value => value);
    internal static object? Outer_M060_OrderDescending(IEnumerable<int> source) => source.OrderDescending();
    internal static object? Outer_M061_Prepend_element(IEnumerable<int> source) => source.Prepend(0);
    internal static object? Outer_M062_Range_start_count(IEnumerable<int> source) => SystemEnumerable.Range(1,4);
    internal static object? Outer_M063_Repeat_element_count(IEnumerable<int> source) => SystemEnumerable.Repeat(2,3);
    internal static object? Outer_M064_Reverse(IEnumerable<int> source) => source.Reverse();
    internal static object? Outer_M065_RightJoin_keySelectors_resultSelector(IEnumerable<int> source) => source.RightJoin(Second(),static value => value,static value => value,static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M066_Select_selector(IEnumerable<int> source) => source.Select(static value => value*2);
    internal static object? Outer_M067_Select_selector_index(IEnumerable<int> source) => source.Select(static (value,index) => value+index);
    internal static object? Outer_M068_Select_selector_Query(IEnumerable<int> source) => source.Select(static value => value*2);
    internal static object? Outer_M069_SelectMany_selector(IEnumerable<int> source) => source.SelectMany(static value => new[] { value,value+10 });
    internal static object? Outer_M070_SelectMany_selector_index(IEnumerable<int> source) => source.SelectMany(static (value,index) => new[] { value,index });
    internal static object? Outer_M071_SelectMany_collectionSelector_resultSelector(IEnumerable<int> source) => source.SelectMany(static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M072_SelectMany_collectionSelector_resultSelector_Query(IEnumerable<int> source) => source.SelectMany(static outerValue => Second(),static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M073_SequenceEqual_second(IEnumerable<int> source) => source.SequenceEqual(source);
    internal static object? Outer_M074_Single(IEnumerable<int> source) => EnsureSingle(source).Single();
    internal static object? Outer_M075_Single_predicate(IEnumerable<int> source) => new[] { 2 }.Single(static value => value==2);
    internal static object? Outer_M076_SingleOrDefault(IEnumerable<int> source) => EnsureSingle(source).SingleOrDefault();
    internal static object? Outer_M077_SingleOrDefault_predicate_defaultValue(IEnumerable<int> source) => source.SingleOrDefault(static value => value==9,-1);
    internal static object? Outer_M078_Skip_count(IEnumerable<int> source) => source.Skip(1);
    internal static object? Outer_M079_SkipLast_count(IEnumerable<int> source) => source.SkipLast(1);
    internal static object? Outer_M080_SkipWhile_predicate(IEnumerable<int> source) => source.SkipWhile(static value => value<3);
    internal static object? Outer_M081_SkipWhile_predicate_index(IEnumerable<int> source) => source.SkipWhile(static (value,index) => value<3&&index<2);
    internal static object? Outer_M082_Sum_int(IEnumerable<int> source) => source.Sum();
    internal static object? Outer_M083_Sum_selector(IEnumerable<int> source) => source.Sum(static value => value);
    internal static object? Outer_M084_Take_count(IEnumerable<int> source) => source.Take(3);
    internal static object? Outer_M085_Take_range(IEnumerable<int> source) => source.Take(1..3);
    internal static object? Outer_M086_TakeLast_count(IEnumerable<int> source) => source.TakeLast(2);
    internal static object? Outer_M087_TakeWhile_predicate(IEnumerable<int> source) => source.TakeWhile(static value => value<4);
    internal static object? Outer_M088_TakeWhile_predicate_index(IEnumerable<int> source) => source.TakeWhile(static (value,index) => value<4&&index<3);
    internal static object? Outer_M089_ThenBy_keySelector(IEnumerable<int> source) => source.OrderBy(static value => value).ThenBy(static value => value);
    internal static object? Outer_M090_ThenByDescending_keySelector(IEnumerable<int> source) => source.OrderBy(static value => value).ThenByDescending(static value => value);
    internal static object? Outer_M091_ToArray(IEnumerable<int> source) => source.ToArray();
    internal static object? Outer_M092_ToDictionary_keySelector(IEnumerable<int> source) => SystemEnumerable.Select(source,static (value,index) => new KeyValuePair<int,int>(index,value)).ToDictionary(static pair => pair.Key,static pair => pair.Value);
    internal static object? Outer_M093_ToHashSet(IEnumerable<int> source) => source.ToHashSet();
    internal static object? Outer_M094_ToList(IEnumerable<int> source) => source.ToList();
    internal static object? Outer_M095_ToLookup_keySelector(IEnumerable<int> source) => source.ToLookup(static value => value%2);
    internal static object? Outer_M096_TryGetNonEnumeratedCount_source(IEnumerable<int> source) => TryGetNonEnumeratedCountResult_Extension(source);
    internal static object? Outer_M097_Union_second(IEnumerable<int> source) => source.Union(Second());
    internal static object? Outer_M098_UnionBy_second_keySelector(IEnumerable<int> source) => source.UnionBy(Second(),static value => value);
    internal static object? Outer_M099_Where_predicate(IEnumerable<int> source) => source.Where(static value => value>2);
    internal static object? Outer_M100_Where_predicate_index(IEnumerable<int> source) => source.Where(static (value,index) => value>index);
    internal static object? Outer_M101_Where_predicate_Query(IEnumerable<int> source) => source.Where(static value => value>2);
    internal static object? Outer_M102_Zip_second(IEnumerable<int> source) => source.Zip(Second());
    internal static object? Outer_M103_Zip_second_resultSelector(IEnumerable<int> source) => source.Zip(Second(),static (outerValue,innerValue) => outerValue+innerValue);
    internal static object? Outer_M104_Zip_second_third(IEnumerable<int> source) => source.Zip(Second(),Third());
}
