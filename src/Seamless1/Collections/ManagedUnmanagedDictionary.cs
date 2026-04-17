using System.Diagnostics;
using System.Runtime.CompilerServices;
//
//
namespace Seamless.Collections;
/// <summary>
/// todo 1.1.1 TKeyとTValueの片方が可変長でも可変長の扱いになっている。将来、TKeyの固定長が実装できた場合は書き直す
/// </summary>
public sealed partial class ManagedUnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> {





















    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value){
        var KeyBytes=TKeySerializer.Serialize(ref key);
        if(this.WouldOverflowMutable(KeyBytes.Length,4+Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ValueBytes=TValueSerializer.Serialize(ref value);
        var ClientSession=this.AddClientSession;
        var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput=SpanByte.FromPinnedSpan(ValueBytes);
        var Output=false;
        var Status=ClientSession.BasicContext.RMW(ref FixedKey,ref FixedInput,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
