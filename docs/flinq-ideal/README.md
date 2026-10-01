# FLinq 理想ループ比較と IL 監査

FLinq が意図した変換をしているか、その出力が「手書きの理想ループ」と比べて最良かを確かめるための仕組みと、現在分かっていることをまとめる。

## IL 監査ツール(Benchmark/Benchmark.Audit)

ベンチ本体のアセンブリを System.Reflection.Metadata で読み、`[BenchFact]` メソッドごとに次を数える。数値は IL 上の静的な出現数で、実行回数ではない。

- 置き換え(傍受)されたか: `X()` から `X_Impl()` への呼び出しが `Intercept.*` の生成メソッドへ差し替わっているか。
- 置き換え後のコードの形: 置き換えメソッドから同じアセンブリの `Intercept.*` メソッドだけを辿り、ループ(後方分岐の区間)の内外に分けて数える。
  - インターフェース越しの呼び出し(`constrained.` の付かない callvirt)
  - デリゲートの `Invoke`
  - 割り当て(参照型の newobj、newarr。例外を作ってすぐ投げる形は除く)
  - ボックス化(直後が isinst・ldnull・分岐・unbox.any の形は JIT が消すので除く)
  - 計数器 `LinqCounterScope.*` の呼び出し
  - System.Linq/ZLinq の呼び出し、展開不可(FLINQ002)の印、補助型と未使用の補助型

使い方(出力は git 管理外の `/Work/` に置く):

```
dotnet build Benchmark/Benchmark.Audit -c Release -m:1
dotnet Benchmark/Benchmark.Audit/bin/Release/net10.0/Benchmark.Audit.dll audit <ベンチ本体.dll>... --json Work/audit/audit.json --md Work/audit/audit.md
```

不変条件は `Benchmark/Benchmark.Audit.Tests` でテストしている。対象はメモリ上4種(Array/List/IEnumerable/Range)の FLinq ベンチと、全最適化の複合連鎖ベンチのメモリ上の行。

- 置き換え漏れが無いこと
- 置き換え後に、要素を列挙する System.Linq/ZLinq を呼ばないこと(件数確認の `TryGetNonEnumeratedCount` は System.Linq 自身も行う O(1) の確認なので許す)
- 展開不可で例外を投げるだけの置き換えメソッドが無いこと

既知の違反は `KnownExceptions.cs` に理由付きで載せる。直したら必ず消す(違反していない項目が残るとテストが失敗する)。

## 2026-10-02 時点の監査結果(メモリ上4種)

Array/List/IEnumerable は各97本、Range は81本、複合連鎖はメモリ上の行が120本。置き換え済みは 449/452(複合連鎖を含む)。

置き換え漏れ(3件):

- Array・List の `ElementAtOrDefault_index`: `ElementAtOrDefault(int)` が傍受されず System.Linq のまま。原因は未調査。
- IEnumerable の `ElementAt_index`: ベンチ定義が `[LinqIntercept(false)]` の `ElementAt_index_Core` を経由しており、FLinq 列が System.Linq を測っている。

置き換え後も要素を列挙する外部 LINQ を呼ぶもの(6件):

- List の `ToArray`: `List<T>.ToArray` との衝突回避で、ベンチ定義が静的形 `Enumerable.ToArray(src)` になっている。生成器は静的形を(Contains/Reverse 以外)不透明な入口として扱うため、System.Linq の ToArray を呼んでから回す。
- IEnumerable の `Single_predicate`: `[LinqIntercept(false)]` の `srcOne` のコレクション式 `[FromInt(0)]` が、呼び出し元へ展開されるときに `Enumerable.Repeat(…,1)` へ書き換わる。
- 射影融合の `SelectMany_collection` 系4行: ラムダ内の `Enumerable.Repeat(x,1)` が、外側の要素ごとに System.Linq の反復子を作る。

そのほかの形(理想版との比較は理想版の作成後に行う):

- 配列・List・Range 起点で最内ループにインターフェース呼び出しがあるのは SelectMany 系(内側が IEnumerable)と ToLookup 系。
- ループ内の割り当ては AggregateBy と Chunk。ボックス化はループ内ゼロ。
- 計数器の呼び出しは置き換え済みの約半数の行にあり、うち25行はループ内。
