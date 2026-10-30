# SQL Server互換の接続単位ロック待機時間

## 背景と観測事実
SSMSの実接続を診断用TDS転送器で採取したところ、包含認証確認の次の `SET LOCK_TIMEOUT 10000` が40515で拒否されていた。証跡は `F:/Seamless/Comparison/tds-sql-trace-05/sql.jsonl`。SMOの既存試験ではLockTimeoutプロパティを設定していなかった。

## 引き継ぐ境界
[SSMSと生成DBの仕様](20261029-generic-sql/ssms-generated-db.md)に従い、基本SQLと既存GUIの利用を支える。SQL Server独自のSETを一括で無視しない。Core/C12の確定・WAL・排他方式は変更しない。既存の `DatabaseContainer.BeginTransaction(CancellationToken)` と `BeginReadSession(CancellationToken)` を利用する。

## 修正方針
- 接続単位に待機時間を保存し、既定値と接続リセット後の値は-1（無期限）とする。`SELECT @@LOCK_TIMEOUT` と単一列の別名を提供する。
- 最初の対応範囲は整数リテラルの-1と1～Int32.MaxValue。即時取得を正確に提供するCore契約はないため0を拒否する。変数・範囲外・その他のSETコマンドも拒否し、値を変更しない。
- 有限の時間はロック取得だけに適用し、取得後にタイマーを破棄する。コマンド全体や取引寿命の制限にはしない。利用者の取消を優先し、ロック待機期限だけの場合はSQLエラー1222とする。
- GatewayのSQL実行コンテキストをAsyncLocalで設定し、処理と列挙の終了時に復元する。キャッシュされた計画に接続を保存しない。カタログ解決・スキーマ指紋・表の列挙の読み取りscopeへ同じ待機方針を伝える。
- 暗黙DMLは名前解決前に既存の書き込み取引を開始し、失敗時に戻す。RuntimeSqlCatalogには取消付きBeginTransactionのオーバーロードを追加し、既存の引数なしAPIを維持する。
- 待機対象は既存Coreが提供するDB単位の排他である。SQL Serverの行ロックやデッドロック検出方式を追加するものではない。

## 受け入れと配置
設定・接続隔離・reset・不正値、SharedV2の競合下でSELECT/明示取引/暗黙DMLが1222になること、失敗後の再実行、取消を検証する。Debug/Releaseの関連回帰、LockTimeoutを設定したSMO試験を実行する。NuGetの新しい一意な版で実データを再構築し、全検証が通った世代だけ14333番へ切り替える。実SSMS画面の成功は利用者の再確認まで未検証とする。
