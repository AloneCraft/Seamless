# SSMSで確認する生成DBとSQL Serverの一致

## 合意と目的
2026-10-29の利用者の追加指定。SQL ServerのAdventureWorks等にあるスキーマと実データから型付きSeamless DBを生成し、SSMSから基本SQLを実行して元SQL Serverと同じ結果になることを完成条件とする。SSMSでテーブル、ビュー、元ストアド/関数に対応する生成C#メソッドを確認できることも必要。FLinqを第一級APIとし、vendor独自機能の網羅実装は追加しない。

## 生成と表示の契約
1. 元DB一覧とスキーマを取得し、元DB名・スキーマ名・SQL名を保持する。C#識別子の正規化や衝突解決後の名前を別の対応表に保存する。
2. 型付きテーブル・ビューの型、ストアド/関数に対応するC#メソッドを生成し、RoslynでDBアセンブリをビルドする。C#ソース生成とコンパイル成功、実装本体の変換成功、実行結果一致を別に確認する。
3. 生成時の名前解決結果からルーチンの対応表を作る。元DB/スキーマ/SQL名/種別、生成された宣言型/メソッド名、引数と戻り値、C#ソース、実装状態、対象スキーマ指紋を保持する。表示時に名前を推測して再正規化しない。
4. SSMSのオブジェクトエクスプローラーには元SQL名で表・ビュー・ストアド・関数を表示する。生成メソッドの名前・署名・C#ソースは対応情報としてSSMSから照会できるようにする。独自カタログ等の具体的な公開SQL名は実装時に既存契約と照合して確定する。
5. 元T-SQL定義と生成C#ソースを別に扱う。C#をsys.sql_modulesのT-SQL定義と偽らない。表示するメソッドはコンパイル済みアセンブリの実メンバーと照合する。署名だけで本体がNotSupportedExceptionのものはSignatureOnly/Stubとして明示し、実行可能な変換済み機能として宣伝しない。

ルーチンの表示は今回の必須条件である。任意のストアド全般をSQLから実行する機能の追加とは分ける。実行を提供するメソッドを比較ケースに含める場合、その引数・戻り値・結果セット・副作用も原本と照合する。

## ルーチン本体の変換経路
SQL Serverから取得した定義→OSSパーサー→Seamless所有の名前解決/型検査/SQL意味論IR→C#ソース生成→Roslynコンパイル→生成DBの型付きメソッド→元関数/ストアドとの結果比較を実装経路とする。Expressionツリーの生成とC#ソースの生成は別であり、SSMSで表示するC#ソースを保持する。現行Stubの署名生成だけではこの経路の完了に数えない。

単純なスカラー関数とSELECTを返すテーブル値関数から、引数/戻り値/NULL/精度を原本と照合する。更新ストアドはOUTPUT引数・複数結果セット・既存取引契約も検証し、実行対応する範囲を明示する。未対応の本体は理由と対象を表示し、既定値を返して変換成功にしない。

参考資産はG:/GitHub/LinqDB/GenerateDatabase/Generate.csおよびLinqDB/Optimizers/ReturnTSqlFragmentTraverser/作成_TSqlFragmentからType.cs、TestLinqDB/Optimizers/ReturnTSqlFragmentTraverser/変換TSqlFragmentからExpression.cs。SQLの型/NULL/式の変換と試験の考え方を調べ、ScriptDom ASTへの依存を製品へ持ち込まず所有IRへ適用する。参考実装には未実装箇所があり、完全な変換器として扱わない。

## SSMSの受け入れ
| ID | 前提と操作 | 合格条件 |
|---|---|---|
| SSMS-DB | 元SQL Serverと生成Seamlessの同じDBを並べて接続 | 接続先/世代/スキーマ指紋が確認でき、元DB名を保持 |
| SSMS-T | テーブルと列を展開、基本SELECTを実行 | 元名・列順・論理SQL型・NULL可否・長さ/精度/scaleが一致し、実データを取得 |
| SSMS-V | ビューと列を展開、選定した基本ビューをSELECT | 一覧が実在するビューに対応し、ビュー定義を実行した結果が原本と一致。保存済み結果への置換は不可 |
| SSMS-R | ストアド/関数一覧と生成メソッドの対応を照会 | 元名/種別と実際の生成メンバーが1対1で照合でき、署名・C#ソース・Stub/実装済みの状態を確認 |
| SSMS-Q | 表照会/WHERE/ORDER BY/TOP/集計/GROUP BY/基本JOINを共通SQLで実行 | 元SQL Serverと結果metadata・全値・NULL・重複が一致。順序指定なしは多重集合で比較 |
| SSMS-E | 手書きSQLを両サーバーで再実行 | 同じSQL/引数/データ世代と結果差分を保存。未対応や実行エラーを一致へ数えない |

