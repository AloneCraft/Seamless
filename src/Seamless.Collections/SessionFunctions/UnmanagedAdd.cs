#if TSAVORITE1_1_1
using Tsavorite.core;

namespace Seamless.SessionFunctions;

using Empty = Seamless.Collections.Empty;

/// <summary>
/// RMW で「存在しなければ追加、存在すれば何もしない」を行うセッション関数。
/// Status.NotFound → 追加成功、Status.Found → キー既存。
/// </summary>
public class UnmanagedAdd<TKey, TValue> : SessionFunctionsBase<TKey, TValue, TValue, TValue, Empty>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    public override bool ConcurrentReader(ref TKey key, ref TValue input, ref TValue value, ref TValue dst, ref ReadInfo readInfo, ref RecordInfo recordInfo)
    {
        dst = value;
        return true;
    }

    public override bool SingleReader(ref TKey key, ref TValue input, ref TValue value, ref TValue dst, ref ReadInfo readInfo)
    {
        dst = value;
        return true;
    }

    public override bool InitialUpdater(ref TKey key, ref TValue input, ref TValue value, ref TValue output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
    {
        value = input;
        return true;
    }

    /// <summary>既存キーの場合は何もしない（Foundを返す）。</summary>
    public override bool InPlaceUpdater(ref TKey key, ref TValue input, ref TValue value, ref TValue output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
        => true;

    public override bool NeedCopyUpdate(ref TKey key, ref TValue input, ref TValue oldValue, ref TValue output, ref RMWInfo rmwInfo) => false;

    public override bool CopyUpdater(ref TKey key, ref TValue input, ref TValue oldValue, ref TValue newValue, ref TValue output, ref RMWInfo rmwInfo, ref RecordInfo recordInfo)
    {
        newValue = oldValue;
        return true;
    }
}
#endif
