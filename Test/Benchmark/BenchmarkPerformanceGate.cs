namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク性能ゲートロジック{
    [Fact,Trait("Category","Unit")]
    public void 検証して解決(){
        {
            var マニフェスト=マニフェストを作成();
            var カタログ=カタログを作成(マニフェスト);

            var 解決一覧=global::UI.ベンチマーク性能ゲートロジック.検証して解決(マニフェスト,カタログ);

            Assert.Equal(11,解決一覧.Count);
            Assert.Equal(マニフェスト.対象一覧.Select(static 対象=>対象.ID),解決一覧.Select(static 対象=>対象.定義.ID));
        }
        {
            var 元=マニフェストを作成();
            var 重複対象一覧=元.対象一覧.ToArray();
            重複対象一覧[10]=重複対象一覧[0];
            var マニフェスト=元 with{対象一覧=重複対象一覧};

            var 例外=Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク性能ゲートロジック.検証して解決(マニフェスト,[]));

            Assert.Contains("重複",例外.Message,StringComparison.Ordinal);
        }
        {
            var 元=マニフェストを作成();
            var 重複対象一覧=元.対象一覧.ToArray();
            重複対象一覧[10]=重複対象一覧[0] with{ID="another-id"};
            var マニフェスト=元 with{対象一覧=重複対象一覧};

            var 例外=Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク性能ゲートロジック.検証して解決(マニフェスト,[]));

            Assert.Contains("対象ペア",例外.Message,StringComparison.Ordinal);
        }
        {
            var 元=マニフェストを作成();
            var 重複対象一覧=元.対象一覧.ToArray();
            var カタログ=カタログを作成(元).ToList();
            var タブ=global::UI.ベンチマークソースタブ一覧.IDから取得を試みる(重複対象一覧[10].タブID)!;
            カタログ.Add(new global::UI.ベンチマークケース(
                タブ.右プロジェクト,タブ.右アセンブリ名,重複対象一覧[10].右型完全名,重複対象一覧[0].メソッド名,重複対象一覧[10].引数キー,[]));
            重複対象一覧[10]=重複対象一覧[10] with{左型完全名=重複対象一覧[0].左型完全名,メソッド名=重複対象一覧[0].メソッド名};
            var マニフェスト=元 with{対象一覧=重複対象一覧};

            var 例外=Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク性能ゲートロジック.検証して解決(マニフェスト,カタログ));

            Assert.Contains("22ケース",例外.Message,StringComparison.Ordinal);
        }
        {
            var マニフェスト=マニフェストを作成() with{比較元実行ID=248};

            var 例外=Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク性能ゲートロジック.検証して解決(マニフェスト,[]));

            Assert.Contains("Run 249",例外.Message,StringComparison.Ordinal);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 判定(){
        {
            var マニフェスト=マニフェストを作成();
            var 測定一覧=合格測定一覧を作成(マニフェスト);

            var 判定=global::UI.ベンチマーク性能ゲートロジック.判定(マニフェスト,測定一覧);

            Assert.True(判定.合格か);
            Assert.All(判定.対象判定一覧,static 対象=>{
                Assert.Equal(0.8d,対象.比率中央値);
                Assert.Equal(4d/9d,対象.最終シェア);
            });
        }
        {
            var マニフェスト=マニフェストを作成();
            var 測定一覧=合格測定一覧を作成(マニフェスト).ToArray();
            測定一覧[0]=測定一覧[0] with{
                FLinq結果ダイジェスト="different",
                FLinq論理CPU=3,
                FLinqループ回数=7,
                FLinq割当バイト=101,
                FLinqアフィニティ適用成功か=false,
            };

            var 判定=global::UI.ベンチマーク性能ゲートロジック.判定(マニフェスト,測定一覧);

            Assert.False(判定.合格か);
            var 失敗=判定.対象判定一覧[0];
            Assert.Contains(失敗.失敗理由一覧,static 理由=>理由.Contains("ダイジェスト",StringComparison.Ordinal));
            Assert.Contains(失敗.失敗理由一覧,static 理由=>理由.Contains("論理CPU",StringComparison.Ordinal));
            Assert.Contains(失敗.失敗理由一覧,static 理由=>理由.Contains("LoopCount",StringComparison.Ordinal));
            Assert.Contains(失敗.失敗理由一覧,static 理由=>理由.Contains("割当量",StringComparison.Ordinal));
            Assert.Contains(失敗.失敗理由一覧,static 理由=>理由.Contains("アフィニティ",StringComparison.Ordinal));
        }
        {
            var マニフェスト=マニフェストを作成();
            var 測定一覧=合格測定一覧を作成(マニフェスト).Skip(1).ToArray();

            var 判定=global::UI.ベンチマーク性能ゲートロジック.判定(マニフェスト,測定一覧);

            Assert.False(判定.合格か);
            Assert.Contains(判定.対象判定一覧[0].失敗理由一覧,static 理由=>理由.Contains("重複なく",StringComparison.Ordinal));
        }
        {
            var マニフェスト=マニフェストを作成() with{比較元実行ID=250};

            var 例外=Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク性能ゲートロジック.判定(マニフェスト,[]));

            Assert.Contains("Run 249",例外.Message,StringComparison.Ordinal);
        }
    }

    private static global::UI.ベンチマーク性能ゲートマニフェスト マニフェストを作成(){
        var タブID=global::UI.ベンチマークソースタブ一覧.System配列ID;
        var 対象一覧=global::System.Linq.Enumerable.Range(1,11)
            .Select(番号=>new global::UI.ベンチマーク性能ゲート対象(
                "target-"+番号,
                タブID,
                "Type"+番号,
                "Method"+番号,
                string.Empty,
                "Left.Type"+番号,
                "Right.Type"+番号,
                100))
            .ToArray();
        return new(1,249,4096,1024d,5,対象一覧);
    }

    private static IReadOnlyList<global::UI.ベンチマークケース>カタログを作成(global::UI.ベンチマーク性能ゲートマニフェスト マニフェスト)=>
        マニフェスト.対象一覧.SelectMany(static 対象=>{
            var タブ=global::UI.ベンチマークソースタブ一覧.IDから取得を試みる(対象.タブID)!;
            return new[]{
                new global::UI.ベンチマークケース(タブ.左プロジェクト,タブ.左アセンブリ名,対象.左型完全名,対象.メソッド名,対象.引数キー,[]),
                new global::UI.ベンチマークケース(タブ.右プロジェクト,タブ.右アセンブリ名,対象.右型完全名,対象.メソッド名,対象.引数キー,[]),
            };
        }).ToArray();

    private static IReadOnlyList<global::UI.ベンチマーク性能ゲート測定>合格測定一覧を作成(global::UI.ベンチマーク性能ゲートマニフェスト マニフェスト)=>
        マニフェスト.対象一覧.SelectMany(対象=>global::System.Linq.Enumerable.Range(1,5).Select(反復=>
            new global::UI.ベンチマーク性能ゲート測定(対象.ID,反復,8d,80,"digest",2,8,true,10d,100,"digest",2,8,true))).ToArray();

}

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク性能ゲートCPU選択{
    [Fact,Trait("Category","Unit")]
    public void 最初のPコア主論理CPUを解決(){
        {
            global::UI.CPUコア情報[]CPU一覧=[
                new(8,global::UI.CPUコア種別.高効率,8,0,1L<<8,true),
                new(5,global::UI.CPUコア種別.高性能,2,1,1L<<5,true),
                new(3,global::UI.CPUコア種別.高性能,1,0,1L<<3,true),
                new(2,global::UI.CPUコア種別.高性能,1,0,1L<<2,true),
            ];

            var CPU=global::UI.ベンチマーク性能ゲートCPU選択.最初のPコア主論理CPUを解決(CPU一覧);

            Assert.Equal(2,CPU);
        }
        {
            global::UI.CPUコア情報[]CPU一覧=[
                new(0,global::UI.CPUコア種別.高性能,0,0,0,false),
                new(1,global::UI.CPUコア種別.高性能,0,0,1L<<1,true),
                new(2,global::UI.CPUコア種別.高性能,1,1,1L<<2,true),
            ];

            var CPU=global::UI.ベンチマーク性能ゲートCPU選択.最初のPコア主論理CPUを解決(CPU一覧);

            Assert.Equal(2,CPU);
        }
        {
            global::UI.CPUコア情報[]CPU一覧=[new(0,global::UI.CPUコア種別.不明,0,0,1,true)];

            var CPU=global::UI.ベンチマーク性能ゲートCPU選択.最初のPコア主論理CPUを解決(CPU一覧);

            Assert.Null(CPU);
        }
    }
}
