using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineLastSliceTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipelineLastSlice_TakeLastAndSkipLast_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class LastSliceService {
                public int ExecuteTakeLastSum(int[] array, int take) => array.TakeLast(take).Sum();
                public int RunTakeLastSum(int[] array, int take) => ExecuteTakeLastSum(array, take);

                public int ExecuteSkipLastCount(int[] array, int skip) => array.SkipLast(skip).Count();
                public int RunSkipLastCount(int[] array, int skip) => ExecuteSkipLastCount(array, skip);

                public int ExecuteSkipTakeLastWhereSum(int[] array, int skip, int takeLast, int threshold) => array.Skip(skip).TakeLast(takeLast).Where(x => x > threshold).Sum();
                public int RunSkipTakeLastWhereSum(int[] array, int skip, int takeLast, int threshold) => ExecuteSkipTakeLastWhereSum(array, skip, takeLast, threshold);

                public bool ExecuteTakeSkipLastWhereAny(int[] array, int take, int skipLast, int threshold) => array.Take(take).SkipLast(skipLast).Where(x => x > threshold).Any();
                public bool RunTakeSkipLastWhereAny(int[] array, int take, int skipLast, int threshold) => ExecuteTakeSkipLastWhereAny(array, take, skipLast, threshold);

                public int ExecuteTakeLastLast(int[] array, int take) => array.TakeLast(take).Last();
                public int RunTakeLastLast(int[] array, int take) => ExecuteTakeLastLast(array, take);

                public int ExecuteSkipLastTakeLastSum(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).Sum();
                public int RunSkipLastTakeLastSum(int[] array, int skipLast, int takeLast) => ExecuteSkipLastTakeLastSum(array, skipLast, takeLast);

                public int ExecuteTakeLastSelectMax(int[] array, int take) => array.TakeLast(take).Select(x => x * 2).Max();
                public int RunTakeLastSelectMax(int[] array, int take) => ExecuteTakeLastSelectMax(array, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "LastSliceFusion", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.LastSliceService", type => {
            var instance = Activator.CreateInstance(type)!;

            void Verify(string expectedName, string actualName, object[] args) {
                var expected = type.GetMethod(expectedName)!;
                var actual = type.GetMethod(actualName)!;
                (object? value, Type? exception) Invoke(global::System.Reflection.MethodInfo m) {
                    try { return (m.Invoke(instance, args), null); }
                    catch (global::System.Reflection.TargetInvocationException ex) { return (null, ex.InnerException?.GetType()); }
                }
                Assert.Equal(Invoke(expected), Invoke(actual));
            }

            var testArrays = new[] {
                new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 10, -5, 20, -1, 30, 40 },
                new[] { 42 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays) {
                Verify("ExecuteTakeLastSum", "RunTakeLastSum", [arr, 3]);
                Verify("ExecuteTakeLastSum", "RunTakeLastSum", [arr, 0]);
                Verify("ExecuteTakeLastSum", "RunTakeLastSum", [arr, 100]);
                Verify("ExecuteTakeLastSum", "RunTakeLastSum", [arr, -2]);

                Verify("ExecuteSkipLastCount", "RunSkipLastCount", [arr, 2]);
                Verify("ExecuteSkipLastCount", "RunSkipLastCount", [arr, 0]);
                Verify("ExecuteSkipLastCount", "RunSkipLastCount", [arr, 100]);
                Verify("ExecuteSkipLastCount", "RunSkipLastCount", [arr, -1]);

                Verify("ExecuteSkipTakeLastWhereSum", "RunSkipTakeLastWhereSum", [arr, 2, 4, 3]);
                Verify("ExecuteSkipTakeLastWhereSum", "RunSkipTakeLastWhereSum", [arr, 0, 100, 5]);
                Verify("ExecuteSkipTakeLastWhereSum", "RunSkipTakeLastWhereSum", [arr, 5, 2, 50]);

                Verify("ExecuteTakeSkipLastWhereAny", "RunTakeSkipLastWhereAny", [arr, 8, 2, 3]);
                Verify("ExecuteTakeSkipLastWhereAny", "RunTakeSkipLastWhereAny", [arr, 5, 10, 0]);

                Verify("ExecuteTakeLastLast", "RunTakeLastLast", [arr, 3]);
                Verify("ExecuteTakeLastLast", "RunTakeLastLast", [arr, 1]);
                Verify("ExecuteTakeLastLast", "RunTakeLastLast", [arr, 0]);
                Verify("ExecuteTakeLastLast", "RunTakeLastLast", [arr, -1]);

                Verify("ExecuteSkipLastTakeLastSum", "RunSkipLastTakeLastSum", [arr, 2, 3]);
                Verify("ExecuteSkipLastTakeLastSum", "RunSkipLastTakeLastSum", [arr, 0, 5]);
                Verify("ExecuteSkipLastTakeLastSum", "RunSkipLastTakeLastSum", [arr, 10, 2]);

                Verify("ExecuteTakeLastSelectMax", "RunTakeLastSelectMax", [arr, 4]);
                Verify("ExecuteTakeLastSelectMax", "RunTakeLastSelectMax", [arr, 0]);
            }

            return true;
        });
    }
}
