using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace UI;
internal sealed record UiSnapshot(
    IReadOnlyList<RunEvent> RunningUpdates,
    IReadOnlyList<RunEvent> TerminalResults,
    bool RefreshProgress,
    bool RefreshChart,
    PipelineMetrics Metrics){
    public bool HasUpdates => this.RunningUpdates.Count>0||this.TerminalResults.Count>0||this.RefreshProgress||this.RefreshChart;
}
