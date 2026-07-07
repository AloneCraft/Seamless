using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public interface IRunService {
    Task<RunReport> RunAsync(
        IReadOnlyList<BenchmarkCase> benchmarks,
        IReadOnlyList<int> logicalCpus,
        Func<IReadOnlyList<int>> logicalCpuProvider,
        IReadOnlyList<int> elementCounts,
        BenchmarkSourceTab sourceTab,
        BenchmarkExecutionProfile executionProfile,
        TimeSpan adaptiveThreshold,
        bool restartFromRequestedElementCount,
        string? runRootOverride,
        Func<IReadOnlyList<BenchmarkCase>>? benchmarkProvider,
        Action<BenchmarkResultItem> resultReceived,
        Action<string> statusChanged,
        BenchmarkRunExecutionOptions? executionOptions,
        CancellationToken cancellationToken);
}
