using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineSliceFusionTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipelineSliceFusion_SkipAndTake_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class SliceFusionService {
                public int ExecuteSkipTakeSum(int[] array, int skip, int take) => array.Skip(skip).Take(take).Sum();
                public int RunSkipTakeSum(int[] array, int skip, int take) => ExecuteSkipTakeSum(array, skip, take);

                public int ExecuteSkipWhereCount(int[] array, int skip, int threshold) => array.Skip(skip).Where(x => x > threshold).Count();
                public int RunSkipWhereCount(int[] array, int skip, int threshold) => ExecuteSkipWhereCount(array, skip, threshold);

                public bool ExecuteTakeWhereAny(int[] array, int take, int threshold) => array.Take(take).Where(x => x > threshold).Any();
                public bool RunTakeWhereAny(int[] array, int take, int threshold) => ExecuteTakeWhereAny(array, take, threshold);

                public int ExecuteWhereTakeSum(int[] array, int threshold, int take) => array.Where(x => x > threshold).Take(take).Sum();
                public int RunWhereTakeSum(int[] array, int threshold, int take) => ExecuteWhereTakeSum(array, threshold, take);

                public int ExecuteSkipTakeLast(int[] array, int skip, int take) => array.Skip(skip).Take(take).Last();
                public int RunSkipTakeLast(int[] array, int skip, int take) => ExecuteSkipTakeLast(array, skip, take);

                public int ExecuteTakeSelectMax(int[] array, int take) => array.Take(take).Select(x => x * 2).Max();
                public int RunTakeSelectMax(int[] array, int take) => ExecuteTakeSelectMax(array, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "SliceFusion", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.SliceFusionService", type => {
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
                Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [arr, 2, 5]);
                Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [arr, 0, 100]);
                Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [arr, 100, 5]);
                Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [arr, -1, 3]);

                Verify("ExecuteSkipWhereCount", "RunSkipWhereCount", [arr, 3, 4]);
                Verify("ExecuteSkipWhereCount", "RunSkipWhereCount", [arr, 0, 100]);

                Verify("ExecuteTakeWhereAny", "RunTakeWhereAny", [arr, 4, 2]);
                Verify("ExecuteTakeWhereAny", "RunTakeWhereAny", [arr, 2, 50]);

                Verify("ExecuteWhereTakeSum", "RunWhereTakeSum", [arr, 2, 3]);
                Verify("ExecuteWhereTakeSum", "RunWhereTakeSum", [arr, 0, 10]);

                Verify("ExecuteSkipTakeLast", "RunSkipTakeLast", [arr, 1, 3]);
                Verify("ExecuteSkipTakeLast", "RunSkipTakeLast", [arr, 50, 5]);

                Verify("ExecuteTakeSelectMax", "RunTakeSelectMax", [arr, 5]);
                Verify("ExecuteTakeSelectMax", "RunTakeSelectMax", [arr, 0]);
            }

            return true;
        });
    }
}
