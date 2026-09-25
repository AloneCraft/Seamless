---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.Linq, Seamless.Linq.Analyzers
evidence_sources:
  - Seamless.Linq/ScopedQuery.cs
  - Seamless.Linq/ScopedOrderedQuery.cs
  - Seamless.Linq/ScopedGrouping.cs
  - Seamless.Linq/ScopedLookup.cs
  - Seamless.Linq/ExternalSort.cs
  - Seamless.Linq/DiskLookup.cs
  - Seamless.Linq/Internal/ProviderEnumerators.cs
  - Seamless.Linq/ProviderValueEnumerable.cs
  - Seamless.Linq.Analyzers/UnorderedEnumerableAnalyzer.cs
  - Execution.Tests/ScopedQuery.cs
  - docs/execution-and-database.md
confidence: medium
review_status: draft
---

# 04 LINQ ランタイムと大規模 LINQ（Seamless.Linq）

## 行うべき機能

`Seamless.Linq` は2つの役割を持つ。

1. **値列挙の LINQ ランタイム**：FLinq の生成コードが呼ぶ補助（`GeneratedCodeHelpers.cs`、`Internal/*`）と、`System.Linq` と同名の演算子群（90 ファイル）。Tsavorite/Persisted をネイティブ走査で読む入口（`Internal/ProviderEnumerators.cs`、`ProviderValueEnumerable.cs`）。
2. **大規模 LINQ（スコープ付きクエリ）**：`scope.Query(source)` から始め、予算内に収まらない並べ替え・グループ化・結合・集合演算をディスクへ退避しながら行う。

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| スコープ付きクエリの入口 | `ExecutionScope.Query(source)` | 実装済み | `ScopedQuery.cs:4-9` |
| 逐次演算 | `Where`、`Select`、`SelectMany`、`Take`、`Skip`、`Concat` | 実装済み | `ScopedQuery.cs:29-50` |
| 終端 | `LongCount`、`Count`、`Any`、`First`、`ToList`、`ToArray`、`ToStorage` | 実装済み | `ScopedQuery.cs:51-102` |
| 外部ソート | `OrderBy`/`OrderByDescending`/`ThenBy`/`ThenByDescending`。安定 | 実装済み | `ScopedOrderedQuery.cs:4-25`、`ExternalSort.cs` |
| 退避可能なグループ化・結合・集合演算 | `GroupBy`、`Join`、`Distinct`、`DistinctBy`、`Union`、`Intersect`、`Except` | 実装済み | `ScopedGrouping.cs:4-46`、`DiskLookup.cs`、`ScopedLookup.cs` |
| 既定の等値性 | 比較子を省略したらバイト列の完全一致。ハッシュは候補の絞り込みだけ | 実装済み | `docs/execution-and-database.md`「等値性とCodec」 |
| 順序なし列の検査 | 順序なしの列に順序依存の演算を使うとコンパイルエラー（`SEAMLINQ001`）、由来が失われる境界もエラー（`SEAMLINQ002`） | 実装済み | `Seamless.Linq.Analyzers/UnorderedEnumerableAnalyzer.cs:7-24` |
| 上の一覧に無い演算子の扱い | `GroupJoin`、`LeftJoin`、`RightJoin`、`Reverse`、`Last`、`ToLookup`、`Zip`、集計（`Sum` など）など | **未実装**（【推測・高】`System.Linq` の拡張メソッドへ解決され、全件をメモリで処理しうる） | `ScopedQuery<T>` は `IEnumerable<T>` を実装している（`ScopedQuery.cs:11`） |

## 1. 不変条件

1. **1つのスコープで同時に1つの列挙。** 並行には別スコープを使う（`ExecutionScope.cs:25-37`、`ScopedQuery.cs:24-27`）。
2. **スコープ破棄後の列挙は `ObjectDisposedException`。** 遅延クエリ、`ToStorage()` の結果、`GroupBy` のグループはスコープ所有（`Execution.Tests/ScopedQuery.cs:18`、`:31`）。
3. **キャンセルは列挙の各要素で確認する**（`ScopedQuery.cs:15-18`、`Execution.Tests/ScopedQuery.cs:90`）。
4. **`ToList`/`ToArray` は全件を管理対象メモリへ実体化し、予算を超えたら `ResourceLimitException`**（`ScopedQuery.cs:67-93`、`Execution.Tests/ScopedQuery.cs:83`）。大きな結果は `ToStorage()` を使う。
5. **外部ソートは元の位置を保持して安定。** グループはキーの初出順と要素順、Join は外側の順序と一致する内側の順序を保つ（`docs/execution-and-database.md`）。
6. **1つの巨大グループ・同一キーが大量にある Join でも、全件を1つの `List<T>` に入れない**（同上「大規模LINQ」）。
7. **Codec を作れない値でも、退避不要の逐次演算は使える。退避が必要になった時点で、通常 LINQ へ暗黙に切り替えず、理由つきで失敗する**（同上）。
8. **スコープ付きクエリのバイト等値性は、スコープ外の FLinq の .NET 等値性を変えない**（`Execution.Tests/ScopedQuery.cs:70` `ScopedByteEquality_DoesNotChangeFLinqSystemEquality`）。
9. **Tsavorite/Persisted のネイティブ走査の結果は順序なしとして扱う**（`Internal/ProviderEnumerators.cs:10,51,88` の `[return: UnorderedEnumerable]`）。

