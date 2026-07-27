#if NET10_0_OR_GREATER && WINDOWS
using UI.Storage;

using System.Collections.Concurrent;

namespace Test.Benchmark.Pattern;

public sealed class 実行ビューモデル {
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマークアセンブリ登録_完全一致集合を受け入れる() {
        var プロジェクト一覧 = new global::UI.ベンチマークプロジェクト[] {
            new("左","Benchmark.Left"),
            new("左の重複","benchmark.left"),
            new("右","Benchmark.Right"),
        };

        global::UI.ベンチマークカタログ.アセンブリ登録を検証(
            プロジェクト一覧,
            ["BENCHMARK.LEFT","Benchmark.Right"]);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマークアセンブリ登録_不一致時に不足と予期しない項目を報告する() {
        var プロジェクト一覧 = new global::UI.ベンチマークプロジェクト[] {
            new("左","Benchmark.Left"),
            new("右","Benchmark.Right"),
        };

        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            global::UI.ベンチマークカタログ.アセンブリ登録を検証(
                プロジェクト一覧,
                ["Benchmark.Left","Benchmark.Unexpected"]));

        Assert.Contains("Benchmark.Right",例外.Message,StringComparison.Ordinal);
        Assert.Contains("Benchmark.Unexpected",例外.Message,StringComparison.Ordinal);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマークアセンブリ登録_標準プロジェクト一覧は左右2列の全アセンブリを受け入れる() {
        var 標準アセンブリ名一覧 = global::UI.ベンチマークソースタブ一覧.標準全て
            .SelectMany(static タブ => タブ.列一覧.Select(static 列 => 列.アセンブリ名))
            .Where(static 名前 => !string.IsNullOrWhiteSpace(名前))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(標準アセンブリ名一覧);

        var 登録一覧 = global::UI.ベンチマークカタログ.標準プロジェクト一覧
            .Select(static プロジェクト => プロジェクト.アセンブリ名)
            .ToArray();
        global::UI.ベンチマークカタログ.アセンブリ登録を検証(
            global::UI.ベンチマークカタログ.標準プロジェクト一覧,
            登録一覧);
        Assert.All(標準アセンブリ名一覧,名前 => Assert.Contains(名前,登録一覧,StringComparer.OrdinalIgnoreCase));
    }
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマークアセンブリ登録_2列タブも成功する() {
        var 最適化プロジェクト一覧 = new global::UI.ベンチマークプロジェクト[] {
            new("左","Benchmark.Optimization.All"),
            new("右","Benchmark.Optimization.NoRewrite"),
        };

        global::UI.ベンチマークカタログ.アセンブリ登録を検証(
            最適化プロジェクト一覧,
            ["Benchmark.Optimization.All","Benchmark.Optimization.NoRewrite"]);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマークアセンブリ登録_未知の余分なアセンブリは失敗し続ける() {
        var 例外 = Assert.Throws<InvalidOperationException>(() =>
            global::UI.ベンチマークカタログ.アセンブリ登録を検証(
                global::UI.ベンチマークカタログ.標準プロジェクト一覧,
                global::UI.ベンチマークカタログ.標準プロジェクト一覧
                    .Select(static プロジェクト => プロジェクト.アセンブリ名)
                    .Append("Benchmark.Unexpected.Truly")));

        Assert.Contains("Benchmark.Unexpected.Truly",例外.Message,StringComparison.Ordinal);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ペア停止を適用_左右ペアで停止表示を更新する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 行 = 配列ベンチマーク行を作成("AggregateBy_keySelector");
        行.ペア停止を適用(ソースタブ.左プロジェクト,ソースタブ.右プロジェクト,4096,true);
        Assert.Equal("4096",行.停止要素数文字列);

        var 適用前 = 行.停止要素数文字列;
        行.ペア停止を適用("Unrelated.Left","Unrelated.Right",1024,true);
        Assert.Equal(適用前,行.停止要素数文字列);
    }
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ペア停止を適用_2系列タブでも従来どおり左右ペアで停止表示を更新する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.最適化書き換え);
        var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Optimization.Rewrite.ArrayInt32","Where_predicate_Count","Array<int>"),
            [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Optimization.Rewrite.ArrayInt32","Where_predicate_Count","Array<int>"),
        };
        var 行 = new global::UI.ベンチマーク表行(
            "int|Where_predicate_Count",
            "Where_predicate_Count",
            "Where_predicate_Count",
            ソースタブ,
            ケース一覧);

        行.ペア停止を適用(ソースタブ.左プロジェクト,ソースタブ.右プロジェクト,8192,true);

        Assert.Equal("8192",行.停止要素数文字列);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task ソート列選択肢_2列タブは時間メモリシェアを提供し旧速度列を含まない() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var 列一覧 = フィクスチャ.ビューモデル.ベンチマークソート列選択肢一覧.Select(static 選択肢 => 選択肢.列).ToArray();

        Assert.Contains(global::UI.ベンチマークグリッドソート列.時間シェア,列一覧);
        Assert.Contains(global::UI.ベンチマークグリッドソート列.メモリシェア,列一覧);
    }
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 配列タブの全ベンチマークをチェックして1msで実行() {
        // UI操作: 配列タブを前提に「全て選択」し、「計測」を押下する。
        // 観測: 選択セルでCPU実行中アイコンが出ること、最終完了へ遷移すること、完了数と合計数が一致することを確認する。
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);
        var 実行中追跡 = 実行中アイコンを追跡(対象一覧);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));

        Assert.Contains(ビューモデル.ベンチマーク行一覧,行 => 行.CPU表示ありか);
        Assert.All(対象一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.Equal(対象一覧.Count,ビューモデル.選択ソースタブ.進行.完了数);
        Assert.Equal(対象一覧.Count,ビューモデル.選択ソースタブ.進行.合計数);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 複数ソースタブ_一回の実行で選択済み全タブが最終完了する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        フィクスチャ.実行サービス.全要素数を送信するか=true;
        var 配列タブ = ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==global::UI.ベンチマークソースタブ.System配列);
        var 列挙タブ = ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==global::UI.ベンチマークソースタブ.System範囲);
        var リストタブ = ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==global::UI.ベンチマークソースタブ.Systemリスト);
        var 配列状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列);
        var 列挙状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲);
        var リスト状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.Systemリスト);

        ビューモデル.選択ソースタブ=配列タブ;
        ビューモデル.全選択コマンド.Execute(null);
        var 配列対象一覧 = 選択セル一覧を取得(配列状態);

        ビューモデル.選択ソースタブ=列挙タブ;
        ビューモデル.全選択コマンド.Execute(null);
        var 列挙対象一覧 = 選択セル一覧を取得(列挙状態);

        ビューモデル.選択ソースタブ=リストタブ;
        ビューモデル.全選択コマンド.Execute(null);
        var リスト対象一覧 = 選択セル一覧を取得(リスト状態);
        var 全対象一覧 = 配列対象一覧.Concat(列挙対象一覧).Concat(リスト対象一覧).ToArray();

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 全対象一覧.All(最終完了か));
        await 条件成立まで待機または続行Async(() =>
            配列タブ.完了アイコンソース==global::UI.ベンチマーク状態画像名.最終完了
            &&列挙タブ.完了アイコンソース==global::UI.ベンチマーク状態画像名.最終完了
            &&リストタブ.完了アイコンソース==global::UI.ベンチマーク状態画像名.最終完了);

        Assert.All(配列対象一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.All(列挙対象一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.All(リスト対象一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        タブが最終完了したことを検証(配列タブ);
        タブが最終完了したことを検証(列挙タブ);
        タブが最終完了したことを検証(リストタブ);
    }


    private static void タブが最終完了したことを検証(global::UI.ソースタブ選択肢 選択肢) {
        Assert.True(
            選択肢.完了アイコンソース==global::UI.ベンチマーク状態画像名.最終完了,
            選択肢.表示名
            +" アイコン="+選択肢.完了アイコンソース
            +" 進行="+選択肢.進行.完了数+"/"+選択肢.進行.合計数
            +" 完了か="+選択肢.完了か
            +" 最終完了か="+選択肢.最終完了か);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ソースタブ完了アイコン_最終完了まで完了表示を使う() {
        var 選択肢 = new global::UI.ソースタブ選択肢(global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列));

        選択肢.進行を設定(new global::UI.ベンチマークタブ進行(1,1));
        選択肢.最終完了状態を設定(false);

        Assert.True(選択肢.完了アイコン表示中か);
        Assert.False(選択肢.進行リング表示中か);
        Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,選択肢.完了アイコンソース);

        選択肢.最終完了状態を設定(true);

        Assert.True(選択肢.完了アイコン表示中か);
        Assert.False(選択肢.進行リング表示中か);
        Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,選択肢.完了アイコンソース);

        選択肢.進行を設定(new global::UI.ベンチマークタブ進行(0,1));
        選択肢.最終完了状態を設定(true);

        Assert.True(選択肢.完了アイコン表示中か);
        Assert.False(選択肢.進行リング表示中か);
        Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,選択肢.完了アイコンソース);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ソースタブ進捗_待機実行停止完了を排他的に表示する() {
        var 選択肢 = new global::UI.ソースタブ選択肢(global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列));

        Assert.False(選択肢.進行リング表示中か);
        Assert.False(選択肢.実行スピナー表示中か);
        Assert.False(選択肢.完了アイコン表示中か);

        選択肢.進行を設定(new global::UI.ベンチマークタブ進行(0,2));

        Assert.True(選択肢.進行リング表示中か);
        Assert.False(選択肢.実行スピナー表示中か);
        Assert.False(選択肢.完了アイコン表示中か);
        Assert.Equal("待機 0/2",選択肢.進行自動化状態);

        選択肢.実行中状態を設定(true);
        選択肢.進行を設定(new global::UI.ベンチマークタブ進行(1,2));

        Assert.True(選択肢.進行リング表示中か);
        Assert.True(選択肢.実行スピナー表示中か);
        Assert.False(選択肢.完了アイコン表示中か);
        Assert.Equal("実行中 1/2",選択肢.進行自動化状態);

        選択肢.実行中状態を設定(false);

        Assert.True(選択肢.進行リング表示中か);
        Assert.False(選択肢.実行スピナー表示中か);
        Assert.False(選択肢.完了アイコン表示中か);
        Assert.Equal("待機 1/2",選択肢.進行自動化状態);

        選択肢.実行中状態を設定(true);
        選択肢.進行を設定(new global::UI.ベンチマークタブ進行(2,2));

        Assert.False(選択肢.進行リング表示中か);
        Assert.False(選択肢.実行スピナー表示中か);
        Assert.True(選択肢.完了アイコン表示中か);
        Assert.Equal("完了 2/2",選択肢.進行自動化状態);

        選択肢.実行中状態を設定(true);

        Assert.False(選択肢.進行リング表示中か);
        Assert.False(選択肢.実行スピナー表示中か);
        Assert.True(選択肢.完了アイコン表示中か);
        Assert.Equal("完了 2/2",選択肢.進行自動化状態);
    }
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ソースタブ進捗_並列実行では完了済みと実行中を独立表示する() {
        var 完了済み = new global::UI.ソースタブ選択肢(global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列));
        var 実行中 = new global::UI.ソースタブ選択肢(global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SystemIEnumerable));

        完了済み.進行を設定(new global::UI.ベンチマークタブ進行(1,1));
        完了済み.実行中状態を設定(false);
        実行中.進行を設定(new global::UI.ベンチマークタブ進行(1,2));
        実行中.実行中状態を設定(true);

        Assert.True(完了済み.完了アイコン表示中か);
        Assert.False(完了済み.進行リング表示中か);
        Assert.False(完了済み.実行スピナー表示中か);
        Assert.False(実行中.完了アイコン表示中か);
        Assert.True(実行中.進行リング表示中か);
        Assert.True(実行中.実行スピナー表示中か);
    }
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 初期化_復元した一時停止実行と新規選択で実行履歴を選択できる() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(
            global::UI.ベンチマークソースタブ.System配列,
            ストア => 一時停止した選択実行を初期登録(ストア));
        var ビューモデル = フィクスチャ.ビューモデル;

        Assert.Equal(0,ビューモデル.選択実行選択肢?.UI実行ID);
        Assert.Equal(0,ビューモデル.選択実行選択肢?.実行番号);
        Assert.Equal("新規",ビューモデル.選択実行選択肢?.表示名);
        var 選択実行選択肢 = ビューモデル.選択実行選択肢;
        Assert.Contains(ビューモデル.実行選択肢一覧.ToArray(),選択肢 => ReferenceEquals(選択肢,選択実行選択肢));
        Assert.False(ビューモデル.ベンチマーク一時停止中か);
        Assert.True(ビューモデル.実行履歴を選択可能か);
        Assert.True(ビューモデル.ベンチマーク条件を変更可能か);
        Assert.True(ビューモデル.新規グリッド作成可能か);
    }

    private static void 一時停止した選択実行を初期登録(global::UI.Storage.UI実行ストア ストア) {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 実行 = ストア.実行を作成(new(
            UI実行アプリ種別.ベンチマーク,
            "Recovered",
            global::UI.ベンチマーク実行プロファイル.テスト.ToString(),
            ソースタブ.タブ.ToString(),
            DateTimeOffset.UtcNow));
        ストア.実行状態を設定(実行.実行ID,UI実行状態.一時停止中,"復元テスト実行。");
        ストア.実行設定を保存(実行.実行ID,"RecoveredRoot",[4096],1_000_000,[]);
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[
            new(
                ソースタブ.ID,
                "int",
                "AggregateBy_keySelector",
                0,
                true,
                new(UI実行状態.待機中,string.Empty,null,null,null),
                new(UI実行状態.待機中,string.Empty,null,null,null)),
        ]);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void スナップショット表行_保存済み表示型を使う() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"int","AggregateBy_keySelector"),
            [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"int","AggregateBy_keySelector"),
        };
        var 行 = new global::UI.ベンチマーク表行(
            "int|AggregateBy_keySelector",
            "AggregateBy_keySelector",
            "AggregateBy_keySelector",
            ソースタブ,
            ケース一覧,
            "int");

        Assert.Equal("int",行.ベンチマーク型文字列);
        Assert.Equal("int.AggregateBy_keySelector",行.ベンチマークメソッド文字列);

        var 計測日時 = new DateTimeOffset(2026,7,8,13,39,31,TimeSpan.Zero);
        var 結果 = new global::UI.ベンチマーク結果項目(new global::UI.ベンチマークジョブ {
            プロジェクト=ソースタブ.左プロジェクト,
            アセンブリ名=ソースタブ.左アセンブリ名,
            型完全名="int",
            メソッド名="AggregateBy_keySelector",
            ソースタブ=ソースタブ.タブ,
            要素数=4096,
            ループ回数=global::UI.ベンチマーク作業軸.既定ループ回数,
            状態=global::UI.ジョブ状態.完了,
            割り当て論理CPU=0,
            開始日時=計測日時,
            終了日時=計測日時,
            平均ナノ秒=1_000_000,
            割り当てバイト数=1024,
            終了コード=0,
        });
        行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果,0,計測日時);

        Assert.Equal("4096",行.左セル.要素数文字列);
        Assert.NotEmpty(行.左セル.実行文字列);
        Assert.True(行.左セル.CPU表示ありか);
        Assert.Equal("0",行.左セル.論理プロセッサ文字列);
        Assert.NotEmpty(行.左時間文字列);

        行.ベンチマーク名を更新(4096);

        Assert.Equal("int",行.ベンチマーク型文字列);
        Assert.Equal("int.AggregateBy_keySelector",行.ベンチマークメソッド文字列);
        Assert.Equal(TextDecorations.None,行.ベンチマーク識別文字装飾);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void スナップショット表行_保存済み型を完全ベンチマーク名へ使う() {
        {
            var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.Seamless永続化);
            var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
                [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqInt32","Aggregate_func"),
                [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqInt32","Aggregate_func"),
            };
            var 行 = new global::UI.ベンチマーク表行(
                "Persisted<int>|Aggregate_func",
                "Aggregate_func",
                "Aggregate_func",
                ソースタブ,
                ケース一覧,
                "Persisted<int>");

            Assert.Equal("Persisted<int>",行.ベンチマーク型文字列);
            Assert.Equal("Persisted<int>.Aggregate_func",行.ベンチマークメソッド文字列);
            行.ベンチマーク名を更新(4096);
            Assert.Equal("Persisted<int>.Aggregate_func",行.ベンチマークメソッド文字列);
        }
        {
            var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);
            var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
                [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqClassInt32","Aggregate_func"),
                [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqClassInt32","Aggregate_func"),
            };
            var 行 = new global::UI.ベンチマーク表行(
                "Tsavorite<ClassInt32>|Aggregate_func",
                "Aggregate_func",
                "Aggregate_func",
                ソースタブ,
                ケース一覧,
                "Tsavorite<ClassInt32>");

            Assert.Equal("Tsavorite<ClassInt32>",行.ベンチマーク型文字列);
            Assert.Equal("Tsavorite<ClassInt32>.Aggregate_func",行.ベンチマークメソッド文字列);
            行.ベンチマーク名を更新(4096);
            Assert.Equal("Tsavorite<ClassInt32>.Aggregate_func",行.ベンチマークメソッド文字列);
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマーク表行_パス付きメソッド名を表示する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.最適化書き換え);
        {
            // int型のArray書き換えを表示する。
            var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
                [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Optimization.Rewrite.ArrayInt32","Where_predicate_Count","Array<int>"),
                [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Optimization.Rewrite.ArrayInt32","Where_predicate_Count","Array<int>"),
            };
            var 行 = new global::UI.ベンチマーク表行(
                "Array<int>|Where_predicate_Count",
                "Where_predicate_Count",
                "Where_predicate_Count",
                ソースタブ,
                ケース一覧);

            Assert.Equal("int",行.ベンチマーク型文字列);
            Assert.Equal("Array<int>.Where_predicate_Count",行.ベンチマークメソッド文字列);
            行.ベンチマーク名を更新(4096);
            Assert.Equal("Array<int>.Where_predicate_Count",行.ベンチマーク名);
            Assert.Equal("Array<int>.Where_predicate_Count",行.ベンチマークメソッド文字列);
        }
        {
            // ClassInt32型のArray書き換えを表示する。
            var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
                [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Optimization.Rewrite.ArrayClassInt32","Where_predicate_Count","Array<ClassInt32>"),
                [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Optimization.Rewrite.ArrayClassInt32","Where_predicate_Count","Array<ClassInt32>"),
            };
            var 行 = new global::UI.ベンチマーク表行(
                "Array<ClassInt32>|Where_predicate_Count",
                "Where_predicate_Count",
                "Where_predicate_Count",
                ソースタブ,
                ケース一覧);

            Assert.Equal("ClassInt32",行.ベンチマーク型文字列);
            Assert.Equal("Array<ClassInt32>.Where_predicate_Count",行.ベンチマークメソッド文字列);
            行.ベンチマーク名を更新(4096);
            Assert.Equal("Array<ClassInt32>.Where_predicate_Count",行.ベンチマーク名);
            Assert.Equal("Array<ClassInt32>.Where_predicate_Count",行.ベンチマークメソッド文字列);
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void スナップショット表行_ベンチマークケース不在時に型とメソッドへ取消線を表示する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 行 = new global::UI.ベンチマーク表行(
            "int|MissingMethod",
            "MissingMethod",
            "MissingMethod",
            ソースタブ,
            new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase),
            "int");

        Assert.All(行.プロジェクトセル一覧,static セル => Assert.False(セル.ケースありか));
        Assert.Equal(TextDecorations.Strikethrough,行.ベンチマーク識別文字装飾);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void 表行_欠けた側に対象なしを表示する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 左ケース = new global::UI.ベンチマークケース(
            ソースタブ.左プロジェクト,
            ソースタブ.左アセンブリ名,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            "Aggregate_func");
        var 行 = new global::UI.ベンチマーク表行(
            "int|Aggregate_func",
            "Aggregate_func",
            "Aggregate_func",
            ソースタブ,
            new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
                [ソースタブ.左プロジェクト]=左ケース,
            });

        Assert.True(行.左セル.ケースありか);
        Assert.Empty(行.左セル.実行文字列);
        Assert.False(行.右セル.ケースありか);
        Assert.Equal("対象なし",行.右セル.実行文字列);
        Assert.Equal("対象なし",行.右セル.状態文字列);
        Assert.False(行.右セル.状態アイコン表示中か);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void 表行_ThenByのカタログ親型引数を表示する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System範囲);
        var 左ケース = new global::UI.ベンチマークケース(
            ソースタブ.左プロジェクト,
            ソースタブ.左アセンブリ名,
            "Shared.Linq.Range_start_count+OrderBy_keySelector_chain",
            "ThenBy_keySelector",
            "Range_start_count.OrderBy_keySelector");
        var 行 = new global::UI.ベンチマーク表行(
            "int|ThenBy_keySelector",
            "ThenBy_keySelector",
            "Range_start_count.OrderBy_keySelector.ThenBy_keySelector",
            ソースタブ,
            new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
                [ソースタブ.左プロジェクト]=左ケース,
            },
            "int");

        Assert.Equal("int",行.ベンチマーク型文字列);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void 行チェック切替で選択チェック状態が真偽を循環する() {
        var 行 = 配列ベンチマーク行を作成("AggregateBy_keySelector");

        行.選択中か=true;

        Assert.True(行.選択チェック状態);
        Assert.True(行.選択中か);
        Assert.True(行.左セル.実行予定か);
        Assert.True(行.右セル.実行予定か);

        行.選択中か=false;

        Assert.False(行.選択チェック状態);
        Assert.False(行.選択中か);
        Assert.False(行.左セル.実行予定か);
        Assert.False(行.右セル.実行予定か);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task グリッド行選択_実行チェックを変えずUI選択を更新する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 定義 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 状態 = new global::UI.ベンチマークソースタブ表示状態(定義);
        var 行 = 配列ベンチマーク行を作成("AggregateBy_keySelector");
        var 実行チェック中か = 行.選択チェック状態;

        ビューモデル.ベンチマーク行UI選択を同期(状態,[行],[]);

        Assert.True(行.UI選択中か);
        Assert.Equal(実行チェック中か,行.選択チェック状態);

        ビューモデル.ベンチマーク行UI選択を同期(状態,[],[行]);

        Assert.False(行.UI選択中か);
        Assert.Equal(実行チェック中か,行.選択チェック状態);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void バインド行インデックス_ジョブから結果とケースまで保持される() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ジョブ = new global::UI.ベンチマークジョブ {
            プロジェクト=ソースタブ.左プロジェクト,
            アセンブリ名=ソースタブ.左アセンブリ名,
            型完全名=ソースタブ.左型接頭辞+".Sample",
            ソースタブ=ソースタブ.タブ,
            バインド行インデックス=7,
        };

        var 結果 = new global::UI.ベンチマーク結果項目(ジョブ);
        var ベンチマーク = ジョブ.ベンチマークケースに変換();

        Assert.Equal(7,結果.バインド行インデックス);
        Assert.Equal(7,ベンチマーク.バインド行インデックス);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task CPU空白対象_全選択は解除し混在は選択する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var 対象一覧 = new[]{
            new global::UI.CPU選択項目(0,global::UI.CPUコア種別.高性能,0,0,true),
            new global::UI.CPU選択項目(1,global::UI.CPUコア種別.高性能,0,1,true),
        };

        foreach(var CPU in 対象一覧) {
            CPU.選択中か=true;
        }
        フィクスチャ.ビューモデル.CPUチェックを切り替え(対象一覧);
        Assert.All(対象一覧,static CPU => Assert.False(CPU.選択中か));

        対象一覧[0].選択中か=true;
        フィクスチャ.ビューモデル.CPUチェックを切り替え(対象一覧);
        Assert.All(対象一覧,static CPU => Assert.True(CPU.選択中か));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void 実行中結果_右ZLinqセルへ適用される() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 行 = 配列ベンチマーク行を作成("Aggregate_seed_func_resultSelector");
        var 結果 = 配列結果を作成(
            ソースタブ.右プロジェクト,
            "Aggregate_seed_func_resultSelector",
            global::UI.ジョブ状態.実行中,
            実行項目インデックス: 1);

        行.実行結果を適用(結果,null,DateTimeOffset.Now);

        Assert.True(行.右セル.実行中表示か);
        Assert.False(行.左セル.実行中表示か);
        Assert.Equal(global::UI.ベンチマーク状態画像名.CPU実行中,行.右セル.状態画像ソース);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void 実行中結果_左FLinqセルへ適用される() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 行 = 配列ベンチマーク行を作成("Aggregate_seed_func_resultSelector");
        var 結果 = 配列結果を作成(
            ソースタブ.左プロジェクト,
            "Aggregate_seed_func_resultSelector",
            global::UI.ジョブ状態.実行中,
            実行項目インデックス: 0);

        行.実行結果を適用(結果,null,DateTimeOffset.Now);

        Assert.True(行.左セル.実行中表示か);
        Assert.False(行.右セル.実行中表示か);
        Assert.Equal(global::UI.ベンチマーク状態画像名.CPU実行中,行.左セル.状態画像ソース);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 実行イベントパイプライン_ZLinqの即時終端前に実行中状態を保持する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var パイプライン = new global::UI.ベンチマーク実行イベントパイプライン();
        var 実行中追跡 = 配列結果を作成(ソースタブ.右プロジェクト,"Aggregate_seed_func_resultSelector",global::UI.ジョブ状態.実行中,実行項目インデックス: 1);
        var 完了結果 = 配列結果を作成(ソースタブ.右プロジェクト,"Aggregate_seed_func_resultSelector",global::UI.ジョブ状態.完了,実行項目インデックス: 1);

        パイプライン.イベントを送信(new global::UI.ベンチマーク実行イベント(実行中追跡,1,null,DateTimeOffset.UtcNow));
        パイプライン.イベントを送信(new global::UI.ベンチマーク実行イベント(完了結果,1,null,DateTimeOffset.UtcNow));
        var スナップショット = await パイプラインスナップショットを待機Async(パイプライン);

        Assert.Contains(スナップショット.実行中更新一覧,項目 =>
            string.Equals(項目.結果.プロジェクト,ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&global::UI.ジョブ状態名.保存値と一致するか(項目.結果.状態,global::UI.ジョブ状態.実行中));
        Assert.Contains(スナップショット.終端結果一覧,項目 =>
            string.Equals(項目.結果.プロジェクト,ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(項目.結果.状態,"Completed",StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 実行イベントパイプライン_FLinqとZLinqの実行中キーを分離する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var パイプライン = new global::UI.ベンチマーク実行イベントパイプライン();
        var 左 = 配列結果を作成(ソースタブ.左プロジェクト,"Aggregate_seed_func_resultSelector",global::UI.ジョブ状態.実行中,実行項目インデックス: 0);
        var 右 = 配列結果を作成(ソースタブ.右プロジェクト,"Aggregate_seed_func_resultSelector",global::UI.ジョブ状態.実行中,実行項目インデックス: 1);

        パイプライン.イベントを送信(new global::UI.ベンチマーク実行イベント(左,1,null,DateTimeOffset.UtcNow));
        パイプライン.イベントを送信(new global::UI.ベンチマーク実行イベント(右,1,null,DateTimeOffset.UtcNow));
        var スナップショット = await パイプラインスナップショットを待機Async(パイプライン);

        Assert.Contains(スナップショット.実行中更新一覧,項目 => string.Equals(項目.結果.プロジェクト,ソースタブ.左プロジェクト,StringComparison.OrdinalIgnoreCase));
        Assert.Contains(スナップショット.実行中更新一覧,項目 => string.Equals(項目.結果.プロジェクト,ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2,スナップショット.実行中更新一覧.Count);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_未保存選択肢を残さず保存済みDB実行を選択する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);

        var 保存済み実行 = ビューモデル.選択実行選択肢!;
        Assert.True(保存済み実行.UI実行ID>0);
        Assert.Contains(ビューモデル.実行選択肢一覧,選択肢 => ReferenceEquals(選択肢,保存済み実行));
        Assert.NotEqual("未保存",保存済み実行.表示名);
        Assert.Contains("/",保存済み実行.表示名);
        Assert.True(保存済み実行.総ジョブ数>0);
        Assert.All(ビューモデル.実行選択肢一覧,選択肢 => Assert.NotEqual("未保存",選択肢.表示名));
        Assert.All(対象一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_新規グリッドで未保存一時実行を削除する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Count==1);
        var 一時実行ID = フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Single().実行ID;

        Assert.True(ビューモデル.DB保存可能か);
        Assert.DoesNotContain(ビューモデル.実行選択肢一覧,選択肢 => 選択肢.UI実行ID==一時実行ID);

        ビューモデル.新規グリッドコマンド.Execute(null);

        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).All(実行 => 実行.実行ID!=一時実行ID));
        Assert.Empty(フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_DB保存で一時実行を昇格し同じ実行を維持する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Count==1);
        var 一時実行ID = フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Single().実行ID;

        await 今回実行を保存Async(ビューモデル);

        var 保存済み実行 = ビューモデル.選択実行選択肢!;
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Any(実行 => 実行.実行ID==一時実行ID)
            &&ビューモデル.実行選択肢一覧.Any(選択肢 => 選択肢.UI実行ID==一時実行ID));
        Assert.True(保存済み実行.UI実行ID>0);
        Assert.Equal(一時実行ID,保存済み実行.UI実行ID);
        Assert.Single(フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_保存済み実行の読込で未保存一時実行を削除する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 最初の対象一覧 = 選択セル一覧を取得(ビューモデル);
        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 最初の対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);
        var 保存済み実行 = ビューモデル.選択実行選択肢!;

        ビューモデル.新規グリッドコマンド.Execute(null);
        await 条件成立まで待機Async(() => ビューモデル.選択実行選択肢?.UI実行ID==0);
        ビューモデル.全選択コマンド.Execute(null);
        var 次の対象一覧 = 選択セル一覧を取得(ビューモデル);
        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 次の対象一覧.All(最終完了か));
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Count==2);
        var 一時実行ID = フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク)
            .Single(実行 => 実行.実行ID!=保存済み実行.UI実行ID)
            .実行ID;

        ビューモデル.選択実行選択肢=保存済み実行;

        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).All(実行 => 実行.実行ID!=一時実行ID)
            &&フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Any(実行 => 実行.実行ID==保存済み実行.UI実行ID));
        await 条件成立まで待機Async(() => !ビューモデル.実行履歴読込中か&&ビューモデル.実行履歴を選択可能か);
        Assert.Single(フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク));
        Assert.Equal(保存済み実行.UI実行ID,フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Single().実行ID);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_再実行で前の一時実行を削除する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 最初の対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 最初の対象一覧.All(最終完了か));
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Count==1);
        var 最初の一時実行ID = フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Single().実行ID;

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Any(実行 => 実行.実行ID!=最初の一時実行ID));
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).All(実行 => 実行.実行ID!=最初の一時実行ID));

        var 残存実行 = フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Single();
        Assert.NotEqual(最初の一時実行ID,残存実行.実行ID);
        Assert.True(ビューモデル.DB保存可能か);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_新規選択後に保存済みDB実行を再読込できる() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);
        var 保存済み実行 = ビューモデル.選択実行選択肢!;
        var データグリッド一括イベント一覧 = new List<bool>();
        ビューモデル.データグリッド更新バッチ変更時+=(_,イベント引数) => データグリッド一括イベント一覧.Add(イベント引数.開始時か);

        ビューモデル.新規グリッドコマンド.Execute(null);
        await 条件成立まで待機Async(() => 対象一覧.All(static セル => セル.状態画像ソース==global::UI.ベンチマーク状態画像名.待機中));
        Assert.Equal("新規",ビューモデル.選択実行選択肢?.表示名);

        新規グリッドのカタログ表示を検証(ビューモデル);
        ビューモデル.選択実行選択肢=保存済み実行;
        Assert.True(ビューモデル.実行履歴読込中か);
        Assert.False(ビューモデル.実行履歴を選択可能か);
        await 条件成立まで待機Async(() => ビューモデル.結果項目一覧.Any(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)));
        await 条件成立まで待機Async(() => !ビューモデル.実行履歴読込中か&&ビューモデル.実行履歴を選択可能か);

        Assert.NotEmpty(ビューモデル.ベンチマーク行一覧);
        Assert.True(ビューモデル.結果項目一覧.Count>0);
        var 完了結果一覧 = ビューモデル.結果項目一覧
            .Where(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.All(完了結果一覧,項目 => Assert.True(項目.要素数>0));
        Assert.All(完了結果一覧,項目 => Assert.NotNull(項目.割り当て論理CPU));
        Assert.Contains(ビューモデル.結果項目一覧,項目 => ビューモデル.選択ソースタブ.定義.プロジェクトを含むか(項目.プロジェクト));
        await 条件成立まで待機または続行Async(() => 選択セル一覧を取得(ビューモデル).All(読込済みセルメタデータがあるか));
        var 読込対象一覧 = 選択セル一覧を取得(ビューモデル);
        Assert.All(読込対象一覧,セル => Assert.True(読込済みセルメタデータがあるか(セル),セルメタデータメッセージを作成(ビューモデル,セル)));
        var 読込行一覧 = ビューモデル.ベンチマーク行一覧
            .Where(static 行 => 行.選択中か&&行.プロジェクトセル一覧.Any(static セル => セル.ケースありか))
            .ToArray();
        Assert.All(読込行一覧,行 => Assert.Equal("int",行.ベンチマーク型文字列));
        Assert.All(読込行一覧,読込行にペア測定値があることを検証);

        Assert.Equal(保存済み実行.UI実行ID,ビューモデル.選択実行選択肢?.UI実行ID);
        Assert.NotEmpty(ビューモデル.結果項目一覧);
        Assert.True(データグリッド一括イベント一覧.Count>=2);
        Assert.Equal(0,データグリッド一括イベント一覧.Aggregate(0,(深さ,開始か) => 開始か ? 深さ+1 : 深さ-1));
        Assert.True(データグリッド一括イベント一覧.First());
        Assert.False(データグリッド一括イベント一覧.Last());
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 読込済み保存DB実行_再実行でも保存済みチェック状態を保持する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        現在タブを全選択(ビューモデル);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);
        var 保存済み選択数 = 対象一覧.Count;
        Assert.True(保存済み選択数>0);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);
        var 保存済み実行 = ビューモデル.選択実行選択肢!;
        var 実行回数 = フィクスチャ.実行サービス.要求要素数から再開する指定一覧.Count;

        ビューモデル.新規グリッドコマンド.Execute(null);
        await 条件成立まで待機Async(() => 対象一覧.All(static セル => セル.状態画像ソース==global::UI.ベンチマーク状態画像名.待機中));
        ビューモデル.選択実行選択肢=保存済み実行;
        await 条件成立まで待機Async(() => !ビューモデル.実行履歴読込中か
            &&ビューモデル.結果項目一覧.Any(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)));

        Assert.True(ビューモデル.実行コマンド.CanExecute(null),$"状態={ビューモデル.状態文字列} 実行可能か={ビューモデル.ベンチマークを実行可能か} 選択数={選択セル一覧を取得(ビューモデル).Count} 選択肢={ビューモデル.選択実行選択肢?.表示名}");
        await ビューモデル.実行コマンド.ExecuteAsync(null);

        await 条件成立まで待機または続行Async(() => フィクスチャ.実行サービス.要求要素数から再開する指定一覧.Count==実行回数+1);
        Assert.True(
            フィクスチャ.実行サービス.要求要素数から再開する指定一覧.Count==実行回数+1,
            $"状態={ビューモデル.状態文字列} 実行可能か={ビューモデル.ベンチマークを実行可能か} 選択数={選択セル一覧を取得(ビューモデル).Count} 選択肢={ビューモデル.選択実行選択肢?.表示名}");
        Assert.Equal("新規",ビューモデル.選択実行選択肢?.表示名);
        Assert.Equal(保存済み選択数,選択セル一覧を取得(ビューモデル).Count);
        Assert.Equal(保存済み選択数,フィクスチャ.実行サービス.観測ベンチマーク数一覧[^1]);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 行チェック_実行はチェック済み行ペアだけを使う() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 行 = ビューモデル.ベンチマーク行一覧.First(static 行 => 行.左セル.ケースありか&&行.右セル.ケースありか);

        ビューモデル.ベンチマーク行を切り替え(行);

        Assert.True(ビューモデル.選択ベンチマークセルありか);
        Assert.True(行.選択中か);
        Assert.True(行.左セル.実行予定か);
        Assert.True(行.右セル.実行予定か);
        Assert.Equal(2,選択セル一覧を取得(ビューモデル).Count);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機または続行Async(() => フィクスチャ.実行サービス.観測ベンチマーク数一覧.Count>0);

        Assert.Equal(2,フィクスチャ.実行サービス.観測ベンチマーク数一覧[^1]);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 行チェック_実行結果を未選択行へコピーしない() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 選択行 = ビューモデル.ベンチマーク行一覧.First(static 行 => 行.左セル.ケースありか&&行.右セル.ケースありか);
        var 未選択行一覧 = ビューモデル.ベンチマーク行一覧.Where(行 => !ReferenceEquals(行,選択行)).ToArray();
        ビューモデル.ベンチマーク行を切り替え(選択行);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 選択行.左セル.状態画像ソース==global::UI.ベンチマーク状態画像名.最終完了
            &&選択行.右セル.状態画像ソース==global::UI.ベンチマーク状態画像名.最終完了);

        Assert.True(選択行.左セル.測定値.測定値ありか);
        Assert.True(選択行.右セル.測定値.測定値ありか);
        Assert.All(未選択行一覧,行 => Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.False(セル.測定値.測定値ありか)));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 読込済み保存DB実行_全スナップショット行を表示しCPU表示を復元する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 初期行数 = ビューモデル.ベンチマーク行一覧.Count;
        var 選択行一覧 = ビューモデル.ベンチマーク行一覧.Take(1).ToArray();
        Assert.Single(選択行一覧);
        foreach(var 行 in 選択行一覧) {
            ビューモデル.ベンチマーク行を切り替え(行);
        }

        var 選択セル一覧 = 選択セル一覧を取得(ビューモデル);
        Assert.Equal(2,選択セル一覧.Count);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 選択セル一覧.All(最終完了か));
        await 条件成立まで待機Async(() => フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Count==1);
        var 一時実行ID = フィクスチャ.ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク).Single().実行ID;
        await 条件成立まで待機Async(() => フィクスチャ.ストア.ベンチマークスナップショット項目一覧を取得(一時実行ID).Count>選択行一覧.Length);
        await 今回実行を保存Async(ビューモデル);
        var 保存済み実行 = ビューモデル.選択実行選択肢!;

        ビューモデル.新規グリッドコマンド.Execute(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク行一覧.Count>0&&選択セル一覧を取得(ビューモデル).Count==0);
        ビューモデル.選択実行選択肢=保存済み実行;
        await 条件成立まで待機Async(() => !ビューモデル.実行履歴読込中か
            &&ビューモデル.結果項目一覧.Any(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)));

        var 読込行一覧 = ビューモデル.ベンチマーク行一覧.ToArray();
        Assert.Equal(初期行数,読込行一覧.Length);
        Assert.Single(読込行一覧,static 行 => 行.選択中か);
        Assert.Contains(読込行一覧,行 => !行.選択中か);
        Assert.All(読込行一覧.Where(static 行 => 行.選択中か),行 => {
            読込行にペア測定値があることを検証(行);
            Assert.True(行.左セル.CPU表示ありか,セルメタデータメッセージを作成(ビューモデル,行.左セル));
            Assert.True(行.右セル.CPU表示ありか,セルメタデータメッセージを作成(ビューモデル,行.右セル));
            Assert.False(string.IsNullOrWhiteSpace(行.左セル.コア種別画像ソース),セルメタデータメッセージを作成(ビューモデル,行.左セル));
            Assert.False(string.IsNullOrWhiteSpace(行.右セル.コア種別画像ソース),セルメタデータメッセージを作成(ビューモデル,行.右セル));
        });
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 読込済み保存DB実行_結果要素数変更後も部分選択と保存済み実行を維持する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        フィクスチャ.実行サービス.全要素数を送信するか=true;
        現在タブを全選択(ビューモデル);
        Assert.All(ビューモデル.ベンチマーク行一覧,行 => Assert.True(行.選択チェック状態));
        Assert.Null(ビューモデル.全ベンチマーク選択状態);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.結果項目一覧.Any(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)));
        Assert.All(ビューモデル.ベンチマーク行一覧,行 => {
            Assert.True(行.左セル.実行予定か);
            Assert.True(行.右セル.実行予定か);
            Assert.True(行.選択チェック状態);
        });
        await 今回実行を保存Async(ビューモデル);
        var 保存済み実行 = ビューモデル.選択実行選択肢!;

        ビューモデル.新規グリッドコマンド.Execute(null);
        await 条件成立まで待機Async(() => 選択セル一覧を取得(ビューモデル).Count==0);
        ビューモデル.選択実行選択肢=保存済み実行;
        await 条件成立まで待機Async(() => !ビューモデル.実行履歴読込中か
            &&ビューモデル.結果項目一覧.Any(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase))
            &&ビューモデル.結果要素数選択肢一覧.Count>0);

        Assert.Equal(保存済み実行.UI実行ID,ビューモデル.選択実行選択肢?.UI実行ID);
        Assert.All(ビューモデル.ベンチマーク行一覧,行 => {
            Assert.True(行.左セル.実行予定か);
            Assert.True(行.右セル.実行予定か);
            Assert.True(行.選択チェック状態);
            Assert.True(行.選択中か);
        });
        Assert.False(ビューモデル.全ベンチマーク選択状態);

        ビューモデル.選択結果要素数=ビューモデル.結果要素数選択肢一覧.First();

        Assert.Equal(保存済み実行.UI実行ID,ビューモデル.選択実行選択肢?.UI実行ID);
        Assert.All(ビューモデル.ベンチマーク行一覧,行 => Assert.True(行.選択チェック状態));
        Assert.False(ビューモデル.全ベンチマーク選択状態);

        ビューモデル.ベンチマーク行を切り替え(ビューモデル.ベンチマーク行一覧[0]);

        Assert.Equal(保存済み実行.UI実行ID,ビューモデル.選択実行選択肢?.UI実行ID);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了実行_再起動せず以前の保存済みDB実行を選択できる() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);
        var 最初の実行ID = ビューモデル.選択実行選択肢!.UI実行ID;

        ビューモデル.新規グリッドコマンド.Execute(null);
        await 条件成立まで待機Async(() => 対象一覧.All(static セル => セル.状態画像ソース==global::UI.ベンチマーク状態画像名.待機中));
        ビューモデル.全選択コマンド.Execute(null);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);
        await 条件成立まで待機Async(() => ビューモデル.選択実行選択肢 is { UI実行ID:>0 }&&ビューモデル.選択実行選択肢.UI実行ID!=最初の実行ID);
        var 最初の実行 = ビューモデル.実行選択肢一覧.First(選択肢 => 選択肢.UI実行ID==最初の実行ID);

        ビューモデル.選択実行選択肢=最初の実行;
        await 条件成立まで待機Async(() => ビューモデル.選択実行選択肢?.UI実行ID==最初の実行ID
            &&ビューモデル.結果項目一覧.Any(static 項目 => string.Equals(項目.状態,"Completed",StringComparison.OrdinalIgnoreCase)));
        await 条件成立まで待機Async(() => !ビューモデル.実行履歴読込中か&&ビューモデル.実行履歴を選択可能か);

        Assert.Equal(最初の実行ID,ビューモデル.選択実行選択肢?.UI実行ID);
        Assert.NotEmpty(ビューモデル.結果項目一覧);
    }

