using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [StructLayout(LayoutKind.Auto)]
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
        > session,
        TIterator iterator
    ):IEnumerator<T>
        where TIterator:ScanIteratorFunctions<
            T,
            TKey,
            TValue
        >{
        private TIterator Iterator=iterator;
        private long Cursor=0,Count;
        private int Index=-1;
        private bool Done;
        public T Current=>this.Iterator.Chunk[this.Index];
        object? IEnumerator.Current=>this.Current;
        public bool MoveNext(){
            if(++this.Index<this.Count){
                return true;
            }
            if(this.Done){
                this.Index=-1;
                return false;
            }
            this.Iterator.Count=0;
            var result=session.IterateLookup(ref this.Iterator,ref this.Cursor,resetCursor:false);
            var count=this.Iterator.Count;
            this.Count=count;
            if(!result)
                this.Done=true;
            if(count>0){
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
        public void Dispose(){}
    }
    public bool Contains(TKey Key)=>this.PrivateProtectedContainsKey(ref Key);
    /// <summary>
    /// キーの存在を判定する。
    /// ContainsDelete の Reader が Read 時に output=true を返すことで存在判定を行う。
    /// </summary>
    public bool ContainsKey(TKey Key)=>this.PrivateProtectedContainsKey(ref Key);




    private protected override Status Read(TKey Key,out TValue Output){
        var Session=this.ReadClientSession;
        Output=default;
        var status=Session.BasicContext.Read(ref Key,ref Output);
        return Pending(Session,status,ref Output);
    }

    //public sealed class CounterIterator:IScanIteratorFunctions<TKey,TValue>{
    //    public bool SingleReader(ref TKey key,ref TValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult){
    //        cursorRecordResult=CursorRecordResult.Accept;
    //        return true;
    //    }
    //    public bool ConcurrentReader(ref TKey key,ref TValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult){
    //        cursorRecordResult=CursorRecordResult.Accept;
    //        return true;
    //    }
    //    public long Counter;
    //    public bool OnStart(long beginAddress,long endAddress){
    //        return true;
    //    }
    //    public void OnException(Exception exception,long numberOfRecords){
    //    }
    //    public void OnStop(bool completed,long numberOfRecords) {
    //        this.Counter=numberOfRecords;
    //    }
    //}
    public sealed class KeyIterator:ScanIteratorFunctions<TKey,TKey,TValue>{
        private protected override TKey Deserialize(ref TKey key,ref TValue value)=>key;
    }
    internal sealed class ValueIterator:ScanIteratorFunctions<TValue,TKey,TValue>{
        private protected override TValue Deserialize(ref TKey key,ref TValue value)=>value;
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>,TKey,TValue>{
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value)=>new(key,value);
    }

    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value){
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())){
            return TryAddResult.LogFull;
        }
        var Session=this.AddClientSession;
        var Output=false;
        var Status=Session.BasicContext.RMW(ref key,ref value,ref Output);
        Status=Pending(Session,Status);
        if(Status.NotFound)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }

    public bool Remove(TKey key){
        var Session=this.ContainsDeleteClientSession;
        var Input=default(EqualityEmpty);
        var Output=false;
        var Status=Session.BasicContext.RMW(ref key,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
}
