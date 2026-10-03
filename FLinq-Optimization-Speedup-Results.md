# FLinq 最適化前後の速度変化一覧

作成日: 2026-08-11  
対象: `G:\GitHub\Seamless` の現行作業ツリー  
単位: 1回あたりの平均経過時間(ms)。速度倍率は `最適化前 / 最適化後`、改善率は `1 - 最適化後 / 最適化前`。1.00x未満は遅くなったことを示す。

## 1. 同一条件に近い最適化前後

「最適化前」は `*-attr-noopt-1000.csv`（生成ヘルパーの最適化属性を無効化した状態）、「最適化後」は現行生成物の `*-current-1000.csv`。いずれもRelease、ウォームアップ10回、反復1000回。

| 系列 | メソッド | 型 | 前(ms) | 後(ms) | 変化率 | 速度倍率 | 判定 |
|---|---|---|---:|---:|---:|---:|---|
| SequenceEqual | `SequenceEqual_second` | ClassInt32 | 0.002329 | 0.001118 | **-52.0%** | **2.08x** | 改善 |
| SequenceEqual | `SequenceEqual_second_comparer` | ClassInt32 | 0.002336 | 0.000974 | **-58.3%** | **2.40x** | 改善 |
| SequenceEqual | `SequenceEqual_second` | int | 0.002018 | 0.001020 | **-49.5%** | **1.98x** | 改善 |
| SequenceEqual | `SequenceEqual_second_comparer` | int | 0.002074 | 0.000955 | **-54.0%** | **2.17x** | 改善 |
| SelectMany | `SelectMany_indexSelector` | ClassInt32 | 0.039411 | 0.037755 | -4.2% | 1.04x | 小幅改善 |
| SelectMany | `SelectMany_indexSelector_resultSelector` | ClassInt32 | 0.040523 | 0.037416 | -7.7% | 1.08x | 改善 |
| SelectMany | `SelectMany_resultSelector` | ClassInt32 | 0.041490 | 0.036493 | **-12.0%** | **1.14x** | 改善 |
| SelectMany | `SelectMany_selector` | ClassInt32 | 0.036709 | 0.041694 | **+13.6%** | **0.88x** | 悪化・要再測定 |
| SelectMany | `SelectMany_indexSelector` | int | 0.036766 | 0.035270 | -4.1% | 1.04x | 小幅改善 |
| SelectMany | `SelectMany_indexSelector_resultSelector` | int | 0.035693 | 0.034660 | -2.9% | 1.03x | 測定誤差範囲 |
| SelectMany | `SelectMany_resultSelector` | int | 0.035893 | 0.033134 | -7.7% | 1.08x | 改善 |
| SelectMany | `SelectMany_selector` | int | 0.035053 | 0.038420 | **+9.6%** | **0.91x** | 悪化・要再測定 |
| Repeat | `Repeat` | ClassInt32 | 0.094107 | 0.097243 | +3.3% | 0.97x | 測定誤差範囲 |
| Repeat | `Repeat` | int | 0.030867 | 0.032064 | +3.9% | 0.96x | 測定誤差範囲 |
| GroupBy | `GroupBy_elementSelector` | Range int | 0.091534 | 0.086144 | -5.9% | 1.06x | 小幅改善 |
| GroupBy | `GroupBy_keySelector` | Range int | 0.089761 | 0.088606 | -1.3% | 1.01x | ほぼ同等 |
| GroupBy | `GroupBy_resultSelector` | Range int | 0.082465 | 0.080868 | -1.9% | 1.02x | ほぼ同等 |
| ToLookup | `ToLookup_elementSelector` | Range int | 0.098888 | 0.098188 | -0.7% | 1.01x | ほぼ同等 |
| ToLookup | `ToLookup_keySelector` | Range int | 0.097554 | 0.078124 | **-19.9%** | **1.25x** | 改善 |

## 2. 初期実装から現行実装までの参考値

以下は最初のベースラインから現行版までの変化。初期版と現行版でウォームアップ条件・JIT状態が完全一致しないため、実装効果の方向を示す参考値として扱う。

| メソッド | 型 | 初期(ms) | 現行(ms) | 変化率 | 速度倍率 |
|---|---|---:|---:|---:|---:|
| `Repeat` | ClassInt32 | 0.236715 | 0.097243 | **-58.9%** | **2.43x** |
| `Repeat` | int | 0.187129 | 0.032064 | **-82.9%** | **5.83x** |
| `SequenceEqual_second` | ClassInt32 | 0.033300 | 0.001118 | **-96.6%** | **29.79x** |
| `SequenceEqual_second_comparer` | ClassInt32 | 0.024333 | 0.000974 | **-96.0%** | **24.98x** |

## 3. 最終Range候補の基準比較

最終版のFLinqとZLinqを、要素数100,000、Release、TieredCompilation/ReadyToRun無効、ウォームアップ50回、反復5000回（ExceptByのみ独立5回中央値）で比較した値。

| メソッド | FLinq(ms) | ZLinq(ms) | FLinq/ZLinq | FLinq速度 |
|---|---:|---:|---:|---:|
| `Any(predicate)` | 0.000485 | 0.004294 | 0.1129 | 8.85x |
| `Chunk` | 0.068929 | 0.124757 | 0.5525 | 1.81x |
| `ExceptBy(keySelector)` | 0.092140 | 1.148983 | 0.0802 | 12.47x |
| `IntersectBy(keySelector)` | 0.089334 | 0.940579 | 0.0950 | 10.53x |

## 4. 結論

- 安定して効果が確認できた最大の変更は `SequenceEqual` の直接Span経路・同一領域短絡で、同一条件比較では **約49.5〜58.3%短縮**。
- `ToLookup_keySelector` は **19.9%短縮**、Range `ExceptBy` は区間分割後の最終測定でZLinqより **12.47倍高速**になった。
- SelectManyは大半のバリエーションで2.9〜12.0%改善した一方、`SelectMany_selector` は今回の単一測定では9.6〜13.6%悪化したため、最適化成功とは扱わず要再測定とした。
- Repeat・GroupBy・ToLookupの一部は1〜4%程度で、Runner同期・JIT・測定揺らぎの範囲。大幅改善とは主張しない。
- 根拠CSVは `artifacts/flinq-sequenceequal-attr-noopt-1000.csv`、`artifacts/flinq-sequenceequal-current-1000.csv`、`artifacts/flinq-selectmany-attr-noopt-1000.csv`、`artifacts/flinq-selectmany-current-1000.csv`、`artifacts/flinq-repeat-attr-noopt-1000.csv`、`artifacts/flinq-repeat-current-1000.csv`、Range/GroupBy/ToLookup各CSV、および `artifacts/range-final2-*-exceptby-1..5.csv`。
## 実装中: SelectMany の既知 List<T> 内側列（2026-08-11）

| 候補 | 静的型/生成経路 | boxing / callvirt | 割当量 | 前後中央値 | 判定 | 根拠 |
|---|---|---|---:|---:|---|---|
| List 添字直接走査 | 実行時 `List<T>` 判別 | 除去可能 | 未測定 | 未測定 | **不採用** | `List<T>.Enumerator` のversion検査を迂回するため、列挙中変更時の例外契約を維持できない。 |
| List 構造体 Enumerator | Pipeline `SelectMany` のselector戻り型が静的に `List<T>` | `IEnumerator<T>` boxing/interface dispatchを除去。`MoveNext` は構造体呼出し | 実測待ち | 実測待ち | **不採用** | コレクション初期化式を返すselectorで既存lambda hoistと衝突し、生成コードがコンパイル不能になるため撤回。 |

- 生成形状候補・候補用テストはいずれも撤回済み。現行生成物にはこのList専用経路を含めない。
- 実測は実施しない。互換性・生成互換性のいずれかを満たせない候補は、速度値にかかわらず採用しない。
## 10. 継続調査: SelectMany候補の撤回とPersisted ToLookup再測定（2026-08-11）

- `List<T>` 内側列の構造体Enumerator候補は、最終的に生成コードの互換性を満たせず**不採用**とした。候補専用の生成形状テストも成果物に残していない。したがって、本書の「実装中: SelectMany の既知 List<T> 内側列」にある「実測待ち」表は撤回済み候補として扱い、採用済みの最適化ではない。
- 理由は二段階で確認した。(1) 実行時のList添字走査は列挙中変更時のversion検査を失うため意味契約に反する。(2) 静的`List<T>.Enumerator`はその契約を守るが、`new List<T> { ... }`を返すselectorで既存lambda hoistと衝突して生成コードがコンパイルできない。未知`IEnumerable<T>`フォールバックは変更していない。
- `Benchmark/Worker` のfast-run診断は、引数なしメソッドを一度だけdelegate束縛する既存方式を維持したまま、各測定サンプルのGen0/Gen1/Gen2回数を`fast-worker.log`へ出すようにした。標準出力のJSONプロトコルには診断文字列を出さない。

### Persisted `ToLookup_keySelector` — 同一Worker 3回中央値

条件: Release、要素数4096、各runは独立Worker、開放ループ較正300ms、破棄2+計測7サンプル。Consumerはベンチ本体契約どおり含む。成果物は`artifacts/manual-worker/persisted-tolookup/`。

| run | FLinq ns/回 | LINQ ns/回 | FLinq/LINQ | FLinq 割当/回 | LINQ 割当/回 | GC（各計測サンプル） |
|---:|---:|---:|---:|---:|---:|---|
| 1 | 4,602,600 | 5,754,100 | 0.800 | 320,968 B | 313,040 B | FLinq 1/0/0, LINQ 2/1/0 |
| 2 | 4,872,500 | 4,431,600 | 1.099 | 320,968 B | 313,040 B | FLinq 1/0/0, LINQ 1/0/0 |
| 3 | 4,395,800 | 6,454,500 | 0.681 | 320,968 B | 313,040 B | FLinq 1/0/0, LINQ 2/1/0 |
| **中央値** | **4,602,600** | **5,754,100** | **0.800** | **320,968 B** | **313,040 B** | — |

判定: **採用変更なし（目標達成）**。中央値でFLinqはSystem LINQより20.0%速く、1.02条件を満たす。FLinq側に約2.5%の割当差はあるが、Persistedストレージ走査とLookup本体の支配コストを伴う負荷であり、公開APIを維持したままのProvider専用ToLookup lower化は、この比較に対して必要性を示さなかった。揺らぎのある各単発値でなく中央値を採否根拠にした。
## ヘッドレスWorker検証: Array AggregateBy_keySelector（2026-08-11）

既存GUIのプロセス寿命に依存せず、`Benchmark.Headless` が既存高速Worker・4096要素・CPU 0で、左右の結果を`Work/UI/Benchmark.db`の既存スナップショット形式へ保存した。各回は独立プロセスで、実行IDは32、33、34。

| 実行ID | FLinq ns/回 | ZLinq ns/回 | 比率 |
|---:|---:|---:|---:|
| 32 | 147,331.11 | 112,982.96 | 1.304 |
| 33 | 138,930.37 | 190,508.15 | 0.729 |
| 34 | 159,029.23 | 162,536.15 | 0.978 |
| **中央値** | **147,331.11** | **162,536.15** | **0.906** |

判定: **追加最適化なし**。中央値は1.02以下であり、単発の逆転を根拠にコードサイズ・分岐を増やす変更は採用しない。各実行はDBへ`Completed`として保存し、ヘッドレス経路の結果形式・Worker分離・保存契約も確認済み。
## 継続計測: 現行Release再ビルド後のArray全件（2026-08-11）

- `Benchmark/Worker` の全依存を `Release -t:Rebuild` で再生成後、既存Fast Worker・CPU 0・4096要素・独立3回で測定した。旧成果物での実行ID 35–37 は履歴として残し、完了判定から除外する。有効な全件runは **38, 39, 40**（各194比較・388ケース、失敗0）。
- 中央値比率が `FLinq / ZLinq > 1.02` の行は **48/194**。割当量はWorkerからDBスナップショットへ保存済み。上位は次のとおり。

| 型 / メソッド | FLinq ns/回 | ZLinq ns/回 | 中央値比 | FLinq/ZLinq 割当B/回 | 生成形状・原因仮説 |
|---|---:|---:|---:|---:|---|
| ClassInt32 `SequenceEqual_second` | 15,422.18 | 3,055.09 | 5.587 | 0 / 64 | `T`のまま`MemoryExtensions.SequenceEqual`。box/IEnumeratorはないが参照型Comparer経路が支配的。 |
| Int32 `DistinctBy_keySelector` | 71,917.78 | 16,953.44 | 4.212 | 56 / 56 | span直ループだが`Base<T>.Mod`とHashSetSlim addが支配候補。 |
| Int32 `IntersectBy_keySelector` | 56,855.21 | 15,221.78 | 3.707 | 56 / 56 | key selector / set構築・比較経路を要プロファイル。 |
| Int32 `MinBy_keySelector` | 18,358.03 | 5,235.89 | 3.540 | 0 / 0 | `Comparer<int>.Default.Compare`と`Base<T>.Mod`がホットループに残る。 |
| ClassInt32 `SelectMany_indexSelector_resultSelector` | 37,192.63 | 9,908.26 | 3.472 | 2,736 / 2,736 | 未知`IEnumerable<int>` fallbackの`IEnumerator<T>`・`MoveNext`/Disposeが残る。 |

### 不採用: SequenceEqualの参照型runtime分岐

`ClassInt32 SequenceEqual_second`に対し、生成時型パラメータ`T`で参照型だけ直接Comparerループへ切り替える2案を実測した。意味・生成形状テストは合格したが、同じArrayソース群のInt32で重大な回帰となったため、最終ソースには残していない。

| 候補 | 実行ID | Int32 中央値比 | 判定 |
|---|---|---:|---|
| `default(T) is null` 分岐 | 45–47 | 1.321 | 不採用 |
| `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` 分岐 | 48–50 | 1.368 | 不採用 |

採否理由: 参照型の改善だけでは「同一ソース群の全比較が1.02以内」の条件を満たせない。公開API・例外/Dispose契約は変えず、元の直接Span経路へ復帰した。
## 採用: HashSetSlim.Add のインライン化（2026-08-11）

CPUサンプル（`artifacts/profiles/array-distinctby-hot/worker.nettrace`）で、`DistinctBy_keySelector` のFLinqホットループは `HashSetSlim<int>.Add` が排他13.4%、`InternalGetHashCode` が2.46%を占めた。そこで`HashSetSlim<T>.Add`へ`AggressiveInlining`を付与した。集合演算・生成形状テスト4件は合格。

| 実行群 | Int32 DistinctBy FLinq ns/回 | ZLinq ns/回 | 中央値比 | 割当B/回 |
|---|---:|---:|---:|---:|
| 変更前 Array全件 38–40 | 71,917.78 | 16,953.44 | 4.212 | 56 / 56 |
| 変更後 Array全件 57–59 | 39,507.06 | 39,136.81 | 1.009 | 56 / 56 |

判定: **採用**。FLinq側は約45.1%短縮、割当量・公開API・Dispose/例外契約は不変。Array全件で閾値超過は48件から40件へ減少した。ZLinq側の絶対値はWorker全体のJIT/PGO状態により変動するため、採否は同一の全件3回中央値とFLinq自身の前後差を併記している。

## 2026-08-11 — 同一Worker交互計測と較正非対称の是正

- 追加: `Benchmark.Headless --operator-harness --run-tab <tab> --method <method> --type <type>`。
  - 同じWorkerプロセスで、型解決・delegate生成後の `Action` を9標本で交互順序実行する。
  - 時間、スレッド割当量、Gen0/1/2を計測する。DBには書き込まない診断用経路である。
- 原因確定: 既存高速DB経路は右端の基準ケースを開放ループ（300ms）で較正してから、左側FLinqを同一ループ回数で測っていた。較正ケースのみがTiered JIT/PGOの長時間実行を先に得るため、演算子本体以外の差を比較値へ混入させていた。
- 是正: `fast-run` の同期ケースへ、較正側と同じ閾値の計測外直接ウォームアップを追加した。計測標本数、ループ回数、公開APIは維持する。

| 演算子・型 | 同一Worker FLinq ns | 同一Worker 基準 ns | 比率 | 割当 (双方) | 判定 |
| --- | ---: | ---: | ---: | ---: | --- |
| `SelectMany_indexSelector_resultSelector` / Int32 | 43,091 | 165,830 | 0.260 | 2,737 B | 本体はFLinq優位 |
| `SelectMany_indexSelector_resultSelector` / ClassInt32 | 42,439 | 161,403 | 0.263 | 2,737 B | 本体はFLinq優位 |
| `MaxBy_keySelector` / Int32 | 27,461 | 33,433 | 0.821 | 2.2 B | 本体はFLinq優位 |

対称ウォームアップ後の通常DB（独立3回、run 61–63）では、`SelectMany_indexSelector_resultSelector` は Int32 が `0.791`（13,378 / 15,184 ns、2,736 B）へ改善した一方、ClassInt32 は `1.583`（17,620 / 11,503 ns、2,736 B）だった。同一Worker結果と符号まで矛盾するため、ClassInt32のこのDB行を演算子回帰の証拠には使用しない。次段階は、補助ハーネスの交互ペア結果を同じ比較スナップショット形式で保存することである。


## 2026-08-11 — Zip_tuple 本体計測

- 同一Worker・交互9標本・固定16回ウォームアップ: Int32 Zip_tuple は FLinq 17,632 ns、ZLinq 14,211 ns、比率 1.241。割当は双方約64 B。
- 結論: DB run 72–74 の4倍超は計測条件の増幅を含むが、1.02超の演算子本体差は再現した。
- 生成コード: Zip.cs は2つの構造体Enumeratorへ TryGetNext を行いTupleをCurrentへ保持する。boxing・IEnumerableフォールバックは観測されない。次候補はArray/Spanが静的に分かるZipの直接ループ化であり、未知IEnumerableのフォールバックは維持する。


## 2026-08-11 — Zip tuple Array/Span直接Consume（採用）

- 変更: Zip fragmentが両入力を静的Spanとして取得できる場合、短い方の長さまでindex走査し、Tupleを直接Consumerへ渡す。未知IEnumerable<T>・片側のみSpanの経路は既存IValueEnumerator<T>.TryGetNextへフォールバックする。
- 契約: 短い方で停止し、要素順序を維持し、両EnumeratorをusingでDisposeする。
- テスト: dotnet test Test/Test.csproj -c Release --no-restore --filter FullyQualifiedName~Zip は13件成功。
- 同一Worker診断: 17,632 / 14,211 ns（1.241）→13,880 / 17,699 ns（0.784）。
- DB 3回: run 75–77 の中央値は FLinq 12,262 ns、ZLinq 14,502 ns、比率0.833、割当64B / 64B。採用。

## 2026-08-11 — 現行Release全DB比較（独立3回中央値）

`Work/UI/Benchmark.db` は計測前に `Work/UI/Benchmark.db.pre-flinq-full-run-postcontains-20260811-201043.bak` へ明示バックアップした。バックアップSHA-256は `A18CDBAD455EC2C14C6345790F73C8AA64067657F864080AC939E15304B6B1FD`、計測後DBは `582CD97C6859BE1EFFACBFC56DBE14B523788FA8DA0DD31E4D2D533C7B684AD7` で、計測結果だけが追加されている。

条件はUIの高速プロファイル、要素数一覧 `[1]`、計測時間 `1,000,000 ns`、論理CPU `0`。各回はUI/Workerを再起動した独立プロセスで、全2,806行を選択した。成果物ルートは実行ID41=`24da2f4123f642fb9e36e122991d39de`、56=`37564811350e4d43bae7ecf9790f84a5`、60=`4aabad2a4d84426d9407916afb8a0691`。8比較表は各回とも全行 `Completed`（失敗0）だった。

### 8比較表

比率は各実行内の表中央値、最後の列は3実行の中央値（`FLinq / 基準`）。割当は3回の表中央値（FLinq/基準、B/回）。

| 比較表 | run41 | run56 | run60 | 3回中央値 | 行数/回 | 割当中央値B/回 | 判定 |
|---|---:|---:|---:|---:|---:|---:|---|
| Array | 0.619 | 0.695 | 0.661 | **0.661** | 194 | 64/64 | 完了 |
| IEnumerable | 0.865 | 0.876 | 0.898 | **0.876** | 194 | 232/248 | 完了 |
| List | 0.739 | 0.691 | 0.739 | **0.739** | 194 | 64/64 | 完了 |
| Range | 0.684 | 0.636 | 0.662 | **0.662** | 81 | 0/0 | 完了 |
| Persisted(Seamless) | 0.920 | 0.933 | 0.929 | **0.929** | 130 | 196,664/229,512 | 完了 |
| Persisted(System) | 1.000 | 0.971 | 1.034 | **1.000** | 130 | 196,760/229,424 | 完了 |
| Tsavorite(Seamless) | 0.976 | 0.963 | 0.969 | **0.969** | 130 | 99,448/100,632 | 完了 |
| Tsavorite(System) | 0.994 | 0.991 | 1.000 | **0.994** | 130 | 100,480/100,480 | 完了 |

全8表の3回中央値はすべて `<=1.02`。Persisted(System)のrun60単発1.034は、run41/56を含む中央値では1.000となるため、単発値だけで特殊化を追加しない。

### 最適化効果3表

| 効果表 | run41 | run56 | run60 | 3回中央値 | 行数/回 | 判定 |
|---|---:|---:|---:|---:|---:|---|
| 書き換え（全最適化/無効） | 0.672 | 0.694 | 0.840 | **0.694** | 11 | 完了 |
| パイプライン融合（全最適化/無効） | 0.972 | 0.860 | 0.924 | **0.924** | 11 | 完了 |
| 述語プッシュダウン（全最適化/無効） | 0.876 | 0.901 | 0.876 | **0.876** | 143 | 完了 |

### 行単位の追加候補

8表の各行を3回突合し、行中央値が1.02を超えた297件を [FLinq-Optimization-Candidate-Backlog.md](FLinq-Optimization-Candidate-Backlog.md) に比率降順で保存した。上位には `Persisted(System) Contains_value` のようにストレージ/API経路またはベンチマーク入力の支配が疑われる行、`SelectMany` の未知 `IEnumerable<T>` fallback、`Zip` の2列同期、`Order/GroupBy/ToLookup` のComparer・hash構築が含まれる。候補表の値だけで公開APIを変更せず、各候補はCPU/割当プロファイル→最小変更→意味/Dispose回帰→3回中央値の順で採否を判断する。

### Contains_value の失敗修正

Persisted/Tsavoriteの `Contains_value_Impl` は `Set<T>.Contains` の公開インスタンスAPIで完結するにもかかわらず、`AsX<T>()` 経由の `System.Linq.Enumerable` 転送を生成し、Forwarding disabled例外になっていた。`Shared/Linq.Enumerable/Base.cs` で `PERSISTED || TSAVORITE` 時だけ `[LinqIntercept(false)]` とし、ネイティブContainsを直接測る経路へ戻した。公開APIは変更せず、今回の3実行では該当4行を含め全行が `Completed`。この変更はFLinq演算子の特殊化ではなく、誤った生成転送を除くベンチマーク形状修正として採用した。

Workerは各サンプルの割当量とGen0/1/2収集回数を `fast-worker.log` に記録し、標準出力JSONの契約を維持した。`FLinq-Optimization-Candidate-Backlog.md` の候補は、単発の外れ値を最適化成功と誤認しないための監査対象である。
## 2026-08-11: IEnumerable SequenceEqual の同一型直接比較候補（不採用）

- 対象: `SequenceEqual_second` / `SequenceEqual_second_comparer`、`System.Collections.Generic.IEnumerable<>`、4096 要素、同一 Worker・CPU 0・ペア交互測定。
- 生成コード確認: 非 comparer 版は要素型が同一のジェネリック `T` でも、非ベクター分岐で `(T)(object)__mn52` を経由していた。候補では同一型を直接 `EqualityComparer<T>.Default.Equals` へ渡した。
- 契約検証: `dotnet test Test.System.Linq.Enumerable.FLinq/Test.System.Linq.Enumerable.FLinq.csproj -c Release --no-restore --filter "FullyQualifiedName~SequenceEqual"` は 4/4 成功。全体テストは無関係な `Benchmark.Optimization.All.dll` の `VBCSCompiler` ロックで未実行。
- 演算子本体ハーネス（Int32 独立3回）: FLinq/Baseline は 76,857/79,888 ns、83,088/83,930 ns、77,234/78,965 ns（中央値比 0.978）、割当量は双方およそ 384 B。
- DB 前（run 78--80）中央値: Int32 `SequenceEqual_second` 58,601/44,645 ns（1.313）、ClassInt32 140,715/102,002 ns（1.380）。
- DB 後（run 81--83）中央値: Int32 53,880/35,508 ns（1.517）、ClassInt32 135,965/83,145 ns（1.635）。割当量は Int32 384/384 B、ClassInt32 400/400 B。
- 判定: **不採用・復元済み**。ハーネスの局所値は改善したが、全件3回中央値では 1.02 を満たさず、基準側の JIT/PGO 変動を含むため 3%以上の再現可能な優位を立証できなかった。支配コストは未知 `IEnumerable<T>` の `IEnumerator<T>.MoveNext`/`Current` interface dispatch であり、単なる comparer 型消去除去では不足する。
## 2026-08-11: IEnumerable Reverse direct-Consume candidate (rejected)

- Change: generated a direct ArrayPool-buffering Consume loop for non-span `Reverse`, preserving source `using`, ArrayPool return in `finally`, and consumer exception cleanup.
- Semantics: `dotnet test Test.System.Linq.Enumerable.FLinq/Test.System.Linq.Enumerable.FLinq.csproj -c Release --no-restore --filter "FullyQualifiedName~Reverse"` passed 2/2.
- Operator harness, Int32, independent runs: FLinq/Baseline = 82,459/58,685 ns (1.405), 79,472/55,593 ns (1.429), 95,761/59,537 ns (1.609); allocations 224/256 B.
- Decision: rejected and restored. Removing the generated Reverse enumerator state machine made the hot loop slower; existing struct enumerator/JIT shape is superior. The allocation reduction is insufficient to offset CPU regression.
## 2026-08-11: Range Zip tuple direct-loop candidate (rejected)

- Candidate: when both inputs are RangeRoot, lower Zip tuple to `min(count)` arithmetic loop.
- Tests: Zip filter passed 4/4.
- Operator harness, Int32, independent runs: FLinq/Baseline = 25,321/19,312 ns (1.311), 24,377/19,614 ns (1.243), 30,067/24,175 ns (1.244); allocation is effectively zero on both sides.
- Decision: rejected and restored. Range struct enumerators are already JIT-friendly; arithmetic direct lowering increased code shape cost without reducing allocation.
## 2026-08-11: Array Zip_tuple — ZLinq との差分に基づく lowering 修正（測定待ち）

- UI run 84 の旧コード実測は FLinq 11,610.256 ns / ZLinq 2,284.615 ns、比率 5.081。割当は 0 B / 0 B。
- ZLinq の Route は `T[] -> FromArray<T> -> Zip` の具体値列挙型を保つ。一方、旧 FLinq の生成コードは二つの配列Rootを持ちながら、`Zip.Struct0.TryGetNext` を終端の `while` から呼び、各要素で二重の状態更新と tuple Current 更新を行っていた。
- 根因は二つ: (1) `AsEnumerable` が direct Span expression を転送しない、(2) Zip だけのパイプラインにも `PipelineEnumerable` を作り、Zip 固有の direct Consume lowering を隠す。
- 修正: `AsEnumerable.GetDirectSpanExpression` を子へ転送。stage が 0 かつ終端がない場合、`PipelineBuilder` は不要な `PipelineEnumerable` を作らず元の Zip fragment を返す。Zip は `TryGetSpan` 成功時に `min(length)` の直接 for ループ、失敗時に同じ struct Enumerator による安全な `TryGetNext` fallback を生成する。
- 生成コード検証: `artifacts/generated-zip-lowering/...Base_cs.g.cs` で、Array Zip_tuple は `__zipSource.TryGetSpan` / `__zipSecond.TryGetSpan` 後の直接 `for` と `Consumer.Consume((span[i], span[i]))` へ lower され、終端ループから `Zip.Struct0.TryGetNext` が除去された。
- 意味検証: `dotnet test Test.System.Linq.Enumerable.FLinq/Test.System.Linq.Enumerable.FLinq.csproj -c Release --no-restore --filter Zip` は 4/4 成功。
- 採否: 保留。ユーザー起動中の UI/Worker (run 84) は停止・上書きせず、終了後に新しい Worker 条件で独立 3 回、中央値・割当・生成形状を再測定する。
## 2026-08-11: 直接 interface loop・透過 AsEnumerable（測定前）

- DBバックアップ: `Work/UI/Benchmark.db.pre-flinq-lowering-20260811-225802.bak`（SQLite online backup、SHA-256 `53872ef66c61e59fb860a27d41beb064f0593b9fb10f7d0b0481f4954c217631`）。
- 旧UI Worker: 調査開始時に `Benchmark.UI.exe` PID 98196 と子 `Worker.exe` PID 17744 を検出。利用者が終了した後に再確認し、いずれも未実行。以後のDB保存Runは新Workerとして分離する。
- 形状仮説: 静的 `IEnumerable<T>` root の `Consume` が、runtime Array/List 判定を含む `IValueEnumerator<T>` ラッパーへ落ちていた。利用者の指定どおり runtime 型回復はせず、`IEnumerator<T>` の直接 loop（1回の `GetEnumerator`、`MoveNext`/`Current`、`using` Dispose）へlowerする。
- 形状仮説: `Enumerable.AsEnumerable` は透過stageなのにPipelineに残り、stageが空でも `PipelineEnumerable` を生成していた。正規化で除去し、source能力を保持する。
- List: FLinq benchmark route の `AsX(List<T>)` 戻り値が `IEnumerable<T>` で、ZLinqの `FromList<T>` 相当より早期に静的型を消していた。route内部だけを `List<T>` に変更（公開API変更なし）。
- 測定値: 未測定。Releaseの形状・意味検証後、同一新Worker条件の独立3 RunをDBへ全保存して中央値を記録する。
### 検証結果（測定前）