#if DEBUG
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 実行履歴クリアコマンド_履歴を削除し表示をリセットする() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.結果項目一覧.Count>0&&対象一覧.All(最終完了か));
        await 今回実行を保存Async(ビューモデル);

        Assert.True(ビューモデル.実行履歴クリアコマンド.CanExecute(null));
        await ビューモデル.実行履歴クリアコマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.実行選択肢一覧.Count==1
            &&ビューモデル.選択実行選択肢?.表示名=="新規"
            &&ビューモデル.結果項目一覧.Count==0
            &&対象一覧.All(static セル => セル.状態画像ソース==global::UI.ベンチマーク状態画像名.待機中));

        Assert.Single(ビューモデル.実行選択肢一覧);
        Assert.Equal("新規",ビューモデル.選択実行選択肢?.表示名);
        Assert.Empty(ビューモデル.結果項目一覧);
        Assert.All(対象一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.待機中,セル.状態画像ソース));
    }
#endif

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 配列タブのFLinqベンチマークをチェックして1msで実行() {
        // UI操作: 配列タブでFLinq側グループを選択し、「計測」を押下する。
        // 観測: 左列（FLinq）セルのCPU実行中から最終完了への遷移を確認する。関連する右列（ZLinq）行とのペア表示整合も含めて確認する。
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        現在タブを全選択(ビューモデル);
        var FLinqセル一覧 = ビューモデル.ベンチマーク行一覧.Select(static 行 => 行.左セル).Where(static セル => セル.ケースありか).ToArray();
        var ZLinqセル一覧 = ビューモデル.ベンチマーク行一覧.Select(static 行 => 行.右セル).Where(static セル => セル.ケースありか).ToArray();
        var 全観測セル一覧 = FLinqセル一覧.Concat(ZLinqセル一覧).ToArray();
        var 実行中追跡 = 実行中アイコンを追跡(全観測セル一覧);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 全観測セル一覧.All(最終完了か));

        Assert.All(FLinqセル一覧,セル => Assert.Contains(セル,実行中追跡));
        Assert.All(FLinqセル一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.All(ZLinqセル一覧,セル => Assert.Contains(セル,実行中追跡));
        Assert.All(ZLinqセル一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.Equal(全観測セル一覧.Length,ビューモデル.選択ソースタブ.進行.完了数);
        Assert.Equal(全観測セル一覧.Length,ビューモデル.選択ソースタブ.進行.合計数);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 配列タブのZLinqベンチマークをチェックして1msで実行() {
        // UI操作: 配列タブでZLinq側グループを選択し、「計測」を押下する。
        // 観測: 右列（ZLinq）セルのCPU実行中アイコンと最終完了への遷移、完了数と合計数を確認する。
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        現在タブを全選択(ビューモデル);
        var ZLinqセル一覧 = ビューモデル.ベンチマーク行一覧.Select(static 行 => 行.右セル).Where(static セル => セル.ケースありか).ToArray();
        var 全観測セル一覧 = ビューモデル.ベンチマーク行一覧.SelectMany(static 行 => 行.プロジェクトセル一覧).Where(static セル => セル.ケースありか).ToArray();
        var 実行中追跡 = 実行中アイコンを追跡(全観測セル一覧);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 全観測セル一覧.All(最終完了か));

        Assert.All(全観測セル一覧,セル => Assert.Contains(セル,実行中追跡));
        Assert.All(全観測セル一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.Equal(全観測セル一覧.Length,ビューモデル.選択ソースタブ.進行.完了数);
        Assert.Equal(全観測セル一覧.Length,ビューモデル.選択ソースタブ.進行.合計数);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task IEnumerableタブの全ベンチマークをチェックして1msで実行() {
        // UI操作: IEnumerable（Range）ベンチマーク一覧で「全て選択」し、「計測」を押下する。
        // 観測: 対象セルのCPU状態アイコンと最終完了への遷移、完了数と合計数を確認する。
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System範囲);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);
        var FLinqセル一覧 = ビューモデル.ベンチマーク行一覧.Select(static 行 => 行.左セル).Where(static セル => セル.ケースありか).ToArray();
        var 実行中追跡 = 実行中アイコンを追跡(対象一覧);

        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));

        Assert.All(FLinqセル一覧,セル => Assert.Contains(セル,実行中追跡));
        Assert.All(FLinqセル一覧,セル => Assert.Equal(global::UI.ベンチマーク状態画像名.最終完了,セル.状態画像ソース));
        Assert.Equal(対象一覧.Count,ビューモデル.選択ソースタブ.進行.完了数);
        Assert.Equal(対象一覧.Count,ビューモデル.選択ソースタブ.進行.合計数);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task ソースタブの全選択は他タブへ同期しない() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 配列状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列);
        var 範囲状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲);
        var 範囲タブ選択肢 = ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==global::UI.ベンチマークソースタブ.System範囲);

        // 配列タブで全選択しても、Rangeタブの行や進行ゲージには反映されない。
        現在タブを全選択(ビューモデル);

        Assert.All(配列状態.行一覧,行 => Assert.True(行.左セル.実行予定か));
        Assert.All(範囲状態.行一覧,行 => Assert.False(行.左セル.実行予定か));

        // Rangeタブへ切り替えても、左セルの選択状態は偽のまま（同期しない）。
        ビューモデル.選択ソースタブ=範囲タブ選択肢;

        Assert.All(範囲状態.行一覧,行 => Assert.False(行.左セル.実行予定か));
        Assert.All(配列状態.行一覧,行 => Assert.True(行.左セル.実行予定か));

        // Rangeタブ側で全選択した場合は、Rangeタブだけに反映される。
        現在タブを全選択(ビューモデル);

        Assert.All(範囲状態.行一覧,行 => Assert.True(行.左セル.実行予定か));
        Assert.All(配列状態.行一覧,行 => Assert.True(行.左セル.実行予定か));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task ソースタブ切替でも緑クリック選択をタブ毎に保持する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 配列状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列);
        var 範囲状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲);
        {
            //1タブ目の1行目を緑選択し、2タブ目へ漏れず1タブ目には保持されることを確認する
            ビューモデル.ベンチマーク行UI選択を同期(配列状態,[配列状態.行一覧[0]],[]);
            Assert.True(配列状態.行一覧[0].UI選択中か);

            ビューモデル.選択ソースタブ=ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==global::UI.ベンチマークソースタブ.System範囲);

            Assert.All(範囲状態.行一覧,static 行 => Assert.False(行.UI選択中か));
            Assert.True(配列状態.行一覧[0].UI選択中か);
        }
        {
            //2タブ目の2行目を緑選択しても1タブ目へ混線せず、1タブ目へ戻ると選択が復元されていることを確認する
            ビューモデル.ベンチマーク行UI選択を同期(範囲状態,[範囲状態.行一覧[1]],[]);

            Assert.True(範囲状態.行一覧[1].UI選択中か);
            Assert.False(範囲状態.行一覧[0].UI選択中か);
            Assert.True(配列状態.行一覧[0].UI選択中か);

            ビューモデル.選択ソースタブ=ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==global::UI.ベンチマークソースタブ.System配列);

            Assert.True(配列状態.行一覧[0].UI選択中か);
            Assert.False(配列状態.行一覧[1].UI選択中か);
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 選択タブとずれた行への緑クリック選択はその行のタブへ適用される() {
        // 切替保留などで表示タブとビューモデルの選択タブが食い違っても、クリックされた行が属するタブの表示状態へ適用されることを確認する。
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 配列状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列);
        var 範囲状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲);

        ビューモデル.ベンチマーク行UI選択を同期(範囲状態,[範囲状態.行一覧[1]],[]);

        Assert.True(範囲状態.行一覧[1].UI選択中か);
        Assert.All(配列状態.行一覧,static 行 => Assert.False(行.UI選択中か));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了後にもう一度実行できる() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);
        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        {
            // 完了後に計測ボタンが有効で、2回目の実行が実際に開始（CPU実行中を再通過）して完了することを確認する。
            Assert.True(ビューモデル.実行コマンド.CanExecute(null));
            var 実行中追跡 = 実行中アイコンを追跡(対象一覧);
            await ビューモデル.実行コマンド.ExecuteAsync(null);
            await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
            Assert.All(対象一覧,セル => Assert.Contains(セル,実行中追跡));
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 完了後の選択変更に実行ボタン活性が追従する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);
        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        {
            //完了後に選択を全解除するとボタン活性もライブに追従する(凍結スナップショットを見ない)ことを確認する
            ビューモデル.選択クリアコマンド.Execute(null);
            Assert.False(ビューモデル.選択ベンチマークセルありか);
            Assert.False(ビューモデル.実行コマンド.CanExecute(null));
        }
        {
            //再選択で活性が復帰し、実行が実際に走ることを確認する
            ビューモデル.全選択コマンド.Execute(null);
            Assert.True(ビューモデル.選択ベンチマークセルありか);
            Assert.True(ビューモデル.実行コマンド.CanExecute(null));
            var 再選択値 = 選択セル一覧を取得(ビューモデル);
            var 実行中追跡 = 実行中アイコンを追跡(再選択値);
            await ビューモデル.実行コマンド.ExecuteAsync(null);
            await 条件成立まで待機Async(() => 再選択値.All(最終完了か));
            Assert.Contains(ビューモデル.ベンチマーク行一覧,行 => 行.CPU表示ありか);
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task フレッシュ実行では全リスタート指定が渡る() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 対象一覧 = 選択セル一覧を取得(ビューモデル);
        await ビューモデル.実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
        {
            // 新規実行（初回）では真が渡ることを確認する。
            Assert.Equal([true],フィクスチャ.実行サービス.要求要素数から再開する指定一覧);
        }
    }

