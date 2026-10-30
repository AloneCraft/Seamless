# Tsavorite / FASTER チューニング (Tsavorite Tuning)

`Seamless.Collections.Tsavorite` は、Microsoft Research の **Tsavorite / Garnet** をベースに構築されています。数億〜数十億件のデータをミリ秒未満で処理するためには、その内部パラメータの適切なチューニングが不可欠です。

---

## 1. HybridLog の主要パラメータ

Tsavorite の中核である **HybridLog** は、以下の設定によってメモリとディスクの境界を定義します。

```mermaid
flowchart TD
    subgraph RAM [RAM (高速アクセス領域)]
        MemorySize["MemorySizeBits (RAM上のページ総量)"]
        PageSize["PageSizeBits (1ページのサイズ)"]
    end

    subgraph SSD [SSD / Disk (不揮発ログ領域)]
        SegmentSize["SegmentFileBits (1セグメントファイルのサイズ)"]
    end

    RAM -->|メモリ上限を超えると自動フラッシュ| SSD
```

### パラメータの意味と推奨値

| パラメータ | 意味 | 既定・推奨設定 | チューニングの指針 |
| :--- | :--- | :--- | :--- |
| **`PageSizeBits`** | 1ページのサイズ（$2^N$ バイト） | `14` (16 KB) 〜 `20` (1 MB) | 小さいと細かいメモリ管理が可能。大きいとディスク IO スループットが向上。 |
| **`MemorySizeBits`** | RAM 内に保持する HybridLog の総容量（$2^N$ バイト） | `26` (64 MB) 〜 `30` (1 GB) | アプリケーションが許容する最大 RAM 容量に合わせて設定。 |
| **`SegmentFileBits`** | 1つのディスクセグメントファイルのサイズ | `28` (256 MB) 〜 `30` (1 GB) | SSD への連続書き込み効率を最大化するため、256MB 以上を推奨。 |
| **`IndexSize`** | ハッシュテーブルのバケット数 | 格納予定件数の 1.5倍〜2倍 | バケットが不足するとハッシュ衝突連鎖が起き、レイテンシが増大。 |

---

## 2. `TsavoriteTuning` によるカスタマイズ

Seamless では、`TsavoriteTuning` 構造体を通じて、ストア作成時に詳細なパラメータを注入できます。

```csharp
using Seamless.Collections.Tsavorite;

// 1,000 万件の注文データを格納する高スループット設定
var tuning = new TsavoriteTuning
{
    IndexSize = 16_777_216,   // 16M バケット (2^24)
    PageSizeBits = 18,        // 256 KB ページ
    MemorySizeBits = 30,      // 1 GB RAM キャッシュ
    SegmentFileBits = 29,     // 512 MB セグメントファイル
    AutoGrowIndex = false     // 実行時のインデックス拡張によるスパイクを防止
};

using var store = new DictionaryStore<long, OrderRecord>("./data/orders", tuning);
```

---

## 3. チェックポイントとリカバリの設計

Tsavorite は、稼働中のデータ整合性を損なうことなく、瞬時にディスクへ状態を退避する **スナップショット・チェックポイント（Snapshot Checkpoint）** 機能を備えています。

- **`TakeFullCheckpointAsync()`**:
  ハッシュインデックスと HybridLog の両方を確定。次回起動時に `Recover()` を呼ぶことで、数秒以内で完全な状態へ復帰可能。
- **定期チェックポイントの運用**:
  大量書き込みを行うバッチ処理やサーバーでは、定期的にチェックポイントを取得することで、WAL ファイルの肥大化を防ぎ、復旧時間を短縮できます。

---

## 関連項目

- [コレクション内部構造](../architecture/data-structures.md) : HybridLog の動作原理
- [共通ストアと実行スコープ](../architecture/execution-scope.md) : `Store` による全体リソース統合
- [永続化ストレージの選び方](../guides/persistence-guide.md) : 各ストレージの比較
