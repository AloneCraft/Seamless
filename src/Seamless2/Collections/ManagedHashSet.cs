using Seamless.SessionFunctions;
using Seamless.Stores;

using static System.Collections.Specialized.BitVector32;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
public partial class ManagedHashSet<TKey>
    where TKey : IEquatable<TKey> {
    private readonly ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        ManagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
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
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<MemoryKeyIterator,TKey>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    public partial void ExceptWith(ManagedHashSet<TKey> other) {
        var ReadSession = other.ReadSession;
        var ContainsDeleteSession = this.ContainsDeleteSession;
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_ExceptWith(ContainsDeleteSession);
        ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        ContainsDeleteSession.BasicContext.CompletePending(wait: true);
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
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_SymmetricExceptWith(this.SymmetricExceptWith_ClientSession);
        ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void UnionWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var Cursor = 0L;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession);
        ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.Add_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
