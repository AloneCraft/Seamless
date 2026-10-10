using Seamless.Collections;
using Seamless.Collections.Persisted;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
// 骨組みは gen_db.py で生成し、以後は手で直す。
/// <summary>Persisted に置いた TPC-E の DB。表も索引も <c>Dictionary&lt;TKey,Slot&lt;T&gt;&gt;</c>(削除は墓標)。</summary>
public sealed class PersistedTpcEDatabase:TpcEDatabaseBase{
    private readonly List<ILiveCounter> counters=[];
    public PersistedTpcEDatabase(Store store,TpcEScale scale):base(store,scale){
        var s=scale;
        this.AccountPermission=this.Table<AccountPermissionKey,AccountPermissionRow>("account_permission");
        this.Customer=this.Table<CustomerKey,CustomerRow>("customer");
        this.CustomerAccount=this.Table<CustomerAccountKey,CustomerAccountRow>("customer_account");
        this.CustomerTaxrate=this.Table<CustomerTaxrateKey,CustomerTaxrateRow>("customer_taxrate");
        this.Holding=this.Table<HoldingKey,HoldingRow>("holding");
        this.HoldingHistory=this.Table<HoldingHistoryKey,HoldingHistoryRow>("holding_history");
        this.HoldingSummary=this.Table<HoldingSummaryKey,HoldingSummaryRow>("holding_summary");
        this.WatchItem=this.Table<WatchItemKey,WatchItemRow>("watch_item");
        this.WatchList=this.Table<WatchListKey,WatchListRow>("watch_list");
        this.Broker=this.Table<BrokerKey,BrokerRow>("broker");
        this.CashTransaction=this.Table<CashTransactionKey,CashTransactionRow>("cash_transaction");
        this.Charge=this.Table<ChargeKey,ChargeRow>("charge");
        this.CommissionRate=this.Table<CommissionRateKey,CommissionRateRow>("commission_rate");
        this.Settlement=this.Table<SettlementKey,SettlementRow>("settlement");
        this.Trade=this.Table<TradeKey,TradeRow>("trade");
        this.TradeHistory=this.Table<TradeHistoryKey,TradeHistoryRow>("trade_history");
        this.TradeRequest=this.Table<TradeRequestKey,TradeRequestRow>("trade_request");
        this.TradeType=this.Table<TradeTypeKey,TradeTypeRow>("trade_type");
        this.Company=this.Table<CompanyKey,CompanyRow>("company");
        this.CompanyCompetitor=this.Table<CompanyCompetitorKey,CompanyCompetitorRow>("company_competitor");
        this.DailyMarket=this.Table<DailyMarketKey,DailyMarketRow>("daily_market");
        this.Exchange=this.Table<ExchangeKey,ExchangeRow>("exchange");
        this.Financial=this.Table<FinancialKey,FinancialRow>("financial");
        this.Industry=this.Table<IndustryKey,IndustryRow>("industry");
        this.LastTrade=this.Table<LastTradeKey,LastTradeRow>("last_trade");
        this.NewsItem=this.Table<NewsItemKey,NewsItemRow>("news_item");
        this.NewsXref=this.Table<NewsXrefKey,NewsXrefRow>("news_xref");
        this.Sector=this.Table<SectorKey,SectorRow>("sector");
        this.Security=this.Table<SecurityKey,SecurityRow>("security");
        this.Address=this.Table<AddressKey,AddressRow>("address");
        this.StatusType=this.Table<StatusTypeKey,StatusTypeRow>("status_type");
        this.Taxrate=this.Table<TaxrateKey,TaxrateRow>("taxrate");
        this.ZipCode=this.Table<ZipCodeKey,ZipCodeRow>("zip_code");
        this.Initialize(
            this.AccountPermission,
            this.Customer,
            this.CustomerAccount,
            this.CustomerTaxrate,
            this.Holding,
            this.HoldingHistory,
            this.HoldingSummary,
            this.WatchItem,
            this.WatchList,
            this.Broker,
            this.CashTransaction,
            this.Charge,
            this.CommissionRate,
            this.Settlement,
            this.Trade,
            this.TradeHistory,
            this.TradeRequest,
            this.TradeType,
            this.Company,
            this.CompanyCompetitor,
            this.DailyMarket,
            this.Exchange,
            this.Financial,
            this.Industry,
            this.LastTrade,
            this.NewsItem,
            this.NewsXref,
            this.Sector,
            this.Security,
            this.Address,
            this.StatusType,
            this.Taxrate,
            this.ZipCode);
        // 生存件数は表と索引の後に登録する(保存済みなら登録時に読み戻される)。
        store.Register(new LiveCountComponent(this.counters));
    }
    public new PersistedTable<AccountPermissionKey,AccountPermissionRow> AccountPermission{get;}
    public new PersistedTable<CustomerKey,CustomerRow> Customer{get;}
    public new PersistedTable<CustomerAccountKey,CustomerAccountRow> CustomerAccount{get;}
    public new PersistedTable<CustomerTaxrateKey,CustomerTaxrateRow> CustomerTaxrate{get;}
    public new PersistedTable<HoldingKey,HoldingRow> Holding{get;}
    public new PersistedTable<HoldingHistoryKey,HoldingHistoryRow> HoldingHistory{get;}
    public new PersistedTable<HoldingSummaryKey,HoldingSummaryRow> HoldingSummary{get;}
    public new PersistedTable<WatchItemKey,WatchItemRow> WatchItem{get;}
    public new PersistedTable<WatchListKey,WatchListRow> WatchList{get;}
    public new PersistedTable<BrokerKey,BrokerRow> Broker{get;}
    public new PersistedTable<CashTransactionKey,CashTransactionRow> CashTransaction{get;}
    public new PersistedTable<ChargeKey,ChargeRow> Charge{get;}
    public new PersistedTable<CommissionRateKey,CommissionRateRow> CommissionRate{get;}
    public new PersistedTable<SettlementKey,SettlementRow> Settlement{get;}
    public new PersistedTable<TradeKey,TradeRow> Trade{get;}
    public new PersistedTable<TradeHistoryKey,TradeHistoryRow> TradeHistory{get;}
    public new PersistedTable<TradeRequestKey,TradeRequestRow> TradeRequest{get;}
    public new PersistedTable<TradeTypeKey,TradeTypeRow> TradeType{get;}
    public new PersistedTable<CompanyKey,CompanyRow> Company{get;}
    public new PersistedTable<CompanyCompetitorKey,CompanyCompetitorRow> CompanyCompetitor{get;}
    public new PersistedTable<DailyMarketKey,DailyMarketRow> DailyMarket{get;}
    public new PersistedTable<ExchangeKey,ExchangeRow> Exchange{get;}
    public new PersistedTable<FinancialKey,FinancialRow> Financial{get;}
    public new PersistedTable<IndustryKey,IndustryRow> Industry{get;}
    public new PersistedTable<LastTradeKey,LastTradeRow> LastTrade{get;}
    public new PersistedTable<NewsItemKey,NewsItemRow> NewsItem{get;}
    public new PersistedTable<NewsXrefKey,NewsXrefRow> NewsXref{get;}
    public new PersistedTable<SectorKey,SectorRow> Sector{get;}
    public new PersistedTable<SecurityKey,SecurityRow> Security{get;}
    public new PersistedTable<AddressKey,AddressRow> Address{get;}
    public new PersistedTable<StatusTypeKey,StatusTypeRow> StatusType{get;}
    public new PersistedTable<TaxrateKey,TaxrateRow> Taxrate{get;}
    public new PersistedTable<ZipCodeKey,ZipCodeRow> ZipCode{get;}
    private PersistedTable<TKey,TRow> Table<TKey,TRow>(string name)
        where TKey:unmanaged
        where TRow:struct,IRecord<TKey>{
        var data=this.Store.CreatePersistedDictionary<TKey,Slot<TRow>>(name);
        var table=this.AddTable(new PersistedTable<TKey,TRow>(data,this.Log,this.NextTableId,name));
        this.counters.Add(table);
        return table;
    }
    protected override IKeyValueStore<TKey,TValue> CreateKeyValueStore<TKey,TValue>(string name){
        var data=this.Store.CreatePersistedDictionary<TKey,Slot<TValue>>(name);
        return new PersistedKeyValueStore<TKey,TValue>(data,name);
    }
}
/// <summary>Persisted の表の生存件数を保存と一緒に記録する子資源。</summary>
internal sealed class LiveCountComponent(List<ILiveCounter> counters):Seamless.Execution.IStoreComponent{
    public string Name=>"~persisted-live-counts";
    public string Save(){
        var values=new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var counter in counters){
            values.Add(counter.CounterName,counter.LiveCount);
        }
        return System.Text.Json.JsonSerializer.Serialize(values);
    }
    public void Flush(){
    }
    public void Load(string state){
        var values=System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string,long>>(state)??[];
        foreach(var counter in counters){
            if(values.TryGetValue(counter.CounterName,out var count)){
                counter.LiveCount=count;
            }else{
                counter.LiveCount=0;
            }
        }
    }
    public void Dispose(){
    }
}
