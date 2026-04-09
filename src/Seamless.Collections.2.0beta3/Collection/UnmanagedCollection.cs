using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

namespace Seamless.Collections;

public abstract class UnmanagedCollection<TKey,TValue>:Collection<
    TKey,
    TValue,
    PinnedSpanByte,
    SpanByteAndMemory,
    PinnedSpanByte,
    SpanByteAndMemory,
    Add,
    Delete,
    StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,
    SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {

    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
        BlittableAllocator<TKey,TValue,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>.AssertBlittable();
    }

    private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key) => UnmanagedSerializer<TKey>.Serialize(ref key);
    private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value) => UnmanagedSerializer<TValue>.Serialize(ref value);

    public override bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var Output = default(SpanByteAndMemory);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = default(PinnedSpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status = Pending(DeleteClientSession,Status);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Status.Found;
    }
}
