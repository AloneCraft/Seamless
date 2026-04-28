using System.Collections;
using System.Diagnostics;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使う UnmanagedHashSet。
/// 重複を許さず、unmanaged キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(UnmanagedHashSetDebugView<>))]
public partial class UnmanagedHashSet<TKey>:UnmanagedUnmanagedCollection<TKey,EqualityEmpty,UnmanagedSerializer<TKey>,UnmanagedSerializer<EqualityEmpty>>, IHashSet<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    private IDisposable? ownedStore;
    public bool IsReadOnly => false;
    void ICollection<TKey>.Add(TKey item) => this.Add(item);
    public bool Add(TKey item) {
        var Result = this.PrivateTryAdd(item,EqualityEmpty.Default);
        switch(Result) {
            case TryAddResult.Success:
                return true;
            case TryAddResult.KeyAlreadyExists:
                return false;
            default:
                Debug.Assert(TryAddResult.LogFull==Result);
                throw this.ThrowLogFull();
        }
    }
    public void CopyTo(TKey[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this) {
            array[arrayIndex++]=item;
        }
    }
    public partial void ExceptWith(UnmanagedHashSet<TKey> other);
    public partial void IntersectWith(UnmanagedHashSet<TKey> other);
    public partial bool IsSubsetOf(UnmanagedHashSet<TKey> other);
    public partial bool IsSupersetOf(UnmanagedHashSet<TKey> other);
    public partial bool IsProperSubsetOf(UnmanagedHashSet<TKey> other);
    public partial bool IsProperSupersetOf(UnmanagedHashSet<TKey> other);
    public partial bool Overlaps(UnmanagedHashSet<TKey> other);
    public partial bool SetEquals(UnmanagedHashSet<TKey> other);
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other);
    public partial void UnionWith(UnmanagedHashSet<TKey> other);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed) return;
        if(disposing) {
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        }
        this.disposed=true;
        base.Dispose(disposing);
        if(disposing)
            this.ownedStore?.Dispose();
    }
}
