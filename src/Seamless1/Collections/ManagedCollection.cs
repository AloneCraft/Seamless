using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
public abstract class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue
    SpanByte,//TSessionKey
    SpanByte,//TSessionValue
    SpanByteAndMemory,//TReadOutput
    SpanByte,//TAddInput
    ManagedRead,//TReadFunctions
    ManagedAdd,//TAddFunctions
    ManagedContainsDelete,//TContainsDeleteFunction
    ManagedStoreFunctions,//TStoreFunctions
    ManagedAllocator//TAllocator
>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions<TKey,SpanByte,SpanByte> {
        private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value)=>
            TKeySerializer.Deserialize(key.AsReadOnlySpan());
    }
    internal sealed class ValueIterator:ScanIteratorFunctions<TValue,SpanByte,SpanByte> {
        private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value)=>
            TValueSerializer.Deserialize(value.AsReadOnlySpan());
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>,SpanByte,SpanByte> {
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref SpanByte key,ref SpanByte value){
            var Key=TKeySerializer.Deserialize(key.AsReadOnlySpan());
            var Value=TValueSerializer.Deserialize(value.AsReadOnlySpan());
            return new(Key,Value);
        }
    }
    public struct Enumerator<TIterator, T>(
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
        where TIterator:ScanIteratorFunctions<
            T,
            SpanByte,
            SpanByte
        >
    {
        private long Cursor=0,Count;
        private int Index=-1;
        private bool Done;
        public readonly T Current=>Iterator.Chunk[this.Index];
        readonly object? IEnumerator.Current=>this.Current;
        public bool MoveNext(){
            if(++this.Index<this.Count)
                return true;
            if(this.Done){
                this.Index=-1;
                return false;
            }
            Iterator.Count=0;
            var r=Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor:false);
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
        public readonly void Dispose(){
        }
    }
    private protected ManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
    public bool ContainsKey(TKey Key){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        var SessionKey=SpanByte.FromPinnedSpan(KeyBytes);
        return this.PrivateProtectedContainsKey(ref SessionKey);
    }
    public bool Contains(TKey Key)=>this.ContainsKey(Key);
    private protected override Status Read(TKey Key,out TValue Value){
        var ClientSession=this.ReadClientSession;
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
        var Output=default(SpanByteAndMemory);
        var Status=ClientSession.BasicContext.Read(ref FixedKey,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Status.Found){
            Value=TValueSerializer.Deserialize(Output.AsReadOnlySpan());
        }else{
            Value=default!;
        }
        return Status;
    }
    public bool Remove(TKey key){
        var ClientSession=this.ContainsDeleteClientSession;
        var KeyBytes=TKeySerializer.Serialize(ref key);
        var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
        var Input=default(EqualityEmpty);
        var Output=false;
        var Status=ClientSession.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        var ValueBytes=TValueSerializer.Serialize(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            return TryAddResult.LogFull;
        var ClientSession=this.AddClientSession;
        var FixedKey=SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput=SpanByte.FromPinnedSpan(ValueBytes);
        var Output=false;
        var Status=ClientSession.BasicContext.RMW(ref FixedKey,ref FixedInput,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
