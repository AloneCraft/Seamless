using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
// 骨組みは gen_db.py で生成し、以後は手で直す。
/// <summary>Tsavorite に置いた TPC-E の DB。表は <c>Set&lt;TKey,TRow&gt;</c>(FLinq の直接走査の起点)、索引は辞書。</summary>
public sealed class TsavoriteTpcEDatabase:TpcEDatabaseBase{
    public TsavoriteTpcEDatabase(Store store,TpcEScale scale):base(store,scale){
        var s=scale;
        this.AccountPermission=this.Table<AccountPermissionKey,AccountPermissionRow>("account_permission",s.Accounts*142/100);
        this.Customer=this.Table<CustomerKey,CustomerRow>("customer",s.Customers);
        this.CustomerAccount=this.Table<CustomerAccountKey,CustomerAccountRow>("customer_account",s.Accounts);
        this.CustomerTaxrate=this.Table<CustomerTaxrateKey,CustomerTaxrateRow>("customer_taxrate",s.Customers*2);
        this.Holding=this.Table<HoldingKey,HoldingRow>("holding",s.Accounts*10);
        this.HoldingHistory=this.Table<HoldingHistoryKey,HoldingHistoryRow>("holding_history",s.TotalTrades*134/100);
        this.HoldingSummary=this.Table<HoldingSummaryKey,HoldingSummaryRow>("holding_summary",s.Accounts*10);
        this.WatchItem=this.Table<WatchItemKey,WatchItemRow>("watch_item",s.Customers*100);
        this.WatchList=this.Table<WatchListKey,WatchListRow>("watch_list",s.Customers);
        this.Broker=this.Table<BrokerKey,BrokerRow>("broker",s.Brokers);
        this.CashTransaction=this.Table<CashTransactionKey,CashTransactionRow>("cash_transaction",s.TotalTrades*92/100);
        this.Charge=this.Table<ChargeKey,ChargeRow>("charge",15);
        this.CommissionRate=this.Table<CommissionRateKey,CommissionRateRow>("commission_rate",240);
        this.Settlement=this.Table<SettlementKey,SettlementRow>("settlement",s.TotalTrades);
        this.Trade=this.Table<TradeKey,TradeRow>("trade",s.TotalTrades);
        this.TradeHistory=this.Table<TradeHistoryKey,TradeHistoryRow>("trade_history",s.TotalTrades*24/10);
        this.TradeRequest=this.Table<TradeRequestKey,TradeRequestRow>("trade_request",4096);
        this.TradeType=this.Table<TradeTypeKey,TradeTypeRow>("trade_type",5);
        this.Company=this.Table<CompanyKey,CompanyRow>("company",s.Companies);
        this.CompanyCompetitor=this.Table<CompanyCompetitorKey,CompanyCompetitorRow>("company_competitor",s.Companies*3);
        this.DailyMarket=this.Table<DailyMarketKey,DailyMarketRow>("daily_market",s.Securities*s.MarketDays);
        this.Exchange=this.Table<ExchangeKey,ExchangeRow>("exchange",4);
        this.Financial=this.Table<FinancialKey,FinancialRow>("financial",s.Companies*20);
        this.Industry=this.Table<IndustryKey,IndustryRow>("industry",102);
        this.LastTrade=this.Table<LastTradeKey,LastTradeRow>("last_trade",s.Securities);
        this.NewsItem=this.Table<NewsItemKey,NewsItemRow>("news_item",s.Companies*2);
        this.NewsXref=this.Table<NewsXrefKey,NewsXrefRow>("news_xref",s.Companies*2);
        this.Sector=this.Table<SectorKey,SectorRow>("sector",12);
        this.Security=this.Table<SecurityKey,SecurityRow>("security",s.Securities);
        this.Address=this.Table<AddressKey,AddressRow>("address",s.Customers+s.Companies+4);
        this.StatusType=this.Table<StatusTypeKey,StatusTypeRow>("status_type",5);
        this.Taxrate=this.Table<TaxrateKey,TaxrateRow>("taxrate",320);
        this.ZipCode=this.Table<ZipCodeKey,ZipCodeRow>("zip_code",14741);
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
    }
    public new TsavoriteTable<AccountPermissionKey,AccountPermissionRow> AccountPermission{get;}
    public new TsavoriteTable<CustomerKey,CustomerRow> Customer{get;}
    public new TsavoriteTable<CustomerAccountKey,CustomerAccountRow> CustomerAccount{get;}
    public new TsavoriteTable<CustomerTaxrateKey,CustomerTaxrateRow> CustomerTaxrate{get;}
    public new TsavoriteTable<HoldingKey,HoldingRow> Holding{get;}
    public new TsavoriteTable<HoldingHistoryKey,HoldingHistoryRow> HoldingHistory{get;}
    public new TsavoriteTable<HoldingSummaryKey,HoldingSummaryRow> HoldingSummary{get;}
    public new TsavoriteTable<WatchItemKey,WatchItemRow> WatchItem{get;}
    public new TsavoriteTable<WatchListKey,WatchListRow> WatchList{get;}
    public new TsavoriteTable<BrokerKey,BrokerRow> Broker{get;}
    public new TsavoriteTable<CashTransactionKey,CashTransactionRow> CashTransaction{get;}
    public new TsavoriteTable<ChargeKey,ChargeRow> Charge{get;}
    public new TsavoriteTable<CommissionRateKey,CommissionRateRow> CommissionRate{get;}
    public new TsavoriteTable<SettlementKey,SettlementRow> Settlement{get;}
    public new TsavoriteTable<TradeKey,TradeRow> Trade{get;}
    public new TsavoriteTable<TradeHistoryKey,TradeHistoryRow> TradeHistory{get;}
    public new TsavoriteTable<TradeRequestKey,TradeRequestRow> TradeRequest{get;}
    public new TsavoriteTable<TradeTypeKey,TradeTypeRow> TradeType{get;}
    public new TsavoriteTable<CompanyKey,CompanyRow> Company{get;}
    public new TsavoriteTable<CompanyCompetitorKey,CompanyCompetitorRow> CompanyCompetitor{get;}
    public new TsavoriteTable<DailyMarketKey,DailyMarketRow> DailyMarket{get;}
    public new TsavoriteTable<ExchangeKey,ExchangeRow> Exchange{get;}
    public new TsavoriteTable<FinancialKey,FinancialRow> Financial{get;}
    public new TsavoriteTable<IndustryKey,IndustryRow> Industry{get;}
    public new TsavoriteTable<LastTradeKey,LastTradeRow> LastTrade{get;}
    public new TsavoriteTable<NewsItemKey,NewsItemRow> NewsItem{get;}
    public new TsavoriteTable<NewsXrefKey,NewsXrefRow> NewsXref{get;}
    public new TsavoriteTable<SectorKey,SectorRow> Sector{get;}
    public new TsavoriteTable<SecurityKey,SecurityRow> Security{get;}
    public new TsavoriteTable<AddressKey,AddressRow> Address{get;}
    public new TsavoriteTable<StatusTypeKey,StatusTypeRow> StatusType{get;}
    public new TsavoriteTable<TaxrateKey,TaxrateRow> Taxrate{get;}
    public new TsavoriteTable<ZipCodeKey,ZipCodeRow> ZipCode{get;}
    private TsavoriteTable<TKey,TRow> Table<TKey,TRow>(string name,long expected)
        where TKey:unmanaged
        where TRow:struct,IRecord<TKey>{
        var data=this.Store.CreateTsavoriteSet<TKey,TRow>(name,TpcESizing.TableTuning(name,expected,this.Scale),expected);
        return this.AddTable(new TsavoriteTable<TKey,TRow>(data,this.Log,this.NextTableId,name));
    }
    protected override IKeyValueStore<TKey,TValue> CreateKeyValueStore<TKey,TValue>(string name){
        var data=this.Store.CreateTsavorite<TKey,TValue>(name,TpcESizing.IndexTuning(name,this.Scale));
        return new TsavoriteKeyValueStore<TKey,TValue>(data,name);
    }
}
