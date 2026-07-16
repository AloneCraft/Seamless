namespace Seamless.Linq;

public static partial class Enumerable {
    [return: UnorderedEnumerable]
    public static ValueEnumerable<Internal.PersistedSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Persisted.Collections.Set<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<Internal.TsavoriteHashSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Tsavorite.Collections.HashSet<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<Internal.TsavoriteSetEnumerator<TKey,TRecord>,TRecord> AsValueEnumerable<TKey,TRecord>(
        this global::Seamless.Tsavorite.Collections.Set<TKey,TRecord> source)
        where TRecord : global::Seamless.Collections.IRecord<TKey> =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));
}
