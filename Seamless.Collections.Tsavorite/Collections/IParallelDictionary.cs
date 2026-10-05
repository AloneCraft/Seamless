namespace Seamless.Collections.Tsavorite;

/// <summary>
/// Tsavorite の辞書のマルチ用の操作用オブジェクト。並列区間(<see cref="DictionaryStore{TKey,TValue}.BeginParallel"/>)の中で、スレッドごとに作る。
/// 件数を管理しないので、件数の窓口(Count・ICollection・IReadOnlyDictionary など)は持たない。件数は <see cref="ScanCount"/> が毎回全件を数える。
/// </summary>
public interface IParallelDictionary<TKey, TValue>:IEnumerable<KeyValuePair<TKey,TValue>>, IDisposable {
    bool IsDisposed { get; }
    /// <summary>読むときはキーが無ければ KeyNotFoundException。書くときは追加か上書き(新規かどうかは数えない)。</summary>
    TValue this[TKey key] { get; set; }
    TryAddResult TryAdd(TKey key,TValue value);
    void Add(TKey key,TValue value);
    bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value);
    bool ContainsKey(TKey key);
    bool Remove(TKey key);
    bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value);
    /// <summary>全件を数える(毎回全走査)。他のスレッドが書いている途中でも呼べるが、その時点の値ではない。</summary>
    long ScanCount();
}
