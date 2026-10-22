namespace Shared.TpcE.Transactions;
/// <summary>Market-Watch(3.3.4)のフレーム 1。手順と問い合わせは既存の <see cref="MarketWatchFrames"/> を使い、ここは構造体との変換だけ。</summary>
internal sealed class MarketWatchHarness{
    private readonly MarketWatchFrames frames=new();
    public void Frame1(TpcEDatabase db,MarketWatchTxnInput input,MarketWatchFrame1Output output){
        var result=this.frames.Run(db,new MarketWatchInput{
            AccountId=input.AcctId,
            CustomerId=input.CId,
            IndustryName=input.IndustryName,
            StartingCompanyId=input.StartingCoId,
            EndingCompanyId=input.EndingCoId,
            StartDate=TpcETime.DateOf(input.StartDay.ToMilliseconds()),
        });
        output.PctChange=(double)result.PctChange;
    }
}
