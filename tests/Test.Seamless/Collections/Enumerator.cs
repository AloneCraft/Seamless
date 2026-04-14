using System.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;


public class Enumerator{
    // Enumerator.MoveNext: 空コレクション → false
    [Fact,Trait("Category","Unit")]
    public void MoveNext_空コレクション(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        using var e=dict.GetEnumerator();
        Assert.False(e.MoveNext());
    }
    // Enumerator.MoveNext: Chunk_Length=2 要素でチャンク内パス (Index < Count)
    [Fact,Trait("Category","Unit")]
    public void MoveNext_チャンク内パス_2要素(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var count=0;
        foreach(var _ in dict) count++;
        Assert.Equal(2,count);
    }
    // Enumerator.MoveNext: 3要素で複数チャンク (1チャンク目2件+2チャンク目1件)
    [Fact,Trait("Category","Unit")]
    public void MoveNext_複数チャンク_3要素(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        dict.Add(3,"c");
        var count=0;
        foreach(var _ in dict) count++;
        Assert.Equal(3,count);
    }
    // Enumerator.Current の値確認
    [Fact,Trait("Category","Unit")]
    public void Current_値が正しい(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        dict.Add(42,"hello");
        using var e=dict.GetEnumerator();
        Assert.True(e.MoveNext());
        var kvp=e.Current;
        Assert.Equal(42,kvp.Key);
        Assert.Equal("hello",kvp.Value);
    }
    // IEnumerator.Current (object 版)
    [Fact,Trait("Category","Unit")]
    public void Current_IEnumerator_オブジェクト版(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        dict.Add(7,"x");
        using var e=dict.GetEnumerator();
        Assert.True(e.MoveNext());
        IEnumerator nonGeneric=e;
        Assert.NotNull(nonGeneric.Current);
    }
    // Enumerator.Reset 後に先頭から再列挙できる
    [Fact,Trait("Category","Unit")]
    public void Reset_先頭から再列挙(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        using var e=dict.GetEnumerator();
        Assert.True(e.MoveNext());
        e.Reset();
        var count=0;
        while(e.MoveNext()) count++;
        Assert.Equal(2,count);
    }
    // Enumerator.Dispose は安全に呼び出せる
    [Fact,Trait("Category","Unit")]
    public void Dispose_安全(){
        using var store=Factory.Create().Create<int,string>();
        using var dict=store.Create();
        dict.Add(1,"a");
        var e=dict.GetEnumerator();
        e.MoveNext();
        e.Dispose();
    }
    // HashSet による KeyEnumerable struct GetEnumerator() パス
    [Fact,Trait("Category","Unit")]
    public void MoveNext_HashSet_複数チャンク_3要素(){
        using var store=Factory.Create().Create<int>();
        using var set=store.Create();
        set.Add(1);
        set.Add(2);
        set.Add(3);
        var count=0;
        foreach(var _ in set) count++;
        Assert.Equal(3,count);
    }
}
