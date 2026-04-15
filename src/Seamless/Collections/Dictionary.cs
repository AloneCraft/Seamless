using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Seamless.Collections;

public sealed class Dictionary<TKey, TValue, TStrategy> : ISeamlessDictionary<TKey,TValue>, IDictionaryStrategyHost<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TStrategy : struct, IDictionaryStrategy<TKey,TValue,IDictionaryStrategyHost<TKey,TValue>> {
    private readonly ISeamlessDictionary<TKey,TValue> dictionary;

    public Dictionary(ISeamlessDictionary<TKey,TValue> dictionary){
        ArgumentNullException.ThrowIfNull(dictionary);
        this.dictionary=dictionary;
    }

    public ISeamlessDictionary<TKey,TValue> DictionaryCore => this.dictionary;
    public ICollection<KeyValuePair<TKey,TValue>> CollectionCore => this.dictionary;
    public bool IsDisposed => this.dictionary.IsDisposed;
    public long EntryCount => this.dictionary.EntryCount;
    public TryAddResult TryAdd(TKey key,TValue value) => this.dictionary.TryAdd(key,value);
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) => this.dictionary.Remove(key,out value);

    public TValue this[TKey key] {
        get => TStrategy.get_Item(this,key);
        set => TStrategy.set_Item(this,key,value);
    }
    public ICollection<TKey> Keys => TStrategy.Keys(this);
    public ICollection<TValue> Values => TStrategy.Values(this);
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Values;
    public bool ContainsKey(TKey key) => TStrategy.ContainsKey(this,key);
    public void Add(TKey key,TValue value) => TStrategy.Add(this,key,value);
    public bool Remove(TKey key) => TStrategy.Remove(this,key);
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value) => TStrategy.TryGetValue(this,key,out value);
    public long GetLongCount() => TStrategy.GetLongCount(this);
    IEnumerator IEnumerable.GetEnumerator() => TStrategy.IEnumerable_GetEnumerator(this);
    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => TStrategy.GetEnumerator(this);
    public int Count => TStrategy.Count(this);
    public bool IsReadOnly => TStrategy.IsReadOnly(this);
    void ICollection<KeyValuePair<TKey,TValue>>.Add(KeyValuePair<TKey,TValue> item) => TStrategy.Add(this,item);
    public void Clear() => TStrategy.Clear(this);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) => TStrategy.Contains(this,item);
    void ICollection<KeyValuePair<TKey,TValue>>.CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) => TStrategy.CopyTo(this,array,arrayIndex);
    bool ICollection<KeyValuePair<TKey,TValue>>.Remove(KeyValuePair<TKey,TValue> item) => TStrategy.Remove(this,item);
    public void Dispose() => this.dictionary.Dispose();
}