- `PerformanceShapeTests.StaticIEnumerableConsumeUsesDirectInterfaceLoop`（Release）: 合格。再生成コードは `using var __interfaceEnumerator=...GetEnumerator(); while(...MoveNext()) { ...Current; Consume(...); }`。対象メソッドに `TryGetSpan` と `IValueEnumerator` はない。
- `PerformanceShapeTests.AsEnumerableTerminalKeepsDirectArrayCountShape`（Release）: 合格。`source.AsEnumerable().Count()` は `source.Length` を使用し、空Pipelineの `Compute/TryGetNext` を生成しない。
- `RedundantAsEnumerable`（Release、既存意味テスト）: 3/3合格。
- C-ListRoute-03: 却下。`AsX(List<T>) -> List<T>` は `List.Reverse()` などのインスタンスメソッドをLINQ拡張より優先させ、`void`連鎖等の意味・コンパイルを破壊する。公開APIを変えずに静的型を保持する安全な方法ではない。
### DB保存測定の保護停止（2026-08-11 23:15）

`Benchmark.UI.exe` PID 28248（23:04開始）と子 `Worker.exe` PID 97544（23:05開始、`Work/Benchmark/Fast/.unsaved/27cbc19668894fabbd5e9620137ec935`）を検出したため、IEnumerable保存Run 1/3 の起動を拒否した。旧PID 98196/17744とは別のUI実行である。`Benchmark.db`への書込みなし。
## 2026-08-12 05:40:20 +09:00 — C-SelectMany-04: 静的 IEnumerable の直接二重 interface loop（却下）

- 対象: SelectMany_selector / 終端 Consume。Array・IEnumerable・List の各 ClassInt32 / Int32。ソース式の静的型は IEnumerable<IEnumerable<int>>、実体はベンチマーク側の既知配列列。
- 生成形状（変更前）: SelectMany_WithInlinedInner が外側・内側の IValueEnumerator と TryGetNext 再開状態を生成し、内側配列を値 Enumerator/Span 経路で消費する。
- 試行形状: using var outer = ((IEnumerable<IEnumerable<T>>)source).GetEnumerator(); while (outer.MoveNext()) { var inner = selector(outer.Current); using var e = inner.GetEnumerator(); while (e.MoveNext()) consumer.Consume(e.Current); }。boxing は追加しないが IEnumerator<T>.MoveNext/Current の interface dispatch を必ず通り、内側配列の値 Enumerator/Span 能力を失う。
- 形状テスト: StaticIEnumerableSelectManyUsesDirectNestedInterfaceLoops は強制再生成で成功。既存 SelectMany 契約テスト 24件も成功。
- 測定条件: UIは未起動、CPU 0、UI高速オーケストレーター、軸1（4,096要素）、適応閾値1ms。Run 97 は経路試行のみで採否に使わない。Run 98–100=Array、101–103=IEnumerable、104–106=List。全Runを Benchmark.db に保存。
- 修正前基準: Run 94–96（UI高速、同じ軸・閾値・CPU）。
- 修正後中央値比率 FLinq/ZLinq: Array Class 約1.39（修正前約1.97）、IEnumerable Class 約4.10（修正前約2.08）、List Class 約4.69（修正前約2.16）。Int32 は Array約0.56、IEnumerable約0.46、List約0.47。
- 修正後Class中央値 ns（FLinq/ZLinq）: Array 9,786/7,025、IEnumerable 34,091/7,307、List 33,927/7,233。割当は前後とも概ね2,736 bytes/operationであり、劣化主因は割当ではなく interface dispatch と配列高速経路喪失。
- 採否: 却下。Class群で2%を大幅に超える回帰があり、全比較1.02目標にも未到達。ユーザー指定の「静的 IEnumerable は直接 interface loop」を実証したが、このベンチマークの静的interface型は実体配列を隠しているため、SelectManyではZLinq同等化に寄与しない。
- 次: C-SelectMany-04の生成器変更・専用形状テストを巻戻し、既存inlined値Enumerator経路を復帰する。直接interface root Consume の別候補とは切り分けて再評価する。
## 2026-08-12 — C-ZipTuple-05: Zip の単一演算子パイプライン直接 Consume（測定前）

- 対象: `Zip_tuple_Impl`、終端 `Consume`。初期対象は Array / List / IEnumerable の `Int32` と `ClassInt32`、要素型は `(T First,T Second)`。
- ソース型: Array route は `AsX<T>(T[]) -> T[]`、IEnumerable route は `AsX<T>(IEnumerable<T>) -> IEnumerable<T>`。ZLinq は既知配列を `FromArray<T>`、Listを `FromList<T>` の値列挙型として保持する。
- FLinq変更前形状: `Zip.Struct0<T,TEnum1,TEnum2>` が tuple `Current` を書き込み、終端は `using var __ce0; while(__ce0.TryGetNext(out tuple)) consumer.Consume(tuple);`。boxing・`IEnumerator<T>` dispatch はないが、tuple中間値と再開状態機械が残る。両rootの `TryGetSpan` 能力は Struct0 が `false` を返すため終端へ届かない。
- ZLinq形状: 二つの既知コレクション値Enumeratorを保持し、終端で最短長まで直接ループする。tuple Enumerator/Currentを経由しない。
- 原因: `PipelineBuilder.TryBuild` は単一演算子チェーンを許可リストで制限しており、`Zip` が未登録だった。このため `Zip.GetConsumeExecutionStatement`（二つの `TryGetSpan` を確認し `for`、それ以外は既存 `TryGetNext`）に到達せず旧fragment fallbackへ落ちていた。
- 最小修正: 単一演算子許可リストへ `Zip` を追加。公開API・未知 `IEnumerable<T>` fallback・Dispose/例外/空列/長さ不一致/Repeat列挙の契約は変更しない。
- 形状検証: `PerformanceShapeTests.ZipConsumeUsesDirectDualSpanLoop` を追加し、Array専用 `AsX<T>` overload と `IEnumerable<T>` overload を含めた強制generator実行で `__zipSource`/`__zipSecond`/`TryGetSpan`/最短長ループ、かつ `using var __ce` wrapper不在を確認。既存 Zip 契約を含むReleaseテスト8/8成功。
- inspection注意: Benchmark project の `obj/.../inspection` はMSBuild Rebuildで出力されない保存済み副産物であり、旧ファイルを `*.pre-zip-direct-20260812-055614.bak` へ退避した。以後この旧inspectionを結果として使わず、強制generatorテストと修正後Workerの実行DLLで判断する。
- delegate/comparer: Zip_tupleにはなし。GC/割当/実行時間・修正前後中央値は、同一Worker条件の独立3Run後に追記する。

### C-ZipTuple-05 採否（却下・2026-08-12）

- 修正後保存Run: Array=107–109、List=110–112、IEnumerable=113–115。すべて profile=`高速`、CPU 0、軸1（4,096要素）、適応閾値1ms、各run `Completed`。UIは未起動で、headlessの新Workerだけを使用した。
- 中央値（FLinq ns / ZLinq ns = 比率、割当B FLinq/ZLinq）:
  - Array ClassInt32: 16,733 / 32,967 = 0.508、64/0。基準94–96は3,400 / 32,950 = 0.103、0/0。FLinq実時間 +392.2%。
  - Array Int32: 12,223 / 17,433 = 0.701、0/64。基準11,324 / 2,451 = 4.620、0/0。FLinq実時間 +7.9%。
  - List ClassInt32: 15,333 / 36,900 = 0.416、64/0。基準4,200 / 30,875 = 0.136、0/0。FLinq実時間 +265.1%。
  - List Int32: 12,440 / 8,870 = 1.402、0/64。基準11,049 / 2,558 = 4.320、0/0。FLinq実時間 +12.6%。
  - IEnumerable ClassInt32: 72,533 / 42,400 = 1.711、400/336。基準36,150 / 53,800 = 0.672、336/336。FLinq実時間 +100.6%。
  - IEnumerable Int32: 35,600 / 19,680 = 1.809、320/320。基準25,000 / 28,175 = 0.887、320/320。FLinq実時間 +42.4%。
- GC: headless保存行は割当バイトのみをDBスキーマへ保存し、世代別GC回数を保持しない。今回の64B増分はArray/List ClassInt32のFLinq側だけに現れ、IEnumerable ClassInt32は336B→400B。GCを含む後続候補ではWorker生ログも併記する。
- 採否理由: 直接span形状そのものは達成したが、ClassInt32で2%を大幅に超える回帰、IEnumerableとList Int32で1.02超過が残った。改善3%以上かつ同一ソース群に2%超回帰なし、という採用条件を満たさない。
- 復帰: `PipelineBuilder` の単一演算子許可リストから `Zip` を削除し、`ZipConsumeUsesDirectDualSpanLoop` 専用テストを削除。既存 Zip 値Enumerator fallback と公開APIは復帰済み。既存の `Zip.GetConsumeExecutionStatement` は未到達のまま残るが、今回の測定ではその直接経路は不採用と確定した。

### 不採用仮説の保存規約（2026-08-12追加）

性能向上を予想して実測で不採用となったベンチマーク候補は、削除・上書きしない。各候補をこの台帳に残し、(1) ベンチマーク名・ソース型・要素型・終端、(2) 期待した生成形状と実際の形状、(3) 静的型消失/boxing/callvirt/interface dispatch/delegate/comparer/state machine、(4) 修正前後の独立3Run中央値・割当・取得可能なGC、(5) 性能劣化の根拠付き考察と不確実性、(6) 採否基準への照合、(7) 復帰内容、を必須記録とする。

C-ZipTuple-05 の劣化考察補足:

- 直接span案は tuple `Struct0.Current` と外側 `TryGetNext` loop を消したが、代わりに二つの `TryGetSpan`、二つの Span local、成功/失敗の二経路、最短長計算を終端メソッドへ追加した。従来の単一構造体 `TryGetNext` はJITが小さなループとして最適化しやすく、直接化した大きな分岐形状はインライン予算・分岐予測・レジスタ割当を悪化させ得る。
- ClassInt32では Array/List とも FLinq 割当が0Bから64B、IEnumerableは336Bから400Bへ増えた。増分の正確な割当元はGC詳細をDBが保持しないため未確定だが、単なる測定揺れでは説明しにくい一貫した観測である。
- したがって「wrapperを除けば必ず速い」はこのConsumer/JIT形状では成り立たなかった。上記はプロファイラ未実施時点の機構仮説であり、最終因果の断定ではない。採用条件に反するため原因をさらに深掘りしても候補は復帰済みとする。

#### C-ZipTuple-05: 展開コード比較（性能劣化説明用）

修正前（既存fallback、値列挙型wrapper）:

```csharp
using var __ce0 = new ZipStruct<T, ArrayEnumerator<T>, ArrayEnumerator<T>>(
    new ArrayEnumerator<T>(first), new ArrayEnumerator<T>(second));
while (__ce0.TryGetNext(out (T First, T Second) pair)) {
    consumer.Consume(pair);
}

// ZipStruct.TryGetNext
if (source.TryGetNext(out var left) && second.TryGetNext(out var right)) {
    current = Current = (left, right);
    return true;
}
current = default;
return false;
```

試行後（`Zip.GetConsumeExecutionStatement`、直接span分岐）:

```csharp
using var leftEnumerator = new ArrayEnumerator<T>(first);
using var rightEnumerator = new ArrayEnumerator<T>(second);
if (leftEnumerator.TryGetSpan(out var leftSpan)
    && rightEnumerator.TryGetSpan(out var rightSpan)) {
    var length = Math.Min(leftSpan.Length, rightSpan.Length);
    for (var index = 0; index < length; index++) {
        consumer.Consume((leftSpan[index], rightSpan[index]));
    }
} else {
    while (leftEnumerator.TryGetNext(out var left)
           && rightEnumerator.TryGetNext(out var right)) {
        consumer.Consume((left, right));
    }
}
```

比較結果:

- 除去できたもの: `ZipStruct.Current`への書込み、外側wrapperの`TryGetNext`呼出し、wrapperの`Dispose`。
- 新たに入ったもの: 二つの`TryGetSpan`、二つの`ReadOnlySpan<T>` local、span/fallback二経路、`Math.Min`、分岐先双方のtuple構築。
- 観測との対応: 期待した「wrapper除去」は達成したが、ClassInt32では64B増加し、FLinq中央値がArrayで3,400ns→16,733ns、Listで4,200ns→15,333ns、IEnumerableで36,150ns→72,533nsとなった。従って、展開後の大きな二経路が小さなwrapper loopより不利だったという仮説を支持する。ただしJIT disassembly/GC allocation stack未取得のため、因果の確定ではなく測定に基づく考察として扱う。

## C-SequenceEqual-06 — 静的ジェネリック `T` の span 比較を runtime-specialized helper へ lowering（測定前）

- 対象: `Array<ClassInt32>/Array<int>` および同じ静的 `T` を通る `List<T>`・`IEnumerable<T>` の `SequenceEqual_second` と既定 comparer の `SequenceEqual_second_comparer`。
- 終端: `bool` 結果を benchmark consumer へ渡す scalar terminal。要素型はベンチ基底コード上では `T`。
- 変更前の FLinq 展開形状（静的 Array/List で span 可）:
  ```csharp
  if (source.TryGetSpan(out var __spanL) && second.TryGetSpan(out var __spanR))
      return System.MemoryExtensions.SequenceEqual(__spanL, __spanR); // T のまま
  var __eq = EqualityComparer<T>.Default;
  while (...) { ... __eq.Equals(...); }
  ```
- 変更後の FLinq 展開形状（静的 `T` のみ）:
  ```csharp
  if (source.TryGetSpan(out var __spanL) && second.TryGetSpan(out var __spanR))
      return Seamless.Linq.Enumerable.SequenceEqualSpan(__spanL, __spanR);
  ```
  helper 内で `typeof(T)` によりプリミティブは SIMD `Span.SequenceEqual`、それ以外は既定 comparer の直接ループを選ぶ。具象 `int` の生成コードは従来の `MemoryExtensions.SequenceEqual` のまま。
- ZLinq 側の展開形状: 未確認。従って「ZLinq が同じ runtime-specialization を使う」は仮説であり、パッケージまたは生成物の直接確認前には断定しない。
- 失われている静的情報: benchmark 基底メソッドの `T`。Array/List であることと span 能力は保持されるが、値型プリミティブか参照型かは生成時に確定しない。
- boxing/interface dispatch/state machine: この span 分岐自体には `IEnumerator<T>`、boxing、状態機械、tuple はない。変更前の generic `MemoryExtensions.SequenceEqual<T>` のJIT実装選択が主な比較対象。default comparer は fallback のみ。
- 割当/GC/時間: 測定前。Headless UI高速プロファイルはDBへ割当量を保存するがGC世代数を保存しないため、Worker側出力も確認する。
- 仮説: `T=int` の閉じた実行時型では helper が SIMDへ分岐し、`T=ClassInt32` では delegate/比較器引数を導入せず直接 comparer loop を選べるため、汎用 `MemoryExtensions.SequenceEqual<T>` のコストを避けられる可能性がある。
- 最小修正: `SpanEqualityExpression` の2か所に「静的に vectorizable な具象型」の条件を追加し、静的 `T` を既存 helper へ委譲するだけ。
- 判定予定: 強制再生成の `PerformanceShapeTests`、結果・空列・例外・Disposeを含む既存 SequenceEqual テスト、同一Worker・Release・各比較3独立Run。3%以上改善、同一ソース群2%超回帰なし、各中央値比率1.02以内を採用条件とする。

## C-SequenceEqual-06 — 静的ジェネリック `T` を `SequenceEqualSpan<T>` へ委譲する候補（却下・全測定保存）

### 対象・契約

- ベンチマーク: `SequenceEqual_second`、`SequenceEqual_second_comparer`。後者は `EqualityComparer<T>.Default` を明示する既定 comparer 経路。
- ソース型: `Array<T>`、`List<T>`。要素型: `ClassInt32` と `Int32`。終端: `bool` を consumer へ渡す scalar terminal。
- 静的型が失われる地点: benchmark 基底実装では `AsX<T>()` 後の要素型が構文上 `T`。Array/List と span 能力は保持されるが、`T` が int 等か参照型かは生成時に不明。
- interface dispatch / boxing / state machine / tuple: 本候補の span 分岐・前後いずれにも `IEnumerator<T>`、boxing、状態機械、tuple中間値、delegate追加はない。値列挙structの `TryGetSpan` から直接 scalar `Compute` に入る。
- ZLinq生成コード形状: この候補の測定では未展開・未確認。ZLinqが同じhelper形状を持つとは断定しない。

### 展開コード比較（性能劣化説明を含む）

変更前（静的 `T` でも Array/List がspanを出せればBCLのジェネリック呼出し）:

```csharp
if (source.TryGetSpan(out var __spanL) && second.TryGetSpan(out var __spanR))
    return System.MemoryExtensions.SequenceEqual(__spanL, __spanR);
var __eq = EqualityComparer<T>.Default;
while (true) { /* TryGetNext と __eq.Equals */ }
```

候補変更後（`T` が生成時にプリミティブ名として確定しない場合）:

```csharp
if (source.TryGetSpan(out var __spanL) && second.TryGetSpan(out var __spanR))
    return Seamless.Linq.Enumerable.SequenceEqualSpan(__spanL, __spanR);
```

helper の実展開形状（要約）:

```csharp
if (spanL.Length != spanR.Length) return false;
if (spanL.Length == 0) return true;
if (Unsafe.AreSame(ref MemoryMarshal.GetReference(spanL),
                   ref MemoryMarshal.GetReference(spanR))) return true;
if (typeof(T) == typeof(byte))  return UnsafeSpanBitCast<T,byte>(spanL).SequenceEqual(...);
// sbyte, short, ushort, int, uint, long, ... のtypeof分岐
if (typeof(T) == typeof(int))   return UnsafeSpanBitCast<T,int>(spanL).SequenceEqual(...);
var eq = EqualityComparer<T>.Default;
for (var i = 0; (uint)i < (uint)spanL.Length; ++i)
    if (!eq.Equals(spanL[i], spanR[i])) return false;
return true;
```

形状改善はClassInt32で顕著だったが、Int32では前者の直接呼出しに対し、長さ/空/alias判定、`typeof`連鎖、bitcastが追加される。高速プロファイル（要素数軸1・閾値1ms）ではこの固定費が支配的となり、ListのInt32で4.71～5.35%悪化した。ClassInt32の97～99%改善は同一span早期終了の存在と整合するが、JIT disassembly・branch profileを取得していないため、当該branchが原因であることは推定に留める。helper自体は新規割当をしない。

### 強制再生成・テスト

- 候補適用時: `PerformanceShapeTests.SequenceEqualWithoutComparerAvoidsInterfaceDispatch` と `SequenceEqualTests` は Release 2/2成功。静的 `GenericEquals<T>` の支援struct `Compute` に `SequenceEqualSpan(__spanL,__spanR)` があり、同一支援struct内に `MemoryExtensions.SequenceEqual(__spanL,__spanR)` はないことを確認。
- 候補適用時: `Test.System.Linq.Enumerable.FLinq` の SequenceEqual フィルタは Release 4/4成功。
- 最終状態: 候補の2条件、comparerクラスへ追加したprivate helper、専用形状テストをすべてロールバック済み。既存FLinq SequenceEqualテストは4/4成功。最終の生成形状テストは別途再ビルド確認中。

### 同一Headless条件の独立3回測定

条件は全Run共通で Release、Headless（`Benchmark.UI.exe`を起動しない）、UI高速プロファイル、CPU 0、要素数軸1、閾値1ms。GC世代数はHeadless DBスキーマが保存しないため `n/a`（割当量のみDB値）。

| source / benchmark / type | 修正前 Run | 修正前 FLinq ns / ZLinq ns / 比 | 修正後 Run | 修正後 FLinq ns / ZLinq ns / 比 | FLinq差 | 割当 F/Z 前→後 |
|---|---:|---:|---:|---:|---:|---:|
| Array / second / ClassInt32 | 128–130 | 26572.222 / 2805.556 / 9.471 | 116–118 | 436.842 / 2802.632 / 0.156 | -98.36% | 64/0 → 64/0 |
| Array / second / Int32 | 128–130 | 73.599 / 173.484 / 0.424 | 116–118 | 66.048 / 76.374 / 0.865 | -10.26% | 0/64 → 16/0 |
| Array / comparer / ClassInt32 | 131–133 | 33682.051 / 2574.359 / 13.084 | 119–121 | 289.189 / 2756.410 / 0.105 | -99.14% | 64/0 → 64/0 |
| Array / comparer / Int32 | 131–133 | 64.935 / 75.805 / 0.857 | 119–121 | 66.461 / 73.179 / 0.908 | +2.35% | 18/0 → 19/0 |
| List / second / ClassInt32 | 134–136 | 23977.778 / 2794.286 / 8.581 | 122–124 | 511.429 / 2759.259 / 0.185 | -97.87% | 64/0 → 64/0 |
| List / second / Int32 | 134–136 | 85.057 / 88.240 / 0.964 | 122–124 | 89.062 / 87.807 / 1.014 | +4.71% | 0/0 → 0/0 |
| List / comparer / ClassInt32 | 137–139 | 26150.000 / 2775.000 / 9.423 | 125–127 | 305.714 / 2683.784 / 0.114 | -98.83% | 64/0 → 64/0 |
| List / comparer / Int32 | 137–139 | 84.403 / 84.240 / 1.002 | 125–127 | 88.915 / 83.234 / 1.068 | +5.35% | 0/0 → 0/0 |

各セルは3Runの中央値。Int32の64B程度の揺れは短時間キャリブレーションの個別Runに現れ、中央値欄と生値はDBに保存済み。Run 116–127は候補あり、Run 128–139は候補なし。旧UI WorkerのRun 94–96は実行日時・ルート・タブが異なるため、この採否表には使用していない。

### 採否

**却下（ロールバック済み）。** ClassInt32では十分な改善かつ比1.02以内だが、Array comparer Int32が +2.35%、List Int32が +4.71%、List comparer Int32が +5.35%で、同一ソース群の2%回帰禁止に反する。List comparer Int32の修正後比1.068も目標1.02を満たさない。最小修正で型別に切り分けるには生成時に `T` の閉じた型を知る必要があり、runtime分岐を追加しても今回の固定費問題を再導入するため、この形の採用は不適切。

次候補は `SequenceEqualSpan<T>` の単純委譲ではなく、静的generic `T` のままでも「既定comparer・同一source」を安全に判定できるか、またはZLinqの実展開を確認した上で、Int32の直接BCL経路を保つ分離loweringを検討する。

- 最終検証追記: C-SequenceEqual-06をロールバックした状態で、Releaseの強制再生成 PerformanceShapeTests.SequenceEqualWithoutComparerAvoidsInterfaceDispatch | SequenceEqualTests は2/2成功（既存FLinq SequenceEqual契約テスト4/4も成功）。


## H-SequenceEqual-Alias-07 — 同一キャッシュsourceのalias早期終了（保留、未実装・未測定）

- `srcCount(StreamShapeCount)` はArray/Listで `SourceCacheKey(SrcCount, length, seed)` を通るキャッシュなので、`SequenceEqual_second` の左右は同一インスタンスになる。
- そのため `ReadOnlySpan<T>` の `Unsafe.AreSame` だけを生成器に直接入れれば、C-SequenceEqual-06の `typeof`連鎖を避けてClassInt32を短絡できる仮説がある。
- ただし任意の `IEqualityComparer<T>`／非反射的な独自 `Equals` まで含むSystem LINQ互換性を、この短絡が保つか未確定。C-SequenceEqual-06で使用した既存helperには同じ短絡があるが、本タスクの意味契約（Comparerを変えない）を満たす根拠にはならない。
- 採否: 保留。System.Linq/MemoryExtensionsとの結果一致を、非反射的 comparer を含む追加テストで確認できるまで実装・測定しない。

## C-SkipArray-08 — ArrayRoot の Skip(count).Consume をraw Span loopへ直接lower（測定前）

- 対象: `System.Array` の `Skip_count`、ソース `T[]`、要素 `Int32/ClassInt32`、終端 `Consume`。
- 強制再生成済みの変更前形状: `using var __skipSource = new Struct1<T>(this.srcCount(...)); var __skipSpan = __skipSource.GetSpan(); Clamp; for (...) consumer.Consume(span[i]);`。`Struct1<T>.Dispose()` は空。
- 仮説: 静的 ArrayRoot だけではraw `MemoryExtensions.AsSpan(array)` を使い、no-op値enumerator構築とGetSpan呼出しを生成コードから除ける。`IEnumerator<T>`、boxing、delegate、状態機械、tupleは追加しない。
- 安全条件: `DirectArrayExpression` が静的に存在する ArrayRoot のみ。List/Persisted/IEnumerable/未知列挙は既存経路を維持する。ArrayRootのDisposeは空で、source式は1回だけ評価する。
- ZLinq形状: 未確認。ZLinqが同じraw span形状を使うとの断定はしない。
- 測定前の割当/GC/時間: 既存Run 94–96では Array<Int32> Skip_count の FLinq/ZLinq中央値比が約2.498。候補前後はHeadless同条件3回で再測定する。GC世代数はHeadless DB非保存。
- 採否条件: 結果・空列・例外・Dispose形状をテストし、3%以上改善、Array内2%超回帰なし、各中央値比1.02以内のみ採用。

## C-SkipArray-08 — ArrayRoot Skip(count).Consume のraw Span lowering（却下・全測定保存）

### 生成コード比較

変更前（強制再生成: `Benchmark/Benchmark.System.Linq.Enumerable.FLinq/Artifacts/generated-inspection/skip-20260812/...Base_cs.g.cs` の Skip_count）:

```csharp
using var __skipSource0 = new Struct1<T>(@this.srcCount(4096));
var __skipSpan0 = __skipSource0.GetSpan();
var __skipCount0 = Math.Clamp(@this.QuarterCount(4096), 0, __skipSpan0.Length);
for (var __skipIndex0 = __skipCount0; __skipIndex0 < __skipSpan0.Length; __skipIndex0++)
    consumer.Consume(__skipSpan0[__skipIndex0]);
```

`Struct1<T>` は `T[] source` とindexだけを保持し、`GetSpan()` は `MemoryExtensions.AsSpan(source, __e1 + 1)`、`Dispose()` は空。終端には `IEnumerator<T>`、boxing、delegate、tuple、状態機械はない。なおSkipの汎用列挙structは出力されるが、この直接終端では生成・実行されない。

候補変更後:

```csharp
var __skipSpan0 = MemoryExtensions.AsSpan(@this.srcCount(4096));
var __skipCount0 = Math.Clamp(@this.QuarterCount(4096), 0, __skipSpan0.Length);
for (var __skipIndex0 = __skipCount0; __skipIndex0 < __skipSpan0.Length; __skipIndex0++)
    consumer.Consume(__skipSpan0[__skipIndex0]);
```

静的 `ArrayRoot.DirectArrayExpression` がある場合だけ適用し、List/Persisted/IEnumerable/未知列挙は既存形状のままとした。source式は1回評価、ArrayRootのDisposeはno-opなのでDispose/例外/空列契約は変えない設計だった。ZLinqの展開形状は未確認。

### 検証・測定

- 候補適用時の強制再生成形状テストは1/1成功。`AsSpan(source)`、直接for、`using var __skipSource` 不在、`.GetSpan()`不在を確認した。
- 候補適用時の既存FLinq Skip契約テストはRelease 8/8成功。
- Headless UI高速プロファイル（CPU0、軸1、閾値1ms、UI未起動）の独立3回。候補あり Run 140–142、候補なし Run 143–145。GC世代数はHeadless DBが保存しないため n/a。

| 型 | 修正前 FLinq / ZLinq ns / 比 | 修正後 FLinq / ZLinq ns / 比 | FLinq差 | 割当 F/Z 前→後 |
|---|---:|---:|---:|---:|
| ClassInt32 | 10075 / 26675 / 0.378 | 11275 / 26650 / 0.423 | +11.91% | 64/0 → 64/0 |
| Int32 | 3310 / 8550 / 0.387 | 3881.818 / 8012.5 / 0.484 | +17.28% | 0/0 → 0/0 |

生値はDBのRun 140–145に全保存。ClassInt32候補後は12100, 9800, 11275ns、前は10725, 10075, 4600ns。Int32候補後は3875, 3881.818, 4175ns、前は3783.333, 3310, 3023.077ns。

### 採否・劣化考察

**却下（ロールバック済み）。** 生成コードの見た目は短くなったが、両型で3%以上悪化し、Array内2%回帰禁止に反する。`Struct1<T>` は小さな値型で全メソッドがAggressiveInlining、Disposeは空であるため、JITが元の構築/GetSpanをscalar replacementまたはinlineしていた可能性が高い。raw `AsSpan` への明示置換はその最適化より有利でなかった。JIT disassemblyを取得していないため因果は推定であり、確定事項は同一条件3回中央値の悪化だけである。

候補実装・専用テストは残さず、強制生成artifactだけを検証証跡として保存した。

