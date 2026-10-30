namespace Benchmark.TpcE.Core;
// 骨組みは gen_sink.py で生成し、以後は手で直す。
/// <summary>初期投入の受け口。生成器が 1 行ずつ押し出す(全件をメモリに持たない)。</summary>
public interface ILoadSink{
    void Load(in AccountPermissionRow row);
    void Load(in CustomerRow row);
    void Load(in CustomerAccountRow row);
    void Load(in CustomerTaxrateRow row);
    void Load(in HoldingRow row);
    void Load(in HoldingHistoryRow row);
    void Load(in HoldingSummaryRow row);
    void Load(in WatchItemRow row);
    void Load(in WatchListRow row);
    void Load(in BrokerRow row);
    void Load(in CashTransactionRow row);
    void Load(in ChargeRow row);
    void Load(in CommissionRateRow row);
    void Load(in SettlementRow row);
    void Load(in TradeRow row);
    void Load(in TradeHistoryRow row);
    void Load(in TradeRequestRow row);
    void Load(in TradeTypeRow row);
    void Load(in CompanyRow row);
    void Load(in CompanyCompetitorRow row);
    void Load(in DailyMarketRow row);
    void Load(in ExchangeRow row);
    void Load(in FinancialRow row);
    void Load(in IndustryRow row);
    void Load(in LastTradeRow row);
    void Load(in NewsItemRow row);
    void Load(in NewsXrefRow row);
    void Load(in SectorRow row);
    void Load(in SecurityRow row);
    void Load(in AddressRow row);
    void Load(in StatusTypeRow row);
    void Load(in TaxrateRow row);
    void Load(in ZipCodeRow row);
}
/// <summary>DB の表へ書く受け口(取り消し記録も通知も通さない。索引は更新する)。</summary>
public sealed class DatabaseLoadSink(TpcEDatabaseBase db):ILoadSink{
    public void Load(in AccountPermissionRow row)=>db.AccountPermission.Load(row);
    public void Load(in CustomerRow row)=>db.Customer.Load(row);
    public void Load(in CustomerAccountRow row)=>db.CustomerAccount.Load(row);
    public void Load(in CustomerTaxrateRow row)=>db.CustomerTaxrate.Load(row);
    public void Load(in HoldingRow row)=>db.Holding.Load(row);
    public void Load(in HoldingHistoryRow row)=>db.HoldingHistory.Load(row);
    public void Load(in HoldingSummaryRow row)=>db.HoldingSummary.Load(row);
    public void Load(in WatchItemRow row)=>db.WatchItem.Load(row);
    public void Load(in WatchListRow row)=>db.WatchList.Load(row);
    public void Load(in BrokerRow row)=>db.Broker.Load(row);
    public void Load(in CashTransactionRow row)=>db.CashTransaction.Load(row);
    public void Load(in ChargeRow row)=>db.Charge.Load(row);
    public void Load(in CommissionRateRow row)=>db.CommissionRate.Load(row);
    public void Load(in SettlementRow row)=>db.Settlement.Load(row);
    public void Load(in TradeRow row)=>db.Trade.Load(row);
    public void Load(in TradeHistoryRow row)=>db.TradeHistory.Load(row);
    public void Load(in TradeRequestRow row)=>db.TradeRequest.Load(row);
    public void Load(in TradeTypeRow row)=>db.TradeType.Load(row);
    public void Load(in CompanyRow row)=>db.Company.Load(row);
    public void Load(in CompanyCompetitorRow row)=>db.CompanyCompetitor.Load(row);
    public void Load(in DailyMarketRow row)=>db.DailyMarket.Load(row);
    public void Load(in ExchangeRow row)=>db.Exchange.Load(row);
    public void Load(in FinancialRow row)=>db.Financial.Load(row);
    public void Load(in IndustryRow row)=>db.Industry.Load(row);
    public void Load(in LastTradeRow row)=>db.LastTrade.Load(row);
    public void Load(in NewsItemRow row)=>db.NewsItem.Load(row);
    public void Load(in NewsXrefRow row)=>db.NewsXref.Load(row);
    public void Load(in SectorRow row)=>db.Sector.Load(row);
    public void Load(in SecurityRow row)=>db.Security.Load(row);
    public void Load(in AddressRow row)=>db.Address.Load(row);
    public void Load(in StatusTypeRow row)=>db.StatusType.Load(row);
    public void Load(in TaxrateRow row)=>db.Taxrate.Load(row);
    public void Load(in ZipCodeRow row)=>db.ZipCode.Load(row);
}
/// <summary>複数の受け口へ同じ行を配る(4 通りへ 1 回の生成で投入する)。</summary>
public sealed class FanOutLoadSink(IReadOnlyList<ILoadSink> sinks):ILoadSink{
    public void Load(in AccountPermissionRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CustomerRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CustomerAccountRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CustomerTaxrateRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in HoldingRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in HoldingHistoryRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in HoldingSummaryRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in WatchItemRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in WatchListRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in BrokerRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CashTransactionRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in ChargeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CommissionRateRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in SettlementRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in TradeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in TradeHistoryRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in TradeRequestRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in TradeTypeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CompanyRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in CompanyCompetitorRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in DailyMarketRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in ExchangeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in FinancialRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in IndustryRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in LastTradeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in NewsItemRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in NewsXrefRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in SectorRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in SecurityRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in AddressRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in StatusTypeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in TaxrateRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
    public void Load(in ZipCodeRow row){
        foreach(var sink in sinks){
            sink.Load(row);
        }
    }
}
/// <summary>表ごとの件数と、順序に依らないダイジェストを取る受け口(生成の決定性の確認用)。</summary>
public sealed class CountingLoadSink:ILoadSink{
    private readonly System.Collections.Generic.Dictionary<string,TableDigestBuilder> tables=new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string,TableDigestBuilder> Tables=>this.tables;
    private TableDigestBuilder For(string name){
        if(!this.tables.TryGetValue(name,out var builder)){
            builder=new(name);
            this.tables.Add(name,builder);
        }
        return builder;
    }
    public void Load(in AccountPermissionRow row)=>this.For("account_permission").Add(row);
    public void Load(in CustomerRow row)=>this.For("customer").Add(row);
    public void Load(in CustomerAccountRow row)=>this.For("customer_account").Add(row);
    public void Load(in CustomerTaxrateRow row)=>this.For("customer_taxrate").Add(row);
    public void Load(in HoldingRow row)=>this.For("holding").Add(row);
    public void Load(in HoldingHistoryRow row)=>this.For("holding_history").Add(row);
    public void Load(in HoldingSummaryRow row)=>this.For("holding_summary").Add(row);
    public void Load(in WatchItemRow row)=>this.For("watch_item").Add(row);
    public void Load(in WatchListRow row)=>this.For("watch_list").Add(row);
    public void Load(in BrokerRow row)=>this.For("broker").Add(row);
    public void Load(in CashTransactionRow row)=>this.For("cash_transaction").Add(row);
    public void Load(in ChargeRow row)=>this.For("charge").Add(row);
    public void Load(in CommissionRateRow row)=>this.For("commission_rate").Add(row);
    public void Load(in SettlementRow row)=>this.For("settlement").Add(row);
    public void Load(in TradeRow row)=>this.For("trade").Add(row);
    public void Load(in TradeHistoryRow row)=>this.For("trade_history").Add(row);
    public void Load(in TradeRequestRow row)=>this.For("trade_request").Add(row);
    public void Load(in TradeTypeRow row)=>this.For("trade_type").Add(row);
    public void Load(in CompanyRow row)=>this.For("company").Add(row);
    public void Load(in CompanyCompetitorRow row)=>this.For("company_competitor").Add(row);
    public void Load(in DailyMarketRow row)=>this.For("daily_market").Add(row);
    public void Load(in ExchangeRow row)=>this.For("exchange").Add(row);
    public void Load(in FinancialRow row)=>this.For("financial").Add(row);
    public void Load(in IndustryRow row)=>this.For("industry").Add(row);
    public void Load(in LastTradeRow row)=>this.For("last_trade").Add(row);
    public void Load(in NewsItemRow row)=>this.For("news_item").Add(row);
    public void Load(in NewsXrefRow row)=>this.For("news_xref").Add(row);
    public void Load(in SectorRow row)=>this.For("sector").Add(row);
    public void Load(in SecurityRow row)=>this.For("security").Add(row);
    public void Load(in AddressRow row)=>this.For("address").Add(row);
    public void Load(in StatusTypeRow row)=>this.For("status_type").Add(row);
    public void Load(in TaxrateRow row)=>this.For("taxrate").Add(row);
    public void Load(in ZipCodeRow row)=>this.For("zip_code").Add(row);
}
