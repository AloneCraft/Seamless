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
            &&generated.Contains("new global::Seamless.Linq.DictionarySlim<int,int>",StringComparison.Ordinal)
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
            "Reverse().Consume(...) over a span-incapable source (IEnumerable<T>) 鬩包ｽｯ繝ｻ・ｶ驛｢譎｢・ｽ・ｻthe exact shape benchmarks use 鬩包ｽｯ繝ｻ・ｶ驛｢譎｢・ｽ・ｻ"
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
    public void SumOverIntArrayUsesSimdSumSpan() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public class Container {
                private static int SumInts(int[] src) => src.Sum();
                private static float SumFloats(float[] src) => src.Sum();
                public void Run(int[] ints,float[] floats) {
                    _ = SumInts(ints);
                    _ = SumFloats(floats);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.Enumerable.SumSpan<int>(__span)",StringComparison.Ordinal)
            &&!generated.Contains("global::Seamless.Linq.Enumerable.SumSpan<float>",StringComparison.Ordinal),
            "Sum() over int should use the SIMD SumSpan helper; float must keep the scalar checked loop "
            +"(SumSpan's fallback path does not reproduce System.Linq's double-accumulation semantics for float).",
            generated,
            output);
    }

    [Fact]
    public void MaxMinOverListUsesSimdSpanNotRuntimeProbe() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static (int Max,int Min) ListMinMax(List<int> src) => (src.Max(),src.Min());
                private static (int Max,int Min) ArrayMinMax(int[] src) => (src.Max(),src.Min());
                public void Run(List<int> list,int[] array) {
                    _ = ListMinMax(list);
                    _ = ArrayMinMax(array);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var listMethod = GeneratedMethod(generated,"ListMinMax");

        GeneratedSourceAssert.True(
            listMethod.Contains("global::Seamless.Linq.Enumerable.MaxSpan<int>",StringComparison.Ordinal)
            &&listMethod.Contains("global::Seamless.Linq.Enumerable.MinSpan<int>",StringComparison.Ordinal)
            &&!listMethod.Contains(".TryGetSpan(out var",StringComparison.Ordinal),
            "Max/Min over a List<T> source should use the SIMD MaxSpan/MinSpan helpers via the direct span path, "
            +"not the runtime TryGetSpan probe.",
            generated,
            output);
    }

    [Fact]
    public void OrderByIndexSortUsesDevirtualizedComparer() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<int> OrderByMod(int[] src) =>
                    src.OrderBy(static x => x%1000).ToList();
                private static List<int> ThenByMod(int[] src) =>
                    src.OrderBy(static x => x%1000).ThenBy(static x => x).ToList();
                public void Run(int[] src) {
                    _ = OrderByMod(src);
                    _ = ThenByMod(src);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes(",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Array.Sort(",StringComparison.Ordinal),
            "OrderBy(keySelector)/ThenBy index sorting must go through GeneratedCodeHelpers.SortIndexes "
            +"(devirtualized via a struct-constrained comparer) instead of Array.Sort with a boxed IComparer<int>.",
            generated,
            output);
    }

    [Fact]
    public void RangeSingleStageOperatorsConsumeDirectlyWithoutStateMachine() {
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
                private static void RangeConsume(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Consume(consumer);
                private static void AppendRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Append(0).Consume(consumer);
                private static void PrependRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Prepend(0).Consume(consumer);
                private static void ConcatRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Concat(Enumerable.Range(1,16)).Consume(consumer);
                private static void WhereRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Where(static x=>(x&1)==0).Consume(consumer);
                private static void TakeRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Take(3072).Consume(consumer);
                private static void SkipRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).Skip(1024).Consume(consumer);
                private static void TakeWhileIndexRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).TakeWhile(static (x,i)=>i<2048).Consume(consumer);
                private static void SkipWhileRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).SkipWhile(static x=>x<2048).Consume(consumer);
                private static void SkipWhileIndexRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).SkipWhile(static (x,i)=>i<2048).Consume(consumer);
                private static void DefaultIfEmptyRange(Consumer<int>consumer)=>
                    Enumerable.Range(1,4096).DefaultIfEmpty(0).Consume(consumer);
                public void Run(Consumer<int>consumer){
                    RangeConsume(consumer);
                    AppendRange(consumer);
                    PrependRange(consumer);
                    ConcatRange(consumer);
                    WhereRange(consumer);
                    TakeRange(consumer);
                    SkipRange(consumer);
                    TakeWhileIndexRange(consumer);
                    SkipWhileRange(consumer);
                    SkipWhileIndexRange(consumer);
                    DefaultIfEmptyRange(consumer);
                }
            }
            """;

        var generated = GeneratedSource(source);
        foreach(var methodName in new[] {
            "RangeConsume","AppendRange","PrependRange","ConcatRange","WhereRange","DefaultIfEmptyRange",
            "TakeRange","SkipRange","TakeWhileIndexRange","SkipWhileRange","SkipWhileIndexRange"
        }) {
            var method = GeneratedMethod(generated,methodName);
            GeneratedSourceAssert.True(
                method.Contains("for(",StringComparison.Ordinal)
                &&!method.Contains("TryGetNext",StringComparison.Ordinal),
                $"{methodName} over a Range source must consume via a direct start+index for loop, "
                +"not a state-machine TryGetNext loop.",
                generated,
                output);
        }

        foreach(var methodName in new[] {
            "WhereRange","TakeRange","SkipRange","SkipWhileRange","SkipWhileIndexRange",
        }) {
            var method = GeneratedMethod(generated,methodName);
            GeneratedSourceAssert.True(
                method.Contains("new global::System.ArgumentOutOfRangeException(\"count\")",StringComparison.Ordinal)
                &&!method.Contains("global::System.Linq.Enumerable.Range(",StringComparison.Ordinal),
                $"{methodName} must preserve Enumerable.Range argument validation while retaining "
                +"its allocation-free direct loop.",
                generated,
                output);
        }

        var skipWhileMethod = GeneratedMethod(generated,"SkipWhileRange");
        var firstLoop = skipWhileMethod.IndexOf("for(",StringComparison.Ordinal);
        var secondLoop = skipWhileMethod.IndexOf("for(",firstLoop+1,StringComparison.Ordinal);
        var thirdLoop = skipWhileMethod.IndexOf("for(",secondLoop+1,StringComparison.Ordinal);
        GeneratedSourceAssert.True(
            firstLoop>=0
            &&secondLoop>firstLoop
            &&thirdLoop<0
            &&!skipWhileMethod.Contains("RangeIndex",StringComparison.Ordinal),
            "SkipWhile(predicate) over a Range source must use exactly two direct loops: "
            +"one predicate scan followed by one unguarded consume loop, without index state.",
            generated,
            output);
    }

    [Fact]
    public void RangeSumAndSelectUseUninstrumentedDirectIndexLoops() {
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
                private static int SumRange(int start,int count)=>
                    Enumerable.Range(start,count).Sum();
                private static void SelectRange(int start,int count,Consumer<int>consumer)=>
                    Enumerable.Range(start,count).Select(static value=>value*2).Consume(consumer);
                public void Run(int start,int count,Consumer<int>consumer){
                    _=SumRange(start,count);
                    SelectRange(start,count,consumer);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var sumMethod = GeneratedMethod(generated,"SumRange");
        var selectMethod = GeneratedMethod(generated,"SelectRange");

        GeneratedSourceAssert.True(
            sumMethod.Contains("checked",StringComparison.Ordinal)
            &&sumMethod.Contains("for(",StringComparison.Ordinal)
            &&sumMethod.Contains("new global::System.ArgumentOutOfRangeException(\"count\")",StringComparison.Ordinal)
            &&!sumMethod.Contains("TryGetNext",StringComparison.Ordinal)
            &&!sumMethod.Contains(".Compute(",StringComparison.Ordinal)
            &&!sumMethod.Contains("global::System.Linq.Enumerable.Range(",StringComparison.Ordinal)
            &&selectMethod.Contains("for(",StringComparison.Ordinal)
            &&selectMethod.Contains("+__i",StringComparison.Ordinal)
            &&selectMethod.Contains("new global::System.ArgumentOutOfRangeException(\"count\")",StringComparison.Ordinal)
            &&!selectMethod.Contains("TryGetNext",StringComparison.Ordinal)
            &&!selectMethod.Contains("global::System.Linq.Enumerable.Range(",StringComparison.Ordinal)
            &&!selectMethod.Contains("LinqCounterScope",StringComparison.Ordinal),
            "Uninstrumented Range Sum/Select should lower to start+index loops without an enumerator wrapper or runtime diagnostics lookup.",
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
            &&generated.Contains("global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes(__orderIndexes",StringComparison.Ordinal)
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
    public void ArrayToListConsumeMaterializesInlineWithoutScalarEnumerator() {
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
                public static T[] AsX<T>(this T[] source) => source;
            }
            public sealed class Container<T> where T:notnull {
                protected internal readonly Consumer<T> consumer=new();
                protected internal T[] Source() => global::System.Array.Empty<T>();
                private void Materialize() =>
                    this.Source().AsX<T>().ToList().Consume(this.consumer);
                public void Run() => this.Materialize();
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Materialize");

        GeneratedSourceAssert.True(
            method.Contains("global::System.Runtime.InteropServices.CollectionsMarshal.SetCount",StringComparison.Ordinal)
            &&method.Contains(".CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan",StringComparison.Ordinal)
            &&method.Contains("foreach(var __item",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal)
            &&!method.Contains("using var e_",StringComparison.Ordinal),
            "Array ToList consumed immediately should bulk-copy its span in the interceptor body without a one-element scalar enumerator wrapper.",
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
    public void EnumerableToListUsesSegmentedBufferHelper() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public class Container {
                private static List<int> ToListFallback(IEnumerable<int> src) => src.ToList();
                private static List<int> WhereToListFallback(IEnumerable<int> src) => src.Where(static x => x>0).ToList();
                public void Run(IEnumerable<int> src) {
                    _ = ToListFallback(src);
                    _ = WhereToListFallback(src);
                }
            }
            """;

        var generated = GeneratedSource(source);
        var fused = GeneratedMethod(generated,"WhereToListFallback");

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.GeneratedCodeHelpers.ToListFromValueEnumerator<TEnum,int>(ref enumerator)",StringComparison.Ordinal)
            &&generated.Contains("enumerator.Dispose();",StringComparison.Ordinal)
            &&fused.Contains("global::Seamless.Linq.GeneratedCodeHelpers.ToListFromValueEnumerator<",StringComparison.Ordinal)
            &&fused.Contains("(ref ",StringComparison.Ordinal)
            &&!generated.Contains("__result=new global::System.Collections.Generic.List<int>();",StringComparison.Ordinal)
            &&!fused.Contains(".Add(__item",StringComparison.Ordinal),
            "ToList slow paths must build through the pooled segmented helper instead of List<T>.Add growth, and the caller must keep enumerator ownership via try-finally.",
            generated,
            output);
    }

    // 鬮ｫ・ｴ陝ｷ・｢繝ｻ・ｽ繝ｻ・ｫ鬮ｯ譏ｴ繝ｻ繝ｻ・ｽ繝ｻ・ｾ鬩幢ｽ｢繝ｻ・ｧ郢晢ｽｻ繝ｻ・ｦ鬩幢ｽ｢繝ｻ・ｧ郢晢ｽｻ繝ｻ・｣鬩幢ｽ｢隴趣ｽ｢繝ｻ・ｽ繝ｻ・ｳ鬩幢ｽ｢隴取得・ｽ・ｳ繝ｻ・ｨ驍ｵ・ｺ騾趣ｽｯ繝ｻ蟲ｨ繝ｻ繝ｻ・ｻ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｯ鬯ｯ・ｯ繝ｻ・ｧ驛｢譎｢・ｽ・ｻ鬮ｯ謔滂ｽ､螂・ｽｽ・ｭ郢晢ｽｻ繝ｻ・ｽ繝ｻ・ｹ鬮ｮ蜿悶・繝ｻ・ｼ隴・搨ﾂ陝ｶ譏ｴ笙髴托ｽｹ陞滂ｽｲ繝ｻ・ｽ繝ｻ・ｷ郢晢ｽｻ繝ｻ・ｯ鬩搾ｽｵ繝ｻ・ｺ髫ｰ逍ｲ・ｺ蛟･繝ｻ鬩搾ｽｵ繝ｻ・ｺ髣包ｽｵ隴趣ｽ｢繝ｻ・ｽ隶呵ｶ｣・ｽ・ｹ繝ｻ・ｧ髣包ｽｵ隰ｨ魑ｴﾂ郢晢ｽｻ繝ｻ・｡.Consume(...)` 鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｮ鬩幢ｽ｢繝ｻ・ｧ髯具ｽｹ繝ｻ・ｻ驕ｶ蛹・ｽｽ・ｧ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｫ鬮ｯ貅ｷ・｢髮｣・ｽ・｢繝ｻ・ｧ郢晢ｽｻ繝ｻ・ｮ郢晢ｽｻ繝ｻ・ｵ鬩搾ｽｵ繝ｻ・ｺ驛｢譎｢・ｽ・ｻLINQ 鬯ｩ謳ｾ・ｽ・ｨ驛｢・ｧ闔ｨ螟ｲ・ｽ・ｽ繝ｻ・ｫ郢晢ｽｻ繝ｻ・ｯ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｧ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｪ鬩搾ｽｵ繝ｻ・ｺ驛｢譎｢・ｽ・ｻ郢晢ｽｻ繝ｻ・ｽ郢晢ｽｻ繝ｻ・｢鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｯ
    // 鬮ｯ・ｷ鬮ｮ繝ｻﾂ繝ｻ・｡髯晢ｽｲ繝ｻ・｡鬩幢ｽ｢隴弱・・ｽ・ｼ鬩･繝ｻ・ｸ・ｷ繝ｻ・ｹ繝ｻ・ｧ郢晢ｽｻ繝ｻ・ｰ鬩幢ｽ｢隴趣ｽ｢繝ｻ・ｽ繝ｻ・｡鬩幢ｽ｢隴趣ｽ｢繝ｻ・ｽ繝ｻ・ｳ鬩幢ｽ｢隴寂握縺狗ｹ晢ｽｻ繝ｻ・ｼ驛｢譎｢・ｽ・ｻkipLast.cs / TakeLast.cs驛｢譎｢・ｽ・ｻ髯晢ｽｲ繝ｻ・ｨ繝ｻ縺､ﾂ郢晢ｽｻ郢晢ｽｻCount()` 鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｮ鬩幢ｽ｢繝ｻ・ｧ髯具ｽｹ繝ｻ・ｻ驕ｶ蛹・ｽｽ・ｧ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｫ LINQ 鬯ｩ謳ｾ・ｽ・ｨ驛｢・ｧ闔ｨ螟ｲ・ｽ・ｽ繝ｻ・ｫ郢晢ｽｻ繝ｻ・ｯ鬩搾ｽｵ繝ｻ・ｺ髫ｶ蜻ｵ・ｶ・｣繝ｻ・ｽ繝ｻ・ｶ髯橸ｽ｢繝ｻ・ｹ郢晢ｽｻ繝ｻ・･鬮ｯ貅ｷ遘√・・ｽ繝ｻ・｢鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｯ
    // 鬯ｮ・ｯ隶灘･・ｽｽ・ｦ鬯倩ｲｻ・ｽ・ｲ霑｢證ｦ・ｽ・ｹ隴乗・・ｽ・ｻ繝ｻ・｣驍ｵ・ｺ郢晢ｽｻ繝ｻ・ｹ隴惹ｸ橸ｽｹ・ｲ繝ｻ荳ｻ・ｸ・ｷ繝ｻ・ｹ繝ｻ・ｧ郢晢ｽｻ繝ｻ・､鬩幢ｽ｢隴趣ｽ｢繝ｻ・ｽ繝ｻ・ｳ鬮ｫ・ｹ繝ｻ・ｿ郢晢ｽｻ繝ｻ・ｵ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｫ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｪ鬩幢ｽ｢繝ｻ・ｧ髣包ｽｵ隰ｨ魑ｴﾂ驛｢・ｧ郢晢ｽｻ郢晢ｽｻ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・｡鬩幢ｽ｢繝ｻ・ｧ髯晢ｽｲ繝ｻ・ｨ郢晢ｽｻ郢晢ｽｻGC 鬩幢ｽ｢隴惹ｸ翫・驛｢譎｢・ｽ・ｻ鬩幢ｽ｢隴惹ｸ橸ｽｹ・ｲ驛｢譎｢・ｽ・ｻ Queue<T> 鬩幢ｽ｢繝ｻ・ｧ髯ｷ莉｣繝ｻ繝ｻ・ｽ繝ｻ・ｽ郢晢ｽｻ繝ｻ・ｿ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・｣鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｦ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｯ鬩搾ｽｵ繝ｻ・ｺ郢晢ｽｻ繝ｻ・ｪ鬩幢ｽ｢繝ｻ・ｧ髯晢ｽｲ繝ｻ・ｨ驕ｶ莨√・繝ｻ・ｸ繝ｻ・ｺ驛｢譎｢・ｽ・ｻ繝ｻ縺､ﾂ驛｢譎｢・ｽ・ｻ
    [Fact]
    public void LastWindowOperatorsUsePooledBuffers() {
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
                private static void TakeLastAlone(IEnumerable<int> src, Consumer<int> consumer) => src.TakeLast(4).Consume(consumer);
                private static void SkipLastAlone(IEnumerable<int> src, Consumer<int> consumer) => src.SkipLast(4).Consume(consumer);
                private static int TakeLastFused(IEnumerable<int> src) => src.Where(static x => x>0).TakeLast(4).Count();
                private static int SkipLastFused(IEnumerable<int> src) => src.Where(static x => x>0).SkipLast(4).Count();
                public void Run(IEnumerable<int> src, Consumer<int> consumer) {
                    TakeLastAlone(src,consumer);
                    SkipLastAlone(src,consumer);
                    _ = TakeLastFused(src);
                    _ = SkipLastFused(src);
                }
            }
            """;

        var generated = GeneratedSource(source);

        GeneratedSourceAssert.True(
            generated.Contains("global::Seamless.Linq.ValueQueue<int>",StringComparison.Ordinal)
            &&generated.Contains(".Dispose();",StringComparison.Ordinal)
            &&!generated.Contains("global::System.Collections.Generic.Queue<",StringComparison.Ordinal),
            "SkipLast/TakeLast windows must use pooled buffers and dispose them, never a GC-heap Queue<T>.",
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
    public void AsEnumerableTerminalKeepsDirectArrayCountShape() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public sealed class Container {
                private static int CountViaAsEnumerable(int[] source) => source.AsEnumerable().Count();
                public void Run(int[] source) => _ = CountViaAsEnumerable(source);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"CountViaAsEnumerable");

        GeneratedSourceAssert.True(
            method.Contains("source.Length",StringComparison.Ordinal)
            &&!method.Contains(".Compute(__s",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal),
            "A transparent AsEnumerable before Count must keep the direct array count path instead of creating an empty pipeline enumerator.",
            generated,
            output);
    }
    [Fact]
    public void StaticIEnumerableConsumeUsesDirectInterfaceLoop() {
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
            public static class ConsumerExtensions {
                public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public sealed class Container {
                private readonly Consumer<int> consumer = new();
                private void Direct(IEnumerable<int> source) => source.AsX().Consume(this.consumer);
                public void Run(IEnumerable<int> source) => this.Direct(source);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Direct");

        GeneratedSourceAssert.True(
            method.Contains(".GetEnumerator()",StringComparison.Ordinal)
            &&method.Contains(".MoveNext()",StringComparison.Ordinal)
            &&method.Contains(".Current",StringComparison.Ordinal)
            &&method.Contains("using var __interfaceEnumerator",StringComparison.Ordinal)
            &&!method.Contains("TryGetSpan",StringComparison.Ordinal)
            &&!method.Contains("IValueEnumerator",StringComparison.Ordinal),
            "A statically IEnumerable<T> Consume terminal should lower directly to one IEnumerator<T> loop without runtime span recovery or a value-enumerator wrapper.",
            generated,
            output);
    }
    [Fact]
    public void StaticIEnumerableConcatConsumeUsesTwoDirectInterfaceLoops() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumerExtensions {
                public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
                public static IEnumerable<T> AsX<T>(this IEnumerable<T> source) => source;
            }
            public sealed class Container<T> where T:notnull {
                protected internal readonly Consumer<T> consumer=new();
                protected internal IEnumerable<T> First() => global::System.Array.Empty<T>();
                protected internal IEnumerable<T> Second() => global::System.Array.Empty<T>();
                private void Direct() =>
                    this.First().AsX<T>().Concat(this.Second().AsX<T>()).Consume(this.consumer);
                public void Run() => this.Direct();
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Direct");
        var getEnumeratorCount = method.Split(".GetEnumerator()",StringSplitOptions.None).Length-1;
        var moveNextCount = method.Split(".MoveNext()",StringSplitOptions.None).Length-1;

        GeneratedSourceAssert.True(
            getEnumeratorCount==2
            &&moveNextCount==2
            &&method.Contains("global::System.ArgumentNullException.ThrowIfNull",StringComparison.Ordinal)
            &&method.Contains(".Current",StringComparison.Ordinal)
            &&!method.Contains("TryGetSpan",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal)
            &&!method.Contains("IValueEnumerator",StringComparison.Ordinal),
            "Concat over two statically IEnumerable<T> sources should enumerate each source directly and dispose each IEnumerator<T> without value-enumerator wrappers or span probes.",
            generated,
            output);
    }

    [Fact]
    public void StaticIEnumerableDefaultIfEmptyConsumeUsesDirectInterfaceLoop() {
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
            public static class ConsumerExtensions {
                public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public sealed class Container {
                private readonly Consumer<int> consumer = new();
                private void Direct(IEnumerable<int> source) => source.AsX().DefaultIfEmpty(0).Consume(this.consumer);
                public void Run(IEnumerable<int> source) => this.Direct(source);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Direct");

        GeneratedSourceAssert.True(
            method.Contains(".GetEnumerator()",StringComparison.Ordinal)
            &&method.Contains(".MoveNext()",StringComparison.Ordinal)
            &&method.Contains(".Current",StringComparison.Ordinal)
            &&method.Contains("if(__defaultIfEmptyEnumerator",StringComparison.Ordinal)
            &&method.Contains("__defaultIfEmptyValue",StringComparison.Ordinal)
            &&!method.Contains("IValueEnumerator",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal),
            "A statically IEnumerable<T> DefaultIfEmpty Consume terminal should preserve the empty fallback while lowering the non-empty path to one direct IEnumerator<T> loop.",
            generated,
            output);
    }

    [Fact]
    public void IdentitySelectManyConsumeUsesDirectNestedInterfaceLoops() {
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
            public static class ConsumerExtensions {
                public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public sealed class Container {
                private readonly Consumer<int> consumer = new();
                private void Direct(IEnumerable<IEnumerable<int>> source) =>
                    source.AsX().SelectMany(static item => item).Consume(this.consumer);
                public void Run(IEnumerable<IEnumerable<int>> source) => this.Direct(source);
            }
            """;

        var generated = GeneratedSource(source);
        var method = GeneratedMethod(generated,"Direct");

        GeneratedSourceAssert.True(
            method.Contains("__selectManyOuterEnumerator",StringComparison.Ordinal)
            &&method.Contains("__selectManyInnerEnumerator",StringComparison.Ordinal)
            &&method.Contains("using(var __selectManyOuterEnumerator",StringComparison.Ordinal)
            &&method.Contains("using(var __selectManyInnerEnumerator",StringComparison.Ordinal)
            &&!method.Contains("IValueEnumerator",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal),
            "An identity SelectMany Consume terminal should enumerate the outer and inner IEnumerable values directly with deterministic disposal.",
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
                public static IEnumerable<T> AsX<T>(this global::Seamless.Collections.PersistedList<T> source) => source;
            }
            public class Container {
                private static int CountList(global::Seamless.Collections.PersistedList<int> source) =>
                    source.AsX().Count();
                private static int[] SelectToArray(global::Seamless.Collections.PersistedList<int> source) =>
                    source.AsX().Where(static x => x>0).Select(static x => x+1).ToArray();
                public void Run(global::Seamless.Collections.PersistedList<int> source) {
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
            generated.Contains("global::Seamless.Collections.PersistedList<int>",StringComparison.Ordinal)
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
