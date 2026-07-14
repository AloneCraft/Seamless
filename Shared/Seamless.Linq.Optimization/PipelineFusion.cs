namespace Benchmark.Optimization.PipelineFusion {
    public sealed class ArrayInt32:global::Seamless.Linq.Optimization.ArrayPipeline<int>;
    public sealed class ArrayClassInt32:global::Seamless.Linq.Optimization.ArrayPipeline<ClassInt32>;
    public sealed class IEnumerableInt32:global::Seamless.Linq.Optimization.EnumerablePipeline<int>;
    public sealed class IEnumerableClassInt32:global::Seamless.Linq.Optimization.EnumerablePipeline<ClassInt32>;
    public sealed class ListInt32:global::Seamless.Linq.Optimization.ListPipeline<int>;
    public sealed class ListClassInt32:global::Seamless.Linq.Optimization.ListPipeline<ClassInt32>;
    public sealed class Range_start_count:global::Seamless.Linq.Optimization.RangePipeline;
    public sealed class PersistedInt32:global::Seamless.Linq.Optimization.PersistedPipeline<int>;
    public sealed class PersistedClassInt32:global::Seamless.Linq.Optimization.PersistedPipeline<ClassInt32>;
    public sealed class TsavoriteInt32:global::Seamless.Linq.Optimization.TsavoritePipeline<int>;
    public sealed class TsavoriteClassInt32:global::Seamless.Linq.Optimization.TsavoritePipeline<ClassInt32>;
}
