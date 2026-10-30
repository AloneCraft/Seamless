using Seamless.Collections.Tsavorite;
namespace Benchmark.TpcC.Core;
/// <summary>
/// 表ごとの見込み件数と Tsavorite の詳細設定。メモリ上のログを表の大きさに合わせる(あふれた分はディスクへ出る)。
/// 大きい規模でのメモリとディスクの見積もりは、規模の事前計測(C6)で実測して見直す。
/// </summary>
public static class TpcCSizing{
    private const long MinimumLog=1L<<18;
    private const long MaximumLog=1L<<28;
    /// <summary>区画 1 つに置く表の見込み件数。</summary>
    public static long ExpectedRows(string table,TpcCScale scale){
        long warehouses=scale.MaxWarehousesPerPartition;
        long districts=warehouses*TpcCScale.DistrictsPerWarehouse;
        var runShare=scale.ExpectedRunOrders/scale.PartitionCount+1;
        var orders=districts*scale.InitialOrdersPerDistrict+runShare;
        return table switch{
            "warehouse"=>warehouses,
            "district"=>districts,
            "customer"=>districts*scale.CustomersPerDistrict,
            "history"=>districts*scale.CustomersPerDistrict+runShare,
            "new_order"=>districts*(scale.InitialOrdersPerDistrict-scale.FirstInitialNewOrder+1)+runShare,
            "order"=>orders,
            "order_line"=>orders*10,
            "item"=>scale.Items,
            "stock"=>warehouses*scale.Items,
            _=>throw new ArgumentOutOfRangeException(nameof(table),table,"unknown table."),
        };
    }
    public static TsavoriteTuning TableTuning(string table,long expectedRows){
        var log=Math.Clamp(PowerOfTwoAtLeast(Math.Max(1,expectedRows)*RowBytes(table)),MinimumLog,MaximumLog);
        return TsavoriteTuning.Default with{LogMemorySize=log};
    }
    public static TsavoriteTuning IndexTuning(string name,TpcCScale scale){
        // 索引のチャンクは 1 個 800 バイト程度(32 件)。顧客数と注文数に比例する。
        long owners=Math.Max(64,(long)scale.MaxWarehousesPerPartition*TpcCScale.DistrictsPerWarehouse*scale.CustomersPerDistrict);
        var log=Math.Clamp(PowerOfTwoAtLeast(owners*800/8),MinimumLog,MaximumLog);
        return TsavoriteTuning.Default with{LogMemorySize=log};
    }
    /// <summary>Persisted の表の初期容量(レコード数、データの大きさ)。更新と削除も追記なので、見込みの 2 倍で確保する。</summary>
    public static (long Capacity,long DataCapacity) PersistedCapacity(string table,long expectedRows){
        var rows=Math.Max(16,expectedRows*2);
        return (rows,Math.Max(4096,rows*(RowBytes(table)+32)));
    }
    /// <summary>Persisted の索引の辞書の初期容量。</summary>
    public static (long Capacity,long DataCapacity) PersistedIndexCapacity(string name,TpcCScale scale){
        var isChunks=name.EndsWith(".c",StringComparison.Ordinal);
        long owners=Math.Max(64,(long)scale.MaxWarehousesPerPartition*TpcCScale.DistrictsPerWarehouse*scale.CustomersPerDistrict);
        var rows=owners*2;
        long entryBytes=40;
        if(isChunks){
            entryBytes=800;
        }
        return (rows,Math.Max(4096,rows*entryBytes));
    }
    private static long RowBytes(string table){
        return table switch{
            "customer"=>700,
            "stock"=>400,
            "order_line"=>110,
            "history"=>90,
            "item"=>120,
            "district"=>150,
            "warehouse"=>150,
            _=>48,
        };
    }
    private static long PowerOfTwoAtLeast(long value){
        var result=1L;
        while(result<value){
            result<<=1;
        }
        return result;
    }
}
