namespace Benchmark.Optimization.Rewrite {
    //public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayRewrite<int>;
    //public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayRewrite<ClassInt32>;
    public class ArrayInt32:global::Seamless.Linq.Optimization.Rewrite.Array<int>;
    public class ArrayClassInt32:global::Seamless.Linq.Optimization.Rewrite.Array<ClassInt32>;
    public class IEnumerableInt32:global::Seamless.Linq.Optimization.Rewrite.IEnumerable<int>;
    public class IEnumerableClassInt32:global::Seamless.Linq.Optimization.Rewrite.IEnumerable<ClassInt32>;
    public class ListInt32:global::Seamless.Linq.Optimization.Rewrite.List<int>;
    public class ListClassInt32:global::Seamless.Linq.Optimization.Rewrite.List<ClassInt32>;
    public class Range_start_count:global::Seamless.Linq.Optimization.Rewrite.Range;
    public class PersistedInt32:global::Seamless.Linq.Optimization.Rewrite.Persisted<int>;
    public class PersistedClassInt32:global::Seamless.Linq.Optimization.Rewrite.Persisted<ClassInt32>;
    public class TsavoriteInt32:global::Seamless.Linq.Optimization.Rewrite.Tsavorite<int>;
    public class TsavoriteClassInt32:global::Seamless.Linq.Optimization.Rewrite.Tsavorite<ClassInt32>;
}
