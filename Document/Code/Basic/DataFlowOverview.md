# Data Flow Overview

## スコープ付きクエリ

```mermaid
flowchart LR
    A[入力 IEnumerable] --> B[ExecutionScope.Query]
    B --> C[ScopedQuery 演算子]
    C --> D{管理メモリ内か}
    D -->|はい| E[チャンク・メモリ索引]
    D -->|いいえ| F[一時ファイル・バイト索引]
    E --> G[遅延結果]
    F --> G
    G --> H[逐次列挙または ToStorage]
```

すべての予約は一つの `ResourceBudget` へ集約する。比較子省略時のキーは決定的Codecのバイト列である。外部ソートは元位置を保持し、GroupBy／Joinは入力順契約を維持する。

## DB commitと復旧

```mermaid
sequenceDiagram
    participant C as Caller
    participant T as Transaction overlay
    participant W as WAL
    participant S as Tsavorite store
    C->>T: Add/Remove/Clear
    T->>W: begin + operations + commit + flush
    T->>S: committed changesを反映
    C->>S: Disposeでcheckpoint
    S->>W: 全対象checkpoint確認後にSHA archive
```

起動時はcommit済みWALだけを冪等に再生する。未commit、切断末尾、未openテーブルを含むactive WALを成功済みとして消去しない。

## 変更後の検証

```mermaid
flowchart LR
    A[Git差分・未追跡・共有ソース] --> B[Project依存解析]
    B --> C[VerificationPlan]
    C --> D[共通Runner]
    D --> E[dotnet / Benchmark / Scalable / Sweep / Dynamic adapter]
    E --> F[生測定・診断・途中状態]
    F --> G[(Verification.db)]
    F --> H[ハッシュ付きArtifacts]
    G --> I[固定baselineとの比較]
    I --> J[replay / resume / shrink]
```

CLIとMAUIは同じ `VerificationWorkspace` を使う。計画時のソース指紋、ケース入力、意味論、Codec、測定方法、環境、CPU、構成を保存し、比較条件が違う結果を性能判定へ混ぜない。
