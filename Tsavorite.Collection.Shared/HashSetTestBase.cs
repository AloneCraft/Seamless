namespace Tsavorite.Collection.Shared;
/// <summary>
/// HashSet ・ｽe・ｽX・ｽg・ｽﾌ具ｿｽ・ｽﾊ奇ｿｽ・ｽN・ｽ・ｽ・ｽX・ｽB
/// Dictionary ・ｽ・ｽ DictionaryTestBase ・ｽﾆ難ｿｽ・ｽl・ｽﾉ、
/// ・ｽ・ｽ・ｽ・ｽ・ｽ ITsavoriteHashSet&lt;T&gt; ・ｽｼ接呼び出・ｽ・ｽ・ｽB
/// </summary>
public abstract class HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    [BenchFact,Benchmark]
    public void IsReadOnly() {
        using var h = TStrategy.CreateMemorySet();
        ICollection<T> set = h.Set;
        {
            // ・ｽﾇみ趣ｿｽ・ｽ・ｽp・ｽﾅはなゑｿｽ・ｽ・ｽ・ｽ・ｽ
            Assert.False(set.IsReadOnly);
        }
    }
    [BenchFact,Benchmark]
    public void Add() {
        {
            // ・ｽV・ｽK・ｽv・ｽf・ｽﾌ追会ｿｽ・ｽ・ｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // ・ｽd・ｽ・ｽ・ｽv・ｽf・ｽﾌ追会ｿｽ・ｽ・ｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
            Assert.False(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // ICollection.Add ・ｽo・ｽR・ｽﾅゑｿｽﾇ会ｿｽ・ｽﾅゑｿｽ・ｽ驍ｱ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            ICollection<T> set = h.Set;
            set.Add(TStrategy.Elem(1));
            Assert.Single(set);
        }
    }
    [Xunit.Fact]
    public void CopyTo() {
        {
            // ・ｽS・ｽv・ｽf・ｽ・ｽz・ｽ・ｽﾖコ・ｽs・ｽ[・ｽﾅゑｿｽ・ｽ驍ｱ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var array = new T[2];
            h.Set.CopyTo(array,0);
            Assert.Contains(TStrategy.Elem(1),array);
            Assert.Contains(TStrategy.Elem(2),array);
        }
        {
            // null ・ｽz・ｽ・ｽﾍ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.CopyTo(null!,0));
        }
        {
            // ・ｽ・ｽ・ｽﾌイ・ｽ・ｽ・ｽf・ｽb・ｽN・ｽX・ｽﾍ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Set.CopyTo(new T[2],-1));
        }
        {
            // ・ｽz・ｽｪ擾ｿｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽﾆ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            Assert.Throws<ArgumentException>(() => h.Set.CopyTo(new T[1],0));
        }
    }
    [Xunit.Fact]
    public void ExceptWith() {
        {
            // ・ｽ・ｽ・ｽﾊ要・ｽf・ｽ・ｽ・ｽ尞懶ｿｽ・ｽ・ｽ鼾ｷ・ｽW・ｽ・ｽ・ｽﾉなるこ・ｽ・ｽ
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
            // null ・ｽ・ｽn・ｽ・ｽ・ｽﾆ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.ExceptWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [Xunit.Fact]
    public void IntersectWith() {
        {
            // ・ｽ・ｽ・ｽﾊ要・ｽf・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽc・ｽ驍ｱ・ｽ・ｽ
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
            // null ・ｽ・ｽn・ｽ・ｽ・ｽﾆ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.IntersectWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact,Benchmark]
    public void IsSubsetOf() {
        {
            // ・ｽ・ｽ・ｽ・ｽ・ｽW・ｽ・ｽ・ｽﾈゑｿｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsSubsetOf(other.Set));
        }
        {
            // ・ｽﾜまゑｿｽﾈゑｿｽ・ｽv・ｽf・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
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
            // ・ｽ・ｽﾊ集・ｽ・ｽ・ｽﾈゑｿｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsSupersetOf(other.Set));
        }
        {
            // ・ｽﾜまゑｿｽﾈゑｿｽ・ｽv・ｽf・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
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
            // ・ｽ^・ｽ・ｽ・ｽ・ｽ・ｽW・ｽ・ｽ・ｽﾈゑｿｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsProperSubsetOf(other.Set));
        }
        {
            // ・ｽ・ｽ・ｽT・ｽC・ｽY・ｽﾅゑｿｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
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
            // ・ｽ^・ｽ・ｽﾊ集・ｽ・ｽ・ｽﾈゑｿｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsProperSupersetOf(other.Set));
        }
        {
            // ・ｽ・ｽ・ｽT・ｽC・ｽY・ｽﾅゑｿｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
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
            // ・ｽ・ｽ・ｽﾊ要・ｽf・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.Overlaps(other.Set));
        }
        {
            // ・ｽ・ｽ・ｽﾊ要・ｽf・ｽ・ｽ・ｽﾈゑｿｽ・ｽ・ｽ・ｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
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
            // ・ｽ・ｽ・ｽ・ｽv・ｽf・ｽﾈゑｿｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.SetEquals(other.Set));
        }
        {
            // ・ｽﾙなゑｿｽv・ｽf・ｽﾈゑｿｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.False(h.Set.SetEquals(other.Set));
        }
    }
    [Xunit.Fact]
    public void SymmetricExceptWith() {
        {
            // ・ｽ・ｽ・ｽﾊ要・ｽf・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽﾎ称搾ｿｽ・ｽﾉなるこ・ｽ・ｽ
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
            // null ・ｽ・ｽn・ｽ・ｽ・ｽﾆ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.SymmetricExceptWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [Xunit.Fact]
    public void UnionWith() {
        {
            // ・ｽa・ｽW・ｽ・ｽ・ｽﾉなるこ・ｽ・ｽ
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
            // null ・ｽ・ｽn・ｽ・ｽ・ｽﾆ暦ｿｽO・ｽﾉなるこ・ｽ・ｽ
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
            ISet<T> other = new global::System.Collections.Generic.HashSet<T> { TStrategy.Elem(2),TStrategy.Elem(3) };
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
            // ・ｽ・ｽ・ｽﾝゑｿｽ・ｽ・ｽv・ｽf・ｽﾉゑｿｽ true ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // ・ｽ・ｽ・ｽﾝゑｿｽ・ｽﾈゑｿｽ・ｽv・ｽf・ｽﾉゑｿｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Contains(TStrategy.Elem(99)));
        }
    }
    [BenchFact,Benchmark]
    public void Remove() {
        {
            // ・ｽ・ｽ・ｽﾝゑｿｽ・ｽ・ｽv・ｽf・ｽﾍ削除・ｽ・ｽ・ｽ・ｽ驍ｱ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Remove(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // ・ｽ・ｽ・ｽﾝゑｿｽ・ｽﾈゑｿｽ・ｽv・ｽf・ｽ・ｽ false ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Remove(TStrategy.Elem(99)));
        }
    }
    [BenchFact,Benchmark]
    public void Clear() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        {
            // Clear ・ｽ・ｽﾍ具ｿｽW・ｽ・ｽ・ｽﾉなるこ・ｽ・ｽ
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
            // ・ｽv・ｽf・ｽ・ｽ・ｽ・ｽﾔゑｿｽ・ｽ・ｽ・ｽ・ｽ
            Assert.Equal(2,h.Set.Count);
        }
    }
    [BenchFact,Benchmark]
    public void GetEnumerator() {
        {
            // ・ｽW・ｽF・ｽl・ｽ・ｽ・ｽb・ｽN・ｽ唐ﾅ全・ｽv・ｽf・ｽ枕・ｽ・ｽﾅゑｿｽ・ｽ驍ｱ・ｽ・ｽ
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
            // ・ｽ・ｽW・ｽF・ｽl・ｽ・ｽ・ｽb・ｽN・ｽ唐ﾅゑｿｽ・ｽ・ｽ・ｽ・ｽﾅゑｿｽ・ｽ驍ｱ・ｽ・ｽ
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
            // Dispose ・ｽﾍ包ｿｽ・ｽ・ｽ・ｽ・ｽﾄゑｿｽﾅゑｿｽ・ｽ・ｽS・ｽﾈゑｿｽ・ｽ・ｽ
            Assert.False(set.IsDisposed);
            set.Dispose();
            Assert.True(set.IsDisposed);
            set.Dispose();
            h.Dispose();
        }
    }
}
