namespace Benchmark.Optimization.Rewrite{
    public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayRewrite<int>;
    public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayRewrite<global::Common.ClassInt32>;
    public sealed class IEnumerableInt32:global::Seamless.Linq.Optimization.EnumerableRewrite<int>;
    public sealed class IEnumerableClassInt32:global::Seamless.Linq.Optimization.EnumerableRewrite<global::Common.ClassInt32>;
    public sealed class ListInt32:global::Seamless.Linq.Optimization.ListRewrite<int>;
    public sealed class ListClassInt32:global::Seamless.Linq.Optimization.ListRewrite<global::Common.ClassInt32>;
    public sealed class Range_start_count:global::Seamless.Linq.Optimization.RangeRewrite;
    public sealed class PersistedInt32:global::Seamless.Linq.Optimization.PersistedRewrite<int>;
    public sealed class PersistedClassInt32:global::Seamless.Linq.Optimization.PersistedRewrite<global::Common.ClassInt32>;
    public sealed class TsavoriteInt32:global::Seamless.Linq.Optimization.TsavoriteRewrite<int>;
    public sealed class TsavoriteClassInt32:global::Seamless.Linq.Optimization.TsavoriteRewrite<global::Common.ClassInt32>;
}
namespace Benchmark.Optimization.PipelineFusion{
    public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayPipeline<int>;
    public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayPipeline<global::Common.ClassInt32>;
    public sealed class IEnumerableInt32:global::Seamless.Linq.Optimization.EnumerablePipeline<int>;
    public sealed class IEnumerableClassInt32:global::Seamless.Linq.Optimization.EnumerablePipeline<global::Common.ClassInt32>;
    public sealed class ListInt32:global::Seamless.Linq.Optimization.ListPipeline<int>;
    public sealed class ListClassInt32:global::Seamless.Linq.Optimization.ListPipeline<global::Common.ClassInt32>;
    public sealed class Range_start_count:global::Seamless.Linq.Optimization.RangePipeline;
    public sealed class PersistedInt32:global::Seamless.Linq.Optimization.PersistedPipeline<int>;
    public sealed class PersistedClassInt32:global::Seamless.Linq.Optimization.PersistedPipeline<global::Common.ClassInt32>;
    public sealed class TsavoriteInt32:global::Seamless.Linq.Optimization.TsavoritePipeline<int>;
    public sealed class TsavoriteClassInt32:global::Seamless.Linq.Optimization.TsavoritePipeline<global::Common.ClassInt32>;
}
namespace Benchmark.Optimization.PredicatePushdown{
    public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayPushdown<int>;
    public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayPushdown<global::Common.ClassInt32>;
    public sealed class IEnumerableInt32:global::Seamless.Linq.Optimization.EnumerablePushdown<int>;
    public sealed class IEnumerableClassInt32:global::Seamless.Linq.Optimization.EnumerablePushdown<global::Common.ClassInt32>;
    public sealed class ListInt32:global::Seamless.Linq.Optimization.ListPushdown<int>;
    public sealed class ListClassInt32:global::Seamless.Linq.Optimization.ListPushdown<global::Common.ClassInt32>;
    public sealed class Range_start_count:global::Seamless.Linq.Optimization.RangePushdown;
    public sealed class PersistedInt32:global::Seamless.Linq.Optimization.PersistedPushdown<int>;
    public sealed class PersistedClassInt32:global::Seamless.Linq.Optimization.PersistedPushdown<global::Common.ClassInt32>;
    public sealed class TsavoriteInt32:global::Seamless.Linq.Optimization.TsavoritePushdown<int>;
    public sealed class TsavoriteClassInt32:global::Seamless.Linq.Optimization.TsavoritePushdown<global::Common.ClassInt32>;
}
