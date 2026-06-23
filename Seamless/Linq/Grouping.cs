namespace Seamless.Linq;

public sealed class Grouping<TKey,TElement>(TKey key):IGrouping<TKey,TElement> {
    private readonly List<TElement> elements=[];

    public TKey Key { get; } = key; public void Add(TElement element) { elements.Add(item: element); }
    public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
