# 先行のゼロアロケーションLINQを超えた理由 (Beyond Struct-based LINQ)

近年、C# / .NET のハイパフォーマンス領域では、「イテレータをクラス（参照型）ではなく `struct`（値型）として定義し、ヒープアロケーションを 0 に抑える」という**構造体ベースの LINQ（ValueEnumerable）アプローチ**が広く研究・利用されてきました。

確かに、構造体化によって基本的なアロケーションは排除されます。しかし、**「真に C 言語や手書きの命令型ループと同等の極限性能が出せるか？」** という問いに対しては、構造体ベースのアーキテクチャには越えられない「構造的な限界」が存在していました。

Seamless（FLinq）が、先行する構造体ベースの LINQ 実装を大幅に超えるスループットを達成した技術的理由を解説します。

---

## 1. 構造体ベース LINQ が直面する「3つの壁」

### 壁①: `MoveNext()` / `Current` ハンドシェイクの連鎖
構造体ベースの LINQ では、各演算子が独立した構造体としてチェーン（ネスト）されます：

```csharp
// 構造体の多段ネスト
WhereStruct<SelectStruct<ArrayStruct<int>, int>, int> iterator;
```

要素を 1 つ進めるたびに、内側の構造体から外側の構造体へと順々に `MoveNext()` が呼び出され、`Current` プロパティ経由で値が受け渡されます。
C# コンパイラや JIT（Just-In-Time）コンパイラがメソッド呼び出しをインライン展開したとしても、**「各ステージごとの状態フラグ管理」「条件分岐」「一時レジスタへの退避と復元」のオーバーヘッドが依然として残り、JIT コンパイラがループ全体を一体化（Loop Fusion）することができません**。

### 壁②: 型爆発（Type Explosion）と JIT コンパイル遅延
クエリが `source.Where(...).Select(...).Skip(...).Take(...)` と長くなるにつれ、型パラメータが幾何級数的に巨大化します。
これはアプリケーション起動時の JIT コンパイル時間や Native AOT のバイナリサイズを著しく増大させます。

### 壁③: メモリ連続領域（Span）に対するバイパス不可
元のソースが `int[]` や `Span<int>` のような連続した平坦なメモリであっても、構造体イテレータの規約に従う以上、一歩一歩 `MoveNext()` をインクリメントする抽象化レイヤーを完全に剥ぎ取ることができません。

---

## 2. Seamless (FLinq) による革新: コンパイル時フラット化

Seamless（FLinq）は、構造体のラッパーを連鎖させるアプローチを根本から捨て去りました。

**「Roslyn C# 12+ Interceptor を使い、クエリ式全体を 1 つの平坦な単一ループへとコンパイル時に直接コード生成する」** という手法を採用しています。

```mermaid
flowchart TD
    subgraph Traditional [従来の構造体ベース LINQ]
        Outer[WhereStruct] -->|MoveNext / Current| Middle[SelectStruct]
        Middle -->|MoveNext / Current| Inner[SourceStruct]
        Note1[多段の状態機械が残り、JIT がループを融合できない]
    end

    subgraph FLinq [Seamless (FLinq)]
        FlatLoop["単一のフラットループへ直接展開<br/>for (int i = 0; i < len; i++) {<br/>    int val = span[i];<br/>    if (val > 0) sum += val * 2;<br/>}"]
        Note2[中間オブジェクト・状態機械 0<br/>C 言語手書きループと完全に同等]
    end
```

### なぜこれが圧倒的に速いのか？

1. **JIT コンパイラの最適化能力を 100% 引き出す**:
   生成されたコードは単なる 1 つのプレーンな `for` ループであるため、JIT コンパイラは **ループアンローリング（Loop Unrolling）**、**レジスタ割り当ての最適化**、**不要な境界チェックの完全除去** を完璧に適用できます。
2. **Direct Span Loop**:
   配列やリストに対しては、エニュメレータ呼び出しを一切挟まず、ポインタまたは `Span<T>` の直接走査へと展開されます。
3. **Pipeline Fusion と Predicate Pushdown**:
   複数の条件式（`Where`）をコンパイル時に 1 つの `&&` 式へ合成し、射影式（`Select`）を直接埋め込むことで、中間ステップを完全に消滅させます。

---

## 3. 実測ベンチマークによる証明

リポジトリ内のベンチマーク検証（4096 要素、Release ビルド、同一環境下での比較検証）において、FLinq は従来の構造体ベース実装に対して以下の実測優位性を記録しています：

- **基本クエリ（`Where` + `Select` + 集計）**:
  構造体ベース実装比で **1.2x 〜 1.7x** の高速化を達成。
- **特定操作（`SequenceEqual`, `TakeWhile`, `Concat` 等）**:
  直接スパン走査や不要な状態機械のバイパスにより、**数倍〜数十倍** のレイテンシ短縮を記録。
- **ヒープアロケーション**:
  どちらのアプローチも 0 バイトですが、FLinq は **CPU 命令数（Instruction Count）とキャッシュミス率が大幅に低い** という本質的なアドバンテージを持っています。

---

## 関連項目

- [C# の性能限界を引き出す](csharp-to-the-metal.md) : Unsafe, Span, MemoryPack の極限活用
- [FLinq 内部設計](../architecture/flinq-internals.md) : パイプライン融合と Roslyn 最適化パス
- [ゼロアロケーション LINQ ガイド](../guides/flinq-quickstart.md) : ユーザー向け導入手順
