using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
/// <summary>試験用の小さな DB。品目 200・地区あたりの顧客 30 に縮小して、初期投入を軽くする。</summary>
internal static class TestDatabases{
    public static Core.TpcCScale Mini(int warehouses,int partitions)=>new(){Warehouses=warehouses,Partitions=partitions,Items=200,CustomersPerDistrict=30,ExpectedRunOrders=1_000};
    /// <summary>4 つの組み合わせ(Tsavorite/Persisted × 標準 LINQ/FLinq)。</summary>
    public static IReadOnlyList<Core.ITpcCVariant> Variants{get;}=[
        new global::Benchmark.TpcC.Tsavorite.Linq.TsavoriteLinqVariant(),
        new global::Benchmark.TpcC.Tsavorite.FLinq.TsavoriteFLinqVariant(),
        new global::Benchmark.TpcC.Persisted.Linq.PersistedLinqVariant(),
        new global::Benchmark.TpcC.Persisted.FLinq.PersistedFLinqVariant(),
    ];
    /// <summary>組み合わせを開いて初期データを流し込む。</summary>
    public static (Store Store,Core.ITpcCEngine Engine) Open(Core.ITpcCVariant variant,Core.TpcCScale scale,Core.TpcCMode mode,ulong seed=1){
        var store=Store.CreateTemporary();
        var engine=variant.Open(store,scale,mode);
        engine.Database.BeginBulkLoad();
        new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(mode),seed).Generate(engine.Loader);
        engine.Database.EndBulkLoad();
        return (store,engine);
    }
    /// <summary>倉庫 w を持つ区画。</summary>
    public static Core.TpcCPartitionBase PartitionOf(Core.ITpcCEngine engine,int warehouse)=>engine.Database.PartitionBases[engine.Database.Scale.PartitionOf(warehouse)];
    public static void Close(Store store,Core.ITpcCEngine engine){
        engine.Dispose();
        store.Dispose();
    }
    /// <summary>1 つの Store に全区画を置いた Tsavorite の DB へ初期データを流し込んで返す。</summary>
    /// <summary>索引も取り消し記録も通さずに行を書く(故障の再現用。ITableView の明示実装を呼ぶ)。</summary>
    public static void PutRaw<TKey,TRow>(TableCore<TKey,TRow> table,TRow row) where TKey:unmanaged where TRow:struct,Seamless.Collections.IRecord<TKey>{
        ((ITableView)table).PutRawBoxed(row);
    }
    /// <summary>索引も取り消し記録も通さずに行を消す(故障の再現用)。</summary>
    public static void DeleteRaw<TKey,TRow>(TableCore<TKey,TRow> table,TKey key) where TKey:unmanaged where TRow:struct,Seamless.Collections.IRecord<TKey>{
        ((ITableView)table).DeleteRawBoxed(key);
    }
    public static (Store Store,Core.TsavoriteTpcCDatabase Database) Load(Core.TpcCScale scale,Core.TpcCMode mode=Core.TpcCMode.Spec,ulong seed=1){
        var store=Store.CreateTemporary();
        var database=new Core.TsavoriteTpcCDatabase(store,scale);
        database.BeginBulkLoad();
        new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(mode),seed).Generate(new Core.TpcCDatabaseLoadSink<Core.TsavoriteTpcCPartition>(database));
        database.EndBulkLoad();
        return (store,database);
    }
}
