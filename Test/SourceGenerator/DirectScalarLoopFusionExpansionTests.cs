using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Test.SourceGenerator;

public class DirectScalarLoopFusionExpansionTests {
    [Fact]
    public void DirectScalarLoopFusion_ExpandedTerminals_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class TerminalExpansionService {
                public bool ExecuteAll(int[] array, int threshold) => array.Where(x => x > 0).All(x => x >= threshold);
                public bool RunAll(int[] array, int threshold) => ExecuteAll(array, threshold);

                public bool ExecuteContains(int[] array, int target) => array.Where(x => x > 0).Contains(target);
                public bool RunContains(int[] array, int target) => ExecuteContains(array, target);

                public int ExecuteFirstOrDefault(int[] array, int threshold) => array.Where(x => x > threshold).FirstOrDefault();
                public int RunFirstOrDefault(int[] array, int threshold) => ExecuteFirstOrDefault(array, threshold);

                public int ExecuteLast(int[] array, int threshold) => array.Where(x => x > threshold).Last();
                public int RunLast(int[] array, int threshold) => ExecuteLast(array, threshold);

                public int ExecuteLastOrDefault(int[] array, int threshold) => array.Where(x => x > threshold).LastOrDefault();
                public int RunLastOrDefault(int[] array, int threshold) => ExecuteLastOrDefault(array, threshold);

                public int ExecuteMin(int[] array) => array.Where(x => x > 0).Min();
                public int RunMin(int[] array) => ExecuteMin(array);

                public int ExecuteMax(int[] array) => array.Where(x => x > 0).Max();
                public int RunMax(int[] array) => ExecuteMax(array);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "TerminalExpansion", 1);
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.TerminalExpansionService", type => {
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
                new[] { 1, 3, 5, 7, 9 },
                new[] { 10, -5, 20, -1, 30 },
                new[] { 2 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays) {
                Verify("ExecuteAll", "RunAll", [arr, 3]);
                Verify("ExecuteAll", "RunAll", [arr, 1]);
                Verify("ExecuteContains", "RunContains", [arr, 5]);
                Verify("ExecuteContains", "RunContains", [arr, 99]);
                Verify("ExecuteFirstOrDefault", "RunFirstOrDefault", [arr, 4]);
                Verify("ExecuteFirstOrDefault", "RunFirstOrDefault", [arr, 100]);
                Verify("ExecuteLast", "RunLast", [arr, 4]);
                Verify("ExecuteLast", "RunLast", [arr, 100]);
                Verify("ExecuteLastOrDefault", "RunLastOrDefault", [arr, 4]);
                Verify("ExecuteLastOrDefault", "RunLastOrDefault", [arr, 100]);
                Verify("ExecuteMin", "RunMin", [arr]);
                Verify("ExecuteMax", "RunMax", [arr]);
            }

            return true;
        });
    }
}
