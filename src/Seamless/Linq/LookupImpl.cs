namespace Seamless.Linq;
public sealed class LookupImpl<TKey,TElement>(IEqualityComparer<TKey>? Comparer):ILookup<TKey,TElement> {
    public LookupImpl():this(Comparer: EqualityComparer<TKey>.Default) { }
    public List<GroupingImpl<TKey,TElement>> Groups { get; }=new();
    public int Count => this.Groups.Count;
    public IEnumerable<TElement> this[TKey key] {
        get {
            var group= this.Find(key: key);
            return group is null ? System.Array.Empty<TElement>() : group;
        }
    }
    public bool Contains(TKey key) => this.Find(key: key) is not null;
    public void Add(TKey key,TElement element) {
        var group= this.Find(key: key);
        if(group is null) {
            group=new GroupingImpl<TKey,TElement>(key: key);
            this.Groups.Add(item: group);
        }
        group.Add(element: element);
    }
    public IEnumerator<IGrouping<TKey,TElement>> GetEnumerator() {
        var result=new List<IGrouping<TKey,TElement>>();
        foreach(var group in this.Groups) result.Add(item: group);
        return result.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    private GroupingImpl<TKey,TElement>? Find(TKey key) {
        foreach(var group in this.Groups) {
            if(Comparer.Equals(x: group.Key,y: key)) return group;
        }
        return null;
    }
}
