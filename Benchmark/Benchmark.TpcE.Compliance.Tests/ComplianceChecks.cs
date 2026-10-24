using System.Text.Json;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class ComplianceChecks{
    private static readonly string[] Names=["BrokerVolume","CustomerPosition","MarketFeed","MarketWatch","SecurityDetail","TradeLookup","TradeOrder","TradeResult","TradeStatus","TradeUpdate"];
    /// <summary>仕様の目標比率どおりの件数(合計 100,000 件)で、応答は十分速い結果を作る。</summary>
    private static DriverResult Build(Func<string,long,long> count,double tpse=2.0,double seconds=7200,double p90=0.5,double maintenanceMax=1.0,long maintenanceCount=120,double target=2.0,int users=8,long failed=0){
        var transactions=new List<object>();
        foreach(var name in Names){
            var target_=Compliance.ComplianceSpec.MixRanges[name].Target;
            var value=count(name,(long)Math.Round(target_*1000));
            transactions.Add(new{name,count=value,failed=0L,mix_percent=0.0,avg_s=p90/2,p90_s=p90,min_s=0.0,max_s=p90*2});
        }
        transactions.Add(new{name="DataMaintenance",count=maintenanceCount,failed=failed,mix_percent=0.0,avg_s=maintenanceMax/2,p90_s=maintenanceMax,min_s=0.0,max_s=maintenanceMax});
        var json=JsonSerializer.Serialize(new{customers=1000,scale_factor=500,days=1,users,target_tpse=target,measure_seconds=seconds,tpsE=tpse,nominal_tpsE=2.0,fatal=false,transactions,trade_result_per_second=new long[]{1,2},errors=Array.Empty<object>()});
        return DriverResult.Parse(json);
    }
    private static long SkewBrokerVolume(string name,long target){
        if(name=="BrokerVolume"){
            return target+100;
        }
        return target;
    }
    private static CheckResult Find(IReadOnlyList<CheckResult> checks,string clause,string name){
        foreach(var check in checks){
            if(check.Clause==clause&&check.Name==name){
                return check;
            }
        }
        throw new InvalidOperationException($"No check {clause} {name}.");
    }
    [Fact,Trait("Category","Unit")]public void AcceptsAnIdealRun(){
        {
            //目標どおりの比率・十分速い応答・公称ちょうどの性能・2 時間・7.5 分のチェックポイントなら全部合格
            var checks=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target),new MeasureSettings(7.5));
            Assert.True(Compliance.ComplianceChecks.AllPass(checks),string.Join("\n",checks.Where(static check=>check.Outcome==CheckOutcome.Fail).Select(static check=>check.Name+": "+check.Detail)));
            Assert.Equal(2.0,Compliance.ComplianceChecks.ReportedThroughput(Build(static (_,target)=>target)));
        }
    }
    [Fact,Trait("Category","Unit")]public void RejectsMixOutsideTheRequiredRange(){
        {
            //Broker-Volume が許容幅(4.875〜4.925%)の外
            var checks=Compliance.ComplianceChecks.Evaluate(Build(SkewBrokerVolume),new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.Fail,Find(checks,"6.3","取引比率 BrokerVolume").Outcome);
            Assert.False(Compliance.ComplianceChecks.AllPass(checks));
        }
    }
    [Fact,Trait("Category","Unit")]public void ChecksResponseTimeAndFailures(){
        {
            //Trade-Status は 1 秒、Trade-Order は 2 秒が上限
            var slow=Build(static (_,target)=>target,p90:1.5);
            var checks=Compliance.ComplianceChecks.Evaluate(slow,new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.Fail,Find(checks,"6.5.1.2","応答時間 TradeStatus").Outcome);
            Assert.Equal(CheckOutcome.Pass,Find(checks,"6.5.1.2","応答時間 TradeOrder").Outcome);
        }
        {
            //Data-Maintenance は 55 秒以内
            var checks=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target,maintenanceMax:60),new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.Fail,Find(checks,"6.5.1.5","応答時間 DataMaintenance").Outcome);
        }
        {
            //失敗した取引が 1 件でもあれば不合格
            var checks=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target,failed:1),new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.Fail,Find(checks,"6.7","すべての取引が成功した").Outcome);
        }
    }
    [Fact,Trait("Category","Unit")]public void ChecksThroughputIntervalAndCheckpoints(){
        {
            //性能は公称の 80〜102%。102% を超えたら報告できず、100〜102% は公称に丸める
            Assert.Null(Compliance.ComplianceChecks.ReportedThroughput(Build(static (_,target)=>target,tpse:1.5)));
            Assert.Null(Compliance.ComplianceChecks.ReportedThroughput(Build(static (_,target)=>target,tpse:2.1)));
            Assert.Equal(2.0,Compliance.ComplianceChecks.ReportedThroughput(Build(static (_,target)=>target,tpse:2.03)));
            Assert.Equal(1.7,Compliance.ComplianceChecks.ReportedThroughput(Build(static (_,target)=>target,tpse:1.7)));
        }
        {
            //短い計測は 6.6.5.1 に届かない(リハーサルの注記つき)
            var checks=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target,seconds:600),new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.Fail,Find(checks,"6.6.5.1","計測区間が 2 時間以上").Outcome);
        }
        {
            //チェックポイントが 7.5 分より長いと、データが 15 分以上古くなりうる
            var checks=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target),new MeasureSettings(10));
            Assert.Equal(CheckOutcome.Fail,Find(checks,"6.6.5.3","データがコミット済みの状態より 15 分以上古くならない").Outcome);
        }
        {
            //1 ユーザーの間隔が 1 秒を超える設定(ユーザーが多すぎる)は不合格、ペース調整なしは判定しない
            var tooMany=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target,users:40),new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.Fail,Find(tooMany,"6.5.2.2","取引の間隔が 1 秒以下").Outcome);
            var unthrottled=Compliance.ComplianceChecks.Evaluate(Build(static (_,target)=>target,target:0),new MeasureSettings(7.5));
            Assert.Equal(CheckOutcome.NotExercised,Find(unthrottled,"6.5.2.2","取引の間隔が 1 秒以下").Outcome);
        }
    }
    [Fact,Trait("Category","Unit")]public void WritesReportWithTheNotice(){
        {
            //報告の冒頭に「非監査・比較不可」が必ず出る
            var result=Build(static (_,target)=>target);
            var checks=Compliance.ComplianceChecks.Evaluate(result,new MeasureSettings(7.5));
            var markdown=ReportWriter.Markdown(result,checks,new MeasureSettings(7.5),"試験");
            Assert.Contains(Program.Notice,markdown);
            Assert.Contains("監査を受けていない",markdown);
            Assert.Contains(Program.Notice,ReportWriter.Json(result,checks,new MeasureSettings(7.5)));
        }
    }
}
