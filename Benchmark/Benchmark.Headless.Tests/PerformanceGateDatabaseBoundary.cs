using System.Security.Cryptography;
using UI;

namespace Benchmark.Headless.Tests;

public sealed class PerformanceGateDatabaseBoundaryTest{
    [Fact]
    public void 不合格ならDB保存を呼ばずDB指紋を変えない(){
        var 一時ルート=Path.Combine(Path.GetTempPath(),"Seamless.PerformanceGateBoundary",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(一時ルート);
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            File.WriteAllBytes(DBパス,[0x53,0x65,0x61,0x6d,0x6c,0x65,0x73,0x73]);
            var 変更前指紋=指紋を取得(DBパス);
            var 保存呼出回数=0;
            var 不合格判定=new ベンチマーク性能ゲート判定(
                false,
                [new("failing-target",false,1.01d,0.51d,["意図した不合格"])]);

            var 保存結果=global::Benchmark.Headless.PerformanceGateDatabaseBoundary.判定後更新を実行(
                不合格判定,
                ベンチマーク性能ゲートロジック.必須比較元実行ID,
                ()=>{
                    保存呼出回数++;
                    File.AppendAllBytes(DBパス,[0xff]);
                    return new(250,DBパス+".bak");
                });

            Assert.Null(保存結果);
            Assert.Equal(0,保存呼出回数);
            Assert.Equal(変更前指紋,指紋を取得(DBパス));
        }finally{
            Directory.Delete(一時ルート,true);
        }
    }

    private static string 指紋を取得(string パス)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(パス)));
}
