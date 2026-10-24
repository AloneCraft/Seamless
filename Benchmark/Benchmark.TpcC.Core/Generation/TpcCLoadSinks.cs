namespace Benchmark.TpcC.Core;
/// <summary>初期データの受け口。ITEM は倉庫に依らず 1 回だけ渡される(区画ごとの複製は受け口が行う)。</summary>
public interface ITpcCLoadSink{
    void Load(in WarehouseRow row);
    void Load(in DistrictRow row);
    void Load(in CustomerRow row);
    void Load(in HistoryRow row);
    void Load(in NewOrderRow row);
    void Load(in OrderRow row);
    void Load(in OrderLineRow row);
    void Load(in ItemRow row);
    void Load(in StockRow row);
}
/// <summary>DB へ直接流し込む(取り消し記録も通知も通さず、索引は更新する)。<see cref="TpcCDatabase{TPartition}.BeginBulkLoad"/> の間に使う。</summary>
public sealed class TpcCDatabaseLoadSink<TPartition>(TpcCDatabase<TPartition> db):ITpcCLoadSink where TPartition:TpcCPartitionBase{
    public void Load(in WarehouseRow row)=>db.For(row.Id).Warehouse.Load(row);
    public void Load(in DistrictRow row)=>db.For(row.WarehouseId).District.Load(row);
    public void Load(in CustomerRow row)=>db.For(row.WarehouseId).Customer.Load(row);
    public void Load(in HistoryRow row)=>db.For(row.WarehouseId).History.Load(row);
    public void Load(in NewOrderRow row)=>db.For(row.WarehouseId).NewOrder.Load(row);
    public void Load(in OrderRow row)=>db.For(row.WarehouseId).Order.Load(row);
    public void Load(in OrderLineRow row)=>db.For(row.WarehouseId).OrderLine.Load(row);
    public void Load(in StockRow row)=>db.For(row.WarehouseId).Stock.Load(row);
    public void Load(in ItemRow row){
        foreach(var partition in db.Partitions){
            partition.Item.Load(row);
        }
    }
}
/// <summary>件数だけ数える(generate コマンドと試験用)。</summary>
public sealed class TpcCCountingLoadSink:ITpcCLoadSink{
    public long Warehouses{get;private set;}
    public long Districts{get;private set;}
    public long Customers{get;private set;}
    public long Histories{get;private set;}
    public long NewOrders{get;private set;}
    public long Orders{get;private set;}
    public long OrderLines{get;private set;}
    public long Items{get;private set;}
    public long Stocks{get;private set;}
    public void Load(in WarehouseRow row)=>this.Warehouses++;
    public void Load(in DistrictRow row)=>this.Districts++;
    public void Load(in CustomerRow row)=>this.Customers++;
    public void Load(in HistoryRow row)=>this.Histories++;
    public void Load(in NewOrderRow row)=>this.NewOrders++;
    public void Load(in OrderRow row)=>this.Orders++;
    public void Load(in OrderLineRow row)=>this.OrderLines++;
    public void Load(in ItemRow row)=>this.Items++;
    public void Load(in StockRow row)=>this.Stocks++;
}
