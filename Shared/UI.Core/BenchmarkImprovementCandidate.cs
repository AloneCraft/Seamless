using System.Globalization;
using System.Text;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク改善候補ビルダー{
    public const int 既定自動選択数=10;

    public static IReadOnlyList<ベンチマーク改善候補>候補を構築(進行状態 状態){
        return 候補を構築([状態]);
    }

    public static IReadOnlyList<ベンチマーク改善候補>候補を構築(IEnumerable<進行状態>状態一覧){
        var 項目一覧=状態一覧.SelectMany(項目を作成).ToArray();
        return 候補を構築(項目一覧);
    }

    public static IReadOnlyList<ベンチマーク改善候補>比較行から候補を構築(
        IEnumerable<ベンチマーク比較行>行一覧,
        int 要素数){
        var 項目一覧=行一覧
            .Where(static 行=>行.ソースタブ is not null)
            .SelectMany(行=>(行.比較一覧.Count>0 ? 行.比較一覧 : [行.左右シェア])
                .Select(比較=>new ベンチマーク改善項目(
                    行.ソースタブ,
                    ベンチマーク改善カテゴリ分類器.分類(行.メソッド名),
                    要素数,
                    行,
                    比較)))
            .ToArray();
        return 候補を構築(項目一覧);
    }

    private static IReadOnlyList<ベンチマーク改善候補>候補を構築(IReadOnlyList<ベンチマーク改善項目>項目一覧){
        return 項目一覧
            .GroupBy(static 項目=>候補キーを作成(項目.ソースタブ,項目.カテゴリ,項目.行.メソッド名,項目.比較.定義.キー),StringComparer.OrdinalIgnoreCase)
            .Select(static グループ=>候補を作成(グループ.Key,グループ.ToArray()))
            .Where(static 候補=>候補.回帰ありか)
            .OrderByDescending(static 候補=>回帰重要度を取得(候補.時間シェア,候補.メモリシェア))
            .ThenBy(static 候補=>候補.ソースタブ.ID,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static 候補=>候補.メソッド名,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlySet<string>既定選択キーを作成(
        IReadOnlyList<ベンチマーク改善候補>候補一覧,
        int 取得数=既定自動選択数){
        var 選択一覧=new global::System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var 候補 in 候補一覧
            .Where(static 項目=>項目.時間回帰か)
            .OrderByDescending(static 項目=>順位値を取得(項目.時間シェア))
            .Take(取得数)){
            選択一覧.Add(候補.キー);
        }

        foreach(var 候補 in 候補一覧
            .Where(static 項目=>項目.メモリ回帰か)
            .OrderByDescending(static 項目=>順位値を取得(項目.メモリシェア))
            .Take(取得数)){
            選択一覧.Add(候補.キー);
        }

        return 選択一覧;
    }

    public static string 速度プロンプトを構築(IEnumerable<ベンチマーク改善候補>候補一覧)=>プロンプトを構築(候補一覧.Where(static 候補=>候補.時間回帰か));

    public static string メモリプロンプトを構築(IEnumerable<ベンチマーク改善候補>候補一覧)=>プロンプトを構築(候補一覧.Where(static 候補=>候補.メモリ回帰か));

    public static string プロンプトを構築(IEnumerable<ベンチマーク改善候補>候補一覧){
        var 選択一覧=候補一覧
            .OrderBy(static 項目=>項目.カテゴリ,StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(static 項目=>回帰重要度を取得(項目.時間シェア,項目.メモリシェア))
            .ThenBy(static 項目=>項目.メソッド名,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if(選択一覧.Length==0){
            return "ベンチマーク改善プロンプト\n\n改善対象となる悪化候補がありません。\n";
        }

        var ビルダー=new StringBuilder();
        ビルダー.AppendLine("ベンチマーク改善プロンプト");
        ビルダー.AppendLine();
        ビルダー.AppendLine("目的: 以下の悪化候補すべてを、段階分けせず一気に実装・テスト・検証まで進めてください。");
        ビルダー.AppendLine("計画や調査だけで止まらず、コード変更と再測定可能な状態まで完了させてください。");
        ビルダー.AppendLine("比較は左:右のシェアです。左シェアが0.50より大きいほど左(FLinq)側の負荷が大きく、改善優先度が高いです。");
        ビルダー.AppendLine();
        ビルダー.AppendLine("改善制約:");
        ビルダー.AppendLine("- ベンチマークだけを速くする一般化できない特殊化を入れないでください。特定のメソッド名、型、引数値、Count、データ内容、実行パス、ベンチマーク実行の検出を条件に処理を変えることは禁止です。");
        ビルダー.AppendLine("- すべての有効な入力に適用できるアルゴリズム、データアクセス、割り当て削減、特殊化規則として実装し、ベンチマーク外の複数サイズと異なるデータ分布でも効果と正しさを確認してください。");
        ビルダー.AppendLine("- 結果、列挙順、遅延実行、例外、破棄、デリゲートの副作用、公開API互換性を維持してください。必要な処理の省略、結果キャッシュの使い回し、検証やフォールバック計測の無効化で数値を改善してはいけません。");
        ビルダー.AppendLine("- 時間改善のためにメモリを無制限に増やしたり、メモリ改善のために時間を大幅に悪化させたりしないでください。平均ナノ秒と割り当てバイト数を同時に比較してください。");
        ビルダー.AppendLine("- 空、1件、小規模、大規模、境界値、例外を送出するデリゲート、副作用を持つデリゲートを含む回帰テストを追加し、最適化経路と通常経路の結果一致を確認してください。");
        ビルダー.AppendLine("- 測定ノイズや一度だけの結果で成功と判断せず、同一条件の反復測定で改善が再現することを確認してください。");
        ビルダー.AppendLine();
        ビルダー.AppendLine("検証方針:");
        ビルダー.AppendLine("- 対象メソッドの単体テストを実行し、既存挙動を壊していないことを確認してください。");
        ビルダー.AppendLine("- 改善後に同じベンチマーク条件で再測定し、平均ナノ秒と割り当てバイト数の差分を確認してください。");
        ビルダー.AppendLine("- 実行時フォールバックカウンター(LinqCounter.EnumerableFallback)が0のままであることも確認してください。");
        ビルダー.AppendLine();

        foreach(var グループ in 選択一覧.GroupBy(static 項目=>項目.カテゴリ).OrderBy(static 項目=>項目.Key,StringComparer.OrdinalIgnoreCase)){
            ビルダー.AppendLine("## "+グループ.Key);
            foreach(var 候補 in グループ){
                候補を追記(ビルダー,候補);
            }
            ビルダー.AppendLine();
        }

        ビルダー.AppendLine("調査手順:");
        ビルダー.AppendLine("- メソッド名とプロジェクト名で実装箇所を検索してください。");
        ビルダー.AppendLine("- 列挙処理、割り当て、分岐、バッファリング、特殊化漏れを重点的に確認してください。");
        ビルダー.AppendLine("- 変更は対象カテゴリに絞り、無関係なリファクタリングは避けてください。");
        return ビルダー.ToString();
    }

    public static string 候補キーを作成(ベンチマークソースタブ定義 ソースタブ,string カテゴリ,string メソッド名)=>
        候補キーを作成(ソースタブ,カテゴリ,メソッド名,ソースタブ.比較ペア一覧.FirstOrDefault().キー);

    public static string 候補キーを作成(ベンチマークソースタブ定義 ソースタブ,string カテゴリ,string メソッド名,string 比較ペアキー)=>
        ソースタブ.ID+"|"+カテゴリ+"|"+メソッド名+"|"+比較ペアキー;

    private static ベンチマーク改善候補 候補を作成(string キー,IReadOnlyList<ベンチマーク改善項目>項目一覧){
        var 時間項目=項目一覧
            .OrderByDescending(static 項目=>順位値を取得(項目.比較.時間シェア))
            .ThenBy(static 項目=>項目.要素数)
            .First();
        var メモリ項目=項目一覧
            .OrderByDescending(static 項目=>順位値を取得(項目.比較.メモリシェア))
            .ThenBy(static 項目=>項目.要素数)
            .First();
        IReadOnlyList<ベンチマーク引数>引数一覧;
        if(時間項目.行.引数一覧.Count>0){
            引数一覧=時間項目.行.引数一覧;
        }else{
            引数一覧=メモリ項目.行.引数一覧;
        }
        return new(
            キー,
            時間項目.ソースタブ,
            時間項目.カテゴリ,
            時間項目.行.メソッド名,
            時間項目.行.ベンチマーク,
            時間項目.要素数,
            メモリ項目.要素数,
            時間項目.比較.時間シェア,
            メモリ項目.比較.メモリシェア,
            時間項目.比較.左,
            時間項目.比較.右,
            メモリ項目.比較.左,
            メモリ項目.比較.右,
            時間項目.行.型完全名,
            最初の文字列(時間項目.行.引数キー,メモリ項目.行.引数キー),
            引数一覧,
            時間項目.比較.定義);
    }

    private static IEnumerable<ベンチマーク改善項目>項目を作成(進行状態 状態){
        foreach(var グループ in 状態.ジョブ一覧
            .Where(static ジョブ=>ジョブ.状態==ジョブ状態.完了)
            .GroupBy(static ジョブ=>new{ジョブ.ソースタブ,ジョブ.要素数})
            .OrderBy(static グループ=>ベンチマークソースタブ一覧.取得(グループ.Key.ソースタブ).ID,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static グループ=>グループ.Key.要素数)){
            var ソースタブ=ベンチマークソースタブ一覧.取得(グループ.Key.ソースタブ);
            var 報告行一覧=グループ.Select(static ジョブ=>ジョブ.報告行に変換()).ToArray();
            if(報告行一覧.Length==0){
                continue;
            }

            IReadOnlyList<ベンチマーク比較行>行一覧;
            try{
                行一覧=ベンチマーク比較フォーマッター.比較行を構築(報告行一覧,ソースタブ,グループ.Key.要素数);
            }catch(InvalidDataException){
                continue;
            }

            foreach(var 行 in 行一覧){
                foreach(var 比較 in 行.比較一覧.Count>0 ? 行.比較一覧 : [行.左右シェア]){
                    yield return new ベンチマーク改善項目(
                        ソースタブ,
                        ベンチマーク改善カテゴリ分類器.分類(行.メソッド名),
                        グループ.Key.要素数,
                        行,
                        比較);
                }
            }
        }
    }

    private static void 候補を追記(StringBuilder ビルダー,ベンチマーク改善候補 候補){
        ビルダー.AppendLine("- "+候補.メソッド名+" ("+候補.ソースタブ.表示名+")");
        ビルダー.AppendLine("  - 比較: "+候補.比較ペア.表示名+" ("+候補.比較ペア.左プロジェクト+" / "+候補.比較ペア.右プロジェクト+")");
        ビルダー.AppendLine("  - 時間差: 要素数="+要素数を整形(候補.時間要素数)+" / シェア 左:右 "+シェアを整形(候補.時間シェア)+" / 左 "+時間を整形(候補.時間左.時間ナノ秒)+" / 右 "+時間を整形(候補.時間右.時間ナノ秒));
        ビルダー.AppendLine("  - メモリ差: 要素数="+要素数を整形(候補.メモリ要素数)+" / シェア 左:右 "+シェアを整形(候補.メモリシェア)+" / 左 "+割り当てを整形(候補.メモリ左.割り当てバイト数)+" / 右 "+割り当てを整形(候補.メモリ右.割り当てバイト数));
        if(!string.IsNullOrWhiteSpace(候補.型完全名)){
            ビルダー.AppendLine("  - 型: "+候補.型完全名);
        }
        if(!string.IsNullOrWhiteSpace(候補.引数キー)){
            ビルダー.AppendLine("  - 引数: "+候補.引数キー);
        }
        ビルダー.AppendLine("  - 検索ヒント: rg \""+候補.メソッド名+"|"+候補.比較ペア.左プロジェクト+"\"");
    }

    internal static double 順位値を取得(シェア値 シェア){
        if(シェア.左シェア is { } 値&&値>0.5){
            return 値;
        }
        return double.NegativeInfinity;
    }

    internal static double 回帰重要度を取得(シェア値 時間シェア,シェア値 メモリシェア){
        var 時間重要度=0.0;
        if(時間シェア.左シェア is { } 時間左&&時間左>0.5){
            時間重要度=(時間左-0.5)*2;
        }
        var メモリ重要度=0.0;
        if(メモリシェア.左シェア is { } メモリ左&&メモリ左>0.5){
            メモリ重要度=(メモリ左-0.5)*2;
        }
        return Math.Max(時間重要度,メモリ重要度);
    }

    internal static string シェアを整形(シェア値 シェア)=>シェア.表示文字列;

    internal static string 時間を整形(double?値){
        if(値 is { } 数値&&double.IsFinite(数値)){
            return ベンチマークフォーマッター.ナノ秒を整形(数値);
        }
        return "-";
    }

    internal static string 割り当てを整形(long?値){
        if(値 is { } 数値){
            return ベンチマークフォーマッター.バイト数を整形(数値);
        }
        return "-";
    }

    private static string 要素数を整形(int 値)=>
        値.ToString("N0",CultureInfo.InvariantCulture);

    private static string 最初の文字列(params string?[]値一覧)=>
        値一覧.FirstOrDefault(static 値=>!string.IsNullOrWhiteSpace(値))??string.Empty;

    private sealed record ベンチマーク改善項目(
        ベンチマークソースタブ定義 ソースタブ,
        string カテゴリ,
        int 要素数,
        ベンチマーク比較行 行,
        シェア比較 比較);
}

internal static class ベンチマーク改善カテゴリ分類器{
    public static string 分類(string メソッド名){
        if(メソッド名.StartsWith("Aggregate",StringComparison.OrdinalIgnoreCase)){
            return "集約";
        }
        if(メソッド名.StartsWith("Any",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("All",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Count",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Contains",StringComparison.OrdinalIgnoreCase)){
            return "述語/Count";
        }
        if(メソッド名.StartsWith("Distinct",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Except",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Intersect",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Union",StringComparison.OrdinalIgnoreCase)){
            return "集合";
        }
        if(メソッド名.StartsWith("Order",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Then",StringComparison.OrdinalIgnoreCase)){
            return "並べ替え";
        }
        if(メソッド名.StartsWith("Select",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Where",StringComparison.OrdinalIgnoreCase)){
            return "射影/絞り込み";
        }
        if(メソッド名.StartsWith("Group",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("ToLookup",StringComparison.OrdinalIgnoreCase)){
            return "グループ化";
        }
        if(メソッド名.StartsWith("Skip",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Take",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("ElementAt",StringComparison.OrdinalIgnoreCase)){
            return "インデックス操作";
        }
        if(メソッド名.StartsWith("Append",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Prepend",StringComparison.OrdinalIgnoreCase)
            ||メソッド名.StartsWith("Concat",StringComparison.OrdinalIgnoreCase)){
            return "連結";
        }
        return "その他";
    }
}

public sealed record ベンチマーク改善候補(
    string キー,
    ベンチマークソースタブ定義 ソースタブ,
    string カテゴリ,
    string メソッド名,
    string 表示名,
    int 時間要素数,
    int メモリ要素数,
    シェア値 時間シェア,
    シェア値 メモリシェア,
    プロジェクト測定値 時間左,
    プロジェクト測定値 時間右,
    プロジェクト測定値 メモリ左,
    プロジェクト測定値 メモリ右,
    string 型完全名,
    string 引数キー,
    IReadOnlyList<ベンチマーク引数>引数一覧,
    比較ペア定義 比較ペア){
    public bool 時間回帰か=>ベンチマーク改善候補ビルダー.順位値を取得(this.時間シェア)>0.5;
    public bool メモリ回帰か=>this.メモリシェア.左シェア>0.5;
    public bool 回帰ありか=>this.時間回帰か||this.メモリ回帰か;
}
