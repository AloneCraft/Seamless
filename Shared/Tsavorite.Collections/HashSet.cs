#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif

public abstract class HashSet<TStrategy, T> : HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T>;
public sealed class HashSet_String : HashSet<MemoryPackHashSetStrategy<StringHashSetValueProvider, string>, string>;
public sealed class HashSet_Int32 : HashSet<MemoryPackHashSetStrategy<Int32HashSetValueProvider, int>, int>;
public sealed class HashSet_UnmanagedStruct : HashSet<MemoryPackHashSetStrategy<UnmanagedStructHashSetValueProvider, UnmanagedStruct>, UnmanagedStruct>;
public sealed class HashSet_ManagedStruct : HashSet<MemoryPackHashSetStrategy<ManagedStructHashSetValueProvider, ManagedStruct>, ManagedStruct>;
public sealed class HashSet_ManagedClass : HashSet<MemoryPackHashSetStrategy<ManagedClassHashSetValueProvider, ManagedClass>, ManagedClass>;
