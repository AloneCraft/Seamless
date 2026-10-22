using System.Text.Json;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// 運転側(EGen の CE/MEE/DM)が送る JSON 1 件を、既存の取引の入力へ写す。
/// フィールド名は EGen の TxnHarnessStructs.h の構造体の名前をそのまま使う。日時は "yyyy-MM-dd HH:mm:ss.fff"、金額は小数 2 桁の数値。
/// 形: {"txn":"TradeOrder","input":{...}}。MEE が送る Trade-Result と Market-Feed、DM が送る Data-Maintenance と Trade-Cleanup も同じ形。
/// </summary>
internal static class EGenRequestMapper{
    public static TxnRequest Map(JsonElement message,long now){
        var name=message.GetProperty("txn").GetString()!;
        var input=message.GetProperty("input");
        switch(name){
            case "BrokerVolume":
                return new BrokerVolumeInput{
                    Now=now,
                    BrokerList=Strings(input,"broker_list"),
                    SectorName=Text(input,"sector_name"),
                };
            case "CustomerPosition":
                return new CustomerPositionInput{
                    Now=now,
                    CustomerId=Long(input,"cust_id"),
                    TaxId=Code24.From(Text(input,"tax_id")),
                    GetHistory=Flag(input,"get_history"),
                    AccountIdIndex=Int(input,"acct_id_idx"),
                };
            case "MarketWatch":
                return new MarketWatchInput{
                    Now=now,
                    AccountId=Long(input,"acct_id"),
                    CustomerId=Long(input,"c_id"),
                    IndustryName=Text(input,"industry_name"),
                    StartingCompanyId=Long(input,"starting_co_id"),
                    EndingCompanyId=Long(input,"ending_co_id"),
                    StartDate=Date(input,"start_day"),
                };
            case "SecurityDetail":
                return new SecurityDetailInput{
                    Now=now,
                    AccessLobFlag=Flag(input,"access_lob_flag"),
                    MaxRowsToReturn=Int(input,"max_rows_to_return"),
                    StartDay=Date(input,"start_day"),
                    Symbol=Code16.From(Text(input,"symbol")),
                };
            case "TradeLookup":
                return new TradeLookupInput{
                    Now=now,
                    FrameToExecute=Int(input,"frame_to_execute"),
                    TradeIds=Longs(input,"trade_id"),
                    AccountId=Long(input,"acct_id"),
                    MaxAccountId=Long(input,"max_acct_id"),
                    MaxTrades=Int(input,"max_trades"),
                    StartTradeDts=DateTime(input,"start_trade_dts"),
                    EndTradeDts=DateTime(input,"end_trade_dts"),
                    Symbol=Code16.From(Text(input,"symbol")),
                };
            case "TradeUpdate":
                return new TradeUpdateInput{
                    Now=now,
                    FrameToExecute=Int(input,"frame_to_execute"),
                    TradeIds=Longs(input,"trade_id"),
                    AccountId=Long(input,"acct_id"),
                    MaxAccountId=Long(input,"max_acct_id"),
                    MaxTrades=Int(input,"max_trades"),
                    MaxUpdates=Int(input,"max_updates"),
                    StartTradeDts=DateTime(input,"start_trade_dts"),
                    EndTradeDts=DateTime(input,"end_trade_dts"),
                    Symbol=Code16.From(Text(input,"symbol")),
                };
            case "TradeStatus":
                return new TradeStatusInput{Now=now,AccountId=Long(input,"acct_id")};
            case "TradeOrder":
                return new TradeOrderInput{
                    Now=now,
                    AccountId=Long(input,"acct_id"),
                    ExecFirstName=Text(input,"exec_f_name"),
                    ExecLastName=Text(input,"exec_l_name"),
                    ExecTaxId=Code24.From(Text(input,"exec_tax_id")),
                    CompanyName=Text(input,"co_name"),
                    Issue=Code8.From(Text(input,"issue")),
                    Symbol=Code16.From(Text(input,"symbol")),
                    RequestedPrice=Decimal(input,"requested_price"),
                    TradeQuantity=Int(input,"trade_qty"),
                    TradeTypeId=Code8.From(Text(input,"trade_type_id")),
                    TypeIsMargin=Flag(input,"type_is_margin"),
                    IsLifo=Flag(input,"is_lifo"),
                    RollItBack=Flag(input,"roll_it_back"),
                    // 取引 ID は SUT が採番して後から入れる。
                };
            case "TradeResult":
                return new TradeResultInput{Now=now,TradeId=Long(input,"trade_id"),TradePrice=Decimal(input,"trade_price")};
            case "MarketFeed":{
                var entries=new List<TickerEntry>();
                foreach(var entry in input.GetProperty("entries").EnumerateArray()){
                    entries.Add(new TickerEntry(Code16.From(Text(entry,"symbol")),Decimal(entry,"price_quote"),Int(entry,"trade_qty")));
                }
                return new MarketFeedInput{
                    Now=now,
                    Entries=entries.ToArray(),
                    StatusSubmitted=Code8.From(Text(input,"status_submitted")),
                    TypeLimitBuy=Code8.From(Text(input,"type_limit_buy")),
                    TypeLimitSell=Code8.From(Text(input,"type_limit_sell")),
                    TypeStopLoss=Code8.From(Text(input,"type_stop_loss")),
                    UniqueSymbols=Int(input,"unique_symbols"),
                };
            }
            case "DataMaintenance":
                return new DataMaintenanceInput{
                    Now=now,
                    TableName=Text(input,"table_name"),
                    AccountId=Long(input,"acct_id"),
                    CustomerId=Long(input,"c_id"),
                    CompanyId=Long(input,"co_id"),
                    DayOfMonth=Int(input,"day_of_month"),
                    Symbol=Code16.From(Text(input,"symbol")),
                    TaxId=Code8.From(Text(input,"tx_id")),
                    VolumeIncrement=Int(input,"vol_incr"),
                };
            case "TradeCleanup":
                return new TradeCleanupInput{
                    Now=now,
                    StatusCanceled=Code8.From(Text(input,"st_canceled_id")),
                    StatusPending=Code8.From(Text(input,"st_pending_id")),
                    StatusSubmitted=Code8.From(Text(input,"st_submitted_id")),
                    TradeId=Long(input,"start_trade_id"),
                };
            default:
                throw new ArgumentException($"Unknown transaction {name}.");
        }
    }
    private static string Text(JsonElement element,string name)=>element.GetProperty(name).GetString() ?? "";
    private static long Long(JsonElement element,string name)=>element.GetProperty(name).GetInt64();
    private static int Int(JsonElement element,string name)=>element.GetProperty(name).GetInt32();
    private static decimal Decimal(JsonElement element,string name)=>element.GetProperty(name).GetDecimal();
    /// <summary>EGen の bool と INT32 の真偽値(0/1)の両方を受ける。</summary>
    private static bool Flag(JsonElement element,string name){
        var value=element.GetProperty(name);
        if(value.ValueKind==JsonValueKind.True){
            return true;
        }
        if(value.ValueKind==JsonValueKind.False){
            return false;
        }
        return value.GetInt32()!=0;
    }
    private static string[] Strings(JsonElement element,string name){
        var list=new List<string>();
        foreach(var item in element.GetProperty(name).EnumerateArray()){
            var text=item.GetString() ?? "";
            if(text.Length>0){
                list.Add(text);
            }
        }
        return list.ToArray();
    }
    private static long[] Longs(JsonElement element,string name){
        var list=new List<long>();
        foreach(var item in element.GetProperty(name).EnumerateArray()){
            list.Add(item.GetInt64());
        }
        return list.ToArray();
    }
    private static long DateTime(JsonElement element,string name)=>Time.Milliseconds(Text(element,name));
    private static long Date(JsonElement element,string name)=>TpcETime.DateOf(Time.Milliseconds(Text(element,name)));
    /// <summary>"yyyy-MM-dd HH:mm:ss.fff"(または日付だけ)を 0001-01-01 からのミリ秒にする。</summary>
    internal static class Time{
        public static long Milliseconds(string text){
            var year=int.Parse(text.AsSpan(0,4));
            var month=int.Parse(text.AsSpan(5,2));
            var day=int.Parse(text.AsSpan(8,2));
            var hour=0;
            var minute=0;
            var second=0;
            var millisecond=0;
            if(text.Length>=19){
                hour=int.Parse(text.AsSpan(11,2));
                minute=int.Parse(text.AsSpan(14,2));
                second=int.Parse(text.AsSpan(17,2));
            }
            if(text.Length>=23){
                millisecond=int.Parse(text.AsSpan(20,3));
            }
            return TpcETime.FromDateTime(new System.DateTime(year,month,day,hour,minute,second))+millisecond;
        }
    }
}
