using System.Collections;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
namespace Seamless.Collections;

public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue


    SpanByteAndMemory,//TReadOutput
    PinnedSpanByte,//TAddInput
    ManagedRead,//TReadFunctions
    ManagedAdd,//TAddFunctions
    ContainsDelete,//TDeleteFunctions
    ManagedStoreFunctions,//TStoreFunctions
    ManagedAllocator//TAllocator
>{
    public sealed partial class KeyIterator{
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    internal sealed partial class ValueIterator{
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed partial class KeyValueIterator{
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord){
            var Key=TKeySerializer.Deserialize(logRecord.Key);
            var Value=TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public partial struct Enumerator<TIterator,T>(
        ClientSession<
            FixedSpanByteKey,//TKey

            EqualityEmpty,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            ManagedRead,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator
    ):IEnumerator<T>
    where TIterator:ScanIteratorFunctions<T>{
        private TIterator Iterator=Iterator;
        private long Cursor=0,Count;
        private int Index=-1;
        private bool Done;
        public T Current=>this.Iterator.Chunk[this.Index];
        object? IEnumerator.Current=>this.Current;
        public bool MoveNext(){
            if(++this.Index<this.Count) return true;
            if(this.Done){
                this.Index=-1;
                return false;
            }
            this.Iterator.Count=0;
            var r=Session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor:false);
            var Count=this.Iterator.Count;
            this.Count=Count;
            if(!r)
                this.Done=true;
            if(Count>0){
                this.Index=0;
                return true;
            }
            this.Index=-1;
            return false;
        }
        public void Reset(){
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
        }
        public void Dispose(){
        }
    }
    public bool Contains(TKey Key){
        
        
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    private protected override Status Read(TKey Key,out TValue Value){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        var Session=this.ReadClientSession;
        var FixedKey=(FixedSpanByteKey)KeyBytes;
        var Output=default(SpanByteAndMemory);
        var Status=Session.BasicContext.Read(FixedKey,ref Output);
        Status=Pending(Session,Status,ref Output);
        if(Status.Found){
            Value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
        }
        else{
            Value=default!;
        }
        return Status;
    }
    public bool Remove(TKey key){
        var ClientSession=this.ContainsDeleteClientSession;
        var KeyBytes=TKeySerializer.Serialize(ref key);

        var Input=default(EqualityEmpty);
        var Output=false;
        var Status=ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        var ValueBytes=TValueSerializer.Serialize(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            return TryAddResult.LogFull;
        var Session=this.AddClientSession;
        var FixedKey=(FixedSpanByteKey)KeyBytes;
        var FixedInput=PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output=false;
        var Status=Session.BasicContext.RMW(FixedKey,ref FixedInput,ref Output);
        Status=Pending(Session,Status,ref Output);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
