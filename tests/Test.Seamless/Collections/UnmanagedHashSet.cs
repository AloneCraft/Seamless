namespace Test.Seamless.Collections;

public abstract class UnmanagedHashSet<TStrategy, T> : HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : unmanaged, IEquatable<T>
{
}

public sealed class UnmanagedHashSet_Int : UnmanagedHashSet<UnmanagedHashSetStrategy<Int32HashSetValueProvider, int>, int>
{
}