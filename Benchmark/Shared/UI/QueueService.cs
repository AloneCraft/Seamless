using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class QueueService:IQueueService {
    public ConcurrentQueue<BenchmarkCase> CreateQueue(IEnumerable<BenchmarkCase> benchmarks) {
        return new ConcurrentQueue<BenchmarkCase>(benchmarks);
    }
}
