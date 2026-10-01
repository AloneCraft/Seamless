namespace Sample.NetShop.Core;

/// <summary>章が実行中に画面へ送る知らせ。画面は種類ごとに右列へ描く。</summary>
public abstract record 章の報告;

/// <summary>流れていく1行の記録。</summary>
public sealed record ログ報告(string 文,ログ種別 種別):章の報告;

public enum ログ種別{
    情報,
    成功,
    失敗,
}

/// <summary>名前つきの値。同じ名前の指標は上書きされる。</summary>
public sealed record 指標報告(string 名前,string 値):章の報告;

/// <summary>上限に対する使用量。同じ名前のメーターは上書きされる。</summary>
public sealed record メーター報告(string 名前,long 値,long 上限):章の報告;

/// <summary>棒グラフ。同じ題名のグラフは上書きされる。</summary>
public sealed record 棒グラフ報告(string 題名,string 単位,IReadOnlyList<棒>棒一覧):章の報告;

public readonly record struct 棒(string ラベル,double 値,棒の色 色);

public enum 棒の色{
    標準,
    強調,
    比較,
    警告,
}

/// <summary>説明中のコードのうち、いま実行している行。</summary>
public sealed record コード強調報告(int 行番号):章の報告;
