using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>Tsavorite に置いた TPC-C の区画。表は <c>Set&lt;TKey,TRow&gt;</c>(FLinq の直接走査の起点)、索引は辞書。</summary>
public sealed class TsavoriteTpcCPartition:TpcCPartitionBase{
    public TsavoriteTpcCPartition(Store store,TpcCScale scale,int partition):base(store,scale,partition){
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
    }
    public new TsavoriteTable<WarehouseKey,WarehouseRow> Warehouse{get;}
    public new TsavoriteTable<DistrictKey,DistrictRow> District{get;}
    public new TsavoriteTable<CustomerKey,CustomerRow> Customer{get;}
    public new TsavoriteTable<HistoryKey,HistoryRow> History{get;}
    public new TsavoriteTable<NewOrderKey,NewOrderRow> NewOrder{get;}
    public new TsavoriteTable<OrderKey,OrderRow> Order{get;}
    public new TsavoriteTable<OrderLineKey,OrderLineRow> OrderLine{get;}
    public new TsavoriteTable<ItemKey,ItemRow> Item{get;}
    public new TsavoriteTable<StockKey,StockRow> Stock{get;}
    private TsavoriteTable<TKey,TRow> Table<TKey,TRow>(string table)
        where TKey:unmanaged
        where TRow:struct,IRecord<TKey>{
        var name=this.PhysicalName(table);
        try{
            var expected=TpcCSizing.ExpectedRows(table,this.Scale);
            var data=this.Store.CreateTsavoriteSet<TKey,TRow>(name,TpcCSizing.TableTuning(table,expected),expected);
            return this.AddTable(new TsavoriteTable<TKey,TRow>(data,this.Log,this.NextTableId,name));
        }catch(Exception error){
            throw new InvalidOperationException("Tsavorite table "+name+" could not be opened: "+error.Message,error);
        }
    }
    protected override IKeyValueStore<TKey,TValue> CreateKeyValueStore<TKey,TValue>(string name){
        try{
            var data=this.Store.CreateTsavorite<TKey,TValue>(name,TpcCSizing.IndexTuning(name,this.Scale));
            return new TsavoriteKeyValueStore<TKey,TValue>(data,name);
        }catch(Exception error){
            throw new InvalidOperationException("Tsavorite store "+name+" could not be opened: "+error.Message,error);
        }
    }
}
