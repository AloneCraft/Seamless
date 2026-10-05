namespace Seamless.Linq;

public static partial class Enumerable {
    [return: UnorderedEnumerable]
    public static ValueEnumerable<PersistedSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Collections.Persisted.Set<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteHashSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Collections.Tsavorite.HashSet<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteSetEnumerator<TKey,TRecord>,TRecord> AsValueEnumerable<TKey,TRecord>(
        this global::Seamless.Collections.Tsavorite.Set<TKey,TRecord> source)
        where TRecord : global::Seamless.Collections.IRecord<TKey> =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteParallelHashSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Collections.Tsavorite.ParallelHashSet<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteParallelSetEnumerator<TKey,TRecord>,TRecord> AsValueEnumerable<TKey,TRecord>(
        this global::Seamless.Collections.Tsavorite.ParallelSet<TKey,TRecord> source)
        where TRecord : global::Seamless.Collections.IRecord<TKey> =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));
}
