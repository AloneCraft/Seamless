using System.Collections;
namespace Test.Seamless.Collections;

/// <summary>
/// <see cref="IDictionary{TKey,TValue}"/> インターフェース経由でテストする汎用ベースクラス。
/// <typeparamref name="TStrategy"/> の静的ポリモーフィズムにより具体型を差し替える。
/// <para>
/// <c>TryAdd</c>・<c>Remove(key, out value)</c>・<c>EntryCount</c> など
/// <c>ISeamlessDictionary</c> 固有メソッドは使用しない。
/// </para>
/// </summary>
public abstract class IDictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    // ── 基本操作 ──────────────────────────────────────────────────

    [Fact, Trait("Category","Unit")]
    public void IsReadOnly_偽を返す() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        Assert.False(dict.IsReadOnly);
    }

    [Fact, Trait("Category","Unit")]
    public void Add_新規キー_追加される() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        Assert.True(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Add_重複キー_例外を投げる() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1),TStrategy.V(2)));
    }

    [Fact, Trait("Category","Unit")]
    public void Add_KVP_追加される() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        col.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(5),TStrategy.V(5)));
        Assert.True(((IDictionary<TKey,TValue>)col).ContainsKey(TStrategy.K(5)));
    }

    [Fact, Trait("Category","Unit")]
    public void TryGetValue_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(3),TStrategy.V(3));
        Assert.True(dict.TryGetValue(TStrategy.K(3),out var val));
        Assert.Equal(TStrategy.V(3),val);
    }

    [Fact, Trait("Category","Unit")]
    public void TryGetValue_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        Assert.False(dict.TryGetValue(TStrategy.K(0),out var val));
        Assert.Equal(default,val);
    }

    [Fact, Trait("Category","Unit")]
    public void Indexer_Setter_値を設定する() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict[TStrategy.K(10)]=TStrategy.V(10);
        Assert.True(dict.TryGetValue(TStrategy.K(10),out var v));
        Assert.Equal(TStrategy.V(10),v);
    }

    [Fact, Trait("Category","Unit")]
    public void Indexer_Getter_存在しないキー_例外() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        Assert.Throws<KeyNotFoundException>(() => { var _ = dict[TStrategy.K(999)]; });
    }

    [Fact, Trait("Category","Unit")]
    public void Indexer_Setter_既存値を上書きする() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict[TStrategy.K(1)]=TStrategy.V(1);
        dict[TStrategy.K(1)]=TStrategy.V(2);
        Assert.Equal(TStrategy.V(2),dict[TStrategy.K(1)]);
    }

    [Fact, Trait("Category","Unit")]
    public void ContainsKey_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        Assert.True(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void ContainsKey_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        Assert.False(dict.ContainsKey(TStrategy.K(999)));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_KVP_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        col.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
        Assert.True(col.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_KVP_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        Assert.False(col.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong())));
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_キー_存在する() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        Assert.True(dict.Remove(TStrategy.K(1)));
        Assert.False(dict.ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_キー_存在しない() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        Assert.False(dict.Remove(TStrategy.K(99)));
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_KVP_値一致_削除成功() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        col.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
        Assert.True(col.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
        Assert.False(((IDictionary<TKey,TValue>)col).ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_KVP_値不一致_削除しない() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        col.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
        Assert.False(col.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.VWrong())));
        Assert.True(((IDictionary<TKey,TValue>)col).ContainsKey(TStrategy.K(1)));
    }

    [Fact, Trait("Category","Unit")]
    public void Count_件数を返す() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        Assert.Equal(2,dict.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void Clear_後Count0() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Clear();
        Assert.Equal(0,dict.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void Keys_追加した全キーを含む() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        Assert.Contains(TStrategy.K(1),dict.Keys);
        Assert.Contains(TStrategy.K(2),dict.Keys);
    }

    [Fact, Trait("Category","Unit")]
    public void Values_追加した全値を含む() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        Assert.Contains(TStrategy.V(1),dict.Values);
        Assert.Contains(TStrategy.V(2),dict.Values);
    }

    [Fact, Trait("Category","Unit")]
    public void CopyTo_正常() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        col.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
        col.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(2),TStrategy.V(2)));
        var array = new KeyValuePair<TKey,TValue>[2];
        col.CopyTo(array,0);
        Assert.Equal(2,array.Length);
    }

    [Fact, Trait("Category","Unit")]
    public void CopyTo_null配列_例外() {
        using var sh = TStrategy.CreateMemoryStore();
        ICollection<KeyValuePair<TKey,TValue>> col = sh.NewDict();
        Assert.Throws<ArgumentNullException>(() => col.CopyTo(null!,0));
    }

    [Fact, Trait("Category","Unit")]
    public void GetEnumerator_全エントリを列挙できる() {
        using var sh = TStrategy.CreateMemoryStore();
        IDictionary<TKey,TValue> dict = sh.NewDict();
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        var list = new List<KeyValuePair<TKey,TValue>>();
        foreach(var kv in dict)
            list.Add(kv);
        Assert.Equal(2,list.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void IEnumerable_GetEnumerator_全エントリを列挙できる() {
        using var sh = TStrategy.CreateMemoryStore();
        IEnumerable dict = sh.NewDict();
        var count = 0;
        ((IDictionary<TKey,TValue>)dict).Add(TStrategy.K(1),TStrategy.V(1));
        foreach(var _ in dict)
            count++;
        Assert.Equal(1,count);
    }
}
