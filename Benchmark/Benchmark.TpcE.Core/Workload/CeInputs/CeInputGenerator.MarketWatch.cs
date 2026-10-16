namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Market-Watch の業種で見る会社 ID の範囲の幅(starting_co_id から ending_co_id = starting_co_id + 4,999 まで、3.3.4.1)。</summary>
    private const long MarketWatchCompanyRange=5000;
    /// <summary>Market-Watch の start_date を選ぶ NURand の A(仕様は「非一様」とだけ書くので推測)。</summary>
    private const long MarketWatchStartDateA=255;
    /// <summary>
    /// Market-Watch の入力を作る(3.3.4.1、6.4.1)。60% はウォッチリスト(顧客 ID)、35% は口座の保有(口座 ID)、5% は業種(業種名と会社 ID の範囲)で銘柄群を指す。
    /// 顧客は層の取引頻度で重みづけて選び、口座はその顧客の口座から一様に選ぶ。start_date は DAILY_MARKET の営業日から NURand で選ぶ。
    /// </summary>
    public MarketWatchInput MarketWatch(ref Rng rng){
        long customerId=0;
        long accountId=0;
        var industryName="";
        long startingCompanyId=0;
        long endingCompanyId=0;
        var pick=rng.Range(0,99);
        if(pick<TpcESpec.MarketWatchByWatchListPercent){
            customerId=this.Model.PickCustomerByTier(ref rng);
        }else if(pick<TpcESpec.MarketWatchByWatchListPercent+TpcESpec.MarketWatchByAccountPercent){
            var customer=this.Model.PickCustomerByTier(ref rng);
            accountId=this.Model.PickAccountOf(customer,ref rng);
        }else{
            industryName=TpcEModel.IndustryName(rng.Range(0,TpcEModel.IndustryCount-1));
            // 会社 ID の範囲 [1, Companies] から幅 5,000 の範囲を選ぶ。会社が 5,000 社以下なら全社を覆う範囲にする。
            var lastStart=Math.Max(1,this.Context.Scale.Companies-CeInputGenerator.MarketWatchCompanyRange+1);
            startingCompanyId=rng.Range(1,lastStart);
            endingCompanyId=startingCompanyId+CeInputGenerator.MarketWatchCompanyRange-1;
        }
        var days=this.Context.MarketDays;
        if(days.Length==0){
            throw new InvalidOperationException("Market-Watch: CeContext.MarketDays is empty.");
        }
        var dayIndex=rng.NURand(CeInputGenerator.MarketWatchStartDateA,0,days.Length-1,0);
        return new MarketWatchInput{
            AccountId=accountId,
            CustomerId=customerId,
            IndustryName=industryName,
            StartingCompanyId=startingCompanyId,
            EndingCompanyId=endingCompanyId,
            StartDate=days[dayIndex],
        };
    }
}
