# FLinq 内部設計とパイプライン融合 (FLinq Internals)

`Seamless.FLinq.Generator` は、C# の標準 LINQ 構文をネイティブコードと同等の超高速命令型ループへと変換する Roslyn Incremental Generator です。

---

## 1. 最適化アーキテクチャの全体像

FLinq のコンパイル時処理は、以下の 4 つのフェーズで実行されます。

```mermaid
flowchart TD
    SyntaxTree[C# 構文木解析<br/>- System.Linq メソッド呼び出しの検出] --> TargetDiscovery[ターゲット特定<br/>- 呼び出し箇所のファイルパス・行・列]

    TargetDiscovery --> PipelineBuilder[パイプライン構築<br/>- 演算子の連結順序の把握<br/>- 式の構文木フラグメント化]

    PipelineBuilder --> OptimizationPass[最適化パス<br/>1. Pipeline Fusion (パイプライン融合)<br/>2. Predicate Pushdown (述語プッシュダウン)<br/>3. Projection Merge (射影マージ)<br/>4. Direct Span Loop (直接ループ展開)]

    OptimizationPass --> CodeEmission[コード生成 & Interceptor 注入<br/>- [InterceptsLocation] 属性付与<br/>- 最適化ループコードの生成]
```

---

## 2. 4大最適化パス

### ① Pipeline Fusion（パイプライン融合）
複数の連続する演算子を単一のループステージへ統合します。

- **多段 Where の統合**:
  `source.Where(p1).Where(p2)` → `source.Where(x => p1(x) && p2(x))`
- **Where と Select の融合**:
  `source.Where(p).Select(s)` を 1 つのループブロック内にまとめ、不要な中間変数代入を排除します。

### ② Predicate Pushdown（述語プッシュダウン）
フィルタリング条件（`Where` 述語）を、可能な限りデータソース側の走査ステージへ押し込みます。
たとえば、Tsavorite や Persisted コレクションからデータをスキャンする際、レコード全体をデシリアライズする前に、キーやヘッダ情報だけで判定を行い、不一致レコードのデコード処理をスキップします。

### ③ Projection Merge（射影マージ）
連続する `Select` 操作の関数合成をコンパイル時に展開します。
- `source.Select(f).Select(g)` → `source.Select(x => g(f(x)))`

### ④ Direct Span Loop（直接ポインタ・スパン展開）
データソースが配列（`T[]`）、`List<T>`、または連続メモリ（`Span<T>` / `ReadOnlySpan<T>`）であることが判明している場合、エニュメレータオブジェクトの作成を完全にスキップし、生のインデックスベース `for` ループまたはポインタインクリメントループへと直接展開します。

---

## 3. Fragment システム

FLinq の内部では、各 LINQ 演算子が「**Fragment（断片）**」と呼ばれる独立したコンポーネントクラス群として設計されています（`WhereFragment`, `SelectFragment`, `SumFragment` など 120 種類以上）。

各 Fragment は以下の責務を持ちます：
- 前後の Fragment との融合可能性（CanFuse）の判定
- ループ開始前の初期化コード（バッファ確保、カウンタ初期化）
- ループ本体（要素ごとの処理、早期脱出判定）
- 終了コード（集計値の返却、リソース解放）

このモジュール構造により、新しい LINQ 演算子や最適化パターンの追加が極めて安全かつ迅速に行えるよう設計されています。

---

## 関連項目

- [先行の構造体LINQを超えた理由](../performance/beyond-struct-linq.md) : 構造体イテレータとの性能・アーキテクチャ差
- [C# の性能限界を引き出す](../performance/csharp-to-the-metal.md) : Interceptor と Unsafe の活用
- [ゼロアロケーション LINQ ガイド](../guides/flinq-quickstart.md) : ユーザー向けの利用方法
