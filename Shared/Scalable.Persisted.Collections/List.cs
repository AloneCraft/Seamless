#if BENCHMARK_PROJECT
namespace Benchmark.Scalable.Persisted.Collections.List;
#else
namespace Scalable.Persisted.Collections.List;
#endif

public class ListInt32
    :PersistedListScalable<BlittableListStrategy<Int32ListValueProvider,int>,int>;

public class ListString
    :PersistedListScalable<MemoryPackListStrategy<StringListValueProvider,string>,string>;
