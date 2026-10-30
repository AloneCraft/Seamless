using System.Diagnostics;
using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class EGenFlatFileImporter{
    private const string DefaultBuild=@"G:\GitHub\Seamless\Work\TpcE\egen-build";
    private const string DefaultInputs=@"G:\GitHub\Seamless\Work\TpcE\egen\flat_in";
    [Fact,Trait("Category","Unit")]public void CoversAll33Tables(){
        {
            //仕様の 33 表をすべて取り込む(EGen 不要)
            Assert.Equal(33,Compliance.EGenFlatFileImporter.TableCount);
        }
    }
    [Fact,Trait("Category","Integration")]public void ImportsEGenOutputIntoEngine(){
        {
            //EGen のビルド(build-egen.ps1 の出力)がある環境でだけ流す。無ければ何もせず抜ける(CI には入れない前提)
            var build=Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD") ?? DefaultBuild;
            var inputs=Environment.GetEnvironmentVariable("TPCE_EGEN_INPUTS") ?? DefaultInputs;
            var exe=Path.Combine(build,"EGenLoader.exe");
            if(!File.Exists(exe)||!Directory.Exists(inputs)){
                return;
            }
            var output=Path.Combine(Path.GetTempPath(),"tpce-egen-test-"+Guid.NewGuid().ToString("N"))+Path.DirectorySeparatorChar;
            Directory.CreateDirectory(output);
            try{
                var start=new ProcessStartInfo(exe){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
                foreach(var argument in new[]{"-b","1","-c","1000","-t","1000","-f","500","-w","1","-i",inputs+Path.DirectorySeparatorChar,"-o",output,"-x"}){
                    start.ArgumentList.Add(argument);
                }
                using(var process=Process.Start(start)!){
                    process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.Equal(0,process.ExitCode);
                }
                var scale=new TpcEScale{Customers=1000,InitialTradeDays=1,MarketDays=1305,NewsItemBytes=100_000,TsavoriteSegmentBytes=NativeTestProgress.SegmentBytes};
                using var store=Store.CreateTemporary();
                using var engine=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant().Open(store,scale);
                engine.Database.BeginBulkLoad();
                var counts=new Compliance.EGenFlatFileImporter(output).Import(engine.Loader);
                engine.Database.EndBulkLoad();
                var db=engine.Database;
                Assert.Equal(1000,db.Customer.Count);
                Assert.Equal(57_600,counts["Trade.txt"]);
                Assert.Equal(counts["Trade.txt"],db.Trade.Count);
                Assert.Equal(counts["Security.txt"],db.Security.Count);
                Assert.Equal(counts["DailyMarket.txt"],db.DailyMarket.Count);
                var digest=StateDigest.Of(db);
                Assert.DoesNotContain(digest.Tables,static table=>table.Name.EndsWith("#count-mismatch",StringComparison.Ordinal));
                // 口座→取引の索引に全取引が入っている。
                Assert.Equal(db.Trade.Count,digest.Indexes.Single(static index=>index.Name=="x07-account-trades").Count);
            }finally{
                try{
                    Directory.Delete(output,true);
                }catch(IOException){
                }
            }
        }
    }
}
