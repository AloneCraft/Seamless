using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// UnmanagedHashSet の 1.1.1 固有部分。IntersectWith/SymmetricExceptWith 実装とイテレータ。
/// </summary>
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private readonly ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        UnmanagedIntersectWith<
            TKey
        >,
        StoreFunctions<
            TKey,
            EqualityEmpty,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                EqualityEmpty
            >
        >,
        BlittableAllocator<
            TKey,
            EqualityEmpty,
            StoreFunctions<
                TKey
                ,EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >
        >
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        UnmanagedSymmetricExceptWith<
            TKey,
            EqualityEmpty
        >,
        StoreFunctions<
            TKey,
            EqualityEmpty,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                EqualityEmpty
            >
        >,
        BlittableAllocator<
            TKey,
            EqualityEmpty,
            StoreFunctions<
                TKey,
                EqualityEmpty,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    EqualityEmpty
                >
            >
        >
    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedIntersectWith<
                TKey
            >
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedSymmetricExceptWith<
                TKey,
                EqualityEmpty
            >
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadClientSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<KeyIterator,TKey>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private readonly struct IntersectWithImpl(
        UnmanagedHashSet<TKey> other,
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedIntersectWith<TKey>,
            StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>,
            BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>>
        > session
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(other.PrivateProtectedContainsKey(ref key)) {
            }
            else {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                var status = session.BasicContext.RMW(ref key,ref input,ref output);
                Pending(session,status,ref output);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    private readonly struct SymmetricExceptWithImpl(
        UnmanagedHashSet<TKey> Self,
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedSymmetricExceptWith<TKey,EqualityEmpty>,
            StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>,
            BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,EqualityEmpty>>>
        > session
    ):IScanIteratorFunctions<TKey,EqualityEmpty> {
        public bool SingleReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
            if(Self.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),1))
                throw Self.ThrowLogFull();
            var Input = default(EqualityEmpty);
            var Output = default(EqualityEmpty);
            var Status = session.BasicContext.RMW(ref key,ref Input,ref Output);
            Pending(session,Status,ref Output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key,ref EqualityEmpty value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.IntersectWith_ClientSession;
        var iterator = new IntersectWithImpl(other,session);
        var cursor = 0L;
        session.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.SymmetricExceptWith_ClientSession;
        var iterator = new SymmetricExceptWithImpl(this,session);
        var cursor = 0L;
        other.ReadClientSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        other.ReadClientSession.BasicContext.CompletePending(wait: true);
        session.BasicContext.CompletePending(wait: true);
    }
}
