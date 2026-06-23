namespace Shared.Linq.Enumerable;

#if TSAVORITE
public abstract class Methods1 : TsavoriteBase {
#elif PERSISTED
public abstract class Methods1 : PersistedBase {
#else
public abstract class Methods1:Base {
#endif
#if BENCH_AS_TEST
    private const int Any_iterationCount=100;
    private const int Empty_iterationCount=100;
    private const int First_iterationCount=100;
    private const int FirstOrDefault_iterationCount=100;
    private const int Single_iterationCount=100;
    private const int SingleOrDefault_iterationCount=100;
    private const int TryGetNonEnumeratedCount_iterationCount=100;
    private const int ElementAt_index_iterationCount=1;
    private const int ElementAt_index_index=500;
#else
    private const int Any_iterationCount = 800_000;
    private const int Empty_iterationCount = 25_000;
    private const int First_iterationCount = 5_000;
    private const int FirstOrDefault_iterationCount = 4_000;
    private const int Single_iterationCount = 6_000;
    private const int SingleOrDefault_iterationCount = 4_500;
    private const int TryGetNonEnumeratedCount_iterationCount = 2_000;
    private const int ElementAt_index_iterationCount = 3;
    private const int ElementAt_index_index = 500_000;
#endif
    private const int Contains_value_iterationCount = 3;
    private const int ImmediateShortCircuitCount = 96_000_000;
    private const int ImmediateFullScanCount = 96_000_000;
    private const int ImmediatePredicateScanCount = 96_000_000;
    private const int StreamOneToOneCount = 36_000_000;
    private const int StreamFilterCount = 36_000_000;
    private const int StreamPartitionCount = 36_000_000;
    private const int StreamShapeCount = 36_000_000;
    private const int MaterializeLinearCount = 48_000_000;
    private const int MaterializeHashCount = 24_000_000;
    private const int MaterializeLookupCount = 32_000_000;
    private const int SetCount = 19_000_000;
    private const int GroupingCount = 28_000_000;
    private const int JoinCount = 320_000;
    private const int SortCount = 1_700_000;
    private const int NestedCount = 24_000_000;
    private const int GeneratedCount = 104_400_000;
    private const int SourceShapeCount = 60_000_000;
    private const int SelectManyOuterCount = 12_000;
    private const int SelectManyInnerCount = 1_280;
    // [Aggregate_func]
    [BenchFact, Benchmark]
    public void Aggregate_func() => Aggregate_func_Impl();

    [LinqIntercept(true)]
    private void Aggregate_func_Impl() {
        var result = srcCount(ImmediateFullScanCount).AsX<int>().Aggregate((acc,x) => acc+x);
        Result=result;
        consumer.Consume(result);
    }
    // [Aggregate_seed_func]
    [BenchFact, Benchmark]
    public void Aggregate_seed_func() => Aggregate_seed_func_Impl();

    [LinqIntercept(true)]
    private void Aggregate_seed_func_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Aggregate(0L,(acc,x) => acc+x));
    // [Aggregate_seed_func_resultSelector]
    [BenchFact, Benchmark]
    public void Aggregate_seed_func_resultSelector() => Aggregate_seed_func_resultSelector_Impl();

    [LinqIntercept(true)]
    private void Aggregate_seed_func_resultSelector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Aggregate(0L,(acc,x) => acc+x,acc => (double)acc/1000000));
    // [AggregateBy_keySelector]
    [BenchFact, Benchmark]
    public void AggregateBy_keySelector() => AggregateBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void AggregateBy_keySelector_Impl() => srcCount(GroupingCount).AsX<int>().AggregateBy(x => x%1000,0,(acc,x) => acc+x).Consume(consumer);
    // [All_predicate]
    [BenchFact, Benchmark]
    public void All_predicate() => All_predicate_Impl();

