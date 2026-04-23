using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(ManagedHashSetDebugView<>))]
public partial class ManagedHashSet<TKey>
    where TKey : IEquatable<TKey> {
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        ExceptWith,//TFunctions
        ManagedStoreFunctions,
        ManagedAllocator
    > ExceptWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator
    > IntersectWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        ManagedStoreFunctions,
        ManagedAllocator
    > SymmetricExceptWith_ClientSession = null!;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
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
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<MemoryKeyIterator,TKey>(this.ReadSession,new(),this.HeadAddress);
    private readonly struct ExceptWithScanIteratorFunctions(
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            ExceptWith,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session
    ):IScanIteratorFunctions {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            Session.BasicContext.Delete((FixedSpanByteKey)logRecord.Key);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    public partial void ExceptWith(ManagedHashSet<TKey> other) {
        var ReadSession = other.ReadSession;
        var Cursor = 0L;
        var Iterator = new ExceptWithScanIteratorFunctions(this.ExceptWith_ClientSession);
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.ExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other) {
        IntersectWith(this,other,this.IntersectWith_ClientSession);
    }
    public partial bool IsProperSubsetOf(ManagedHashSet<TKey> other) => IsProperSubsetOfSet(this,other);
    public partial bool IsProperSupersetOf(ManagedHashSet<TKey> other) => IsProperSupersetOfSet(this,other);
    public partial bool IsSubsetOf(ManagedHashSet<TKey> other) => IsSubsetOfSet(this,other);
    public partial bool IsSupersetOf(ManagedHashSet<TKey> other) => IsSupersetOfSet(this,other);
    public partial bool Overlaps(ManagedHashSet<TKey> other) => OverlapsSet(this,other);
    public partial bool SetEquals(ManagedHashSet<TKey> other) => SetEqualsSet(this,other);
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        base.SymmetricExceptWith(other,this.SymmetricExceptWith_ClientSession);
    }
    public partial void UnionWith(ManagedHashSet<TKey> other) {
        var ReadSession = other.ReadSession;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession);
        var Cursor = 0L;
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
