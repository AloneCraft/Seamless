#if !SYSTEM_IENUMERABLE
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Linq.Ideal;

/// <summary>
/// 配列・List 起点の理想ループ版。ソースの取り出し口だけを #if で分け、演算子の本体は span を直接回す。
/// メソッド名と並び順は Shared/Linq.Enumerable/Base.cs と同じにする(行の突き合わせはメソッド名で行う)。
/// </summary>
public abstract partial class IdealBase<T> {
#if SYSTEM_LIST
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> SourceSpan(int elementCount) => CollectionsMarshal.AsSpan(this.srcCount(elementCount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> SecondSpan(int elementCount) => CollectionsMarshal.AsSpan(this.src2Count(elementCount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> OneSpan() => CollectionsMarshal.AsSpan(this.srcOne(1));
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> SourceSpan(int elementCount) => this.srcCount(elementCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> SecondSpan(int elementCount) => this.src2Count(elementCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> OneSpan() => this.srcOne(1);
#endif

    // [Aggregate_func]
    [BenchFact,Benchmark]
    public void Aggregate_func() => this.Aggregate_func_Impl();

    private void Aggregate_func_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(typeof(T)==typeof(string)) {
            var hasValue = false;
            var sum = 0;
            foreach(var x in source) {
                hasValue=true;
                sum+=Number(x);
            }
            if(!hasValue) {
                throw new InvalidOperationException("Sequence contains no elements.");
            }
            var aggregateResult = Value(sum);
            this.Result=aggregateResult;
            this.consumer.Consume(aggregateResult);
            return;
        }
        if(source.Length==0) {
            throw IdealErrors.NoElements();
        }
        var result = source[0];
        for(var index = 1;index<source.Length;index++) {
            result=Add(result,source[index]);
        }
        this.Result=result;
        this.consumer.Consume(result);
    }

    // [Aggregate_seed_func]
    [BenchFact,Benchmark]
    public void Aggregate_seed_func() => this.Aggregate_seed_func_Impl();

    private void Aggregate_seed_func_Impl() {
        var accumulate = 0L;
        foreach(var x in this.SourceSpan(ImmediateFullScanCount)) {
            accumulate+=LongNumber(x);
        }
        this.consumer.Consume(accumulate);
    }

    // [Aggregate_seed_func_resultSelector]
    [BenchFact,Benchmark]
    public void Aggregate_seed_func_resultSelector() => this.Aggregate_seed_func_resultSelector_Impl();

    private void Aggregate_seed_func_resultSelector_Impl() {
        var accumulate = 0L;
        foreach(var x in this.SourceSpan(ImmediateFullScanCount)) {
            accumulate+=LongNumber(x);
        }
        this.consumer.Consume((double)accumulate/1000000);
    }

    // [AggregateBy_keySelector]
    [BenchFact,Benchmark]
    public void AggregateBy_keySelector() => this.AggregateBy_keySelector_Impl();

    private void AggregateBy_keySelector_Impl() {
        var source = this.SourceSpan(GroupingCount);
        var map = new Dictionary<int,int>(source.Length);
        foreach(var x in source) {
            ref var accumulate = ref CollectionsMarshal.GetValueRefOrAddDefault(map,Mod(x,1000),out _);
            accumulate+=Number(x);
        }
        var consumer = this.consumer;
        foreach(var pair in map) {
            consumer.Consume(pair);
        }
    }

    // [All_predicate]
    [BenchFact,Benchmark]
    public void All_predicate() => this.All_predicate_Impl();

    private void All_predicate_Impl() {
        var result = true;
        foreach(var x in this.SourceSpan(ImmediatePredicateScanCount)) {
            if(!GreaterThan(x,0)) {
                result=false;
                break;
            }
        }
        this.consumer.Consume(result);
    }

    // [Any]
    [BenchFact,Benchmark]
    public void Any() => this.Any_Impl();

    private void Any_Impl() {
        var source = this.SourceSpan(ImmediateShortCircuitCount);
        for(var a = 0;a<IterationCount(Any_iterationCount);a++) {
            this.consumer.Consume(source.Length!=0);
        }
    }

    // [Any_predicate]
    [BenchFact,Benchmark]
    public void Any_predicate() => this.Any_predicate_Impl();

    private void Any_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        var result = false;
        foreach(var x in this.SourceSpan(ImmediatePredicateScanCount)) {
            if(GreaterThanOrEqual(x,value)) {
                result=true;
                break;
            }
        }
        this.consumer.Consume(result);
    }

    // [Append_element]
    [BenchFact,Benchmark]
    public void Append_element() => ExecuteOrderDependent(() => this.Append_element_Impl());

    private void Append_element_Impl() {
        var source = this.SourceSpan(StreamShapeCount);
        var element = Value(0);
        var consumer = this.consumer;
        foreach(var x in source) {
            consumer.Consume(x);
        }
        consumer.Consume(element);
    }

    // [Average]
    [BenchFact,Benchmark]
    public void Average() => this.Average_Impl();

    private void Average_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            throw IdealErrors.NoElements();
        }
        var sum = 0d;
        foreach(var x in source) {
            sum+=(double)Number(x);
        }
        this.consumer.Consume(sum/source.Length);
    }

    // [Average_selector]
    [BenchFact,Benchmark]
    public void Average_selector() => this.Average_selector_Impl();

    private void Average_selector_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            throw IdealErrors.NoElements();
        }
        var sum = 0d;
        foreach(var x in source) {
            sum+=(double)Number(x);
        }
        this.consumer.Consume(sum/source.Length);
    }

