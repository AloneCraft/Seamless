using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Store;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    //:Collection<
    //TKey,
    //TValue,
    //UnmanagedRead<TKey,TValue>,
    //UnmanagedAdd<TKey,TValue>,
    //UnmanagedDelete<TKey,TValue>,
    //StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>,
    //SpanByteAllocator<StoreFunctions<UnmanagedKeyComparer<TKey>,SpanByteRecordDisposer>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {

    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore) {
    }

    private protected ReadOnlySpan<byte> SerializeKey(ref TKey key) => TKeySerializer.Serialize(ref key);
    private protected ReadOnlySpan<byte> SerializeValue(ref TValue value) => TValueSerializer.Serialize(ref value);



    //private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value) {
    //    if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
    //        return TryAddResult.LogFull;
    //    }
    //    var Session = this.ClientSession;
    //    var KeyBytes = this.SerializeKey(ref Key);
    //    var ValueBytes = this.SerializeValue(ref Value);
    //    var FixedKey = (FixedSpanByteKey)KeyBytes;
    //    var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
    //    SpanByteAndMemory Output = default;
    //    var Status = Session.BasicContext.RMW(FixedKey, ref FixedInput, ref Output);
    //    Status = Pending(Session, Status, ref Output);
    //    return Output.Length > 0 ? TryAddResult.Success : TryAddResult.KeyAlreadyExists;
    //}

    //public override bool Remove(TKey key) {
    //    var DeleteClientSession = this.DeleteClientSession;
    //    var KeyBytes = this.SerializeKey(ref key);
    //    var FixedKey = (FixedSpanByteKey)KeyBytes;
    //    var Input = default(EqualityEmpty);
    //    var Output = false;
    //    var Status = DeleteClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
    //    Status = Pending(DeleteClientSession,Status,ref Output);
    //    return Output;
    //}
}
