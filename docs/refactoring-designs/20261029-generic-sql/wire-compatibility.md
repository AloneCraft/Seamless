# 実クライアントの通信検証

## 現在の判定
世代 `20261030T032530680Z-3b7609ad`（package `0.0.0-ssms.20261030031940904`）はValidatedとなり稼働中である。AdventureWorks2016とfixtureの計94対象で取り込み全値とTDS全値・metadataが一致し、基本SQL・生成関数・SQL/native比較も成功した。一方、利用者の実SSMSではDB一覧にAdventureWorksが表示されず、`Server/Database/Option` の取得時に `RPC data type 0x63 is not supported` が記録された。既存の限定したSMO API試験の成功を、この画面の合格とは扱わない。値を空文字・切り捨てに置き換えない。

## DB一覧とOption照会の相互運用
NTEXT（0x63）のRPC入力は、最大バイト長、collation、実バイト長、UTF-16値を順に読み取る。NULL、空文字、長さ不正、後続パラメーター、`sp_executesql` のSQL本文自体をNTEXTにする場合を実SqlClientで検証する。ROW出力のtext pointer形式はRPC入力へ混在させない。

インストール済みのSSMS SDKのSFC Enumeratorが実際に生成するDB一覧とOption照会をembedded SQLへ保存する。`EngineEdition=3` のprofileではAzure Edge専用のデータ保持IF分岐は実行されず、その既知の空一時表の式をfalseへ正規化する。CREATE＋IFとcleanupの全文トークン一致を必要とし、分割送信の状態は接続内だけに保持し、接続resetで破棄する。一般DDLや利用者の書き込みへのread-only制限は維持する。既知のStatus式だけは整数の状態マスクとしてhexリテラルを10進表現へ正規化し、業務SQLのbinary値には適用しない。

`sys.databases` は既存18列を維持して27列の管理属性を加える。ANSI式の既定値と未提供のSQL自動統計・縮小・Broker・full-text・Ledger等の無効状態を返し、元SQL Serverの機能設定を複製したとは扱わない。`sys.database_mirroring` は型を持つ空集合を返す。DB名、read-only、collation、system/snapshot分類は実際の登録DBから評価する。実SFCのOption全30列とStatusを含むDB一覧を、従来の明示した少数フィールドの照会に加えて受け入れ対象とする。

修正前は実SFCのSQLをNTEXTで送る2件がread-only DDLとhex解釈で失敗し、修正後は成功した。接続間分離、分割送信、cleanup、reset、改変IFと後続DELETEの拒否、一般binary値の保持を含むGateway全125件がDebug/Release各125件成功、skip 0。証跡は `F:/CodexTemp/SsmsLockTimeout/database-options-before-03.trx` と `database-options-all-debug-03.trx` / `database-options-all-release.trx`。全回帰で判明したHTTP試験のポート競合はnativeの動的ポートへ分離し、JSONの日本語はUnicode escapeを復号して列名を確認するよう試験を修正した。

package `0.0.0-ssms.20261030055409051` のみを導入したprivate fixtureで、実SSMS SDKのOption全30列、Status付きDB一覧、既存7種のSMO操作、msdb能力照会、接続設定が成功した。製品依存12件が同版のローカルフィード由来であることを検証した。証跡は `F:/CodexTemp/SmoOptionsPackage-v15-process/`。これは14333への反映や実SSMS画面の合格を意味しない。

既定master接続の追加試験では、未修飾の `sys.database_mirroring` を仮想masterに解決できず208となることが判明した。仮想masterでもこの空カタログだけを解決する修正を加えた。通常DB/master×Option/Listの4経路を含むGateway全127件がDebug/Release各127件成功、skip 0。証跡は `F:/CodexTemp/SsmsLockTimeout/database-options-master-before.trx` と `database-options-master-all-{debug,release}.trx`。上記private fixtureの旧成功は通常DB接続の結果であり、master修正の配置済み証拠には使用しない。

## SSMS階層定義が要求するメタデータ
SSMS 22のObjectExplorer.dllにあるSQL Server 16.0用定義は、DBのOptionに加えて12項目の親DBプロパティを要求する。派生のParentColumns/Columnsは祖先の値を置換し、要求生成時にはName/Urn等の最低限の列、filter/optional列を追加する。祖先のDatabaseGuid等を単純にunionしない。DLLのSHA-256は`49B31E9A7113D155416C7357AE370FF4914B288325BFF306389A256B3EEB22D1`。抽出XMLとILに基づく契約であり、実画面の通信traceと混同しない。

