using System.Diagnostics;
using System.Runtime.CompilerServices;
using Garnet.common;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using Tsavorite.core;

namespace Seamless.Collections;
#if TSAVORITE2_0BETA3
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
#elif TSAVORITE1_1_1
public abstract class UnmanagedCollection<TKey,TValue>:Collection<
    TKey,
    TValue,
    TKey,
    TValue,
    TValue,
    TValue,
    UnmanagedAdd<TKey,TValue>,
    UnmanagedDelete<TKey,TValue>,
    StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
    BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {

    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
    }

    private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key) => UnmanagedSerializer<TKey>.Serialize(ref key);
    private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value) => UnmanagedSerializer<TValue>.Serialize(ref value);

    private protected override Status Read(TKey key,out TValue output) {
        var session = this.ClientSession;
        output = default;
        var status = session.BasicContext.Read(ref key,ref output);
        return Pending(session,status,ref output);
    }

    private protected override TValue ConvertOutput(ref TValue output) => output;

    public override long GetLongCount() {
        var iterator = new UnmanagedCounterIterator<TKey,TValue>();
        long cursor = 0;
        this.ClientSession.IterateLookup(ref iterator,ref cursor,resetCursor:false);
        return iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) {
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
            return TryAddResult.LogFull;
        }
        var session = this.ClientSession;
        var output = default(TValue);
        var status = session.BasicContext.RMW(ref key,ref value,ref output,default(core_Empty));
        status = Pending(session,status);
        return status.NotFound ? TryAddResult.Success : TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key) {
        var session = this.DeleteClientSession;
        var input = default(TValue);
        var output = default(TValue);
        var status = session.BasicContext.RMW(ref key,ref input,ref output,default(core_Empty));
        status = Pending(session,status);
        Debug.Assert(status.Found!=status.NotFound);
        return status.Found;
    }
}
#endif
