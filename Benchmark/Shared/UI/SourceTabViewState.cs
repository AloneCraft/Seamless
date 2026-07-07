using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class SourceTabViewState:ObservableObject{
    private double chartHeight = 300;
    private string benchmarkRatioSummaryText = string.Empty;

    public SourceTabViewState(BenchmarkSourceTabDefinition definition){
        this.Definition=definition;
    }

    public BenchmarkSourceTabDefinition Definition { get; }
    public ObservableRangeCollection<TableRow> Rows { get; } = [];
    public ObservableCollection<ChartComparisonPoint> ChartPoints { get; } = [];
    public ObservableCollection<DiagnosticRow> LatencyHotspotRows { get; } = [];
    public ObservableCollection<DiagnosticRow> AllocationDiagnosticRows { get; } = [];
    public ObservableCollection<DiagnosticRow> CpuTimelineRows { get; } = [];
    public ObservableCollection<BenchmarkRankedRatioPoint> ImprovementCandidateRankPoints { get; } = [];
    public ObservableCollection<BenchmarkRankedRatioPoint> TimeRatioWorstPoints { get; } = [];
    public ObservableCollection<BenchmarkRankedRatioPoint> AllocationRatioWorstPoints { get; } = [];
    public ObservableCollection<BenchmarkCategoryRatioPoint> CategoryRatioPoints { get; } = [];
    public ObservableCollection<BenchmarkBadgeItem> MissingFailureBadges { get; } = [];
    public ObservableCollection<BenchmarkQualityGaugePoint> MeasurementQualityGauges { get; } = [];
    public ObservableCollection<BenchmarkBadgeItem> FallbackDiagnosticBadges { get; } = [];
    public ObservableCollection<BenchmarkTrendSeries> HistoryTrendSeries { get; } = [];
    public ObservableCollection<BenchmarkTrendSeries> ScaleTrendSeries { get; } = [];
    public IReadOnlyList<BenchmarkComparisonRow> ComparisonRows { get; set; } = [];

    public double ChartHeight{
        get => this.chartHeight;
        set => this.SetProperty(ref this.chartHeight,value);
    }

    public string BenchmarkRatioSummaryText{
        get => this.benchmarkRatioSummaryText;
        set => this.SetProperty(ref this.benchmarkRatioSummaryText,value);
    }
}
