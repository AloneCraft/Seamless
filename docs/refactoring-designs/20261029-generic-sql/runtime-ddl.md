# 実行時DDLと必要なSQL制御文

## 背景・合意状態
利用者は2026-10-29、必要なvendor拡張を取引・TRY/CATCH・ROLLBACK・RAISERROR・PRINT・DDLに絞り、DDLについて「SSMSから実行時DDLも使う」と明示した。ビルド時のDDL取り込みだけでは満たさない。従来の初期DDL除外をこの合意で置き換える。本書は実装契約であり、DDLや制御文の完成報告ではない。

## 観測した現在の境界
- `SqlExecutionEngine.ExecuteCore` はSELECT、CRUD、取引と限定PRINTを扱う。限定DDLの実行分岐はソースへ実装済みで、TRY/CATCH/RAISERRORは未実装。PRINTは文字・整数・decimal・NULL、4000/8000文字等の上限と順序付きINFOを実装し、SQL Server実機との通知/ExecuteNonQuery比較をDebug/Releaseで確認した。
- `DatabaseContainer.GetTable` は同じ名前の既存表を要求した固定CLR型へキャストする。型のhot swap、DDLのDROP、スキーマ取引を提供する公開契約は確認できない。
- `InProcessDatabaseAdapter` は起動時に生成型から列・getter・codecを固定する。型の追加だけではSQLカタログは更新されない。
- SharedV2は全typed participantを事前登録してから回復・取引を開始し、取引開始後の追加登録を拒否する。別DBコンテナを併用して同一取引と称してはならない。
- 計画キャッシュには実参照DBの世代追跡、保持済み計画の再検査、未確定取引中の共有キャッシュ回避を実装した。Registryの動的revision・指紋対応は適用済みで、ALTER後の同一SQL再実行とsysカタログIDの全体回帰はCPU保留解除待ちである。
- PRINTはTDS INFOとして送信し、通知のみのDONE_COUNTを抑制する。ERROR出力のseverity/stateは引き続き固定で、TRY/CATCH等の実装には遅延列挙中のエラーも扱う必要がある。

## 目的・非目的
SSMSから基本的な表・ビューの作成、変更、削除とデータ操作を再現し、SQL Serverと結果・取引状態を比較する。C#/LINQ/FLinqの固定型高速経路を保持する。任意のvendor DDL、全管理ウィザード、任意ストアドの完全変換は目的にしない。

最初の実装単位はCREATE TABLE（明示列型・NULL・主キー）、ALTER TABLE ADD（NULL許容列）、DROP TABLEとする。CREATE/ALTER/DROP VIEWは定義・依存関係を伴う次の単位とする。型変更、既存行への非NULL列追加、DROP COLUMN、索引、routine DDL等は対応表で範囲を明示し、未対応を変更前に拒否する。この段階分割だけをDDL全体の完成に数えない。

## 保存と取引の不変条件
1. DDLのカタログと実データを同じDatabaseContainerの既存取引/WALへ参加させる。独自WALや別の確定手順を重ねない。
2. 未commitの定義を他接続から見せず、rollback・切断・障害回復で定義と行を一緒に戻す。DDL後の部分的なファイルやカタログだけを成功扱いにしない。
3. object IDはdrop/recreateで再利用せず、旧行・旧WALの復活を防ぐ。列順・SQL型・NULL・制約を実行とカタログで一致させる。
4. commit時にスキーマ世代を原子的に公開し、旧計画・準備済みSQLを再bindまたは明示拒否する。実行中reader/取引の世代の寿命を管理する。
5. 固定型クエリモジュールは対象指紋を検証し、不適合なら拒否する。SQLの動的列をnativeの全件object[]変換経路へ強制しない。
6. AdventureWorks等の元DB/比較コピーは読み取り専用。DDL・CRUDは専用fixtureで行い、変更前に明示backupを取得する。

## 実装候補と判断ゲート
生成時に固定CLR型の動的カタログ/行用backing tableを同一コンテナへ事前登録し、論理スキーマ変更を既存取引の行更新に載せる構成を候補とする。可変行のcodec、制約、SQL adapter、schema versionを別責務にする。固定backingと生成rootの通常commit・rollback・reopenは小fixtureで実証したが、異常終了や製品DDL経路の安全性は未検証である。新規typed tableの後付けと生成型のhot swapは現契約では保証されない。