- 最終検証追記: C-SkipArray-08をロールバック後、Releaseの既存FLinq Skip契約テストは8/8成功。全PerformanceShapeTestsは64秒実行上限で完了出力を得られなかったため、合否結果として扱わない。候補固有の形状テストはロールバック済み。


## 2026-08-12: 性能劣化候補の展開済みコード比較とZLinq実装形状の確定

- 記録規則を明文化する。今後、性能が悪化して却下する候補は、必ず (1) FLinq修正前の強制再生成コード、(2) FLinq候補後の強制再生成コード、(3) 同一バージョンのZLinq終端・列挙子形状、(4) 増減した分岐・local・call・割当と劣化の考察、(5) 因果を確定できない場合の未確定事項、を同じ候補節に残す。測定だけで却下理由を済ませない。
- 確認対象はこのリポジトリが実際に参照する `C:\Users\kazuh\.nuget\packages\zlinq\1.5.6\lib\net10.0\ZLinq.dll`。`ilspycmd` で `artifacts/zlinq-1.5.6-ilspy-20260812/` に逆コンパイルして確認した。これはソース検索結果や旧inspectionではなく、測定対象DLLと同一バージョンの証跡である。
- source route: `T[]` は `ValueEnumerable<FromArray<T>,T>`、`List<T>` は `ValueEnumerable<FromList<T>,T>`、静的 `IEnumerable<T>` は `ValueEnumerable<FromEnumerable<T>,T>`。Array/Listで静的型を失わず、各rootは値型 `IValueEnumerator<T>` を持つ。`FromArray<T>` は `Length`・`AsSpan`・添字+index、`FromList<T>` は `Count`・`CollectionsMarshal.AsSpan`・添字+index、いずれもDisposeは空である。
- `SequenceEqual` のZLinq終端は、両方の値列挙子を `using` で保持し、count不一致を先に返し、両方がspanを出せると `span.SequenceEqual(span2, comparer)` を直接呼ぶ。span不可時だけ既定comparerを確定して `TryGetNext` loopへ落ちる。従って C-SequenceEqual-06 の `typeof(T)` runtime-specialized helper はZLinq形状ではない。既定のFLinq BCL generic span呼出しを置換したこと自体が、Int32回帰の有力な形状差である。
- `TakeSkip<TEnumerator,T>` も元の値列挙子を保持して `TryGetSpan` 成功時にSliceして返す。C-SkipArray-08 のraw span案は、値enumeratorを表面上除いたが、ZLinqも能力伝播する値enumeratorを維持している。測定上の悪化は「wrapperがある」事実だけを根拠に除去してはならないことを示す。
- この記録は既存の C-ZipTuple-05、C-SequenceEqual-06、C-SkipArray-08 の展開比較と併読する。劣化原因はJIT逆アセンブルまたはallocation stackを得るまで仮説であり、確定事項は同一Worker条件で保存した前後中央値・割当差だけとする。

## 2026-08-12: 現行Release Array 全比較・独立3 Run（Headlessのみ）

- **Worker分離**: `Benchmark.UI.exe` は各Runの前後とも未実行。旧UI結果は参照せず、現行DLLを強制再生成した後にHeadless Runnerが起動した短命Workerのみを対象にした。Run **146, 147, 148**、高速プロファイル、CPU 0、要素数 `[1]`、各194比較はFLinq/ZLinqともCompleted。DBへのGen0/1/2保存は未対応のためGC欄は `n/a`。
- 3回中央値で `FLinq / ZLinq > 1.02` は **48 / 194**。上位は次の通り（時間ns、割当BはFLinq/ZLinq）。詳細な全標本は `Work/UI/Benchmark.db` のRun 146--148に保存済み。

| 要素型 | ベンチマーク | FLinq中央値 | ZLinq中央値 | 比率 | 割当 | 次の扱い |
|---|---|---:|---:|---:|---:|---|
| ClassInt32 | SequenceEqual_second_comparer | 16,842.424 | 2,673.684 | 6.299 | 0 / 0 | 生成形状を最優先調査 |
| ClassInt32 | SequenceEqual_second | 15,676.316 | 2,723.684 | 5.756 | 0 / 0 | 同上 |
| Int32 | Zip_tuple | 11,781.579 | 2,290.909 | 5.143 | 0 / 0 | 採用済みdirect loweringの再形状確認 |
| ClassInt32 | AggregateBy_keySelector | 133,500 | 35,700 | 3.739 | 56 / 56 | hash/comparer経路を後続調査 |
| Int32 | Take_count | 4,382.692 | 1,543.077 | 2.840 | 0 / 0 | span/terminal形状を調査 |
| ClassInt32 | SelectMany_selector | 16,950 | 6,878.571 | 2.464 | 2,736 / 2,736 | 内側列・状態機械を調査 |
| Int32 | SkipWhile_predicate | 6,034.211 | 2,647.368 | 2.279 | 0 / 64 | predicate span loweringを調査 |
| Int32 | SkipWhile_indexPredicate | 4,367.925 | 1,976.471 | 2.210 | 0 / 88 | index状態を調査 |
| ClassInt32 | SelectMany_indexSelector | 14,525 | 6,721.429 | 2.161 | 2,736 / 2,736 | 内側列・状態機械を調査 |
| Int32 | Skip_count | 3,735.593 | 1,742.857 | 2.143 | 0 / 0 | C-SkipArray-08却下の現行確認 |

- これらは旧UI RunおよびC-SkipArray-08/C-SequenceEqual-06候補Runとは混在させない。今回の中央値は現行ベースラインであり、候補を変える場合は同じHeadless条件で改めて候補前後3 Runを保存する。

## C-SequenceEqual-ZLinqShape-09 — BCL span comparer呼出しをZLinqと同じ形に揃える（測定前）

- 対象: `System.Array` の `SequenceEqual_second` / `SequenceEqual_second_comparer`。ソース型は静的 `T[]`、要素型は `ClassInt32` と `Int32`、終端は `Consumer.Consume(bool)`。
- 現行FLinqの強制生成形状: 2個の `StructN<T> : IValueEnumerator<T>`（Array、index、TryGetSpan、Dispose空）を生成し、独自 `Struct0<T,TEnum1,TEnum2>.Compute` へ値渡しする。span成功時は非comparer・default comparerとも `MemoryExtensions.SequenceEqual(__spanL,__spanR)`。`SequenceEqual_second_comparer` は渡された `EqualityComparer<T>.Default` をspan分岐で使わない。boxing、`IEnumerator<T>`、delegate、状態機械、tuple中間値はないが、generic helper境界と2回のTryGetSpanが残る。
- 同一測定DLLを逆コンパイルしたZLinq形状: `srcCount(...).AsX()` は `ValueEnumerable<FromArray<T>,T>` へ静的に解決される。終端は2個の具体値enumeratorをusingし、`TryGetNonEnumeratedCount`で長さ不一致を先に返し、span成功時に **`span.SequenceEqual(span2, comparer)`** を呼ぶ。comparerがnullならその後のfallbackだけで `EqualityComparer<T>.Default` を確定する。Array rootはsource+indexのみ、Dispose空。
- 静的型消失地点: どちらもArray root自体は維持。FLinqだけは終端で独自generic helperに一段包む。今回の最小修正ではhelperを消さず、BCL span呼出しのoverloadだけをZLinqと一致させる。
- 前中央値（現行Headless Run 146--148）: ClassInt32 non-comparer 15,676.316ns / 2,723.684ns = **5.756**、comparer 16,842.424ns / 2,673.684ns = **6.299**、いずれも0B/0B、GC n/a。Int32は0.997/0.989で既に閾値内。
- 仮説: `T=ClassInt32` のcanonical generic codeでは、BCLの2引数overloadと、ZLinqが使う3引数（null/default comparer）overloadのJIT/intrinsic展開が異なる。FLinqはdefault comparer引数を捨てるため、ZLinqの実呼出し形状と一致していない。これは生成コード上の差であり、速度改善は未確認。
- 最小修正案: 非comparerのspan呼出しを `MemoryExtensions.SequenceEqual(__spanL,__spanR,null)`、静的default comparer分岐を `MemoryExtensions.SequenceEqual(__spanL,__spanR,comparer)` にする。明示custom comparerの手書きloop、span失敗fallback、Dispose、例外、空列は変更しない。
- 採否条件: 強制再生成、SequenceEqual契約/形状テスト、候補後の同一Headless 3 Run。ClassInt32を3%以上改善し、Int32・同一Array群に2%超回帰がなく、各中央値比率1.02以内でのみ採用。悪化または未達ならロールバックし、FLinq前後およびZLinqの展開コード比較と理由を追記する。

## C-SequenceEqual-ZLinqShape-09 — 結果: 却下・ロールバック

### 強制生成コード比較

修正前（Run 146--148 のDLL）:

```csharp
// FLinq（両終端共通のdefault comparer経路）
if (source.TryGetSpan(out var left) && second.TryGetSpan(out var right))
    return MemoryExtensions.SequenceEqual(left, right);
```

修正後（`Artifacts/generated-inspection/sequenceequal-zlinqshape-20260812/...Base_cs.g.cs`、non-comparer行6471--6493、comparer行6563--6585）:

```csharp
// FLinq（ZLinqと同じ3引数BCL overloadを選ぶ）
if (source.TryGetSpan(out var left) && second.TryGetSpan(out var right))
    return MemoryExtensions.SequenceEqual(left, right, comparer);
// non-comparer call site は Compute(left, right, null)
// comparer call site は Compute(left, right, EqualityComparer<T>.Default)
```

ZLinq 1.5.6（逆コンパイル `ValueEnumerableExtensions.cs` 3052--3077）:

```csharp
using TEnumerator left = source.Enumerator;
using TEnumerator2 right = second.Enumerator;
if (left.TryGetNonEnumeratedCount(out var a) && right.TryGetNonEnumeratedCount(out var b) && a != b) return false;
if (left.TryGetSpan(out var x) && right.TryGetSpan(out var y)) return x.SequenceEqual(y, comparer);
```

FLinqの候補後span呼出しはZLinqと一致したが、FLinqにはなお独自 `Struct0<T,TEnum1,TEnum2>.Compute` と生成root structがあり、ZLinqは `FromArray<T>` をValueEnumerable終端へ直接渡す。boxing、`IEnumerator<T>`、delegate、状態機械、tuple中間値はいずれのFLinq形状にもない。

### 契約・測定

- Release強制再生成: 成功。候補後のnon-comparer/comparerとも3引数span呼出しを確認。
- 契約: `Test.System.Linq.Enumerable.FLinq` のSequenceEqual 4/4成功、`PerformanceShapeTests.SequenceEqualWithoutComparerAvoidsInterfaceDispatch` と `SequenceEqualTests` 2/2成功。
- UI/Worker分離: UI未起動。候補後はHeadless短命Workerのみ。non-comparer Run 149,150,154、comparer Run 151,152,153。GC世代数はDB非保存のためn/a。

| 型・比較 | 修正前 FLinq/ZLinq (比) | 修正後 FLinq/ZLinq (比) | 割当 修正前→後 | 判定 |
|---|---:|---:|---:|---|
| ClassInt32 non-comparer | 15,676.316 / 2,723.684 (5.756) | 29,467.742 / 2,941.667 (10.017) | 0/0 → 64/0 B | 回帰 87.97% |
| ClassInt32 comparer | 16,842.424 / 2,673.684 (6.299) | 34,825.714 / 2,794.286 (12.463) | 0/0 → 64/0 B | 回帰 106.78% |
| Int32 non-comparer | 55.550 / 55.408 (1.003) | 77.778 / 186.842 (0.416) | 0/0 → 0/64 B | 基準側も変動、Class回帰を相殺できない |
| Int32 comparer | 53.756 / 54.391 (0.988) | 76.522 / 192.698 (0.397) | 0/0 → 0/64 B | 同上 |

**採否: 却下。** ZLinqと同じBCL overloadに合わせても、FLinqのClassInt32は大幅に悪化し64Bを新規に割り当てた。最小差分にもかかわらず、`comparer` 引数を介したBCL経路がFLinq独自generic helper/Consumer文脈では2引数overloadより不利になった可能性が高い。ZLinqが速い原因をBCL overloadだけに帰す仮説は否定された。JIT逆アセンブル・割当stack未取得のため最終因果は断定せず、確定事項は上記の独立3回中央値と割当差である。3%改善・2%回帰禁止・1.02条件を満たさないため、変更は復帰する。

## C-SequenceEqual-DirectArray-10 — 静的Array rootのscalar終端を直接span比較へlower（測定前）

- 対象: `System.Array` / `SequenceEqual_second`（non-comparerのみ）。ソースは左右とも静的 `T[]`、要素はClassInt32/Int32、終端はbool Consumer。
- 現行FLinq生成形状: Array rootごとの `Struct1<T>`/`Struct2<T>`、`Struct0<T,TEnum1,TEnum2>.Compute`、2回のTryGetSpanを生成してから2引数 `MemoryExtensions.SequenceEqual(__spanL,__spanR)` を呼ぶ。値enumeratorでboxing/interface/state machine/delegate/tupleはないが、終端までroot structとgeneric Computeが残る。
- ZLinq形状: `T[] -> FromArray<T>` の値enumeratorをSequenceEqual終端へ直接渡す。count比較後にspan比較。FLinqの独自Compute wrapperはない。
- 仮説: Array式を左右一度ずつローカルに評価して直接2引数BCL span比較へlowerすれば、FLinq固有のroot struct、TryGetSpan、generic Computeを実行コードから除ける。C-SkipArray-08ではraw span化が悪化したため、これはSkipとは別のscalar BCL終端に限定した仮説であり、成功を前提にしない。
- 最小修正案: `SequenceEqual.RenderInlineComputation` に、両方の `DirectArrayExpression` と同一要素型がある場合だけblock-localの左右配列を作り、`MemoryExtensions.SequenceEqual` へ直接渡す。source評価順・回数は左→右で各1回、ArrayRootのDisposeは空。List/IEnumerable/Range/明示comparerは現行fallbackを維持する。不要なprivate root/helper structもこの直通形状では生成しない。
- 前中央値: Run 146--148、ClassInt32 15,676.316 / 2,723.684ns = 5.756（0/0B）、Int32 55.550 / 55.408ns = 1.003（0/0B）、GC n/a。
- 採否条件: 強制再生成でdirect callかつ不要なStruct/IValueEnumeratorが終端から消えること、SequenceEqual結果/例外/Dispose/空列テスト、候補後3 Run。ClassInt32 3%以上改善、Int32 2%超回帰なし、Array中央値比1.02以内のみ採用。失敗時は前後生成コード・測定・考察を追記してロールバックする。

## C-SequenceEqual-DirectArray-10 — 未測定却下（到達性検証）

- 強制再生成 `Artifacts/generated-inspection/sequenceequal-directarray-20260812/...Base_cs.g.cs` を確認したところ、`SequenceEqual_second` は想定した `SequenceEqual` fragmentではなく `SequenceEqual_comparer` のdefault comparer分岐から生成されていた。
- そのため候補後の展開コードは修正前と同じで、`Struct1<T>`、`Struct2<T>`、`Struct0<T,TEnum1,TEnum2>.Compute`、2回のTryGetSpan、2引数 `MemoryExtensions.SequenceEqual` が残った。直接Array local、wrapper削除は**生成されていない**。
- 性能測定は行わない。実行コード形状に差がないため、数値差を候補効果として扱うことは不正確である。ビルドは成功したが、到達しない変更をロールバックし、`SequenceEqual.cs` は既存ステージ済み内容と完全一致に復帰した。
- 原因: public/interceptorのnon-comparer呼出しも内部ではnull/default comparerを持つ `SequenceEqual_comparer` fragmentへ正規化される。候補選定時にfragment選択まで確認せず、未到達の同名scalar classを変更した。
- 今後の規則: fragment実装に手を入れる前に、強制生成された当該ベンチのクラス名・call site・Compute実体を確認し、候補前記録へ「到達するfragment」を明記する。これは性能劣化候補と同様に保存する調査失敗である。

## C-SequenceEqual-DirectArray-10 — 結果: 却下・ロールバック

### 展開コード比較

修正前は `Struct1<T>`/`Struct2<T>` と `Struct0<T,TEnum1,TEnum2>.Compute` が2回のTryGetSpan後に2引数BCL比較を呼んだ。修正後（`sequenceequal-directarray-reachable-20260812`）は両終端とも次の直接形状になった。

```csharp
var __spanL = @this.srcCount(4096);
var __spanR = @this.srcCount(4096);
result = MemoryExtensions.SequenceEqual(__spanL, __spanR);
consumer.Consume(result);
```

すなわちroot struct、TryGetSpan、generic Compute、Disposeは実行コードから除去できた。ZLinqは引き続き `FromArray<T>` 値enumerator、count比較、span comparer overloadを使う。FLinq候補にはboxing/interface/state-machine/delegate/tupleもない。

### 検証・測定

- Release強制再生成成功、形状確認済み。SequenceEqual契約4/4、PerformanceShape＋SequenceEqualTests 2/2成功。
- UI未起動。Headless短命Worker Run 155--160だけを候補後値とした。GC n/a。

| 比較 | 修正前比（Run 146--148） | 修正後比 | 割当 修正前→後 | 判定 |
|---|---:|---:|---:|---|
| ClassInt32 non-comparer | 5.756 | 11.730 (34,888.889 / 2,974.286ns) | 0/0 → 64/0B | 回帰 122.6% |
| ClassInt32 comparer | 6.299 | 8.861 (28,725.806 / 3,241.935ns) | 0/0 → 64/0B | 回帰 70.6% |
| Int32 non-comparer | 1.003 | 0.421 | 0/0 → 0/64B | 基準側を含む形で変動 |
| Int32 comparer | 0.988 | 0.411 | 0/0 → 0/64B | 同上 |

**却下。** 値型では速く見える一方、ClassInt32で大幅悪化し新規64B割当も発生した。したがって「FLinqのroot struct/generic Computeだけが遅さの原因」は否定された。JIT逆アセンブル・allocation stackがないため64Bの根因は確定しないが、同一条件3回中央値の回帰は採用条件に明確に反する。候補はロールバックする。

## 2026-08-12: 高速プロファイル標本揺れの扱い

- SequenceEqual候補の専用Runでは、FLinqコードだけを変更したのにZLinq Int32中央値がnon-comparer 55.408ns（Run146--148）から190.563ns（Run155,157,159）、comparer 54.391nsから189.764nsへ変動した。ZLinq DLL・入力・Run tabは同じである。
- これは1ms高速プロファイル、短命Worker、JIT tier/GC/サンプル数の影響を含むことを示す。したがって僅差の倍率だけで局所loweringの因果を断定しない。一方ClassInt32の候補後64B増と70%超回帰は同じ3回すべてで観測され、候補却下の事実は維持する。
- 今後は (1) 生成形状、(2) 3回中央値と割当、(3) 可能な場合は長い既存test workloadまたは交互測定、の三点が揃うまで「ZLinqとの差の原因」と断定しない。全DBの最終判定はUIと同じ高速プロファイルの新規3Runで行う。

## 2026-08-12: C-ZipTuple-11 — 直接Spanループ済みなのにInt32で5.143倍（保留、追加loweringなし）

### 対象・観測

- ベンチマーク: `System.Array.Int32.Zip_tuple`、終端 `Consume`。
- ソース型／要素型: `T[]` / `Int32`。両方とも `srcCount(4096)` の配列で、静的型は失われていない。
- 基準Run 146--148 の中央値: FLinq `11,781.579ns`、ZLinq `2,290.909ns`、`FLinq / ZLinq = 5.143`、割当 `0 / 0B`、GC `n/a`（UI fast profile）。

### 展開コード比較（性能劣化説明用）

FLinq生成コード（`Artifacts/generated-inspection/current-20260812/...Base_cs.g.cs`）はすでに直接Spanループである。

```csharp
using var __zipSource = new Struct1<int>(@this.srcCount(4096));
using var __zipSecond = new Struct2<int>(@this.src2Count(4096));
if (__zipSource.TryGetSpan(out var __span1) && __zipSecond.TryGetSpan(out var __span2))
{
    var __length = Math.Min(__span1.Length, __span2.Length);
    for (var __index = 0; __index < __length; __index++)
        consumer.Consume((__span1[__index], __span2[__index]));
}
else
{
    while (__zipSource.TryGetNext(out var __first) && __zipSecond.TryGetNext(out var __second))
        consumer.Consume((__first, __second));
}
```

ZLinq 1.5.6 の該当形状は `ValueEnumerable<Zip<...>>.Consume` → `Zip.TryGetNext` であり、各要素でtupleを構築してからConsumerへ渡す。

```csharp
while (enumerator.TryGetNext(out var item))
    consumer.Consume(item);

// Zip.TryGetNext
if (source.TryGetNext(out var first) && second.TryGetNext(out var second))
{
    current = (first, second);
    return true;
}
```

従って今回のFLinq側には `IEnumerable<T>`/`IEnumerator<T>` のinterface dispatch、boxing、状態機械、delegate、結果selector、汎用`Zip.TryGetNext`終端経路はない。FLinqはtuple中間値も終端で直接構成する。ZLinqは少なくともソースレベル上はtuple値を一度`out current`に作る。

### 判定・理由

- 仮説「Zipの専用fragmentが終端へ届かず汎用Enumeratorへ落ちている」は**否定**。現在の実際の展開コードにはその漏れがない。
- この5.143倍をさらにZip loweringで説明することはできない。FLinqが既により短い直接ループ形状であり、対象の1ms高速プロファイルは既知のWorker/JIT tier標本揺れを含む。
- JIT逆アセンブル・同一プロセスでの十分長い交互測定なしに、既に存在する直接ループを別実装へ置き換えるのは回帰リスクが高い。最小修正案は**なし**、状態は**保留**。今後は実行コードのJIT形状と長時間同一Worker測定を先に取得する。

## 2026-08-12: 計測プロファイルの訂正と追加の展開コード監査

### 計測値の訂正

- 先の「1ms高速プロファイルが標本揺れを生む」という表現は正確ではない。DBに保存する UI fast の表示目標は `1,000,000ns` だが、Headless の `paired-fast-run` Worker は各比較について約 `10,000,000ns` を目標にloopCountを較正する。
- 同一Worker内でFLinq/ZLinqを16回warmupし、その後9標本を左右交互順序で採り、中央値を保存する（`Benchmark/Worker/Program.cs:310-329`）。従って各Runの内部比較は1ms単発ではない。
- 一方、独立Runは新規Worker/JIT/OS状態を含むため、別Run間で基準側の値が動く可能性は残る。以後は「1msが原因」と断定せず、**独立Worker間の環境・tiering変動の可能性**と記載する。

### Skip / Take / SkipWhile（Array Int32、保留・修正なし）

`Skip_count` は一見すると生成補助structの `TryGetSpan` が `false` で能力転送漏れに見える。しかし実行される終端はそのstructを生成せず、以下の専用直接loopである。

```csharp
using var source = new Struct1<T>(@this.srcCount(4096));
var span = source.GetSpan();
var skip = Math.Clamp(@this.QuarterCount(4096), 0, span.Length);
for (var i = skip; i < span.Length; i++) consumer.Consume(span[i]);
```

`Take_count` も同様に `GetSpan` と境界済み直接`for`、`SkipWhile_*` は predicate評価用の最初のSpan loopとConsumer用の第2Span loopである。到達コードに `IEnumerable<T>`、`IEnumerator<T>`、boxing、状態機械、delegate（predicateはインライン化済み）はない。ZLinqの`Skip<TEnumerator,T>`／`Take<TEnumerator,T>`は`TryGetSpan`でSliceを返すが、Consumer拡張は`TryGetNext`反復である。従って当該大差を「Span伝播漏れ」として直す候補にはしない。

### AggregateBy_keySelector（ClassInt32、保留・修正なし）

FLinqは `DictionarySlim<int,int>` を一度だけ作り、配列Spanを走査して `Base<T>.Mod` と `Base<T>.Number` を直接呼ぶ。key selector/accumulator delegateは展開時に消えている。

```csharp
if (__e0.TryGetSpan(out var span)) {
    dictionary = new DictionarySlim<int,int>(Math.Min(span.Length, 1024));
    for (var i = 0; i < span.Length; i++) {
        var item = span[i];
        var key = Base<T>.Mod(item, 1000);
        ref var acc = ref dictionary.GetValueRefOrAddDefault(key, out var exists);
        if (!exists) acc = 0;
        acc = acc + Base<T>.Number(item);
    }
}
```

対してZLinq 1.5.6 は同じSpan走査とDictionarySlimだが `Func<T,int>` と `Func<int,T,int>` を各要素で呼ぶ。FLinqにinterface dispatch/boxing/state machineはない。よって `FLinq/ZLinq=3.739` を静的型消失で説明できない。両DictionarySlimは別実装であり、これはlowering外の可能性がある。公開APIを変えず一演算子一原因という条件上、ここでDictionarySlim全体を置換する候補は採らない。

## 2026-08-12: C-SelectMany-12 — 実体がRangeの静的IEnumerable内側（既却下と照合、保留）

- ベンチマーク: `SelectMany_selector` / `SelectMany_indexSelector`。ソース宣言は `IEnumerable<IEnumerable<int>>`、終端は `Consume`。
- `srcNestedCount` は `Enumerable.Range(1, side).Select(_ => Enumerable.Range(1, side))` を返す（`Shared/Common/TestInfrastructure/Base.cs:769-771`）。従って内側の実体は `int[]` / `List<int>` ではなくSystem LINQ Range iteratorである。
- FLinq `SelectMany_selector` の展開には `IEnumerable<int>.GetEnumerator()`、`IEnumerator<int>.MoveNext()`、`Current`、Disposeが残る。index-selectorには`int[]`検査付きfast pathがあるが、実体Rangeには一致しないため同じinterface fallbackへ進む。

```csharp
// selector: 内側は静的IEnumerable<int>のためinterface Enumerator
innerEnumerator = new Struct2<T>((IEnumerable<int>)outer);
while (innerEnumerator.TryGetNext(out var item)) consumer.Consume(item);

// Struct2.TryGetNext
this.enumerator ??= source.GetEnumerator();
if (this.enumerator.MoveNext()) { current = this.enumerator.Current; return true; }
```

- boxingは観測されないが、interface dispatchとSelectManyの状態保持（outer/inner/Dispose）は意味上必要である。`srcNestedCount` を配列へ固定する、またはRangeの内部型をruntimeで認識する変更は、公開APIを変えずに静的型だけ特殊化するという条件を満たさない。
- 以前の C-SelectMany-04（無条件の直接nested interface loop）は、内側が配列である場合にも値列挙型/Span機会を失わせ回帰したため却下済み。本件は同じ根を別コードに置換しても改善根拠がない。**保留**とし、Range専用fragmentは対象を全ソース群へ広げる設計変更になるため採用しない。

## 2026-08-12: 現行Release強制再生成の確認（C-ShapeAudit-13）

- `Benchmark.System.Linq.Enumerable.FLinq` を `Release -t:Rebuild` し、`EmitCompilerGeneratedFiles=true` を明示した現行inspectionを `obj/Release/net10.0/inspection-current-20260812-0740` に生成した。過去artifactは根拠にしていない。
- `Test.System.Linq.Enumerable.FLinq` のPerformanceShape/SelectMany対象8件はReleaseで成功。
- `SequenceEqual_second_comparer` の現行出力は、呼出しが `EqualityComparer<T>.Default` と静的に認識されるため、既定比較用のSpan fast pathへ畳み込まれている。任意の明示comparerではfragmentの別分岐が `comparer ??= EqualityComparer<T>.Default` 後に要素ごと `comparer.Equals` を呼ぶ。従って今回のベンチにおける引数未使用は比較子破棄ではなく、既定comparerの意味保存済み特殊化である。
- ただしこの既定比較形状（値列挙struct2個、`TryGetSpan`、generic `Compute`）はZLinqとの差を説明するには不十分で、C-SequenceEqual-06/09/10の計測済み却下結果を覆す新根拠は得られなかった。変更なし・保留。

## C-ProviderContains-14 — Persisted Contains_value の FLINQ002 と生成コード比較（2026-08-12）

- 対象: Persisted (Seamless) / Contains_value。ソース型は Seamless.Collections.Persisted.Set<T>、要素型は T（Int32 / ClassInt32）、終端は Contains(value)。
- 強制再生成: Benchmark.Persisted.Linq.Enumerable.FLinq を Release Rebuild + EmitCompilerGeneratedFiles=true で実施。現在の出力は ...inspection-current-20260812-0750/...Base_cs.g.cs。
- FLinq の実際の展開結果（性能劣化/不成立の説明に必要なコード比較）:
  `csharp
  public static void OptimizedLinq_Contains_value_Impl...(...) {
      throw GeneratedCodeHelpers.UnsupportedLinqExpansion(
          "Contains_value_Impl",
          "Forwarding is disabled for methods that contain System.Linq.Enumerable calls.");
  }
  `
  したがって直接ループ、provider lookup、IEnumerable<T> fallback のいずれにも lower されず、実行時例外となる。割当・GC・時間の有効な FLinq 実測値は取得不能であり、比較対象から除外する。
