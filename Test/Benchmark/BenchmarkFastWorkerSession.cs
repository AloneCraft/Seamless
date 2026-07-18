using System.Diagnostics;

namespace Test.Benchmark;

public sealed class ベンチマーク高速ワーカーセッション {
    [Fact]
    public async Task 実行Async_結果を返さず終了したワーカーは保留ジョブを失敗で解除する() {
        var 実行ルート=Path.Combine(Path.GetTempPath(),"Seamless-Test-FastWorkerEof-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(実行ルート);
        using var プロセス=Process.Start(new ProcessStartInfo {
            FileName=Environment.GetEnvironmentVariable("ComSpec")??"cmd.exe",
            Arguments="/d /c \"echo ready & set /p task= & exit /b 0\"",
            UseShellExecute=false,
            RedirectStandardInput=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            CreateNoWindow=true,
        })??throw new InvalidOperationException("EOF再現ワーカーを開始できませんでした。");
        try {
            Assert.Equal("ready",(await プロセス.StandardOutput.ReadLineAsync())?.Trim());
            await using var セッション=new global::UI.ベンチマーク高速ワーカーセッション(プロセス,実行ルート);
            var ジョブ=new global::UI.ベンチマークジョブ {
                プロジェクト="System.Range.Linq",
                アセンブリ名="Benchmark.System.Linq.Enumerable.Range.Linq",
                型完全名="Benchmark.System.Linq.Enumerable.Range.LinqInt32",
                メソッド名="SequenceEqual_second",
                ソースタブ=global::UI.ベンチマークソースタブ.System範囲,
                要素数=4_096,
                ループ回数=1,
                準備済み要素数一覧=[4_096],
            };

            var 例外=await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await セッション.実行Async(ジョブ,0,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Contains("結果を返さず終了",例外.Message,StringComparison.Ordinal);
        } finally {
            try {
                if(!プロセス.HasExited) {
                    プロセス.Kill(true);
                    await プロセス.WaitForExitAsync();
                }
            } catch(InvalidOperationException) {
            }
            Directory.Delete(実行ルート,true);
        }
    }
}