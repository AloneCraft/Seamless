using MemoryPack;
using Seamless.Collections;
namespace Benchmark.TpcE.Core;
// 顧客系の 9 表(TPC-E 2.2.4)。キーは long/ulong だけの構造体(パディング無し)、行は IRecord を明示的に実装する。
// 列名の対応は TPC-E の接頭辞(AP_, C_, CA_, CX_, H_, HH_, HS_, WI_, WL_)を外した名前。

/// <summary>ACCOUNT_PERMISSION の主キー (AP_CA_ID, AP_TAX_ID)。</summary>
public readonly record struct AccountPermissionKey(long AccountId,Code24 TaxId);
/// <summary>ACCOUNT_PERMISSION(2.2.4.1)。口座で取引できる人と権限。</summary>
[MemoryPackable]
public partial record struct AccountPermissionRow(long AccountId,Code8 Acl,Code24 TaxId,string LastName,string FirstName):IRecord<AccountPermissionKey>{
    readonly AccountPermissionKey IRecord<AccountPermissionKey>.Key=>new(this.AccountId,this.TaxId);
}
public readonly record struct CustomerKey(long Id);
/// <summary>CUSTOMER(2.2.4.2)。電話は 3 組。</summary>
[MemoryPackable]
public partial record struct CustomerRow(
    long Id,Code24 TaxId,Code8 StatusId,string LastName,string FirstName,string? MiddleInitial,string? Gender,int Tier,long DateOfBirth,long AddressId,
    string? Country1,string? Area1,string? Local1,string? Extension1,
    string? Country2,string? Area2,string? Local2,string? Extension2,
    string? Country3,string? Area3,string? Local3,string? Extension3,
    string? Email1,string? Email2):IRecord<CustomerKey>{
    readonly CustomerKey IRecord<CustomerKey>.Key=>new(this.Id);
}
public readonly record struct CustomerAccountKey(long Id);
/// <summary>CUSTOMER_ACCOUNT(2.2.4.3)。TaxStatus: 0=非課税、1=課税で源泉徴収あり、2=課税で源泉徴収なし。</summary>
[MemoryPackable]
public partial record struct CustomerAccountRow(long Id,long BrokerId,long CustomerId,string? Name,int TaxStatus,decimal Balance):IRecord<CustomerAccountKey>{
    readonly CustomerAccountKey IRecord<CustomerAccountKey>.Key=>new(this.Id);
}
public readonly record struct CustomerTaxrateKey(Code8 TaxrateId,long CustomerId);
/// <summary>CUSTOMER_TAXRATE(2.2.4.4)。顧客 1 人につき 2 行(国と地域)。</summary>
public readonly record struct CustomerTaxrateRow(Code8 TaxrateId,long CustomerId):IRecord<CustomerTaxrateKey>{
    CustomerTaxrateKey IRecord<CustomerTaxrateKey>.Key=>new(this.TaxrateId,this.CustomerId);
}
public readonly record struct HoldingKey(long TradeId);
/// <summary>HOLDING(2.2.4.5)。Quantity は買い持ちが正、売り持ちが負。</summary>
public readonly record struct HoldingRow(long TradeId,long AccountId,Code16 Symbol,long Dts,decimal Price,int Quantity):IRecord<HoldingKey>{
    HoldingKey IRecord<HoldingKey>.Key=>new(this.TradeId);
}
public readonly record struct HoldingHistoryKey(long HoldingTradeId,long TradeId);
/// <summary>HOLDING_HISTORY(2.2.4.6)。保有行を変えた取引の記録。</summary>
public readonly record struct HoldingHistoryRow(long HoldingTradeId,long TradeId,int BeforeQuantity,int AfterQuantity):IRecord<HoldingHistoryKey>{
    HoldingHistoryKey IRecord<HoldingHistoryKey>.Key=>new(this.HoldingTradeId,this.TradeId);
}
public readonly record struct HoldingSummaryKey(long AccountId,Code16 Symbol);
/// <summary>HOLDING_SUMMARY(2.2.4.7)。口座×銘柄の合計保有数量。</summary>
public readonly record struct HoldingSummaryRow(long AccountId,Code16 Symbol,int Quantity):IRecord<HoldingSummaryKey>{
    HoldingSummaryKey IRecord<HoldingSummaryKey>.Key=>new(this.AccountId,this.Symbol);
}
public readonly record struct WatchItemKey(long WatchListId,Code16 Symbol);
/// <summary>WATCH_ITEM(2.2.4.8)。</summary>
public readonly record struct WatchItemRow(long WatchListId,Code16 Symbol):IRecord<WatchItemKey>{
    WatchItemKey IRecord<WatchItemKey>.Key=>new(this.WatchListId,this.Symbol);
}
public readonly record struct WatchListKey(long Id);
/// <summary>WATCH_LIST(2.2.4.9)。</summary>
public readonly record struct WatchListRow(long Id,long CustomerId):IRecord<WatchListKey>{
    WatchListKey IRecord<WatchListKey>.Key=>new(this.Id);
}
