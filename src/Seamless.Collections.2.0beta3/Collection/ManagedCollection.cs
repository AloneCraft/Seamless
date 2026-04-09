using System.Diagnostics;
using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer> {
    private protected override Status Read(TKey Key,out SpanByteAndMemory Output){
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        Output = default;
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        return Pending(Session, Status, ref Output);
    }

    private protected override TValue ConvertOutput(ref SpanByteAndMemory output) =>
        TValueSerializer.Deserialize(GetReadOnlySpan(output));

    public override long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Session = this.ClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey, ref FixedInput, ref Output);
        Status = Pending(Session, Status);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key){
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var Output = default(SpanByteAndMemory);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(PinnedSpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input, ref Output);
        Status = Pending(DeleteClientSession, Status);
        Debug.Assert(Status.Found != Status.NotFound);
        return Status.Found;
    }
}
