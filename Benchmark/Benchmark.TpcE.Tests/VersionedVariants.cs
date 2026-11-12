using Benchmark.TpcE.Core;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;

public sealed partial class VersionedVariants{
    [Fact,Trait("Category","Integration")]
    public void LockstepComparesLegacyAndVersionedQueriesAndReopensTheirCommonCheckpoint(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"tpc-variants-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var options=new Core.RunOptions{Customers=12,InitialTradeDays=1,MarketDays=60,NewsItemBytes=64,Transactions=32,Warmup=0,SaveEvery=16,ReopenEvery=1,MemoryMegabytes=TpcETestStores.MemoryMegabytes,DiskGigabytes=1,KeepStores=true,Variants=["TL","VL","VF"]};
        using var output=new StringWriter();var runner=new Core.LockstepRunner(options,options.Variants.Select(VariantCatalog.ByShortName).ToArray(),root,output);var result=runner.Run();
        Assert.True(result.ExitCode==0,output.ToString()+Environment.NewLine+result.Failure);Assert.NotNull(result.FinalState);Assert.True(result.FinalState.CeCount>=32);
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("VL")]
    [InlineData("VF")]
    public void DurableDriverMetadataAndBusinessRowsRecoverFromTheSameCommit(string name){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"tpc-driver-"+Guid.NewGuid().ToString("N"));var variant=VariantCatalog.ByShortName(name);var scale=new TpcEScale{Customers=12,InitialTradeDays=0,MarketDays=60,ExpectedRunTrades=64,NewsItemBytes=64};var state=new DriverState{Seed=7,Scale=scale,TxnIndex=3,TradeCleanupDone=true};
        using(var store=Store.Open(root,new(){MemoryBytes=TpcETestStores.MemoryBytes,DiskBytes=128<<20}))using(var engine=variant.Open(store,scale)){
            engine.Database.BeginBulkLoad();engine.Database.CustomerAccount.Load(new(1,1,10,"account",0,100));engine.Database.EndBulkLoad(VersionedDriverState.Encode(state));engine.Database.Checkpoint();
            engine.Database.Log.Begin();var row=engine.Database.CustomerAccount.Get(new(1));engine.Database.CustomerAccount.Update(row with{Balance=125});engine.Database.Log.Commit(VersionedDriverState.Encode(state with{TxnIndex=4}));
        }
        using(var store=Store.Open(root,new(){MemoryBytes=TpcETestStores.MemoryBytes,DiskBytes=128<<20}))using(var engine=variant.Open(store,scale)){
            Assert.Equal(4,VersionedDriverState.Restore(engine)!.TxnIndex);using var snapshot=engine.Database.BeginReadSnapshot();Assert.Equal(125,engine.Database.CustomerAccount.Get(new(1)).Balance);
        }
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("VL",false)]
    [InlineData("VL",true)]
    [InlineData("VF",false)]
    [InlineData("VF",true)]
    public void RecoveryRejectsRowsThatAdvancedWithoutTheirDriverCursor(string name,bool checkpoint){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"tpc-cursor-gap-"+Guid.NewGuid().ToString("N"));var variant=VariantCatalog.ByShortName(name);var scale=new TpcEScale{Customers=12,InitialTradeDays=0,MarketDays=60,ExpectedRunTrades=64,NewsItemBytes=64};
        using(var store=Store.Open(root,new(){MemoryBytes=TpcETestStores.MemoryBytes,DiskBytes=128<<20}))using(var engine=variant.Open(store,scale)){
            engine.Database.BeginBulkLoad();engine.Database.CustomerAccount.Load(new(1,1,10,"account",0,100));engine.Database.EndBulkLoad(VersionedDriverState.Encode(new(){Scale=scale,TxnIndex=3}));engine.Database.Checkpoint();
            engine.Database.Log.Begin();engine.Database.CustomerAccount.Update(engine.Database.CustomerAccount.Get(new(1)) with{Balance=125});engine.Database.Log.Commit();if(checkpoint){engine.Database.Checkpoint();}
        }
        using(var store=Store.Open(root,new(){MemoryBytes=TpcETestStores.MemoryBytes,DiskBytes=128<<20}))using(var engine=variant.Open(store,scale)){
            Assert.Throws<InvalidDataException>(()=>VersionedDriverState.Restore(engine));using var snapshot=engine.Database.BeginReadSnapshot();Assert.Equal(125,engine.Database.CustomerAccount.Get(new(1)).Balance);
        }
    }
    [Fact,Trait("Category","Unit")]
    public void ExplicitVariantSelectionPreservesTheExistingDefault(){
        var options=Core.RunOptions.Parse(["run","--variants","VL,VF"]);Assert.Equal(new[]{"VL","VF"},options.Variants);Assert.Equal(new[]{"TL","TF","PL","PF"},Core.RunOptions.Parse(["run","--variants","all"]).Variants);Assert.All(VariantCatalog.Versioned,static variant=>Assert.True(variant.UsesVersionedTransactions));
    }
}
