using System.Globalization;

namespace Benchmark.Dynamic.UI;

/// <summary>ライブ検証タブの配色。</summary>
internal static class 動的ライブ配色{
    public static readonly Color 背景=Color.FromArgb("#F4F6FA");
    public static readonly Color 面=Colors.White;
    public static readonly Color 枠=Color.FromArgb("#D5DBE5");
    public static readonly Color 文字=Color.FromArgb("#1F2937");
    public static readonly Color 薄い文字=Color.FromArgb("#64748B");
    public static readonly Color アクセント=Color.FromArgb("#2563EB");
    public static readonly Color 成功=Color.FromArgb("#15803D");
    public static readonly Color 警告=Color.FromArgb("#D97706");
    public static readonly Color 失敗=Color.FromArgb("#B91C1C");
    public static readonly Color コード背景=Color.FromArgb("#1E293B");
    public static readonly Color コード文字=Color.FromArgb("#E2E8F0");
    public static readonly Color 展開背景=Color.FromArgb("#0F172A");
    public static readonly Color 展開文字=Color.FromArgb("#A7F3D0");
    public static readonly Color 実行中の段階=Color.FromArgb("#DBEAFE");

    public static Color 実装の色(string 実装){
        return 実装 switch{
            "System.Linq"=>Color.FromArgb("#94A3B8"),
            "FLinq"=>Color.FromArgb("#2563EB"),
            "FLinq最適化なし"=>Color.FromArgb("#F59E0B"),
            "ZLinq"=>Color.FromArgb("#10B981"),
            _=>Color.FromArgb("#64748B"),
        };
    }
}

public sealed record 動的ライブ棒(string ラベル,double 値,Color 色);

/// <summary>実行時間や割り当てバイト数の比較を縦棒で描く。</summary>
internal sealed class 動的ライブ棒グラフ:IDrawable{
    private IReadOnlyList<動的ライブ棒> 棒一覧=[];
    private string 題名=string.Empty;
    private string 単位=string.Empty;

    public void 更新する(string 題名,string 単位,IReadOnlyList<動的ライブ棒> 棒一覧){
        this.題名=題名;
        this.単位=単位;
        this.棒一覧=棒一覧;
    }

    public void Draw(ICanvas 画布,RectF 範囲){
        var 一覧=this.棒一覧;
        if(一覧.Count==0){
            return;
        }
        const float 見出し高さ=22;
        const float ラベル高さ=18;
        const float 値高さ=16;
        画布.FontColor=動的ライブ配色.文字;
        画布.FontSize=13;
        画布.DrawString($"{this.題名} ({this.単位})",範囲.Left,範囲.Top,範囲.Width,見出し高さ,HorizontalAlignment.Left,VerticalAlignment.Center);

        var 最大=一覧.Max(static 棒=>棒.値);
        if(最大<=0){
            最大=1;
        }
        var 領域上端=範囲.Top+見出し高さ+値高さ;
        var 領域高さ=範囲.Height-見出し高さ-値高さ-ラベル高さ;
        var 一本の幅=範囲.Width/一覧.Count;
        var 棒の幅=Math.Min(一本の幅*0.68f,54f);
        画布.FontSize=11;
        for(var 番号=0;番号<一覧.Count;番号++){
            var 棒=一覧[番号];
            var 高さ=(float)(棒.値/最大)*領域高さ;
            var 中心=範囲.Left+一本の幅*(番号+0.5f);
            var 上端=領域上端+領域高さ-高さ;
            画布.FillColor=棒.色;
            画布.FillRoundedRectangle(中心-棒の幅/2,上端,棒の幅,Math.Max(高さ,2),3);
            画布.FontColor=動的ライブ配色.文字;
            画布.DrawString(値の文字(棒.値),中心-一本の幅/2,上端-値高さ,一本の幅,値高さ,HorizontalAlignment.Center,VerticalAlignment.Bottom);
            画布.FontColor=動的ライブ配色.薄い文字;
            画布.DrawString(棒.ラベル,中心-一本の幅/2,領域上端+領域高さ,一本の幅,ラベル高さ,HorizontalAlignment.Center,VerticalAlignment.Center);
        }
    }

    private static string 値の文字(double 値){
        return 値>=100
            ?値.ToString("N0",CultureInfo.InvariantCulture)
            :値.ToString("0.##",CultureInfo.InvariantCulture);
    }
}
