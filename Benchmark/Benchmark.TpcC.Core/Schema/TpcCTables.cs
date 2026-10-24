using MemoryPack;
using Seamless.Collections;
namespace Benchmark.TpcC.Core;
// TPC-C 5.11 の 9 表(1.3)。金額は decimal(仕様は正確な数値型を要求する)。
// 日時は 1970-01-01 からの秒(仕様は 1 秒の分解能)。null になり得る O_CARRIER_ID と OL_DELIVERY_D は 0 で表す(範囲外の値で常に同じ)。
// 鍵は詰め物の入らない unmanaged(Tsavorite と Persisted は鍵をバイト列で比べる)。

public readonly record struct WarehouseKey(int Id);
/// <summary>WAREHOUSE(1.3.1)。</summary>
[MemoryPackable]
public partial record struct WarehouseRow(int Id,string Name,string Street1,string Street2,string City,string State,string Zip,decimal Tax,decimal Ytd):IRecord<WarehouseKey>{
    readonly WarehouseKey IRecord<WarehouseKey>.Key=>new(this.Id);
}
public readonly record struct DistrictKey(int WarehouseId,int Id);
/// <summary>
/// DISTRICT。<see cref="NextHistoryId"/> は仕様の列ではなく、HISTORY に主キーが無いので代理の連番を地区ごとに採るための値
/// (区画の数に依らない鍵にするため、地区の行に持つ)。
/// </summary>
[MemoryPackable]
public partial record struct DistrictRow(int WarehouseId,int Id,string Name,string Street1,string Street2,string City,string State,string Zip,decimal Tax,decimal Ytd,int NextOrderId,int NextHistoryId):IRecord<DistrictKey>{
    readonly DistrictKey IRecord<DistrictKey>.Key=>new(this.WarehouseId,this.Id);
}
public readonly record struct CustomerKey(int WarehouseId,int DistrictId,int Id);
/// <summary>CUSTOMER。Credit は "GC" か "BC"。</summary>
[MemoryPackable]
public partial record struct CustomerRow(
    int WarehouseId,int DistrictId,int Id,string First,string Middle,string Last,string Street1,string Street2,string City,string State,string Zip,string Phone,
    long Since,string Credit,decimal CreditLimit,decimal Discount,decimal Balance,decimal YtdPayment,int PaymentCount,int DeliveryCount,string Data):IRecord<CustomerKey>{
    readonly CustomerKey IRecord<CustomerKey>.Key=>new(this.WarehouseId,this.DistrictId,this.Id);
}
public readonly record struct HistoryKey(int WarehouseId,int DistrictId,int Id);
/// <summary>HISTORY。仕様に主キーが無いので、(支払先の倉庫, 地区, 地区ごとの連番)を代理の鍵にする。</summary>
[MemoryPackable]
public partial record struct HistoryRow(int WarehouseId,int DistrictId,int Id,int CustomerWarehouseId,int CustomerDistrictId,int CustomerId,long Date,decimal Amount,string Data):IRecord<HistoryKey>{
    readonly HistoryKey IRecord<HistoryKey>.Key=>new(this.WarehouseId,this.DistrictId,this.Id);
}
public readonly record struct NewOrderKey(int WarehouseId,int DistrictId,int OrderId);
/// <summary>NEW-ORDER。</summary>
public readonly record struct NewOrderRow(int WarehouseId,int DistrictId,int OrderId):IRecord<NewOrderKey>{
    NewOrderKey IRecord<NewOrderKey>.Key=>new(this.WarehouseId,this.DistrictId,this.OrderId);
}
public readonly record struct OrderKey(int WarehouseId,int DistrictId,int Id);
/// <summary>ORDER。CarrierId は未配送なら 0。</summary>
public readonly record struct OrderRow(int WarehouseId,int DistrictId,int Id,int CustomerId,long EntryDate,int CarrierId,int LineCount,int AllLocal):IRecord<OrderKey>{
    OrderKey IRecord<OrderKey>.Key=>new(this.WarehouseId,this.DistrictId,this.Id);
}
public readonly record struct OrderLineKey(int WarehouseId,int DistrictId,int OrderId,int Number);
/// <summary>ORDER-LINE。DeliveryDate は未配送なら 0。</summary>
[MemoryPackable]
public partial record struct OrderLineRow(int WarehouseId,int DistrictId,int OrderId,int Number,int ItemId,int SupplyWarehouseId,long DeliveryDate,int Quantity,decimal Amount,string DistInfo):IRecord<OrderLineKey>{
    readonly OrderLineKey IRecord<OrderLineKey>.Key=>new(this.WarehouseId,this.DistrictId,this.OrderId,this.Number);
}
public readonly record struct ItemKey(int Id);
/// <summary>ITEM。倉庫に依らない読み取り専用の表なので、区画ごとに複製する。</summary>
[MemoryPackable]
public partial record struct ItemRow(int Id,int ImageId,string Name,decimal Price,string Data):IRecord<ItemKey>{
    readonly ItemKey IRecord<ItemKey>.Key=>new(this.Id);
}
public readonly record struct StockKey(int WarehouseId,int ItemId);
/// <summary>STOCK。Dist01〜Dist10 は S_DIST_01〜S_DIST_10。</summary>
[MemoryPackable]
public partial record struct StockRow(
    int WarehouseId,int ItemId,int Quantity,
    string Dist01,string Dist02,string Dist03,string Dist04,string Dist05,string Dist06,string Dist07,string Dist08,string Dist09,string Dist10,
    long Ytd,int OrderCount,int RemoteCount,string Data):IRecord<StockKey>{
    readonly StockKey IRecord<StockKey>.Key=>new(this.WarehouseId,this.ItemId);
    /// <summary>地区 d(1〜10)の S_DIST_xx。</summary>
    public string DistInfoOf(int district){
        switch(district){
            case 1:
                return this.Dist01;
            case 2:
                return this.Dist02;
            case 3:
                return this.Dist03;
            case 4:
                return this.Dist04;
            case 5:
                return this.Dist05;
            case 6:
                return this.Dist06;
            case 7:
                return this.Dist07;
            case 8:
                return this.Dist08;
            case 9:
                return this.Dist09;
            case 10:
                return this.Dist10;
            default:
                throw new ArgumentOutOfRangeException(nameof(district),district,"district must be 1..10.");
        }
    }
}
// ---- 副索引の持ち主と要素(持ち主は 8 バイトの倍数にして、チャンクの鍵に詰め物が入らないようにする) ----
/// <summary>(倉庫, 地区)を持ち主とする索引。</summary>
public readonly record struct DistrictOwner(int WarehouseId,int DistrictId);
/// <summary>(倉庫, 地区, 顧客)を持ち主とする索引。末尾の Reserved は 8 バイトの倍数にするための 0。</summary>
public readonly record struct CustomerOwner(int WarehouseId,int DistrictId,int CustomerId,int Reserved);
/// <summary>(倉庫, 地区, 姓)を持ち主とする索引。</summary>
public readonly record struct CustomerNameOwner(int WarehouseId,int DistrictId,Code16 Last);
/// <summary>姓の索引の要素。名の昇順(同じ名なら顧客 ID の昇順)に並ぶ。</summary>
public readonly record struct CustomerNameEntry(Code16 First,long CustomerId):IComparable<CustomerNameEntry>{
    public int CompareTo(CustomerNameEntry other){
        var first=this.First.CompareTo(other.First);
        if(first!=0){
            return first;
        }
        return this.CustomerId.CompareTo(other.CustomerId);
    }
}