全171ビューのvendor機能を補完する条件には戻さない。表示対象の実オブジェクトを欠落させず、実行比較ケースと未対応の一覧を明示する。基本ビューの実行を一律拒否する試作入口はこの受け入れを満たさない。

## 原本との比較と不一致
SQL Server→スキーマ取得→型/C#生成→データ取り込み→Seamless SQL実行→TDS結果変換の全経路を検証対象にする。原本と取り込み後の値/件数を先に照合し、その後で同じSQLの結果を照合する。SSMSの目視とADO.NETによる自動全件比較は両方実施し、ファイル形式の検査を実UI合格としない。

一致しないケースは変換環境の受け入れ失敗であり、変換成功として扱わない。期待/実際の列と値、差分件数、SQLと引数、元/生成スキーマ・アセンブリ/パッケージ版を保存し、スキーマ・型変換・取り込み・SQL意味論・結果シリアライズのどこで差が出たかを調べる。比較中に元データが変更された場合も合格にはせず、同一入力を確保して再検証する。整数/decimal/文字列/日時/取り込み値は厳密比較し、浮動計算だけ既存の明示した許容誤差を使用する。

## 現コードと検証状況
- `GeneratedRoutineBinding` は生成時に確定した元名、実C#メンバー、ソース、Stub状態を保存する。`ComparisonHost.ResolveRoutines` は実アセンブリの宣言型・署名と照合する。
- `SqlServerRoutineCompiler` はOSS tokenizer/ASTを使い、tinyint引数をCASEでnvarcharへ写す厳密なスカラー関数profileだけをC#へ変換する。AdventureWorks2016の3関数が非Stubとして生成された。その他の本体は理由付きStubであり、実装済みと扱わない。
- `sys.objects`、`sys.procedures`、`sys.parameters`、`sys.sql_modules`とTVF列のカタログを実定義から返す。元のSQL種別を保持し、生成C#は`seamless.generated_methods`で別に照会する。
- 生成契約・スカラー変換・カタログ・native入口・bit比較の選定試験はDebug/Release合計32件成功。これは全回帰や実SSMSの合格ではない。
- 世代`20261029T070650156Z-6a4faf46`、package `0.0.0-ssms.20261029064937709`でAdventureWorks2016の760,838行を取り込んだ。71表/20ビューとfixture3表の94対象で全値照合が成功した。`import-checks.jsonl`に対象ごとの件数と結果を記録している。
- 後続世代`20261029T082930704Z-fd927f1f`、package `0.0.0-ssms.20261029082743678`では、実SqlClientの基本4SQL（20/67/20/290行）と生成3関数の各257入力が元SQL Server・Seamless SQL・保存コピー上のC#直接実行で一致した。21ルーチンのカタログと生成実メンバーの照合も成功した。続く全テーブルTDS検証のDepartmentで非NULL日付型の符号化不整合が発生し、状態はImportedのままで、稼働切替/実UI合格にはしていない。[通信検証](wire-compatibility.md)に失敗と修正状況を記録する。
- 後続のValidated稼働世代 `20261029T085200772Z-e76f5309`（package `0.0.0-ssms.20261029084842137`）では、非NULL日付型とUnicode PLP修正後の94対象の全TDS検証、基本SQL・生成関数・ルーチン対応・SQL/native比較が成功した。SSMSの接続初期化には別の型不整合が残り、ソース側SMO7操作成功を新パッケージと実UIへ適用する工程が残る。
- SSMS資産のStage/Publish/削除・編集保護・参照先検証は25件成功。生成プロジェクトの形式検証を実SSMS接続/実行の代替にしない。
- Query/Gateway等のScriptDom依存は残り、OSS完全移行は未完了。関数のOSS変換成功をSQL実行全経路のOSS化と混同しない。

## 実装順序
生成器の明示的なルーチン対応表→生成アセンブリとの照合→世代/Registryへの保存→SSMSカタログとC#ソース照会→AdventureWorksの基本SQL/ビューを元SQL Serverと照合→SSMS実操作の順で接続する。既存の表/ビュー取り込み・比較資産を再利用し、Core/C12の保存・取引を変更しない。DB作成/再構築時の作業はDをSSD、Fをデータ/成果物に使う。

## 追加の基本制御・実行時DDL
取引・TRY/CATCH・ROLLBACK・RAISERROR・PRINTとSSMSからの実行時DDLを追加の必須範囲にする。[実行時DDL仕様](runtime-ddl.md)に実装済みの限定範囲、未検証の世代更新と、固定型・取引・永続化の判断ゲートを記す。

SSMS22の実UIは旧.ssmssln拡張子を拒否した。同じ内容の.sln診断コピーで2プロジェクトを読み込めたため、正式入口を`TestSeamless/Ssms/TestSeamless.Compare.sln`に変更した。正式.slnの2プロジェクト読み込みは実UIで確認した。新世代の両サーバー接続・スクリプト実行は追加確認が必要。
