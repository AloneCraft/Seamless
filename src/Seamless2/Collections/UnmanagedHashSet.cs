using System.Diagnostics;
using Seamless.SessionFunctions;
using Seamless.Stores;

using static System.Collections.Specialized.BitVector32;
namespace Seamless.Collections;

/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(UnmanagedHashSetDebugView<>))]
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private readonly ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        UnmanagedStoreFunctions,//TStoreFunctions
        UnmanagedAllocator
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
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
    public partial void ExceptWith(UnmanagedHashSet<TKey> other) {
        var ReadSession = other.ReadSession;
        var ContainsDeleteSession = this.ContainsDeleteSession;
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_ExceptWith(ContainsDeleteSession);
        ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        ContainsDeleteSession.BasicContext.CompletePending(wait: true);
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other) {
        IntersectWith(this,other,this.IntersectWith_ClientSession);
    }
    public partial bool IsProperSubsetOf(UnmanagedHashSet<TKey> other) => IsProperSubsetOfSet(this,other);
    public partial bool IsProperSupersetOf(UnmanagedHashSet<TKey> other) => IsProperSupersetOfSet(this,other);
    public partial bool IsSubsetOf(UnmanagedHashSet<TKey> other) => IsSubsetOfSet(this,other);
    public partial bool IsSupersetOf(UnmanagedHashSet<TKey> other) => IsSupersetOfSet(this,other);
    public partial bool Overlaps(UnmanagedHashSet<TKey> other) => OverlapsSet(this,other);
    public partial bool SetEquals(UnmanagedHashSet<TKey> other) => SetEqualsSet(this,other);
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        var ReadSession = other.ReadSession;
        var Cursor = 0L;
        var Iterator = new ScanIteratorFunctions_SymmetricExceptWith(this.SymmetricExceptWith_ClientSession);
        var 最後まで読み切ったか = ReadSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        ReadSession.BasicContext.CompletePending(wait: true);
        this.SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void UnionWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var ReadSession = other.ReadSession;
        var cursor = 0L;
        var Iterator = new ScanIteratorFunctions_UnionWith(this.Add_ClientSession);
        var 最後まで読み切ったか = ReadSession.IterateLookup(ref Iterator,ref cursor,resetCursor: false);
        other.ReadSession.BasicContext.CompletePending(wait: true);
        SymmetricExceptWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
}