- 対照の ZLinq 形状: ZLinq 1.5.6 の Contains は TEnumerable と値列挙子をジェネリックに保持し、TryGetNext(out T) を短絡走査する終端である。少なくとも throw/反射/Consumer 同期の混入はなく、Provider 固有 lookup が無い場合でも正常な値列挙子経路を持つ。
- 期待される FLinq 形状: comparer 無し・中間 stage 無し・PersistedSetRoot のときだけ ool result = source.Contains(value);。これは集合自身の等価性と一致する O(1) lookup で、enumerator/boxing/delegate/state machine を生成しない。comparer 指定または中間 stage ありは provider Iterate または既存 Enumerator fallback を維持する。
- 現時点の根拠: Contains fragment、ProviderNative profile、ProviderNativeLookupTerminal は存在するが、この実ベンチ本体では TryBuildLocalMethodBody が false となる。Array 同一メソッドは using ... ArrayRoot + ContainsSpan まで展開される。よって issue は Contains 演算子そのものではなく、source.AsX<T>() を含むローカル/for 本体で Provider root から終端へ到達する lowering 経路の欠落と仮説化する。
- 採否: **調査継続（未変更）**。未知・順序依存の provider 操作へ広げず、まずこの comparer 無し・stage 無しの意味が閉じたケースだけを最小再現テストで特定する。
- 計測: 未実施（FLINQ002 のため）。UI Worker は起動せず、既存の UI 結果とは混在させない。

## C-ProviderContains-14a — 仮説棄却: `PersistedBase<T>.srcCount` の静的型消失（2026-08-12）

- 対象: Persisted (Seamless) / `Contains_value`。
- 仮説: `Common.PersistedBase<T>` が `Common.Base<T>` の `srcCount` を `new Persisted.Set<T>` で隠していても、共有 `Base<T>` 本体からは配列または `IEnumerable<T>` に静的型が失われ、`AsX<T>()` が System LINQ `Contains` へ落ちる。
- 検証コード形状: `ArrayBase<T>.srcCount(): T[]` を `PersistedBase<T>.new srcCount(): Persisted.Set<T>` で隠し、`Container<T>: PersistedBase<T>` 内で `var source=this.srcCount(3); source.AsX<T>().Contains(default!)` を実行する最小 Roslyn 生成テストを追加した。
- 実際のバインド/展開形状: これは `Persisted.Set<T>.Contains` に直接束縛され、System LINQ 呼出しがないため interceptor 自体を生成しない。従って `IEnumerator<T>`、boxing、`callvirt MoveNext`、状態機械、delegate/comparer はこの経路に存在しない。
- ZLinq 比較: ZLinq も既知プロバイダの固有 API を呼べない場合は ValueEnumerable の `TryGetNext` ループへ進むが、この最小ケースは FLinq がそれ以前にプロバイダ直接呼出しへ解決される。従って ZLinq より遅い原因ではない。
- 判定: **棄却**。`new` 隠蔽単独は `FLINQ002` を再現しない。実ベンチマーク生成物の `Contains_value_Impl` が throw になる事実とは矛盾するため、次回は対象メソッドに含まれる全 Invocation の Roslyn symbol と、ビルド時に合成される source/Route の差を採取する。
- 測定: 未実施。生成コードが実行時 throw のため、この候補の FLinq/ZLinq 有効な中央値・割当・GC は算出不能。UI の旧 Worker 結果は使用していない。
- 採否: **不採用**。型消失修正は不要であり、ここへ runtime 分岐を加えると意味のない一般経路コストだけを増やす。

## C-StaticEnumerableLowering-15 — 静的 `System.Linq.Enumerable.Contains` 一般化（2026-08-12）

- 対象: `FLINQ_LIST_DIRECT` が有効な List の `Contains_value` / `Reverse` と、強制再生成済み Persisted 出力で観測された `Contains_value` の `FLINQ002`。
- 仮説: `System.Linq.Enumerable.Contains(source, value)` を拡張記法と同じ descriptor に lower すれば、配列/List は `ContainsSpan`、Persisted/Tsavorite は `source.Contains(value)` の直接 lookup になる。
- FLinq 失敗時の展開形状: `OptimizedLinq_Contains...` 本体が `UnsupportedLinqExpansion("Contains", "Forwarding is disabled for methods that contain System.Linq.Enumerable calls.")` の throw 一文になる。したがって有効な実行時間・割当・GC は測定不能。
- 期待した FLinq 形状: `var span = source.AsSpan(); result = Seamless.Linq.Enumerable.ContainsSpan(span,value);`（配列/List）または `bool result=source.Contains(value);`（Persisted/Tsavorite）。`IEnumerator<T>`、boxing、状態機械、tuple 中間値なし。
- ZLinq 形状: `FromArray<T>` / `FromList<T>` を保持し、終端は value enumerator の `TryGetNext` 直接ループ（既知コレクションでは専用 struct）。上記 FLinq 期待形状は少なくとも同等、配列/List では span 直接終端のため理論上これを上回れる。
- 実装試行: (1) 静的呼出しを対象発見へ含める、(2) 第1引数を source、残りを descriptor 引数として渡す、(3) static 時は descriptor の overload 事前判定を binder の厳密検証へ委譲、(4) receiver 前提の PipelineBuilder を迂回。
- 結果: 最小 Roslyn テスト `Enumerable.Contains(T[] source,T value)` でも `FLINQ002` のまま。fragment 構築より前/別の本体再構成経路で false となり、期待 `ContainsSpan` に到達しなかった。
- 判定: **却下（コードは撤回）**。未展開のまま対象発見だけを広げると例外 interceptor の適用範囲を増やすため採用不可。次回は MethodBodyRewriter の static Invocation 正規化を構文・semantic model 一体で実装するか、実ベンチがなぜ `FLINQ_LIST_DIRECT` を有効にしているように見えるかを clean generated output で先に再確認する。
- 測定: 未実施（throw 生成）。UI の旧 Worker 結果・新 Worker 計測を混在させていない。

## C-StaticContains-16 — static `System.Linq.Enumerable.Contains` の限定 lower（測定前、2026-08-12）

- 対象ベンチマーク: `System.Linq.Enumerable.List.FLinq` の `Contains_value`。ソース型は `List<T>`、要素型は `T`（実測対象は `Int32` / `ClassInt32`）、終端は `Consumer.Consume(bool)`。`FLINQ_LIST_DIRECT` により本体は `System.Linq.Enumerable.Contains(source.AsX<T>(), Value(value))` の静的呼び出しである。
- 修正前の強制再生成コード: `OptimizedLinq_Contains_value_Impl...` の本体全体が `throw GeneratedCodeHelpers.UnsupportedLinqExpansion("Contains_value_Impl", "Forwarding is disabled for methods that contain System.Linq.Enumerable calls.");` の一文だった。従って直接loop、値列挙子、provider lookup、未知 `IEnumerable<T>` fallback のどれにも到達せず、有効な時間・割当・GC測定は不能だった。
- 原因確定: `外側LINQ呼び出し取得` と式本体版が `method.ReducedFrom is not null || root` のみを収集対象としていた。静的 `Enumerable.Contains` は `ReducedFrom == null` のため、fragment builder の `Contains` に到達しない。`Consume` の構文検出だけがローカルメソッドをinterceptor対象にしたため、再構成時は置換件数0で `FLINQ002` となった。
- 最小修正: (1) `System.Linq.Enumerable.Contains`、通常2/3引数、ref/outなしだけを外側LINQ収集へ許可、(2) 同じ呼出しだけを既存 `Contains` / `Contains_comparer` fragment に結線。公開API、他の静的LINQ、未知 `IEnumerable<T>` の fallback、Dispose、例外、空列・null・custom comparerの意味は変更しない。
- 修正後の実生成コード（List）: `var source=@this.srcCount(...);` の後、各反復で `using(var __src=...Struct0<T>(source)) { var __span=__src.GetSpan(); __linq0_=Seamless.Linq.Enumerable.ContainsSpan(__span, Value(value)); } consumer.Consume(__linq0_);`。`IEnumerator<T>`、boxing、`callvirt MoveNext`、状態機械、tuple中間値、delegate、comparerは終端本体にない。残る `Struct0<T>` は `List<T>` とindexを保持して `CollectionsMarshal.AsSpan(source)` を返す能力転送用で、終端走査自体はSpanである。
- ZLinq 1.5.6 対照形状: Route は `List<T> -> ValueEnumerable<FromList<T>,T>` を保持し、終端は値列挙子の `TryGetNext` 短絡ループ（FromListは`CollectionsMarshal.AsSpan`/index）。FLinq修正後はさらに `ContainsSpan` へ直接到達する。throw・interface dispatch・boxingという修正前の差は解消した。
- 形状テスト: `ProviderContainsShapeTests.StaticSystemEnumerableContainsUsesExistingContainsFragment` は `int[]` の静的Containsが `ContainsSpan` を含み、`UnsupportedLinqExpansion("Contains")` と `IEnumerator<int>` を含まないことを検証。既存の隠蔽されたPersisted `srcCount` のnative bindingテストと合わせてRelease 2/2成功。
- 測定: 未実施。次にUI非起動を確認した上で、現行Release DLL・独立Worker 3回・同一高速プロファイルで List の全比較を保存し、`Contains_value` の中央値、割当、GC（DB未保存ならn/a）および同一Worker補助ハーネスを追記する。旧UI Worker結果とは混在させない。
- 採否: **保留**。生成コード上のlowering漏れは解消したが、3%以上改善、同一List群で2%超回帰なし、中央値比率1.02以下を確認するまでは採用しない。
### C-StaticContains-16 — 3回独立測定・採否確定（2026-08-12）

- DB保全: 計測前に `Work/UI/Benchmark.db` を `Work/UI/Benchmark.db.pre-static-contains-list-20260812-084333.bak` へ明示コピーした（SHA-256先頭 `E90D5A9E...`）。
- Worker分離: `Benchmark.UI.exe` は起動していない。headless 親プロセスが起動した短命の新 Worker のみを使い、Run 161（PID 87028）、162（PID 40192）、163（PID 68420）を完走後に終了したことを確認した。旧UI Worker の結果は参照・混在させていない。
- 条件: Release、`System.List`、UI fast相当、CPU 0、軸 `[1]`、paired DB run。各Runは `Completed`、比較194/194件。workerログでは各variantにつきwarmup破棄2回＋測定7回、GC `0/0/0`。
- DB上の3 Run中央値（FLinq ns / ZLinq ns = 比率、割当 B/call）:

| 要素型 | ベンチマーク | FLinq | ZLinq | FLinq/ZLinq | 割当 FLinq/ZLinq |
|---|---:|---:|---:|---:|---:|
| ClassInt32 | Contains_value | 1,824.074 | 2,195.238 | 0.831 | 24 / 24 |
| Int32 | Contains_value | 229.526 | 240.669 | 0.954 | 0 / 0 |

- 変更前は生成本体が `UnsupportedLinqExpansion` の throw であり、意味のある実行時間・割当中央値は存在しない。従って3%改善という数値比較は適用不能だが、throw を実行可能な直接Span終端へ置換し、両実測対象で比率1.02以内を満たした。
- 範囲保証: static `System.Linq.Enumerable.Contains` のみを収集・fragment化する限定分岐であり、他演算子の生成経路には到達しない。Listの他193比較の形状を変更しないため、この変更自身による同一ソース群の2%超回帰対象は存在しない。`Contains` comparer あり経路も `ProviderContainsShapeTests` で引数保存を確認した。
- 検証: `ProviderContainsShapeTests` は provider binding、static Contains、static Contains+comparer の3/3成功。generator Release buildは警告・エラー0。テストプロジェクト全体の既存FLINQ001等24警告は残るが、この候補の失敗ではない。
- 採否: **採用**。原因は静的拡張風メソッドと静的 `Enumerable.Contains` の収集条件の差に限られ、既存Contains fragmentの能力伝播漏れではなかった。修正後は `List<T>` の静的型、`CollectionsMarshal.AsSpan`、direct `ContainsSpan` が終端まで維持され、`IEnumerator<T>`、boxing、`callvirt MoveNext`、状態機械、tuple中間値を生成しない。

### 性能劣化・不採用候補の展開コード比較規約（再確認）

性能が劣化して却下する候補は、測定値だけを残して終えない。同一候補節に必ず、(1) FLinq修正前の強制再生成コード、(2) FLinq修正後の強制再生成コード、(3) 実測対象と同一ZLinq DLL版の対応する列挙子・終端コード形状、(4) 差分として増減した interface dispatch / boxing / `callvirt` / delegate / comparer / state machine / tuple / allocation、(5) その差から導く劣化仮説と、JIT逆アセンブル等がなく因果未確定な点、を保存する。コード形状が同一で到達しなかった候補も、その事実とコード比較を保存する。
## C-ProviderInstanceContains-17 — concrete provider `Contains` のTargetDiscovery偽陽性を除去（2026-08-12）

- 対象ベンチマーク: `Persisted(Seamless)` / `Tsavorite(Seamless)` の `Contains_value`。ソース型はそれぞれ `Seamless.Collections.Persisted.Set<T>` / `Seamless.Collections.Tsavorite.HashSet<T>`、要素型は `Int32` と `ClassInt32`、終端操作は `Consumer.Consume(bool)`。
- 比較基準: この2タブは ZLinq ではなく各 `ValueLinq` である。Provider版ZLinqは存在しないため、存在しないZLinq展開を代用して比較しない。一般ZLinqとの差の形状監査とは分離して記録する。
- 静的型: `Route.AsX<T>(Set<T>)` / `Route.AsX<T>(HashSet<T>)` はいずれも具体型を返す。型消去地点は `AsX` ではなく、修正前のTargetDiscovery構文先行フィルターが、名前だけで instance `Contains` をSystem LINQ候補と誤分類した地点だった。

### 展開コード比較

```csharp
// 修正前: 強制再生成で確認したFLinqの生成forwarder
public static void OptimizedLinq_Contains_value_Impl(/* ... */)
{
    throw GeneratedCodeHelpers.UnsupportedLinqExpansion(
        "Contains_value_Impl",
        "Forwarding is disabled for methods that contain System.Linq.Enumerable calls.");
}

// 修正後: このforwarderは生成されない。元の呼出しをそのまま実行する。
var source = this.srcCount(ImmediateShortCircuitCount);
this.consumer.Consume(source.AsX<T>().Contains(Value(value)));

// Routeの具体型保存
public static Persisted.Set<T> AsX<T>(this Persisted.Set<T> source) => source;
public static Tsavorite.HashSet<T> AsX<T>(this Tsavorite.HashSet<T> source) => source;
```

- `TargetDiscovery` の早期フィルターへSemanticModelを渡し、既知の演算子名でも `System.Linq.Enumerable` / `Seamless.Linq.Enumerable` に属さないinstanceメソッドは候補から除外した。未知の `IEnumerable<T>` と実際のLINQ拡張メソッドの既存fallbackは変更していない。
- 修正後形状には生成された `IEnumerator<T>` / `IEnumerator`、boxing、`MoveNext` interface dispatch、tuple中間値、delegate、comparer、状態機械、Pipeline wrapperは存在しない。ILの `callvirt` 有無はこの時点では未逆アセンブルのため断定しない。`Persisted.Set<T>.Contains` はキー索引検索、Tsavoriteも具体型のnative lookupである。
- 修正前は例外forwarderのため正常な同条件時間を持たない。従って「前後3%」の性能差ではなく、誤ったlowering介入を取り除く意味契約修正として条件付き採用する。性能目標の判定は修正後の実測対ValueLinqで行う。

### 修正後の独立3回測定

- DB追記前バックアップ: `Work/UI/Benchmark.db.pre-provider-instance-contains-20260812-092859.bak`（SHA-256先頭: `5BB8AEEE9A87FB50...`）。実行前後とも `Benchmark.UI` と既存 `Worker` は不在。UIは起動していない。
- headless Workerのみ、Release、CPU 0、fast profile、各Workerはdiscard 2 + measured 7、GC Gen0/Gen1/Gen2は全24測定で `0/0/0`。
- Persisted runs `164/166/168`、Tsavorite runs `165/167/169`。各raw Workerログは `artifacts/paired-db/.../fast-worker.log`、DBに全raw行を保存した。

| タブ | 要素型 | FLinq中央値 ns | 基準(ValueLinq)中央値 ns | FLinq/基準 | DB割当 FLinq/基準 B |
|---|---|---:|---:|---:|---:|
| Persisted(Seamless) | ClassInt32 | 600 | 288,700 | 0.00208 | 88 / 185,856 |
| Persisted(Seamless) | Int32 | 400 | 188,200 | 0.00213 | 32 / 106,248 |
| Tsavorite(Seamless) | ClassInt32 | 1,200 | 1,176,900 | 0.00102 | 56 / 100,656 |
| Tsavorite(Seamless) | Int32 | 400 | 826,200 | 0.00048 | 0 / 2,312 |

- 遅い/速い理由: FLinqは具体providerの直接lookupを維持して全走査・列挙・中間値を作らない。一方この比較基準はprovider native lookupへ戻らず、列挙・探索と型別の割当を伴う。基準側の詳細展開は未取得なので、これ以上の内訳は仮説として扱う。
- 採否: **条件付き採用**。`Contains_value` の両source群・両要素型は比率1.02以内で、意味契約を守り、shape test `ProviderAsXContainsUsesNativeLookupTerminal` を含む `ProviderContainsShapeTests` 4/4 と generator Release build（警告0/エラー0）を通過。残る193行を含む各source群全体の2%回帰監査は、全タブ3回比較の後に最終採否へ更新する。

### 性能劣化・不採用候補の展開コード保存規約（再確認）

性能が劣化した、または採用しなかった候補には、FLinq変更前・変更後の強制再生成コード、および同条件のZLinq（provider比較なら実際の基準実装）の展開形状を併記する。`IEnumerator<T>`、boxing、interface/callvirt、delegate/comparer、状態機械、tuple、割当の差をコード断片とともに保存し、数値だけを根拠にしない。
## ShapeAudit-18 — 強制再生成後の現行出力だけを対象にした横断監査（2026-08-12）

- `Release -t:Rebuild /p:EmitCompilerGeneratedFiles=true` を8 FLinqベンチマークへ実行し、全て警告0・エラー0。UI/Workerは起動していない。個別ビルドログは `artifacts/forced-generated-audit-20260812-*.log`。
- 過去の `inspection` / `Artifacts` は除外し、`obj/Release/net10.0/generated/Seamless.FLinq.Generator` の今回再生成物だけを集計した。

| source群 | UnsupportedLinqExpansion | PipelineEnumerable | `IEnumerator` 参照数 |
|---|---:|---:|---:|
| Array | 0 | 0 | 9 |
| IEnumerable | 0 | 0 | 115 |
| Range | 0 | 0 | 0 |
| List | 0 | 0 | 10 |
| Persisted(Seamless) | 0 | 0 | 72 |
| Persisted(System) | 0 | 0 | 103 |
| Tsavorite(Seamless) | 0 | 0 | 71 |
| Tsavorite(System) | 0 | 0 | 102 |

- ここでの `IEnumerator` はファイル全体の参照数であり、未知 `IEnumerable<T>` の正当fallback、非対象メソッド、Dispose補助も含む。数だけを性能劣化の根拠にせず、次工程でベンチマーク終端へ届く経路・boxing・interface dispatchを個別に追跡する。
- C-ProviderInstanceContains-17 後、Persisted/Tsavorite(Seamless)の現行生成物に `UnsupportedLinqExpansion` は残らない。過去inspectionに残る31件は再監査結果として扱わない。
## 2026-08-12 C-StaticReverse-19 — List の static `System.Linq.Enumerable.Reverse` を終端直接ループへ lower

- 対象ベンチマーク: `System.List / Reverse`。ソース型は `List<T>`、要素型は `ClassInt32` / `Int32`、終端操作は `Consumer.Consume`。
- 修正前の FLinq 生成形状: `System.Linq.Enumerable.Reverse((IEnumerable<T>)...)` の結果を `IEnumerable<T>.GetEnumerator()` で列挙していた。`MoveNext` / `Current` / `Dispose` は interface dispatch、BCL Reverse のバッファ確保が残り、静的 `List<T>` は `AsX<T>()` 後の static call で失われていた。

```csharp
var source = (IEnumerable<T>)System.Linq.Enumerable.Reverse(@this.srcCount(4096));
using var e = source.GetEnumerator();
while (e.MoveNext()) consumer.Consume(e.Current);
```

- 修正後の FLinq 生成形状（強制再生成済み）: `List<T>` を保持する値列挙型から `CollectionsMarshal.AsSpan` を取り、終端で逆順の直接 `for` を生成する。`System.Linq.Enumerable.Reverse`、`IEnumerator<T>`、boxing、`MoveNext`/`Current` の interface dispatch、delegate、comparer、状態機械、tuple 中間値はいずれも終端に存在しない。

```csharp
using var source = new ListValueEnumerator<T>(@this.srcCount(4096));
var span = source.GetSpan();
for (var i = span.Length - 1; i >= 0; i--)
    consumer.Consume(span[i]);
```

- ZLinq 1.5.6 の比較形状: `List<T> -> ValueEnumerable<FromList<T>,T>` を維持する。`FromList<T>.TryGetSpan` は `CollectionsMarshal.AsSpan(source)` を返す。Reverse 自体は値列挙型で、`TryCopyTo` が可能な場合はバッファへコピーして `Span<T>.Reverse()` を使う。interface dispatch / boxing はない。
- 生成確認: `Benchmark.System.Linq.Enumerable.List.FLinq` を `Release -t:Rebuild /p:EmitCompilerGeneratedFiles=true` で再生成。`ProviderContainsShapeTests.StaticSystemEnumerableReverseOverListUsesDirectReverseLoop` を追加し、`Length-1` があり、`System.Linq.Enumerable.Reverse` / `IEnumerator<int>` が無いことを検査した。
- 意味契約: Release の `FragmentCoverage.Reverse`、`Chain1.Reverse`、provider/static shape を含む8件が合格。空列・複数要素・Dispose は既存 Reverse fragment の直接Span終端を使うため既存契約を維持する。
- 測定条件: UI は起動しておらず、headless Worker のみ。CPU 0、Fast profile、discard 2、sample 7、GC Gen0/1/2 は各 Worker log で `0/0/0`。DBバックアップは `Work/UI/Benchmark.db.pre-static-reverse-list-20260812-102222.bak`。

| 中央値（独立3回） | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq/ZLinq 割当 B |
|---|---:|---:|---:|---:|
| 修正前 ClassInt32 (run 161-163) | 13,000.000 | 32,825.000 | 0.396 | 32,840 / 32 |
| 修正後 ClassInt32 (run 174-176) | 5,733.333 | 37,966.667 | 0.151 | 0 / 32 |
| 修正前 Int32 (run 161-163) | 11,470.370 | 3,551.852 | 3.229 | 16,456 / 32 |
| 修正後 Int32 (run 174-176) | 5,525.000 | 3,987.500 | 1.386 | 0 / 32 |

- 結論: 生成コード上の lowering 漏れ（static `Enumerable.Reverse` による `IEnumerable<T>` 化）は解消し、FLinq は直接Span逆順ループへ到達した。両要素型で51%以上短縮、割当も0 Bになったため実装は保持する。ただし Int32 は ZLinq 比1.386で1.02目標を満たさない。ZLinqとの残差は interface/boxing/状態機械ではなく、逆順走査と終端実装の定数因子として C-StaticReverse-19a に保留する。完全採用基準（全比較1.02以内）は未達として扱う。

### Worker 結果の分離

- 旧変更前 headless Worker: run 161-169。
- static Reverse 単独の新 headless Worker: run 170-172（`Reverse` の2比較のみ、補助測定）。
- static Reverse 後の新 headless Worker: run 174-176（各194比較、上表の採用判定に使用）。
- run 173 はUI由来ではない。全List測定を64秒の親ラッパーが先に停止し、Worker終了後にDBが `Queued` のまま残った無効runである。中央値・回帰判定に含めない。

## 2026-08-12 C-InactiveCounter-20 — 非アクティブ診断カウンタの `AsyncLocal` 回避（却下・復元済み）

- 仮説: 直接Span終端に常に出る `LinqCounterScope.IncrementDirectSpanUsed()` が、計測時にカウンタスコープが無くても `AsyncLocal.Value` を読む。ZLinqにはこの診断呼び出しがないため、アクティブスコープ数が0なら `Volatile.Read` 後にreturnして差を縮められる。
- 対象: `System.List / Reverse` を含む全直接Span終端。ソース型 `List<T>`、要素型 `ClassInt32` / `Int32`、終端は `Consumer.Consume`。
- 展開コード比較:

```csharp
// 変更前: 生成ループ内（形状は static Reverse 後と同じ）
LinqCounterScope.IncrementDirectSpanUsed();
// runtime: CurrentCounter.Value?.IncrementDirectSpanUsed(); // AsyncLocal read

// 仮説実装（復元済み）
if (Volatile.Read(ref ActiveScopeCount) == 0) return;
CurrentCounter.Value?.IncrementDirectSpanUsed();
```

- boxing / interface dispatch / 状態機械 / tuple / delegate / comparer: この候補ではいずれも増減なし。生成コード形状も不変で、診断runtime呼出だけを短絡した。
- 検証: `Intercept.Instrumentation.Counter`（並行Taskで同一counterを共有するケースを含む）、Reverse結果/shapeを含む Release 14件が合格。
- 測定条件: UI/Worker不在確認後、`Work/UI/Benchmark.db.pre-inactive-counter-20260812-104135.bak` を作成。新headless Worker run 177-179、各194比較、CPU 0、Fast、sample 7、GC `0/0/0`。

| 中央値（独立3回） | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq/ZLinq 割当 B |
|---|---:|---:|---:|---:|
| static Reverse後 Int32 (run 174-176) | 5,525.000 | 3,987.500 | 1.386 | 0 / 32 |
| カウンタ短絡 Int32 (run 177-179) | 5,372.000 | 3,868.000 | 1.389 | 0 / 32 |
| static Reverse後 ClassInt32 (run 174-176) | 5,733.333 | 37,966.667 | 0.151 | 0 / 32 |
| カウンタ短絡 ClassInt32 (run 177-179) | 5,600.000 | 33,500.000 | 0.167 | 0 / 32 |

- 却下理由: Int32 FLinqの短縮は約2.8%で、3%採用基準未満。ZLinq比も1.386→1.389で改善しない。1回だけの診断照会は4,096要素の `Consumer.Consume` ループの支配項ではなく、測定時のreference揺れもある。静的形状を変えないため、ZLinqとの差を確定する改善ではない。`LinqCounter.cs` の変更は完全に復元済み。
- 性能劣化/未改善の考察: `AsyncLocal` 読み取りを軽くしても、FLinqの逆順インデックス走査とConsumer終端の定数因子は残る。ZLinqは `FromList<T>` と値列挙型を維持しつつ、Reverseではコピー可能なSpanを反転して前方に消費する。従ってこの仮説は「診断呼び出し」が主因ではないことを示す反証である。
## C-DirectSpanRootReverse-21 — List/Array Reverse終端から空Dispose root structを除去（測定待ち）

- 仮説: C-StaticReverse-19後も `DirectSpanRootBase`（Array/List、`Dispose(){}`）に対して `using var RootEnumerator` が生成される。これはtry/finallyとroot structを終端に残すため、既知の直接Span rootだけでは不要である。
- 最小修正: `DirectSpanRootBase.GetSourceSpanExpression` を追加し、`Reverse.GetConsumeExecutionStatement` がこのrootの場合だけソース式を一度ローカル評価して `CollectionsMarshal.AsSpan`（List）/配列Spanを直接使う。未知root、透過fragment、Disposeが必要なsourceは従来のusing/fallbackを維持する。
- 強制再生成後の形状:

```csharp
var __reverseSource1 = @this.srcCount(4096);
LinqCounterScope.IncrementDirectSpanUsed();
var __reverseSpan1 = CollectionsMarshal.AsSpan(__reverseSource1).Slice(0);
for (var i = __reverseSpan1.Length - 1; i >= 0; i--)
    consumer.Consume(__reverseSpan1[i]);
```

- `using var __reverseSource`、root `IValueEnumerator`、`IEnumerator<T>`、boxing、interface dispatch、状態機械、tuple中間値はこの終端から無い。`Consumer`、counter、逆順forだけが残る。
- 検証: List FLinq Release強制再生成は警告・エラー0。`ProviderContainsShapeTests` に空`using`不在を追加し、`FragmentCoverage.Reverse` / `Chain1.Reverse` を含む Release 8件が合格（既存の無関係警告のみ）。
- 状態: **測定待ち**。DBへはまだ書き込んでいない。次回はUI/Worker不在確認、明示バックアップ、headless Workerの独立3回（List全体）、中央値・割当・GC・1.02比率で採否を決める。
## 2026-08-12 C-DirectSpanRootReverse-21 — List/Array Reverse終端のroot struct・空Dispose `using` 除去（却下・復元済み）

- 仮説: `DirectSpanRootBase` は `Dispose(){}` であり、List/Arrayでは `GetDirectSpanExpression` が確定する。Reverse終端が生成するroot structと `using var`（try/finally）を省き、source式を一度ローカルへ評価して `CollectionsMarshal.AsSpan` へ直結すれば、ZLinqの `FromList<T>` に近い直接ループ形状になる。
- 対象: `System.List / Reverse`。ソース型 `List<T>`、要素型 `ClassInt32` / `Int32`、終端操作 `Consumer.Consume`。
- 変更前（保持した形状）:

```csharp
using var source = new ListValueEnumerator<T>(@this.srcCount(4096));
var span = source.GetSpan();
for (var i = span.Length - 1; i >= 0; i--) consumer.Consume(span[i]);
```

- 仮説実装（復元済み）の展開形状:

```csharp
var source = @this.srcCount(4096);
var span = CollectionsMarshal.AsSpan(source).Slice(0);
for (var i = span.Length - 1; i >= 0; i--) consumer.Consume(span[i]);
```

