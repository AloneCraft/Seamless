namespace Test.Benchmark;

public sealed class ベンチマーク作業軸テスト {
    [Fact]
    public void 仕様確認01() {
        Assert.Equal(4_096,global::UI.ベンチマーク作業軸.要素数を解決(global::UI.ベンチマークソースタブ.System配列,1));
        Assert.Equal(4,global::UI.ベンチマーク作業軸.ループ回数を解決(global::UI.ベンチマークソースタブ.System配列,4));
        Assert.Equal([4_096],global::UI.ベンチマーク作業軸.準備済み要素数を解決(global::UI.ベンチマークソースタブ.System配列,[1,2,4]));
    }

    [Fact]
    public async Task 仕様確認02() {
        var ルート = Path.Combine(Path.GetTempPath(),"SeamlessLoopAxis",Guid.NewGuid().ToString("N"));
        try {
            var タブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
            var 実行器 = new 校正済み実行器(タブ.右プロジェクト,332_000,114_000,4);
            var オーケストレーター = new global::UI.ベンチマークオーケストレーター(new global::UI.ベンチマーク状態マネージャー(ルート),実行器);
            var 結果 = await オーケストレーター.実行Async(
                new global::UI.ベンチマーク実行要求(
                    [
                        new global::UI.ベンチマークケース(タブ.左プロジェクト,タブ.左アセンブリ名,タブ.左型接頭辞+".Sample","ToHashSet"),
                        new global::UI.ベンチマークケース(タブ.右プロジェクト,タブ.右アセンブリ名,タブ.右型接頭辞+".Sample","ToHashSet"),
                    ],
                    [0],
                    [1],
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromSeconds(5),
                    ソースタブ: global::UI.ベンチマークソースタブ.System配列),
                _ => { },
                _ => { },
                CancellationToken.None);

            // 右列(較正対象)は単発の開放ループ較正でワーカー実測のループ回数(4)を確定し、左列はその値でだけ測定する。
            // 旧倍々探索の中間ティア(1,2)は発生しない。要素数はループ回数タブなので常に4096へ解決される。
            Assert.Equal([4],結果.状態.ジョブ一覧.Where(ジョブ => ジョブ.プロジェクト==タブ.右プロジェクト).Select(ジョブ => ジョブ.ループ回数).Order().ToArray());
            Assert.Equal([4],結果.状態.ジョブ一覧.Where(ジョブ => ジョブ.プロジェクト==タブ.左プロジェクト).Select(ジョブ => ジョブ.ループ回数).ToArray());
            Assert.All(結果.状態.ジョブ一覧,ジョブ => {
                Assert.Equal(4_096,ジョブ.要素数);
                Assert.Equal([4_096],ジョブ.準備済み要素数一覧);
            });
        } finally {
            if(Directory.Exists(ルート)) {
                Directory.Delete(ルート,true);
            }
        }
    }

    private sealed class 校正済み実行器(string 較正プロジェクト,double 較正ナノ秒,double 同期ナノ秒,int 実測ループ回数):global::UI.Iベンチマークジョブ実行器 {
        public Task<global::UI.ベンチマークジョブ実行結果> 実行Async(global::UI.ベンチマークジョブ ジョブ,string 実行ルート,int 論理CPU,TimeSpan タイムアウト,CancellationToken キャンセルトークン) {
            double 呼出単位;
            if(ジョブ.プロジェクト==較正プロジェクト){
                呼出単位=較正ナノ秒;
            }else{
                呼出単位=同期ナノ秒;
            }

            // 開放ループ較正は単発プローブなので、ワーカーが実測したループ回数を結果に積んで返す。
            int ループ回数;
            if(ジョブ.開放ループ較正か){
                ループ回数=実測ループ回数;
            }else{
                ループ回数=ジョブ.ループ回数;
            }

            return Task.FromResult(new global::UI.ベンチマークジョブ実行結果(
                ジョブ.プロジェクト,
                ジョブ.メソッド名,
                ジョブ.型完全名,
                ジョブ.要素数,
                "Completed",
                string.Empty,
                string.Empty,
                string.Empty,
                呼出単位*ループ回数,
                null,
                null,
                0,
                0,
                実行ルート,
                string.Empty,
                ループ回数: ループ回数));
        }
    }
}
