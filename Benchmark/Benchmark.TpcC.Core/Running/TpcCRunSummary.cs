using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
namespace Benchmark.TpcC.Core;
/// <summary>実行の要約(summary.json / summary.txt / txn-stats.csv)。</summary>
public sealed record TpcCRunSummary{
    public required TpcCRunOptions Options{get;init;}
    public required string Result{get;init;}
    public required double WallSeconds{get;init;}
    public required long Transactions{get;init;}
    public required IReadOnlyDictionary<string,long> Counts{get;init;}
    public required IReadOnlyList<VariantSummary> Variants{get;init;}
    public required EnvironmentInfo Environment{get;init;}
    public sealed record VariantSummary(string Name,string ShortName,IReadOnlyList<KindStats> Kinds,IReadOnlyDictionary<string,double> PhaseSeconds,double EngineSeconds,double TransactionsPerSecond);
    public sealed record KindStats(string Kind,long Count,double MeanMicroseconds,double P50,double P90,double P99,double Max,long AllocatedBytesPerTxn,long GcCount);
    public sealed record EnvironmentInfo(string Framework,string Os,string Architecture,int ProcessorCount,string MachineName);
    public static TpcCRunSummary Build(TpcCRunOptions options,IReadOnlyList<TpcCVariantSession> sessions,TpcCDriverState? state,TimeSpan wall,TpcCFailure? failure){
        var variants=new List<VariantSummary>();
        foreach(var session in sessions){
            var kinds=new List<KindStats>();
            var total=new LatencyHistogram();
            foreach(var pair in session.Timings.OrderBy(static pair=>pair.Key)){
                var histogram=pair.Value;
                total.Merge(histogram);
                var allocation=0L;
                if(histogram.Count>0){
                    allocation=histogram.AllocatedBytes/histogram.Count;
                }
                kinds.Add(new(pair.Key.ToString(),histogram.Count,Math.Round(histogram.MeanMicroseconds,1),Math.Round(histogram.PercentileMicroseconds(50),1),Math.Round(histogram.PercentileMicroseconds(90),1),Math.Round(histogram.PercentileMicroseconds(99),1),Math.Round(histogram.MaxMicroseconds,1),allocation,histogram.GcCount));
            }
            var engineSeconds=LatencyHistogram.TicksToMicroseconds(total.TotalTicks)/1_000_000.0;
            var perSecond=0.0;
            if(engineSeconds>0){
                perSecond=total.Count/engineSeconds;
            }
            variants.Add(new(session.Name,session.ShortName,kinds,session.PhaseSeconds.ToDictionary(static pair=>pair.Key,static pair=>Math.Round(pair.Value,2)),Math.Round(engineSeconds,2),Math.Round(perSecond,0)));
        }
        var result="ok";
        if(failure is not null){
            result="failed: "+failure.Kind;
        }
        return new(){
            Options=options,
            Result=result,
            WallSeconds=Math.Round(wall.TotalSeconds,1),
            Transactions=state?.TxnIndex??0,
            Counts=state?.Counts??new Dictionary<string,long>(),
            Variants=variants,
            Environment=new(RuntimeInformation.FrameworkDescription,RuntimeInformation.OSDescription,RuntimeInformation.OSArchitecture.ToString(),System.Environment.ProcessorCount,System.Environment.MachineName),
        };
    }
    public static string ToText(TpcCRunSummary summary){
        var text=new StringBuilder();
        text.AppendLine($"結果: {summary.Result}、取引 {summary.Transactions:N0} 件、{summary.WallSeconds:F1} 秒");
        text.AppendLine($"形: {TpcCRunOptions.ModeName(summary.Options.Mode)}、倉庫 {summary.Options.Warehouses}、区画 {summary.Options.Partitions}、品目 {summary.Options.Items}、地区あたり顧客 {summary.Options.CustomersPerDistrict}、端末 {summary.Options.Terminals}");
        text.AppendLine("取引の件数(種類 → 件数。:rolledback は業務上の巻き戻し):");
        foreach(var pair in summary.Counts.OrderBy(static pair=>pair.Key,StringComparer.Ordinal)){
            text.AppendLine($"  {pair.Key,-24}{pair.Value,10:N0}");
        }
        foreach(var variant in summary.Variants){
            text.AppendLine($"[{variant.ShortName}] {variant.Name}: 取引の実行 {variant.EngineSeconds:F2} 秒({variant.TransactionsPerSecond:N0} 件/秒)、段階 {string.Join(", ",variant.PhaseSeconds.Select(static pair=>$"{pair.Key}={pair.Value:F1}s"))}");
            foreach(var kind in variant.Kinds){
                text.AppendLine($"    {kind.Kind,-12}{kind.Count,9:N0} 件  平均 {kind.MeanMicroseconds,9:F1} µs  p50 {kind.P50,9:F1}  p90 {kind.P90,9:F1}  p99 {kind.P99,10:F1}  最大 {kind.Max,11:F1}  {kind.AllocatedBytesPerTxn,8:N0} B/件");
            }
        }
        return text.ToString();
    }
    public static string ToCsv(TpcCRunSummary summary){
        var csv=new StringBuilder("variant,kind,count,mean_us,p50_us,p90_us,p99_us,max_us,allocated_bytes_per_txn,gc_count\n");
        foreach(var variant in summary.Variants){
            foreach(var kind in variant.Kinds){
                csv.AppendLine(string.Create(CultureInfo.InvariantCulture,$"{variant.ShortName},{kind.Kind},{kind.Count},{kind.MeanMicroseconds},{kind.P50},{kind.P90},{kind.P99},{kind.Max},{kind.AllocatedBytesPerTxn},{kind.GcCount}"));
            }
        }
        return csv.ToString();
    }
}
