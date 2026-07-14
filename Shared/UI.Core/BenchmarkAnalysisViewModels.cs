using System.Globalization;

namespace UI;

public sealed record ベンチマーク順位シェア点(string ラベル,double 値,string 詳細文字列);
public sealed record ベンチマークカテゴリシェア点(string カテゴリ,double 平均時間左シェア,double 平均メモリ左シェア,int サンプル数);
public sealed record ベンチマーク傾向点(double 横軸値,double 縦軸値);
public sealed record ベンチマーク傾向系列(string 系列名,IReadOnlyList<ベンチマーク傾向点>点一覧);

public enum ベンチマークバッジ重要度{
    警告,
    重大,
    情報,
}

public sealed record ベンチマークバッジ項目(string タイトル,string 詳細,ベンチマークバッジ重要度 重要度);
public sealed record ベンチマーク品質ゲージ点(string ラベル,double 標準偏差割合,double 誤差割合,string 詳細文字列);

/// <summary>
/// SQLite分析ビューの汎用行をインフォグラフィック描画向けの型付きモデルへ変換する。
/// MAUI依存を持たない純ロジック。列名はUI実行ストアのビュー定義に合わせる。
/// </summary>
[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク分析表示マッパー{
    public static IReadOnlyList<ベンチマーク順位シェア点>改善候補を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>>行一覧)=>
        行一覧.Select(行=>new ベンチマーク順位シェア点(
                文字列を取得(行,"メソッド名"),
                数値を取得(行,"改善スコア"),
                文字列を取得(行,"左プロジェクト")+" / "+文字列を取得(行,"右プロジェクト")+" / 要素数="+文字列を取得(行,"要素数")))
            .OrderByDescending(static 点=>点.値)
            .ToArray();

    public static IReadOnlyList<ベンチマークカテゴリシェア点>カテゴリ概要を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>>行一覧)=>
        行一覧.Select(行=>new ベンチマークカテゴリシェア点(
                文字列を取得(行,"カテゴリ"),
                数値を取得(行,"平均時間左シェア"),
                数値を取得(行,"平均メモリ左シェア"),
                (int)数値を取得(行,"サンプル数")))
            .OrderByDescending(static 点=>点.平均時間左シェア)
            .ToArray();

    public static IReadOnlyList<ベンチマーク傾向系列>規模傾向を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>>行一覧,int 最大系列数=8)=>
        行一覧.GroupBy(行=>文字列を取得(行,"メソッド名"),StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(グループ=>グループ.Max(行=>数値を取得(行,"平均時間左シェア")))
            .Take(最大系列数)
            .Select(グループ=>new ベンチマーク傾向系列(
                グループ.Key,
                グループ.OrderBy(行=>数値を取得(行,"要素数"))
                    .Select(行=>new ベンチマーク傾向点(数値を取得(行,"要素数"),数値を取得(行,"平均時間左シェア")))
                    .ToArray()))
            .ToArray();

    public static IReadOnlyList<ベンチマークバッジ項目>欠測失敗行列を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>>行一覧)=>
        行一覧.Select(行=>{
            var 失敗か=string.Equals(文字列を取得(行,"ジョブ状態"),ジョブ状態名.保存値に変換(ジョブ状態.失敗),StringComparison.OrdinalIgnoreCase);
            var 欠測か=数値を取得(行,"対向欠測")>0;
            var 重要度=ベンチマークバッジ重要度.情報;
            if(失敗か){
                重要度=ベンチマークバッジ重要度.重大;
            }else if(欠測か){
                重要度=ベンチマークバッジ重要度.警告;
            }
            var 詳細=文字列を取得(行,"プロジェクト")+" / "+側表示を取得(文字列を取得(行,"側"))+" / 要素数="+文字列を取得(行,"要素数");
            if(失敗か){
                詳細+=" / "+文字列を取得(行,"失敗種別");
            }else if(欠測か){
                詳細+=" / 対向側が欠測";
            }
            return new ベンチマークバッジ項目(文字列を取得(行,"メソッド名"),詳細,重要度);
        }).ToArray();

    public static IReadOnlyList<ベンチマーク品質ゲージ点>測定品質を変換(IReadOnlyList<IReadOnlyDictionary<string,object?>>行一覧)=>
        行一覧.Select(行=>new ベンチマーク品質ゲージ点(
                文字列を取得(行,"メソッド名")+" ("+文字列を取得(行,"プロジェクト")+")",
                Math.Clamp(数値を取得(行,"標準偏差割合"),0,1),
                Math.Clamp(数値を取得(行,"誤差割合"),0,1),
                "要素数="+文字列を取得(行,"要素数")+" / CPU "+文字列を取得(行,"割り当て論理CPU")))
            .ToArray();

    private static string 側表示を取得(string 側){
        if(比較側名.保存値と一致するか(側,比較側.左)){
            return "左";
        }
        if(比較側名.保存値と一致するか(側,比較側.右)){
            return "右";
        }
        return 側;
    }

    private static string 文字列を取得(IReadOnlyDictionary<string,object?>行,string 列名){
        if(行.TryGetValue(列名,out var 値)&&値 is not null){
            return Convert.ToString(値,CultureInfo.InvariantCulture)??"-";
        }
        return "-";
    }

    private static double 数値を取得(IReadOnlyDictionary<string,object?>行,string 列名){
        if(行.TryGetValue(列名,out var 値)&&値 is not null&&値 is not DBNull){
            return Convert.ToDouble(値,CultureInfo.InvariantCulture);
        }
        return 0;
    }
}
