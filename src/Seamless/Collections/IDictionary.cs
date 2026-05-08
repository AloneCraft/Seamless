using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Seamless.Collections;

/// <summary>
/// Tsavorite バックエンドを持つ Dictionary の共通インターフェース。
/// <see cref="IDictionary{TKey,TValue}"/> に加え、管理・アンマネージ両実装が共有する
/// <see cref="IsDisposed"/>、<see cref="EntryCount"/>、<see cref="TryAdd"/>、
/// <see cref="Remove(TKey,out TValue)"/> を公開する。
/// </summary>
public interface IDictionary<TKey, TValue>:System.Collections.Generic.IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>, IMemoryCollection<KeyValuePair<TKey,TValue>>, IDisposable {
    /// <summary>このインスタンスが既に破棄されているかどうかを取得する。</summary>
    bool IsDisposed { get; }
    /// <summary>
    /// 内部 session を張り替えて、Tsavorite が保持している古い epoch 世代参照を解放する。
    /// 既定実装は no-op で、session を明示管理する実装が必要に応じて上書きする。
    /// </summary>
    void Refresh() {
    }
    /// <summary>ストアに格納されているエントリ数を取得する。</summary>
    long EntryCount { get; }
    /// <summary>既存呼び出し互換のための long 件数取得メソッド。</summary>
    long GetLongCount() => this.EntryCount;
    /// <summary>キーが存在しない場合のみ追加する。</summary>
    TryAddResult TryAdd(TKey key,TValue value);
    /// <summary>指定したキーが存在するかどうかを返す。</summary>
    bool Contains(TKey key) => this.ContainsKey(key);
    /// <summary>キーを削除し、削除された値を返す。</summary>
    bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value);
    // IDictionary<TKey,TValue> と IReadOnlyDictionary<TKey,TValue> の両方に同名メンバーが存在するため、
    // new で単一のスロットとして宣言し、呼び出し側のあいまいさを解消する。
    new bool ContainsKey(TKey key);
    new bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value);
    new TValue this[TKey key] { get; set; }
    new int Count { get; }
    new ICollection<TKey> Keys { get; }
    new ICollection<TValue> Values { get; }
}
