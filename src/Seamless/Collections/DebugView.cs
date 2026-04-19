using System.Diagnostics;
namespace Seamless.Collections;

public class DebugView<TKey> {
    /// <summary>
    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
    /// </summary>
    private const int MaxItems = 10_000;
    private readonly IMemoryCollection<TKey> Collection;
    public DebugView(IMemoryCollection<TKey> collection) {
        ArgumentNullException.ThrowIfNull(collection);
        this.Collection=collection;
    }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public string[] Items {
        get {
            var items = new List<string>(Math.Min(64,MaxItems));
            // foreach(var item in this.Collection) {
            //     if(items.Count>=MaxItems)
            //         break;
            //     items.Add(item);
            // }
            items.Add("abc");
            return items.ToArray();
        }
    }
    // public TKey[] Items {
    //     get {
    //         var items = new List<TKey>(Math.Min(64,MaxItems));
    //         // foreach(var item in this.Collection) {
    //         //     if(items.Count>=MaxItems)
    //         //         break;
    //         //     items.Add(item);
    //         // }
    //         //items.Add(default!);
    //         return items.ToArray();
    //     }
    // }
}
public class DebugView<TKey, TValue>:DebugView<KeyValuePair<TKey,TValue>> {
    public DebugView(IMemoryCollection<KeyValuePair<TKey,TValue>> collection) : base(collection) {
    }
}
public class ManagedHashSetDebugView<TKey>:DebugView<TKey>
    where TKey : IEquatable<TKey> {
    public ManagedHashSetDebugView(IMemoryCollection<TKey> collection) : base(collection) {
    }
}
public class UnmanagedHashSetDebugView<TKey>:DebugView<TKey>
    where TKey : unmanaged, IEquatable<TKey> {
    public UnmanagedHashSetDebugView(IMemoryCollection<TKey> collection) : base(collection) {
    }
}
/// <summary>
/// DebuggerTypeProxy の型引数数を Dictionary の 4 型引数に合わせるためのラッパー。
/// デバッガーはオープンジェネリック型を使う際に型引数数が一致している必要がある。
/// </summary>
public class DictionaryDebugView<TKey, TValue, TKeySerializer, TValueSerializer>:DebugView<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public DictionaryDebugView(IMemoryCollection<KeyValuePair<TKey,TValue>> collection) : base(collection) {
    }
}
