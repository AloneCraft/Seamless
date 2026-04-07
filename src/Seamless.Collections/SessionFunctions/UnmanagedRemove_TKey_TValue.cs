#if TSAVORITE1_1_1
using Seamless.Collections;
using Tsavorite.core;

namespace Seamless.SessionFunctions;

using Empty = Seamless.Collections.Empty;

/// <summary>
/// RMW でキーが存在する場合に値を Output に返してから論理削除するセッション関数。
/// </summary>
internal class UnmanagedRemove_TKey_TValue<TKey, TValue> : SessionFunctionsBase<TKey, TValue, TValue, TValue, Empty>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    /// <summary>キーが存在しない場合は新規作成しない。</summary>
    public override bool NeedInitialUpdate(ref TKey key, ref TValue input, ref TValue output, ref RMWInfo rmwInfo) => false;

    /// <summary>Mutable なレコードが存在する場合：値を Output にコピーして論理削除。</summary>
    public override bool InPlaceUpdater(ref TKey key, ref TValue input, ref TValue value, ref TValue output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
    {
        output = value;
        rmwInfo.Action = RMWAction.ExpireAndStop;
        return false;
    }

    /// <summary>ReadOnly / Disk のレコードが存在する場合：コピー更新を許可。</summary>
    public override bool NeedCopyUpdate(ref TKey key, ref TValue input, ref TValue oldValue, ref TValue output, ref RMWInfo rmwInfo) => true;

    /// <summary>コピー更新時：値を Output にコピーして論理削除。</summary>
    public override bool CopyUpdater(ref TKey key, ref TValue input, ref TValue oldValue, ref TValue newValue, ref TValue output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
    {
        output = oldValue;
        rmwInfo.Action = RMWAction.ExpireAndStop;
        return false;
    }
}
#endif
