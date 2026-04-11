using Seamless.Stores;
namespace Test.Seamless.Collections;


public class HashSet {
    [Fact, Trait("Category","Unit")]
    public void IsReadOnly_偽を返す() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.False(set.IsReadOnly);
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo_正常() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);
        var array = new int[2];
        set.CopyTo(array,0);
        Assert.Equal(2,array.Length);
        Assert.Contains(1,array);
        Assert.Contains(2,array);
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo_null配列_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!,0));
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo_負のインデックス_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[2],-1));
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo_配列が小さすぎる_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[1],0));
    }
    [Fact, Trait("Category","Unit")]
    public void ExceptWith_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void ExceptWith_正常() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(2);
        set1.ExceptWith(set2);
        Assert.Equal(1,set1.GetLongCount());
        Assert.Contains(1,set1);
        Assert.DoesNotContain(2,set1);
    }
    [Fact, Trait("Category","Unit")]
    public void IntersectWith_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void IntersectWith_正常() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(2);
        set2.Add(3);
        set1.IntersectWith(set2);
        Assert.Contains(2,set1);
    }
    [Fact, Trait("Category","Unit")]
    public void UnionWith_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void UnionWith_正常() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        set1.UnionWith(set2);
        Assert.Equal(2,set1.GetLongCount());
        Assert.Contains(1,set1);
        Assert.Contains(2,set1);
    }
    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWith_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWith_正常() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(2);
        set2.Add(3);
        set1.SymmetricExceptWith(set2);
        Assert.DoesNotContain(2,set1);
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf_同数_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        Assert.False(set1.IsProperSubsetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf_真の部分集合_真() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(1);
        set2.Add(2);
        Assert.True(set1.IsProperSubsetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSubsetOf_共通要素なし_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        set2.Add(3);
        Assert.False(set1.IsProperSubsetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf_真の上位集合_真() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(1);
        Assert.True(set1.IsProperSupersetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf_同数_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        Assert.False(set1.IsProperSupersetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsProperSupersetOf_包含しない要素あり_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(3);
        Assert.False(set1.IsProperSupersetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf_部分集合_真() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(1);
        set2.Add(2);
        Assert.True(set1.IsSubsetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsSubsetOf_含まれない要素あり_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(99);
        set2.Add(1);
        Assert.False(set1.IsSubsetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf_上位集合_真() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(1);
        Assert.True(set1.IsSupersetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void IsSupersetOf_含まれない要素あり_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(1);
        set2.Add(99);
        Assert.False(set1.IsSupersetOf(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void Overlaps_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void Overlaps_共通要素あり_真() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(1);
        set2.Add(2);
        Assert.True(set1.Overlaps(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void Overlaps_共通要素なし_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        Assert.False(set1.Overlaps(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void SetEquals_null_例外() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
    }
    [Fact, Trait("Category","Unit")]
    public void SetEquals_異なる件数_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(1);
        set2.Add(2);
        Assert.False(set1.SetEquals(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void SetEquals_同一内容_真() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set1.Add(2);
        set2.Add(1);
        set2.Add(2);
        Assert.True(set1.SetEquals(set2));
    }
    [Fact, Trait("Category","Unit")]
    public void SetEquals_同数_要素不一致_偽() {
        using var store1 = Factory.Create().Create<int>();
        using var store2 = Factory.Create().Create<int>();
        using var set1 = store1.Create();
        using var set2 = store2.Create();
        set1.Add(1);
        set2.Add(2);
        Assert.False(set1.SetEquals(set2));
    }
    [Fact, Trait("Category","Unit")]public void Add() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.True(set.Add(1),"TryAddResult.Success");
        Assert.False(set.Add(1),"TryAddResult.KeyAlreadyExists");
        Assert.True(Record.Exception(() => {
            using var fullStore = Factory.Create(MutableFraction: 0.01).Create<int>();
            using var fullSet = fullStore.Create();
            for(var a=0;a<10000;a++){
                fullSet.Add(1);
            }
        }) is OutOfMemoryException,"TryAddResult.LogFull");
        //switch(Result) {
        //    case TryAddResult.Success:
        //        return true;
        //    case TryAddResult.KeyAlreadyExists:
        //        return false;
        //    default:
        //        Debug.Assert(TryAddResult.LogFull==Result);
        //        throw this.ThrowLogFull();
        //}
    }
    [Fact, Trait("Category","Unit")]public void ICollection_Add() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        ICollection<int> c = set;
        c.Add(42);
        Assert.Contains(42,set);
    }
    [Fact, Trait("Category","Unit")]
    public void Contains_存在する() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        Assert.True(set.Contains(1));
    }
    [Fact, Trait("Category","Unit")]
    public void Contains_存在しない() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.False(set.Contains(99));
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_存在する() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        Assert.True(set.Remove(1));
        Assert.False(set.Contains(1));
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_存在しない() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.False(set.Remove(99));
    }
    [Fact, Trait("Category","Unit")]
    public void Clear_後Count0() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);
        set.Clear();
        Assert.Equal(0,set.Count);
    }
    [Fact, Trait("Category","Unit")]
    public void GetEnumerator_IEnumerable_明示的() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(1);
        var count = 0;
        var en = ((System.Collections.IEnumerable)set).GetEnumerator();
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose_安全に呼び出せる() {
        var store = Factory.Create().Create<int>();
        var set = store.Create();
        Assert.False(set.IsDisposed);
        set.Dispose();
        Assert.True(set.IsDisposed);
        set.Dispose();
        store.Dispose();
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose_GCファイナライザ経路() {
        static void CreateOrphan() {
            var store = Factory.Create().Create<int>();
            _ = store.Create();
        }
        CreateOrphan();
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
    }
    }
