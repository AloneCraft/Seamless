# メモリアクセス効率化とゼロアロケーション (Memory & Zero-Allocation)

高パフォーマンスな .NET アプリケーションにおいて、最大の敵は **ガベージコレクション（GC）によるスレッド停止（Stop-the-World ポーズ）** です。
ゲームではフレーム落ち（Micro-stutter）の原因となり、サーバーではテールレイテンシ（p99 / p99.9）の悪化を招きます。

Seamless は、メモリの確保・レイアウト・再利用において、徹底した「ゼロアロケーション設計」を貫いています。

---

## 1. Blittable レイアウトとアンマネージド・アロケーション

### Blittable（ブリッタブル）型とは？
マネージドメモリ上とアンマネージドメモリ上で、全く同じバイナリ表現を持つデータ型のことです（`byte`, `int`, `long`, `float`、およびこれらの型のみで構成される `struct`）。

```csharp
// Blittable な構造体（メモリコピーのみで転送可能）
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ItemPosition(int X, int Y, int Z);

// 非 Blittable なクラス（参照ポインタやヘッダを持つため、GC 走査が必要）
public class ManagedItemPosition { public int X; public int Y; public int Z; }
```

### なぜ Blittable が重要なのか？
1. **GC の走査コストが完全に 0**:
   `NativeMemory.Alloc` で確保したアンマネージド領域に Blittable 構造体を 1,000 万個格納しても、.NET の GC はその領域を一切スキャンしません。結果として、Gen2 GC が発生してもポーズ時間は 1 マイクロ秒も伸びません。
2. **高速なバルクコピー（`Buffer.MemoryCopy`）**:
   メモリからディスク、あるいはネットワークバッファへの転送時に、シリアライズ処理が不要で、生の `memcpy`（CPU の DMA / rep movsb）による最高帯域でデータが流れます。

---

## 2. バッファプールとリサイクル設計

一時的な配列やバッファが必要な場合、`new T[size]` を呼ぶことは固く禁じられています。
Seamless では、以下の 2 段階のプール機構を内部で備えています：

1. **`ArrayPool<T>.Shared` の適切なラッピング**:
   一時的なハッシュテーブルやソート用バッファはプールから借用し、処理終了時に `using` ステートメントで確実に返却します。
2. **`OwnedBuffer` による所有権管理**:
   `Seamless.Execution` は、バッファの二重解放やリークをコンパイル時および実行時に追跡する `OwnedBuffer` を提供し、安全な再利用を担保します。

---

## 3. キャッシュライン（64バイト）を意識したパディングと整列

現代の CPU は、メモリをバイト単位ではなく **64 バイトのキャッシュライン（Cache Line）** 単位で L1/L2 キャッシュにロードします。

複数のスレッドが、同一の 64 バイトキャッシュライン内に存在する異なる変数（例: スレッドAのカウンタと、スレッドBのカウンタ）を同時に更新すると、**偽共有（False Sharing）** が発生し、L1 キャッシュが無効化され合って激しい性能低下を引き起こします。

```text
[ 偽共有 (False Sharing) の発生例 ]
Cache Line (64 bytes)
+-------------------------+-------------------------+
| Thread 1 が更新する変数 | Thread 2 が更新する変数 |  <-- キャッシュが無効化され続ける
+-------------------------+-------------------------+

[ Seamless によるキャッシュライン・パディング ]
Cache Line 1 (64 bytes)
+-------------------------+-------------------------+
| Thread 1 の変数         | [Padding 56 bytes]      |
+-------------------------+-------------------------+
Cache Line 2 (64 bytes)
+-------------------------+-------------------------+
| Thread 2 の変数         | [Padding 56 bytes]      |
+-------------------------+-------------------------+
```

Seamless の内部カウンタ（`LinqCounter`）や並行ワーカーでは、`[StructLayout(LayoutKind.Explicit, Size = 64)]` 等を用いてキャッシュライン境界をまたぐように整列（Align）されており、マルチスレッド実行時のスケーラビリティを極限まで高めています。

---

## 関連項目

- [先行の構造体LINQを超えた理由](beyond-struct-linq.md) : ループ展開による一時オブジェクトの排除
- [C# の性能限界を引き出す](csharp-to-the-metal.md) : Unsafe と MemoryPack の活用
- [ハードウェア適合と NUMA / Affinity](hardware-and-numa.md) : メモリの局所性設計
