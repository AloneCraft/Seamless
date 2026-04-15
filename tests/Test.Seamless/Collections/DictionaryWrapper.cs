using Seamless.Collections;
using Seamless.Stores;

namespace Test.Seamless.Collections;

public class DictionaryWrapper
{
    [Fact, Trait("Category", "Unit")]
    public void Create_ManagedStore_AddとTryGetValueを委譲する()
    {
        using var store = Factory.Create().Create<int, string>(new UnmanagedTag(), new ManagedTag());
        using var dict = store.Create();

        dict.Add(1, "one");

        Assert.True(dict.TryGetValue(1, out var value));
        Assert.Equal("one", value);
        Assert.Single(dict);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_UnmanagedStore_RemoveOutを委譲する()
    {
        using var store = Factory.Create().Create<int, int>();
        using var dict = store.Create();

        Assert.Equal(TryAddResult.Success, dict.TryAdd(1, 10));
        Assert.True(dict.Remove(1, out var value));
        Assert.Equal(10, value);
        Assert.False(dict.ContainsKey(1));
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_KeyValuePair操作を委譲する()
    {
        using var store = Factory.Create().Create<int, string>(new UnmanagedTag(), new ManagedTag());
        using var dict = store.Create();
        ICollection<KeyValuePair<int, string>> collection = dict;

        dict.Add(1, "one");
        dict.Add(2, "two");

        Assert.True(collection.Contains(new KeyValuePair<int, string>(1, "one")));
        Assert.True(collection.Remove(new KeyValuePair<int, string>(1, "one")));
        Assert.Equal(new[] { 2 }, dict.Keys.OrderBy(key => key).ToArray());
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_ConcreteForeachはstruct列挙子で列挙できる()
    {
        using var store = Factory.Create().Create<int, string>(new UnmanagedTag(), new ManagedTag());
        using var dict = store.Create();

        dict.Add(1, "one");
        dict.Add(2, "two");

        var enumerator = dict.GetEnumerator();
        Assert.True(enumerator.GetType().IsValueType);

        var count = 0;
        foreach (var pair in dict)
        {
            Assert.True(pair.Key is 1 or 2);
            count++;
        }
        Assert.Equal(2, count);
    }
}

public abstract class StrategyDictionary<TStrategy, TKey, TValue> : DictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
}

public abstract class StrategyManagedDictionary<TInnerStrategy, TCollectionStrategy, TKey, TValue>
    : StrategyDictionary<StrategyDictionaryTestStrategy<TInnerStrategy, ManagedDictionaryStrategy<TKey, TValue>, TCollectionStrategy, TKey, TValue>, TKey, TValue>
    where TInnerStrategy : IDictTestStrategy<TKey, TValue>
    where TCollectionStrategy : struct, ICollectionStrategy<KeyValuePair<TKey, TValue>, ICollectionStrategyHost<KeyValuePair<TKey, TValue>>>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
}

public abstract class StrategyUnmanagedDictionary<TInnerStrategy, TCollectionStrategy, TKey, TValue>
    : StrategyDictionary<StrategyDictionaryTestStrategy<TInnerStrategy, UnmanagedDictionaryStrategy<TKey, TValue>, TCollectionStrategy, TKey, TValue>, TKey, TValue>
    where TInnerStrategy : IDictTestStrategy<TKey, TValue>
    where TCollectionStrategy : struct, ICollectionStrategy<KeyValuePair<TKey, TValue>, ICollectionStrategyHost<KeyValuePair<TKey, TValue>>>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_UnmanagedStruct_ManagedStruct
    : StrategyManagedDictionary<UnmanagedKeyDictionaryStrategy<UnmanagedStructDictTypeProvider, ManagedStructDictTypeProvider, UnmanagedStruct, ManagedStruct>, ManagedCollectionStrategy<KeyValuePair<UnmanagedStruct, ManagedStruct>>, UnmanagedStruct, ManagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_UnmanagedStruct_ManagedClass
    : StrategyManagedDictionary<UnmanagedKeyDictionaryStrategy<UnmanagedStructDictTypeProvider, ManagedClassDictTypeProvider, UnmanagedStruct, ManagedClass>, ManagedCollectionStrategy<KeyValuePair<UnmanagedStruct, ManagedClass>>, UnmanagedStruct, ManagedClass>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_ManagedStruct_UnmanagedStruct
    : StrategyManagedDictionary<UnmanagedValueDictionaryStrategy<ManagedStructDictTypeProvider, UnmanagedStructDictTypeProvider, ManagedStruct, UnmanagedStruct>, ManagedCollectionStrategy<KeyValuePair<ManagedStruct, UnmanagedStruct>>, ManagedStruct, UnmanagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_ManagedStruct_ManagedStruct
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider, ManagedStructDictTypeProvider, ManagedStruct, ManagedStruct>, ManagedCollectionStrategy<KeyValuePair<ManagedStruct, ManagedStruct>>, ManagedStruct, ManagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_ManagedStruct_ManagedClass
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider, ManagedClassDictTypeProvider, ManagedStruct, ManagedClass>, ManagedCollectionStrategy<KeyValuePair<ManagedStruct, ManagedClass>>, ManagedStruct, ManagedClass>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_ManagedClass_UnmanagedStruct
    : StrategyManagedDictionary<UnmanagedValueDictionaryStrategy<ManagedClassDictTypeProvider, UnmanagedStructDictTypeProvider, ManagedClass, UnmanagedStruct>, ManagedCollectionStrategy<KeyValuePair<ManagedClass, UnmanagedStruct>>, ManagedClass, UnmanagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_ManagedClass_ManagedStruct
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider, ManagedStructDictTypeProvider, ManagedClass, ManagedStruct>, ManagedCollectionStrategy<KeyValuePair<ManagedClass, ManagedStruct>>, ManagedClass, ManagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_ManagedCollectionStrategy_ManagedClass_ManagedClass
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider, ManagedClassDictTypeProvider, ManagedClass, ManagedClass>, ManagedCollectionStrategy<KeyValuePair<ManagedClass, ManagedClass>>, ManagedClass, ManagedClass>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_UnmanagedStruct_ManagedStruct
    : StrategyManagedDictionary<UnmanagedKeyDictionaryStrategy<UnmanagedStructDictTypeProvider, ManagedStructDictTypeProvider, UnmanagedStruct, ManagedStruct>, UnmanagedCollectionStrategy<KeyValuePair<UnmanagedStruct, ManagedStruct>>, UnmanagedStruct, ManagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_UnmanagedStruct_ManagedClass
    : StrategyManagedDictionary<UnmanagedKeyDictionaryStrategy<UnmanagedStructDictTypeProvider, ManagedClassDictTypeProvider, UnmanagedStruct, ManagedClass>, UnmanagedCollectionStrategy<KeyValuePair<UnmanagedStruct, ManagedClass>>, UnmanagedStruct, ManagedClass>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_ManagedStruct_UnmanagedStruct
    : StrategyManagedDictionary<UnmanagedValueDictionaryStrategy<ManagedStructDictTypeProvider, UnmanagedStructDictTypeProvider, ManagedStruct, UnmanagedStruct>, UnmanagedCollectionStrategy<KeyValuePair<ManagedStruct, UnmanagedStruct>>, ManagedStruct, UnmanagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_ManagedStruct_ManagedStruct
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider, ManagedStructDictTypeProvider, ManagedStruct, ManagedStruct>, UnmanagedCollectionStrategy<KeyValuePair<ManagedStruct, ManagedStruct>>, ManagedStruct, ManagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_ManagedStruct_ManagedClass
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider, ManagedClassDictTypeProvider, ManagedStruct, ManagedClass>, UnmanagedCollectionStrategy<KeyValuePair<ManagedStruct, ManagedClass>>, ManagedStruct, ManagedClass>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_ManagedClass_UnmanagedStruct
    : StrategyManagedDictionary<UnmanagedValueDictionaryStrategy<ManagedClassDictTypeProvider, UnmanagedStructDictTypeProvider, ManagedClass, UnmanagedStruct>, UnmanagedCollectionStrategy<KeyValuePair<ManagedClass, UnmanagedStruct>>, ManagedClass, UnmanagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_ManagedClass_ManagedStruct
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider, ManagedStructDictTypeProvider, ManagedClass, ManagedStruct>, UnmanagedCollectionStrategy<KeyValuePair<ManagedClass, ManagedStruct>>, ManagedClass, ManagedStruct>
{
}

public sealed class StrategyDictionary_ManagedDictionaryStrategy_UnmanagedCollectionStrategy_ManagedClass_ManagedClass
    : StrategyManagedDictionary<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider, ManagedClassDictTypeProvider, ManagedClass, ManagedClass>, UnmanagedCollectionStrategy<KeyValuePair<ManagedClass, ManagedClass>>, ManagedClass, ManagedClass>
{
}

public sealed class StrategyDictionary_UnmanagedDictionaryStrategy_ManagedCollectionStrategy_UnmanagedStruct_UnmanagedStruct
    : StrategyUnmanagedDictionary<UnmanagedStoreDictionaryStrategy<UnmanagedStructDictTypeProvider, UnmanagedStructDictTypeProvider, UnmanagedStruct, UnmanagedStruct>, ManagedCollectionStrategy<KeyValuePair<UnmanagedStruct, UnmanagedStruct>>, UnmanagedStruct, UnmanagedStruct>
{
}

public sealed class StrategyDictionary_UnmanagedDictionaryStrategy_UnmanagedCollectionStrategy_UnmanagedStruct_UnmanagedStruct
    : StrategyUnmanagedDictionary<UnmanagedStoreDictionaryStrategy<UnmanagedStructDictTypeProvider, UnmanagedStructDictTypeProvider, UnmanagedStruct, UnmanagedStruct>, UnmanagedCollectionStrategy<KeyValuePair<UnmanagedStruct, UnmanagedStruct>>, UnmanagedStruct, UnmanagedStruct>
{
}