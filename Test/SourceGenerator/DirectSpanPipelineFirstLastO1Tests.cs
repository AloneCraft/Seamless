using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineFirstLastO1Tests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_FirstLastO1ZeroLoop_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class FirstLastO1Service {
                public int ExecuteSkipTakeFirst(int[] array, int skip, int take) => array.Skip(skip).Take(take).First();
                public int RunSkipTakeFirst(int[] array, int skip, int take) => ExecuteSkipTakeFirst(array, skip, take);

                public int ExecuteSkipTakeFirstOrDefault(int[] array, int skip, int take) => array.Skip(skip).Take(take).FirstOrDefault();
                public int RunSkipTakeFirstOrDefault(int[] array, int skip, int take) => ExecuteSkipTakeFirstOrDefault(array, skip, take);

                public int ExecuteSkipTakeLast(int[] array, int skip, int take) => array.Skip(skip).Take(take).Last();
                public int RunSkipTakeLast(int[] array, int skip, int take) => ExecuteSkipTakeLast(array, skip, take);

                public int ExecuteSkipTakeLastOrDefault(int[] array, int skip, int take) => array.Skip(skip).Take(take).LastOrDefault();
                public int RunSkipTakeLastOrDefault(int[] array, int skip, int take) => ExecuteSkipTakeLastOrDefault(array, skip, take);

                public int ExecuteTakeLastFirst(int[] array, int takeLast) => array.TakeLast(takeLast).First();
                public int RunTakeLastFirst(int[] array, int takeLast) => ExecuteTakeLastFirst(array, takeLast);

                public int ExecuteTakeLastLast(int[] array, int takeLast) => array.TakeLast(takeLast).Last();
                public int RunTakeLastLast(int[] array, int takeLast) => ExecuteTakeLastLast(array, takeLast);

                public int ExecuteSkipLastTakeLastFirst(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).First();
                public int RunSkipLastTakeLastFirst(int[] array, int skipLast, int takeLast) => ExecuteSkipLastTakeLastFirst(array, skipLast, takeLast);

                public int ExecuteSkipLastTakeLastLast(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).Last();
                public int RunSkipLastTakeLastLast(int[] array, int skipLast, int takeLast) => ExecuteSkipLastTakeLastLast(array, skipLast, takeLast);

                public int ExecuteSkipTakeSelectFirst(int[] array, int skip, int take) => array.Skip(skip).Take(take).Select(x => x * 10).First();
                public int RunSkipTakeSelectFirst(int[] array, int skip, int take) => ExecuteSkipTakeSelectFirst(array, skip, take);

                public int ExecuteSkipTakeSelectLast(int[] array, int skip, int take) => array.Skip(skip).Take(take).Select(x => x * 10).Last();
                public int RunSkipTakeSelectLast(int[] array, int skip, int take) => ExecuteSkipTakeSelectLast(array, skip, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "FirstLastO1", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.FirstLastO1Service", type => {
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
                // First / FirstOrDefault
                Verify("ExecuteSkipTakeFirst", "RunSkipTakeFirst", [arr, 2, 5]);
                Verify("ExecuteSkipTakeFirst", "RunSkipTakeFirst", [arr, 0, 100]);
                Verify("ExecuteSkipTakeFirst", "RunSkipTakeFirst", [arr, 100, 5]); // 空

                Verify("ExecuteSkipTakeFirstOrDefault", "RunSkipTakeFirstOrDefault", [arr, 2, 5]);
                Verify("ExecuteSkipTakeFirstOrDefault", "RunSkipTakeFirstOrDefault", [arr, 100, 5]); // 空

                // Last / LastOrDefault
                Verify("ExecuteSkipTakeLast", "RunSkipTakeLast", [arr, 2, 5]);
                Verify("ExecuteSkipTakeLast", "RunSkipTakeLast", [arr, 0, 100]);
                Verify("ExecuteSkipTakeLast", "RunSkipTakeLast", [arr, 100, 5]); // 空

                Verify("ExecuteSkipTakeLastOrDefault", "RunSkipTakeLastOrDefault", [arr, 2, 5]);
                Verify("ExecuteSkipTakeLastOrDefault", "RunSkipTakeLastOrDefault", [arr, 100, 5]); // 空

                // TakeLast First / Last
                Verify("ExecuteTakeLastFirst", "RunTakeLastFirst", [arr, 3]);
                Verify("ExecuteTakeLastLast", "RunTakeLastLast", [arr, 3]);
                Verify("ExecuteTakeLastFirst", "RunTakeLastFirst", [arr, 0]); // 空

                // SkipLast -> TakeLast First / Last
                Verify("ExecuteSkipLastTakeLastFirst", "RunSkipLastTakeLastFirst", [arr, 2, 3]);
                Verify("ExecuteSkipLastTakeLastLast", "RunSkipLastTakeLastLast", [arr, 2, 3]);
                Verify("ExecuteSkipLastTakeLastFirst", "RunSkipLastTakeLastFirst", [arr, 100, 5]); // 空

                // Select First / Last
                Verify("ExecuteSkipTakeSelectFirst", "RunSkipTakeSelectFirst", [arr, 1, 4]);
                Verify("ExecuteSkipTakeSelectLast", "RunSkipTakeSelectLast", [arr, 1, 4]);
            }

            return true;
        });
    }
}
