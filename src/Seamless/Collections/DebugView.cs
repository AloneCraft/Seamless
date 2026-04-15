using System.Diagnostics;
namespace Seamless.Collections;

public class DebugView<TKey> {
    /// <summary>
    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
    /// </summary>
    private const int MaxItems=10_000;
    private readonly ICollection<TKey> Collection;
    public DebugView(ICollection<TKey> set){
        ArgumentNullException.ThrowIfNull(set);
        this.Collection=set;
    }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public TKey[] Items {
        get {
            var Count=Math.Min(this.Collection.Count,MaxItems);
            var items=new TKey[Count];
            var i=0;
            foreach(var item in this.Collection){
                if(i>=Count)
                    break;
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
