using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

// MaxItems 切り詰めテスト用の共通定数
file static class MaxItemsSettings {
    internal const int Total = 10_001;
    internal const int MaxItems = 10_000;
    internal const int IndexSize = 1 << 14;
    internal const long MemorySize = global::Seamless.Const.LogMemorySize << 10;
}

/// <summary><see cref="Seamless.Collections.DebugView.HashSet{TKey}"/> のテスト。</summary>
public class ManagedHashSetDebugView {
    [Fact, Trait("Category","Unit")]
    public void Items_全エントリを返す() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(10);
        set.Add(20);
        var view = new DebugView.HashSet<int>(set);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(10,items);
        Assert.Contains(20,items);
    }

    [Fact, Trait("Category","Unit")]
    public void ctor_null_例外() {
        Assert.Throws<ArgumentNullException>(() => new DebugView.HashSet<int>(null!));
    }

    [Fact, Trait("Category","Unit")]
    public void Items_MaxItems超過時に最大件数に切り詰める() {
        using var store = Factory.Create(IndexSize:MaxItemsSettings.IndexSize,MemorySize:MaxItemsSettings.MemorySize).Create<int>();
        using var set = store.Create();
        for(var i = 0; i < MaxItemsSettings.Total; i++) set.Add(i);
        var view = new DebugView.HashSet<int>(set);
        Assert.Equal(MaxItemsSettings.MaxItems,view.Items.Length);
    }
}

/// <summary><see cref="Seamless.Collections.DebugView.UnmanagedDictionary{TKey,TValue,TKeySerializer,TValueSerializer}"/> のテスト。</summary>
public class UnmanagedDictionaryDebugView {
    [Fact, Trait("Category","Unit")]
    public void Items_全エントリを返す() {
        using var store = Factory.Create().Create<int,int>();
        using var dict = store.Create();
        dict.Add(1,100);
        dict.Add(2,200);
        var view = new DebugView.UnmanagedDictionary<int,int,UnmanagedSerializer<int>,UnmanagedSerializer<int>>(dict);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(items,x => x.Key==1&&x.Value==100);
        Assert.Contains(items,x => x.Key==2&&x.Value==200);
    }

    [Fact, Trait("Category","Unit")]
    public void ctor_null_例外() {
        Assert.Throws<ArgumentNullException>(() => new DebugView.UnmanagedDictionary<int,int,UnmanagedSerializer<int>,UnmanagedSerializer<int>>(null!));
    }

    [Fact, Trait("Category","Unit")]
    public void Items_MaxItems超過時に最大件数に切り詰める() {
        using var store = Factory.Create(IndexSize:MaxItemsSettings.IndexSize,MemorySize:MaxItemsSettings.MemorySize).Create<int,int>();
        using var dict = store.Create();
        for(var i = 0; i < MaxItemsSettings.Total; i++) dict.Add(i,i);
        var view = new DebugView.UnmanagedDictionary<int,int,UnmanagedSerializer<int>,UnmanagedSerializer<int>>(dict);
        Assert.Equal(MaxItemsSettings.MaxItems,view.Items.Length);
    }
}

/// <summary><see cref="Seamless.Collections.DebugView.UnmanagedHashSet{TKey}"/> の MaxItems 切り詰めテスト。</summary>
public class UnmanagedHashSetDebugViewMaxItems {
    [Fact, Trait("Category","Unit")]
    public void Items_MaxItems超過時に最大件数に切り詰める() {
        using var store = Factory.Create(IndexSize:MaxItemsSettings.IndexSize,MemorySize:MaxItemsSettings.MemorySize).CreateUnmanagedHashSet<int>();
        using var set = store.Create();
        for(var i = 0; i < MaxItemsSettings.Total; i++) set.Add(i);
        var view = new DebugView.UnmanagedHashSet<int>(set);
        Assert.Equal(MaxItemsSettings.MaxItems,view.Items.Length);
    }
}

/// <summary><see cref="Seamless.Collections.DebugView.Dictionary{TKey,TValue,TKeySerializer,TValueSerializer}"/> の MaxItems 切り詰めテスト。</summary>
public class DictionaryDebugViewMaxItems {
    [Fact, Trait("Category","Unit")]
    public void Items_MaxItems超過時に最大件数に切り詰める() {
        using var store = Factory.Create(IndexSize:MaxItemsSettings.IndexSize,MemorySize:MaxItemsSettings.MemorySize).Create<int,string>();
        using var dict = store.Create();
        for(var i = 0; i < MaxItemsSettings.Total; i++) dict.Add(i,i.ToString());
        var view = new DebugView.Dictionary<int,string,UnmanagedSerializer<int>,ManagedSerializer<string>>(dict);
        Assert.Equal(MaxItemsSettings.MaxItems,view.Items.Length);
    }
}
