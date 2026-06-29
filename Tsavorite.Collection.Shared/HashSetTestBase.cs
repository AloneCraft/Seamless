namespace Tsavorite.Collection;
/// <summary>
/// <see cref="global::System.Collections.Generic.ISet{T}"/> 相当�E共通テスト基底クラス、E/// Dictionary 系と重褁E��なぁEHashSet 固有�E振る�EぁE��検証する、E/// </summary>
public abstract class HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    [BenchFact,Benchmark]
    public void IsReadOnly() {
        using var h = TStrategy.CreateMemorySet();
        ICollection<T> set = h.Set;
        {
            // コメント文字化け修正済み
            Assert.False(set.IsReadOnly);
        }
    }
    [BenchFact,Benchmark]
    public void Add() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
            Assert.False(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            ICollection<T> set = h.Set;
            set.Add(TStrategy.Elem(1));
            Assert.Single(set);
        }
    }
    [BenchFact]
    public void CopyTo() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var array = new T[2];
            h.Set.CopyTo(array,0);
            Assert.Contains(TStrategy.Elem(1),array);
            Assert.Contains(TStrategy.Elem(2),array);
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.CopyTo(null!,0));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Set.CopyTo(new T[2],-1));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            Assert.Throws<ArgumentException>(() => h.Set.CopyTo(new T[1],0));
        }
    }
    [BenchFact]
    public void ExceptWith() {
        {
            // コメント文字化け修正済み
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
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.ExceptWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact]
    public void IntersectWith() {
        {
            // コメント文字化け修正済み
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
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.IntersectWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact,Benchmark]
    public void IsSubsetOf() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsSubsetOf(other.Set));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(99));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsSubsetOf(other.Set));
        }
    }
    [BenchFact,Benchmark]
    public void IsSupersetOf() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsSupersetOf(other.Set));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(99));
            Assert.False(h.Set.IsSupersetOf(other.Set));
        }
    }
    [BenchFact,Benchmark]
    public void IsProperSubsetOf() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsProperSubsetOf(other.Set));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsProperSubsetOf(other.Set));
        }
    }
    [BenchFact,Benchmark]
    public void IsProperSupersetOf() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsProperSupersetOf(other.Set));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            Assert.False(h.Set.IsProperSupersetOf(other.Set));
        }
    }
    [BenchFact,Benchmark]
    public void Overlaps() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.Overlaps(other.Set));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(99));
            Assert.False(h.Set.Overlaps(other.Set));
        }
    }
    [BenchFact,Benchmark]
    public void SetEquals() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.SetEquals(other.Set));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.False(h.Set.SetEquals(other.Set));
        }
    }
    [BenchFact]
    public void SymmetricExceptWith() {
        {
            // コメント文字化け修正済み
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
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.SymmetricExceptWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact]
    public void UnionWith() {
        {
            // コメント文字化け修正済み
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
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.UnionWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact,Benchmark]
    public void WithOverloads_AcceptConcreteHashSetSystemISetAndEnumerable() {
        {
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(2));
            h.Set.ExceptWith(other.Set);
            Assert.Single(h.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            global::System.Collections.Generic.ISet<T> other = new global::System.Collections.Generic.HashSet<T> { TStrategy.Elem(2),TStrategy.Elem(3) };
            h.Set.IntersectWith(other);
            Assert.Single(h.Set);
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
        }
        {
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            h.Set.UnionWith(new[] { TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(3) }.AsEnumerable());
            Assert.Equal(3,h.Set.Count);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(2)));
            Assert.True(h.Set.Contains(TStrategy.Elem(3)));
        }
        {
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            h.Set.SymmetricExceptWith(new[] { TStrategy.Elem(2),TStrategy.Elem(3),TStrategy.Elem(3) }.AsEnumerable());
            Assert.Equal(2,h.Set.Count);
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
            Assert.True(h.Set.Contains(TStrategy.Elem(3)));
        }
    }
    [BenchFact,Benchmark]
    public void Contains() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Contains(TStrategy.Elem(99)));
        }
    }
    [BenchFact,Benchmark]
    public void Remove() {
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Remove(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Remove(TStrategy.Elem(99)));
        }
    }
    [BenchFact,Benchmark]
    public void Clear() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        {
            // コメント文字化け修正済み
            h.Set.Clear();
            Assert.Empty(h.Set);
        }
    }
    [BenchFact,Benchmark]
    public void Count() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        h.Set.Add(TStrategy.Elem(2));
        {
            // コメント文字化け修正済み
            Assert.Equal(2,h.Set.Count);
        }
    }
    [BenchFact,Benchmark]
    public void GetEnumerator() {
        {
            // コメント文字化け修正済み
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
            // コメント文字化け修正済み
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            global::System.Collections.IEnumerable set = h.Set;
            var count = 0;
            foreach(var _ in set)
                count++;
            Assert.Equal(1,count);
        }
    }
    [BenchFact,Benchmark]
    public void Dispose() {
        var h = TStrategy.CreateMemorySet();
        var set = h.Set;
        {
            // コメント文字化け修正済み
            Assert.False(set.IsDisposed);
            set.Dispose();
            Assert.True(set.IsDisposed);
            set.Dispose();
            h.Dispose();
        }
    }
}
