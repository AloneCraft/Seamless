using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed record AutoScrollRequest(BenchmarkSourceTab SourceTab,string MethodName,string RowKey,int Sequence);

public sealed class ImprovementCandidateRow:ObservableObject{
    private bool isSelected;

    public ImprovementCandidateRow(BenchmarkImprovementCandidate candidate,bool isSelected){
        this.Candidate=candidate;
        this.isSelected=isSelected;
    }

    public event EventHandler? SelectionChanged;

    internal BenchmarkImprovementCandidate Candidate { get; }
    public string Key => this.Candidate.Key;
    public string Category => this.Candidate.Category;
    public string MethodName => this.Candidate.MethodName;
    public string SourceTabText => this.Candidate.SourceTab.DisplayName;
    public string WorstElementCountText =>
        this.Candidate.TimeElementCount==this.Candidate.AllocatedElementCount
            ? this.Candidate.TimeElementCount.ToString("N0",CultureInfo.InvariantCulture)
            : this.Candidate.TimeElementCount.ToString("N0",CultureInfo.InvariantCulture)+"/"+this.Candidate.AllocatedElementCount.ToString("N0",CultureInfo.InvariantCulture);
    public string TimeRatioText => BenchmarkImprovementCandidateBuilder.FormatRatio(this.Candidate.TimeRatio);
    public string AllocatedRatioText => BenchmarkImprovementCandidateBuilder.FormatRatio(this.Candidate.AllocatedRatio);
    public string TimeGaugeText => CreateGaugeText("時間",this.Candidate.TimeRatio);
    public string AllocatedGaugeText => CreateGaugeText("メモリ",this.Candidate.AllocatedRatio);
    public double TimeGaugeFraction => CreateGaugeFraction(this.Candidate.TimeRatio);
    public double AllocatedGaugeFraction => CreateGaugeFraction(this.Candidate.AllocatedRatio);
    public double WorstLeftRatioSortValue => Math.Max(
        BenchmarkImprovementCandidateBuilder.GetRankValue(this.Candidate.TimeRatio),
        BenchmarkImprovementCandidateBuilder.GetRankValue(this.Candidate.AllocatedRatio));
    public string LeftTimeText => BenchmarkImprovementCandidateBuilder.FormatTime(this.Candidate.TimeLeft.TimeNanoseconds);
    public string RightTimeText => BenchmarkImprovementCandidateBuilder.FormatTime(this.Candidate.TimeRight.TimeNanoseconds);
    public string LeftAllocatedText => BenchmarkImprovementCandidateBuilder.FormatAllocated(this.Candidate.AllocatedLeft.AllocatedBytes);
    public string RightAllocatedText => BenchmarkImprovementCandidateBuilder.FormatAllocated(this.Candidate.AllocatedRight.AllocatedBytes);
    public string TooltipText =>
        this.SourceTabText+"\n"
        +this.Candidate.SourceTab.LeftProject+" / "+this.Candidate.SourceTab.RightProject+"\n"
        +"Time "+this.TimeRatioText+" / Alloc "+this.AllocatedRatioText;

    private static double CreateGaugeFraction(RatioValue ratio) =>
        ratio.Kind switch{
            RatioValueKind.Numeric when ratio.NumericValue is { } value => Math.Clamp(value/3.0,0,1),
            RatioValueKind.LeftOnly => 1.0,
            _ => 0.0,
        };

    private static string CreateGaugeText(string label,RatioValue ratio) =>
        ratio.Kind==RatioValueKind.Numeric
            ? label+" "+BenchmarkImprovementCandidateBuilder.FormatRatio(ratio)+"x"
            : label+" "+BenchmarkImprovementCandidateBuilder.FormatRatio(ratio);

    public bool IsSelected{
        get => this.isSelected;
        set{
            if(this.SetProperty(ref this.isSelected,value)){
                this.SelectionChanged?.Invoke(this,EventArgs.Empty);
            }
        }
    }
}
