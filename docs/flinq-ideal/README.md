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

理想版との比較(`compare`)は、FLinq と理想版の同じ行を突き合わせ、ループ内のインターフェース呼び出し・デリゲート呼び出し・割り当て・ボックス化・計数器・未使用補助型を並べた表を出す(理想版は `X_Impl` から同じアセンブリの Shared.Linq 配下を辿って数える)。

```
dotnet Benchmark/Benchmark.Audit/bin/Release/net10.0/Benchmark.Audit.dll compare --flinq <FLinq のベンチ本体.dll>... --ideal <理想版.dll>... --md Work/audit/compare.md
```

配列・List・Range 起点では、ループ内のインターフェース呼び出し・デリゲート呼び出し・ボックス化が理想版以下であることもテストする。IEnumerable 起点は実行時の型判定で span 経路と列挙子経路を両方持つため、静的な数では比べず計測で判断する。

既知の違反は `KnownExceptions.cs` に理由付きで載せる。直したら必ず消す(違反していない項目が残るとテストが失敗する)。

## 手書きの理想ループ版

- `Benchmark.System.Linq.Enumerable.Ideal`(配列)・`.List.Ideal`・`.IEnumerable.Ideal`・`.Range.Ideal`・`Benchmark.Optimization.Ideal`(複合連鎖)。書き方は `理想版の記述規則.md`。
- `Benchmark.Audit.Tests` で、結果ダイジェストが比較相手(単一演算子は ZLinq、複合連鎖は全最適化版)と一致すること、LINQ 実装を呼ばないこと、定数の写しがずれていないことを検査する。

## 3者計測(FLinq・比較相手・理想)

`Benchmark.Headless --multi-run` は、タブの行ごとに FLinq・比較相手(較正役。単一演算子は ZLinq、複合連鎖は最適化無効版)・理想版を、Worker の `multi-fast-run` で同じプロセスの中で交互に計測し、1行1ケースの JSONL へ書く。

- 先に3者の結果ダイジェストを照合し、一致しなければ計測しない。
- JIT 最適化待機(最低1000ms・JIT コンパイル数が200ms静穏・呼出速度のドリフト15%以内)を3者交互に済ませてから測るので、コンパイル時間は含まない。
- ループ回数は較正役だけで決め(1サンプル約10ms)、9ラウンドの順番を回転させて各実装が各位置に均等に入るようにする。
- Worker は型ごとに起動し直す。性能コア1個に固定して1並列で回す(`--logical-cpu`、既定14)。

```
dotnet exec Benchmark/Benchmark.Headless/bin/Release/net10.0-windows10.0.19041/win-x64/Benchmark.Headless.dll --run-tab System.Array --multi-run --out Work/ideal/run1.jsonl [--type Int32] [--method Where_predicate] [--seed 1|none] [--logical-cpu 14]
```

同じビルドを2回測り、`Benchmark.Audit noise --first <1回目>... --second <2回目>... --md Work/ideal/noise.md` で行ごとのぶれ e と理想比 g=ln(FLinq/理想) を求めて分類する。2回とも g&gt;e の行が改善対象で、3%未満は記録のみ、2回とも g&lt;-e の行は理想版の見直し対象。

## 結果の誤り(理想版との比較で発見)

- 複合連鎖 `srcCount().Order().Where(p)`(配列・List 起点): ソースが最初から昇順のとき、並べ替えを省く高速経路が後続の `Where` を適用せず全件を流す。高速経路に入れないときはソース式を2回評価する。シャッフルされたソースでは正しい。

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

- 配列・List・Range 起点で最内ループにインターフェース呼び出しがあるのは SelectMany 系(内側が IEnumerable。理想版も同じ)と ToLookup 系。ToLookup は結果を ILookup 型で受けて foreach するため、理想版より多い。
- ループ内の割り当ては AggregateBy と Chunk。ボックス化はループ内ゼロ。
- 計数器の呼び出しは置き換え済みの約半数の行にあり、うち25行はループ内。
