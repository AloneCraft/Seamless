

using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

/// <summary>
/// UnmanagedHashSet の 2.0beta3 固有部分。IntersectWith/SymmetricExceptWith セッションとイテレータ。
/// </summary>
public partial class UnmanagedHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private readonly ClientSession<
        FixedSpanByteKey,

        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,


        UnmanagedStoreFunctions,










        UnmanagedAllocator














    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,

        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,



        UnmanagedStoreFunctions,










        UnmanagedAllocator














    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith


        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,
            SymmetricExceptWith



        >(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() => new(this.ReadClientSession,new());
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() => new Enumerator<MemoryKeyIterator,TKey>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private void ExceptWithZeroCopy(UnmanagedHashSet<TKey> other) {
        ExceptWith(this,other,other.IntersectWith_ClientSession,this.ContainsDeleteClientSession);
    }
    private void UnionWithZeroCopy(UnmanagedHashSet<TKey> other) {
        UnionWith(this,other,other.IntersectWith_ClientSession,this.AddClientSession);
    }








































































    public partial void IntersectWith(UnmanagedHashSet<TKey> other) {
        IntersectWith(this,other,this.IntersectWith_ClientSession);
    }





    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        SymmetricExceptWith(this,other,other.IntersectWith_ClientSession,this.SymmetricExceptWith_ClientSession);
    }





}
