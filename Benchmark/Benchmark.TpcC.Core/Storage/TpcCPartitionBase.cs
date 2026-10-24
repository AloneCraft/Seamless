using Seamless.Collections;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>
/// TPC-C の区画 1 つ(連続した範囲の倉庫を持つ)。取り消し記録(TxnLog)は区画ごとに持つ。
/// 倉庫・地区・顧客・履歴・注文・新規注文・注文明細・在庫は持つ倉庫の分だけ、ITEM は区画ごとに全件を複製して持つ(読み取り専用なので区画をまたがずに引ける)。
/// 保存先ごとの派生クラスが具体的な表を作って <see cref="Initialize"/> に渡す。
/// </summary>
public abstract class TpcCPartitionBase:TpcDatabaseBase{
    protected TpcCPartitionBase(Store store,TpcCScale scale,int partition):base(store){
        this.Scale=scale;
        this.Partition=partition;
        var (first,last)=scale.WarehousesOf(partition);
        this.FirstWarehouse=first;
        this.LastWarehouse=last;
    }
    public TpcCScale Scale{get;}
    /// <summary>区画の番号(0 始まり)。</summary>
    public int Partition{get;}
    public int FirstWarehouse{get;}
    public int LastWarehouse{get;}
    public int WarehouseCount=>this.LastWarehouse-this.FirstWarehouse+1;
    public bool Owns(int warehouse)=>warehouse>=this.FirstWarehouse&&warehouse<=this.LastWarehouse;
    /// <summary>表と索引の物理名。1 つのストアに複数の区画を置いても名前が重ならないよう、区画の番号を付ける。</summary>
    public string PhysicalName(string name)=>"p"+this.Partition.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+name;
    public TableCore<WarehouseKey,WarehouseRow> Warehouse{get;private set;}=null!;
    public TableCore<DistrictKey,DistrictRow> District{get;private set;}=null!;
    public TableCore<CustomerKey,CustomerRow> Customer{get;private set;}=null!;
    public TableCore<HistoryKey,HistoryRow> History{get;private set;}=null!;
    public TableCore<NewOrderKey,NewOrderRow> NewOrder{get;private set;}=null!;
    public TableCore<OrderKey,OrderRow> Order{get;private set;}=null!;
    public TableCore<OrderLineKey,OrderLineRow> OrderLine{get;private set;}=null!;
    public TableCore<ItemKey,ItemRow> Item{get;private set;}=null!;
    public TableCore<StockKey,StockRow> Stock{get;private set;}=null!;
    /// <summary>姓から顧客を引く(名の昇順)。Payment と Order-Status の 60% が使う。</summary>
    public OrderedIndex<CustomerNameOwner,CustomerNameEntry> CustomerByName{get;private set;}=null!;
    /// <summary>顧客の注文(番号の昇順)。末尾が最新の注文。</summary>
    public OrderedIndex<CustomerOwner,long> CustomerOrders{get;private set;}=null!;
    /// <summary>地区の未配送の注文(番号の昇順)。先頭が最も古い注文。Delivery は先頭から取る。</summary>
    public OrderedIndex<DistrictOwner,long> NewOrderQueue{get;private set;}=null!;
    protected void Initialize(
        TableCore<WarehouseKey,WarehouseRow> warehouse,
        TableCore<DistrictKey,DistrictRow> district,
        TableCore<CustomerKey,CustomerRow> customer,
        TableCore<HistoryKey,HistoryRow> history,
        TableCore<NewOrderKey,NewOrderRow> newOrder,
        TableCore<OrderKey,OrderRow> order,
        TableCore<OrderLineKey,OrderLineRow> orderLine,
        TableCore<ItemKey,ItemRow> item,
        TableCore<StockKey,StockRow> stock){
        this.Warehouse=warehouse;
        this.District=district;
        this.Customer=customer;
        this.History=history;
        this.NewOrder=newOrder;
        this.Order=order;
        this.OrderLine=orderLine;
        this.Item=item;
        this.Stock=stock;
        this.CustomerByName=this.CreateIndex<CustomerNameOwner,CustomerNameEntry>(this.PhysicalName("x1-customer-by-name"));
        IndexBinding.Bind(this.Customer,this.CustomerByName,static r=>(new CustomerNameOwner(r.WarehouseId,r.DistrictId,Code16.From(r.Last)),new CustomerNameEntry(Code16.From(r.First),r.Id)));
        this.CustomerOrders=this.CreateIndex<CustomerOwner,long>(this.PhysicalName("x2-customer-orders"));
        IndexBinding.Bind(this.Order,this.CustomerOrders,static r=>(new CustomerOwner(r.WarehouseId,r.DistrictId,r.CustomerId,0),(long)r.Id));
        this.NewOrderQueue=this.CreateIndex<DistrictOwner,long>(this.PhysicalName("x3-new-order-queue"));
        IndexBinding.Bind(this.NewOrder,this.NewOrderQueue,static r=>(new DistrictOwner(r.WarehouseId,r.DistrictId),(long)r.OrderId));
    }
}
