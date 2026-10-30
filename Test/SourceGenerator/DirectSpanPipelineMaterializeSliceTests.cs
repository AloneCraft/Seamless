using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineMaterializeSliceTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_MaterializeSliceFusion_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class MaterializeSliceService {
                public int[] ExecuteSkipTakeToArray(int[] array, int skip, int take) => array.Skip(skip).Take(take).ToArray();
                public int[] RunSkipTakeToArray(int[] array, int skip, int take) => ExecuteSkipTakeToArray(array, skip, take);

                public int[] ExecuteSkipLastTakeLastToArray(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).ToArray();
                public int[] RunSkipLastTakeLastToArray(int[] array, int skipLast, int takeLast) => ExecuteSkipLastTakeLastToArray(array, skipLast, takeLast);

                public List<int> ExecuteSkipTakeToList(int[] array, int skip, int take) => array.Skip(skip).Take(take).ToList();
                public List<int> RunSkipTakeToList(int[] array, int skip, int take) => ExecuteSkipTakeToList(array, skip, take);

                public List<int> ExecuteTakeLastToList(int[] array, int takeLast) => array.TakeLast(takeLast).ToList();
                public List<int> RunTakeLastToList(int[] array, int takeLast) => ExecuteTakeLastToList(array, takeLast);

                public List<int> ExecuteSkipLastToList(int[] array, int skipLast) => array.SkipLast(skipLast).ToList();
                public List<int> RunSkipLastToList(int[] array, int skipLast) => ExecuteSkipLastToList(array, skipLast);

                public int[] ExecuteSkipTakeSelectToArray(int[] array, int skip, int take) => array.Skip(skip).Take(take).Select(x => x * 2).ToArray();
                public int[] RunSkipTakeSelectToArray(int[] array, int skip, int take) => ExecuteSkipTakeSelectToArray(array, skip, take);

                public List<int> ExecuteSkipTakeSelectToList(int[] array, int skip, int take) => array.Skip(skip).Take(take).Select(x => x + 10).ToList();
                public List<int> RunSkipTakeSelectToList(int[] array, int skip, int take) => ExecuteSkipTakeSelectToList(array, skip, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "MaterializeSliceFusion", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);
        Assert.Contains(".CopyTo(", generated);

        ExecutableGeneration.Run(compilation, "Sample.MaterializeSliceService", type => {
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
                if (expVal is int[] expArr && actVal is int[] actArr) {
                    Assert.Equal(expArr, actArr);
                } else if (expVal is List<int> expList && actVal is List<int> actList) {
                    Assert.Equal(expList, actList);
                } else {
                    Assert.Equal(expVal, actVal);
                }
            }

            var testArrays = new[] {
                new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 10, -5, 20, -1, 30, 40 },
                new[] { 42 },
                Array.Empty<int>()
            };

            foreach (var arr in testArrays) {
                // ToArray
                Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [arr, 2, 5]);
                Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [arr, 0, 100]);
                Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [arr, 100, 5]);
                Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [arr, -1, 3]);

                Verify("ExecuteSkipLastTakeLastToArray", "RunSkipLastTakeLastToArray", [arr, 2, 3]);
                Verify("ExecuteSkipLastTakeLastToArray", "RunSkipLastTakeLastToArray", [arr, 0, 100]);
                Verify("ExecuteSkipLastTakeLastToArray", "RunSkipLastTakeLastToArray", [arr, 100, 5]);

                // ToList
                Verify("ExecuteSkipTakeToList", "RunSkipTakeToList", [arr, 2, 5]);
                Verify("ExecuteSkipTakeToList", "RunSkipTakeToList", [arr, 0, 100]);
                Verify("ExecuteSkipTakeToList", "RunSkipTakeToList", [arr, -2, 4]);

                Verify("ExecuteTakeLastToList", "RunTakeLastToList", [arr, 3]);
                Verify("ExecuteTakeLastToList", "RunTakeLastToList", [arr, 0]);
                Verify("ExecuteTakeLastToList", "RunTakeLastToList", [arr, 20]);

                Verify("ExecuteSkipLastToList", "RunSkipLastToList", [arr, 2]);
                Verify("ExecuteSkipLastToList", "RunSkipLastToList", [arr, 0]);
                Verify("ExecuteSkipLastToList", "RunSkipLastToList", [arr, 20]);

                // Select + ToArray / ToList
                Verify("ExecuteSkipTakeSelectToArray", "RunSkipTakeSelectToArray", [arr, 2, 4]);
                Verify("ExecuteSkipTakeSelectToArray", "RunSkipTakeSelectToArray", [arr, 0, 20]);
                Verify("ExecuteSkipTakeSelectToList", "RunSkipTakeSelectToList", [arr, 1, 5]);
                Verify("ExecuteSkipTakeSelectToList", "RunSkipTakeSelectToList", [arr, 10, 2]);
            }

            return true;
        });
    }
}
