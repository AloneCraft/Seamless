namespace Benchmark.TpcC.Tests;
public sealed class TpcCDriver{
    [Fact,Trait("Category","Unit")]public void Next(){
        foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
            var options=TpcCLockstepRunner.Mini with{Mode=mode};
            var driver=new Core.TpcCDriver(options.Scale,mode,options.Seed,options.Terminals,Core.TpcCDriver.Initial(options.Seed));
            for(var index=0;index<37;index++){
                driver.Next();
            }
            var restored=new Core.TpcCDriver(options.Scale,mode,options.Seed,options.Terminals,driver.State.DeepClone());
            for(var index=0;index<100;index++){
                Assert.Equal(CanonicalWriter.ToBytes(driver.Next()),CanonicalWriter.ToBytes(restored.Next()));
            }
        }
    }
}
