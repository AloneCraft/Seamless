using System.Globalization;
using System.Text;
using System.Text.Json;
namespace Benchmark.TpcE.Compliance;
/// <summary>点検の結果を Markdown と JSON に書く。冒頭に常に「非監査・比較不可」を出す。</summary>
internal static class ReportWriter{
    private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    public static string Markdown(DriverResult result,IReadOnlyList<CheckResult> checks,MeasureSettings settings,string title){
        var builder=new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"**{Program.Notice}**");
        builder.AppendLine();
        builder.AppendLine("TPC の認定監査人による監査を受けていない自己計測です。tpsE・TPC-E の名称を付けた数字を公開してはいけません(公正使用の規則)。");
        builder.AppendLine();
        var reported=ComplianceChecks.ReportedThroughput(result);
        builder.AppendLine("## 結果");
        builder.AppendLine();
        builder.AppendLine("| 項目 | 値 |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| 顧客数 | {result.Customers:N0} |");
        builder.AppendLine($"| 初期取引の営業日数 | {result.Days} |");
        builder.AppendLine($"| ユーザー数 | {result.Users} |");
        builder.AppendLine($"| 計測区間 | {result.MeasureSeconds.ToString("F0",Invariant)} 秒 |");
        builder.AppendLine($"| 実測 tpsE 相当(非監査) | {result.TpsE.ToString("F3",Invariant)} |");
        builder.AppendLine($"| 公称 | {ComplianceSpec.NominalTpse(result.Customers).ToString("F3",Invariant)} |");
        if(reported is null){
            builder.AppendLine("| 報告できる性能 | なし(公称の 80〜102% の外) |");
        }else{
            builder.AppendLine($"| 報告できる性能 | {reported.Value.ToString("F3",Invariant)} |");
        }
        builder.AppendLine($"| チェックポイント間隔 | {settings.CheckpointMinutes.ToString("F1",Invariant)} 分 |");
        builder.AppendLine();
        builder.AppendLine("## 取引");
        builder.AppendLine();
        builder.AppendLine("| 取引 | 件数 | 失敗 | 比率 % | 平均 秒 | 90% 秒 | 最大 秒 |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach(var transaction in result.Transactions){
            builder.AppendLine($"| {transaction.Name} | {transaction.Count:N0} | {transaction.Failed:N0} | {transaction.MixPercent.ToString("F3",Invariant)} | {transaction.AverageSeconds.ToString("F2",Invariant)} | {transaction.P90Seconds.ToString("F2",Invariant)} | {transaction.MaximumSeconds.ToString("F2",Invariant)} |");
        }
        builder.AppendLine();
        builder.AppendLine("## 点検(仕様 1.14.0)");
        builder.AppendLine();
        builder.AppendLine("| 条項 | 点検 | 結果 | 内容 |");
        builder.AppendLine("|---|---|---|---|");
        foreach(var check in checks){
            builder.AppendLine($"| {check.Clause} | {check.Name} | {Mark(check.Outcome)} | {check.Detail} |");
        }
        builder.AppendLine();
        if(ComplianceChecks.AllPass(checks)){
            builder.AppendLine("判定: 点検した項目はすべて満たした(未実施の項目を除く)。");
        }else{
            builder.AppendLine("判定: 満たしていない項目がある。");
        }
        builder.AppendLine();
        builder.AppendLine("この表に無い要件(ACID 試験、価格、開示報告書、監査、製品の一般提供)は、別の手順または対象外です。`audit-report` を参照してください。");
        return builder.ToString();
    }
    public static string Json(DriverResult result,IReadOnlyList<CheckResult> checks,MeasureSettings settings){
        var reported=ComplianceChecks.ReportedThroughput(result);
        var document=new{
            notice=Program.Notice,
            customers=result.Customers,
            days=result.Days,
            users=result.Users,
            measure_seconds=result.MeasureSeconds,
            tpse_unaudited=result.TpsE,
            nominal_tpse=ComplianceSpec.NominalTpse(result.Customers),
            reported_tpse_unaudited=reported,
            checkpoint_minutes=settings.CheckpointMinutes,
            all_pass=ComplianceChecks.AllPass(checks),
            checks=checks.Select(static check=>new{clause=check.Clause,name=check.Name,outcome=check.Outcome.ToString(),detail=check.Detail}).ToArray(),
        };
        return JsonSerializer.Serialize(document,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
    }
    private static string Mark(CheckOutcome outcome){
        switch(outcome){
            case CheckOutcome.Pass:
                return "合格";
            case CheckOutcome.Fail:
                return "**不合格**";
            default:
                return "未判定";
        }
    }
}
