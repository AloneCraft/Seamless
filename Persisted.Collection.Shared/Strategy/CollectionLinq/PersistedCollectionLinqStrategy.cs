using Collection.Helper.CollectionLinq;
using Collection.Strategy.CollectionLinq;
using Seamless.Serialization;

namespace Persisted.Collection.Strategy.CollectionLinq;

public sealed class PersistedSetLinqSource<T>(string path,global::Seamless.Persisted.Collections.Set<T> set):IDisposable
    where T : IEquatable<T>
{
    public global::Seamless.Persisted.Collections.Set<T> Set { get; } = set;

    public void Dispose() {
        this.Set.Dispose();
        PersistedCollectionLinqPaths.TryDeleteDirectory(path);
    }
}

public sealed class PersistedListLinqSource<T>(string path,ListHandle<T> handle):IDisposable
{
    public global::Seamless.Persisted.Collections.PersistedList<T> List => handle.List;

    public void Dispose() {
        handle.Dispose();
        PersistedCollectionLinqPaths.TryDeleteDirectory(path);
    }
}

public sealed class PersistedDictionaryLinqSource<TKey,TValue>(
    string path,
    IReadOnlyDictionary<TKey,TValue> dictionary,
    IDisposable disposable):IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    public IReadOnlyDictionary<TKey,TValue> Dictionary { get; } = dictionary;

    public void Dispose() {
        disposable.Dispose();
        PersistedCollectionLinqPaths.TryDeleteDirectory(path);
    }
}

public readonly struct PersistedSetCollectionLinqStrategy<TProvider,TElement>
    :ICollectionLinqStrategy<PersistedSetLinqSource<TElement>,TElement,TElement,TElement>
    where TProvider : IHashSetValueProvider<TElement>
    where TElement : IEquatable<TElement>
{
    public static PersistedSetLinqSource<TElement> Create(int count) =>
        CreateRange(nameof(Create),1,count);

    public static PersistedSetLinqSource<TElement> CreateSecond(int count) =>
        CreateRange(nameof(CreateSecond),count/2+1,count);

    public static IEnumerable<TElement> AsEnumerable(PersistedSetLinqSource<TElement> source) =>
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

    private static PersistedSetLinqSource<TElement> CreateRange(string tag,int start,int count) {
        var path = PersistedCollectionLinqPaths.CreatePath<PersistedSetCollectionLinqStrategy<TProvider,TElement>,TElement>(tag);
        var set = new global::Seamless.Persisted.Collections.Set<TElement>(path,initialCapacity: 1,initialDataCapacity: 4096);
        for(var index = 0;index<count;index++) {
            set.Add(TProvider.Create(start+index));
        }
        set.Compact();
        return new PersistedSetLinqSource<TElement>(path,set);
    }
}

public readonly struct PersistedListCollectionLinqStrategy<TListStrategy,TElement>
    :ICollectionLinqStrategy<PersistedListLinqSource<TElement>,TElement,TElement,TElement>
    where TListStrategy : IListTestStrategy<TElement>
    where TElement : IEquatable<TElement>
{
    public static PersistedListLinqSource<TElement> Create(int count) =>
        CreateRange(nameof(Create),1,count);

    public static PersistedListLinqSource<TElement> CreateSecond(int count) =>
        CreateRange(nameof(CreateSecond),count/2+1,count);

    public static IEnumerable<TElement> AsEnumerable(PersistedListLinqSource<TElement> source) =>
        source.List;

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

    private static PersistedListLinqSource<TElement> CreateRange(string tag,int start,int count) {
        var path = PersistedCollectionLinqPaths.CreatePath<PersistedListCollectionLinqStrategy<TListStrategy,TElement>,TElement>(tag);
        var handle = TListStrategy.CreateList(path,initialCapacity: 1,initialDataCapacity: 4096);
        for(var index = 0;index<count;index++) {
            handle.List.Add(TListStrategy.Elem(start+index));
        }
        return new PersistedListLinqSource<TElement>(path,handle);
    }
}

