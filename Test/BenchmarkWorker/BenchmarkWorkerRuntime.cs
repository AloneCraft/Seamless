using BenchmarkDotNet.Jobs;

namespace Test.BenchmarkWorker;

public sealed class BenchmarkWorkerRuntime {
    [Fact]
    public void アフィニティマスクを作成_対応CPUだけを許可する() {
        Assert.Equal(1UL<<2,global::ベンチマークワーカーランタイム.アフィニティマスクを作成(2,8,64));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.アフィニティマスクを作成(-1,8,64));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.アフィニティマスクを作成(8,8,64));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.アフィニティマスクを作成(63,64,64));
    }

    [Fact]
    public void スレッドアフィニティ適用結果を検証_ゼロなら失敗する() {
        global::ベンチマークワーカーランタイム.スレッドアフィニティ適用結果を検証(1,2,5);
        var 例外=Assert.Throws<global::System.ComponentModel.Win32Exception>(() =>
            global::ベンチマークワーカーランタイム.スレッドアフィニティ適用結果を検証(0,2,5));
        Assert.Equal(5,例外.NativeErrorCode);
    }

    [Fact]
    public void 設定を作成_正確と中間の反復回数を分離する() {
        var 正確設定=global::ベンチマークワーカーランタイム.設定を作成("artifacts",8,7);
        var 中間設定=global::ベンチマークワーカーランタイム.設定を作成("artifacts",8,1);

        var 正確ジョブ=Assert.Single(正確設定.GetJobs());
        var 中間ジョブ=Assert.Single(中間設定.GetJobs());
        Assert.Equal(1,設定値を取得(正確ジョブ,"Run","LaunchCount"));
        Assert.Equal(1,設定値を取得(正確ジョブ,"Run","WarmupCount"));
        Assert.Equal(7,設定値を取得(正確ジョブ,"Run","IterationCount"));
        Assert.Equal(8,設定値を取得(正確ジョブ,"Run","InvocationCount"));
        Assert.Equal(1,設定値を取得(中間ジョブ,"Run","IterationCount"));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.設定を作成("artifacts",8,0));
    }

    [Fact]
    public void 一バッチ割当バイト数を正規化_反復回数に依存しない() {
        Assert.Equal(7_168,global::ベンチマークワーカーランタイム.一バッチ割当バイト数を正規化(7_168,4,4));
        Assert.Equal(7_168,global::ベンチマークワーカーランタイム.一バッチ割当バイト数を正規化(50_176,28,4));
        Assert.Equal(7_168,global::ベンチマークワーカーランタイム.一バッチ割当バイト数を正規化(7_168,null,4));
        Assert.Null(global::ベンチマークワーカーランタイム.一バッチ割当バイト数を正規化(null,4,4));
    }

    [Fact]
    public void ウォームアップ目標ナノ秒を正規化_三百ミリ秒から一秒二十四ミリ秒へ制限する() {
        Assert.Equal(300_000_000d,global::ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(1d));
        Assert.Equal(500_000_000d,global::ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(500_000_000d));
        Assert.Equal(1_024_000_000d,global::ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(double.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => global::ベンチマークワーカーランタイム.ウォームアップ目標ナノ秒を正規化(double.PositiveInfinity));
    }

    [Fact]
    public void 次の較正ループ回数を取得_倍増し上限後は失敗する() {
        Assert.Equal(2,global::ベンチマークワーカーランタイム.次の較正ループ回数を取得(1));
        Assert.Equal(int.MaxValue,global::ベンチマークワーカーランタイム.次の較正ループ回数を取得((int.MaxValue/2)+1));
        Assert.Throws<InvalidOperationException>(() => global::ベンチマークワーカーランタイム.次の較正ループ回数を取得(int.MaxValue));
    }

    [Fact]
    public void ループ回数を較正_冷間初回を時間外にして一バッチを倍増する() {
        var 呼出回数=0;

        var 結果=global::ベンチマークワーカーランタイム.ループ回数を較正(() => {
            呼出回数++;
            if(呼出回数==1) {
                Thread.Sleep(20);
            }
        },1_000_000d,1);

        Assert.True(結果.ループ回数>1);
        Assert.True(結果.バッチナノ秒>=1_000_000d);
        Assert.Equal(300_000_000d,結果.ウォームアップ目標ナノ秒);
    }

    [Fact]
    public async Task 開放ループ較正_実WorkerはLoopCountだけを返す() {
        var 実行ルート=Path.Combine(Path.GetTempPath(),"Seamless-Test-WorkerCalibration-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(実行ルート);
        global::System.Diagnostics.Process? プロセス=null;
        try {
            var 開始情報=new global::System.Diagnostics.ProcessStartInfo {
                FileName=Path.Combine(AppContext.BaseDirectory,"Worker.exe"),
                UseShellExecute=false,
                RedirectStandardInput=true,
                RedirectStandardOutput=true,
                RedirectStandardError=true,
                CreateNoWindow=true,
            };
            開始情報.ArgumentList.Add("--logical-cpu");
            開始情報.ArgumentList.Add("0");
            開始情報.ArgumentList.Add("--run-root");
            開始情報.ArgumentList.Add(実行ルート);
            プロセス=global::System.Diagnostics.Process.Start(開始情報)
                ??throw new InvalidOperationException("Workerを開始できませんでした。");
            Assert.Equal("ready",await プロセス.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));

            var メッセージ=new {
                コマンド="run",
                タスクID="calibration-test",
                アセンブリ名="Benchmark.System.Linq.Enumerable.Range.ZLinq",
                型完全名="Benchmark.System.Linq.Enumerable.Range.ZLinq.ZLinqInt32",
                メソッド名="Sum",
                引数キー=string.Empty,
                引数=global::System.Array.Empty<object>(),
                ソースタブ=6,
                準備済み要素数=new[]{4_096},
                要素数=4_096,
                ループ回数=1,
                要素倍率="1",
                成果物フォルダー=Path.Combine(実行ルート,"artifact"),
                開放ループ較正か=true,
                較正閾値ナノ秒=1_000_000d,
                ベンチマーク反復回数=1,
                ウォームアップ目標ナノ秒=1_000_000d,
            };
            await プロセス.StandardInput.WriteLineAsync(global::System.Text.Json.JsonSerializer.Serialize(メッセージ));
            await プロセス.StandardInput.FlushAsync();
            var 応答行=await プロセス.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            using var 応答=global::System.Text.Json.JsonDocument.Parse(応答行??throw new InvalidOperationException("Worker応答がありません。"));
            var ルート=応答.RootElement;

            Assert.Equal("Completed",ルート.GetProperty("状態").GetString());
            Assert.True(ルート.GetProperty("アフィニティ適用成功か").GetBoolean());
            Assert.Equal(string.Empty,ルート.GetProperty("平均").GetString());
            Assert.Equal(string.Empty,ルート.GetProperty("誤差").GetString());
            Assert.Equal(string.Empty,ルート.GetProperty("標準偏差").GetString());
            Assert.Equal(global::System.Text.Json.JsonValueKind.Null,ルート.GetProperty("平均ナノ秒").ValueKind);
            Assert.Equal(global::System.Text.Json.JsonValueKind.Null,ルート.GetProperty("誤差ナノ秒").ValueKind);
            Assert.Equal(global::System.Text.Json.JsonValueKind.Null,ルート.GetProperty("標準偏差ナノ秒").ValueKind);
            Assert.Equal(global::System.Text.Json.JsonValueKind.Null,ルート.GetProperty("割り当てバイト数").ValueKind);
            Assert.True(ルート.GetProperty("実測ループ回数").GetInt32()>=1);
        } finally {
            if(プロセス is not null) {
                try {
                    if(!プロセス.HasExited) {
                        プロセス.Kill(true);
                        await プロセス.WaitForExitAsync();
                    }
                } finally {
                    プロセス.Dispose();
                }
            }
            if(Directory.Exists(実行ルート)) {
                Directory.Delete(実行ルート,true);
            }
        }
    }

    [Fact]
    public async Task 一バッチ割当バイト数_実Workerの反復一と七で同じ値を返す() {
        var 反復一の割当バイト数=await ToList割当バイト数を測定Async(1);
        var 反復七の割当バイト数=await ToList割当バイト数を測定Async(7);

        Assert.True(反復一の割当バイト数>0);
        Assert.Equal(反復一の割当バイト数,反復七の割当バイト数);
    }

    private static int 設定値を取得(Job ジョブ,string モード名,string 設定名) {
        var モード=ジョブ.GetType().GetProperty(モード名)?.GetValue(ジョブ)
            ??throw new InvalidOperationException($"BenchmarkDotNet Job.{モード名}が見つかりません。");
        var 値=モード.GetType().GetProperty(設定名)?.GetValue(モード)
            ??throw new InvalidOperationException($"BenchmarkDotNet Job.{モード名}.{設定名}が見つかりません。");
        return Convert.ToInt32(値,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> ToList割当バイト数を測定Async(int ベンチマーク反復回数) {
        var 実行ルート=Path.Combine(
            Path.GetTempPath(),
            "Seamless-Test-WorkerAllocation-"+ベンチマーク反復回数+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(実行ルート);
        global::System.Diagnostics.Process? プロセス=null;
        try {
            var 開始情報=new global::System.Diagnostics.ProcessStartInfo {
                FileName=Path.Combine(AppContext.BaseDirectory,"Worker.exe"),
                UseShellExecute=false,
                RedirectStandardInput=true,
                RedirectStandardOutput=true,
                RedirectStandardError=true,
                CreateNoWindow=true,
            };
            開始情報.ArgumentList.Add("--logical-cpu");
            開始情報.ArgumentList.Add("0");
            開始情報.ArgumentList.Add("--run-root");
            開始情報.ArgumentList.Add(実行ルート);
            プロセス=global::System.Diagnostics.Process.Start(開始情報)
                ??throw new InvalidOperationException("Workerを開始できませんでした。");
            Assert.Equal("ready",await プロセス.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));

            var メッセージ=new {
                コマンド="run",
                タスクID="allocation-iteration-"+ベンチマーク反復回数,
                アセンブリ名="Benchmark.System.Linq.Enumerable.FLinq",
                型完全名="Benchmark.System.Linq.Enumerable.FLinq.FLinqClassInt32",
                メソッド名="ToList",
                引数キー=string.Empty,
                引数=global::System.Array.Empty<object>(),
                ソースタブ=4,
                準備済み要素数=new[]{4_096},
                要素数=4_096,
                ループ回数=4,
                要素倍率="1",
                成果物フォルダー=Path.Combine(実行ルート,"artifact"),
                開放ループ較正か=false,
                較正閾値ナノ秒=(double?)null,
                ベンチマーク反復回数,
                ウォームアップ目標ナノ秒=1_000_000d,
            };
            await プロセス.StandardInput.WriteLineAsync(global::System.Text.Json.JsonSerializer.Serialize(メッセージ));
            await プロセス.StandardInput.FlushAsync();
            var 応答行=await プロセス.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
            using var 応答=global::System.Text.Json.JsonDocument.Parse(応答行??throw new InvalidOperationException("Worker応答がありません。"));
            var ルート=応答.RootElement;

            Assert.True(
                string.Equals("Completed",ルート.GetProperty("状態").GetString(),StringComparison.Ordinal),
                ルート.GetProperty("ログ").GetString());
            return ルート.GetProperty("割り当てバイト数").GetInt64();
        } finally {
            if(プロセス is not null) {
                try {
                    if(!プロセス.HasExited) {
                        プロセス.Kill(true);
                        await プロセス.WaitForExitAsync();
                    }
                } finally {
                    プロセス.Dispose();
                }
            }
            if(Directory.Exists(実行ルート)) {
                Directory.Delete(実行ルート,true);
            }
        }
    }

}
