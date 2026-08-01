namespace UI;

public sealed record 報告行(
    string プロジェクト,
    string メソッド,
    string 型完全名,
    string 完全関数名,
    string 状態,
    string 平均,
    string 誤差,
    string 標準偏差,
    string 割り当てバイト数文字列,
    string 最大RSSバイト数文字列,
    string 推定最大RSSバイト数文字列,
    double 平均ナノ秒,
    double 誤差ナノ秒,
    double 標準偏差ナノ秒,
    long 割り当てバイト数値,
    long 最大RSSバイト数値,
    long 推定最大RSSバイト数値,
    string 論理CPU,
    string コア,
    string CPU表示,
    string 開始日時文字列,
    string 終了日時文字列,
    string 成果物フォルダー,
    int?終了コード,
    string 引数キー="",
    IReadOnlyList<ベンチマーク引数>?引数値一覧=null){
    public IReadOnlyList<ベンチマーク引数>引数一覧{get;}=引数値一覧??[];
}
