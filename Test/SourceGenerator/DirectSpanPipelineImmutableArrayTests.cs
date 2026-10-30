using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using GHashSet = System.Collections.Generic.HashSet<int>;

namespace Test.SourceGenerator;

public class DirectSpanPipelineImmutableArrayTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_ImmutableArray_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;

            namespace Sample;

            public class ImmutableArrayService {
                public int[] ExecuteSkipTakeToArray(ImmutableArray<int> arr, int skip, int take) => arr.Skip(skip).Take(take).ToArray();
                public int[] RunSkipTakeToArray(ImmutableArray<int> arr, int skip, int take) => ExecuteSkipTakeToArray(arr, skip, take);

                public int ExecuteSkipTakeSum(ImmutableArray<int> arr, int skip, int take) => arr.Skip(skip).Take(take).Sum();
                public int RunSkipTakeSum(ImmutableArray<int> arr, int skip, int take) => ExecuteSkipTakeSum(arr, skip, take);

                public int ExecuteSkipTakeElementAt(ImmutableArray<int> arr, int skip, int take, int index) => arr.Skip(skip).Take(take).ElementAt(index);
                public int RunSkipTakeElementAt(ImmutableArray<int> arr, int skip, int take, int index) => ExecuteSkipTakeElementAt(arr, skip, take, index);

                public HashSet<int> ExecuteTrailingSliceToHashSet(ImmutableArray<int> arr, int skipLast, int takeLast) => arr.SkipLast(skipLast).TakeLast(takeLast).ToHashSet();
                public HashSet<int> RunTrailingSliceToHashSet(ImmutableArray<int> arr, int skipLast, int takeLast) => ExecuteTrailingSliceToHashSet(arr, skipLast, takeLast);

                public Dictionary<int, int> ExecuteSkipTakeToDictionary(ImmutableArray<int> arr, int skip, int take) => arr.Skip(skip).Take(take).ToDictionary(x => x, x => x * 2);
                public Dictionary<int, int> RunSkipTakeToDictionary(ImmutableArray<int> arr, int skip, int take) => ExecuteSkipTakeToDictionary(arr, skip, take);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "ImmutableArrayPipeline", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.ImmutableArrayService", type => {
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
                } else if (expVal is GHashSet expSet && actVal is GHashSet actSet) {
                    Assert.True(expSet.SetEquals(actSet));
                } else if (expVal is Dictionary<int, int> expDict && actVal is Dictionary<int, int> actDict) {
                    Assert.Equal(expDict.Count, actDict.Count);
                    foreach (var kvp in expDict) {
                        Assert.True(actDict.TryGetValue(kvp.Key, out var v));
                        Assert.Equal(kvp.Value, v);
                    }
                } else {
                    Assert.Equal(expVal, actVal);
                }
            }

            var testArray = ImmutableArray.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [testArray, 2, 5]);
            Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [testArray, 2, 5]);
            Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [testArray, 2, 5, 3]);
            Verify("ExecuteTrailingSliceToHashSet", "RunTrailingSliceToHashSet", [testArray, 2, 3]);
            Verify("ExecuteSkipTakeToDictionary", "RunSkipTakeToDictionary", [testArray, 2, 4]);

            var emptyArray = ImmutableArray<int>.Empty;
            Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [emptyArray, 0, 5]);
            Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [emptyArray, 0, 5]);

            return true;
        });
    }
}
