using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// UnmanagedHashSet の 1.1.1 固有部分。IntersectWith/SymmetricExceptWith 実装とイテレータ。
/// </summary>
public abstract partial class UnmanagedHashSet<TKey>
    where TKey:unmanaged,IEquatable<TKey> {
    private readonly ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        bool,//TOutput
        EqualityEmpty,//TContext
        UnmanagedDelete<TKey,EqualityEmpty>,
        StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,EqualityEmpty>>,
        BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,EqualityEmpty>>>
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        TKey,
        EqualityEmpty,
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        UnmanagedSymmetricExceptWith<TKey,EqualityEmpty>,
        StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,EqualityEmpty>>,
        BlittableAllocator<TKey,EqualityEmpty,StoreFunctions<TKey,EqualityEmpty,UnmanagedKeyComparer<TKey>,UnmanagedRecordDisposer<TKey,EqualityEmpty>>>
    > SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession=Store.NewSession<EqualityEmpty,bool,EqualityEmpty,UnmanagedDelete<TKey,EqualityEmpty>>(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<EqualityEmpty,EqualityEmpty,EqualityEmpty,UnmanagedSymmetricExceptWith<TKey,EqualityEmpty>>(new());
    }
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator()=>new(this.ReadClientSession,new());
    public partial void IntersectWith(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.IntersectWith_ClientSession;
        EqualityEmpty Input = default;
        bool Output = false;
        foreach(var key in this) {
            if(other.Contains(key)) continue;
            var k = key;
            Session.BasicContext.RMW(ref k, ref Input, ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
        EqualityEmpty Input = default;
        EqualityEmpty Output = default;
        foreach(var key in other) {
            var k = key;
            var Status = Session.BasicContext.RMW(ref k, ref Input, ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
}
