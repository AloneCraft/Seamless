using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
public sealed class RunOptions{
    [Fact,Trait("Category","Unit")]public void Parse(){
        {
            //再現コマンドの引数を解析し直すと元に戻る
            var original=new Core.RunOptions{Command="run",Seed=9,Customers=250,InitialTradeDays=2,Transactions=777,Warmup=5,Variants=["TF","PL"],SaveEvery=100,ReopenEvery=3,StopAt=42,TraceFrom=20,InjectFault="PF:30:Output"};
            var parsed=Core.RunOptions.Parse(original.ToArguments());
            Assert.Equal(original.ToArguments(),parsed.ToArguments());
            Assert.Equal(["TF","PL"],parsed.Variants);
        }
        {
            //誤りは ArgumentException
            Assert.Throws<ArgumentException>(()=>Core.RunOptions.Parse(["run","--nope","1"]));
            Assert.Throws<ArgumentException>(()=>Core.RunOptions.Parse(["run","--variants","XX"]));
            Assert.Throws<ArgumentException>(()=>Core.RunOptions.Parse(["run","--customers","0"]));
        }
    }
}
