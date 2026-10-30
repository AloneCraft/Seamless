# SQLからFLinqへ導入する体験仕様

## 目的と合意
ユーザーは本来C#・LINQ・FLinqを使ってほしい。未経験者にはSSMS/MySQL Workbenchで基本SQLが使えることを体験してもらい、その処理を型付きFLinqへ移せることを示す。SQL ServerやMySQLの全機能を再現することは目標ではない。2026-10-29の利用者の目的補正を反映した最新版の範囲である。

## 必須の導入体験
1. SQL ServerにあるAdventureWorks等の実DBのスキーマ・データから型付きSeamless DBを生成する。DB名・スキーマ名・表/ビュー/ルーチン名と生成C#名を対応付ける。小fixtureは回帰試験に使い、実DBによるSSMS受け入れは[生成DBの受け入れ](ssms-generated-db.md)で必須とする。
2. SSMSとMySQL Workbenchの接続をそれぞれ検証。DB、表、列を一覧でき、SQLエディタを開ける。後者にはMySQL wireが必要で、parserだけで接続対応とはしない。
3. SELECT列/WHERE/parameter、基本COUNT/SUM/AVG/MIN/MAX、GROUP BY、ORDER BY、基本INNER/LEFT JOINを示す。TOP/LIMITの基本件数制限は方言adapterへ。
4. 専用fixtureで明示ID付きINSERT、通常列UPDATE、DELETE、COMMIT/ROLLBACKを実行する。readonlyの元DBを使わない。NULLや主キー違反など基本的なデータ整合性は維持。
5. 同じ検索・集計をC#・LINQ・FLinqの例と並べ、生成型、引数、結果の型、ビルド時検証を説明する。nativeはSQLへ変換せず既存生成器の実行経路を利用する。
6. SQL結果とnative結果をfixtureの独立期待値と照合する。性能を見せる場合はparser・通信と本体の時間を区別し、未測定の高速性を宣伝しない。

## 補完しない機能
XML/XQuery、APPLY、PIVOT、複雑なUDF・ストアド、vendor専用分析/管理機能、全171ビュー実行、全管理画面、全DDLウィザード、バックアップ管理、vendor固有型の全演算は初期必須から外す。OSSに不足があるという理由だけで実装を増やさない。未対応は構文・対象・理由を返し、成功や空結果へ置き換えない。SourceReadOnlyGuard等は未知の構文を黙認しない。

## GUI内部SQLの扱い
利用者が独自関数を書かなくてもGUIはversion/DB/表/列等を照会する。ここは接続・一覧・エディタに必要な最小subsetを互換providerで提供する。一般のvendor関数実行と分離し、実在metadataに基づく応答にする。未実装管理機能をインストール済み・利用可能として偽装しない。固定したGUI版のtraceと実操作で必要量を決め、広いregexで未知SQLを成功扱いしない。

## OSS移行の合格条件
SqlParserCS等で基本subsetと必要なGUI内部SQLを扱い、ScriptDomの製品・consumer依存を外す。任意の171view調査はcoverage記録に留める。自社所有AST/enumと既存bounded実行を再利用し、native/core/WALを変更しない。source検索・復元assets・packageと実動の両方を検査する。

## 受け入れ
| 対象 | 操作と合格 |
|---|---|
| SSMS | 接続→DB/表/列一覧→検索/集計/JOIN→fixture CRUD/取引。対応版と操作ログ・画面証跡を記録 |
| Workbench | 同操作をMySQL wireで実行。socket成功と操作成功を区別 |
| SQL | NULL/空集合/外部結合/重複/列metadataを基本範囲で照合。未対応は変更前にエラー |
| FLinq導線 | 同じfixture/引数/期待結果のコード例がbuild・実行でき、生成器適用を確認 |
| 資源・取消 | 大結果の逐次送信、budget超過、cancel/dispose/rollbackの安全性を保持 |
| 説明 | 対応操作一覧とSQL→FLinq対比例を提示。「SQL Server/MySQL完全互換」と表記しない |

PostgreSQLの基本profile設計と将来wire/pgAdmin拡張は残すが、最初の説明用GUIは利用者が挙げたSSMSとMySQL Workbenchに集中する。過去の網羅的比較仕様はこの導入体験へ不足機能補完を持ち込む根拠にしない。

## 基本の制御文と実行時DDL
2026-10-29の追加合意で、取引・TRY/CATCH・ROLLBACK・RAISERROR・PRINT・SSMSからの実行時DDLを必要な小範囲として追加した。vendor機能の網羅へ広げず、[DDLと制御文の受け入れ](runtime-ddl.md)を満たす。全DDLウィザードの互換性と、基本CREATE/ALTER/DROPを実際に実行できることは区別する。
