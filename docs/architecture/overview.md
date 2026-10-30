# 全体アーキテクチャ (Architecture Overview)

Seamless は、**「コンパイル時コード生成（Roslyn Source Generator / Interceptor）」** と **「実行時高密度ストレージ（Runtime Collections & Stores）」** の緊密な相互補完によって成り立つ、2層構造アーキテクチャを採用しています。

```mermaid
flowchart TD
    subgraph CompileTime [コンパイル時 (Compile-time)]
        UserSource[ユーザー C# ソースコード]
        FLinqGen[FLinq Generator<br/>- Interceptor 置換<br/>- パイプライン融合]
        DBGen[Database Generator<br/>- スキーマ展開<br/>- シリアライザ自動生成]

        UserSource --> FLinqGen
        UserSource --> DBGen
    end

    subgraph GeneratedCode [生成コード (Generated Code)]
        InlinedLoops[インライン化ループ<br/>& Direct Span アクセス]
        TypedTables[型付きテーブル & レコードバインディング]
    end

    CompileTime --> GeneratedCode

    subgraph RuntimeEngine [実行時エンジン (Runtime Engine)]
        ExecutionLayer[Seamless.Execution<br/>- ResourceBudget (メモリ/ディスク予算)<br/>- ExecutionScope (実行スコープ)<br/>- 一時資源回収 (Temporary Recovery)]

        StorageLayer[ストレージ層<br/>- Seamless.Collections (Blittable)<br/>- Seamless.Collections.Persisted (MMF)<br/>- Seamless.Collections.Tsavorite (HybridLog)]

        DBEngine[Seamless.Database<br/>- ACID トランザクション<br/>- WAL (Write-Ahead Log)]
    end

    GeneratedCode --> RuntimeEngine
```

---

## 1. コンパイル時レイヤー (Compile-time Layer)

### Roslyn Source Generator & C# 12+ Interceptor
従来のライブラリが実行時にリフレクションや動的式木（`Expression<T>`）を用いて解決していた処理を、すべてコンパイル時に完了させます。

- **`Seamless.FLinq.Generator`**:
  `System.Linq.Enumerable` に対するメソッド呼び出し箇所（構文木の位置情報）を正確に特定し、C# 12+ の Interceptor 属性を用いて、直接最適化されたループコードへ差し替えます。
- **`Seamless.Database.Generator`**:
  `DatabaseContainer` に記述されたプロパティ宣言から、メモリレイアウト、主キー索引、シリアライズ関数、テーブルバインディングを事前に完全生成します。

---

## 2. 実行時レイヤー (Runtime Layer)

### `Seamless.Execution` (共通基盤)
すべてのストレージとクエリ処理の土台となる共通実行基盤です。
- **`ResourceBudget`**:
  プロセス全体で消費可能な管理対象メモリ量（例: 512MB）および一時ディスク量（例: 4GB）をトークン管理します。上限を超えるアロケーション要求は即座に拒否され、メモリ暴走を防ぎます。
- **`ExecutionScope`**:
  1つのクエリや処理トランザクションのスコープを定義し、処理中に生成された一時ファイルやバッファの確実なクリーンアップを保証します。

### ストレージ層 (`Collections`, `Persisted`, `Tsavorite`)
- **`Seamless.Collections`**:
  ヒープを汚染しない純アンマネージド・Blittable 配列。
- **`Seamless.Collections.Persisted`**:
  OS のファイルシステムキャッシュと連動するメモリマップトファイル追記ストレージ。
- **`Seamless.Collections.Tsavorite`**:
  RAM と SSD をシームレスにまたぐ Microsoft Tsavorite ベースのハイブリッドログ KVS。

### データベースエンジン (`Seamless.Database`)
- テーブル、ビュー、ストアドプロシージャの型安全な定義。
- WAL（Write-Ahead Log）による追記と、クラッシュリカバリ（障害復旧）。
- 単一ライブラリ内で ACID トランザクションを完結。

---

## 関連項目

- [ORマッパーとの根本的相違](orm-vs-typed-database.md) : 実行時オーバーヘッド排除の比較
- [コレクション内部構造](data-structures.md) : 各データ構造のメモリモデル
- [共通ストアと実行スコープ](execution-scope.md) : リソース管理とライフサイクル
