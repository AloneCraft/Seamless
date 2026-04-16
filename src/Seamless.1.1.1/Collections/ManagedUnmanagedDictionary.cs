using System.Diagnostics;
using System.Runtime.CompilerServices;
namespace Seamless.Collections;

public sealed partial class ManagedUnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> {
    /// <summary>
    /// TValue のサイズは compile-time 定数。
    /// オーバーフロー時に値シリアライズをスキップすることで無駄な作業を省く。
    /// </summary>
    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        if(this.WouldOverflowMutable(KeyBytes.Length,Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ValueBytes=TValueSerializer.Serialize(ref Value);
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
