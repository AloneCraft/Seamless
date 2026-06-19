namespace Tsavorite.Collections;

public abstract class DebugView<TKey> {
    /// <summary>
    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
    /// </summary>
    private const int MaxItems = 10_000;
    private readonly IMemoryCollection<TKey> Collection;
    private protected DebugView(IMemoryCollection<TKey> collection) {
        ArgumentNullException.ThrowIfNull(collection);
        this.Collection=collection;
    }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public string[] Items {
        get {
            var items = new global::System.Collections.Generic.List<string?>(Math.Min(64,MaxItems));
            foreach(var item in this.Collection) {
                if(items.Count>=MaxItems)
                    break;
                Debug.Assert(item!=null,nameof(item)+" != null");
                items.Add(item.ToString());
            }
            //items.Add("abc");
            return items.ToArray()!;
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
public abstract class DebugView<TKey, TValue>:DebugView<KeyValuePair<TKey,TValue>> {
    private protected DebugView(IMemoryCollection<KeyValuePair<TKey,TValue>> collection) : base(collection) {
    }
}
public class HashSetDebugView<TKey>(IMemoryCollection<TKey> collection):DebugView<TKey>(collection) {
}
/// <summary>
/// DebuggerTypeProxy の型引数数を Dictionary の 4 型引数に合わせるためのラッパー。
/// デバッガーはオープンジェネリック型を使う際に型引数数が一致している必要がある。
/// </summary>
public class DictionaryDebugView<TKey, TValue, TKeySerializer, TValueSerializer>(IMemoryCollection<KeyValuePair<TKey,TValue>> collection):DebugView<TKey,TValue>(collection)    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
}
