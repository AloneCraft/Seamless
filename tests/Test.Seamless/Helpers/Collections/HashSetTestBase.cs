using System.Collections;
namespace Test.Seamless.Collections;
/// <summary>
/// HashSet テストの共通基底クラス。
/// Dictionary の DictionaryTestBase と同様に、
/// 操作は ISeamlessHashSet&lt;T&gt; を直接呼び出す。
/// </summary>
public abstract class HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    [Fact, Trait("Category","Unit")]
    public void IsReadOnly() {
        using var h = TStrategy.CreateMemorySet();
        ICollection<T> set = h.Set;
        {
            // 読み取り専用ではないこと
            Assert.False(set.IsReadOnly);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add() {
        {
            // 新規要素の追加は true を返すこと
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // 重複要素の追加は false を返すこと
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
            Assert.False(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // ICollection.Add 経由でも追加できること
            using var h = TStrategy.CreateMemorySet();
            ICollection<T> set = h.Set;
            set.Add(TStrategy.Elem(1));
            Assert.Single(set);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo() {
        {
            // 全要素を配列へコピーできること
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var array = new T[2];
            h.Set.CopyTo(array,0);
            Assert.Contains(TStrategy.Elem(1),array);
            Assert.Contains(TStrategy.Elem(2),array);
        }
        {
            // null 配列は例外になること
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.CopyTo(null!,0));
        }
        {
            // 負のインデックスは例外になること
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Set.CopyTo(new T[2],-1));
        }
        {
            // 配列が小さすぎると例外になること
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            Assert.Throws<ArgumentException>(() => h.Set.CopyTo(new T[1],0));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ExceptWith() {
        {
            // 共通要素が削除され差集合になること
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            h.Set.ExceptWith(other.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(2)));
        }
        {
            // null を渡すと例外になること
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.ExceptWith(null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IntersectWith() {
        {
            // 共通要素だけが残ること
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(3));
            h.Set.IntersectWith(other.Set);
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
            Assert.Single(h.Set);
        }
        {
            // null を渡すと例外になること
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.IntersectWith(null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf() {
        {
            // 部分集合なら true を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsSubsetOf(other.Set));
        }
        {
            // 含まれない要素があると false を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(99));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsSubsetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf() {
        {
            // 上位集合なら true を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsSupersetOf(other.Set));
        }
        {
            // 含まれない要素があると false を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(99));
            Assert.False(h.Set.IsSupersetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf() {
        {
            // 真部分集合なら true を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsProperSubsetOf(other.Set));
        }
        {
            // 同サイズでは false を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsProperSubsetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf() {
        {
            // 真上位集合なら true を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsProperSupersetOf(other.Set));
        }
        {
            // 同サイズでは false を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsProperSupersetOf(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Overlaps() {
        {
            // 共通要素があれば true を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.Overlaps(other.Set));
        }
        {
            // 共通要素がなければ false を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(99));
            Assert.False(h.Set.Overlaps(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void SetEquals() {
        {
            // 同一要素なら true を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.SetEquals(other.Set));
        }
        {
            // 異なる要素なら false を返すこと
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.False(h.Set.SetEquals(other.Set));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWith() {
        {
            // 共通要素を除いた対称差になること
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(3));
            h.Set.SymmetricExceptWith(other.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(2)));
            Assert.True(h.Set.Contains(TStrategy.Elem(3)));
        }
        {
            // null を渡すと例外になること
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.SymmetricExceptWith(null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void UnionWith() {
        {
            // 和集合になること
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            h.Set.UnionWith(other.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
            Assert.Equal(2,h.Set.Count);
        }
        {
            // null を渡すと例外になること
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.UnionWith(null!));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Contains() {
        {
            // 存在する要素には true を返すこと
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // 存在しない要素には false を返すこと
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Contains(TStrategy.Elem(99)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove() {
        {
            // 存在する要素は削除されること
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Remove(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // 存在しない要素は false を返すこと
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Remove(TStrategy.Elem(99)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Clear() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        {
            // Clear 後は空集合になること
            h.Set.Clear();
            Assert.Empty(h.Set);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Count() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        {
            // 要素数を返すこと
            Assert.Equal(2,h.Set.Count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void GetEnumerator() {
        {
            // ジェネリック列挙で全要素を走査できること
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var list = new List<T>();
            foreach(var item in h.Set)
                list.Add(item);
            Assert.Equal(2,list.Count);
            Assert.Contains(TStrategy.Elem(1),list);
            Assert.Contains(TStrategy.Elem(2),list);
        }
        {
            // 非ジェネリック列挙でも走査できること
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            IEnumerable set = h.Set;
            var count = 0;
            foreach(var _ in set)
                count++;
            Assert.Equal(1,count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose() {
        var h = TStrategy.CreateMemorySet();
        var set = h.Set;
        {
            // Dispose は複数回呼んでも安全なこと
            Assert.False(set.IsDisposed);
            set.Dispose();
            Assert.True(set.IsDisposed);
            set.Dispose();
            h.Dispose();
        }
    }
}
