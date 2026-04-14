using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
public partial class ManagedHashSet<TKey>
    where TKey : IEquatable<TKey> {
    private readonly ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        ManagedIntersectWith,
        ManagedStoreFunctions,
        ManagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        SpanByte,//TKey
        SpanByte,//TValue
        SpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,
        ManagedStoreFunctions,
        ManagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<
            EqualityEmpty,//TKey

            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            ManagedIntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            SpanByte,//TKey

            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadClientSession,new());
    private readonly struct IntersectWithIterator(
        ManagedHashSet<TKey> other,
        ClientSession<
            SpanByte,
            SpanByte,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            ManagedIntersectWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > session
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(other.PrivateProtectedContainsKey(ref key)) {
            }
            else {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                var status = session.BasicContext.RMW(ref key,ref input,ref output);
                if(status.IsPending) {
                    session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
                    using(completedOutputs) {
                        Debug.Assert(completedOutputs.Next());
                    }
                }
                else {
                }
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
           => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private readonly struct SymmetricExceptWithIterator(
        ManagedHashSet<TKey> self,
        ClientSession<
            SpanByte,
            SpanByte,
            SpanByte,
            SpanByteAndMemory,
            EqualityEmpty,
            SymmetricExceptWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > session
    ):IScanIteratorFunctions<SpanByte,SpanByte> {
        public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(self.WouldOverflowMutable(key.Length,1))
                throw self.ThrowLogFull();
            var input = default(SpanByte);
            SpanByteAndMemory output = default;
            var status = session.BasicContext.RMW(ref key,ref input,ref output);
            if(status.IsPending) {
                session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
                using(completedOutputs) {
                    Debug.Assert(completedOutputs.Next());
                }
            }
            else {
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
           => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var session = this.IntersectWith_ClientSession;
        var iterator = new IntersectWithIterator(other,session);
        var cursor = 0L;
        session.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.SymmetricExceptWith_ClientSession;
        var iterator = new SymmetricExceptWithIterator(this,session);
        var cursor = 0L;
        other.ReadClientSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }
}
