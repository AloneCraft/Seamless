#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif

public abstract class BlittableHashSet<TStrategy, T> : HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : unmanaged, IEquatable<T>
{
}

public sealed class BlittableHashSet_Int : BlittableHashSet<BlittableHashSetStrategy<Int32HashSetValueProvider, int>, int>
{
}

public sealed class BlittableHashSet_UnmanagedStruct : BlittableHashSet<BlittableHashSetStrategy<UnmanagedStructHashSetValueProvider, UnmanagedStruct>, UnmanagedStruct>
{
}
