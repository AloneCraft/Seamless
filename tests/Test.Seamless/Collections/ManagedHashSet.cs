namespace Test.Seamless.Collections;

public abstract class ManagedHashSet<TStrategy, T> : HashSetTestBase<TStrategy, global::Seamless.Collections.ManagedHashSet<T>, T>
    where TStrategy : IHashSetTestStrategy<global::Seamless.Collections.ManagedHashSet<T>, T>
    where T : IEquatable<T>
{
}

public sealed class ManagedHashSet_String:ManagedHashSet<ManagedStringHashSetStrategy,string>;
