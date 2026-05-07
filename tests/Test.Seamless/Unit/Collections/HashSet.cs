using Test.Seamless.Unit.Helpers.Collections;
namespace Test.Seamless.Unit.Collections;

public abstract class ManagedHashSet<TStrategy, T> : HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T>;
public sealed class HashSet_String : ManagedHashSet<ManagedHashSetStrategy<StringHashSetValueProvider, string>, string>;
public sealed class HashSet_Int32 : ManagedHashSet<ManagedHashSetStrategy<Int32HashSetValueProvider, int>, int>;