実装前にSharedV2の事前登録/回復と永続世代への互換性を小fixtureで検証する。既存生成DBへbacking tableを追加すると取引catalog identityが変わり得るため、旧保存先を直接変更せず新世代とコピーで検証する。公開Core/C12契約の変更が実際に必要になった場合は当該変更を止め、対象と影響を報告する。

## 制御文の契約
SQL Server互換層が必要な構造構文をSeamless所有のcommand表現へ変換し、外部ASTやScriptDomの型を共通契約へ持ち込まない。OSSパーサーが受理しないTRY/CATCH・PRINT・RAISERRORは、文字列の単純分割で代用せずコメント・引用・ブロック・位置を保持して解析する。

PRINTと通知severityのRAISERRORを順序付きの通知として送り、SqlClient.InfoMessage/SSMSメッセージに表示する。エラーは番号・severity・state・lineを保持する。TRY/CATCHは遅延行列挙中の実行エラーも扱い、構文/bindエラー・取消・実行時エラーを区別する。CATCH時のERROR_*と取引状態を検証し、ROLLBACKしないまま失敗を成功へ変えない。

## 受け入れ手順
| ID | 前提・操作 | 期待結果・合格条件 |
|---|---|---|
| DDL-01 | backup済みfixtureにSSMSで表作成→INSERT/SELECT→nullable列ADD→再照会→DROP | 実行値・列metadata・一覧がSQL Serverと一致。解析成功だけでは合格にしない |
| DDL-02 | CREATE/ALTER/DROPをBEGIN内で実行しCOMMIT/ROLLBACK、別接続で照会 | 確定前後の可視性と取消結果が一致し、部分変更が残らない |
| DDL-03 | 古いprepare済みSQL/module、実行中readerに対しDDLを確定 | 誤った列型/旧データを返さず、世代契約に従って再bind/拒否する |
| DDL-04 | commit前後の切断/異常停止後に再起動し同じSQLを実行 | 宣言した耐久性に従い定義とデータが同時に回復。削除表が復活しない |
| DDL-05 | 重複名、不存在、不正型/制約、未対応DDLを実行 | 対象と理由を返し、実行前後でschema/dataを変えない |
| CTL-01 | PRINT→SELECT→PRINT、低severity通知、severity16、TRY/CATCH、取消 | 実SqlClient/SSMSで通知順序・結果・番号/state/line・捕捉対象が一致 |
| CTL-02 | 明示取引でエラー→CATCH→ROLLBACK/COMMIT、再接続で確認 | 状態・可視性・永続値を元SQL Serverと照合し、未実行を合格にしない |

Debug/Releaseのpackage利用試験と実SSMSを分けて記録する。永続化・回復・Core/C12回帰に未検証がある間は、runtime DDLを提供済みと説明しない。

## 実測した基礎契約
`RuntimeSchemaStorageContractTests` は固定2表の事前登録と生成rootを含め、catalog/dataとtyped表のcommit・rollback・独立readerの待機・通常reopenをDebug/Releaseで確認した（各2件）。通常DDLの解析/実行は別の独立SqlClient試験で確認した。異常終了回復、schema世代公開の全体回帰、稼働生成DBへの組み込みはまだ検証していない。この基礎試験をruntime DDL完成へ数えない。

## 中間基底と計画の実装条件
生成DBにだけ明示選択する中間基底で、動的カタログ・可変行の固定backingを事前登録する案を検証する。backingはprivate field/通常メソッドで保持し、public Tableプロパティとして元のSQLスキーマへ公開しない。Query→Databaseの追加依存はSQL利用側に限定し、Core→Queryの依存は作らない。

生成rootのコンストラクター前にstorage identityが評価されるため、そのoverrideはインスタンス初期化に依存させない。runtime profile/codec版を永続identityへ含め、旧世代を誤って開かない。既存importはLegacyで直接追加するため、この経路をSharedV2へ切り替える際は明示取引と予算内のバッチ投入を別に実装する。

計画の依存先には3部名とビュー経由の別DBがある。現在DBのrevisionだけのキャッシュキーは不十分であり、依存DB集合を追跡するか安全側の全体世代で無効化する。保持済みSqlQueryPlanも実行時に確認する。未commit定義で作る計画は共有キャッシュへ公開しない。DatabaseTransactionはCommit()呼び出し時でなくDispose()時に確定するため、Dispose後の単純なrevision更新では公開原子性を保証できない。永続カタログrevisionと読み取りscopeを合わせて検証する。