    // [Cast]
    [BenchFact,Benchmark]
    public void Cast() => this.Cast_Impl();

    private void Cast_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(StreamOneToOneCount)) {
            consumer.Consume(x);
        }
    }

    // [Chunk_size]
    [BenchFact,Benchmark]
    public void Chunk_size() => ExecuteOrderDependent(() => this.Chunk_size_Impl());

    private void Chunk_size_Impl() {
        var source = this.SourceSpan(StreamPartitionCount);
        var consumer = this.consumer;
        for(var start = 0;start<source.Length;start+=1000) {
            consumer.Consume(source.Slice(start,Math.Min(1000,source.Length-start)).ToArray());
        }
    }

    // [Concat_second]
    [BenchFact,Benchmark]
    public void Concat_second() => ExecuteOrderDependent(() => this.Concat_second_Impl());

    private void Concat_second_Impl() {
        var first = this.SourceSpan(StreamShapeCount);
        var second = this.SecondSpan(StreamShapeCount);
        var consumer = this.consumer;
        foreach(var x in first) {
            consumer.Consume(x);
        }
        foreach(var x in second) {
            consumer.Consume(x);
        }
    }

    // [Contains_value]
    [BenchFact,Benchmark]
    public void Contains_value() => this.Contains_value_Impl();

    private void Contains_value_Impl() {
        var value = this.MidpointValue(ImmediateShortCircuitCount);
        var source = this.SourceSpan(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(Contains_value_iterationCount);i++) {
            this.consumer.Consume(SpanContains(source,Value(value)));
        }
    }

    // [Count]
    [BenchFact,Benchmark]
    public void Count() => this.Count_Impl();

    private void Count_Impl() => this.consumer.Consume(this.SourceSpan(ImmediateFullScanCount).Length);

    // [Count_predicate]
    [BenchFact,Benchmark]
    public void Count_predicate() => this.Count_predicate_Impl();

    private void Count_predicate_Impl() {
        var count = 0;
        foreach(var x in this.SourceSpan(ImmediatePredicateScanCount)) {
            if(IsEven(x)) {
                count=checked(count+1);
            }
        }
        this.consumer.Consume(count);
    }

    // [CountBy_keySelector]
    [BenchFact,Benchmark]
    public void CountBy_keySelector() => this.CountBy_keySelector_Impl();

    private void CountBy_keySelector_Impl() {
        var source = this.SourceSpan(GroupingCount);
        var map = new Dictionary<int,int>(source.Length);
        foreach(var x in source) {
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(map,Mod(x,1000),out _);
            count=checked(count+1);
        }
        var consumer = this.consumer;
        foreach(var pair in map) {
            consumer.Consume(pair);
        }
    }

    // [DefaultIfEmpty]
    [BenchFact,Benchmark]
    public void DefaultIfEmpty() => this.DefaultIfEmpty_Impl();

    private void DefaultIfEmpty_Impl() {
        var source = this.SourceSpan(StreamShapeCount);
        var defaultValue = Value(0);
        var consumer = this.consumer;
        if(source.Length==0) {
            consumer.Consume(defaultValue);
            return;
        }
        foreach(var x in source) {
            consumer.Consume(x);
        }
    }

    // [Distinct]
    [BenchFact,Benchmark]
    public void Distinct() => this.Distinct_Impl();

    private void Distinct_Impl() {
        var source = this.SourceSpan(SetCount);
        var set = new HashSet<T>(source.Length);
        var consumer = this.consumer;
        foreach(var x in source) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [DistinctBy_keySelector]
    [BenchFact,Benchmark]
    public void DistinctBy_keySelector() => this.DistinctBy_keySelector_Impl();

    private void DistinctBy_keySelector_Impl() {
        var source = this.SourceSpan(SetCount);
        var set = new HashSet<int>(source.Length);
        var consumer = this.consumer;
        foreach(var x in source) {
            if(set.Add(Mod(x,1000))) {
                consumer.Consume(x);
            }
        }
    }

    // [ElementAt_index]
    [BenchFact,Benchmark]
    public void ElementAt_index() => ExecuteOrderDependent(() => this.ElementAt_index_Impl());

    private void ElementAt_index_Impl() {
        var index = this.MidpointIndex(ImmediateShortCircuitCount);
        var source = this.SourceSpan(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(ElementAt_index_iterationCount);i++) {
            if((uint)index>=(uint)source.Length) {
                throw IdealErrors.IndexOutOfRange();
            }
            this.consumer.Consume(source[index]);
        }
    }

    // [ElementAtOrDefault_index]
    [BenchFact,Benchmark]
    public void ElementAtOrDefault_index() => ExecuteOrderDependent(() => this.ElementAtOrDefault_index_Impl());

    private void ElementAtOrDefault_index_Impl() {
        var index = this.MidpointIndex(ImmediateShortCircuitCount);
        var source = this.SourceSpan(ImmediateShortCircuitCount);
        if((uint)index<(uint)source.Length) {
            this.consumer.Consume(source[index]);
        } else {
            this.consumer.Consume(default(T));
        }
    }

    // [Empty]
    [BenchFact,Benchmark]
    public void Empty() => this.Empty_Impl();

    private void Empty_Impl() {
        for(var i = 0;i<IterationCount(Empty_iterationCount);i++) {
        }
    }

    // [Except_second]
    [BenchFact,Benchmark]
    public void Except_second() => this.Except_second_Impl();

    private void Except_second_Impl() {
        var first = this.SourceSpan(SetCount);
        var second = this.SecondSpan(SetCount);
        var set = new HashSet<T>(first.Length+second.Length);
        foreach(var x in second) {
            set.Add(x);
        }
        var consumer = this.consumer;
        foreach(var x in first) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [ExceptBy_keySelector]
    [BenchFact,Benchmark]
    public void ExceptBy_keySelector() => this.ExceptBy_keySelector_Impl();

    private void ExceptBy_keySelector_Impl() {
        var first = this.SourceSpan(SetCount);
        var second = this.SecondSpan(SetCount);
        var set = new HashSet<T>(first.Length+second.Length);
        foreach(var key in second) {
            set.Add(key);
        }
        var consumer = this.consumer;
        foreach(var x in first) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [First]
    [BenchFact,Benchmark]
    public void First() => ExecuteOrderDependent(() => this.First_Impl());

    private void First_Impl() {
        var source = this.SourceSpan(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(First_iterationCount);i++) {
            if(source.Length==0) {
                throw IdealErrors.NoElements();
            }
            this.consumer.Consume(source[0]);
        }
    }

    // [FirstOrDefault]
    [BenchFact,Benchmark]
    public void FirstOrDefault() => ExecuteOrderDependent(() => this.FirstOrDefault_Impl());

    private void FirstOrDefault_Impl() {
        var source = this.SourceSpan(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(FirstOrDefault_iterationCount);i++) {
            if(source.Length==0) {
                this.consumer.Consume(default(T));
            } else {
                this.consumer.Consume(source[0]);
            }
        }
    }

    // [FirstOrDefault_predicate]
    [BenchFact,Benchmark]
    public void FirstOrDefault_predicate() => ExecuteOrderDependent(() => this.FirstOrDefault_predicate_Impl());

    private void FirstOrDefault_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        T result = default!;
        foreach(var x in this.SourceSpan(ImmediatePredicateScanCount)) {
            if(GreaterThanOrEqual(x,value)) {
                result=x;
                break;
            }
        }
        this.consumer.Consume(result);
    }

    // [GroupBy_elementSelector]
    [BenchFact,Benchmark]
    public void GroupBy_elementSelector() => this.GroupBy_elementSelector_Impl();

    private void GroupBy_elementSelector_Impl() {
        var source = this.SourceSpan(GroupingCount);
        using var groups = new PooledGroups<int,int>(source.Length);
        for(var index = 0;index<source.Length;index++) {
            var x = source[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=Number(x)*2;
        }
        var sorted = new int[source.Length];
        groups.Group(source.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,int>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [GroupBy_elementSelector_resultSelector]
    [BenchFact,Benchmark]
    public void GroupBy_elementSelector_resultSelector() => this.GroupBy_elementSelector_resultSelector_Impl();

    private void GroupBy_elementSelector_resultSelector_Impl() {
        var source = this.SourceSpan(GroupingCount);
        var keys = new HashSet<int>(source.Length);
        var consumer = this.consumer;
        foreach(var x in source) {
            var key = Mod(x,1000);
            _=Number(x)*2;
            if(keys.Add(key)) {
                consumer.Consume(key);
            }
        }
    }

    // [GroupBy_keySelector]
    [BenchFact,Benchmark]
    public void GroupBy_keySelector() => this.GroupBy_keySelector_Impl();

    private void GroupBy_keySelector_Impl() {
        var source = this.SourceSpan(GroupingCount);
        using var groups = new PooledGroups<int,T>(source.Length);
        for(var index = 0;index<source.Length;index++) {
            var x = source[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sorted = new T[source.Length];
        groups.Group(source.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,T>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [GroupBy_resultSelector]
    [BenchFact,Benchmark]
    public void GroupBy_resultSelector() => this.GroupBy_resultSelector_Impl();

    private void GroupBy_resultSelector_Impl() {
        var source = this.SourceSpan(GroupingCount);
        var keys = new HashSet<int>(source.Length);
        var consumer = this.consumer;
        foreach(var x in source) {
            var key = Mod(x,1000);
            if(keys.Add(key)) {
                consumer.Consume(key);
            }
        }
    }

    // [GroupJoin_inner]
    [BenchFact,Benchmark]
    public void GroupJoin_inner() => this.GroupJoin_inner_Impl();

    private void GroupJoin_inner_Impl() {
        // 結果選択子は内側のグループを使わず、キー選択子は恒等で純粋なので、内側の探索表は結果に影響しない(FLinq の Range 版と同じ扱い)。
        var outer = this.SourceSpan(JoinCount);
        _=this.SecondSpan(JoinCount);
        var consumer = this.consumer;
        foreach(var x in outer) {
            consumer.Consume(x);
        }
    }

    // [Index]
    [BenchFact,Benchmark]
    public void Index() => ExecuteOrderDependent(() => this.Index_Impl());

    private void Index_Impl() {
        var source = this.SourceSpan(StreamOneToOneCount);
        var consumer = this.consumer;
        for(var index = 0;index<source.Length;index++) {
            consumer.Consume((Index: index,Item: source[index]));
        }
    }

    // [Intersect_second]
    [BenchFact,Benchmark]
    public void Intersect_second() => this.Intersect_second_Impl();

    private void Intersect_second_Impl() {
        var first = this.SourceSpan(SetCount);
        var second = this.SecondSpan(SetCount);
        var set = new HashSet<T>(second.Length);
        foreach(var x in second) {
            set.Add(x);
        }
        var consumer = this.consumer;
        foreach(var x in first) {
            if(set.Remove(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [IntersectBy_keySelector]
    [BenchFact,Benchmark]
    public void IntersectBy_keySelector() => this.IntersectBy_keySelector_Impl();

    private void IntersectBy_keySelector_Impl() {
        var first = this.SourceSpan(SetCount);
        var second = this.SecondSpan(SetCount);
        var set = new HashSet<T>(second.Length);
        foreach(var key in second) {
            set.Add(key);
        }
        var consumer = this.consumer;
        foreach(var x in first) {
            if(set.Remove(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [InvariantHoisting]
    [BenchFact,Benchmark]
    public void InvariantHoisting() => this.InvariantHoisting_Impl();

    private void InvariantHoisting_Impl() {
        var source = this.SourceSpan(StreamOneToOneCount);
        var invariant = global::Common.HoistInvariant.Value();
        var consumer = this.consumer;
        foreach(var x in source) {
            consumer.Consume(Number(x)+invariant);
        }
    }

    // [Join_inner]
    [BenchFact,Benchmark]
    public void Join_inner() => this.Join_inner_Impl();

    private void Join_inner_Impl() {
        var outer = this.SourceSpan(JoinCount);
        var inner = this.SecondSpan(JoinCount);
        var counts = new Dictionary<T,int>(inner.Length);
        foreach(var x in inner) {
            ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts,x,out _);
            count++;
        }
        var consumer = this.consumer;
        foreach(var x in outer) {
            if(counts.TryGetValue(x,out var count)) {
                for(var match = 0;match<count;match++) {
                    consumer.Consume(x);
                }
            }
        }
    }

    // [Join_innerKey_Hoisting]
    [BenchFact,Benchmark]
    public void Join_innerKey_Hoisting() => this.Join_innerKey_Hoisting_Impl();

    private void Join_innerKey_Hoisting_Impl() {
        var outer = this.SourceSpan(JoinCount);
        var inner = this.SecondSpan(JoinCount);
        var invariant = global::Common.HoistInvariant.Value();
        using var groups = new PooledGroups<int,T>(inner.Length);
        for(var index = 0;index<inner.Length;index++) {
            var x = inner[index];
            groups.Keys[index]=(Number(x)+invariant)%1000;
            groups.Elements[index]=x;
        }
        var sorted = ArrayPool<T>.Shared.Rent(inner.Length);
        try {
            groups.Group(inner.Length,sorted);
            var consumer = this.consumer;
            foreach(var o in outer) {
                if(groups.Map.TryGetValue(Mod(o,1000),out var group)) {
                    var end = groups.Starts[group]+groups.Counts[group];
                    for(var index = groups.Starts[group];index<end;index++) {
                        consumer.Consume(Number(o)+Number(sorted[index]));
                    }
                }
            }
        } finally {
            ArrayPool<T>.Shared.Return(sorted);
        }
    }

    // [Join_outerKey_Hoisting]
    [BenchFact,Benchmark]
    public void Join_outerKey_Hoisting() => this.Join_outerKey_Hoisting_Impl();

    private void Join_outerKey_Hoisting_Impl() {
        var outer = this.SourceSpan(JoinCount);
        var inner = this.SecondSpan(JoinCount);
        var invariant = global::Common.HoistInvariant.Value();
        using var groups = new PooledGroups<int,T>(inner.Length);
        for(var index = 0;index<inner.Length;index++) {
            var x = inner[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sorted = ArrayPool<T>.Shared.Rent(inner.Length);
        try {
            groups.Group(inner.Length,sorted);
            var consumer = this.consumer;
            foreach(var o in outer) {
                if(groups.Map.TryGetValue((Number(o)+invariant)%1000,out var group)) {
                    var end = groups.Starts[group]+groups.Counts[group];
                    for(var index = groups.Starts[group];index<end;index++) {
                        consumer.Consume(Number(o)+Number(sorted[index]));
                    }
                }
            }
        } finally {
            ArrayPool<T>.Shared.Return(sorted);
        }
    }

    // [Join_resultSelector_Hoisting]
    [BenchFact,Benchmark]
    public void Join_resultSelector_Hoisting() => this.Join_resultSelector_Hoisting_Impl();

    private void Join_resultSelector_Hoisting_Impl() {
        var outer = this.SourceSpan(JoinCount);
        var inner = this.SecondSpan(JoinCount);
        var invariant = global::Common.HoistInvariant.Value();
        using var groups = new PooledGroups<int,T>(inner.Length);
        for(var index = 0;index<inner.Length;index++) {
            var x = inner[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sorted = ArrayPool<T>.Shared.Rent(inner.Length);
        try {
            groups.Group(inner.Length,sorted);
            var consumer = this.consumer;
            foreach(var o in outer) {
                if(groups.Map.TryGetValue(Mod(o,1000),out var group)) {
                    var end = groups.Starts[group]+groups.Counts[group];
                    for(var index = groups.Starts[group];index<end;index++) {
                        consumer.Consume(Number(o)+Number(sorted[index])+invariant);
                    }
                }
            }
        } finally {
            ArrayPool<T>.Shared.Return(sorted);
        }
    }

    // [Last]
    [BenchFact,Benchmark]
    public void Last() => ExecuteOrderDependent(() => this.Last_Impl());

    private void Last_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(source[^1]);
    }

    // [LastOrDefault]
    [BenchFact,Benchmark]
    public void LastOrDefault() => ExecuteOrderDependent(() => this.LastOrDefault_Impl());

    private void LastOrDefault_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            this.consumer.Consume(default(T));
        } else {
            this.consumer.Consume(source[^1]);
        }
    }

    // [LastOrDefault_predicate]
    [BenchFact,Benchmark]
    public void LastOrDefault_predicate() => ExecuteOrderDependent(() => this.LastOrDefault_predicate_Impl());

    private void LastOrDefault_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        var source = this.SourceSpan(ImmediatePredicateScanCount);
        T result = default!;
        // System.Linq は IList のとき末尾から述語を評価する。
        for(var index = source.Length-1;index>=0;index--) {
            if(GreaterThanOrEqual(source[index],value)) {
                result=source[index];
                break;
            }
        }
        this.consumer.Consume(result);
    }

    // [LongCount]
    [BenchFact,Benchmark]
    public void LongCount() => this.LongCount_Impl();

    private void LongCount_Impl() {
        if(global::Common.BenchmarkExecutionMode.IsElementCountScoped) {
            this.consumer.Consume((long)this.EffectiveSourceCount(ImmediateFullScanCount));
            return;
        }
        this.consumer.Consume((long)this.SourceSpan(ImmediateFullScanCount).Length);
    }

    // [LongCount_predicate]
    [BenchFact,Benchmark]
    public void LongCount_predicate() => this.LongCount_predicate_Impl();

    private void LongCount_predicate_Impl() {
        var count = 0L;
        foreach(var x in this.SourceSpan(ImmediatePredicateScanCount)) {
            if(IsEven(x)) {
                count=checked(count+1);
            }
        }
        this.consumer.Consume(count);
    }

    // [Max]
    [BenchFact,Benchmark]
    public void Max() => this.Max_Impl();

    private void Max_Impl() => this.consumer.Consume(SpanMax(this.SourceSpan(ImmediateFullScanCount)));

    // [Max_selector]
    [BenchFact,Benchmark]
    public void Max_selector() => this.Max_selector_Impl();

    private void Max_selector_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            throw IdealErrors.NoElements();
        }
        var result = Number(source[0])*2;
        for(var index = 1;index<source.Length;index++) {
            var value = Number(source[index])*2;
            if(value>result) {
                result=value;
            }
        }
        this.consumer.Consume(result);
    }

    // [MaxBy_keySelector]
    [BenchFact,Benchmark]
    public void MaxBy_keySelector() => this.MaxBy_keySelector_Impl();

    private void MaxBy_keySelector_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            if(default(T) is null) {
                this.consumer.Consume(default(T));
                return;
            }
            throw IdealErrors.NoElements();
        }
        var result = source[0];
        var key = Mod(result,1000);
        for(var index = 1;index<source.Length;index++) {
            var nextKey = Mod(source[index],1000);
            if(nextKey>key) {
                key=nextKey;
                result=source[index];
            }
        }
        this.consumer.Consume(result);
    }

    // [Min]
    [BenchFact,Benchmark]
    public void Min() => this.Min_Impl();

    private void Min_Impl() => this.consumer.Consume(SpanMin(this.SourceSpan(ImmediateFullScanCount)));

    // [Min_selector]
    [BenchFact,Benchmark]
    public void Min_selector() => this.Min_selector_Impl();

    private void Min_selector_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            throw IdealErrors.NoElements();
        }
        var result = Number(source[0])*2;
        for(var index = 1;index<source.Length;index++) {
            var value = Number(source[index])*2;
            if(value<result) {
                result=value;
            }
        }
        this.consumer.Consume(result);
    }

    // [MinBy_keySelector]
    [BenchFact,Benchmark]
    public void MinBy_keySelector() => this.MinBy_keySelector_Impl();

    private void MinBy_keySelector_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        if(source.Length==0) {
            if(default(T) is null) {
                this.consumer.Consume(default(T));
                return;
            }
            throw IdealErrors.NoElements();
        }
        var result = source[0];
        var key = Mod(result,1000);
        for(var index = 1;index<source.Length;index++) {
            var nextKey = Mod(source[index],1000);
            if(nextKey<key) {
                key=nextKey;
                result=source[index];
            }
        }
        this.consumer.Consume(result);
    }

    // [OfType]
    [BenchFact,Benchmark]
    public void OfType() => this.OfType_Impl();

    private void OfType_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(StreamOneToOneCount)) {
            if(x is not null) {
                consumer.Consume(x);
            }
        }
    }

    // [Order]
    [BenchFact,Benchmark]
    public void Order() => ExecuteOrderDependent(() => this.Order_Impl());

    private void Order_Impl() => ConsumeSorted(this.SourceSpan(SortCount),false,this.consumer);

    // [OrderBy_keySelector]
    [BenchFact,Benchmark]
    public void OrderBy_keySelector() => ExecuteOrderDependent(() => this.OrderBy_keySelector_Impl());

    private void OrderBy_keySelector_Impl() => ConsumeSorted(this.SourceSpan(SortCount),false,this.consumer);

    // [OrderByDescending_keySelector]
    [BenchFact,Benchmark]
    public void OrderByDescending_keySelector() => ExecuteOrderDependent(() => this.OrderByDescending_keySelector_Impl());

    private void OrderByDescending_keySelector_Impl() => ConsumeSorted(this.SourceSpan(SortCount),true,this.consumer);

    // [OrderDescending]
    [BenchFact,Benchmark]
    public void OrderDescending() => ExecuteOrderDependent(() => this.OrderDescending_Impl());

    private void OrderDescending_Impl() => ConsumeSorted(this.SourceSpan(SortCount),true,this.consumer);

    // [Prepend_element]
    [BenchFact,Benchmark]
    public void Prepend_element() => ExecuteOrderDependent(() => this.Prepend_element_Impl());

    private void Prepend_element_Impl() {
        var source = this.SourceSpan(StreamShapeCount);
        var element = Value(0);
        var consumer = this.consumer;
        consumer.Consume(element);
        foreach(var x in source) {
            consumer.Consume(x);
        }
    }

    // [Repeat]
    [BenchFact,Benchmark]
    public void Repeat() => this.Repeat_Impl();

    private void Repeat_Impl() {
        var element = Value(7);
        var count = this.EffectiveSourceCount(GeneratedCount);
        if(count<0) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        var consumer = this.consumer;
        for(var index = 0;index<count;index++) {
            consumer.Consume(element);
        }
    }

    // [Reverse]
    [BenchFact,Benchmark]
    public void Reverse() => ExecuteOrderDependent(() => this.Reverse_Impl());

    private void Reverse_Impl() {
        var source = this.SourceSpan(StreamShapeCount);
        var consumer = this.consumer;
        for(var index = source.Length-1;index>=0;index--) {
            consumer.Consume(source[index]);
        }
    }

    // [Select_indexSelector]
    [BenchFact,Benchmark]
    public void Select_indexSelector() => ExecuteOrderDependent(() => this.Select_indexSelector_Impl());

    private void Select_indexSelector_Impl() {
        var source = this.SourceSpan(StreamOneToOneCount);
        var consumer = this.consumer;
        for(var index = 0;index<source.Length;index++) {
            consumer.Consume(Number(source[index])+index);
        }
    }

    // [Select_selector]
    [BenchFact,Benchmark]
    public void Select_selector() => this.Select_selector_Impl();

    private void Select_selector_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(StreamOneToOneCount)) {
            consumer.Consume(Add(x,x));
        }
    }

    // [SelectMany_indexSelector]
    [BenchFact,Benchmark]
    public void SelectMany_indexSelector() => this.SelectMany_indexSelector_Impl();

    private void SelectMany_indexSelector_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcNestedCount(NestedCount)) {
            foreach(var item in x) {
                consumer.Consume(item);
            }
        }
    }

    // [SelectMany_indexSelector_resultSelector]
    [BenchFact,Benchmark]
    public void SelectMany_indexSelector_resultSelector() => this.SelectMany_indexSelector_resultSelector_Impl();

    private void SelectMany_indexSelector_resultSelector_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcNestedCount(NestedCount)) {
            foreach(var item in x) {
                consumer.Consume(item+1);
            }
        }
    }

    // [SelectMany_resultSelector]
    [BenchFact,Benchmark]
    public void SelectMany_resultSelector() => this.SelectMany_resultSelector_Impl();

    private void SelectMany_resultSelector_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcNestedCount(NestedCount)) {
            foreach(var item in x) {
                consumer.Consume(item+1);
            }
        }
    }

    // [SelectMany_selector]
    [BenchFact,Benchmark]
    public void SelectMany_selector() => this.SelectMany_selector_Impl();

    private void SelectMany_selector_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcNestedCount(NestedCount)) {
            foreach(var item in x) {
                consumer.Consume(item);
            }
        }
    }

    // [SequenceEqual_second]
    [BenchFact,Benchmark]
    public void SequenceEqual_second() => ExecuteOrderDependent(() => this.SequenceEqual_second_Impl());

    private void SequenceEqual_second_Impl() {
        var first = this.SourceSpan(StreamShapeCount);
        var second = this.SourceSpan(StreamShapeCount);
        this.consumer.Consume(first.SequenceEqual(second,null));
    }

    [BenchFact,Benchmark]
    public void SequenceEqual_second_comparer() => ExecuteOrderDependent(() => this.SequenceEqual_second_comparer_Impl());

    private void SequenceEqual_second_comparer_Impl() {
        var first = this.SourceSpan(StreamShapeCount);
        var second = this.SourceSpan(StreamShapeCount);
        this.consumer.Consume(first.SequenceEqual(second,EqualityComparer<T>.Default));
    }

    // [Single]
    [BenchFact,Benchmark]
    public void Single() => this.Single_Impl();

    private void Single_Impl() {
        var source = this.OneSpan();
        for(var i = 0;i<IterationCount(Single_iterationCount);i++) {
            if(source.Length==0) {
                throw IdealErrors.NoElements();
            }
            if(source.Length!=1) {
                throw IdealErrors.MoreThanOneElement();
            }
            this.consumer.Consume(source[0]);
        }
    }

    // [Single_predicate]
    [BenchFact,Benchmark]
    public void Single_predicate() => this.Single_predicate_Impl();

    private void Single_predicate_Impl() {
        _=SinglePredicate(this.OneSpan(),0);
        this.consumer.Consume(SinglePredicate(this.SourceSpan(ImmediatePredicateScanCount),1));
    }

    // [SingleOrDefault]
    [BenchFact,Benchmark]
    public void SingleOrDefault() => this.SingleOrDefault_Impl();

    private void SingleOrDefault_Impl() {
        var source = this.OneSpan();
        for(var i = 0;i<IterationCount(SingleOrDefault_iterationCount);i++) {
            if(source.Length==0) {
                this.consumer.Consume(default(T));
                continue;
            }
            if(source.Length!=1) {
                throw IdealErrors.MoreThanOneElement();
            }
            this.consumer.Consume(source[0]);
        }
    }

    // [SingleOrDefault_predicate]
    [BenchFact,Benchmark]
    public void SingleOrDefault_predicate() => this.SingleOrDefault_predicate_Impl();

    private void SingleOrDefault_predicate_Impl() {
        T result = default!;
        var found = false;
        foreach(var x in this.SourceSpan(ImmediatePredicateScanCount)) {
            if(EqualNumber(x,1)) {
                if(found) {
                    throw IdealErrors.MoreThanOneMatch();
                }
                found=true;
                result=x;
            }
        }
        this.consumer.Consume(result);
    }

    // [Skip_count]
    [BenchFact,Benchmark]
    public void Skip_count() => ExecuteOrderDependent(() => this.Skip_count_Impl());

    private void Skip_count_Impl() {
        var source = this.SourceSpan(StreamPartitionCount);
        var count = Math.Clamp(this.QuarterCount(StreamPartitionCount),0,source.Length);
        var consumer = this.consumer;
        foreach(var x in source[count..]) {
            consumer.Consume(x);
        }
    }

    // [SkipLast_count]
    [BenchFact,Benchmark]
    public void SkipLast_count() => ExecuteOrderDependent(() => this.SkipLast_count_Impl());

    private void SkipLast_count_Impl() {
        var source = this.SourceSpan(StreamPartitionCount);
        var count = Math.Clamp(this.QuarterCount(StreamPartitionCount),0,source.Length);
        var consumer = this.consumer;
        foreach(var x in source[..^count]) {
            consumer.Consume(x);
        }
    }

    // [SkipWhile_indexPredicate]
    [BenchFact,Benchmark]
    public void SkipWhile_indexPredicate() => ExecuteOrderDependent(() => this.SkipWhile_indexPredicate_Impl());

    private void SkipWhile_indexPredicate_Impl() {
        var takeUntil = this.QuarterCount(StreamFilterCount);
        var source = this.SourceSpan(StreamFilterCount);
        var index = 0;
        while(index<source.Length&&index<takeUntil) {
            index++;
        }
        var consumer = this.consumer;
        for(;index<source.Length;index++) {
            consumer.Consume(source[index]);
        }
    }

    // [SkipWhile_predicate]
    [BenchFact,Benchmark]
    public void SkipWhile_predicate() => ExecuteOrderDependent(() => this.SkipWhile_predicate_Impl());

    private void SkipWhile_predicate_Impl() {
        var source = this.SourceSpan(StreamFilterCount);
        var bound = this.QuarterCount(StreamFilterCount)+1;
        var index = 0;
        while(index<source.Length&&LessThan(source[index],bound)) {
            index++;
        }
        var consumer = this.consumer;
        for(;index<source.Length;index++) {
            consumer.Consume(source[index]);
        }
    }

    // [Sum]
    [BenchFact,Benchmark]
    public void Sum() => this.Sum_Impl();

    private void Sum_Impl() {
        var sum = 0;
        foreach(var x in this.srcMinusPlusCount(ImmediateFullScanCount)) {
            sum=checked(sum+Number(x));
        }
        this.consumer.Consume(sum);
    }

    // [Sum_selector]
    [BenchFact,Benchmark]
    public void Sum_selector() => this.Sum_selector_Impl();

    private void Sum_selector_Impl() {
        var sum = 0L;
        foreach(var x in this.SourceSpan(ImmediateFullScanCount)) {
            sum=checked(sum+LongNumber(x));
        }
        this.consumer.Consume(sum);
    }

    // [Take_count]
    [BenchFact,Benchmark]
    public void Take_count() => ExecuteOrderDependent(() => this.Take_count_Impl());

    private void Take_count_Impl() {
        var source = this.SourceSpan(StreamPartitionCount);
        var count = Math.Clamp(this.ThreeQuarterCount(StreamPartitionCount),0,source.Length);
        var consumer = this.consumer;
        foreach(var x in source[..count]) {
            consumer.Consume(x);
        }
    }

    // [TakeLast_count]
    [BenchFact,Benchmark]
    public void TakeLast_count() => ExecuteOrderDependent(() => this.TakeLast_count_Impl());

    private void TakeLast_count_Impl() {
        var source = this.SourceSpan(StreamPartitionCount);
        var count = Math.Clamp(this.QuarterCount(StreamPartitionCount),0,source.Length);
        var consumer = this.consumer;
        foreach(var x in source[^count..]) {
            consumer.Consume(x);
        }
    }

    // [TakeWhile_indexPredicate]
    [BenchFact,Benchmark]
    public void TakeWhile_indexPredicate() => ExecuteOrderDependent(() => this.TakeWhile_indexPredicate_Impl());

    private void TakeWhile_indexPredicate_Impl() {
        var takeUntil = this.ThreeQuarterCount(StreamFilterCount);
        var source = this.SourceSpan(StreamFilterCount);
        var consumer = this.consumer;
        for(var index = 0;index<source.Length&&index<takeUntil;index++) {
            consumer.Consume(source[index]);
        }
    }

    // [TakeWhile_predicate]
    [BenchFact,Benchmark]
    public void TakeWhile_predicate() => ExecuteOrderDependent(() => this.TakeWhile_predicate_Impl());

    private void TakeWhile_predicate_Impl() {
        var source = this.SourceSpan(StreamFilterCount);
        var bound = this.ThreeQuarterCount(StreamFilterCount)+1;
        var consumer = this.consumer;
        foreach(var x in source) {
            if(!LessThan(x,bound)) {
                break;
            }
            consumer.Consume(x);
        }
    }

    // [ToArray]
    [BenchFact,Benchmark]
    public void ToArray() => this.ToArray_Impl();

    private void ToArray_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(MaterializeLinearCount)) {
            consumer.Consume(x);
        }
    }

    // [ToDictionary_elementSelector]
    [BenchFact,Benchmark]
    public void ToDictionary_elementSelector() => this.ToDictionary_elementSelector_Impl();

    private void ToDictionary_elementSelector_Impl() {
        var source = this.SourceSpan(MaterializeHashCount);
        var dictionary = new Dictionary<T,int>(source.Length);
        foreach(var x in source) {
            dictionary.Add(x,Number(x)*2);
        }
        var consumer = this.consumer;
        foreach(var pair in dictionary) {
            consumer.Consume(pair);
        }
    }

    // [ToDictionary_keySelector]
    [BenchFact,Benchmark]
    public void ToDictionary_keySelector() => this.ToDictionary_keySelector_Impl();

    private void ToDictionary_keySelector_Impl() {
        var source = this.SourceSpan(MaterializeHashCount);
        var dictionary = new Dictionary<T,T>(source.Length);
        foreach(var x in source) {
            dictionary.Add(x,x);
        }
        var consumer = this.consumer;
        foreach(var pair in dictionary) {
            consumer.Consume(pair);
        }
    }

    // [ToHashSet]
    [BenchFact,Benchmark]
    public void ToHashSet() => this.ToHashSet_Impl();

    private void ToHashSet_Impl() {
        var source = this.SourceSpan(MaterializeHashCount);
        var set = new HashSet<T>(source.Length);
        var consumer = this.consumer;
        foreach(var x in source) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [ToList]
    [BenchFact,Benchmark]
    public void ToList() => this.ToList_Impl();

    private void ToList_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(MaterializeLinearCount)) {
            consumer.Consume(x);
        }
    }

    // [ToLookup_elementSelector]
    [BenchFact,Benchmark]
    public void ToLookup_elementSelector() => this.ToLookup_elementSelector_Impl();

    private void ToLookup_elementSelector_Impl() {
        var source = this.SourceSpan(MaterializeLookupCount);
        using var groups = new PooledGroups<int,int>(source.Length);
        for(var index = 0;index<source.Length;index++) {
            var x = source[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=Number(x)*2;
        }
        var sorted = new int[source.Length];
        groups.Group(source.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,int>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [ToLookup_keySelector]
    [BenchFact,Benchmark]
    public void ToLookup_keySelector() => this.ToLookup_keySelector_Impl();

    private void ToLookup_keySelector_Impl() {
        var source = this.SourceSpan(MaterializeLookupCount);
        using var groups = new PooledGroups<int,T>(source.Length);
        for(var index = 0;index<source.Length;index++) {
            var x = source[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sorted = new T[source.Length];
        groups.Group(source.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,T>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [TryGetNonEnumeratedCount]
    [BenchFact,Benchmark]
    public void TryGetNonEnumeratedCount() => this.TryGetNonEnumeratedCount_Impl();

    private void TryGetNonEnumeratedCount_Impl() {
        var source = this.SourceSpan(ImmediateFullScanCount);
        for(var i = 0;i<IterationCount(TryGetNonEnumeratedCount_iterationCount);i++) {
            ConsumeResult(this.consumer,true,source.Length);
        }
    }

    // [Union_second]
    [BenchFact,Benchmark]
    public void Union_second() => this.Union_second_Impl();

    private void Union_second_Impl() {
        var first = this.SourceSpan(SetCount);
        var second = this.SecondSpan(SetCount);
        var set = new HashSet<T>(first.Length+second.Length);
        var consumer = this.consumer;
        foreach(var x in first) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
        foreach(var x in second) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [UnionBy_keySelector]
    [BenchFact,Benchmark]
    public void UnionBy_keySelector() => this.UnionBy_keySelector_Impl();

    private void UnionBy_keySelector_Impl() {
        var first = this.SourceSpan(SetCount);
        var second = this.SecondSpan(SetCount);
        var set = new HashSet<T>(first.Length+second.Length);
        var consumer = this.consumer;
        foreach(var x in first) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
        foreach(var x in second) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [Where_indexPredicate]
    [BenchFact,Benchmark]
    public void Where_indexPredicate() => ExecuteOrderDependent(() => this.Where_indexPredicate_Impl());

    private void Where_indexPredicate_Impl() {
        var source = this.SourceSpan(StreamFilterCount);
        var consumer = this.consumer;
        for(var index = 0;index<source.Length;index++) {
            if(index%2==0) {
                consumer.Consume(source[index]);
            }
        }
    }

    // [Where_predicate]
    [BenchFact,Benchmark]
    public void Where_predicate() => this.Where_predicate_Impl();

    private void Where_predicate_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(StreamFilterCount)) {
            if(IsEven(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [Where_SourceShape]
    [BenchFact,Benchmark]
    public void Where_SourceShape() => this.Where_SourceShape_Impl();

    private void Where_SourceShape_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.SourceSpan(SourceShapeCount)) {
            if(IsEven(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [Zip_second]
    [BenchFact,Benchmark]
    public void Zip_second() => ExecuteOrderDependent(() => this.Zip_second_Impl());

    private void Zip_second_Impl() {
        var first = this.SourceSpan(StreamShapeCount);
        var second = this.SecondSpan(StreamShapeCount);
        var length = Math.Min(first.Length,second.Length);
        var consumer = this.consumer;
        for(var index = 0;index<length;index++) {
            consumer.Consume(Add(first[index],second[index]));
        }
    }

    // [Zip_tuple]
    [BenchFact,Benchmark]
    public void Zip_tuple() => ExecuteOrderDependent(() => this.Zip_tuple_Impl());

    private void Zip_tuple_Impl() {
        var first = this.SourceSpan(StreamShapeCount);
        var second = this.SecondSpan(StreamShapeCount);
        var length = Math.Min(first.Length,second.Length);
        var consumer = this.consumer;
        for(var index = 0;index<length;index++) {
            consumer.Consume((First: first[index],Second: second[index]));
        }
    }
}
#endif