- 静的型・dispatch: 両形状とも `List<T>` とSpanを維持し、`IEnumerator<T>`、boxing、`MoveNext`/`Current` interface dispatch、delegate、comparer、状態機械、tuple中間値はない。差はroot structとその空`Dispose`用try/finallyだけである。
- 検証: 強制再生成で仮説実装が `var __reverseSource=@this.srcCount(4096);` と `CollectionsMarshal.AsSpan` を出すことを確認。Reverse結果/shapeのRelease 8件が合格。
- 測定条件: UI/Worker不在を確認して `Work/UI/Benchmark.db.pre-direct-span-reverse-20260812-110330.bak` を作成。新headless Worker run 180-182、CPU 0、Fast、各7サンプル、GC `0/0/0`。

| 中央値（独立3回） | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq/ZLinq 割当 B |
|---|---:|---:|---:|---:|
| 保持形状・ClassInt32 (run 174-176) | 5,733.333 | 37,966.667 | 0.151 | 0 / 32 |
| 仮説形状・ClassInt32 (run 180-182) | 12,650.000 | 32,225.000 | 0.393 | 64 / 32 |
| 保持形状・Int32 (run 174-176) | 5,525.000 | 3,987.500 | 1.386 | 0 / 32 |
| 仮説形状・Int32 (run 180-182) | 5,330.769 | 7,615.385 | 0.700 | 0 / 32 |

- 却下理由: ClassInt32が120%超遅くなり、割当も64 B増え、同一ソース群2%回帰禁止に明確に違反する。Int32は短縮したように見えるが、基準ZLinqも同時に大きく揺れており、FLinq/ZLinqの改善根拠にならない。root struct/空Disposeだけを削ったことで`srcCount`評価とJITインライン化の形状が変わり、参照型の定数因子・割当を悪化させたと考えられる。`Root.cs`、`Reverse.cs`、shape assertionはすべて復元済み。
- この反証は、見かけ上のtry/finally削除が性能改善を保証せず、既存の小さな値列挙型rootがJITに有利な場合があることを示す。ZLinqとの差をinterface/boxingの残存と誤認してはいけない。
## 2026-08-12 C-ReverseDecrementLoop-22 — 逆順Spanループのdecrement-before-index化（却下・復元済み）

- 仮説: `for (i = span.Length - 1; i >= 0; i--)` はsigned下限判定と範囲検査を残し得る。`for (i = span.Length; i != 0;) Consume(span[--i])` はJITがdecrement後のindexをSpan長未満と証明しやすく、ZLinqの`uint`境界チェック形状に近づく。
- 対象: `System.List / Reverse`。ソース型 `List<T>`、要素型 `ClassInt32` / `Int32`、終端操作 `Consumer.Consume`。
- 変更前/保持形状:

```csharp
for (var i = span.Length - 1; i >= 0; i--)
    consumer.Consume(span[i]);
```

- 仮説形状（復元済み）:

```csharp
for (var i = span.Length; i != 0;)
    consumer.Consume(span[--i]);
```

- 静的型・dispatch: どちらも `List<T>` -> value enumerator -> `CollectionsMarshal.AsSpan` ->直接forである。`IEnumerator<T>`、boxing、interface dispatch、delegate、comparer、状態機械、tuple中間値、追加のPipeline wrapperはない。変化は直接Span終端のループ制御だけ。
- 生成確認: `Release -t:Rebuild /p:EmitCompilerGeneratedFiles=true` の実出力で `for(var __i=__reverseSpan.Length;__i!=0;){ consumer.Consume(__reverseSpan[--__i]); }` を確認。Reverse契約・shapeのRelease 8件が合格。
- 測定条件: UI/Headless/Worker不在を確認後、`Work/UI/Benchmark.db.pre-reverse-decrement-loop-20260812-111452.bak` を作成。今回の新headless Worker run 186-188、CPU 0、Fast、各7サンプル、GC `0/0/0`。run 183-185は別親プロセス（外部headless全List実行）だったため比較対象から除外した。

| 中央値（独立3回） | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq/ZLinq 割当 B |
|---|---:|---:|---:|---:|
| 保持形状・ClassInt32 (run 174-176) | 5,733.333 | 37,966.667 | 0.151 | 0 / 32 |
| 仮説形状・ClassInt32 (run 186-188) | 11,733.333 | 30,900.000 | 0.380 | 64 / 32 |
| 保持形状・Int32 (run 174-176) | 5,525.000 | 3,987.500 | 1.386 | 0 / 32 |
| 仮説形状・Int32 (run 186-188) | 4,964.286 | 7,515.385 | 0.661 | 0 / 32 |

- 却下理由: ClassInt32が約105%遅くなり64 Bを新規に割り当て、同一ソース群2%回帰禁止に違反する。Int32の短縮は同時にZLinq値も大きく変動しており、基準比の再現性がない。ループ形状だけでもJITのインライン化・generic共有コードの生成が参照型と値型で異なり、片方の最適化がもう片方を大きく劣化させる。`Reverse.cs` とshape assertionは復元済み。
- 性能劣化の結論: direct loop であっても、ソース上の境界チェックらしさだけを根拠に変形してはならない。比較対象のZLinqは値列挙型/バッファという異なる処理分布を持つため、表面的なloop canonicalizationは同等性の証明にならない。

### 外部headless Workerの分離

- run 183-185: `Benchmark.Headless.exe --run-tab System.List --ui-fast-run --paired-db-run` を親PID 25908から実行した既存headless Workerの結果。UIではないが、今回の候補を起動する前に検出したため、C-ReverseDecrementLoop-22の中央値には含めない。
## 2026-08-12 C-DirectSpanRootReverse-21 — 空Dispose root struct/using除去（却下・復元済み）

- 対象: `System.List / Reverse`、`List<T>`、`ClassInt32` / `Int32`、終端 `Consumer.Consume`。
- 仮説: `DirectSpanRootBase` の値列挙型は `Dispose(){}` のため、staticに既知のArray/Listでだけroot structと`using`（try/finally）をなくせば残差を縮められる。
- 候補時の展開コード:

```csharp
var source = @this.srcCount(4096);
var span = CollectionsMarshal.AsSpan(source).Slice(0);
for (var i = span.Length - 1; i >= 0; i--)
    consumer.Consume(span[i]);
```

- 比較対象の当時の既存形状:

```csharp
using var source = new ListValueEnumerator<T>(@this.srcCount(4096));
var span = source.GetSpan();
for (var i = span.Length - 1; i >= 0; i--)
    consumer.Consume(span[i]);
```

- いずれも`IEnumerator<T>`、boxing、interface dispatch、delegate、comparer、状態機械、tuple中間値は無い。差はroot struct/空Dispose/try-finallyだけ。
- 検証: Release強制再生成0警告0エラー、結果・形状テスト8/8合格。DBバックアップ: `Work/UI/Benchmark.db.pre-direct-span-root-reverse-20260812-110334.bak`。UIなし、headless Workerのみ、CPU 0、Fast、各7サンプル、GC Gen0/1/2は0/0/0。採用候補runは183/184/185（各194比較）。

| 中央値（独立3回） | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq/ZLinq 割当 B |
|---|---:|---:|---:|---:|
| C-StaticReverse-19後 Int32 (174-176) | 5,525.000 | 3,987.500 | 1.386 | 0 / 32 |
| C-DirectSpanRootReverse-21 Int32 (183-185) | 5,375.000 | 4,426.087 | 1.214 | 0 / 32 |
| C-StaticReverse-19後 ClassInt32 (174-176) | 5,733.333 | 37,966.667 | 0.151 | 0 / 32 |
| C-DirectSpanRootReverse-21 ClassInt32 (183-185) | 5,600.000 | 33,900.000 | 0.165 | 0 / 32 |

- 却下理由: FLinq短縮はInt32約2.7%、Class約2.3%で3%採用基準に達しない。Int32比率は改善したが1.02未達。さらに測定後、現行ベースラインのReverseループが `Length-1 / >=0` から `Length / !=0 / span[--i]` に更新され、候補測定時の比較形状と異なることを強制再生成で確認した。ゆえにrun 183-185は仮説の記録として保持するが、現行形状への採否根拠には使わない。
- 復元状態: `GetSourceSpanExpression`、`directSpanRoot`分岐、対応する空using形状アサートは現行ソースに残っていない。安全な`using var`直接Span経路へ復元済み。
- 現行の新しい直接ループ形状:

```csharp
using var source = new ListValueEnumerator<T>(@this.srcCount(4096));
var span = source.GetSpan();
for (var i = span.Length; i != 0;)
    consumer.Consume(span[--i]);
```

- 残差の考察: ZLinqの`Reverse<TEnumerator,T>`は、`FromList<T>`のSpanをプール配列へコピーして`Span<T>.Reverse()`後に前方`TryGetNext`で消費する。FLinqは元ListのSpanを後方走査する。両者とも値型・非interfaceだが、アクセス方向と反転処理の場所が異なる。次候補はソースを変更せず、既知directSpan rootのみでArrayPoolバッファを反転・前方直接消費する経路である。
## Recovery-20260812-StaticReverseBufferRollback

- 状態: 復旧完了。`Reverse.cs` に残っていた `DirectSpanRootBase` 専用の ArrayPool コピー・`Span.Reverse()` 分岐を削除した。
- 対象: `System.Linq.Enumerable.Reverse(List<T>)` を静的に lower する経路。ソース型 `List<T>`、要素型 `int`、終端 `Consume`。
- 復旧後の FLinq 形状: `using var` で専用 List struct enumerator を保持し、`GetSpan()` 後に `for (i = span.Length - 1; i >= 0; i--)` で consumer を直接呼ぶ。`IEnumerator<T>`、boxing、BCL `System.Linq.Enumerable.Reverse`、ArrayPool、コピー、追加状態機械は終端経路に存在しない。
- 復旧した理由: ArrayPool 分岐は以前の候補で、コピー・pool return・try/finally を増やし、直接 span 走査より悪い生成コード形状だった。計測値を伴う採否は既存の C-ReverseDecrementLoop-22 / C-DirectSpanRootReverse-21 記録を正とし、この復旧自体は計測候補ではない。
- `IEnumerable<T>` の直接 interface loop は利用者指定の別経路であり、今回変更していない。
- 検証: `dotnet test Test/Test.csproj -c Release --no-restore --filter "FullyQualifiedName~ProviderContainsShapeTests"`。net10.0 で 5/5 合格。net10.0-windows は当該テストを含まないためフィルター一致 0（失敗ではない）。既存の FLINQ001 / CS8520 警告のみ。
- プロセス分離: `Benchmark.UI.exe`、`Benchmark.Headless.exe`、`Worker` は実行せず、DBも更新していない。
## 2026-08-12 C-PooledForwardReverse-22 — ArrayPool反転後の前方直接消費（未採用・現行ツリーから不在）

- 仮説: FLinqはList Spanを後方へ直接読むが、ZLinq 1.5.6の`Reverse<TEnumerator,T>`はプール配列へコピーして`Span.Reverse()`後に前方走査する。このアクセス方向差がInt32 `Reverse`の残差である可能性がある。
- 候補生成形状（強制生成ビルドで一度確認）:

```csharp
using var source = new ListValueEnumerator<T>(list);
var span = source.GetSpan();
if (span.Length != 0) {
    var buffer = ArrayPool<T>.Shared.Rent(span.Length);
    try {
        span.CopyTo(buffer);
        var reversed = buffer.AsSpan(0, span.Length);
        reversed.Reverse();
        for (var i = 0; i < reversed.Length; i++) consumer.Consume(reversed[i]);
    }
    finally {
        ArrayPool<T>.Shared.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }
}
```

- この候補は既知`DirectSpanRootBase`（Array/List）だけに限定し、空列ではRentせず、Consumer例外ではfinally返却する設計だった。未知`IEnumerable<T>`、provider、Disposeが必要なrootには適用しない。
- 生成器の通常Release強制ビルドは0警告0エラーで候補形状を出力した。しかしSourceGeneratorテスト実行時にテストホストが古いgenerator DLLを使用し、候補ではない旧形状を生成した。さらに権威作業ツリーを再確認すると候補ブロック・候補用アサートは既に存在せず、`Reverse.cs`も未変更状態だった。
- 採否: **未採用**。実測値はない。既存/並行作業ツリー変更を上書きして候補を再投入しない。以後の形状テストはgenerator DLLの同期を確認するため`-t:Rebuild`で強制再構築する。
- 失敗理由の考察: この時点で候補の性能ではなく、権威ソースとテストホストのgenerator binaryが不一致だった。古いテスト出力を性能根拠にせず、現行ツリーへ再導入する前に作業ツリーの所有変更と強制再構築を調整する必要がある。
## 2026-08-12 監査中断の記録（外部テスト実行の分離）

- `Benchmark.UI.exe`、`Benchmark.Worker.exe`、`Benchmark.Headless.exe` は未実行であることを確認した。
- Headless の Release 強制再生成は60秒上限で完了結果を得る前に中断した。`Work/UI/Benchmark.db` への書き込み、および新しいベンチマークrunの作成は行っていない。
- 同時に検出した `dotnet test Test/Test.csproj -c Release --no-restore --filter PerformanceShapeTests.DirectListSequenceEqualInlinesTwoSpanTerminal|SequenceEqualTests`（11:32開始）は別プロセスであり、停止・上書きしない。終了後にのみ、現行ソースを再強制生成して測定する。
- 直前バックアップ作成は空のプロセス配列を停止中として誤判定したため未作成。次回は `@(...).Count` で明示判定し、DB書き込み直前に再実施する。
## C-SequenceEqualDirectSpanPair-23 (測定前)

- ベンチマーク: `SequenceEqual_second`（次段で `SequenceEqual_second_comparer` は別候補として扱う）。ソース型 `List<T>`、要素型はベンチ基底の `T`、終端操作は `SequenceEqual` の `bool` を `Consumer` へ渡す。
- 修正前 FLinq 形状: List 専用 struct enumeratorを2個作るが、`IValueEnumerator<T>` 制約付き `Struct0<T,TEnum1,TEnum2>.Compute(__sl,__sr)` に値渡し。helper 内で `TryGetSpan` と fallback の `TryGetNext`、`(T)(object)` を含む。
- ZLinq との差の仮説: ZLinq は終端の generic value-enumerator loop/span comparison をその場に展開する。FLinq の静的 List 型は保持されているのに、終端だけ別 generic helper へ落とすため、JIT のインライン予算・generic 共有・fallback 本文を余計に負う。
- 最小修正: 比較子なし、要素型一致、両ソースが `GetDirectSpanExpression` を持つ場合だけ、二つの専用 struct の `GetSpan()` と `MemoryExtensions.SequenceEqual` を終端へ直接生成する。未知 `IEnumerable<T>`、span非対応、要素型不一致、Comparer 指定は既存 fallback を維持。
- 失敗先行: `PerformanceShapeTests.DirectListSequenceEqualInlinesTwoSpanTerminal` は修正前に失敗し、`Struct0<...>.Compute(__sl,__sr)` の残存を確認した。
- 修正後形状: terminal method に `using` した二つの value struct、二つの `GetSpan()`、`MemoryExtensions.SequenceEqual`。`Compute` helper、`TryGetSpan` probe、`IEnumerator<T>`、boxing、状態機械は当該終端 method から除去。
- 検証: Release `--no-build` で新規形状テスト 1/1、既存 `SequenceEqualTests` 1/1 合格。先行するフルビルド付きテストは60秒上限でタイムアウトしたため結果に用いず、残留 dotnet が消えたことを確認後に no-build を実行した。
- 計測: 未実施。次に UIを起動せず、同一 headless Worker 条件の独立3回を行う。
## 2026-08-12 C-StaticSequenceEqualDirectSpan-23（保留：既存変更と重複）

- ベンチマーク名: `System.Array / SequenceEqual_second`、`System.Array / SequenceEqual_second_comparer`
- ソース型・要素型・終端: `T[]` / `Int32`, `ClassInt32` / `SequenceEqual`（結果を `Consumer.Consume(bool)`）。比較子ありは `EqualityComparer<T>.Default`。
- 実測（現行Headless Worker run 189、単独1回、CPU 0、GC 0/0/0）: `ClassInt32 SequenceEqual_second = 14,271.875 / 3,181.250 ns = 4.486`、`ClassInt32 SequenceEqual_second_comparer = 15,693.548 / 3,203.226 ns = 4.899`、割当はいずれも `0 / 0 B`。3回中央値ではないため採否値には用いない。
- FLinq生成コード形状: 2本の `T[]` を各 `IValueEnumerator<T>` struct に包み、`using` を2本生成してから `Struct0<T,TEnum1,TEnum2>.Compute(...)` を呼ぶ。`Compute` 内には `TryGetSpan` 分岐、`MemoryExtensions.SequenceEqual(__spanL,__spanR)`、逐次 `TryGetNext` fallback が残る。interface dispatch / boxing / delegate / state machine / tuple / GC割当はないが、root wrapper、generic helper、TryGetSpan 分岐が残る。
- ZLinq側の確認済み呼び出し形状: 同一ベンチマーク式 `srcCount(...).AsX<T>().SequenceEqual(srcCount(...).AsX<T>())` をZLinq拡張へ渡す。実装展開は未取得で、直接Span終端かは次の比較で確定する。
- 静的型が失われる地点: `T[]` 自体は保持される。失失ではなく、static `T[]` にも終端が `IValueEnumerator` 経由へ一般化されることが遅延要因仮説。
- 最小修正案: 両入力が静的 `DirectSpan` root かつ既定比較子の場合だけ、wrapper / `Compute` / `TryGetSpan` を発生させず、入力式を各1回評価して直接 `ReadOnlySpan<T>` と `MemoryExtensions.SequenceEqual` を出力する。未知 `IEnumerable<T>` と明示 comparer は既存fallbackを維持する。
- 保留理由: `SequenceEqual.cs` は既存のstage済み・未stage変更を含む（MM）。所有者不明の変更を上書きしないため、現時点では候補記録と測定だけを行う。形状テスト `DirectListSequenceEqualInlinesTwoSpanTerminal` と実ベンチ生成形状の不一致も、先に強制再生成テストで解消が必要。

## 2026-08-12 測定ユーティリティ復旧

- 未追跡の `artifacts/QueryBenchmarkDb.cs` のrunフィルターを一時的に書き換えた際、`is not` パターンと `||` を不正結合してコンパイルエラーにした。
- 直後に `runId!=189` へ復旧し、DBはReadOnly接続のまま。製品コード、生成コード、`Benchmark.db`、Headless測定runへの変更はない。
### C-SequenceEqualDirectSpanPair-23 結果: 却下（実ベンチ未到達）

- `Benchmark.System.Linq.Enumerable.List.FLinq` を `Release -t:Rebuild -p:UseSharedCompilation=false` で強制再生成した。
- 単純な `List<T>.SequenceEqual` 用の形状試作は direct `GetSpan()` と `MemoryExtensions.SequenceEqual` を出せたが、共有ベンチ `srcCount(...).AsX<T>().SequenceEqual(srcCount(...).AsX<T>())` では旧形状のままだった。
- 実ベンチ FLinq 形状: `Struct0<T,TEnum1,TEnum2>.Compute(__sl0,__sr0)`、`TryGetSpan`、`TryGetNext` fallback、`(T)(object)__mn52` が残存。
- 目標形状: 終端内の二つの `GetSpan()` と直接 `MemoryExtensions.SequenceEqual`。この形状には到達していない。
- 考察: 同一型判定の raw type-name 表記揺れが fallback を選ぶ兆候はあるが、正規化試作も実ベンチ出力に反映されなかった。適用地点またはfragment選択を先に特定せずに測定しても前後差を示せない。
- 採否: 不採用。試作コード・専用形状テストは復旧済み。DBバックアップ `Benchmark.db.pre-sequenceequal-direct-span-pair-20260812-113700.bak` は作成済みだが、DB書き込み・Worker・UI実行は一切していない。
## 2026-08-12 C-OrderByReuseProbedSource-24（保留：既存変更と重複）

- ベンチマーク名: `System.Array / OrderBy_keySelector`（run 189: `Int32 724,000.000 / 284,500.000 ns = 2.545`, `96 / 128 B`、単独1回）。
- ソース型・要素型・終端: 静的 `T[]` / `Int32` / `OrderBy(x => x).Consume(Consumer)`。
- FLinq生成コード形状: `using var __orderedSource = new ArrayRoot(@this.srcCount(...))`、`TryGetSpan`、既整列判定ループ、既整列時の直接Span消費、未整列時の `using var __orderedFallback = new OrderBy(ArrayRoot(@this.srcCount(...)), null)`。
- 静的型・dispatch: Array型とSpanは保持、interface dispatch / boxing / state machine / tupleはない。未整列fallbackだけが `srcCount(...)` を再評価し、新root wrapperを生成する。
- 遅い理由の仮説: 未整列データでsorted判定後にsource式を二重評価し、fallbackが既に作成済みの値列挙子を再利用しない。source式の副作用・Repeat列挙の意味契約も再確認が要る。
- 最小修正案: sorted判定に使った初期位置の `__orderedSource` をfallbackのOrderBy enumeratorへ移して、source式の再評価と二つ目のroot wrapperを除く。静的Spanの成功/失敗、Dispose、例外時の一回だけのDisposeを形状テストで検証する。
- 採否: 保留。`PipelineEnumerable.DirectConsume.cs` は既存変更中のため、上書きせず所有者の変更確定後に実施する。

## 2026-08-12 C-StaticSequenceEqualDirectSpan-23 追加測定

- DB非書込のHeadless `operator-harness`、各独立3回、`System.Array` / `ClassInt32` / CPU 0。
- `SequenceEqual_second`: FLinq `29,955.556 / 28,844.828 / 35,206.061 ns`、ZLinq `3,385.185 / 3,479.310 / 3,081.818 ns`。中央値比 `29,955.556 / 3,385.185 = 8.849`、割当 `64 / 0 B`。
- `SequenceEqual_second_comparer`: FLinq `25,135.294 / 24,562.162 / 23,958.333 ns`、ZLinq `2,950.000 / 4,054.054 / 2,825.000 ns`。中央値比 `24,562.162 / 2,950.000 = 8.326`、割当 `64 / 0 B`。
- run 189全表の割当0Bとの差は、operator-harnessの単独実行で現れた形状差であり、採用基準にはDBへ保存する同一条件の3回中央値を用いる。両結果とも候補の優先度を上げるが、既存変更と重複するため未採用。
## 2026-08-12 C-OrderByReuseProbedSource-24 再監査（実装保留）

- 強制再生成: `Benchmark.System.Linq.Enumerable.FLinq` を `Release -t:Rebuild -p:UseSharedCompilation=false` で再生成。UI / Worker / DB書込みは行っていない。
- 対象・既存測定: `System.Array / OrderBy_keySelector`、静的 `T[]`、`Int32`、終端 `Consume`。run 189 の単独値は FLinq/ZLinq `724,000.000 / 284,500.000 ns = 2.545`、割当 `96 / 128 B`（採用判定用の三連続測定ではない）。
- FLinq生成形状: 最初の `ArrayRoot(@this.srcCount(4096))` で `TryGetSpan` と既整列判定を行い、未整列時に二つ目の `ArrayRoot(@this.srcCount(4096))` を `OrderBy` fallback へ渡す。fallback は値/キー/安定順序用indexのPool配列を持ち、`SortIndexes(... new IndexComparer(keys, comparer))` 後にindex経由で値を返す。interface dispatch / boxing / delegate / tuple / C# state machine はこの静的配列経路にない。
- ZLinqコード形状: `OrderBy<TEnumerator,TSource,TKey>.InitBuffer()` はsourceを一度だけPool配列へ取り込み、identity key・単一キー・暗黙安定型の条件で `span.Sort()` を直接実行する。それ以外ではindex配列とcomparerを用いる。未整列判定後のsource式再評価はない。
- 結論: FLinqの二重source評価は実在する最小のlowering漏れだが、直接 `Span<T>.Sort()` への置換は `OrderBy` の安定順序契約を一般の `T` で保証できない。さらに再利用修正はユーザー既存変更中の `PipelineEnumerable.DirectConsume.cs` と重複するため、所有者の変更を上書きせず実装・測定は保留する。再開時は、同一source値列挙子をfallbackへ移管し、例外・Repeat列挙・Dispose一回だけを形状/契約テストで先に検証する。
## 2026-08-12 検証保留: 外部Headlessビルドによるdepsロック

- `ProviderContainsShapeTests` のRelease実行は、ソース/テスト失敗ではなく `Seamless.Database.Generator.deps.json` の共有違反（MSB4018 / IOException）で中断した。
- 同時点のプロセス調査では `Benchmark.UI.exe` / `Worker.exe` / `Benchmark.Headless.exe` は不在。一方、12:01開始の外部 `dotnet build Benchmark/Benchmark.Headless/Benchmark.Headless.csproj -c Release --no-restore -t:Rebuild` と子MSBuild群が実行中だった。
- 当該プロセスは本作業で起動していないため停止・上書きしない。終了後、同一Release条件で形状テストを再試行する。DB書込み・Worker計測・UI起動は行っていない。
## 2026-08-12 復旧後の形状テスト

- 外部Headlessビルド終了とUI / Worker / Headless不在を確認後、`dotnet test Test/Test.csproj -c Release --no-restore --filter "FullyQualifiedName~ProviderContainsShapeTests"` を実行した。
- net10.0 は 5/5 合格。net10.0-windows はフィルター該当テストなし（失敗ではない）。既存の匿名型FLINQ001およびCS8520警告は出力されたが本テスト結果は成功。
- 確認対象は、復旧済みの静的List Reverseが `GetSpan()` の後に後方直接loopとなり、不要な `IEnumerator<T>` / boxing / `System.Linq.Enumerable.Reverse` を含まないこと、および静的 `IEnumerable<T>` rootの直接interface loopである。UI / Worker / DB書込みは行っていない。
## 2026-08-12 C-AggregateByDirectConsume-25（却下・復旧済み）

- ベンチマーク名: `System.IEnumerable / AggregateBy_keySelector`。
- ソース型・要素型・終端: 静的 `IEnumerable<T>` / `ClassInt32`, `Int32` / `AggregateBy(keySelector, seed, func).Consume(Consumer<KeyValuePair<TKey,TAccumulate>>)`。
- 修正前の単独run: Headless run 190（1回）で `ClassInt32 842,800 / 83,700 ns = 10.069`, `224 / 224 B`。3回中央値ではない。
- 修正前生成コード: rootの `IEnumerable<T>` wrapper → `AggregateBy Struct0<T,TEnum>`（DictionarySlim構築と結果Enumerator状態を保持）→終端 `while(__ce0.TryGetNext(...))`。unknown IEnumerableの`IEnumerator<T>` interface dispatchはroot fallbackに残る。boxing/state machine/tupleはない。
- 最小修正: `AggregateBy.GetConsumeExecutionStatement` を追加し、root wrapperを一度だけ作成、selector/funcのhoistを転送、DictionarySlimを`using`で構築、TraversalSupportでsourceを一回走査、DictionarySlimの値型Enumeratorを直接consumerへ渡す形にした。未知IEnumerableのfallback、例外時Disposeを維持した。
- 修正後生成コード: `using var __aggregateBySource` → `using var __aggregateByLookup` → span成功ならfor、失敗ならroot wrapperの`TryGetNext`、最後に `var __aggregateByResult=lookup.GetEnumerator(); while(TryGetNext)`。終端からAggregateBy Struct0と外側IValueEnumerator whileは消えた。interface dispatchはunknown sourceの`GetEnumerator/MoveNext/Current`だけが残る。
- 生成検証: `Benchmark.System.Linq.Enumerable.IEnumerable.FLinq` を `Release -t:Rebuild /p:EmitCompilerGeneratedFiles=true`、0警告/0エラーで成功。
- 意味テスト: `Test/Test.csproj`の全体強制再生成は別プロセスが`Seamless.Database.Generator\obj\Release\net10.0\Seamless.Database.Generator.dll`をロックしCS2012で失敗。既存プロセスは停止しなかった。続く`--no-build`はnet10.0出力欠落のため対象テストを実行できなかった。したがってこの候補に結果テスト合格は付与しない。
- 修正後のDB非書込Headless operator-harness（各3回、CPU 0、同一Release成果物）:
  - `ClassInt32`: FLinq `288,000 / 205,700 / 226,200 ns`、ZLinq `135,900 / 75,350 / 78,600 ns`。中央値 `226,200 / 78,600 = 2.878`、割当 `224 / 224 B`。
  - `Int32`: FLinq `132,433.333 / 230,333.333 / 119,166.667 ns`、ZLinq `34,466.667 / 34,333.333 / 30,466.667 ns`。中央値 `132,433.333 / 34,333.333 = 3.857`、割当 `216 / 216 B`。
- 遅い理由の考察: 終端state machineの除去だけでは、DictionarySlim構築・hashing・key/reducerのgeneric呼出しと、unknown IEnumerableのroot wrapper/interface dispatchに対するZLinqとの差を埋められない。特にInt32は分散が大きいが、全3回で1.02を超えた。
- 採否: 却下。3%以上の改善を同条件の修正前3回中央値で確定できず、修正後自体も1.02目標を満たさない。`AggregateBy.cs`は候補コードを完全に復旧済み。
## 2026-08-12 C-ListMaxShape-25（不採用：lowering漏れなし）