#if DEBUG
    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 一時停止後に計測すると再実行され真を渡す() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 実行タスク = ビューモデル.無限実行コマンド.ExecuteAsync(null);
        // ベンチマーク実行有効かはDB保存の待ち合わせより前に立つため、実際に偽実行サービスへ到達するまで待つ。
        await 条件成立まで待機Async(() => フィクスチャ.実行サービス.要求要素数から再開する指定一覧.Count>0);

        await ビューモデル.一時停止コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク一時停止中か);
        await 実行タスク;
        {
            //一時停止後の計測が実際に実行を開始・完了することを確認する
            Assert.True(ビューモデル.実行コマンド.CanExecute(null));
            var 対象一覧 = 選択セル一覧を取得(ビューモデル);
            var 実行中追跡 = 実行中アイコンを追跡(対象一覧);
            await ビューモデル.実行コマンド.ExecuteAsync(null);
            await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
            Assert.All(対象一覧,セル => Assert.Contains(セル,実行中追跡));
        }
        {
            // 新仕様では再開導線を使わず、計測は再実行として要求要素数から再開する指定に真が渡ることを確認する。
            Assert.Equal([true,true],フィクスチャ.実行サービス.要求要素数から再開する指定一覧);
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 停止後にもう一度実行できる() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        ビューモデル.全選択コマンド.Execute(null);
        var 実行タスク = ビューモデル.無限実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク実行有効か);

        await ビューモデル.停止コマンド.ExecuteAsync(null);
        await 実行タスク;
        {
            // 停止後に計測ボタンが有効で、再実行が実際に開始・完了することを確認する。
            Assert.True(ビューモデル.実行コマンド.CanExecute(null));
            var 対象一覧 = 選択セル一覧を取得(ビューモデル);
            var 実行中追跡 = 実行中アイコンを追跡(対象一覧);
            await ビューモデル.実行コマンド.ExecuteAsync(null);
            await 条件成立まで待機Async(() => 対象一覧.All(最終完了か));
            Assert.All(対象一覧,セル => Assert.Contains(セル,実行中追跡));
        }
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 無限実行コマンド_実行と一時停止状態に追従する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;

        Assert.False(ビューモデル.無限実行コマンド.CanExecute(null));

        ビューモデル.全選択コマンド.Execute(null);

        Assert.True(ビューモデル.無限実行コマンド.CanExecute(null));

        var 実行タスク = ビューモデル.無限実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク実行有効か);

        Assert.False(ビューモデル.無限実行コマンド.CanExecute(null));

        await ビューモデル.一時停止コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク一時停止中か);

        Assert.True(ビューモデル.無限実行コマンド.CanExecute(null));

        await ビューモデル.停止コマンド.ExecuteAsync(null);
        await 実行タスク;
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task ベンチマーク条件_実行中と一時停止中はしきい値変更を無効にする() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 初期しきい値 = ビューモデル.選択閾値;
        var 代替しきい値 = ビューモデル.閾値選択肢一覧.First(選択肢 => !ReferenceEquals(選択肢,初期しきい値));
        ビューモデル.全選択コマンド.Execute(null);

        Assert.True(ビューモデル.ベンチマーク条件を変更可能か);

        var 実行タスク = ビューモデル.無限実行コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク実行有効か);

        Assert.False(ビューモデル.ベンチマーク条件を変更可能か);
        ビューモデル.選択閾値=代替しきい値;
        Assert.Same(初期しきい値,ビューモデル.選択閾値);

        await ビューモデル.一時停止コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.ベンチマーク一時停止中か);

        Assert.False(ビューモデル.ベンチマーク条件を変更可能か);
        ビューモデル.選択閾値=代替しきい値;
        Assert.Same(初期しきい値,ビューモデル.選択閾値);

        await ビューモデル.停止コマンド.ExecuteAsync(null);
        await 実行タスク;

        Assert.True(ビューモデル.ベンチマーク条件を変更可能か);
        ビューモデル.選択閾値=代替しきい値;
        Assert.Same(代替しきい値,ビューモデル.選択閾値);
    }
