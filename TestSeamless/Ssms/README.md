# SSMS 比較ソリューション

SSMS 22 では `TestSeamless.Compare.sln` を「プロジェクト/ソリューションを開く」から開きます。これは SSMS の SQL Server Scripts solution です。`SQLServer` と `SeamlessDB` の2プロジェクトが比較元と比較先の接続を保持します。元サーバーの接続先とWindows認証方式は世代manifestの `SourceServer` / `SourceIntegratedSecurity` から読みます。接続文字列の環境変数やパスワードを生成処理に渡す必要はありません。比較先は `127.0.0.1,14333`、SQL認証の `sa` です。このマシンの既定 SqlClient で `localhost` が接続前にタイムアウトしたため、比較環境では IPv4 アドレスを指定しています。パスワードは保存せず、SSMS接続時に入力します。テスト EXE の接続文字列は `ConnectionStrings__Seamless` で上書きでき、SSMS プロジェクトの接続設定はその環境変数では変わりません。14335は型付きnative protocol用です。

再構築パイプラインは、公開前に `Update-SsmsWorkspace.ps1 -Operation Stage -GenerationManifest <generation.json> -OutputDirectory <新しいstageディレクトリ>` を実行します。生成される接続、表・ビューのSELECT、プロジェクトXMLと全SQL参照を検査し、所有マーカーとSHA256付き台帳 `managed-ssms-files.json` を作成します。最終公開先の既定はこのスクリプトのディレクトリです。別の場所へ公開する場合はStage時に `-PublishDirectory <公開先>` も指定します。

SSMS 22のプロジェクト内のWindows認証名はUI言語に依存します。日本語は `Windows 認証`、英語は `Windows Authentication` で、異なる文字列はSQL認証として読み込まれます。生成時はPowerShellのUI言語から日本語・英語を選びます。SSMSの言語と生成環境が異なる場合は `-WindowsAuthenticationName 'Windows 認証'` 等で対象SSMSの保存名を明示してください。他言語の自動選択は行わず、同パラメーターを必須とします。パスワードは生成しません。既存の誤ったプロファイルは再生成してから読み直す必要があります。

ローカル比較用プロファイルには暗号化設定も明記します。比較元のlocalhostは `EncryptionOption=Mandatory`、TDSゲートウェイは `EncryptionOption=Optional` です。利用者が許可したローカル接続の `TrustServerCertificate=True` も保存します。この設定は現在のlocalhost/loopback専用生成経路のものであり、任意の外部サーバーへの接続設定ではありません。認証方式と暗号化方式は別々に設定します。

SeamlessのSQL Server互換profileは、未提供のmsdb管理機能への`HAS_DBACCESS('msdb')`照会に整数0（利用不可）を返します。SSMSは接続時にこの値を整数へ変換するため、能力照会として明示します。実登録DBとmasterは1、未知のユーザーDB名とNULL引数はNULLです。msdbを実DBとして明示登録した場合は登録状態の1を優先します。管理機能の可用性は比較元と異なり、AdventureWorks等の業務データの一致検証とは分けて扱います。

「ポリシーの正常性状態を表示できません」という通知は、未提供のmsdb管理機能についてのものです。比較元SQL Serverの権限を変更する必要はありません。DB一覧にAdventureWorksが出ない場合は別途、出力ウィンドウの失敗URNとSQLエラーを確認します。実際に`Server/Database/Option`がNTEXT RPC（0x63）で失敗したため、NTEXT入力と実SDKが生成するOption全30列・Status付きDB一覧を回帰検証の対象にしています。限定した列指定のSMO照会や接続成功だけを、オブジェクトエクスプローラーの表示成功とは扱いません。

世代の切替成功後に `-Operation Publish -StagingDirectory <stageディレクトリ> -OutputDirectory <公開先>` で公開します。公開先とstageの所有情報・相対パス・SHA256を照合してから変更し、削除DBの旧生成SQLは台帳にあるファイルだけを削除します。空になった旧DB用ディレクトリも削除します。編集された生成ファイル、パス逸脱、リンクを介した参照、所有情報の不整合があれば処理を中止します。手書きSQLを削除しません。複数の公開処理はファイルロックで排他し、公開途中の失敗時は変更したファイルをバックアップから戻します。バックアップはstage内に残ります。

`-Operation Validate -OutputDirectory <stageまたは公開先>` で再検証できます。既定の `Update` は一時stageの生成・検証と公開を順に行う互換入口です。既存の台帳がない初回は、同一内容の生成SQLだけを引き継ぎます。以前のプロジェクトXMLはプロジェクト名が一致するものを引き継ぎ、以後は台帳のハッシュで編集を検出します。台帳外の既存ファイルは削除しません。

共通の手書きケースは `../Cases/Common/` にあり、2プロジェクトから同じファイルを参照します。`Read-AggregateJoinView.sql` は集計・JOIN・VIEW、`Fixture-CRUD-CommitRollback.sql` はfixture DBのCRUDとCOMMIT/ROLLBACKを扱います。生成されたSQLは閲覧用で、更新スクリプトはSQLを実行しません。

SSMS 22のSQLファイルの`FullPath`は、ソリューションを置いたディレクトリを基準に解決されます。`Cases`の参照は`..\Cases\...`、生成SQLは`SQLServer\Generated\...`または`SeamlessDB\Generated\...`とします。プロジェクトのサブディレクトリを基準にしません。Stageでは最終公開先を基準に共通SQLの実パスを確認し、生成SQLはstage内の同じ相対パスで管理台帳と照合します。別ドライブにある手書きSQLは絶対パスで参照します。プロジェクト参照の更新後はSSMSのソリューションを読み直してください。

`Test-SsmsWorkspace.ps1 -TestRoot <新しい検証ディレクトリ>` は公開の回帰確認です。比較環境の `Invoke-LocalProcess.ps1` を通して最大8論理CPUで直接実行します。ソリューション基準での共通/生成SQL参照、削除DBの掃除、手書き保持、編集済み生成物保護、所有情報、台帳、XML参照、リンク拒否、日本語・英語の認証名と旧生成入口を検証します。SSMSアプリ上のロード確認は別の検証項目です。
