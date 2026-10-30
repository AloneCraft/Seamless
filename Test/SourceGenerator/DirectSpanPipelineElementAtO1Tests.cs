using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineElementAtO1Tests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_ElementAtO1ZeroLoop_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class ElementAtO1Service {
                public int ExecuteSkipTakeElementAt(int[] array, int skip, int take, int index) => array.Skip(skip).Take(take).ElementAt(index);
                public int RunSkipTakeElementAt(int[] array, int skip, int take, int index) => ExecuteSkipTakeElementAt(array, skip, take, index);

                public int ExecuteSkipTakeElementAtOrDefault(int[] array, int skip, int take, int index) => array.Skip(skip).Take(take).ElementAtOrDefault(index);
                public int RunSkipTakeElementAtOrDefault(int[] array, int skip, int take, int index) => ExecuteSkipTakeElementAtOrDefault(array, skip, take, index);

                public int ExecuteTakeLastElementAt(int[] array, int takeLast, int index) => array.TakeLast(takeLast).ElementAt(index);
                public int RunTakeLastElementAt(int[] array, int takeLast, int index) => ExecuteTakeLastElementAt(array, takeLast, index);

                public int ExecuteTakeLastElementAtOrDefault(int[] array, int takeLast, int index) => array.TakeLast(takeLast).ElementAtOrDefault(index);
                public int RunTakeLastElementAtOrDefault(int[] array, int takeLast, int index) => ExecuteTakeLastElementAtOrDefault(array, takeLast, index);

                public int ExecuteSkipLastTakeLastElementAt(int[] array, int skipLast, int takeLast, int index) => array.SkipLast(skipLast).TakeLast(takeLast).ElementAt(index);
                public int RunSkipLastTakeLastElementAt(int[] array, int skipLast, int takeLast, int index) => ExecuteSkipLastTakeLastElementAt(array, skipLast, takeLast, index);

                public int ExecuteSkipTakeSelectElementAt(int[] array, int skip, int take, int index) => array.Skip(skip).Take(take).Select(x => x * 10).ElementAt(index);
                public int RunSkipTakeSelectElementAt(int[] array, int skip, int take, int index) => ExecuteSkipTakeSelectElementAt(array, skip, take, index);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "ElementAtO1", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);
        Assert.Contains("(uint)", generated);

        ExecutableGeneration.Run(compilation, "Sample.ElementAtO1Service", type => {
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
                new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 },
                new[] { 5, -2, 100, 42 },
                new[] { 999 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays) {
                // Skip -> Take -> ElementAt
                Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [arr, 2, 5, 0]);
                Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [arr, 2, 5, 2]);
                Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [arr, 2, 5, 4]);
                Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [arr, 2, 5, 5]); // 境界外
                Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [arr, 2, 5, -1]); // 負数
                Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [arr, 0, 100, 3]);

                // Skip -> Take -> ElementAtOrDefault
                Verify("ExecuteSkipTakeElementAtOrDefault", "RunSkipTakeElementAtOrDefault", [arr, 2, 5, 0]);
                Verify("ExecuteSkipTakeElementAtOrDefault", "RunSkipTakeElementAtOrDefault", [arr, 2, 5, 2]);
                Verify("ExecuteSkipTakeElementAtOrDefault", "RunSkipTakeElementAtOrDefault", [arr, 2, 5, 5]); // 境界外既定値
                Verify("ExecuteSkipTakeElementAtOrDefault", "RunSkipTakeElementAtOrDefault", [arr, 2, 5, -1]); // 負数既定値

                // TakeLast -> ElementAt / ElementAtOrDefault
                Verify("ExecuteTakeLastElementAt", "RunTakeLastElementAt", [arr, 3, 0]);
                Verify("ExecuteTakeLastElementAt", "RunTakeLastElementAt", [arr, 3, 2]);
                Verify("ExecuteTakeLastElementAt", "RunTakeLastElementAt", [arr, 3, 10]); // 境界外
                Verify("ExecuteTakeLastElementAtOrDefault", "RunTakeLastElementAtOrDefault", [arr, 3, 1]);
                Verify("ExecuteTakeLastElementAtOrDefault", "RunTakeLastElementAtOrDefault", [arr, 3, 10]);

                // SkipLast -> TakeLast -> ElementAt
                Verify("ExecuteSkipLastTakeLastElementAt", "RunSkipLastTakeLastElementAt", [arr, 2, 3, 1]);
                Verify("ExecuteSkipLastTakeLastElementAt", "RunSkipLastTakeLastElementAt", [arr, 1, 2, 0]);
                Verify("ExecuteSkipLastTakeLastElementAt", "RunSkipLastTakeLastElementAt", [arr, 1, 2, 5]);

                // Select -> ElementAt
                Verify("ExecuteSkipTakeSelectElementAt", "RunSkipTakeSelectElementAt", [arr, 1, 4, 2]);
                Verify("ExecuteSkipTakeSelectElementAt", "RunSkipTakeSelectElementAt", [arr, 0, 3, 0]);
                Verify("ExecuteSkipTakeSelectElementAt", "RunSkipTakeSelectElementAt", [arr, 0, 3, 5]);
            }

            return true;
        });
    }
}
