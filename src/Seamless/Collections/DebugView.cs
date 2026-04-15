using System.Diagnostics;
namespace Seamless.Collections;

public class DebugView<TKey> {
    /// <summary>
    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
    /// </summary>
    private const int MaxItems=10_000;
    private readonly ICollection<TKey> _set;
    public DebugView(ICollection<TKey> set){
        ArgumentNullException.ThrowIfNull(set);
        _set=set;
    }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public TKey[] Items {
        get {
            var count=Math.Min(_set.Count,MaxItems);
            var items=new TKey[count];
            var i=0;
            foreach(var item in _set){
                if(i>=count) break;
                items[i++]=item;
            }
            return items;
        }
    }
}
public class DebugView<TKey, TValue>:DebugView<KeyValuePair<TKey,TValue>> {
    public DebugView(ICollection<KeyValuePair<TKey,TValue>> set):base(set){
    }
}
