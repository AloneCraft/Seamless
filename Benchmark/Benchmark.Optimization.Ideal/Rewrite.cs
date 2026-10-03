namespace Benchmark.Optimization.Rewrite {
    public class ArrayInt32:global::Shared.Linq.Optimization.Rewrite.Array<int>;
    public class ArrayClassInt32:global::Shared.Linq.Optimization.Rewrite.Array<ClassInt32>;
    public class IEnumerableInt32:global::Shared.Linq.Optimization.Rewrite.IEnumerable<int>;
    public class IEnumerableClassInt32:global::Shared.Linq.Optimization.Rewrite.IEnumerable<ClassInt32>;
    public class ListInt32:global::Shared.Linq.Optimization.Rewrite.List<int>;
    public class ListClassInt32:global::Shared.Linq.Optimization.Rewrite.List<ClassInt32>;
    public class Range_start_count:global::Shared.Linq.Optimization.Rewrite.Range;
}

// Where(p).Count() の理想版。述語を満たす件数を1回の走査で数える(配列・List は長さが int に収まるので件数は桁あふれしない)。Range は境界から求める。
namespace Shared.Linq.Optimization.Rewrite {
    public abstract class Array<T>(int count = 4_096):global::Common.Base<T>(count) where T : notnull {
        [BenchFact,Benchmark]
        public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
        private void Where_predicate_Count_Impl() {
            var result = 0;
            foreach(var value in this.srcCount(4_096)) {
                if(ToInt(value)>2_048) {
                    result++;
                }
            }
            this.consumer.Consume(result);
        }
    }

    public abstract class IEnumerable<T>(int count = 4_096):EnumerableSource<T>(count) where T : notnull {
        [BenchFact,Benchmark]
        public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
        private void Where_predicate_Count_Impl() {
            var result = 0;
            foreach(var value in this.Source()) {
                if(ToInt(value)>2_048) {
                    result=checked(result+1);
                }
            }
            this.consumer.Consume(result);
        }
    }

    public abstract class List<T>(int count = 4_096):ListSource<T>(count) where T : notnull {
        [BenchFact,Benchmark]
        public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
        private void Where_predicate_Count_Impl() {
            var result = 0;
            foreach(var value in global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(this.Source())) {
                if(ToInt(value)>2_048) {
                    result++;
                }
            }
            this.consumer.Consume(result);
        }
    }

    public abstract class Range(int count = 4_096):global::Common.Base<int>(count) {
        [BenchFact,Benchmark]
        public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
        private void Where_predicate_Count_Impl() {
            var length = this.EffectiveSourceCount(4_096);
            if(length<0) {
                throw new ArgumentOutOfRangeException("count");
            }
            // 1..n のうち 2048 を超える値は n-2048 個(負なら 0)。
            this.consumer.Consume(Math.Max(0,length-2_048));
        }
    }
}
