namespace Benchmark.Optimization.Rewrite {
    //public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayRewrite<int>;
    //public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayRewrite<ClassInt32>;
    public sealed class ArrayInt32:global::Seamless.Linq.Optimization.Rewrite.Array<int>;
    public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.Rewrite.Array<ClassInt32>;
    public sealed class IEnumerableInt32:global::Seamless.Linq.Optimization.Rewrite.IEnumerable<int>;
    public sealed class IEnumerableClassInt32:global::Seamless.Linq.Optimization.Rewrite.IEnumerable<ClassInt32>;
    public sealed class ListInt32:global::Seamless.Linq.Optimization.Rewrite.List<int>;
    public sealed class ListClassInt32:global::Seamless.Linq.Optimization.Rewrite.List<ClassInt32>;
    public sealed class Range_start_count:global::Seamless.Linq.Optimization.Rewrite.Range;
    public sealed class PersistedInt32:global::Seamless.Linq.Optimization.Rewrite.Persisted<int>;
    public sealed class PersistedClassInt32:global::Seamless.Linq.Optimization.Rewrite.Persisted<ClassInt32>;
    public sealed class TsavoriteInt32:global::Seamless.Linq.Optimization.Rewrite.Tsavorite<int>;
    public sealed class TsavoriteClassInt32:global::Seamless.Linq.Optimization.Rewrite.Tsavorite<ClassInt32>;
}
