# SSMS 比較ソリューション

SSMS 22 では `TestSeamless.Compare.sln` を「プロジェクト/ソリューションを開く」から開きます。これは SSMS の SQL Server Scripts solution です。`SQLServer` と `SeamlessDB` の2プロジェクトが比較元と比較先の接続を保持します。元サーバーの接続先とWindows認証方式は世代manifestの `SourceServer` / `SourceIntegratedSecurity` から読みます。接続文字列の環境変数やパスワードを生成処理に渡す必要はありません。比較先は `127.0.0.1,14333`、SQL認証の `sa` です。このマシンの既定 SqlClient で `localhost` が接続前にタイムアウトしたため、比較環境では IPv4 アドレスを指定しています。パスワードは保存せず、SSMS接続時に入力します。テスト EXE の接続文字列は `ConnectionStrings__Seamless` で上書きでき、SSMS プロジェクトの接続設定はその環境変数では変わりません。14335は型付きnative protocol用です。

再構築パイプラインは、公開前に `Update-SsmsWorkspace.ps1 -Operation Stage -GenerationManifest <generation.json> -OutputDirectory <新しいstageディレクトリ>` を実行します。生成される接続、表・ビューのSELECT、プロジェクトXMLと全SQL参照を検査し、所有マーカーとSHA256付き台帳 `managed-ssms-files.json` を作成します。最終公開先の既定はこのスクリプトのディレクトリです。別の場所へ公開する場合はStage時に `-PublishDirectory <公開先>` も指定します。

世代の切替成功後に `-Operation Publish -StagingDirectory <stageディレクトリ> -OutputDirectory <公開先>` で公開します。公開先とstageの所有情報・相対パス・SHA256を照合してから変更し、削除DBの旧生成SQLは台帳にあるファイルだけを削除します。空になった旧DB用ディレクトリも削除します。編集された生成ファイル、パス逸脱、リンクを介した参照、所有情報の不整合があれば処理を中止します。手書きSQLを削除しません。複数の公開処理はファイルロックで排他し、公開途中の失敗時は変更したファイルをバックアップから戻します。バックアップはstage内に残ります。

`-Operation Validate -OutputDirectory <stageまたは公開先>` で再検証できます。既定の `Update` は一時stageの生成・検証と公開を順に行う互換入口です。既存の台帳がない初回は、同一内容の生成SQLだけを引き継ぎます。以前のプロジェクトXMLはプロジェクト名が一致するものを引き継ぎ、以後は台帳のハッシュで編集を検出します。台帳外の既存ファイルは削除しません。

共通の手書きケースは `../Cases/Common/` にあり、2プロジェクトから同じファイルを参照します。`Read-AggregateJoinView.sql` は集計・JOIN・VIEW、`Fixture-CRUD-CommitRollback.sql` はfixture DBのCRUDとCOMMIT/ROLLBACKを扱います。生成されたSQLは閲覧用で、更新スクリプトはSQLを実行しません。

`Test-SsmsWorkspace.ps1 -TestRoot <新しい検証ディレクトリ>` は公開の回帰確認です。BGO経由でworkspaceのS、検証出力ディレクトリのXを取得して実行します。削除DBの掃除、手書き保持、編集済み生成物保護、所有情報、台帳、XML参照、リンク拒否を検証します。SSMSアプリ上のロード確認は別の検証項目です。
