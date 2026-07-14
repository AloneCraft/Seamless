namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class ベンチマーク実行投影サービス{
    public IReadOnlyList<ベンチマーク結果項目>表示結果を取得(
        IEnumerable<ベンチマーク結果項目>結果一覧,
        int?選択要素数)=>
        結果一覧
            .Where(項目=>選択要素数 is null||項目.軸数==0||項目.軸数==選択要素数)
            .ToArray();

    public IReadOnlyList<ベンチマーク比較行>要素数の比較行を構築(
        IReadOnlyList<ベンチマーク結果項目>セッション結果一覧,
        ベンチマークソースタブ定義 ソースタブ,
        int 要素数,
        Func<ベンチマークソースタブ定義,IReadOnlyList<ベンチマーク結果項目>,報告行[]>報告行作成器){
        var 元結果一覧=セッション結果一覧
            .Where(結果=>結果.軸数==要素数
                &&結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return 比較行を構築(元結果一覧,ソースタブ,報告行作成器);
    }

    public IReadOnlyList<ベンチマーク比較行>最新比較行を構築(
        IReadOnlyList<ベンチマーク結果項目>セッション結果一覧,
        ベンチマークソースタブ定義 ソースタブ,
        Func<ベンチマークソースタブ定義,IReadOnlyList<ベンチマーク結果項目>,報告行[]>報告行作成器){
        var 元結果一覧=セッション結果一覧
            .Where(結果=>結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase))
            .GroupBy(static 結果=>結果.メソッド+"|"+結果.型完全名+"|"+結果.引数キー,StringComparer.OrdinalIgnoreCase)
            .SelectMany(グループ=>最新要素数結果を選択(グループ,ソースタブ))
            .ToArray();
        return 比較行を構築(元結果一覧,ソースタブ,報告行作成器);
    }

    public IReadOnlyList<ベンチマーク比較行>要素数の表示行を構築(
        IEnumerable<ベンチマーク結果項目>結果一覧,
        ベンチマークソースタブ定義 ソースタブ,
        int 要素数,
        Func<ベンチマークソースタブ定義,IReadOnlyList<ベンチマーク結果項目>,報告行[]>報告行作成器){
        var 元結果一覧=結果一覧
            .Where(結果=>結果.軸数==要素数
                &&結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return 比較行を構築(元結果一覧,ソースタブ,報告行作成器);
    }

    public IReadOnlyList<ベンチマーク比較行>表示行を構築(
        IEnumerable<ベンチマーク結果項目>結果一覧,
        ベンチマークソースタブ定義 ソースタブ,
        Func<ベンチマークソースタブ定義,IReadOnlyList<ベンチマーク結果項目>,報告行[]>報告行作成器){
        var 元結果一覧=結果一覧
            .Where(結果=>結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return 比較行を構築(元結果一覧,ソースタブ,報告行作成器);
    }

    public IReadOnlyList<ベンチマーク比較行>選択要素数の表示行を構築(
        IReadOnlyList<ベンチマーク結果項目>結果一覧,
        ベンチマークソースタブ定義 ソースタブ,
        int?選択要素数,
        Func<ベンチマークソースタブ定義,IReadOnlyList<ベンチマーク結果項目>,報告行[]>報告行作成器){
        if(選択要素数 is not { } 表示要素数){
            return this.表示行を構築(結果一覧,ソースタブ,報告行作成器);
        }

        // 適応的ループ回数では行ごとに終端軸が異なる。選択軸の計測があればそれを、
        // 無ければ同じメソッド・型・引数の各プロジェクト最新計測を採用する。
        var 元結果一覧=結果一覧
            .Where(結果=>結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase))
            .GroupBy(static 結果=>結果.メソッド+"|"+結果.型完全名+"|"+結果.引数キー,StringComparer.OrdinalIgnoreCase)
            .SelectMany(グループ=>優先要素数結果を選択(グループ,ソースタブ,表示要素数))
            .ToArray();
        return 比較行を構築(元結果一覧,ソースタブ,報告行作成器);
    }

    private static IReadOnlyList<ベンチマーク比較行>比較行を構築(
        IReadOnlyList<ベンチマーク結果項目>元結果一覧,
        ベンチマークソースタブ定義 ソースタブ,
        Func<ベンチマークソースタブ定義,IReadOnlyList<ベンチマーク結果項目>,報告行[]>報告行作成器){
        var 行一覧=報告行作成器(ソースタブ,元結果一覧);
        if(行一覧.Length==0){
            return [];
        }
        return ベンチマーク比較フォーマッター.比較行を構築(行一覧,ソースタブ);
    }

    private static IReadOnlyList<ベンチマーク結果項目>優先要素数結果を選択(
        IGrouping<string,ベンチマーク結果項目>メソッドグループ,
        ベンチマークソースタブ定義 ソースタブ,
        int 優先要素数)=>
        プロジェクト別結果を選択(メソッドグループ,ソースタブ,優先要素数);

    private static IReadOnlyList<ベンチマーク結果項目>最新要素数結果を選択(
        IGrouping<string,ベンチマーク結果項目>メソッドグループ,
        ベンチマークソースタブ定義 ソースタブ)=>
        プロジェクト別結果を選択(メソッドグループ,ソースタブ,null);

    // プロジェクトごとに独立して選ぶ。高速・適応では打ち切り軸が異なるため、
    // 両プロジェクトの軸一致を要求すると片側が脱落し時間・メモリシェアが欠測になる。
    private static IReadOnlyList<ベンチマーク結果項目>プロジェクト別結果を選択(
        IGrouping<string,ベンチマーク結果項目>メソッドグループ,
        ベンチマークソースタブ定義 ソースタブ,
        int?優先要素数){
        return ソースタブ.プロジェクト順
            .Select(プロジェクト=>{
                var プロジェクト結果一覧=メソッドグループ.Where(結果=>string.Equals(結果.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase));
                if(優先要素数 is { } 優先値){
                    var 優先結果=プロジェクト結果一覧
                        .Where(結果=>結果.軸数==優先値)
                        .OrderByDescending(static 結果=>結果.開始日時??DateTimeOffset.MinValue)
                        .FirstOrDefault();
                    if(優先結果 is not null){
                        return 優先結果;
                    }
                }

                return プロジェクト結果一覧
                    .OrderByDescending(static 結果=>結果.開始日時??DateTimeOffset.MinValue)
                    .FirstOrDefault();
            })
            .Where(static 結果=>結果 is not null)
            .Select(static 結果=>結果!)
            .ToArray();
    }
}
