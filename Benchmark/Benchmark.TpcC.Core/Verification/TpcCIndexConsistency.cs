namespace Benchmark.TpcC.Core;
/// <summary>
/// 副索引が表の行と一致しているかの点検(索引を表から作り直した結果と、索引の中身を、(持ち主, 要素)の多重集合として比べる)。
/// 表の書き込み・取り消し・チャンクの追加と削除のどこかで索引が食い違うと見つかる。
/// </summary>
public static class TpcCIndexConsistency{
    private const int ExampleLimit=5;
    /// <summary>食い違いの説明(無ければ空)。</summary>
    public static IReadOnlyList<string> Check(ITpcCDatabase db){
        var problems=new List<string>();
        foreach(var partition in db.PartitionBases){
            var expectedByName=new Dictionary<(CustomerNameOwner,CustomerNameEntry),int>();
            foreach(var row in partition.Customer.EnumerateRows()){
                Add(expectedByName,(new CustomerNameOwner(row.WarehouseId,row.DistrictId,Code16.From(row.Last)),new CustomerNameEntry(Code16.From(row.First.ToUpperInvariant()),row.Id)));
            }
            var expectedOrders=new Dictionary<(CustomerOwner,long),int>();
            foreach(var row in partition.Order.EnumerateRows()){
                Add(expectedOrders,(new CustomerOwner(row.WarehouseId,row.DistrictId,row.CustomerId,0),(long)row.Id));
            }
            var expectedQueue=new Dictionary<(DistrictOwner,long),int>();
            foreach(var row in partition.NewOrder.EnumerateRows()){
                Add(expectedQueue,(new DistrictOwner(row.WarehouseId,row.DistrictId),(long)row.OrderId));
            }
            Compare(problems,partition,partition.CustomerByName,expectedByName);
            Compare(problems,partition,partition.CustomerOrders,expectedOrders);
            Compare(problems,partition,partition.NewOrderQueue,expectedQueue);
        }
        return problems;
    }
    private static void Add<TKey>(Dictionary<TKey,int> counts,TKey key) where TKey:notnull{
        counts[key]=counts.GetValueOrDefault(key)+1;
    }
    private static void Compare<TOwner,TEntry>(List<string> problems,TpcCPartitionBase partition,OrderedIndex<TOwner,TEntry> index,Dictionary<(TOwner,TEntry),int> expected)
        where TOwner:unmanaged
        where TEntry:unmanaged,IComparable<TEntry>{
        var actual=new Dictionary<(TOwner,TEntry),int>();
        foreach(var (owner,entry) in ((IIndexView)index).EnumerateEntriesBoxed()){
            Add(actual,((TOwner)owner,(TEntry)entry));
        }
        var examples=new List<string>();
        var count=0L;
        foreach(var (key,times) in expected){
            if(actual.GetValueOrDefault(key)!=times){
                count++;
                if(examples.Count<ExampleLimit){
                    examples.Add($"missing/different {key} expected {times} actual {actual.GetValueOrDefault(key)}");
                }
            }
        }
        foreach(var (key,times) in actual){
            if(!expected.ContainsKey(key)){
                count++;
                if(examples.Count<ExampleLimit){
                    examples.Add($"extra {key} x{times}");
                }
            }
        }
        if(count>0){
            problems.Add($"partition {partition.Partition} {index.Name}: {count} differences: {string.Join("; ",examples)}");
        }
    }
}
