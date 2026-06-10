namespace Tsavorite.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="System.Collections.Generic.ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey>:Collection<TKey,EqualityEmpty,AutoSerializer<TKey>,MemoryPackSerializer<EqualityEmpty>>, IHashSet<TKey> {
    //private readonly byte[] EmptyBytes;
    private IDisposable? ownedStore;
    public bool IsReadOnly => false;
    public bool Add(TKey item) {
        if(this.Contains(item))
            return false;
        this.PrivateUpsert(item,EqualityEmpty.Default);
        return true;
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
    public partial void ExceptWith(HashSet<TKey> other);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    public partial bool IsProperSubsetOf(HashSet<TKey> other);
    public partial bool IsProperSupersetOf(HashSet<TKey> other);
    public partial bool IsSubsetOf(HashSet<TKey> other);
    public partial bool IsSupersetOf(HashSet<TKey> other);
    public partial bool Overlaps(HashSet<TKey> other);
    public partial bool SetEquals(HashSet<TKey> other);
    public partial void IntersectWith(HashSet<TKey> other);
    public partial void SymmetricExceptWith(HashSet<TKey> other);
    public partial void UnionWith(HashSet<TKey> other);
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
