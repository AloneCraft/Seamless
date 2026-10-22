using System.Text.Json;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class EGenRequestMapper{
    private static TxnRequest Map(string json){
        using var document=JsonDocument.Parse(json);
        return Compliance.EGenRequestMapper.Map(document.RootElement.Clone(),123_456);
    }
    [Fact,Trait("Category","Unit")]public void MapsTradeOrder(){
        {
            //Trade-Order: 金額は小数のまま、真偽は 0/1 でも true/false でも受ける
            var request=(TradeOrderInput)Map("""
                {"txn":"TradeOrder","input":{"acct_id":43000000001,"exec_f_name":"Ann","exec_l_name":"Lee","exec_tax_id":"123AB4567CD890","co_name":"Zi Corporation","issue":"COMMON","symbol":"ZICA","requested_price":25.93,"trade_qty":400,"trade_type_id":"TLB","type_is_margin":0,"is_lifo":1,"roll_it_back":false}}
                """);
            Assert.Equal(43_000_000_001,request.AccountId);
            Assert.Equal(25.93m,request.RequestedPrice);
            Assert.Equal(Code16.From("ZICA"),request.Symbol);
            Assert.Equal(Code8.From("TLB"),request.TradeTypeId);
            Assert.False(request.TypeIsMargin);
            Assert.True(request.IsLifo);
            Assert.False(request.RollItBack);
            Assert.Equal(123_456,request.Now);
        }
    }
    [Fact,Trait("Category","Unit")]public void MapsReadTransactions(){
        {
            //Broker-Volume: 空の名前で打ち切る
            var request=(BrokerVolumeInput)Map("""{"txn":"BrokerVolume","input":{"broker_list":["Alpha","Beta","",""],"sector_name":"Energy"}}""");
            Assert.Equal(["Alpha","Beta"],request.BrokerList);
            Assert.Equal("Energy",request.SectorName);
        }
        {
            //Trade-Lookup: 日時はミリ秒に、取引 ID の並びはそのまま
            var request=(TradeLookupInput)Map("""
                {"txn":"TradeLookup","input":{"trade_id":[1,2,3],"acct_id":7,"max_acct_id":9,"frame_to_execute":1,"max_trades":3,"start_trade_dts":"2005-01-03 09:00:02.092","end_trade_dts":"2005-01-03 09:00:03.000","symbol":""}}
                """);
            Assert.Equal([1L,2L,3L],request.TradeIds);
            Assert.Equal(TpcETime.FromDateTime(new DateTime(2005,1,3,9,0,2))+92,request.StartTradeDts);
            Assert.Equal(1,request.FrameToExecute);
        }
        {
            //Market-Watch: 開始日は日番号
            var request=(MarketWatchInput)Map("""
                {"txn":"MarketWatch","input":{"acct_id":1,"c_id":0,"industry_name":"","starting_co_id":10,"ending_co_id":20,"start_day":"2000-01-05 00:00:00.000"}}
                """);
            Assert.Equal(TpcETime.DayNumber(2000,1,5),request.StartDate);
        }
    }
    [Fact,Trait("Category","Unit")]public void MapsMarketSideTransactions(){
        {
            //Market-Feed: 更新の並びと、状態・取引種別の名前
            var request=(MarketFeedInput)Map("""
                {"txn":"MarketFeed","input":{"unique_symbols":2,"status_submitted":"SBMT","type_limit_buy":"TLB","type_limit_sell":"TLS","type_stop_loss":"TSL","entries":[{"symbol":"ZICA","price_quote":20.50,"trade_qty":100},{"symbol":"CITY","price_quote":3.25,"trade_qty":200}]}}
                """);
            Assert.Equal(2,request.Entries.Length);
            Assert.Equal(20.50m,request.Entries[0].PriceQuote);
            Assert.Equal(Code8.From("TSL"),request.TypeStopLoss);
        }
        {
            //Trade-Result と Data-Maintenance と Trade-Cleanup
            Assert.Equal(7,((TradeResultInput)Map("""{"txn":"TradeResult","input":{"trade_id":7,"trade_price":12.34}}""")).TradeId);
            var maintenance=(DataMaintenanceInput)Map("""{"txn":"DataMaintenance","input":{"table_name":"SECURITY","acct_id":0,"c_id":0,"co_id":0,"day_of_month":3,"symbol":"ZICA","tx_id":"","vol_incr":0}}""");
            Assert.Equal("SECURITY",maintenance.TableName);
            Assert.Equal(Code16.From("ZICA"),maintenance.Symbol);
            var cleanup=(TradeCleanupInput)Map("""{"txn":"TradeCleanup","input":{"start_trade_id":200000000000001,"st_canceled_id":"CNCL","st_pending_id":"PNDG","st_submitted_id":"SBMT"}}""");
            Assert.Equal(Code8.From("CNCL"),cleanup.StatusCanceled);
        }
    }
    [Fact,Trait("Category","Unit")]public void RejectsUnknownTransaction(){
        {
            Assert.Throws<ArgumentException>(()=>Map("""{"txn":"Nope","input":{}}"""));
        }
    }
}
