using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Store;
namespace Seamless.Collections;

public abstract class UnmanagedCollection<TKey,TValue>:Collection<
    TKey,
    TValue,
    UnmanagedAdd<TKey,TValue>,
    UnmanagedDelete<TKey,TValue>,
    StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>,
    SpanByteAllocator<StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {

    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore) {
    }

    private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key) => UnmanagedSerializer<TKey>.Serialize(ref key);
    private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value) => UnmanagedSerializer<TValue>.Serialize(ref value);

    private protected override Status Read(TKey Key,out SpanByteAndMemory Output) {
        var Session = this.ClientSession;
        var KeyBytes = this.SerializeKey(ref Key);
        Output = default;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        return Pending(Session, Status, ref Output);
    }

    private protected override TValue ConvertOutput(ref SpanByteAndMemory output) =>
        UnmanagedSerializer<TValue>.Deserialize(GetReadOnlySpan(output));

    public override long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value) {
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
            return TryAddResult.LogFull;
        }
        var Session = this.ClientSession;
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey, ref FixedInput, ref Output);
        Status = Pending(Session, Status);
        return Status.NotFound ? TryAddResult.Success : TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var Output = default(SpanByteAndMemory);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(PinnedSpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status = Pending(DeleteClientSession,Status);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Status.Found;
    }
}
