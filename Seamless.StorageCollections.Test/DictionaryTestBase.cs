namespace Seamless.StorageCollections.Test;

/// <summary>
/// Abstract base class for all dictionary tests. Provides helper methods that
/// delegate to the <typeparamref name="TStrategy"/> factory, so concrete
/// subclasses only need to specify the strategy type.
/// </summary>
/// <typeparam name="TStrategy">The test strategy that supplies key/value factories and store constructors.</typeparam>
/// <typeparam name="TKey">The type of dictionary keys.</typeparam>
/// <typeparam name="TValue">The type of dictionary values.</typeparam>
public abstract class DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>,new()
    where TKey:notnull{
    protected readonly TStrategy Strategy=new();
    protected TKey Key(int seed=0)=>Strategy.Key(seed);
    protected TValue Value(int seed=0)=>Strategy.Value(seed);
    protected IDictionary<TKey,TValue> CreateMemory()=>Strategy.CreateMemory();
    protected IDictionary<TKey,TValue> CreateDisk()=>Strategy.CreateDisk();
}
