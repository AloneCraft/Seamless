#if TSAVORITE1_1_1
using Seamless.Collections;
using Tsavorite.core;

namespace Seamless.SessionFunctions;

using Empty = Seamless.Collections.Empty;

/// <summary>
/// RMW でキーと値が両方一致する場合のみ論理削除するセッション関数。
/// TInput = TValue（期待する値）、TOutput = bool（削除が行われたか）。
/// </summary>
internal class UnmanagedRemove_KeyValuePair<TKey, TValue> : SessionFunctionsBase<TKey, TValue, TValue, bool, Empty>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    /// <summary>キーが存在しない場合は新規作成しない。</summary>
    public override bool NeedInitialUpdate(ref TKey key, ref TValue input, ref bool output, ref RMWInfo rmwInfo) => false;

    /// <summary>
    /// Mutable なレコードが存在する場合：値が一致すれば output=true で論理削除。
    /// 不一致なら何もしない（output は false のまま）。
    /// </summary>
    public override bool InPlaceUpdater(ref TKey key, ref TValue input, ref TValue value, ref bool output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
    {
        if (value.Equals(input))
        {
            output = true;
            rmwInfo.Action = RMWAction.ExpireAndStop;
            return false;
        }
        return true;
    }

    /// <summary>ReadOnly / Disk のレコードが存在し値が一致する場合のみコピー更新を許可。</summary>
    public override bool NeedCopyUpdate(ref TKey key, ref TValue input, ref TValue oldValue, ref bool output, ref RMWInfo rmwInfo) => oldValue.Equals(input);

    /// <summary>コピー更新時：output=true で論理削除。</summary>
    public override bool CopyUpdater(ref TKey key, ref TValue input, ref TValue oldValue, ref TValue newValue, ref bool output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
    {
        output = true;
        rmwInfo.Action = RMWAction.ExpireAndStop;
        return false;
    }
}
#endif