#endif

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 全ベンチマーク選択コマンド_全ソースタブへ適用する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 状態一覧 = new[]{
            ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列),
            ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲),
            ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.Systemリスト),
        };

        ビューモデル.全選択コマンド.Execute(null);

        Assert.True(ビューモデル.全ベンチマーク選択状態);
        Assert.All(状態一覧,状態 => Assert.All(状態.行一覧,行 => {
            Assert.True(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.True(セル.実行予定か));
        }));

        ビューモデル.ベンチマーク行を切り替え(状態一覧[1].行一覧[0]);

        Assert.Null(ビューモデル.全ベンチマーク選択状態);

        ビューモデル.選択クリアコマンド.Execute(null);

        Assert.False(ビューモデル.全ベンチマーク選択状態);
        Assert.All(状態一覧,状態 => Assert.All(状態.行一覧,行 => {
            Assert.False(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.False(セル.実行予定か));
        }));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task ベンチマーク行選択ヘッダー_行チェックを切り替えると真偽を循環する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var 行 = フィクスチャ.ビューモデル.ベンチマーク行一覧.First(行 => 行.左セル.ケースありか&&行.右セル.ケースありか);
        行.選択中か=false;

        Assert.False(行.選択チェック状態);

        フィクスチャ.ビューモデル.ベンチマーク行を切り替え(行);

        Assert.True(行.選択チェック状態);
        Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.True(セル.実行予定か));

        フィクスチャ.ビューモデル.ベンチマーク行を切り替え(行);

        Assert.False(行.選択チェック状態);
        Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.False(セル.実行予定か));

        フィクスチャ.ビューモデル.ベンチマーク行を切り替え(行);

        Assert.True(行.選択チェック状態);
        Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.True(セル.実行予定か));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task タブベンチマーク選択ヘッダー_対象ソースタブだけを循環し他タブを選択しない() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 配列状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列);
        var 範囲状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲);
        var リスト状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.Systemリスト);
        ビューモデル.選択クリアコマンド.Execute(null);
        var 配列行 = 配列状態.行一覧.First(行 => 行.左セル.ケースありか&&行.右セル.ケースありか);
        配列行.選択中か=true;

        Assert.Null(配列状態.ベンチマーク選択状態);
        Assert.False(範囲状態.ベンチマーク選択状態);
        Assert.False(リスト状態.ベンチマーク選択状態);
        Assert.Null(ビューモデル.全ベンチマーク選択状態);

        ビューモデル.全ベンチマーク選択を切り替える(配列状態.定義);

        Assert.True(配列状態.ベンチマーク選択状態);
        Assert.False(範囲状態.ベンチマーク選択状態);
        Assert.False(リスト状態.ベンチマーク選択状態);
        Assert.Null(ビューモデル.全ベンチマーク選択状態);
        Assert.All(配列状態.行一覧,行 => {
            Assert.True(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.True(セル.実行予定か));
        });
        Assert.All(範囲状態.行一覧,行 => {
            Assert.False(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.False(セル.実行予定か));
        });

        ビューモデル.全ベンチマーク選択を切り替える(配列状態.定義);

        Assert.False(配列状態.ベンチマーク選択状態);
        Assert.False(範囲状態.ベンチマーク選択状態);
        Assert.False(リスト状態.ベンチマーク選択状態);
        Assert.False(ビューモデル.全ベンチマーク選択状態);

        ビューモデル.全ベンチマーク選択を切り替える(配列状態.定義);

        Assert.True(配列状態.ベンチマーク選択状態);
        Assert.False(範囲状態.ベンチマーク選択状態);
        Assert.False(リスト状態.ベンチマーク選択状態);
        Assert.Null(ビューモデル.全ベンチマーク選択状態);
        Assert.True(配列行.選択チェック状態);
        Assert.True(配列行.左セル.実行予定か);
        Assert.True(配列行.右セル.実行予定か);
        Assert.All(範囲状態.行一覧,行 => {
            Assert.False(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.False(セル.実行予定か));
        });
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task 全ベンチマーク選択ヘッダー_タブをまたいで混在から真と偽を循環して混在へ戻る() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 配列状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System配列);
        var 範囲状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.System範囲);
        var リスト状態 = ビューモデル.ソースタブ表示状態を取得(global::UI.ベンチマークソースタブ.Systemリスト);
        var 状態一覧 = new[] { 配列状態,範囲状態,リスト状態 };
        ビューモデル.選択クリアコマンド.Execute(null);
        var 配列行 = 配列状態.行一覧.First(行 => 行.左セル.ケースありか&&行.右セル.ケースありか);
        var 範囲行 = 範囲状態.行一覧.First(行 => 行.左セル.ケースありか&&行.右セル.ケースありか);
        配列行.選択中か=true;
        範囲行.選択中か=true;

        ビューモデル.全ベンチマーク選択を切り替える();

        Assert.True(ビューモデル.全ベンチマーク選択状態);
        Assert.All(状態一覧,状態 => Assert.All(状態.行一覧,行 => {
            Assert.True(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.True(セル.実行予定か));
        }));

        ビューモデル.全ベンチマーク選択を切り替える();

        Assert.False(ビューモデル.全ベンチマーク選択状態);
        Assert.All(状態一覧,状態 => Assert.All(状態.行一覧,行 => {
            Assert.False(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.False(セル.実行予定か));
        }));

        ビューモデル.全ベンチマーク選択を切り替える();

        Assert.True(ビューモデル.全ベンチマーク選択状態);
        Assert.True(配列行.選択チェック状態);
        Assert.True(範囲行.選択チェック状態);
        Assert.True(配列行.左セル.実行予定か);
        Assert.True(配列行.右セル.実行予定か);
        Assert.True(範囲行.左セル.実行予定か);
        Assert.True(範囲行.右セル.実行予定か);
        Assert.All(リスト状態.行一覧,行 => {
            Assert.True(行.選択チェック状態);
            Assert.All(行.プロジェクトセル一覧.Where(static セル => セル.ケースありか),セル => Assert.True(セル.実行予定か));
        });
    }

    private static void 現在タブを全選択(global::UI.ViewModels.実行ビューモデル ビューモデル) {
        foreach(var 行 in ビューモデル.ベンチマーク行一覧) {
            行.選択中か=false;
        }
        ビューモデル.全ベンチマーク選択を切り替える(ビューモデル.選択ソース定義);
    }

    private static IReadOnlyList<global::UI.ベンチマークプロジェクトセル> 選択セル一覧を取得(global::UI.ViewModels.実行ビューモデル ビューモデル) =>
        ビューモデル.ベンチマーク行一覧
            .Where(static 行 => 行.選択中か)
            .SelectMany(static 行 => 行.プロジェクトセル一覧)
            .Where(static セル => セル.ケースありか)
            .ToArray();

    private static IReadOnlyList<global::UI.ベンチマークプロジェクトセル> 選択セル一覧を取得(global::UI.ベンチマークソースタブ表示状態 状態) =>
        状態.行一覧
            .Where(static 行 => 行.選択中か)
            .SelectMany(static 行 => 行.プロジェクトセル一覧)
            .Where(static セル => セル.ケースありか)
            .ToArray();

    private static void 新規グリッドのカタログ表示を検証(global::UI.ViewModels.実行ビューモデル ビューモデル) {
        Assert.NotEmpty(ビューモデル.ベンチマーク行一覧);
        Assert.Empty(ビューモデル.結果項目一覧);
        Assert.Empty(ビューモデル.表示結果項目一覧);
        Assert.Empty(ビューモデル.結果要素数選択肢一覧);
        Assert.Empty(選択セル一覧を取得(ビューモデル));
        var セル一覧 = ビューモデル.ベンチマーク行一覧
            .SelectMany(static 行 => 行.プロジェクトセル一覧)
            .Where(static セル => セル.ケースありか)
            .ToArray();
        Assert.NotEmpty(セル一覧);
        Assert.All(セル一覧,static セル => {
            Assert.False(セル.実行予定か);
            Assert.False(セル.CPU表示ありか);
            Assert.True(string.IsNullOrEmpty(セル.状態画像ソース)||セル.状態画像ソース==global::UI.ベンチマーク状態画像名.待機中);
        });
    }

    private static void 読込行にペア測定値があることを検証(global::UI.ベンチマーク表行 行) {
        Assert.False(string.IsNullOrWhiteSpace(行.左時間文字列),"左時間が復元されていません: "+行.行キー);
        Assert.False(string.IsNullOrWhiteSpace(行.右時間文字列),"右時間が復元されていません: "+行.行キー);
        Assert.True(行.左セル.測定値.測定値ありか,"左指標が復元されていません: "+行.行キー);
        Assert.True(行.右セル.測定値.測定値ありか,"右指標が復元されていません: "+行.行キー);
    }

    private static ConcurrentDictionary<global::UI.ベンチマークプロジェクトセル,byte> 実行中アイコンを追跡(IEnumerable<global::UI.ベンチマークプロジェクトセル> セル一覧) {
        var 実行中追跡 = new ConcurrentDictionary<global::UI.ベンチマークプロジェクトセル,byte>();
        foreach(var セル in セル一覧) {
            if(セル.状態画像ソース==global::UI.ベンチマーク状態画像名.CPU実行中) {
                実行中追跡.TryAdd(セル,0);
            }
            セル.PropertyChanged+=(_,イベント引数) => {
                if(イベント引数.PropertyName==nameof(global::UI.ベンチマークプロジェクトセル.状態画像ソース)
                    &&セル.状態画像ソース==global::UI.ベンチマーク状態画像名.CPU実行中) {
                    実行中追跡.TryAdd(セル,0);
                }
            };
        }

        return 実行中追跡;
    }

    private static bool 最終完了か(global::UI.ベンチマークプロジェクトセル セル) =>
        セル.状態画像ソース==global::UI.ベンチマーク状態画像名.最終完了;

    private static bool 読込済みセルメタデータがあるか(global::UI.ベンチマークプロジェクトセル セル) =>
        !string.IsNullOrWhiteSpace(セル.要素数文字列)
        &&!string.IsNullOrWhiteSpace(セル.実行文字列)
        &&セル.CPU表示ありか
        &&!string.IsNullOrWhiteSpace(セル.論理プロセッサ文字列);

    private static string セルメタデータメッセージを作成(global::UI.ViewModels.実行ビューモデル ビューモデル,global::UI.ベンチマークプロジェクトセル セル) =>
        "セルメタデータが復元されていません。要素数='"+セル.要素数文字列
        +"' 実行='"+セル.実行文字列
        +"' CPU表示ありか='"+セル.CPU表示ありか
        +"' CPU='"+セル.論理プロセッサ文字列
        +"' 時間='"+セル.時間文字列
        +"' メモリ='"+セル.割り当て文字列
        +"' 状態='"+セル.状態画像ソース
        +"' セルプロジェクト='"+セル.プロジェクト
        +"' 行メソッド='"+(ビューモデル.ベンチマーク行一覧.FirstOrDefault(行 => 行.プロジェクトセル一覧.Contains(セル))?.メソッド名??"-")
        +"' 行型='"+(ビューモデル.ベンチマーク行一覧.FirstOrDefault(行 => 行.プロジェクトセル一覧.Contains(セル))?.ベンチマーク型文字列??"-")
        +"' 行キー='"+(ビューモデル.ベンチマーク行一覧.FirstOrDefault(行 => 行.プロジェクトセル一覧.Contains(セル))?.行キー??"-")
        +"' 行左時間='"+(ビューモデル.ベンチマーク行一覧.FirstOrDefault(行 => 行.プロジェクトセル一覧.Contains(セル))?.左時間文字列??"-")
        +"' 行右時間='"+(ビューモデル.ベンチマーク行一覧.FirstOrDefault(行 => 行.プロジェクトセル一覧.Contains(セル))?.右時間文字列??"-")
        +"' 結果一覧='"+string.Join(";",ビューモデル.結果項目一覧.Take(8).Select(static 項目 => 項目.プロジェクト+"/"+項目.メソッド+"/状態="+項目.状態+"/N="+項目.軸数+"/CPU="+(項目.割り当て論理CPU?.ToString()??"-")+"/平均="+(項目.平均ナノ秒?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-")))+"'";

    private static async Task<global::UI.ベンチマークUIスナップショット> パイプラインスナップショットを待機Async(global::UI.ベンチマーク実行イベントパイプライン パイプライン) {
        var 指標 = new global::UI.ベンチマークパイプライン指標(0,0,0,0,0,0,0,0,0,0,0,0,0,0);
        for(var インデックス = 0;インデックス<50;インデックス++) {
            if(パイプライン.UIスナップショットを作成できるか(指標,out var スナップショット)) {
                return スナップショット;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("パイプラインスナップショットが生成されませんでした。");
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public async Task ベンチマークグリッド_型列を削除して列幅を再配分する() {
        await using var フィクスチャ = await ビューモデルフィクスチャ.作成Async(global::UI.ベンチマークソースタブ.System配列);
        var ビューモデル = フィクスチャ.ビューモデル;
        var 固定列定義取得メソッド = typeof(global::UI.MainPage).GetMethod(
            "ベンチマーク固定列UI一覧を取得",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(固定列定義取得メソッド);
        var 固定列定義一覧 = Assert.IsAssignableFrom<System.Collections.IEnumerable>(固定列定義取得メソッド.Invoke(null,null))
            .Cast<object>()
            .Select(static 定義 => {
                var 型 = 定義.GetType();
                var 見出し = Assert.IsType<string>(型.GetProperty("見出し")!.GetValue(定義));
                var 幅 = Assert.IsType<double>(型.GetProperty("幅")!.GetValue(定義));
                return (見出し,幅);
            })
            .ToArray();
        Assert.DoesNotContain(固定列定義一覧,static 定義 => 定義.見出し=="型");
        var ベンチマーク列 = Assert.Single(固定列定義一覧,static 定義 => 定義.見出し=="ベンチマーク");
        Assert.Equal(320d,ベンチマーク列.幅);
        var 日時列幅 = typeof(global::UI.MainPage).GetField(
            "ベンチマーク_日時",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(日時列幅);
        Assert.Equal(132d,Assert.IsType<double>(日時列幅.GetRawConstantValue()));

        Assert.DoesNotContain(
            ビューモデル.ベンチマークソート列選択肢一覧,
            static 選択肢 => 選択肢.列==global::UI.ベンチマークグリッドソート列.ベンチマーク型);
        ビューモデル.ベンチマークソート規則をリセット();
        Assert.Collection(
            ビューモデル.ベンチマークソート規則一覧,
            static 規則 => Assert.Equal(global::UI.ベンチマークグリッドソート列.ベンチマークメソッド,規則.列));
        var 有効ソート規則作成メソッド = typeof(global::UI.ViewModels.実行ビューモデル).GetMethod(
            "有効ソート規則を作成",
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(有効ソート規則作成メソッド);
        var 復元ソート規則一覧 = Assert.IsAssignableFrom<IReadOnlyList<global::UI.ベンチマークソート規則>>(
            有効ソート規則作成メソッド.Invoke(ビューモデル,[new[] {
                new global::UI.ベンチマークソート規則(global::UI.ベンチマークグリッドソート列.ベンチマーク型),
                new global::UI.ベンチマークソート規則(global::UI.ベンチマークグリッドソート列.ベンチマークメソッド),
            }]));
        Assert.Collection(
            復元ソート規則一覧,
            static 規則 => Assert.Equal(global::UI.ベンチマークグリッドソート列.ベンチマークメソッド,規則.列));
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマークソート値_シェアは丸め前の左値で比較し欠測は常に末尾になる() {
        var 丸め前高値 = global::UI.ベンチマークソート値.シェアから作成(global::UI.シェア値.メモリから作成(995,5));
        var 丸め前低値 = global::UI.ベンチマークソート値.シェアから作成(global::UI.シェア値.メモリから作成(994,6));

        Assert.Equal(0.995,丸め前高値.数値!.Value,12);
        Assert.True(global::UI.ベンチマークソート値.比較(丸め前低値,丸め前高値,false)<0);
        Assert.True(global::UI.ベンチマークソート値.比較(global::UI.ベンチマークソート値.欠測,丸め前高値,false)>0);
        Assert.True(global::UI.ベンチマークソート値.比較(global::UI.ベンチマークソート値.欠測,丸め前高値,true)>0);
    }

    [Fact, Trait("Category","BenchmarkUI"), Trait("Source","Pattern")]
    public void ベンチマーク表行_日時とループ回数を新契約で表示しソートする() {
        var 行 = 配列ベンチマーク行を作成("Any");
        var ソースタブ = 行.ソースタブ;
        var 旧日時 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var 新日時 = DateTimeOffset.UtcNow;
        行.スナップショット結果を適用(ソースタブ.左プロジェクト,配列結果を作成(ソースタブ.左プロジェクト,"Any",global::UI.ジョブ状態.完了,0,2),0,旧日時);
        行.スナップショット結果を適用(ソースタブ.右プロジェクト,配列結果を作成(ソースタブ.右プロジェクト,"Any",global::UI.ジョブ状態.完了,1,4),0,新日時);

        Assert.Equal("4",行.ループ回数文字列);
        Assert.Equal(4,行.行ループ回数);
        Assert.Equal(4,行.ソート値を取得(global::UI.ベンチマークグリッドソート列.ループ回数).数値);
        Assert.Equal(旧日時.ToUnixTimeMilliseconds(),行.ソート値を取得(global::UI.ベンチマークグリッドソート列.左日時).数値);
        Assert.Equal(新日時.ToUnixTimeMilliseconds(),行.ソート値を取得(global::UI.ベンチマークグリッドソート列.右日時).数値);
    }

    private static global::UI.ベンチマーク表行 配列ベンチマーク行を作成(string メソッド名) {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ケース一覧 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,配列型完全名を作成(ソースタブ.左プロジェクト),メソッド名),
            [ソースタブ.右プロジェクト]=new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,配列型完全名を作成(ソースタブ.右プロジェクト),メソッド名),
        };
        return new global::UI.ベンチマーク表行("int|"+メソッド名,メソッド名,メソッド名,ソースタブ,ケース一覧,"int");
    }

    private static global::UI.ベンチマーク結果項目 配列結果を作成(
        string プロジェクト,
        string メソッド名,
        global::UI.ジョブ状態 状態,
        int 実行項目インデックス,
        int ループ回数 = global::UI.ベンチマーク作業軸.既定ループ回数) {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 現在日時 = DateTimeOffset.UtcNow;
        return new global::UI.ベンチマーク結果項目(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            アセンブリ名=ソースタブ.アセンブリ名を取得(プロジェクト),
            型完全名=配列型完全名を作成(プロジェクト),
            メソッド名=メソッド名,
            ソースタブ=ソースタブ.タブ,
            要素数=4096,
            ループ回数=ループ回数,
            状態=状態,
            割り当て論理CPU=0,
            開始日時=現在日時,
            終了日時=状態==global::UI.ジョブ状態.完了 ? 現在日時 : null,
            平均ナノ秒=状態==global::UI.ジョブ状態.完了 ? 1_000_000 : null,
            割り当てバイト数=状態==global::UI.ジョブ状態.完了 ? 1024 : null,
            終了コード=状態==global::UI.ジョブ状態.完了 ? 0 : null,
            実行項目インデックス=実行項目インデックス,
        });
    }

    private static string 配列型完全名を作成(string プロジェクト) =>
        プロジェクト.Contains("ZLinq",StringComparison.OrdinalIgnoreCase)
            ? "Benchmark.System.Linq.Enumerable.ZLinq.ZLinqInt32"
            : "Benchmark.System.Linq.Enumerable.FLinq.FLinqInt32";

    private static async Task 今回実行を保存Async(global::UI.ViewModels.実行ビューモデル ビューモデル) {
        if(ビューモデル.選択実行選択肢 is { UI実行ID:>0 }&&!ビューモデル.更新中か&&ビューモデル.実行履歴を選択可能か) {
            return;
        }

        await 条件成立まで待機Async(() => ビューモデル.DB保存可能か&&ビューモデル.DB保存コマンド.CanExecute(null));
        await ビューモデル.DB保存コマンド.ExecuteAsync(null);
        await 条件成立まで待機Async(() => ビューモデル.選択実行選択肢 is { UI実行ID:>0 }
            &&!ビューモデル.更新中か
            &&ビューモデル.実行履歴を選択可能か);
    }

    private static async Task 条件成立まで待機Async(Func<bool> 条件) {
        for(var インデックス = 0;インデックス<60;インデックス++) {
            if(条件()) {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(条件());
    }

    private static async Task 条件成立まで待機または続行Async(Func<bool> 条件) {
        for(var インデックス = 0;インデックス<60;インデックス++) {
            if(条件()) {
                return;
            }

            await Task.Delay(50);
        }
    }

    private sealed class ビューモデルフィクスチャ:IAsyncDisposable {
        private readonly string ストアパス;
        private readonly string ベンチマークルート;
        private int 破棄済みか;

        private ビューモデルフィクスチャ(
            global::UI.ViewModels.実行ビューモデル ビューモデル,
            偽実行サービス 実行サービス,
            global::UI.Storage.UI実行ストア ストア,
            string ストアパス,
            string ベンチマークルート) {
            this.ビューモデル=ビューモデル;
            this.実行サービス=実行サービス;
            this.ストア=ストア;
            this.ストアパス=ストアパス;
            this.ベンチマークルート=ベンチマークルート;
        }

        public global::UI.ViewModels.実行ビューモデル ビューモデル { get; }
        public 偽実行サービス 実行サービス { get; }
        public global::UI.Storage.UI実行ストア ストア { get; }

        public static async Task<ビューモデルフィクスチャ> 作成Async(global::UI.ベンチマークソースタブ ソースタブ,Action<global::UI.Storage.UI実行ストア>? ストア構成処理 = null) {
            var ケース一覧 = カタログケース一覧を作成();
            var ストアパス = Path.Combine(Path.GetTempPath(),"Seamless-Test-Benchmark-UI-"+Guid.NewGuid().ToString("N")+".db");
            var ストア = new global::UI.Storage.UI実行ストア(ストアパス);
            ストア.スキーマを確認();
            ストア構成処理?.Invoke(ストア);
            var 実行サービス = new 偽実行サービス(ケース一覧);
            var ベンチマークルート = Path.Combine(Path.GetTempPath(),"Seamless-Test-Benchmark-UI-"+Guid.NewGuid().ToString("N"));
            var ビューモデル = new global::UI.ViewModels.実行ビューモデル(
                new 偽カタログサービス(ケース一覧),
                new 偽CPUトポロジーサービス(),
                実行サービス,
                new global::UI.ベンチマーク比較サービス(new global::UI.ベンチマーク結果サービス()),
                ストア,
                ベンチマークルート,
                new 即時ディスパッチャー(),
                new メモリ内設定(),
                false);
            await ビューモデル.初期化Async();
            ビューモデル.選択ソースタブ=ビューモデル.ソースタブ一覧.First(選択肢 => 選択肢.定義.タブ==ソースタブ);
            ビューモデル.選択実行プロファイル=ビューモデル.実行プロファイル一覧.FirstOrDefault(選択肢 => 選択肢.プロファイル==global::UI.ベンチマーク実行プロファイル.テスト)
                ??ビューモデル.実行プロファイル一覧.First();
            ビューモデル.選択閾値=ビューモデル.閾値選択肢一覧.First(選択肢 => 選択肢.ミリ秒==1);
            ビューモデル.選択クリアコマンド.Execute(null);
            return new(ビューモデル,実行サービス,ストア,ストアパス,ベンチマークルート);
        }

        public async ValueTask DisposeAsync() {
            if(Interlocked.Exchange(ref this.破棄済みか,1)==1) {
                return;
            }

            await this.ビューモデル.終了準備Async();
            global::Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await 一時ファイルを削除Async(this.ストアパス);
            await 一時ファイルを削除Async(this.ストアパス+"-wal");
            await 一時ファイルを削除Async(this.ストアパス+"-shm");
            await 一時ディレクトリを削除Async(this.ベンチマークルート);
        }

        private static async Task 一時ファイルを削除Async(string パス) {
            var 絶対パス = 一時パスを検証(パス);
            await 一時パスを削除Async(() => File.Delete(絶対パス));
        }

        private static async Task 一時ディレクトリを削除Async(string パス) {
            var 絶対パス = 一時パスを検証(パス);
            await 一時パスを削除Async(() => {
                if(Directory.Exists(絶対パス)) {
                    Directory.Delete(絶対パス,true);
                }
            });
        }

        private static async Task 一時パスを削除Async(Action 削除処理) {
            const int 最大試行回数 = 40;
            for(var 試行 = 1;試行<=最大試行回数;試行++) {
                try {
                    削除処理();
                    return;
                } catch(IOException) when(試行<最大試行回数) {
                    global::Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    await Task.Delay(50);
                }
            }
        }

        private static string 一時パスを検証(string パス) {
            var 絶対パス = Path.GetFullPath(パス);
            var 一時ルート = Path.GetFullPath(Path.GetTempPath());
            if(!絶対パス.StartsWith(一時ルート,StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("テスト一時パスが一時ディレクトリ外です: "+絶対パス);
            }

            return 絶対パス;
        }

        private static IReadOnlyList<global::UI.ベンチマークケース> カタログケース一覧を作成() {
            var 配列定義 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
            var 列挙定義 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System範囲);
            var リスト定義 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.Systemリスト);
            return [
                ケースを作成(配列定義.左プロジェクト,配列定義.左アセンブリ名,"Benchmark.System.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                ケースを作成(配列定義.右プロジェクト,配列定義.右アセンブリ名,"Benchmark.System.Linq.Enumerable.ZLinqInt32","AggregateBy_keySelector"),
                ケースを作成(配列定義.左プロジェクト,配列定義.左アセンブリ名,"Benchmark.System.Linq.Enumerable.FLinqInt32","Select_selector"),
                ケースを作成(配列定義.右プロジェクト,配列定義.右アセンブリ名,"Benchmark.System.Linq.Enumerable.ZLinqInt32","Select_selector"),
                ケースを作成(列挙定義.左プロジェクト,列挙定義.左アセンブリ名,"Benchmark.System.Linq.Enumerable.Range.FLinqInt32","AggregateBy_keySelector"),
                ケースを作成(列挙定義.右プロジェクト,列挙定義.右アセンブリ名,"Benchmark.System.Linq.Enumerable.Range.ZLinqInt32","AggregateBy_keySelector"),
                ケースを作成(列挙定義.左プロジェクト,列挙定義.左アセンブリ名,"Benchmark.System.Linq.Enumerable.Range.FLinqInt32","Select_selector"),
                ケースを作成(列挙定義.右プロジェクト,列挙定義.右アセンブリ名,"Benchmark.System.Linq.Enumerable.Range.ZLinqInt32","Select_selector"),
                ケースを作成(リスト定義.左プロジェクト,リスト定義.左アセンブリ名,"Benchmark.System.Linq.Enumerable.List.FLinqInt32","AggregateBy_keySelector"),
                ケースを作成(リスト定義.右プロジェクト,リスト定義.右アセンブリ名,"Benchmark.System.Linq.Enumerable.List.ZLinqInt32","AggregateBy_keySelector"),
                ケースを作成(リスト定義.左プロジェクト,リスト定義.左アセンブリ名,"Benchmark.System.Linq.Enumerable.List.FLinqInt32","Select_selector"),
                ケースを作成(リスト定義.右プロジェクト,リスト定義.右アセンブリ名,"Benchmark.System.Linq.Enumerable.List.ZLinqInt32","Select_selector"),
            ];
        }

        private static global::UI.ベンチマークケース ケースを作成(string プロジェクト,string アセンブリ名,string 型完全名,string メソッド名) =>
            new(プロジェクト,アセンブリ名,型完全名,メソッド名);
    }

    private sealed class 偽カタログサービス(IReadOnlyList<global::UI.ベンチマークケース> ケース一覧):global::UI.Iベンチマークカタログサービス {
        public Task<IReadOnlyList<global::UI.ベンチマークケース>> 読込Async(CancellationToken キャンセルトークン) =>
            Task.FromResult(ケース一覧);
    }

    private sealed class 偽CPUトポロジーサービス:global::UI.ICPUトポロジーサービス {
        public IReadOnlyList<global::UI.CPU選択項目> 読み込む() => [
            new(0,global::UI.CPUコア種別.高性能,0,0,true),
        ];
    }

    private sealed class 偽実行サービス(IReadOnlyList<global::UI.ベンチマークケース> 全ケース一覧):global::UI.Iベンチマーク実行サービス {
        public List<bool> 要求要素数から再開する指定一覧 { get; } = [];
        public List<int> 観測ベンチマーク数一覧 { get; } = [];
        public bool 全要素数を送信するか { get; set; }

        public async Task<global::UI.ベンチマーク実行報告> 実行Async(
            IReadOnlyList<global::UI.ベンチマークケース> ベンチマーク一覧,
            IReadOnlyList<int> 論理CPU一覧,
            Func<IReadOnlyList<int>> 論理CPU取得器,
            IReadOnlyList<int> 要素数一覧,
            global::UI.ベンチマークソースタブ ソースタブ,
            global::UI.ベンチマーク実行プロファイル 実行プロファイル,
            TimeSpan 適応しきい値,
            bool 要求要素数から再開するか,
            string? 実行ルート上書き,
            Func<IReadOnlyList<global::UI.ベンチマークケース>>? ベンチマーク取得器,
            Action<global::UI.ベンチマーク結果項目> 結果受信時,
            Action<string> 状態変更時,
            global::UI.ベンチマーク実行オプション? 実行オプション,
            CancellationToken キャンセルトークン) {
            this.要求要素数から再開する指定一覧.Add(要求要素数から再開するか);
            this.観測ベンチマーク数一覧.Add(ベンチマーク一覧.Count);
            var 送信ケース一覧 = this.送信ケース一覧を作成(ベンチマーク一覧,ソースタブ);
            var 論理CPU = 論理CPU一覧.FirstOrDefault();
            var 準備済み要素数一覧 = this.全要素数を送信するか&&要素数一覧.Count>0 ? 要素数一覧.ToArray() : [1];
            var 送信要素数一覧 = 準備済み要素数一覧;
            foreach(var 要素数 in 送信要素数一覧) {
                foreach(var ベンチマーク in 送信ケース一覧) {
                    var ベンチマークソースタブ = ソースタブを解決(ベンチマーク,ソースタブ);
                    キャンセルトークン.ThrowIfCancellationRequested();
                    結果受信時(結果を作成(ベンチマーク,ベンチマークソースタブ,global::UI.ジョブ状態.実行中,論理CPU,null,要素数,準備済み要素数一覧));
                    if(実行オプション?.無限テスト実行器を使うか==true) {
                        await Task.Delay(Timeout.InfiniteTimeSpan,キャンセルトークン);
                    }
                    結果受信時(結果を作成(ベンチマーク,ベンチマークソースタブ,global::UI.ジョブ状態.完了,論理CPU,適応しきい値.TotalMilliseconds*1_000_000,要素数,準備済み要素数一覧));
                }
            }
            状態変更時("Completed");
            return new global::UI.ベンチマーク実行報告(実行ルート上書き);
        }

        private IReadOnlyList<global::UI.ベンチマークケース> 送信ケース一覧を作成(
            IReadOnlyList<global::UI.ベンチマークケース> 選択一覧,
            global::UI.ベンチマークソースタブ ソースタブ) {
            var ソース定義 = global::UI.ベンチマークソースタブ一覧.取得(ソースタブ);
            var 対応表 = new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase);
            foreach(var ベンチマーク in 選択一覧) {
                追加(ベンチマーク);
                var ペアプロジェクト = string.Equals(ベンチマーク.プロジェクト,ソース定義.左プロジェクト,StringComparison.OrdinalIgnoreCase)
                    ? ソース定義.右プロジェクト
                    : ソース定義.左プロジェクト;
                var ペア = 全ケース一覧.FirstOrDefault(候補 =>
                    string.Equals(候補.プロジェクト,ペアプロジェクト,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(候補.メソッド名,ベンチマーク.メソッド名,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(候補.引数キー,ベンチマーク.引数キー,StringComparison.OrdinalIgnoreCase));
                if(ペア is not null) {
                    追加(ペア);
                }
            }

            return 対応表.Values.ToArray();

            void 追加(global::UI.ベンチマークケース ベンチマーク) {
                対応表.TryAdd(ベンチマーク.プロジェクト+"|"+ベンチマーク.型完全名+"|"+ベンチマーク.メソッド名+"|"+ベンチマーク.引数キー,ベンチマーク);
            }
        }

        private static global::UI.ベンチマークソースタブ ソースタブを解決(global::UI.ベンチマークケース ベンチマーク,global::UI.ベンチマークソースタブ フォールバック) =>
            global::UI.ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名)?.タブ??フォールバック;

        private static global::UI.ベンチマーク結果項目 結果を作成(
            global::UI.ベンチマークケース ベンチマーク,
            global::UI.ベンチマークソースタブ ソースタブ,
            global::UI.ジョブ状態 状態,
            int 論理CPU,
            double? 平均ナノ秒,
            int 要素数 = 1,
            IReadOnlyList<int>? 準備済み要素数一覧 = null) {
            var ジョブ = new global::UI.ベンチマークジョブ {
                プロジェクト=ベンチマーク.プロジェクト,
                アセンブリ名=ベンチマーク.アセンブリ名,
                型完全名=ベンチマーク.型完全名,
                メソッド名=ベンチマーク.メソッド名,
                引数キー=ベンチマーク.引数キー,
                引数一覧=ベンチマーク.引数一覧.ToList(),
                ソースタブ=ソースタブ,
                要素数=global::UI.ベンチマーク作業軸.要素数を解決(ソースタブ,要素数),
                ループ回数=global::UI.ベンチマーク作業軸.ループ回数を解決(ソースタブ,要素数),
                準備済み要素数一覧=準備済み要素数一覧?.ToList()?? [要素数],
                状態=状態,
                割り当て論理CPU=論理CPU,
                開始日時=DateTimeOffset.Now,
                終了日時=状態==global::UI.ジョブ状態.完了 ? DateTimeOffset.Now : null,
                平均ナノ秒=平均ナノ秒,
                割り当てバイト数=状態==global::UI.ジョブ状態.完了 ? 0 : null,
                終了コード=状態==global::UI.ジョブ状態.完了 ? 0 : null,
                結果ダイジェスト=状態==global::UI.ジョブ状態.完了 ? "test" : string.Empty,
                結果確認文字列=状態==global::UI.ジョブ状態.完了 ? "OK" : string.Empty,
                バインド行インデックス=ベンチマーク.バインド行インデックス,
            };
            return new global::UI.ベンチマーク結果項目(ジョブ);
        }
    }

    private sealed class 即時ディスパッチャー:global::UI.IベンチマークUIディスパッチャー {
        private readonly object 排他 = new();

        public void 呼び出し開始(Action 処理) {
            lock(this.排他) {
                処理();
            }
        }
    }

    private sealed class メモリ内設定:global::UI.IベンチマークUI設定 {
        private readonly Dictionary<string,string> 値一覧 = [with(StringComparer.OrdinalIgnoreCase)];

        public string 取得(string キー,string 既定値) =>
            this.値一覧.TryGetValue(キー,out var 値) ? 値 : 既定値;

        public void 設定(string キー,string 値) => this.値一覧[キー]=値;
    }
}
#endif
