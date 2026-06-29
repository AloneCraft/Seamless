using SystemCollectionsGeneric = System.Collections.Generic;

namespace Seamless.Tsavorite.Collections.Generic;

public sealed class IEnumerable<T>:SystemCollectionsGeneric.IEnumerable<T> {
    private readonly SystemCollectionsGeneric.IEnumerable<T> source;

    public IEnumerable(SystemCollectionsGeneric.IEnumerable<T> source) {
        ArgumentNullException.ThrowIfNull(source);
        this.source=source;
    }

    public static implicit operator IEnumerable<T>(T[] source) => new(source);
    public static implicit operator IEnumerable<T>(List<T> source) => new(source);
    public static implicit operator IEnumerable<T>(SerializedList<T> source) => new(source);

    public IEnumerator<T> GetEnumerator() => this.source.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
