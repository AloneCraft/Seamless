# Layer Mapping

依存方向は上から下ではなく、次の矢印だけを許す。

```mermaid
flowchart TD
    UI[Verification.UI / Verification.Cli] --> VI[Verification.Infrastructure]
    VI --> VC[Verification.Core]
    VI --> W[Headless / Worker adapters]
    DBT[Database.Tool] --> SQL[Database.SqlServer]
    SQL --> RDBMS[Database.Rdbms]
    SQL --> DB[Seamless.Database]
    DB --> LINQ[Seamless.Linq]
    DB --> TS[Collections.Tsavorite]
    DB --> P
    LINQ --> P[Collections.Persisted]
    LINQ --> TS
    LINQ --> EX[Seamless.Execution]
    P --> EX
    P --> C
    TS --> EX
    DB --> EX
    EX --> C[Seamless.Collections]
    TS --> C
    FL[FLinq.Generator] -. generated code .-> LINQ
```

## ガードレール

- `Seamless.Execution` はUI、Verification、Database、具体的な永続ストアへ依存しない。共通ストア（`Store`）はエンジンを知らず、Tsavorite／Persisted が拡張メソッドと子資源（`IStoreComponent`）で接続する。
- Persistedだけを参照した場合にGarnetが必要にならないよう、Persisted→Tsavoriteの依存を作らない。
- `Seamless.Linq` はDB状態やUI状態を持たない。
- Persisted／Tsavoriteは検証計画やLINQ実行方針を持たない。
- FLinqが最適化するスコープ外のSystem.Linqは.NET意味論を維持する。
- Verificationの型をBenchmark UI固有のViewModelへ依存させない。
- ランタイム配布パッケージにVerification UI、SQLite結果DB、テストassemblyを含めない。
- DBの保存形式変更は明示バックアップと明示初期化で行い、起動時の自動削除・自動移行を追加しない。
- 共有ソースを追加・移動するときは `.projitems`、同じ場所の `.shproj`、`Seamless.slnx` を同時に更新する。
