using System.Collections;
using System.Diagnostics;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(DebugView<>))]
public partial class ManagedHashSet<TKey>:ManagedCollection<TKey,EqualityEmpty,ManagedSerializer<TKey>,ManagedSerializer<EqualityEmpty>>,ISeamlessHashSet<TKey>
    where TKey:IEquatable<TKey>{
    //private readonly byte[] EmptyBytes;
    public bool IsReadOnly=>false;
    public bool Add(TKey item){
        var Result=this.PrivateTryAdd(item,EqualityEmpty.Default);
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
    void ICollection<TKey>.Add(TKey item)=>this.Add(item);
    public void CopyTo(TKey[] array,int arrayIndex){
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this){
            array[arrayIndex++]=item;
        }
    }
    public void ExceptWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            this.Remove(item);
        }
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()=>this.GetEnumerator();
    public bool IsProperSubsetOf(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.GetLongCount()>=other.GetLongCount())
            return false;
        foreach(var Key in this){
            if(other.Contains(Key))
                continue;
            return false;
        }
        return true;
    }
    public bool IsProperSupersetOf(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.GetLongCount()<=other.GetLongCount())
            return false;
        foreach(var item in other){
            if(!this.Contains(item)){
                return false;
            } else{
            }
        }
        return true;
    }
    public bool IsSubsetOf(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var Key in this){
            if(!other.Contains(Key)){
                return false;
            } else{
            }
        }
        return true;
    }
    public bool IsSupersetOf(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            if(!this.Contains(item)){
                return false;
            } else{
            }
        }
        return true;
    }
    public bool Overlaps(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            if(this.Contains(item)){
                return true;
            } else{
            }
        }
        return false;
    }
    public bool SetEquals(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.GetLongCount()!=other.GetLongCount())
            return false;
        foreach(var Key in this){
            if(!other.Contains(Key)){
                return false;
            } else{
            }
        }
        return true;
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other);
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other);
    public void UnionWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other){
            this.Add(item);
        }
    }
    public override bool IsDisposed=>this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing){
        if(this.disposed) return;
        if(disposing){
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        } else{
        }
        this.disposed=true;
        base.Dispose(disposing);
    }
}
