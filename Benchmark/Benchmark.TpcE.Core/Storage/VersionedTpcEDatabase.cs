using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
// 既存の表・索引宣言を共通domainへ接続する明示adapter。
/// <summary>明示した共通MVCC domainへTPC-E全主表と索引を登録する。RowsはFLinqのnative走査の起点。</summary>
public sealed class VersionedTpcEDatabase:TpcEDatabaseBase{
    private readonly TsavoriteTuning tuning;
    public VersionedTpcDomain Domain{get;}
    public VersionedTpcEDatabase(Store store,TpcEScale scale,global::Seamless.Transactions.TransactionCoordinatorOptions transactions,global::Seamless.Execution.ExecutionOptions execution,TsavoriteTuning tuning):base(store,scale){
        this.tuning=tuning;this.Domain=this.Own(new VersionedTpcDomain(store,this.Log,transactions,execution));
        try{
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
        this.Domain.Recover();
        }catch{this.Dispose();throw;}
    }
    // 共通の私有変更が更新をまとめるので、従来のscope外BulkCacheは使わない。
    public override void BeginBulkLoad()=>this.Log.Begin();
    public override void EndBulkLoad()=>this.Log.Commit();

    public new VersionedTpcTable<AccountPermissionKey,AccountPermissionRow> AccountPermission{get;}
    public new VersionedTpcTable<CustomerKey,CustomerRow> Customer{get;}
    public new VersionedTpcTable<CustomerAccountKey,CustomerAccountRow> CustomerAccount{get;}
    public new VersionedTpcTable<CustomerTaxrateKey,CustomerTaxrateRow> CustomerTaxrate{get;}
    public new VersionedTpcTable<HoldingKey,HoldingRow> Holding{get;}
    public new VersionedTpcTable<HoldingHistoryKey,HoldingHistoryRow> HoldingHistory{get;}
    public new VersionedTpcTable<HoldingSummaryKey,HoldingSummaryRow> HoldingSummary{get;}
    public new VersionedTpcTable<WatchItemKey,WatchItemRow> WatchItem{get;}
    public new VersionedTpcTable<WatchListKey,WatchListRow> WatchList{get;}
    public new VersionedTpcTable<BrokerKey,BrokerRow> Broker{get;}
    public new VersionedTpcTable<CashTransactionKey,CashTransactionRow> CashTransaction{get;}
    public new VersionedTpcTable<ChargeKey,ChargeRow> Charge{get;}
    public new VersionedTpcTable<CommissionRateKey,CommissionRateRow> CommissionRate{get;}
    public new VersionedTpcTable<SettlementKey,SettlementRow> Settlement{get;}
    public new VersionedTpcTable<TradeKey,TradeRow> Trade{get;}
    public new VersionedTpcTable<TradeHistoryKey,TradeHistoryRow> TradeHistory{get;}
    public new VersionedTpcTable<TradeRequestKey,TradeRequestRow> TradeRequest{get;}
    public new VersionedTpcTable<TradeTypeKey,TradeTypeRow> TradeType{get;}
    public new VersionedTpcTable<CompanyKey,CompanyRow> Company{get;}
    public new VersionedTpcTable<CompanyCompetitorKey,CompanyCompetitorRow> CompanyCompetitor{get;}
    public new VersionedTpcTable<DailyMarketKey,DailyMarketRow> DailyMarket{get;}
    public new VersionedTpcTable<ExchangeKey,ExchangeRow> Exchange{get;}
    public new VersionedTpcTable<FinancialKey,FinancialRow> Financial{get;}
    public new VersionedTpcTable<IndustryKey,IndustryRow> Industry{get;}
    public new VersionedTpcTable<LastTradeKey,LastTradeRow> LastTrade{get;}
    public new VersionedTpcTable<NewsItemKey,NewsItemRow> NewsItem{get;}
    public new VersionedTpcTable<NewsXrefKey,NewsXrefRow> NewsXref{get;}
    public new VersionedTpcTable<SectorKey,SectorRow> Sector{get;}
    public new VersionedTpcTable<SecurityKey,SecurityRow> Security{get;}
    public new VersionedTpcTable<AddressKey,AddressRow> Address{get;}
    public new VersionedTpcTable<StatusTypeKey,StatusTypeRow> StatusType{get;}
    public new VersionedTpcTable<TaxrateKey,TaxrateRow> Taxrate{get;}
    public new VersionedTpcTable<ZipCodeKey,ZipCodeRow> ZipCode{get;}
    private VersionedTpcTable<TKey,TRow> Table<TKey,TRow>(string name,long expected)
        where TKey:unmanaged
        where TRow:struct,IRecord<TKey>{
        try{
            var data=this.Domain.CreateTable<TKey,TRow>(name,static row=>row.Key,this.tuning);
            return this.AddTable(new VersionedTpcTable<TKey,TRow>(this.Domain,data,this.Log,this.NextTableId,name));
        }catch(Exception error){
            throw new InvalidOperationException("Tsavorite table "+name+" could not be opened: "+error.Message,error);
        }
    }
    protected override IKeyValueStore<TKey,TValue> CreateKeyValueStore<TKey,TValue>(string name){
        try{
            return new VersionedKeyValueStore<TKey,TValue>(this.Domain,name,this.tuning);
        }catch(Exception error){
            throw new InvalidOperationException("Tsavorite store "+name+" could not be opened: "+error.Message,error);
        }
    }
}
