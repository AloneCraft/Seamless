using MemoryPack;
using Seamless.Collections;
namespace Benchmark.TpcE.Core;
// 市場系の 11 表(TPC-E 2.2.6)。

public readonly record struct CompanyKey(long Id);
/// <summary>COMPANY(2.2.6.1)。</summary>
[MemoryPackable]
public partial record struct CompanyRow(long Id,Code8 StatusId,string Name,Code8 IndustryId,string SpRate,string Ceo,long AddressId,string Description,long OpenDate):IRecord<CompanyKey>{
    CompanyKey IRecord<CompanyKey>.Key{
        readonly get=>new(this.Id);
        init=>this.Id=value.Id;
    }
}
public readonly record struct CompanyCompetitorKey(long CompanyId,long CompetitorId,Code8 IndustryId);
/// <summary>COMPANY_COMPETITOR(2.2.6.2)。</summary>
public readonly record struct CompanyCompetitorRow(long CompanyId,long CompetitorId,Code8 IndustryId):IRecord<CompanyCompetitorKey>{
    CompanyCompetitorKey IRecord<CompanyCompetitorKey>.Key{
        get=>new(this.CompanyId,this.CompetitorId,this.IndustryId);
        init{
            this.CompanyId=value.CompanyId;
            this.CompetitorId=value.CompetitorId;
            this.IndustryId=value.IndustryId;
        }
    }
}
public readonly record struct DailyMarketKey(Code16 Symbol,long Date);
/// <summary>DAILY_MARKET(2.2.6.3)。</summary>
public readonly record struct DailyMarketRow(long Date,Code16 Symbol,decimal Close,decimal High,decimal Low,long Volume):IRecord<DailyMarketKey>{
    DailyMarketKey IRecord<DailyMarketKey>.Key{
        get=>new(this.Symbol,this.Date);
        init{
            this.Symbol=value.Symbol;
            this.Date=value.Date;
        }
    }
}
public readonly record struct ExchangeKey(Code8 Id);
/// <summary>EXCHANGE(2.2.6.4)。Open/Close は時刻を HHMM の 4 桁で表す。</summary>
[MemoryPackable]
public partial record struct ExchangeRow(Code8 Id,string Name,int NumSymbols,int Open,int Close,string? Description,long AddressId):IRecord<ExchangeKey>{
    ExchangeKey IRecord<ExchangeKey>.Key{
        readonly get=>new(this.Id);
        init=>this.Id=value.Id;
    }
}
public readonly record struct FinancialKey(long CompanyId,long Year,long Quarter);
/// <summary>FINANCIAL(2.2.6.5)。会社ごとの四半期決算。</summary>
public readonly record struct FinancialRow(
    long CompanyId,long Year,long Quarter,long QuarterStartDate,decimal Revenue,decimal NetEarnings,decimal BasicEps,decimal DilutedEps,decimal Margin,
    decimal Inventory,decimal Assets,decimal Liability,long OutBasic,long OutDiluted):IRecord<FinancialKey>{
    FinancialKey IRecord<FinancialKey>.Key{
        get=>new(this.CompanyId,this.Year,this.Quarter);
        init{
            this.CompanyId=value.CompanyId;
            this.Year=value.Year;
            this.Quarter=value.Quarter;
        }
    }
}
public readonly record struct IndustryKey(Code8 Id);
/// <summary>INDUSTRY(2.2.6.6)。</summary>
[MemoryPackable]
public partial record struct IndustryRow(Code8 Id,string Name,Code8 SectorId):IRecord<IndustryKey>{
    IndustryKey IRecord<IndustryKey>.Key{
        readonly get=>new(this.Id);
        init=>this.Id=value.Id;
    }
}
public readonly record struct LastTradeKey(Code16 Symbol);
/// <summary>LAST_TRADE(2.2.6.7)。銘柄ごとに 1 行。</summary>
public readonly record struct LastTradeRow(Code16 Symbol,long Dts,decimal Price,decimal OpenPrice,long Volume):IRecord<LastTradeKey>{
    LastTradeKey IRecord<LastTradeKey>.Key{
        get=>new(this.Symbol);
        init=>this.Symbol=value.Symbol;
    }
}
public readonly record struct NewsItemKey(long Id);
/// <summary>NEWS_ITEM(2.2.6.8)。Item は本文(BLOB)。</summary>
[MemoryPackable]
public partial record struct NewsItemRow(long Id,string Headline,string Summary,byte[] Item,long Dts,string Source,string? Author):IRecord<NewsItemKey>{
    NewsItemKey IRecord<NewsItemKey>.Key{
        readonly get=>new(this.Id);
        init=>this.Id=value.Id;
    }
}
public readonly record struct NewsXrefKey(long NewsItemId,long CompanyId);
/// <summary>NEWS_XREF(2.2.6.9)。</summary>
public readonly record struct NewsXrefRow(long NewsItemId,long CompanyId):IRecord<NewsXrefKey>{
    NewsXrefKey IRecord<NewsXrefKey>.Key{
        get=>new(this.NewsItemId,this.CompanyId);
        init{
            this.NewsItemId=value.NewsItemId;
            this.CompanyId=value.CompanyId;
        }
    }
}
public readonly record struct SectorKey(Code8 Id);
/// <summary>SECTOR(2.2.6.10)。</summary>
[MemoryPackable]
public partial record struct SectorRow(Code8 Id,string Name):IRecord<SectorKey>{
    SectorKey IRecord<SectorKey>.Key{
        readonly get=>new(this.Id);
        init=>this.Id=value.Id;
    }
}
public readonly record struct SecurityKey(Code16 Symbol);
/// <summary>SECURITY(2.2.6.11)。Yield は百分率。</summary>
[MemoryPackable]
public partial record struct SecurityRow(
    Code16 Symbol,Code8 Issue,Code8 StatusId,string Name,Code8 ExchangeId,long CompanyId,long NumOutstanding,long StartDate,long ExchangeDate,
    decimal Pe,decimal High52Week,long High52WeekDate,decimal Low52Week,long Low52WeekDate,decimal Dividend,decimal Yield):IRecord<SecurityKey>{
    SecurityKey IRecord<SecurityKey>.Key{
        readonly get=>new(this.Symbol);
        init=>this.Symbol=value.Symbol;
    }
}
