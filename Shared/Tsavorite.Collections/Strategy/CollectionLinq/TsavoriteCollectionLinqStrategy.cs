using Collection.Helper.CollectionLinq;
using Collection.Strategy.CollectionLinq;
#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections.Strategy.CollectionLinq;
#else
namespace Tsavorite.Collections.Strategy.CollectionLinq;
#endif

public sealed class TsavoriteDictionaryLinqSource<TKey, TValue>(
    StoreHandle<TKey,TValue> store,
    global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> dictionary):IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    public global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> Dictionary { get; } = dictionary;

    public void Dispose() {
        this.Dictionary.Dispose();
        store.Dispose();
    }
}

public readonly struct TsavoriteHashSetCollectionLinqStrategy<TSetStrategy, TElement>:ICollectionLinqStrategy<HashSetHandle<TElement>,TElement,TElement,TElement>
    where TSetStrategy : IHashSetTestStrategy<TElement>
    where TElement : IEquatable<TElement> {
    public static HashSetHandle<TElement> Create(int count) =>
        CreateRange(1,count);

    public static HashSetHandle<TElement> CreateSecond(int count) =>
        CreateRange(count/2+1,count);

    public static IEnumerable<TElement> AsEnumerable(HashSetHandle<TElement> source) =>
        source.Set;

    public static TElement Key(TElement element) =>
        element;

    public static TElement Value(TElement element) =>
        element;

    public static int Number(TElement element) =>
        CollectionLinqNumber.Parse(element);

    public static int KeyNumber(TElement key) =>
        CollectionLinqNumber.Parse(key);

    public static int ValueNumber(TElement value) =>
        CollectionLinqNumber.Parse(value);

    private static HashSetHandle<TElement> CreateRange(int start,int count) {
        var handle = TSetStrategy.CreateMemorySet(memorySize: CollectionLinqSourceSizing.GetMemorySize(count));
        for(var index = 0;index<count;index++) {
            handle.Set.Add(TSetStrategy.Elem(start+index));
        }
        return handle;
    }
}

public readonly struct TsavoriteDictionaryCollectionLinqStrategy<TDictionaryStrategy, TKey, TValue>
    :IDictionaryLinqStrategy<TsavoriteDictionaryLinqSource<TKey,TValue>,KeyValuePair<TKey,TValue>,TKey,TValue>
    where TDictionaryStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    public static TsavoriteDictionaryLinqSource<TKey,TValue> Create(int count) =>
        CreateRange(1,count);

    public static TsavoriteDictionaryLinqSource<TKey,TValue> CreateSecond(int count) =>
        CreateRange(count/2+1,count);

    public static IEnumerable<KeyValuePair<TKey,TValue>> AsEnumerable(TsavoriteDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary;

    public static TKey Key(KeyValuePair<TKey,TValue> element) =>
        element.Key;

    public static TValue Value(KeyValuePair<TKey,TValue> element) =>
        element.Value;

    public static int Number(KeyValuePair<TKey,TValue> element) =>
        CollectionLinqNumber.Parse(element.Key);

    public static int KeyNumber(TKey key) =>
        CollectionLinqNumber.Parse(key);

    public static int ValueNumber(TValue value) =>
        CollectionLinqNumber.Parse(value);

    public static IEnumerable<TKey> Keys(TsavoriteDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary.Keys;

    public static IEnumerable<TValue> Values(TsavoriteDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary.Values;

    private static TsavoriteDictionaryLinqSource<TKey,TValue> CreateRange(int start,int count) {
        var store = TDictionaryStrategy.CreateMemoryStore(memorySize: CollectionLinqSourceSizing.GetMemorySize(count));
        var dictionary = store.NewDict();
        for(var index = 0;index<count;index++) {
            var value = start+index;
            dictionary.Add(TDictionaryStrategy.K(value),TDictionaryStrategy.V(value));
        }
        return new TsavoriteDictionaryLinqSource<TKey,TValue>(store,dictionary);
    }
}
