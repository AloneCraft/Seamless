namespace Seamless.Tsavorite.Collections;

/// <summary>
/// IDictionaryの動作を検証する。
/// IDictionaryの動作を検証する。
/// IDictionaryの動作を検証する。
/// IDictionaryの動作を検証する。
/// </summary>
public interface IDictionary<TKey, TValue>:global::System.Collections.Generic.IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>, IMemoryCollection<KeyValuePair<TKey,TValue>>, IDisposable {
    /// <summary>IDictionaryの動作を検証する。</summary>
    bool IsDisposed { get; }
    /// <summary>
    /// LightEpoch の取得と解放を検証する。
    /// IDictionaryの動作を検証する。
    /// </summary>
    void Refresh() {
    }
    /// <summary>IDictionaryの動作を検証する。</summary>
    long EntryCount { get; }
    /// <summary>IDictionaryの動作を検証する。</summary>
    long GetLongCount() => this.EntryCount;
    /// <summary>IDictionaryの動作を検証する。</summary>
    TryAddResult TryAdd(TKey key,TValue value);
    /// <summary>IDictionaryの動作を検証する。</summary>
    bool Contains(TKey key) => this.ContainsKey(key);
    /// <summary>IDictionaryの動作を検証する。</summary>
    bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value);
    // Read 経路はこの処理では使用しない。
    // IDictionaryの動作を検証する。
    new bool ContainsKey(TKey key);
    new bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value);
    new TValue this[TKey key] { get; set; }
    new int Count { get; }
    new ICollection<TKey> Keys { get; }
    new ICollection<TValue> Values { get; }
}
