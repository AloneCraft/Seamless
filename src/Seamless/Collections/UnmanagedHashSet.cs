using System.Collections;
using System.Diagnostics;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使う UnmanagedHashSet。
/// 重複を許さず、unmanaged キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(DebugView<>))]
public partial class UnmanagedHashSet<TKey>:UnmanagedCollection<TKey,EqualityEmpty,UnmanagedSerializer<TKey>,UnmanagedSerializer<EqualityEmpty>>, ICollection<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    public bool IsReadOnly => false;
    void ICollection<TKey>.Add(TKey item) => this.Add(item);
    public bool Add(TKey item){
        var Result = this.PrivateTryAdd(item,EqualityEmpty.Default);
        switch(Result){
            case TryAddResult.Success:
                return true;
            case TryAddResult.KeyAlreadyExists:
                return false;
            default:
                Debug.Assert(TryAddResult.LogFull==Result);
                throw this.ThrowLogFull();
        }
    }
    public void CopyTo(TKey[] array,int arrayIndex){
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this){
            array[arrayIndex++]=item;
        }
    }
    public void ExceptWith(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            this.Remove(item);
        }
    }
    public partial void IntersectWith(UnmanagedHashSet<TKey> other);
    public bool IsSubsetOf(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var Key in this){
            if(!other.Contains(Key)){
                return false;
            }
        }
        return true;
    }
    public bool IsSupersetOf(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            if(!this.Contains(item)){
                return false;
            }
        }
        return true;
    }
    public bool IsProperSubsetOf(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.GetLongCount()>=other.GetLongCount())
            return false;
        foreach(var Key in this){
            if(!other.Contains(Key)){
                return false;
            }
        }
        return true;
    }
    public bool IsProperSupersetOf(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.GetLongCount()<=other.GetLongCount())
            return false;
        foreach(var item in other){
            if(!this.Contains(item)){
                return false;
            }
        }
        return true;
    }
    public bool Overlaps(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            if(this.Contains(item)){
                return true;
            }
        }
        return false;
    }
    public bool SetEquals(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.GetLongCount()!=other.GetLongCount())
            return false;
        foreach(var Key in this){
            if(!other.Contains(Key)){
                return false;
            }
        }
        return true;
    }
    public partial void SymmetricExceptWith(UnmanagedHashSet<TKey> other);
    public void UnionWith(UnmanagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            this.Add(item);
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing){
        if(this.disposed) return;
        if(disposing){
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        }
        this.disposed=true;
        base.Dispose(disposing);
    }
}
