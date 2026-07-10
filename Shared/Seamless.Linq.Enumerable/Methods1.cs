namespace Seamless.Linq.Shared;

#if TSAVORITE
public abstract class Methods1<T> : TsavoriteBase<T> where T:notnull {
#elif PERSISTED
public abstract class Methods1<T> : PersistedBase<T> where T:notnull {
#else
public abstract class Methods1<T>:Base<T> where T:notnull {
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
#else
    private const int Any_iterationCount = 800_000;
    private const int Empty_iterationCount = 25_000;
    private const int First_iterationCount = 5_000;
    private const int FirstOrDefault_iterationCount = 4_000;
    private const int Single_iterationCount = 6_000;
    private const int SingleOrDefault_iterationCount = 4_500;
    private const int TryGetNonEnumeratedCount_iterationCount = 2_000;
    private const int ElementAt_index_iterationCount = 3;
#endif
    private const int Contains_value_iterationCount = 3;
    private const int ImmediateShortCircuitCount = 96_000_000;
    private const int StandardBenchmarkElementCount = 4_096;
    private const int ImmediateFullScanCount = StandardBenchmarkElementCount;
    private const int ImmediatePredicateScanCount = StandardBenchmarkElementCount;
    private const int StreamOneToOneCount = StandardBenchmarkElementCount;
    private const int StreamFilterCount = StandardBenchmarkElementCount;
    private const int StreamPartitionCount = StandardBenchmarkElementCount;
    private const int StreamShapeCount = StandardBenchmarkElementCount;
    private const int MaterializeLinearCount = StandardBenchmarkElementCount;
    private const int MaterializeHashCount = StandardBenchmarkElementCount;
    private const int MaterializeLookupCount = StandardBenchmarkElementCount;
    private const int SetCount = StandardBenchmarkElementCount;
    private const int GroupingCount = StandardBenchmarkElementCount;
    private const int JoinCount = StandardBenchmarkElementCount;
    private const int SortCount = StandardBenchmarkElementCount;
    private const int NestedCount = StandardBenchmarkElementCount;
    private const int GeneratedCount = StandardBenchmarkElementCount;
    private const int SourceShapeCount = StandardBenchmarkElementCount;
    private const int SelectManyOuterCount = 64;
    private const int SelectManyInnerCount = StandardBenchmarkElementCount/SelectManyOuterCount;

    private static int IterationCount(int defaultCount) =>
        BenchmarkExecutionMode.IsElementCountScoped ? 1 : defaultCount;

    private int MidpointIndex(int elementCount) =>
        global::System.Math.Max(0,this.EffectiveSourceCount(elementCount)/2);

    private int MidpointValue(int elementCount) =>
        global::System.Math.Max(1,this.EffectiveSourceCount(elementCount)/2);

    private int QuarterCount(int elementCount) =>
        global::System.Math.Max(0,this.EffectiveSourceCount(elementCount)/4);

    private int ThreeQuarterCount(int elementCount) =>
        global::System.Math.Max(0,this.EffectiveSourceCount(elementCount)*3/4);

    private static int Number(T value) =>
        ToInt(value);

    private static T Value(int value) =>
        FromInt(value);

    private static T Add(T left,T right) =>
        Value(Number(left)+Number(right));

    private static T Add(T left,int right) =>
        Value(Number(left)+right);

    private static bool EqualNumber(T value,int number) =>
        Number(value)==number;

    private static bool GreaterThan(T value,int number) =>
        Number(value)>number;

    private static bool GreaterThanOrEqual(T value,int number) =>
        Number(value)>=number;

    private static bool LessThan(T value,int number) =>
        Number(value)<number;

    private static bool IsEven(T value) =>
        (Number(value)&1)==0;

    private static int Mod(T value,int divisor) =>
        Number(value)%divisor;

    private static long LongNumber(T value) =>
        Number(value);
    // [Aggregate_func]
    [BenchFact, Benchmark]
    public void Aggregate_func() => this.Aggregate_func_Impl();

    [LinqIntercept(true)]
    private void Aggregate_func_Impl() {
        var source = this.srcCount(ImmediateFullScanCount).AsX<T>();
        if(typeof(T)==typeof(string)) {
            var aggregate = source.Aggregate((HasValue:false,Sum:0),(acc,x) => (true,acc.Sum+Number(x)));
            if(!aggregate.HasValue) {
                throw new InvalidOperationException("Sequence contains no elements.");
            }

            var aggregateResult = Value(aggregate.Sum);
            this.Result=aggregateResult;
            this.consumer.Consume(aggregateResult);
            return;
        }

        var result = source.Aggregate((acc,x) => Add(acc,x));
        this.Result=result;
        this.consumer.Consume(result);
    }
    // [Aggregate_seed_func]
    [BenchFact, Benchmark]
    public void Aggregate_seed_func() => this.Aggregate_seed_func_Impl();

    [LinqIntercept(true)]
    private void Aggregate_seed_func_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Aggregate(0L,(acc,x) => acc+LongNumber(x)));
    // [Aggregate_seed_func_resultSelector]
    [BenchFact, Benchmark]
    public void Aggregate_seed_func_resultSelector() => this.Aggregate_seed_func_resultSelector_Impl();

