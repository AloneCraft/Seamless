using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
public sealed class TpcCCrashChild{
    [Fact,Trait("Category","Unit")]public void Parse(){
        var options=TpcCLockstepRunner.Mini with{Command="crash-child"};
        foreach(var mode in Enum.GetValues<Core.TpcCKillMode>()){
            var original=new Core.TpcCCrashChild.Arguments(options,"a store path","PF",mode,2,3);
            var restored=Core.TpcCCrashChild.Parse(Core.TpcCCrashChild.ToArguments(original));
            Assert.Equal(original.StoreDirectory,restored.StoreDirectory);
            Assert.Equal(original.Variant,restored.Variant);
            Assert.Equal(original.Kill,restored.Kill);
            Assert.Equal(original.KillArgument,restored.KillArgument);
            Assert.Equal(original.KillWrite,restored.KillWrite);
            Assert.Equal(original.Options.ToArguments(),restored.Options.ToArguments());
        }
        Assert.Throws<ArgumentException>(()=>Core.TpcCCrashChild.Parse(["crash-child","--store"]));
        Assert.Throws<ArgumentException>(()=>Core.TpcCCrashChild.Parse(["crash-child","--store","x","--variant","PF","--kill","999"]));
    }
    [Fact,Trait("Category","Integration")]public void Run(){
        //停止しない子を2回呼び、全4変種で保存済み運転状態から続くことを検証する。
        foreach(var variant in TestDatabases.Variants){
            TpcCLockstepRunner.InDirectory(directory=>{
                var options=TpcCLockstepRunner.Mini with{Command="crash-child",Transactions=20,SaveEvery=10};
                var arguments=new Core.TpcCCrashChild.Arguments(options,directory,variant.ShortName,Core.TpcCKillMode.None,0,1);
                Assert.Equal(0,Core.TpcCCrashChild.Run(arguments,variant,TextWriter.Null));
                Assert.Equal(0,Core.TpcCCrashChild.Run(arguments,variant,TextWriter.Null));
                using var store=Store.Open(directory,Core.TpcCLockstepRunner.LimitsFor(options));
                Core.TpcCDriverState? restored=null;
                store.Register(new Core.TpcCDriverStateComponent(()=>throw new InvalidOperationException(),state=>restored=state));
                using var actual=variant.Open(store,options.Scale,options.Mode);
                Assert.NotNull(restored);
                Assert.Equal(40,restored.TxnIndex);
                Assert.Equal(40,restored.Counts.Values.Sum());
                var (referenceStore,reference)=TestDatabases.Open(variant,options.Scale,options.Mode,options.Seed);
                try{
                    var driver=new Core.TpcCDriver(options.Scale,options.Mode,options.Seed,options.Terminals,Core.TpcCDriver.Initial(options.Seed));
                    for(var index=0;index<40;index++){
                        var request=driver.Next();
                        driver.Completed(request,reference.Execute(request));
                    }
                    Assert.Empty(Core.TpcCStateDigest.Of(reference.Database).Differences(Core.TpcCStateDigest.Of(actual.Database)));
                    Assert.Equal(Core.TpcCDriverStateComponent.Serialize(driver.State),Core.TpcCDriverStateComponent.Serialize(restored));
                }finally{
                    TestDatabases.Close(referenceStore,reference);
                }
            });
        }
    }
}
