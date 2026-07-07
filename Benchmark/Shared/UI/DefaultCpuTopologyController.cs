using System.Globalization;

namespace UI;

// ここを編集する場合:
// - CPU セクション生成ロジック
// - CPU 選択の一時状態復元
internal sealed class DefaultCpuTopologyController:ICpuTopologyController {
    private readonly ICpuTopologyControllerContext context;
    private readonly ICpuTopologyService cpuTopologyService;
    private bool isApplyingCpuSelection;
    private int[] selectedLogicalCpus = [];

    public DefaultCpuTopologyController(ICpuTopologyControllerContext context,ICpuTopologyService cpuTopologyService) {
        this.context=context;
        this.cpuTopologyService=cpuTopologyService;
    }

    public void Initialize() {
        foreach(var cpu in this.cpuTopologyService.Load()) {
            this.context.CpuItems.Add(cpu);
            cpu.PropertyChanged+=(_,args) => {
                if(args.PropertyName==nameof(CpuSelectionItem.IsSelected)&&!this.isApplyingCpuSelection) {
                    if(this.context.IsRefreshing) {
                        this.isApplyingCpuSelection=true;
                        try {
                            foreach(var item in this.context.CpuItems) {
                                item.IsSelected=this.selectedLogicalCpus.Contains(item.LogicalProcessorIndex);
                            }
                        }
                        finally {
                            this.isApplyingCpuSelection=false;
                            this.RefreshCpuSectionStates();
                        }
                        return;
                    }

                    this.EnsureAtLeastOneCpuSelected(cpu);
                    this.RefreshSelectedLogicalCpus();
                    this.RefreshCpuSectionStates();
                }
            };
        }
        this.RebuildCpuSections();
        this.RefreshSelectedLogicalCpus();
    }

    public void ToggleCpu(CpuSelectionItem cpu) {
        if(this.context.IsRefreshing) {
            return;
        }

        cpu.IsSelected=!cpu.IsSelected;
    }

    public void ToggleCpuSection(CpuTableSection section) {
        if(this.context.IsRefreshing) {
            return;
        }

        var selected = section.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(section.Cells.Select(static cell => cell.Cpu),selected);
    }

    public void ToggleCpuColumn(CpuTableSection section,int columnIndex) {
        if(this.context.IsRefreshing) {
            return;
        }

        var cells = section.Rows
            .Where(row => columnIndex<row.Cells.Count)
            .Select(row => row.Cells[columnIndex])
            .ToArray();
        var selected = section.GetColumnState(columnIndex)!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(cells.Select(static cell => cell.Cpu),selected);
    }

    public void ToggleCpuRow(CpuTableRow row) {
        if(this.context.IsRefreshing) {
            return;
        }

        var selected = row.State!=CpuSelectionState.Checked;
        this.ApplyCpuSelection(row.Cells.Select(static cell => cell.Cpu),selected);
    }

    public void ApplyCpuSelection(IEnumerable<CpuSelectionItem> cpus,bool selected) {
        if(this.context.IsRefreshing&&!this.isApplyingCpuSelection) {
            return;
        }

        var targets = cpus.Distinct().ToArray();
        if(targets.Length==0) {
            return;
        }

        this.isApplyingCpuSelection=true;
        try {
            if(!selected) {
                var selectedOutsideTargets = this.context.CpuItems.Any(cpu => cpu.IsSelected&&!targets.Contains(cpu));
                if(!selectedOutsideTargets&&this.context.CpuItems.Any(static cpu => cpu.IsSelected)) {
                    var keep = targets.FirstOrDefault(static cpu => cpu.IsSelected)??targets[0];
                    foreach(var cpu in targets) {
                        cpu.IsSelected=ReferenceEquals(cpu,keep);
                    }
                    return;
                }
            }

            foreach(var cpu in targets) {
                cpu.IsSelected=selected;
            }
        }
        finally {
            this.isApplyingCpuSelection=false;
            this.EnsureAtLeastOneCpuSelected(null);
            this.RefreshSelectedLogicalCpus();
            this.RefreshCpuSectionStates();
        }
    }

    public void EnsureAtLeastOneCpuSelected(CpuSelectionItem? fallback) {
        if(this.context.CpuItems.Count==0||this.context.CpuItems.Any(static cpu => cpu.IsSelected)) {
            return;
        }

        (fallback??this.context.CpuItems[0]).IsSelected=true;
    }

    public IReadOnlyList<int> GetSelectedLogicalCpus() =>
        Volatile.Read(ref this.selectedLogicalCpus);

    public void RefreshSelectedLogicalCpus() =>
        Volatile.Write(ref this.selectedLogicalCpus,this.CreateSelectedLogicalCpus());

    public void RebuildCpuSections() {
        this.context.CpuSections.Clear();
        this.AddCpuSection("P core",CpuCoreKind.Performance);
        this.AddCpuSection("E core",CpuCoreKind.Efficiency);
        this.AddCpuSection("Unknown core",CpuCoreKind.Unknown);
        this.context.NotifyCpuTopologyPropertyChanged(nameof(RunnerViewModel.PerformanceCpuSections));
        this.context.NotifyCpuTopologyPropertyChanged(nameof(RunnerViewModel.EfficiencyCpuSections));
        this.context.NotifyCpuTopologyPropertyChanged(nameof(RunnerViewModel.UnknownCpuSections));
    }

    private int[] CreateSelectedLogicalCpus() =>
        this.context.CpuItems
            .Where(static cpu => cpu.IsSelected)
            .Select(static cpu => cpu.LogicalProcessorIndex)
            .ToArray();

    private void AddCpuSection(string title,CpuCoreKind kind) {
        var groups = this.context.CpuItems
            .Where(cpu => cpu.CoreKind==kind)
            .GroupBy(static cpu => cpu.PhysicalCoreIndex)
            .OrderBy(static group => group.Min(static cpu => cpu.LogicalProcessorIndex))
            .Select(static group => group.OrderBy(static cpu => cpu.LogicalProcessorIndex).ToArray())
            .Where(static group => group.Length>0)
            .ToArray();
        if(groups.Length==0) {
            return;
        }

        var columnCount = groups.Max(static group => group.Length);
        var columns = Enumerable.Range(0,columnCount)
            .Select(static index => new CpuTableColumn(index,"Select"))
            .ToArray();
        var rows = groups
            .Select(group => new CpuTableRow(
                CreateCpuRangeLabel(group),
                group[0].CoreKind,
                group.Select(static cpu => new CpuTableCell(cpu)).ToArray(),
                columnCount))
            .ToArray();
        this.context.CpuSections.Add(new CpuTableSection(title,kind,columns,rows));
    }

    private static string CreateCpuRangeLabel(IReadOnlyList<CpuSelectionItem> cpus) {
        var min = cpus.Min(static cpu => cpu.LogicalProcessorIndex);
        var max = cpus.Max(static cpu => cpu.LogicalProcessorIndex);
        return min==max
            ? min.ToString(CultureInfo.InvariantCulture)
            : min.ToString(CultureInfo.InvariantCulture)+"-"+max.ToString(CultureInfo.InvariantCulture);
    }

    private void RefreshCpuSectionStates() {
        foreach(var section in this.context.CpuSections) {
            section.RefreshState();
            foreach(var column in section.Columns) {
                this.context.NotifyCpuTopologyPropertyChanged("CpuColumnState"+section.Title+column.Index);
            }
        }
    }
}
