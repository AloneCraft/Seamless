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
- 前提は FLinq の `.Anchor` 契約と同じ(2026-10-02 改訂): `.Anchor` の無い式は副作用・例外なしとみなし、評価の省略・並べ替え・数式化(Range の偶数の個数、和の公式、`x%1000` の並びなど)をしてよい。`.Anchor` を書いた式だけ System.Linq と同じ回数で評価する。
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

同じビルドを2回測り、`Benchmark.Audit noise --first <1回目>... --second <2回目>... --md Work/ideal/noise.md` で行ごとの理想比 g=ln(FLinq/理想) とぶれ e を求めて分類する。e は比の周回間の差 |g1−g2| と、FLinq・理想それぞれの周回内のばらつき((四分位範囲/中央値)/2)の大きい方。実装ごとの所要時間は周回(プロセス)ごとに中央値で11%動くが、3者に共通の分が比で打ち消され、比の差は中央値5%に収まる。2回とも g&gt;e の行が改善対象で、3%未満は記録のみ、2回とも g&lt;-e の行は理想版の見直し対象。

結果の要約: [baseline-20261002.md](baseline-20261002.md)(初回)、[baseline-20261003.md](baseline-20261003.md)(`.Anchor` 契約に合わせた理想版と修正後の取り直し。全803行の FLinq÷理想 の幾何平均 1.597→1.296)。

## 生成器の修正の前後比較

1. 修正前の Headless 出力(`Benchmark/Benchmark.Headless/bin/Release/.../win-x64`)を `Work/ab/before-<名前>` へ複製してから生成器を直し、作り直した出力を `Work/ab/after-<名前>` へ複製する。
2. `Benchmark.Audit ildiff --before <修正前の FLinq/Optimization.All の dll>... --after <修正後の同じ dll>...` で、置き換え後の IL(辿った補助メソッドを含む。トークンは参照先の名前に置き換えて比べる)が変わったベンチを列挙する。
3. 変わった行を含むタブを、修正前と修正後で交互に `--multi-run` で測る(`Work/ab/run_ab.sh`)。理想版は前後で同じなので物差しにし、行ごとに g=ln(FLinq/理想) の変化を見る。
4. **IL が変わらなかった行の変化はすべてぶれ**なので、その分布(2026-10-02 の測定では2周ずつで中央値6%・90パーセンタイル21%)より大きい変化だけを効果とみなす。2周ずつでは3%の改善は判定できないので、効果を主張するには周回を増やす。

## UI と DB(Benchmark.db 版27)

- 最適化4タブとメモリ上4タブは「左・右・理想」の3列になる。理想列は常に最後に置き、ループ回数は列の並びに関係なく右(ZLinq/最適化無効)で決める。
- この8タブでは割合の帯の代わりに「左÷理想」「右÷理想」の時間倍率を出す(1.00x 以下は青、それより遅いと赤、欠測は「—」)。Persisted/Tsavorite の4タブは2列と割合の帯のまま。
- 結果確認(OK/NG)は、理想列の結果があれば3列のダイジェストがすべて一致したときだけ OK。
- DB は版27で、8タブの表の末尾に `理想_*` の8列があり、射影融合の表が加わった。比率ビューには `<左>÷理想` と `<右>÷理想` の列がある。
- UI は版や形状の合わない DB をバックアップして作り直すので、版26の DB を残すには新しい UI を起動する前に移行する。移行後に古いビルドの UI を起動すると逆に作り直されるので起動しない。

```
python docs/db-migrations/20261002-benchmark-db-v27.py            # Work/UI/Benchmark.db を移行(Work/UI/backup/ へ複製してから)
dotnet exec <Benchmark.Headless.dll> --verify-db-schema            # 版と形状の一致だけを調べる(DB は変えない)
dotnet exec <Benchmark.Headless.dll> --import-jsonl Work/ideal/baseline-1/*.jsonl   # --multi-run の結果を1つの実行として保存
```

`--import-jsonl` は FLinq→左、比較相手→右、理想→理想の列へ入れ、ダイジェストが一致しなかった行は3列とも失敗として残す。同じ行を2回含めると後の行で上書きされるので、1回の取り込みには1周ぶんだけを渡す。

## 結果の誤り(理想版との比較で発見)

- (修正済み 2026-10-02)`src.Order().Where(p).Consume(c)` など、配列・List 起点で並べ替えの後ろに段が続く連鎖: ソースが最初から昇順のとき、並べ替えを省く直接経路が後続の `Where`/`Select` を適用せず全件を流していた。昇順でないときはソース式を2回評価していた。直接経路を並べ替えだけの連鎖に限り、昇順でないときは取得済みのソース列挙子を並べ替えの列挙子へ引き渡すよう直した。回帰テストは `Test/SourceGenerator/SortedOrderDirectConsumeTests.cs`。
- (修正済み 2026-10-03)`Enumerable.Range(1,n).Count(x => x>=F())` のように、Range の比較述語の右辺に巻き上げ対象の呼び出しがあると、範囲の形から件数・真偽を出す近道(`RangePredicateOptimizer`)が巻き上げた変数 `__hoist*` を参照するのに宣言を出さず、生成コードがコンパイルできなかった。近道の5か所で巻き上げの宣言を値の式の前に出すよう直した。回帰テストは `Test/SourceGenerator/AnchorShortcutTests.cs`。
- (修正済み 2026-10-03)`.Anchor` を書いた述語・連鎖でも、Range の述語の近道(All/Any/Count)と、Select の後の Count を列挙せずに出す近道が使われ、評価の回数が System.Linq と変わっていた。`.Anchor` を含むときは近道を使わない。`.Anchor` は生成コードでは消えるので、ソースの根を作るときに構文から記録する(`Fragment.SourceContainsAnchor`・`TreeContainsAnchor`)。
- (未修正)ソースを返す補助メソッドに `[LinqIntercept(false)]` が無く、その本体が生成コードへ展開されるとき、本体中の静的フィールドへの修飾なし参照(`SourceCalls++`)が CS0103、型名で修飾しても CS0117 になる例があった(上のテストを作る途中で発見。補助メソッドに `[LinqIntercept(false)]` を付けると起きない)。

テストの書き方の注意: FLinq は `[LinqIntercept(true)]` のメソッドの**呼び出し箇所**を本体展開版へ差し替える。反射でそのメソッドを直接呼ぶと差し替わらず、`static class` の中でも差し替わらない。生成コードを実行して確かめるテストは、公開の入口メソッドから属性付きメソッドを呼び、差し替えが生成されたこと(`InterceptsLocationAttribute(1,` の数)も確かめる。

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
