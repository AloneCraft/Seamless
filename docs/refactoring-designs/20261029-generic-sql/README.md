# Seamless 汎用SQL基盤

## 背景・合意状態
2026-10-29、利用者が計画を承認しP0の実施を指示した。本書群は現在の製品実装の説明と、今後実装する規範仕様を区別する。**P0は現状分析・仕様・移行・受け入れ定義。P1〜P5の製品実装完了を意味しない。**

Seamlessの主力はC#・LINQ・FLinq。SQLは相互運用、移行、共通比較、導入容易性の入力言語。T-SQL・SSMSは維持する互換対象の一つであり、SQL対応のためにネイティブをSQL文字列へ変換しない。

## 読む順序
1. [現状分析](current-state.md)：証拠と4分類、現在の処理経路。
2. [アーキテクチャ](architecture.md)：規範、内部IR、意味論、通信・ADO.NET・移行境界。
3. [パーサー評価](parser-decision.md)：12項目、選定と保留条件。
4. [パッケージ・移行](migration.md)：依存方向、移設先、段階移行。
5. [比較仕様](benchmark.md)：公平性、A〜G、取得不能の扱い。
6. [受け入れ](acceptance.md)：各段階の前提・手順・期待結果。
7. [利用例](examples.md)：実在APIと提案APIの区別。
8. [共通workload](workloads.json)：固定データ・12case（未実装仕様）。
9. [文書検査](validation.json)：静的整合・保全確認。
10. [調査証跡](evidence.json)：HEAD、作業差分、ファイルハッシュ、依存グラフ。

## 目的と非目的
初期の実装目標はSQL Server・MySQL・PostgreSQL。Oracle・SQLiteは拡張設計のみ。標準はOSS、有償パーサーは任意アダプター。ADO.NETは構築済み型付きDBの照会・CRUD・取引を提供する。2026-10-29の追加合意で、SSMSからの実行時CREATE/ALTER/DROPとTRY/CATCH・RAISERROR・PRINTも受け入れ対象に含めた。[実行時DDLと制御文](runtime-ddl.md)に範囲、現在の未実装状態、取引・復元の条件を定義する。採番・UPSERT全般、任意ストアド全般、Oracle/SQLite通信の網羅は必須にしない。移行時DDL解析と実行時スキーマ変更は別の責務である。

## 不変条件・ガードレール
- 最上位名前空間Seamless、新規製品はSeamless.*。SeamlessDB.*は新設しない。既存名前空間・保存型を一括改名しない。
- 既存Execution、Database、取引、WALを再利用。別の確定方式をSQL側に作らない。
- 外部ASTを共通IRにせず、パーサー・通信・GUI互換・移行を分離する。
- .NETとSQLのNULL・等値性・順序の差を隠さない。未対応を実行前に診断する。
- 未公開と確認したSQL APIのみ整理可能。既存native API、保存形式、接続互換を保護する。
- 旧SQL Server比較計画の全19DB・171ビュー受け入れは未完。既存成功結果を現在全体の成功にしない。

## OSS完全移行の追加合意
利用者はGSPが必須ではないと確認し、OSS候補を評価・補完してScriptDom依存の完全除去を目指す方針を承認した。GSP採用は前提にしない。旧「SQL Server用にScriptDomを恒久維持」は撤回し、移行中の比較基準としてのみ保持する。完了条件は[完全移行ゲート](oss-cutover.md)。着手時18件の直接依存を除去し、下記の導入体験の実動を検証する。

## 導入体験を優先する追加合意
利用者の主目的はFLinqへの導入であり、SQLは使い慣れたGUIから当面操作できる入口である。OSSの不足機能を網羅的に補完しない。171ビュー・XML/APPLY/PIVOT/複雑なUDF・任意vendor関数の完全実行は必須条件から外す。GUIの接続/一覧に必要な最小互換SQLだけを専用providerで扱う。[導入体験仕様](onboarding.md)がこの範囲の最新版で、過去の広い受け入れ一覧より優先する。

## SSMSによる実DB受け入れ
AdventureWorks等の元SQL Server DBから生成し、SSMSで基本SQLの結果が一致すること、表・ビューと生成ストアド/関数メソッドを確認できることを完成条件に追加した。[実DBと生成メソッドの受け入れ仕様](ssms-generated-db.md)を参照。小fixtureだけの成功では満たさない。

## 現在の判定
文書・静的分析に加え、SqlParserCS 0.6.5で3方言66件の構文・AST・診断試験が成功した。基本経路の実装へ進む。net10のPackageReference利用によるDB生成/取り込みとAdventureWorks基本SQL/生成3関数の比較は成功した。全wire/GUI検証と依存除去は未完であり、完全移行は未完了。パーサーの性能・割当測定は未完了。速度を理由にした採用判断や性能向上の主張は行わない。

## 引き継ぐ設計
[SQL Server比較の個別仕様](../20261029-sqlserver-seamless-comparison.md)、[C12取引契約](../20261029-generic-transactions-tpc-c12.md)、[ExecutionとDatabase](../../execution-and-database.md)の有効な契約を維持する。旧個別仕様の目的自体は廃止しない。

## 通信相互運用の現在の制約
[実クライアントの通信検証](wire-compatibility.md)に、修正済みのNULL/空PLP/packet交渉/長Unicode値と、環境依存のホスト名接続を記録する。既定比較endpointは127.0.0.1を用い、未検証世代を稼働へ切り替えない。
