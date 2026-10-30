# 受け入れテスト仕様

**目的補正:** [導入体験仕様](onboarding.md)の基本SQL・GUI操作・FLinq対比例を初期必須とする。以下の広い移行/171view/障害網羅/全GUI範囲は発展工程であり、導入版の合格を妨げる不足補完へ拡大しない。基本操作のデータ整合性・安全性は緩めない。

すべて前提・操作・独立した期待結果を保存する。Passed / Failed / Unavailable / NotImplemented / NotEquivalentを区別し、未実行を合格にしない。以下は今後の実施規範であり、今回通過したという一覧ではない。

| ID・段階 | 前提・操作 | 期待結果・合格条件 |
|---|---|---|
| P0-01 棚卸し | 現HEAD/差分とprojectを列挙 | 分類・依存・公開面・source evidenceが一致、旧差分を破壊しない |
| P0-02 文書 | links/状態/命名/図/例を検査 | 実在/提案を区別、相対link解決、矛盾する上位仕様を参照更新 |
| P0-03 parser | 固定版・導入fixture・必要GUI SQLを同条件評価。171viewは任意の調査 | 構文・AST・診断66件は成功。製品実行・GUI・依存除去は後段で検証。速度・割当測定は未実施 |
| S-01 実DBから生成 | SQL Server AdventureWorks等から同一世代のスキーマ・実データを生成/取り込み | 元名と型付きDBの対応・値/NULL/重複・件数を保存。小fixtureのみを合格にしない |
| S-02 SSMS一覧 | 両サーバーで同じDBを開き表/ビュー/ストアド/関数を展開 | 実オブジェクトと生成メソッドへの対応を表示。空の固定応答や元SQLを生成C#と偽った表示をしない |
| S-03 同じSQL | 固定ケースと引数で表照会・集計・JOIN・基本ビューを両サーバーから実行 | 列metadata・値・NULL・重複・指定順序が一致。不一致はFailed、未対応はNotImplemented。詳細は[生成DBの受け入れ](ssms-generated-db.md) |
| P1-01 境界 | package graph/公開signature/実行sourceを検査 | 共通IR・実行にScriptDom/TDS型が残らず循環なし |
| P1-01b 完全移行 | source/package/公開型、consumer、導入fixtureと必要GUI操作を検証 | ScriptDom依存ゼロ、namespace偽装なし、基本範囲の結果・metadata・操作を保持。高度なvendor機能は対象外を明示。[詳細](oss-cutover.md) |
| P1-02 意味論 | NULL、空集合、外部結合、精度、case/collation、時刻、castを原本比較 | 値だけでなく列名/順/型/NULL/長さ/精度一致。未知は実行前エラー |
| P1-03 最適化 | ruleごとon/off、例外/副作用/列挙回数も比較 | 結果・評価規則を壊さずbudget/取消を保持 |
| P1-04 cache | DB/方言/profile/引数型/schema世代を変え並行Prepare | 誤plan再利用なし、容量上限、実行session/iteratorを保持しない |
| P1-05 native | 既存LINQ/FLinq回帰と生成コードを検査 | nativeにSQL/IR実行時経由を強制せず従来の意味を保持 |
| P2-01 ADO | Factory/接続/command/parameter/reader/transaction、Dapperを実行 | 構築済み型付きDBへnetwork無しで実行、型/DBNull/multi-result正確 |
| P2-02 寿命 | Reader未読Dispose、cancel/timeout/接続Dispose/reset | scope/lock/一時file解放、未確定rollback、再利用可能 |
| P2-03 CRUD | duplicate-key、key変更、明示NULL、keyless重複、commit/rollback | 勝手な値0化/行消失/偽PK公開なし。未対応は変更前に拒否 |
| P2-04 schema | 誤hash/古い生成DLL、SSMSからCREATE/ALTER/DROP、複数接続・再起動・rollback | 不適合を拒否。DDLは実データとカタログを同一取引で確定/取消し、再接続・再起動で復元。古いplan/moduleを誤使用しない。[詳細](runtime-ddl.md) |
| P2-05 制御文 | PRINT、RAISERROR、TRY/CATCHと明示取引、行列挙中エラー、取消 | 通知順序と番号/severity/state/lineを保持し、捕捉対象と取引状態をSQL Serverの独立観測と照合。取消/構文失敗を成功へ変えない |
| P3-01 正しさ | W01〜12を3DBとnativeで照合 | 独立期待値・多重集合・順序・float列限定誤差が一致 |
| P3-02 計測 | A〜G、1/4/16、warmup/roundを実行 | 区間とcache/耐久性が明示、sample不足/Unavailableを隠さない |
| P3-03 同期native | 同期で完了する仕事を並行実行 | 4/16要求が逐次呼出しへ潰れない。計測harnessの回帰で確認 |
| P4-01 metadata | 3DBのcatalogとsource DDL/型を取得 | 元名・制約・索引・型/照合/定義の対応を保存 |
| P4-02 data | NULL/重複/PKなし/大結果/表現限界を逐次移行 | 全値と重複数を照合、丸め/切捨て/黙った除外なし |
| P4-03 failure | 権限不足/OFFLINE/一覧失敗/DDL変化/途中取消 | 削除と誤認せず旧世代維持、不完全世代を公開しない |
| P4-04 lifecycle | 管理fixture DB追加/削除/schema変更、再実行と切替失敗 | 実検出へ追従、失敗時旧構成へ復帰。所有台帳・絶対path照合 |
| P5-01 TDS | SqlClient両系、login DB/USE/RPC/prepare/pool reset/取消 | metadata/値/multi-result/error/session/取引が回帰しない |
| P5-02 GUI | SSMS/Workbench/pgAdmin/DBeaverの固定版で実操作 | DB/表/列列挙、SELECT/CRUD/取引/取消・診断を証跡化 |
| G-01 recovery | 各durabilityでcommit前後/再起動を故障注入 | 宣言した保証どおり、失われたcommit/二重適用を独立検出 |

Debug/Releaseで機能・package利用・生成器を検証、性能はReleaseのみ。CIは小fixtureとPackageReferenceの実行、実RDBMS/GUI/全19DB171viewは独立local工程。GUIファイル形式検査を実UI合格としない。各結果にsource hash、package版、コマンド、入力、stdout/stderr/TRX、環境有無を添付する。

新規project登録・shared visibility・focused solutionsを検査する。未公開API移動は既存利用元を再buildし、既存native/public保存型に差があれば移行を止める。P0の文書保存・静的検査だけでP1〜P5完了を宣言しない。
