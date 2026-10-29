# ハードウェア適合と NUMA / Affinity (Hardware & NUMA Tuning)

ソフトウェアのアルゴリズムがどれほど優れていても、現代のマルチコア・マルチソケット CPU アーキテクチャを無視したメモリアクセスを行えば、性能は容易に半減以下へと落ち込みます。

Seamless は、ハードウェアの物理構造（CPU トポロジ、キャッシュ階層、NUMA ノード）を意識した最適化技術を内蔵しています。

---

## 1. NUMA（Non-Uniform Memory Access）とメモリ局所性

近年のサーバー向け CPU（AMD EPYC, Intel Xeon）やハイエンドデスクトップ CPU（Threadripper 等）は、複数のダイ（チップレット）で構成される **NUMA アーキテクチャ** を採用しています。

```mermaid
flowchart LR
    subgraph Node0 [NUMA Node 0]
        Core0[CPU Cores 0-15]
        L3_0[L3 Cache (Shared)]
        RAM0[(Local Memory)]
        Core0 <--> L3_0 <--> RAM0
    end

    subgraph Node1 [NUMA Node 1]
        Core1[CPU Cores 16-31]
        L3_1[L3 Cache (Shared)]
        RAM1[(Local Memory)]
        Core1 <--> L3_1 <--> RAM1
    end

    Node0 <== Interconnect (Infinity Fabric / UPI) ==> Node1
```

### リモートアクセスのペナルティ
- **ローカルメモリ（Local Access）**: レイテンシ 80〜100ns、高スループット
- **リモートメモリ（Remote Access）**: ノード間インターコネクトを経由するため、レイテンシは 150〜200ns（約 2 倍以上）に跳ね上がり、インターコネクト帯域がボトルネックになります。

Seamless では、スレッドが割り当てられた NUMA ノードと、そのスレッドがアクセスするストレージバッファを同一ノード内に配置するようリソース割り当てを最適化します。

---

## 2. CPU トポロジ検出と Affinity（論理コア固定）

OS のスケジューラは、実行中のスレッドをランダムに別の論理コアへと移動（スレッドマイグレーション）させることがあります。
スレッドが別のコアへ移動すると、それまで L1/L2 キャッシュに乗っていたデータがすべて破棄され、深刻な **キャッシュ汚染（Cache Thrashing）** が発生します。

Seamless のベンチマークおよび実行エンジン（`BenchmarkCpuTopologyProvider`）は、以下のネイティブ API を通じてハードウェアトポロジを自動解析します：

- **Windows**: `GetLogicalProcessorInformationEx` API により、プロセッサグループ、NUMA ノード、物理コア、ハイパースレッディング（SMT）、および P-Core / E-Core（性能コアと高効率コア）の別を特定。
- **Linux**: `/sys/devices/system/cpu/` の `topology` 情報を走査し、物理コアおよび L3 キャッシュ共有関係を把握。

### Core Affinity の効果
高スループットな処理を行うワーカープロセスに対して CPU Affinity（プロセッサ親和性）を設定し、特定の物理コア（同一 L3 キャッシュを共有するコア群）に固定することで：
1. **スレッドのコア間移動を完全防止**
2. **L1/L2/L3 キャッシュヒット率の最大化**
3. **ベンチマーク計測結果のばらつき（ジッター）の徹底排除**
を実現しています。

---

## 3. P-Core / E-Core ハイブリッドアーキテクチャへの対応

最新の Intel CPU（第12世代以降）や Apple Silicon など、性能の異なるコアが混在する環境では、高負荷なデータベース集計処理が誤って E-Core（効率コア）に割り当てられると、実行時間が数倍に伸びてしまいます。

Seamless は、トポロジ解析により各コアの「効率スコア（Efficiency Score）」を算定し、重要な並行ワーカーには優先して P-Core をバインドするアーキテクチャを確立しています。

---

## 関連項目

- [C# の性能限界を引き出す](csharp-to-the-metal.md) : SIMD 命令とキャッシュアラインメント
- [Tsavorite チューニング](tsavorite-tuning.md) : スレッドセッションと並行性設計
- [開発で得られた技術的知見](lessons-learned.md) : ベンチマーク環境における affinity 制約の教訓
