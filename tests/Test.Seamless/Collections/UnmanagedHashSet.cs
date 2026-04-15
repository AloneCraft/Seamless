namespace Test.Seamless.Collections;

public abstract class UnmanagedHashSet<TStrategy, T> : HashSetTestBase<TStrategy, global::Seamless.Collections.UnmanagedHashSet<T>, T>
    where TStrategy : IHashSetTestStrategy<global::Seamless.Collections.UnmanagedHashSet<T>, T>
    where T : unmanaged, IEquatable<T>
{
}

public sealed class UnmanagedHashSet_Int : UnmanagedHashSet<UnmanagedIntHashSetStrategy, int>
{
}