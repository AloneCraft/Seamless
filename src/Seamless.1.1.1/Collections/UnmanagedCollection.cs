using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>:Collection<
    TKey,
    TValue,
    TKey,//TSessionKey
    TValue,//TSessionValue
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
    UnmanagedDelete<
        TKey,
        TValue
    >,
    StoreFunctions<
        TKey,
        TValue,
        UnmanagedKeyComparer<
            TKey
        >,
        DefaultRecordDisposer<
            TKey,
            TValue
        >
    >,
    BlittableAllocator<
        TKey,
        TValue,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                TValue
            >
        >
    >
>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    public sealed partial class KeyIterator:ScanIteratorFunctions<TKey,TKey,TValue>{
        private protected override TKey Deserialize(ref TKey key,ref TValue value)=>
            key;
    }
    internal sealed partial class ValueIterator:ScanIteratorFunctions<TValue,TKey,TValue>{
        private protected override TValue Deserialize(ref TKey key,ref TValue value)=>
            value;
    }
    public sealed partial class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>,TKey,TValue>{
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value)=>
            new(
                key,
                value
            );
    }
    public struct Enumerator<TIterator,T>(
        ClientSession<
            TKey,//TKey
            TValue,//TValue
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            UnmanagedRead<
                TKey,
                TValue
            >,//TReadFunctions
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    TValue
                >
            >,//TStoreFunctions
            BlittableAllocator<
                TKey,
                TValue,
                StoreFunctions<
                    TKey,
                    TValue,
                    UnmanagedKeyComparer<
                        TKey
                    >,
                    DefaultRecordDisposer<
                        TKey,
                        TValue
                    >
                >
            >//TAllocator
        > Session,
        TIterator Iterator
    ):IEnumerator<T>
        where TIterator:ScanIteratorFunctions<
            T,
            TKey,
            TValue
        >{
        private long Cursor=0,Count;
        private int Index=-1;
        private bool Done;
        public T Current=>Iterator.Chunk[this.Index];
        object? IEnumerator.Current=>this.Current;
        public bool MoveNext(){
            if(++this.Index<this.Count){
                return true;
            }
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
        public void Dispose(){
        }
    }
    /// <summary>
    /// キーの存在を判定する。
    /// </summary>
    public bool ContainsKey(TKey Key)=>this.PrivateProtectedContainsKey(
        ref Key
    );
    public bool Contains(TKey Key)=>this.PrivateProtectedContainsKey(
        ref Key
    );
    private protected override Status Read(TKey Key,out TValue Output){
        var ClientSession=this.ReadClientSession;
        Output=default;
        var Status=ClientSession.BasicContext.Read(ref Key,ref Output);
        return Pending(ClientSession,Status,ref Output);
    }
    public bool Remove(TKey key){
        var ClientSession=this.ContainsDeleteClientSession;

        var Input=default(EqualityEmpty);
        var Output=false;
        var Status=ClientSession.BasicContext.RMW(ref key,ref Input,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value){
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;

        var ClientSession=this.AddClientSession;
        var Output=false;
        var Status=ClientSession.BasicContext.RMW(ref key,ref value,ref Output);
        Status=Pending(ClientSession,Status);
        Debug.Assert(Status.NotFound==Output);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
