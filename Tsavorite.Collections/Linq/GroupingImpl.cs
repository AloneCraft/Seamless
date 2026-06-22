namespace Seamless.Collections.Tsavorite.Linq;

public sealed class GroupingImpl<TKey,TElement>(TKey key):IGrouping<TKey,TElement> {
    private readonly List<TElement> elements=[];

    public TKey Key { get; } = key; public void Add(TElement element) { this.elements.Add(item: element); }
    public IEnumerator<TElement> GetEnumerator() => this.elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
