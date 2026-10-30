using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineWhileTests(ITestOutputHelper output)
{
    [Fact]
    public void DirectSpanPipeline_TakeWhileAndSkipWhile_GenerateAndExecuteCorrectly()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class WhileFusionService {
                public int ExecuteTakeWhileSum(int[] array, int threshold) => array.TakeWhile(x => x < threshold).Sum();
                public int RunTakeWhileSum(int[] array, int threshold) => ExecuteTakeWhileSum(array, threshold);

                public int ExecuteSkipWhileCount(int[] array, int threshold) => array.SkipWhile(x => x < threshold).Count();
                public int RunSkipWhileCount(int[] array, int threshold) => ExecuteSkipWhileCount(array, threshold);

                public int ExecuteWhereTakeWhileSum(int[] array, int min, int max) => array.Where(x => x > min).TakeWhile(x => x < max).Sum();
                public int RunWhereTakeWhileSum(int[] array, int min, int max) => ExecuteWhereTakeWhileSum(array, min, max);

                public bool ExecuteSkipWhileWhereAny(int[] array, int threshold, int target) => array.SkipWhile(x => x < threshold).Where(x => x == target).Any();
                public bool RunSkipWhileWhereAny(int[] array, int threshold, int target) => ExecuteSkipWhileWhereAny(array, threshold, target);

                public int ExecuteTakeWhileSelectMax(int[] array, int threshold) => array.TakeWhile(x => x < threshold).Select(x => x * 2).Max();
                public int RunTakeWhileSelectMax(int[] array, int threshold) => ExecuteTakeWhileSelectMax(array, threshold);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "WhileFusion", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.WhileFusionService", type =>
        {
            var instance = Activator.CreateInstance(type)!;

            void Verify(string expectedName, string actualName, object[] args)
            {
                var expected = type.GetMethod(expectedName)!;
                var actual = type.GetMethod(actualName)!;
                (object? value, Type? exception) Invoke(global::System.Reflection.MethodInfo m)
                {
                    try { return (m.Invoke(instance, args), null); }
                    catch (global::System.Reflection.TargetInvocationException ex) { return (null, ex.InnerException?.GetType()); }
                }
                Assert.Equal(Invoke(expected), Invoke(actual));
            }

            var testArrays = new[]
            {
                new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 10, -5, 20, -1, 30, 40 },
                new[] { 5, 4, 3, 2, 1 },
                new[] { 42 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays)
            {
                // TakeWhile Sum
                Verify("ExecuteTakeWhileSum", "RunTakeWhileSum", [arr, 5]);
                Verify("ExecuteTakeWhileSum", "RunTakeWhileSum", [arr, 0]);
                Verify("ExecuteTakeWhileSum", "RunTakeWhileSum", [arr, 100]);

                // SkipWhile Count
                Verify("ExecuteSkipWhileCount", "RunSkipWhileCount", [arr, 3]);
                Verify("ExecuteSkipWhileCount", "RunSkipWhileCount", [arr, 0]);
                Verify("ExecuteSkipWhileCount", "RunSkipWhileCount", [arr, 100]);

                // Where + TakeWhile Sum
                Verify("ExecuteWhereTakeWhileSum", "RunWhereTakeWhileSum", [arr, 2, 8]);
                Verify("ExecuteWhereTakeWhileSum", "RunWhereTakeWhileSum", [arr, 10, 5]);

                // SkipWhile + Where Any
                Verify("ExecuteSkipWhileWhereAny", "RunSkipWhileWhereAny", [arr, 4, 6]);
                Verify("ExecuteSkipWhileWhereAny", "RunSkipWhileWhereAny", [arr, 0, 99]);

                // TakeWhile + Select Max
                Verify("ExecuteTakeWhileSelectMax", "RunTakeWhileSelectMax", [arr, 6]);
                Verify("ExecuteTakeWhileSelectMax", "RunTakeWhileSelectMax", [arr, 0]);
            }

            return true;
        });
    }
}
