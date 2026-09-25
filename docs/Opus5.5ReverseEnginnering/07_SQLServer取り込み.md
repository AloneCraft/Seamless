---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.Database.Rdbms, Seamless.Database.SqlServer, Seamless.Database.Tool
evidence_sources:
  - Seamless.Database.Tool/Program.cs
  - Seamless.Database.Tool/DatabaseImporter.cs
  - Seamless.Database.Rdbms/CSharpDatabaseGenerator.cs
  - Seamless.Database.Rdbms/RelationalAlgebraOperator.cs
  - Seamless.Database.SqlServer/SqlServerRoutineAnalyzer.cs
  - Seamless.Database.SqlServer/SqlServerTypeMapper.cs
  - Seamless.Database.SqlServer/SqlServerImportSizer.cs
  - Seamless.Database.SqlServer/SqlServerSampleCatalog.cs
  - Test/Seamless/Database/SqlServer/*
  - Test/Seamless/Database/Tool/*
  - docs/execution-and-database.md
confidence: medium
review_status: draft
---

# 07 SQL Server 取り込み（Rdbms / SqlServer / Tool）

## 行うべき機能

SQL Server のスキーマから `DatabaseContainer` の C# コードを生成し、データを取り込む。取り込みは安全に（宛先を直接書き換えず、検証してから公開）行う。

CLI：`seamless database sqlserver <generate|import|prepare|samples> --project ... --connection ...`（`Seamless.Database.Tool/Program.cs:7-9`）

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| カタログ読み取り | テーブル・列・主キー・一意キー・外部キー・チェック制約・ビュー・ルーチン・トリガーを読む | 実装済み | `Seamless.Database.Rdbms/*Catalog.cs`、`SqlServerCatalogReader.cs`、`SqlServerMetadataReader.cs` |
| 型の対応付け | SQL Server の型を C# の型へ。未対応は例外 | 実装済み | `SqlServerTypeMapper.cs:12-30` |
| コード生成 | テーブル・ビュー（マテリアライズドビュー）・ルーチンの C# を生成 | 実装済み | `Seamless.Database.Rdbms/CSharpDatabaseGenerator.cs`、`GeneratedProjectWriter.cs` |
| 容量の見積もり | 最大行サイズと最大行数から Tsavorite の大きさを決める | 実装済み | `SqlServerImportSizer.cs:48-63` |
| 安全な取り込み | staging に構築 → 行数を検証 → 公開。旧宛先は backup へ退避して削除しない | 実装済み | `Seamless.Database.Tool/DatabaseImporter.cs:24-81`、`:198-205` |
| 取り込み記録 | `seamless-import.json` にソース・スキーマ指紋・時刻・行数・容量設定 | 実装済み | `DatabaseImporter.cs:114-118` |
| サンプル DB の準備 | AdventureWorks・WideWorldImporters などを取得・復元 | 実装済み | `SqlServerSampleCatalog.cs`、`SqlServerSamplePreparer.cs` |
| ルーチンの分類 | T-SQL を解析し、変更の有無・結果セット数・関係代数の操作列を得る | 実装済み（分類だけ） | `SqlServerRoutineAnalyzer.cs:12-35`、`RelationalAlgebraOperator.cs` |
| ルーチンの実行 | テーブル関数・スカラー関数・ストアドプロシージャを C# で実行する | **スタブ**（呼ぶと `NotSupportedException`） | `CSharpDatabaseGenerator.cs:195-221` |
| 動的 SQL | `EXEC` で文字列を実行するルーチン | **未対応**（解析時に例外） | `SqlServerRoutineAnalyzer.cs:87-105` |

## 1. 不変条件

1. **宛先を直接書かない。** `<destination>.staging.<guid>` に作り、全関係を開き直して SQL Server の行数と `LongCount` が一致することを確かめてから公開する（`DatabaseImporter.cs:24-73`、`docs/execution-and-database.md`「SQL Server取り込み」）。
2. **旧宛先は `<destination>.backup.<timestamp>.<guid>` へ移し、削除しない**（`DatabaseImporter.cs:198-205`）。
3. **失敗したら staging を消し、宛先は変えない**（`DatabaseImporter.cs:76-78`）。
4. **未対応の構文・型は `NotSupportedException` で止め、対象の構文名・型名を診断に出す。別の意味へ暗黙変換しない**（`SqlServerTypeMapper.cs:30`、`SqlServerRoutineAnalyzer.cs:28-29`）。
5. **1行が 1 GiB の Tsavorite ページ上限を超える表は取り込まない**（`SqlServerImportSizer.cs:55-58`）。
6. **関係ごとの件数から容量を決め、最大の表の容量を全表へ重複配分しない**（`docs/execution-and-database.md`「Database」）。
7. **生成したルーチンは、実行できないことを呼び出し時に明示する**（`CSharpDatabaseGenerator.cs:201`、`:210`、`:219` の例外メッセージ）。

## 2. 禁止事項

- 取り込み中に既存の宛先を上書き・削除しない。
- 対応していない型を文字列やバイト列へ黙って落とさない（暗号化列は例外的にバイト列として扱う：`SqlServerTypeMapper.cs:12`）。
- ルーチンの一部だけを C# に訳して、元と違う結果を返さない（訳せない構文があれば全体を未対応にする）。

## 3. 前提条件

- SQL Server に接続できること（接続文字列はプロジェクトの設定から解決：`ConnectionStringResolver.cs`）。
- T-SQL の解析は `Microsoft.SqlServer.TransactSql.ScriptDom` の `TSql180Parser`（`SqlServerRoutineAnalyzer.cs:15`）。

## 4. 変更範囲 / スコープ

- `Seamless.Database.Rdbms/**`、`Seamless.Database.SqlServer/**`、`Seamless.Database.Tool/**`
- テスト：`Test/Seamless/Database/SqlServer/*`、`Test/Seamless/Database/Tool/*`、`Test/Seamless/Database/Rdbms/*`

## 5. 非対象 / Out of Scope

- SQL Server 以外の RDBMS（`Rdbms` は共通の型を持つが、読み取り実装は SQL Server だけ）。
- 双方向の同期（取り込みは一方向・一括）。
- トリガーの実行（カタログとして読むだけ）。

## 6. 完了条件 / 成功条件

- テーブルとビューの取り込み：サンプル DB（AdventureWorks 系・WideWorldImporters）を取り込み、全表の行数が一致し、代表クエリの結果が SQL Server と一致する。
- ルーチン：スコープを決めたうえで（例：副作用のない SELECT だけのテーブル関数から）、関係代数から C# の LINQ へ訳して実行でき、SQL Server と結果が一致する。訳せないものは今までどおり明示的に失敗する。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 型の対応付けと未対応型の例外 | `Test/Seamless/Database/SqlServer/SqlServerTypeMapperTests.cs` |
| ルーチン解析と未対応構文の例外 | `SqlServerRoutineAnalyzerTests.cs` |
| 容量の見積もり | `SqlServerImportSizerTests.cs` |
| 取り込みの staging・検証・公開・backup | `Test/Seamless/Database/Tool/DatabaseToolImportIntegrationTests.cs` |
| 生成コード | `Test/Seamless/Database/Rdbms/RdbmsSourceGeneratorTests.cs`、`RdbmsViewSourceGeneratorTests.cs` |
| ルーチンの実行結果が SQL Server と一致 | **未整備**（ルーチン実行を実装するときに追加） |

## 8. 移行条件

- ルーチン実行を入れる場合、生成コードの形（`TableFunction<T>`/`ScalarFunction<T>`/`StoredProcedure<T>` の署名）は変えず、中身だけを例外から実行へ差し替える。
- 取り込み済みの DB は作り直す（自動変換しない）。

## 9. ロールバック条件

- ルーチン実行の結果が SQL Server と一致しないものがあれば、そのルーチンの種類を「未対応（明示的な例外）」へ戻す。
- 取り込みで問題が出たら、新しい宛先を捨て、backup フォルダを元の名前へ戻す。

## 10. 既知の問題 / 許容事項

- 生成されたルーチンはすべてスタブ。呼ぶと `NotSupportedException`（「requires relational-algebra execution」）。
- 出力パラメーターは `default` を入れてから例外になる（`CSharpDatabaseGenerator.cs:227-241`、特に `:238`）。
- `sql_variant` など `SqlServerTypeMapper.cs:13-29` の一覧に無い型は取り込めない。
- `bit` は専用の `Types.Bit` 型になる（`SqlServerTypeMapper.cs:36-38`）。

## 確認済み / 推測 / 不明

- **確認済み**：CLI の4コマンド、staging→検証→公開→backup、未対応の扱い、ルーチンがスタブであること。
- **推測**：なし。
- **不明**：ルーチン実行をどこまで対応するか（全部か、副作用のない関数だけか）。これは製品判断が必要。
