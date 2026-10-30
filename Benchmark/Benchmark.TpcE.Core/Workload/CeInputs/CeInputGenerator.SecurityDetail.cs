namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>
    /// Security-Detail の入力を作る(3.3.5.1、6.4.1)。銘柄は全銘柄から一様、max_rows_to_return は 5〜20 の一様、
    /// start_day は DAILY_MARKET の営業日から「最後の max_rows_to_return 日の手前」までの一様、access_lob_flag は 1%。
    /// </summary>
    public SecurityDetailInput SecurityDetail(ref Rng rng){
        var security=rng.Range(1L,this.Model.Scale.Securities);
        var maxRows=rng.Range(TpcESpec.MinDayLength,TpcESpec.MaxDayLength);
        var days=this.Context.MarketDays;
        if(days.Length==0){
            days=this.Model.MarketDays();
        }
        var startDay=TpcEModel.LastMarketDay;
        if(days.Length>0){
            // 先頭から数えて max_rows_to_return 日ぶんが残る日までを選ぶ(営業日が足りなければ先頭の日)。
            startDay=days[rng.Range(0,Math.Max(0,days.Length-maxRows))];
        }
        var accessLob=rng.Range(0,999)<TpcESpec.SecurityDetailAccessLobPerMille;
        return new SecurityDetailInput{AccessLobFlag=accessLob,MaxRowsToReturn=maxRows,StartDay=startDay,Symbol=this.Model.SymbolOf(security)};
    }
}
