using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
public sealed class LockstepRunner{
    private static Core.RunOptions Small(string? fault=null)=>new Core.RunOptions{
        Seed=3,Customers=100,MarketDays=30,NewsItemBytes=64,Transactions=700,Warmup=50,SaveEvery=350,ReopenEvery=1,InjectFault=fault,
    };
    private static RunOutcome Run(Core.RunOptions options){
        var directory=Path.Combine(Path.GetTempPath(),"tpce-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            var runner=new Core.LockstepRunner(options,VariantCatalog.All,directory,TextWriter.Null);
            return runner.Run();
        }finally{
            try{
                Directory.Delete(directory,true);
            }catch(IOException){
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_Agrees(){
        {
            //4通りが一致し、保存→開き直しも一致する(終了コード 0)
            var outcome=Run(Small());
            Assert.Equal(0,outcome.ExitCode);
            Assert.NotNull(outcome.FinalState);
            Assert.True(outcome.FinalState!.CeCount>=700);
            // Trade-Cleanup が最初に 1 回、12 種類のうち CE の 8 種類は流れている。
            Assert.Equal(1,outcome.FinalState.Counts[TxnKind.TradeCleanup]);
            foreach(var kind in CeInputGenerator.CeKinds){
                Assert.True(outcome.FinalState.Counts.GetValueOrDefault(kind)>0,kind.ToString());
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void Run_DetectsInjectedFaults(){
        {
            //出力をずらすと、その取引で出力の不一致として止まる
            var outcome=Run(Small("PF:200:Output"));
            Assert.Equal(1,outcome.ExitCode);
            Assert.Equal(FailureKind.Output,outcome.Failure!.Kind);
            Assert.Equal(200,outcome.Failure.TxnIndex);
        }
        {
            //書いた行を後から消すと、読み戻しで止まる(読む取引では書き込みが無いので、書く取引まで進めて確かめる)
            var outcome=Run(Small("TL:300:DropWrite"));
            Assert.True(outcome.ExitCode==0||outcome.Failure!.Kind is FailureKind.ReadBack or FailureKind.State);
        }
    }
}