DB一覧の既知HADR setup/cleanupは全トークンで照合する。HADR非提供profileでは、既知の空同期状態temp表への参照を型付きの空derived tableへ正規化し、残りのSELECTを共通エンジンで評価する。一般temp DDL、HAS_PERMS_BY_NAME全般、Always On本体を実装するものではない。分割送信の状態は接続内に限定し、4文のcleanupとresetで破棄する。SQL本文の余分な文や改変setupを隠さない。

SQL Server用カタログの問い合わせでは、SERVERPROPERTYの既知profile値とsysname別名をGatewayで型付きの式へ正規化する。QUOTENAMEと整数属性IsSchemaBoundに限定したOBJECTPROPERTYEXは、既存のISqlCatalog経由で解決する。CoreからGatewayへ依存を追加しない。sql_variant完全対応は含まない。ビューのschema bindingは完全なCREATE/ALTER VIEW定義から取得し、判定不能はNULLを保持する。

表・ビュー・ストアド・関数の表示用に、未提供機能の無効状態と空のextended_properties/system_sql_modules/data_spaces、all_sql_modulesの別名を提供する。元のXML/空間列の有無は列型から評価する。主キーなし表にもheap行を返し、SSMSのsys.indexesとのINNER JOINで表を消さない。既存の主キーID・型の対応は維持する。これらのカタログは互換表示用の論理情報であり、SQL Serverの物理索引・partition・Ledger実装の同一性を示さない。

階層定義相当のSFC要求5種（親DB＋Option、Table160、View160、Procedure120、Functions）は、比較元で成功し、旧候補ではDECLARE/カタログ不足で5種とも失敗した。修正後の実TDS試験とGateway全136件はDebug/Release各136件成功、skip 0。さらに実SSMS SDKが16.0向けに生成する要求5種がsource診断とPackageReferenceだけのfixtureの両方で成功した。製品依存12件がpackage `0.0.0-ssms.20261030070500980` のローカルフィード由来であることを確認した。証跡は`F:/CodexTemp/SfcObjectExplorer-source-01/`、`SfcObjectExplorer-v16-before/`、`SfcObjectExplorer-source-server-01/`、`SmoObjectExplorerPackage-v17-process/`、`SsmsLockTimeout/object-explorer-all-{debug,release}-01.trx`。小fixtureの成功は実データ全件検証・14333反映・利用者の実画面成功と別の判定である。

稼働情報は `D:/Seamless/Comparison/Managed/active.json`、検証結果は同管理領域の `generations/20261030T032530680Z-3b7609ad/validation.json` に保存する。利用者指示により共有調停器を使用せず、所有する比較処理とサーバーは共通のaffinity `0xFF`（最大8論理CPU）で実行する。外部のSQL ServerサービスのCPU制限を変更したという意味ではない。

## 修正・確認済み
| 項目 | 観測した原因と変更 | 検証 |
|---|---|---|
| sys.columns.collation_name | 数値列のNULLを返す一方metadataが非NULLだった。NULL属性を修正 | 実SqlClientのNULL/非NULL行・metadata・終端がDebug/Release成功、SystemCatalog回帰各6件成功 |
| 空のPLP値 | total=0の後にゼロchunkとterminatorを重複出力していた。正長chunkだけ出力し終端を1つにした | 修正前nvarchar/varchar/varbinaryの3件失敗。修正後varchar/varbinaryの空/NULL/大値/後続列と行はDebug/Release成功。Unicode PLPは別のchunk配置修正で初回読取も成功 |
| packet-size交渉 | 4096-byte送信を行う一方でENVCHANGE type4が欠落。選択値4096を明示 | 要求512/4096/8000/8192に対する実PacketSizeとraw通知5件がDebug/Release成功。Unicodeのchunk配置は別の契約で検証 |
| IPv6 loopback | IPv4だけの待受から、IPv4とIPv6専用loopbackの同一portへ変更。起動片側失敗は両側解放 | 実SqlClient直接IP・port0・停止/再起動/接続中停止と競合cleanupをDebug/Releaseで確認 |
| PRINT | 既存SQL型評価を使い文字/整数/decimal/NULLを通知として送信 | INFO内容・番号/class/state・SELECTとの順序・ExecuteNonQueryをSQL Server実機とDebug/Releaseで比較 |