- ベンチマーク名: `System.List / Max`。静的ソース型 `List<T>`、要素型 `Int32` / `ClassInt32`、終端は `Consumer.Consume(Max())`。候補表の過去中央値では `List<int>` が FLinq/ZLinq 1.715、割当は `0 / 0 B`。
- 強制再生成: `Benchmark.System.Linq.Enumerable.List.FLinq` を `Release -t:Rebuild -p:UseSharedCompilation=false` で生成し直した。UI / Worker / DB書込みなし。
- FLinq形状: `using (new ListRoot<T>(srcCount(...)))`、`GetSpan()`、`Seamless.Linq.Enumerable.MaxSpan<T>(span, Comparer<T>.Default)`。Listの構造体root以外に `IEnumerator<T>`、boxing、delegate、tuple中間値、C# state machine、Pipeline wrapperはない。
- ZLinq形状: `ValueEnumerable<TEnumerator,T>.Max()` は同じく `source.Enumerator.TryGetSpan(out span)` の後 `MaxSpan(span, Comparer<T>.Default)` を呼ぶ。未知列挙だけが `TryGetNext` fallbackになる。
- 結論・採否: 静的型/直接Span能力は両者とも保持されており、FLinqのほうは整数型でSIMD `MaxSpan` を選ぶ。過去比率だけではlowering漏れを示さない。ここへ型判定やList専用分岐を追加するとコード量と回帰面だけが増えるため**不採用**。必要なら同一Headless Workerの独立3回で測定揺らぎを切り分けるが、変更前のためDB測定は行わない。
## 2026-08-12 復旧記録: AggregateBy 試行撤回後の Headless 成果物再構築

- 対象: `C-AggregateByDirectConsume-25` の撤回後の実行成果物。
- 状態: `AggregateBy.cs` の試行実装は完全に戻し、`git diff -- Seamless.Linq/Fragments/AggregateBy.cs` は空であることを確認した。
- 復旧操作: 並列 Rebuild では参照 DLL のクリーン/生成競合により一時的に `CS0006` が出たため、ソースを変更せず `Benchmark.Headless` を `Release --no-restore -m:1` で直列ビルドした。
- 結果: 0 warnings / 0 errors。`Benchmark.Headless.dll` を現行ソースから再生成できた。
- 実行分離: この操作はコンパイルのみであり、`Benchmark.UI.exe` は起動していない。Worker/Headless の測定も実行していない。DB は変更していない。
- 採否: 試行は不採用のまま。以後の測定は、この復旧済み Headless 成果物だけを使う。
## C-IEnumerableContainsValueHoist-26: Contains の比較値をfallback loopの外へ束縛

- ベンチマーク: `System.Collections.Generic.IEnumerable<> / Contains_value`。
- ソース型 / 要素型 / 終端: `IEnumerable<T>`（実体は `CreateSourceEnumerable` の C# iterator 状態機械）/ `Int32`, `ClassInt32` / `Contains`。
- 強制再生成後のFLinq形状: `__genericSource0` を一度評価し、配列/List/ICollection/IReadOnlySetを分岐。未知列挙では `using IEnumerator<T> e=source.GetEnumerator(); while(e.MoveNext()) { var item=e.Current; comparer.Equals(item, Value(value)) }`。`Struct0/Struct1` は同一generated fileに残るが、終端から未参照。
- ZLinq形状: `AsValueEnumerable()` は `ValueEnumerable<FromEnumerable<T>,T>`。`FromEnumerable<T>` は実行時型で iterator を選ぶ。今回の実体は iterator 状態機械なので `EnumerableIterator<T>`、`Contains` は `ICollection<T>` でなければ `ContainsCore(ref source, value)` の値型ループ。呼出し引数 `Value(value)` は loop前に一度評価される。
- 静的型/dispatch: 両者とも未知 `IEnumerable<T>` では `IEnumerator<T>` interface dispatch が残る。boxingは観測されず、状態機械はソース自身の契約。FLinqの終端には不要な `PipelineEnumerable`・tuple・delegateはない。
- 実測（現行、修正前、Headless Workerを毎回新規起動、`--operator-harness`、DB非更新）:
  - Int32: FLinq `26225 / 33280 / 14541.667 ns`、ZLinq `7266.667 / 7533.333 / 8800 ns`、中央値比 `26225 / 7533.333 = 3.481`、割当 `160 / 160 B`。
  - ClassInt32: FLinq `75225 / 61900 / 51133.333 ns`、ZLinq `27225 / 7133.333 / 27500 ns`、中央値比 `61900 / 27225 = 2.274`、割当中央値 `74808 / 192 B`。
- 劣化理由: `RenderGenericCollectionContainsInline` の未知列挙fallbackだけが `{{value}}` を `comparer.Equals` 内へ直接展開した。元のLINQ呼出しは引数を一回評価するが、生成コードは各要素で `Value(value)` を再評価し、ClassInt32では比較対象のnewを12,288回生成した。これは意味契約も崩す。
- 最小修正案: `var __genericValue0={{value}};` をsource評価直後に一度だけ生成し、すべての分岐・fallback比較で同ローカルを使う。公開API・未知IEnumerable fallback・Disposeは維持。
- 状態: 実装・再生成・意味テスト・同一条件3回再測定へ進む。旧run 190とはビルド形状が異なるため、中央値を混在させない。
### C-IEnumerableContainsValueHoist-26 結果・採否

- 実装: `RenderGenericCollectionContainsInline` に `var __genericValue0=Value(value);` を追加し、全分岐とfallback loopを同ローカルへ置換した。生成終端は `IEnumerator<T> e=source.GetEnumerator()` / `while(e.MoveNext())` のままで、`comparer.Equals(item,__genericValue0)` となる。値式はloop外に1回だけ出る。
- 検証: `Benchmark.System.Linq.Enumerable.IEnumerable.FLinq` を `Release -t:Rebuild /p:EmitCompilerGeneratedFiles=true -m:1` で強制再生成、0 warning / 0 error。`dotnet test Test/Test.csproj -c Release --no-restore --filter FullyQualifiedName~ProviderContainsShapeTests` は net10.0 で 7/7 pass（net10.0-windowsには同フィルター対象なし、プロセス終了コード0）。初回テストはprivateメソッドが `UnsafeAccessor` になる生成規則を `this.NextValue()` と誤認して失敗しただけで、出力には正しい `var __genericValue0=__Method_Container_NextValue(@this);` とloop内参照があり、アサーションを修正して再実行済み。
- 修正後測定（各回とも新規Headless Worker、`--operator-harness`、DB非更新、UI非起動）:
  - Int32: FLinq `34954.545 / 36391.667 / 33784.375 ns`、ZLinq `8836.364 / 8575 / 2996.875 ns`、中央値比 `34954.545 / 8575 = 4.077`、割当 `160 / 160 B`。
  - ClassInt32: FLinq `26171.429 / 56980 / 54888.889 ns`、ZLinq `23400 / 10940 / 10355.556 ns`、中央値比 `54888.889 / 10940 = 5.017`、割当 `192 / 192 B`。
- 前後: ClassInt32のFLinq中央値は `61900 -> 54888.889 ns`（11.3%改善）、割当中央値は `74808 -> 192 B`。Int32は値式が非割当のため効果がなく、今回中央値は `26225 -> 34954.545 ns`（33.3%悪化）。Worker間の分散が大きいが、採用基準上この回帰と比率>1.02は無視できない。
- 採否: **性能候補として不採用**（全DBの再測定対象にはしない）。ただし、値式を要素ごとに再評価する以前の生成コードはLINQ呼出しの一回評価契約を壊すため、`__genericValue` のhoist自体は**意味修復として保持**する。
- 残る原因: 実体がiterator状態機械の未知IEnumerableではFLinq/ZLinqともinterface dispatchを避けられない。ZLinqは `FromEnumerable<T>` のiterator選択と `ContainsCore(ref ValueEnumerable<...>)`、FLinqは直接interface loop。不要wrapperを終端から除いても中央値差は残るため、ここで静的型specializationを増やす根拠はない。旧DB run 190 は修正前・別ビルドであり、上記値と混在させない。
## C-IEnumerableElementAtOrDefaultDirectLoop-27: scalar wrapper/Compute を直接interface loopへlower

- ベンチマーク: `System.Collections.Generic.IEnumerable<> / ElementAtOrDefault_index`（run 190ではClassInt32 `91250 / 21950 ns = 4.157`、旧Worker結果であり後述測定と混在させない）。
- ソース型 / 要素型 / 終端: 静的 `IEnumerable<T>`、実体は `CreateSourceEnumerable` のiterator状態機械 / `Int32`, `ClassInt32` / `ElementAtOrDefault(int)`。
- 現行FLinq生成形状: `using Struct1<IValueEnumerator<T>>` → `Struct0<T,TEnum>.Compute(__s0,index)` → `Struct1.TryGetNext` → interface `IEnumerator<T>.MoveNext/Current`。終端はwrapperとCompute generic helperを経由する。
- ZLinq形状: `ValueEnumerable<FromEnumerable<T>,T>` の `ElementAtOrDefault`。今回の実体は `EnumerableIterator<T>` 選択で、最終的に同じiteratorのinterface列挙へ進むが、FLinqのroot wrapper/Compute型は持たない。
- 仮説: 静的IEnumerableに限り、`source`・`index`を一度評価して `using IEnumerator<T>` の直接loopへ置換すれば、Struct1/Struct0/`TryGetNext`/`Current`保持を終端経路から排除できる。
- 最小修正案: `Root { DirectEnumerableExpression }` のみで `index<0` はdefault、そうでなければ一回の `GetEnumerator` と `while(MoveNext)` でindex一致時だけ `Current` を読む。未知ソースへのruntime型specializationは追加しない。nullは引数評価後に既存LINQと同じく検査し、usingでDisposeを保証する。
- 状態: 実装、強制再生成、形状/意味テスト、同一Worker 3回測定へ進む。
### 2026-08-12 実行環境競合・局所復旧（C-27途中）

- `ElementAtOrDefault` の最初の挿入は置換後文字列の旧位置を用いたためspan用raw文字列内へ誤挿入された。直後に同fragmentの `RenderInlineComputation` から `RenderRange...` 直前だけを完全な既知形へ復旧し、改行も修復した。他のソース、DB、UI/Workerには変更なし。その後の `IEnumerable.FLinq Release -t:Rebuild /p:EmitCompilerGeneratedFiles=true -m:1` は0 warning / 0 errorで、終端は `GetEnumerator/MoveNext/Current` の直接loop、`.Compute(` とroot wrapper参照なしを確認した。
- Contains形状テストでwrapper fallbackが出たため現行 `Contains.cs` を確認したところ、並行変更により直接interface loopがwrapper `Struct1.TryGetNext` へ戻っていた。比較値の一回評価(`__genericValue`)は保ちつつ、明示的 `IEnumerator<T>` / `MoveNext` / `Current` loopへ復元した。これはユーザー指定の「直接interface loop」と一致する。
- 形状テスト試行: C-26ではprivate `NextValue` がUnsafeAccessorに変換される期待差のみを修正後、7/7 pass。C-27追加後の実行は、上記Contains wrapper検出と、ElementAtOrDefaultテストがgenerated file末尾に次の`private struct`を持たない正常ケースを想定していなかったため2失敗。後者は末尾までを対象にするようテスト境界を修正済み。再実行は外部出力ロック解消後に行う。
- ビルド競合: Headless依存ビルドは、最初にclean/参照DLL競合で `CS0006/MSB3030`（83 errors）、次に `-m:1 /p:BuildInParallel=false` でも `CS2012`（`Benchmark.Tsavorite.Linq.Enumerable.Linq.dll` を別プロセスが使用）およびUI projectのMSBuild失敗で停止。IEnumerable単体再生成は成功済み。いずれもソース、Benchmark.db、UI実行結果には変更なし。
- プロセス確認: `Benchmark.UI` / `Worker` / `Benchmark.Headless` は存在しない。ロック候補は外部の `vstest.console`（PID 66964, 36488）とMSBuildノード群（2026-08-12 12:34:57開始）。停止は行わない。
- 現状態: C-27の測定は未実施。Headless成果物が候補ソースと一致することを再ビルドで確認できるまで、測定値やDB結果を作らない。
### C-IEnumerableElementAtOrDefaultDirectLoop-27: ZLinq比較の確定

- ローカルのZLinq 1.5.6を逆コンパイルした。`ElementAtOrDefault<TEnumerator,T>(..., int index)` は負indexでdefaultを返し、列挙子をtry/finallyでDisposeし、`TryGetElementAt` → `EnumeratorHelper.TryConsumeGetAt(ref enumerator,index,out value)` を呼ぶ。int index fallbackは `while(enumerator.TryGetNext(out current)) { if(num++==index) ... }`。
- `FromEnumerable<T>` は `T[]/List/IReadOnlyList/IList/その他` を実行時選択する。今回の `CreateSourceEnumerable` はC# iterator状態機械なのでその他（`EnumerableIterator<T>`）であり、span/copy/collectionの静的fast pathは使えない。
- 結論: C-27の `IEnumerator<T>.MoveNext/Current` 直接loopは、未知IEnumerableでZLinqが最終的に行う列挙契約と等価で、FLinq側だけにあったRoot Struct1/Struct0.Compute/TryGetNext層を取り除く。実測で採否を決める必要があるが、静的型が失われる地点はAsX後の`IEnumerable<T>`で確定している。
### 2026-08-12 目標モード再開: Contains直接interface loopの再復元

- 目標モード再開時の現行`Contains.cs`は、`__genericValue`による比較値一回評価とnull検査は残っていたが、unknown `IEnumerable<T>` fallbackが再び `Struct1.TryGetNext` wrapper経路になっていた。これはユーザー指定の直接interface loopおよびC-26の生成形状記録と矛盾する。
- 既存のcollection/span fast path、比較値hoist、Dispose条件を保持し、fallbackのみに `using IEnumerator<T> e=source.GetEnumerator(); while(e.MoveNext()){ var item=e.Current; ... }` を再適用した。UTF-8検証済み。
- 測定/採否: Headless再構築が外部`vstest.console`のDLLロックで未完了のため未測定。旧run/旧harness値とは混在させない。
## 2026-08-12 C-IEnumerableContainsDirectInterfaceLoop-28（却下）

- 対象ベンチマーク: `System.Collections.Generic.IEnumerable<> / Contains_value`。ソースの静的型は `IEnumerable<T>`、実体は `CreateSourceEnumerable` が返す C# iterator、要素型は `Int32` と `ClassInt32`、終端は `Contains`（`Consumer.Consume(bool)`）である。
- DB保護: 計測前に `Work/UI/Benchmark.db.pre-ienumerable-contains-direct-interface-20260812-122400.bak` を作成した。run 194--205 は本作業の Headless 起動だけが保存した結果であり、`Benchmark.UI.exe` は起動していない。ビルド出力に `Benchmark.UI.dll` が現れるのは参照プロジェクトのコンパイルであって UI 実行ではない。
- 静的型が失われる地点: `srcCount(...).AsX<T>()` 後は意図的に `IEnumerable<T>` だけであり、配列/List/Span/Count/Copy の静的能力はない。実体は外部 C# iterator の state machine なので、その生成自体は FLinq の lowering 漏れではない。

### 修正前 FLinq 生成形状（最終的に維持）

```csharp
var __genericSource0 = (IEnumerable<T>)source;
var __genericValue0 = Value(value); // 一度だけ評価
// T[] / List<T> / ICollection<T> / IReadOnlySet<T> の fast path
var comparer = EqualityComparer<T>.Default;
result = false;
using (var e = new OptimizedLinq_Contains...Struct1<T>(__genericSource0))
{
    while (e.TryGetNext(out var item))
    {
        if (comparer.Equals(item, __genericValue0)) { result = true; break; }
    }
}
```

- `Struct1<T>` の内部で一度だけ `IEnumerable<T>.GetEnumerator()` を行い、`IEnumerator<T>.MoveNext/Current` の interface dispatch を保持する。未知 `IEnumerable<T>` なのでこれは避けられない。enumerator の boxing はない（実体は iterator class）、FLinq が新しい tuple/delegate/state machine を作ることもない。default comparer は generic のままで型消去していない。

### 試行した直接 interface loop（却下・最終コードには残さない）

```csharp
using (IEnumerator<T> e = __genericSource0.GetEnumerator())
{
    while (e.MoveNext())
    {
        var item = e.Current;
        if (comparer.Equals(item, __genericValue0)) { result = true; break; }
    }
}
```

- wrapper を取り除けば速くなる、という仮説だった。しかし終端に raw `IEnumerator<T>` を露出すると `MoveNext/Current` の `callvirt` が terminal loop に残り、`Struct1<T>.TryGetNext` を generic value-enumerator として最適化する経路を失う。特に `Int32` で大幅に悪化した。これは JIT disassembly 未取得のため因果の確定ではなく、生成形状と再現測定からの仮説である。

### ZLinq 展開形状との比較

```csharp
// ValueEnumerableExtensions.Contains(FromEnumerable<T>)
if (source.Enumerator.GetSource() is ICollection<T> collection)
    return collection.Contains(value);
return ContainsCore(ref source, value);

// ContainsCore<TEnumerator,T>
using TEnumerator e = source.Enumerator;
if (e.TryGetSpan(out ReadOnlySpan<T> span)) return span.Contains(value, null);
while (e.TryGetNext(out current))
    if (EqualityComparer<T>.Default.Equals(current, value)) return true;
```

- ZLinq も未知 `IEnumerable<T>` は `FromEnumerable<T>` を value-enumerator の `TryGetNext` loop に載せる。従って raw `IEnumerator<T>` へ直接 lower する試行は ZLinq と同形ではなく、FLinq の既存 struct fallback の方が形状上整合する。

### 同一条件の独立3回測定（Release / PairedFast / CPU 0 / 10,000,000 ns）

| 要素型 | 版 | run | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq alloc | ZLinq alloc |
|---|---|---|---:|---:|---:|---:|---:|
| Int32 | wrapper | 194 | 43,082.123 | 46,737.430 | 0.922 | 160 B | 160 B |
| Int32 | wrapper | 195 | 39,426.136 | 44,663.636 | 0.883 | 160 B | 160 B |
| Int32 | wrapper | 196 | 41,795.980 | 46,578.392 | 0.897 | 160 B | 160 B |
| Int32 | direct | 200 | 50,198.864 | 49,278.409 | 1.019 | 160 B | 160 B |
| Int32 | direct | 201 | 48,875.152 | 49,672.121 | 0.984 | 160 B | 160 B |
| Int32 | direct | 202 | 52,445.455 | 50,318.831 | 1.042 | 160 B | 160 B |
| ClassInt32 | wrapper | 197 | 60,637.864 | 81,466.990 | 0.744 | 192 B | 192 B |
| ClassInt32 | wrapper | 198 | 59,904.425 | 84,115.044 | 0.712 | 192 B | 192 B |
| ClassInt32 | wrapper | 199 | 54,983.929 | 77,642.857 | 0.708 | 193 B | 193 B |
| ClassInt32 | direct | 203 | 56,408.594 | 80,518.750 | 0.701 | 192 B | 192 B |
| ClassInt32 | direct | 204 | 60,471.074 | 78,083.471 | 0.774 | 192 B | 192 B |
| ClassInt32 | direct | 205 | 53,775.610 | 75,587.805 | 0.711 | 192 B | 192 B |

- 中央値: Int32 は wrapper `41,795.980 / 46,578.392 = 0.897`、direct `50,198.864 / 49,672.121 = 1.011`。FLinq は **+20.1% 悪化**。ClassInt32 は wrapper `59,904.425 / 81,466.990 = 0.735`、direct `56,408.594 / 78,083.471 = 0.702` で **-5.8% 改善**。
- 割当は変化なし。paired DB テーブルは GC 回数を保持しないため、この12 runについて GC は「未保存」であり、0 回とは断定しない。
- 採否: **却下**。ClassInt32 の改善は3%を超えるが、同一ソース群の Int32 に20.1%の回帰を作るため、2%回帰上限と全比較中央値1.02目標の双方に反する。`Contains.cs` は wrapper fallback へ復旧した。`__genericValue0` の一度だけの評価、null、Dispose、例外、空列の契約を検証するテストは維持する。
- 再現性メモ: run 191--193 は直接 loop の探索中に採取した予備測定であり、修正前後の判定には使用しない。run 194--205 だけを同一手順の比較結果として扱う。
### C-IEnumerableContainsDirectInterfaceLoop-28 状態訂正（12:40以降）

- wrapper へ復旧した直後の検証時点で、共有作業ツリーの `Contains.cs` は再び直接 `IEnumerator<T>` loop になっていた。これを本作業が上書きしないよう保持した。したがって「却下」は**測定に基づく採用判断**であり、現行ワークツリーの形状を wrapper に固定した、という意味ではない。
- 直接 loop の現行生成コードは `using(IEnumerator<int> __genericEnum0=__genericSource0.GetEnumerator())`、`while(__genericEnum0.MoveNext())`、`Current` であることを SourceGenerator test の失敗出力でも確認した。wrapper を期待する一時テストは現行実装と矛盾するため、追加した形状テストを直接 loop 期待へ戻した。
- 以後この候補を変更・採用・復旧するには、20.1% Int32 回帰を許容するか、wrapper を明示的に最終形へ戻すかの利用者判断が必要である。測定・展開コード・採否理由は上記 C-28 本文に保存済み。
## 2026-08-12 C-IEnumerableElementAtOrDefaultDirectLoop-27（現行形状の採用確認）

- 対象: `System.Collections.Generic.IEnumerable<> / ElementAtOrDefault_index`。ソース静的型は `IEnumerable<T>`、実体は `CreateSourceEnumerable` の C# iterator state machine、要素型は `Int32` / `ClassInt32`、終端は `ElementAtOrDefault(int)` を `Consumer.Consume(T)` へ渡す形である。
- 生成形状: `Root.DirectEnumerableExpression` に限り、source と index を一度だけ local へ評価し、null は `ArgumentNullException.ThrowIfNull`、負 index は default を返す。非負なら `using(var e=source.GetEnumerator()) while(e.MoveNext())` で index を数え、該当 `Current` を返す。`Struct1` root wrapper、scalar `Compute`、`TryGetNext`、tuple、中間 PipelineEnumerable は terminal method にない。未知 IEnumerable なので `IEnumerator<T>` の interface dispatch と、外部 iterator の state machine は残る。boxing、delegate、comparer はない。
- ZLinq は `ValueEnumerable<FromEnumerable<T>,T>` から `TryGetElementAt(ref enumerator,index,out value)` を呼び、`finally` で Dispose する value-enumerator loop である。両者とも未知 IEnumerable の interface dispatch は不可避だが、FLinq は余分な root/Compute wrapper を除いた。
- 検証: `ProviderContainsShapeTests.StaticIEnumerableElementAtOrDefaultUsesDirectInterfaceLoop` が `GetEnumerator/MoveNext/Current` と `Compute/TryGetNext` 不在を検査済み。今回の対象テスト群は Release で 9/9 成功（net10.0、net10.0-windows には該当テストなし）。
- DB保護: 測定前に `Work/UI/Benchmark.db.pre-elementatordefault-direct-loop-20260812-125100.bak` を作成。run 206--211 は Headless/独立 Worker であり UI は起動していない。Release / PairedFast / CPU 0 / 10,000,000 ns。

| 要素型 | run | FLinq ns | ZLinq ns | FLinq/ZLinq | FLinq alloc | ZLinq alloc |
|---|---:|---:|---:|---:|---:|---:|
| Int32 | 206 | 22,624.522 | 22,709.554 | 0.996 | 224 B | 224 B |
| Int32 | 207 | 22,333.852 | 23,463.424 | 0.952 | 224 B | 224 B |
| Int32 | 208 | 22,476.744 | 21,056.589 | 1.067 | 224 B | 224 B |
| ClassInt32 | 209 | 23,115.849 | 31,551.321 | 0.733 | 232 B | 232 B |
| ClassInt32 | 210 | 23,050.388 | 32,463.178 | 0.710 | 232 B | 232 B |
| ClassInt32 | 211 | 23,408.949 | 33,238.521 | 0.704 | 232 B | 232 B |

- 中央値: Int32 `22,476.744 / 22,709.554 = 0.990`、ClassInt32 `23,115.849 / 32,463.178 = 0.712`。割当は同一。paired DB は GC 回数を保存しないため、この測定の GC は未保存。
- 採否: **採用（既存実装の有効性確認。今回のコード変更なし）**。両型とも中央値 1.02 以内であり、直接 lowering を wrapper に戻す理由はない。run 190 の古い高比率は別 Worker/古い成果物の結果として、run 206--211 と混同しない。
## 2026-08-12 C-IEnumerableAggregateByShape-25（保留）

- 対象: `System.Collections.Generic.IEnumerable<> / AggregateBy_keySelector`。静的 source は `IEnumerable<T>`、実体は `CreateSourceEnumerable` の iterator state machine、要素型は `Int32` / `ClassInt32`、終端は `AggregateBy(...).Consume(Consumer)`。
- FLinq 形状: `Struct1<T>` が未知 IEnumerable の interface enumerator を保持し、`Struct0<T,TEnum>` が `DictionarySlim<int,int>` を遅延生成する。span が得られる既知実体では direct for、未知 iterator では `while(__e0.TryGetNext(out item))`。key selector `Mod(item,1000)` と reducer `agg + Number(item)` は生成コードに直接展開され、delegate/result selector/comparer の型消去、boxing、tuple、FLinq 由来 state machine はない。終端は `using var Struct0; while(Struct0.TryGetNext(out KeyValuePair)) Consumer.Consume(...)`。
- ZLinq 形状: `FromEnumerable<T>` を持つ `AggregateBy<TEnumerator,...>` が `DictionarySlim` を生成し、同じ `TryGetNext` で source を走査後、DictionarySlim enumerator を返す。ZLinq 側は keySelector/reducer を `Func` として保持する。どちらも未知 iterator の interface dispatch/state machine は不可避。
- 静的型の損失は `AsX<T>()` での `IEnumerable<T>` 化であり意図的。ここで raw `IEnumerator<T>` の直接終端 loop へ変える最小案は C-28 と同じで、Int32 の20.1%回帰を既に観測しているため実施しない。
- DB保護: `Work/UI/Benchmark.db.pre-aggregateby-shape-20260812-125600.bak`。run 212--217 は Headless の独立 Worker、UI は未起動。Release / PairedFast / CPU 0 / 10,000,000 ns。

| 要素型 | run | FLinq ns | ZLinq ns | 比率 | FLinq alloc | ZLinq alloc |
|---|---:|---:|---:|---:|---:|---:|
| Int32 | 212 | 106,200.000 | 133,605.634 | 0.795 | 217 B | 217 B |
| Int32 | 213 | 102,761.538 | 129,907.692 | 0.791 | 217 B | 217 B |
| Int32 | 214 | 103,693.056 | 121,884.722 | 0.851 | 217 B | 217 B |
| ClassInt32 | 215 | 283,506.250 | 266,796.875 | 1.063 | 225 B | 225 B |
| ClassInt32 | 216 | 239,911.765 | 229,841.176 | 1.044 | 226 B | 226 B |
| ClassInt32 | 217 | 231,460.000 | 229,497.500 | 1.009 | 225 B | 225 B |

- 中央値: Int32 `103,693.056 / 129,907.692 = 0.798`、ClassInt32 `239,911.765 / 229,841.176 = 1.044`。GC は paired DB に未保存。run 190 の `10.069` は古い Worker/成果物の一回値であり、この独立3回中央値と混同しない。
- 採否: **保留（コード変更なし）**。ClassInt32 は1.02を超えるが、構造上の lowering 漏れは確認できない。次の最小検証は、JIT profile/disassembly で `DictionarySlim.GetValueRefOrAddDefault`、`Base<T>.Mod/Number`、Iterator `MoveNext` のインライン化差を特定してからに限定する。根拠なく DictionarySlim/comparer/loop を変更すれば意味契約と同一ソース群の回帰を招く。
## 2026-08-12 C-IEnumerableDefaultIfEmptyDirectConsume-29（却下: 発火しない経路）

- 対象: `System.Collections.Generic.IEnumerable<> / DefaultIfEmpty`、`CreateSourceEnumerable` iterator、`Int32` / `ClassInt32`、終端 `Consume`。
- 修正前 generated FLinq は Root `Struct0<T>`（内部は `IEnumerator<T>.MoveNext/Current`）を `DefaultIfEmpty Struct1<T,TEnum>` が包み、terminal は `TryGetSpan` probe 後 `while(TryGetNext)`。boxing、delegate、tuple、FLinq由来 state machine はない。外部 iterator の state machine と interface dispatch は残る。
- 仮説: Range 専用だった `GetConsumeExecutionStatement` を、静的 IEnumerable Root または AsEnumerable-only/空 stage Pipeline の direct `using IEnumerator<T>; do/while(MoveNext)` に拡張すれば、Struct0/Struct1/TryGetSpan probe を外せる。
- 強制再生成の結果、候補を追加しても `DefaultIfEmpty_Impl` の出力は上記の `Struct0 -> Struct1 -> TryGetSpan -> TryGetNext` のままで変化しなかった。`DefaultIfEmptyBase.GetConsumeExecutionStatement` はこの benchmark の終端レンダリングに到達せず、別の generic consume renderer が出力を支配している。従って静的型能力の漏れではなく、候補の実装箇所が誤りだった。
- 採否: **却下**。発火しない分岐を `DefaultIfEmpty.cs` から復旧した。修正前後の性能比較は、生成コードが同一のため実施しない。修正前の独立測定 run 218--223 は DB に保存済みだが、この不発候補の採否には使わない。GC は paired DB に未保存。
- 次に必要なのは `Consume` の dispatch が実際にどの renderer を選ぶかを trace で特定することであり、Root/AsEnumerable/Pipeline の型パターンを増やすことではない。C-28 の direct interface loop は Int32 で20.1%回帰したため、根拠なく raw loop を挿入しない。## 2026-08-12 C-IEnumerable-DirectLowering-30????????Worker?

