namespace Benchmark.Optimization.PredicatePushdown {
    public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayPushdown<int>;
    public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayPushdown<ClassInt32>;
    public sealed class IEnumerableInt32:global::Seamless.Linq.Optimization.EnumerablePushdown<int>;
    public sealed class IEnumerableClassInt32:global::Seamless.Linq.Optimization.EnumerablePushdown<ClassInt32>;
    public sealed class ListInt32:global::Seamless.Linq.Optimization.ListPushdown<int>;
    public sealed class ListClassInt32:global::Seamless.Linq.Optimization.ListPushdown<ClassInt32>;
    public sealed class Range_start_count:global::Seamless.Linq.Optimization.RangePushdown;
    public sealed class PersistedInt32:global::Seamless.Linq.Optimization.PersistedPushdown<int>;
    public sealed class PersistedClassInt32:global::Seamless.Linq.Optimization.PersistedPushdown<ClassInt32>;
    public sealed class TsavoriteInt32:global::Seamless.Linq.Optimization.TsavoritePushdown<int>;
    public sealed class TsavoriteClassInt32:global::Seamless.Linq.Optimization.TsavoritePushdown<ClassInt32>;
}
