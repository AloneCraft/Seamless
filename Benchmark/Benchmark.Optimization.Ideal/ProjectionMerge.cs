namespace Benchmark.Optimization.ProjectionMerge {
    public class ArrayInt32:global::Shared.Linq.Optimization.ProjectionMerge.Array<int>.Select_selector;
    public class ArrayClassInt32:global::Shared.Linq.Optimization.ProjectionMerge.Array<ClassInt32>.Select_selector;
    public class ArrayInt32Join:global::Shared.Linq.Optimization.ProjectionMerge.Array<int>.Join_inner;
    public class ArrayClassInt32Join:global::Shared.Linq.Optimization.ProjectionMerge.Array<ClassInt32>.Join_inner;
    public class ArrayInt32SelectMany:global::Shared.Linq.Optimization.ProjectionMerge.Array<int>.SelectMany_collection;
    public class ArrayClassInt32SelectMany:global::Shared.Linq.Optimization.ProjectionMerge.Array<ClassInt32>.SelectMany_collection;
    public class IEnumerableInt32:global::Shared.Linq.Optimization.ProjectionMerge.IEnumerable<int>.Select_selector;
    public class IEnumerableClassInt32:global::Shared.Linq.Optimization.ProjectionMerge.IEnumerable<ClassInt32>.Select_selector;
    public class IEnumerableInt32Join:global::Shared.Linq.Optimization.ProjectionMerge.IEnumerable<int>.Join_inner;
    public class IEnumerableClassInt32Join:global::Shared.Linq.Optimization.ProjectionMerge.IEnumerable<ClassInt32>.Join_inner;
    public class IEnumerableInt32SelectMany:global::Shared.Linq.Optimization.ProjectionMerge.IEnumerable<int>.SelectMany_collection;
    public class IEnumerableClassInt32SelectMany:global::Shared.Linq.Optimization.ProjectionMerge.IEnumerable<ClassInt32>.SelectMany_collection;
    public class ListInt32:global::Shared.Linq.Optimization.ProjectionMerge.List<int>;
    public class ListClassInt32:global::Shared.Linq.Optimization.ProjectionMerge.List<ClassInt32>;
    public class Range_start_count:global::Shared.Linq.Optimization.ProjectionMerge.Range;
}

// 射影の融合の理想版。中間の組(carrier)を作らず、最後の射影の値だけを計算する。
namespace Shared.Linq.Optimization.ProjectionMerge {
    using Consumer = global::Common.Consumer;

    internal static class Projection<T> where T : notnull {
        private static int Number(T value) => global::Common.Base<T>.ToInt(value);

        public static void SelectMember(Consumer consumer,ReadOnlySpan<T> source) {
            foreach(var value in source) {
                consumer.Consume(Number(value));
            }
        }

        public static void SelectMember(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            foreach(var value in source) {
                consumer.Consume(Number(value));
            }
        }

        public static void SelectComputed(Consumer consumer,ReadOnlySpan<T> source) {
            foreach(var value in source) {
                consumer.Consume(Number(value)*2+Number(value));
            }
        }

        public static void SelectComputed(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            foreach(var value in source) {
                consumer.Consume(Number(value)*2+Number(value));
            }
        }

        private static IdentityLookup<T> BuildInner(ReadOnlySpan<T> inner) {
            var lookup = new IdentityLookup<T>(inner.Length);
            foreach(var value in inner) {
                lookup.Add(value);
            }
            lookup.Build();
            return lookup;
        }

        public static void JoinOuterMember(Consumer consumer,ReadOnlySpan<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner);
            foreach(var value in outer) {
                if(lookup.TryGet(value,out _,out var length)) {
                    for(var match = 0;match<length;match++) {
                        consumer.Consume(Number(value));
                    }
                }
            }
        }

