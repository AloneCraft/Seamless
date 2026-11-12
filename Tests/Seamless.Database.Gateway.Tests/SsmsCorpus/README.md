# SSMS Object Explorer 要求コーパス

SSMS 22 の SQL Server 16.0 用階層定義と SFC Enumerator の要求に基づき、SQL Server の AdventureWorks2016 から取得した SQL。`<msparam>` は Enumerator が表示するパラメーター値であり、テスト時に型に応じた SQL リテラルへ変換する。製品側の管理バッチ認識用リソースとは別に保持する。

通常のテーブル一覧に加えて、テンポラル・メモリ最適化・台帳、グラフ・FileTable・外部テーブル、削除済み台帳、台帳ビュー、種類別関数とネイティブプロシージャの一覧を含む。非対応機能の一覧が空でも、SELECT の名前解決と型検査まで実行する。テストfixtureでの成功を実SSMS画面の確認、SQL Serverの機能実装、または全クライアント互換性とみなさない。

テストでは SQL Server から取得したコーパスを実 SqlClient 経由で送る。階層の要求そのものと比較元・候補サーバーの SDK 応答の比較は `Work/TestSeamless/Probe-SfcObjectExplorerRequests.ps1` で別途実施する。SQL結果の同値が必要な通常のDB、表、ビュー、ルーチンと、未提供機能を空集合として扱う能力差は区別する。
