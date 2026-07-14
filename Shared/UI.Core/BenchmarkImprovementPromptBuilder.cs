using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク改善プロンプトビルダー{
    public const string ファイル名="ImprovementPrompt.md";

    public static ベンチマークシェア概要 概要を作成(IReadOnlyList<ベンチマーク比較行>行一覧)=>
        new(
            シェア平均を計算(行一覧.Select(static 行=>行.左右シェア.時間シェア),out var 時間対象数,out var 時間除外数),
            シェア平均を計算(行一覧.Select(static 行=>行.左右シェア.メモリシェア),out var メモリ対象数,out var メモリ除外数),
            時間対象数,
            時間除外数,
            メモリ対象数,
            メモリ除外数);

    public static string プロンプトを構築(進行状態 状態){
        var 候補一覧=ベンチマーク改善候補ビルダー.候補を構築(状態);
        return ベンチマーク改善候補ビルダー.プロンプトを構築(候補一覧);
    }

    public static string プロンプトを構築(IEnumerable<ベンチマーク改善候補>候補一覧)=>
        ベンチマーク改善候補ビルダー.プロンプトを構築(候補一覧);

    private static double?シェア平均を計算(IEnumerable<シェア値>シェア一覧,out int 対象数,out int 除外数){
        対象数=0;
        除外数=0;
        var 合計=0.0;
        foreach(var シェア in シェア一覧){
            if(シェア.左シェア is { } 値&&double.IsFinite(値)){
                合計+=値;
                対象数++;
            }else{
                除外数++;
            }
        }

        if(対象数==0){
            return null;
        }
        return 合計/対象数;
    }
}

internal sealed record ベンチマークシェア概要(
    double?時間左平均,
    double?メモリ左平均,
    int 時間対象数,
    int 時間除外数,
    int メモリ対象数,
    int メモリ除外数){
    public string 表示文字列=>
        "時間シェア "+シェアを整形(this.時間左平均)
        +" (対象 "+this.時間対象数.ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+this.時間除外数.ToString(CultureInfo.InvariantCulture)+")"
        +" / メモリシェア "+シェアを整形(this.メモリ左平均)
        +" (対象 "+this.メモリ対象数.ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+this.メモリ除外数.ToString(CultureInfo.InvariantCulture)+")";

    public string 全体表示文字列=>"全体 "+this.表示文字列;

    public string 短縮表示文字列=>
        "時間シェア "+シェアを整形(this.時間左平均)
        +",メモリシェア "+シェアを整形(this.メモリ左平均);

    private static string シェアを整形(double?左シェア){
        if(左シェア is not { } 値||!double.IsFinite(値)){
            return "-";
        }
        return シェア値.左シェアから作成(値).表示文字列;
    }
}
