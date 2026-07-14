using Microsoft.CodeAnalysis;

using System.Text;
namespace Test.SourceGenerator;

internal static class GeneratedSourceAssert {
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
