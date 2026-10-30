# 永続化ストレージの選び方 (Persistence Guide)

Seamless は、データの性質やライフサイクル、ハードウェア要件に合わせて **3つの主要なストレージバックエンド** を提供しています。

---

## 3つのストレージモードの比較

| 特性 | InMemory | Persisted | Tsavorite |
| :--- | :--- | :--- | :--- |
| **主な提供パッケージ** | `Seamless.Collections` | `Seamless.Collections.Persisted` | `Seamless.Collections.Tsavorite` |
| **永続性** | 揮発性 (プロセス終了で消去) | 永続 (追記型ファイル) | 永続 (ハイブリッドログ + KVS) |
| **RAM 上限を超えたデータ** | 扱えない (OOM となる) | メモリマップトファイルで退避可能 | 自動的にディスクへ退避 (HybridLog) |
| **主な適正用途** | 高速計算、一時作業、単体テスト | 追記型ログ、順次アクセス、軽量組み込み | 巨大KVS、ランダムアクセス、DBテーブル |
| **バックエンドエンジン** | 純 C# 配列 / 構造体 | メモリマップトファイル (Paging) | Microsoft Tsavorite (FASTER / Garnet) |
| **並行性** | ロックフリー / 並行コレクション | 追記スレッド安全 | セッション分割並行アクセス |

---

## 1. InMemory コレクション (`Seamless.Collections`)

プロセス内の RAM のみを使用する最高速のコレクション群です。

- **`BlittableList<T>`**:
  要素が blittable（アンマネージド値型）である場合、アンマネージドメモリブロックを直接確保し、GC の管理対象外とすることで大量要素を保持しても GC 停止時間を増やしません。
- **適しているシナリオ**:
  - 毎フレーム大量の座標データを更新するゲーム内バッファ
  - 外部 DB から一時的にフェッチして一括処理するバッチ計算
  - 単体テストの実行

---

## 2. Persisted コレクション (`Seamless.Collections.Persisted`)

OS の **メモリマップトファイル（Memory-Mapped Files）** をバックエンドとし、シーケンシャルな追記と高速なスキャンに特化したコレクション群です。

- **`AppendOnlyList<T>` / `BlittableSet<T>`**:
  データを末尾に連続的に書き込みます。OS のファイルページキャッシュを最大限に活用し、ディスクへの書き込み遅延を隠蔽します。
- **適しているシナリオ**:
  - イベントソーシングや操作ログの記録
  - LINQ の大規模 `GroupBy` / `OrderBy` の中間結果退避（Spill-to-Disk）
  - リソース制約の厳しいモバイル・組み込み環境でのファイル永続化

---

## 3. Tsavorite コレクション (`Seamless.Collections.Tsavorite`)

Microsoft Research が開発した **Tsavorite (FASTER) / Garnet** のアーキテクチャをベースとする、エンタープライズ級のハイブリッド KVS です。

- **`DictionaryStore<TKey, TValue>` / `HashSetStore<TKey>`**:
  RAM 上のキャッシュ領域とディスク上のログ領域を一体化した **HybridLog** により、RAM 容量を大幅に超える数億〜数十億件のデータをミリ秒未満のレイテンシでランダムアクセスできます。
- **適しているシナリオ**:
  - `DatabaseContainer` のテーブルバックエンド
  - 大規模なセッションキャッシュ・KV ストア
  - 高速なチェックポイント保存とクラッシュリカバリが必要な基幹データ

---

## 関連項目

- [共通ストアと実行スコープ](../architecture/execution-scope.md) : `Store` によるライフサイクルとメモリ上限の統合管理
- [コレクション内部構造](../architecture/data-structures.md) : 各ストレージのメモリ配置とアルゴリズム
- [Tsavorite チューニング](../performance/tsavorite-tuning.md) : ページサイズ、セグメント、チェックポイントの最適設定
