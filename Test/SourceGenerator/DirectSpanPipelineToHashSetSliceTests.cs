using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using GHashSet = System.Collections.Generic.HashSet<int>;

namespace Test.SourceGenerator;

public class DirectSpanPipelineToHashSetSliceTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_ToHashSetSliceFusion_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class HashSetSliceService {
                public HashSet<int> ExecuteSkipTakeToHashSet(int[] array, int skip, int take) => array.Skip(skip).Take(take).ToHashSet();
                public HashSet<int> RunSkipTakeToHashSet(int[] array, int skip, int take) => ExecuteSkipTakeToHashSet(array, skip, take);

                public HashSet<int> ExecuteTakeLastToHashSet(int[] array, int takeLast) => array.TakeLast(takeLast).ToHashSet();
                public HashSet<int> RunTakeLastToHashSet(int[] array, int takeLast) => ExecuteTakeLastToHashSet(array, takeLast);

                public HashSet<int> ExecuteSkipLastToHashSet(int[] array, int skipLast) => array.SkipLast(skipLast).ToHashSet();
                public HashSet<int> RunSkipLastToHashSet(int[] array, int skipLast) => ExecuteSkipLastToHashSet(array, skipLast);

                public HashSet<int> ExecuteSkipLastTakeLastToHashSet(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).ToHashSet();
                public HashSet<int> RunSkipLastTakeLastToHashSet(int[] array, int skipLast, int takeLast) => ExecuteSkipLastTakeLastToHashSet(array, skipLast, takeLast);

                public HashSet<int> ExecuteSkipTakeSelectToHashSet(int[] array, int skip, int take) => array.Skip(skip).Take(take).Select(x => x * 2).ToHashSet();
                public HashSet<int> RunSkipTakeSelectToHashSet(int[] array, int skip, int take) => ExecuteSkipTakeSelectToHashSet(array, skip, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "ToHashSetSliceFusion", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.HashSetSliceService", type => {
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
                if (expVal is GHashSet expSet && actVal is GHashSet actSet) {
                    Assert.True(expSet.SetEquals(actSet), $"Expected: [{string.Join(", ", expSet)}], Actual: [{string.Join(", ", actSet)}]");
                    Assert.Equal(expSet.Count, actSet.Count);
                } else {
                    Assert.Equal(expVal, actVal);
                }
            }

            var testArrays = new[] {
                new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 10, -5, 20, -1, 30, 40 },
                new[] { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4 }, // 重複要素あり
                new[] { 42 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays) {
                // Skip + Take
                Verify("ExecuteSkipTakeToHashSet", "RunSkipTakeToHashSet", [arr, 2, 5]);
                Verify("ExecuteSkipTakeToHashSet", "RunSkipTakeToHashSet", [arr, 0, 100]);
                Verify("ExecuteSkipTakeToHashSet", "RunSkipTakeToHashSet", [arr, 100, 5]);
                Verify("ExecuteSkipTakeToHashSet", "RunSkipTakeToHashSet", [arr, -1, 3]);

                // TakeLast
                Verify("ExecuteTakeLastToHashSet", "RunTakeLastToHashSet", [arr, 3]);
                Verify("ExecuteTakeLastToHashSet", "RunTakeLastToHashSet", [arr, 0]);
                Verify("ExecuteTakeLastToHashSet", "RunTakeLastToHashSet", [arr, 20]);

                // SkipLast
                Verify("ExecuteSkipLastToHashSet", "RunSkipLastToHashSet", [arr, 2]);
                Verify("ExecuteSkipLastToHashSet", "RunSkipLastToHashSet", [arr, 0]);
                Verify("ExecuteSkipLastToHashSet", "RunSkipLastToHashSet", [arr, 20]);

                // SkipLast + TakeLast
                Verify("ExecuteSkipLastTakeLastToHashSet", "RunSkipLastTakeLastToHashSet", [arr, 2, 3]);
                Verify("ExecuteSkipLastTakeLastToHashSet", "RunSkipLastTakeLastToHashSet", [arr, 0, 100]);
                Verify("ExecuteSkipLastTakeLastToHashSet", "RunSkipLastTakeLastToHashSet", [arr, 100, 5]);

                // Skip + Take + Select
                Verify("ExecuteSkipTakeSelectToHashSet", "RunSkipTakeSelectToHashSet", [arr, 2, 4]);
                Verify("ExecuteSkipTakeSelectToHashSet", "RunSkipTakeSelectToHashSet", [arr, 0, 10]);
            }
            return true;
        });
    }
}
