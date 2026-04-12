using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// ストレージ用の UnmanagedHashSet 実装。
/// ログの容量制限を行わず、オーバーフローチェックを常に false を返す。
/// </summary>
/// <typeparam name="TKey">unmanaged かつ IEquatable を実装する型</typeparam>
public sealed class StorageUnmanagedHashSet<TKey> : UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey>
{
    /// <summary>
    /// StorageUnmanagedHashSet のコンストラクタ。
    /// </summary>
    /// <param name="collectionStore">UnmanagedCollectionStore インスタンス</param>
    internal StorageUnmanagedHashSet(
        UnmanagedCollectionStore<TKey, EqualityEmpty> collectionStore
    ) : base(collectionStore)
    {
    }

    /// <summary>
    /// ストレージ版ではログのオーバーフローを考慮しないため、常に false を返す。
    /// </summary>
    /// <param name="keyLength">キーのバイト長</param>
    /// <param name="valueLength">値のバイト長</param>
    /// <returns>常に false</returns>
    private protected override bool WouldOverflowMutable(int keyLength, int valueLength) => false;
}