---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/TsavoriteDBSourceGeneratorInternalDesign.md
evidence_sources:
  - TsavoriteDB/TsavoriteDB.csproj
  - TsavoriteDB/SourceGenerator/GenerateDatabase.cs
  - SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs
  - Test/Tsavorite/Database/
confidence: medium
review_status: draft
---

# TsavoriteDB Source Generator 内部設計

## 対象
`TsavoriteDB` は Roslyn source generator として構成され、`DatabaseGenerator` marker と schema 型から MemoryPack 対応 record を生成する（`TsavoriteDB/TsavoriteDB.csproj:3`, `TsavoriteDB/TsavoriteDB.csproj:8`, `TsavoriteDB/TsavoriteDB.csproj:14`, `TsavoriteDB/TsavoriteDB.csproj:19`）。

## 主要クラス

| クラス/領域 | 責務 | 実装証拠 |
|---|---|---|
| `TsavoriteSchemaGenerator` | schema generator の入口。型宣言を syntax provider で収集し、生成ソースを `AddSource` する。 | `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:8`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:9`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:11`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:17`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:21` |
| `型対象生成` | 型宣言から symbol を取り、marker/partial などの生成対象判定を行う。 | `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:27`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:37`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:47` |
| `データベースソース構築` | marker namespace を基点に Keys/Records namespace の型を集約し、生成順を安定化する。 | `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:52`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:55`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:70`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:79`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:86` |
| `レコードソース本体追記` | partial struct を unmanaged/managed storage に分け、constructor/property/MemoryPack 実装を生成する。 | `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:114`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:124`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:130`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:148`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:161` |
| `GeneratorHelpers` | symbol 表示形式、namespace 文字列、indent、空行除去などの共通処理を持つ。 | `SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs:3`, `SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs:4`, `SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs:19`, `SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs:28`, `SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs:44` |

## データ生成規則

- schema record は unmanaged property と managed property に分割される（`TsavoriteDB/SourceGenerator/GenerateDatabase.cs:124`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:125`）。
- unmanaged 側は `_U`、managed 側は `_M` に格納される（`TsavoriteDB/SourceGenerator/GenerateDatabase.cs:148`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:151`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:297`）。
- managed 側は MemoryPack serializer/deserializer を生成する（`TsavoriteDB/SourceGenerator/GenerateDatabase.cs:265`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:268`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:274`）。

## Review Gate: Code Internal

### Confirmed
- incremental generator で schema record を生成する。
- `DatabaseGenerator` marker と namespace 規約を使用して Keys/Records を集約する。
- unmanaged/managed storage を分離して MemoryPack 対応を生成する。

### Inferred
- confidence: medium。大量データ record のシリアライズ効率と型安全性を両立する意図がある。
- confidence: low。生成対象 namespace の規約はテストや利用例で補完される可能性がある。

### Unknown
- `DatabaseGenerator` marker の正式な利用者向け文書。
- schema 変更時の互換性、migration、versioning の方針。

### Questions
- 通常設計化時には schema namespace 規約をどのレベルで公開契約にするかを確認する。
