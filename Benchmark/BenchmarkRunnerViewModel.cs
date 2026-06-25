using System.Collections.ObjectModel;
using Benchmark.Worker;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Benchmark;

public sealed class BenchmarkRunnerViewModel:ObservableObject {
    private readonly IBenchmarkCatalogService catalogService;
    private readonly ICpuTopologyService cpuTopologyService;
    private readonly IBenchmarkRunService runService;
    private readonly IBenchmarkCsvService csvService;
    private readonly IBenchmarkComparisonService comparisonService;
    private CancellationTokenSource? runCancellation;
    private string 状態テキスト = "準備完了";
    private bool 実行中;
    private BenchmarkTab 選択タブ値 = BenchmarkTab.Results;
    private ComparisonPairOption 選択比較軸値;
    private CpuPresetOption? 選択CPUプリセット値;
    private スケール選択肢? 選択スケール値;
    private 指標選択肢 選択指標値;
    private double チャート高さ値 = 300;
    private bool CPUプリセット適用中;
    private IReadOnlyList<BenchmarkComparisonRow> comparisonRows = [];

    public BenchmarkRunnerViewModel(
        IBenchmarkCatalogService catalogService,
        ICpuTopologyService cpuTopologyService,
        IBenchmarkRunService runService,
        IBenchmarkCsvService csvService,
        IBenchmarkComparisonService comparisonService) {
        this.catalogService=catalogService;
        this.cpuTopologyService=cpuTopologyService;
        this.runService=runService;
        this.csvService=csvService;
        this.comparisonService=comparisonService;
        this.比較軸一覧=[
            new ComparisonPairOption("System","System FLinq/ZLinq",BenchmarkComparisonFormatter.SystemFLinq,BenchmarkComparisonFormatter.SystemZLinq),
            new ComparisonPairOption("Persisted","PersistedFLinq/PersistedLinq",BenchmarkComparisonFormatter.PersistedFLinq,BenchmarkComparisonFormatter.PersistedLinq),
            new ComparisonPairOption("Tsavorite","TsavoriteFLinq/TsavoriteLinq",BenchmarkComparisonFormatter.TsavoriteFLinq,BenchmarkComparisonFormatter.TsavoriteLinq),
            new ComparisonPairOption("PersistedLinqTsavoriteLinq","PersistedLinq/TsavoriteLinq",BenchmarkComparisonFormatter.PersistedLinq,BenchmarkComparisonFormatter.TsavoriteLinq),
            new ComparisonPairOption("PersistedFLinqTsavoriteFLinq","PersistedFLinq/TsavoriteFLinq",BenchmarkComparisonFormatter.PersistedFLinq,BenchmarkComparisonFormatter.TsavoriteFLinq),
        ];
        this.選択比較軸値=this.比較軸一覧[0];
        this.指標一覧=[
            new 指標選択肢("時間",BenchmarkMetricKind.Time),
            new 指標選択肢("割当",BenchmarkMetricKind.Allocated),
        ];
        this.選択指標値=this.指標一覧[0];
        this.スケール一覧=[
            new スケール選択肢("1/8","0.125"),
            new スケール選択肢("1/4","0.25"),
            new スケール選択肢("1/2","0.5"),
            new スケール選択肢("1","1"),
            new スケール選択肢("2","2"),
            new スケール選択肢("4","4"),
            new スケール選択肢("8","8"),
        ];
        this.選択スケール値=this.スケール一覧.First(static item => item.表示名=="1");
        this.更新コマンド=new AsyncRelayCommand(this.RefreshAsync);
        this.全選択コマンド=new RelayCommand(() => this.全行選択を設定(true));
        this.選択解除コマンド=new RelayCommand(() => this.全行選択を設定(false));
        this.実行コマンド=new AsyncRelayCommand(this.RunAsync,() => !this.実行中);
        this.中止コマンド=new RelayCommand(this.Cancel);
        this.CSV保存コマンド=new AsyncRelayCommand(this.ExportCsvAsync);
        this.CSV読込コマンド=new AsyncRelayCommand(this.LoadCsvAsync);
        this.結果表示コマンド=new RelayCommand(() => this.選択タブ=BenchmarkTab.Results);
        this.チャート表示コマンド=new RelayCommand(() => this.選択タブ=BenchmarkTab.Chart);

        foreach(var cpu in this.cpuTopologyService.Load()) {
            this.CPU一覧.Add(cpu);
            cpu.PropertyChanged+=(_,_) => {
                if(!this.CPUプリセット適用中) {
                    this.最低1CPUを選択する();
                }
            };
        }
        this.CPUプリセットを再構築();
    }