    [LinqIntercept(true)]
    private void All_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().All(x => x>0));
    // [Any]
    [BenchFact, Benchmark]
    public void Any() => Any_Impl();

    [LinqIntercept(true)]
    private void Any_Impl() {
        for(var a = 0; a<Any_iterationCount; a++)
            consumer.Consume(srcCount(ImmediateShortCircuitCount).AsX<int>().Any());
    }
    // [Any_predicate]
    [BenchFact, Benchmark]
    public void Any_predicate() => Any_predicate_Impl();

    [LinqIntercept(true)]
    private void Any_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().Any(x => x>999_999));
    // [Append_element]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Append_element() => Append_element_Impl();

    [LinqIntercept(true)]
    private void Append_element_Impl() => srcCount(StreamShapeCount).AsX<int>().Append(0).Consume(consumer);
    // [Average]
    [BenchFact, Benchmark]
    public void Average() => Average_Impl();

    [LinqIntercept(true)]
    private void Average_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Average());
    // [Average_selector]
    [BenchFact, Benchmark]
    public void Average_selector() => Average_selector_Impl();

    [LinqIntercept(true)]
    private void Average_selector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Average(x => (double)x));
    // [Cast]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Cast() => Cast_Impl();

    [LinqIntercept(true)]
    private void Cast_Impl() => srcCount(StreamOneToOneCount).AsX<int>().Cast<int>().Consume(consumer);
    // [Chunk_size]
    [BenchFact, Benchmark]
    public void Chunk_size() => Chunk_size_Impl();

    [LinqIntercept(true)]
    private void Chunk_size_Impl() => srcCount(StreamPartitionCount).AsX<int>().Chunk(1000).Consume(consumer);
    // [Concat_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Concat_second() => Concat_second_Impl();

    [LinqIntercept(true)]
    private void Concat_second_Impl() => srcCount(StreamShapeCount).AsX<int>().Concat(src2Count(StreamShapeCount)).Consume(consumer);
    // [Contains_value]
    [BenchFact, Benchmark]
    public void Contains_value() => Contains_value_Impl();

    [LinqIntercept(true)]
    private void Contains_value_Impl() {
        const int value = 500_000;
        for(var i = 0; i<Contains_value_iterationCount; i++) {
            consumer.Consume(srcCount(ImmediateShortCircuitCount).AsX<int>().Contains(value));
        }
    }
    // [Count]
    [BenchFact, Benchmark]
    public void Count() => Count_Impl();

    [LinqIntercept(true)]
    private void Count_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Count());
    // [Count_predicate]
    [BenchFact, Benchmark]
    public void Count_predicate() => Count_predicate_Impl();

    [LinqIntercept(true)]
    private void Count_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().Count(x => (x&1)==0));
    // [CountBy_keySelector]
    [BenchFact, Benchmark]
    public void CountBy_keySelector() => CountBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void CountBy_keySelector_Impl() => srcCount(GroupingCount).AsX<int>().CountBy(x => x%1000).Consume(consumer);
    // [DefaultIfEmpty]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void DefaultIfEmpty() => DefaultIfEmpty_Impl();

    [LinqIntercept(true)]
    private void DefaultIfEmpty_Impl() => srcCount(StreamShapeCount).AsX<int>().DefaultIfEmpty(0).Consume(consumer);
    // [Distinct]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Distinct() => Distinct_Impl();

    [LinqIntercept(true)]
    private void Distinct_Impl() => srcCount(SetCount).AsX<int>().Distinct().Consume(consumer);
    // [DistinctBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void DistinctBy_keySelector() => DistinctBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void DistinctBy_keySelector_Impl() => srcCount(SetCount).AsX<int>().DistinctBy(x => x%1000).Consume(consumer);
    // [ElementAt_index]
    [BenchFact, Benchmark]
    public void ElementAt_index() => ElementAt_index_Impl();

    [LinqIntercept(true)]
    private void ElementAt_index_Impl() {
        for(var i = 0; i<ElementAt_index_iterationCount; i++) {
            consumer.Consume(srcCount(ImmediateShortCircuitCount).AsX<int>().ElementAt(ElementAt_index_index));
        }
    }
    // [ElementAtOrDefault_index]
    [BenchFact, Benchmark]
    public void ElementAtOrDefault_index() => ElementAtOrDefault_index_Impl();

    [LinqIntercept(true)]
    private void ElementAtOrDefault_index_Impl() => consumer.Consume(srcCount(ImmediateShortCircuitCount).AsX<int>().ElementAtOrDefault(500_000));
    // [Empty]
    [BenchFact, Benchmark]
    public void Empty() => Empty_Impl();

    [LinqIntercept(true)]
    private void Empty_Impl() {
        for(var i = 0; i<Empty_iterationCount; i++) {
            global::System.Linq.Enumerable.Empty<int>().Consume(consumer);
        }
    }
    // [Except_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Except_second() => Except_second_Impl();

    [LinqIntercept(true)]
    private void Except_second_Impl() => srcCount(SetCount).AsX<int>().Except(src2Count(SetCount)).Consume(consumer);
    // [ExceptBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ExceptBy_keySelector() => ExceptBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void ExceptBy_keySelector_Impl() => srcCount(SetCount).AsX<int>().ExceptBy(src2Count(SetCount),x => x).Consume(consumer);
    // [First]
    [BenchFact, Benchmark]
    public void First() => First_Impl();

    [LinqIntercept(true)]
    private void First_Impl() {
        for(var i = 0; i<First_iterationCount; i++) {
            consumer.Consume(srcCount(ImmediateShortCircuitCount).AsX<int>().First());
        }
    }
    // [FirstOrDefault]
    [BenchFact, Benchmark]
    public void FirstOrDefault() => FirstOrDefault_Impl();

    [LinqIntercept(true)]
    private void FirstOrDefault_Impl() {
        for(var i = 0; i<FirstOrDefault_iterationCount; i++) {
            consumer.Consume(srcCount(ImmediateShortCircuitCount).AsX<int>().FirstOrDefault());
        }
    }
    // [FirstOrDefault_predicate]
    [BenchFact, Benchmark]
    public void FirstOrDefault_predicate() => FirstOrDefault_predicate_Impl();

    [LinqIntercept(true)]
    private void FirstOrDefault_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().FirstOrDefault(x => x>500_000));
    // [GroupBy_elementSelector]
    [BenchFact, Benchmark]
    public void GroupBy_elementSelector() => GroupBy_elementSelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_elementSelector_Impl() => srcCount(GroupingCount).AsX<int>().GroupBy(x => x%1000,x => x*2).Consume(consumer);
    // [GroupBy_elementSelector_resultSelector]
    [BenchFact, Benchmark]
    public void GroupBy_elementSelector_resultSelector() => GroupBy_elementSelector_resultSelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_elementSelector_resultSelector_Impl() => srcCount(GroupingCount).AsX<int>().GroupBy(x => x%1000,x => x*2,(key,g) => key).Consume(consumer);
    // [GroupBy_keySelector]
    [BenchFact, Benchmark]
    public void GroupBy_keySelector() => GroupBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_keySelector_Impl() => srcCount(GroupingCount).AsX<int>().GroupBy(x => x%1000).Consume(consumer);
    // [GroupBy_resultSelector]
    [BenchFact, Benchmark]
    public void GroupBy_resultSelector() => GroupBy_resultSelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_resultSelector_Impl() => srcCount(GroupingCount).AsX<int>().GroupBy(x => x%1000,(key,g) => key).Consume(consumer);
    // [GroupJoin_inner]
    [BenchFact, Benchmark]
    public void GroupJoin_inner() => GroupJoin_inner_Impl();

    [LinqIntercept(true)]
    private void GroupJoin_inner_Impl() => srcCount(JoinCount).AsX<int>().GroupJoin(src2Count(JoinCount),x => x,x => x,(outer,inner) => outer).Consume(consumer);
    // [Index]
    [BenchFact, Benchmark]
    public void Index() => Index_Impl();

    [LinqIntercept(true)]
    private void Index_Impl() => srcCount(StreamOneToOneCount).AsX<int>().Index().Consume(consumer);
    // [Intersect_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Intersect_second() => Intersect_second_Impl();

    [LinqIntercept(true)]
    private void Intersect_second_Impl() => srcCount(SetCount).AsX<int>().Intersect(src2Count(SetCount)).Consume(consumer);
    // [IntersectBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void IntersectBy_keySelector() => IntersectBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void IntersectBy_keySelector_Impl() => srcCount(SetCount).AsX<int>().IntersectBy(src2Count(SetCount),x => x).Consume(consumer);
    // [InvariantHoisting]
    [BenchFact, Benchmark]
    public void InvariantHoisting() => InvariantHoisting_Impl();

    [LinqIntercept(true)]
    private void InvariantHoisting_Impl() => srcCount(StreamOneToOneCount).AsX<int>().Select(x => x+HoistInvariant.Value()).Consume(consumer);
    // [Join_inner]
    [BenchFact, Benchmark]
    public void Join_inner() => Join_inner_Impl();

    [LinqIntercept(true)]
    private void Join_inner_Impl() => srcCount(JoinCount).AsX<int>().Join(src2Count(JoinCount),x => x,x => x,(a,b) => a).Consume(consumer);
    // [Join_innerKey_Hoisting]
    [BenchFact, Benchmark]
    public void Join_innerKey_Hoisting() => Join_innerKey_Hoisting_Impl();

    [LinqIntercept(true)]
    private void Join_innerKey_Hoisting_Impl() => srcCount(JoinCount).AsX<int>().Join(src2Count(JoinCount),o => o%1000,i => (i+HoistInvariant.Value())%1000,(o,i) => o+i).Consume(consumer);
    // [Join_outerKey_Hoisting]
    [BenchFact, Benchmark]
    public void Join_outerKey_Hoisting() => Join_outerKey_Hoisting_Impl();

    [LinqIntercept(true)]
    private void Join_outerKey_Hoisting_Impl() => srcCount(JoinCount).AsX<int>().Join(src2Count(JoinCount),o => (o+HoistInvariant.Value())%1000,i => i%1000,(o,i) => o+i).Consume(consumer);
    // [Join_resultSelector_Hoisting]
    [BenchFact, Benchmark]
    public void Join_resultSelector_Hoisting() => Join_resultSelector_Hoisting_Impl();

    [LinqIntercept(true)]
    private void Join_resultSelector_Hoisting_Impl() => srcCount(JoinCount).AsX<int>().Join(src2Count(JoinCount),o => o%1000,i => i%1000,(o,i) => o+i+HoistInvariant.Value()).Consume(consumer);
    // [Last]
    [BenchFact, Benchmark]
    public void Last() => Last_Impl();

    [LinqIntercept(true)]
    private void Last_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Last());
    // [LastOrDefault]
    [BenchFact, Benchmark]
    public void LastOrDefault() => LastOrDefault_Impl();

    [LinqIntercept(true)]
    private void LastOrDefault_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().LastOrDefault());
    // [LastOrDefault_predicate]
    [BenchFact, Benchmark]
    public void LastOrDefault_predicate() => LastOrDefault_predicate_Impl();

    [LinqIntercept(true)]
    private void LastOrDefault_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().LastOrDefault(x => x>500000));
    // [LongCount]
    [BenchFact, Benchmark]
    public void LongCount() => LongCount_Impl();

    [LinqIntercept(true)]
    private void LongCount_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().LongCount());
    // [LongCount_predicate]
    [BenchFact, Benchmark]
    public void LongCount_predicate() => LongCount_predicate_Impl();

    [LinqIntercept(true)]
    private void LongCount_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().LongCount(x => (x&1)==0));
    // [Max]
    [BenchFact, Benchmark]
    public void Max() => Max_Impl();

    [LinqIntercept(true)]
    private void Max_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Max());
    // [Max_selector]
    [BenchFact, Benchmark]
    public void Max_selector() => Max_selector_Impl();

    [LinqIntercept(true)]
    private void Max_selector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Max(x => x*2));
    // [MaxBy_keySelector]
    [BenchFact, Benchmark]
    public void MaxBy_keySelector() => MaxBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void MaxBy_keySelector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().MaxBy(x => x%1000));
    // [Min]
    [BenchFact, Benchmark]
    public void Min() => Min_Impl();

    [LinqIntercept(true)]
    private void Min_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Min());
    // [Min_selector]
    [BenchFact, Benchmark]
    public void Min_selector() => Min_selector_Impl();

    [LinqIntercept(true)]
    private void Min_selector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Min(x => x*2));
    // [MinBy_keySelector]
    [BenchFact, Benchmark]
    public void MinBy_keySelector() => MinBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void MinBy_keySelector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().MinBy(x => x%1000));
    // [OfType]
    [BenchFact, Benchmark]
    public void OfType() => OfType_Impl();

    [LinqIntercept(true)]
    private void OfType_Impl() => srcCount(StreamOneToOneCount).AsX<int>().OfType<int>().Consume(consumer);
    // [Order]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Order() => Order_Impl();

    [LinqIntercept(true)]
    private void Order_Impl() => srcCount(SortCount).AsX<int>().Order().Consume(consumer);
    // [OrderBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderBy_keySelector() => OrderBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void OrderBy_keySelector_Impl() => srcCount(SortCount).AsX<int>().OrderBy(x => x).Consume(consumer);
    // [OrderByDescending_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderByDescending_keySelector() => OrderByDescending_keySelector_Impl();

    [LinqIntercept(true)]
    private void OrderByDescending_keySelector_Impl() => srcCount(SortCount).AsX<int>().OrderByDescending(x => x).Consume(consumer);
    // [OrderDescending]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderDescending() => OrderDescending_Impl();

    [LinqIntercept(true)]
    private void OrderDescending_Impl() => srcCount(SortCount).AsX<int>().OrderDescending().Consume(consumer);
    // [Prepend_element]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Prepend_element() => Prepend_element_Impl();

    [LinqIntercept(true)]
    private void Prepend_element_Impl() => srcCount(StreamShapeCount).AsX<int>().Prepend(0).Consume(consumer);
    // [Range]
    [BenchFact, Benchmark]
    public void Range() => Range_Impl();

    [LinqIntercept(true)]
    private void Range_Impl() => global::System.Linq.Enumerable.Range(1,CountForComparison(GeneratedCount)).Consume(consumer);
    // [Repeat]
    [BenchFact, Benchmark]
    public void Repeat() => Repeat_Impl();

    [LinqIntercept(true)]
    private void Repeat_Impl() => global::System.Linq.Enumerable.Repeat(7,CountForComparison(GeneratedCount)).Consume(consumer);
    // [Reverse]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Reverse() => Reverse_Impl();

    [LinqIntercept(true)]
    private void Reverse_Impl() => srcCount(StreamShapeCount).AsX<int>().Reverse().Consume(consumer);
    // [Select_indexSelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select_indexSelector() => Select_indexSelector_Impl();

    [LinqIntercept(true)]
    private void Select_indexSelector_Impl() => srcCount(StreamOneToOneCount).AsX<int>().Select((x,i) => x+i).Consume(consumer);
    // [Select_selector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select_selector() => Select_selector_Impl();

    [LinqIntercept(true)]
    private void Select_selector_Impl() => srcCount(StreamOneToOneCount).AsX<int>().Select(x => x+x).Consume(consumer);
    // [SelectMany_Hoisting_PredicatePushdown]
    [BenchFact, Benchmark]
    public void SelectMany_Hoisting_PredicatePushdown() => SelectMany_Hoisting_PredicatePushdown_Impl();

    [LinqIntercept(true)]
    private void SelectMany_Hoisting_PredicatePushdown_Impl() => outer.AsX<int>().SelectMany(_ => inner).Where(x => x>Threshold&&HoistInvariant.Value()==0).Consume(consumer);
    // [SelectMany_indexSelector]
    [BenchFact, Benchmark]
    public void SelectMany_indexSelector() => SelectMany_indexSelector_Impl();

    [LinqIntercept(true)]
    private void SelectMany_indexSelector_Impl() => srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany((x,i) => x).Consume(consumer);
    // [SelectMany_indexSelector_resultSelector]
    [BenchFact, Benchmark]
    public void SelectMany_indexSelector_resultSelector() => SelectMany_indexSelector_resultSelector_Impl();

    [LinqIntercept(true)]
    private void SelectMany_indexSelector_resultSelector_Impl() => srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany((x,i) => x,(outer,inner) => inner+1).Consume(consumer);
    // [SelectMany_resultSelector]
    [BenchFact, Benchmark]
    public void SelectMany_resultSelector() => SelectMany_resultSelector_Impl();

    [LinqIntercept(true)]
    private void SelectMany_resultSelector_Impl() => srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany(x => x,(outer,inner) => inner+1).Consume(consumer);
    // [SelectMany_selector]
    [BenchFact, Benchmark]
    public void SelectMany_selector() => SelectMany_selector_Impl();

    [LinqIntercept(true)]
    private void SelectMany_selector_Impl() => srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany(x => x).Consume(consumer);
    // [SelectManyPredicatePushdown]
    [BenchFact, Benchmark]
    public void SelectManyPredicatePushdown() => SelectManyPredicatePushdown_Impl();

    [LinqIntercept(true)]
    private void SelectManyPredicatePushdown_Impl() => outer.AsX<int>().SelectMany(_ => inner).Where(x => x>Threshold).Consume(consumer);
    // [SequenceEqual_second]
    [BenchFact, Benchmark]
    public void SequenceEqual_second() => SequenceEqual_second_Impl();

    [LinqIntercept(true)]
    private void SequenceEqual_second_Impl() => consumer.Consume(srcCount(StreamShapeCount).AsX<int>().SequenceEqual(srcCount(StreamShapeCount)));
    // [Single]
    [BenchFact, Benchmark]
    public void Single() => Single_Impl();

    [LinqIntercept(true)]
    private void Single_Impl() {
        for(var i = 0; i<Single_iterationCount; i++) {
            consumer.Consume(srcOne(1).Single());
        }
    }
    // [Single_predicate]
    [BenchFact, Benchmark]
    public void Single_predicate() => Single_predicate_Impl();

    [LinqIntercept(true)]
    private void Single_predicate_Impl() {
        var y = srcOne(1).ToArray();
        var x = y.Single(x => x==0);
        consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().Single(x => x==1));
    }
    // [SingleOrDefault]
    [BenchFact, Benchmark]
    public void SingleOrDefault() => SingleOrDefault_Impl();

    [LinqIntercept(true)]
    private void SingleOrDefault_Impl() {
        for(var i = 0; i<SingleOrDefault_iterationCount; i++) {
            consumer.Consume(srcOne(1).SingleOrDefault());
        }
    }
    // [SingleOrDefault_predicate]
    [BenchFact, Benchmark]
    public void SingleOrDefault_predicate() => SingleOrDefault_predicate_Impl();

    [LinqIntercept(true)]
    private void SingleOrDefault_predicate_Impl() => consumer.Consume(srcCount(ImmediatePredicateScanCount).AsX<int>().SingleOrDefault(x => x==1));
    // [Skip_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Skip_count() => Skip_count_Impl();

    [LinqIntercept(true)]
    private void Skip_count_Impl() => srcCount(StreamPartitionCount).AsX<int>().Skip(StreamPartitionCount/4).Consume(consumer);
    // [SkipLast_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipLast_count() => SkipLast_count_Impl();

    [LinqIntercept(true)]
    private void SkipLast_count_Impl() => srcCount(StreamPartitionCount).AsX<int>().SkipLast(StreamPartitionCount/4).Consume(consumer);
    // [SkipWhile_indexPredicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipWhile_indexPredicate() => SkipWhile_indexPredicate_Impl();

    [LinqIntercept(true)]
    private void SkipWhile_indexPredicate_Impl() => srcCount(StreamFilterCount).AsX<int>().SkipWhile((x,i) => i<StreamFilterCount/4).Consume(consumer);
    // [SkipWhile_predicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipWhile_predicate() => SkipWhile_predicate_Impl();

    [LinqIntercept(true)]
    private void SkipWhile_predicate_Impl() => srcCount(StreamFilterCount).AsX<int>().SkipWhile(x => x<StreamFilterCount/4+1).Consume(consumer);
    // [Sum]
    [BenchFact, Benchmark]
    public void Sum() => Sum_Impl();

    [LinqIntercept(true)]
    private void Sum_Impl() => consumer.Consume(srcMinusPlusCount(ImmediateFullScanCount).AsX<int>().Sum());
    // [Sum_selector]
    [BenchFact, Benchmark]
    public void Sum_selector() => Sum_selector_Impl();

    [LinqIntercept(true)]
    private void Sum_selector_Impl() => consumer.Consume(srcCount(ImmediateFullScanCount).AsX<int>().Sum(x => (long)x));
    // [Take_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Take_count() => Take_count_Impl();

    [LinqIntercept(true)]
    private void Take_count_Impl() => srcCount(StreamPartitionCount).AsX<int>().Take(StreamPartitionCount*3/4).Consume(consumer);
    // [TakeLast_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeLast_count() => TakeLast_count_Impl();

    [LinqIntercept(true)]
    private void TakeLast_count_Impl() => srcCount(StreamPartitionCount).AsX<int>().TakeLast(StreamPartitionCount/4).Consume(consumer);
    // [TakeWhile_indexPredicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeWhile_indexPredicate() => TakeWhile_indexPredicate_Impl();

    [LinqIntercept(true)]
    private void TakeWhile_indexPredicate_Impl() => srcCount(StreamFilterCount).AsX<int>().TakeWhile((x,i) => i<StreamFilterCount*3/4).Consume(consumer);
    // [TakeWhile_predicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeWhile_predicate() => TakeWhile_predicate_Impl();

    [LinqIntercept(true)]
    private void TakeWhile_predicate_Impl() => srcCount(StreamFilterCount).AsX<int>().TakeWhile(x => x<StreamFilterCount*3/4+1).Consume(consumer);
    // [ThenBy_keySelector]
    [BenchFact, Benchmark]
    public void ThenBy_keySelector() => ThenBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void ThenBy_keySelector_Impl() => srcCount(SortCount).AsX<int>().OrderBy(x => x%1000).ThenBy(x => x).Consume(consumer);
    // [ThenByDescending_keySelector]
    [BenchFact, Benchmark]
    public void ThenByDescending_keySelector() => ThenByDescending_keySelector_Impl();

    [LinqIntercept(true)]
    private void ThenByDescending_keySelector_Impl() => srcCount(SortCount).AsX<int>().OrderBy(x => x%1000).ThenByDescending(x => x).Consume(consumer);
    // [ToArray]
    [BenchFact, Benchmark]
    public void ToArray() => ToArray_Impl();

    [LinqIntercept(true)]
    private void ToArray_Impl() => srcCount(MaterializeLinearCount).AsX<int>().ToArray().Consume(consumer);
    // [ToDictionary_elementSelector]
    [BenchFact, Benchmark]
    public void ToDictionary_elementSelector() => ToDictionary_elementSelector_Impl();

    [LinqIntercept(true)]
    private void ToDictionary_elementSelector_Impl() => srcCount(MaterializeHashCount).AsX<int>().ToDictionary(x => x,x => x*2).Consume(consumer);
    // [ToDictionary_keySelector]
    [BenchFact, Benchmark]
    public void ToDictionary_keySelector() => ToDictionary_keySelector_Impl();

    [LinqIntercept(true)]
    private void ToDictionary_keySelector_Impl() => srcCount(MaterializeHashCount).AsX<int>().ToDictionary(x => x).Consume(consumer);
    // [ToHashSet]
    [BenchFact, Benchmark]
    public void ToHashSet() => ToHashSet_Impl();

    [LinqIntercept(true)]
    private void ToHashSet_Impl() => srcCount(MaterializeHashCount).AsX<int>().ToHashSet().Consume(consumer);
    // [ToList]
    [BenchFact, Benchmark]
    public void ToList() => ToList_Impl();

    [LinqIntercept(true)]
    private void ToList_Impl() => srcCount(MaterializeLinearCount).AsX<int>().ToList().Consume(consumer);
    // [ToLookup_elementSelector]
    [BenchFact, Benchmark]
    public void ToLookup_elementSelector() => ToLookup_elementSelector_Impl();

    [LinqIntercept(true)]
    private void ToLookup_elementSelector_Impl() => srcCount(MaterializeLookupCount).AsX<int>().ToLookup(x => x%1000,x => x*2).Consume(consumer);
    // [ToLookup_keySelector]
    [BenchFact, Benchmark]
    public void ToLookup_keySelector() => ToLookup_keySelector_Impl();

    [LinqIntercept(true)]
    private void ToLookup_keySelector_Impl() => srcCount(MaterializeLookupCount).AsX<int>().ToLookup(x => x%1000).Consume(consumer);
    // [TryGetNonEnumeratedCount]
    [BenchFact, Benchmark]
    public void TryGetNonEnumeratedCount() => TryGetNonEnumeratedCount_Impl();

    [LinqIntercept(true)]
    private void TryGetNonEnumeratedCount_Impl() {
        for(var i = 0; i<TryGetNonEnumeratedCount_iterationCount; i++) {
            ConsumeResult(consumer,global::System.Linq.Enumerable.TryGetNonEnumeratedCount(srcCount(ImmediateFullScanCount),out var count),count);
        }
    }
    // [Union_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Union_second() => Union_second_Impl();

    [LinqIntercept(true)]
    private void Union_second_Impl() => srcCount(SetCount).AsX<int>().Union(src2Count(SetCount)).Consume(consumer);
    // [UnionBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void UnionBy_keySelector() => UnionBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void UnionBy_keySelector_Impl() => srcCount(SetCount).AsX<int>().UnionBy(src2Count(SetCount),x => x).Consume(consumer);
    // [Where_indexPredicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Where_indexPredicate() => Where_indexPredicate_Impl();

    [LinqIntercept(true)]
    private void Where_indexPredicate_Impl() => srcCount(StreamFilterCount).AsX<int>().Where((x,i) => i%2==0).Consume(consumer);
    // [Where_predicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Where_predicate() => Where_predicate_Impl();

    [LinqIntercept(true)]
    private void Where_predicate_Impl() => srcCount(StreamFilterCount).AsX<int>().Where(x => (x&1)==0).Consume(consumer);
    // [Where_SourceShape]
    [BenchFact, Benchmark]
    [BenchmarkCategory("Array")]
    public void Where_SourceShape() => Where_SourceShape_Impl();

    [LinqIntercept(true)]
    private void Where_SourceShape_Impl() => array.Where(static x => (x&1)==0).Consume(consumer);
    // [Zip_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Zip_second() => Zip_second_Impl();

    [LinqIntercept(true)]
    private void Zip_second_Impl() => srcCount(StreamShapeCount).AsX<int>().Zip(src2Count(StreamShapeCount),(a,b) => a+b).Consume(consumer);
    // [Zip_tuple]
    [BenchFact, Benchmark]
    public void Zip_tuple() => Zip_tuple_Impl();

    [LinqIntercept(true)]
    private void Zip_tuple_Impl() => srcCount(StreamShapeCount).AsX<int>().Zip(src2Count(StreamShapeCount)).Consume(consumer);

    private int[] outer = [];
    private int[] inner = [];
    private const int Threshold = 120;

    private int[] array = [];
    private List<int> list = [];
    private int sourceCount;

    public Methods1(int count) : base(count) {
        Initialize();
    }

    private void Initialize() {
        outer=global::System.Linq.Enumerable.Range(0,CountForComparison(SelectManyOuterCount)).ToArray();
        inner=global::System.Linq.Enumerable.Range(0,CountForComparison(SelectManyInnerCount)).ToArray();
        sourceCount=CountForComparison(CountForTestTiming(SourceShapeCount));
        array=global::System.Linq.Enumerable.Range(1,sourceCount).ToArray();
        list=array.ToList();
    }

    private static void ConsumeResult(global::Benchmark.Shared.Consumer consumer,bool result,int count) {
        consumer.Consume(result);
        if(result) {
            consumer.Consume(count);
        }
        else {
            consumer.Consume(-1);
        }
    }

    private IEnumerable<int> YieldRange() {
        for(var i = 1; i<=sourceCount; i++) {
            yield return i;
        }
    }
}





