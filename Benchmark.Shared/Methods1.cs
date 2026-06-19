namespace Labo.Shared.Linq.Enumerable;

#if LABO_TSAVORITE
public abstract class Methods1 : global::Benchmark.Shared.TsavoriteBase {
#else
public abstract class Methods1 : global::Benchmark.Shared.Base {
#endif
    private const int Any_iterationCount=800_000;
    private const int Contains_value_iterationCount=3;
#if BENCH_AS_TEST
    private const int ElementAt_index_iterationCount=1;
    private const int ElementAt_index_index=500;
#else
    private const int ElementAt_index_iterationCount=3;
    private const int ElementAt_index_index=500_000;
#endif
    private const int Empty_iterationCount=25_000;
    private const int First_iterationCount=5_000;
    private const int FirstOrDefault_iterationCount=4_000;
    private const int Single_iterationCount=6_000;
    private const int SingleOrDefault_iterationCount=4_500;
    private const int TryGetNonEnumeratedCount_iterationCount=2_000;
    // [Aggregate_func]
    [BenchFact,Benchmark]
    public void Aggregate_func() => this.Aggregate_func_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Aggregate_func_Impl() 
{
        var result=this.src(1).AsX<int>().Aggregate((acc,x)=>acc+x);
        this.Result=result;
        this.consumer.Consume(result);
    }
    // [Aggregate_seed_func]
    [BenchFact,Benchmark]
    public void Aggregate_seed_func() => this.Aggregate_seed_func_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Aggregate_seed_func_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Aggregate(0L,(acc,x)=>acc+x));
    // [Aggregate_seed_func_resultSelector]
    [BenchFact,Benchmark]
    public void Aggregate_seed_func_resultSelector() => this.Aggregate_seed_func_resultSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Aggregate_seed_func_resultSelector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Aggregate(0L,(acc,x)=>acc+x,acc=>(double)acc/1000000));
    // [AggregateBy_keySelector]
    [BenchFact,Benchmark]
    public void AggregateBy_keySelector() => this.AggregateBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void AggregateBy_keySelector_Impl() => this.src(1).AsX<int>().AggregateBy(x=>x%1000,0,(acc,x)=>acc+x).Consume(this.consumer);
    // [All_predicate]
    [BenchFact,Benchmark]
    public void All_predicate() => this.All_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void All_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().All(x=>x>0));
    // [Any]
    [BenchFact,Benchmark]
    public void Any() => this.Any_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Any_Impl() 
{
        for(var a=0;a<Any_iterationCount;a++)
        this.consumer.Consume(this.src(1).AsX<int>().Any());
    }
    // [Any_predicate]
    [BenchFact,Benchmark]
    public void Any_predicate() => this.Any_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Any_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Any(x=>x>999_999));
    // [Append_element]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Append_element() => this.Append_element_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Append_element_Impl() => this.src(1).AsX<int>().Append(0).Consume(this.consumer);
    // [Average]
    [BenchFact,Benchmark]
    public void Average() => this.Average_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Average_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Average());
    // [Average_selector]
    [BenchFact,Benchmark]
    public void Average_selector() => this.Average_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Average_selector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Average(x=>(double)x));
    // [Cast]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Cast() => this.Cast_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Cast_Impl() => this.src(1).AsX<int>().Cast<int>().Consume(this.consumer);
    // [Chunk_size]
    [BenchFact,Benchmark]
    public void Chunk_size() => this.Chunk_size_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Chunk_size_Impl() => this.src(1).AsX<int>().Chunk(1000).Consume(this.consumer);
    // [Concat_second]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Concat_second() => this.Concat_second_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Concat_second_Impl() => this.src(1).AsX<int>().Concat(this.src2(1)).Consume(this.consumer);
    // [Contains_value]
    [BenchFact,Benchmark]
    public void Contains_value() => this.Contains_value_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Contains_value_Impl() 
{
        const int value=500_000;
        for(var i=0;i<Contains_value_iterationCount;i++){
            this.consumer.Consume(this.src(1).AsX<int>().Contains(value));
        }
    }
    // [Count]
    [BenchFact,Benchmark]
    public void Count() => this.Count_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Count_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Count());
    // [Count_predicate]
    [BenchFact,Benchmark]
    public void Count_predicate() => this.Count_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Count_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Count(x=>(x&1)==0));
    // [CountBy_keySelector]
    [BenchFact,Benchmark]
    public void CountBy_keySelector() => this.CountBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void CountBy_keySelector_Impl() => this.src(1).AsX<int>().CountBy(x=>x%1000).Consume(this.consumer);
    // [DefaultIfEmpty]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void DefaultIfEmpty() => this.DefaultIfEmpty_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void DefaultIfEmpty_Impl() => this.src(1).AsX<int>().DefaultIfEmpty(0).Consume(this.consumer);
    // [Distinct]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Distinct() => this.Distinct_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Distinct_Impl() => this.src(1).AsX<int>().Distinct().Consume(this.consumer);
    // [DistinctBy_keySelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void DistinctBy_keySelector() => this.DistinctBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void DistinctBy_keySelector_Impl() => this.src(1).AsX<int>().DistinctBy(x=>x%1000).Consume(this.consumer);
    // [ElementAt_index]
    [BenchFact,Benchmark]
    public void ElementAt_index() => this.ElementAt_index_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ElementAt_index_Impl(){
        for(var i=0;i<ElementAt_index_iterationCount;i++){
            this.consumer.Consume(this.src(1).AsX<int>().ElementAt(ElementAt_index_index));
        }
    }
    // [ElementAtOrDefault_index]
    [BenchFact,Benchmark]
    public void ElementAtOrDefault_index() => this.ElementAtOrDefault_index_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ElementAtOrDefault_index_Impl() => this.consumer.Consume(this.src(1).AsX<int>().ElementAtOrDefault(500_000));
    // [Empty]
    [BenchFact,Benchmark]
    public void Empty() => this.Empty_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Empty_Impl() 
{
        for(var i=0;i<Empty_iterationCount;i++){
            global::System.Linq.Enumerable.Empty<int>().Consume(this.consumer);
        }
    }
    // [Except_second]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Except_second() => this.Except_second_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Except_second_Impl() => this.src(1).AsX<int>().Except(this.src2(1)).Consume(this.consumer);
    // [ExceptBy_keySelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ExceptBy_keySelector() => this.ExceptBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ExceptBy_keySelector_Impl() => this.src(1).AsX<int>().ExceptBy(this.src2(1),x=>x).Consume(this.consumer);
    // [First]
    [BenchFact,Benchmark]
    public void First() => this.First_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void First_Impl() 
{
        for(var i=0;i<First_iterationCount;i++){
            this.consumer.Consume(this.src(1).AsX<int>().First());
        }
    }
    // [FirstOrDefault]
    [BenchFact,Benchmark]
    public void FirstOrDefault() => this.FirstOrDefault_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void FirstOrDefault_Impl() 
{
        for(var i=0;i<FirstOrDefault_iterationCount;i++){
            this.consumer.Consume(this.src(1).AsX<int>().FirstOrDefault());
        }
    }
    // [FirstOrDefault_predicate]
    [BenchFact,Benchmark]
    public void FirstOrDefault_predicate() => this.FirstOrDefault_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void FirstOrDefault_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().FirstOrDefault(x=>x>500_000));
    // [GroupBy_elementSelector]
    [BenchFact,Benchmark]
    public void GroupBy_elementSelector() => this.GroupBy_elementSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void GroupBy_elementSelector_Impl() => this.src(1).AsX<int>().GroupBy(x=>x%1000,x=>x*2).Consume(this.consumer);
    // [GroupBy_elementSelector_resultSelector]
    [BenchFact,Benchmark]
    public void GroupBy_elementSelector_resultSelector() => this.GroupBy_elementSelector_resultSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void GroupBy_elementSelector_resultSelector_Impl() => this.src(1).AsX<int>().GroupBy(x=>x%1000,x=>x*2,(key,g)=>key).Consume(this.consumer);
    // [GroupBy_keySelector]
    [BenchFact,Benchmark]
    public void GroupBy_keySelector() => this.GroupBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void GroupBy_keySelector_Impl() => this.src(1).AsX<int>().GroupBy(x=>x%1000).Consume(this.consumer);
    // [GroupBy_resultSelector]
    [BenchFact,Benchmark]
    public void GroupBy_resultSelector() => this.GroupBy_resultSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void GroupBy_resultSelector_Impl() => this.src(1).AsX<int>().GroupBy(x=>x%1000,(key,g)=>key).Consume(this.consumer);
    // [GroupJoin_inner]
    [BenchFact,Benchmark]
    public void GroupJoin_inner() => this.GroupJoin_inner_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void GroupJoin_inner_Impl() => this.src(1).AsX<int>().GroupJoin(this.src2(1),x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);
    // [Index]
    [BenchFact,Benchmark]
    public void Index() => this.Index_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Index_Impl() => this.src(1).AsX<int>().Index().Consume(this.consumer);
    // [Intersect_second]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Intersect_second() => this.Intersect_second_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Intersect_second_Impl() => this.src(1).AsX<int>().Intersect(this.src2(1)).Consume(this.consumer);
    // [IntersectBy_keySelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void IntersectBy_keySelector() => this.IntersectBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void IntersectBy_keySelector_Impl() => this.src(1).AsX<int>().IntersectBy(this.src2(1),x=>x).Consume(this.consumer);
    // [InvariantHoisting]
    [BenchFact,Benchmark]
    public void InvariantHoisting() => this.InvariantHoisting_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void InvariantHoisting_Impl() => this.src(1).AsX<int>().Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
    // [Join_inner]
    [BenchFact,Benchmark]
    public void Join_inner() => this.Join_inner_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Join_inner_Impl() => this.src(1).AsX<int>().Join(this.src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer);
    // [Join_innerKey_Hoisting]
    [BenchFact,Benchmark]
    public void Join_innerKey_Hoisting() => this.Join_innerKey_Hoisting_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Join_innerKey_Hoisting_Impl() => this.src(1).AsX<int>().Join(this.src2(1),o=>o%1000,i=>(i+HoistInvariant.Value())%1000,(o,i)=>o+i).Consume(this.consumer);
    // [Join_outerKey_Hoisting]
    [BenchFact,Benchmark]
    public void Join_outerKey_Hoisting() => this.Join_outerKey_Hoisting_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Join_outerKey_Hoisting_Impl() => this.src(1).AsX<int>().Join(this.src2(1),o=>(o+HoistInvariant.Value())%1000,i=>i%1000,(o,i)=>o+i).Consume(this.consumer);
    // [Join_resultSelector_Hoisting]
    [BenchFact,Benchmark]
    public void Join_resultSelector_Hoisting() => this.Join_resultSelector_Hoisting_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Join_resultSelector_Hoisting_Impl() => this.src(1).AsX<int>().Join(this.src2(1),o => o%1000,i => i%1000,(o,i)=>o+i+HoistInvariant.Value()).Consume(this.consumer);
    // [Last]
    [BenchFact,Benchmark]
    public void Last() => this.Last_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Last_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Last());
    // [LastOrDefault]
    [BenchFact,Benchmark]
    public void LastOrDefault() => this.LastOrDefault_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void LastOrDefault_Impl() => this.consumer.Consume(this.src(1).AsX<int>().LastOrDefault());
    // [LastOrDefault_predicate]
    [BenchFact,Benchmark]
    public void LastOrDefault_predicate() => this.LastOrDefault_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void LastOrDefault_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().LastOrDefault(x=>x>500000));
    // [LongCount]
    [BenchFact,Benchmark]
    public void LongCount() => this.LongCount_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void LongCount_Impl() => this.consumer.Consume(this.src(1).AsX<int>().LongCount());
    // [LongCount_predicate]
    [BenchFact,Benchmark]
    public void LongCount_predicate() => this.LongCount_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void LongCount_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().LongCount(x=>(x&1)==0));
    // [Max]
    [BenchFact,Benchmark]
    public void Max() => this.Max_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Max_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Max());
    // [Max_selector]
    [BenchFact,Benchmark]
    public void Max_selector() => this.Max_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Max_selector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Max(x=>x*2));
    // [MaxBy_keySelector]
    [BenchFact,Benchmark]
    public void MaxBy_keySelector() => this.MaxBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void MaxBy_keySelector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().MaxBy(x=>x%1000));
    // [Min]
    [BenchFact,Benchmark]
    public void Min() => this.Min_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Min_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Min());
    // [Min_selector]
    [BenchFact,Benchmark]
    public void Min_selector() => this.Min_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Min_selector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Min(x=>x*2));
    // [MinBy_keySelector]
    [BenchFact,Benchmark]
    public void MinBy_keySelector() => this.MinBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void MinBy_keySelector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().MinBy(x=>x%1000));
    // [OfType]
    [BenchFact,Benchmark]
    public void OfType() => this.OfType_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void OfType_Impl() => this.src(1).AsX<int>().OfType<int>().Consume(this.consumer);
    // [Order]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Order() => this.Order_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Order_Impl() => this.src(1).AsX<int>().Order().Consume(this.consumer);
    // [OrderBy_keySelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderBy_keySelector() => this.OrderBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void OrderBy_keySelector_Impl() => this.src(1).AsX<int>().OrderBy(x=>x).Consume(this.consumer);
    // [OrderByDescending_keySelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderByDescending_keySelector() => this.OrderByDescending_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void OrderByDescending_keySelector_Impl() => this.src(1).AsX<int>().OrderByDescending(x=>x).Consume(this.consumer);
    // [OrderDescending]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderDescending() => this.OrderDescending_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void OrderDescending_Impl() => this.src(1).AsX<int>().OrderDescending().Consume(this.consumer);
    // [Prepend_element]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Prepend_element() => this.Prepend_element_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Prepend_element_Impl() => this.src(1).AsX<int>().Prepend(0).Consume(this.consumer);
    // [Range]
    [BenchFact,Benchmark]
    public void Range() => this.Range_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Range_Impl() => global::System.Linq.Enumerable.Range(1,1_000_000).Consume(this.consumer);
    // [Repeat]
    [BenchFact,Benchmark]
    public void Repeat() => this.Repeat_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Repeat_Impl() => global::System.Linq.Enumerable.Repeat(7,1_000_000).Consume(this.consumer);
    // [Reverse]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Reverse() => this.Reverse_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Reverse_Impl() => this.src(1).AsX<int>().Reverse().Consume(this.consumer);
    // [Select_indexSelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select_indexSelector() => this.Select_indexSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Select_indexSelector_Impl() => this.src(1).AsX<int>().Select((x,i)=>x+i).Consume(this.consumer);
    // [Select_selector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select_selector() => this.Select_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Select_selector_Impl() => this.src(1).AsX<int>().Select(x=>x+x).Consume(this.consumer);
    // [SelectMany_Hoisting_PredicatePushdown]
    [BenchFact,Benchmark]
    public void SelectMany_Hoisting_PredicatePushdown() => this.SelectMany_Hoisting_PredicatePushdown_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SelectMany_Hoisting_PredicatePushdown_Impl() => this.outer.AsX<int>().SelectMany(_=>this.inner).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);
    // [SelectMany_indexSelector]
    [BenchFact,Benchmark]
    public void SelectMany_indexSelector() => this.SelectMany_indexSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SelectMany_indexSelector_Impl() => this.srcNested(1).AsX<IEnumerable<int>>().SelectMany((x,i)=>x).Consume(this.consumer);
    // [SelectMany_indexSelector_resultSelector]
    [BenchFact,Benchmark]
    public void SelectMany_indexSelector_resultSelector() => this.SelectMany_indexSelector_resultSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SelectMany_indexSelector_resultSelector_Impl() => this.srcNested(1).AsX<IEnumerable<int>>().SelectMany((x,i)=>x,(outer,inner)=>inner+1).Consume(this.consumer);
    // [SelectMany_resultSelector]
    [BenchFact,Benchmark]
    public void SelectMany_resultSelector() => this.SelectMany_resultSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SelectMany_resultSelector_Impl() => this.srcNested(1).AsX<IEnumerable<int>>().SelectMany(x=>x,(outer,inner)=>inner+1).Consume(this.consumer);
    // [SelectMany_selector]
    [BenchFact,Benchmark]
    public void SelectMany_selector() => this.SelectMany_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SelectMany_selector_Impl() => this.srcNested(1).AsX<IEnumerable<int>>().SelectMany(x=>x).Consume(this.consumer);
    // [SelectManyPredicatePushdown]
    [BenchFact,Benchmark]
    public void SelectManyPredicatePushdown() => this.SelectManyPredicatePushdown_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SelectManyPredicatePushdown_Impl() => this.outer.AsX<int>().SelectMany(_=>this.inner).Where(x=>x>Threshold).Consume(this.consumer);
    // [SequenceEqual_second]
    [BenchFact,Benchmark]
    public void SequenceEqual_second() => this.SequenceEqual_second_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SequenceEqual_second_Impl() => this.consumer.Consume(this.src(1).AsX<int>().SequenceEqual(this.src(1)));
    // [Single]
    [BenchFact,Benchmark]
    public void Single() => this.Single_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Single_Impl() 
{
        for(var i=0;i<Single_iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).Single());
        }
    }
    // [Single_predicate]
    [BenchFact,Benchmark]
    public void Single_predicate() => this.Single_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Single_predicate_Impl() 
{
        var y= this.srcOne(1).ToArray();
        var x=y.Single(x => x==0);
        this.consumer.Consume(this.src(1).AsX<int>().Single(x=>x==1));
    }
    // [SingleOrDefault]
    [BenchFact,Benchmark]
    public void SingleOrDefault() => this.SingleOrDefault_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SingleOrDefault_Impl() 
{
        for(var i=0;i<SingleOrDefault_iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).SingleOrDefault());
        }
    }
    // [SingleOrDefault_predicate]
    [BenchFact,Benchmark]
    public void SingleOrDefault_predicate() => this.SingleOrDefault_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SingleOrDefault_predicate_Impl() => this.consumer.Consume(this.src(1).AsX<int>().SingleOrDefault(x=>x==1));
    // [Skip_count]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Skip_count() => this.Skip_count_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Skip_count_Impl() => this.src(1).AsX<int>().Skip(250000).Consume(this.consumer);
    // [SkipLast_count]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipLast_count() => this.SkipLast_count_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SkipLast_count_Impl() => this.src(1).AsX<int>().SkipLast(250000).Consume(this.consumer);
    // [SkipWhile_indexPredicate]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipWhile_indexPredicate() => this.SkipWhile_indexPredicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SkipWhile_indexPredicate_Impl() => this.src(1).AsX<int>().SkipWhile((x,i)=>i<250000).Consume(this.consumer);
    // [SkipWhile_predicate]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipWhile_predicate() => this.SkipWhile_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void SkipWhile_predicate_Impl() => this.src(1).AsX<int>().SkipWhile(x=>x<250001).Consume(this.consumer);
    // [Sum]
    [BenchFact,Benchmark]
    public void Sum() => this.Sum_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Sum_Impl() => this.consumer.Consume(this.srcMinusPlus(1).AsX<int>().Sum());
    // [Sum_selector]
    [BenchFact,Benchmark]
    public void Sum_selector() => this.Sum_selector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Sum_selector_Impl() => this.consumer.Consume(this.src(1).AsX<int>().Sum(x=>(long)x));
    // [Take_count]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Take_count() => this.Take_count_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Take_count_Impl() => this.src(1).AsX<int>().Take(750_000).Consume(this.consumer);
    // [TakeLast_count]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeLast_count() => this.TakeLast_count_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void TakeLast_count_Impl() => this.src(1).AsX<int>().TakeLast(250000).Consume(this.consumer);
    // [TakeWhile_indexPredicate]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeWhile_indexPredicate() => this.TakeWhile_indexPredicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void TakeWhile_indexPredicate_Impl() => this.src(1).AsX<int>().TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    // [TakeWhile_predicate]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeWhile_predicate() => this.TakeWhile_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void TakeWhile_predicate_Impl() => this.src(1).AsX<int>().TakeWhile(x=>x<750001).Consume(this.consumer);
    // [ThenBy_keySelector]
    [BenchFact,Benchmark]
    public void ThenBy_keySelector() => this.ThenBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ThenBy_keySelector_Impl() => this.src(1).AsX<int>().OrderBy(x=>x%1000).ThenBy(x=>x).Consume(this.consumer);
    // [ThenByDescending_keySelector]
    [BenchFact,Benchmark]
    public void ThenByDescending_keySelector() => this.ThenByDescending_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ThenByDescending_keySelector_Impl() => this.src(1).AsX<int>().OrderBy(x=>x%1000).ThenByDescending(x=>x).Consume(this.consumer);
    // [ToArray]
    [BenchFact,Benchmark]
    public void ToArray() => this.ToArray_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToArray_Impl() => this.src(1).AsX<int>().ToArray().Consume(this.consumer);
    // [ToDictionary_elementSelector]
    [BenchFact,Benchmark]
    public void ToDictionary_elementSelector() => this.ToDictionary_elementSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToDictionary_elementSelector_Impl() => this.src(1).AsX<int>().ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    // [ToDictionary_keySelector]
    [BenchFact,Benchmark]
    public void ToDictionary_keySelector() => this.ToDictionary_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToDictionary_keySelector_Impl() => this.src(1).AsX<int>().ToDictionary(x=>x).Consume(this.consumer);
    // [ToHashSet]
    [BenchFact,Benchmark]
    public void ToHashSet() => this.ToHashSet_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToHashSet_Impl() => this.src(1).AsX<int>().ToHashSet().Consume(this.consumer);
    // [ToList]
    [BenchFact,Benchmark]
    public void ToList() => this.ToList_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToList_Impl() => this.src(1).AsX<int>().ToList().Consume(this.consumer);
    // [ToLookup_elementSelector]
    [BenchFact,Benchmark]
    public void ToLookup_elementSelector() => this.ToLookup_elementSelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToLookup_elementSelector_Impl() => this.src(1).AsX<int>().ToLookup(x=>x%1000,x=>x*2).Consume(this.consumer);
    // [ToLookup_keySelector]
    [BenchFact,Benchmark]
    public void ToLookup_keySelector() => this.ToLookup_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void ToLookup_keySelector_Impl() => this.src(1).AsX<int>().ToLookup(x=>x%1000).Consume(this.consumer);
    // [TryGetNonEnumeratedCount]
    [BenchFact,Benchmark]
    public void TryGetNonEnumeratedCount() => this.TryGetNonEnumeratedCount_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void TryGetNonEnumeratedCount_Impl() 
{
        for(var i=0;i<TryGetNonEnumeratedCount_iterationCount;i++){
            ConsumeResult(this.consumer,global::System.Linq.Enumerable.TryGetNonEnumeratedCount(this.src(1),out var count),count);
        }
    }
    // [Union_second]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Union_second() => this.Union_second_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Union_second_Impl() => this.src(1).AsX<int>().Union(this.src2(1)).Consume(this.consumer);
    // [UnionBy_keySelector]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void UnionBy_keySelector() => this.UnionBy_keySelector_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void UnionBy_keySelector_Impl() => this.src(1).AsX<int>().UnionBy(this.src2(1),x=>x).Consume(this.consumer);
    // [Where_indexPredicate]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Where_indexPredicate() => this.Where_indexPredicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Where_indexPredicate_Impl() => this.src(1).AsX<int>().Where((x,i)=>i%2==0).Consume(this.consumer);
    // [Where_predicate]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Where_predicate() => this.Where_predicate_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Where_predicate_Impl() => this.src(1).AsX<int>().Where(x=>(x&1)==0).Consume(this.consumer);
    // [Where_SourceShape]
    [BenchFact,Benchmark]
    [BenchmarkCategory("Array")]
    public void Where_SourceShape() => this.Where_SourceShape_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Where_SourceShape_Impl() => this.array.Where(static x=>(x&1)==0).Consume(this.consumer);
    // [Zip_second]
    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Zip_second() => this.Zip_second_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Zip_second_Impl() => this.src(1).AsX<int>().Zip(this.src2(1),(a,b)=>a+b).Consume(this.consumer);
    // [Zip_tuple]
    [BenchFact,Benchmark]
    public void Zip_tuple() => this.Zip_tuple_Impl();

    [LaboLinqInterceptAttribute(true)]
    private void Zip_tuple_Impl() => this.src(1).AsX<int>().Zip(this.src2(1)).Consume(this.consumer);

    private readonly int[] outer = global::System.Linq.Enumerable.Range(0,30_000).ToArray();
    private readonly int[] inner = global::System.Linq.Enumerable.Range(0,128).ToArray();
    private const int Threshold = 120;

    private int[] array = [];
    private global::System.Collections.Generic.List<int> list = [];
    private int sourceCount;

    public Methods1(int count):base(count) {
        this.Initialize();
    }

    private void Initialize() {
        this.sourceCount=this.CountForTestTiming(1_000_000);
        this.array=global::System.Linq.Enumerable.Range(1,this.sourceCount).ToArray();
        this.list=this.array.ToList();
    }

    private static void ConsumeResult(global::Benchmark.Shared.Consumer consumer,bool result,int count){
        consumer.Consume(result);
        if(result){
            consumer.Consume(count);
        } else {
            consumer.Consume(-1);
        }
    }

    private global::System.Collections.Generic.IEnumerable<int> YieldRange() {
        for(var i = 1;i<=this.sourceCount;i++) {
            yield return i;
        }
    }
}





