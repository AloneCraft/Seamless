using System.Globalization;
using System.Text;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance;
/// <summary>一貫性の点検 1 件。<see cref="Violations"/> が 0 なら満たしている。</summary>
internal sealed record ConsistencyResult(string Clause,string Name,long Checked,long Violations,string Detail){
    public bool Passed=>this.Violations==0;
}
/// <summary>
/// 仕様 7.3.2 の一貫性の条件 3 つを、DB の全行から数え直して点検する。初期投入の直後と、障害からの復旧の後に実行する(7.3.3)。
/// 表を 1 回ずつ走査する。SUT が取引を止めている間(作業スレッド上)でだけ呼ぶ。
/// </summary>
internal static class ConsistencyChecker{
    private const int MaximumSamples=10;
    private delegate bool BrokerRule(BrokerRow broker,long actualCount,decimal actualCommission);
    public static IReadOnlyList<ConsistencyResult> Check(TpcEDatabaseBase database){
        var completed=Code8.From("CMPT");
        var brokerOfAccount=new Dictionary<long,long>();
        foreach(var account in database.CustomerAccount.EnumerateRows()){
            brokerOfAccount[account.Id]=account.BrokerId;
        }
        var tradeCount=new Dictionary<long,long>();
        var commission=new Dictionary<long,decimal>();
        long orphanTrades=0;
        foreach(var trade in database.Trade.EnumerateRows()){
            if(trade.StatusId!=completed){
                continue;
            }
            if(!brokerOfAccount.TryGetValue(trade.AccountId,out var broker)){
                orphanTrades++;
                continue;
            }
            tradeCount.TryGetValue(broker,out var count);
            tradeCount[broker]=count+1;
            commission.TryGetValue(broker,out var total);
            commission[broker]=total+trade.Commission;
        }
        return [
            CheckBrokers(database,"7.3.2.1","B_NUM_TRADES = 完了した取引の件数",orphanTrades,tradeCount,commission,static (broker,count,_)=>broker.NumTrades==count),
            CheckBrokers(database,"7.3.2.2","B_COMM_TOTAL = 完了した取引の手数料の合計",orphanTrades,tradeCount,commission,static (broker,_,total)=>broker.CommissionTotal==total),
            CheckHoldings(database),
        ];
    }
    private static ConsistencyResult CheckBrokers(TpcEDatabaseBase database,string clause,string name,long orphanTrades,Dictionary<long,long> counts,Dictionary<long,decimal> commissions,BrokerRule rule){
        long checkedCount=0;
        var violations=orphanTrades;
        var samples=new StringBuilder();
        if(orphanTrades>0){
            samples.Append(string.Create(CultureInfo.InvariantCulture,$"口座の無い完了済み取引 {orphanTrades} 件; "));
        }
        var sampleCount=0;
        foreach(var broker in database.Broker.EnumerateRows()){
            checkedCount++;
            counts.TryGetValue(broker.Id,out var actualCount);
            commissions.TryGetValue(broker.Id,out var actualCommission);
            if(rule(broker,actualCount,actualCommission)){
                continue;
            }
            violations++;
            if(sampleCount<MaximumSamples){
                sampleCount++;
                samples.Append(string.Create(CultureInfo.InvariantCulture,$"ブローカー {broker.Id}: 記録 件数 {broker.NumTrades} 手数料 {broker.CommissionTotal}、実際 件数 {actualCount} 手数料 {actualCommission}; "));
            }
        }
        return new ConsistencyResult(clause,name,checkedCount,violations,samples.ToString());
    }
    private static ConsistencyResult CheckHoldings(TpcEDatabaseBase database){
        var quantity=new Dictionary<(long Account,Code16 Symbol),long>();
        foreach(var holding in database.Holding.EnumerateRows()){
            var key=(holding.AccountId,holding.Symbol);
            quantity.TryGetValue(key,out var total);
            quantity[key]=total+holding.Quantity;
        }
        long checkedCount=0;
        long violations=0;
        var samples=new StringBuilder();
        var sampleCount=0;
        var summarized=new HashSet<(long Account,Code16 Symbol)>();
        foreach(var summary in database.HoldingSummary.EnumerateRows()){
            checkedCount++;
            var key=(summary.AccountId,summary.Symbol);
            summarized.Add(key);
            quantity.TryGetValue(key,out var actual);
            if(summary.Quantity==actual){
                continue;
            }
            violations++;
            if(sampleCount<MaximumSamples){
                sampleCount++;
                samples.Append(string.Create(CultureInfo.InvariantCulture,$"口座 {summary.AccountId} {summary.Symbol}: 記録 {summary.Quantity}、実際 {actual}; "));
            }
        }
        // 仕様は「保有の要約ごと」だが、保有があるのに要約が無い組も食い違いとして数える(取り込みや更新の漏れを見つけるため)。
        foreach(var (key,total) in quantity){
            if(total==0||summarized.Contains(key)){
                continue;
            }
            violations++;
            if(sampleCount<MaximumSamples){
                sampleCount++;
                samples.Append(string.Create(CultureInfo.InvariantCulture,$"要約の無い保有 口座 {key.Account} {key.Symbol} 数量 {total}; "));
            }
        }
        return new ConsistencyResult("7.3.2.3","HS_QTY = 保有の数量の合計",checkedCount,violations,samples.ToString());
    }
}
