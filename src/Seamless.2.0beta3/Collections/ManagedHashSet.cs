using Seamless.SessionFunctions;
using Seamless.Stores;

using System.Diagnostics;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class ManagedHashSet<TKey>where TKey : IEquatable<TKey>{
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions


        StoreFunctions<
            SpanByteComparer,//TKeyComparer
            DefaultRecordDisposer//TRecordDisposer
        >,//TStoreFunctions


        SpanByteAllocator<
            StoreFunctions<
                SpanByteComparer,//TKeyComparer
                DefaultRecordDisposer//TRecordDisposer
            >
        >
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        PinnedSpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        StoreFunctions<
            SpanByteComparer,//TKeyComparer
            DefaultRecordDisposer//TRecordDisposer


        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByteComparer,//TKeyComparer
                DefaultRecordDisposer//TRecordDisposer


            >
        >
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession      =Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            PinnedSpanByte,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator()=>new (this.ReadClientSession,new());
    private readonly struct IntersectWithIterator(
        ManagedHashSet<TKey> self,
        ManagedHashSet<TKey> other,
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith,
            StoreFunctions<
                SpanByteComparer,
                DefaultRecordDisposer
            >,
            SpanByteAllocator<
                StoreFunctions<
                    SpanByteComparer,
                    DefaultRecordDisposer
                >
            >
        > session
    ):IScanIteratorFunctions{
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord:ISourceLogRecord{
            var keyBytes=logRecord.Key;
            if(!other.PrivateProtectedContainsKey(keyBytes)){
                if(self.WouldOverflowMutable(keyBytes.Length,1))
                    throw self.ThrowLogFull();
                var input=default(EqualityEmpty);
                var output=default(EqualityEmpty);
                var status=session.BasicContext.RMW((FixedSpanByteKey)keyBytes,ref input,ref output);
                if(status.IsPending){
                    session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                    using(completedOutputs){
                        Debug.Assert(completedOutputs.Next());
                    }
                }
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress)=>true;
        public void OnException(Exception exception,long numberOfRecords){
        }
        public void OnStop(bool completed,long numberOfRecords){
        }
    }
    private readonly struct SymmetricExceptWithIterator(
        ClientSession<
            FixedSpanByteKey,
            PinnedSpanByte,
            SpanByteAndMemory,
            EqualityEmpty,
            SymmetricExceptWith,
            StoreFunctions<
                SpanByteComparer,
                DefaultRecordDisposer
            >,
            SpanByteAllocator<
                StoreFunctions<
                    SpanByteComparer,
                    DefaultRecordDisposer
                >
            >
        > session
    ):IScanIteratorFunctions{
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord:ISourceLogRecord{
            var input=default(PinnedSpanByte);
            SpanByteAndMemory output=default;
            var status=session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key,ref input,ref output);
            if(status.IsPending){
                session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                using(completedOutputs){
                    Debug.Assert(completedOutputs.Next());
                }
            } else{
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress)=>true;
        public void OnException(Exception exception,long numberOfRecords){
        }
        public void OnStop(bool completed,long numberOfRecords){
        }
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var session=this.IntersectWith_ClientSession;
        var iterator=new IntersectWithIterator(this,other,session);
        var cursor=0L;
        session.IterateLookup(ref iterator,ref cursor,resetCursor:false);
        session.BasicContext.CompletePending(wait:true);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var session=this.SymmetricExceptWith_ClientSession;
        var iterator=new SymmetricExceptWithIterator(session);
        var cursor=0L;
        other.ReadClientSession.IterateLookup(ref iterator,ref cursor,resetCursor:false);
        session.BasicContext.CompletePending(wait:true);
    }
}
