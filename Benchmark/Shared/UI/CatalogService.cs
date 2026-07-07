using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class CatalogService:ICatalogService {
    public Task<IReadOnlyList<BenchmarkCase>> LoadAsync(CancellationToken cancellationToken) {
        return Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            return BenchmarkCatalog.Load();
        },cancellationToken);
    }
}
