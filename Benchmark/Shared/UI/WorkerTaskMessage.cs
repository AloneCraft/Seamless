using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace UI;
internal sealed record WorkerTaskMessage(
    string Command,
    string TaskId,
    string AssemblyName,
    string TypeFullName,
    string MethodName,
    string ArgumentKey,
    BenchmarkArgument[] Arguments,
    BenchmarkSourceTab SourceTab,
    int[] PreparedElementCounts,
    int ElementCount,
    int LoopCount,
    string ElementScale,
    string ArtifactFolder,
    int? AssignedLogicalCpu = null);
