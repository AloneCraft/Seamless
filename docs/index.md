# Seamless ドキュメントポータル (Documentation Portal)

**Seamless** は、.NET 向けの高パフォーマンスなインメモリ／永続化コレクション、ゼロアロケーション LINQ（FLinq）、および型付きインメモリ/ハイブリッド データベース ライブラリです。

「**メモリに載り切らない膨大なデータであっても、.NET 標準のコレクション・LINQ・C# クラスの書き味のまま、極限の性能でストレージへ分散して扱える**」ことを目指して設計されています。

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
