namespace Seamless.FLinq.Generator;

/// <summary>
/// 展開の部品が生成コードに出す計測点の目印。注釈なので通常版のコンパイル結果には何も残らない。
/// 計測版を作るとき、<see cref="QueryProfileSource"/> が目印の位置へ計測の記録を差し込む。
/// 生成したコードの形(ループの書き方など)から計測の位置を推測しないための仕組み。
/// </summary>
internal static class QueryProbeMarkers{
    /// <summary>この文の直前で、入力の要素を1件受け取った(値を取り出せた後)。</summary>
    internal const string Input="/*flinq-probe:input*/";
    /// <summary>この経路では入力の件数を数えない(例：SIMDの一括加算)。この目印がある本体は入力件数を未収集にする。</summary>
    internal const string Unknown="/*flinq-probe:unknown*/";
    internal const string Prefix="/*flinq-probe:";
    /// <summary>目印を取り除く。通常版・実行計画・ハッシュには目印のない本体を使い、生成ソースを目印の導入前と同じにする。</summary>
    internal static string Strip(string text)=>text.IndexOf(Prefix,StringComparison.Ordinal)<0?text:text.Replace(Input,"").Replace(Unknown,"");
}
