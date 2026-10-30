using Seamless.Collections;
namespace Benchmark.TpcE.Core;
// 表と索引の宣言。骨組みは gen_db.py で生成し、以後は手で直す。
public abstract partial class TpcEDatabaseBase{
    public TableCore<AccountPermissionKey,AccountPermissionRow> AccountPermission{get;private set;}=null!;
    public TableCore<CustomerKey,CustomerRow> Customer{get;private set;}=null!;
    public TableCore<CustomerAccountKey,CustomerAccountRow> CustomerAccount{get;private set;}=null!;
    public TableCore<CustomerTaxrateKey,CustomerTaxrateRow> CustomerTaxrate{get;private set;}=null!;
    public TableCore<HoldingKey,HoldingRow> Holding{get;private set;}=null!;
    public TableCore<HoldingHistoryKey,HoldingHistoryRow> HoldingHistory{get;private set;}=null!;
    public TableCore<HoldingSummaryKey,HoldingSummaryRow> HoldingSummary{get;private set;}=null!;
    public TableCore<WatchItemKey,WatchItemRow> WatchItem{get;private set;}=null!;
    public TableCore<WatchListKey,WatchListRow> WatchList{get;private set;}=null!;
    public TableCore<BrokerKey,BrokerRow> Broker{get;private set;}=null!;
    public TableCore<CashTransactionKey,CashTransactionRow> CashTransaction{get;private set;}=null!;
    public TableCore<ChargeKey,ChargeRow> Charge{get;private set;}=null!;
    public TableCore<CommissionRateKey,CommissionRateRow> CommissionRate{get;private set;}=null!;
    public TableCore<SettlementKey,SettlementRow> Settlement{get;private set;}=null!;
    public TableCore<TradeKey,TradeRow> Trade{get;private set;}=null!;
    public TableCore<TradeHistoryKey,TradeHistoryRow> TradeHistory{get;private set;}=null!;
    public TableCore<TradeRequestKey,TradeRequestRow> TradeRequest{get;private set;}=null!;
    public TableCore<TradeTypeKey,TradeTypeRow> TradeType{get;private set;}=null!;
    public TableCore<CompanyKey,CompanyRow> Company{get;private set;}=null!;
    public TableCore<CompanyCompetitorKey,CompanyCompetitorRow> CompanyCompetitor{get;private set;}=null!;
    public TableCore<DailyMarketKey,DailyMarketRow> DailyMarket{get;private set;}=null!;
    public TableCore<ExchangeKey,ExchangeRow> Exchange{get;private set;}=null!;
    public TableCore<FinancialKey,FinancialRow> Financial{get;private set;}=null!;
    public TableCore<IndustryKey,IndustryRow> Industry{get;private set;}=null!;
    public TableCore<LastTradeKey,LastTradeRow> LastTrade{get;private set;}=null!;
    public TableCore<NewsItemKey,NewsItemRow> NewsItem{get;private set;}=null!;
    public TableCore<NewsXrefKey,NewsXrefRow> NewsXref{get;private set;}=null!;
    public TableCore<SectorKey,SectorRow> Sector{get;private set;}=null!;
    public TableCore<SecurityKey,SecurityRow> Security{get;private set;}=null!;
    public TableCore<AddressKey,AddressRow> Address{get;private set;}=null!;
    public TableCore<StatusTypeKey,StatusTypeRow> StatusType{get;private set;}=null!;
    public TableCore<TaxrateKey,TaxrateRow> Taxrate{get;private set;}=null!;
    public TableCore<ZipCodeKey,ZipCodeRow> ZipCode{get;private set;}=null!;

