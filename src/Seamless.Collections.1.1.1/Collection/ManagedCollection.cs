using Seamless.ScanIteratorFunctions;
using System.Diagnostics;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer> {
    private protected override Status Read(TKey Key,out SpanByteAndMemory Output){
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        SpanByteAndMemory value0 = default;
        var Status = Session.BasicContext.Read(ref FixedKey, ref value0);
        var Result = Pending(Session, Status, ref value0);
        Output = value0;
        return Result;
    }

    private protected override TValue ConvertOutput(ref SpanByteAndMemory output) =>
        TValueSerializer.Deserialize(output.AsReadOnlySpan());

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
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput = SpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(ref FixedKey, ref FixedInput, ref Output, default(core_Empty));
        Status = Pending(this.ClientSession, Status);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key){
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(SpanByte);
        SpanByteAndMemory Output0 = default;
        var Status = DeleteClientSession.BasicContext.RMW(ref FixedKey, ref Input, ref Output0, default(core_Empty));
        Status = Pending(DeleteClientSession, Status);
        Debug.Assert(Status.Found != Status.NotFound);
        return Status.Found;
    }
}
