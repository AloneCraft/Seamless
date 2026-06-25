using System.Globalization;
using Benchmark.Worker;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark;

public enum BenchmarkTab {
    Results,
    Chart,
}

public enum BenchmarkMetricKind {
    Time,
    Allocated,
}

public sealed record ComparisonPairOption(string Key,string DisplayName,string LeftProject,string RightProject);

public sealed record CpuPresetOption(string Key,string DisplayName,IReadOnlyList<int> LogicalProcessorIndexes);

public sealed record スケール選択肢(string 表示名,string 実行値);

public sealed record 指標選択肢(string 表示名,BenchmarkMetricKind 種別);

public sealed class ベンチマーク行:ObservableObject {
    private bool 選択;
    private string 比率System時間 = string.Empty;
    private string 比率System割当 = string.Empty;
    private string systemFLinq時間 = string.Empty;
    private string systemFLinq割当 = string.Empty;
    private string systemZLinq時間 = string.Empty;
    private string systemZLinq割当 = string.Empty;
    private string 比率Persisted時間 = string.Empty;
    private string 比率Persisted割当 = string.Empty;
    private string persistedFLinq時間 = string.Empty;
    private string persistedFLinq割当 = string.Empty;
    private string persistedLinq時間 = string.Empty;
    private string persistedLinq割当 = string.Empty;
    private string 比率Tsavorite時間 = string.Empty;
    private string 比率Tsavorite割当 = string.Empty;
    private string tsavoriteFLinq時間 = string.Empty;
    private string tsavoriteFLinq割当 = string.Empty;
    private string tsavoriteLinq時間 = string.Empty;
    private string tsavoriteLinq割当 = string.Empty;

    public ベンチマーク行(string メソッド名,IReadOnlyDictionary<string,BenchmarkCase> 実行ケースByプロジェクト) {
        this.メソッド名=メソッド名;
        this.ベンチマーク名=メソッド名;
        this.実行ケースByプロジェクト=実行ケースByプロジェクト;
    }

    public string メソッド名 { get; }
    public string ベンチマーク名 { get; }
    public IReadOnlyDictionary<string,BenchmarkCase> 実行ケースByプロジェクト { get; }

    public bool 選択中 {
        get => this.選択;
        set {
            if(this.SetProperty(ref this.選択,value)) {
                this.OnPropertyChanged(nameof(this.チェック表示));
            }
        }
    }

    public string チェック表示 => this.選択中 ? "✓" : string.Empty;

    public string 比率System時間表示 {
        get => this.比率System時間;
        private set => this.SetProperty(ref this.比率System時間,value);
    }

    public string 比率System割当表示 {
        get => this.比率System割当;
        private set => this.SetProperty(ref this.比率System割当,value);
    }

    public string SystemFLinq時間表示 {
        get => this.systemFLinq時間;
        private set => this.SetProperty(ref this.systemFLinq時間,value);
    }

    public string SystemFLinq割当表示 {
        get => this.systemFLinq割当;
        private set => this.SetProperty(ref this.systemFLinq割当,value);
    }

    public string SystemZLinq時間表示 {
        get => this.systemZLinq時間;
        private set => this.SetProperty(ref this.systemZLinq時間,value);
    }

    public string SystemZLinq割当表示 {
        get => this.systemZLinq割当;
        private set => this.SetProperty(ref this.systemZLinq割当,value);
    }

    public string 比率Persisted時間表示 {
        get => this.比率Persisted時間;
        private set => this.SetProperty(ref this.比率Persisted時間,value);
    }

    public string 比率Persisted割当表示 {
        get => this.比率Persisted割当;
        private set => this.SetProperty(ref this.比率Persisted割当,value);
    }

    public string PersistedFLinq時間表示 {
        get => this.persistedFLinq時間;
        private set => this.SetProperty(ref this.persistedFLinq時間,value);
    }

    public string PersistedFLinq割当表示 {
        get => this.persistedFLinq割当;
        private set => this.SetProperty(ref this.persistedFLinq割当,value);
    }

    public string PersistedLinq時間表示 {
        get => this.persistedLinq時間;
        private set => this.SetProperty(ref this.persistedLinq時間,value);
    }

    public string PersistedLinq割当表示 {
        get => this.persistedLinq割当;
        private set => this.SetProperty(ref this.persistedLinq割当,value);
    }

    public string 比率Tsavorite時間表示 {
        get => this.比率Tsavorite時間;
        private set => this.SetProperty(ref this.比率Tsavorite時間,value);
    }

    public string 比率Tsavorite割当表示 {
        get => this.比率Tsavorite割当;
        private set => this.SetProperty(ref this.比率Tsavorite割当,value);
    }

