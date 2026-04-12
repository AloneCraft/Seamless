using System.Collections;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using System.Diagnostics;
namespace Seamless.Collections;

public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey, 
    TValue,
    SpanByte,//TSessionKey
    SpanByte,//TSessionValue
    SpanByteAndMemory,//TReadOutput
    SpanByte,//TAddInput
    ManagedRead,
    ManagedAdd,
    ManagedDelete,
    ManagedStoreFunctions, 
    ManagedAllocator
>{
    public sealed partial class KeyIterator {
        private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value) =>
            TKeySerializer.Deserialize(key.AsReadOnlySpan());
    }
    internal sealed partial class ValueIterator {
        private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
            TValueSerializer.Deserialize(value.AsReadOnlySpan());
    }
    public sealed partial class KeyValueIterator {
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value) {
            var Key=TKeySerializer.Deserialize(key.AsReadOnlySpan());
            var Value=TValueSerializer.Deserialize(value.AsReadOnlySpan());
            return new(Key,Value);
        }
    }
    public partial struct Enumerator<TIterator,T>(
        ClientSession<
            SpanByte,//TKey
            SpanByte,//TValue
            EqualityEmpty,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            ManagedRead,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator
    ):IEnumerator<T>
        where TIterator : ScanIteratorFunctions<T> {
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
        public void Dispose(){}
    }
    public override bool Contains(TKey Key) {
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ReadClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.Read(ref FixedKey, ref Output);
        Status = Pending(Session, Status, ref Output);
        return Status.Found;
    }

    private protected override Status Read(TKey Key, out TValue Output) {
        var KeyBytes = this.SerializeKey(ref Key);
        var Session = this.ReadClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        SpanByteAndMemory value0 = default;
        var Status = Session.BasicContext.Read(ref FixedKey, ref value0);
        if (Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using (CompletedOutputs) {
                Debug.Assert(CompletedOutputs.Next());
                Status = CompletedOutputs.Current.Status;
                value0 = CompletedOutputs.Current.Output;
            }
        }
        if(Status.Found){
            Output=TValueSerializer.Deserialize(value0.AsReadOnlySpan());
        } else{
            //Unsafe.SkipInit(out Output);
            Output=default!;
        }
        return Status;
    }

    public override long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.AddClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }
    public sealed partial class CounterIterator : IScanIteratorFunctions<SpanByte, SpanByte> {
        public bool SingleReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult) {
            cursorRecordResult = CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult) {
            cursorRecordResult = CursorRecordResult.Accept;
            return true;
        }
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        if (this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Session = this.AddClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput = SpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = Session.BasicContext.RMW(ref FixedKey, ref FixedInput, ref Output, default(EqualityEmpty));
        if (Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using (CompletedOutputs) {
                Debug.Assert(CompletedOutputs.Next());
                Status = CompletedOutputs.Current.Status;
                Output = CompletedOutputs.Current.Output;
            }
        }
        if (Output) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }

    public override bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = DeleteClientSession.BasicContext.RMW(ref FixedKey, ref Input, ref Output, default(EqualityEmpty));
        if (Status.IsPending) {
            DeleteClientSession.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using (CompletedOutputs) {
                Debug.Assert(CompletedOutputs.Next());
                Status = CompletedOutputs.Current.Status;
                Output = CompletedOutputs.Current.Output;
            }
        }
        return Output;
    }
}
