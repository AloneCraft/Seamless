using Test.Tsavorite.Unit.Helpers.Collections;
namespace Test.Tsavorite.Unit.Collections;

public abstract class HashSet<TStrategy, T> : HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T>;
public sealed class HashSet_String : HashSet<MemoryPackHashSetStrategy<StringHashSetValueProvider, string>, string>;
public sealed class HashSet_Int32 : HashSet<MemoryPackHashSetStrategy<Int32HashSetValueProvider, int>, int>;