    public string TsavoriteFLinq時間表示 {
        get => this.tsavoriteFLinq時間;
        private set => this.SetProperty(ref this.tsavoriteFLinq時間,value);
    }

    public string TsavoriteFLinq割当表示 {
        get => this.tsavoriteFLinq割当;
        private set => this.SetProperty(ref this.tsavoriteFLinq割当,value);
    }

    public string TsavoriteLinq時間表示 {
        get => this.tsavoriteLinq時間;
        private set => this.SetProperty(ref this.tsavoriteLinq時間,value);
    }

    public string TsavoriteLinq割当表示 {
        get => this.tsavoriteLinq割当;
        private set => this.SetProperty(ref this.tsavoriteLinq割当,value);
    }

    public IReadOnlyList<BenchmarkCase> 実行ケース一覧() {
        return this.実行ケースByプロジェクト
            .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
            .Select(static item => item.Value)
            .ToArray();
    }

    public void 比較行を反映(BenchmarkComparisonRow? row) {
        if(row is null) {
            this.表示をクリア();
            return;
        }

        var value = row.Value;
        this.比率System時間表示=this.比率表示(value.SystemRatio.TimeRatio);
        this.比率System割当表示=this.比率表示(value.SystemRatio.AllocatedRatio);
        this.SystemFLinq時間表示=this.時間表示(value.SystemFLinq);
        this.SystemFLinq割当表示=this.割当表示(value.SystemFLinq);
        this.SystemZLinq時間表示=this.時間表示(value.SystemZLinq);
        this.SystemZLinq割当表示=this.割当表示(value.SystemZLinq);
        this.比率Persisted時間表示=this.比率表示(value.PersistedRatio.TimeRatio);
        this.比率Persisted割当表示=this.比率表示(value.PersistedRatio.AllocatedRatio);
        this.PersistedFLinq時間表示=this.時間表示(value.PersistedFLinq);
        this.PersistedFLinq割当表示=this.割当表示(value.PersistedFLinq);
        this.PersistedLinq時間表示=this.時間表示(value.PersistedLinq);
        this.PersistedLinq割当表示=this.割当表示(value.PersistedLinq);
        this.比率Tsavorite時間表示=this.比率表示(value.TsavoriteRatio.TimeRatio);
        this.比率Tsavorite割当表示=this.比率表示(value.TsavoriteRatio.AllocatedRatio);
        this.TsavoriteFLinq時間表示=this.時間表示(value.TsavoriteFLinq);
        this.TsavoriteFLinq割当表示=this.割当表示(value.TsavoriteFLinq);
        this.TsavoriteLinq時間表示=this.時間表示(value.TsavoriteLinq);
        this.TsavoriteLinq割当表示=this.割当表示(value.TsavoriteLinq);
    }

    private void 表示をクリア() {
        this.比率System時間表示=string.Empty;
        this.比率System割当表示=string.Empty;
        this.SystemFLinq時間表示=string.Empty;
        this.SystemFLinq割当表示=string.Empty;
        this.SystemZLinq時間表示=string.Empty;
        this.SystemZLinq割当表示=string.Empty;
        this.比率Persisted時間表示=string.Empty;
        this.比率Persisted割当表示=string.Empty;
        this.PersistedFLinq時間表示=string.Empty;
        this.PersistedFLinq割当表示=string.Empty;
        this.PersistedLinq時間表示=string.Empty;
        this.PersistedLinq割当表示=string.Empty;
        this.比率Tsavorite時間表示=string.Empty;
        this.比率Tsavorite割当表示=string.Empty;
        this.TsavoriteFLinq時間表示=string.Empty;
        this.TsavoriteFLinq割当表示=string.Empty;
        this.TsavoriteLinq時間表示=string.Empty;
        this.TsavoriteLinq割当表示=string.Empty;
    }

    private string 時間表示(ProjectMetrics metrics) {
        return BenchmarkFormatting.FormatNanoseconds(metrics.TimeNanoseconds);
    }

    private string 割当表示(ProjectMetrics metrics) {
        return BenchmarkFormatting.FormatBytes(metrics.AllocatedBytes);
    }

    private string 比率表示(RatioValue ratio) {
        return ratio.Kind switch {
            RatioValueKind.Numeric when ratio.NumericValue is { } value => value.ToString("0.00",CultureInfo.InvariantCulture),
            RatioValueKind.BothZero => "0",
            RatioValueKind.LeftOnly => "∞",
            RatioValueKind.RightOnly => "0",
            _ => string.Empty,
        };
    }
}

public sealed class CpuSelectionItem:ObservableObject {
    private bool isSelected;

