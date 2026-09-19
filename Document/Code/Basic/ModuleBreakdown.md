# Module Breakdown

この文書は現在の実装責務を示す。移行途中の仮説や旧プロジェクト名は正本に含めない。

| モジュール | プロジェクト | 責務 |
|---|---|---|
| 実行契約 | `Seamless.Execution` | 実行スコープ、共有資源予算、Codec、一時資源所有、キャンセル、残骸回収 |
| コレクション契約 | `Seamless.Collections` | 共通インターフェース、Serializer契約、データ構造 |
| 永続コレクション | `Seamless.Collections.Persisted` | ファイル列、スコープ所有の保存列・バイト索引 |
| Tsavoriteコレクション | `Seamless.Collections.Tsavorite` | ログ／索引／deviceを使うSet・Dictionary実装 |
| クエリ実行 | `Seamless.Linq` | 逐次演算、外部ソート、退避可能GroupBy・Join・集合演算 |
| コンパイル時最適化 | `Seamless.FLinq.Generator` | System.Linq呼び出しの解析、Fragment最適化、interceptor生成 |
| DBランタイム | `Seamless.Database` | テーブル、read session、transaction、共有予算、WAL、復旧、materialized view |
| RDBMS契約 | `Seamless.Database.Rdbms` | カタログと関係代数モデル |
| SQL Server | `Seamless.Database.SqlServer` | カタログ解析、型変換、routine解析、逐次取り込み |
| DBツール | `Seamless.Database.Tool` | 生成、staging検証、manifest、バックアップ付き公開 |
| 検証ドメイン | `Verification.Core` | 計画、run、case、測定、診断、比較条件と判定 |
| 検証インフラ | `Verification.Infrastructure` | Git影響解析、Worker実行、SQLite、成果物、replay／resume／shrink |
| 検証入口 | `Verification.Cli`, `Verification.UI` | 共通Workspaceを使うCLIとWindows MAUIアプリ |
| 既存検証アダプター | `Benchmark.*.Headless`, `Benchmark.Dynamic.Worker`, `Benchmark.Worker` | 分離プロセスでの測定・容量探索・動的クエリ検証 |

配布用ランタイムパッケージはVerification、UI、検証DB、テストassemblyを参照しない。`Verification.Infrastructure` は既存Worker群へ依存してよいが、逆方向の依存は作らない。

`Shared/UI.Core/BenchmarkPerformanceGate.cs` は旧UI互換であり、新しい比較契約ではない。新しい比較の正本は `Verification.Core.VerificationComparison` である。