    [LinqIntercept(true)]
    private void Aggregate_seed_func_resultSelector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Aggregate(0L,(acc,x) => acc+LongNumber(x),acc => (double)acc/1000000));
    // [AggregateBy_keySelector]
    [BenchFact, Benchmark]
    public void AggregateBy_keySelector() => this.AggregateBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void AggregateBy_keySelector_Impl() => this.srcCount(GroupingCount).AsX<T>().AggregateBy(x => Mod(x,1000),0,(acc,x) => acc+Number(x)).Consume(this.consumer);
    // [All_predicate]
    [BenchFact, Benchmark]
    public void All_predicate() => this.All_predicate_Impl();

    [LinqIntercept(true)]
    private void All_predicate_Impl() => this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().All(x => GreaterThan(x,0)));
    // [Any]
    [BenchFact, Benchmark]
    public void Any() => this.Any_Impl();

    [LinqIntercept(true)]
    private void Any_Impl() {
        for(var a = 0; a<IterationCount(Any_iterationCount); a++)
            this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().Any());
    }
    // [Any_predicate]
    [BenchFact, Benchmark]
    public void Any_predicate() => this.Any_predicate_Impl();

    [LinqIntercept(true)]
    private void Any_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().Any(x => GreaterThanOrEqual(x,value)));
    }
    // [Append_element]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Append_element() => this.Append_element_Impl();

    [LinqIntercept(true)]
    private void Append_element_Impl() => this.srcCount(StreamShapeCount).AsX<T>().Append(Value(0)).Consume(this.consumer);
    // [Average]
    [BenchFact, Benchmark]
    public void Average() => this.Average_Impl();

    [LinqIntercept(true)]
    private void Average_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Average(static x => (double)Number(x)));
    // [Average_selector]
    [BenchFact, Benchmark]
    public void Average_selector() => this.Average_selector_Impl();

    [LinqIntercept(true)]
    private void Average_selector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Average(static x => (double)Number(x)));
    // [Cast]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Cast() => this.Cast_Impl();

    [LinqIntercept(true)]
    private void Cast_Impl() => this.srcCount(StreamOneToOneCount).AsX<T>().Cast<T>().Consume(this.consumer);
    // [Chunk_size]
    [BenchFact, Benchmark]
    public void Chunk_size() => this.Chunk_size_Impl();

    [LinqIntercept(true)]
    private void Chunk_size_Impl() => this.srcCount(StreamPartitionCount).AsX<T>().Chunk(1000).Consume(this.consumer);
    // [Concat_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Concat_second() => this.Concat_second_Impl();

    [LinqIntercept(true)]
    private void Concat_second_Impl() => this.srcCount(StreamShapeCount).AsX<T>().Concat(this.src2Count(StreamShapeCount)).Consume(this.consumer);
    // [Contains_value]
    [BenchFact, Benchmark]
    public void Contains_value() => this.Contains_value_Impl();

    [LinqIntercept(true)]
    private void Contains_value_Impl() {
        var value = this.MidpointValue(ImmediateShortCircuitCount);
        for(var i = 0; i<IterationCount(Contains_value_iterationCount); i++) {
            this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().Contains(Value(value)));
        }
    }
    // [Count]
    [BenchFact, Benchmark]
    public void Count() => this.Count_Impl();

    [LinqIntercept(true)]
    private void Count_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Count());
    // [Count_predicate]
    [BenchFact, Benchmark]
    public void Count_predicate() => this.Count_predicate_Impl();

    [LinqIntercept(true)]
    private void Count_predicate_Impl() => this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().Count(static x => IsEven(x)));
    // [CountBy_keySelector]
    [BenchFact, Benchmark]
    public void CountBy_keySelector() => this.CountBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void CountBy_keySelector_Impl() => this.srcCount(GroupingCount).AsX<T>().CountBy(static x => Mod(x,1000)).Consume(this.consumer);
    // [DefaultIfEmpty]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void DefaultIfEmpty() => this.DefaultIfEmpty_Impl();

    [LinqIntercept(true)]
    private void DefaultIfEmpty_Impl() => this.srcCount(StreamShapeCount).AsX<T>().DefaultIfEmpty(Value(0)).Consume(this.consumer);
    // [Distinct]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Distinct() => this.Distinct_Impl();

    [LinqIntercept(true)]
    private void Distinct_Impl() => this.srcCount(SetCount).AsX<T>().Distinct().Consume(this.consumer);
    // [DistinctBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void DistinctBy_keySelector() => this.DistinctBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void DistinctBy_keySelector_Impl() => this.srcCount(SetCount).AsX<T>().DistinctBy(static x => Mod(x,1000)).Consume(this.consumer);
    // [ElementAt_index]
    [BenchFact, Benchmark]
    public void ElementAt_index() => this.ElementAt_index_Impl();

    [LinqIntercept(true)]
    private void ElementAt_index_Impl() {
        var index = this.MidpointIndex(ImmediateShortCircuitCount);
        for(var i = 0; i<IterationCount(ElementAt_index_iterationCount); i++) {
#if SYSTEM_RANGE
            this.ElementAt_index_Core(index);
#else
            this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().ElementAt(index));
#endif
        }
    }

#if SYSTEM_RANGE
    [LinqIntercept(false)]
    private void ElementAt_index_Core(int index) => this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().ElementAt(index));
