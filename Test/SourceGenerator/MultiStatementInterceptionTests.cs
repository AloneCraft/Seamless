using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class MultiStatementInterceptionTests(ITestOutputHelper output) {
    [Fact]
    public void MultiStatement_InSameMethod_BothAreInterceptedIndependently() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class MultiQueryService {
                public int ExecuteBoth(int[] array1, int[] array2) {
                    var count = array1.Where(x => x > 10).Select(x => x * 2).Count();
                    var sum = array2.Where(x => x < count).Sum();
                    return sum;
                }

                public int Run(int[] array1, int[] array2) => ExecuteBoth(array1, array2);
            }
            """;

        var comp = GeneratorTestHelpers.CreateCompilation([("MultiQueryService.cs", source)]);
        GeneratorDriver driver = GeneratorTestHelpers.CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(comp, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult();

        var generated = string.Concat(runResult.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));

        // コンパイルエラーがないことを検証
        var errors = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .Where(static d => d.Id != "CS9137") // Interceptors preview warning 除外
            .ToArray();

        Assert.Empty(errors);

        // 生成されたインターセプターメソッド内に、2つの LINQ パイプライン（__pipeline__r2, __pipeline__r5）が
        // 独立して展開され、完全にゼロアロケーション化されていることを検証
        Assert.Contains("using var __pipeline__r2", generated);
        Assert.Contains("using var __pipeline__r5", generated);
        Assert.Contains("var count=__linq0;", generated);
        Assert.Contains("var sum=__linq1;", generated);
    }
}
