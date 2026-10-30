using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>
/// Trade-Update(TPC-E 3.3.10)を 4 通りのエンジンで流し、出力と書き込み後の DB の一致、仕様メモから手計算した値を確かめる。
/// 表は Trade-Lookup のテストと同じ手作りの表(<see cref="TradeLookupFrames.Load"/>)。
/// </summary>
public sealed class TradeUpdateFrames{
    private static readonly long Base=TpcEModel.InitialTradeStart;
    private static readonly Code16 Alpha=Code16.From("AAAA");
    private static readonly Code16 Beta=Code16.From("BBBB");
    [Fact,Trait("Category","Unit")]public void Run(){
        {
            //Frame 1: " X " を付け外しする(取引 3 は 2 回出るので付けて外す)。出力は各時点の変更後の値、DB は最後の値
            var output=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=1,TradeIds=[3,1,3],MaxTrades=3,MaxUpdates=3},static db=>{
                Assert.Equal("Ann Lee",db.Trade.Get(new TradeKey(1)).ExecName);
                Assert.Equal("Cy Dee",db.Trade.Get(new TradeKey(3)).ExecName);
                Assert.Equal("Ann Lee",db.Trade.Get(new TradeKey(5)).ExecName);
            });
            Assert.Equal(0,output.Status);
            Assert.Equal(1,output.FrameExecuted);
            Assert.Equal(3,output.NumFound);
            Assert.Equal(3,output.NumUpdated);
            Assert.Equal(new string[]{"Cy X Dee","Ann Lee","Cy Dee"},output.Trades.Select(static trade=>trade.ExecName).ToArray());
            Assert.All(output.Trades,static trade=>Assert.True(trade.IsMarket));
            Assert.Equal("Cash Account",output.Trades[0].Settlement!.Value.CashType);
            Assert.Equal("Market-Sell 200 shares of Alpha",output.Trades[0].Cash!.Value.Name);
            Assert.Equal(2,output.Trades[0].History.Length);
        }
        {
            //Frame 1: max_updates が 1 なら 2 件目は変えず、num_found も増えない(擬似コードどおり -1011)
            var output=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=1,TradeIds=[1,6],MaxTrades=2,MaxUpdates=1},static db=>{
                Assert.Equal("Ann Lee",db.Trade.Get(new TradeKey(1)).ExecName);
                Assert.Equal("Dee Fox",db.Trade.Get(new TradeKey(6)).ExecName);
            });
            Assert.Equal(-1011,output.Status);
            Assert.Equal(1,output.NumFound);
            Assert.Equal(1,output.NumUpdated);
            Assert.Equal(new string[]{"Ann Lee","Dee Fox"},output.Trades.Select(static trade=>trade.ExecName).ToArray());
            Assert.False(output.Trades[1].IsMarket);
        }
        {
            //Frame 2: 口座 10 の取引 1・2・4・5。決済のある 3 件の SE_CASH_TYPE を切り替える(未約定の 4 は決済が無く変えない → -1022)
            var output=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=2,AccountId=10,MaxTrades=20,MaxUpdates=20,StartTradeDts=TradeUpdateFrames.Base,EndTradeDts=TradeUpdateFrames.Base+9500},static db=>{
                Assert.Equal("Cash",db.Settlement.Get(new SettlementKey(1)).CashType);
                Assert.Equal("Margin Account",db.Settlement.Get(new SettlementKey(2)).CashType);
                Assert.Equal("Cash Account",db.Settlement.Get(new SettlementKey(3)).CashType);
                Assert.Equal("Cash Account",db.Settlement.Get(new SettlementKey(5)).CashType);
                Assert.Equal("Cash Account",db.Settlement.Get(new SettlementKey(7)).CashType);
            });
            Assert.Equal(-1022,output.Status);
            Assert.Equal(2,output.FrameExecuted);
            Assert.Equal(4,output.NumFound);
            Assert.Equal(3,output.NumUpdated);
            Assert.Equal(new long[]{1,2,4,5},output.Trades.Select(static trade=>trade.TradeId).ToArray());
            Assert.Equal(new string?[]{"Cash","Margin Account",null,"Cash Account"},output.Trades.Select(static trade=>trade.Settlement?.CashType).ToArray());
            Assert.Null(output.Trades[1].Cash);
        }
        {
            //Frame 2: 口座 11 は取引 3 だけで全部変えるので status 0 / 期間に取引が無ければ +1021
            var output=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=2,AccountId=11,MaxTrades=20,MaxUpdates=20,StartTradeDts=TradeUpdateFrames.Base,EndTradeDts=TradeUpdateFrames.Base+9500},static db=>{
                Assert.Equal("Cash",db.Settlement.Get(new SettlementKey(3)).CashType);
            });
            Assert.Equal(0,output.Status);
            Assert.Equal(1,output.NumUpdated);
            var empty=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=2,AccountId=11,MaxTrades=20,MaxUpdates=20,StartTradeDts=TradeUpdateFrames.Base+5000,EndTradeDts=TradeUpdateFrames.Base+9500},static db=>{
                Assert.Equal("Cash Account",db.Settlement.Get(new SettlementKey(3)).CashType);
            });
            Assert.Equal(1021,empty.Status);
        }
        {
            //Frame 3: 銘柄 AAAA の取引 1・3・4・5。現金取引の CT_NAME を "shares of"(小文字)なら大文字の "Shares of" で、それ以外は小文字で作り直す
            var output=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=3,Symbol=TradeUpdateFrames.Alpha,MaxAccountId=19,MaxTrades=20,MaxUpdates=20,StartTradeDts=TradeUpdateFrames.Base,EndTradeDts=TradeUpdateFrames.Base+9500},static db=>{
                Assert.Equal("Market-Buy 100 Shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(1)).Name);
                Assert.Equal("Market-Sell 200 Shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(3)).Name);
                Assert.Equal("Market-Buy 100 shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(5)).Name);
                Assert.Equal("Market-Sell 150 shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(7)).Name);
            });
            Assert.Equal(0,output.Status);
            Assert.Equal(3,output.FrameExecuted);
            Assert.Equal(4,output.NumFound);
            Assert.Equal(3,output.NumUpdated);
            Assert.Equal(new long[]{1,3,4,5},output.Trades.Select(static trade=>trade.TradeId).ToArray());
            Assert.Equal(new string?[]{"Market-Buy","Market-Sell","Limit-Buy","Market-Buy"},output.Trades.Select(static trade=>trade.TypeName).ToArray());
            Assert.All(output.Trades,static trade=>Assert.Equal("Alpha",trade.SecurityName));
            Assert.Equal(new string?[]{"Market-Buy 100 Shares of Alpha","Market-Sell 200 Shares of Alpha",null,"Market-Buy 100 shares of Alpha"},output.Trades.Select(static trade=>trade.Cash?.Name).ToArray());
            Assert.Equal(new long[]{10,11,10,10},output.Trades.Select(static trade=>trade.AccountId).ToArray());
        }
        {
            //Frame 3: max_updates が 1 なら最初の現金取引だけ変える / 取引が無ければ +1032
            var output=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=3,Symbol=TradeUpdateFrames.Alpha,MaxTrades=20,MaxUpdates=1,StartTradeDts=TradeUpdateFrames.Base,EndTradeDts=TradeUpdateFrames.Base+9500},static db=>{
                Assert.Equal("Market-Buy 100 Shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(1)).Name);
                Assert.Equal("Market-Sell 200 shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(3)).Name);
                Assert.Equal("Market-Buy 100 Shares of Alpha",db.CashTransaction.Get(new CashTransactionKey(5)).Name);
            });
            Assert.Equal(0,output.Status);
            Assert.Equal(1,output.NumUpdated);
            Assert.Equal("Market-Sell 200 shares of Alpha",output.Trades[1].Cash!.Value.Name);
            var none=TradeUpdateFrames.ExecuteAll(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=3,Symbol=TradeUpdateFrames.Beta,MaxTrades=20,MaxUpdates=20,StartTradeDts=TradeUpdateFrames.Base+3000,EndTradeDts=TradeUpdateFrames.Base+4000},static _=>{
            });
            Assert.Equal(1032,none.Status);
            Assert.Equal(0,none.NumFound);
        }
        {
            //T_EXEC_NAME の切り替え: " X " を含めば外し、含まなければ空白ごとに付ける(小文字の x は区別する)
            Assert.Equal("Ann X Lee",TradeUpdateFrames.Toggle("Ann Lee"));
            Assert.Equal("Ann Lee",TradeUpdateFrames.Toggle("Ann X Lee"));
            Assert.Equal("Ann X x X Lee",TradeUpdateFrames.Toggle("Ann x Lee"));
            Assert.Equal("Ann",TradeUpdateFrames.Toggle("Ann"));
        }
    }
    /// <summary>
    /// 4 通りのエンジンに同じ表を入れて取引を流し、出力と書き込み後の DB(表と索引のダイジェスト)が一致することを確かめて、先頭の出力を返す。
    /// verify は各エンジンの書き込み後の DB で呼ぶ。
    /// </summary>
    private static TradeUpdateOutput ExecuteAll(TradeUpdateInput input,Action<TpcEDatabaseBase> verify){
        var outputs=new List<TradeUpdateOutput>();
        var digests=new List<DatabaseDigest>();
        foreach(var variant in VariantCatalog.All){
            var store=Store.CreateTemporary();
            try{
                var engine=variant.Open(store,new TpcEScale{Customers=10});
                try{
                    TradeLookupFrames.Load(engine.Loader);
                    outputs.Add((TradeUpdateOutput)engine.Execute(input));
                    verify(engine.Database);
                    digests.Add(StateDigest.Of(engine.Database));
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
            Assert.Empty(digests[0].Differences(digests[index]));
        }
        return outputs[0];
    }
    /// <summary>T_EXEC_NAME の切り替えを、Frame 1 を 1 件だけ流して確かめる。</summary>
    private static string Toggle(string execName){
        var variant=VariantCatalog.All[0];
        var store=Store.CreateTemporary();
        try{
            var engine=variant.Open(store,new TpcEScale{Customers=10});
            try{
                TradeLookupFrames.Load(engine.Loader);
                var trade=engine.Database.Trade.Get(new TradeKey(6));
                engine.Database.Trade.Load(trade with{Id=1000,ExecName=execName});
                var output=(TradeUpdateOutput)engine.Execute(new TradeUpdateInput{Now=TradeUpdateFrames.Base+100_000,FrameToExecute=1,TradeIds=[1000],MaxTrades=1,MaxUpdates=1});
                Assert.Equal(output.Trades[0].ExecName,engine.Database.Trade.Get(new TradeKey(1000)).ExecName);
                return output.Trades[0].ExecName;
            }finally{
                engine.Dispose();
            }
        }finally{
            store.Dispose();
        }
    }
}
