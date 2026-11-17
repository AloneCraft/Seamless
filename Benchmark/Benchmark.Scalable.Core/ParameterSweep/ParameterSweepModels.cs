using MemoryPack;

using Seamless.Collections;

using System.Globalization;

namespace Benchmark.Tsavorite.ParameterSweep;

public enum パラメータスイープコレクション種別 {
    ハッシュセット,
    セット,
    辞書,
}

public enum パラメータスイープバックエンド {
    メモリ,
    ストレージ,
}

public enum パラメータスイープ軸 {
    Indexサイズ,
    Memoryサイズ,
    Pageサイズ,
    Segmentサイズ,
}

public enum パラメータスイープ失敗種別 {
    完了,
    ログ満杯,
    メモリ不足,
    タイムアウト,
    無効な構成,
    例外,
    メモリ上限超過,
    ディスク上限超過,
}

public readonly record struct パラメータスイープ設定(
    long Indexサイズ,
    long Pageサイズ,
    long Segmentサイズ,
    long Memoryサイズ,
    double 可変Fraction);

public sealed record パラメータスイープケース(
    string ケース名,
    パラメータスイープコレクション種別 コレクション,
    string ジェネリック名,
    パラメータスイープバックエンド バックエンド,
    パラメータスイープ軸 スイープ軸,
    パラメータスイープ設定 設定);

public sealed record パラメータスイープ結果(
    string 実行ID,
    DateTimeOffset 計測日時,
    string ケース名,
    パラメータスイープコレクション種別 コレクション,
    string ジェネリック名,
    パラメータスイープバックエンド バックエンド,
    パラメータスイープ軸 スイープ軸,
    long Indexサイズ,
    long Pageサイズ,
    long Segmentサイズ,
    long Memoryサイズ,
    double 可変Fraction,
    long 最大成功Count,
    long エントリCount,
    パラメータスイープ失敗種別 失敗種別,
    string 失敗メッセージ,
    double 追加経過ミリ秒,
    double? 検索平均ナノ秒,
    long? 索引バケット数 = null,
    long? 溢れバケット数 = null,
    long? メモリログバイト数 = null,
    long? ディスクログバイト数 = null) {
    public IReadOnlyList<パラメータスイープワーカー指標> ワーカー指標一覧 { get; init; } = [];
    public string CPUセットキー { get; init; } = string.Empty;
    public int 並行数 { get; init; } = 1;
}

public sealed record パラメータスイープワーカー指標(
    int? 論理CPU,
    long 追加操作Count,
    double 追加経過ミリ秒,
    long 検索操作Count,
    double 検索経過ミリ秒,
    パラメータスイープ失敗種別 失敗種別,
    string 失敗メッセージ);

public sealed record パラメータスイープ比較行(
    パラメータスイープ結果 今回,
    パラメータスイープ結果? 前回,
    long? 最大Count差分,
    double? 今回追加時間シェア,
    double? 前回追加時間シェア,
    double? 今回検索時間シェア,
    double? 前回検索時間シェア,
    bool 失敗種別変更か);

[MemoryPackable]
public readonly partial record struct パラメータスイープ非管理セットレコード(int Key,long 値):IRecord<int>;

[MemoryPackable]
public readonly partial record struct パラメータスイープ管理セットレコード(int Key,string 名前):IRecord<int>;

public static class パラメータスイープフォーマッター {
    public static string サイズを整形(long 値) {
        if(値%(1L<<30)==0&&値>=1L<<30) {
            return (値>>30).ToString(CultureInfo.InvariantCulture)+"G";
        }
        if(値%(1L<<20)==0&&値>=1L<<20) {
            return (値>>20).ToString(CultureInfo.InvariantCulture)+"M";
        }
        if(値%(1L<<10)==0&&値>=1L<<10) {
            return (値>>10).ToString(CultureInfo.InvariantCulture)+"K";
        }
        return 値.ToString(CultureInfo.InvariantCulture);
    }

    public static string ケース名を作成(
        パラメータスイープコレクション種別 コレクション,
        string ジェネリック名,
        パラメータスイープバックエンド バックエンド,
        パラメータスイープ軸 スイープ軸,
        パラメータスイープ設定 設定) =>
        コレクション+"<"+ジェネリック名+">."
        +バックエンド+"."
        +スイープ軸+"_"
        +パラメーター接尾辞を作成(設定);

    public static string パラメーター接尾辞を作成(パラメータスイープ設定 設定) =>
        固定長で整形(設定.Indexサイズ,6)
        +"_"+固定長で整形(設定.Pageサイズ,7)
        +"_"+固定長で整形(設定.Segmentサイズ,7)
        +"_"+固定長で整形(設定.Memoryサイズ,7)
        +"_"+Fractionを整形(設定.可変Fraction);

    private static string 固定長で整形(long 値,int 幅) =>
        値.ToString("D"+幅.ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);

    private static string Fractionを整形(double 値) =>
        ((int)Math.Round(値*10,MidpointRounding.AwayFromZero)).ToString("D2",CultureInfo.InvariantCulture);
}
