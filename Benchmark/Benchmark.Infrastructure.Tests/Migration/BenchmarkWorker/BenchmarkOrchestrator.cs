using UI;

namespace Test.BenchmarkWorker;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマークオーケストレーター {
    private const int 前回完了軸数 = 1024;
    private const int 実行中軸数 = 2048;
    private const int 固定ベンチマーク軸数 = ベンチマーク作業軸.既定ループ回数;
    private const double 高速左平均ナノ秒 = 876_100;
    private const double 低速右平均ナノ秒 = 1_406_300;
    private const double 基準平均ナノ秒 = 900_000;
    private const double 今回左平均ナノ秒 = 1_200_000;
    private const double 今回右平均ナノ秒 = 1_300_000;

    [Fact]
    public void 仕様確認01() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [128],
            ジョブ一覧= [
                new ベンチマークジョブ{
                    ジョブID="ジョブ",
                    プロジェクト="System.Array.FLinq",
                    アセンブリ名="Assembly",
                    型完全名="Type",
                    メソッド名="Where",
                    要素数=128,
                    状態=ジョブ状態.実行中,
                },
            ],
        };

        状態マネージャー.保存(状態);
        var 読込結果 = 状態マネージャー.読み込む(実行ルート);

        var ジョブ = Assert.Single(読込結果!.ジョブ一覧);
        Assert.Equal(ジョブ状態.失敗,ジョブ.状態);
        Assert.Contains("クラッシュ",ジョブ.失敗理由,StringComparison.Ordinal);
    }

    [Fact]
    public async Task 仕様確認02() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new global::UI.ベンチマーク状態マネージャー(ルート);
        var 取消元 = new CancellationTokenSource();
        var 開始通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ワーカー解放通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 実行器 = new 遅延偽実行器(async (ジョブ,_) => {
            開始通知.TrySetResult();
            // 実プロセスの後始末に時間がかかる状況を模す（キャンセルトークン自体は監視しない）。
            await ワーカー解放通知.Task;
            取消元.Token.ThrowIfCancellationRequested();
            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        var 実行タスク = オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            取消元.Token);

        await 開始通知.Task;
        取消元.Cancel();
        await Task.Delay(50);
        {
            // ワーカーがまだ後始末中の間は、外側の実行Asyncは戻ってこない（待たずに戻る競合状態がない）ことを確認する。
            Assert.False(実行タスク.IsCompleted);
        }

        ワーカー解放通知.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 実行タスク);

        var 実行ルート = Directory.GetDirectories(ルート).Single();
        var 取消後状態 = new global::UI.ベンチマーク状態マネージャー(ルート).読み込む(実行ルート)!;
        {
            // 外側の実行Asyncが戻った時点で実行中のまま残っているジョブがないことを確認する。
            Assert.DoesNotContain(取消後状態.ジョブ一覧,static ジョブ => ジョブ.状態==ジョブ状態.実行中);
        }

        var 再開実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var 再開オーケストレーター = new global::UI.ベンチマークオーケストレーター(new global::UI.ベンチマーク状態マネージャー(ルート),再開実行器);
        await 再開オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 再開状態 = new global::UI.ベンチマーク状態マネージャー(ルート).読み込む(実行ルート)!;
        {
            // 再開直後に読込のクラッシュ復旧ロジックが失敗へ誤確定させず、正しく完了になることを確認する。
            Assert.DoesNotContain(再開状態.ジョブ一覧,static ジョブ => ジョブ.状態==ジョブ状態.失敗);
            Assert.Contains(再開状態.ジョブ一覧,static ジョブ => ジョブ.状態==ジョブ状態.完了);
        }
    }

    [Fact]
    public async Task 仕様確認03() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => new ベンチマークジョブ実行結果(
            ジョブ.プロジェクト,
            ジョブ.メソッド名,
            ジョブ.型完全名,
            ベンチマーク作業軸.軸数を取得(ジョブ),
            "Completed",
            "100 ns",
            string.Empty,
            string.Empty,
                ベンチマーク作業軸.軸数を取得(ジョブ)>=256 ? 60_000_000_000 : 100,
            0,
            0,
            0,
            0,
            ジョブ.成果物フォルダー,
            string.Empty));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [128,256],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        // 一時較正プローブは状態へ残らず、最終測定の右列1件+左列1件だけが保存される。
        Assert.Equal(2,状態.ジョブ一覧.Count);
        Assert.All(状態.ジョブ一覧,static ジョブ => Assert.Equal(ジョブ状態.完了,ジョブ.状態));
    }

    [Fact]
    public async Task 仕様確認04() {
        var ルート = 一時ルートを作成();
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    new ベンチマークケース(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.System.Linq.Enumerable.FLinqInt32","Aggregate_func"),
                    new ベンチマークケース(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.System.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
                ],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.正確,
                ソースタブ: ソースタブ.タブ),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);

        Assert.Equal(2,状態.ジョブ一覧.Count);
        Assert.Contains(状態.ジョブ一覧,ジョブ => ジョブ.プロジェクト==ソースタブ.左プロジェクト&&ジョブ.状態 is ジョブ状態.完了 or ジョブ状態.失敗);
        Assert.Contains(状態.ジョブ一覧,ジョブ => ジョブ.プロジェクト==ソースタブ.右プロジェクト&&ジョブ.状態 is ジョブ状態.完了 or ジョブ状態.失敗);
    }

    [Fact]
    public async Task 仕様確認05() {
        var ルート = 一時ルートを作成();
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    new ベンチマークケース(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqInt32","Aggregate_func"),
                ],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.正確,
                ソースタブ: ソースタブ.タブ),
            _ => { },
            _ => { },
            CancellationToken.None);

        var ジョブ = Assert.Single(唯一の状態を読み込む(ルート).ジョブ一覧);
        Assert.Equal(ソースタブ.タブ,ジョブ.ソースタブ);
        Assert.Equal(ソースタブ.右プロジェクト,ジョブ.プロジェクト);
        Assert.Equal(ソースタブ.右アセンブリ名,ジョブ.アセンブリ名);
        Assert.True(ジョブ.状態 is ジョブ状態.完了 or ジョブ状態.失敗);
    }

    [Fact]
    public async Task 仕様確認06() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        var 結果 = await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1,2],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(Path.Combine(ルート,"000001"),結果.実行ルート);
        Assert.Empty(Directory.EnumerateFiles(結果.実行ルート,"*.csv",SearchOption.AllDirectories));
        var 状態 = new ベンチマーク状態マネージャー(ルート).読み込む(結果.実行ルート)!;
        Assert.Equal(1,状態.実行番号);
    }

    [Fact]
    public async Task 仕様確認07() {
        var ルート = 一時ルートを作成();
        var 実行ルート = Path.Combine(ルート,"000001");
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        var 結果 = await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(実行ルート,結果.実行ルート);
        Assert.Single(Directory.GetDirectories(ルート));
        Assert.Empty(Directory.EnumerateFiles(実行ルート,"*.csv",SearchOption.AllDirectories));
    }

    [Fact]
    public async Task 仕様確認08() {
        var ルート = 一時ルートを作成();
        // 各ケースが1軸目で閾値超過して止まるよう、平均値を閾値(1ms)より大きくする。
        var 実行器 = new 遅延偽実行器(async (ジョブ,_) => {
            await Task.Delay(10);
            return new ベンチマークジョブ実行結果(
                ジョブ.プロジェクト,
                ジョブ.メソッド名,
                ジョブ.型完全名,
                ベンチマーク作業軸.軸数を取得(ジョブ),
                "Completed",
                "2 ms",
                string.Empty,
                string.Empty,
                2_000_000,
                0,
                0,
                0,
                0,
                ジョブ.成果物フォルダー,
                string.Empty);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    ケースを作成("System.Array.FLinq","Where"),
                    ケースを作成("System.Array.ZLinq","Where"),
                    ケースを作成("System.Array.FLinq","Select"),
                    ケースを作成("System.Array.ZLinq","Select"),
                    ケースを作成("System.Array.FLinq","Count"),
                    ケースを作成("System.Array.ZLinq","Count"),
                ],
                [0,1,2,3],
                [128],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal(6,状態.ジョブ一覧.Count);
        Assert.All(状態.ジョブ一覧,static ジョブ => Assert.Equal(ジョブ状態.完了,ジョブ.状態));
        Assert.Empty(Directory.GetFiles(状態.実行ルート,"*.tmp"));
    }

    [Fact]
    public async Task 仕様確認09() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [128],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal(ベンチマーク実行プロファイル.高速,状態.実行プロファイル);
        Assert.False(File.Exists(Path.Combine(状態.実行ルート,"BenchmarkRunSummary.txt")));
    }

    [Fact]
    public async Task 仕様確認10() {
        var ルート = 一時ルートを作成();
        var 観測 = new List<(string メソッド,string プロジェクト,int 要素数,int CPU)>();
        var 排他 = new object();
        var 実行器 = new 偽実行器((ジョブ,CPU) => {
            lock(排他) {
                観測.Add((ジョブ.メソッド名,ジョブ.プロジェクト,ベンチマーク作業軸.軸数を取得(ジョブ),CPU));
            }

            // 一時較正プローブは実測ループ回数(ここでは2)だけを返し、通常測定とは区別する。
            return ジョブ.開放ループ較正か
                ? 完了結果を作成(ジョブ,60_000_000_000) with { ループ回数=2 }
                : 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    ケースを作成("System.Array.FLinq","Where"),
                    ケースを作成("System.Array.ZLinq","Where"),
                    ケースを作成("System.Array.FLinq","Select"),
                    ケースを作成("System.Array.ZLinq","Select"),
                    ケースを作成("System.Array.FLinq","Count"),
                    ケースを作成("System.Array.ZLinq","Count"),
                ],
                [10,20],
                [1,2],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.正確),
            _ => { },
            _ => { },
            CancellationToken.None);

        //各計測は選択された論理CPU(10/20)のいずれかで実行される。
        Assert.All(観測,static 項目 => Assert.True(項目.CPU is 10 or 20));
        //表示行(=FLinq/ZLinqペア×メソッド)は空きCPUへ分配され、両方のCPUが使われる。
        Assert.Contains(10,観測.Select(static 項目 => 項目.CPU));
        Assert.Contains(20,観測.Select(static 項目 => 項目.CPU));
        foreach(var グループ in 観測.GroupBy(static 項目 => 項目.メソッド)) {
            //1つの表示行は左右プロジェクトを含めて開始から完了まで同じ1コアに固定される。
            Assert.Single(グループ.Select(static 項目 => 項目.CPU).Distinct());
            // 一時較正プローブの後、右列と左列を同じループ回数・通常エンジンで本測定する。
            Assert.Equal(
                [
                    ("System.Array.ZLinq",1),
                    ("System.Array.ZLinq",2),
                    ("System.Array.FLinq",2),
                ],
                グループ.Select(static 項目 => (項目.プロジェクト,項目.要素数)).ToArray());
        }
    }

    [Fact]
    public async Task 仕様確認11() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    ケースを作成("System.Array.FLinq","Where"),
                    ケースを作成("System.Array.ZLinq","Where"),
                    ケースを作成("System.List.FLinq","Where"),
                    ケースを作成("System.List.ZLinq","Where"),
                ],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Empty(Directory.EnumerateFiles(状態.実行ルート,"*.csv",SearchOption.AllDirectories));
        Assert.Contains(状態.ジョブ一覧,static ジョブ => ジョブ.ソースタブ==ベンチマークソースタブ.System配列);
        Assert.Contains(状態.ジョブ一覧,static ジョブ => ジョブ.ソースタブ==ベンチマークソースタブ.Systemリスト);
    }

    [Fact]
    public async Task 仕様確認12() {
        var ルート = 一時ルートを作成();
        var 選択済み = new List<ベンチマークケース> { ケースを作成("System.Array.FLinq","A") };
        var 観測 = new List<string>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.開放ループ較正か) {
                観測.Add(ジョブ.メソッド名);
            }
            if(ジョブ.メソッド名=="A") {
                選択済み=[
                    ケースを作成("System.Array.FLinq","A"),
                    ケースを作成("System.Array.FLinq","B"),
                ];
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                選択済み,
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ベンチマーク取得器: () => 選択済み),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(["A","B"],観測);
    }

    [Fact]
    public async Task 仕様確認13() {
        var ルート = 一時ルートを作成();
        var 選択済み = new List<ベンチマークケース> {
            ケースを作成("System.Array.FLinq","A"),
            ケースを作成("System.Array.FLinq","B"),
        };
        var 観測 = new List<string>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.開放ループ較正か) {
                観測.Add(ジョブ.メソッド名);
            }
            if(ジョブ.メソッド名=="A") {
                選択済み=[ケースを作成("System.Array.FLinq","A")];
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                選択済み,
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                ベンチマーク取得器: () => 選択済み),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(["A"],観測);
    }

    [Fact]
    public async Task 仕様確認14() {
        var ルート = 一時ルートを作成();
        var 観測 = new List<(bool ウォームアップか,string プロジェクト,int 要素数)>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            観測.Add((ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal),ジョブ.プロジェクト,ベンチマーク作業軸.軸数を取得(ジョブ)));
            // 一時較正プローブは実測ループ回数(ここでは2)だけを返す。
            return ジョブ.開放ループ較正か
                ? 完了結果を作成(ジョブ,60_000_000_000) with { ループ回数=2 }
                : 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1,2],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速),
            _ => { },
            _ => { },
            CancellationToken.None);

        // 一時較正プローブの後、右列と左列を同じループ回数でウォームアップ・本測定する。
        Assert.Equal(
            [
                (false,"System.Array.ZLinq",1),
                (true,"System.Array.ZLinq",1),
                (false,"System.Array.ZLinq",2),
                (true,"System.Array.FLinq",1),
                (false,"System.Array.FLinq",2),
            ],
            観測);
        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal(2,状態.ジョブ一覧.Count);
        Assert.DoesNotContain(状態.ジョブ一覧,static ジョブ => ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal));
        Assert.Empty(Directory.EnumerateFiles(状態.実行ルート,"*.csv",SearchOption.AllDirectories));
    }

    [Fact]
    public async Task 仕様確認15() {
        var ルート = 一時ルートを作成();
        var 観測 = new List<bool>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            観測.Add(ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal));
            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.正確),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal([false,false,false],観測);
    }

    [Fact]
    public async Task 仕様確認16() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal)
            ? new ベンチマークジョブ実行結果(
                ジョブ.プロジェクト,
                ジョブ.メソッド名,
                ジョブ.型完全名,
                ベンチマーク作業軸.軸数を取得(ジョブ),
                "Failed",
                "ウォームアップ失敗",
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                1,
                ジョブ.成果物フォルダー,
                "warmup boom")
            : 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1,2],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速),
            _ => { },
            _ => { },
            CancellationToken.None);

        // ウォームアップ失敗は当該側を失敗確定し、同じ比較行の反対側へは進まない。
        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Single(状態.ジョブ一覧);
        Assert.Contains(状態.ジョブ一覧,static ジョブ => ジョブ.プロジェクト=="System.Array.ZLinq");
        Assert.All(状態.ジョブ一覧,static ジョブ => {
            Assert.Equal(1,ベンチマーク作業軸.軸数を取得(ジョブ));
            Assert.Equal(ジョブ状態.失敗,ジョブ.状態);
            Assert.Contains("warmup boom",ジョブ.失敗理由,StringComparison.Ordinal);
        });
        Assert.False(File.Exists(Path.Combine(状態.実行ルート,"System.Array","N1.csv")));
    }

    [Fact]
    public async Task 仕様確認17() {
        var ルート = 一時ルートを作成();
        var CPU一覧 = new[] { 0 };
        var 開始通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 解放通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // 行は CPU ごとに並行して走るため、記録の順番は負荷で入れ替わる。どの行がどの CPU で測られたかを確かめる。
        var 観測 = new List<(string メソッド,int CPU)>();
        var 排他 = new object();
        var 実行器 = new 遅延偽実行器(async (ジョブ,CPU) => {
            if(!ジョブ.開放ループ較正か) {
                lock(排他) {
                    観測.Add((ジョブ.メソッド名,CPU));
                }
            }

            if(ジョブ.メソッド名=="A") {
                開始通知.TrySetResult();
                await 解放通知.Task;
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);
        var 実行処理 = オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","A"),ケースを作成("System.Array.FLinq","B")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => CPU一覧),
            _ => { },
            _ => { },
            CancellationToken.None);

        await 開始通知.Task;
        CPU一覧= [1];
        解放通知.SetResult();
        await 実行処理;

        Assert.Equal([("A",0),("B",1)],観測.OrderBy(static 項目=>項目.メソッド,StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task 仕様確認18() {
        var ルート = 一時ルートを作成();
        var CPU一覧 = new[] { 0,1 };
        var CPU一開始通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var CPU一解放通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 観測 = new List<int>();
        var 排他 = new object();
        var 実行器 = new 遅延偽実行器(async (ジョブ,CPU) => {
            if(!ジョブ.開放ループ較正か) {
                lock(排他) {
                    観測.Add(CPU);
                }
            }

            if(CPU==1) {
                CPU一開始通知.TrySetResult();
                await CPU一解放通知.Task;
            } else {
                await Task.Delay(40);
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);
        var 実行処理 = オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","A"),ケースを作成("System.Array.FLinq","B"),ケースを作成("System.Array.FLinq","C"),ケースを作成("System.Array.FLinq","D")],
                [0,1],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => CPU一覧),
            _ => { },
            _ => { },
            CancellationToken.None);

        await CPU一開始通知.Task;
        CPU一覧= [0];
        CPU一解放通知.SetResult();
        await 実行処理;

        var CPU一実行一覧 = 観測.Count(static CPU => CPU==1);
        Assert.Equal(1,CPU一実行一覧);
        Assert.All(唯一の状態を読み込む(ルート).ジョブ一覧,static ジョブ => Assert.Equal(ジョブ状態.完了,ジョブ.状態));
    }

    [Fact]
    public async Task 仕様確認19() {
        var ルート = 一時ルートを作成();
        var CPU一覧 = new[] { 0,1 };
        var CPU一開始通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var CPU一解放通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 実行器 = new 動的偽実行器(async (ジョブ,CPU) => {
            if(CPU==1) {
                CPU一開始通知.TrySetResult();
                await CPU一解放通知.Task;
            } else {
                await Task.Delay(40);
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);
        var 実行処理 = オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","A"),ケースを作成("System.Array.FLinq","B"),ケースを作成("System.Array.FLinq","C"),ケースを作成("System.Array.FLinq","D")],
                [0,1],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => CPU一覧),
            _ => { },
            _ => { },
            CancellationToken.None);

        await CPU一開始通知.Task;
        CPU一覧= [0];
        CPU一解放通知.SetResult();
        await 実行処理;

        Assert.Contains(実行器.適用済みCPU集合一覧,static 項目 => 項目.SequenceEqual([0]));
    }

    [Fact]
    public async Task 仕様確認20() {
        var ルート = 一時ルートを作成();
        var CPU一覧 = new[] { 0 };
        var 先頭開始通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 先頭解放通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 観測 = new List<int>();
        var 排他 = new object();
        var 実行器 = new 遅延偽実行器(async (ジョブ,CPU) => {
            lock(排他) {
                観測.Add(CPU);
            }

            if(ジョブ.メソッド名=="A") {
                先頭開始通知.TrySetResult();
                await 先頭解放通知.Task;
            } else {
                await Task.Delay(40);
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);
        var 実行処理 = オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","A"),ケースを作成("System.Array.FLinq","B"),ケースを作成("System.Array.FLinq","C"),ケースを作成("System.Array.FLinq","D")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                () => CPU一覧),
            _ => { },
            _ => { },
            CancellationToken.None);

        await 先頭開始通知.Task;
        CPU一覧= [0,1];
        先頭解放通知.SetResult();
        await 実行処理;

        Assert.Contains(1,観測);
        Assert.All(唯一の状態を読み込む(ルート).ジョブ一覧,static ジョブ => Assert.Equal(ジョブ状態.完了,ジョブ.状態));
    }

    [Fact]
    public async Task 仕様確認21() {
        var ルート = 一時ルートを作成();
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [128],
            ジョブ一覧= [
                完了ジョブを作成("System.Array.FLinq","Where",128),
                完了ジョブを作成("System.Array.ZLinq","Where",128),
            ],
        };

        await Task.WhenAll(Enumerable.Range(0,32).Select(_ => Task.Run(() => 状態マネージャー.保存(状態))));

        var 読込結果 = 状態マネージャー.読み込む(実行ルート);
        Assert.NotNull(読込結果);
        Assert.Equal(2,読込結果.ジョブ一覧.Count);
        Assert.Empty(Directory.GetFiles(実行ルート,"*.tmp"));
    }

    [Fact]
    public async Task 仕様確認22() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1],
            適応しきい値ナノ秒=1_000_000,
            適応状態一覧= [
                new 適応ベンチマーク状態 { メソッド名="Where" },
            ],
            ジョブ一覧= [
                new ベンチマークジョブ{
                    ジョブID="system",
                    プロジェクト="System.Array.FLinq",
                    アセンブリ名="Assembly",
                    型完全名="Type",
                    メソッド名="Where",
                    要素数=1,
                    状態=ジョブ状態.保留,
                },
                new ベンチマークジョブ{
                    ジョブID="永続化",
                    プロジェクト="Seamless.Persisted.FLinq",
                    アセンブリ名="Assembly",
                    型完全名="Type",
                    メソッド名="Where",
                    要素数=1,
                    状態=ジョブ状態.保留,
                },
            ],
        };
        var 観測プロジェクト一覧 = new List<string>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.開放ループ較正か) {
                観測プロジェクト一覧.Add(ジョブ.プロジェクト);
            }
            return 完了結果を作成(ジョブ);
        });
        状態マネージャー.保存(状態);
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 読込結果 = 状態マネージャー.読み込む(実行ルート)!;
        Assert.Equal(["System.Array.FLinq"],観測プロジェクト一覧);
        Assert.All(読込結果.ジョブ一覧,static ジョブ => Assert.Equal("System.Array.FLinq",ジョブ.プロジェクト));
    }

    [Fact]
    public async Task 仕様確認23() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 完了ジョブ = 完了ジョブを作成("System.Array.FLinq","Where",1);
        完了ジョブ.平均="10.56 ms";
        完了ジョブ.平均ナノ秒=10_560_000;
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1,2],
            適応しきい値ナノ秒=1_000_000,
            適応状態一覧= [
                new 適応ベンチマーク状態 { メソッド名="Where" },
            ],
            ジョブ一覧= [
                完了ジョブ,
            ],
        };
        var 観測要素数一覧 = new List<int>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal)&&!ジョブ.開放ループ較正か) {
                観測要素数一覧.Add(ベンチマーク作業軸.軸数を取得(ジョブ));
            }
            return 完了結果を作成(ジョブ);
        });
        状態マネージャー.保存(状態);
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1,2],
                TimeSpan.FromMilliseconds(1024),
                TimeSpan.FromSeconds(30),
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 読込結果 = 状態マネージャー.読み込む(実行ルート)!;
        Assert.Equal(1_024_000_000,読込結果.適応しきい値ナノ秒);
        // 一時較正プローブの結果を使って右列を本測定する。
        Assert.Equal([1],観測要素数一覧);
    }

    [Fact]
    public async Task 仕様確認24() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1,2],
            適応しきい値ナノ秒=1_000_000_000,
            適応状態一覧= [
                new 適応ベンチマーク状態 { メソッド名="Where" },
            ],
            ジョブ一覧= [
                完了ジョブを作成("System.Array.FLinq","Where",1),
                完了ジョブを作成("System.Array.FLinq","Where",2),
            ],
        };
        var 観測要素数一覧 = new List<int>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.開放ループ較正か) {
                観測要素数一覧.Add(ベンチマーク作業軸.軸数を取得(ジョブ));
            }
            return 完了結果を作成(ジョブ);
        });
        状態マネージャー.保存(状態);
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1,2],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                要求要素数から再開するか: true,
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal([1],観測要素数一覧);
    }

    [Fact]
    public async Task 仕様確認25() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => ジョブ.開放ループ較正か
            // 一時較正プローブが512回で閾値へ到達したと想定する。
            ? 完了結果を作成(ジョブ,2_000_000_000) with { ループ回数=512 }
            : 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [128,256,512],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        // 一時較正プローブで実測ループ回数(512)を確定し、右列と左列を同じ回数で本測定する。
        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal(
            [512],
            状態.ジョブ一覧.Where(static ジョブ => ジョブ.プロジェクト=="System.Array.FLinq").Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Order().ToArray());
        Assert.Equal(
            [512],
            状態.ジョブ一覧.Where(static ジョブ => ジョブ.プロジェクト=="System.Array.ZLinq").Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Order().ToArray());
    }

    [Fact]
    public async Task 仕様確認26() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => ジョブ.開放ループ較正か
            ? 完了結果を作成(ジョブ,2_000_000) with { ループ回数=256 }
            : 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [128,256,512],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Contains(状態.ジョブ一覧,static ジョブ => ジョブ.プロジェクト=="System.Array.FLinq"&&ベンチマーク作業軸.軸数を取得(ジョブ)==256);
        Assert.Contains(状態.ジョブ一覧,static ジョブ => ジョブ.プロジェクト=="System.Array.ZLinq"&&ベンチマーク作業軸.軸数を取得(ジョブ)==256);
        Assert.DoesNotContain(状態.ジョブ一覧,static ジョブ => ジョブ.プロジェクト=="System.Array.FLinq"&&ベンチマーク作業軸.軸数を取得(ジョブ)==512);
        Assert.DoesNotContain(状態.ジョブ一覧,static ジョブ => ジョブ.プロジェクト=="System.Array.ZLinq"&&ベンチマーク作業軸.軸数を取得(ジョブ)==512);
    }

    [Fact]
    public async Task 仕様確認27() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => ジョブ.開放ループ較正か
            ? 完了結果を作成(ジョブ,2_000_000) with { ループ回数=4 }
            : 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1,2,4,8],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        // 一時較正プローブは状態へ残らず、実測ループ回数(4)の最終ジョブ1件だけが残る。
        Assert.Equal([4],状態.ジョブ一覧.Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Order().ToArray());
        Assert.DoesNotContain(状態.ジョブ一覧,static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)==8);
    }

    [Fact]
    public async Task 仕様確認28() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => ジョブ.開放ループ較正か
            ? 完了結果を作成(ジョブ,2_000_000) with { ループ回数=262144 }
            : 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [65536,131072],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal([262144],状態.ジョブ一覧.Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Order().ToArray());
        Assert.DoesNotContain(262144,状態.要素数一覧);
        Assert.DoesNotContain(状態.ジョブ一覧,static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)==524288);
    }

    [Fact]
    public async Task 仕様確認29() {
        var ルート = 一時ルートを作成();
        var 測定済み一覧 = new List<(string プロジェクト,int ループ回数)>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal)) {
                測定済み一覧.Add((ジョブ.プロジェクト,ベンチマーク作業軸.軸数を取得(ジョブ)));
            }

            // 一時較正プローブは実測ループ回数(4)だけを返し、右列と左列は同じ回数で本測定する。
            return ジョブ.開放ループ較正か
                ? 完了結果を作成(ジョブ,2_000_000) with { ループ回数=4 }
                : 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Any"),ケースを作成("System.Array.ZLinq","Any")],
                [0],
                [1,2],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            [
                ("System.Array.ZLinq",1),
                ("System.Array.ZLinq",4),
                ("System.Array.FLinq",4),
            ],
            測定済み一覧);
        var 最終ジョブ一覧 = 唯一の状態を読み込む(ルート).ジョブ一覧.Where(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)==4).ToArray();
        Assert.Equal(2,最終ジョブ一覧.Length);
        Assert.All(最終ジョブ一覧,static ジョブ => Assert.Equal(ジョブ状態.完了,ジョブ.状態));
    }

    [Fact]
    public async Task 仕様確認30() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.開放ループ較正か) {
                return 完了結果を作成(ジョブ);
            }

            // 正確プロファイルの一時較正プローブはメソッド毎に異なる実測ループ回数を返す。
            var 実測ループ回数 = ジョブ.メソッド名=="A" ? 2 : 8;
            return 完了結果を作成(ジョブ,2_000_000) with { ループ回数=実測ループ回数 };
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    ケースを作成("System.Array.FLinq","A"),ケースを作成("System.Array.ZLinq","A"),
                    ケースを作成("System.Array.FLinq","Any"),ケースを作成("System.Array.ZLinq","Any"),
                ],
                [0,1],
                [1],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.正確),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal([1],状態.要素数一覧);
        // 一時較正プローブで実測ループ回数を確定し、右列と左列を同じ回数で本測定する。
        Assert.Equal(
            [2,2],
            状態.ジョブ一覧.Where(static ジョブ => ジョブ.メソッド名=="A").Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Order().ToArray());
        Assert.Equal(
            [8,8],
            状態.ジョブ一覧.Where(static ジョブ => ジョブ.メソッド名=="Any").Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Order().ToArray());
    }

    [Fact]
    public async Task 仕様確認32() {
        var ルート = 一時ルートを作成();
        // 一時較正プローブでワーカーがエラー("boom")を返した場合、右列の最終ジョブ1件だけが失敗として記録される。
        var 実行器 = new 偽実行器((ジョブ,_) => new ベンチマークジョブ実行結果(
            ジョブ.プロジェクト,
            ジョブ.メソッド名,
            ジョブ.型完全名,
            ベンチマーク作業軸.軸数を取得(ジョブ),
            "Failed",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            1,
            ジョブ.成果物フォルダー,
            "boom"));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [65536,131072],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        var ジョブ = Assert.Single(状態.ジョブ一覧);
        Assert.Equal(ジョブ状態.失敗,ジョブ.状態);
        Assert.Contains("boom",ジョブ.失敗理由,StringComparison.Ordinal);
    }

    [Fact]
    public async Task 仕様確認33() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            var 平均 = 2_000_000d;
            return new ベンチマークジョブ実行結果(
                ジョブ.プロジェクト,
                ジョブ.メソッド名,
                ジョブ.型完全名,
                ベンチマーク作業軸.軸数を取得(ジョブ),
                "Completed",
                平均+" ns",
                string.Empty,
                string.Empty,
                平均,
                0,
                0,
                0,
                0,
                ジョブ.成果物フォルダー,
                string.Empty);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    Countケースを作成("Scalable.Persisted.Set","Add",1024),
                    Countケースを作成("Scalable.Persisted.Set","Add",2048),
                    Countケースを作成("Scalable.Persisted.Set","Add",4096),
                ],
                [0],
                [1024,2048,4096],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30),
                ソースタブ: ベンチマークソースタブ.スケーラブル永続化セット),
            _ => { },
            _ => { },
            CancellationToken.None);

        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal([1024],状態.ジョブ一覧.Select(static ジョブ => ベンチマーク作業軸.軸数を取得(ジョブ)).Distinct().Order().ToArray());
        Assert.All(状態.ジョブ一覧,static ジョブ => Assert.Equal(ジョブ状態.完了,ジョブ.状態));
    }

    [Fact]
    public async Task 仕様確認34() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => new ベンチマークジョブ実行結果(
            ジョブ.プロジェクト,
            ジョブ.メソッド名,
            ジョブ.型完全名,
            ベンチマーク作業軸.軸数を取得(ジョブ),
            "Completed",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            0,
            ジョブ.成果物フォルダー,
            string.Empty));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where")],
                [0],
                [1],
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(30)),
            _ => { },
            _ => { },
            CancellationToken.None);

        var ジョブ = Assert.Single(唯一の状態を読み込む(ルート).ジョブ一覧);
        Assert.Equal(ジョブ状態.失敗,ジョブ.状態);
        Assert.Contains("ベンチマーク統計がありません",ジョブ.失敗理由,StringComparison.Ordinal);
    }

    [Fact]
    public void 仕様確認35() {
        var 候補一覧 = ベンチマーク要素数ロジック.適応候補を作成(ベンチマーク要素数一覧.全て,ベンチマーク要素数一覧.基本要素数);

        Assert.Equal([ベンチマーク作業軸.既定ループ回数],候補一覧);
    }

    [Fact]
    public void 仕様確認36() {
        Assert.Equal([ベンチマーク作業軸.既定ループ回数],ベンチマーク要素数一覧.全て);
        Assert.Equal([ベンチマーク作業軸.既定ループ回数],ベンチマーク要素数一覧.既定初期値);
        Assert.Equal(ベンチマーク作業軸.標準要素数,ベンチマーク要素数一覧.基本要素数);
        Assert.Equal(ベンチマーク作業軸.既定ループ回数,固定ベンチマーク軸数);
        Assert.Equal(ベンチマーク作業軸.標準要素数,ベンチマーク作業軸.要素数を解決(ベンチマークソースタブ.System配列,1));
        Assert.Equal(4,ベンチマーク作業軸.ループ回数を解決(ベンチマークソースタブ.System配列,4));
        Assert.Equal([ベンチマーク作業軸.標準要素数],ベンチマーク作業軸.準備済み要素数を解決(ベンチマークソースタブ.System配列,[1,2,4]));
    }

    [Fact]
    public void 仕様確認37() {
        var システムタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 永続化タブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        ベンチマーク要素測定[] 測定一覧 = [
            new(システムタブ.左プロジェクト,"Where","","",128,"Completed",2_000_000,2_000_000),
            new(システムタブ.右プロジェクト,"Where","","",128,"Completed",900_000,900_000),
            new(システムタブ.左プロジェクト,"Where","","",256,"Completed",900_000,900_000),
            new(システムタブ.右プロジェクト,"Where","","",256,"Completed",2_000_000,2_000_000),
            new(永続化タブ.左プロジェクト,"Where","","",128,"Completed",2_000_000,2_000_000),
        ];

        var 停止日時 = ベンチマークスキップ表示ロジック.ペア停止要素数を検索(
            測定一覧,
            "Where",
            システムタブ.左プロジェクト,
            512,
            1_000_000);
        var 片側 = ベンチマークスキップ表示ロジック.ペア停止要素数を検索(
            測定一覧,
            "Where",
            永続化タブ.左プロジェクト,
            512,
            1_000_000);

        Assert.Equal(128,停止日時);
        Assert.Null(片側);
    }

    [Fact]
    public void 仕様確認38() {
        ベンチマーク要素測定[] 測定一覧 = [
            new("Left","Any","System.Array.FLinq","",128,"Completed",900_000,900_000),
            new("Right","Any","System.Array.ZLinq","",128,"Completed",2_000_000,2_000_000),
        ];
        var 左セル = new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1");
        var 右セル = new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1");

        var 停止日時 = ベンチマークスキップ表示ロジック.ペア停止要素数を検索(
            測定一覧,
            左セル,
            右セル,
            256,
            1_000_000);
        var 右で完了 = ベンチマークペア完了ロジック.選択ペアが閾値完了済みか(
            測定一覧,
            [左セル,右セル],
            左セル,
            "Left",
            "Right",
            1_000_000);

        Assert.Equal(128,停止日時);
        Assert.True(右で完了);
    }

    [Fact]
    public void 仕様確認39() {
        var システムタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        const int 選択軸数 = 実行中軸数+1;
        ベンチマーク要素測定[] 測定一覧 = [
            new(システムタブ.左プロジェクト,"AggregateBy_keySelector",前回完了軸数,"Completed",高速左平均ナノ秒),
            new(システムタブ.右プロジェクト,"AggregateBy_keySelector",前回完了軸数,"Completed",低速右平均ナノ秒),
            new(システムタブ.左プロジェクト,"AggregateBy_keySelector",実行中軸数,"Completed",基準平均ナノ秒),
            new(システムタブ.右プロジェクト,"AggregateBy_keySelector",実行中軸数,"Running",基準平均ナノ秒),
            new(システムタブ.左プロジェクト,"AggregateBy_keySelector",固定ベンチマーク軸数,"Completed",今回左平均ナノ秒),
            new(システムタブ.右プロジェクト,"AggregateBy_keySelector",固定ベンチマーク軸数,"Completed",今回右平均ナノ秒),
        ];

        var 停止日時 = ベンチマークスキップ表示ロジック.直前の完了ペア要素数を検索(
            測定一覧,
            "AggregateBy_keySelector",
            システムタブ.左プロジェクト,
            システムタブ.右プロジェクト,
            選択軸数);
        var 欠測ペア = ベンチマークスキップ表示ロジック.直前の完了ペア要素数を検索(
            測定一覧,
            "Aggregate_func",
            システムタブ.左プロジェクト,
            システムタブ.右プロジェクト,
            選択軸数);

        Assert.Equal(前回完了軸数,停止日時);
        Assert.Null(欠測ペア);
    }

    [Fact]
    public async Task 仕様確認40() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 較正ジョブ = 完了ジョブを作成("System.Array.ZLinq","Where",1);
        較正ジョブ.ループ回数=500_000;
        較正ジョブ.平均ナノ秒=2_000_000_000;
        var 同期ジョブ = 完了ジョブを作成("System.Array.FLinq","Where",1);
        同期ジョブ.ループ回数=500_000;
        同期ジョブ.平均ナノ秒=2_000_000_000;
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1],
            適応しきい値ナノ秒=1_000_000_000,
            適応状態一覧= [new 適応ベンチマーク状態 { メソッド名="Where" }],
            ジョブ一覧= [較正ジョブ,同期ジョブ],
        };
        状態マネージャー.保存(状態);
        var 実行回数 = 0;
        var 実行器 = new 偽実行器((ジョブ,_) => {
            実行回数++;
            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速,
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        // 既に閾値を満たして完了している較正ジョブ・同期ジョブは、軸数(ループ回数)ではなく
        // ケース識別で再利用され、二重に作られたり再計測されたりしない。
        Assert.Equal(0,実行回数);
        var 読込結果 = 状態マネージャー.読み込む(実行ルート)!;
        Assert.Equal(2,読込結果.ジョブ一覧.Count);
        Assert.All(読込結果.ジョブ一覧,static ジョブ => Assert.Equal(500_000,ジョブ.ループ回数));
    }

    [Fact]
    public async Task 仕様確認41() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 較正ジョブ = 完了ジョブを作成("System.Array.ZLinq","Where",1);
        較正ジョブ.ループ回数=1_000; // 旧実測値。現在の閾値には未達。
        較正ジョブ.平均ナノ秒=100;
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1],
            適応しきい値ナノ秒=1_000_000_000,
            適応状態一覧= [new 適応ベンチマーク状態 { メソッド名="Where" }],
            ジョブ一覧= [較正ジョブ],
        };
        状態マネージャー.保存(状態);
        var 実行器 = new 偽実行器((ジョブ,_) => ジョブ.開放ループ較正か
            ? 完了結果を作成(ジョブ,2_000_000_000) with { ループ回数=999_000 }
            : 完了結果を作成(ジョブ));
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速,
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        // 閾値未達の旧ジョブは新規作成ではなく同じジョブが再計測され、新しいループ回数へ更新される。
        var 読込結果 = 状態マネージャー.読み込む(実行ルート)!;
        Assert.Equal(
            ["System.Array.FLinq","System.Array.ZLinq"],
            読込結果.ジョブ一覧.Select(static ジョブ => ジョブ.プロジェクト).Order().ToArray());
        Assert.All(読込結果.ジョブ一覧,static ジョブ => Assert.Equal(999_000,ジョブ.ループ回数));
    }

    [Fact]
    public async Task 仕様確認42() {
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 較正ジョブ = 完了ジョブを作成("System.Array.ZLinq","Where",1);
        較正ジョブ.ループ回数=300_000;
        較正ジョブ.平均ナノ秒=2_000_000_000; // 現在の閾値を既に満たしている。
        var 同期ジョブ = 完了ジョブを作成("System.Array.FLinq","Where",1);
        同期ジョブ.ループ回数=250_000; // 較正ジョブとループ回数が食い違っている(過去のパス由来)。
        同期ジョブ.平均ナノ秒=1_600_000_000;
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1],
            適応しきい値ナノ秒=1_000_000_000,
            適応状態一覧= [new 適応ベンチマーク状態 { メソッド名="Where" }],
            ジョブ一覧= [較正ジョブ,同期ジョブ],
        };
        状態マネージャー.保存(状態);
        var 再計測されたプロジェクト一覧 = new List<string>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            if(!ジョブ.ジョブID.EndsWith("|warmup",StringComparison.Ordinal)) {
                再計測されたプロジェクト一覧.Add(ジョブ.プロジェクト);
            }

            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速,
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        // 較正ジョブは既に閾値を満たしているため再計測されず、ループ回数が食い違っていた
        // 同期ジョブだけが較正ジョブと同じループ回数で測り直される。
        Assert.Equal(["System.Array.FLinq"],再計測されたプロジェクト一覧);
        var 読込結果 = 状態マネージャー.読み込む(実行ルート)!;
        Assert.Equal(2,読込結果.ジョブ一覧.Count);
        Assert.All(読込結果.ジョブ一覧,static ジョブ => Assert.Equal(300_000,ジョブ.ループ回数));
    }

    [Fact]
    public async Task 仕様確認43() {
        // テストプロファイル(倍々探索)で、過去パスの較正が別の軸数に確定していた際に残った
        // 古い軸数の同期ジョブが、今回の確定軸数で破棄・測り直されることを検証する。
        var ルート = 一時ルートを作成();
        var 状態マネージャー = new ベンチマーク状態マネージャー(ルート);
        var 実行ルート = Path.Combine(ルート,"実行処理");
        var 較正ジョブ = 完了ジョブを作成("System.Array.ZLinq","Where",1);
        較正ジョブ.平均ナノ秒=2_000_000_000; // 軸数1で既に閾値到達済み。
        var 旧同期ジョブ = 完了ジョブを作成("System.Array.FLinq","Where",8); // 過去パスの確定軸数8で完了した残留ジョブ。
        var 状態 = new 進行状態 {
            実行ID="実行処理",
            実行ルート=実行ルート,
            作成日時=DateTimeOffset.UtcNow,
            更新日時=DateTimeOffset.UtcNow,
            要素数一覧= [1],
            適応しきい値ナノ秒=1_000_000_000,
            適応状態一覧= [new 適応ベンチマーク状態 { メソッド名="Where" }],
            ジョブ一覧= [較正ジョブ,旧同期ジョブ],
        };
        状態マネージャー.保存(状態);
        var 実行済みプロジェクト一覧 = new List<string>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            実行済みプロジェクト一覧.Add(ジョブ.プロジェクト);
            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(状態マネージャー,実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.テスト,
                実行ルート上書き: 実行ルート),
            _ => { },
            _ => { },
            CancellationToken.None);

        // 同期ケースは確定軸数(1)で測り直され、旧軸数(8)のジョブは破棄される。
        Assert.Equal(["System.Array.FLinq"],実行済みプロジェクト一覧);
        var 読込結果 = 状態マネージャー.読み込む(実行ルート)!;
        Assert.Equal(2,読込結果.ジョブ一覧.Count);
        Assert.All(読込結果.ジョブ一覧,static ジョブ => Assert.Equal(1,ベンチマーク作業軸.軸数を取得(ジョブ)));
    }

    [Fact]
    public async Task タブ境界では残り1件と次タブ7件を8CPUで同時実行する() {
        var ルート = 一時ルートを作成();
        var 開始通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 解放通知 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var 排他 = new object();
        var 同時実行タブ一覧 = new List<ベンチマークソースタブ>();
        var 実行器 = new 遅延偽実行器(async (ジョブ,_) => {
            lock(排他) {
                同時実行タブ一覧.Add(ジョブ.ソースタブ);
                if(同時実行タブ一覧.Count==8) {
                    開始通知.TrySetResult();
                }
            }

            await 解放通知.Task;
            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);
        var 実行タスク = オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [
                    ケースを作成("System.Array.FLinq","ArrayLast"),
                    ケースを作成("System.List.FLinq","List01"),
                    ケースを作成("System.List.FLinq","List02"),
                    ケースを作成("System.List.FLinq","List03"),
                    ケースを作成("System.List.FLinq","List04"),
                    ケースを作成("System.List.FLinq","List05"),
                    ケースを作成("System.List.FLinq","List06"),
                    ケースを作成("System.List.FLinq","List07"),
                ],
                [0,1,2,3,4,5,6,7],
                [1],
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.高速),
            _ => { },
            _ => { },
            CancellationToken.None);
        try {
            await 開始通知.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ベンチマークソースタブ[] 観測;
            lock(排他) {
                観測=同時実行タブ一覧.ToArray();
            }

            Assert.Equal(1,観測.Count(static タブ => タブ==ベンチマークソースタブ.System配列));
            Assert.Equal(7,観測.Count(static タブ => タブ==ベンチマークソースタブ.Systemリスト));
        } finally {
            解放通知.TrySetResult();
        }

        await 実行タスク;
    }

    [Fact]
    public async Task 開放ループ較正は一時プローブに限定し右列の本測定が目標時間へ達するまで倍増する() {
        var ルート = 一時ルートを作成();
        var 実行一覧 = new List<(string プロジェクト,int ループ回数,bool 較正か)>();
        var 通知一覧 = new List<(string プロジェクト,int ループ回数,bool 較正か)>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            実行一覧.Add((ジョブ.プロジェクト,ジョブ.ループ回数,ジョブ.開放ループ較正か));
            if(ジョブ.開放ループ較正か) {
                return 完了結果を作成(ジョブ,0) with {
                    平均ナノ秒=null,
                    誤差ナノ秒=null,
                    標準偏差ナノ秒=null,
                    割り当てバイト数=null,
                    ループ回数=4,
                };
            }
            if(ジョブ.プロジェクト=="System.Array.ZLinq"&&ジョブ.ループ回数==4) {
                return 完了結果を作成(ジョブ,500);
            }

            return 完了結果を作成(ジョブ,1_500);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],
                [1],
                TimeSpan.FromTicks(10),
                TimeSpan.FromSeconds(30),
                実行プロファイル: ベンチマーク実行プロファイル.正確),
            更新 => 通知一覧.Add((更新.ジョブ.プロジェクト,更新.ジョブ.ループ回数,更新.ジョブ.開放ループ較正か)),
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            [
                ("System.Array.ZLinq",1,true),
                ("System.Array.ZLinq",4,false),
                ("System.Array.ZLinq",8,false),
                ("System.Array.FLinq",8,false),
            ],
            実行一覧);
        Assert.Equal(6,通知一覧.Count);
        Assert.DoesNotContain(通知一覧,static 通知 => 通知.較正か);
        var 状態 = 唯一の状態を読み込む(ルート);
        Assert.Equal(2,状態.ジョブ一覧.Count);
        Assert.All(状態.ジョブ一覧,static ジョブ => {
            Assert.Equal(8,ジョブ.ループ回数);
            Assert.False(ジョブ.開放ループ較正か);
            Assert.Null(ジョブ.較正閾値ナノ秒);
        });
        Assert.Equal(1_500,状態.ジョブ一覧.Single(static ジョブ => ジョブ.プロジェクト=="System.Array.ZLinq").平均ナノ秒);
    }

    [Fact]
    public async Task 開放ループ較正失敗は右列の最終ジョブへ記録する() {
        var ルート = 一時ルートを作成();
        var 実行器 = new 偽実行器((ジョブ,_) => 完了結果を作成(ジョブ) with {
            状態="Failed",
            平均ナノ秒=null,
            ループ回数=0,
            ログ="probe failed",
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],[1],TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(30)),
            _ => { },_ => { },CancellationToken.None);

        var ジョブ = Assert.Single(唯一の状態を読み込む(ルート).ジョブ一覧);
        Assert.Equal("System.Array.ZLinq",ジョブ.プロジェクト);
        Assert.Equal(ジョブ状態.失敗,ジョブ.状態);
        Assert.False(ジョブ.開放ループ較正か);
        Assert.Contains("probe failed",ジョブ.失敗理由,StringComparison.Ordinal);
    }

    [Fact]
    public async Task 最大ループ回数の右列本測定が目標時間未満なら到達不能として終了する() {
        var ルート = 一時ルートを作成();
        var 実行一覧 = new List<(int ループ回数,bool 較正か)>();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            実行一覧.Add((ジョブ.ループ回数,ジョブ.開放ループ較正か));
            if(ジョブ.開放ループ較正か) {
                return 完了結果を作成(ジョブ,0) with { 平均ナノ秒=null,ループ回数=int.MaxValue };
            }

            return 完了結果を作成(ジョブ,500);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],[1],TimeSpan.FromTicks(10),TimeSpan.FromSeconds(30)),
            _ => { },_ => { },CancellationToken.None);

        Assert.Equal([(1,true),(int.MaxValue,false)],実行一覧);
        var ジョブ = Assert.Single(唯一の状態を読み込む(ルート).ジョブ一覧);
        Assert.Equal(ジョブ状態.失敗,ジョブ.状態);
        Assert.Equal(int.MaxValue,ジョブ.ループ回数);
        Assert.Contains("int.MaxValue",ジョブ.失敗理由,StringComparison.Ordinal);
    }

    [Fact]
    public async Task 開放ループ較正のキャンセルでは一時プローブを状態へ残さない() {
        var ルート = 一時ルートを作成();
        using var 取消元 = new CancellationTokenSource();
        var 実行器 = new 偽実行器((ジョブ,_) => {
            Assert.True(ジョブ.開放ループ較正か);
            取消元.Cancel();
            取消元.Token.ThrowIfCancellationRequested();
            return 完了結果を作成(ジョブ);
        });
        var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new ベンチマーク状態マネージャー(ルート),実行器);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => オーケストレーター.実行Async(
            new ベンチマーク実行要求(
                [ケースを作成("System.Array.FLinq","Where"),ケースを作成("System.Array.ZLinq","Where")],
                [0],[1],TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(30)),
            _ => { },_ => { },取消元.Token));

        Assert.Empty(唯一の状態を読み込む(ルート).ジョブ一覧);
    }

    private static ベンチマークケース ケースを作成(string プロジェクト,string メソッド) =>
        new(プロジェクト,アセンブリ名を取得(プロジェクト),"Type",メソッド);

    private static ベンチマークケース Countケースを作成(string プロジェクト,string メソッド,int Count) {
        var 引数一覧 = new ベンチマーク引数[] {
            new("Count",Count.ToString(global::System.Globalization.CultureInfo.InvariantCulture)),
        };
        return new ベンチマークケース(
            プロジェクト,
            アセンブリ名を取得(プロジェクト),
            "Type",
            メソッド,
            ベンチマーク引数フォーマッター.キーを作成(引数一覧),
            引数一覧);
    }

    private static string アセンブリ名を取得(string プロジェクト) {
        var ソースタブ = ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)
            ??throw new InvalidDataException($"テスト用ソースタブを一意に解決できませんでした。プロジェクト={プロジェクト}");
        return ソースタブ.アセンブリ名を取得(プロジェクト);
    }

    private static ベンチマークジョブ 完了ジョブを作成(string プロジェクト,string メソッド,int 要素数) =>
        new() {
            ジョブID=プロジェクト+"|"+メソッド+"|"+要素数,
            プロジェクト=プロジェクト,
            アセンブリ名=アセンブリ名を取得(プロジェクト),
            型完全名="Type",
            メソッド名=メソッド,
            ソースタブ=global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ??global::UI.ベンチマークソースタブ.System配列,
            要素数=global::UI.ベンチマーク作業軸.要素数を解決(global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ??global::UI.ベンチマークソースタブ.System配列,要素数),
            ループ回数=global::UI.ベンチマーク作業軸.ループ回数を解決(global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ??global::UI.ベンチマークソースタブ.System配列,要素数),
            状態=ジョブ状態.完了,
            平均="100 ns",
            平均ナノ秒=100,
            割り当てバイト数=0,
            終了コード=0,
            成果物フォルダー=string.Empty,
            開始日時=DateTimeOffset.UtcNow,
            終了日時=DateTimeOffset.UtcNow,
        };

    private static ベンチマークジョブ実行結果 完了結果を作成(ベンチマークジョブ ジョブ,double 合計ナノ秒 = 60_000_000_000) =>
        new(
            ジョブ.プロジェクト,
            ジョブ.メソッド名,
            ジョブ.型完全名,
            ベンチマーク作業軸.軸数を取得(ジョブ),
            "Completed",
            合計ナノ秒+" ns",
            string.Empty,
            string.Empty,
            合計ナノ秒,
            0,
            0,
            0,
            0,
            ジョブ.成果物フォルダー,
            string.Empty);

    private static string 一時ルートを作成() {
        var ルート = Path.Combine(AppContext.BaseDirectory,"BenchmarkOrchestratorTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return ルート;
    }

    private static 進行状態 唯一の状態を読み込む(string ルート) {
        var 実行ルート = Directory.GetDirectories(ルート).Single();
        return new ベンチマーク状態マネージャー(ルート).読み込む(実行ルート)!;
    }

    private sealed class 偽実行器(Func<ベンチマークジョブ,int,ベンチマークジョブ実行結果> 実行処理):Iベンチマークジョブ実行器 {
        public Task<ベンチマークジョブ実行結果> 実行Async(
            ベンチマークジョブ ジョブ,
            string 実行ルート,
            int 論理CPU,
            TimeSpan タイムアウト,
            CancellationToken キャンセルトークン) {
            ジョブ.成果物フォルダー=Path.Combine(実行ルート,"artifacts",ジョブ.ジョブID);
            return Task.FromResult(較正結果を補完(ジョブ,実行処理(ジョブ,論理CPU)));
        }
    }

    private sealed class 遅延偽実行器(Func<ベンチマークジョブ,int,Task<ベンチマークジョブ実行結果>> 実行処理):Iベンチマークジョブ実行器 {
        public async Task<ベンチマークジョブ実行結果> 実行Async(
            ベンチマークジョブ ジョブ,
            string 実行ルート,
            int 論理CPU,
            TimeSpan タイムアウト,
            CancellationToken キャンセルトークン) {
            ジョブ.成果物フォルダー=Path.Combine(実行ルート,"artifacts",ジョブ.ジョブID);
            return 較正結果を補完(ジョブ,await 実行処理(ジョブ,論理CPU));
        }
    }

    private sealed class 動的偽実行器(Func<ベンチマークジョブ,int,Task<ベンチマークジョブ実行結果>> 実行処理):Iベンチマークジョブ実行器, Iベンチマーク動的CPUセット実行器 {
        private readonly object 排他 = new();

        public List<int[]> 適用済みCPU集合一覧 { get; } = [];

        public Task CPUセットを適用Async(IReadOnlyList<int> 論理CPU一覧) {
            lock(this.排他) {
                this.適用済みCPU集合一覧.Add(論理CPU一覧.ToArray());
            }
            return Task.CompletedTask;
        }

        public async Task<ベンチマークジョブ実行結果> 実行Async(
            ベンチマークジョブ ジョブ,
            string 実行ルート,
            int 論理CPU,
            TimeSpan タイムアウト,
            CancellationToken キャンセルトークン) {
            ジョブ.成果物フォルダー=Path.Combine(実行ルート,"artifacts",ジョブ.ジョブID);
            return 較正結果を補完(ジョブ,await 実行処理(ジョブ,論理CPU));
        }
    }

    private static ベンチマークジョブ実行結果 較正結果を補完(ベンチマークジョブ ジョブ,ベンチマークジョブ実行結果 結果) {
        if(ジョブ.開放ループ較正か
            &&ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了)
            &&結果.ループ回数<=0) {
            return 結果 with { ループ回数=ジョブ.ループ回数 };
        }

        return 結果;
    }
}
