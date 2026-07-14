namespace UI;

public sealed record ベンチマーク実行選択肢(
    int 実行番号,
    long UI実行ID,
    string 表示名,
    string 実行ルート,
    ベンチマーク実行プロファイル 実行プロファイル,
    DateTimeOffset 作成日時,
    DateTimeOffset 更新日時,
    int 完了ジョブ数,
    int 総ジョブ数,
    IReadOnlyList<int>要素数一覧);
