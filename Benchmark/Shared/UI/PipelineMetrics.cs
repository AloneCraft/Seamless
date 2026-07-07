using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed record PipelineMetrics(
    long ReceivedEvents,
    long ConsumedEvents,
    long RenderedEvents,
    long CoalescedRunningEvents,
    int RunningUpdateCount,
    int TerminalBacklogCount,
    int PendingPersistenceEvents,
    double LastRenderMilliseconds,
    double LastPersistenceMilliseconds,
    double LastDispatcherGapMilliseconds,
    long DispatcherStallCount,
    int ActiveRunningCount,
    int DirtyRunningCount,
    int TerminalAppliedCount);
