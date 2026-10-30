using Seamless.Collections;
using Seamless.Collections.Persisted;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>Persisted に置いた TPC-C の区画。表も索引も <c>Dictionary&lt;TKey,Slot&lt;T&gt;&gt;</c>(削除は墓標)。</summary>
public sealed class PersistedTpcCPartition:TpcCPartitionBase{
    private readonly List<ILiveCounter> counters=[];
    public PersistedTpcCPartition(Store store,TpcCScale scale,int partition):base(store,scale,partition){
        this.Warehouse=this.Table<WarehouseKey,WarehouseRow>("warehouse");
        this.District=this.Table<DistrictKey,DistrictRow>("district");
        this.Customer=this.Table<CustomerKey,CustomerRow>("customer");
        this.History=this.Table<HistoryKey,HistoryRow>("history");
        this.NewOrder=this.Table<NewOrderKey,NewOrderRow>("new_order");
        this.Order=this.Table<OrderKey,OrderRow>("order");
        this.OrderLine=this.Table<OrderLineKey,OrderLineRow>("order_line");
        this.Item=this.Table<ItemKey,ItemRow>("item");
        this.Stock=this.Table<StockKey,StockRow>("stock");
        this.Initialize(this.Warehouse,this.District,this.Customer,this.History,this.NewOrder,this.Order,this.OrderLine,this.Item,this.Stock);
        // 生存件数は表と索引の後に登録する(保存済みなら登録時に読み戻される)。区画ごとに名前を分ける。
        store.Register(new LiveCountComponent(this.counters,"~persisted-live-counts-"+this.PhysicalName("tables")));
    }
    public new PersistedTable<WarehouseKey,WarehouseRow> Warehouse{get;}
    public new PersistedTable<DistrictKey,DistrictRow> District{get;}
    public new PersistedTable<CustomerKey,CustomerRow> Customer{get;}
    public new PersistedTable<HistoryKey,HistoryRow> History{get;}
    public new PersistedTable<NewOrderKey,NewOrderRow> NewOrder{get;}
    public new PersistedTable<OrderKey,OrderRow> Order{get;}
    public new PersistedTable<OrderLineKey,OrderLineRow> OrderLine{get;}
    public new PersistedTable<ItemKey,ItemRow> Item{get;}
    public new PersistedTable<StockKey,StockRow> Stock{get;}
    private PersistedTable<TKey,TRow> Table<TKey,TRow>(string table)
        where TKey:unmanaged
        where TRow:struct,IRecord<TKey>{
        var name=this.PhysicalName(table);
        // 初期容量が小さいと、ファイルが伸びるたびにディスクへの書き出しと付け替えが走って極端に遅い。見込み件数で最初から確保する。
        var (capacity,dataCapacity)=TpcCSizing.PersistedCapacity(table,TpcCSizing.ExpectedRows(table,this.Scale));
        var data=this.Store.CreatePersistedDictionary<TKey,Slot<TRow>>(name,capacity,dataCapacity);
        var created=this.AddTable(new PersistedTable<TKey,TRow>(data,this.Log,this.NextTableId,name));
        this.counters.Add(created);
        return created;
    }
    protected override IKeyValueStore<TKey,TValue> CreateKeyValueStore<TKey,TValue>(string name){
        var (capacity,dataCapacity)=TpcCSizing.PersistedIndexCapacity(name,this.Scale);
        var data=this.Store.CreatePersistedDictionary<TKey,Slot<TValue>>(name,capacity,dataCapacity);
        return new PersistedKeyValueStore<TKey,TValue>(data,name);
    }
}
/// <summary>Persisted に置いた TPC-C の DB。</summary>
public sealed class PersistedTpcCDatabase:TpcCDatabase<PersistedTpcCPartition>{
    /// <summary>全区画を 1 つの Store に置く(保存が原子的)。</summary>
    public PersistedTpcCDatabase(Store store,TpcCScale scale):base(scale,partition=>new PersistedTpcCPartition(store,scale,partition)){
    }
    /// <summary>区画ごとに別の Store に置く。</summary>
    public PersistedTpcCDatabase(IReadOnlyList<Store> stores,TpcCScale scale):base(scale,PerPartition(stores,scale)){
    }
    private static Func<int,PersistedTpcCPartition> PerPartition(IReadOnlyList<Store> stores,TpcCScale scale){
        if(stores.Count!=scale.PartitionCount){
            throw new ArgumentException("one store per partition is required.",nameof(stores));
        }
        return partition=>new PersistedTpcCPartition(stores[partition],scale,partition);
    }
}