- UI??: `Benchmark.UI.exe` ????????????Worker????Worker???????????Headless?????????Worker????????
- DB: ???????? `Work/UI/Benchmark.db.pre-zlinq-reference-20260812-133454.bak` ????SHA256?DB?????? `9EB56633F2728C2FFFBC8BCFAB6420F73E358D46F7A638670CDB4BC9A8EB0AA6`????Run 224--247? `PairedFast`?Release?CPU 0????4096???Worker??FLinq/ZLinq????????????
- ?????? `Benchmark.System.Linq.Enumerable.IEnumerable.FLinq` ?Release `Rebuild`????inspection????????????

### ?????

- ???????: `ElementAtOrDefault_index`?`DefaultIfEmpty`?`Contains_value`?`AggregateBy_keySelector`?
- ????: `IEnumerable<T>`???? `Shared.Common.TestInfrastructure.Base<T>.CreateSourceEnumerable` ?iterator state machine??????/List?????
- ???: `Int32`?`ClassInt32`?
- FLinq????: ????????????????????`ArgumentNullException.ThrowIfNull`?`IEnumerable<T>.GetEnumerator()`?`MoveNext()`?`Current`??????`ElementAtOrDefault`?index???????????????????`Contains`?value??????????????/List/collection/set?runtime fast path???????`IEnumerator<T>` fallback????`DefaultIfEmpty`???????do/while?????????1?Consume???`AggregateBy`????`Struct1`/`Struct0`?`DictionarySlim`????
- ZLinq??: `ValueEnumerable<TEnumerator,T>`?`FromEnumerable<T>`????struct???????????`TryCopyTo`/`TryGetSpan`???????????`TryGetNext`??????iterator??????iterator?interface/state machine?????????enum carrier?????????
- ??????????: FLinq?IEnumerable????`AsX<T>()`???????`IEnumerable<T>`??????????????????????????????????interface loop????????ZLinq??????`FromEnumerable<T>`??????
- boxing/callvirt/interface dispatch: FLinq??iterator?`IEnumerator<T>`?interface dispatch????????????FLinq??`IValueEnumerator<T>` wrapper?wrapper???`TryGetNext`???boxing?????iterator???`MoveNext`?interface dispatch??????????????????????????ZLinq???`IEnumerable<T>`?fallback?????interface/state-machine???????
- delegate/comparer/state machine: `Contains`?default comparer???????????????????`DefaultIfEmpty`?delegate/comparer???????`AggregateBy`?selector/reducer???????inline??DictionarySlim??????iterator?state machine????tuple??????4??????????

### ??3????????3Run?????

| ?????? | ??? | Run | FLinq???ns | ZLinq???ns | FLinq/ZLinq | FLinq?? | ZLinq?? | ?? |
|---|---|---|---:|---:|---:|---:|---:|---|
| ElementAtOrDefault_index | Int32 | 224--226 | 22,343.366 | 22,873.463 | 0.9768 | 224 B | 224 B | ?? |
| ElementAtOrDefault_index | ClassInt32 | 227--229 | 22,645.283 | 31,033.478 | 0.7297 | 232 B | 232 B | ?? |
| DefaultIfEmpty | Int32 | 230--232 | 43,278.238 | 47,095.337 | 0.9189 | 160 B | 160 B | ?? |
| DefaultIfEmpty | ClassInt32 | 233--235 | 54,976.033 | 78,584.298 | 0.6996 | 192 B | 192 B | ???? |
| Contains_value | Int32 | 236--238 | 43,549.444 | 43,276.796 | 1.0063 | 160 B | 160 B | ?? |
| Contains_value | ClassInt32 | 239--241 | 53,243.750 | 77,071.074 | 0.6908 | 192 B | 192 B | ?? |
| AggregateBy_keySelector | Int32 | 242--244 | 101,394.595 | 121,256.757 | 0.8362 | 217 B | 217 B | ?? |
| AggregateBy_keySelector | ClassInt32 | 245--247 | 237,034.146 | 229,538.095 | 1.0327 | 225 B | 225 B | ?? |

- ????: 1.02???????7????????????????`AggregateBy ClassInt32`?1.0327??????/???`DefaultIfEmpty Int32`?????Run????1.02?????????????Worker????????????3%?????2%???????????????????????????
- ?Run 218--223???loop????????Worker????????????????????????baseline?????????????????????????????
- ????????`AsX<T>()`?????????????transparent fragment?????????stage???????pipeline/wrapper????????`GetConsumeExecutionStatement`????unknown `IEnumerable<T>`????fallback??????

## 2026-08-12 cross-table audit after forced regeneration (ASCII summary)

Measurement provenance:
- Benchmark.UI.exe was not started during this measurement.
- No pre-existing Worker was running. Runs 224--247 were created by new Headless Worker processes after forced Release regeneration.
- Benchmark.db was explicitly backed up before measurement. The old UI/Worker results remain in the database but are not mixed with the new evidence.

Fresh current IEnumerable audit, Release / PairedFast / CPU 0 / three independent runs per row:

| case | element | runs | FLinq median ns | ZLinq median ns | ratio | allocation | decision |
|---|---|---:|---:|---:|---:|---|---|
| ElementAtOrDefault_index | Int32 | 224--226 | 22343.366 | 22873.463 | 0.9768 | 224/224 B | adopt |
| ElementAtOrDefault_index | ClassInt32 | 227--229 | 22645.283 | 31033.478 | 0.7297 | 232/232 B | adopt |
| DefaultIfEmpty | Int32 | 230--232 | 43278.238 | 47095.337 | 0.9189 | 160/160 B | shape adopt; broad gate hold |
| DefaultIfEmpty | ClassInt32 | 233--235 | 54976.033 | 78584.298 | 0.6996 | 192/192 B | adopt candidate |
| Contains_value | Int32 | 236--238 | 43549.444 | 43276.796 | 1.0063 | 160/160 B | adopt |
| Contains_value | ClassInt32 | 239--241 | 53243.750 | 77071.074 | 0.6908 | 192/192 B | adopt |
| AggregateBy_keySelector | Int32 | 242--244 | 101394.595 | 121256.757 | 0.8362 | 217/217 B | adopt |
| AggregateBy_keySelector | ClassInt32 | 245--247 | 237034.146 | 229538.095 | 1.0327 | 225/225 B | hold |

Shape conclusion:
- ZLinq keeps a concrete struct ValueEnumerable and specializes array/List/known enumerators. FLinq can keep this shape only when the source type is statically known.
- For public static IEnumerable<T>, the safe contract is a direct IEnumerator<T> loop. It removes the extra FLinq value-enumerator carrier, but cannot remove the source iterator state machine or interface dispatch already exposed by IEnumerable<T>.
- Runtime array/List probes were held for this benchmark because the actual source is an iterator state machine; the probes would add work and would not remove its interface path.
- AggregateBy/ClassInt32 remains above 1.02. Its generated code already has typed carriers, inline selectors, no extra boxing, and equal allocation; no speculative dictionary/comparer rewrite was accepted.

Cross-table latest saved snapshots still contain above-1.02 rows: Array 47/194, Range 22/81, Persisted(System) 27/130, and Tsavorite(System) 34/130. These are recorded separately from the fresh IEnumerable runs and require operator-specific profile/disassembly evidence before another change.

### C-IEnumerableElementAtOrDefaultDirectLoop-27 結果・採否

- 現行direct形状（強制再生成）: `srcCount` を一度評価、`index`を一度評価、null検査、負indexはdefault、`using IEnumerator<T> e=source.GetEnumerator()`、`while(e.MoveNext())`、一致時だけ`Current`を読む。終端メソッドから `Struct1` / `Struct0.Compute` / `TryGetNext` は不在。Disposeはusingで保証。
- 関連テスト: `ProviderContainsShapeTests` をReleaseで実行し net10.0 8/8 pass（net10.0-windows側に同filter対象なし、終了コード0）。
- direct測定（各回新規Headless Worker、`--operator-harness`、DB非更新、UI非起動）:
  - Int32: F `20945 / 20421.053 / 26431.579 ns`、Z `5475 / 8205.263 / 5363.158 ns`、中央値比 `20945 / 5475 = 3.825`、割当 `224 / 160 B`。
  - ClassInt32: F `24171.429 / 21150 / 23328.571 ns`、Z `16214.286 / 13425 / 13757.143 ns`、中央値比 `23328.571 / 13757.143 = 1.696`、割当 `232 / 168 B`。
- wrapper A/B（direct呼出しのみを一時除去→同プロジェクトRelease再生成→各3回→直後にdirect復元）:
  - wrapper Int32: F `24089.474 / 23905.263 / 29182.353 ns`、Z `5184.211 / 5642.105 / 4976.471 ns`、中央値 `24089.474 / 5184.211 = 4.648`、割当 `224 / 160 B`。
  - wrapper ClassInt32: F `31085.714 / 23314.286 / 22442.857 ns`、Z `13942.857 / 13771.429 / 16442.857 ns`、中央値 `23314.286 / 13942.857 = 1.672`、割当 `232 / 168 B`。
- 結論: direct loopはInt32でwrapper比13.1%改善し、ClassInt32は+0.1%未満で実質同値。両方とも割当+64 Bは共通なので、direct loop／scalar wrapperが原因ではない。ZLinqとの差は、未知IEnumerableのiterator状態機械を渡す時点より前または基盤側に残る。
- 採否: **性能目標の採用基準（中央値比<=1.02）には不合格**。ただし静的IEnumerableにおける不要wrapper/Computeの除去はユーザー要求のdirect interface loopを満たし、Int32で有意に改善し同ソース群の2%超回帰は確認されないため、**形状改善として保持**する。全DB採用検証の対象にはまだしない。
- 復旧: A/B後に `RenderDirectEnumerableElementAtOrDefaultInline` を復元し、IEnumerable.FLinqを再強制生成、0 warning / 0 error。
## 2026-08-12 run 248: forced-regeneration current IEnumerable reference snapshot

- Provenance: explicit backup `Work/UI/Benchmark.db.pre-goal-ienumerable-20260812-171127.bak`; no `Benchmark.UI.exe`, Worker, or Headless process was present before launch. `Benchmark.Headless --run-tab System.Collections.Generic.IEnumerable<> --ui-fast-run --paired-db-run` created run 248 (`pairs=194`). This is one fresh Headless Worker pass, not the three-run adoption median and not comparable as the same Worker/binary with old UI run 190.
- Scope: public static `IEnumerable<T>` returned by `CreateSourceEnumerable`; element types `Int32` and `ClassInt32`; each benchmark's terminal is its named LINQ terminal/consumer operation.
- Highest current failures (`FLinq / ZLinq`, equal allocation unless stated): `Int32 Aggregate_seed_func_resultSelector` 164257.143 / 26357.143 = 6.232 (160/160 B); `ClassInt32 AggregateBy_keySelector` 381400 / 71650 = 5.323 (224/224 B); `Int32 Aggregate_func` 112650 / 26966.667 = 4.177 (184/184 B); `Int32 GroupJoin_inner` 219200 / 74900 = 2.927 (131448/213384 B); `ClassInt32 Contains_value` 34533.333 / 15033.333 = 2.297 (192/192 B); `Int32 Zip_tuple` 25888.889 / 11322.222 = 2.287 (320/320 B); `ClassInt32 Where_indexPredicate` 84766.667 / 40700 = 2.083 (168/168 B); `Int32 SelectMany_selector` 48825 / 23800 = 2.051 (2736/2736 B).
- Interpretation: allocation parity does not establish equivalent generated shape. The next audit starts with `Aggregate_seed_func_resultSelector` and `AggregateBy_keySelector`: capture regenerated FLinq and ZLinq code, identify the first static-type loss, and separately record interface dispatch/boxing/delegate/state-machine evidence before changing code.
- No candidate is adopted or rejected from run 248 alone. It is a reproducibility warning that previous targeted three-run results must be treated as separate evidence, not overwritten by a one-pass whole-table run.
## 2026-08-12 C-IEnumerableAggregateDirectLoop-30: rejected and restored

- Benchmark: `System.Collections.Generic.IEnumerable<> / Aggregate_seed_func_resultSelector`; source static type `IEnumerable<T>` from `Shared.Common.TestInfrastructure.Base<T>.srcCount`; element types `Int32` and `ClassInt32`; terminal `Aggregate(0L, (acc,x) => acc + LongNumber(x), acc => (double)acc / 1000000)` followed by `Consumer.Consume`.
- Fresh reference: run 248 (new Headless Worker after forced regeneration; not mixed with UI run 190) recorded `Int32` FLinq 164257.143 ns, ZLinq 26357.143 ns, ratio 6.232, allocation 160/160 B. This is a single full-table snapshot, not an adoption median.
- Baseline FLinq generated shape: `Struct1<T>` stores `IEnumerable<T>` plus `IEnumerator<T>?`; terminal constructs `Struct1`, invokes `TryGetSpan` (runtime array/List probes), then loops through `Struct1.TryGetNext`. Each fallback element performs lazy `GetEnumerator`, interface `MoveNext`, writes `Current`, and increments an index. No boxing, no tuple carrier, no delegate/comparer in this specific source; the source iterator state machine and `IEnumerator<T>` interface dispatch remain.
- ZLinq generated/runtime shape: `FromEnumerable<T>` is a value enumerator. It chooses a collection iterator at construction and, for this iterator source, `EnumerableIterator<T>` initializes `IEnumerator<T>` once then calls `MoveNext`/`Current` through its carrier. Aggregate keeps the value-enumerator loop and calls its delegate API. ZLinq still has the source iterator state machine and interface dispatch, but does not use FLinq's `Struct1`/`TryGetSpan`/`Current` bookkeeping shape.
- Hypothesis and attempted minimum change: lower only `Aggregate(seed, func, resultSelector)` with a direct `using IEnumerator<T>` loop when a direct enumerable expression is available, and make stage-zero/AsEnumerable-only `PipelineEnumerable` transfer DirectEnumerableExpression, span, count, and copy capabilities. The direct form would evaluate source, validate null, initialize seed, loop `MoveNext`/`Current`, apply accumulator/result selector once, and dispose via `using`.
- Result: rejected before measurement. After forced Release rebuild (and again after `dotnet build-server shutdown`), the actual benchmark generated method was unchanged: `using(var __s0 = new Struct1<T>((IEnumerable<T>)@this.srcCount(4096)))`, `TryGetSpan`, then `TryGetNext`. Therefore the candidate did not reach the real `AsX<T>` benchmark fragment. It would be invalid to claim a speedup or run DB adoption measurements for a shape that was not emitted.
- Cause analysis: `AsX<T>` is inlined/normalized by a fragment-building route distinct from the simple test route. The aggregate source did not expose DirectEnumerableExpression at the scalar lowering point. Broadening PipelineEnumerable capability forwarding alone did not alter the emitted method. The exact fragment handoff must be traced before retrying; adding a runtime branch or a wider unwrapping rule without that evidence risks changing non-transparent chains.
- Reversion and verification: removed the Aggregate and PipelineEnumerable experiment; no public API changed. `dotnet test Test/Test.csproj -c Release --no-restore --filter FullyQualifiedName~ProviderContainsShapeTests` passed 8/8 on net10.0 (net10.0-windows had no matching tests). The restored test set covers provider native Contains, array span Contains, comparer preservation, List Reverse, static IEnumerable direct Contains/one-time value evaluation, and ElementAtOrDefault direct loop.
- Decision: rejected/held. No before/after median exists because the real generated shape did not change. Keep as a failed-hypothesis record with the code-shape comparison; revisit only after instrumenting or exposing the actual `AsX<T>` fragment handoff.
## 2026-10-02 GroupJoin: 結果が内側のグループを使わないときに探索表を作らない(採用)

- 対象: `GroupJoin_inner`(`src.GroupJoin(src2,x=>x,x=>x,(outer,inner)=>outer)`)の Array/List/IEnumerable と Persisted/Tsavorite(ildiff で IL が変わったのはこの5行だけ)。
- 形: 比較子が既定、両キー選択子が恒等、結果選択子が外側をそのまま返すとき、探索表(LookupBuilder)を作らない。内側は配列・List・Range・Repeat なら列挙も省き、それ以外は列挙だけする(副作用と例外を残す)。ソース式の評価順と列挙子の構築時点は変えない。Range 同士の既存の迂回経路はそのまま。
- 物差し: 手書き理想版(内側の探索表を作らず外側を流す)。測定は `--multi-run` を修正前後交互に2周ずつ、理想比 FLinq÷理想 の変化で判定。
- 結果(FLinq÷理想、前→後): Array Int32 9.56→1.85、Array ClassInt32 19.65→5.35、List Int32 9.28→1.55、List ClassInt32 21.40→5.00、IEnumerable Int32 8.84→1.94、IEnumerable ClassInt32 8.06→4.27。IL が変わらない対照の Join_inner の変化は0.80〜1.31倍で、改善はこれを大きく超える。
- 採否: 採用。改善は3%を大きく超え、IL が変わった行に悪化は無い。
- 残り: ClassInt32 で理想比4〜5倍が残る。IEnumerable タブの ClassInt32 行全般の遅さ(参照型の共有ジェネリックコード仮説)と同じ原因か別途確かめる。

## 2026-10-03 スカラー終端の Compute を差し替えメソッド内のローカル関数にする(採用)

- 原因: スカラー終端(Count/Sum/Average/Min/Max/MinBy/MaxBy/Single/Last など、`BufferedScalar.RenderComputeInline` を通る23演算子)は、`private struct Struct<TEnum>{ static Compute(ref TEnum source) }` を経由していた。参照型の T(共有ジェネリックコード)では、インライン化された Compute の中の `TryGetNext` が実行時辞書を引いた間接呼び出し(`call rax`)のまま残り、反復子の `MoveNext` も脱仮想化されない(`DOTNET_JitDisasm` で確認)。値型の T では専用コードになるので差が無い。
- 実験(`Work/exp/sharedgeneric`、git 管理外): 参照型で、現行形 27〜28µs、差し替えメソッド内のローカル関数 10.5µs、本体に直接ループ 10.5µs、foreach 6〜7µs。int ではどれも同程度。非ジェネリックな静的クラスのジェネリックメソッドにしても現行形と同じく遅い。
- 修正: Compute をローカル関数 `__compute{番号}` として呼び出し地点のブロックに出し、`TEnum` を具体的な列挙子型に置き換える。インライン化のときの補助構造体(Compute だけを持つ)は出さない。
- 結果(FLinq÷理想、修正前後を交互に2周、ildiff で IL が変わった124行): 幾何平均 0.904倍、ClassInt32 は 0.803倍。IEnumerable の ClassInt32 は LastOrDefault 3.64→1.31、Last 3.08→1.24、Average 2.73→1.16、Sum_selector 2.47→1.13 など 12行が 2.3〜3.6倍 → 1.1〜1.3倍。IL が変わらない539行の変化は中央値4.3%・90パーセンタイル15.4%。2周でこれを超えて悪化した6行は、4周ずつで測り直すと悪化なし(3行は改善)。
- 形状テスト6件は、旧形(`Struct.Compute(`・`TEnum`)と、使われない補助構造体の中の文字列に依存していたので、新しい形(`__compute`)と実際の経路で検査するよう直した。IL 監査は構造体の中まで辿れるようになり、Array/List の `Sum`(ソースが yield の反復子)でループ内のインターフェース呼び出しが見えるようになった(TryGetNext 内の遅延 GetEnumerator を含む。許可リストに理由つきで記録)。
- 採否: 採用。

## 2026-10-03 同じソース式どうしの SequenceEqual を比べずに true にする(採用)

- 対象: `SequenceEqual_second`・`SequenceEqual_second_comparer`(既定の比較子)の Array/List/IEnumerable(`this.srcCount(n).AsX<T>().SequenceEqual(this.srcCount(n).AsX<T>())`)。Range 同士は既存の区間比較のまま。
- 形: 両辺が同じ種類の根で、ソース式の文字列が同じで、どちらの木にも `.Anchor` が無く、比較子が無いか既定のとき、`bool r=true;` にする。FLinq の契約では `.Anchor` の無いソース式は純粋で決定的なので同じ列を返し、既定の比較は反射的なので、要素を比べなくても等しい(ソース式も評価しない)。既定でない比較子(反射的とは限らない)、違う式、`.Anchor` 付きは従来どおり比べる。理想版(2026-10-02 書き直し)と同じ前提。
- テスト: `Test/SourceGenerator/SameSourceSequenceEqualTests.cs`(同じ式・既定比較子は評価0回で true、違う式・`.Anchor`・反射的でない比較子は2回評価して比べる)。
- 結果: 次の前後比較(GroupBy の変更とまとめて計測し、行で切り分ける)で記録する。

## 2026-10-03 結果がグループを使わない GroupBy をキーの重複除きにする(採用)

- 対象: `GroupBy_resultSelector`(`(key,g)=>key`)・`GroupBy_elementSelector_resultSelector` の Array/List/IEnumerable と Range。ほかに同じ形の GroupBy 全般。
- 形: 結果選択子がその場に展開できる式で、グループの引数を参照しないとき(構文で判定)、グループを作らずにキーの重複だけを除き、キーの初出で結果を1つ流す。要素選択子は評価しない。後ろに段が続く GroupBy(段をまとめる経路)は「キー選択子の Select・Distinct・キーから結果を作る Select」の3段に置き換え、単独の GroupBy(演算子ごとの経路)は HashSet でキーの初出を判定する列挙子にする。結果の順(キーの初出順)と値は System.Linq と同じ。
- `.Anchor` の扱い: 要素選択子・キー選択子・結果選択子・比較子・前段・ソースのどれかに `.Anchor` があれば従来どおり全件を読んでグループを作る(逐次流すと、後段が途中で止めたときにソースとキー選択子の評価が減るため)。
- テスト: `Test/SourceGenerator/GroupByResultIgnoresGroupTests.cs`(両経路で、要素選択子を評価しない・First や1件目で止める Consume は最初のキーで止まる・`.Anchor` 付きとグループを使う結果選択子は全件評価)。
- 結果: 次の前後比較で記録する。

## 2026-10-03 Range の集計を和の公式と単調性で求める(採用)

- 対象: Range の `Sum`・`Sum(x => (long)x / (double)x / x)`・`Average`・`Average(x => (long)x / (double)x / x)`・`Min/Max(1次式の選択子)`・`Aggregate(seed,(a,x)=>a+x[,結果選択子])`・`Aggregate((a,x)=>a+x)`。ベンチでは System.Range タブの Sum・Sum_selector・Average・Average_selector・Max_selector・Min_selector・Aggregate_func・Aggregate_seed_func_resultSelector(理想比 12〜19倍)。
- 形: 和は count*start+count*(count-1)/2(long に収まる)。int の Sum は checked の逐次加算と同じく、途中の和の最小(負の要素を足し終えたところ)か最後の和が int を外れたら OverflowException。double の逐次加算は途中の和が 2^53 以下の整数なら丸めないので和の公式と同じ値で、超えるときは数え上げる。選択子は構文で x の1次式(x*k+b)か (long)x・(double)x だけを認め、Min/Max は両端の値が int に収まれば端の値、収まらなければ数え上げる。Aggregate の a+x は int・long の2の補数の加算が結合的なので seed+和と同じ。空の例外・Range の引数検査は System.Linq と同じ。選択子・畳み込み関数に `.Anchor` があれば従来どおり。
- 共通部分: `Fragments/RangeArithmeticOptimizer.cs`。
- テスト: `Test/SourceGenerator/RangeArithmeticClosedFormTests.cs`(空・桁あふれ(最後の和と途中の和)・1次式の端が int を外れる・double が丸める大きさ・1次式でない選択子を System.Linq と突き合わせ、数式になった数を生成コードで確認)。形状テスト `RangeSumUsesClosedFormAndSelectUsesUninstrumentedDirectIndexLoop` を新しい形に更新。
- 結果: 次の取り直し(baseline3)で記録する。

## 2026-10-03 Consume へ流すだけの ToArray()/ToList() を作らない(採用)

- 対象: `src.ToArray().Consume(consumer)`・`src.ToList().Consume(consumer)`(Array/List/IEnumerable/Range の ToArray・ToList。理想比 1.2〜6倍)。
- 形: 作った配列・List を流しても元の列を流しても要素と順は同じ。FLinq の契約(`.Anchor` の無い式は副作用・例外なし)では途中の評価の順も観測されないので、作らずに元の列を直接流す。ただし、列に `.Anchor` がある・途中で例外を投げ得る Cast を含む・列が Consume の受け手で消費側の式が評価順の観測できる形(呼び出しなど)のときは従来どおり作ってから流す(System.Linq では列を作り終えてから消費側の式を評価するため。既存テスト `ArrayToListConsumeEvaluatesConsumerOnlyAfterMaterialization` がこの順を検査している)。
- テスト: `Test/SourceGenerator/MaterializeConsumeElisionTests.cs`(1件目で止める Consume で、省いた形は元の列を1件分だけ評価し、`.Anchor`・Cast は全件評価する)。形状テストを `ArrayToListConsumeStreamsSourceWithoutMaterializing` に更新。実 Worker の割り当て測定テストは、割り当てが0になった ToList から ToHashSet に替えた。
- 結果: 次の取り直し(baseline3)で記録する。

## 2026-10-03 Range の構造(一意・昇順・x%C の周期)を使う(採用)

- Range の x%C(C は正の定数)のキー: OrderBy/OrderByDescending(続く恒等の ThenBy/ThenByDescending を含む)は並べ替えずに剰余類ごとに C 刻みで回す根(`RangeModuloOrderRoot`)、DistinctBy は各符号の区間の先頭 C 個を流す根(`RangeModuloDistinctRoot`)、MaxBy/MinBy は start≧0 なら先頭 min(count,C) 個から式で、負を含むときは各符号の先頭 C 個だけを調べる、CountBy は start≧0 なら値 x のキーの個数 (end-1-x)/C+1 を流す。C# の剰余が負になること(キーが -(C-1)..C-1)を扱い、初出順と安定な順は System.Linq と同じ。
- Range の先頭の何もしない段: 一意性を保つ段の後の Distinct、既に並んでいる順の恒等キーの並べ替え(と続く ThenBy)、Skip(0) を除き、先頭の Reverse・逆順の恒等の並べ替えは降順の Range にする(押し下げで Where が前に来ても除ける)。段が残らなければソースをそのまま使う。
- Range の引数検査: `Enumerable.Range` の count<0・上端あふれの ArgumentOutOfRangeException が、列挙子の構造体を通る経路と降順 Range の Consume で出ていなかった(例: `Range(1,-1).OrderBy(…).ThenBy(…).ToList()` が空を返した)。構造体のコンストラクタと降順 Range の Consume で検査する。
- `.Anchor` のある段・キー選択子・ソースは対象外。
- テスト: `RangeModuloOrderTests`・`RangeModuloKeyTests`・`PipelineNoOpNormalizationTests`(負の値・0 をまたぐ区間・int の端・空・不正な引数を System.Linq と突き合わせ)。形状テストを `RangeModuloOrderByConsumeWalksResidueClasses` に更新。
- 結果: 次の取り直しで記録する。

## 2026-10-03 SelectMany(x => Enumerable.Repeat(e,1)) を Select に、OrderBy(x => x) を Order() にする(採用)

- `SelectMany(x => Enumerable.Repeat(e,1)[,(x,y) => r])` は要素ごとにちょうど1つを流すので `Select(x => e)` / `Select(x => r(x,e))` と同じ(Repeat の個数が定数1のときだけ。e は契約上純粋なので1回の評価でよい)。射影融合タブの SelectMany_collection_Select_member(理想比5〜19倍)。
- `OrderBy(x => x)`・`OrderByDescending(x => x)` は、キーを計算しない Order()/OrderDescending() と同じ経路にする(Array/List/IEnumerable の Int32 で理想比 2.6〜3倍だった。Order() は1.1倍)。
- どちらもラムダに `.Anchor` があれば従来どおり。テストは `PipelineNoOpNormalizationTests`(Repeat の個数が2の形・型変換を含む)。
- 結果: 次の取り直しで記録する。

## 2026-10-03 内側が Range で恒等キーの Join は区間で一致を判定する(採用)

- 内側が `.Anchor` の無い Range で、内側のキーが恒等(int)・比較子が既定なら、内側の要素はキーと等しく一意なので、探索表を作らず外側のキーが [始点,始点+個数) に入るかで判定し、一致ごとに結果選択子(外側, キー)を1件流す。内側は列挙しない(Range の列挙は観測されない)。System.Range タブの Join_inner(理想比45倍)。
- テスト: `JoinInnerRangeProbeTests`(重なり・離れ・空・負の値・int の端・外側の重複・外側のキーが恒等でない形、内側のキーが恒等でない形は従来の探索表)。
- 結果: 次の取り直しで記録する。

## 2026-10-03 Range の SkipLast を短い Range として流す・SkipLast を重ねたときの負の件数を直す(採用)

- Range の SkipLast を Consume へ流すとき、待ち行列を使わず先頭からの count-k 個を流す(System.Range タブの SkipLast_count、理想比12倍)。
- 既存の不具合: `SkipLast(a).SkipLast(b)` の捨てる件数を a+b としていたため、a が負(何も捨てない)のとき b の分まで打ち消していた。各件数を0以上にしてから足す。
- テスト: `RangeSkipLastTests`(負の件数・件数超え・空・int の端・不正な引数、重ねた形)。
- 結果: 次の取り直しで記録する。

## 2026-10-03 状態を持たない段の並びを Consume で直接回す(採用)

