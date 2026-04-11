using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Store;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey,
    TValue,
    TKey,//TSessionKey
    TValue,//TSessionValue
    TValue,//TReadOutput
    TValue,//TAddInput
    UnmanagedRead<TKey,TValue>,
    UnmanagedAdd<TKey,TValue>,
    UnmanagedDelete<TKey,TValue>,
    StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>,
    BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {

    //protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
    //}

    //private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key) => UnmanagedSerializer<TKey>.Serialize(ref key);
    //private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value) => UnmanagedSerializer<TValue>.Serialize(ref value);

    private protected override Status Read(TKey Key,out TValue Output) {
        var Session = this.ReadClientSession;
        Output = default;
        var status = Session.BasicContext.Read(ref Key,ref Output);
        return Pending(Session,status,ref Output);
    }


    public override long GetLongCount() {
        var iterator = new CounterIterator<TKey,TValue>();
        var Cursor = 0L;
        this.ReadClientSession.IterateLookup(ref iterator,ref Cursor,resetCursor:false);
        return iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) {
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
            return TryAddResult.LogFull;
        }
        var Session = this.AddClientSession;
        var Output = false;
        var Status = Session.BasicContext.RMW(ref key,ref value,ref Output);
        Status = Pending(Session,Status);
        if(Status.NotFound)
            return TryAddResult.Success;
        else
            return TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key) {
        var Session = this.DeleteClientSession;
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = Session.BasicContext.RMW(ref key,ref Input,ref Output);
        Status = Pending(Session,Status,ref Output);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
}
