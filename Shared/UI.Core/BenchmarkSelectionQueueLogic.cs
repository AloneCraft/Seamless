namespace UI;

/// <summary>
/// チェック済み行辞書から実行キュー対象のベンチマークを組み立てる純ロジック。
/// 行キーは「タブID|行キー」で、選択行の左右両方に存在するケースを実行対象にする。
/// </summary>
[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク選択キューロジック{
    public static string 行選択キーを作成(ベンチマークソースタブ定義 ソースタブ,string 行キー)=>
        ソースタブ.ID+"|"+行キー;

    public static IEnumerable<ベンチマークケース>選択ケースを作成(
        IReadOnlyList<ベンチマークケース>カタログケース一覧,
        ベンチマークソースタブ定義 ソースタブ,
        IReadOnlyDictionary<string,bool>行選択一覧){
        return ベンチマーク行グループ化ロジック.行を作成(カタログケース一覧,ソースタブ)
            .Where(行=>行.プロジェクト別ケース.Count>0
                &&行選択一覧.TryGetValue(行選択キーを作成(ソースタブ,行.行キー),out var 選択中か)
                &&選択中か)
            .SelectMany(行=>ソースタブ.プロジェクト順
                .Where(プロジェクト=>行.プロジェクト別ケース.ContainsKey(プロジェクト))
                .Select(プロジェクト=>行.プロジェクト別ケース[プロジェクト]));
    }

    public static IReadOnlyList<ベンチマークケース>全タブから選択ケースを作成(
        IReadOnlyList<ベンチマークケース>カタログケース一覧,
        IEnumerable<ベンチマークソースタブ定義>ソースタブ一覧,
        IReadOnlyDictionary<string,bool>行選択一覧,
        Func<ベンチマークソースタブ定義,IEnumerable<ベンチマークケース>?>?上書きケース取得器=null){
        return ソースタブ一覧
            .SelectMany(ソースタブ=>
                上書きケース取得器?.Invoke(ソースタブ)
                ??選択ケースを作成(カタログケース一覧,ソースタブ,行選択一覧))
            .GroupBy(
                static ベンチマーク=>string.Join("|",ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー),
                StringComparer.OrdinalIgnoreCase)
            .Select(static グループ=>グループ.First())
            .ToArray();
    }
}