動的表の名前解決・sys.tables/sys.columns・SELECT/DML・read-only判定を同じカタログへ接続する。別DBへの書き込みを単一取引に含めたと説明しない。生成固定型表のALTERは初期単位では変更前に拒否し、SQLで新規作成した表のnullable列ADDから提供する。


## 固定 backing の保存 API（第1単位）

SharedV2 を明示した新規領域に限り、生成 root の中間基底は constructor の base 呼出し前に RuntimeSqlV1 profile marker とスキーマ identity を照合し、未認識または異なる既存領域を開かない。空の新規領域だけ marker を新設し、旧領域の自動移行はしない。base 構築後に catalog と row の固定 MemoryPack participant を登録し、その後に生成 typed schema を登録して通常の SharedV2 recovery を行う。既存 generator/Core/取引を変更しない。

永続 catalog は版付きの中立 DTO とし SQL 型名・長さ・precision・scale・collation・NULL・列順・PK を保持する。nvarchar(n) の n は1〜4000、varbinary(n) は1〜8000、MAXは-1で表す。System.Type を永続化しない。第1単位の型は int、bigint、bit、nvarchar、varbinary、decimal に限定し、不正属性、未対応型、値の暗黙変換・桁落ち・切捨て・丸めは拒否する。文字列 PK は照合順序の完全な比較契約が未導入のため拒否する。識別子の比較は第1単位では ordinal ignore-case とし、この制約をSQL互換の完成とは扱わない。

カタログ、行、次object/row IDとschema revisionは同一コンテナの明示取引で変更し、commit/rollbackを一体化する。DROPは論理削除し、古い行は非表示とする。確定したobject IDは再使用しない。ADDはnullable列だけを末尾に追加し、旧行にはNULLを補完する。読取は実列挙時にread scopeを取得して期待revisionと照合し、そのscopeの間だけ逐次行を返す。表数1024、列数128、行payload 16 MiBを上限とし、全表・全行のリスト化をしない。

行形式は明示されたversionとセル数、値の型別lengthと厳密な終端検査を持ち、未知タグ・切断・不正長・余分な末尾を拒否する。schema/catalog内容も読み取り時に検証する。Gateway、SQL DDL構文と固定typed表へのALTER、cross-database世代追跡および異常終了耐久はこの単位に含めない。


変更 API は `RuntimeSqlCatalog.BeginTransaction()` が返す catalog 固有の明示 scope を必須とし、Core `DatabaseTransaction` の Commit / Rollback / Dispose に委譲する。Core の BeginTransaction だけで開始された scope は動的変更の権限にならない。scope 内の typed table 更新は同じ Core 取引に参加する。独立 WAL や偽の更新による状態 probe は使わない。

## ソース実装と未受入項目（2026-10-29）

OSSパーサーから所有DDLコマンドへ変換し、runtime profileだけにCREATE TABLE、nullable列ADD、DROP TABLEを接続した。独立SqlClient試験はCREATE/INSERT/ALTER/UPDATE/DROP、整数・decimal・文字長の不正値拒否、明示取引のrollback-only、通常再オープンをDebug/Releaseで確認した。生成固定型表のALTER/DROPとread-only DBの変更は拒否する。明示取引のDML失敗後は取引全体をrollback-onlyとする限定動作であり、SQL Serverのすべての文単位エラー回復とは同等でない。

ALTER後に同じSELECTを再実行する旧試験はmetadataと行の列数不一致で失敗した。Registry/schema snapshot修正後のRuntimeDdlWireTestsはDebug/Release各5件成功し、同じSELECTの再実行、動的object IDのsys.tables/sys.columns対応と通常reopen後の一致を確認した。Query回帰はDebug/Release各21件成功した。SharedV2上のnative明示取引によるcommit・rollback・削除後読取もDebug/Release各1件成功した。これらを異常終了回復や実SSMSの合格へ拡大しない。

残る制約：未知schemaのCREATE TABLEを受理しschemaが暗黙に出現する。空schemaを含む元catalogとの統一は未実装で、SQL Server互換の合格条件を満たさない。metadataのread lock待機の取消、nativeモジュールのDDL後拒否と競合、異常終了回復、実SSMS DDL操作も未検証。各行の世代確認はruntime metadataの指紋計算を行うためコスト測定と改善が必要であり、高速性を主張しない。
