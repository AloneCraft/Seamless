using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public interface IWorkerProcessService:IBenchmarkJobRunner {
    Task<RunResult> RunAsync(BenchmarkCase benchmark,string runRoot,BenchmarkSourceTab sourceTab,IReadOnlyList<int> preparedElementCounts,string scale,int logicalCpu,CancellationToken cancellationToken);
}
