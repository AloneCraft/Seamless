using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(UnmanagedHashSetDebugView<>))]
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        ExceptWith,//TFunctions
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > ExceptWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        UnmanagedStoreFunctions,//TStoreFunctions
        UnmanagedAllocator
    > IntersectWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession = null!;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        this.CreateAdditionalSessions();
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.ExceptWith_ClientSession);
        CompletePendingAndDispose(this.IntersectWith_ClientSession);
        CompletePendingAndDispose(this.SymmetricExceptWith_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
        this.ExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            ExceptWith//TFunctions
        >(new());
        this.IntersectWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    public UnmanagedList<TKey>.Enumerator GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-hashset-enumerator");
        try {
            var items = ListStore.Create<TKey>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new KeyIterator();
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            return new(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-hashset-memory-enumerator");
        try {
            var items = ListStore.Create<TKey>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new MemoryKeyIterator();
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            return new UnmanagedList<TKey>.Enumerator(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
    private readonly struct UnionWithForEachStrategy:IForEachStrategy<UnionWithForEachStrategy> {
        private readonly ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            UnmanagedAdd<TKey,EqualityEmpty>,
            UnmanagedStoreFunctions,
            UnmanagedAllocator
        > _session;
        public UnionWithForEachStrategy(
            ClientSession<
                FixedSpanByteKey,
                EqualityEmpty,
                bool,
                EqualityEmpty,
                UnmanagedAdd<TKey,EqualityEmpty>,
                UnmanagedStoreFunctions,
                UnmanagedAllocator
            > session) {
            this._session=session;
        }
        public static unsafe bool Reader<TSourceLogRecord>(
            ref UnionWithForEachStrategy self,
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var input = default(EqualityEmpty);
            var output = false;
            ReadOnlySpan<byte> keyBytes = logRecord.KeyBytes;
            Status status;
            fixed(byte* _ = keyBytes) {
                status=self._session.BasicContext.RMW((FixedSpanByteKey)keyBytes,ref input,ref output);
            }
            if(status.IsPending)
                self._session.BasicContext.CompletePending(wait: true);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    public partial void ExceptWith(UnmanagedHashSet<TKey> other) {
        base.ExceptWith(other,this.ExceptWith_ClientSession);
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other) {
        this.IntersectWith(this,other,this.IntersectWith_ClientSession);
    }
    public partial bool IsProperSubsetOf(UnmanagedHashSet<TKey> other) => IsProperSubsetOfSet(this,other);
    public partial bool IsProperSupersetOf(UnmanagedHashSet<TKey> other) => IsProperSupersetOfSet(this,other);
    public partial bool IsSubsetOf(UnmanagedHashSet<TKey> other) => IsSubsetOfSet(this,other);
    public partial bool IsSupersetOf(UnmanagedHashSet<TKey> other) => IsSupersetOfSet(this,other);
    public partial bool Overlaps(UnmanagedHashSet<TKey> other) => OverlapsSet(this,other);
    public partial bool SetEquals(UnmanagedHashSet<TKey> other) => SetEqualsSet(this,other);
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        base.SymmetricExceptWith(other,this.SymmetricExceptWith_ClientSession);
    }
    public partial void UnionWith(UnmanagedHashSet<TKey> other) {
        var strategy = new UnionWithForEachStrategy(this.Add_ClientSession);
        ForEach(other,ref strategy);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
