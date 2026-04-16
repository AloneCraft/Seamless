using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Seamless.Collections;

public sealed partial class UnmanagedManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    /// <summary>
    /// todo 1.1.1 TKeyとTValueの片方が可変長でも可変長の扱いになっている。将来、TKeyの固定長が実装できた場合は書き直す
    /// </summary>
    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value) {
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var ValueBytes = TValueSerializer.Serialize(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            return TryAddResult.LogFull;

        var ClientSession = this.AddClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput = SpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = ClientSession.BasicContext.RMW(ref FixedKey,ref FixedInput,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);

        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