public sealed class Dictionary<TKey, TValue, TDictionaryStrategy, TCollectionStrategy> : ISeamlessDictionary<TKey,TValue>, IDictionaryStrategyHost<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TDictionaryStrategy : struct, IDictionaryStrategy<TKey,TValue,IDictionaryStrategyHost<TKey,TValue>>
    where TCollectionStrategy : struct, ICollectionStrategy<KeyValuePair<TKey,TValue>,ICollectionStrategyHost<KeyValuePair<TKey,TValue>>> {
    private readonly ISeamlessDictionary<TKey,TValue> dictionary;

    public Dictionary(ISeamlessDictionary<TKey,TValue> dictionary){
        ArgumentNullException.ThrowIfNull(dictionary);
        this.dictionary=dictionary;
    }

    public ISeamlessDictionary<TKey,TValue> DictionaryCore => this.dictionary;
    public ICollection<KeyValuePair<TKey,TValue>> CollectionCore => this.dictionary;
    public bool IsDisposed => this.dictionary.IsDisposed;
    public long EntryCount => this.dictionary.EntryCount;
    public TryAddResult TryAdd(TKey key,TValue value) => this.dictionary.TryAdd(key,value);
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) => this.dictionary.Remove(key,out value);

    public TValue this[TKey key] {
        get => TDictionaryStrategy.get_Item(this,key);
        set => TDictionaryStrategy.set_Item(this,key,value);
    }
    public ICollection<TKey> Keys => TDictionaryStrategy.Keys(this);
    public ICollection<TValue> Values => TDictionaryStrategy.Values(this);
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Values;
    public bool ContainsKey(TKey key) => TDictionaryStrategy.ContainsKey(this,key);
    public void Add(TKey key,TValue value) => TDictionaryStrategy.Add(this,key,value);
    public bool Remove(TKey key) => TDictionaryStrategy.Remove(this,key);
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value) => TDictionaryStrategy.TryGetValue(this,key,out value);
    public long GetLongCount() => TDictionaryStrategy.GetLongCount(this);
    IEnumerator IEnumerable.GetEnumerator() => TDictionaryStrategy.IEnumerable_GetEnumerator(this);
    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => TDictionaryStrategy.GetEnumerator(this);
    public int Count => TCollectionStrategy.Count(this);
    public bool IsReadOnly => TCollectionStrategy.IsReadOnly(this);
    void ICollection<KeyValuePair<TKey,TValue>>.Add(KeyValuePair<TKey,TValue> item) => TCollectionStrategy.Add(this,item);
    public void Clear() => TCollectionStrategy.Clear(this);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) => TCollectionStrategy.Contains(this,item);
    void ICollection<KeyValuePair<TKey,TValue>>.CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) => TCollectionStrategy.CopyTo(this,array,arrayIndex);
    bool ICollection<KeyValuePair<TKey,TValue>>.Remove(KeyValuePair<TKey,TValue> item) => TCollectionStrategy.Remove(this,item);
    public void Dispose() => this.dictionary.Dispose();
}
public interface IHashSet<TKey, in THashSet>:ICollection<TKey>
where TKey : IEquatable<TKey>
where THashSet : IHashSet<TKey,THashSet> {
    new bool Add(TKey item);
    void UnionWith(THashSet other);
    void IntersectWith(THashSet other);
    void ExceptWith(THashSet other);
    void SymmetricExceptWith(THashSet other);
    bool IsSubsetOf(THashSet other);
    bool IsSupersetOf(THashSet other);
    bool IsProperSupersetOf(THashSet other);
    bool IsProperSubsetOf(THashSet other);
    bool Overlaps(THashSet other);
    bool SetEquals(THashSet other);
}

public sealed class HashSet<TKey, TStrategy> : IHashSetStrategyHost<TKey>
    where TKey : IEquatable<TKey>
    where TStrategy : struct,
        IHashSetStrategy<TKey,IHashSetStrategyHost<TKey>>,
        ICollectionStrategy<TKey,ICollectionStrategyHost<TKey>> {
    private readonly object hashSetCore;
    private readonly ICollection<TKey> collectionCore;
    private readonly IDisposable? disposable;

    public HashSet(ICollection<TKey> hashSet){
        ArgumentNullException.ThrowIfNull(hashSet);
        this.hashSetCore=hashSet;
        this.collectionCore=hashSet;
        this.disposable=hashSet as IDisposable;
    }

    public object HashSetCore => this.hashSetCore;
    public ICollection<TKey> CollectionCore => this.collectionCore;

    public bool Add(TKey item) => TStrategy.TryAdd(this,item);
    public void UnionWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        TStrategy.UnionWith(this,other);
    }
    public void IntersectWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        TStrategy.IntersectWith(this,other);
    }
    public void ExceptWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        TStrategy.ExceptWith(this,other);
    }
    public void SymmetricExceptWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        TStrategy.SymmetricExceptWith(this,other);
    }
    public bool IsSubsetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return TStrategy.IsSubsetOf(this,other);
    }
    public bool IsSupersetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return TStrategy.IsSupersetOf(this,other);
    }
    public bool IsProperSupersetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return TStrategy.IsProperSupersetOf(this,other);
    }
    public bool IsProperSubsetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return TStrategy.IsProperSubsetOf(this,other);
    }
    public bool Overlaps(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return TStrategy.Overlaps(this,other);
    }
    public bool SetEquals(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return TStrategy.SetEquals(this,other);
    }
    IEnumerator IEnumerable.GetEnumerator() => TStrategy.IEnumerable_GetEnumerator(this);
    public IEnumerator<TKey> GetEnumerator() => TStrategy.GetEnumerator(this);
    public int Count => TStrategy.Count(this);
    public bool IsReadOnly => TStrategy.IsReadOnly(this);
    void ICollection<TKey>.Add(TKey item) => TStrategy.Add(this,item);
    public void Clear() => TStrategy.Clear(this);
    public bool Contains(TKey item) => TStrategy.Contains(this,item);
    public void CopyTo(TKey[] array,int arrayIndex) => TStrategy.CopyTo(this,array,arrayIndex);
    public bool Remove(TKey item) => TStrategy.Remove(this,item);
    public void Dispose() => this.disposable?.Dispose();
}

