using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class CpuTopologyService:ICpuTopologyService {
    public IReadOnlyList<CpuSelectionItem> Load() {
        return CpuTopologyProvider.Load()
            .OrderBy(static cpu => cpu.LogicalProcessorIndex)
            .Select((cpu,index) => new CpuSelectionItem(
                cpu.LogicalProcessorIndex,
                cpu.CoreKind,
                cpu.PhysicalCoreIndex,
                cpu.KindCoreIndex,
                index==0))
            .ToArray();
    }
}
