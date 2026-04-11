using System.Collections;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey, 
    TValue,
    SpanByteAndMemory,
    PinnedSpanByte, 
    ManagedRead,
    ManagedAdd,
    ContainsDelete,
    ManagedStoreFunctions, 
    ManagedAllocator
>{
    //public partial struct Enumerator<TIterator,T>(
    //    ClientSession<
    //        FixedSpanByteKey,//TKey
    //        EqualityEmpty,//TInput
    //        SpanByteAndMemory,//TOutput
    //        EqualityEmpty,//TContext
    //        ManagedRead,
    //        ManagedStoreFunctions,
    //        ManagedAllocator
    //    > Session,
    //    TIterator Iterator
    //):IEnumerator<T>where TIterator : ScanIteratorFunctions<T> {
    //    private TIterator Iterator = Iterator;
    //    private long Cursor = 0, Count;
    //    private int Index = -1;
    //    private bool _done;
    //    public T Current => this.Iterator.Chunk[this.Index];
    //    object? IEnumerator.Current => this.Current;
    //    public bool MoveNext() {
    //        if(++this.Index<this.Count) return true;
    //        if(this._done) {
    //            this.Index = -1;
    //            return false;
    //        }
    //        this.Iterator.Count = 0;
    //        var r = Session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
    //        var Count = this.Iterator.Count;
    //        this.Count=Count;
    //        if(!r)
    //            this._done = true;
    //        if(Count>0) {
    //            this.Index=0;
    //            return true;
    //        }
    //        this.Index=-1;
    //        return false;
    //    }
    //    public void Reset() {
    //        this.Cursor=0;
    //        this.Count=0;
    //        this.Index=-1;
    //        this._done = false;
    //    }
    //    public void Dispose() {
    //    }
    //}
    public partial struct Enumerator<TIterator,T>(ClientSession<FixedSpanByteKey,EqualityEmpty,SpanByteAndMemory,EqualityEmpty,ManagedRead,ManagedStoreFunctions,ManagedAllocator> Session,TIterator Iterator):IEnumerator<T>where TIterator : ScanIteratorFunctions<T> {
        private TIterator Iterator = Iterator;
        private long Cursor = 0, Count;
        private int Index = -1;
        private bool _done;
        public T Current => this.Iterator.Chunk[this.Index];
        object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(++this.Index<this.Count) return true;
            if(this._done) {
                this.Index = -1;
                return false;
            }
            this.Iterator.Count = 0;
            var r = Session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor: false);
            var Count = this.Iterator.Count;
            this.Count=Count;
            if(!r)
                this._done = true;
            if(Count>0) {
                this.Index=0;
                return true;
            }
            this.Index=-1;
            return false;
        }
        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this._done = false;
        }
        public void Dispose() {
        }
    }
    public override bool Contains(TKey Key) {
        var KeyBytes = this.SerializeKey(ref Key);
        return this.ContainsKey(KeyBytes);
    }

    /// <summary>
    /// シリアライズ済みキーバイト列で直接存在判定する。
    /// </summary>
    internal bool ContainsKey(ReadOnlySpan<byte> KeyBytes) {
        var Session = this.DeleteClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = false;
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        Status = Pending(Session, Status, ref Output);
        return Status.Found;
    }
    private protected override Status Read(TKey Key,out TValue Value){
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ReadClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        Status=Pending(Session, Status, ref Output);
        if(Status.Found){
            Value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
        } else{
            //Unsafe.SkipInit(out Value);
            Value =default!;
        }
        return Status;
    }

    //private protected override TValue ConvertOutput(ref TValue output) => output;

    public override long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ReadClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Session = this.AddClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = Session.BasicContext.RMW(FixedKey, ref FixedInput, ref Output);
        Status = Pending(Session, Status, ref Output);
        return Output ? TryAddResult.Success : TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key){
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input, ref Output);
        Status = Pending(DeleteClientSession, Status, ref Output);
        return Output;
    }

    }