public readonly struct PersistedBlittableDictionaryCollectionLinqStrategy<TProvider,TKey,TValue>
    :IDictionaryLinqStrategy<PersistedDictionaryLinqSource<TKey,TValue>,KeyValuePair<TKey,TValue>,TKey,TValue>
    where TProvider : IDictTestStrategy<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    public static PersistedDictionaryLinqSource<TKey,TValue> Create(int count) =>
        CreateRange(nameof(Create),1,count);

    public static PersistedDictionaryLinqSource<TKey,TValue> CreateSecond(int count) =>
        CreateRange(nameof(CreateSecond),count/2+1,count);

    public static IEnumerable<KeyValuePair<TKey,TValue>> AsEnumerable(PersistedDictionaryLinqSource<TKey,TValue> source) =>
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

    public static IEnumerable<TKey> Keys(PersistedDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary.Keys;

    public static IEnumerable<TValue> Values(PersistedDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary.Values;

    private static PersistedDictionaryLinqSource<TKey,TValue> CreateRange(string tag,int start,int count) {
        var path = PersistedCollectionLinqPaths.CreatePath<PersistedBlittableDictionaryCollectionLinqStrategy<TProvider,TKey,TValue>,KeyValuePair<TKey,TValue>>(tag);
        var dictionary = new global::Seamless.Persisted.Collections.BlittableDictionary<TKey,TValue>(path,initialCapacity: 1,initialDataCapacity: 4096);
        Populate(dictionary,start,count);
        dictionary.Compact();
        return new PersistedDictionaryLinqSource<TKey,TValue>(path,dictionary,dictionary);
    }

    private static void Populate(global::Seamless.Persisted.Collections.BlittableDictionary<TKey,TValue> dictionary,int start,int count) {
        for(var index = 0;index<count;index++) {
            var value = start+index;
            dictionary.Add(TProvider.K(value),TProvider.V(value));
        }
    }
}

public readonly struct PersistedSerializedDictionaryCollectionLinqStrategy<TProvider,TKey,TValue>
    :IDictionaryLinqStrategy<PersistedDictionaryLinqSource<TKey,TValue>,KeyValuePair<TKey,TValue>,TKey,TValue>
    where TProvider : IDictTestStrategy<TKey,TValue>
    where TKey : notnull, IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    public static PersistedDictionaryLinqSource<TKey,TValue> Create(int count) =>
        CreateRange(nameof(Create),1,count);

    public static PersistedDictionaryLinqSource<TKey,TValue> CreateSecond(int count) =>
        CreateRange(nameof(CreateSecond),count/2+1,count);

    public static IEnumerable<KeyValuePair<TKey,TValue>> AsEnumerable(PersistedDictionaryLinqSource<TKey,TValue> source) =>
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

    public static IEnumerable<TKey> Keys(PersistedDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary.Keys;

    public static IEnumerable<TValue> Values(PersistedDictionaryLinqSource<TKey,TValue> source) =>
        source.Dictionary.Values;

    private static PersistedDictionaryLinqSource<TKey,TValue> CreateRange(string tag,int start,int count) {
        var path = PersistedCollectionLinqPaths.CreatePath<PersistedSerializedDictionaryCollectionLinqStrategy<TProvider,TKey,TValue>,KeyValuePair<TKey,TValue>>(tag);
        var dictionary = new global::Seamless.Persisted.Collections.SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(path,initialCapacity: 1,initialDataCapacity: 4096);
        Populate(dictionary,start,count);
        dictionary.Compact();
        return new PersistedDictionaryLinqSource<TKey,TValue>(path,dictionary,dictionary);
    }

    private static void Populate(global::Seamless.Persisted.Collections.SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>> dictionary,int start,int count) {
        for(var index = 0;index<count;index++) {
            var value = start+index;
            dictionary.Add(TProvider.K(value),TProvider.V(value));
        }
    }
}

internal static class PersistedCollectionLinqPaths {
    public static string CreatePath<TStrategy,TElement>(string tag) {
        var directoryPath = Path.Combine(
            Environment.CurrentDirectory,
            typeof(TStrategy).Name,
            typeof(TElement).Name,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath,tag+".dat");
    }

    public static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath)) {
            return;
        }
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch(IOException) {
        }
        catch(UnauthorizedAccessException) {
        }
    }
}
