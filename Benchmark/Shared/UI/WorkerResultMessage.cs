using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace UI;
internal sealed record WorkerResultMessage(
    string TaskId,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    double? MeanNanoseconds,
    double? ErrorNanoseconds,
    double? StdDevNanoseconds,
    long? AllocatedBytes,
    int ExitCode,
    string ArtifactFolder,
    string Log,
    int? RandomSeed = null,
    string ResultDigest = "");
