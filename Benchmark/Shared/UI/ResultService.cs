using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class ResultService:IResultService {
    public ReportRow ToReportRow(BenchmarkResultItem item) {
        var method = item.ElementCount>0
            ? item.Method+" [N="+item.ElementCount.ToString(CultureInfo.InvariantCulture)+"]"
            : item.Method;
        return new ReportRow(
            item.Project,
            method,
            item.TypeFullName,
            method,
            item.Status,
            item.Mean,
            string.Empty,
            string.Empty,
            item.AllocatedBytesValue?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.MeanNanoseconds??0,
            item.ErrorNanoseconds??0,
            item.StdDevNanoseconds??0,
            item.AllocatedBytesValue??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            item.ArtifactFolder,
            item.ExitCode,
            item.ArgumentKey,
            item.Arguments);
    }
}
