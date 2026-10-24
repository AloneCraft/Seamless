using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>Security-Detail(TPC-E 3.3.5)を 4 通りのエンジンで流し、出力の一致と仕様メモから手計算した値を確かめる。</summary>
public sealed class SecurityDetailFrames{
    private static readonly Code16 Alpha=Code16.From("ALPH");
    private static readonly Code16 Beta=Code16.From("BETA");
    private static readonly Code8 Active=Code8.From("ACTV");
    private static readonly long Now=TpcETime.FromDateTime(new DateTime(2005,1,5,9,0,0));
    [Fact,Trait("Category","Unit")]public void Run(){
        {
            //見出しと要約を読む。日次相場 5 行(土曜の start_day は次の月曜から)・財務 20 行・ニュース 2 件がそろうので status 0
            var input=new SecurityDetailInput{Now=SecurityDetailFrames.Now,AccessLobFlag=false,MaxRowsToReturn=5,StartDay=TpcETime.DayNumber(2004,12,4),Symbol=SecurityDetailFrames.Alpha};
            var output=SecurityDetailFrames.ExecuteAll(input);
            Assert.Equal(0,output.Status);
            Assert.Equal("Alpha common",output.SecurityName);
            Assert.Equal(5_000_000,output.NumOutstanding);
            Assert.Equal(TpcETime.DayNumber(1990,1,2),output.StartDate);
            Assert.Equal(TpcETime.DayNumber(1990,2,3),output.ExchangeDate);
            Assert.Equal(12.5m,output.Pe);
            Assert.Equal(30.25m,output.High52Week);
            Assert.Equal(0.50m,output.Dividend);
            Assert.Equal(1.25m,output.Yield);
            Assert.Equal("Company 1",output.CompanyName);
            Assert.Equal("CEO 1",output.CeoName);
            Assert.Equal(SecurityDetailFrames.Active,output.CompanyStatusId);
            Assert.Equal(new SecurityDetailAddress("1 Main St.",null,"Springfield","Ohio",Code16.From("10001"),"USA"),output.CompanyAddress);
            Assert.Equal(new SecurityDetailAddress("11 Wall St.","Floor 3","Riverton","Utah",Code16.From("20002"),"USA"),output.ExchangeAddress);
            Assert.Equal("New York Stock Exchange",output.ExchangeName);
            Assert.Equal(930,output.ExchangeOpen);
            Assert.Equal(1600,output.ExchangeClose);
            Assert.Equal(2,output.ExchangeNumSymbols);
            // 競合は 4 件のうち会社 ID の昇順に 3 件
            Assert.Equal(new SecurityDetailCompetitor[]{new SecurityDetailCompetitor("Company 2","Industry AB"),new SecurityDetailCompetitor("Company 3","Industry AA"),new SecurityDetailCompetitor("Company 4","Industry AA")},output.Competitors);
            // 財務は 2000 年第 1 四半期から 2004 年第 4 四半期まで昇順に 20 件(会社 2 の行は混ざらない)
            Assert.Equal(20,output.Financials.Length);
            for(var index=0;index<20;index++){
                var year=2000+index/4;
                var quarter=index%4+1;
                Assert.Equal(SecurityDetailFrames.Financial(1,year,quarter).Revenue,output.Financials[index].Revenue);
                Assert.Equal(year,output.Financials[index].Year);
                Assert.Equal(quarter,output.Financials[index].Quarter);
            }
            // 日次相場は 2004-12-06(月)〜12-10(金)
            Assert.Equal(new long[]{TpcETime.DayNumber(2004,12,6),TpcETime.DayNumber(2004,12,7),TpcETime.DayNumber(2004,12,8),TpcETime.DayNumber(2004,12,9),TpcETime.DayNumber(2004,12,10)},output.Days.Select(static day=>day.Date).ToArray());
            Assert.Equal(SecurityDetailFrames.Close(TpcETime.DayNumber(2004,12,6)),output.Days[0].Close);
            Assert.Equal(1000+TpcETime.DayNumber(2004,12,10)%100,output.Days[4].Volume);
            Assert.Equal(25.50m,output.LastPrice);
            Assert.Equal(25.00m,output.LastOpen);
            Assert.Equal(12345,output.LastVolume);
            // ニュースはニュース ID の昇順に 2 件(本文は空、見出しと要約が入る)
            Assert.Equal(2,output.News.Length);
            Assert.Equal("Headline 11",output.News[0].Headline);
            Assert.Equal("Summary 12",output.News[1].Summary);
            Assert.Empty(output.News[0].Item);
            Assert.Equal("Source 11",output.News[0].Source);
        }
        {
            //本文を読む。2004-12-29 以降は 3 営業日しか無いので日次相場 3 行で -511
            var input=new SecurityDetailInput{Now=SecurityDetailFrames.Now,AccessLobFlag=true,MaxRowsToReturn=20,StartDay=TpcETime.DayNumber(2004,12,29),Symbol=SecurityDetailFrames.Alpha};
            var output=SecurityDetailFrames.ExecuteAll(input);
            Assert.Equal(-511,output.Status);
            Assert.Equal(new long[]{TpcETime.DayNumber(2004,12,29),TpcETime.DayNumber(2004,12,30),TpcETime.DayNumber(2004,12,31)},output.Days.Select(static day=>day.Date).ToArray());
            Assert.Equal(2,output.News.Length);
            Assert.Equal("",output.News[0].Headline);
            Assert.Equal("",output.News[0].Summary);
            Assert.Equal(SecurityDetailFrames.NewsBody(11),output.News[0].Item);
            Assert.Equal(SecurityDetailFrames.NewsBody(12),output.News[1].Item);
        }
        {
            //会社 2 の銘柄は財務が 4 行・ニュースが 1 件なので、先に判定する -512 になる
            var input=new SecurityDetailInput{Now=SecurityDetailFrames.Now,AccessLobFlag=false,MaxRowsToReturn=6,StartDay=TpcETime.DayNumber(2004,11,1),Symbol=SecurityDetailFrames.Beta};
            var output=SecurityDetailFrames.ExecuteAll(input);
            Assert.Equal(-512,output.Status);
            Assert.Equal(6,output.Days.Length);
            Assert.Equal(4,output.Financials.Length);
            Assert.Single(output.News);
            Assert.Equal(new SecurityDetailCompetitor[]{new SecurityDetailCompetitor("Company 1","Industry AA")},output.Competitors);
        }
    }
    /// <summary>4 通りのエンジンに同じ表を入れて取引を流し、出力が一致することと DB が変わらないことを確かめて、先頭の出力を返す。</summary>
    private static SecurityDetailOutput ExecuteAll(SecurityDetailInput input){
        var outputs=new List<SecurityDetailOutput>();
        foreach(var variant in VariantCatalog.All){
            var store=Store.CreateTemporary();
            try{
                var engine=variant.Open(store,new TpcEScale{Customers=10});
                try{
                    SecurityDetailFrames.Load(engine.Loader);
                    var before=StateDigest.Of(engine.Database);
                    outputs.Add((SecurityDetailOutput)engine.Execute(input));
                    Assert.Empty(before.Differences(StateDigest.Of(engine.Database)));
                }finally{
                    engine.Dispose();
                }
            }finally{
                store.Dispose();
            }
        }
        var expected=CanonicalWriter.ToBytes(outputs[0]);
        for(var index=1;index<outputs.Count;index++){
            Assert.True(expected.AsSpan().SequenceEqual(CanonicalWriter.ToBytes(outputs[index])),$"{VariantCatalog.All[index].Name} differs from {VariantCatalog.All[0].Name}.");
        }
        return outputs[0];
    }
    /// <summary>手作りの表。会社 1(銘柄 ALPH)と会社 2(銘柄 BETA)、競合・財務・日次相場・ニュース。</summary>
    private static void Load(ILoadSink sink){
        sink.Load(new ZipCodeRow(Code16.From("10001"),"Springfield","Ohio"));
        sink.Load(new ZipCodeRow(Code16.From("20002"),"Riverton","Utah"));
        sink.Load(new AddressRow(101,"1 Main St.",null,Code16.From("10001"),"USA"));
        sink.Load(new AddressRow(102,"2 Side St.",null,Code16.From("10001"),"USA"));
        sink.Load(new AddressRow(201,"11 Wall St.","Floor 3",Code16.From("20002"),"USA"));
        sink.Load(new ExchangeRow(Code8.From("NYSE"),"New York Stock Exchange",2,930,1600,"Big board",201));
        sink.Load(new IndustryRow(Code8.From("AA"),"Industry AA",Code8.From("SA")));
        sink.Load(new IndustryRow(Code8.From("AB"),"Industry AB",Code8.From("SA")));
        for(var company=1L;company<=5;company++){
            var address=102L;
            if(company==1){
                address=101;
            }
            sink.Load(new CompanyRow(company,SecurityDetailFrames.Active,"Company "+company,Code8.From("AA"),"AAA","CEO "+company,address,"Description "+company,TpcETime.DayNumber(1900,1,1)));
        }
        sink.Load(new SecurityRow(SecurityDetailFrames.Alpha,Code8.From("COMMON"),SecurityDetailFrames.Active,"Alpha common",Code8.From("NYSE"),1,5_000_000,TpcETime.DayNumber(1990,1,2),TpcETime.DayNumber(1990,2,3),12.5m,30.25m,TpcETime.DayNumber(2004,6,1),20.75m,TpcETime.DayNumber(2004,2,2),0.50m,1.25m));
        sink.Load(new SecurityRow(SecurityDetailFrames.Beta,Code8.From("COMMON"),SecurityDetailFrames.Active,"Beta common",Code8.From("NYSE"),2,7_000_000,TpcETime.DayNumber(1991,1,2),TpcETime.DayNumber(1991,2,3),8.5m,22.00m,TpcETime.DayNumber(2004,7,1),21.00m,TpcETime.DayNumber(2004,3,2),0.10m,0.75m));
        // 競合は入れる順をばらす(索引が会社 ID の昇順に並べる)
        sink.Load(new CompanyCompetitorRow(1,5,Code8.From("AA")));
        sink.Load(new CompanyCompetitorRow(1,3,Code8.From("AA")));
        sink.Load(new CompanyCompetitorRow(1,2,Code8.From("AB")));
        sink.Load(new CompanyCompetitorRow(1,4,Code8.From("AA")));
        sink.Load(new CompanyCompetitorRow(2,1,Code8.From("AA")));
        for(var year=2004;year>=2000;year--){
            for(var quarter=4;quarter>=1;quarter--){
                sink.Load(SecurityDetailFrames.Financial(1,year,quarter));
            }
        }
        for(var quarter=1;quarter<=4;quarter++){
            sink.Load(SecurityDetailFrames.Financial(2,2003,quarter));
        }
        for(var day=TpcETime.DayNumber(2004,11,1);day<=TpcETime.DayNumber(2004,12,31);day++){
            var weekday=TpcETime.ToDate(day).DayOfWeek;
            if(weekday==DayOfWeek.Saturday||weekday==DayOfWeek.Sunday){
                continue;
            }
            var close=SecurityDetailFrames.Close(day);
            sink.Load(new DailyMarketRow(day,SecurityDetailFrames.Alpha,close,close+1,close-1,1000+day%100));
            sink.Load(new DailyMarketRow(day,SecurityDetailFrames.Beta,close+5,close+6,close+4,2000+day%100));
        }
        sink.Load(new LastTradeRow(SecurityDetailFrames.Alpha,SecurityDetailFrames.Now-60_000,25.50m,25.00m,12345));
        sink.Load(new LastTradeRow(SecurityDetailFrames.Beta,SecurityDetailFrames.Now-60_000,21.50m,21.00m,543));
        for(var news=11L;news<=14;news++){
            sink.Load(new NewsItemRow(news,"Headline "+news,"Summary "+news,SecurityDetailFrames.NewsBody(news),SecurityDetailFrames.Now-news*1000,"Source "+news,null));
        }
        sink.Load(new NewsXrefRow(13,1));
        sink.Load(new NewsXrefRow(11,1));
        sink.Load(new NewsXrefRow(12,1));
        sink.Load(new NewsXrefRow(14,2));
    }
    private static FinancialRow Financial(long company,long year,long quarter){
        return new FinancialRow(company,year,quarter,TpcETime.DayNumber((int)year,(int)(quarter-1)*3+1,1),company*1000+year*10+quarter,year+quarter,1.5m,1.25m,0.1m,100,200,300,1000,1100);
    }
    private static decimal Close(long day)=>20m+day%50/10m;
    private static byte[] NewsBody(long news)=>[(byte)'n',(byte)news,(byte)'!'];
}
