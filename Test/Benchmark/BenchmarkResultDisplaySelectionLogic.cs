using UI;

namespace Test.Benchmark;

public sealed class ベンチマーク結果表示選択ロジック {
    private static readonly ベンチマークソースタブ定義 ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
    private const string 型完全名 = "Benchmark.System.Linq.Enumerable.FLinq.Sample";

    [Fact]
    public void 結果を選択中か() {
        var 結果 = 結果を作成(ソースタブ.左プロジェクト,"Aggregate",型完全名,"Count=1");
        {
            //選択中の一致セルだけ表示対象にする
            Assert.True(global::UI.ベンチマーク結果表示選択ロジック.結果を選択中か(
                結果,
                [
                    new ベンチマーク結果選択セル(ソースタブ.左プロジェクト,"Aggregate","Sample","Count=1",true,true),
                    new ベンチマーク結果選択セル(ソースタブ.右プロジェクト,"Aggregate",型完全名,"Count=1",true,false),
                ]));
        }
        {
            //完了済み結果でも現在未選択なら表示対象にしない
            Assert.False(global::UI.ベンチマーク結果表示選択ロジック.結果を選択中か(
                結果,
                [
                    new ベンチマーク結果選択セル(ソースタブ.左プロジェクト,"Aggregate","Sample","Count=1",true,false),
                ]));
        }
        {
            //別引数の結果を混ぜない
            Assert.False(global::UI.ベンチマーク結果表示選択ロジック.結果を選択中か(
                結果,
                [
                    new ベンチマーク結果選択セル(ソースタブ.左プロジェクト,"Aggregate","Sample","Count=2",true,true),
                ]));
        }
    }

    [Fact]
    public void 仕様確認01() {
        var 結果 = 結果を作成(ソースタブ.左プロジェクト,"Aggregate",型完全名,"Count=1");
        ベンチマーク結果選択セル[] 選択済み = [
            new(ソースタブ.左プロジェクト,"Aggregate","Sample","Count=1",true,true),
        ];
        ベンチマーク結果選択セル[] 未選択 = [
            new(ソースタブ.左プロジェクト,"Aggregate","Sample","Count=1",true,false),
        ];

        Assert.True(global::UI.ベンチマーク結果表示選択ロジック.結果を選択中か(結果,選択済み));
        Assert.False(global::UI.ベンチマーク結果表示選択ロジック.結果を選択中か(結果,未選択));
        Assert.True(global::UI.ベンチマーク結果表示選択ロジック.結果を選択中か(結果,選択済み));
    }

    private static ベンチマーク結果項目 結果を作成(string プロジェクト,string メソッド,string 型完全名,string 引数キー) =>
        new(new ベンチマークジョブ {
            プロジェクト=プロジェクト,
            アセンブリ名=ソースタブ.アセンブリ名を取得(プロジェクト),
            メソッド名=メソッド,
            型完全名=型完全名,
            引数キー=引数キー,
            ソースタブ=ベンチマークソースタブ.System配列,
            要素数=4096,
            ループ回数=1,
            状態=ジョブ状態.完了,
            平均ナノ秒=100,
            割り当てバイト数=10,
            終了コード=0,
        });
}
