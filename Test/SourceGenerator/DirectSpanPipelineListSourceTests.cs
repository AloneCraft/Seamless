using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class DirectSpanPipelineListSourceTests(ITestOutputHelper output) {
    [Fact]
    public void DirectSpanPipeline_ListSource_GenerateAndExecuteCorrectly() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class ListSourceService {
                public int[] ExecuteSkipTakeToArray(List<int> list, int skip, int take) => list.Skip(skip).Take(take).ToArray();
                public int[] RunSkipTakeToArray(List<int> list, int skip, int take) => ExecuteSkipTakeToArray(list, skip, take);

                public int ExecuteSkipTakeSum(List<int> list, int skip, int take) => list.Skip(skip).Take(take).Sum();
                public int RunSkipTakeSum(List<int> list, int skip, int take) => ExecuteSkipTakeSum(list, skip, take);

                public int ExecuteSkipTakeElementAt(List<int> list, int skip, int take, int index) => list.Skip(skip).Take(take).ElementAt(index);
                public int RunSkipTakeElementAt(List<int> list, int skip, int take, int index) => ExecuteSkipTakeElementAt(list, skip, take, index);
            }
            """;

        var (compilation, generated) = ExecutableGeneration.Generate(source, "ListSourcePipeline", 1);
        output.WriteLine("=== GENERATED CODE ===\n" + generated + "\n======================");
        Assert.DoesNotContain("UnsupportedLinqExpansion(", generated);

        ExecutableGeneration.Run(compilation, "Sample.ListSourceService", type => {
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
                } else {
                    Assert.Equal(expVal, actVal);
                }
            }

            var testList = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Verify("ExecuteSkipTakeToArray", "RunSkipTakeToArray", [testList, 2, 5]);
            Verify("ExecuteSkipTakeSum", "RunSkipTakeSum", [testList, 2, 5]);
            Verify("ExecuteSkipTakeElementAt", "RunSkipTakeElementAt", [testList, 2, 5, 3]);

            return true;
        });
    }
}
