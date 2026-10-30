using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineMinByMaxByAggregateTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_MinByMaxByAggregate_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class ScalarExtendedService {
                public int ExecuteSkipTakeMinBy(int[] array, int skip, int take) => array.Skip(skip).Take(take).MinBy(x => x % 7);
                public int RunSkipTakeMinBy(int[] array, int skip, int take) => ExecuteSkipTakeMinBy(array, skip, take);

                public int ExecuteTrailingSliceMaxBy(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).MaxBy(x => -x);
                public int RunTrailingSliceMaxBy(int[] array, int skipLast, int takeLast) => ExecuteTrailingSliceMaxBy(array, skipLast, takeLast);

                public int ExecuteSkipTakeAggregateNoSeed(int[] array, int skip, int take) => array.Skip(skip).Take(take).Aggregate((acc, x) => acc + x);
                public int RunSkipTakeAggregateNoSeed(int[] array, int skip, int take) => ExecuteSkipTakeAggregateNoSeed(array, skip, take);

                public int ExecuteTrailingSliceAggregateSeed(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).Aggregate(10, (acc, x) => acc + x);
                public int RunTrailingSliceAggregateSeed(int[] array, int skipLast, int takeLast) => ExecuteTrailingSliceAggregateSeed(array, skipLast, takeLast);

                public string ExecuteSkipTakeAggregateResultSelector(int[] array, int skip, int take) => array.Skip(skip).Take(take).Aggregate(100, (acc, x) => acc + x, acc => $"Result: {acc}");
                public string RunSkipTakeAggregateResultSelector(int[] array, int skip, int take) => ExecuteSkipTakeAggregateResultSelector(array, skip, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "MinByMaxByAggregate", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.ScalarExtendedService", type => {
            var instance = Activator.CreateInstance(type)!;

            void Verify(string expectedName, string actualName, object[] args) {
                var expected = type.GetMethod(expectedName)!;
                var actual = type.GetMethod(actualName)!;
                (object? value, Type? exception) Invoke(global::System.Reflection.MethodInfo m) {
                    try { return (m.Invoke(instance, args), null); }
                    catch (global::System.Reflection.TargetInvocationException ex) { return (null, ex.InnerException?.GetType()); }
                }
                var (expVal, expEx) = Invoke(expected);
                var (actVal, actEx) = Invoke(actual);
                Assert.Equal(expEx, actEx);
                Assert.Equal(expVal, actVal);
            }

            var testArrays = new[] {
                new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 10, -5, 20, -1, 30, 40 },
                new[] { 42 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays) {
                // MinBy
                Verify("ExecuteSkipTakeMinBy", "RunSkipTakeMinBy", [arr, 2, 5]);
                Verify("ExecuteSkipTakeMinBy", "RunSkipTakeMinBy", [arr, 0, 100]);
                Verify("ExecuteSkipTakeMinBy", "RunSkipTakeMinBy", [arr, 100, 5]);

                // MaxBy
                Verify("ExecuteTrailingSliceMaxBy", "RunTrailingSliceMaxBy", [arr, 2, 3]);
                Verify("ExecuteTrailingSliceMaxBy", "RunTrailingSliceMaxBy", [arr, 0, 100]);
                Verify("ExecuteTrailingSliceMaxBy", "RunTrailingSliceMaxBy", [arr, 100, 5]);

                // Aggregate (No Seed)
                Verify("ExecuteSkipTakeAggregateNoSeed", "RunSkipTakeAggregateNoSeed", [arr, 2, 5]);
                Verify("ExecuteSkipTakeAggregateNoSeed", "RunSkipTakeAggregateNoSeed", [arr, 0, 100]);
                Verify("ExecuteSkipTakeAggregateNoSeed", "RunSkipTakeAggregateNoSeed", [arr, 100, 5]);

                // Aggregate (Seed)
                Verify("ExecuteTrailingSliceAggregateSeed", "RunTrailingSliceAggregateSeed", [arr, 2, 3]);
                Verify("ExecuteTrailingSliceAggregateSeed", "RunTrailingSliceAggregateSeed", [arr, 0, 100]);
                Verify("ExecuteTrailingSliceAggregateSeed", "RunTrailingSliceAggregateSeed", [arr, 100, 5]);

                // Aggregate (ResultSelector)
                Verify("ExecuteSkipTakeAggregateResultSelector", "RunSkipTakeAggregateResultSelector", [arr, 2, 4]);
                Verify("ExecuteSkipTakeAggregateResultSelector", "RunSkipTakeAggregateResultSelector", [arr, 0, 10]);
                Verify("ExecuteSkipTakeAggregateResultSelector", "RunSkipTakeAggregateResultSelector", [arr, 100, 5]);
            }
            return true;
        });
    }
}
