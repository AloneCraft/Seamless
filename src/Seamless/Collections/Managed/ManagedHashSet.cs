using System.Collections;
using System.Diagnostics;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="System.Collections.Generic.ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(ManagedHashSetDebugView<>))]
public partial class ManagedHashSet<TKey>:ManagedUnmanagedCollection<TKey,EqualityEmpty,ManagedSerializer<TKey>,ManagedSerializer<EqualityEmpty>>, IHashSet<TKey>
    where TKey : IEquatable<TKey> {
    //private readonly byte[] EmptyBytes;
    private IDisposable? ownedStore;
    public bool IsReadOnly => false;
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
    void ICollection<TKey>.Add(TKey item) => this.Add(item);
    public void CopyTo(TKey[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this) {
            array[arrayIndex++]=item;
        }
    }
    public partial void ExceptWith(ManagedHashSet<TKey> other);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    public partial bool IsProperSubsetOf(ManagedHashSet<TKey> other);
    public partial bool IsProperSupersetOf(ManagedHashSet<TKey> other);
    public partial bool IsSubsetOf(ManagedHashSet<TKey> other);
    public partial bool IsSupersetOf(ManagedHashSet<TKey> other);
    public partial bool Overlaps(ManagedHashSet<TKey> other);
    public partial bool SetEquals(ManagedHashSet<TKey> other);
    public partial void IntersectWith(ManagedHashSet<TKey> other);
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other);
    public partial void UnionWith(ManagedHashSet<TKey> other);
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed) return;
        if(disposing) {
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        }
        else {
        }
        this.disposed=true;
        base.Dispose(disposing);
        if(disposing)
            this.ownedStore?.Dispose();
    }
}
