using System.Diagnostics.CodeAnalysis;

namespace Seamless.Collections;

/// <summary>
/// Tsavorite バックエンドを持つ Dictionary の共通インターフェース。
/// <see cref="IDictionary{TKey,TValue}"/> に加え、管理・アンマネージ両実装が共有する
/// <see cref="IsDisposed"/>、<see cref="EntryCount"/>、<see cref="TryAdd"/>、
/// <see cref="Remove(TKey,out TValue)"/> を公開する。
/// </summary>
public interface ISeamlessDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    /// <summary>このインスタンスが既に破棄されているかどうかを取得する。</summary>
    bool IsDisposed { get; }
    /// <summary>ストアに格納されているエントリ数を取得する。</summary>
    long EntryCount { get; }
    /// <summary>キーが存在しない場合のみ追加する。</summary>
    new TryAddResult TryAdd(TKey key, TValue value);
    /// <summary>キーを削除し、削除された値を返す。</summary>
    bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value);
}
