using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey,
    TValue,


    TValue,//TReadOutput
    TValue,//TAddInput
    UnmanagedRead<
        TKey,
        TValue
    >,
    UnmanagedAdd<
        TKey,
        TValue
    >,
    ContainsDelete,



    UnmanagedStoreFunctions,










    UnmanagedAllocator














>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    public sealed partial class KeyIterator:ScanIteratorFunctions<TKey>{
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    internal sealed partial class ValueIterator:ScanIteratorFunctions<TValue>{
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed partial class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>{
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)=>
            new(
                TKeySerializer.Deserialize(logRecord.Key),
                TValueSerializer.Deserialize(logRecord.ValueSpan)
            );
    }
    public struct Enumerator<TIterator,T>(
        ClientSession<
            FixedSpanByteKey,//TKey

            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            UnmanagedRead<
                TKey,
                TValue
            >,//TReadFunctions










            UnmanagedStoreFunctions,//TStoreFunctions














            UnmanagedAllocator//TAllocator
        > session,
        TIterator Iterator
    ):IEnumerator<T>
        where TIterator:ScanIteratorFunctions<
            T


        >{
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
            var r=session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor:false);
            //1.0と違い同期的に完了
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
    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
    public bool ContainsKey(TKey Key){
        //
        //
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool Contains(TKey Key)=>this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    private protected override Status Read(TKey Key,out TValue Output){
        var ClientSession=this.ReadClientSession;
        
        
        Output=default;
        var Status=ClientSession.BasicContext.Read((FixedSpanByteKey)TKeySerializer.Serialize(ref Key),ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
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
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value){
        var KeyBytes=TKeySerializer.Serialize(ref key);
        if(this.WouldOverflowMutable(KeyBytes.Length,Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ClientSession=this.AddClientSession;
        var Output=false;
        var Status=ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref value,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        Debug.Assert(Status.NotFound==Output);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
