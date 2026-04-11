using Seamless.ScanIteratorFunctions;
using Seamless.Store;
namespace Seamless.Collections;
/// <summary>
/// UnmanagedHashSet の 1.1.1 固有部分。IntersectWith/SymmetricExceptWith 実装とイテレータ。
/// </summary>
public abstract partial class UnmanagedHashSet<TKey>
    where TKey:unmanaged,IEquatable<TKey> {
    private readonly NullDisposable IntersectWith_ClientSession;
    private readonly NullDisposable SymmetricExceptWith_ClientSession;
    internal UnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        this.IntersectWith_ClientSession=new();
        this.SymmetricExceptWith_ClientSession=new();
    }
    public UnmanagedCollection<
        TKey,
        EqualityEmpty,
        UnmanagedSerializer<TKey>,
        UnmanagedSerializer<EqualityEmpty>
    >.Enumerator<
        UnmanagedKeyIterator<TKey,EqualityEmpty>,
        TKey
    > GetEnumerator()=>new(this.ReadClientSession,new());
    public partial void IntersectWith(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var keysToRemove = new List<TKey>();
        foreach(var key in this) {
            if(!other.Contains(key)) {
                keysToRemove.Add(key);
            }
        }
        foreach(var key in keysToRemove) {
            this.Remove(key);
        }
    }
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var keysToAdd = new List<TKey>();
        var keysToRemove = new List<TKey>();
        foreach(var key in other) {
            if(this.Contains(key)) {
                keysToRemove.Add(key);
            } else {
                keysToAdd.Add(key);
            }
        }
        foreach(var key in keysToRemove) {
            this.Remove(key);
        }
        foreach(var key in keysToAdd) {
            this.Add(key);
        }
    }
    private readonly struct NullDisposable {
        public void Dispose() { }
    }
}
