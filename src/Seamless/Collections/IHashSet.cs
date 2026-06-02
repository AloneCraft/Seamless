using System;
using System.Collections;
using System.Collections.Generic;
namespace Seamless.Collections;

/// <summary>
/// Tsavorite バックエンドを持つ HashSet の共通インターフェース。
/// HashSet と HashSet の両実装が共有する操作を公開する。
/// 引数は ISeamlessHashSet&lt;T&gt; を受け取る既定実装を持つ。
/// </summary>
public interface IHashSet<T>:IMemoryCollection<T>, IDisposable {
    /// <summary>このインスタンスが既に破棄されているかどうかを取得する。</summary>
    bool IsDisposed { get; }
    /// <summary>
    /// 内部 session を張り替えて、Tsavorite が保持している古い epoch 世代参照を解放する。
    /// 既定実装は no-op で、session を明示管理する実装が必要に応じて上書きする。
    /// </summary>
    void Refresh() {
    }
    new bool Add(T item);
    void ExceptWith(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<T>)other) this.Remove(item);
    }
    void IntersectWith(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        var toRemove = new global::System.Collections.Generic.List<T>();
        foreach(var item in (ICollection<T>)this) if(!other.Contains(item)) toRemove.Add(item);
        foreach(var item in toRemove) this.Remove(item);
    }
    bool IsSubsetOf(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<T>)this) if(!other.Contains(item)) return false;
        return true;
    }
    bool IsSupersetOf(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<T>)other) if(!this.Contains(item)) return false;
        return true;
    }
    bool IsProperSubsetOf(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(this.Count>=other.Count) return false;
        return this.IsSubsetOf(other);
    }
    bool IsProperSupersetOf(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(this.Count<=other.Count) return false;
        return this.IsSupersetOf(other);
    }
    bool Overlaps(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<T>)other) if(this.Contains(item)) return true;
        return false;
    }
    bool SetEquals(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(this.Count!=other.Count) return false;
        foreach(var item in (ICollection<T>)this) if(!other.Contains(item)) return false;
        return true;
    }
    void SymmetricExceptWith(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        var toAdd = new global::System.Collections.Generic.List<T>();
        var toRemove = new global::System.Collections.Generic.List<T>();
        foreach(var item in (ICollection<T>)other) {
            if(this.Contains(item)) toRemove.Add(item);
            else toAdd.Add(item);
        }
        foreach(var item in toRemove) this.Remove(item);
        foreach(var item in toAdd) this.Add(item);
    }
    void UnionWith(IHashSet<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<T>)other) this.Add(item);
    }
}