## 2. 禁止事項

- スコープ付きクエリの演算子を、黙って `System.Linq` の全件メモリ処理へ落とさない（不変条件7）。
- 順序なし列の由来を、注釈のない境界で失わせない（`SEAMLINQ002`）。解析器のエラーを抑止して回避しない（`NotConfigurable`：`UnorderedEnumerableAnalyzer.cs:14`、`:23`）。
- 比較子の省略時にハッシュだけで等しいと判断しない（衝突時はバイト列を照合する）。

## 3. 前提条件

- 退避には決定的な Codec が必要。一般クラスは明示登録する（`scope.Codecs.Register(...)`）。登録はクエリ実行前（Codec は最初の解決で固定）。
- 一時ファイルはスコープのフォルダに置き、スコープ破棄で消える（`ExecutionScope.cs:48-61`、`67-80`）。

## 4. 変更範囲 / スコープ

- `Seamless.Linq/Scoped*.cs`、`ExternalSort.cs`、`DiskLookup.cs`、`Seamless.Linq.Analyzers/*`、`Execution.Tests/ScopedQuery.cs`。
- 値列挙ランタイム（`Seamless.Linq/*.cs` の演算子群）は FLinq の生成コードの呼び先なので、変えるときは [05](05_FLinqソース生成.md) と同時に扱う。

## 5. 非対象 / Out of Scope

- スコープ外の通常 LINQ の意味（.NET の意味のまま）。
- 並列クエリ（1スコープ1列挙の制約を外すこと）。

## 6. 完了条件 / 成功条件

- スコープ付きクエリで使える演算子の一覧が文書化され、一覧外の演算子を使うと **コンパイル時か実行時に理由つきで失敗する**（黙って全件メモリにしない）。
- 退避が必要な演算子（`GroupJoin`、`LeftJoin`、`RightJoin`、`Reverse`、`ToLookup` など）のうち、必要なものは退避可能な実装を持つ。
- どの演算子でも、予算を超える入力で `ResourceLimitException` か正しい結果のどちらかになり、プロセスのメモリが予算とは無関係に伸びない。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 同時列挙・破棄後・キャンセルの例外 | `Execution.Tests/ScopedQuery.cs:7`（`Query`）、`:86`（`Where`） |
| GroupBy・Join・Distinct の結果と順序 | 同 `:20`、`:40`、`:49` |
| 外部ソートの安定性 | 同 `:96`（`OrderBy`） |
| `ToStorage` の逐次保存 | 同 `:78` |
| 予算超過の `ToArray` | 同 `:83` |
| 一覧外の演算子が黙って全件メモリにならない | **未整備**。`Reverse`・`Last`・`GroupJoin` などを小さい予算で呼び、例外になることを確かめるテストを追加する |
| 順序なし列の解析器 | 解析器のテスト（【不明】専用テストの所在は未確認） |

## 8. 移行条件

- 一覧外の演算子を扱う方針が決まるまでは、既存の利用コードを変えない。
- 方針が「解析器で禁止」なら、まず警告で導入して利用箇所を洗い出し、なくなってからエラーにする。
- 方針が「退避可能な実装を足す」なら、1演算子ずつ、結果が通常 LINQ と一致するテストと予算超過のテストを揃えてから公開する。

## 9. ロールバック条件

- 新しい退避演算子で、通常 LINQ との結果の不一致または順序の違いが出たら、その演算子を外す（一覧外に戻す）。

## 10. 既知の問題 / 許容事項

- 【推測・高】`ScopedQuery<T>` が持たない演算子は、C# の拡張メソッド解決で `System.Linq` 側へ解決される。その場合は予算の外で全件メモリ処理になりうる。FLinq が有効なプロジェクトでは FLinq がその呼び出しを展開する可能性もあるが、どちらも退避しない。不変条件7との不一致であり、[20](20_未実装と既知の問題.md) U-10 として扱う。
- `ToList`/`ToArray` の予約量は推定値（要素サイズ＋符号化長×4 など：`ScopedQuery.cs:76-90`）。実際の使用量とは一致しない。

## 確認済み / 推測 / 不明

- **確認済み**：スコープ付きクエリが持つ演算子の一覧、同時列挙・破棄・キャンセル・予算超過の挙動、解析器の診断2種。
- **推測**：一覧外の演算子が全件メモリ処理になること（高。ただし実行して確かめてはいない）。
- **不明**：一覧外の演算子を「禁止」にするか「実装」するかの方針。解析器テストの所在。
