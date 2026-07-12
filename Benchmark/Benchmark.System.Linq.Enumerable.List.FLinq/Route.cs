namespace Benchmark.System.Linq.Enumerable.List {
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqInt32():global::Seamless.Linq.Shared.List<int>(4_096);
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class FLinqClassInt32():global::Seamless.Linq.Shared.List<global::Common.ClassInt32>(4_096);
}

namespace Seamless.Linq.Shared {
    internal static class Route {
        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.List<T> source) => source;

        public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
    }
}