#endif
    // [ElementAtOrDefault_index]
    [BenchFact, Benchmark]
    public void ElementAtOrDefault_index() => this.ElementAtOrDefault_index_Impl();

    [LinqIntercept(true)]
    private void ElementAtOrDefault_index_Impl() {
        var index = this.MidpointIndex(ImmediateShortCircuitCount);
        this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().ElementAtOrDefault(index));
    }
    // [Empty]
    [BenchFact, Benchmark]
    public void Empty() => this.Empty_Impl();

    [LinqIntercept(true)]
    private void Empty_Impl() {
        for(var i = 0; i<IterationCount(Empty_iterationCount); i++) {
            global::System.Linq.Enumerable.Empty<T>().Consume(this.consumer);
        }
    }
    // [Except_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Except_second() => this.Except_second_Impl();

    [LinqIntercept(true)]
    private void Except_second_Impl() => this.srcCount(SetCount).AsX<T>().Except(this.src2Count(SetCount)).Consume(this.consumer);
    // [ExceptBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ExceptBy_keySelector() => this.ExceptBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void ExceptBy_keySelector_Impl() => this.srcCount(SetCount).AsX<T>().ExceptBy(this.src2Count(SetCount),x => x).Consume(this.consumer);
    // [First]
    [BenchFact, Benchmark]
    public void First() => this.First_Impl();

    [LinqIntercept(true)]
    private void First_Impl() {
        for(var i = 0; i<IterationCount(First_iterationCount); i++) {
            this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().First());
        }
    }
    // [FirstOrDefault]
    [BenchFact, Benchmark]
    public void FirstOrDefault() => this.FirstOrDefault_Impl();

    [LinqIntercept(true)]
    private void FirstOrDefault_Impl() {
        for(var i = 0; i<IterationCount(FirstOrDefault_iterationCount); i++) {
            this.consumer.Consume(this.srcCount(ImmediateShortCircuitCount).AsX<T>().FirstOrDefault());
        }
    }
    // [FirstOrDefault_predicate]
    [BenchFact, Benchmark]
    public void FirstOrDefault_predicate() => this.FirstOrDefault_predicate_Impl();

    [LinqIntercept(true)]
    private void FirstOrDefault_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().FirstOrDefault(x => GreaterThanOrEqual(x,value)));
    }
    // [GroupBy_elementSelector]
    [BenchFact, Benchmark]
    public void GroupBy_elementSelector() => this.GroupBy_elementSelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_elementSelector_Impl() => this.srcCount(GroupingCount).AsX<T>().GroupBy(static x => Mod(x,1000),static x => Number(x)*2).Consume(this.consumer);
    // [GroupBy_elementSelector_resultSelector]
    [BenchFact, Benchmark]
    public void GroupBy_elementSelector_resultSelector() => this.GroupBy_elementSelector_resultSelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_elementSelector_resultSelector_Impl() => this.srcCount(GroupingCount).AsX<T>().GroupBy(static x => Mod(x,1000),static x => Number(x)*2,(key,g) => key).Consume(this.consumer);
    // [GroupBy_keySelector]
    [BenchFact, Benchmark]
    public void GroupBy_keySelector() => this.GroupBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_keySelector_Impl() => this.srcCount(GroupingCount).AsX<T>().GroupBy(static x => Mod(x,1000)).Consume(this.consumer);
    // [GroupBy_resultSelector]
    [BenchFact, Benchmark]
    public void GroupBy_resultSelector() => this.GroupBy_resultSelector_Impl();

    [LinqIntercept(true)]
    private void GroupBy_resultSelector_Impl() => this.srcCount(GroupingCount).AsX<T>().GroupBy(static x => Mod(x,1000),(key,g) => key).Consume(this.consumer);
    // [GroupJoin_inner]
    [BenchFact, Benchmark]
    public void GroupJoin_inner() => this.GroupJoin_inner_Impl();

    [LinqIntercept(true)]
    private void GroupJoin_inner_Impl() => this.srcCount(JoinCount).AsX<T>().GroupJoin(this.src2Count(JoinCount),x => x,x => x,(outer,inner) => outer).Consume(this.consumer);
    // [Index]
    [BenchFact, Benchmark]
    public void Index() => this.Index_Impl();

    [LinqIntercept(true)]
    private void Index_Impl() => this.srcCount(StreamOneToOneCount).AsX<T>().Index().Consume(this.consumer);
    // [Intersect_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Intersect_second() => this.Intersect_second_Impl();

    [LinqIntercept(true)]
    private void Intersect_second_Impl() => this.srcCount(SetCount).AsX<T>().Intersect(this.src2Count(SetCount)).Consume(this.consumer);
    // [IntersectBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void IntersectBy_keySelector() => this.IntersectBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void IntersectBy_keySelector_Impl() => this.srcCount(SetCount).AsX<T>().IntersectBy(this.src2Count(SetCount),x => x).Consume(this.consumer);
    // [InvariantHoisting]
    [BenchFact, Benchmark]
    public void InvariantHoisting() => this.InvariantHoisting_Impl();

    [LinqIntercept(true)]
    private void InvariantHoisting_Impl() => this.srcCount(StreamOneToOneCount).AsX<T>().Select(static x => Number(x)+HoistInvariant.Value()).Consume(this.consumer);
    // [Join_inner]
    [BenchFact, Benchmark]
    public void Join_inner() => this.Join_inner_Impl();

    [LinqIntercept(true)]
    private void Join_inner_Impl() => this.srcCount(JoinCount).AsX<T>().Join(this.src2Count(JoinCount),x => x,x => x,(a,b) => a).Consume(this.consumer);
    // [Join_innerKey_Hoisting]
    [BenchFact, Benchmark]
    public void Join_innerKey_Hoisting() => this.Join_innerKey_Hoisting_Impl();

    [LinqIntercept(true)]
    private void Join_innerKey_Hoisting_Impl() => this.srcCount(JoinCount).AsX<T>().Join(this.src2Count(JoinCount),static o => Mod(o,1000),static i => (Number(i)+HoistInvariant.Value())%1000,static (o,i) => Number(o)+Number(i)).Consume(this.consumer);
    // [Join_outerKey_Hoisting]
    [BenchFact, Benchmark]
    public void Join_outerKey_Hoisting() => this.Join_outerKey_Hoisting_Impl();

    [LinqIntercept(true)]
    private void Join_outerKey_Hoisting_Impl() => this.srcCount(JoinCount).AsX<T>().Join(this.src2Count(JoinCount),static o => (Number(o)+HoistInvariant.Value())%1000,static i => Mod(i,1000),static (o,i) => Number(o)+Number(i)).Consume(this.consumer);
    // [Join_resultSelector_Hoisting]
    [BenchFact, Benchmark]
    public void Join_resultSelector_Hoisting() => this.Join_resultSelector_Hoisting_Impl();

    [LinqIntercept(true)]
    private void Join_resultSelector_Hoisting_Impl() => this.srcCount(JoinCount).AsX<T>().Join(this.src2Count(JoinCount),static o => Mod(o,1000),static i => Mod(i,1000),static (o,i) => Number(o)+Number(i)+HoistInvariant.Value()).Consume(this.consumer);
    // [Last]
    [BenchFact, Benchmark]
    public void Last() => this.Last_Impl();

    [LinqIntercept(true)]
    private void Last_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Last());
    // [LastOrDefault]
    [BenchFact, Benchmark]
    public void LastOrDefault() => this.LastOrDefault_Impl();

    [LinqIntercept(true)]
    private void LastOrDefault_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().LastOrDefault());
    // [LastOrDefault_predicate]
    [BenchFact, Benchmark]
    public void LastOrDefault_predicate() => this.LastOrDefault_predicate_Impl();

    [LinqIntercept(true)]
    private void LastOrDefault_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().LastOrDefault(x => GreaterThanOrEqual(x,value)));
    }
    // [LongCount]
    [BenchFact, Benchmark]
    public void LongCount() => this.LongCount_Impl();

    [LinqIntercept(true)]
    private void LongCount_Impl() {
        if(BenchmarkExecutionMode.IsElementCountScoped) {
            this.consumer.Consume((long)this.EffectiveSourceCount(ImmediateFullScanCount));
            return;
        }

        this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().LongCount());
    }
    // [LongCount_predicate]
    [BenchFact, Benchmark]
    public void LongCount_predicate() => this.LongCount_predicate_Impl();

    [LinqIntercept(true)]
    private void LongCount_predicate_Impl() => this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().LongCount(static x => IsEven(x)));
    // [Max]
    [BenchFact, Benchmark]
    public void Max() => this.Max_Impl();

    [LinqIntercept(true)]
    private void Max_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Max());
    // [Max_selector]
    [BenchFact, Benchmark]
    public void Max_selector() => this.Max_selector_Impl();

    [LinqIntercept(true)]
    private void Max_selector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Max(static x => Number(x)*2));
    // [MaxBy_keySelector]
    [BenchFact, Benchmark]
    public void MaxBy_keySelector() => this.MaxBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void MaxBy_keySelector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().MaxBy(static x => Mod(x,1000)));
    // [Min]
    [BenchFact, Benchmark]
    public void Min() => this.Min_Impl();

    [LinqIntercept(true)]
    private void Min_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Min());
    // [Min_selector]
    [BenchFact, Benchmark]
    public void Min_selector() => this.Min_selector_Impl();

    [LinqIntercept(true)]
    private void Min_selector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Min(static x => Number(x)*2));
    // [MinBy_keySelector]
    [BenchFact, Benchmark]
    public void MinBy_keySelector() => this.MinBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void MinBy_keySelector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().MinBy(static x => Mod(x,1000)));
    // [OfType]
    [BenchFact, Benchmark]
    public void OfType() => this.OfType_Impl();

    [LinqIntercept(true)]
    private void OfType_Impl() => this.srcCount(StreamOneToOneCount).AsX<T>().OfType<T>().Consume(this.consumer);
    // [Order]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Order() => this.Order_Impl();

    [LinqIntercept(true)]
    private void Order_Impl() => this.srcCount(SortCount).AsX<T>().Order().Consume(this.consumer);
    // [OrderBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderBy_keySelector() => this.OrderBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void OrderBy_keySelector_Impl() => this.srcCount(SortCount).AsX<T>().OrderBy(x => x).Consume(this.consumer);
    // [OrderByDescending_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderByDescending_keySelector() => this.OrderByDescending_keySelector_Impl();

    [LinqIntercept(true)]
    private void OrderByDescending_keySelector_Impl() => this.srcCount(SortCount).AsX<T>().OrderByDescending(x => x).Consume(this.consumer);
    // [OrderDescending]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void OrderDescending() => this.OrderDescending_Impl();

    [LinqIntercept(true)]
    private void OrderDescending_Impl() => this.srcCount(SortCount).AsX<T>().OrderDescending().Consume(this.consumer);
    // [Prepend_element]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Prepend_element() => this.Prepend_element_Impl();

    [LinqIntercept(true)]
    private void Prepend_element_Impl() => this.srcCount(StreamShapeCount).AsX<T>().Prepend(Value(0)).Consume(this.consumer);
    // [Range]
    [BenchFact, Benchmark]
    public void Range() => this.Range_Impl();

    [LinqIntercept(true)]
    private void Range_Impl() => global::System.Linq.Enumerable.Range(1,this.EffectiveSourceCount(GeneratedCount)).Select(static x => Value(x)).Consume(this.consumer);
    // [Repeat]
    [BenchFact, Benchmark]
    public void Repeat() => this.Repeat_Impl();

    [LinqIntercept(true)]
    private void Repeat_Impl() => global::System.Linq.Enumerable.Repeat(Value(7),this.EffectiveSourceCount(GeneratedCount)).Consume(this.consumer);
    // [Reverse]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Reverse() => this.Reverse_Impl();

    [LinqIntercept(true)]
    private void Reverse_Impl() => this.srcCount(StreamShapeCount).AsX<T>().Reverse().Consume(this.consumer);
    // [Select_indexSelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select_indexSelector() => this.Select_indexSelector_Impl();

    [LinqIntercept(false)]
    private void Select_indexSelector_Impl() => this.srcCount(StreamOneToOneCount).AsX<T>().Select(static (x,i) => Number(x)+i).Consume(this.consumer);
    // [Select_selector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select_selector() => this.Select_selector_Impl();

    [LinqIntercept(true)]
    private void Select_selector_Impl() => this.srcCount(StreamOneToOneCount).AsX<T>().Select(static x => Add(x,x)).Consume(this.consumer);
    // [SelectMany_Hoisting_PredicatePushdown]
    [BenchFact, Benchmark]
    public void SelectMany_Hoisting_PredicatePushdown() => this.SelectMany_Hoisting_PredicatePushdown_Impl();

    [LinqIntercept(true)]
    private void SelectMany_Hoisting_PredicatePushdown_Impl() => this.outer.AsX<int>().SelectMany(_ => this.inner).Where(static x => x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);
    // [SelectMany_indexSelector]
    [BenchFact, Benchmark]
    public void SelectMany_indexSelector() => this.SelectMany_indexSelector_Impl();

    [LinqIntercept(false)]
    private void SelectMany_indexSelector_Impl() => this.srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany((x,i) => x).Consume(this.consumer);
    // [SelectMany_indexSelector_resultSelector]
    [BenchFact, Benchmark]
    public void SelectMany_indexSelector_resultSelector() => this.SelectMany_indexSelector_resultSelector_Impl();

    [LinqIntercept(false)]
    private void SelectMany_indexSelector_resultSelector_Impl() => this.srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany((x,i) => x,(outer,inner) => inner+1).Consume(this.consumer);
    // [SelectMany_resultSelector]
    [BenchFact, Benchmark]
    public void SelectMany_resultSelector() => this.SelectMany_resultSelector_Impl();

    [LinqIntercept(true)]
    private void SelectMany_resultSelector_Impl() => this.srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany(x => x,(outer,inner) => inner+1).Consume(this.consumer);
    // [SelectMany_selector]
    [BenchFact, Benchmark]
    public void SelectMany_selector() => this.SelectMany_selector_Impl();

    [LinqIntercept(true)]
    private void SelectMany_selector_Impl() => this.srcNestedCount(NestedCount).AsX<IEnumerable<int>>().SelectMany(x => x).Consume(this.consumer);
    // [SelectManyPredicatePushdown]
    [BenchFact, Benchmark]
    public void SelectManyPredicatePushdown() => this.SelectManyPredicatePushdown_Impl();

    [LinqIntercept(true)]
    private void SelectManyPredicatePushdown_Impl() => this.outer.AsX<int>().SelectMany(_ => this.inner).Where(static x => x>Threshold).Consume(this.consumer);
    // [SequenceEqual_second]
    [BenchFact, Benchmark]
    public void SequenceEqual_second() => this.SequenceEqual_second_Impl();

    [LinqIntercept(true)]
    private void SequenceEqual_second_Impl() => this.consumer.Consume(this.srcCount(StreamShapeCount).AsX<T>().SequenceEqual(this.srcCount(StreamShapeCount)));
    // [Single]
    [BenchFact, Benchmark]
    public void Single() => this.Single_Impl();

    [LinqIntercept(true)]
    private void Single_Impl() {
        for(var i = 0; i<IterationCount(Single_iterationCount); i++) {
            this.consumer.Consume(this.srcOne(1).Single());
        }
    }
    // [Single_predicate]
    [BenchFact, Benchmark]
    public void Single_predicate() => this.Single_predicate_Impl();

    [LinqIntercept(true)]
    private void Single_predicate_Impl() {
        var y = this.srcOne(1).ToArray();
        var x = y.Single(static x => EqualNumber(x,0));
        this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().Single(static x => EqualNumber(x,1)));
    }
    // [SingleOrDefault]
    [BenchFact, Benchmark]
    public void SingleOrDefault() => this.SingleOrDefault_Impl();

    [LinqIntercept(true)]
    private void SingleOrDefault_Impl() {
        for(var i = 0; i<IterationCount(SingleOrDefault_iterationCount); i++) {
            this.consumer.Consume(this.srcOne(1).SingleOrDefault());
        }
    }
    // [SingleOrDefault_predicate]
    [BenchFact, Benchmark]
    public void SingleOrDefault_predicate() => this.SingleOrDefault_predicate_Impl();

    [LinqIntercept(true)]
    private void SingleOrDefault_predicate_Impl() => this.consumer.Consume(this.srcCount(ImmediatePredicateScanCount).AsX<T>().SingleOrDefault(static x => EqualNumber(x,1)));
    // [Skip_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Skip_count() => this.Skip_count_Impl();

    [LinqIntercept(true)]
    private void Skip_count_Impl() => this.srcCount(StreamPartitionCount).AsX<T>().Skip(this.QuarterCount(StreamPartitionCount)).Consume(this.consumer);
    // [SkipLast_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipLast_count() => this.SkipLast_count_Impl();

    [LinqIntercept(true)]
    private void SkipLast_count_Impl() => this.srcCount(StreamPartitionCount).AsX<T>().SkipLast(this.QuarterCount(StreamPartitionCount)).Consume(this.consumer);
    // [SkipWhile_indexPredicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipWhile_indexPredicate() => this.SkipWhile_indexPredicate_Impl();

    [LinqIntercept(true)]
    private void SkipWhile_indexPredicate_Impl() {
        var takeUntil = this.QuarterCount(StreamFilterCount);
        this.srcCount(StreamFilterCount).AsX<T>().SkipWhile((x,i) => i<takeUntil).Consume(this.consumer);
    }
    // [SkipWhile_predicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void SkipWhile_predicate() => this.SkipWhile_predicate_Impl();

    [LinqIntercept(true)]
    private void SkipWhile_predicate_Impl() => this.srcCount(StreamFilterCount).AsX<T>().SkipWhile(x => LessThan(x,this.QuarterCount(StreamFilterCount)+1)).Consume(this.consumer);
    // [Sum]
    [BenchFact, Benchmark]
    public void Sum() => this.Sum_Impl();

    [LinqIntercept(true)]
    private void Sum_Impl() => this.consumer.Consume(this.srcMinusPlusCount(ImmediateFullScanCount).AsX<T>().Sum(static x => Number(x)));
    // [Sum_selector]
    [BenchFact, Benchmark]
    public void Sum_selector() => this.Sum_selector_Impl();

    [LinqIntercept(true)]
    private void Sum_selector_Impl() => this.consumer.Consume(this.srcCount(ImmediateFullScanCount).AsX<T>().Sum(static x => LongNumber(x)));
    // [Take_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Take_count() => this.Take_count_Impl();

    [LinqIntercept(true)]
    private void Take_count_Impl() => this.srcCount(StreamPartitionCount).AsX<T>().Take(this.ThreeQuarterCount(StreamPartitionCount)).Consume(this.consumer);
    // [TakeLast_count]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeLast_count() => this.TakeLast_count_Impl();

    [LinqIntercept(true)]
    private void TakeLast_count_Impl() => this.srcCount(StreamPartitionCount).AsX<T>().TakeLast(this.QuarterCount(StreamPartitionCount)).Consume(this.consumer);
    // [TakeWhile_indexPredicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeWhile_indexPredicate() => this.TakeWhile_indexPredicate_Impl();

    [LinqIntercept(true)]
    private void TakeWhile_indexPredicate_Impl() {
        var takeUntil = this.ThreeQuarterCount(StreamFilterCount);
        this.srcCount(StreamFilterCount).AsX<T>().TakeWhile((x,i) => i<takeUntil).Consume(this.consumer);
    }
    // [TakeWhile_predicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void TakeWhile_predicate() => this.TakeWhile_predicate_Impl();

    [LinqIntercept(true)]
    private void TakeWhile_predicate_Impl() => this.srcCount(StreamFilterCount).AsX<T>().TakeWhile(x => LessThan(x,this.ThreeQuarterCount(StreamFilterCount)+1)).Consume(this.consumer);
    // [ThenBy_keySelector]
    [BenchFact, Benchmark]
    public void ThenBy_keySelector() => this.ThenBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void ThenBy_keySelector_Impl() => this.srcCount(SortCount).AsX<T>().OrderBy(static x => Mod(x,1000)).ThenBy(static x => x).Consume(this.consumer);
    // [ThenByDescending_keySelector]
    [BenchFact, Benchmark]
    public void ThenByDescending_keySelector() => this.ThenByDescending_keySelector_Impl();

    [LinqIntercept(true)]
    private void ThenByDescending_keySelector_Impl() => this.srcCount(SortCount).AsX<T>().OrderBy(static x => Mod(x,1000)).ThenByDescending(static x => x).Consume(this.consumer);
    // [ToArray]
    [BenchFact, Benchmark]
    public void ToArray() => this.ToArray_Impl();

    [LinqIntercept(true)]
    private void ToArray_Impl() => this.srcCount(MaterializeLinearCount).AsX<T>().ToArray().Consume(this.consumer);
    // [ToDictionary_elementSelector]
    [BenchFact, Benchmark]
    public void ToDictionary_elementSelector() => this.ToDictionary_elementSelector_Impl();

    [LinqIntercept(true)]
    private void ToDictionary_elementSelector_Impl() => this.srcCount(MaterializeHashCount).AsX<T>().ToDictionary(static x => x,static x => Number(x)*2).Consume(this.consumer);
    // [ToDictionary_keySelector]
    [BenchFact, Benchmark]
    public void ToDictionary_keySelector() => this.ToDictionary_keySelector_Impl();

    [LinqIntercept(true)]
    private void ToDictionary_keySelector_Impl() => this.srcCount(MaterializeHashCount).AsX<T>().ToDictionary(x => x).Consume(this.consumer);
    // [ToHashSet]
    [BenchFact, Benchmark]
    public void ToHashSet() => this.ToHashSet_Impl();

    [LinqIntercept(true)]
    private void ToHashSet_Impl() => this.srcCount(MaterializeHashCount).AsX<T>().ToHashSet().Consume(this.consumer);
    // [ToList]
    [BenchFact, Benchmark]
    public void ToList() => this.ToList_Impl();

    [LinqIntercept(true)]
    private void ToList_Impl() => this.srcCount(MaterializeLinearCount).AsX<T>().ToList().Consume(this.consumer);
    // [ToLookup_elementSelector]
    [BenchFact, Benchmark]
    public void ToLookup_elementSelector() => this.ToLookup_elementSelector_Impl();

    [LinqIntercept(true)]
    private void ToLookup_elementSelector_Impl() => this.srcCount(MaterializeLookupCount).AsX<T>().ToLookup(static x => Mod(x,1000),static x => Number(x)*2).Consume(this.consumer);
    // [ToLookup_keySelector]
    [BenchFact, Benchmark]
    public void ToLookup_keySelector() => this.ToLookup_keySelector_Impl();

    [LinqIntercept(true)]
    private void ToLookup_keySelector_Impl() => this.srcCount(MaterializeLookupCount).AsX<T>().ToLookup(static x => Mod(x,1000)).Consume(this.consumer);
    // [TryGetNonEnumeratedCount]
    [BenchFact, Benchmark]
    public void TryGetNonEnumeratedCount() => this.TryGetNonEnumeratedCount_Impl();

    [LinqIntercept(true)]
    private void TryGetNonEnumeratedCount_Impl() {
        for(var i = 0; i<IterationCount(TryGetNonEnumeratedCount_iterationCount); i++) {
            ConsumeResult(this.consumer,global::System.Linq.Enumerable.TryGetNonEnumeratedCount(this.srcCount(ImmediateFullScanCount),out var count),count);
        }
    }
    // [Union_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Union_second() => this.Union_second_Impl();

    [LinqIntercept(true)]
    private void Union_second_Impl() => this.srcCount(SetCount).AsX<T>().Union(this.src2Count(SetCount)).Consume(this.consumer);
    // [UnionBy_keySelector]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void UnionBy_keySelector() => this.UnionBy_keySelector_Impl();

    [LinqIntercept(true)]
    private void UnionBy_keySelector_Impl() => this.srcCount(SetCount).AsX<T>().UnionBy(this.src2Count(SetCount),x => x).Consume(this.consumer);
    // [Where_indexPredicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Where_indexPredicate() => this.Where_indexPredicate_Impl();

    [LinqIntercept(false)]
    private void Where_indexPredicate_Impl() => this.srcCount(StreamFilterCount).AsX<T>().Where((x,i) => i%2==0).Consume(this.consumer);
    // [Where_predicate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Where_predicate() => this.Where_predicate_Impl();

    [LinqIntercept(true)]
    private void Where_predicate_Impl() => this.srcCount(StreamFilterCount).AsX<T>().Where(static x => IsEven(x)).Consume(this.consumer);
    // [Where_SourceShape]
    [BenchFact, Benchmark]
    [BenchmarkCategory("Array")]
    public void Where_SourceShape() => this.Where_SourceShape_Impl();

    [LinqIntercept(true)]
    private void Where_SourceShape_Impl() => this.sourceShape().AsX<T>().Where(static x => IsEven(x)).Consume(this.consumer);
    // [Zip_second]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Zip_second() => this.Zip_second_Impl();

    [LinqIntercept(true)]
    private void Zip_second_Impl() => this.srcCount(StreamShapeCount).AsX<T>().Zip(this.src2Count(StreamShapeCount),static (a,b) => Add(a,b)).Consume(this.consumer);
    // [Zip_tuple]
    [BenchFact, Benchmark]
    public void Zip_tuple() => this.Zip_tuple_Impl();

    [LinqIntercept(true)]
    private void Zip_tuple_Impl() => this.srcCount(StreamShapeCount).AsX<T>().Zip(this.src2Count(StreamShapeCount)).Consume(this.consumer);
    // [ExecutionPlan_FilterProjectCount]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.ExecutionPlan)]
    public void ExecutionPlan_FilterProjectCount() => this.ExecutionPlan_FilterProjectCount_Impl();

    [LinqIntercept(true)]
    private void ExecutionPlan_FilterProjectCount_Impl() => this.srcCount(StreamFilterCount).AsX<T>()
        .Where(static x => IsEven(x))
        .Select(static x => (Number(x)*3)%2048)
        .CountBy(static x => x)
        .OrderByDescending(static x => x.Value)
        .Take(64)
        .Consume(this.consumer);
    // [ExecutionPlan_FilterGroupAggregate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.ExecutionPlan)]
    public void ExecutionPlan_FilterGroupAggregate() => this.ExecutionPlan_FilterGroupAggregate_Impl();

    [LinqIntercept(true)]
    private void ExecutionPlan_FilterGroupAggregate_Impl() => this.srcCount(GroupingCount).AsX<T>()
        .Where(static x => Mod(x,3)!=0)
        .Select(static x => Number(x)+HoistInvariant.Value())
        .CountBy(static x => x%1024)
        .OrderByDescending(static x => x.Value)
        .Take(128)
        .Consume(this.consumer);
    // [ExecutionPlan_FilterJoinAggregate]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.ExecutionPlan)]
    public void ExecutionPlan_FilterJoinAggregate() => this.ExecutionPlan_FilterJoinAggregate_Impl();

    [LinqIntercept(true)]
    private void ExecutionPlan_FilterJoinAggregate_Impl() => this.srcCount(JoinCount).AsX<T>()
        .Where(static outer => IsEven(outer))
        .Join(
            this.src2Count(JoinCount),
            static outer => Mod(outer,4096),
            static inner => Mod(inner,3)==0 ? Mod(inner,4096) : -1,
            static (outer,inner) => Number(outer)+Number(inner))
        .CountBy(static value => value%512)
        .OrderByDescending(static value => value.Value)
        .Take(128)
        .Consume(this.consumer);
    // [ExecutionPlan_GroupJoinMaterialize]
    [BenchFact, Benchmark]
    [BenchmarkCategory(Categories.ExecutionPlan)]
    public void ExecutionPlan_GroupJoinMaterialize() => this.ExecutionPlan_GroupJoinMaterialize_Impl();

    [LinqIntercept(false)]
    private void ExecutionPlan_GroupJoinMaterialize_Impl() => this.srcCount(JoinCount).AsX<T>()
        .Where(static outer => Mod(outer,5)!=0)
        .GroupJoin(
            this.src2Count(JoinCount).Where(static inner => IsEven(inner)),
            static outer => Mod(outer,2048),
            static inner => Mod(inner,2048),
            static (outer,inner) => Number(outer)+inner.Count())
        .OrderByDescending(static value => value)
        .Take(128)
        .ToList()
        .Consume(this.consumer);

    private int[] outer = [];
    private int[] inner = [];
    private const int Threshold = 120;

    private int sourceCount;
    protected Methods1(int count) : base(count) {
        this.Initialize();
    }

    private void Initialize() {
        var selectManyOuterCount = this.CountForComparison(SelectManyOuterCount);
        var selectManyInnerCount = this.CountForComparison(SelectManyInnerCount);
        if(BenchmarkExecutionMode.IsElementCountScoped) {
            var totalCount = global::System.Math.Max(1,this.EffectiveSourceCount(NestedCount));
            selectManyOuterCount=global::System.Math.Max(1,(int)global::System.Math.Sqrt(totalCount));
            selectManyInnerCount=global::System.Math.Max(1,totalCount/selectManyOuterCount);
        }
        this.outer=global::System.Linq.Enumerable.Range(0,selectManyOuterCount).ToArray();
        this.inner=global::System.Linq.Enumerable.Range(0,selectManyInnerCount).ToArray();
        this.sourceCount=this.CountForComparison(this.CountForTestTiming(SourceShapeCount));
    }

#if SYSTEM_RANGE
    [LinqIntercept(false)]
    private IEnumerable<T> sourceShape() =>
        this.srcCount(SourceShapeCount);
#elif SYSTEM_LIST
    [LinqIntercept(false)]
    private List<T> sourceShape() =>
        this.srcCount(SourceShapeCount);
#elif PERSISTED
    [LinqIntercept(false)]
    private Seamless.Persisted.Collections.Set<T> sourceShape() =>
        this.srcCount(SourceShapeCount);
#elif TSAVORITE
    [LinqIntercept(false)]
    private global::Seamless.Tsavorite.Collections.HashSet<T> sourceShape() =>
        this.srcCount(SourceShapeCount);
#else
    [LinqIntercept(false)]
    private T[] sourceShape() =>
        this.srcCount(SourceShapeCount);
#endif

    private static void ConsumeResult(global::Common.Consumer consumer,bool result,int count) {
        consumer.Consume(result);
        if(result) {
            consumer.Consume(count);
        }else{
            consumer.Consume(-1);
        }
    }

    private IEnumerable<int> YieldRange() {
        for(var i = 1; i<=this.sourceCount; i++) {
            yield return i;
        }
    }
}





