using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineToDictionarySliceTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_ToDictionarySliceFusion_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class DictionarySliceService {
                public Dictionary<int, int> ExecuteSkipTakeToDictKeyOnly(int[] array, int skip, int take) => array.Skip(skip).Take(take).ToDictionary(x => x);
                public Dictionary<int, int> RunSkipTakeToDictKeyOnly(int[] array, int skip, int take) => ExecuteSkipTakeToDictKeyOnly(array, skip, take);

                public Dictionary<int, int> ExecuteSkipTakeToDictKeyVal(int[] array, int skip, int take) => array.Skip(skip).Take(take).ToDictionary(x => x, x => x * 10);
                public Dictionary<int, int> RunSkipTakeToDictKeyVal(int[] array, int skip, int take) => ExecuteSkipTakeToDictKeyVal(array, skip, take);

                public Dictionary<int, string> ExecuteTakeLastToDict(int[] array, int takeLast) => array.TakeLast(takeLast).ToDictionary(x => x, x => x.ToString());
                public Dictionary<int, string> RunTakeLastToDict(int[] array, int takeLast) => ExecuteTakeLastToDict(array, takeLast);

                public Dictionary<int, int> ExecuteSkipLastToDict(int[] array, int skipLast) => array.SkipLast(skipLast).ToDictionary(x => x);
                public Dictionary<int, int> RunSkipLastToDict(int[] array, int skipLast) => ExecuteSkipLastToDict(array, skipLast);

                public Dictionary<int, int> ExecuteSkipLastTakeLastToDict(int[] array, int skipLast, int takeLast) => array.SkipLast(skipLast).TakeLast(takeLast).ToDictionary(x => x, x => x + 100);
                public Dictionary<int, int> RunSkipLastTakeLastToDict(int[] array, int skipLast, int takeLast) => ExecuteSkipLastTakeLastToDict(array, skipLast, takeLast);

                public Dictionary<int, int> ExecuteSkipTakeSelectToDict(int[] array, int skip, int take) => array.Skip(skip).Take(take).Select(x => x * 2).ToDictionary(x => x, x => x + 1);
                public Dictionary<int, int> RunSkipTakeSelectToDict(int[] array, int skip, int take) => ExecuteSkipTakeSelectToDict(array, skip, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "ToDictionarySliceFusion", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.DictionarySliceService", type => {
            var instance = Activator.CreateInstance(type)!;

            void Verify<TKey, TVal>(string expectedName, string actualName, object[] args) where TKey : notnull {
                var expected = type.GetMethod(expectedName)!;
                var actual = type.GetMethod(actualName)!;
                (object? value, Type? exception) Invoke(global::System.Reflection.MethodInfo m) {
                    try { return (m.Invoke(instance, args), null); }
                    catch (global::System.Reflection.TargetInvocationException ex) { return (null, ex.InnerException?.GetType()); }
                }
                var (expVal, expEx) = Invoke(expected);
                var (actVal, actEx) = Invoke(actual);
                Assert.Equal(expEx, actEx);
                if (expVal is Dictionary<TKey, TVal> expDict && actVal is Dictionary<TKey, TVal> actDict) {
                    Assert.Equal(expDict.Count, actDict.Count);
                    foreach (var kvp in expDict) {
                        Assert.True(actDict.TryGetValue(kvp.Key, out var actualVal));
                        Assert.Equal(kvp.Value, actualVal);
                    }
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
                // Key only
                Verify<int, int>("ExecuteSkipTakeToDictKeyOnly", "RunSkipTakeToDictKeyOnly", [arr, 2, 5]);
                Verify<int, int>("ExecuteSkipTakeToDictKeyOnly", "RunSkipTakeToDictKeyOnly", [arr, 0, 100]);
                Verify<int, int>("ExecuteSkipTakeToDictKeyOnly", "RunSkipTakeToDictKeyOnly", [arr, 100, 5]);
                Verify<int, int>("ExecuteSkipTakeToDictKeyOnly", "RunSkipTakeToDictKeyOnly", [arr, -1, 3]);

                // Key + Value
                Verify<int, int>("ExecuteSkipTakeToDictKeyVal", "RunSkipTakeToDictKeyVal", [arr, 2, 4]);
                Verify<int, int>("ExecuteSkipTakeToDictKeyVal", "RunSkipTakeToDictKeyVal", [arr, 0, 10]);

                // TakeLast
                Verify<int, string>("ExecuteTakeLastToDict", "RunTakeLastToDict", [arr, 3]);
                Verify<int, string>("ExecuteTakeLastToDict", "RunTakeLastToDict", [arr, 0]);
                Verify<int, string>("ExecuteTakeLastToDict", "RunTakeLastToDict", [arr, 20]);

                // SkipLast
                Verify<int, int>("ExecuteSkipLastToDict", "RunSkipLastToDict", [arr, 2]);
                Verify<int, int>("ExecuteSkipLastToDict", "RunSkipLastToDict", [arr, 0]);
                Verify<int, int>("ExecuteSkipLastToDict", "RunSkipLastToDict", [arr, 20]);

                // SkipLast + TakeLast
                Verify<int, int>("ExecuteSkipLastTakeLastToDict", "RunSkipLastTakeLastToDict", [arr, 2, 3]);
                Verify<int, int>("ExecuteSkipLastTakeLastToDict", "RunSkipLastTakeLastToDict", [arr, 0, 100]);
                Verify<int, int>("ExecuteSkipLastTakeLastToDict", "RunSkipLastTakeLastToDict", [arr, 100, 5]);

                // Skip + Take + Select
                Verify<int, int>("ExecuteSkipTakeSelectToDict", "RunSkipTakeSelectToDict", [arr, 2, 4]);
                Verify<int, int>("ExecuteSkipTakeSelectToDict", "RunSkipTakeSelectToDict", [arr, 0, 10]);
            }
            return true;
        });
    }
}