## Unicode PLPの相互運用条件
実SqlClient 7.0.0で長いnvarchar(max)を読む際、所有clientのpacket残量が負になり文字コピーで例外になることを観測した。独立decoderの値復元は成功しており、MS-TDSがUnicode packet跨ぎを禁止するとは解釈しない。

送信側は[Unicode PLP送信設計](plp-streaming.md)に従い、Unicode PLPの各正長chunkを最大4084bytesとし、chunk headerとdataを一つのpacketへ収める。これにより文字読取要求をpacket内の値データ範囲に限定する。行を先にserializeする既存の16MiB上限、NULL/空、total/terminator、後続列を保持し、paddingや待ち時間を加えない。chunk範囲のリストは結果内で再利用・行ごとClearし、全応答は蓄積しない。

Debug/Releaseの関連試験は各30/30成功、skip 0。大値、ANSI/binary、NULL/空、2 Unicode列、境界前後、surrogate pair、後続列/行、packet交渉、PRINT、catalog、loopback、SharedV2基礎契約を含む。独立新process初回は同期/非同期×SequentialAccess有無×Debug/Releaseの全8条件で成功した。値は独立decoderとSQL Serverの期待結果で確認している。

証跡はF:/CodexTemp/SeamlessLargeValueAcceptance/Selected-{Debug,Release}.trxおよびPLP-first-*.jsonl。旧失敗・切り分け記録も同成果物フォルダーに保持する。実データ全体は上記Validated世代で別途照合した。この30件を全製品/全GUI合格へ拡大解釈しない。

