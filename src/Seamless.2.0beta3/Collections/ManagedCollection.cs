using System.Collections;
using System.Diagnostics;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
using System.Runtime.InteropServices;
namespace Seamless.Collections;

public abstract class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue


    SpanByteAndMemory,//TReadOutput
    PinnedSpanByte,//TAddInput
    ManagedRead,//TReadFunctions
    ManagedAdd,//TAddFunctions
    ContainsDelete,//TDeleteFunctions
    ManagedStoreFunctions,//TStoreFunctions
    ManagedAllocator//TAllocator
>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions<TKey> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    internal sealed class ValueIterator:ScanIteratorFunctions<TValue> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord){
            var Key=TKeySerializer.Deserialize(logRecord.Key);
            var Value=TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator<TIterator, T>(
        ClientSession<
            FixedSpanByteKey,//TKey
                             //
            EqualityEmpty,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            ManagedRead,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator
    ):IEnumerator<T>
    where TIterator:ScanIteratorFunctions<T> {
        private long Cursor=0, Count;
        private int Index=-1;
        private bool Done;
        public T Current => Iterator.Chunk[this.Index];
        object? IEnumerator.Current => this.Current;
        public bool MoveNext(){
            if(++this.Index<this.Count) return true;
            if(this.Done){
                this.Index=-1;
                return false;
            }
            Iterator.Count=0;
            var r=Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor: false);
            var Count=Iterator.Count;
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
    private protected ManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
    public bool Contains(TKey Key){
        //
        //
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool ContainsKey(TKey Key) =>this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    private protected override Status Read(TKey Key,out TValue Value){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        var Session=this.ReadClientSession;
        var FixedKey=(FixedSpanByteKey)KeyBytes;
        var Output=default(SpanByteAndMemory);
        var Status=Session.BasicContext.Read(FixedKey,ref Output);
        Status=Pending(Session,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Status.Found){
            Value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
        }
        else {
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
        Debug.Assert(Status.IsCompleted);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value){
        var KeyBytes=TKeySerializer.Serialize(ref key);
        var ValueBytes=TValueSerializer.Serialize(ref value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            return TryAddResult.LogFull;
        var ClientSession=this.AddClientSession;
        var FixedKey=(FixedSpanByteKey)KeyBytes;
        var FixedInput=PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output=false;
        var Status=ClientSession.BasicContext.RMW(FixedKey,ref FixedInput,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
