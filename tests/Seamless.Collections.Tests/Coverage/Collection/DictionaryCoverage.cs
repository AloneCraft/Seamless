using System.Collections;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public partial class Dictionary {
    [Fact, Trait("Category","Coverage")]
    public void IsReadOnly_偽を返す() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.False(dict.IsReadOnly);
    }
    [Fact, Trait("Category","Coverage")]
    public void TryAdd_新規キー_Successを返す() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        var result = dict.TryAdd(1,"a");
        Assert.Equal(TryAddResult.Success,result);
    }
    [Fact, Trait("Category","Coverage")]
    public void TryAdd_重複キー_KeyAlreadyExistsを返す() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var result = dict.TryAdd(1,"b");
        Assert.Equal(TryAddResult.KeyAlreadyExists,result);
    }
    [Fact, Trait("Category","Coverage")]
    public void Add_KVP_正常追加() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(new KeyValuePair<int,string>(5,"v"));
        Assert.True(dict.ContainsKey(5));
    }
    [Fact, Trait("Category","Coverage")]
    public void Add_重複キー_例外を投げる() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Throws<ArgumentException>(() => dict.Add(1,"b"));
    }
    [Fact, Trait("Category","Coverage")]
    public void Contains_KVP_存在する() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        ICollection<KeyValuePair<int,string>> c = dict;
        Assert.True(c.Contains(new KeyValuePair<int,string>(1,"a")));
    }
    [Fact, Trait("Category","Coverage")]
    public void Contains_KVP_存在しない() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        ICollection<KeyValuePair<int,string>> c = dict;
        Assert.False(c.Contains(new KeyValuePair<int,string>(99,"x")));
    }
    [Fact, Trait("Category","Coverage")]
    public void ContainsKey_存在する() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.ContainsKey(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void ContainsKey_存在しない() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.False(dict.ContainsKey(999));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_KVP_値一致_削除成功() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var removed = dict.Remove(new KeyValuePair<int,string>(1,"a"));
        Assert.True(removed);
        Assert.False(dict.ContainsKey(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_KVP_値不一致_削除しない() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var removed = dict.Remove(new KeyValuePair<int,string>(1,"wrong"));
        Assert.False(removed);
        Assert.True(dict.ContainsKey(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_KVP_キー不存在_偽を返す() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        var removed = dict.Remove(new KeyValuePair<int,string>(99,"x"));
        Assert.False(removed);
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_out_存在する() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var removed = dict.Remove(1,out var value);
        Assert.True(removed);
        Assert.Equal("a",value);
        Assert.False(dict.ContainsKey(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_out_存在しない() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        var removed = dict.Remove(99,out var value);
        Assert.False(removed);
        Assert.Null(value);
    }
    [Fact, Trait("Category","Coverage")]
    public void CopyTo_正常() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var array = new KeyValuePair<int,string>[2];
        dict.CopyTo(array,0);
        Assert.Equal(2,array.Length);
    }
    [Fact, Trait("Category","Coverage")]
    public void CopyTo_null配列_例外() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
    }
    [Fact, Trait("Category","Coverage")]
    public void CopyTo_負のインデックス_例外() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<int,string>[2],-1));
    }
    [Fact, Trait("Category","Coverage")]
    public void CopyTo_配列が小さすぎる_例外() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<int,string>[1],0));
    }
    [Fact, Trait("Category","Coverage")]
    public void ToList_全エントリを返す() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var list = dict.ToList();
        Assert.Equal(2,list.Count);
    }
    [Fact, Trait("Category","Coverage")]
    public void Item_Setter_値を設定する() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict[10]="ten";
        Assert.True(dict.TryGetValue(10,out var v));
        Assert.Equal("ten",v);
    }
    [Fact, Trait("Category","Coverage")]
    public void Item_Getter_存在しないキー_例外() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.Throws<KeyNotFoundException>(() => { var _ = dict[999]; });
    }
    [Fact, Trait("Category","Coverage")]
    public void TryGetValue_存在する() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(3,"c");
        Assert.True(dict.TryGetValue(3,out var val));
        Assert.Equal("c",val);
    }
    [Fact, Trait("Category","Coverage")]
    public void TryGetValue_存在しない() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.False(dict.TryGetValue(0,out var val));
        Assert.Null(val);
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_存在する() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.Remove(1));
        Assert.False(dict.ContainsKey(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_存在しない() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.False(dict.Remove(99));
    }
    [Fact, Trait("Category","Coverage")]
    public void Clear_後Count0() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        dict.Clear();
        Assert.Equal(0,dict.Count);
    }
    [Fact, Trait("Category","Coverage")]
    public void Count_件数を返す() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Equal(2,dict.Count);
    }
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerableT() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        IEnumerable<KeyValuePair<int,string>> e = dict;
        Assert.Equal(1,e.Count());
    }
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerable() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        IEnumerable e = dict;
        var count = 0;
        var en = e.GetEnumerator();
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
    }
