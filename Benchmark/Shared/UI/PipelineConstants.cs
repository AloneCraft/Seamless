using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal static class PipelineConstants{
    public const int UiResponsivenessTargetMs = 100;
    public const int UiHeartbeatIntervalMs = 50;
    public const int UiRenderIntervalMs = 100;
    public const int ProgressRefreshIntervalMs = 500;
    public const int ChartRefreshIntervalMs = 1000;
    public const int PersistenceBatchIntervalMs = 1000;
    public const int MaxTerminalResultsPerTick = 6;
}
