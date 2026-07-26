namespace Seamless.UI.Storage;

public enum UI実行アプリ種別{
    ベンチマーク,
    スケーラブルベンチマーク,
    パラメータスイープ,
}

public enum UI実行状態{
    待機中,
    実行中,
    一時停止中,
    停止済み,
    完了,
    失敗,
}


public sealed record UI実行作成要求(
    UI実行アプリ種別 アプリ種別,
    string 表示名,
    string 実行プロファイル,
    string タブ名,
    DateTimeOffset 作成日時);

public sealed record UI実行記録(
    long 実行ID,
    UI実行アプリ種別 アプリ種別,
    string 表示名,
    UI実行状態 状態,
    string 実行プロファイル,
    string タブ名,
    DateTimeOffset 更新日時);

public sealed record UIパラメータスイープ結果(
    long 実行ID,
    string ケース名,
    string コレクション,
    string ジェネリック名,
    string バックエンド,
    string スイープ,
    long Indexサイズ,
    long Pageサイズ,
    long Segmentサイズ,
    long Memoryサイズ,
    double 可変Fraction,
    long 最大成功Count,
    long エントリCount,
    string 失敗種別,
    string 失敗理由,
    double 追加経過ミリ秒,
    double? 検索平均ナノ秒,
    DateTimeOffset 計測日時);

public sealed record UIベンチマークグリッド側書込(
    UI実行状態 状態,
    string 失敗理由,
    DateTimeOffset? 計測日時,
    double? 平均ナノ秒,
    long? 割当バイト,
    int? ループ回数 = null,
    double? 合計ナノ秒 = null,
    bool 実行内計測か = false,
    string 計測段階 = "");

public sealed record UIベンチマークスナップショットケース書込(
    string アセンブリ名,
    string 型完全名,
    string メソッド名,
    string 引数キー,
    string 引数JSON);

public sealed record UIベンチマークスナップショット項目書込(
    string タブ名,
    string 型名,
    string ベンチマーク名,
    int? CPU番号,
    bool 選択中か,
    UIベンチマークグリッド側書込 左,
    UIベンチマークグリッド側書込 右,
    UIベンチマークスナップショットケース書込? 左ケース = null,
    UIベンチマークスナップショットケース書込? 右ケース = null);

public sealed record UIベンチマークグリッド側記録(
    UI実行状態 状態,
    string 失敗理由,
    DateTimeOffset? 計測日時,
    double? 平均ナノ秒,
    long? 割当バイト,
    int? ループ回数 = null,
    double? 合計ナノ秒 = null,
    bool 実行内計測か = false,
    string 計測段階 = "");

public sealed record UIベンチマークスナップショット項目記録(
    long 実行ID,
    string タブ名,
    string 型名,
    string ベンチマーク名,
    int? CPU番号,
    bool 選択中か,
    UIベンチマークグリッド側記録 左,
    UIベンチマークグリッド側記録 右,
    UIベンチマークスナップショットケース書込? 左ケース = null,
    UIベンチマークスナップショットケース書込? 右ケース = null);

public sealed record UI実行設定記録(
    long 実行ID,
    string 実行ルート,
    string 要素数一覧,
    double 適応閾値ナノ秒,
    string 論理CPU一覧);

public sealed record UI指標値記録(
    long ジョブID,
    string 指標名,
    double 数値,
    string 単位,
    DateTimeOffset 計測日時);
