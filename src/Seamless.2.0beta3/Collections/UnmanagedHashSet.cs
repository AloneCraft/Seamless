using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// UnmanagedHashSet の 2.0beta3 固有部分。IntersectWith/SymmetricExceptWith セッションとイテレータ。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey>
{
    private readonly ClientSession<
        FixedSpanByteKey,
        EqualityEmpty,
        EqualityEmpty,
        EqualityEmpty,
        IntersectWith,
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,
        PinnedSpanByte,
        SpanByteAndMemory,
        EqualityEmpty,
        SymmetricExceptWith,
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey, EqualityEmpty> CollectionStore) : base(CollectionStore)
    {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession = Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith
        >(new());
        this.SymmetricExceptWith_ClientSession = Store.NewSession<
            FixedSpanByteKey,
            PinnedSpanByte,
            SpanByteAndMemory,
            EqualityEmpty,
            SymmetricExceptWith
        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadClientSession, new());
    private readonly struct IntersectWithIterator(
        UnmanagedHashSet<TKey> self,
        UnmanagedHashSet<TKey> other,
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith,
            UnmanagedStoreFunctions,
            UnmanagedAllocator
        > session
    ) : IScanIteratorFunctions
    {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord
        {
            var keyBytes = logRecord.Key;
            if (!other.PrivateProtectedContainsKey(keyBytes))
            {
                if (self.WouldOverflowMutable(keyBytes.Length, 1))
                    throw self.ThrowLogFull();
                var input = default(EqualityEmpty);
                var output = default(EqualityEmpty);
                var status = session.BasicContext.RMW((FixedSpanByteKey)keyBytes, ref input, ref output);
                if (status.IsPending)
                {
                    session.BasicContext.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                    using (completedOutputs)
                    {
                        Debug.Assert(completedOutputs.Next());
                    }
                }
            }
            cursorRecordResult = CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress, long endAddress) => true;
        public void OnException(Exception exception, long numberOfRecords) { }
        public void OnStop(bool completed, long numberOfRecords) { }
    }
    private readonly struct SymmetricExceptWithIterator(
        ClientSession<
            FixedSpanByteKey,
            PinnedSpanByte,
            SpanByteAndMemory,
            EqualityEmpty,
            SymmetricExceptWith,
            UnmanagedStoreFunctions,
            UnmanagedAllocator
        > session
    ) : IScanIteratorFunctions
    {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord
        {
            var input = default(PinnedSpanByte);
            SpanByteAndMemory output = default;
            var status = session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key, ref input, ref output);
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
        public bool OnStart(long beginAddress, long endAddress) => true;
        public void OnException(Exception exception, long numberOfRecords) { }
        public void OnStop(bool completed, long numberOfRecords) { }
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.IntersectWith_ClientSession;
        var iterator = new IntersectWithIterator(this, other, session);
        var cursor = 0L;
        session.IterateLookup(ref iterator, ref cursor, resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }

    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var session = this.SymmetricExceptWith_ClientSession;
        var iterator = new SymmetricExceptWithIterator(session);
        var cursor = 0L;
        other.ReadClientSession.IterateLookup(ref iterator, ref cursor, resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
    }
}