    /// <summary>納税者番号→顧客(X1)</summary>
    public OrderedIndex<Code24,long> CustomerByTaxId{get;private set;}=null!;
    /// <summary>顧客→口座(X2)</summary>
    public OrderedIndex<long,long> CustomerAccounts{get;private set;}=null!;
    /// <summary>口座→権限を持つ人の納税者番号(X3)</summary>
    public OrderedIndex<long,Code24> AccountPermissions{get;private set;}=null!;
    /// <summary>顧客→税率(X4)</summary>
    public OrderedIndex<long,Code8> CustomerTaxrates{get;private set;}=null!;
    /// <summary>口座→保有の要約の銘柄(X5)</summary>
    public OrderedIndex<long,Code16> AccountHoldingSummaries{get;private set;}=null!;
    /// <summary>口座×銘柄→保有(時刻順、X6)</summary>
    public OrderedIndex<HoldingSummaryKey,TimedId> PositionHoldings{get;private set;}=null!;
    /// <summary>口座→取引(時刻順、X7)</summary>
    public OrderedIndex<long,TimedId> AccountTrades{get;private set;}=null!;
    /// <summary>銘柄→取引(時刻順、X8)</summary>
    public OrderedIndex<Code16,TimedId> SymbolTrades{get;private set;}=null!;
    /// <summary>銘柄→発動待ちの指値注文(X9)</summary>
    public OrderedIndex<Code16,long> PendingBySymbol{get;private set;}=null!;
    /// <summary>取引→その取引が変えた保有(X10)</summary>
    public OrderedIndex<long,long> HoldingHistoryByTrade{get;private set;}=null!;
    /// <summary>保有→それを変えた取引(X11)</summary>
    public OrderedIndex<long,long> HoldingHistoryByHolding{get;private set;}=null!;
    /// <summary>顧客→ウォッチリスト(X12)</summary>
    public OrderedIndex<long,long> CustomerWatchLists{get;private set;}=null!;
    /// <summary>ウォッチリスト→銘柄(X13)</summary>
    public OrderedIndex<long,Code16> WatchListItems{get;private set;}=null!;
    /// <summary>業種→会社(X14)</summary>
    public OrderedIndex<Code8,long> IndustryCompanies{get;private set;}=null!;
    /// <summary>会社→銘柄(X15)</summary>
    public OrderedIndex<long,Code16> CompanySecurities{get;private set;}=null!;
    /// <summary>会社名のハッシュ→会社(X16)</summary>
    public OrderedIndex<NameHash,long> CompanyByName{get;private set;}=null!;
    /// <summary>会社→競合(X17)</summary>
    public OrderedIndex<long,CompetitorEntry> CompanyCompetitors{get;private set;}=null!;
    /// <summary>会社→ニュース(X18)</summary>
    public OrderedIndex<long,long> CompanyNews{get;private set;}=null!;
    /// <summary>派生クラスが表を作り終えたら呼ぶ。表を基底へ渡し、索引を作って表の変化につなぐ。</summary>
    protected void Initialize(
        TableCore<AccountPermissionKey,AccountPermissionRow> accountPermission,
        TableCore<CustomerKey,CustomerRow> customer,
        TableCore<CustomerAccountKey,CustomerAccountRow> customerAccount,
        TableCore<CustomerTaxrateKey,CustomerTaxrateRow> customerTaxrate,
        TableCore<HoldingKey,HoldingRow> holding,
        TableCore<HoldingHistoryKey,HoldingHistoryRow> holdingHistory,
        TableCore<HoldingSummaryKey,HoldingSummaryRow> holdingSummary,
        TableCore<WatchItemKey,WatchItemRow> watchItem,
        TableCore<WatchListKey,WatchListRow> watchList,
        TableCore<BrokerKey,BrokerRow> broker,
        TableCore<CashTransactionKey,CashTransactionRow> cashTransaction,
        TableCore<ChargeKey,ChargeRow> charge,
        TableCore<CommissionRateKey,CommissionRateRow> commissionRate,
        TableCore<SettlementKey,SettlementRow> settlement,
        TableCore<TradeKey,TradeRow> trade,
        TableCore<TradeHistoryKey,TradeHistoryRow> tradeHistory,
        TableCore<TradeRequestKey,TradeRequestRow> tradeRequest,
        TableCore<TradeTypeKey,TradeTypeRow> tradeType,
        TableCore<CompanyKey,CompanyRow> company,
        TableCore<CompanyCompetitorKey,CompanyCompetitorRow> companyCompetitor,
        TableCore<DailyMarketKey,DailyMarketRow> dailyMarket,
        TableCore<ExchangeKey,ExchangeRow> exchange,
        TableCore<FinancialKey,FinancialRow> financial,
        TableCore<IndustryKey,IndustryRow> industry,
        TableCore<LastTradeKey,LastTradeRow> lastTrade,
        TableCore<NewsItemKey,NewsItemRow> newsItem,
        TableCore<NewsXrefKey,NewsXrefRow> newsXref,
        TableCore<SectorKey,SectorRow> sector,
        TableCore<SecurityKey,SecurityRow> security,
        TableCore<AddressKey,AddressRow> address,
        TableCore<StatusTypeKey,StatusTypeRow> statusType,
        TableCore<TaxrateKey,TaxrateRow> taxrate,
        TableCore<ZipCodeKey,ZipCodeRow> zipCode){
        this.AccountPermission=accountPermission;
        this.Customer=customer;
        this.CustomerAccount=customerAccount;
        this.CustomerTaxrate=customerTaxrate;
        this.Holding=holding;
        this.HoldingHistory=holdingHistory;
        this.HoldingSummary=holdingSummary;
        this.WatchItem=watchItem;
        this.WatchList=watchList;
        this.Broker=broker;
        this.CashTransaction=cashTransaction;
        this.Charge=charge;
        this.CommissionRate=commissionRate;
        this.Settlement=settlement;
        this.Trade=trade;
        this.TradeHistory=tradeHistory;
        this.TradeRequest=tradeRequest;
        this.TradeType=tradeType;
        this.Company=company;
        this.CompanyCompetitor=companyCompetitor;
        this.DailyMarket=dailyMarket;
        this.Exchange=exchange;
        this.Financial=financial;
        this.Industry=industry;
        this.LastTrade=lastTrade;
        this.NewsItem=newsItem;
        this.NewsXref=newsXref;
        this.Sector=sector;
        this.Security=security;
        this.Address=address;
        this.StatusType=statusType;
        this.Taxrate=taxrate;
        this.ZipCode=zipCode;
        this.CustomerByTaxId=this.CreateIndex<Code24,long>("x01-customer-by-tax-id");
        IndexBinding.Bind(this.Customer,this.CustomerByTaxId,static r=>(r.TaxId,r.Id));
        this.CustomerAccounts=this.CreateIndex<long,long>("x02-customer-accounts");
        IndexBinding.Bind(this.CustomerAccount,this.CustomerAccounts,static r=>(r.CustomerId,r.Id));
        this.AccountPermissions=this.CreateIndex<long,Code24>("x03-account-permissions");
        IndexBinding.Bind(this.AccountPermission,this.AccountPermissions,static r=>(r.AccountId,r.TaxId));
        this.CustomerTaxrates=this.CreateIndex<long,Code8>("x04-customer-taxrates");
        IndexBinding.Bind(this.CustomerTaxrate,this.CustomerTaxrates,static r=>(r.CustomerId,r.TaxrateId));
        this.AccountHoldingSummaries=this.CreateIndex<long,Code16>("x05-account-holding-summaries");
        IndexBinding.Bind(this.HoldingSummary,this.AccountHoldingSummaries,static r=>(r.AccountId,r.Symbol));
        this.PositionHoldings=this.CreateIndex<HoldingSummaryKey,TimedId>("x06-position-holdings");
        IndexBinding.Bind(this.Holding,this.PositionHoldings,static r=>(new HoldingSummaryKey(r.AccountId,r.Symbol),new TimedId(r.Dts,r.TradeId)));
        this.AccountTrades=this.CreateIndex<long,TimedId>("x07-account-trades");
        IndexBinding.Bind(this.Trade,this.AccountTrades,static r=>(r.AccountId,new TimedId(r.Dts,r.Id)));
        this.SymbolTrades=this.CreateIndex<Code16,TimedId>("x08-symbol-trades");
        IndexBinding.Bind(this.Trade,this.SymbolTrades,static r=>(r.Symbol,new TimedId(r.Dts,r.Id)));
        this.PendingBySymbol=this.CreateIndex<Code16,long>("x09-pending-by-symbol");
        IndexBinding.Bind(this.TradeRequest,this.PendingBySymbol,static r=>(r.Symbol,r.TradeId));
        this.HoldingHistoryByTrade=this.CreateIndex<long,long>("x10-holding-history-by-trade");
        IndexBinding.Bind(this.HoldingHistory,this.HoldingHistoryByTrade,static r=>(r.TradeId,r.HoldingTradeId));
        this.HoldingHistoryByHolding=this.CreateIndex<long,long>("x11-holding-history-by-holding");
        IndexBinding.Bind(this.HoldingHistory,this.HoldingHistoryByHolding,static r=>(r.HoldingTradeId,r.TradeId));
        this.CustomerWatchLists=this.CreateIndex<long,long>("x12-customer-watch-lists");
        IndexBinding.Bind(this.WatchList,this.CustomerWatchLists,static r=>(r.CustomerId,r.Id));
        this.WatchListItems=this.CreateIndex<long,Code16>("x13-watch-list-items");
        IndexBinding.Bind(this.WatchItem,this.WatchListItems,static r=>(r.WatchListId,r.Symbol));
        this.IndustryCompanies=this.CreateIndex<Code8,long>("x14-industry-companies");
        IndexBinding.Bind(this.Company,this.IndustryCompanies,static r=>(r.IndustryId,r.Id));
        this.CompanySecurities=this.CreateIndex<long,Code16>("x15-company-securities");
        IndexBinding.Bind(this.Security,this.CompanySecurities,static r=>(r.CompanyId,r.Symbol));
        this.CompanyByName=this.CreateIndex<NameHash,long>("x16-company-by-name");
        IndexBinding.Bind(this.Company,this.CompanyByName,static r=>(NameHash.Of(r.Name),r.Id));
        this.CompanyCompetitors=this.CreateIndex<long,CompetitorEntry>("x17-company-competitors");
        IndexBinding.Bind(this.CompanyCompetitor,this.CompanyCompetitors,static r=>(r.CompanyId,new CompetitorEntry(r.CompetitorId,r.IndustryId)));
        this.CompanyNews=this.CreateIndex<long,long>("x18-company-news");
        IndexBinding.Bind(this.NewsXref,this.CompanyNews,static r=>(r.CompanyId,r.NewsItemId));
    }
}
