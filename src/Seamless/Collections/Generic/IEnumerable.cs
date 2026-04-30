using System;
using SystemCollections = System.Collections;
using SystemCollectionsGeneric = System.Collections.Generic;

namespace Seamless.Collections.Generic;

public sealed class IEnumerable<T>:SystemCollectionsGeneric.IEnumerable<T> {
    private readonly SystemCollectionsGeneric.IEnumerable<T> source;

    public IEnumerable(SystemCollectionsGeneric.IEnumerable<T> source) {
        ArgumentNullException.ThrowIfNull(source);
        this.source=source;
    }

    public static implicit operator IEnumerable<T>(T[] source) => new(source);
    public static implicit operator IEnumerable<T>(SystemCollectionsGeneric.List<T> source) => new(source);
    public static implicit operator IEnumerable<T>(global::Seamless.Collections.List<T> source) => new(source);

    public SystemCollectionsGeneric.IEnumerator<T> GetEnumerator() => this.source.GetEnumerator();

    SystemCollections.IEnumerator SystemCollections.IEnumerable.GetEnumerator() => this.GetEnumerator();
}
