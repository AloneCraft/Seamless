using Microsoft.CodeAnalysis;

using Seamless.FLinq;
namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class PerformanceShapeTests(ITestOutputHelper output) {
    [Fact]
    public void ModuloKeyedGroupingUsesGeneralInputCapacity() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<KeyValuePair<int,int>> AggregateByMod(int[] src) =>
                    src.AggregateBy(static x => x%1000,0,static (acc,x) => acc+x).ToList();
                private static List<KeyValuePair<int,int>> CountByMod(int[] src) =>
                    src.CountBy(static x => x%1000).ToList();
                private static ILookup<int,int> ToLookupMod(int[] src) =>
                    src.ToLookup(static x => x%1000);
                public void Run(int[] src) {
                    _ = AggregateByMod(src);
                    _ = CountByMod(src);
                    _ = ToLookupMod(src);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            !generated.Contains("global::System.Math.Min(__span.Length,1999)",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Math.Min(__count,1999)",StringComparison.Ordinal)
            &&generated.Contains("var __capacity=__hasSpan",StringComparison.Ordinal)
            &&generated.Contains("new global::Seamless.Linq.LookupBuilder<int,int>(__capacity,null)",StringComparison.Ordinal)
            &&generated.Contains("DictionarySlim<int,int>(global::System.Math.Min(__span.Length,1024))",StringComparison.Ordinal)
            &&generated.Contains("DictionarySlim<int,int>(global::System.Math.Min(__count,1024))",StringComparison.Ordinal),
            "Modulo key selectors must use the same input-derived capacity as general selectors.",
            generated,
            output);
    }

    [Fact]
    public void ModuloKeyedGroupingConsumeUsesGeneralPaths() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void GroupingFast(int[] src, Consumer<KeyValuePair<int,int>> consumer) {
                    src.AggregateBy(static x => x%1000,0,static (acc,x) => acc+x).Consume(consumer);
                    src.CountBy(static x => x%1000).Consume(consumer);
                }
                public void Run(int[] src, Consumer<KeyValuePair<int,int>> consumer) => GroupingFast(src,consumer);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"GroupingFast");

        GeneratedSourceAssert.True(
            !method.Contains("__aggregateBySeen",StringComparison.Ordinal)
            &&!method.Contains("__countBySeen",StringComparison.Ordinal)
            &&!method.Contains("global::System.Buffers.ArrayPool<bool>.Shared.Rent(1999)",StringComparison.Ordinal)
            &&generated.Contains("TryGetSpan(out var __span)",StringComparison.Ordinal)
            &&generated.Contains("new global::Seamless.Linq.Internal.DictionarySlim<int,int>",StringComparison.Ordinal)
            &&generated.Contains("__hashKeys",StringComparison.Ordinal),
            "Modulo selectors must not select a dedicated small-range consume implementation.",
            generated,
            output);
    }

    [Fact]
    public void MethodNamedModDoesNotImplyIntegralRange() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T>{
                public void Consume(T item){
                }
            }
            public static class ConsumeSupport{
                public static void Consume<T>(this IEnumerable<T>source,Consumer<T>consumer){
                    foreach(var item in source){
                        consumer.Consume(item);
                    }
                }
            }
            public class Container{
                private static int Mod(int value,int divisor)=>value+100000;
                private static void Group(int[]source,Consumer<IGrouping<int,int>>consumer)=>
                    source.GroupBy(static value=>Mod(value,1000)).Consume(consumer);
                public void Run(int[]source,Consumer<IGrouping<int,int>>consumer)=>Group(source,consumer);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Group");

        GeneratedSourceAssert.True(
            !method.Contains("__groupSlots",StringComparison.Ordinal)
            &&generated.Contains("LookupBuilder<int",StringComparison.Ordinal),
            "A method name alone must not be treated as a modulo range proof.",
            generated,
            output);
    }

    [Fact]
    public void IntCountByEnumeratorUsesPooledInsertionOrderedMap() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<KeyValuePair<int,int>> Count(int[] src) =>
                    src.CountBy(static x => x).ToList();
                public void Run(int[] src) => _ = Count(src);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Count");

        GeneratedSourceAssert.True(
            generated.Contains("__hashKeys",StringComparison.Ordinal)
            &&generated.Contains("ArrayPool<int>.Shared.Rent",StringComparison.Ordinal)
            &&generated.Contains("__hashOrder",StringComparison.Ordinal)
            &&!method.Contains("new global::System.Collections.Generic.Dictionary<int, int>",StringComparison.Ordinal),
            "Int CountBy enumeration should use a pooled insertion-ordered hash map.",
            generated,
            output);
    }

    [Fact]
    public void DistinctByModuloUsesGeneralHashConsumePath() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void DistinctByMod(int[] src, Consumer<int> consumer) =>
                    src.DistinctBy(static x => x%1000).Consume(consumer);
                public void Run(int[] src, Consumer<int> consumer) => DistinctByMod(src, consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            !generated.Contains("ArrayPool<bool>.Shared.Rent(1999)",StringComparison.Ordinal)
            &&!generated.Contains("ArrayPool<bool>.Shared.Return",StringComparison.Ordinal)
            &&!generated.Contains("__seenIndex",StringComparison.Ordinal)
            &&generated.Contains(".Consume(value)",StringComparison.Ordinal)
            &&generated.Contains("HashSetSlim<int>",StringComparison.Ordinal),
            "DistinctBy modulo keys must use the general hash-based consume path.",
            generated,
            output);
    }

    [Fact]
    public void DefaultIfEmptyPreservesNonEmptySpanFastPath() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<int> DefaultIfEmptyFast(int[] src) =>
                    src.DefaultIfEmpty(0).ToList();
                public void Run(int[] src) => _ = DefaultIfEmptyFast(src);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("TryGetSpan(out span)&&span.Length!=0",StringComparison.Ordinal)
            &&generated.Contains("__span.Slice(__spanStart,destination.Length).CopyTo(destination);",StringComparison.Ordinal)
            &&generated.Contains("destination[0]=0;",StringComparison.Ordinal),
            "DefaultIfEmpty should expose/copy the source span when non-empty and only synthesize default for empty sources.",
            generated,
            output);
    }

    [Fact]
    public void ExplicitDefaultComparersUseSortedSpanFastPaths() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void DistinctFast(int[] src, Consumer<int> consumer) =>
                    src.Distinct(EqualityComparer<int>.Default).Consume(consumer);
                private static void ExceptFast(int[] src,int[] second, Consumer<int> consumer) =>
                    src.Except(second,EqualityComparer<int>.Default).Consume(consumer);
                private static List<int> OrderFast(int[] src) =>
                    src.OrderBy(static x => x,Comparer<int>.Default).ToList();
                public void Run(int[] src,int[] second, Consumer<int> consumer) {
                    DistinctFast(src,consumer);
                    ExceptFast(src,second,consumer);
                    _ = OrderFast(src);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("__distinctSpan",StringComparison.Ordinal)
            &&!generated.Contains("__setSrcSpan",StringComparison.Ordinal)
            &&generated.Contains("__sp___sec0",StringComparison.Ordinal)
            &&generated.Contains("__sp___src0",StringComparison.Ordinal)
            &&generated.Contains("__orderSpan",StringComparison.Ordinal),
            "Explicit default comparers should use supported fast paths without the removed speculative sorted-set probe.",
            generated,
            output);
    }

    [Fact]
    public void SortedOrderFastPathAllowsElementwiseContinuation() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<int> OrderThenFilterMapTake(int[] src) =>
                    src.OrderBy(static x => x).Where(static x => x>1).Select(static x => x*2).Take(3).ToList();
                public void Run(int[] src) => _ = OrderThenFilterMapTake(src);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("__orderSpan",StringComparison.Ordinal)
            &&generated.Contains("while(__orderNextIndex",StringComparison.Ordinal)
            &&generated.Contains("if(!(__it",StringComparison.Ordinal)
            &&generated.Contains(">1)) continue;",StringComparison.Ordinal),
            "Sorted Order fast path should keep elementwise continuation inside a loop.",
            generated,
            output);
    }

    [Fact]
    public void PureOrderConsumeUsesSortedSpanDirectPath() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void OrderFast(int[] src, Consumer<int> consumer) =>
                    src.Order().Consume(consumer);
                private static void OrderByFast(int[] src, Consumer<int> consumer) =>
                    src.OrderBy(static x => x).Consume(consumer);
                public void Run(int[] src, Consumer<int> consumer) {
                    OrderFast(src,consumer);
                    OrderByFast(src,consumer);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("__orderedSource",StringComparison.Ordinal)
            &&generated.Contains("__orderedHandled",StringComparison.Ordinal)
            &&generated.Contains("__orderedSpan",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Linq.Enumerable.Order(",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Linq.Enumerable.OrderBy(",StringComparison.Ordinal),
            "Pure Order/OrderBy Consume should use sorted span direct consume instead of delegating back to System.Linq.",
            generated,
            output);
    }

    [Fact]
    public void RangeOrderByConsumeExpandsInsteadOfDelegatingToSystemLinq() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T>{
                public void Consume(T item){
                }
            }
            public static class ConsumeSupport{
                public static void Consume<T>(this IEnumerable<T>source,Consumer<T>consumer){
                    foreach(var item in source){
                        consumer.Consume(item);
                    }
                }
            }
            public class Container{
                private static void RangeOrderBy(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).OrderBy(static x=>x%1000).Consume(consumer);
                private static void RangeThenBy(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).OrderBy(static x=>x%1000).ThenBy(static x=>x).Consume(consumer);
                public void Run(Consumer<int>consumer){
                    RangeOrderBy(consumer);
                    RangeThenBy(consumer);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            !generated.Contains("global::System.Linq.Enumerable.OrderBy(",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Linq.Enumerable.ThenBy(",StringComparison.Ordinal)
            &&generated.Contains("__orderValues",StringComparison.Ordinal)
            &&generated.Contains("__orderIndexes",StringComparison.Ordinal)
            &&generated.Contains("global::System.Buffers.ArrayPool<int>.Shared.Rent",StringComparison.Ordinal),
            "Keyed ordering over a Range source must expand into the pooled order loop instead of delegating back to System.Linq.",
            generated,
            output);
    }

    [Fact]
    public void RangeReverseConsumeComputesValuesWithoutBuffering() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T>{
                public void Consume(T item){
                }
            }
            public static class ConsumeSupport{
                public static void Consume<T>(this IEnumerable<T>source,Consumer<T>consumer){
                    foreach(var item in source){
                        consumer.Consume(item);
                    }
                }
            }
            public class Container{
                private static void RangeReverse(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Reverse().Consume(consumer);
                public void Run(Consumer<int>consumer)=>RangeReverse(consumer);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"RangeReverse");

        GeneratedSourceAssert.True(
            method.Contains("__reverseStart",StringComparison.Ordinal)
            &&!method.Contains("new global::System.Collections.Generic.List<",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal),
            "Reversing a Range for consumption should compute start+index directly instead of buffering every element.",
            generated,
            output);
    }

    [Fact]
    public void EnumerableReverseConsumeUsesPooledArrayNotList() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T>{
                public void Consume(T item){
                }
            }
            public static class ConsumeSupport{
                public static void Consume<T>(this IEnumerable<T>source,Consumer<T>consumer){
                    foreach(var item in source){
                        consumer.Consume(item);
                    }
                }
            }
            public class Container{
                private static void ReverseConsume(IEnumerable<int>src,Consumer<int>consumer)=>
                    src.Reverse().Consume(consumer);
                public void Run(IEnumerable<int>src,Consumer<int>consumer)=>ReverseConsume(src,consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::System.Buffers.ArrayPool<int>.Shared.Rent",StringComparison.Ordinal)
            &&generated.Contains("global::System.Buffers.ArrayPool<int>.Shared.Return",StringComparison.Ordinal)
            &&!generated.Contains("new global::System.Collections.Generic.List<int>",StringComparison.Ordinal),
            "Reverse().Consume(...) over a span-incapable source (IEnumerable<T>) — the exact shape benchmarks use — "
            +"must buffer via pooled array, not List<T>.",
            generated,
            output);
    }

    [Fact]
    public void SequenceEqualWithoutComparerAvoidsInterfaceDispatch() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private static bool PrimitiveEquals(int[] left,int[] right) => left.SequenceEqual(right);
                private static bool ReferenceEquals2(ClassKey[] left,ClassKey[] right) => left.SequenceEqual(right);
                public void Run(int[] a,int[] b,ClassKey[] c,ClassKey[] d) {
                    _ = PrimitiveEquals(a,b);
                    _ = ReferenceEquals2(c,d);
                }
            }
            public sealed class ClassKey:System.IEquatable<ClassKey> {
                public bool Equals(ClassKey? other) => other is not null;
                public override bool Equals(object? obj) => obj is ClassKey other&&this.Equals(other);
                public override int GetHashCode() => 0;
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::System.MemoryExtensions.SequenceEqual(__spanL, __spanR);",StringComparison.Ordinal)
            &&!generated.Contains("SequenceEqual(__spanL, __spanR, comparer)",StringComparison.Ordinal)
            &&generated.Contains("global::System.Collections.Generic.EqualityComparer<global::Sample.ClassKey>.Default",StringComparison.Ordinal)
            &&!generated.Contains("comparer.Equals(",StringComparison.Ordinal),
            "SequenceEqual without an explicit comparer must avoid routing through the IEqualityComparer<T>? "
            +"parameter: known primitives use the 2-arg vectorized overload, other types call "
            +"EqualityComparer<T>.Default.Equals directly so the JIT can devirtualize on the closed generic type.",
            generated,
            output);
    }

    [Fact]
    public void SingleStageArrayPipelinesConsumeDirectSpan() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void RunFast(int[] src,Consumer<int> consumer) {
                    src.Where(static value => (value&1)==0).Consume(consumer);
                    src.Select(static (value,index) => value+index).Consume(consumer);
                    src.Append(4).Consume(consumer);
                    src.Prepend(0).Consume(consumer);
                    src.DefaultIfEmpty(0).Consume(consumer);
                }
                public void Run(int[] src,Consumer<int> consumer) => RunFast(src,consumer);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"RunFast");

        GeneratedSourceAssert.True(
            method.Contains("__whereSpan",StringComparison.Ordinal)
            &&method.Contains("__selectSpan",StringComparison.Ordinal)
            &&method.Contains("__appendSpan",StringComparison.Ordinal)
            &&method.Contains("__defaultSpan",StringComparison.Ordinal)
            &&!method.Contains("while(__ce",StringComparison.Ordinal),
            "Single-stage array pipelines should consume direct spans without nested generated enumerator loops.",
            generated,
            output);
    }

    [Fact]
    public void StringIdentityOrderConsumeUsesSortedSpanDirectPath() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void OrderFast(string[] src, Consumer<string> consumer) =>
                    src.Order().Consume(consumer);
                private static void OrderByFast(string[] src, Consumer<string> consumer) =>
                    src.OrderBy(static x => x).Consume(consumer);
                public void Run(string[] src, Consumer<string> consumer) {
                    OrderFast(src,consumer);
                    OrderByFast(src,consumer);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::System.Collections.Generic.Comparer<string>.Default.Compare",StringComparison.Ordinal)
            &&generated.Contains("__orderedHandled",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Linq.Enumerable.Order(",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Linq.Enumerable.OrderBy(",StringComparison.Ordinal),
            "String identity Order/OrderBy Consume should avoid sorting when the source is already ordered by the default comparer.",
            generated,
            output);
    }

    [Fact]
    public void ScalarTerminalsUseFactsAndStaticComputeInline() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private static (bool Any,long Count,long EvenCount) Scalars(int[] src) =>
                    (src.Any(),src.LongCount(),src.LongCount(static x => (x&1)==0));
                public void Run(int[] src) => _ = Scalars(src);
            }
            """;

        var generated = GeneratedSource(source);
        var compact = generated.Replace(" ","",StringComparison.Ordinal);

        GeneratedSourceAssert.True(
            compact.Contains(".Length)!=0",StringComparison.Ordinal)
            &&compact.Contains("=(long)(src.Length);",StringComparison.Ordinal)
            &&compact.Contains(".Compute(__s",StringComparison.Ordinal)
            &&!generated.Contains("global::Seamless.Linq.IValueEnumerator<bool>",StringComparison.Ordinal)
            &&!generated.Contains("global::Seamless.Linq.IValueEnumerator<long>",StringComparison.Ordinal),
            "Scalar terminals should use source facts or static Compute inline without scalar enumerator structs.",
            generated,
            output);
    }

    [Fact]
    public void PredicateScalarConsumeUsesStaticComputeInline() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public static class Route {
                public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
            }
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public class Base {
                protected IEnumerable<int> srcCount(int count) =>
                    Enumerable.AsEnumerable(new int[count]);
            }
            public class Container : Base {
                private readonly Consumer<long> consumer = new();
                private void LongCountPredicate() =>
                    consumer.Consume(srcCount(1024).AsX<int>().LongCount(x => (x&1)==0));
                public void Run() => LongCountPredicate();
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains(".Compute(__s",StringComparison.Ordinal)
            &&generated.Contains("var count = 0;",StringComparison.Ordinal)
            &&!generated.Contains("long count = 0;",StringComparison.Ordinal)
            &&!generated.Contains("global::Seamless.Linq.IValueEnumerator<long>",StringComparison.Ordinal),
            "Predicate scalar terminals consumed as arguments should use static Compute inline without scalar enumerator structs.",
            generated,
            output);
    }

    [Fact]
    public void BinarySetThenOrderUsesSharedPooledOrderBuffer() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<int> UnionOrderThenBy(int[] first,int[] second) =>
                    first.Union(second).OrderBy(static x => x%2).ThenBy(static x => x).ToList();
                public void Run(int[] first,int[] second) => _ = UnionOrderThenBy(first,second);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("__orderValues",StringComparison.Ordinal)
            &&generated.Contains("__orderIndexes",StringComparison.Ordinal)
            &&generated.Contains("global::System.Array.Sort(__orderIndexes",StringComparison.Ordinal)
            &&generated.Contains("global::System.Buffers.ArrayPool<int>.Shared.Rent",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Collections.Generic.List<__sortitem",StringComparison.Ordinal),
            "BinarySet followed by Order/ThenBy should materialize accepted values directly into the pooled order buffer.",
            generated,
            output);
    }

    [Fact]
    public void DistinctFallbackUsesHashSetSlim() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void DistinctFast(int[] src, Consumer<int> consumer) =>
                    src.Distinct().Consume(consumer);
                public void Run(int[] src, Consumer<int> consumer) => DistinctFast(src, consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.HashSetSlim<int>",StringComparison.Ordinal)
            &&generated.Contains("using var __lk",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Collections.Generic.HashSet<int>",StringComparison.Ordinal),
            "Distinct fallback should use pooled HashSetSlim for internal duplicate tracking.",
            generated,
            output);
    }

    [Fact]
    public void BinarySetOperationsUseHashSetSlim() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void SetOps(int[] first, int[] second, Consumer<int> consumer) {
                    first.Union(second).Consume(consumer);
                    first.Except(second).Consume(consumer);
                    first.Intersect(second).Consume(consumer);
                }
                public void Run(int[] first, int[] second, Consumer<int> consumer) => SetOps(first, second, consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.HashSetSlim<int>",StringComparison.Ordinal)
            &&generated.Contains(".Remove(value)",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Collections.Generic.HashSet<int>",StringComparison.Ordinal),
            "Union/Except/Intersect internal sets should use HashSetSlim.",
            generated,
            output);
    }

    [Fact]
    public void ArrayDirectConsumeUsesDirectSpanWithoutRuntimeFallback() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void RunFast(int[] first, int[] second, Consumer<int> consumer) {
                    first.Except(second).Consume(consumer);
                    first.Distinct().Consume(consumer);
                    first.AsEnumerable().Consume(consumer);
                }
                public void Run(int[] first, int[] second, Consumer<int> consumer) => RunFast(first, second, consumer);
            }
            """;

        var generated = GeneratedSource(source);
        var normalized = generated.Replace("\r\n","\n",StringComparison.Ordinal);

        GeneratedSourceAssert.True(
            generated.Contains("=__src0.GetSpan();",StringComparison.Ordinal)
            &&generated.Contains("=__sec0.GetSpan();",StringComparison.Ordinal)
            &&generated.Contains(".GetSpan();",StringComparison.Ordinal)
            &&!normalized.Contains("if(__src0.TryGetSpan(out var __setSrcSpan",StringComparison.Ordinal)
            &&!normalized.Contains("}else{\n    while(__src0.TryGetNext",StringComparison.Ordinal),
            "Array roots should use direct GetSpan() traversal and avoid runtime TryGetSpan fallback branches.",
            generated,
            output);
    }

    [Fact]
    public void ListDirectConsumeUsesCollectionsMarshalBackedDirectSpan() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void RunFast(List<int> first, List<int> second, Consumer<int> consumer) =>
                    first.Except(second).Consume(consumer);
                public void Run(List<int> first, List<int> second, Consumer<int> consumer) => RunFast(first, second, consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("CollectionsMarshal.AsSpan(source).Slice",StringComparison.Ordinal)
            &&generated.Contains("=__src0.GetSpan();",StringComparison.Ordinal)
            &&generated.Contains("=__sec0.GetSpan();",StringComparison.Ordinal),
            "List roots should expose a direct GetSpan() backed by CollectionsMarshal.AsSpan.",
            generated,
            output);
    }

    [Fact]
    public void EnumerableRuntimeSpanPathKeepsFallback() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void RunFallback(IEnumerable<int> source, Consumer<int> consumer) =>
                    source.Distinct().Consume(consumer);
                public void Run(IEnumerable<int> source, Consumer<int> consumer) => RunFallback(source, consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("if(__src0.TryGetSpan(out var __distinctSpan",StringComparison.Ordinal)
            &&generated.Contains("}else{",StringComparison.Ordinal)
            &&generated.Contains("while(__src0.TryGetNext",StringComparison.Ordinal),
            "IEnumerable<T> roots only have runtime span probing, so the fallback branch must remain.",
            generated,
            output);
    }

    [Fact]
    public void BinarySetByOperationsUseHashSetSlim() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source, Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public class Container {
                private static void SetByOps(int[] first, int[] second, string[] keys, Consumer<int> consumer) {
                    first.UnionBy(second, static x => x.ToString()).Consume(consumer);
                    first.ExceptBy(keys, static x => x.ToString()).Consume(consumer);
                    first.IntersectBy(keys, static x => x.ToString()).Consume(consumer);
                }
                public void Run(int[] first, int[] second, string[] keys, Consumer<int> consumer) => SetByOps(first, second, keys, consumer);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.HashSetSlim<string>",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Collections.Generic.HashSet<string>",StringComparison.Ordinal),
            "UnionBy/ExceptBy/IntersectBy internal key sets should use HashSetSlim.",
            generated,
            output);
    }

    [Fact]
    public void BinarySetOrderFusionUsesHashSetSlimContains() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<int> ExceptOrderThenBy(int[] first,int[] second) =>
                    first.Except(second).OrderBy(static x => x%2).ThenBy(static x => x).ToList();
                public void Run(int[] first,int[] second) => _ = ExceptOrderThenBy(first,second);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.HashSetSlim<int>",StringComparison.Ordinal)
            &&generated.Contains(".Contains(",StringComparison.Ordinal)
            &&generated.Contains(".Dispose();",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Collections.Generic.HashSet<int>",StringComparison.Ordinal),
            "Binary set followed by Order/ThenBy should use HashSetSlim fields and Contains in the fused pipeline.",
            generated,
            output);
    }

    [Fact]
    public void ToHashSetKeepsSystemHashSetResult() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static HashSet<int> ToHashSetFast(int[] src) =>
                    src.ToHashSet();
                public void Run(int[] src) => _ = ToHashSetFast(src);
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::System.Collections.Generic.HashSet<int>",StringComparison.Ordinal)
            &&!generated.Contains("global::Seamless.Linq.HashSetSlim<int>",StringComparison.Ordinal),
            "ToHashSet must keep returning System.Collections.Generic.HashSet<T>.",
            generated,
            output);
    }

    [Fact]
    public void FirstAndToHashSetUseDirectSpanTerminalPath() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static (int First,HashSet<int> Set) Terminals(int[] src) =>
                    (src.First(),src.ToHashSet());
                public void Run(int[] src) => _ = Terminals(src);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Terminals");

        GeneratedSourceAssert.True(
            method.Contains(".GetSpan();",StringComparison.Ordinal)
            &&method.Contains("[0]",StringComparison.Ordinal)
            &&method.Contains("global::System.Collections.Generic.HashSet<int>",StringComparison.Ordinal)
            &&method.Contains("new(__hashSetSpan",StringComparison.Ordinal)
            &&!method.Contains(".TryGetSpan(out var",StringComparison.Ordinal)
            &&!method.Contains("global::System.Linq.Enumerable.First",StringComparison.Ordinal)
            &&!method.Contains("global::System.Linq.Enumerable.ToHashSet",StringComparison.Ordinal),
            "First and ToHashSet over direct array roots should use GetSpan() terminal code while ToHashSet keeps System.HashSet<T>.",
            generated,
            output);
    }

    [Fact]
    public void ArrayAndListMaterializeUseDirectSpanWithoutRuntimeFallback() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int[] ArrayToArray(int[] src) => src.ToArray();
                private static List<int> ArrayToList(int[] src) => src.ToList();
                private static int[] ListToArray(List<int> src) => src.ToArray();
                private static List<int> ListToList(List<int> src) => src.ToList();
                public void Run(int[] array, List<int> list) {
                    _ = ArrayToArray(array);
                    _ = ArrayToList(array);
                    _ = ListToArray(list);
                    _ = ListToList(list);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var methods = string.Concat(
            GeneratedMethod(generated,"ArrayToArray"),
            GeneratedMethod(generated,"ArrayToList"),
            GeneratedMethod(generated,"ListToArray"),
            GeneratedMethod(generated,"ListToList"));

        GeneratedSourceAssert.True(
            methods.Contains(".GetSpan();",StringComparison.Ordinal)
            &&generated.Contains("CollectionsMarshal.AsSpan(source).Slice",StringComparison.Ordinal)
            &&!methods.Contains("global::System.Linq.Enumerable.ToArray",StringComparison.Ordinal)
            &&!methods.Contains("global::System.Linq.Enumerable.ToList",StringComparison.Ordinal)
            &&!methods.Contains("TryGetSpan(out var __sourceSpan",StringComparison.Ordinal)
            &&!methods.Contains("TryGetNonEnumeratedCount(out var __count",StringComparison.Ordinal)
            &&!methods.Contains("TryCopyTo(global::System.MemoryExtensions.AsSpan(__result)",StringComparison.Ordinal),
            "Array/List ToArray/ToList should use direct GetSpan materialization without runtime count/copy/span probing.",
            generated,
            output);
    }

    [Fact]
    public void EnumerableMaterializeKeepsRuntimeFallback() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int[] ToArrayFallback(IEnumerable<int> src) => src.ToArray();
                private static List<int> ToListFallback(IEnumerable<int> src) => src.ToList();
                public void Run(IEnumerable<int> src) {
                    _ = ToArrayFallback(src);
                    _ = ToListFallback(src);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("TryGetNonEnumeratedCount(out var __count",StringComparison.Ordinal)
            &&generated.Contains("TryCopyTo(global::System.MemoryExtensions.AsSpan(__result)",StringComparison.Ordinal)
            &&generated.Contains("TryGetSpan(out var __sourceSpan",StringComparison.Ordinal),
            "IEnumerable<T> materialization should keep runtime count/copy/span fallback paths.",
            generated,
            output);
    }

    [Fact]
    public void DirectSpanScalarTerminalsAvoidRuntimeTryGetSpan() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static (int Sum,double Average,bool Contains,int Element,int Last,int Single) ArrayScalars(int[] src) =>
                    (src.Sum(),src.Average(),src.Contains(3),src.ElementAt(0),src.Last(),src.Single());
                private static (int Sum,bool Contains,int Last) ListScalars(List<int> src) =>
                    (src.Sum(),src.Contains(3),src.Last());
                public void Run(int[] array, List<int> list) {
                    _ = ArrayScalars(array);
                    _ = ListScalars(list);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var methods = string.Concat(
            GeneratedMethod(generated,"ArrayScalars"),
            GeneratedMethod(generated,"ListScalars"));

        GeneratedSourceAssert.True(
            methods.Contains(".GetSpan();",StringComparison.Ordinal)
            &&generated.Contains("CollectionsMarshal.AsSpan(source).Slice",StringComparison.Ordinal)
            &&!methods.Contains(".TryGetSpan(out var __span)",StringComparison.Ordinal),
            "Array/List scalar terminals should read direct GetSpan and avoid runtime TryGetSpan branches.",
            generated,
            output);
    }

    [Fact]
    public void DirectSpanAggregateAvoidsRuntimeTryGetSpanAndSpanForeach() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static (int Plain,int Seed,int Selected) ArrayAggregate(int[] src) =>
                    (src.Aggregate(static (left,right) => left+right),
                     src.Aggregate(10,static (sum,value) => sum+value),
                     src.Aggregate(10,static (sum,value) => sum+value,static sum => sum*2));
                private static int ListAggregate(List<int> src) =>
                    src.Aggregate(static (left,right) => left+right);
                private static int EnumerableAggregate(IEnumerable<int> src) =>
                    src.Aggregate(static (left,right) => left+right);
                public void Run(int[] array, List<int> list, IEnumerable<int> enumerable) {
                    _ = ArrayAggregate(array);
                    _ = ListAggregate(list);
                    _ = EnumerableAggregate(enumerable);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var directMethods = string.Concat(
            GeneratedMethod(generated,"ArrayAggregate"),
            GeneratedMethod(generated,"ListAggregate"));
        var enumerableMethod = GeneratedMethod(generated,"EnumerableAggregate");

        GeneratedSourceAssert.True(
            directMethods.Contains(".GetSpan();",StringComparison.Ordinal)
            &&directMethods.Contains("Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements()",StringComparison.Ordinal)
            &&!directMethods.Contains(".TryGetSpan(out var",StringComparison.Ordinal)
            &&!directMethods.Contains("foreach(var",StringComparison.Ordinal)
            &&!directMethods.Contains("__first",StringComparison.Ordinal)
            &&enumerableMethod.Contains(".TryGetSpan(out var",StringComparison.Ordinal)
            &&enumerableMethod.Contains("}else{",StringComparison.Ordinal),
            "Array/List Aggregate should use direct GetSpan()+for loops while IEnumerable<T> keeps runtime span probing and fallback.",
            generated,
            output);
    }

    [Fact]
    public void DirectSpanCountPreservingPipelineMaterializesByIndexLoop() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int[] SelectToArray(int[] src) =>
                    src.Select(static x => x+1).ToArray();
                private static List<int> SelectToList(int[] src) =>
                    src.Select(static x => x*2).ToList();
                public void Run(int[] src) {
                    _ = SelectToArray(src);
                    _ = SelectToList(src);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var methods = string.Concat(
            GeneratedMethod(generated,"SelectToArray"),
            GeneratedMethod(generated,"SelectToList"));

        GeneratedSourceAssert.True(
            methods.Contains("__pipelineSpan",StringComparison.Ordinal)
            &&methods.Contains("__pipelineDestination",StringComparison.Ordinal)
            &&methods.Contains("=__pipelineSrc",StringComparison.Ordinal)
            &&!methods.Contains("TryCopyTo(global::System.MemoryExtensions.AsSpan(__linq",StringComparison.Ordinal)
            &&!methods.Contains("TryCopyTo(__pipelineDestination",StringComparison.Ordinal),
            "Count-preserving Select over a direct span source should materialize with a span index loop, not TryCopyTo.",
            generated,
            output);
    }

    [Fact]
    public void PersistedListAsXUsesPersistedListRoot() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public static class Route {
                public static IEnumerable<T> AsX<T>(this global::Seamless.Persisted.Collections.PersistedList<T> source) => source;
            }
            public class Container {
                private static int CountList(global::Seamless.Persisted.Collections.PersistedList<int> source) =>
                    source.AsX().Count();
                private static int[] SelectToArray(global::Seamless.Persisted.Collections.PersistedList<int> source) =>
                    source.AsX().Where(static x => x>0).Select(static x => x+1).ToArray();
                public void Run(global::Seamless.Persisted.Collections.PersistedList<int> source) {
                    _ = CountList(source);
                    _ = SelectToArray(source);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var methods = string.Concat(
            GeneratedMethod(generated,"CountList"),
            GeneratedMethod(generated,"SelectToArray"));

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Persisted.Collections.PersistedList<int>",StringComparison.Ordinal)
            &&generated.Contains(".GetSpan();",StringComparison.Ordinal)
            &&generated.Contains("source.AsSpan",StringComparison.Ordinal)
            &&!methods.Contains("global::System.Linq.Enumerable.Count",StringComparison.Ordinal)
            &&!methods.Contains("global::System.Linq.Enumerable.ToArray",StringComparison.Ordinal),
            "AsX should be treated as a transparent test/benchmark route and the original PersistedList<T> receiver should become the optimized root.",
            generated,
            output);
    }

    private static string GeneratedMethod(string generated,string methodName) {
        var summaryMarker = $"<c>{methodName}()</c>";
        var summaryIndex = generated.IndexOf(summaryMarker,StringComparison.Ordinal);
        if(summaryIndex<0) {
            return string.Empty;
        }
        var start = generated.IndexOf("public static",summaryIndex,StringComparison.Ordinal);
        if(start<0) {
            return string.Empty;
        }
        var openBrace = generated.IndexOf('{',start);
        if(openBrace<0) {
            return string.Empty;
        }
        var depth = 0;
        for(var index = openBrace;index<generated.Length;index++) {
            if(generated[index]=='{') {
                depth++;
            } else if(generated[index]=='}') {
                depth--;
                if(depth==0) {
                    return generated[start..(index+1)];
                }
            }
        }
        return generated[start..];
    }

    private static string GeneratedSource(string source) {
        var comp = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        GeneratorDriver driver = GeneratorTestHelpers.CreateDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(comp,out var outputCompilation,out var generatorDiagnostics);
        var generated = string.Concat(driver.GetRunResult().Results.SelectMany(static r => r.GeneratedSources).Select(static s => s.SourceText.ToString()));
        var diagnostics = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static d => d.Severity==DiagnosticSeverity.Error)
            .Where(static d => d.Id!="CS9137")
            .ToArray();
        GeneratedSourceAssert.NoErrors(diagnostics,generated,null);
        return generated;
    }
}
