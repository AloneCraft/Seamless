namespace Seamless.Linq;

public sealed class GroupingImpl<TKey,TElement>:IGrouping<TKey,TElement> {
    private readonly List<TElement> elements=new();
    public GroupingImpl(TKey key) { Key=key; }
    public TKey Key { get; }
    public void Add(TElement element) { elements.Add(item: element); }
    public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
