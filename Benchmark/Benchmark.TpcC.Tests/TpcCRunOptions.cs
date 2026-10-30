namespace Benchmark.TpcC.Tests;
public sealed class TpcCRunOptions{
    [Fact,Trait("Category","Unit")]public void Parse(){
        var original=TpcCLockstepRunner.Mini with{Command="crash",Mode=Core.TpcCMode.Spec,Variants=["TF","PF"],Shadow=false,StopAt=19,TraceFrom=2,InjectFault="PF:10:Output",MemoryMegabytes=512,Cycles=4,Cpu=0,Priority="idle"};
        var restored=Core.TpcCRunOptions.Parse(original.ToArguments());
        Assert.Equal(original.ToArguments(),restored.ToArguments());
        // 既定の再現引数は変えず、有効時だけflagを往復する。
        Assert.False(Core.TpcCRunOptions.Parse([]).HopThreads);
        Assert.False(Core.TpcCRunOptions.Parse(["--hop-threads","false"]).HopThreads);
        Assert.DoesNotContain("--hop-threads",original.ToArguments());
        var hopping=TpcCLockstepRunner.Mini with{HopThreads=true};
        Assert.True(Core.TpcCRunOptions.Parse(hopping.ToArguments()).HopThreads);
        Assert.Equal(hopping.ToArguments(),Core.TpcCRunOptions.Parse(hopping.ToArguments()).ToArguments());
        foreach(var args in new string[][]{["--hop-threads"],["--hop-threads","yes"],["--hop-threads","1"],["crash","--hop-threads","true"],["generate","--hop-threads","true"],["describe","--hop-threads","true"],["crash-child","--hop-threads","true"]}){
            Assert.Throws<ArgumentException>(()=>Core.TpcCRunOptions.Parse(args));
        }
        foreach(var args in new string[][]{["--transactions","-1"],["--save-every","-1"],["--disk-gb","0"],["--variants",""],["crash","--save-every","0"],["--cpu","-1"],["--priority","unknown"],["--items"]}){
            Assert.Throws<ArgumentException>(()=>Core.TpcCRunOptions.Parse(args));
        }
    }
}
