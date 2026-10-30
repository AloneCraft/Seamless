# Seamless ドキュメントポータル (Documentation Portal)

**Seamless** は、.NET 向けの高パフォーマンスなインメモリ／永続化コレクション、ゼロアロケーション LINQ（FLinq）、および型付きインメモリ/ハイブリッド データベース ライブラリです。

「**メモリに載り切らない膨大なデータであっても、.NET 標準のコレクション・LINQ・C# クラスの書き味のまま、極限の性能でストレージへ分散して扱える**」ことを目指して設計されています。

---

<div class="my-4 d-flex justify-content-center">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 960 420" width="100%" height="auto" style="max-width: 960px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;">
<defs>
<linearGradient id="bg-grad" x1="0%" y1="0%" x2="100%" y2="100%">
<stop offset="0%" stop-color="#090d13" />
<stop offset="50%" stop-color="#0d1117" />
<stop offset="100%" stop-color="#161b22" />
</linearGradient>
<linearGradient id="red-card-grad" x1="0%" y1="0%" x2="100%" y2="100%">
<stop offset="0%" stop-color="#2b1114" stop-opacity="0.8" />
<stop offset="100%" stop-color="#1c1215" stop-opacity="0.9" />
</linearGradient>
<linearGradient id="purple-card-grad" x1="0%" y1="0%" x2="100%" y2="100%">
<stop offset="0%" stop-color="#1f1838" stop-opacity="0.9" />
<stop offset="100%" stop-color="#161b22" stop-opacity="0.9" />
</linearGradient>
<linearGradient id="green-card-grad" x1="0%" y1="0%" x2="100%" y2="100%">
<stop offset="0%" stop-color="#102b1f" stop-opacity="0.85" />
<stop offset="100%" stop-color="#121e1a" stop-opacity="0.9" />
</linearGradient>
<linearGradient id="arrow-grad-1" x1="0%" y1="0%" x2="100%" y2="0%">
<stop offset="0%" stop-color="#f85149" />
<stop offset="100%" stop-color="#bc8cff" />
</linearGradient>
<linearGradient id="arrow-grad-2" x1="0%" y1="0%" x2="100%" y2="0%">
<stop offset="0%" stop-color="#bc8cff" />
<stop offset="100%" stop-color="#3fb950" />
</linearGradient>
<filter id="glow-red" x="-20%" y="-20%" width="140%" height="140%">
<feDropShadow dx="0" dy="0" stdDeviation="6" flood-color="#f85149" flood-opacity="0.5"/>
</filter>
<filter id="glow-purple" x="-20%" y="-20%" width="140%" height="140%">
<feDropShadow dx="0" dy="0" stdDeviation="6" flood-color="#bc8cff" flood-opacity="0.6"/>
</filter>
<filter id="glow-green" x="-20%" y="-20%" width="140%" height="140%">
<feDropShadow dx="0" dy="0" stdDeviation="8" flood-color="#3fb950" flood-opacity="0.6"/>
</filter>
</defs>
<style>
.card-link { text-decoration: none; cursor: pointer; }
.interactive-card { transition: filter 0.2s ease, opacity 0.2s ease; }
.card-red:hover { filter: url(#glow-red); opacity: 0.95; }
.card-purple:hover { filter: url(#glow-purple); opacity: 0.95; }
.card-green:hover { filter: url(#glow-green); opacity: 0.95; }
.code-font { font-family: 'Consolas', 'Courier New', monospace; font-size: 11.5px; }
</style>
<rect width="960" height="420" rx="16" fill="url(#bg-grad)" stroke="#30363d" stroke-width="1.5"/>
<g transform="translate(30, 24)">
<circle cx="8" cy="8" r="4" fill="#58a6ff" />
<circle cx="22" cy="8" r="4" fill="#3fb950" />
<circle cx="36" cy="8" r="4" fill="#bc8cff" />
<text x="56" y="13" fill="#f0f6fc" font-size="16" font-weight="700" letter-spacing="0.5">FLinq: コンパイル時ゼロアロケーション・パイプライン展開アーキテクチャ</text>
<text x="56" y="30" fill="#8b949e" font-size="12">C# 12+ Interceptor が標準 LINQ 構文を手書き C 相当のフラットループへ完全自動置換</text>
</g>
<a href="performance/beyond-struct-linq.md" class="card-link" target="_self">
<g class="interactive-card card-red" transform="translate(30, 72)">
<rect width="265" height="320" rx="12" fill="url(#red-card-grad)" stroke="#da3633" stroke-width="1.5"/>
<rect x="14" y="14" width="237" height="28" rx="6" fill="#da3633" fill-opacity="0.25" stroke="#f85149" stroke-width="1"/>
<text x="132" y="33" fill="#ff7b72" font-size="13" font-weight="700" text-anchor="middle">❌ 従来の System.Linq</text>
<rect x="14" y="52" width="237" height="42" rx="6" fill="#0d1117" stroke="#30363d"/>
<text x="22" y="70" fill="#8b949e" class="code-font">// 呼び出しコード</text>
<text x="22" y="85" fill="#f0f6fc" class="code-font">src.<tspan fill="#d2a8ff">Where</tspan>(..).<tspan fill="#d2a8ff">Select</tspan>(..).<tspan fill="#79c0ff">Sum</tspan>()</text>
<text x="18" y="115" fill="#ff7b72" font-size="11" font-weight="700">【実行時の多重オーバーヘッド】</text>
<g transform="translate(18, 126)">
<circle cx="6" cy="10" r="3" fill="#f85149"/>
<text x="16" y="14" fill="#c9d1d9" font-size="11.5">WhereEnumerableIterator 確保</text>
<circle cx="6" cy="30" r="3" fill="#f85149"/>
<text x="16" y="34" fill="#c9d1d9" font-size="11.5">SelectEnumerableIterator 確保</text>
<circle cx="6" cy="50" r="3" fill="#f85149"/>
<text x="16" y="54" fill="#c9d1d9" font-size="11.5">Func&lt;T, bool&gt; デリゲート確保</text>
<circle cx="6" cy="70" r="3" fill="#f85149"/>
<text x="16" y="74" fill="#c9d1d9" font-size="11.5">多段 MoveNext() 仮想関数呼出</text>
<circle cx="6" cy="90" r="3" fill="#f85149"/>
<text x="16" y="94" fill="#ffa198" font-size="11.5" font-weight="600">💥 GC ポーズ / キャッシュミス頻発</text>
</g>
<rect x="14" y="246" width="237" height="34" rx="6" fill="#3d1418" stroke="#f85149" stroke-width="1"/>
<text x="132" y="262" fill="#ff7b72" font-size="11" text-anchor="middle">Heap Allocation: <tspan font-weight="700">&gt; 120 bytes / 呼出</tspan></text>
<text x="132" y="274" fill="#8b949e" font-size="10" text-anchor="middle">毎秒の反復で大量の GC スパイク</text>
<text x="132" y="304" fill="#79c0ff" font-size="11" text-anchor="middle">👉 構造的課題の解説を読む ↗</text>
</g>
</a>
<a href="architecture/flinq-internals.md" class="card-link" target="_self">
<g class="interactive-card card-purple" transform="translate(320, 84)">
<rect width="280" height="296" rx="12" fill="url(#purple-card-grad)" stroke="#8957e5" stroke-width="1.5"/>
<rect x="16" y="14" width="248" height="28" rx="6" fill="#8957e5" fill-opacity="0.3" stroke="#bc8cff" stroke-width="1"/>
<text x="140" y="33" fill="#d2a8ff" font-size="13" font-weight="700" text-anchor="middle">⚡ Roslyn Interceptor Engine</text>
<text x="20" y="65" fill="#bc8cff" font-size="11" font-weight="700">【コンパイル時パイプライン融合】</text>
<g transform="translate(20, 76)">
<rect x="0" y="0" width="240" height="28" rx="4" fill="#0d1117" stroke="#30363d"/>
<text x="10" y="18" fill="#e6edf3" font-size="11">① 構文木解析・呼出位置 (Location) 検知</text>
<rect x="0" y="36" width="240" height="28" rx="4" fill="#0d1117" stroke="#30363d"/>
<text x="10" y="54" fill="#e6edf3" font-size="11">② 呼び出しコード無変更で置換 (0-Touch)</text>
<rect x="0" y="72" width="240" height="28" rx="4" fill="#0d1117" stroke="#30363d"/>
<text x="10" y="90" fill="#e6edf3" font-size="11">③ 多段クエリを単一フラットループへ融合</text>
<rect x="0" y="108" width="240" height="28" rx="4" fill="#0d1117" stroke="#30363d"/>
<text x="10" y="126" fill="#e6edf3" font-size="11">④ 複数文に分かれたクエリも自動展開</text>
</g>
<rect x="16" y="222" width="248" height="34" rx="6" fill="#1f1838" stroke="#bc8cff" stroke-width="1"/>
<text x="140" y="238" fill="#d2a8ff" font-size="10.5" class="code-font" text-anchor="middle">[InterceptsLocation("...", line, col)]</text>
<text x="140" y="250" fill="#8b949e" font-size="10" text-anchor="middle">完全コンパイル時解決 / 実行時リフレクション 0</text>
<text x="140" y="280" fill="#79c0ff" font-size="11" text-anchor="middle">👉 インターセプトの仕組みを詳しく見る ↗</text>
</g>
</a>
<a href="guides/flinq-quickstart.md" class="card-link" target="_self">
<g class="interactive-card card-green" transform="translate(625, 72)">
<rect width="305" height="320" rx="12" fill="url(#green-card-grad)" stroke="#238636" stroke-width="1.8"/>
<rect x="14" y="14" width="277" height="28" rx="6" fill="#238636" fill-opacity="0.35" stroke="#3fb950" stroke-width="1"/>
<text x="152" y="33" fill="#3fb950" font-size="13" font-weight="700" text-anchor="middle">🚀 Seamless FLinq (Zero-Alloc)</text>
<rect x="14" y="52" width="277" height="42" rx="6" fill="#0d1117" stroke="#2ea043"/>
<text x="22" y="68" fill="#3fb950" class="code-font">// 呼び出しコードは 1 文字も不変！</text>
<text x="22" y="85" fill="#f0f6fc" class="code-font">src.<tspan fill="#3fb950">Where</tspan>(..).<tspan fill="#3fb950">Select</tspan>(..).<tspan fill="#79c0ff">Sum</tspan>()</text>
<text x="18" y="115" fill="#3fb950" font-size="11" font-weight="700">【手書き C 相当のネイティブループへ直結】</text>
<g transform="translate(18, 126)">
<circle cx="6" cy="10" r="3" fill="#3fb950"/>
<text x="16" y="14" fill="#c9d1d9" font-size="11.5">単一のインライン flat while/for ループ</text>
<circle cx="6" cy="30" r="3" fill="#3fb950"/>
<text x="16" y="34" fill="#c9d1d9" font-size="11.5">CPU レジスタ直接割り当て &amp; SIMD 最適化</text>
<circle cx="6" cy="50" r="3" fill="#3fb950"/>
<text x="16" y="54" fill="#c9d1d9" font-size="11.5">イテレータ／デリゲートのオブジェクト皆無</text>
<circle cx="6" cy="70" r="3" fill="#3fb950"/>
<text x="16" y="74" fill="#c9d1d9" font-size="11.5">L1 CPU キャッシュ内で最高密度回転</text>
<circle cx="6" cy="90" r="3" fill="#3fb950"/>
<text x="16" y="94" fill="#7ee787" font-size="11.5" font-weight="600">✨ GC 停止なし・フレーム落ち・p99遅延解消</text>
</g>
<rect x="14" y="246" width="277" height="34" rx="6" fill="#0f3d20" stroke="#3fb950" stroke-width="1.5"/>
<text x="152" y="262" fill="#3fb950" font-size="12" font-weight="800" text-anchor="middle">Heap Allocation: 完全 0 bytes</text>
<text x="152" y="274" fill="#7ee787" font-size="10" text-anchor="middle">手書き C 言語ループと同等の限界スループット</text>
<text x="152" y="304" fill="#79c0ff" font-size="11" text-anchor="middle">👉 5分クイックスタート・実装ガイドを見る ↗</text>
</g>
</a>
<path d="M 295 210 L 320 210" stroke="url(#arrow-grad-1)" stroke-width="2.5" fill="none" stroke-dasharray="4,3"/>
<path d="M 600 210 L 625 210" stroke="url(#arrow-grad-2)" stroke-width="3" fill="none"/>
<polygon points="625,210 617,205 617,215" fill="#3fb950" />
</svg>
</div>

---

## ⚡ 驚異的なパフォーマンス: Before & After

Seamless (FLinq) を導入すると、呼び出し側の C# コードを **1 文字も変更することなく**、コンパイル時に手書き C 言語ループと同等の機械語へと直接置き換わります。

```csharp
// 【従来の C# コード (System.Linq)】
// イテレータインスタンスの多段アロケーション、デリゲート呼び出し、多段 MoveNext() により、
// GC ポーズ（フレーム落ちや p99 遅延）の原因となる
var total = list.Where(x => x % 2 == 0).Select(x => x * 2).Sum();

// 【Seamless (FLinq) を有効化したコード】
// 呼び出し側のコードはそのままで、コンパイル時インターセプターが以下と同等の平坦なループへ自動展開！
// ヒープアロケーション: 完全 0 bytes / 手書き C 言語ループと同等の最高スループット
```

### 性能とリソースの対比

| 評価項目 | 従来の LINQ / 一般的な ORM | Seamless (FLinq & Typed Database) |
| :--- | :--- | :--- |
| **ヒープアロケーション** | クエリごとに多数の中間オブジェクトを確保 | **完全 0 bytes (Zero-Allocation)** |
| **LINQ 実行時オーバーヘッド** | 構造体やクラスの状態機械・デリゲート間接呼出 | **単一のフラットループへ直接インライン化** |
| **DB メタデータ解決** | 実行時リフレクション・動的 SQL パース | **コンパイル時完全静的バインディング** |
| **クライアント動作** | モバイルや Unity では GC スパイク頻発 | **60fps / 120fps を維持したままローカル検索** |
| **適用範囲** | サーバー側とクライアント側でコードが分断 | **同一の C# コードがサーバー・端末・WASM で動作** |

---

## 📚 ドキュメント ナビゲーション

<div class="row row-cols-1 row-cols-md-2 g-4 my-3">
  <div class="col">
    <div class="card h-100 border-primary shadow-sm">
      <div class="card-body">
        <h4 class="card-title"><a href="guides/getting-started.md" class="text-decoration-none">📘 ガイド (Guides)</a></h4>
        <p class="card-text text-muted">初めての導入から、型付きDBアクセス、FLinq の使い方、サーバー・クライアント共通設計までをステップバイステップで解説します。</p>
        <ul class="list-unstyled mb-0 small">
          <li class="py-1">👉 <a href="guides/getting-started.md">5分で始める Seamless</a></li>
          <li class="py-1">👉 <a href="guides/typed-database-guide.md">型付きDBアクセス入門</a></li>
          <li class="py-1">👉 <a href="guides/flinq-quickstart.md">ゼロアロケーション LINQ (FLinq) クイックスタート</a></li>
          <li class="py-1">👉 <a href="guides/universal-architecture.md">サーバー・クライアント共通アーキテクチャ</a></li>
          <li class="py-1">👉 <a href="guides/persistence-guide.md">永続化ストレージの選び方</a></li>
        </ul>
      </div>
    </div>
  </div>

  <div class="col">
    <div class="card h-100 border-info shadow-sm">
      <div class="card-body">
        <h4 class="card-title"><a href="architecture/overview.md" class="text-decoration-none">🏛️ アーキテクチャ (Architecture)</a></h4>
        <p class="card-text text-muted">2層構造のシステム設計、データライフサイクル、ORマッパーとの根本的相違、WAL エンジンの内部原理を深く理解します。</p>
        <ul class="list-unstyled mb-0 small">
          <li class="py-1">👉 <a href="architecture/overview.md">全体アーキテクチャ (2層構造)</a></li>
          <li class="py-1">👉 <a href="architecture/orm-vs-typed-database.md">ORマッパーとの根本的相違</a></li>
          <li class="py-1">👉 <a href="architecture/data-structures.md">コレクション内部構造とメモリモデル</a></li>
          <li class="py-1">👉 <a href="architecture/execution-scope.md">共通ストアと実行スコープ (資源予算管理)</a></li>
          <li class="py-1">👉 <a href="architecture/database-engine.md">データベースエンジン内部構造 (WAL)</a></li>
          <li class="py-1">👉 <a href="architecture/flinq-internals.md">FLinq 内部設計とパイプライン融合</a></li>
        </ul>
      </div>
    </div>
  </div>

  <div class="col">
    <div class="card h-100 border-danger shadow-sm">
      <div class="card-body">
        <h4 class="card-title"><a href="performance/beyond-struct-linq.md" class="text-decoration-none">⚡ 性能とハードウェア適合 (Performance)</a></h4>
        <p class="card-text text-muted">C# の極限性能を引き出す技術、NUMA・CPU トポロジ・Affinity 最適化、および実開発で得られたリアルな知見と落とし穴を解説します。</p>
        <ul class="list-unstyled mb-0 small">
          <li class="py-1">👉 <a href="performance/beyond-struct-linq.md">先行の構造体LINQを超えた理由</a></li>
          <li class="py-1">👉 <a href="performance/csharp-to-the-metal.md">C#の性能限界を引き出す技術</a></li>
          <li class="py-1">👉 <a href="performance/hardware-and-numa.md">ハードウェア適合と NUMA / Affinity</a></li>
          <li class="py-1">👉 <a href="performance/memory-and-zero-alloc.md">メモリアクセス効率化とゼロアロケーション</a></li>
          <li class="py-1">👉 <a href="performance/tsavorite-tuning.md">Tsavorite / FASTER チューニング</a></li>
          <li class="py-1">👉 <a href="performance/lessons-learned.md">開発中に得られた技術的知見と落とし穴</a></li>
        </ul>
      </div>
    </div>
  </div>

  <div class="col">
    <div class="card h-100 border-success shadow-sm">
      <div class="card-body">
        <h4 class="card-title"><a href="vision/cross-platform.md" class="text-decoration-none">🚀 展望とエコシステム (Vision)</a></h4>
        <p class="card-text text-muted">Linux/Windows/macOS/Unity/WASM へのマルチプラットフォーム展開と、次世代の分散 KVS・AI ナレッジストア像を紹介します。</p>
        <ul class="list-unstyled mb-0 small">
          <li class="py-1">👉 <a href="vision/cross-platform.md">マルチプラットフォーム展開</a></li>
          <li class="py-1">👉 <a href="vision/future-possibilities.md">将来的な可能性と次世代データストア像</a></li>
          <li class="py-1">👉 <a href="api/index.md">📚 API リファレンス (全10パッケージ完全網羅)</a></li>
        </ul>
      </div>
    </div>
  </div>
</div>
