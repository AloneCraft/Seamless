using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
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
    where TValueSerializer:ISerializer<TValue>{
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator<TIterator,T>(
        ClientSession<
            TKey,
            TValue,
            EqualityEmpty,
            TValue,
            EqualityEmpty,
            UnmanagedRead<TKey,TValue>,
            StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>,
            BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,TValue>>>> session,
        TIterator iterator):IEnumerator<T>
        where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
        private TIterator Iterator = iterator;
        private long Cursor = 0, Count;
        private int Index = -1;
        private bool _done;
        public T Current => this.Iterator.Chunk[this.Index];
        object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(++this.Index<this.Count) {
                return true;
            } 
            if(this._done) {
                this.Index = -1;
                return false;
            }
            this.Iterator.Count = 0;
            var result = session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
            var count = this.Iterator.Count;
            this.Count = count;
            if(!result)
                this._done = true;
            if(count>0) {
                this.Index = 0;
                return true;
            }
            this.Index = -1;
            return false;
        }
        public void Reset() {
            this.Cursor = 0;
            this.Count = 0;
            this.Index = -1;
            this._done = false;
        }
        public void Dispose() {
        }
    }

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
