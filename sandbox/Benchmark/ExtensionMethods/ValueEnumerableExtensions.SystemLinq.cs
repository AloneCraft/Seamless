namespace Benchmark.ZLinq;

#if USE_SYSTEM_LINQ
// Define methods to replace ZLinq's method to System.Linq.
public static partial class ValueEnumerable
{
    public static TSource AsValueEnumerable<TSource, T>(this TSource source)
        where TSource : IEnumerable<T>
    {
        return source;
    }
}
#endif
