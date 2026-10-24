using System.Globalization;
using System.Text;
namespace Benchmark.TpcE.Compliance;
internal enum CheckOutcome{
    Pass,
    Fail,
    /// <summary>この実行では判定できない(例: Data-Maintenance がまだ 1 回も動いていない)。合否には数えない。</summary>
    NotExercised,
}
/// <summary>点検 1 件。<see cref="Clause"/> は仕様 1.14.0 の条項番号。</summary>
internal sealed record CheckResult(string Clause,string Name,CheckOutcome Outcome,string Detail);
/// <summary>測定の設定のうち、点検に要るもの(運転側の結果 JSON には入らない)。</summary>
internal sealed record MeasureSettings(double CheckpointMinutes);
/// <summary>運転側の結果を、仕様の規則に照らして点検する。</summary>
internal static class ComplianceChecks{
    private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    public static IReadOnlyList<CheckResult> Evaluate(DriverResult result,MeasureSettings settings){
        var checks=new List<CheckResult>();
        AddMix(result,checks);
        AddResponseTimes(result,checks);
        AddFailures(result,checks);
        AddInterval(result,checks);
        AddThroughput(result,checks);
        AddDispatch(result,checks);
        checks.Add(CheckpointCheck(settings));
        return checks;
    }
    /// <summary>全部の点検が Pass(NotExercised は数えない)。</summary>
    public static bool AllPass(IReadOnlyList<CheckResult> checks){
        foreach(var check in checks){
            if(check.Outcome==CheckOutcome.Fail){
                return false;
            }
        }
        return true;
    }
    /// <summary>6.7.1.2 の報告値。公称以下ならそのまま、公称を 2% まで超えたら公称に丸める。範囲外は null(報告できない)。</summary>
    public static double? ReportedThroughput(DriverResult result){
        var nominal=ComplianceSpec.NominalTpse(result.Customers);
        if(nominal<=0){
            return null;
        }
        var ratio=result.TpsE/nominal;
        if(ratio<ComplianceSpec.MinimumThroughputRatio||ratio>ComplianceSpec.MaximumThroughputRatio){
            return null;
        }
        return Math.Min(result.TpsE,nominal);
    }
    private static void AddMix(DriverResult result,List<CheckResult> checks){
        long total=0;
        foreach(var name in ComplianceSpec.MixRanges.Keys){
            total+=result.Find(name)?.Count ?? 0;
        }
        foreach(var (name,range) in ComplianceSpec.MixRanges){
            var count=result.Find(name)?.Count ?? 0;
            var percent=0.0;
            if(total>0){
                percent=Math.Round(100.0*count/total,3,MidpointRounding.AwayFromZero);
            }
            var inRange=percent>=range.Minimum&&percent<=range.Maximum;
            var detail=$"{percent.ToString("F3",Invariant)}% (許容 {range.Minimum.ToString("F3",Invariant)}〜{range.Maximum.ToString("F3",Invariant)}%、{count:N0} 件)";
            if(inRange){
                checks.Add(new CheckResult("6.3",$"取引比率 {name}",CheckOutcome.Pass,detail));
            }else{
                checks.Add(new CheckResult("6.3",$"取引比率 {name}",CheckOutcome.Fail,detail));
            }
        }
    }
    private static void AddResponseTimes(DriverResult result,List<CheckResult> checks){
        foreach(var (name,limit) in ComplianceSpec.ResponseLimitSeconds){
            var transaction=result.Find(name);
            if(transaction is null||transaction.Count==0){
                checks.Add(new CheckResult("6.5.1.2",$"応答時間 {name}",CheckOutcome.NotExercised,"計測区間に取引が無い"));
                continue;
            }
            var detail=$"90パーセンタイル {transaction.P90Seconds.ToString("F2",Invariant)} 秒(上限 {limit.ToString("F0",Invariant)} 秒)、平均 {transaction.AverageSeconds.ToString("F2",Invariant)} 秒";
            if(transaction.P90Seconds<=limit){
                checks.Add(new CheckResult("6.5.1.2",$"応答時間 {name}",CheckOutcome.Pass,detail));
            }else{
                checks.Add(new CheckResult("6.5.1.2",$"応答時間 {name}",CheckOutcome.Fail,detail));
            }
            if(transaction.AverageSeconds<=transaction.P90Seconds+1e-9){
                checks.Add(new CheckResult("6.5.1.4",$"平均 ≤ 90パーセンタイル {name}",CheckOutcome.Pass,detail));
            }else{
                checks.Add(new CheckResult("6.5.1.4",$"平均 ≤ 90パーセンタイル {name}",CheckOutcome.Fail,detail));
            }
        }
        var maintenance=result.Find("DataMaintenance");
        if(maintenance is null||maintenance.Count==0){
            checks.Add(new CheckResult("6.5.1.5","応答時間 DataMaintenance",CheckOutcome.NotExercised,"計測区間に取引が無い(60 秒ごとに動くので、2 分以上の計測が要る)"));
        }else{
            var detail=$"最大 {maintenance.MaximumSeconds.ToString("F2",Invariant)} 秒(上限 {ComplianceSpec.DataMaintenanceLimitSeconds.ToString("F0",Invariant)} 秒)、{maintenance.Count:N0} 回";
            if(maintenance.MaximumSeconds<=ComplianceSpec.DataMaintenanceLimitSeconds){
                checks.Add(new CheckResult("6.5.1.5","応答時間 DataMaintenance",CheckOutcome.Pass,detail));
            }else{
                checks.Add(new CheckResult("6.5.1.5","応答時間 DataMaintenance",CheckOutcome.Fail,detail));
            }
        }
    }
    private static void AddFailures(DriverResult result,List<CheckResult> checks){
        long failed=0;
        foreach(var transaction in result.Transactions){
            failed+=transaction.Failed;
        }
        var detail=$"失敗 {failed:N0} 件";
        if(result.Errors.Count>0){
            var builder=new StringBuilder(detail);
            foreach(var error in result.Errors){
                builder.Append("; ").Append(error.Count).Append(" x ").Append(error.Message);
            }
            detail=builder.ToString();
        }
        if(failed==0&&!result.Fatal){
            checks.Add(new CheckResult("6.7","すべての取引が成功した",CheckOutcome.Pass,detail));
        }else{
            if(result.Fatal){
                detail+="; 運転側が異常終了した";
            }
            checks.Add(new CheckResult("6.7","すべての取引が成功した",CheckOutcome.Fail,detail));
        }
    }
    private static void AddInterval(DriverResult result,List<CheckResult> checks){
        var detail=$"計測区間 {result.MeasureSeconds.ToString("F0",Invariant)} 秒(要求 {ComplianceSpec.MinimumMeasurementSeconds.ToString("F0",Invariant)} 秒以上)";
        if(result.MeasureSeconds>=ComplianceSpec.MinimumMeasurementSeconds){
            checks.Add(new CheckResult("6.6.5.1","計測区間が 2 時間以上",CheckOutcome.Pass,detail));
        }else{
            checks.Add(new CheckResult("6.6.5.1","計測区間が 2 時間以上",CheckOutcome.Fail,detail+"。リハーサルの短い実行では未達"));
        }
    }
    private static void AddThroughput(DriverResult result,List<CheckResult> checks){
        var nominal=ComplianceSpec.NominalTpse(result.Customers);
        var ratio=0.0;
        if(nominal>0){
            ratio=result.TpsE/nominal;
        }
        var detail=$"実測 {result.TpsE.ToString("F3",Invariant)} tpsE(非監査)、公称 {nominal.ToString("F3",Invariant)}、比 {(ratio*100).ToString("F1",Invariant)}%(要求 80〜102%)";
        var reported=ReportedThroughput(result);
        if(reported is null){
            checks.Add(new CheckResult("6.7.1.2","性能が公称の 80〜102%",CheckOutcome.Fail,detail));
        }else{
            checks.Add(new CheckResult("6.7.1.2","性能が公称の 80〜102%",CheckOutcome.Pass,detail+$"。報告値 {reported.Value.ToString("F3",Invariant)}"));
        }
    }
    private static void AddDispatch(DriverResult result,List<CheckResult> checks){
        if(result.TargetTpse<=0){
            checks.Add(new CheckResult("6.5.2.2","取引の間隔が 1 秒以下",CheckOutcome.NotExercised,"ペース調整なし(最大速度)で実行した"));
            return;
        }
        var period=result.Users/(ComplianceSpec.CeTransactionsPerTpse*result.TargetTpse);
        var detail=$"1 ユーザーあたりの間隔 {period.ToString("F3",Invariant)} 秒(上限 {ComplianceSpec.MaximumDispatchSeconds.ToString("F0",Invariant)} 秒)";
        if(period<=ComplianceSpec.MaximumDispatchSeconds){
            checks.Add(new CheckResult("6.5.2.2","取引の間隔が 1 秒以下",CheckOutcome.Pass,detail));
        }else{
            checks.Add(new CheckResult("6.5.2.2","取引の間隔が 1 秒以下",CheckOutcome.Fail,detail+"。ユーザー数を減らす"));
        }
    }
    private static CheckResult CheckpointCheck(MeasureSettings settings){
        var detail=$"チェックポイント間隔 {settings.CheckpointMinutes.ToString("F1",Invariant)} 分(上限 {ComplianceSpec.MaximumCheckpointIntervalMinutes.ToString("F1",Invariant)} 分)";
        if(settings.CheckpointMinutes<=ComplianceSpec.MaximumCheckpointIntervalMinutes){
            return new CheckResult("6.6.5.3","データがコミット済みの状態より 15 分以上古くならない",CheckOutcome.Pass,detail);
        }
        return new CheckResult("6.6.5.3","データがコミット済みの状態より 15 分以上古くならない",CheckOutcome.Fail,detail);
    }
}
