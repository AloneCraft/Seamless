using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>Trade-Lookup(TPC-E 3.3.6)を 4 通りのエンジンで流し、出力の一致と仕様メモから手計算した値を確かめる。</summary>
public sealed class TradeLookupFrames{
    private static readonly long Base=TpcEModel.InitialTradeStart;
    private static readonly Code16 Alpha=Code16.From("AAAA");
    private static readonly Code16 Beta=Code16.From("BBBB");
    private static readonly Code16 Gamma=Code16.From("CCCC");
    private static readonly Code8 Completed=Code8.From("CMPT");
    private static readonly Code8 Canceled=Code8.From("CNCL");
    private static readonly Code8 Pending=Code8.From("PNDG");
    private static readonly Code8 Submitted=Code8.From("SBMT");
    private static readonly Code8 Active=Code8.From("ACTV");
    [Fact,Trait("Category","Unit")]public void Run(){
        {
            //Frame 1: 入力の順に読み、無い取引 ID は飛ばす(見つかった 3 件 ≠ max_trades 4 なので -611)
            var output=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=1,TradeIds=[3,1,99,4],MaxTrades=4});
            Assert.Equal(-611,output.Status);
            Assert.Equal(1,output.FrameExecuted);
            Assert.Equal(3,output.NumFound);
            Assert.Equal(new long[]{3,1,4},output.Trades.Select(static trade=>trade.TradeId).ToArray());
            // 取引 3 は成行の売り(現金取引)。決済・現金取引・履歴(SBMT→CMPT)がそろう
            var trade3=output.Trades[0];
            Assert.True(trade3.IsMarket);
            Assert.True(trade3.IsCash);
            Assert.Equal("Cy Dee",trade3.ExecName);
            Assert.Equal(21.00m,trade3.BidPrice);
            Assert.Equal(21.50m,trade3.TradePrice);
            Assert.Equal(new TradeLookupSettlement(4300m,TpcETime.DayNumber(2005,1,6),"Cash Account"),trade3.Settlement);
            Assert.Equal(new TradeLookupCash(4300m,TradeLookupFrames.Base+4100,"Market-Sell 200 shares of Alpha"),trade3.Cash);
            Assert.Equal(new TradeLookupHistory[]{new TradeLookupHistory(TradeLookupFrames.Base+4000,TradeLookupFrames.Submitted),new TradeLookupHistory(TradeLookupFrames.Base+4100,TradeLookupFrames.Completed)},trade3.History);
            // 取引 4 は未約定の指値: 約定価格・決済・現金取引が無く、履歴は PNDG だけ
            var trade4=output.Trades[2];
            Assert.False(trade4.IsMarket);
            Assert.Null(trade4.TradePrice);
            Assert.Null(trade4.Settlement);
            Assert.Null(trade4.Cash);
            Assert.Equal(new TradeLookupHistory[]{new TradeLookupHistory(TradeLookupFrames.Base+7000,TradeLookupFrames.Pending)},trade4.History);
        }
        {
            //Frame 1: 信用取引は CASH_TRANSACTION を読まない。履歴は 4 行のうち時刻順に先頭 3 行(同時刻は状態 ID の昇順)
            var output=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=1,TradeIds=[2,6],MaxTrades=2});
            Assert.Equal(0,output.Status);
            var trade2=output.Trades[0];
            Assert.False(trade2.IsCash);
            Assert.False(trade2.IsMarket);
            Assert.Null(trade2.Cash);
            Assert.Equal("Margin",trade2.Settlement!.Value.CashType);
            Assert.Equal(new TradeLookupHistory[]{new TradeLookupHistory(TradeLookupFrames.Base+2000,TradeLookupFrames.Pending),new TradeLookupHistory(TradeLookupFrames.Base+5000,TradeLookupFrames.Submitted),new TradeLookupHistory(TradeLookupFrames.Base+6000,TradeLookupFrames.Completed)},trade2.History);
            var trade6=output.Trades[1];
            Assert.Equal(new TradeLookupHistory[]{new TradeLookupHistory(TradeLookupFrames.Base+500,TradeLookupFrames.Pending),new TradeLookupHistory(TradeLookupFrames.Base+800,TradeLookupFrames.Canceled),new TradeLookupHistory(TradeLookupFrames.Base+800,TradeLookupFrames.Submitted)},trade6.History);
        }
        {
            //Frame 2: 口座 10 の [Base+1500, Base+9000] の取引を T_DTS の昇順に(取引 2・4・5。両端を含む)
            var output=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=2,AccountId=10,MaxTrades=20,StartTradeDts=TradeLookupFrames.Base+1500,EndTradeDts=TradeLookupFrames.Base+9000});
            Assert.Equal(0,output.Status);
            Assert.Equal(2,output.FrameExecuted);
            Assert.Equal(3,output.NumFound);
            Assert.Equal(new long[]{2,4,5},output.Trades.Select(static trade=>trade.TradeId).ToArray());
            Assert.All(output.Trades,static trade=>Assert.False(trade.IsMarket));
            Assert.Equal(new TradeLookupCash(-2510m,TradeLookupFrames.Base+9100,"Market-Buy 100 Shares of Alpha"),output.Trades[2].Cash);
        }
        {
            //Frame 2: max_trades で打ち切る / 期間に取引が無ければ +621
            var limited=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=2,AccountId=10,MaxTrades=2,StartTradeDts=TradeLookupFrames.Base,EndTradeDts=TradeLookupFrames.Base+20_000});
            Assert.Equal(new long[]{1,2},limited.Trades.Select(static trade=>trade.TradeId).ToArray());
            var empty=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=2,AccountId=10,MaxTrades=20,StartTradeDts=TradeLookupFrames.Base+12_000,EndTradeDts=TradeLookupFrames.Base+20_000});
            Assert.Equal(621,empty.Status);
            Assert.Equal(0,empty.NumFound);
            Assert.Empty(empty.Trades);
        }
        {
            //Frame 3: 銘柄 AAAA の [Base, Base+9000] の取引(口座をまたいで 1・3・4・5)
            var output=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=3,Symbol=TradeLookupFrames.Alpha,MaxAccountId=19,MaxTrades=20,StartTradeDts=TradeLookupFrames.Base,EndTradeDts=TradeLookupFrames.Base+9000});
            Assert.Equal(0,output.Status);
            Assert.Equal(3,output.FrameExecuted);
            Assert.Equal(new long[]{1,3,4,5},output.Trades.Select(static trade=>trade.TradeId).ToArray());
            Assert.Equal(new long[]{10,11,10,10},output.Trades.Select(static trade=>trade.AccountId).ToArray());
            Assert.Equal(new int[]{100,200,300,100},output.Trades.Select(static trade=>trade.Quantity).ToArray());
            Assert.Equal(Code8.From("TMS"),output.Trades[1].TradeTypeId);
            var none=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=3,Symbol=TradeLookupFrames.Beta,MaxTrades=20,StartTradeDts=TradeLookupFrames.Base+3000,EndTradeDts=TradeLookupFrames.Base+4000});
            Assert.Equal(631,none.Status);
        }
        {
            //Frame 4: 口座 10 の Base+9500 以降の最初の取引は 7。7 が変えた保有 1・5 の履歴をすべて(保有・取引の昇順に 5 行)
            var output=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=4,AccountId=10,StartTradeDts=TradeLookupFrames.Base+9500});
            Assert.Equal(0,output.Status);
            Assert.Equal(4,output.FrameExecuted);
            Assert.Equal(1,output.NumTradesFound);
            Assert.Equal(7,output.TradeId);
            Assert.Equal(5,output.NumFound);
            Assert.Equal(new TradeLookupHoldingHistory[]{new TradeLookupHoldingHistory(1,1,0,100),new TradeLookupHoldingHistory(1,7,100,0),new TradeLookupHoldingHistory(5,5,0,100),new TradeLookupHoldingHistory(5,7,100,50),new TradeLookupHoldingHistory(5,8,50,0)},output.HoldingHistory);
        }
        {
            //Frame 4: 起点より後に口座の取引が無ければ +641
            var output=TradeLookupFrames.ExecuteAll(new TradeLookupInput{Now=TradeLookupFrames.Base+100_000,FrameToExecute=4,AccountId=10,StartTradeDts=TradeLookupFrames.Base+50_000});
            Assert.Equal(641,output.Status);
            Assert.Equal(0,output.NumTradesFound);
            Assert.Equal(0,output.TradeId);
            Assert.Empty(output.HoldingHistory);
        }
    }
    /// <summary>4 通りのエンジンに同じ表を入れて取引を流し、出力が一致することと DB が変わらないことを確かめて、先頭の出力を返す。</summary>
    private static TradeLookupOutput ExecuteAll(TradeLookupInput input){
        var outputs=new List<TradeLookupOutput>();
        foreach(var variant in VariantCatalog.All){
            var store=Store.CreateTemporary();
            try{
                var engine=variant.Open(store,new TpcEScale{Customers=10});
                try{
                    TradeLookupFrames.Load(engine.Loader);
                    var before=StateDigest.Of(engine.Database);
                    outputs.Add((TradeLookupOutput)engine.Execute(input));
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
    /// <summary>
    /// 手作りの表。口座 10 の取引 1・2・4・5・7・8、口座 11 の取引 3、口座 12 の取引 6。
    /// 保有 1(取引 1 が作った)と保有 5(取引 5 が作った)を、取引 7 と 8 が売りで減らした。
    /// </summary>
    internal static void Load(ILoadSink sink){
        foreach(var (id,name) in new[]{("CMPT","Completed"),("CNCL","Canceled"),("PNDG","Pending"),("SBMT","Submitted"),("ACTV","Active")}){
            sink.Load(new StatusTypeRow(Code8.From(id),name));
        }
        sink.Load(new TradeTypeRow(Code8.From("TMB"),"Market-Buy",false,true));
        sink.Load(new TradeTypeRow(Code8.From("TMS"),"Market-Sell",true,true));
        sink.Load(new TradeTypeRow(Code8.From("TLB"),"Limit-Buy",false,false));
        sink.Load(new TradeTypeRow(Code8.From("TLS"),"Limit-Sell",true,false));
        sink.Load(new TradeTypeRow(Code8.From("TSL"),"Stop-Loss",true,false));
        sink.Load(new SecurityRow(TradeLookupFrames.Alpha,Code8.From("COMMON"),TradeLookupFrames.Active,"Alpha",Code8.From("NYSE"),1,1000,0,0,1m,1m,0,1m,0,0m,0m));
        sink.Load(new SecurityRow(TradeLookupFrames.Beta,Code8.From("COMMON"),TradeLookupFrames.Active,"Beta",Code8.From("NYSE"),2,1000,0,0,1m,1m,0,1m,0,0m,0m));
        sink.Load(new SecurityRow(TradeLookupFrames.Gamma,Code8.From("COMMON"),TradeLookupFrames.Active,"Gamma",Code8.From("NYSE"),3,1000,0,0,1m,1m,0,1m,0,0m,0m));
        var due=TpcETime.DayNumber(2005,1,6);
        // 取引 1: 口座 10、AAAA の成行の買い 100、現金取引、約定済み
        TradeLookupFrames.LoadCompleted(sink,new TradeRow(1,TradeLookupFrames.Base+1000,TradeLookupFrames.Completed,Code8.From("TMB"),true,TradeLookupFrames.Alpha,100,25.00m,10,"Ann X Lee",25.10m,10m,5m,0m,false),TradeLookupFrames.Base+1000,TradeLookupFrames.Base+1100,new SettlementRow(1,"Cash Account",due,-2525m),"Market-Buy 100 shares of Alpha");
        // 取引 2: 口座 10、BBBB の指値の売り 200、信用取引、約定済み(PNDG→SBMT→CMPT)
        sink.Load(new TradeRow(2,TradeLookupFrames.Base+2000,TradeLookupFrames.Completed,Code8.From("TLS"),false,TradeLookupFrames.Beta,200,22.00m,10,"Bob Ray",22.00m,10m,5m,1m,true));
        sink.Load(new TradeHistoryRow(2,TradeLookupFrames.Base+2000,TradeLookupFrames.Pending));
        sink.Load(new TradeHistoryRow(2,TradeLookupFrames.Base+5000,TradeLookupFrames.Submitted));
        sink.Load(new TradeHistoryRow(2,TradeLookupFrames.Base+6000,TradeLookupFrames.Completed));
        sink.Load(new SettlementRow(2,"Margin",due,4385m));
        // 取引 3: 口座 11、AAAA の成行の売り 200、現金取引、約定済み
        TradeLookupFrames.LoadCompleted(sink,new TradeRow(3,TradeLookupFrames.Base+4000,TradeLookupFrames.Completed,Code8.From("TMS"),true,TradeLookupFrames.Alpha,200,21.00m,11,"Cy Dee",21.50m,10m,5m,0m,true),TradeLookupFrames.Base+4000,TradeLookupFrames.Base+4100,new SettlementRow(3,"Cash Account",due,4300m),"Market-Sell 200 shares of Alpha");
        // 取引 4: 口座 10、AAAA の指値の買い 300、未約定(PNDG だけ。決済と現金取引は無い)
        sink.Load(new TradeRow(4,TradeLookupFrames.Base+7000,TradeLookupFrames.Pending,Code8.From("TLB"),true,TradeLookupFrames.Alpha,300,20.00m,10,"Ann Lee",null,10m,0m,0m,false));
        sink.Load(new TradeHistoryRow(4,TradeLookupFrames.Base+7000,TradeLookupFrames.Pending));
        // 取引 5: 口座 10、AAAA の成行の買い 100(Base+9000 ちょうど)、現金取引、約定済み
        TradeLookupFrames.LoadCompleted(sink,new TradeRow(5,TradeLookupFrames.Base+9000,TradeLookupFrames.Completed,Code8.From("TMB"),true,TradeLookupFrames.Alpha,100,25.00m,10,"Ann Lee",25.00m,10m,0m,0m,false),TradeLookupFrames.Base+9000,TradeLookupFrames.Base+9100,new SettlementRow(5,"Cash",due,-2510m),"Market-Buy 100 Shares of Alpha");
        // 取引 6: 口座 12、CCCC の取り消された指値(履歴が 4 行。SBMT と CNCL は同時刻)
        sink.Load(new TradeRow(6,TradeLookupFrames.Base+800,TradeLookupFrames.Canceled,Code8.From("TLB"),true,TradeLookupFrames.Gamma,100,10.00m,12,"Dee Fox",null,0m,0m,0m,false));
        sink.Load(new TradeHistoryRow(6,TradeLookupFrames.Base+900,TradeLookupFrames.Active));
        sink.Load(new TradeHistoryRow(6,TradeLookupFrames.Base+800,TradeLookupFrames.Submitted));
        sink.Load(new TradeHistoryRow(6,TradeLookupFrames.Base+500,TradeLookupFrames.Pending));
        sink.Load(new TradeHistoryRow(6,TradeLookupFrames.Base+800,TradeLookupFrames.Canceled));
        // 取引 7・8: 口座 10、AAAA の成行の売り 150 と 50(保有 1 と 5 を減らす)
        TradeLookupFrames.LoadCompleted(sink,new TradeRow(7,TradeLookupFrames.Base+10_000,TradeLookupFrames.Completed,Code8.From("TMS"),true,TradeLookupFrames.Alpha,150,24.00m,10,"Ann Lee",24.00m,10m,0m,0m,false),TradeLookupFrames.Base+10_000,TradeLookupFrames.Base+10_100,new SettlementRow(7,"Cash Account",due,3590m),"Market-Sell 150 shares of Alpha");
        TradeLookupFrames.LoadCompleted(sink,new TradeRow(8,TradeLookupFrames.Base+11_000,TradeLookupFrames.Completed,Code8.From("TMS"),true,TradeLookupFrames.Alpha,50,24.00m,10,"Ann Lee",24.00m,10m,0m,0m,false),TradeLookupFrames.Base+11_000,TradeLookupFrames.Base+11_100,new SettlementRow(8,"Cash Account",due,1190m),"Market-Sell 50 shares of Alpha");
        sink.Load(new HoldingHistoryRow(1,1,0,100));
        sink.Load(new HoldingHistoryRow(5,5,0,100));
        sink.Load(new HoldingHistoryRow(5,8,50,0));
        sink.Load(new HoldingHistoryRow(1,7,100,0));
        sink.Load(new HoldingHistoryRow(5,7,100,50));
        sink.Load(new HoldingHistoryRow(3,3,0,-200));
    }
    /// <summary>約定済みの取引(履歴 SBMT→CMPT、決済、現金取引なら現金取引)を入れる。</summary>
    private static void LoadCompleted(ILoadSink sink,TradeRow trade,long submitted,long completed,SettlementRow settlement,string cashName){
        sink.Load(trade);
        sink.Load(new TradeHistoryRow(trade.Id,submitted,TradeLookupFrames.Submitted));
        sink.Load(new TradeHistoryRow(trade.Id,completed,TradeLookupFrames.Completed));
        sink.Load(settlement);
        if(trade.IsCash){
            sink.Load(new CashTransactionRow(trade.Id,completed,settlement.Amount,cashName));
        }
    }
}
