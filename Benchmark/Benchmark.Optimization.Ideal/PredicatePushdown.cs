namespace Benchmark.Optimization.PredicatePushdown {
    public class ArrayInt32:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Select_selector;
    public class ArrayClassInt32:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Select_selector;
    public class ArrayInt32Join:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Join_inner;
    public class ArrayClassInt32Join:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Join_inner;
    public class ArrayInt32Union:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Union_second;
    public class ArrayClassInt32Union:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Union_second;
    public class ArrayInt32Concat:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Concat_second;
    public class ArrayClassInt32Concat:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Concat_second;
    public class ArrayInt32Except:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Except_first;
    public class ArrayClassInt32Except:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Except_first;
    public class ArrayInt32Intersect:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Intersect_first;
    public class ArrayClassInt32Intersect:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Intersect_first;
    public class ArrayInt32Distinct:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Distinct_source;
    public class ArrayClassInt32Distinct:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Distinct_source;
    public class ArrayInt32Reverse:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Reverse_source;
    public class ArrayClassInt32Reverse:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Reverse_source;
    public class ArrayInt32Order:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Order_source;
    public class ArrayClassInt32Order:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Order_source;
    public class ArrayInt32SkipOrder:global::Shared.Linq.Optimization.PredicatePushdown.Array<int>.Skip_count_Order_source;
    public class ArrayClassInt32SkipOrder:global::Shared.Linq.Optimization.PredicatePushdown.Array<ClassInt32>.Skip_count_Order_source;
    public class IEnumerableInt32:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Select_selector;
    public class IEnumerableClassInt32:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Select_selector;
    public class IEnumerableInt32Join:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Join_inner;
    public class IEnumerableClassInt32Join:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Join_inner;
    public class IEnumerableInt32Union:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Union_second;
    public class IEnumerableClassInt32Union:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Union_second;
    public class IEnumerableInt32Concat:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Concat_second;
    public class IEnumerableClassInt32Concat:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Concat_second;
    public class IEnumerableInt32Except:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Except_first;
    public class IEnumerableClassInt32Except:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Except_first;
    public class IEnumerableInt32Intersect:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Intersect_first;
    public class IEnumerableClassInt32Intersect:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Intersect_first;
    public class IEnumerableInt32Distinct:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Distinct_source;
    public class IEnumerableClassInt32Distinct:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Distinct_source;
    public class IEnumerableInt32Reverse:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Reverse_source;
    public class IEnumerableClassInt32Reverse:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Reverse_source;
    public class IEnumerableInt32Order:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Order_source;
    public class IEnumerableClassInt32Order:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Order_source;
    public class IEnumerableInt32SkipOrder:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<int>.Skip_count_Order_source;
    public class IEnumerableClassInt32SkipOrder:global::Shared.Linq.Optimization.PredicatePushdown.IEnumerable<ClassInt32>.Skip_count_Order_source;
    public class ListInt32:global::Shared.Linq.Optimization.PredicatePushdown.List<int>;
    public class ListClassInt32:global::Shared.Linq.Optimization.PredicatePushdown.List<ClassInt32>;
    public class Range_start_count:global::Shared.Linq.Optimization.PredicatePushdown.Range;
}

// 述語の押し下げの理想版。FLinq と同じ純粋性の仮定の下で、述語を集合演算・結合・並べ替えより前に評価する。
namespace Shared.Linq.Optimization.PredicatePushdown {
    using Consumer = global::Common.Consumer;

    /// <summary>配列・List(span)と IEnumerable の外側に共通の本体。内側(第2ソース)は常に配列。</summary>
    internal static class Pushdown<T> where T : notnull {
        private static int Number(T value) => global::Common.Base<T>.ToInt(value);

        private static bool IsEven(T value) => (Number(value)&1)==0;

        public static void SelectWhere(Consumer consumer,ReadOnlySpan<T> source) {
            foreach(var value in source) {
                if(IsEven(value)) {
                    consumer.Consume((Value: value,Number: value));
                }
            }
        }

        public static void SelectWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            foreach(var value in source) {
                if(IsEven(value)) {
                    consumer.Consume((Value: value,Number: value));
                }
            }
        }

        /// <summary>内側を(述語を通るものだけ)恒等キーでまとめる。kind: 0=述語なし、1=偶数かつ16超、2=4の倍数。</summary>
        internal static IdentityLookup<T> BuildInner(ReadOnlySpan<T> inner,int kind) {
            var lookup = new IdentityLookup<T>(inner.Length);
            foreach(var value in inner) {
                if(kind==1) {
                    if(!(IsEven(value)&&Number(value)>16)) {
                        continue;
                    }
                } else if(kind==2) {
                    if((Number(value)&3)!=0) {
                        continue;
                    }
                }
                lookup.Add(value);
            }
            lookup.Build();
            return lookup;
        }

