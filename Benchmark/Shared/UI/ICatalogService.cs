using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public interface ICatalogService {
    Task<IReadOnlyList<BenchmarkCase>> LoadAsync(CancellationToken cancellationToken);
}