public sealed class HashSet<TKey, THashSetStrategy, TCollectionStrategy> : IHashSetStrategyHost<TKey>
    where TKey : IEquatable<TKey>
    where THashSetStrategy : struct, IHashSetStrategy<TKey,IHashSetStrategyHost<TKey>>
    where TCollectionStrategy : struct, ICollectionStrategy<TKey,ICollectionStrategyHost<TKey>> {
    private readonly object hashSetCore;
    private readonly ICollection<TKey> collectionCore;
    private readonly IDisposable? disposable;

    public HashSet(ICollection<TKey> hashSet){
        ArgumentNullException.ThrowIfNull(hashSet);
        this.hashSetCore=hashSet;
        this.collectionCore=hashSet;
        this.disposable=hashSet as IDisposable;
    }

    public object HashSetCore => this.hashSetCore;
    public ICollection<TKey> CollectionCore => this.collectionCore;

    public bool Add(TKey item) => THashSetStrategy.TryAdd(this,item);
    public void UnionWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        THashSetStrategy.UnionWith(this,other);
    }
    public void IntersectWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        THashSetStrategy.IntersectWith(this,other);
    }
    public void ExceptWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        THashSetStrategy.ExceptWith(this,other);
    }
    public void SymmetricExceptWith(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        THashSetStrategy.SymmetricExceptWith(this,other);
    }
    public bool IsSubsetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return THashSetStrategy.IsSubsetOf(this,other);
    }
    public bool IsSupersetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return THashSetStrategy.IsSupersetOf(this,other);
    }
    public bool IsProperSupersetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return THashSetStrategy.IsProperSupersetOf(this,other);
    }
    public bool IsProperSubsetOf(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return THashSetStrategy.IsProperSubsetOf(this,other);
    }
    public bool Overlaps(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return THashSetStrategy.Overlaps(this,other);
    }
    public bool SetEquals(IHashSetStrategyHost<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return THashSetStrategy.SetEquals(this,other);
    }
    IEnumerator IEnumerable.GetEnumerator() => THashSetStrategy.IEnumerable_GetEnumerator(this);
    public IEnumerator<TKey> GetEnumerator() => THashSetStrategy.GetEnumerator(this);
    public int Count => TCollectionStrategy.Count(this);
    public bool IsReadOnly => TCollectionStrategy.IsReadOnly(this);
    void ICollection<TKey>.Add(TKey item) => TCollectionStrategy.Add(this,item);
    public void Clear() => TCollectionStrategy.Clear(this);
    public bool Contains(TKey item) => TCollectionStrategy.Contains(this,item);
    public void CopyTo(TKey[] array,int arrayIndex) => TCollectionStrategy.CopyTo(this,array,arrayIndex);
    public bool Remove(TKey item) => TCollectionStrategy.Remove(this,item);
    public void Dispose() => this.disposable?.Dispose();
}