    public ObservableCollection<ベンチマーク行> ベンチマーク一覧 { get; } = [];
    public ObservableCollection<BenchmarkResultItem> 結果一覧 { get; } = [];
    public ObservableCollection<CpuSelectionItem> CPU一覧 { get; } = [];
    public ObservableCollection<CpuPresetOption> CPUプリセット一覧 { get; } = [];
    public ObservableCollection<ChartComparisonPoint> チャート点一覧 { get; } = [];
    public ObservableCollection<スケール選択肢> スケール一覧 { get; } = [];
    public IReadOnlyList<ComparisonPairOption> 比較軸一覧 { get; }
    public IReadOnlyList<指標選択肢> 指標一覧 { get; }
    public IAsyncRelayCommand 更新コマンド { get; }
    public IRelayCommand 全選択コマンド { get; }
    public IRelayCommand 選択解除コマンド { get; }
    public IAsyncRelayCommand 実行コマンド { get; }
    public IRelayCommand 中止コマンド { get; }
    public IAsyncRelayCommand CSV保存コマンド { get; }
    public IAsyncRelayCommand CSV読込コマンド { get; }
    public IRelayCommand 結果表示コマンド { get; }
    public IRelayCommand チャート表示コマンド { get; }

    public string 状態テキスト表示 {
        get => this.状態テキスト;
        set => this.SetProperty(ref this.状態テキスト,value);
    }

    public bool 実行中表示 {
        get => this.実行中;
        private set {
            if(this.SetProperty(ref this.実行中,value)) {
                this.実行コマンド.NotifyCanExecuteChanged();
            }
        }
    }

    public BenchmarkTab 選択タブ {
        get => this.選択タブ値;
        set {
            if(this.SetProperty(ref this.選択タブ値,value)) {
                this.OnPropertyChanged(nameof(this.結果表示中));
                this.OnPropertyChanged(nameof(this.チャート表示中));
            }
        }
    }

    public bool 結果表示中 => this.選択タブ==BenchmarkTab.Results;
    public bool チャート表示中 => this.選択タブ==BenchmarkTab.Chart;

    public ComparisonPairOption 選択比較軸 {
        get => this.選択比較軸値;
        set {
            if(this.SetProperty(ref this.選択比較軸値,value)) {
                this.チャートを更新();
            }
        }
    }

    public CpuPresetOption? 選択CPUプリセット {
        get => this.選択CPUプリセット値;
        set {
            if(this.SetProperty(ref this.選択CPUプリセット値,value)&&value is not null) {
                this.CPUプリセットを適用(value);
            }
        }
    }

    public スケール選択肢? 選択スケール {
        get => this.選択スケール値;
        set => this.SetProperty(ref this.選択スケール値,value);
    }

    public 指標選択肢 選択指標 {
        get => this.選択指標値;
        set {
            if(this.SetProperty(ref this.選択指標値,value)) {
                this.チャートを更新();
            }
        }
    }

    public double チャート高さ {
        get => this.チャート高さ値;
        private set => this.SetProperty(ref this.チャート高さ値,value);
    }

    public async Task InitializeAsync() {
        await this.RefreshAsync();
    }

    private async Task RefreshAsync() {
        this.実行中表示=true;
        this.状態テキスト表示="読込中";
        try {
            var loaded = await this.catalogService.LoadAsync(CancellationToken.None);
            var existingSelection = this.ベンチマーク一覧
                .Where(static item => item.選択中)
                .Select(static item => item.メソッド名)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            this.ベンチマーク一覧.Clear();
            foreach(var row in this.メソッド行を作成(loaded)) {
                row.選択中=existingSelection.Contains(row.メソッド名);
                this.ベンチマーク一覧.Add(row);
            }
            this.比較行を表へ反映();
            this.状態テキスト表示=$"{this.ベンチマーク一覧.Count} 件のベンチマーク";
        } catch(Exception ex) {
            this.状態テキスト表示="読込失敗: "+ex.Message;
        } finally {
            this.実行中表示=false;
        }
    }

