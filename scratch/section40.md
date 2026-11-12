
### 40. Where/Select 先行 Top-K 直結最適化と検証（2026-11-08）

#### 概要
`OrderBy.Take` の前段に `Where` や `Select` が先行するパイプライン（`source.Where(...).Select(...).OrderBy(...).Take(K).ToArray()` など）において、従来は Direct Span Pipeline が拒否され汎用構造体パイプラインへフォールバックしていた。
`CanUseDirectSpanPipelineOrder` を拡張し、Top-K 時に前段の Direct Span 互換ステージ（`Where`, `Select`, `Cast`, `AsEnumerable`）を許可。走査ループ内でフィルタ条件評価および射影をインライン適用し、`stackalloc` / 配列のストリーミング挿入ソートバッファへ直結展開する最適化（`RenderDirectSpanPipelineOrderTopK`）を実装した。
また、マネージド型（`Trade` 等）に対する `stackalloc` 制約（CS0208）を回避するため、アンマネージド型とマネージド型で安全に分岐するコード生成を導入した。

#### 実測比較結果（static-where-select-order-take）
- **クエリ**: `source.Where(x=>(x&1)==0).Select(x=>x*3+1).OrderBy(x=>x).Take(5).ToArray()`
- **System.Linq**: 1,736.1 ns (2,224 B)
- **ZLinq**: 1,342.7 ns (464 B)
- **FLinq (最適化前)**: 421.8 ns (464 B)
- **FLinq (最適化後)**: **195.5 ns (304 B)** —— **System 比 8.88 倍高速化・メモリ 86.3% 削減・最適化前比 2.16 倍短縮**
- **手書き Ideal**: 136.9 ns (304 B)

#### 回帰検証
- 固定ケース全 33 件（`--fixed`）において回帰ゼロ（**全 33 件すべてで System.Linq に完全勝利、勝率 100% 維持**）。
- 実運用 Q05（`OrderByDescending.ThenBy.Take.ToArray`）においても **4,204.6 ns / Passed: true** を維持。
