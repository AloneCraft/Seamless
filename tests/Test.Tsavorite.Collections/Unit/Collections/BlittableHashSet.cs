using Test.Tsavorite.Unit.Helpers.Collections;
namespace Test.Tsavorite.Unit.Collections;

public abstract class BlittableHashSet<TStrategy, T> : HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : unmanaged, IEquatable<T>
{
}

public sealed class BlittableHashSet_Int : BlittableHashSet<BlittableHashSetStrategy<Int32HashSetValueProvider, int>, int>
{
}
