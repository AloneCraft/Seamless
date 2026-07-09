namespace Benchmark.UI.UITests;

internal static class BenchmarkUiAutomationIds{
    public const string SourceTabView = "Benchmark.SourceTabView";
    public const string TopAllSelectionCheckHost = "Benchmark.TopAllSelectionCheckHost";
    public const string TopAllSelectionCheck = "Benchmark.TopAllSelectionCheck";
    public const string AllSelectionCheckHost = "Benchmark.AllSelectionCheckHost";
    public const string AllSelectionCheck = "Benchmark.AllSelectionCheck";

    public static string SourceTab(string sourceTab) =>
        "Benchmark.SourceTab."+sourceTab;

    public static string Grid(string sourceTab) =>
        "Benchmark.Grid."+sourceTab;

    public static string CellCheckHost(string cellPath) =>
        "Benchmark.CellCheckHost."+cellPath;

    public static string CellCheck(string cellPath) =>
        "Benchmark.CellCheck."+cellPath;
}
