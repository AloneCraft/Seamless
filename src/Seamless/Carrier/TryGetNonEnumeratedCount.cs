namespace Seamless.Carrier;

partial class ValueEnumerableExtensions {
    public static bool TryGetNonEnumeratedCount<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,out int count)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        using(var enumerator = source.Enumerator) {
            return enumerator.TryGetNonEnumeratedCount(out count);
        }
    }
}