    private IReadOnlyList<ベンチマーク行> メソッド行を作成(IReadOnlyList<BenchmarkCase> cases) {
        return cases
            .GroupBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static group => new ベンチマーク行(
                group.Key,
                group
                    .GroupBy(static item => item.Project,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(static item => item.Key,static item => item.First(),StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private void 全行選択を設定(bool selected) {
        foreach(var item in this.ベンチマーク一覧) {
            item.選択中=selected;
        }
    }

    private async Task RunAsync() {
        var selected = this.ベンチマーク一覧
            .Where(static item => item.選択中)
            .SelectMany(static item => item.実行ケース一覧())
            .ToArray();
        if(selected.Length==0) {
            this.状態テキスト表示="選択なし";
            return;
        }

        this.runCancellation?.Cancel();
        this.runCancellation=new CancellationTokenSource();
        this.実行中表示=true;
        this.結果一覧.Clear();
        this.comparisonRows=[];
        this.チャート点一覧.Clear();
        this.比較行を表へ反映();
        try {
            var cpus = this.CPU一覧
                .Where(static cpu => cpu.IsSelected)
                .Select(static cpu => cpu.LogicalProcessorIndex)
                .ToArray();
            _ = await this.runService.RunAsync(
                selected,
                cpus,
                this.選択スケール?.実行値??"1",
                result => MainThread.BeginInvokeOnMainThread(() => {
                    this.結果一覧.Add(result);
                    this.結果から比較行を更新();
                }),
                status => MainThread.BeginInvokeOnMainThread(() => this.状態テキスト表示=status),
                this.runCancellation.Token);
        } catch(OperationCanceledException) {
            this.状態テキスト表示="中止";
        } finally {
            this.実行中表示=false;
            this.runCancellation?.Dispose();
            this.runCancellation=null;
            this.結果から比較行を更新();
        }
    }

    private void Cancel() {
        this.runCancellation?.Cancel();
    }

    private async Task ExportCsvAsync() {
        await Task.Yield();
        if(this.結果一覧.Count==0&&this.comparisonRows.Count==0) {
            this.状態テキスト表示="結果がありません";
            return;
        }

        var root = Path.Combine(
            this.FindSolutionRoot()??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Work",
            "Benchmark",
            DateTime.Now.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture));
        var rows = this.comparisonRows.Count>0
            ? this.comparisonRows
            : this.comparisonService.BuildRows(this.結果一覧);
        var path = this.csvService.WriteComparisonCsv(root,rows);
        this.状態テキスト表示="CSV保存: "+path;
    }

    private async Task LoadCsvAsync() {
        var file = await FilePicker.PickAsync(new PickOptions {
            PickerTitle="BenchmarkComparison.csv",
            FileTypes=new FilePickerFileType(new Dictionary<DevicePlatform,IEnumerable<string>> {
                { DevicePlatform.WinUI, [".csv"] },
                { DevicePlatform.macOS, ["csv"] },
                { DevicePlatform.MacCatalyst, ["public.comma-separated-values-text"] },
            }),
        });
        if(file is null) {
            return;
        }

        try {
            this.comparisonRows=this.csvService.ReadComparisonCsv(file.FullPath);
            this.比較行を結果一覧へ読込(this.comparisonRows);
            this.比較行を表へ反映();
            this.チャートを更新();
            this.状態テキスト表示="CSV読込: "+file.FullPath;
        } catch(Exception ex) {
            this.状態テキスト表示="CSV読込失敗: "+ex.Message;
        }
    }

    private void 結果から比較行を更新() {
        if(this.結果一覧.Count==0) {
            this.comparisonRows=[];
            this.チャートを更新();
            this.比較行を表へ反映();
            return;
        }

        try {
            this.comparisonRows=this.comparisonService.BuildRows(this.結果一覧);
            this.チャートを更新();
            this.比較行を表へ反映();
        } catch(InvalidDataException) {
        }
    }

    private void 比較行を結果一覧へ読込(IReadOnlyList<BenchmarkComparisonRow> rows) {
        this.結果一覧.Clear();
        foreach(var row in rows) {
            this.結果を追加(row.Benchmark,BenchmarkComparisonFormatter.SystemFLinq,row.SystemFLinq);
            this.結果を追加(row.Benchmark,BenchmarkComparisonFormatter.SystemZLinq,row.SystemZLinq);
            this.結果を追加(row.Benchmark,BenchmarkComparisonFormatter.PersistedFLinq,row.PersistedFLinq);
            this.結果を追加(row.Benchmark,BenchmarkComparisonFormatter.PersistedLinq,row.PersistedLinq);
            this.結果を追加(row.Benchmark,BenchmarkComparisonFormatter.TsavoriteFLinq,row.TsavoriteFLinq);
            this.結果を追加(row.Benchmark,BenchmarkComparisonFormatter.TsavoriteLinq,row.TsavoriteLinq);
        }
    }

    private void 結果を追加(string method,string project,ProjectMetrics metrics) {
        if(metrics.HasValue) {
            this.結果一覧.Add(new BenchmarkResultItem(project,method,metrics));
        }
    }

    private void 比較行を表へ反映() {
        var rowsByMethod = this.comparisonRows.ToDictionary(static item => item.Benchmark,StringComparer.OrdinalIgnoreCase);
        foreach(var benchmark in this.ベンチマーク一覧) {
            benchmark.比較行を反映(rowsByMethod.TryGetValue(benchmark.メソッド名,out var row) ? row : null);
        }
    }

    private void チャートを更新() {
        this.チャート点一覧.Clear();
        foreach(var item in this.comparisonService.BuildChartPoints(this.comparisonRows,this.選択比較軸,this.選択指標.種別)) {
            this.チャート点一覧.Add(item);
        }
        this.チャート高さ=Math.Max(300,this.チャート点一覧.Count*28+72);
    }

    private void 最低1CPUを選択する() {
        if(this.CPU一覧.Count==0||this.CPU一覧.Any(static cpu => cpu.IsSelected)) {
            return;
        }

        this.CPU一覧[0].IsSelected=true;
    }

    private void CPUプリセットを再構築() {
        this.CPUプリセット一覧.Clear();
        if(this.CPU一覧.Count==0) {
            return;
        }

        this.CPUプリセットを追加("FIRST","CPU 0",this.CPU一覧.Take(1));
        this.CPUプリセットを追加("ALL","全CPU",this.CPU一覧);
        this.CPUプリセットを追加("P","Pコア",this.CPU一覧.Where(static cpu => cpu.CoreKind==CpuCoreKind.Performance));
        this.CPUプリセットを追加("E","Eコア",this.CPU一覧.Where(static cpu => cpu.CoreKind==CpuCoreKind.Efficiency));
        this.CPUプリセットを追加("U","Unknown",this.CPU一覧.Where(static cpu => cpu.CoreKind==CpuCoreKind.Unknown));
        this.選択CPUプリセット値=this.CPUプリセット一覧.FirstOrDefault();
        this.OnPropertyChanged(nameof(this.選択CPUプリセット));
    }

    private void CPUプリセットを追加(string key,string displayName,IEnumerable<CpuSelectionItem> cpus) {
        var indexes = cpus
            .Select(static cpu => cpu.LogicalProcessorIndex)
            .Distinct()
            .Order()
            .ToArray();
        if(indexes.Length>0) {
            this.CPUプリセット一覧.Add(new CpuPresetOption(key,displayName,indexes));
        }
    }

    private void CPUプリセットを適用(CpuPresetOption preset) {
        var selected = preset.LogicalProcessorIndexes.ToHashSet();
        this.CPUプリセット適用中=true;
        try {
            foreach(var cpu in this.CPU一覧) {
                cpu.IsSelected=selected.Contains(cpu.LogicalProcessorIndex);
            }
        } finally {
            this.CPUプリセット適用中=false;
        }

        this.最低1CPUを選択する();
    }

    private string? FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }
}
