using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// UnmanagedHashSet の 1.1.1 固有部分。IntersectWith/SymmetricExceptWith 実装とイテレータ。
/// </summary>
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey>
{
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
                , EqualityEmpty,
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
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey, EqualityEmpty> CollectionStore) : base(CollectionStore)
    {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession = Store.NewSession<
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedIntersectWith<
                TKey
            >
        >(new());
        this.SymmetricExceptWith_ClientSession = Store.NewSession<
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
    > GetEnumerator() => new(this.ReadClientSession, new());
    private readonly struct IntersectWithIterator(
        UnmanagedHashSet<TKey> other,
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedIntersectWith<TKey>,
            StoreFunctions<TKey, EqualityEmpty, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, EqualityEmpty>>,
            BlittableAllocator<TKey, EqualityEmpty, StoreFunctions<TKey, EqualityEmpty, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, EqualityEmpty>>>
        > session
    ) : IScanIteratorFunctions<TKey, EqualityEmpty>
    {
        public bool SingleReader(ref TKey key, ref EqualityEmpty value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
        {
            if (other.PrivateProtectedContainsKey(ref key))
            {
            }
            else
            {
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                var status = session.BasicContext.RMW(ref key, ref input, ref output);
                if (status.IsPending)
                {
                    session.BasicContext.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                    using (completedOutputs)
                    {
                        Debug.Assert(completedOutputs.Next());
                    }
                }
                else
                {
                }
            }
            cursorRecordResult = CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key, ref EqualityEmpty value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);
        public bool OnStart(long beginAddress, long endAddress) => true;
        public void OnStop(bool completed, long numberOfRecords) { }
        public void OnException(Exception exception, long numberOfRecords) { }
    }
    private readonly struct SymmetricExceptWithIterator(
        UnmanagedHashSet<TKey> self,
        ClientSession<
            TKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            UnmanagedSymmetricExceptWith<TKey, EqualityEmpty>,
            StoreFunctions<TKey, EqualityEmpty, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, EqualityEmpty>>,
            BlittableAllocator<TKey, EqualityEmpty, StoreFunctions<TKey, EqualityEmpty, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, EqualityEmpty>>>
        > session
    ) : IScanIteratorFunctions<TKey, EqualityEmpty>
    {
        public bool SingleReader(ref TKey key, ref EqualityEmpty value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
        {
            if (self.WouldOverflowMutable(Unsafe.SizeOf<TKey>(), 1))
                throw self.ThrowLogFull();
            var input = default(EqualityEmpty);
            var output = default(EqualityEmpty);
            var status = session.BasicContext.RMW(ref key, ref input, ref output);
            if (status.IsPending)
            {
                session.BasicContext.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                using (completedOutputs)
                {
                    Debug.Assert(completedOutputs.Next());
                }
            }
            cursorRecordResult = CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TKey key, ref EqualityEmpty value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
            => this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);
        public bool OnStart(long beginAddress, long endAddress) => true;
        public void OnStop(bool completed, long numberOfRecords) { }
        public void OnException(Exception exception, long numberOfRecords) { }
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.IntersectWith_ClientSession;
        var iterator = new IntersectWithIterator(other, session);
        var cursor = 0L;
        session.IterateLookup(ref iterator, ref cursor, resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.SymmetricExceptWith_ClientSession;
        var iterator = new SymmetricExceptWithIterator(this, session);
        var cursor = 0L;
        other.ReadClientSession.IterateLookup(ref iterator, ref cursor, resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }
}
