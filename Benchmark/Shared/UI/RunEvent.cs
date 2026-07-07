using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed record RunEvent(
    BenchmarkResultItem Result,
    int RunGeneration,
    long? UiRunId,
    DateTimeOffset ReceivedAt);