        internal static void Probe(Consumer consumer,IdentityLookup<T> lookup,T outer) {
            if(lookup.TryGet(outer,out var start,out var length)) {
                for(var index = start;index<start+length;index++) {
                    consumer.Consume((Outer: outer,Inner: lookup[index]));
                }
            }
        }

        public static void JoinOuterOnly(Consumer consumer,ReadOnlySpan<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner,0);
            foreach(var value in outer) {
                if(IsEven(value)&&Number(value)>16) {
                    Probe(consumer,lookup,value);
                }
            }
        }

        public static void JoinOuterOnly(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner,0);
            foreach(var value in outer) {
                if(IsEven(value)&&Number(value)>16) {
                    Probe(consumer,lookup,value);
                }
            }
        }

        public static void JoinInnerOnly(Consumer consumer,ReadOnlySpan<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner,1);
            foreach(var value in outer) {
                Probe(consumer,lookup,value);
            }
        }

        public static void JoinInnerOnly(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner,1);
            foreach(var value in outer) {
                Probe(consumer,lookup,value);
            }
        }

        public static void JoinBothSides(Consumer consumer,ReadOnlySpan<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner,2);
            foreach(var value in outer) {
                if(IsEven(value)) {
                    Probe(consumer,lookup,value);
                }
            }
        }

        public static void JoinBothSides(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner,2);
            foreach(var value in outer) {
                if(IsEven(value)) {
                    Probe(consumer,lookup,value);
                }
            }
        }

        /// <summary>要素に依存しない述語はループの外で1回だけ評価する(偽なら結合そのものが不要)。</summary>
        public static void JoinConstant(Consumer consumer,ReadOnlySpan<T> outer,ReadOnlySpan<T> inner) {
            if(!(global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100)) {
                return;
            }
            using var lookup = BuildInner(inner,0);
            foreach(var value in outer) {
                Probe(consumer,lookup,value);
            }
        }

        public static void JoinConstant(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> outer,ReadOnlySpan<T> inner) {
            if(!(global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100)) {
                return;
            }
            using var lookup = BuildInner(inner,0);
            foreach(var value in outer) {
                Probe(consumer,lookup,value);
            }
        }

        public static void UnionWhere(Consumer consumer,ReadOnlySpan<T> first,ReadOnlySpan<T> second) {
            var set = new HashSet<T>(first.Length+second.Length);
            foreach(var value in first) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
            foreach(var value in second) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void UnionWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> first,ReadOnlySpan<T> second) {
            var set = new HashSet<T>(second.Length);
            foreach(var value in first) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
            foreach(var value in second) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void ConcatWhere(Consumer consumer,ReadOnlySpan<T> first,ReadOnlySpan<T> second) {
            foreach(var value in first) {
                if(IsEven(value)) {
                    consumer.Consume(value);
                }
            }
            foreach(var value in second) {
                if(IsEven(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void ConcatWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> first,ReadOnlySpan<T> second) {
            foreach(var value in first) {
                if(IsEven(value)) {
                    consumer.Consume(value);
                }
            }
            foreach(var value in second) {
                if(IsEven(value)) {
                    consumer.Consume(value);
                }
            }
        }

        private static HashSet<T> SetOf(ReadOnlySpan<T> values,int extra) {
            var set = new HashSet<T>(values.Length+extra);
            foreach(var value in values) {
                set.Add(value);
            }
            return set;
        }

        public static void ExceptWhere(Consumer consumer,ReadOnlySpan<T> first,ReadOnlySpan<T> second) {
            var set = SetOf(second,first.Length);
            foreach(var value in first) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void ExceptWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> first,ReadOnlySpan<T> second) {
            var set = SetOf(second,0);
            foreach(var value in first) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void IntersectWhere(Consumer consumer,ReadOnlySpan<T> first,ReadOnlySpan<T> second) {
            var set = SetOf(second,0);
            foreach(var value in first) {
                if(IsEven(value)&&set.Remove(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void IntersectWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> first,ReadOnlySpan<T> second) {
            var set = SetOf(second,0);
            foreach(var value in first) {
                if(IsEven(value)&&set.Remove(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void DistinctWhere(Consumer consumer,ReadOnlySpan<T> source) {
            var set = new HashSet<T>(source.Length);
            foreach(var value in source) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void DistinctWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            var set = new HashSet<T>();
            foreach(var value in source) {
                if(IsEven(value)&&set.Add(value)) {
                    consumer.Consume(value);
                }
            }
        }

        public static void ReverseWhere(Consumer consumer,ReadOnlySpan<T> source) {
            for(var index = source.Length-1;index>=0;index--) {
                if(IsEven(source[index])) {
                    consumer.Consume(source[index]);
                }
            }
        }

        /// <summary>IEnumerable は逆順に流すため全件が要る。述語を通るものだけを溜めて末尾から流す。</summary>
        public static void ReverseWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            using var buffer = new global::Shared.Linq.Ideal.PooledBuffer<T>(16);
            foreach(var value in source) {
                if(IsEven(value)) {
                    buffer.Add(value);
                }
            }
            var span = buffer.Span;
            for(var index = span.Length-1;index>=0;index--) {
                consumer.Consume(span[index]);
            }
        }

        /// <summary>述語を通るものだけを並べ替える(並べ替える件数が減る)。</summary>
        public static void OrderWhere(Consumer consumer,ReadOnlySpan<T> source) {
            using var buffer = new global::Shared.Linq.Ideal.PooledBuffer<T>(source.Length);
            foreach(var value in source) {
                if(IsEven(value)) {
                    buffer.Add(value);
                }
            }
            IdealOptimization.ConsumeSorted(consumer,buffer.Span);
        }

        public static void OrderWhere(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            using var buffer = new global::Shared.Linq.Ideal.PooledBuffer<T>(16);
            foreach(var value in source) {
                if(IsEven(value)) {
                    buffer.Add(value);
                }
            }
            IdealOptimization.ConsumeSorted(consumer,buffer.Span);
        }
    }

    public abstract class Array<T> where T : notnull {
        public class Select_selector(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.SelectWhere(this.consumer,this.srcCount(4_096));
        }

        public class Join_inner(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_outerOnly() => this.Where_outerOnly_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_outerOnly_Impl() => Pushdown<T>.JoinOuterOnly(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Where_innerOnly() => this.Where_innerOnly_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_innerOnly_Impl() => Pushdown<T>.JoinInnerOnly(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Where_bothSides() => this.Where_bothSides_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_bothSides_Impl() => Pushdown<T>.JoinBothSides(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Where_constant() => this.Where_constant_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_constant_Impl() => Pushdown<T>.JoinConstant(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
        }

        public class Union_second(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.UnionWhere(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
        }

        public class Concat_second(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.ConcatWhere(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
        }

        public class Except_first(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.ExceptWhere(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
        }

        public class Intersect_first(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.IntersectWhere(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
        }

        public class Distinct_source(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.DistinctWhere(this.consumer,this.srcCount(4_096));
        }

        public class Reverse_source(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.ReverseWhere(this.consumer,this.srcCount(4_096));
        }

        public class Order_source(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.OrderWhere(this.consumer,this.srcCount(4_096));
        }

        public class Skip_count_Order_source(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.OrderWhere(this.consumer,this.srcCount(4_096));
        }
    }

    public abstract class IEnumerable<T> where T : notnull {
        public class Select_selector(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.SelectWhere(this.consumer,this.Source());
        }

        public class Join_inner(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_outerOnly() => this.Where_outerOnly_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_outerOnly_Impl() => Pushdown<T>.JoinOuterOnly(this.consumer,this.Source(),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Where_innerOnly() => this.Where_innerOnly_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_innerOnly_Impl() => Pushdown<T>.JoinInnerOnly(this.consumer,this.Source(),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Where_bothSides() => this.Where_bothSides_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_bothSides_Impl() => Pushdown<T>.JoinBothSides(this.consumer,this.Source(),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Where_constant() => this.Where_constant_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_constant_Impl() => Pushdown<T>.JoinConstant(this.consumer,this.Source(),this.src2Count(4_096));
        }

        public class Union_second(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.UnionWhere(this.consumer,this.Source(),this.src2Count(4_096));
        }

        public class Concat_second(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.ConcatWhere(this.consumer,this.Source(),this.src2Count(4_096));
        }

        public class Except_first(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.ExceptWhere(this.consumer,this.Source(),this.src2Count(4_096));
        }

        public class Intersect_first(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.IntersectWhere(this.consumer,this.Source(),this.src2Count(4_096));
        }

        public class Distinct_source(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.DistinctWhere(this.consumer,this.Source());
        }

        public class Reverse_source(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.ReverseWhere(this.consumer,this.Source());
        }

        public class Order_source(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.OrderWhere(this.consumer,this.Source());
        }

        public class Skip_count_Order_source(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Where_predicate() => this.Where_predicate_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Where_predicate_Impl() => Pushdown<T>.OrderWhere(this.consumer,this.Source());
        }
    }

    public abstract class List<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
        private ReadOnlySpan<T> Span() => global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(this.Source());

        [BenchFact,Benchmark]
        public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Select_selector_Where_predicate_Impl() => Pushdown<T>.SelectWhere(this.consumer,this.Span());
        [BenchFact,Benchmark]
        public void Join_inner_Where_outerOnly() => this.Join_inner_Where_outerOnly_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_outerOnly_Impl() => Pushdown<T>.JoinOuterOnly(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Join_inner_Where_innerOnly() => this.Join_inner_Where_innerOnly_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_innerOnly_Impl() => Pushdown<T>.JoinInnerOnly(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Join_inner_Where_bothSides() => this.Join_inner_Where_bothSides_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_bothSides_Impl() => Pushdown<T>.JoinBothSides(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Join_inner_Where_constant() => this.Join_inner_Where_constant_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_constant_Impl() => Pushdown<T>.JoinConstant(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Union_second_Where_predicate() => this.Union_second_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Union_second_Where_predicate_Impl() => Pushdown<T>.UnionWhere(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Concat_second_Where_predicate() => this.Concat_second_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Concat_second_Where_predicate_Impl() => Pushdown<T>.ConcatWhere(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Except_first_Where_predicate() => this.Except_first_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Except_first_Where_predicate_Impl() => Pushdown<T>.ExceptWhere(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Intersect_first_Where_predicate() => this.Intersect_first_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Intersect_first_Where_predicate_Impl() => Pushdown<T>.IntersectWhere(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Distinct_Where_predicate() => this.Distinct_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Distinct_Where_predicate_Impl() => Pushdown<T>.DistinctWhere(this.consumer,this.Span());
        [BenchFact,Benchmark]
        public void Reverse_Where_predicate() => this.Reverse_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Reverse_Where_predicate_Impl() => Pushdown<T>.ReverseWhere(this.consumer,this.Span());
        [BenchFact,Benchmark]
        public void Order_Where_predicate() => this.Order_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Order_Where_predicate_Impl() => Pushdown<T>.OrderWhere(this.consumer,this.Span());
        [BenchFact,Benchmark]
        public void Skip_count_Order_Where_predicate() => this.Skip_count_Order_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Skip_count_Order_Where_predicate_Impl() => Pushdown<T>.OrderWhere(this.consumer,this.Span());
    }

    /// <summary>Range 起点。第2ソースは Range(length/2+1,length/2)。集合演算と恒等キーの結合は区間の計算にする。</summary>
    public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
        private int Length() {
            var length = this.EffectiveSourceCount(4_096);
            if(length<0) {
                throw new ArgumentOutOfRangeException("count");
            }
            return length;
        }

        /// <summary>[from,to) の偶数を昇順に流す。述語 (v&amp;1)==0 は純粋なので、最初の偶数から 2 刻みで回す。</summary>
        private static void ConsumeEven(Consumer consumer,long from,long to) {
            for(var value = from+(from&1);value<to;value+=2) {
                consumer.Consume((int)value);
            }
        }

        [BenchFact,Benchmark]
        public void Select_selector_Where_predicate() => this.Select_selector_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Select_selector_Where_predicate_Impl() {
            var length = this.Length();
            for(var value = 2;value<=length;value+=2) {
                this.consumer.Consume((Value: value,Number: value));
            }
        }

        /// <summary>
        /// 恒等キーの結合。第2ソース(補助メソッドの戻り値)は中身が分からないので1回列挙して、述語を通るものだけ探索表にまとめる。
        /// 外側は Range なので、述語は純粋とみなして kind 1(偶数かつ 16 超)は 18 から2刻み、kind 2(4 の倍数)は 4 から4刻みで探す
        /// (恒等キーの int の結合では外側と内側の値が等しいので、どちらの側の述語も値の条件になる)。
        /// </summary>
        private void JoinOpaqueInner(int kind) {
            var length = this.Length();
            var buffer = OpaqueSecondRange.Collect(OpaqueSecondRange.Create(this.EffectiveSourceCount(4_096)),out var count);
            try {
                using var lookup = Pushdown<int>.BuildInner(buffer.AsSpan(0,count),kind);
                var (from,step) = kind switch {
                    1 => (18L,2L),
                    2 => (4L,4L),
                    _ => (1L,1L),
                };
                for(var value = from;value<=length;value+=step) {
                    Pushdown<int>.Probe(this.consumer,lookup,(int)value);
                }
            } finally {
                global::System.Buffers.ArrayPool<int>.Shared.Return(buffer);
            }
        }

        [BenchFact,Benchmark]
        public void Join_inner_Where_outerOnly() => this.Join_inner_Where_outerOnly_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_outerOnly_Impl() => this.JoinOpaqueInner(1);
        [BenchFact,Benchmark]
        public void Join_inner_Where_innerOnly() => this.Join_inner_Where_innerOnly_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_innerOnly_Impl() => this.JoinOpaqueInner(1);
        [BenchFact,Benchmark]
        public void Join_inner_Where_bothSides() => this.Join_inner_Where_bothSides_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_bothSides_Impl() => this.JoinOpaqueInner(2);
        [BenchFact,Benchmark]
        public void Join_inner_Where_constant() => this.Join_inner_Where_constant_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Where_constant_Impl() {
            if(!(global::Common.HoistInvariant.Value()>0&&global::Common.HoistInvariant.Value()<100)) {
                this.Length();
                return;
            }
            this.JoinOpaqueInner(0);
        }

        // 和集合: 先頭の Range は一意なので偶数をそのまま流し、第2ソースは Range に無い偶数を初出だけ流す。
        [BenchFact,Benchmark]
        public void Union_second_Where_predicate() => this.Union_second_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Union_second_Where_predicate_Impl() {
            var length = this.Length();
            var second = OpaqueSecondRange.Create(this.EffectiveSourceCount(4_096));
            ConsumeEven(this.consumer,1,1L+length);
            var seen = new global::System.Collections.Generic.HashSet<int>();
            foreach(var value in second) {
                if((value&1)==0&&(value<1||value>length)&&seen.Add(value)) {
                    this.consumer.Consume(value);
                }
            }
        }

        [BenchFact,Benchmark]
        public void Concat_second_Where_predicate() => this.Concat_second_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Concat_second_Where_predicate_Impl() {
            var length = this.Length();
            var second = OpaqueSecondRange.Create(this.EffectiveSourceCount(4_096));
            ConsumeEven(this.consumer,1,1L+length);
            foreach(var value in second) {
                if((value&1)==0) {
                    this.consumer.Consume(value);
                }
            }
        }

        // 差集合・積集合: 第2ソースの偶数だけを集合にし、Range の偶数を順に引く(Range は一意なので重複除きは要らない)。
        [BenchFact,Benchmark]
        public void Except_first_Where_predicate() => this.Except_first_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Except_first_Where_predicate_Impl() {
            var length = this.Length();
            var excluded = EvenSet(OpaqueSecondRange.Create(this.EffectiveSourceCount(4_096)));
            for(var value = 2;value<=length;value+=2) {
                if(!excluded.Contains(value)) {
                    this.consumer.Consume(value);
                }
            }
        }

        [BenchFact,Benchmark]
        public void Intersect_first_Where_predicate() => this.Intersect_first_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Intersect_first_Where_predicate_Impl() {
            var length = this.Length();
            var included = EvenSet(OpaqueSecondRange.Create(this.EffectiveSourceCount(4_096)));
            for(var value = 2;value<=length;value+=2) {
                if(included.Contains(value)) {
                    this.consumer.Consume(value);
                }
            }
        }

        private static global::System.Collections.Generic.HashSet<int> EvenSet(global::System.Collections.Generic.IEnumerable<int> source) {
            var set = new global::System.Collections.Generic.HashSet<int>();
            foreach(var value in source) {
                if((value&1)==0) {
                    set.Add(value);
                }
            }
            return set;
        }

        [BenchFact,Benchmark]
        public void Distinct_Where_predicate() => this.Distinct_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Distinct_Where_predicate_Impl() => ConsumeEven(this.consumer,1,1L+this.Length());

        [BenchFact,Benchmark]
        public void Reverse_Where_predicate() => this.Reverse_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Reverse_Where_predicate_Impl() {
            var length = this.Length();
            for(var value = length-(length&1);value>=2;value-=2) {
                this.consumer.Consume(value);
            }
        }

        [BenchFact,Benchmark]
        public void Order_Where_predicate() => this.Order_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Order_Where_predicate_Impl() => ConsumeEven(this.consumer,1,1L+this.Length());

        [BenchFact,Benchmark]
        public void Skip_count_Order_Where_predicate() => this.Skip_count_Order_Where_predicate_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Skip_count_Order_Where_predicate_Impl() => ConsumeEven(this.consumer,1,1L+this.Length());
    }
}
