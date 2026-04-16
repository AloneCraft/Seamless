using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Seamless.Collections;

public sealed partial class UnmanagedManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    /// <summary>
    /// todo 2.0beta3 TKeyとTValueの片方が可変長でも可変長の扱いになっている。将来、TKeyの固定長が実装できた場合は書き直す
    /// </summary>
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) {
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var ValueBytes = TValueSerializer.Serialize(ref value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            return TryAddResult.LogFull;

        var Session = this.AddClientSession;
        var Output = false;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Status = Session.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        Debug.Assert(Status.IsCompleted);

        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
