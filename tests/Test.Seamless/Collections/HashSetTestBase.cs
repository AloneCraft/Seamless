using Seamless.Collections;
namespace Test.Seamless.Collections;

/// <summary>
/// <see cref="Collections.ISet{T}"/> インターフェース経由でテストする汎用ベースクラス。
/// <typeparamref name="TStrategy"/> の静的ポリモーフィズムにより具体型を差し替える。
/// <para>
/// セット演算メソッド（<c>IntersectWith</c> 等）は <c>Seamless.Collections.ISet{T}</c> の
/// シグネチャに従い <see cref="ManagedHashSet{T}"/> を引数に取る。
/// <see cref="SetHandle{T}.Concrete"/> でその具体型を取得する。
/// </para>
/// </summary>
public abstract class HashSetTestBase<TStrategy, T>
    where TStrategy:IHashSetTestStrategy<T>
    where T:IEquatable<T> {
    // ── 基本操作 ──────────────────────────────────────────────────

    [Fact, Trait("Category","Unit")]
    public void Add_新規要素_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        Assert.True(h.Set.Add(TStrategy.Elem(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Add_重複要素_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        Assert.False(h.Set.Add(TStrategy.Elem(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void ICollection_Add_void_追加される(){
        using var h=TStrategy.CreateMemorySet();
        ICollection<T> col=h.Set;
        col.Add(TStrategy.Elem(1));
        Assert.Equal(1,col.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_存在する要素_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        Assert.True(h.Set.Contains(TStrategy.Elem(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_存在しない要素_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        Assert.False(h.Set.Contains(TStrategy.Elem(99)));
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_存在する要素_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        Assert.True(h.Set.Remove(TStrategy.Elem(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_存在しない要素_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        Assert.False(h.Set.Remove(TStrategy.Elem(99)));
    }

    [Fact, Trait("Category","Unit")]
    public void Count_追加後に増加する(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        Assert.Equal(2,h.Set.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void Clear_後Count0(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Clear();
        Assert.Equal(0,h.Set.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void IsReadOnly_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        ICollection<T> col=h.Set;
        Assert.False(col.IsReadOnly);
    }

    [Fact, Trait("Category","Unit")]
    public void CopyTo_正常(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        var array=new T[2];
        ((ICollection<T>)h.Set).CopyTo(array,0);
        Assert.Contains(TStrategy.Elem(1),array);
        Assert.Contains(TStrategy.Elem(2),array);
    }

    [Fact, Trait("Category","Unit")]
    public void CopyTo_null配列_例外(){
        using var h=TStrategy.CreateMemorySet();
        Assert.Throws<ArgumentNullException>(() => ((ICollection<T>)h.Set).CopyTo(null!,0));
    }

    [Fact, Trait("Category","Unit")]
    public void GetEnumerator_全要素を列挙できる(){
        using var h=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        var list=new List<T>();
        foreach(var item in (IEnumerable<T>)h.Set)
            list.Add(item);
        Assert.Equal(2,list.Count);
        Assert.Contains(TStrategy.Elem(1),list);
        Assert.Contains(TStrategy.Elem(2),list);
    }

    // ── セット演算 ────────────────────────────────────────────────

    [Fact, Trait("Category","Unit")]
    public void ExceptWith_差集合になる(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(2));
        h.Set.ExceptWith(other.Concrete);
        Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        Assert.False(h.Set.Contains(TStrategy.Elem(2)));
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptWith_null_例外(){
        using var h=TStrategy.CreateMemorySet();
        Assert.Throws<ArgumentNullException>(() => h.Set.ExceptWith(null!));
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectWith_積集合になる(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(3));
        h.Set.IntersectWith(other.Concrete);
        Assert.False(h.Set.Contains(TStrategy.Elem(1)));
        Assert.True(h.Set.Contains(TStrategy.Elem(2)));
        Assert.Equal(1,h.Set.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void IntersectWith_null_例外(){
        using var h=TStrategy.CreateMemorySet();
        Assert.Throws<ArgumentNullException>(() => h.Set.IntersectWith(null!));
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_和集合になる(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(1));
        h.Set.UnionWith(other.Concrete);
        Assert.Equal(2,h.Set.Count);
        Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        Assert.True(h.Set.Contains(TStrategy.Elem(2)));
    }

    [Fact, Trait("Category","Unit")]
    public void UnionWith_null_例外(){
        using var h=TStrategy.CreateMemorySet();
        Assert.Throws<ArgumentNullException>(() => h.Set.UnionWith(null!));
    }

    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWith_対称差になる(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(3));
        h.Set.SymmetricExceptWith(other.Concrete);
        Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        Assert.False(h.Set.Contains(TStrategy.Elem(2)));
        Assert.True(h.Set.Contains(TStrategy.Elem(3)));
    }

    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWith_null_例外(){
        using var h=TStrategy.CreateMemorySet();
        Assert.Throws<ArgumentNullException>(() => h.Set.SymmetricExceptWith(null!));
    }

    // ── 包含関係 ──────────────────────────────────────────────────

    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf_部分集合_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        Assert.True(h.Set.IsSubsetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf_部分集合でない_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(99));
        other.Concrete.Add(TStrategy.Elem(1));
        Assert.False(h.Set.IsSubsetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf_上位集合_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(1));
        Assert.True(h.Set.IsSupersetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf_上位集合でない_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        Assert.False(h.Set.IsSupersetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf_真部分集合_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        Assert.True(h.Set.IsProperSubsetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf_同サイズは偽(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        Assert.False(h.Set.IsProperSubsetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf_真上位集合_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(1));
        Assert.True(h.Set.IsProperSupersetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf_同サイズは偽(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        Assert.False(h.Set.IsProperSupersetOf(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void Overlaps_共通要素あり_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        Assert.True(h.Set.Overlaps(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void Overlaps_共通要素なし_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(99));
        Assert.False(h.Set.Overlaps(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void SetEquals_同一要素_真を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        other.Concrete.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        Assert.True(h.Set.SetEquals(other.Concrete));
    }

    [Fact, Trait("Category","Unit")]
    public void SetEquals_異なる要素_偽を返す(){
        using var h=TStrategy.CreateMemorySet();
        using var other=TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(1));
        other.Concrete.Add(TStrategy.Elem(2));
        Assert.False(h.Set.SetEquals(other.Concrete));
    }
}
