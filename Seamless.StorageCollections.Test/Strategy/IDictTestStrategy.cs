namespace Seamless.StorageCollections.Test;

/// <summary>
/// Provides factory methods for creating test keys, values, and dictionary instances
/// used by the generic test infrastructure.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public interface IDictTestStrategy<TKey, TValue> where TKey : notnull {
    TKey Key(int seed=0);
    TValue Value(int seed=0);
    IDictionary<TKey,TValue> CreateMemory();
    IDictionary<TKey,TValue> CreateDisk();
}
