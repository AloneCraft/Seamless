namespace Seamless.Linq;
public sealed class LookupImpl<TKey,TElement>(IEqualityComparer<TKey>? Comparer):ILookup<TKey,TElement> {
    public LookupImpl():this(Comparer: EqualityComparer<TKey>.Default) { }
    public List<GroupingImpl<TKey,TElement>> Groups { get; }=new();
    public int Count => Groups.Count;
    public IEnumerable<TElement> this[TKey key] {
        get {
            var group=Find(key: key);
            return group is null ? System.Array.Empty<TElement>() : group;
        }
    }
    public bool Contains(TKey key) => Find(key: key) is not null;
    public void Add(TKey key,TElement element) {
        var group=Find(key: key);
        if(group is null) {
            group=new GroupingImpl<TKey,TElement>(key: key);
            Groups.Add(item: group);
        }
        group.Add(element: element);
    }
    public IEnumerator<IGrouping<TKey,TElement>> GetEnumerator() {
        var result=new List<IGrouping<TKey,TElement>>();
        foreach(var group in Groups) result.Add(item: group);
        return result.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    private GroupingImpl<TKey,TElement>? Find(TKey key) {
        foreach(var group in Groups) {
            if(Comparer.Equals(x: group.Key,y: key)) return group;
        }
        return null;
    }
}
