using InlineDataAttribute=Benchmark.Dynamic.Worker.RegressionDataAttribute;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using FactAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
using TheoryAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
using Microsoft.CodeAnalysis;

using System.Text;
namespace Test.SourceGenerator;

internal static class GeneratedSourceAssert {
    // 診断用属性に原文のメソッド本体を保存しているため、実コードの形状判定から属性を除く。
    public static string ExecutableText(string source)=>global::Benchmark.Dynamic.Worker.GeneratedCodeText.Executable(source);
    public static void True(
        bool condition,
        string message,
        string generatedSource,
        ITestOutputHelper? output,
        IReadOnlyCollection<Diagnostic>? diagnostics = null) {
        if(condition) {
            return;
        }

        var details = BuildFailureMessage(message,generatedSource,diagnostics);
        WriteFailureOutput(output,details);
        Assert.Fail(details);
    }

    public static void NoErrors(
        IReadOnlyCollection<Diagnostic> diagnostics,
        string generatedSource,
        ITestOutputHelper? output) {
        if(diagnostics.Count==0) {
            return;
        }

        var details = BuildFailureMessage("Source generator diagnostics contained errors.",generatedSource,diagnostics);
        WriteFailureOutput(output,details);
        Assert.Fail(details);
    }

    private static string BuildFailureMessage(
        string message,
        string generatedSource,
        IReadOnlyCollection<Diagnostic>? diagnostics) {
        var builder = new StringBuilder();
        builder.AppendLine(message);

        if(diagnostics is { Count:>0 }) {
            builder.AppendLine();
            builder.AppendLine("Diagnostics:");
            foreach(var diagnostic in diagnostics) {
                builder.AppendLine(diagnostic.ToString());
            }
        }

        builder.AppendLine();
        builder.AppendLine("Generated Source:");
        builder.AppendLine(generatedSource);
        return builder.ToString();
    }

    private static void WriteFailureOutput(ITestOutputHelper? output,string details) {
        if(output is null) {
            return;
        }

        foreach(var line in details.Replace("\r\n","\n").Split('\n')) {
            output.WriteLine(line);
        }
    }
}
