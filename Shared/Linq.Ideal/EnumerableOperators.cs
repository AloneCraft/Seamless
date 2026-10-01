#if SYSTEM_IENUMERABLE
using System.Runtime.InteropServices;

namespace Shared.Linq.Ideal;

/// <summary>
/// IEnumerable 起点の理想ループ版。ソースは実行時も yield の反復子で、件数も span も得られない。
/// 静的な型(IEnumerable)から分かること以上の実行時の型判定はせず、foreach で1回だけ列挙する。
/// 全件が必要な演算子(並べ替え・グループ化など)は ArrayPool の配列へ溜めてから span と同じ処理をする。
/// </summary>
public abstract partial class IdealBase<T> {
    private static T SingleOrThrow(global::System.Collections.Generic.IEnumerable<T> source) {
        using var enumerator = source.GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw IdealErrors.NoElements();
        }
        var result = enumerator.Current;
        if(enumerator.MoveNext()) {
            throw IdealErrors.MoreThanOneElement();
        }
        return result;
    }

    private static T SinglePredicate(global::System.Collections.Generic.IEnumerable<T> source,int number) {
        T result = default!;
        var found = false;
        foreach(var x in source) {
            if(EqualNumber(x,number)) {
                if(found) {
                    throw IdealErrors.MoreThanOneMatch();
                }
                found=true;
                result=x;
            }
        }
        if(!found) {
            throw IdealErrors.NoMatch();
        }
        return result;
    }

    private static PooledBuffer<T> Buffer(global::System.Collections.Generic.IEnumerable<T> source) {
        var buffer = new PooledBuffer<T>(16);
        foreach(var x in source) {
            buffer.Add(x);
        }
        return buffer;
    }

    // [Aggregate_func]
    [BenchFact,Benchmark]
    public void Aggregate_func() => this.Aggregate_func_Impl();

    private void Aggregate_func_Impl() {
        var source = this.srcCount(ImmediateFullScanCount);
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
        using var enumerator = source.GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw IdealErrors.NoElements();
        }
        var result = enumerator.Current;
        while(enumerator.MoveNext()) {
            result=Add(result,enumerator.Current);
        }
        this.Result=result;
        this.consumer.Consume(result);
    }

    // [Aggregate_seed_func]
    [BenchFact,Benchmark]
    public void Aggregate_seed_func() => this.Aggregate_seed_func_Impl();

    private void Aggregate_seed_func_Impl() {
        var accumulate = 0L;
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            accumulate+=LongNumber(x);
        }
        this.consumer.Consume(accumulate);
    }

    // [Aggregate_seed_func_resultSelector]
    [BenchFact,Benchmark]
    public void Aggregate_seed_func_resultSelector() => this.Aggregate_seed_func_resultSelector_Impl();

    private void Aggregate_seed_func_resultSelector_Impl() {
        var accumulate = 0L;
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            accumulate+=LongNumber(x);
        }
        this.consumer.Consume((double)accumulate/1000000);
    }

    // [AggregateBy_keySelector]
    [BenchFact,Benchmark]
    public void AggregateBy_keySelector() => this.AggregateBy_keySelector_Impl();

    private void AggregateBy_keySelector_Impl() {
        var map = new Dictionary<int,int>();
        foreach(var x in this.srcCount(GroupingCount)) {
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
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
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
        var source = this.srcCount(ImmediateShortCircuitCount);
        for(var a = 0;a<IterationCount(Any_iterationCount);a++) {
            using var enumerator = source.GetEnumerator();
            this.consumer.Consume(enumerator.MoveNext());
        }
    }

    // [Any_predicate]
    [BenchFact,Benchmark]
    public void Any_predicate() => this.Any_predicate_Impl();

    private void Any_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        var result = false;
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
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
        var source = this.srcCount(StreamShapeCount);
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
        var sum = 0d;
        var count = 0L;
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            sum+=(double)Number(x);
            count++;
        }
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(sum/count);
    }

    // [Average_selector]
    [BenchFact,Benchmark]
    public void Average_selector() => this.Average_selector_Impl();

    private void Average_selector_Impl() {
        var sum = 0d;
        var count = 0L;
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            sum+=(double)Number(x);
            count++;
        }
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(sum/count);
    }

    // [Cast]
    [BenchFact,Benchmark]
    public void Cast() => this.Cast_Impl();

    private void Cast_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcCount(StreamOneToOneCount)) {
            consumer.Consume(x);
        }
    }

    // [Chunk_size]
    [BenchFact,Benchmark]
    public void Chunk_size() => ExecuteOrderDependent(() => this.Chunk_size_Impl());

    private void Chunk_size_Impl() {
        var consumer = this.consumer;
        T[]? chunk = null;
        var count = 0;
        foreach(var x in this.srcCount(StreamPartitionCount)) {
            chunk??=new T[1000];
            chunk[count++]=x;
            if(count==1000) {
                consumer.Consume(chunk);
                chunk=null;
                count=0;
            }
        }
        if(chunk is not null) {
            consumer.Consume(chunk.AsSpan(0,count).ToArray());
        }
    }

    // [Concat_second]
    [BenchFact,Benchmark]
    public void Concat_second() => ExecuteOrderDependent(() => this.Concat_second_Impl());

    private void Concat_second_Impl() {
        var first = this.srcCount(StreamShapeCount);
        var second = this.src2Count(StreamShapeCount);
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
        var source = this.srcCount(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(Contains_value_iterationCount);i++) {
            var target = Value(value);
            var result = false;
            foreach(var x in source) {
                if(EqualityComparer<T>.Default.Equals(x,target)) {
                    result=true;
                    break;
                }
            }
            this.consumer.Consume(result);
        }
    }

    // [Count]
    [BenchFact,Benchmark]
    public void Count() => this.Count_Impl();

    private void Count_Impl() {
        var count = 0;
        foreach(var _ in this.srcCount(ImmediateFullScanCount)) {
            count=checked(count+1);
        }
        this.consumer.Consume(count);
    }

    // [Count_predicate]
    [BenchFact,Benchmark]
    public void Count_predicate() => this.Count_predicate_Impl();

    private void Count_predicate_Impl() {
        var count = 0;
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
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
        var map = new Dictionary<int,int>();
        foreach(var x in this.srcCount(GroupingCount)) {
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
        var source = this.srcCount(StreamShapeCount);
        var defaultValue = Value(0);
        var consumer = this.consumer;
        var any = false;
        foreach(var x in source) {
            any=true;
            consumer.Consume(x);
        }
        if(!any) {
            consumer.Consume(defaultValue);
        }
    }

    // [Distinct]
    [BenchFact,Benchmark]
    public void Distinct() => this.Distinct_Impl();

    private void Distinct_Impl() {
        var set = new HashSet<T>();
        var consumer = this.consumer;
        foreach(var x in this.srcCount(SetCount)) {
            if(set.Add(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [DistinctBy_keySelector]
    [BenchFact,Benchmark]
    public void DistinctBy_keySelector() => this.DistinctBy_keySelector_Impl();

    private void DistinctBy_keySelector_Impl() {
        var set = new HashSet<int>();
        var consumer = this.consumer;
        foreach(var x in this.srcCount(SetCount)) {
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
        for(var i = 0;i<IterationCount(ElementAt_index_iterationCount);i++) {
            var position = 0;
            var found = false;
            T result = default!;
            if(index>=0) {
                foreach(var x in this.srcCount(ImmediateShortCircuitCount)) {
                    if(position==index) {
                        result=x;
                        found=true;
                        break;
                    }
                    position++;
                }
            }
            if(!found) {
                throw IdealErrors.IndexOutOfRange();
            }
            this.consumer.Consume(result);
        }
    }

    // [ElementAtOrDefault_index]
    [BenchFact,Benchmark]
    public void ElementAtOrDefault_index() => ExecuteOrderDependent(() => this.ElementAtOrDefault_index_Impl());

    private void ElementAtOrDefault_index_Impl() {
        var index = this.MidpointIndex(ImmediateShortCircuitCount);
        T result = default!;
        if(index>=0) {
            var position = 0;
            foreach(var x in this.srcCount(ImmediateShortCircuitCount)) {
                if(position==index) {
                    result=x;
                    break;
                }
                position++;
            }
        }
        this.consumer.Consume(result);
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
        var first = this.srcCount(SetCount);
        var second = this.src2Count(SetCount);
        var set = new HashSet<T>();
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
        var first = this.srcCount(SetCount);
        var second = this.src2Count(SetCount);
        var set = new HashSet<T>();
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
        var source = this.srcCount(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(First_iterationCount);i++) {
            using var enumerator = source.GetEnumerator();
            if(!enumerator.MoveNext()) {
                throw IdealErrors.NoElements();
            }
            this.consumer.Consume(enumerator.Current);
        }
    }

    // [FirstOrDefault]
    [BenchFact,Benchmark]
    public void FirstOrDefault() => ExecuteOrderDependent(() => this.FirstOrDefault_Impl());

    private void FirstOrDefault_Impl() {
        var source = this.srcCount(ImmediateShortCircuitCount);
        for(var i = 0;i<IterationCount(FirstOrDefault_iterationCount);i++) {
            using var enumerator = source.GetEnumerator();
            if(enumerator.MoveNext()) {
                this.consumer.Consume(enumerator.Current);
            } else {
                this.consumer.Consume(default(T));
            }
        }
    }

    // [FirstOrDefault_predicate]
    [BenchFact,Benchmark]
    public void FirstOrDefault_predicate() => ExecuteOrderDependent(() => this.FirstOrDefault_predicate_Impl());

    private void FirstOrDefault_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        T result = default!;
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
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
        using var source = Buffer(this.srcCount(GroupingCount));
        var span = source.Span;
        using var groups = new PooledGroups<int,int>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=Number(x)*2;
        }
        var sorted = new int[span.Length];
        groups.Group(span.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,int>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [GroupBy_elementSelector_resultSelector]
    [BenchFact,Benchmark]
    public void GroupBy_elementSelector_resultSelector() => this.GroupBy_elementSelector_resultSelector_Impl();

    private void GroupBy_elementSelector_resultSelector_Impl() {
        var keys = new HashSet<int>();
        var consumer = this.consumer;
        foreach(var x in this.srcCount(GroupingCount)) {
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
        using var source = Buffer(this.srcCount(GroupingCount));
        var span = source.Span;
        using var groups = new PooledGroups<int,T>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sorted = new T[span.Length];
        groups.Group(span.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,T>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [GroupBy_resultSelector]
    [BenchFact,Benchmark]
    public void GroupBy_resultSelector() => this.GroupBy_resultSelector_Impl();

    private void GroupBy_resultSelector_Impl() {
        var keys = new HashSet<int>();
        var consumer = this.consumer;
        foreach(var x in this.srcCount(GroupingCount)) {
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
        var outer = this.srcCount(JoinCount);
        var inner = this.src2Count(JoinCount);
        var lookup = new HashSet<T>();
        foreach(var x in inner) {
            lookup.Add(x);
        }
        var consumer = this.consumer;
        foreach(var x in outer) {
            _=lookup.Contains(x);
            consumer.Consume(x);
        }
    }

    // [Index]
    [BenchFact,Benchmark]
    public void Index() => ExecuteOrderDependent(() => this.Index_Impl());

    private void Index_Impl() {
        var consumer = this.consumer;
        var index = 0;
        foreach(var x in this.srcCount(StreamOneToOneCount)) {
            consumer.Consume((Index: index,Item: x));
            index++;
        }
    }

    // [Intersect_second]
    [BenchFact,Benchmark]
    public void Intersect_second() => this.Intersect_second_Impl();

    private void Intersect_second_Impl() {
        var first = this.srcCount(SetCount);
        var second = this.src2Count(SetCount);
        var set = new HashSet<T>();
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
        var first = this.srcCount(SetCount);
        var second = this.src2Count(SetCount);
        var set = new HashSet<T>();
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
        var source = this.srcCount(StreamOneToOneCount);
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
        var outer = this.srcCount(JoinCount);
        var inner = this.src2Count(JoinCount);
        var counts = new Dictionary<T,int>();
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
        var outer = this.srcCount(JoinCount);
        var invariant = global::Common.HoistInvariant.Value();
        using var inner = Buffer(this.src2Count(JoinCount));
        var span = inner.Span;
        using var groups = new PooledGroups<int,T>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=(Number(x)+invariant)%1000;
            groups.Elements[index]=x;
        }
        var sortedArray = new T[span.Length];
        groups.Group(span.Length,sortedArray);
        var consumer = this.consumer;
        foreach(var o in outer) {
            if(groups.Map.TryGetValue(Mod(o,1000),out var group)) {
                var end = groups.Starts[group]+groups.Counts[group];
                for(var index = groups.Starts[group];index<end;index++) {
                    consumer.Consume(Number(o)+Number(sortedArray[index]));
                }
            }
        }
    }

    // [Join_outerKey_Hoisting]
    [BenchFact,Benchmark]
    public void Join_outerKey_Hoisting() => this.Join_outerKey_Hoisting_Impl();

    private void Join_outerKey_Hoisting_Impl() {
        var outer = this.srcCount(JoinCount);
        var invariant = global::Common.HoistInvariant.Value();
        using var inner = Buffer(this.src2Count(JoinCount));
        var span = inner.Span;
        using var groups = new PooledGroups<int,T>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sortedArray = new T[span.Length];
        groups.Group(span.Length,sortedArray);
        var consumer = this.consumer;
        foreach(var o in outer) {
            if(groups.Map.TryGetValue((Number(o)+invariant)%1000,out var group)) {
                var end = groups.Starts[group]+groups.Counts[group];
                for(var index = groups.Starts[group];index<end;index++) {
                    consumer.Consume(Number(o)+Number(sortedArray[index]));
                }
            }
        }
    }

    // [Join_resultSelector_Hoisting]
    [BenchFact,Benchmark]
    public void Join_resultSelector_Hoisting() => this.Join_resultSelector_Hoisting_Impl();

    private void Join_resultSelector_Hoisting_Impl() {
        var outer = this.srcCount(JoinCount);
        var invariant = global::Common.HoistInvariant.Value();
        using var inner = Buffer(this.src2Count(JoinCount));
        var span = inner.Span;
        using var groups = new PooledGroups<int,T>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sortedArray = new T[span.Length];
        groups.Group(span.Length,sortedArray);
        var consumer = this.consumer;
        foreach(var o in outer) {
            if(groups.Map.TryGetValue(Mod(o,1000),out var group)) {
                var end = groups.Starts[group]+groups.Counts[group];
                for(var index = groups.Starts[group];index<end;index++) {
                    consumer.Consume(Number(o)+Number(sortedArray[index])+invariant);
                }
            }
        }
    }

    // [Last]
    [BenchFact,Benchmark]
    public void Last() => ExecuteOrderDependent(() => this.Last_Impl());

    private void Last_Impl() {
        var found = false;
        T result = default!;
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            found=true;
            result=x;
        }
        if(!found) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(result);
    }

    // [LastOrDefault]
    [BenchFact,Benchmark]
    public void LastOrDefault() => ExecuteOrderDependent(() => this.LastOrDefault_Impl());

    private void LastOrDefault_Impl() {
        T result = default!;
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            result=x;
        }
        this.consumer.Consume(result);
    }

    // [LastOrDefault_predicate]
    [BenchFact,Benchmark]
    public void LastOrDefault_predicate() => ExecuteOrderDependent(() => this.LastOrDefault_predicate_Impl());

    private void LastOrDefault_predicate_Impl() {
        var value = this.MidpointValue(ImmediatePredicateScanCount);
        T result = default!;
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
            if(GreaterThanOrEqual(x,value)) {
                result=x;
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
        var count = 0L;
        foreach(var _ in this.srcCount(ImmediateFullScanCount)) {
            count=checked(count+1);
        }
        this.consumer.Consume(count);
    }

    // [LongCount_predicate]
    [BenchFact,Benchmark]
    public void LongCount_predicate() => this.LongCount_predicate_Impl();

    private void LongCount_predicate_Impl() {
        var count = 0L;
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
            if(IsEven(x)) {
                count=checked(count+1);
            }
        }
        this.consumer.Consume(count);
    }

    // [Max]
    [BenchFact,Benchmark]
    public void Max() => this.Max_Impl();

    private void Max_Impl() {
        using var source = Buffer(this.srcCount(ImmediateFullScanCount));
        this.consumer.Consume(SpanMax(source.Span));
    }

    // [Max_selector]
    [BenchFact,Benchmark]
    public void Max_selector() => this.Max_selector_Impl();

    private void Max_selector_Impl() {
        using var enumerator = this.srcCount(ImmediateFullScanCount).GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw IdealErrors.NoElements();
        }
        var result = Number(enumerator.Current)*2;
        while(enumerator.MoveNext()) {
            var value = Number(enumerator.Current)*2;
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
        using var enumerator = this.srcCount(ImmediateFullScanCount).GetEnumerator();
        if(!enumerator.MoveNext()) {
            if(default(T) is null) {
                this.consumer.Consume(default(T));
                return;
            }
            throw IdealErrors.NoElements();
        }
        var result = enumerator.Current;
        var key = Mod(result,1000);
        while(enumerator.MoveNext()) {
            var next = enumerator.Current;
            var nextKey = Mod(next,1000);
            if(nextKey>key) {
                key=nextKey;
                result=next;
            }
        }
        this.consumer.Consume(result);
    }

    // [Min]
    [BenchFact,Benchmark]
    public void Min() => this.Min_Impl();

    private void Min_Impl() {
        using var source = Buffer(this.srcCount(ImmediateFullScanCount));
        this.consumer.Consume(SpanMin(source.Span));
    }

    // [Min_selector]
    [BenchFact,Benchmark]
    public void Min_selector() => this.Min_selector_Impl();

    private void Min_selector_Impl() {
        using var enumerator = this.srcCount(ImmediateFullScanCount).GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw IdealErrors.NoElements();
        }
        var result = Number(enumerator.Current)*2;
        while(enumerator.MoveNext()) {
            var value = Number(enumerator.Current)*2;
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
        using var enumerator = this.srcCount(ImmediateFullScanCount).GetEnumerator();
        if(!enumerator.MoveNext()) {
            if(default(T) is null) {
                this.consumer.Consume(default(T));
                return;
            }
            throw IdealErrors.NoElements();
        }
        var result = enumerator.Current;
        var key = Mod(result,1000);
        while(enumerator.MoveNext()) {
            var next = enumerator.Current;
            var nextKey = Mod(next,1000);
            if(nextKey<key) {
                key=nextKey;
                result=next;
            }
        }
        this.consumer.Consume(result);
    }

    // [OfType]
    [BenchFact,Benchmark]
    public void OfType() => this.OfType_Impl();

    private void OfType_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcCount(StreamOneToOneCount)) {
            if(x is not null) {
                consumer.Consume(x);
            }
        }
    }

    // [Order]
    [BenchFact,Benchmark]
    public void Order() => ExecuteOrderDependent(() => this.Order_Impl());

    private void Order_Impl() {
        using var source = Buffer(this.srcCount(SortCount));
        ConsumeSorted(source.Span,false,this.consumer);
    }

    // [OrderBy_keySelector]
    [BenchFact,Benchmark]
    public void OrderBy_keySelector() => ExecuteOrderDependent(() => this.OrderBy_keySelector_Impl());

    private void OrderBy_keySelector_Impl() {
        using var source = Buffer(this.srcCount(SortCount));
        ConsumeSorted(source.Span,false,this.consumer);
    }

    // [OrderByDescending_keySelector]
    [BenchFact,Benchmark]
    public void OrderByDescending_keySelector() => ExecuteOrderDependent(() => this.OrderByDescending_keySelector_Impl());

    private void OrderByDescending_keySelector_Impl() {
        using var source = Buffer(this.srcCount(SortCount));
        ConsumeSorted(source.Span,true,this.consumer);
    }

    // [OrderDescending]
    [BenchFact,Benchmark]
    public void OrderDescending() => ExecuteOrderDependent(() => this.OrderDescending_Impl());

    private void OrderDescending_Impl() {
        using var source = Buffer(this.srcCount(SortCount));
        ConsumeSorted(source.Span,true,this.consumer);
    }

    // [Prepend_element]
    [BenchFact,Benchmark]
    public void Prepend_element() => ExecuteOrderDependent(() => this.Prepend_element_Impl());

    private void Prepend_element_Impl() {
        var source = this.srcCount(StreamShapeCount);
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
        using var source = Buffer(this.srcCount(StreamShapeCount));
        var span = source.Span;
        var consumer = this.consumer;
        for(var index = span.Length-1;index>=0;index--) {
            consumer.Consume(span[index]);
        }
    }

    // [Select_indexSelector]
    [BenchFact,Benchmark]
    public void Select_indexSelector() => ExecuteOrderDependent(() => this.Select_indexSelector_Impl());

    private void Select_indexSelector_Impl() {
        var consumer = this.consumer;
        var index = 0;
        foreach(var x in this.srcCount(StreamOneToOneCount)) {
            consumer.Consume(Number(x)+index);
            index++;
        }
    }

    // [Select_selector]
    [BenchFact,Benchmark]
    public void Select_selector() => this.Select_selector_Impl();

    private void Select_selector_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcCount(StreamOneToOneCount)) {
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

    private void SequenceEqual_second_Impl() =>
        this.consumer.Consume(EnumerableSequenceEqual(this.srcCount(StreamShapeCount),this.srcCount(StreamShapeCount)));

    [BenchFact,Benchmark]
    public void SequenceEqual_second_comparer() => ExecuteOrderDependent(() => this.SequenceEqual_second_comparer_Impl());

    private void SequenceEqual_second_comparer_Impl() =>
        this.consumer.Consume(EnumerableSequenceEqual(this.srcCount(StreamShapeCount),this.srcCount(StreamShapeCount)));

    private static bool EnumerableSequenceEqual(global::System.Collections.Generic.IEnumerable<T> first,global::System.Collections.Generic.IEnumerable<T> second) {
        using var left = first.GetEnumerator();
        using var right = second.GetEnumerator();
        while(left.MoveNext()) {
            if(!right.MoveNext()||!EqualityComparer<T>.Default.Equals(left.Current,right.Current)) {
                return false;
            }
        }
        return !right.MoveNext();
    }

    // [Single]
    [BenchFact,Benchmark]
    public void Single() => this.Single_Impl();

    private void Single_Impl() {
        var source = this.srcOne(1);
        for(var i = 0;i<IterationCount(Single_iterationCount);i++) {
            this.consumer.Consume(SingleOrThrow(source));
        }
    }

    // [Single_predicate]
    [BenchFact,Benchmark]
    public void Single_predicate() => this.Single_predicate_Impl();

    private void Single_predicate_Impl() {
        _=SinglePredicate(this.srcOne(1),0);
        this.consumer.Consume(SinglePredicate(this.srcCount(ImmediatePredicateScanCount),1));
    }

    // [SingleOrDefault]
    [BenchFact,Benchmark]
    public void SingleOrDefault() => this.SingleOrDefault_Impl();

    private void SingleOrDefault_Impl() {
        var source = this.srcOne(1);
        for(var i = 0;i<IterationCount(SingleOrDefault_iterationCount);i++) {
            using var enumerator = source.GetEnumerator();
            if(!enumerator.MoveNext()) {
                this.consumer.Consume(default(T));
                continue;
            }
            var result = enumerator.Current;
            if(enumerator.MoveNext()) {
                throw IdealErrors.MoreThanOneElement();
            }
            this.consumer.Consume(result);
        }
    }

    // [SingleOrDefault_predicate]
    [BenchFact,Benchmark]
    public void SingleOrDefault_predicate() => this.SingleOrDefault_predicate_Impl();

    private void SingleOrDefault_predicate_Impl() {
        T result = default!;
        var found = false;
        foreach(var x in this.srcCount(ImmediatePredicateScanCount)) {
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
        var source = this.srcCount(StreamPartitionCount);
        var count = this.QuarterCount(StreamPartitionCount);
        var consumer = this.consumer;
        var index = 0;
        foreach(var x in source) {
            if(index>=count) {
                consumer.Consume(x);
            }
            index++;
        }
    }

    // [SkipLast_count]
    [BenchFact,Benchmark]
    public void SkipLast_count() => ExecuteOrderDependent(() => this.SkipLast_count_Impl());

    private void SkipLast_count_Impl() {
        var source = this.srcCount(StreamPartitionCount);
        var count = this.QuarterCount(StreamPartitionCount);
        var consumer = this.consumer;
        if(count<=0) {
            foreach(var x in source) {
                consumer.Consume(x);
            }
            return;
        }
        // 直近 count 件を環状に保持し、溢れた先頭から流す。
        var ring = System.Buffers.ArrayPool<T>.Shared.Rent(count);
        try {
            var filled = 0;
            var head = 0;
            foreach(var x in source) {
                if(filled<count) {
                    ring[filled++]=x;
                    continue;
                }
                consumer.Consume(ring[head]);
                ring[head]=x;
                head++;
                if(head==count) {
                    head=0;
                }
            }
        } finally {
            System.Buffers.ArrayPool<T>.Shared.Return(ring);
        }
    }

    // [SkipWhile_indexPredicate]
    [BenchFact,Benchmark]
    public void SkipWhile_indexPredicate() => ExecuteOrderDependent(() => this.SkipWhile_indexPredicate_Impl());

    private void SkipWhile_indexPredicate_Impl() {
        var takeUntil = this.QuarterCount(StreamFilterCount);
        var consumer = this.consumer;
        var skipping = true;
        var index = 0;
        foreach(var x in this.srcCount(StreamFilterCount)) {
            if(skipping) {
                if(index<takeUntil) {
                    index++;
                    continue;
                }
                skipping=false;
            }
            consumer.Consume(x);
        }
    }

    // [SkipWhile_predicate]
    [BenchFact,Benchmark]
    public void SkipWhile_predicate() => ExecuteOrderDependent(() => this.SkipWhile_predicate_Impl());

    private void SkipWhile_predicate_Impl() {
        var source = this.srcCount(StreamFilterCount);
        var bound = this.QuarterCount(StreamFilterCount)+1;
        var consumer = this.consumer;
        var skipping = true;
        foreach(var x in source) {
            if(skipping) {
                if(LessThan(x,bound)) {
                    continue;
                }
                skipping=false;
            }
            consumer.Consume(x);
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
        foreach(var x in this.srcCount(ImmediateFullScanCount)) {
            sum=checked(sum+LongNumber(x));
        }
        this.consumer.Consume(sum);
    }

    // [Take_count]
    [BenchFact,Benchmark]
    public void Take_count() => ExecuteOrderDependent(() => this.Take_count_Impl());

    private void Take_count_Impl() {
        var source = this.srcCount(StreamPartitionCount);
        var count = this.ThreeQuarterCount(StreamPartitionCount);
        if(count<=0) {
            return;
        }
        var consumer = this.consumer;
        var taken = 0;
        foreach(var x in source) {
            consumer.Consume(x);
            taken++;
            if(taken==count) {
                break;
            }
        }
    }

    // [TakeLast_count]
    [BenchFact,Benchmark]
    public void TakeLast_count() => ExecuteOrderDependent(() => this.TakeLast_count_Impl());

    private void TakeLast_count_Impl() {
        var source = this.srcCount(StreamPartitionCount);
        var count = this.QuarterCount(StreamPartitionCount);
        if(count<=0) {
            return;
        }
        var ring = System.Buffers.ArrayPool<T>.Shared.Rent(count);
        try {
            var filled = 0;
            var head = 0;
            foreach(var x in source) {
                if(filled<count) {
                    ring[filled++]=x;
                    continue;
                }
                ring[head]=x;
                head++;
                if(head==count) {
                    head=0;
                }
            }
            var consumer = this.consumer;
            for(var offset = 0;offset<filled;offset++) {
                var index = head+offset;
                if(index>=filled) {
                    index-=filled;
                }
                consumer.Consume(ring[index]);
            }
        } finally {
            System.Buffers.ArrayPool<T>.Shared.Return(ring);
        }
    }

    // [TakeWhile_indexPredicate]
    [BenchFact,Benchmark]
    public void TakeWhile_indexPredicate() => ExecuteOrderDependent(() => this.TakeWhile_indexPredicate_Impl());

    private void TakeWhile_indexPredicate_Impl() {
        var takeUntil = this.ThreeQuarterCount(StreamFilterCount);
        var consumer = this.consumer;
        var index = 0;
        foreach(var x in this.srcCount(StreamFilterCount)) {
            if(index>=takeUntil) {
                break;
            }
            consumer.Consume(x);
            index++;
        }
    }

    // [TakeWhile_predicate]
    [BenchFact,Benchmark]
    public void TakeWhile_predicate() => ExecuteOrderDependent(() => this.TakeWhile_predicate_Impl());

    private void TakeWhile_predicate_Impl() {
        var source = this.srcCount(StreamFilterCount);
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
        foreach(var x in this.srcCount(MaterializeLinearCount)) {
            consumer.Consume(x);
        }
    }

    // [ToDictionary_elementSelector]
    [BenchFact,Benchmark]
    public void ToDictionary_elementSelector() => this.ToDictionary_elementSelector_Impl();

    private void ToDictionary_elementSelector_Impl() {
        var dictionary = new Dictionary<T,int>();
        foreach(var x in this.srcCount(MaterializeHashCount)) {
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
        var dictionary = new Dictionary<T,T>();
        foreach(var x in this.srcCount(MaterializeHashCount)) {
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
        var set = new HashSet<T>();
        var consumer = this.consumer;
        foreach(var x in this.srcCount(MaterializeHashCount)) {
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
        foreach(var x in this.srcCount(MaterializeLinearCount)) {
            consumer.Consume(x);
        }
    }

    // [ToLookup_elementSelector]
    [BenchFact,Benchmark]
    public void ToLookup_elementSelector() => this.ToLookup_elementSelector_Impl();

    private void ToLookup_elementSelector_Impl() {
        using var source = Buffer(this.srcCount(MaterializeLookupCount));
        var span = source.Span;
        using var groups = new PooledGroups<int,int>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=Number(x)*2;
        }
        var sorted = new int[span.Length];
        groups.Group(span.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,int>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [ToLookup_keySelector]
    [BenchFact,Benchmark]
    public void ToLookup_keySelector() => this.ToLookup_keySelector_Impl();

    private void ToLookup_keySelector_Impl() {
        using var source = Buffer(this.srcCount(MaterializeLookupCount));
        var span = source.Span;
        using var groups = new PooledGroups<int,T>(span.Length);
        for(var index = 0;index<span.Length;index++) {
            var x = span[index];
            groups.Keys[index]=Mod(x,1000);
            groups.Elements[index]=x;
        }
        var sorted = new T[span.Length];
        groups.Group(span.Length,sorted);
        var consumer = this.consumer;
        for(var group = 0;group<groups.GroupCount;group++) {
            consumer.Consume(new IdealGrouping<int,T>(groups.Distinct[group],sorted,groups.Starts[group],groups.Counts[group]));
        }
    }

    // [TryGetNonEnumeratedCount]
    [BenchFact,Benchmark]
    public void TryGetNonEnumeratedCount() => this.TryGetNonEnumeratedCount_Impl();

    private void TryGetNonEnumeratedCount_Impl() {
        var source = this.srcCount(ImmediateFullScanCount);
        for(var i = 0;i<IterationCount(TryGetNonEnumeratedCount_iterationCount);i++) {
            if(source is ICollection<T> collection) {
                ConsumeResult(this.consumer,true,collection.Count);
            } else if(source is global::System.Collections.ICollection nonGeneric) {
                ConsumeResult(this.consumer,true,nonGeneric.Count);
            } else {
                ConsumeResult(this.consumer,false,0);
            }
        }
    }

    // [Union_second]
    [BenchFact,Benchmark]
    public void Union_second() => this.Union_second_Impl();

    private void Union_second_Impl() {
        var first = this.srcCount(SetCount);
        var second = this.src2Count(SetCount);
        var set = new HashSet<T>();
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
        var first = this.srcCount(SetCount);
        var second = this.src2Count(SetCount);
        var set = new HashSet<T>();
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
        var consumer = this.consumer;
        var index = 0;
        foreach(var x in this.srcCount(StreamFilterCount)) {
            if(index%2==0) {
                consumer.Consume(x);
            }
            index++;
        }
    }

    // [Where_predicate]
    [BenchFact,Benchmark]
    public void Where_predicate() => this.Where_predicate_Impl();

    private void Where_predicate_Impl() {
        var consumer = this.consumer;
        foreach(var x in this.srcCount(StreamFilterCount)) {
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
        foreach(var x in this.srcCount(SourceShapeCount)) {
            if(IsEven(x)) {
                consumer.Consume(x);
            }
        }
    }

    // [Zip_second]
    [BenchFact,Benchmark]
    public void Zip_second() => ExecuteOrderDependent(() => this.Zip_second_Impl());

    private void Zip_second_Impl() {
        using var first = this.srcCount(StreamShapeCount).GetEnumerator();
        using var second = this.src2Count(StreamShapeCount).GetEnumerator();
        var consumer = this.consumer;
        while(first.MoveNext()&&second.MoveNext()) {
            consumer.Consume(Add(first.Current,second.Current));
        }
    }

    // [Zip_tuple]
    [BenchFact,Benchmark]
    public void Zip_tuple() => ExecuteOrderDependent(() => this.Zip_tuple_Impl());

    private void Zip_tuple_Impl() {
        using var first = this.srcCount(StreamShapeCount).GetEnumerator();
        using var second = this.src2Count(StreamShapeCount).GetEnumerator();
        var consumer = this.consumer;
        while(first.MoveNext()&&second.MoveNext()) {
            consumer.Consume((First: first.Current,Second: second.Current));
        }
    }
}
#endif
