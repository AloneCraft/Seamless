using System.Globalization;
using Sample.FLinqChainVerifier.Core;

namespace Sample.FLinqChainVerifier.Controls;

/// <summary>実行時間や割り当てバイト数の比較棒グラフを縦棒で描画する。</summary>
internal sealed class 棒グラフ描画:IDrawable{
    private 棒グラフ報告? データ;

    public void 更新する(棒グラフ報告 新データ)=>this.データ=新データ;

    public void Draw(ICanvas 画布,RectF 範囲){
        var 現在データ=this.データ;
        if(現在データ is null||現在データ.棒一覧.Count==0){
            return;
        }
        const float 見出し高さ=22;
        const float ラベル高さ=18;
        const float 値高さ=16;
        画布.FontColor=配色.文字;
        画布.FontSize=13;
        画布.DrawString($"{現在データ.題名} ({現在データ.単位})",範囲.Left,範囲.Top,範囲.Width,見出し高さ,HorizontalAlignment.Left,VerticalAlignment.Center);

        var 最大=現在データ.棒一覧.Max(static 棒=>棒.値);
        if(最大<=0){
            最大=1;
        }
        var 棒の領域上端=範囲.Top+見出し高さ+値高さ;
        var 棒の領域高さ=範囲.Height-見出し高さ-値高さ-ラベル高さ;
        var 一本の幅=範囲.Width/現在データ.棒一覧.Count;
        var 棒の幅=Math.Min(一本の幅*0.68f,54f);
        画布.FontSize=11;
        for(var 番号=0;番号<現在データ.棒一覧.Count;番号++){
            var 棒=現在データ.棒一覧[番号];
            var 高さ=(float)(棒.値/最大)*棒の領域高さ;
            var 中心=範囲.Left+一本の幅*(番号+0.5f);
            var 上端=棒の領域上端+棒の領域高さ-高さ;
            画布.FillColor=配色.棒(棒.色);
            画布.FillRoundedRectangle(中心-棒の幅/2,上端,棒の幅,Math.Max(高さ,2),3);
            画布.FontColor=配色.文字;
            画布.DrawString(値の文字(棒.値),中心-一本の幅/2,上端-値高さ,一本の幅,値高さ,HorizontalAlignment.Center,VerticalAlignment.Bottom);
            画布.FontColor=配色.薄い文字;
            画布.DrawString(棒.ラベル,中心-一本の幅/2,棒の領域上端+棒の領域高さ,一本の幅,ラベル高さ,HorizontalAlignment.Center,VerticalAlignment.Center);
        }
    }

    private static string 値の文字(double 値){
        if(値>=100){
            return 値.ToString("N0",CultureInfo.InvariantCulture);
        }
        return 値.ToString("0.##",CultureInfo.InvariantCulture);
    }
}
