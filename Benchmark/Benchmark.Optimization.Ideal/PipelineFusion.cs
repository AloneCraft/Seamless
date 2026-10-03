namespace Benchmark.Optimization.PipelineFusion {
    public class ArrayInt32:global::Shared.Linq.Optimization.PipelineFusion.Array<int>.Where_predicate;
    public class ArrayClassInt32:global::Shared.Linq.Optimization.PipelineFusion.Array<ClassInt32>.Where_predicate;
    public class IEnumerableInt32:global::Shared.Linq.Optimization.PipelineFusion.IEnumerable<int>.Where_predicate;
    public class IEnumerableClassInt32:global::Shared.Linq.Optimization.PipelineFusion.IEnumerable<ClassInt32>.Where_predicate;
    public class ListInt32:global::Shared.Linq.Optimization.PipelineFusion.List<int>.Where_predicate;
    public class ListClassInt32:global::Shared.Linq.Optimization.PipelineFusion.List<ClassInt32>.Where_predicate;
    public class Range_start_count:global::Shared.Linq.Optimization.PipelineFusion.Range.Where_predicate;
}

// Where(p).Select(s) の理想版。1本のループで述語と射影を行う。
namespace Shared.Linq.Optimization.PipelineFusion {
    public abstract class Array<T> where T : notnull {
        public class Where_predicate(int count = 4_096):global::Common.Base<T>(count) {
            [BenchFact,Benchmark]
            public void Select_selector() => this.Select_selector_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_selector_Impl() {
                var consumer = this.consumer;
                foreach(var value in this.srcCount(4_096)) {
                    if((ToInt(value)&1)==0) {
                        consumer.Consume(ToInt(value)*2);
                    }
                }
            }
        }
    }

    public abstract class List<T> where T : notnull {
        public class Where_predicate(int count = 4_096):ListSource<T>(count) {
            [BenchFact,Benchmark]
            public void Select_selector() => this.Select_selector_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_selector_Impl() {
                var consumer = this.consumer;
                foreach(var value in global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(this.Source())) {
                    if((ToInt(value)&1)==0) {
                        consumer.Consume(ToInt(value)*2);
                    }
                }
            }
        }
    }

    public abstract class IEnumerable<T> where T : notnull {
        public class Where_predicate(int count = 4_096):EnumerableSource<T>(count) {
            [BenchFact,Benchmark]
            public void Select_selector() => this.Select_selector_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_selector_Impl() {
                var consumer = this.consumer;
                foreach(var value in this.Source()) {
                    if((ToInt(value)&1)==0) {
                        consumer.Consume(ToInt(value)*2);
                    }
                }
            }
        }
    }

    public abstract class Range {
        public class Where_predicate(int count = 4_096):global::Common.Base<int>(count) {
            [BenchFact,Benchmark]
            public void Select_selector() => this.Select_selector_Impl();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private void Select_selector_Impl() {
                var length = this.EffectiveSourceCount(4_096);
                if(length<0) {
                    throw new ArgumentOutOfRangeException("count");
                }
                var consumer = this.consumer;
                // 偶数だけを 2 刻みで回す(述語は純粋なので評価しない)。
                for(var value = 2;value<=length;value+=2) {
                    consumer.Consume(value*2);
                }
            }
        }
    }
}