    public CpuSelectionItem(int logicalProcessorIndex,CpuCoreKind coreKind,int physicalCoreIndex,int kindCoreIndex,bool isSelected) {
        this.LogicalProcessorIndex=logicalProcessorIndex;
        this.CoreKind=coreKind;
        this.PhysicalCoreIndex=physicalCoreIndex;
        this.KindCoreIndex=kindCoreIndex;
        this.isSelected=isSelected;
    }

    public int LogicalProcessorIndex { get; }
    public CpuCoreKind CoreKind { get; }
    public int PhysicalCoreIndex { get; }
    public int KindCoreIndex { get; }
    public string CoreLabel => this.CoreKind switch {
        CpuCoreKind.Performance => "P"+this.KindCoreIndex,
        CpuCoreKind.Efficiency => "E"+this.KindCoreIndex,
        _ => "Core "+this.PhysicalCoreIndex,
    };
    public string DisplayName => $"CPU {this.LogicalProcessorIndex} ({this.CoreLabel})";

    public bool IsSelected {
        get => this.isSelected;
        set {
            if(this.SetProperty(ref this.isSelected,value)) {
                this.OnPropertyChanged(nameof(this.チェック表示));
            }
        }
    }

    public string チェック表示 => this.IsSelected ? "✓" : string.Empty;
}

public sealed class BenchmarkResultItem {
    public BenchmarkResultItem(BenchmarkRunResult result) {
        this.Project=result.Project;
        this.Method=result.Method;
        this.Name=$"{result.Project} / {result.Method}";
        this.Status=this.状態を日本語化(result.Status);
        this.Mean=result.Mean;
        this.AllocatedBytes=result.AllocatedBytes?.ToString("N0",CultureInfo.InvariantCulture)??string.Empty;
        this.AllocatedBytesValue=result.AllocatedBytes;
        this.MeanNanoseconds=result.MeanNanoseconds;
        this.ErrorNanoseconds=result.ErrorNanoseconds;
        this.StdDevNanoseconds=result.StdDevNanoseconds;
        this.ArtifactFolder=result.ArtifactFolder;
        this.ExitCode=result.ExitCode;
    }

    public BenchmarkResultItem(string project,string method,ProjectMetrics metrics) {
        this.Project=project;
        this.Method=method;
        this.Name=$"{project} / {method}";
        this.Status=metrics.HasValue?"完了":"未取得";
        this.Mean=BenchmarkFormatting.FormatNanoseconds(metrics.TimeNanoseconds);
        this.AllocatedBytes=BenchmarkFormatting.FormatBytes(metrics.AllocatedBytes);
        this.AllocatedBytesValue=metrics.AllocatedBytes;
        this.MeanNanoseconds=metrics.TimeNanoseconds;
        this.ErrorNanoseconds=null;
        this.StdDevNanoseconds=null;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=metrics.HasValue?0:null;
    }

    private BenchmarkResultItem(BenchmarkCase benchmark,Exception exception) {
        this.Project=benchmark.Project;
        this.Method=benchmark.MethodName;
        this.Name=$"{benchmark.Project} / {benchmark.MethodName}";
        this.Status="失敗";
        this.Mean=exception.GetType().Name;
        this.AllocatedBytes=string.Empty;
        this.ArtifactFolder=string.Empty;
        this.ExitCode=1;
    }

    public string Project { get; }
    public string Method { get; }
    public string Name { get; }
    public string Status { get; }
    public string Mean { get; }
    public string AllocatedBytes { get; }
    public long? AllocatedBytesValue { get; }
    public double? MeanNanoseconds { get; }
    public double? ErrorNanoseconds { get; }
    public double? StdDevNanoseconds { get; }
    public string ArtifactFolder { get; }
    public int? ExitCode { get; }

    public static BenchmarkResultItem Failed(BenchmarkCase benchmark,Exception exception) => new(benchmark,exception);

    private string 状態を日本語化(string status) {
        return status switch {
            "Completed" => "完了",
            "Failed" => "失敗",
            _ => status,
        };
    }
}

public sealed class ChartComparisonPoint {
    public ChartComparisonPoint(
        string benchmark,
        string displayName,
        double leftValue,
        double rightValue,
        string leftText,
        string rightText,
        string tooltipText,
        double? ratio) {
        this.Benchmark=benchmark;
        this.DisplayName=displayName;
        this.LeftValue=leftValue;
        this.RightValue=rightValue;
        this.LeftText=leftText;
        this.RightText=rightText;
        this.TooltipText=tooltipText;
        this.Ratio=ratio;
    }

    public string Benchmark { get; }
    public string DisplayName { get; }
    public double LeftValue { get; }
    public double RightValue { get; }
    public string LeftText { get; }
    public string RightText { get; }
    public string TooltipText { get; }
    public double? Ratio { get; }
}

public sealed record BenchmarkRunReport(string? CsvPath);
