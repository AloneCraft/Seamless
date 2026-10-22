using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>
/// EGen の実データ(1,000 顧客・初期取引 1 日)を取り込んだ Tsavorite+FLinq のエンジンと、EGenHarness.dll を一度だけ用意する。
/// EGen のビルド(build-egen.ps1 の出力)が無い環境では <see cref="Available"/> が false で、試験は何もせず抜ける。
/// </summary>
public sealed class EGenEngineFixture:IDisposable{
    private const string DefaultBuild=@"G:\GitHub\Seamless\Work\TpcE\egen-build";
    private readonly Store? store;
    private readonly string? flat;
    public EGenEngineFixture(){
        var build=Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD") ?? DefaultBuild;
        var dll=Path.Combine(build,"EGenHarness.dll");
        this.flat=EGenFixture.Generate(1000,1);
        if(this.flat is null||!File.Exists(dll)){
            return;
        }
        this.store=Store.CreateTemporary();
        var scale=new TpcEScale{Customers=1000,InitialTradeDays=1,MarketDays=1305,NewsItemBytes=100_000};
        this.Engine=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant().Open(this.store,scale);
        this.Engine.Database.BeginBulkLoad();
        new Compliance.EGenFlatFileImporter(this.flat).Import(this.Engine.Loader);
        this.Engine.Database.EndBulkLoad();
        this.Host=new Compliance.HarnessHost(dll);
    }
    /// <summary>
    /// 実データとハーネスが使えるか。使えないときは試験は何もせず抜ける(CI には入れない前提)。
    /// 環境変数 TPCE_REQUIRE_EGEN=1 のときは、使えないことを失敗にする(実際に流れたことを確かめるため)。
    /// </summary>
    public bool Available{
        get{
            if(this.Engine is null&&Environment.GetEnvironmentVariable("TPCE_REQUIRE_EGEN")=="1"){
                throw new InvalidOperationException("EGen data or EGenHarness.dll is not available, but TPCE_REQUIRE_EGEN=1.");
            }
            return this.Engine is not null;
        }
    }
    public ITpcEEngine? Engine{get;}
    internal Compliance.HarnessHost? Host{get;}
    public void Dispose(){
        this.Host?.Dispose();
        this.Engine?.Dispose();
        this.store?.Dispose();
        if(this.flat is not null){
            EGenFixture.Delete(this.flat);
        }
    }
}
