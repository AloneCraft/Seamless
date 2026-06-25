namespace Tsavorite.Collection.Shared;
/// <summary>
/// HashSet 繝ｻ・ｽe繝ｻ・ｽX繝ｻ・ｽg繝ｻ・ｽ・悟・・ｿ・ｽ繝ｻ・ｽ・雁･・ｽｿ・ｽ繝ｻ・ｽN繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽX繝ｻ・ｽB
/// Dictionary 繝ｻ・ｽ繝ｻ・ｽ DictionaryTestBase 繝ｻ・ｽ・・屮・ｿ・ｽ繝ｻ・ｽl繝ｻ・ｽ・峨・
/// 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ ITsavoriteHashSet&lt;T&gt; 繝ｻ・ｽ﨟托ｽｼ謗･蜻ｼ縺ｳ蜃ｺ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽB
/// </summary>
public abstract class HashSetTestBase<TStrategy, T>
    where TStrategy : IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    [BenchFact,Benchmark]
    public void IsReadOnly() {
        using var h = TStrategy.CreateMemorySet();
        ICollection<T> set = h.Set;
        {
            // 繝ｻ・ｽ・・∩雜｣・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽp繝ｻ・ｽ・・・縺ｪ繧托ｽｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            Assert.False(set.IsReadOnly);
        }
    }
    [BenchFact,Benchmark]
    public void Add() {
        {
            // 繝ｻ・ｽV繝ｻ・ｽK繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ・瑚ｿｽ莨夲ｽｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // 繝ｻ・ｽd繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ・瑚ｿｽ莨夲ｽｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.True(h.Set.Add(TStrategy.Elem(1)));
            Assert.False(h.Set.Add(TStrategy.Elem(1)));
        }
        {
            // ICollection.Add 繝ｻ・ｽo繝ｻ・ｽR繝ｻ・ｽ・・ｑ・ｿ・ｽ・・ｼ夲ｽｿ・ｽ繝ｻ・ｽ・・ｑ・ｿ・ｽ繝ｻ・ｽ鬩搾ｽｱ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            ICollection<T> set = h.Set;
            set.Add(TStrategy.Elem(1));
            Assert.Single(set);
        }
    }
    [BenchFact]
    public void CopyTo() {
        {
            // 繝ｻ・ｽS繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ繝ｻ・ｽz繝ｻ・ｽ繝ｻ・ｽ・悶さ繝ｻ・ｽs繝ｻ・ｽ[繝ｻ・ｽ・・ｑ・ｿ・ｽ繝ｻ・ｽ鬩搾ｽｱ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            var array = new T[2];
            h.Set.CopyTo(array,0);
            Assert.Contains(TStrategy.Elem(1),array);
            Assert.Contains(TStrategy.Elem(2),array);
        }
        {
            // null 繝ｻ・ｽz繝ｻ・ｽ繝ｻ・ｽ・肴圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.CopyTo(null!,0));
        }
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・後う繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽf繝ｻ・ｽb繝ｻ・ｽN繝ｻ・ｽX繝ｻ・ｽ・肴圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentOutOfRangeException>(() => h.Set.CopyTo(new T[2],-1));
        }
        {
            // 繝ｻ・ｽz繝ｻ・ｽ蘒ｽ・ｪ謫ｾ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            Assert.Throws<ArgumentException>(() => h.Set.CopyTo(new T[1],0));
        }
    }
    [BenchFact]
    public void ExceptWith() {
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・願ｦ√・・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ蟆樊・・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ鮠ｾ・ｷ繝ｻ・ｽW繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
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
            // null 繝ｻ・ｽ繝ｻ・ｽn繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.ExceptWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact]
    public void IntersectWith() {
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・願ｦ√・・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽc繝ｻ・ｽ鬩搾ｽｱ繝ｻ・ｽ繝ｻ・ｽ
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
            // null 繝ｻ・ｽ繝ｻ・ｽn繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.IntersectWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact,Benchmark]
    public void IsSubsetOf() {
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽW繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsSubsetOf(other.Set));
        }
        {
            // 繝ｻ・ｽ・懊∪繧托ｽｿ・ｽ・医ｑ・ｿ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ繝ｻ・ｽ・企寔繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsSupersetOf(other.Set));
        }
        {
            // 繝ｻ・ｽ・懊∪繧托ｽｿ・ｽ・医ｑ・ｿ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ^繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽW繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.IsProperSubsetOf(other.Set));
        }
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽT繝ｻ・ｽC繝ｻ・ｽY繝ｻ・ｽ・・ｑ・ｿ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ^繝ｻ・ｽ繝ｻ・ｽ・企寔繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.IsProperSupersetOf(other.Set));
        }
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽT繝ｻ・ｽC繝ｻ・ｽY繝ｻ・ｽ・・ｑ・ｿ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・願ｦ√・・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.Overlaps(other.Set));
        }
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・願ｦ√・・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ・医ｑ・ｿ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            using var other = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            h.Set.Add(TStrategy.Elem(2));
            other.Set.Add(TStrategy.Elem(1));
            other.Set.Add(TStrategy.Elem(2));
            Assert.True(h.Set.SetEquals(other.Set));
        }
        {
            // 繝ｻ・ｽ・吶↑繧托ｽｿ・ｽv繝ｻ・ｽf繝ｻ・ｽ・医ｑ・ｿ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・願ｦ√・・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・守ｧｰ謳ｾ・ｿ・ｽ繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
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
            // null 繝ｻ・ｽ繝ｻ・ｽn繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.Throws<ArgumentNullException>(() => h.Set.SymmetricExceptWith((global::Seamless.Tsavorite.Collections.HashSet<T>)null!));
        }
    }
    [BenchFact]
    public void UnionWith() {
        {
            // 繝ｻ・ｽa繝ｻ・ｽW繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
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
            // null 繝ｻ・ｽ繝ｻ・ｽn繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・圜・ｿ・ｽO繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・昴ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ・峨ｑ・ｿ・ｽ true 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・昴ｑ・ｿ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ・峨ｑ・ｿ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Contains(TStrategy.Elem(99)));
        }
    }
    [BenchFact,Benchmark]
    public void Remove() {
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・昴ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ・榊炎髯､繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ鬩搾ｽｱ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            h.Set.Add(TStrategy.Elem(1));
            Assert.True(h.Set.Remove(TStrategy.Elem(1)));
            Assert.False(h.Set.Contains(TStrategy.Elem(1)));
        }
        {
            // 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・昴ｑ・ｿ・ｽ繝ｻ・ｽ・医ｑ・ｿ・ｽ繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ繝ｻ・ｽ false 繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            using var h = TStrategy.CreateMemorySet();
            Assert.False(h.Set.Remove(TStrategy.Elem(99)));
        }
    }
    [BenchFact,Benchmark]
    public void Clear() {
        using var h = TStrategy.CreateMemorySet();
        h.Set.Add(TStrategy.Elem(1));
        {
            // Clear 繝ｻ・ｽ繝ｻ・ｽ・榊・・ｿ・ｽW繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・峨↑繧九％繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・斐ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            Assert.Equal(2,h.Set.Count);
        }
    }
    [BenchFact,Benchmark]
    public void GetEnumerator() {
        {
            // 繝ｻ・ｽW繝ｻ・ｽF繝ｻ・ｽl繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽb繝ｻ・ｽN繝ｻ・ｽ﨡・柏・・・繝ｻ・ｽv繝ｻ・ｽf繝ｻ・ｽ﨟先桾繝ｻ・ｽ繝ｻ・ｽ・・ｑ・ｿ・ｽ繝ｻ・ｽ鬩搾ｽｱ繝ｻ・ｽ繝ｻ・ｽ
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
            // 繝ｻ・ｽ繝ｻ・ｽW繝ｻ・ｽF繝ｻ・ｽl繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽb繝ｻ・ｽN繝ｻ・ｽ﨡・柏・・ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・ｑ・ｿ・ｽ繝ｻ・ｽ鬩搾ｽｱ繝ｻ・ｽ繝ｻ・ｽ
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
            // Dispose 繝ｻ・ｽ・榊桁・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・ｑ・ｿ・ｽ・・ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽS繝ｻ・ｽ・医ｑ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ
            Assert.False(set.IsDisposed);
            set.Dispose();
            Assert.True(set.IsDisposed);
            set.Dispose();
            h.Dispose();
        }
    }
}
