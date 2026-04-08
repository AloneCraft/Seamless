using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using System.Diagnostics;
using Tsavorite.core;

namespace Seamless.Collections;
using core_Empty = Tsavorite.core.Empty;
public abstract class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer> :
#if TSAVORITE2_0BETA3
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
    >
#elif TSAVORITE1_1_1
    Collection<
        TKey,
        TValue,
        SpanByte,
        SpanByte,
        SpanByte,
        SpanByteAndMemory,
        Add,
        Delete,
        StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>,
        SpanByteAllocator<StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>>
    >
#endif
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {

    private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key) => TKeySerializer.Serialize(ref key);
    private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value) => TValueSerializer.Serialize(ref value);

    protected ManagedCollection(ManagedCollectionStore<TKey, TValue> CollectionStore) : base(CollectionStore) { }
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
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
#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
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
#endif
    public override bool Remove(TKey key){
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
#if TSAVORITE2_0BETA3
        var Output = default(SpanByteAndMemory);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(PinnedSpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input, ref Output);
#elif TSAVORITE1_1_1
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(SpanByte);
        SpanByteAndMemory Output0 = default;
        var Status = DeleteClientSession.BasicContext.RMW(ref FixedKey, ref Input, ref Output0, default(core_Empty));
#endif
        Status = Pending(DeleteClientSession, Status);
        Debug.Assert(Status.Found != Status.NotFound);
        return Status.Found;
    }
}
