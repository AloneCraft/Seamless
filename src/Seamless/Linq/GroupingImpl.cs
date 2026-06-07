namespace Seamless.Linq;

public sealed class GroupingImpl<TKey,TElement>:IGrouping<TKey,TElement> {
    private readonly List<TElement> elements=new();
    public GroupingImpl(TKey key) { this.Key=key; }
    public TKey Key { get; }
    public void Add(TElement element) { this.elements.Add(item: element); }
    public IEnumerator<TElement> GetEnumerator() => this.elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