        public static void JoinOuterMember(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner);
            foreach(var value in outer) {
                if(lookup.TryGet(value,out _,out var length)) {
                    for(var match = 0;match<length;match++) {
                        consumer.Consume(Number(value));
                    }
                }
            }
        }

        public static void JoinBothMembers(Consumer consumer,ReadOnlySpan<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner);
            foreach(var value in outer) {
                if(lookup.TryGet(value,out var start,out var length)) {
                    for(var index = start;index<start+length;index++) {
                        consumer.Consume(Number(value)+Number(lookup[index]));
                    }
                }
            }
        }

        public static void JoinBothMembers(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> outer,ReadOnlySpan<T> inner) {
            using var lookup = BuildInner(inner);
            foreach(var value in outer) {
                if(lookup.TryGet(value,out var start,out var length)) {
                    for(var index = start;index<start+length;index++) {
                        consumer.Consume(Number(value)+Number(lookup[index]));
                    }
                }
            }
        }

        /// <summary>collection selector の Repeat(outer,1) は外側の要素1つだけを返すので、内側の値は外側の値そのもの。</summary>
        public static void SelectManyMember(Consumer consumer,ReadOnlySpan<T> source) {
            foreach(var value in source) {
                consumer.Consume(Number(value));
            }
        }

        public static void SelectManyMember(Consumer consumer,global::System.Collections.Generic.IEnumerable<T> source) {
            foreach(var value in source) {
                consumer.Consume(Number(value));
            }
        }
    }

    public abstract class Array<T> where T : notnull {
        public class Select_selector(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Select_member() => this.Select_member_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_member_Impl() => Projection<T>.SelectMember(this.consumer,this.srcCount(4_096));
            [BenchFact,Benchmark]
            public void Select_computed() => this.Select_computed_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_computed_Impl() => Projection<T>.SelectComputed(this.consumer,this.srcCount(4_096));
        }

        public class Join_inner(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Select_outerMember() => this.Select_outerMember_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_outerMember_Impl() => Projection<T>.JoinOuterMember(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Select_bothMembers() => this.Select_bothMembers_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_bothMembers_Impl() => Projection<T>.JoinBothMembers(this.consumer,this.srcCount(4_096),this.src2Count(4_096));
        }

        public class SelectMany_collection(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Select_member() => this.Select_member_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_member_Impl() => Projection<T>.SelectManyMember(this.consumer,this.srcCount(4_096));
        }
    }

    public abstract class IEnumerable<T> where T : notnull {
        public class Select_selector(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Select_member() => this.Select_member_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_member_Impl() => Projection<T>.SelectMember(this.consumer,this.Source());
            [BenchFact,Benchmark]
            public void Select_computed() => this.Select_computed_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_computed_Impl() => Projection<T>.SelectComputed(this.consumer,this.Source());
        }

        public class Join_inner(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Select_outerMember() => this.Select_outerMember_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_outerMember_Impl() => Projection<T>.JoinOuterMember(this.consumer,this.Source(),this.src2Count(4_096));
            [BenchFact,Benchmark]
            public void Select_bothMembers() => this.Select_bothMembers_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_bothMembers_Impl() => Projection<T>.JoinBothMembers(this.consumer,this.Source(),this.src2Count(4_096));
        }

        public class SelectMany_collection(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Select_member() => this.Select_member_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_member_Impl() => Projection<T>.SelectManyMember(this.consumer,this.Source());
        }
    }

    public abstract class List<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
        private ReadOnlySpan<T> Span() => global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(this.Source());

        [BenchFact,Benchmark]
        public void Select_selector_Select_member() => this.Select_selector_Select_member_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Select_selector_Select_member_Impl() => Projection<T>.SelectMember(this.consumer,this.Span());
        [BenchFact,Benchmark]
        public void Select_selector_Select_computed() => this.Select_selector_Select_computed_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Select_selector_Select_computed_Impl() => Projection<T>.SelectComputed(this.consumer,this.Span());
        [BenchFact,Benchmark]
        public void Join_inner_Select_outerMember() => this.Join_inner_Select_outerMember_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Select_outerMember_Impl() => Projection<T>.JoinOuterMember(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void Join_inner_Select_bothMembers() => this.Join_inner_Select_bothMembers_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Select_bothMembers_Impl() => Projection<T>.JoinBothMembers(this.consumer,this.Span(),this.src2Count(4_096));
        [BenchFact,Benchmark]
        public void SelectMany_collection_Select_member() => this.SelectMany_collection_Select_member_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SelectMany_collection_Select_member_Impl() => Projection<T>.SelectManyMember(this.consumer,this.Span());
    }

    /// <summary>Range 起点。結合の第2ソースは補助メソッドの戻り値で中身が分からないので、1回列挙して探索表にまとめる。</summary>
    public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
        private int Length() {
            var length = this.EffectiveSourceCount(4_096);
            if(length<0) {
                throw new ArgumentOutOfRangeException("count");
            }
            return length;
        }

        [BenchFact,Benchmark]
        public void Select_selector_Select_member() => this.Select_selector_Select_member_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Select_selector_Select_member_Impl() {
            var length = this.Length();
            for(var value = 1;value<=length;value++) {
                this.consumer.Consume(value);
            }
        }

        [BenchFact,Benchmark]
        public void Select_selector_Select_computed() => this.Select_selector_Select_computed_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Select_selector_Select_computed_Impl() {
            var length = this.Length();
            var invariant = global::Common.HoistInvariant.Value();
            for(var value = 1;value<=length;value++) {
                this.consumer.Consume(invariant*value+value);
            }
        }

        [BenchFact,Benchmark]
        public void Join_inner_Select_outerMember() => this.Join_inner_Select_outerMember_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Select_outerMember_Impl() => this.JoinOpaqueInner(bothMembers: false);

        [BenchFact,Benchmark]
        public void Join_inner_Select_bothMembers() => this.Join_inner_Select_bothMembers_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Join_inner_Select_bothMembers_Impl() => this.JoinOpaqueInner(bothMembers: true);

        /// <summary>恒等キーの結合の結果から外側(bothMembers なら外側+内側)だけを流す。組は作らない。</summary>
        private void JoinOpaqueInner(bool bothMembers) {
            var length = this.Length();
            var buffer = OpaqueSecondRange.Collect(OpaqueSecondRange.Create(this.EffectiveSourceCount(4_096)),out var count);
            try {
                using var lookup = new IdentityLookup<int>(count);
                for(var index = 0;index<count;index++) {
                    lookup.Add(buffer[index]);
                }
                lookup.Build();
                for(var value = 1;value<=length;value++) {
                    if(lookup.TryGet(value,out var start,out var matches)) {
                        for(var index = start;index<start+matches;index++) {
                            this.consumer.Consume(bothMembers ? value+lookup[index] : value);
                        }
                    }
                }
            } finally {
                global::System.Buffers.ArrayPool<int>.Shared.Return(buffer);
            }
        }

        [BenchFact,Benchmark]
        public void SelectMany_collection_Select_member() => this.SelectMany_collection_Select_member_Impl();
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void SelectMany_collection_Select_member_Impl() {
            var length = this.Length();
            for(var value = 1;value<=length;value++) {
                this.consumer.Consume(value);
            }
        }
    }
}
