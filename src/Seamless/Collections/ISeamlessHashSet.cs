namespace Seamless.Collections;

/// <summary>
/// Tsavorite バックエンドを持つ HashSet の共通インターフェース。
/// ManagedHashSet と UnmanagedHashSet の両実装が共有する操作を公開する。
/// 引数は ISeamlessHashSet&lt;T&gt; を受け取る既定実装を持つ。
/// </summary>
public interface ISeamlessHashSet<T>:ICollection<T>,IDisposable
    where T:IEquatable<T>{
    /// <summary>このインスタンスが既に破棄されているかどうかを取得する。</summary>
    bool IsDisposed{get;}
    new bool Add(T item);
    void ExceptWith(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) this.Remove(item);
    }
    void IntersectWith(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        var toRemove=new List<T>();
        foreach(var item in this) if(!other.Contains(item)) toRemove.Add(item);
        foreach(var item in toRemove) this.Remove(item);
    }
    bool IsSubsetOf(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in this) if(!other.Contains(item)) return false;
        return true;
    }
    bool IsSupersetOf(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) if(!this.Contains(item)) return false;
        return true;
    }
    bool IsProperSubsetOf(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.Count>=other.Count) return false;
        return this.IsSubsetOf(other);
    }
    bool IsProperSupersetOf(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.Count<=other.Count) return false;
        return this.IsSupersetOf(other);
    }
    bool Overlaps(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) if(this.Contains(item)) return true;
        return false;
    }
    bool SetEquals(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        if(this.Count!=other.Count) return false;
        foreach(var item in this) if(!other.Contains(item)) return false;
        return true;
    }
    void SymmetricExceptWith(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        var toAdd=new List<T>();
        var toRemove=new List<T>();
        foreach(var item in other){
            if(this.Contains(item)) toRemove.Add(item);
            else toAdd.Add(item);
        }
        foreach(var item in toRemove) this.Remove(item);
        foreach(var item in toAdd) this.Add(item);
    }
    void UnionWith(ISeamlessHashSet<T> other){
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) this.Add(item);
    }
}
