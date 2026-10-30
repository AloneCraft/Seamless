namespace Benchmark.Infrastructure;

/// <summary>標本内の相対標準偏差を制限する。独立実行間の再現性や統計的有意差の証明ではない。</summary>
public sealed record ComparisonPolicy(string Version="noise-v1",int MinimumSamples=7,double MaximumRelativeStandardDeviation=0.20){
    public static ComparisonPolicy Current{get;}=new();
    internal void Validate(){
        ArgumentException.ThrowIfNullOrWhiteSpace(this.Version);
        if(this.MinimumSamples<7||!double.IsFinite(this.MaximumRelativeStandardDeviation)||this.MaximumRelativeStandardDeviation<0){throw new ArgumentOutOfRangeException(nameof(this.MaximumRelativeStandardDeviation));}
    }
    internal bool IsNoisy(Measurement measurement){
        var values=measurement.Samples.Select(static sample=>sample.NanosecondsPerOperation).ToArray();
        var mean=values.Average();
        if(mean==0){return false;}
        return !double.IsFinite(mean)||Statistics.StandardDeviation(values)/mean>this.MaximumRelativeStandardDeviation;
    }
}