## 環境依存: localhost
同じprivate Gatewayに127.0.0.1は接続可能、既定native SNIのlocalhostはIPv4/IPv6 listenerへ到達しない。独立した診断で[公式managed networking switch](https://learn.microsoft.com/ja-jp/sql/connect/ado-net/appcontext-switches?view=sql-server-ver17)を接続前に設定した場合は到達した。製品の既定switchは変えていない。

比較環境の既定は利用者が許容した127.0.0.1,14333に明示し、EXEのConnectionStrings__Seamlessによる上書きを維持する。元SQL Serverのlocalhost/Windows認証は元設定を維持する。localhostの制約をIPv6追加で解消したとは報告しない。診断は`Work/TestSeamless/Implementation/tds-localhost-diagnostics/`に保存した。

## 実SSMS
.ssmsslnをSSMS22が拒否したことと、同じ内容の.sln診断コピーで両projectが読めたことを実UIで確認した。正式入口はTestSeamless.Compare.sln。元SQL ServerへのGUI接続（localhost、Windows認証14700F\kazuh、AdventureWorks2016）は利用者が明示承認済みである。未接続を実操作成功に数えない。XML/台帳の回帰25件成功は実SQL実行とは別に扱う。正式.slnの2プロジェクト読み込みは実UIで確認した。

旧版SSMS初期化の最小再現はSERVERPROPERTY列のInt32→String変換エラーだった。SQL別名によって値・型が変化しない修正、SSMS22の実初期化バッチ、sys.databasesの不足列を検証し、上記パッケージと稼働世代へ配置した。パッケージだけを参照する小fixtureのSMO APIはVersion、Edition、DB、Tables、Views、StoredProcedures、UserDefinedFunctionsの7操作が成功した。これは実SSMS画面の合格とは別である。SERVERPROPERTYの生の戻り型は現在基底型であり、SQL Serverのsql_variant完全互換ではない。

実UIではSeamless接続後に `SELECT DB_NAME() AS [DatabaseName]` がAdventureWorks2016を返した。一方、クエリエディターの設定適用は `SetRowCountStatement` 未対応で失敗した。`SET ROWCOUNT 0` を送る独立SqlClient試験でも修正前の同じエラーを再現した。無制限を表す0だけを受理し、未対応の正の制限値は40515で拒否する修正後、SSMS初期化・カタログ関連はDebug/Release各10件成功した。後続SELECT・接続再利用も含む。同修正は現在の稼働版に含む。実SSMS再確認は別の受け入れ工程として残る。

生成接続のWindows認証が引き継がれなかった別原因は、SSMSのプロジェクト形式がUI言語に依存することだった。インストール済みSSMS 22のConnectionNode setterは認証名をIntegratedSecurityリソースと厳密比較する。日本語のリソース値は `Windows 認証`、英語は `Windows Authentication` だった。生成側の言語選択と明示名指定を修正した。暗号化設定の省略もなくし、元localhostにはMandatory、TDSにはOptional、許可済みローカル証明書信頼を記録する。公開保護を含む34件が成功した。公開中のプロジェクトは利用者の手動操作中には更新しない。

修正前後の証跡は `F:/CodexTemp/SsmsSettingsTrial/RowCount-before.trx`、`RowCount-after-{Debug,Release}.trx` と `F:/CodexTemp/SsmsAuthenticationTrial/` に保存する。認証名の不一致も修正前に試験で失敗し、修正後は旧入口と新入口の日本語/英語の出力を確認した。SSMS実UIの再確認は別の受け入れ工程である。

世代 `20261029T182936330Z-7e135def`（package `0.0.0-ssms.20261029180739974`）は全件検証に成功したValidatedの未採用世代である。94対象760,846行の取り込みとTDS照合、生成受け入れ、SQL/native75ケース、元DB一覧の再照合が成功した。`SET ROWCOUNT 0` は含むが、次節の包含認証プローブ修正は含まないため、同世代を稼働版へは切り替えていない。

元SQL ServerへのWindows認証接続はSSL証明書チェーンの検証で失敗した。このローカル接続に限ったサーバー証明書信頼設定は利用者が許可済み。利用者の指定によりSSMS画面操作は利用者が担当し、こちらは当該SSMS PIDだけの一時Extended Eventsで通常SQL Server接続時の初期化SQL/RPCを採取する。既存のdefault trace・XEvent構成と実行キャッシュには対象のSQLバッチが残っていなかったため、それらを採取成功とは扱わない。

手動操作との時刻調整を不要にするため、`TestSeamless/Diagnostics/Start-SsmsSqlTrace.sql`を用意した。元SQL Serverで利用者が実行し、その後に同じSSMSから新規クエリを開く。クライアントPIDはSQL Serverの実セッションから取得する。時間による終了はなく、同じPIDの重複開始を拒否し、結果回収時に所有セッションだけを削除する。バッファー上限等はDiagnostics/READMEに記す。実SQL Server上の独立API fixtureで、SQL文の採取・重複拒否・削除を確認した（`F:/CodexTemp/SsmsManualTraceProbe-01/result.json`）。このfixtureは実SSMSの証拠には数えない。

## SSMSの包含認証プローブ
利用者による診断用14334番への接続で、SSMSが送信したSQLをTDS転送器で採取した。転送先は同じ14333番であり、接続エラー40515と `CheckIfContainedAuthenticationIsUsed` のスタックも利用者から報告された。採取対象はSQL_BATCHとRPCのSQL文だけで、LOGIN7やパラメーター値は記録しない。採取後、所有する転送器は停止した。

実際のバッチは `USE [AdventureWorks2016]`、`IF (db_id() = 1)`、masterでは `SELECT 0`、それ以外では `sys.dm_exec_sessions.authenticating_database_id` を動的SQLで調べる処理、同じDBへの `USE` から成る。一般のIF文が未対応のため、SSMSの接続確認バッチを構文解析した後、IF文の実行処理で拒否されていた。元SQL Serverへ同じWindows認証で接続した独立照会では `authenticating_database_id=1`、包含認証判定値0だった。

`SqlServerConnectionProbes` は、この構造と内側SQLをAST・トークンで厳密に検証する。現行のサーバーログイン方式を表す値0をintで返し、既存USE処理でDB存在確認と接続コンテキスト変更を行う。条件、内側照会、末尾の文が異なる場合はこの互換処理を適用しない。不明DBは4060、一般のIFは40515のままとする。一般IFや任意の動的SQLの実装として扱わない。

ソース回帰は修正前2件失敗、修正後Debug/Release各17件成功（skip 0）。master/通常DB、結果型、単一結果、DB変更通知、後続DB_NAME、未知DB、変更された条件・動的SQL・余分な文の拒否を含む。証跡は `F:/CodexTemp/SsmsContainedTrial/Contained-{before,after-Debug,after-Release}.trx`。

修正版 `0.0.0-ssms.20261029193430578` の19パッケージを作成し、専用fixtureで12製品依存の版・配布フィードを照合した。SSMS同梱SMOの `IsContainedAuthentication` をDB名指定で呼ぶ同一試験は、旧版では40515で失敗、新版では非包含認証として成功した。新版ではVersion、Edition、DB、Tables、Views、StoredProcedures、UserDefinedFunctionsの7操作とROWCOUNT初期化後のSELECTも成功した。証跡は `F:/CodexTemp/SmoContained-v8-control-process/` と `F:/CodexTemp/SmoContained-v9-final-process/`。SMOのCapturedSqlは当該内部接続処理を保持しなかったため、送信SQLの証拠には実SSMSの `F:/Seamless/Comparison/tds-sql-trace-04/sql.jsonl` を用いる。

この版の世代 `20261029T194244557Z-33e2fd97` は94対象760,846テーブル行の取り込み照合と全値のTDS照合、SQL/native75ケース、生成DB受け入れ8項目、CRUD・取引・準備済みRPC、元DB一覧再確認に成功し、Validatedとなった。所有するprivate検証サーバーのPIDを照合したうえで、実AdventureWorksにもSSMS同梱SMOから接続した。元SQL Serverとテーブル71件、ビュー20件、ストアド10件、関数11件の名前が一致した。生成メソッド21件の宣言型・メソッド名・C#ソース・Stub状態も世代manifestと一致し、非Stubは3件だった。証跡は `F:/CodexTemp/SmoAdventureWorks-v9-candidate/result.json`（`UiVerified=false`）。

2026-10-29 20:13 UTCに14333番へ切り替え、正常性確認とSSMS資産の公開が成功した。稼働後のSMO照合も成功し、同じ71/20/10/11件のオブジェクト名と生成メソッド21件の対応を確認した（`F:/CodexTemp/SmoAdventureWorks-v9-live/result.json`）。両サーバーへ同じ `Cases/AdventureWorks/Basic.sql` をパッケージ利用EXEから送信し、4結果の20/67/20/290行の値・列名・型・行順が一致した（`F:/CodexTemp/LiveBasic-v9/result.json`）。RecordsAffectedはこの結果比較に含めず、SQL実行検証と区別する。実SSMSの再接続は未確認であり、SMO APIの成功をGUI受け入れへ読み替えない。

## SSMSのロック待機設定
利用者の実再接続は包含認証確認を通過し、次の `SET LOCK_TIMEOUT 10000` が40515で拒否された。診断用14334番への利用者操作で実SQLを採取した（`F:/Seamless/Comparison/tds-sql-trace-05/sql.jsonl`）。所有する転送器は採取後に停止した。従来のSMO試験はLockTimeoutプロパティを設定しておらず、この操作を覆っていなかった。

[ロック待機の設計と対応範囲](../20261030-sql-lock-timeout.md)に、接続単位の値、期限と取消、Core/C12との境界、0を含む未対応値の拒否、試験結果を記す。新NuGet `0.0.0-ssms.20261029230935293` の19パッケージを作成した。PackageReferenceだけのSMO fixtureで12製品依存の版・フィードを照合し、LockTimeout=10による10000ミリ秒の設定、包含認証確認、既存7操作、ROWCOUNT初期化が成功した（`F:/CodexTemp/SmoLockTimeout-v10-process/`）。

独立レビュー後の補強を含む最終回帰はDebug/Release各40件成功した。新世代 `20261029T231605529Z-49aa1297` は取り込み時の94対象が一致した。所有する検証用14343番へSMOから接続し、10000ミリ秒を設定した状態で元SQL Serverと表71/ビュー20/ストアド10/関数11の名前、生成メソッド21件の対応、共通Basic.sqlの4結果の値・行順・CLR列情報が一致した（`F:/CodexTemp/SmoAdventureWorks-v10-candidate-02/result.json`）。手書きSQLのSHA256は、Windows PowerShellでも使用できる.NET APIで照合する。

同世代は94対象のTDS全値・列情報、SQL/native75ケース、生成DB受け入れ8項目、CRUD・取引・準備済みRPCと元DB一覧の再確認にも成功した。2026-10-30 08:47 JSTに14333番へ切り替え、正常性確認とSSMS資産の公開が成功した。更新後のPackageReference利用EXEでも共通Basic.sqlの20/67/20/290行の値・列名・論理SQL型・行順が一致した（`F:/CodexTemp/LiveBasic-v10/result.json`）。その後の利用者による実SSMS再接続で、次節のクエリエディター設定と管理バッチの障害が判明した。

## 実SSMSのクエリエディター設定と管理バッチ
上記版への利用者接続では、SetTextSizeStatementの40515が残った。診断用14334番では先に#SVer管理バッチが3906となる別経路も採取した。比較DBの読み取り専用を解除せず、[初期化設計](../20261030-ssms-session-initialization.md)に従い既知管理バッチを全トークンで照合して既存の互換応答へ接続した。

利用者に本物のSQL Serverで新規クエリを開いてもらい、同じSSMS PIDだけのXEventで92イベントを取得した（`F:/Seamless/Comparison/ssms-source-xevent-02/`、切り詰めなし）。所有するXEventは回収後に削除し、転送器も停止した。実送信設定はTEXTSIZE2147483647、QUERY_GOVERNOR_COST_LIMIT0、DEADLOCK_PRIORITY NORMALを含む。未対応の非既定値を無視せず拒否する。

旧3結果初期化の広範なContains分岐も厳密照合へ変更した。読み取り専用保持、改変/追加SQL拒否、NULL metadata、Unicode/binary非切捨て、実設定バッチ全体、大値の元SQL Server読取を含むDebug/Release各67件が成功、skip 0。独立レビューの具体的指摘を修正し再レビュー済み。

NuGet `0.0.0-ssms.20261030003108756` の19パッケージを作成し、PackageReferenceのみのSMO fixtureで12製品依存の版・フィードを照合した。実管理バッチと設定一式、既存SMO操作、ROWCOUNT初期化が成功した（`F:/CodexTemp/SmoInitialization-v11-process/`）。

新世代 `20261030T003606639Z-0439e74d` は取り込み時の94対象が一致した。候補サーバーにもSSMS同梱SMOから接続し、実管理バッチと設定一式を実行後、表71/ビュー20/ストアド10/関数11の名前、生成21メソッド、共通Basic.sqlの4結果が元SQL Server・世代情報と一致した（`F:/CodexTemp/SmoAdventureWorks-v11-candidate-02/result.json`）。94対象のTDS全値・列情報、SQL/native75ケース、生成DB受け入れ8項目、CRUD・取引・準備済みRPC、元DB一覧の再照合も成功し、Validatedとなった。

2026-10-30 10:04 JSTに14333番へ切り替え、正常性確認とSSMS資産の公開が成功した。ホスト上でPID109720と生成時刻・affinity 0xFF・待受の所有者を照合した。稼働先にもSMOで実管理バッチと設定一式を実行し、表71/ビュー20/ストアド10/関数11の名前、生成21メソッド（実装済み3）、Basic.sqlの4結果が一致した（`F:/CodexTemp/SmoAdventureWorks-v11-live/result.json`）。PackageReference利用EXEでも同じBasic.sqlの20/67/20/290行の値・列名・論理SQL型・行順が一致した（`F:/CodexTemp/LiveBasic-v11/result.json`）。両証跡のUiVerifiedはfalseであり、自動試験を実UI合格とは扱わない。

その後、利用者がこの版への実SSMS再接続と「新しいクエリ」の作成の両方に成功したと報告した。接続設定の適用エラーは実画面でも解消した。SQLファイル参照修正後のBasic.sqlの実行結果は次節に記す。オブジェクト展開は別の確認として残る。

## SSMSのSQLファイル参照
利用者がソリューションからBasic.sqlを開くと、元の`..\..\Cases\AdventureWorks\Basic.sql`が`G:/GitHub/Seamless/Cases/AdventureWorks/Basic.sql`へ解決され、存在しないため開けなかった。SSMSの実解決基準はソリューションディレクトリで、生成と検証がともにプロジェクトディレクトリを基準としていた。接続/SQL実行処理とは別の資産生成不具合である。

Updateと旧Invokeの両入口を修正し、共通SQLは公開先のソリューション基準、生成SQLは各sideを先頭に持つパスとした。Validateも同じ基準で管理台帳または許可した手書きSQLの実体へ照合する。実公開先を想定したBasic.sqlの完全解決と生成SQLのファイルオープンは修正前に失敗し、修正後の公開・編集保護・削除保護を含む44項目はPowerShell 7/Windows PowerShellそれぞれ成功、Unavailable 0だった（`F:/CodexTemp/SsmsPaths-after-{pwsh,windows}-02-process/`）。独立した読み取りレビューでも追加の具体的欠陥は指摘されなかった。

稼働世代から両プロジェクトを再生成・公開し、SQLServer99参照/SeamlessDB100参照の欠落0を確認した。旧ファイルは`D:/CodexTemp/SsmsPaths-v11-stage/publication-backup-7a193978d12f48e796276304d9313457/`へ退避した。DBデータや製品パッケージの変更はない。利用者はソリューションを開き直した後、Basic.sqlを開けて4結果（20/67/20/290行）すべての実行に成功したと報告した。実UIの成功はこの利用者報告を根拠とし、全値の一致は上記の独立した自動比較で確認している。

## SSMSのユーザーDB一覧
利用者の画像では、SeamlessのデータベースフォルダーにシステムDB等の子フォルダーしか表示されなかった。稼働先の通常SELECTではAdventureWorks2016（ID5）とfixture（ID6）が登録済みである一方、過去に実SSMSから採取した分類SQLは`dtb.is_distributor`が存在せず失敗した（`F:/CodexTemp/SsmsDatabaseList-before-02/result.json`）。ソース側の同SQLもmaster/fixture接続の両方で失敗した。これまでのSMO試験はDB名だけの列挙で、ユーザー/システムの分類まで検証していなかった。

`sys.databases`の末尾に非NULLのbit列`is_distributor`を追加し、既存ordinalを維持する。Seamlessはレプリケーションの配布DBを持たないので値はfalseとする。列を追加した後、同じ分類SQLの`HAS_DBACCESS(dtb.name)`も通常の式バインドで未対応と分かった。既存のsysカタログ関数解決へ接続し、登録DBとmasterは1、未知名とNULLはNULLを返す。現行認証では登録DBはすべてアクセス可能で、読み取り専用は読み取りアクセスを妨げない。未提供のACLやOFFLINE状態を完全互換と主張しない。

関数の処理はGatewayのカタログに置き、Query側は既存のroutine型契約で評価する。通常のscalar呼び出しも固定値の互換応答を経ず実評価へ渡す。計画はセッション/取引/列挙子を保持せず、同一計画の再実行時もRegistryの登録名を照会する。後から順次Registerした名前が古いスナップショットで拒否されないことを回帰する。実行とRegisterの同時並行更新は既存Registryの同期契約の範囲であり、今回追加しない。

SMOの`Server/Database[@IsSystemObject=false() and @IsDatabaseSnapshot=false()]`という列挙条件を追加検証すると、`source_database_id`の不足も再現した（`F:/CodexTemp/SmoDatabaseList-v12-snapshot-process/`）。末尾ordinal17へnullable intを追加し、非snapshot DBの値はNULLとする。列定義と全行の位置を一致させる。検証にはSMOの`Enumerator.Process(...).Data`が返すDataTableを使い、DatabaseのCLRプロパティが存在しない値をPowerShellの空値で代用しない。

実採取の8列SQL、ユーザーDBと非snapshot DBの抽出、非NULL bit、nullable int、ID、読み取り専用条件、未知名/NULL、大文字小文字、計画再利用を含めDebug/Release各69件成功、skip 0（`F:/CodexTemp/SsmsLockTimeout/database-snapshot-final-{Debug,Release}.trx`）。独立レビューでも追加の具体的欠陥は指摘されなかった。不足の残るpackage`0.0.0-ssms.20261030013535036`の世代`20261030T014714469Z-9e3f46d8`は稼働へ反映せず、所有プロセスのPID/生成時刻を照合して検証を中止し、データと記録を保持した（`F:/CodexTemp/SsmsDatabaseList-v12-aborted.json`）。次のpackage`0.0.0-ssms.20261030020158671`は後述の実UI回帰により不採用となった。

PackageReferenceのみのfixtureは12製品依存の版/フィードを照合し、同梱SMOでユーザー/非snapshot条件とアクセス判定、既存管理バッチ・設定一式を検証して成功した（`F:/CodexTemp/SmoDatabaseList-v13-process/`）。新世代`20261030T021252119Z-f47b1e3d`の実データでも同じSMO列挙条件でAdventureWorks2016とSeamless_Comparison_Fixtureの2件が返り、表71/ビュー20/ストアド10/関数11、生成メソッド21件とBasic.sqlの4結果が一致した（`F:/CodexTemp/SmoAdventureWorks-v13-candidate/result.json`）。取り込み時の94対象も一致した。候補の自動試験と、稼働先への反映・実SSMS表示の確認を区別する。

## msdb管理機能の可用性と接続時のDBNull
同世代は94対象のTDS全値/metadata、SQL/native75ケース、CRUD・取引・RPCと元DB一覧の検証にも成功し、2026-10-30 12:04 JSTに14333番へ反映した。稼働先のSMOとBasic.sqlも成功したが、利用者の実SSMS再接続でDBNullからの型変換例外が発生した。12:10 JSTに接続可能な世代`20261030T003606639Z-0439e74d`へ戻した。失敗世代は元manifestのバックアップとデータ検証結果を保持してState=Rejectedとし、`ui-acceptance.json`にGUI失敗を記録した。データ比較のPassedをGUIの合格へ読み替えない。

原因は、正常SQL Serverの実採取にも含まれる`select has_dbaccess('msdb')`だった。インストール済みSSMSの`ObjectExplorer.dll`を読み取り確認すると、`NavigableItem.get_IsMsdbDatabaseAccessible`は同SQLのExecuteScalar結果が参照nullでなければConvert.ToInt32へ渡す。SQL NULLのDBNullはこの条件を通過し、利用者と同じ例外になる。DLL内の観測は`F:/CodexTemp/SsmsMsdbConsumerIl-process/`、修正前の2件のInvalidCast再現は`F:/CodexTemp/SsmsMsdbAccess-source-before-process/`に保存する。

GatewayのSQL Server互換profileでは、未提供のmsdb管理機能へのアクセス照会を0（利用不可）とする。任意の未知DBを0へ変えない。NULL引数と未知のユーザーDB名はNULL、実登録名とmasterは1、未登録msdbだけ0とし、msdbを明示登録した場合は登録状態の1を優先する。これは管理機能の能力照会についての限定互換であり、任意の未登録DBについてSQL Serverの完全な戻り値互換を主張しない。偽のmsdbデータ/カタログ行を追加せず、SQL文字列や別名ごとの応答にも分けない。

同じSQL→整数変換と、同一キャッシュ計画でのmsdbの0→登録後1を追加し、Debug/Release各69件成功、skip 0（`F:/CodexTemp/SsmsLockTimeout/msdb-access-final-{Debug,Release}.trx`）。独立レビューでも具体的な追加欠陥は指摘されなかった。19製品パッケージ`0.0.0-ssms.20261030031940904`のPackageReferenceだけのSMO fixtureでも、msdb照会の整数変換・ユーザー/非snapshot列挙・接続設定一式が成功した（`F:/CodexTemp/SmoDatabaseList-v14-process/`）。新世代`20261030T032530680Z-3b7609ad`は取り込み/TDS94対象、SQL/native75ケース、生成受け入れ8項目、CRUD・取引・準備済みRPC、元DB一覧の再確認に成功してValidatedとなった。

2026-10-30 13:13 JSTに14333番へ反映し、再起動後の正常性確認とSSMS資産の公開が成功した。ホストのPID/生成時刻・待受所有者・affinity 0xFFも照合した。稼働先への直接SqlClient確認で、msdb能力照会は整数0として変換でき、AdventureWorks2016（ID5）とfixture（ID6）はIsSystemObject=false/IsAccessible=trueだった（`F:/CodexTemp/SsmsDatabaseList-v14-live/result.json`）。この確認を終えてから利用者へ再接続可能と通知した。

稼働先の同梱SMOによる再照合も成功した。msdb管理機能は利用不可、ユーザー/非snapshot列挙は2DB、表71/ビュー20/ストアド10/関数11、生成メソッド21件（実装済み3）が一致した（`F:/CodexTemp/SmoAdventureWorks-v14-live/result.json`）。PackageReference利用EXEで共通Basic.sqlの20/67/20/290行の値・列名・論理SQL型・行順も一致した（`F:/CodexTemp/LiveBasic-v14/result.json`）。これらのUiVerifiedはfalseであり、現版の実SSMS再接続・AdventureWorks表示の受け入れは利用者の確認待ちである。
