using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using System.Diagnostics;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer> :
    Collection<
        TKey,
        TValue,
        PinnedSpanByte,
        SpanByteAndMemory,
        PinnedSpanByte,
        SpanByteAndMemory,
        Add,
        Delete,
        StoreFunctions<SpanByteComparer, DefaultRecordDisposer>,
        SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>
    > {
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
