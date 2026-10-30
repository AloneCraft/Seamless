using System.Text;
namespace Shared.TpcE.Transactions;
/// <summary>
/// Security-Detail(3.3.5)のフレーム 1。手順と問い合わせは既存の <see cref="SecurityDetailFrames"/> を使い、ここは構造体との変換だけ。
/// ハーネスは出力を 0 で初期化しない(大きい LOB のため)ので、返す項目はすべてここで設定する。値のある項目の NULL 指示子は 0。
/// </summary>
internal sealed class SecurityDetailHarness{
    private readonly SecurityDetailFrames frames=new();
    public void Frame1(TpcEDatabase db,SecurityDetailTxnInput input,SecurityDetailFrame1Output output){
        var result=this.frames.Run(db,new SecurityDetailInput{
            AccessLobFlag=input.AccessLobFlag,
            MaxRowsToReturn=input.MaxRowsToReturn,
            StartDay=TpcETime.DateOf(input.StartDay.ToMilliseconds()),
            Symbol=Code16.From(input.Symbol),
        });
        output.SName=result.SecurityName;
        output.NumOut=result.NumOutstanding;
        output.StartDate=Day(result.StartDate);
        output.ExDate=Day(result.ExchangeDate);
        output.Divid=(double)result.Dividend;
        output.Yield=(double)result.Yield;
        output.PeRatio=(double)result.Pe;
        output.S52WkHigh=(double)result.High52Week;
        output.S52WkHighDate=Day(result.High52WeekDate);
        output.S52WkLow=(double)result.Low52Week;
        output.S52WkLowDate=Day(result.Low52WeekDate);
        output.LastPrice=(double)result.LastPrice;
        output.LastOpen=(double)result.LastOpen;
        output.LastVol=result.LastVolume;
        output.CoName=result.CompanyName;
        output.SpRate=result.SpRate;
        output.CeoName=result.CeoName;
        output.CoDesc=result.CompanyDescription;
        output.OpenDate=Day(result.OpenDate);
        output.CoStId=result.CompanyStatusId.ToString();
        output.CoAdLine1=result.CompanyAddress.Line1 ?? "";
        output.CoAdLine2=result.CompanyAddress.Line2 ?? "";
        output.CoAdTown=result.CompanyAddress.Town;
        output.CoAdDiv=result.CompanyAddress.Division;
        output.CoAdZip=result.CompanyAddress.Zip.ToString();
        output.CoAdCty=result.CompanyAddress.Country ?? "";
        output.ExName=result.ExchangeName;
        output.ExDesc=result.ExchangeDescription ?? "";
        output.ExNumSymb=result.ExchangeNumSymbols;
        output.ExOpen=result.ExchangeOpen;
        output.ExClose=result.ExchangeClose;
        output.ExAdLine1=result.ExchangeAddress.Line1 ?? "";
        output.ExAdLine2=result.ExchangeAddress.Line2 ?? "";
        output.ExAdTown=result.ExchangeAddress.Town;
        output.ExAdDiv=result.ExchangeAddress.Division;
        output.ExAdZip=result.ExchangeAddress.Zip.ToString();
        output.ExAdCty=result.ExchangeAddress.Country ?? "";
        var competitors=Math.Min(result.Competitors.Length,3);
        for(var index=0;index<competitors;index++){
            output.SetCpCoName(index,result.Competitors[index].CompanyName);
            output.SetCpInName(index,result.Competitors[index].IndustryName);
        }
        output.FinLen=result.Financials.Length;
        for(var index=0;index<result.Financials.Length&&index<output.FinCount;index++){
            var source=result.Financials[index];
            var fin=output.GetFin(index);
            fin.Year=(int)source.Year;
            fin.Qtr=(int)source.Quarter;
            fin.StartDate=Day(source.QuarterStartDate);
            fin.Rev=(double)source.Revenue;
            fin.NetEarn=(double)source.NetEarnings;
            fin.BasicEps=(double)source.BasicEps;
            fin.DilutEps=(double)source.DilutedEps;
            fin.Margin=(double)source.Margin;
            fin.Invent=(double)source.Inventory;
            fin.Assets=(double)source.Assets;
            fin.Liab=(double)source.Liability;
            fin.OutBasic=source.OutBasic;
            fin.OutDilut=source.OutDiluted;
            fin.AssetsInd=0;
            fin.BasicEpsInd=0;
            fin.DilutEpsInd=0;
            fin.InventInd=0;
            fin.LiabInd=0;
            fin.MarginInd=0;
            fin.NetEarnInd=0;
            fin.OutBasicInd=0;
            fin.OutDilutInd=0;
            fin.QtrInd=0;
            fin.RevInd=0;
            fin.StartDateInd=0;
            fin.YearInd=0;
        }
        output.DayLen=result.Days.Length;
        for(var index=0;index<result.Days.Length&&index<output.DayCount;index++){
            var source=result.Days[index];
            var day=output.GetDay(index);
            day.Date=Day(source.Date);
            day.Close=(double)source.Close;
            day.High=(double)source.High;
            day.Low=(double)source.Low;
            day.Vol=source.Volume;
            day.CloseInd=0;
            day.DateInd=0;
            day.HighInd=0;
            day.LowInd=0;
            day.VolInd=0;
        }
        output.NewsLen=result.News.Length;
        for(var index=0;index<result.News.Length&&index<output.NewsCount;index++){
            var source=result.News[index];
            var news=output.GetNews(index);
            news.Dts=EGenTimestamp.FromMilliseconds(source.Dts);
            news.Src=source.Source;
            news.Auth=source.Author ?? "";
            news.AuthInd=0;
            if(source.Author is null){
                news.AuthInd=-1;
            }
            news.Headline=source.Headline;
            news.Summary=source.Summary;
            news.Item=Encoding.ASCII.GetString(source.Item);
        }
    }
    /// <summary>日番号を、その日の 0 時の EGen の日時にする。</summary>
    private static EGenTimestamp Day(long day)=>EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(day));
}
