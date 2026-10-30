using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
namespace Benchmark.TpcE.Core;
/// <summary>実行の要約(summary.json / summary.txt / txn-stats.csv)。</summary>
public sealed record RunSummary{
    public required RunOptions Options{get;init;}
    public required string Result{get;init;}
    public required double WallSeconds{get;init;}
    public required long Transactions{get;init;}
    public required long CeTransactions{get;init;}
    public required IReadOnlyDictionary<string,long> Counts{get;init;}
    /// <summary>実際の取引比率と仕様の比率(6.3.1、百分率)。</summary>
    public required IReadOnlyList<MixLine> Mix{get;init;}
    /// <summary>仕様の status が 0 以外だった件数(規模が小さいことによる警告を含む)。</summary>
    public required IReadOnlyList<StatusLine> Statuses{get;init;}
    public required IReadOnlyList<VariantSummary> Variants{get;init;}
    public required IReadOnlyList<string> Assumptions{get;init;}
    public required EnvironmentInfo Environment{get;init;}
    public sealed record MixLine(string Kind,long Count,double ActualPercent,double SpecPercent);
    public sealed record StatusLine(string Kind,int Status,long Count);
    public sealed record VariantSummary(string Name,string ShortName,IReadOnlyList<KindStats> Kinds,IReadOnlyDictionary<string,double> PhaseSeconds,double EngineSeconds,double TransactionsPerSecond,double TpsE);
    public sealed record KindStats(string Kind,long Count,double MeanMicroseconds,double P50,double P90,double P99,double Max,long AllocatedBytesPerTxn,long GcCount);
    public sealed record EnvironmentInfo(string Framework,string Os,string Architecture,int ProcessorCount,string Build,string MachineName);
    private static readonly System.Collections.Generic.Dictionary<TxnKind,double> SpecMix=new(){
        [TxnKind.BrokerVolume]=4.9,
        [TxnKind.CustomerPosition]=13,
        [TxnKind.MarketFeed]=1,
        [TxnKind.MarketWatch]=18,
        [TxnKind.SecurityDetail]=14,
        [TxnKind.TradeLookup]=8,
        [TxnKind.TradeOrder]=10.1,
        [TxnKind.TradeResult]=10,
        [TxnKind.TradeStatus]=19,
        [TxnKind.TradeUpdate]=2,
    };
    public static RunSummary Build(RunOptions options,IReadOnlyList<VariantSession> sessions,DriverState? state,TimeSpan wall,TpcEFailure? failure,IReadOnlyDictionary<(TxnKind Kind,int Status),long> statuses){
        var counts=new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);
        long mixTotal=0;
        if(state is not null){
            foreach(var pair in state.Counts){
                counts[pair.Key.ToString()]=pair.Value;
                if(SpecMix.ContainsKey(pair.Key)){
                    mixTotal+=pair.Value;
                }
            }
        }
        var mix=new List<MixLine>();
        foreach(var pair in SpecMix){
            var count=0L;
            if(state is not null){
                count=state.Counts.GetValueOrDefault(pair.Key);
            }
            var actual=0.0;
            if(mixTotal>0){
                actual=count*100.0/mixTotal;
            }
            mix.Add(new(pair.Key.ToString(),count,Math.Round(actual,3),pair.Value));
        }
        var statusLines=new List<StatusLine>();
        foreach(var pair in statuses){
            if(pair.Key.Status!=0){
                statusLines.Add(new(pair.Key.Kind.ToString(),pair.Key.Status,pair.Value));
            }
        }
        statusLines.Sort(static (left,right)=>string.CompareOrdinal(left.Kind+left.Status,right.Kind+right.Status));
        var variants=new List<VariantSummary>();
        foreach(var session in sessions){
            var kinds=new List<KindStats>();
            var total=new LatencyHistogram();
            long tradeResults=0;
            foreach(var pair in session.Timings){
                var histogram=pair.Value;
                total.Merge(histogram);
                if(pair.Key==TxnKind.TradeResult){
                    tradeResults=histogram.Count;
                }
                var allocation=0L;
                if(histogram.Count>0){
                    allocation=histogram.AllocatedBytes/histogram.Count;
                }
                kinds.Add(new(pair.Key.ToString(),histogram.Count,Round(histogram.MeanMicroseconds),Round(histogram.PercentileMicroseconds(50)),Round(histogram.PercentileMicroseconds(90)),Round(histogram.PercentileMicroseconds(99)),Round(histogram.MaxMicroseconds),allocation,histogram.GcCount));
            }
            kinds.Sort(static (left,right)=>string.CompareOrdinal(left.Kind,right.Kind));
            var engineSeconds=LatencyHistogram.TicksToMicroseconds(total.TotalTicks)/1_000_000.0;
            var tps=0.0;
            var tpsE=0.0;
            if(engineSeconds>0){
                tps=total.Count/engineSeconds;
                tpsE=tradeResults/engineSeconds;
            }
            variants.Add(new(session.Variant.Name,session.Variant.ShortName,kinds,new System.Collections.Generic.Dictionary<string,double>(session.PhaseSeconds),Round(engineSeconds),Round(tps),Round(tpsE)));
        }
        string result;
        if(failure is null){
            result="ok";
        }else{
            result=failure.Kind+": "+failure.Message;
        }
        return new(){
            Options=options,
            Result=result,
            WallSeconds=Round(wall.TotalSeconds),
            Transactions=state?.TxnIndex??0,
            CeTransactions=state?.CeCount??0,
            Counts=counts,
            Mix=mix,
            Statuses=statusLines,
            Variants=variants,
            Assumptions=TpcEAssumptions.All,
            Environment=new(RuntimeInformation.FrameworkDescription,RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture.ToString(),System.Environment.ProcessorCount,BuildKind(),System.Environment.MachineName),
        };
    }
    private static double Round(double value)=>Math.Round(value,3);
    private static string BuildKind(){
        var debuggable=typeof(RunSummary).Assembly.GetCustomAttribute<System.Diagnostics.DebuggableAttribute>();
        if(debuggable is not null&&debuggable.IsJITOptimizerDisabled){
            return "Debug";
        }
        return "Release";
    }
    public static string ToText(RunSummary summary){
        var text=new StringBuilder();
        text.AppendLine($"結果: {summary.Result}");
        text.AppendLine($"取引 {summary.Transactions:N0} 件(CE {summary.CeTransactions:N0} 件)、壁時計 {summary.WallSeconds:F1} 秒、{summary.Environment.Build}");
        text.AppendLine("取引比率(実際 / 仕様 %):");
        foreach(var line in summary.Mix){
            text.AppendLine(string.Create(CultureInfo.InvariantCulture,$"  {line.Kind,-18} {line.Count,10:N0} {line.ActualPercent,8:F3} / {line.SpecPercent,6:F1}"));
        }
        if(summary.Statuses.Count>0){
            text.AppendLine("仕様の status が 0 以外(規模が小さいことによる警告を含む):");
            foreach(var line in summary.Statuses){
                text.AppendLine($"  {line.Kind} status={line.Status}: {line.Count:N0} 件");
            }
        }
        foreach(var variant in summary.Variants){
            text.AppendLine(string.Create(CultureInfo.InvariantCulture,$"[{variant.ShortName}] {variant.Name}: エンジン内 {variant.EngineSeconds:F2} 秒、{variant.TransactionsPerSecond:F1} 取引/秒、tpsE 相当 {variant.TpsE:F2}"));
            text.AppendLine("  取引               件数     平均µs    p50µs    p90µs    p99µs    最大µs  割当B/件");
            foreach(var kind in variant.Kinds){
                text.AppendLine(string.Create(CultureInfo.InvariantCulture,$"  {kind.Kind,-18} {kind.Count,7} {kind.MeanMicroseconds,9:F1} {kind.P50,8:F1} {kind.P90,8:F1} {kind.P99,8:F1} {kind.Max,9:F1} {kind.AllocatedBytesPerTxn,9}"));
            }
            var phases=new List<string>();
            foreach(var pair in variant.PhaseSeconds){
                phases.Add(string.Create(CultureInfo.InvariantCulture,$"{pair.Key}={pair.Value:F2}s"));
            }
            text.AppendLine("  段階: "+string.Join(", ",phases));
        }
        return text.ToString();
    }
    public static string ToCsv(RunSummary summary){
        var text=new StringBuilder("variant,kind,count,mean_us,p50_us,p90_us,p99_us,max_us,alloc_bytes_per_txn,gc_count\n");
        foreach(var variant in summary.Variants){
            foreach(var kind in variant.Kinds){
                text.AppendLine(string.Create(CultureInfo.InvariantCulture,$"{variant.ShortName},{kind.Kind},{kind.Count},{kind.MeanMicroseconds},{kind.P50},{kind.P90},{kind.P99},{kind.Max},{kind.AllocatedBytesPerTxn},{kind.GcCount}"));
            }
        }
        return text.ToString();
    }
}
