using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
public sealed class PersistedTpcCDatabase{
    [Fact,Trait("Category","Integration")]public void Load(){
        {
            //Persisted に流し込んだ DB は、Tsavorite の DB と全表・全索引のダイジェストが一致し、一貫性の条件にも合格する(区画は 2 つ、全区画を 1 つの Store に置く)
            var scale=TestDatabases.Mini(3,2);
            var (tsavoriteStore,tsavorite)=TestDatabases.Load(scale);
            using var persistedStore=Store.CreateTemporary();
            using var persisted=new Core.PersistedTpcCDatabase(persistedStore,scale);
            try{
                persisted.BeginBulkLoad();
                new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(Core.TpcCMode.Spec),1).Generate(new Core.TpcCDatabaseLoadSink<Core.PersistedTpcCPartition>(persisted));
                persisted.EndBulkLoad();
                Assert.Empty(Core.TpcCStateDigest.Of(tsavorite).Differences(Core.TpcCStateDigest.Of(persisted)));
                Assert.Empty(Core.TpcCStateDigest.ItemReplicaDifferences(persisted));
                Assert.All(Core.TpcCConsistency.Check(persisted,deep:true,initial:true),result=>Assert.True(result.Passed,$"CC{result.Condition}: {result.Detail}"));
                Assert.Equal(3*10*30,persisted.Partitions.Sum(partition=>partition.Customer.Count));
            }finally{
                tsavorite.Dispose();
                tsavoriteStore.Dispose();
            }
        }
        {
            //区画ごとに別の Store に置いても同じ
            var scale=TestDatabases.Mini(3,2);
            var stores=new List<Store>{Store.CreateTemporary(),Store.CreateTemporary()};
            var (sharedStore,shared)=TestDatabases.Load(scale);
            var separate=new Core.PersistedTpcCDatabase(stores,scale);
            try{
                separate.BeginBulkLoad();
                new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(Core.TpcCMode.Spec),1).Generate(new Core.TpcCDatabaseLoadSink<Core.PersistedTpcCPartition>(separate));
                separate.EndBulkLoad();
                Assert.Empty(Core.TpcCStateDigest.Of(shared).Differences(Core.TpcCStateDigest.Of(separate)));
            }finally{
                separate.Dispose();
                shared.Dispose();
                sharedStore.Dispose();
                foreach(var store in stores){
                    store.Dispose();
                }
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void Constructor(){
        {
            //区画の数と Store の数が合わなければ、区画を作る前に例外にする
            var stores=new List<Store>{Store.CreateTemporary()};
            try{
                Assert.Throws<ArgumentException>(()=>new Core.PersistedTpcCDatabase(stores,TestDatabases.Mini(3,2)));
                Assert.Throws<ArgumentException>(()=>new Core.TsavoriteTpcCDatabase(stores,TestDatabases.Mini(3,2)));
            }finally{
                stores[0].Dispose();
            }
        }
    }
}
