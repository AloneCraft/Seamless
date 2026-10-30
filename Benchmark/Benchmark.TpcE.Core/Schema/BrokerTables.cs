using MemoryPack;
using Seamless.Collections;
namespace Benchmark.TpcE.Core;
// 仲介系の 9 表(TPC-E 2.2.5)。

public readonly record struct BrokerKey(long Id);
/// <summary>BROKER(2.2.5.1)。</summary>
[MemoryPackable]
public partial record struct BrokerRow(long Id,Code8 StatusId,string Name,long NumTrades,decimal CommissionTotal):IRecord<BrokerKey>{
    readonly BrokerKey IRecord<BrokerKey>.Key=>new(this.Id);
}
public readonly record struct CashTransactionKey(long TradeId);
/// <summary>CASH_TRANSACTION(2.2.5.2)。</summary>
[MemoryPackable]
public partial record struct CashTransactionRow(long TradeId,long Dts,decimal Amount,string? Name):IRecord<CashTransactionKey>{
    readonly CashTransactionKey IRecord<CashTransactionKey>.Key=>new(this.TradeId);
}
public readonly record struct ChargeKey(Code8 TradeTypeId,long Tier);
/// <summary>CHARGE(2.2.5.3)。層と取引種別ごとの料金。</summary>
public readonly record struct ChargeRow(Code8 TradeTypeId,long Tier,decimal Charge):IRecord<ChargeKey>{
    ChargeKey IRecord<ChargeKey>.Key=>new(this.TradeTypeId,this.Tier);
}
public readonly record struct CommissionRateKey(long Tier,Code8 TradeTypeId,Code8 ExchangeId,long FromQuantity);
/// <summary>COMMISSION_RATE(2.2.5.4)。Rate は百分率(10% は 10.00)。</summary>
public readonly record struct CommissionRateRow(long Tier,Code8 TradeTypeId,Code8 ExchangeId,long FromQuantity,long ToQuantity,decimal Rate):IRecord<CommissionRateKey>{
    CommissionRateKey IRecord<CommissionRateKey>.Key=>new(this.Tier,this.TradeTypeId,this.ExchangeId,this.FromQuantity);
}
public readonly record struct SettlementKey(long TradeId);
/// <summary>SETTLEMENT(2.2.5.5)。CashType は "Margin" か "Cash Account"(Trade-Update が別表記へ切り替える)。</summary>
[MemoryPackable]
public partial record struct SettlementRow(long TradeId,string CashType,long CashDueDate,decimal Amount):IRecord<SettlementKey>{
    readonly SettlementKey IRecord<SettlementKey>.Key=>new(this.TradeId);
}
public readonly record struct TradeKey(long Id);
/// <summary>TRADE(2.2.5.6)。TradePrice は約定前は null。</summary>
[MemoryPackable]
public partial record struct TradeRow(
    long Id,long Dts,Code8 StatusId,Code8 TypeId,bool IsCash,Code16 Symbol,int Quantity,decimal BidPrice,long AccountId,string ExecName,
    decimal? TradePrice,decimal Charge,decimal Commission,decimal Tax,bool Lifo):IRecord<TradeKey>{
    readonly TradeKey IRecord<TradeKey>.Key=>new(this.Id);
}
public readonly record struct TradeHistoryKey(long TradeId,Code8 StatusId);
/// <summary>TRADE_HISTORY(2.2.5.7)。取引が状態を移るたびの記録。</summary>
public readonly record struct TradeHistoryRow(long TradeId,long Dts,Code8 StatusId):IRecord<TradeHistoryKey>{
    TradeHistoryKey IRecord<TradeHistoryKey>.Key=>new(this.TradeId,this.StatusId);
}
public readonly record struct TradeRequestKey(long TradeId);
/// <summary>TRADE_REQUEST(2.2.5.8)。発動を待つ指値注文。</summary>
public readonly record struct TradeRequestRow(long TradeId,Code8 TradeTypeId,Code16 Symbol,int Quantity,decimal BidPrice,long BrokerId):IRecord<TradeRequestKey>{
    TradeRequestKey IRecord<TradeRequestKey>.Key=>new(this.TradeId);
}
public readonly record struct TradeTypeKey(Code8 Id);
/// <summary>TRADE_TYPE(2.2.5.9)。5 行の固定表。</summary>
[MemoryPackable]
public partial record struct TradeTypeRow(Code8 Id,string Name,bool IsSell,bool IsMarket):IRecord<TradeTypeKey>{
    readonly TradeTypeKey IRecord<TradeTypeKey>.Key=>new(this.Id);
}