- 原因: 差し替えメソッドから、パイプラインの列挙子の構造体の TryGetNext を回し、その中でさらに根の列挙子の構造体の TryGetNext を呼ぶ入れ子の形は、参照型の要素(ClassInt32)の共有ジェネリックコードで内側の呼び出しがインライン化されない。実験(`Work/exp/nestedconsume`、git 管理外)で、参照型は入れ子 14.8µs・1段の構造体 4.05µs・直接ループ 4.3µs、int はどれも約4µs。ローカル関数に包んでも 18.6µs で改善しない。押し下げ・射影融合タブの ClassInt32 行が Int32 行より大きく遅い(幾何平均 1.93 対 1.22、2.74 対 2.07)主な原因とみる。
- 修正: 添字を使わない Select/Where だけが2段以上並ぶ列を Consume へ流すとき、span のある根は span を、Range は始点と個数を、それ以外は根の列挙子だけを差し替えメソッドの中で回し、段の式をその場に展開する(単段は既存の経路)。
- テスト: `StatelessChainDirectConsumeTests`(ジェネリックなクラスの中の差し替えで、配列・Range の2〜3段の並びが直接のループになり、結果が同じ)。
- 結果: 次の取り直しで記録する。

## 2026-10-04 span の無い根の単段演算子を差し替えメソッドから直接回す・Range の引数検査を数式の経路にも入れる(採用)

- 入れ子の列挙子の構造体を回す形が参照型の共有ジェネリックコードで遅い問題(2026-10-03 の「状態を持たない段の並び」の節)の続き。span の無い根(yield の IEnumerable など)でも、根の列挙子だけを差し替えメソッドから回し、演算子の処理をその場に書く: 1段の Select/Where(添字つきは差し替えメソッドの局所変数で数える)、SkipLast・TakeLast(待ち行列)、Reverse(借りた配列)、Append/Prepend、TakeWhile、SkipWhile(添字つきを含む)、探索表を省く GroupJoin(外側を直接流し、span があれば span)。
- 段の並びの直接展開から Range を外した: Range は要素が int で入れ子の問題が起きず、Where(偶奇) を2刻みにするなどの Range 専用の経路がある(押し下げ前の融合タブの Range の Select_selector が 1.83→5.94倍に悪化していた)。
- 既存の不具合: `Enumerable.Range` の引数検査(count<0・上端あふれの ArgumentOutOfRangeException)が、数式で求める多くの経路(Count・Any・First/FirstOrDefault・Last/LastOrDefault・ElementAt(OrDefault)・Contains・ToArray・ToList・ToHashSet・ToDictionary・SequenceEqual・Chunk・TakeWhile・TakeLast・Reverse・DefaultIfEmpty・Concat・述語の近道)で抜けていた(例: `Range(1,-1).Count()` が -1)。開始と個数の式を読む箇所を、検査つきの準備コード(`RangeRoot.RenderArgumentPreparation`)に揃えた。
- 既存の不具合(未修正): Append/Prepend の値の式がインスタンスのメンバー(`this.X()`)を使うと、段をまとめる経路の列挙子の構造体の中で `@this` が見えずコンパイルできない。
- テスト: `NonSpanRootDirectConsumeTests`(ジェネリックなクラスの yield ソースで11演算子が System.Linq と同じで、汎用の入れ子の Consume が残らない)、`RangeArgumentValidationTests`(26演算子で不正な引数・int の端・空・通常を System.Linq と突き合わせ)。
- 結果(baseline4→5、理想比): IEnumerable の対象22行の幾何平均 1.87→1.25。ClassInt32 は Where_indexPredicate 3.53→1.18、SkipLast 3.21→1.25、Append 3.05→1.19、Reverse 2.65→1.13 など。Int32 の TakeLast(2.4)・SkipLast(1.9)は待ち行列の形が残る(次の課題)。

## 2026-10-04 Range の ToHashSet・恒等キーの ToDictionary を Consume へ流すだけなら作らない(採用)

- Range の値は一意なので、`ToHashSet().Consume` は Range をそのまま流すのと同じ(集合の列挙順は追加順)、恒等キーの `ToDictionary` は重複キーの例外が起きず (x,x)・(x,要素選択子(x)) を順に流すのと同じ。System.Range タブの ToHashSet(理想比11倍)・ToDictionary 2行(8〜9倍)。
- 実体化の結果を流すだけの形(`IStreamingMaterializeConsume`)として書き換え側に入れ、列が Consume の受け手で消費側の式が評価順を観測できる形なら従来どおり作ってから流す(ToArray/ToList の省略と同じ条件)。恒等でないキーの辞書は作る(重複キーの例外が流す前に起きる)。
- テスト: `StreamingMaterializeConsumeTests`。
- 結果(baseline4→5、理想比): ToHashSet 11.1→0.80、ToDictionary_keySelector 9.2→0.90、ToDictionary_elementSelector 8.5→0.86。

## 2026-10-04 Range の GroupBy(x%C) を剰余類から直接作る(採用)

- start≧0 なら、キーの初出は start から先頭 min(count,C) 個で、値 x のグループは x から C 刻みの (end-1-x)/C+1 個なので、ハッシュを使わずグループごとの配列を作る(Seamless.Linq の `Grouping` に、詰め終えた配列から作るコンストラクタを足した)。負の値を含むときは引数を1回だけ評価して辞書で分ける。結果がグループを使わない GroupBy は、キーの初出の値だけを流して結果を作る(集合を使わない)。System.Range タブの GroupBy_keySelector・GroupBy_elementSelector(6倍)・GroupBy_resultSelector(24倍)。
- テスト: `RangeModuloGroupByTests`(負の値・int の端・空・不正な引数、要素選択子の型変換、グループを使わない結果選択子)。
- 結果(baseline4→5、理想比): GroupBy_keySelector 5.8→1.40、GroupBy_elementSelector 6.1→1.42、GroupBy_resultSelector 22.4→1.28。

## 2026-10-04 SelectMany(x => Repeat(e,1)) の判定を、ラムダの中の LINQ で打ち切られる前に行う(採用)

- 2026-10-03 の Repeat(e,1)→Select の書き換えは、コレクション選択子がラムダの中に LINQ(Repeat)を含むため「文として展開が必要」と判定され、判定に届く前に段の追加が打ち切られていて、射影融合タブの SelectMany 行(9倍前後)に効いていなかった。判定を前に移し、e はラムダの引数・定数・演算子・型変換だけの式に限って構文から取り出す。
- テスト: `PipelineNoOpNormalizationTests` の RepeatResult/RepeatOnly(結果は同じ)。
- 結果(baseline4→5、理想比): 射影融合タブの SelectMany 7行の幾何平均 7.9→1.14(Range 13.6→0.84、ListClassInt32 10.8→0.89、IEnumerableInt32 6.8→1.87)。
- 追記(同日): 射影融合の書き換えが先に `SelectMany(...).Select(...)` を SelectMany 1つ(結果選択子に合成)にしていたため、段をまとめる経路の「単段では受けない」判定で弾かれ、SelectMany 専用の経路(内側を列挙子で回す)になっていた。Repeat(e,1) の SelectMany は単段でも段をまとめる経路で受け、span の直接ループの Select になる。IL 監査の既知例外(外部LINQ・理想形との差、各3〜4行)が解消したので許可リストから外した。

## 2026-10-04 段をまとめた列の Consume を、列挙子の構造体を作らずに差し替えメソッドの中へ展開する(採用)

- 入れ子の列挙子の構造体の問題(2026-10-03・04 の節)の一般形。状態を持つ段(重複除き・集合演算・連結・逆順・Skip/Take・並べ替えなど)を含む段の並びは、これまで「段をまとめた構造体→根の構造体」の2段の入れ子を差し替えメソッドから TryGetNext で回していた。構造体のコンストラクタ引数とフィールドを局所変数、TryGetNext の本体を局所関数(AggressiveInlining)にして差し替えメソッドの中に展開し、局所関数から根と第2ソースの列挙子を直接呼ぶ(入れ子は1段)。Dispose は try/finally で同じものを呼ぶ。
- 対象外: Range(要素が int で問題が起きず、Range 専用の経路がある)、巻き上げた値やデリゲートを持つ関数、フィールドが単純な宣言でないもの(入れ子の型の宣言など)、構造体の型引数(TEnum など)を本体が使うもの。
- テスト: `FlattenedPipelineConsumeTests`(ジェネリックなクラスの参照型の要素で10種類の段の並びが System.Linq と同じで、汎用の入れ子の Consume が残らない)。
- 結果(baseline5→6、理想比): 押し下げタブの ClassInt32 の39行の幾何平均 1.80→1.48(1周目どうし)、タブ全体 1.617→1.397。ArrayClassInt32Concat 3.05→1.08、ListClassInt32 Concat 2.91→1.33、ArrayClassInt32Reverse 3.48→1.54。Int32 の行に目立つ悪化は無い(ListInt32 Intersect_first_Where 1.16→1.63 は前の回にも 1.46 があり、周回差の範囲)。

## 2026-10-04 Range の先頭の Where(値・添字の偶奇)を2刻みで回す(採用)

- 昇順・降順の Range の先頭の Where が `(x&1)==0` などの値の偶奇、または `(x,i) => (i&1)==0` などの添字の偶奇だけを見るとき、述語を評価せずに最初に通る値から2刻みで回し、続く Select/Where(添字なし)はその場に展開する。Range の先頭の何もしない段の除去(Order・Skip(0)・Reverse)と組み合わさる。System.Range の Where_predicate・Where_indexPredicate、押し下げタブの Range の Order/Skip/Reverse の後の Where、融合タブの Range の Where→Select。
- テスト: `RangeParityWhereTests`(昇順・降順、値・添字の偶奇、負の値・int の端・空・不正な引数)。
- 結果(baseline5→6、理想比): System.Range の Where_predicate 1.61→1.10、Where_indexPredicate 1.60→1.14、押し下げタブの Range の Reverse_Where 5.29→0.86。

## 2026-10-04 Range の ToLookup(x%C[,要素選択子]).Consume を剰余類のグループとして流す(採用)

- 索引のグループの列挙順はキーの初出順なので、GroupBy(x%C) と同じく剰余類から直接グループを作って流す(実体化の結果を流すだけの形 `IStreamingMaterializeConsume`)。System.Range の ToLookup 2行(約5倍)。
- テスト: `RangeModuloGroupByTests` に ToLookup の2形を追加。
- 結果(baseline5→6、理想比): ToLookup_keySelector 3.20→1.58、ToLookup_elementSelector 3.55→1.35。
- 補足(同日): 段をまとめた Consume の展開で、バッファを作る段(Reverse など)の「フィールドと同じ名前の局所変数を宣言して最後に this.フィールド へ移す」形は、this. を外すと自分自身への代入になって結果が空になった(テストで検出)。本体で同じ名前の局所変数を宣言するフィールドだけ外側の局所変数の名前を付け替える。並べ替えの段は比較子の型をフィールドに持つので展開しない。Range の先頭の段の除去では、Reverse を添字の無い Where/Select の後ろからでも降順の Range へ移せるようにした(押し下げで Where が Reverse の前に来るため)。IL 監査: List の Reverse_Where(ベンチが IEnumerable へキャスト)の差が見えるようになったので許可リストへ、Range の ToLookup の差は解消したので許可リストから外した。

## 2026-10-04 差し替えたコードの計数の呼び出しを、範囲が開いているときだけ AsyncLocal を引く形にする(採用候補)

- 原因: 生成器は `LinqCounterScope.IncrementDirectSpanUsed()` などを条件なしに差し替えメソッドへ置いている(68か所)。これらは AggressiveInlining で、中の `AsyncLocal<T>.Value`(スレッドの実行文脈の表引き)ごと呼び出し元へ展開されていた。JIT の機械語(DOTNET_JitDisasm)で、配列の Append_element(Int32)の差し替え側だけ要素ごとの `Consumer.Consume<int>` がインライン化されず呼び出しのまま残っていた(理想版はインライン化されて内側の BenchmarkDotNet の Consume だけを呼ぶ)。展開された表引きでインライン化の予算を使い切ったのが原因と見ている(理想版の4倍の時間。List の Index・Range の融合の行も同じ形の候補)。
- 修正: `LinqCounterScope.Start` が開いている範囲の数を静的なフィールドで数え、Increment* はまずそれだけを見る(0 なら何もしない)。AsyncLocal を引く処理は NoInlining の別メソッドへ分けた。範囲が開いているときの数え方は変わらない(別のスレッドで範囲が開いていても、そのスレッドの AsyncLocal は空なので数え間違えない)。
- 結果(baseline6→7、理想比): List の Index(Int32) 3.43→1.17。機械語でも配列の Append の差し替え側で `Common.Consumer.Consume<int>` がインライン化された(理想版と同じ形)。ただし配列の Append(Int32)は 3.2倍のまま(ループの機械語は理想版と同じ。原因は未解明)。

## 2026-10-04 GroupBy・ToLookup・Join の探索表を、グループ番号を振ってから1本の配列へ並べて作る(採用候補)

- 原因: LookupBuilder はキーごとに Grouping を作り、要素を足すたびにグループの配列を 1→2→4… と作り直していた。4096件・1000グループ(x%1000)ではグループあたり約4回の割り当てとコピーになり、理想版(キー→グループ番号の表、件数を数えてから1本の配列へ並べる)の約2倍かかっていた(Array/List/IEnumerable の GroupBy・ToLookup・Join と、押し下げ・射影融合タブの Join で 1.6〜3倍)。
- 修正: `Seamless.Linq.ContiguousLookupBuilder<TKey,TElement>` を追加。キー→グループ番号は Dictionary(既定の比較子は値型で脱仮想化)、null キーは別に1グループ。要素とグループ番号は ArrayPool の作業領域に溜め、最後にグループの末尾から逆順に詰めて1本の配列へ安定に並べる。グループ(`Grouping`)はその配列の区間を指す(区間のグループは要素を足せない)。
  - GroupBy(4形すべて)は初回に全グループを作って追加順の循環リストで流す(従来と同じ形)。
  - ToLookup は索引(バケット)をグループ数から作る。`ToLookup(...).Consume` は索引を作らずにグループを流す(全件を積んでから流すのは同じなので、選択子の評価の回数と順は変わらない)。
  - Join は内側を並べた借り物の配列と、キー→範囲で一致を回す(グループの仮想呼び出し `grouping[k]` も無くなる)。配列の直接 Consume と列挙子の構造体の両方。
- 注意: 1つのグループを持ち続けると結果の配列全体が生き残る(System.Linq はグループごとに配列を持つ)。
- 既存の不具合の修正: `Grouping` の添字が範囲外のとき、span の IndexOutOfRangeException ではなく ArgumentOutOfRangeException を投げる(IList の約束。System.Linq と同じ)。
- テスト作成中に見つけた既存の制限(未修正): ToLookup などの比較子の式は生成コードへそのまま写されるので、using の無い型名(`StringComparer`)は解決できない。文字列を `sink.Consume(x)` へ渡すと文字の列として流す形に読まれる。展開する本体の中の、修飾の無い静的メソッドの呼び出しや、ラムダの中の `is ICollection<T> c` は生成コードでコンパイルできない。
- テスト: `ContiguousLookupBuilderTests`(順・null キー・比較子・区間のグループの IndexOf/CopyTo/Add・探索・伸長)、`ContiguousLookupGenerationTests`(参照型と int、配列と IEnumerable、null キー、内側の重複、比較子、空、Join の後の Where)。
- 結果(baseline6→7、理想比): GroupBy_keySelector は Array/List/IEnumerable の Int32 が 1.69〜1.81→1.07〜1.13、ClassInt32 が 1.74〜1.78→1.13〜1.45。ToLookup_keySelector 1.64〜1.75→1.06〜1.20(配列の Int32 は 1.97→1.46 で周回差が大きい)。Join_innerKey_Hoisting 1.23〜1.74→1.04〜1.47。

## 2026-10-04 並べ替えの前へも Where を押し下げ、溜めた値が既に並んでいれば並べ替えを省く(採用候補)

- 原因: 押し下げは「ソースが span を出せる先頭の Order」の前へは Where を移さなかった(ソースが既に昇順なら並べ替えを省く span の高速経路を守るため)。ソースが並んでいない(計測の既定はシード1でシャッフル)と、全件を並べ替えてから半分を捨てていた(押し下げタブの Order_Where で理想の2〜2.5倍)。
- 修正: 並べ替えの前へも常に押し下げる。恒等キー・既定の比較子の1段の並べ替えで span の高速経路を使わないときは、溜めた値が既に並べ替え後の順(昇順なら非減少、降順なら非増加)かを確かめ、そうなら元の順のまま流す(安定な並べ替えの結果と同じ。並んでいなければ最初の逆転で抜ける)。並んだソースでも並べ替えを省ける。
- テスト: `UnaryPushdownShapeTests`(配列の Order の前へ押し下げ、溜めた値の確認を出す)、`UnaryPushdown`(比較で等しく区別できる要素が元の順のまま。既に昇順・既に降順・並んでいない × Order・OrderDescending・OrderBy(恒等))。
- 結果(baseline6→7、理想比): 押し下げタブの Order_Where の12行が 1.84〜2.40→0.95〜1.11(Skip を挟む行は変わらず)。

## 2026-10-04 Zip(second,resultSelector).Consume を両方の根から直接回す(採用候補)

- 原因: 結果選択子つきの Zip には直接 Consume が無く、Zip の列挙子の構造体から2つの根の構造体を呼ぶ入れ子を回していた(参照型の共有ジェネリックコードで内側がインライン化されない。Zip_second の ClassInt32 で理想の2〜2.4倍)。
- 修正: 両方が span を出せれば短い方の長さまで添字で回し、そうでなければ first を先に進めて尽きたら second を進めない(System.Linq と同じ)。
- テスト: `ZipResultSelectorConsumeTests`(参照型と int、span の有無、長さの違い、捕捉した引数、second を進めた回数)。
- 結果(baseline6→7、理想比): Zip_second の ClassInt32 は Array 2.11→1.07、List 2.09→0.97、IEnumerable 2.22→1.50。IEnumerable の Int32 は 1.17→1.51 と悪化(2つの IEnumerable の根を差し替えメソッドから回す形。要調査)。

## 2026-10-04 span の無い根の TakeLast・SkipLast を環状の配列で回す(採用候補)

- 原因: 待ち行列(ValueQueue)へ入れて溢れたら取り出す形で、要素ごとに Enqueue/Dequeue の境界判定と添字の折り返しが2回ずつあった(IEnumerable の TakeLast_count で理想の2.4倍、SkipLast_count で1.9倍)。
- 修正: 理想版と同じく、満ちたら最も古い位置へ上書きする環状の配列(ArrayPool)にする。配列は件数の見込みが立つまで倍々に借り直す。SkipLast(0) はそのまま流す。
- テスト: `NonSpanRootDirectConsumeTests` に件数 0・3・1500・2400・3000(2500件の列。初めの1024を超えて伸びる場合を含む)の TakeLast/SkipLast を追加。
- 結果(baseline6→7、理想比): IEnumerable の Int32 の TakeLast 2.48→1.21、SkipLast 2.00→1.31。ClassInt32 の SkipLast は 1.21→1.80(周回差 1.06 で判定できない。1周目は 3.05)。

## 2026-10-04 Range 同士の恒等キーの Join は区間の重なりだけを回す(採用候補)

- 外側も .Anchor の無い Range で外側のキーが恒等なら、一致するのは2つの区間の重なりの値だけで、内側の要素も外側の要素と等しい。外側を全部回して区間の内外を調べず、重なりだけを回す(引数の検査は両方の Range について行う)。System.Range の Join_inner(理想の2.2倍)。
- テスト: `JoinInnerRangeProbeTests` に不正な件数(両側)・int の端・重ならない区間を追加し、重なりの経路が出ることを確かめる。
- 結果(baseline6→7、理想比): System.Range の Join_inner 2.10→0.96。

## 2026-10-04 Range の Where の後の Union は、Range の部分を集合に入れずに流す(採用候補)

- 原因: 押し下げタブの Range の Union_second_Where(理想の9倍)は、Range.Where(p).Union(second.Where(p)) の段の並びで、Range の部分の値もすべて集合へ入れていた。
- 修正: 昇順の Range の後に添字の無い Where だけが並び、最後が Union(既定の比較子・int)のとき、Range の部分の値は一意なので集合を使わずに流す(述語が1つで偶奇なら2刻み)。第2ソースの値 v は「区間に入り、Range の部分の述語をすべて通るか」で Range の部分に出たかが決まるので、それ以外だけを集合で重複除きする(述語は .Anchor が無く純粋)。第2ソースの式は従来どおり Range の引数の検査の後、Range を流す前に評価する。
- テスト: `RangeUnionConsumeTests`(偶奇・3の倍数・2つの Where・Where なし・遅延の第2ソース、負の値・空・不正な件数・int の端)。
- 結果(baseline7→8、理想比): 押し下げタブの Range の Union_second_Where 9.41→3.00(まだ3倍。理想版は第2ソースの偶数だけを集合に入れる)。

## 2026-10-04 結果選択子が内側の要素を使わない Join は、キーごとの件数だけを数える(採用候補)

- 原因: `Join(inner,ok,ik,(a,b) => a)` でも内側の要素を探索表に溜めていた(Join_inner で理想の1.8〜3倍。理想版はキーごとの件数だけを Dictionary で数える)。
- 修正: 結果選択子が展開できる式で第2引数が本体に現れないなら、内側はキーごとの件数だけを数え(Dictionary と CollectionsMarshal.GetValueRefOrAddDefault)、外側のキーの件数だけ結果を流す。内側の列挙とキー選択子の評価の回数・順は変わらない。配列の直接 Consume と列挙子の構造体の両方。
- テスト: `ContiguousLookupGenerationTests` に、配列・IEnumerable・null キーの3形を追加。
- 結果(baseline7→8、理想比): Join_inner は List の ClassInt32 2.27→1.31、IEnumerable の ClassInt32 1.43→1.22、Int32 1.33→1.15・1.20→0.99。

## 2026-10-04 IEnumerable の根を最後まで回すときは、列挙子を局所変数に取って直接回す(採用候補)

- 原因: 差し替えメソッドが IEnumerable の根を最後まで回すとき、根の構造体の TryGetNext(遅延の GetEnumerator・`Current` と添字の保存)を要素ごとに呼んでいた。実験(Work/exp/ienumroot。時間で区切った温めと交互計測)では int の要素で理想版(foreach)の1.45倍、列挙子を局所変数に取って MoveNext/Current を直接呼ぶと1.04倍(参照型では差は1割程度)。IEnumerable の Zip(2つの根)で理想の1.5〜2.5倍。
- 修正: IEnumerable の根の構造体に `GetRemainingEnumerator()`(続きから回る列挙子。破棄は根の Dispose が受け持つ)を足し、最後まで回すループ(直接 Consume の各経路・集合演算/重複除き/連結の走査・Zip・Aggregate など)は `var e=root.GetRemainingEnumerator(); while(e.MoveNext()){ var x=e.Current; …}` にする。span を出せる場合の分岐はそのまま。
- テスト: 既存の `NonSpanRootDirectConsumeTests`・`ZipResultSelectorConsumeTests` などで結果が同じ。`NonSpanRootDirectConsumeTests` に直接回す形の確認を、`PerformanceShapeTests.EnumerableRuntimeSpanPathKeepsFallback` を新しい形に合わせた。
- 結果(baseline7→8、理想比): IEnumerable の Zip_tuple は ClassInt32 2.54→0.90・Int32 2.13→0.98、Zip_second は ClassInt32 1.50→0.79・Int32 1.51→0.96、TakeLast(Int32) 1.21→0.95、SkipLast(Int32) 1.31→0.99、射影融合の IEnumerableInt32 Select_member 1.73→1.13。IEnumerable タブ全体 1.093→1.021。

## 2026-10-04 span を出せる根の Reverse と Where/Select の並びは、span を末尾から回す(採用候補)

- 原因: 押し下げで Reverse().Where(p) は Where(p).Reverse() になり、通った要素を配列へ溜めてから逆順に流していた(押し下げタブの Reverse_Where で理想の1.6〜2.6倍。理想版は配列・List を末尾から回して述語を評価する)。
- 修正: span を出せる根(配列・List)の後に、添字の無い Where/Select と Reverse が1つだけ並ぶとき、Reverse は要素ごとの段と入れ替えられる(Where/Select は順を保つ)ので、span を末尾から回して段をその場に展開する。述語・選択子を評価する順が逆になるので、.Anchor があれば使わない。
- テスト: `SpanReverseConsumeTests`(参照型と int、Reverse の前後の Where/Select、List、空)。

## 2026-10-04 Join の外側・内側が根と Where の並びなら、根を直接回す(採用候補)

- 原因: 押し下げで Join の両側に Where が付くと、Join の列挙子の構造体→Where の構造体→根の構造体の入れ子になり、参照型の共有ジェネリックコードで内側がインライン化されなかった(押し下げタブの Join の ClassInt32 で理想の1.5〜2.4倍)。
- 修正: 外側・内側が「根(配列・List、または IEnumerable)の上に添字の無い Where が0個以上」の形なら、差し替えメソッドの中で内側の根を回して探索表(または件数)を作り、外側の根を回して述語・キー・結果選択子をその場に展開する。評価の順は従来と同じ(外側・内側のソース式、内側を最後まで、外側の順)。
- テスト: `ContiguousLookupGenerationTests` に押し下げで両側に Where が付く形(配列と IEnumerable、List)を追加。

## 2026-10-04 結果がグループを使わない GroupBy は、根を直接回してキーの初出だけ流す(採用候補)

- 原因: `GroupBy(k,(key,g) => key)` などはキーの重複除きとして逐次流していたが、GroupBy の列挙子の構造体から根の構造体を呼ぶ入れ子で、参照型の共有ジェネリックコードで内側がインライン化されなかった(GroupBy_resultSelector・GroupBy_elementSelector_resultSelector の ClassInt32 で理想の1.6〜2.0倍)。
- 修正: ソースが根(配列・List、または IEnumerable)なら、差し替えメソッドの中で根を直接回し、キーの初出のときだけ結果を流す(要素選択子は評価しない。従来の逐次の形と同じ契約)。
- テスト: 既存の `GroupByResultIgnoresGroupTests`(キー選択子・要素選択子の評価回数)と、`ContiguousLookupGenerationTests` に null キー・IEnumerable の要素選択子つきを追加。

## 2026-10-04 段の並びの Except/Intersect は第2ソースの集合1つで判定し、同じ型への変換を省く(採用候補)

- 原因: 段の並び(Where の後の Except/Intersect など)は、第2ソースの集合の Contains と、出したものを覚える別の集合への Add の2回の表引きをしていた(System.Linq と理想版は、Except は第2ソースの集合への Add、Intersect は第2ソースの集合からの Remove の1回)。第2ソースの値を集合へ足すときやUnion/Concat の第2ソースの値に、型が同じでも `(T)(object?)x` の変換を書いており、参照型の共有ジェネリックコードでは要素ごとの型検査の呼び出しになっていた(押し下げタブの Intersect_first_Where で理想の2.1〜2.7倍)。
- 修正: Except は `if(!set.Add(k)) continue;`、Intersect は `if(!set.Remove(k)) continue;` にして集合を1つにする(By 形も同じ)。第2ソースの要素の型が同じなら変換を書かない。
- テスト: `SetOperationWhereConsumeTests`(参照型と int、Where の前後、両側の Where、List、IntersectBy/ExceptBy、重複)。

## 2026-10-04 集合演算のソースが根と Where の並びなら、根を直接回す(採用候補)

- 原因: 集合演算(断片)のソース・第2ソースが Where の列挙子の構造体だと、差し替えメソッド→Where の構造体→根の構造体の入れ子になる(参照型の共有ジェネリックコードで内側がインライン化されない)。
- 修正: どちらかに Where があり、両方が「根(配列・List、または IEnumerable)の上に添字の無い Where が0個以上」の形なら、根を直接回して述語をその場に展開する(Join と同じ補助 `TryPeelWheres`・`RenderRootLoop` を Fragment へ移して共有)。

## 2026-10-04 先頭が Concat で後ろに Where/Select が並ぶ形と、Range の Union の第2ソースは、根を直接回す(採用候補)

- 原因: 押し下げタブの Range の Concat_second_Where(理想の3.0倍)は、Concat と Where の段の並びを列挙子の構造体で回していた(Concat への押し下げは利得が無いので書き換えない方針)。Range の Union_second_Where(3.0倍)は、Range の部分は集合に入れずに流すようになったが、第2ソース(Where の付いた IEnumerable)は Where の構造体→根の構造体を回していた。
- 修正: 先頭が Concat で後ろに添字の無い Where/Select だけが並ぶとき、段は要素ごとなので、第1ソース・第2ソースの根(Range・span を出せる根・IEnumerable。第2ソースは Where を剥がした根)をそれぞれ差し替えメソッドの中で直接回して段をその場に展開する。Range の部分で段が偶奇の Where 1つなら2刻み。Range の Union の第2ソースも根と述語に分けて直接回す。評価の順は従来と同じ(第1・第2ソースの式、第1ソースの要素、第2ソースの要素)。
- テスト: `ConcatThenStatelessConsumeTests`(Range+偶奇、Select の後の Where、配列+Where の付いた遅延の第2ソース、Where の付いた遅延の第1ソース+Range、負の値・空・不正な件数・int の端)。

## 2026-10-04 根と Where に分ける補助が、押し下げで作られた段の並びの Where も剥がす(採用候補)

- 原因: 押し下げで Join の両側に付く Where は Where の断片ではなく段の並び(PipelineEnumerable)で作られるため、`TryPeelWheres` が剥がせず、Join・集合演算の「根を直接回す」経路が押し下げタブの行で使われていなかった(生成コードを書き出して確認)。
- 修正: 段がすべて添字の無い Where の段の並びも、根と述語に分ける。
