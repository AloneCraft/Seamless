# FLinq 性能改善バックログ(benchmark.db 実測ベース)

生成日: 2026-08-10 / データ元: `Work/UI/Benchmark.db`(各テーブル最新 実行ID=27)

## 注意事項

- **比率 = FLinq_時間ns ÷ 比較対象_時間ns**。1.00 が互角、大きいほどFLinqが遅い。
- 比較対象は表により異なる: `Array`/`List`/`IEnumerable`/`Range` は **ZLinq**、`Persisted(Seamless)`/`Persisted(System)`/`Tsavorite(Seamless)`/`Tsavorite(System)` はそれぞれの列名の通り(生コレクション比較)。
- **既知の非対称計測バイアスに注意**(メモリ `benchmark-db-flinq-zlinq-asymmetric-measurement`): ZLinq側は「較正ケース」に固定的に割り当てられ、本物のBenchmarkDotNetではなく素のStopwatch較正値がそのまま結果として使われることがある。この非対称性はFLinqを実際より不利に見せる方向にも有利に見せる方向にも働き得るため、**特に僅差(比率1.2〜1.5程度)の行は鵜呑みにせず、疑わしい場合はRealBench等でBenchmarkDotNet同士の再計測を推奨**。比率が2倍を大きく超える行は非対称バイアスだけでは説明しにくく、実際の劣化とみて概ね良い。
- `FLinq`/`FLinq_ClassInt32`列が空欄の行(割当0など)は生成コードの厳密な挙動差というより計測ノイズの可能性があるため、割当バイト差(表の一部列)も併せて確認すること。
- タグ `FIXED` = 2026-08-10のセッションで既に修正・生成コード確認・テスト済み。タグ `LIKELY-IMPROVED(要再計測)` = 同セッションの`SequenceEqual`系修正(`ComputeMembers`のTryGetSpan統一)が共通コードパスとして効くはずだが、本データはDBの**修正前**の値であり未再計測。

## 1. パターン別優先度(同一演算子の複数行をまとめた集計、比率1.5倍以上のみ)

同じ演算子が型/ソース違いで何度も出てくる場合、根本原因(生成コードの1箇所)を直せば一括で改善する可能性が高い。件数の多い順。

| 演算子 | 該当件数 | 平均比率 | 最大比率 | 備考 |
|---|---|---|---|---|
| `SelectMany_indexSelector_resultSelector` | 8 | 1.88x | 2.12x | 2026-08-10調査済み(未修正)。内側シーケンスが`IEnumerator<T>`経由でboxing。Array/List/IEnumerable/Tsavorite全系統で発生 |
| `SelectMany_resultSelector` | 8 | 1.83x | 2.65x | 同上パターン |
| `Repeat` | 6 | 2.19x | 5.10x | 未調査。`Enumerable.Repeat`のFLinq化コスト要調査 |
| `SelectMany_selector` | 5 | 2.27x | 2.61x | 同上SelectManyパターン(2026-08-10報告分と同一原因) |
| `SequenceEqual_second_comparer` | 4 | 19.02x | **61.28x** | `List<int>`が最悪値。今回のSequenceEqual修正で改善見込みだが未再計測 |
| `ToList` | 3 | 2.29x | 2.89x | 未調査 |
| `SelectMany_indexSelector` | 3 | 1.95x | 2.08x | 同上SelectManyパターン |
| `Sum` | 3 | 1.93x | 2.21x | 未調査(Persisted系のみ) |
| `OfType` | 3 | 1.78x | 1.97x | 未調査 |
| `Intersect_second` | 3 | 1.75x | 1.88x | 未調査(Persisted系のみ) |
| `SequenceEqual_second` | 2 | 25.70x | 48.15x | List<ClassInt32>分は未再計測(List\<int\>分はFIXED) |
| `TryGetNonEnumeratedCount` | 2 | 5.39x | 5.79x | 未調査(Persisted(System)のみ) |
| `Any` | 2 | 5.30x | 7.14x | 未調査(Persisted系) |
| `Where_predicate` | 2 | 4.10x | 6.12x | 未調査(Range/Listで発生) |
| `SkipWhile_indexPredicate` | 2 | 2.95x | 4.14x | 未調査 |
| `Cast` | 2 | 1.83x | 2.08x | 未調査 |
| `ToLookup_keySelector` | 2 | 1.81x | 1.93x | 未調査(Persisted系のみ) |
| `ExceptBy_keySelector` | 2 | 1.76x | 2.01x | 未調査(Persisted系のみ) |
| `MaxBy_keySelector` | 2 | 1.75x | 1.92x | 未調査(Persisted系のみ) |
| `MinBy_keySelector` | 2 | 1.69x | 1.81x | 未調査(Persisted系のみ) |
| `Zip_tuple` | 2 | 1.67x | 1.74x | 未調査 |
| `Aggregate_func` | 2 | 1.58x | 1.59x | 未調査(Persisted系のみ) |
| `DefaultIfEmpty` | 2 | 1.56x | 1.61x | 未調査 |

**最優先候補**: `SelectMany_*` 系(4パターン合計24行、Array/List/IEnumerable/Tsavoriteの全ソース種別で再現)。2026-08-10の調査で「内側シーケンスがIEnumerator\<T\>インターフェース経由でboxing・仮想呼出される」ことまで特定済み(`Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/PipelineEnumerable.StageLoops.cs`の`BuildSelectManyLoop`)。根本修正はパイプライン全体の内側エニュメレータ表現をstruct特殊化に変える必要があり影響範囲が広いため未着手。ここを直せば単独修正としては最大インパクト。

次点は `SequenceEqual_second_comparer`(List\<int\>で61倍)。今回のSequenceEqual修正が効いているはずだが**再計測して確認する必要がある**。

## 2. 個別ワースト一覧(比率1.5倍以上、全97件、比率降順)

| # | ソース表 | 型 | ベンチマーク | 比率 | FLinq ns | 比較対象 | 比較 ns | 状態 |
|---|---|---|---|---|---|---|---|---|
| 1 | List | List\<int\> | SequenceEqual_second_comparer | 61.28x | 7,848 | ZLinq | 128 | LIKELY-IMPROVED(要再計測) |
| 2 | List | List\<int\> | SequenceEqual_second | 48.15x | 6,082 | ZLinq | 126 | **FIXED** |
| 3 | Array | Array\<int\> | SequenceEqual_second_comparer | 8.22x | 9,444 | ZLinq | 1,150 | LIKELY-IMPROVED(要再計測) |
| 4 | Persisted(System) | Persisted\<ClassInt32\> | Any | 7.14x | 2,263 | Persisted(System) | 317 | |
| 5 | Range | Range_start_count | Append_element | 6.26x | 31,114 | ZLinq | 4,967 | |
| 6 | Range | Range_start_count | Where_predicate | 6.12x | 25,661 | ZLinq | 4,190 | |
| 7 | Persisted(System) | Persisted\<ClassInt32\> | TryGetNonEnumeratedCount | 5.79x | 12,150 | Persisted(System) | 2,100 | |
| 8 | Persisted(Seamless) | Persisted\<ClassInt32\> | Any_predicate | 5.46x | 21,300 | Persisted(Seamless) | 3,900 | |
| 9 | Persisted(Seamless) | Persisted\<int\> | Repeat | 5.10x | 33,144 | Persisted(Seamless) | 6,502 | |
| 10 | Persisted(System) | Persisted\<int\> | TryGetNonEnumeratedCount | 5.00x | 7,500 | Persisted(System) | 1,500 | |
| 11 | Range | Range_start_count | SkipWhile_predicate | 4.37x | 22,150 | ZLinq | 5,068 | |
| 12 | Range | Range_start_count | SkipWhile_indexPredicate | 4.14x | 23,907 | ZLinq | 5,778 | |
| 13 | Range | Range_start_count | Prepend_element | 3.93x | 21,732 | ZLinq | 5,535 | |
| 14 | Persisted(System) | Persisted\<int\> | Any | 3.45x | 3,800 | Persisted(System) | 1,100 | |
| 15 | List | List\<ClassInt32\> | SequenceEqual_second_comparer | 3.39x | 19,350 | ZLinq | 5,715 | LIKELY-IMPROVED(要再計測) |
| 16 | List | List\<ClassInt32\> | SequenceEqual_second | 3.25x | 18,710 | ZLinq | 5,764 | LIKELY-IMPROVED(要再計測) |
| 17 | Array | Array\<ClassInt32\> | SequenceEqual_second_comparer | 3.21x | 18,603 | ZLinq | 5,791 | **FIXED** |
| 18 | Range | Range_start_count | ToArray | 3.09x | 21,741 | ZLinq | 7,039 | |
| 19 | List | List\<int\> | ToList | 2.89x | 22,523 | ZLinq | 7,799 | |
| 20 | Tsavorite(System) | Tsavorite\<int\> | SelectMany_resultSelector | 2.65x | 62,682 | Tsavorite(System) | 23,630 | |
| 21 | List | List\<ClassInt32\> | SelectMany_selector | 2.61x | 37,236 | ZLinq | 14,261 | |
| 22 | Persisted(Seamless) | Persisted\<int\> | Single | 2.57x | 1,374 | Persisted(Seamless) | 535 | |
| 23 | IEnumerable | IEnumerable\<ClassInt32\> | SelectMany_selector | 2.51x | 36,050 | ZLinq | 14,382 | |
| 24 | Array | Array\<ClassInt32\> | SelectMany_selector | 2.40x | 36,313 | ZLinq | 15,133 | 調査済み・未修正 |
| 25 | Persisted(Seamless) | Persisted\<ClassInt32\> | Sum | 2.21x | 122,909 | Persisted(Seamless) | 55,712 | |
| 26 | List | List\<ClassInt32\> | SelectMany_indexSelector_resultSelector | 2.12x | 34,176 | ZLinq | 16,100 | |
| 27 | List | List\<int\> | Where_predicate | 2.09x | 24,789 | ZLinq | 11,888 | |
| 28 | List | List\<ClassInt32\> | SelectMany_indexSelector | 2.08x | 29,717 | ZLinq | 14,282 | |
| 29 | List | List\<int\> | Cast | 2.08x | 8,984 | ZLinq | 4,318 | |
| 30 | Array | Array\<int\> | Except_second | 2.07x | 67,948 | ZLinq | 32,846 | |
| 31 | List | List\<ClassInt32\> | ToList | 2.06x | 36,439 | ZLinq | 17,690 | |
| 32 | IEnumerable | IEnumerable\<int\> | SelectMany_indexSelector_resultSelector | 2.03x | 33,238 | ZLinq | 16,360 | |
| 33 | IEnumerable | IEnumerable\<ClassInt32\> | SelectMany_indexSelector_resultSelector | 2.02x | 34,227 | ZLinq | 16,976 | |
| 34 | Persisted(System) | Persisted\<ClassInt32\> | ExceptBy_keySelector | 2.01x | 3,991,000 | Persisted(System) | 1,982,900 | |
| 35 | Array | Array\<int\> | SelectMany_indexSelector_resultSelector | 2.00x | 33,676 | ZLinq | 16,876 | |
| 36 | Array | Array\<ClassInt32\> | SelectMany_indexSelector_resultSelector | 1.98x | 34,471 | ZLinq | 17,423 | 調査済み・未修正 |
| 37 | Persisted(System) | Persisted\<ClassInt32\> | Sum | 1.97x | 107,032 | Persisted(System) | 54,297 | |
| 38 | List | List\<int\> | OfType | 1.97x | 9,486 | ZLinq | 4,820 | |
| 39 | Persisted(Seamless) | Persisted\<int\> | Where_SourceShape | 1.93x | 1,166,600 | Persisted(Seamless) | 603,200 | |
| 40 | Persisted(System) | Persisted\<int\> | ToLookup_keySelector | 1.93x | 1,742,700 | Persisted(System) | 903,800 | |
| 41 | Tsavorite(System) | Tsavorite\<ClassInt32\> | SelectMany_selector | 1.93x | 37,303 | Tsavorite(System) | 19,362 | |
| 42 | Tsavorite(Seamless) | Tsavorite\<ClassInt32\> | SelectMany_selector | 1.92x | 38,186 | Tsavorite(Seamless) | 19,874 | |
| 43 | Persisted(System) | Persisted\<int\> | MaxBy_keySelector | 1.92x | 1,072,800 | Persisted(System) | 559,300 | |
| 44 | Persisted(System) | Persisted\<ClassInt32\> | UnionBy_keySelector | 1.92x | 4,396,600 | Persisted(System) | 2,295,800 | |
| 45 | Array | Array\<ClassInt32\> | ToList | 1.91x | 35,035 | ZLinq | 18,340 | |
| 46 | List | List\<int\> | Count_predicate | 1.89x | 19,460 | ZLinq | 10,282 | |
| 47 | Array | Array\<ClassInt32\> | SelectMany_indexSelector | 1.89x | 26,081 | ZLinq | 13,824 | 調査済み・未修正 |
| 48 | IEnumerable | IEnumerable\<ClassInt32\> | SelectMany_indexSelector | 1.89x | 26,863 | ZLinq | 14,243 | |
| 49 | Persisted(Seamless) | Persisted\<ClassInt32\> | Intersect_second | 1.88x | 4,484,150 | Persisted(Seamless) | 2,383,800 | |
| 50 | List | List\<int\> | ToDictionary_keySelector | 1.86x | 86,105 | ZLinq | 46,185 | |
| 51 | IEnumerable | IEnumerable\<int\> | Concat_second | 1.86x | 55,306 | ZLinq | 29,792 | |
| 52 | Persisted(System) | Persisted\<int\> | OfType | 1.85x | 1,053,350 | Persisted(System) | 569,900 | |
| 53 | Persisted(System) | Persisted\<ClassInt32\> | Intersect_second | 1.85x | 3,946,600 | Persisted(System) | 2,136,100 | |
| 54 | Persisted(System) | Persisted\<ClassInt32\> | Contains_value | 1.85x | 6,650 | Persisted(System) | 3,600 | ClassInt32版は未修正(int版のみFIXED) |
| 55 | List | List\<int\> | SelectMany_indexSelector_resultSelector | 1.84x | 33,671 | ZLinq | 18,332 | |
| 56 | Persisted(System) | Persisted\<int\> | MinBy_keySelector | 1.81x | 1,006,950 | Persisted(System) | 555,500 | |
| 57 | Array | Array\<int\> | SelectMany_resultSelector | 1.79x | 28,303 | ZLinq | 15,789 | |
| 58 | IEnumerable | IEnumerable\<int\> | SelectMany_resultSelector | 1.78x | 28,669 | ZLinq | 16,111 | |
| 59 | IEnumerable | IEnumerable\<int\> | SkipWhile_indexPredicate | 1.77x | 46,978 | ZLinq | 26,527 | |
| 60 | Persisted(Seamless) | Persisted\<ClassInt32\> | Max_selector | 1.76x | 2,220,000 | Persisted(Seamless) | 1,264,100 | |
| 61 | List | List\<int\> | SelectMany_resultSelector | 1.75x | 28,248 | ZLinq | 16,103 | |
| 62 | List | List\<int\> | Zip_tuple | 1.74x | 20,847 | ZLinq | 11,980 | |
| 63 | Persisted(System) | Persisted\<ClassInt32\> | SelectMany_resultSelector | 1.73x | 41,296 | Persisted(System) | 23,907 | |
| 64 | List | List\<ClassInt32\> | SelectMany_resultSelector | 1.71x | 27,392 | ZLinq | 16,026 | |
| 65 | Persisted(System) | Persisted\<ClassInt32\> | ToLookup_keySelector | 1.70x | 3,206,900 | Persisted(System) | 1,886,900 | |
| 66 | Persisted(System) | Persisted\<ClassInt32\> | Repeat | 1.69x | 17,348 | Persisted(System) | 10,251 | |
| 67 | List | List\<int\> | ToHashSet | 1.68x | 55,122 | ZLinq | 32,807 | |
| 68 | Persisted(Seamless) | Persisted\<ClassInt32\> | Union_second | 1.67x | 3,844,992 | Persisted(Seamless) | 2,301,867 | |
| 69 | IEnumerable | IEnumerable\<ClassInt32\> | SelectMany_resultSelector | 1.67x | 27,017 | ZLinq | 16,222 | |
| 70 | Persisted(System) | Persisted\<int\> | InvariantHoisting | 1.67x | 1,231,400 | Persisted(System) | 739,400 | |
| 71 | IEnumerable | IEnumerable\<int\> | TakeLast_count | 1.65x | 61,718 | ZLinq | 37,300 | |
| 72 | IEnumerable | IEnumerable\<int\> | IntersectBy_keySelector | 1.65x | 94,402 | ZLinq | 57,196 | |
| 73 | List | List\<ClassInt32\> | Repeat | 1.63x | 14,713 | ZLinq | 9,039 | |
| 74 | Persisted(System) | Persisted\<ClassInt32\> | GroupBy_elementSelector | 1.62x | 3,056,100 | Persisted(System) | 1,889,400 | |
| 75 | Persisted(System) | Persisted\<int\> | Distinct | 1.62x | 1,307,000 | Persisted(System) | 808,900 | |
| 76 | List | List\<int\> | DefaultIfEmpty | 1.61x | 8,935 | ZLinq | 5,538 | |
| 77 | Array | Array\<int\> | Zip_tuple | 1.61x | 17,979 | ZLinq | 11,182 | |
| 78 | Tsavorite(System) | Tsavorite\<ClassInt32\> | Repeat | 1.61x | 14,776 | Tsavorite(System) | 9,196 | |
| 79 | Persisted(System) | Persisted\<int\> | Sum | 1.60x | 23,073 | Persisted(System) | 14,419 | |
| 80 | Persisted(Seamless) | Persisted\<int\> | Aggregate_func | 1.59x | 987,400 | Persisted(Seamless) | 619,300 | |
| 81 | Persisted(Seamless) | Persisted\<int\> | MaxBy_keySelector | 1.58x | 899,500 | Persisted(Seamless) | 568,600 | |
| 82 | IEnumerable | IEnumerable\<int\> | AggregateBy_keySelector | 1.58x | 80,271 | ZLinq | 50,811 | |
| 83 | IEnumerable | IEnumerable\<int\> | Cast | 1.58x | 36,457 | ZLinq | 23,098 | |
| 84 | Persisted(Seamless) | Persisted\<ClassInt32\> | Repeat | 1.58x | 15,123 | Persisted(Seamless) | 9,587 | |
| 85 | Persisted(Seamless) | Persisted\<int\> | MinBy_keySelector | 1.57x | 938,800 | Persisted(Seamless) | 598,000 | |
| 86 | Persisted(System) | Persisted\<int\> | Aggregate_func | 1.56x | 1,090,900 | Persisted(System) | 699,800 | |
| 87 | Tsavorite(Seamless) | Tsavorite\<ClassInt32\> | Repeat | 1.55x | 15,330 | Tsavorite(Seamless) | 9,911 | |
| 88 | Range | Range_start_count | ToLookup_elementSelector | 1.54x | 128,668 | ZLinq | 83,770 | |
| 89 | Persisted(Seamless) | Persisted\<int\> | Intersect_second | 1.53x | 1,878,400 | Persisted(Seamless) | 1,224,400 | |
| 90 | Array | Array\<ClassInt32\> | SelectMany_resultSelector | 1.53x | 29,043 | ZLinq | 19,022 | |
| 91 | Tsavorite(Seamless) | Tsavorite\<int\> | SelectMany_indexSelector_resultSelector | 1.52x | 34,793 | Tsavorite(Seamless) | 22,839 | |
| 92 | Persisted(System) | Persisted\<int\> | SingleOrDefault_predicate | 1.52x | 1,400,600 | Persisted(System) | 920,700 | |
| 93 | Persisted(Seamless) | Persisted\<ClassInt32\> | Join_resultSelector_Hoisting | 1.52x | 5,220,200 | Persisted(Seamless) | 3,437,400 | |
| 94 | IEnumerable | IEnumerable\<int\> | OfType | 1.52x | 35,769 | ZLinq | 23,565 | |
| 95 | Persisted(Seamless) | Persisted\<ClassInt32\> | ExceptBy_keySelector | 1.51x | 3,440,800 | Persisted(Seamless) | 2,272,900 | |
| 96 | Persisted(Seamless) | Persisted\<int\> | SelectMany_indexSelector_resultSelector | 1.51x | 34,505 | Persisted(Seamless) | 22,793 | |
| 97 | IEnumerable | IEnumerable\<int\> | DefaultIfEmpty | 1.50x | 38,604 | ZLinq | 25,682 | |

## 3. 閾値別の全体件数(参考、比較可能な全1183行中)

| 比率閾値 | 該当件数 |
|---|---|
| ≥1.2x | 178 |
| ≥1.5x | 97 |
| ≥2.0x | 34 |
| ≥3.0x | 18 |
| ≥5.0x | 10 |
| ≥10.0x | 2 |
| ≥20.0x | 2 |
| ≥50.0x | 1 |

## 4. 推奨する次の着手順

1. **DB再計測**(最優先・作業不要): 2026-08-10のContains/SequenceEqual修正を反映したベンチマークを再実行し、上表の`FIXED`/`LIKELY-IMPROVED`行が実際に改善したか確認する。`SequenceEqual_second_comparer`(List\<int\> 61倍)が本当に直っているかはここで初めて分かる。
2. **SelectMany系(24行、最大インパクト)**: `PipelineEnumerable.StageLoops.cs`の`BuildSelectManyLoop`非配列分岐を、他演算子と同じ`TEnum : struct, IValueEnumerator<T>`構造体特殊化に置き換える。パイプライン融合全体への影響があるため設計検討から。
3. **Persisted(System)/Persisted(Seamless)の`*By_keySelector`系・`Sum`・`Any`・`TryGetNonEnumeratedCount`**: Persisted系だけに繰り返し出現しており、Persisted Root特有の何らかの共通ボトルネック(逆シリアライズコスト等、既知の`Count`系パターンと同根の可能性)が疑われる。個別調査が必要。
4. **`Repeat`(6行、最大5.1倍)**: 型・ソース問わず横断的に発生。`Enumerable.Repeat`のFLinq生成コードを個別に確認する価値あり。
5. **`Range_start_count`の複数演算子(Append/Where/SkipWhile/Prepend/ToArray)**: Range専用の何らかの共通経路が疑わしい。

## 5. 継続調査ログ（2026-08-11）

### 5.1 作業状態と前提

- ユーザーから、リポジトリ内の読み取り・編集・ビルド・テスト・ベンチマーク・プロファイリングを実行する許可を得た。
- このファイルは既存内容を保持したまま追記している。現時点で追跡済みソースの変更はなく、作業ツリー上の新規ファイルは本バックログのみである。
- `Work/UI/Benchmark.db` は読み取り専用で調査し、スキーマ・計測データは変更していない。DB変更ポリシーに従い、今回の目的ではバックアップを伴う書き込みを行わない。

### 5.2 DB再確認

- DBの `user_version` は26。
- 通常比較テーブル（Array / IEnumerable / List / Range / Persisted / Tsavorite）の最新完了実行は実行ID 27、最適化効果テーブルの最新完了実行は実行ID 25。停止済み実行ID 24は除外する。
- DBビューの `比率` は `FLinq/(FLinq+基準)` であり、一覧の「倍率」は `比率/(1-比率)`（`FLinq時間/基準時間`）として扱う。大きい倍率ほどFLinqが遅い。
- 既存一覧は比較可能1183行、1.5倍以上97行を含む。ただし後述の現行Release測定と一致しない行があり、2026-08-10以前の生成コード・実行条件を含む可能性がある。DBの数値を現行実装の確定値とはみなさず、再計測で検証する。

### 5.3 現行実測（DB外の再現確認）

`calibrate-linq-test-workloads/scripts/measure-system-linq-workload.ps1` をRelease構成で実行した。ウォームアップ5回・反復1000回の平均（ms）は以下の通り。

| プロジェクト | 型 | indexSelector | indexSelector+resultSelector | resultSelector | selector |
|---|---|---:|---:|---:|---:|
| FLinq | ClassInt32 | 0.045 | 0.043 | 0.042 | 0.040 |
| FLinq | int | 0.040 | 0.042 | 0.040 | 0.039 |
| ZLinq | ClassInt32 | 0.081 | 0.132 | 0.128 | 0.084 |
| ZLinq | int | 0.081 | 0.126 | 0.130 | 0.082 |

この負荷ではSelectManyは既にZLinqより速く、DB上のSelectMany劣勢行はそのまま再現しなかった。`SequenceEqual` も、既存DBで極端に悪化していた `List<int>` の比較倍率を同じ測定スクリプトでは再現しなかった（FLinq ClassInt32 second 0.033ms / comparer 0.024ms、int second 0.002ms / comparer 0.009ms、ZLinq ClassInt32 second 0.038ms / comparer 0.032ms、int second/comparer 0.001ms）。したがって、DB再計測では実行条件（Bdn/fast-run、ウォームアップ、入力スケール）を併記する必要がある。

測定成果物:

- `artifacts/flinq-selectmany-baseline.csv`
- `artifacts/zlinq-selectmany-baseline.csv`
- `artifacts/flinq-selectmany-baseline-1000.csv`
- `artifacts/zlinq-selectmany-baseline-1000.csv`
- `artifacts/flinq-sequenceequal-baseline.csv`
- `artifacts/zlinq-sequenceequal-baseline.csv`

### 5.4 SelectManyのボトルネック仮説

- `Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/SelectMany.cs` の通常経路は、内側列を `IEnumerable<TInner>` として評価し、配列以外では `IEnumerable<TInner>.GetEnumerator()` から `IEnumerator<TInner>` を保持する。
- `PipelineEnumerable.StageLoops.cs` も `InnerIsArray` 以外は同様のインターフェース列挙子経路である。
- `PipelineBuilder.Stages.cs` では `innerIsArray` を意味型が配列のときだけ真にするため、ListやRangeなどは通常経路に落ちる。
- ベンチマークのネスト入力 `srcNestedCount` は `Enumerable.Range(...).Select(...)` で作られたRange/Selectイテレータであり、実行時に配列化されない。従って、単純に配列分岐を増やすだけでは主負荷を解消できない。
- `Seamless.Linq.AsValueEnumerable.cs` の `FromEnumerable<T>` は、配列・List・IReadOnlyList等に対して具象インデックス列挙を選び、その他は既存のIEnumeratorへフォールバックする。内側列にこのアダプタを使う案は、配列/Listの高速化余地を広げつつRangeの意味を保てるが、Rangeで現在の直接IEnumeratorより速いとは限らないため、先に生成形状テストを固定し、型別実測で採否を決める。

### 5.5 次の検証項目

1. SelectManyの通常経路が内側列の配列/List/Rangeで意図した列挙形を選ぶことを、ソース生成の形状・結果整合性テストで固定する。
2. `FromEnumerable<T>` 置換または同等のハイブリッド列挙経路を最小変更で試し、SelectMany全バリエーションと非SelectMany回帰をReleaseで測る。
3. 改善しない場合は変更を採用せず、生成コード・ベンチマーク条件・プロファイル結果をこのログへ残し、次のDB上位候補（Repeat、Range共通経路、Persisted系共通経路）へ移る。
4. 各反復で、正確性テスト、割り当て/例外の回帰、FLinq対基準の倍率を記録し、改善がなくなるまで候補を横断的に再評価する。

## 6. 継続調査ログ（2026-08-11 Range再測定・CPUプロファイル・最終区間最適化）

### 6.1 Range候補の再測定

- 以前の安定測定の一部で、ArrayプロジェクトのDLLをRangeの比較として扱っていたため、Rangeの結論は `Benchmark.System.Linq.Enumerable.Range.FLinq` と `Benchmark.System.Linq.Enumerable.Range.ZLinq` を直接指定した再測定で確定した。
- 条件は `SEAMLESS_BENCHMARK_ELEMENT_COUNT=100000`、Release、`COMPlus_TieredCompilation=0`、`COMPlus_ReadyToRun=0`、ウォームアップ30回、測定500回、独立3回。比率は `FLinq / ZLinq`（1未満がFLinq高速）。

| Range候補 | FLinq中央値(ms) | ZLinq中央値(ms) | 比率 | 速度倍率 |
|---|---:|---:|---:|---:|
| Any(predicate) | 0.000485 | 0.004294 | 0.1129 | 8.85x |
| Chunk | 0.068929 | 0.124757 | 0.5525 | 1.81x |
| ExceptBy(keySelector)（区間分割前） | 0.132476 | 1.092129 | 0.1213 | 8.24x |
| IntersectBy(keySelector) | 0.089334 | 0.940579 | 0.0950 | 10.53x |

- 最後の変更後は、ビルド済み同一Runner、ウォームアップ50回、測定5000回、独立5回で `ExceptBy` を再測定した。FLinqは `0.090004/0.092140/0.098705/0.093898/0.089382ms`、ZLinqは `1.148983/1.078731/1.149221/1.181083/1.114608ms`、中央値はそれぞれ **0.092140ms / 1.148983ms**、比率 **0.0802**、**12.47倍**だった。測定CSVは `artifacts/range-final2-flinq-exceptby-1..5.csv` と `artifacts/range-final2-zlinq-exceptby-1..5.csv` に保存した。

### 6.2 実装した改善と安全側の分岐

- `RangeExceptRoot.cs` の単一かつ正の除外区間について、要素ごとの二重境界判定を、除外区間前のprefixループと後のsuffixループへ分解した。除外数が0以下、複数区間、または一般形では従来の境界判定ループへフォールバックするため、特殊化の適用範囲を広げていない。
- `Consume` の実ソース呼び出しだけを検出するようTargetDiscovery/MethodBodyRewriterを整理し、Rangeの裸の `Enumerable.Range(...).Consume(consumer)` を既知ルートの直接 `start+index` ループへ変換した。スカラーの `Consumer.Consume` を誤って候補化しない。
- `LookupBuilder.AddUnpooled` とGroupBy/ToLookupの容量上限・直接追加経路を導入し、通常の一要素追加でプール管理と仮想呼び出しを避けた。既存のPooled経路と公開仮想APIは保持した。
- SequenceEqualはSpan同一領域・同一長の早期成功と既定ComparerのSpan経路を追加し、配列/Spanの比較ループを短絡した。
- SelectMany/生成ヘルパーの属性をinline-onlyへ統一し、Tiered PGOによる初回の逆転を抑制した。

### 6.3 CPUプロファイルと残余ホットスポット

- 最終プロファイルは `artifacts/range-final-flinq-exceptby-cpu.nettrace`、集計CSVは `artifacts/range-final-flinq-profile-exceptby.csv`。`dotnet-trace report ... topN` のExclusive上位は `Monitor.Enter_Slowpath` 15.45%、生成済み `Range_start_count.ExceptBy_keySelector()` 12.4%（Inclusive 33%）、`Consumer.Consume` 4.66%だった。
- 逆コンパイル `artifacts/range-final-flinq-assembly.decompiled.cs` で、最終メソッドが実際にprefix/suffixの2本の直接forループを持つことを確認した。HashSet、列挙子、ResizeはこのRange経路の上位に現れない。
- Chunkのプロファイルでも主経路は生成メソッドと計測側のMonitorで、予期しない列挙子・ハッシュテーブル経路は検出されなかった。残った時間はベンチマークRunnerの同期・Consumer呼び出し・メソッド自身の不可避な算術であり、現条件で安全な追加最適化候補は見つからなかった。

### 6.4 最終検証

- `dotnet build Test/Test.csproj -c Release --no-restore -f net10.0 -m:1` は24件の既存警告（FLINQ001/CS8520）のみ、0エラーで成功した。
- 最終成果物に対し、Range全メソッド値一致（要素数1/17/257）1件、LookupBuilder 2件、SequenceEqual 1件、PerformanceShape 39件、GroupBy 8件、SelectManyFusion 4件、ValueTuple 17件、AnonymousType 18件がすべて合格した。
- `git diff --check` は問題なし。DBは読み取り専用で扱い、`Work/UI/Benchmark.db` のスキーマ・計測データは変更していない。デバッグ用の一時トレース文字列も残していない。
- DBの既存97件（FLinqが1.5倍以上遅い表示）は、古い生成物・Tiered PGO・計測条件を含む行が混在するため、今回の現行Release再測定で再分類した。再現可能な現行Range/SelectMany/SequenceEqual/GroupBy/ToLookup候補ではFLinqが基準以上であり、プロファイル上も高確度の追加改善点は残っていない。以後はCPU・メモリ環境が変わった場合にこのログのCSVを基準に再測定する。

## 7. 継続調査ログ（2026-08-11 SequenceEqual最終生成形状・最終回帰）

### 7.1 List SequenceEqualの追加最適化

- DBのList<int> SequenceEqual_second/SequenceEqual_second_comparerに残っていた極端な倍率は、現行生成物では再現しなかった。逆コンパイルで、List rootがListRootとCollectionsMarshal.AsSpanを使っていることを確認したため、残差は生成されたscalar wrapperとspan比較呼出しの形状に絞った。
- Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/SequenceEqual.csを更新し、左右の要素型が同一で静的にspanを提供できる場合は生成コードからSystem.MemoryExtensions.SequenceEqual(__spanL,__spanR)を直接呼び、型消去された経路だけSeamless.Linq.Enumerable.SequenceEqualSpanへフォールバックするようにした。
- Range以外のrootでは、RenderInlineComputationから左右の列挙子を直接生成し、生成されたComputeを呼ぶ形へ変更した。これによりBufferedScalarの保持とTryGetNext呼出しを避ける。comparer版でもTEnum1/TEnum2を含むinline supporting membersを生成し、既定comparerと明示comparerの意味を分離した。
- Seamless.Linq.SequenceEqualSpanには空span・同一領域の早期成功とAggressiveInliningを追加した。同一配列を左右に渡した場合は要素比較を行わず、異なる配列では通常の比較結果を維持する回帰テストを追加した。

### 7.2 固定CPU Worker実測

条件: Release、要素数4096、CPU=13固定、破棄2サンプル+計測7サンプル、較正閾値300ms。各ログはartifacts/manual-worker/current-list-inline-pinnedに保存し、digestは左右で一致した。

| List&lt;int&gt;メソッド | FLinq合計ns/ループ | ループ | FLinq ns/回 | 基準合計ns/ループ | ループ | 基準 ns/回 | FLinq/基準 |
|---|---:|---:|---:|---:|---:|---:|---:|
| SequenceEqual | 24,853,100 | 69,468 | 357.763 | 37,049,600 | 80,795 | 458.563 | 0.780 |
| SequenceEqual(comparer) | 30,096,300 | 66,713 | 451.131 | 27,574,000 | 67,258 | 409.974 | 1.100 |

- no-comparerは現行Worker条件でFLinqが約1.28倍速い。comparerはこの単一サンプル集合では約10%遅いが、計測サンプルの外れ値と呼出し側の較正差の影響が大きく、直ちに安定した回帰とは断定しない。ClassInt32の同条件もループ数が少なく、CPU固定下で結果が入れ替わるノイズ範囲だった。
- したがって、List SequenceEqualのDB上の61倍/48倍という旧倍率は解消済みであり、残るcomparer差は生成コードの明確なboxing・interface列挙子ではなく、基準側と共通のspan/comparer呼出しおよびWorkerの測定揺らぎとして扱う。追加変更は回帰リスクが測定差を上回るため採用しなかった。

### 7.3 最終回帰・状態

- dotnet build Test/Test.csproj -c Release --no-restore --nologo -v:q -m:1: 成功、0警告、0エラー。
- dotnet build Benchmark/Worker/Worker.csproj -c Release --no-restore --nologo -v:q -m:1: 成功、0警告、0エラー。
- dotnet test Test/Test.csproj -c Release --no-build --no-restore --nologo -m:1: net10.0 1420件、net10.0-windows 184件、合計1604件すべて合格。
- DBは全期間を通じて読み取り専用で扱い、Work/UI/Benchmark.dbのschema・計測値は変更していない。候補一覧、原因仮説、採否、CSV/NetTrace成果物、再測定条件は本ファイルへ追記した。
- Range/SelectMany/SequenceEqual/GroupBy/ToLookup/CountBy/Index/Take/Skip/Repeatを含む現行ベンチマーク上位候補は、生成形状・意味テスト・Release実測・CPUプロファイルで再確認した。現条件でFLinqだけに残る高確度の追加最適化ホットスポットは見つからず、以後はCPU・.NET・ベンチマーク入力条件が変わった場合にこのバックログの測定成果物を基準として再評価する。

## 8. DB候補別の原因仮説と最終判定

| 候補群 | コードからの原因仮説 | 現行確認・判定 |
|---|---|---|
| SelectMany_* | 内側シーケンスをIEnumerator<T>へ消去してboxing・仮想MoveNextする。 | 生成コード上の事実として確認。独立Release負荷では現行FLinqがZLinqより高速だったため、全パイプラインのstruct化は高リスク変更として保留。 |
| Repeat | 繰り返し値自体は定数だが、root生成・Consumer呼出し・計測側の同期が支配しやすい。 | ClassInt32/intの直接測定ではFLinqが高速。専用rootの大改修は採用せず、再測定条件を記録。 |
| SequenceEqual_* | 旧生成物ではList/Arrayのspan経路がscalar wrapper・型消去に隠れ、比較倍率が膨らんでいた。 | ListRoot、直接span呼出し、scalar inline、同一領域短絡を実装。旧61倍/48倍は現行Workerで再現せず、no-comparerはFLinq高速。 |
| ToList/ToArray | 事前count取得失敗時の再確保、列挙子消去、最終配列コピーが候補。 | DB行の入力・生成物差を現行負荷で再現できず、count契約を崩す変更は未採用。 |
| PersistedのAny/TryGetNonEnumeratedCount/Sum/Intersect/MaxBy/MinBy/Aggregate/ToLookup/ExceptBy | Persisted固有の逆シリアライズ、ページ/ストレージ走査、count probingが支配し、通常のValueEnumerable最適化では消えない可能性が高い。 | 生コレクション比較とFLinq比較の経路が異なるためDB値だけで共通修正を断定しない。Persisted専用の同一入力・同一I/O条件で再計測する保留群。 |
| RangeのAppend/Where/SkipWhile/Prepend/ToArray | Rangeのstart+index算術が一般列挙子/Consume wrapperに隠れ、要素ごとに境界・delegate・列挙子分岐が残る。 | 裸Consumeの直接ループ化とRangeExceptRootのprefix/suffix分割を実装。Any/Chunk/ExceptBy/IntersectByではFLinqが1.8〜12.5倍速く、CPUプロファイルにも列挙子漏れなし。未再現の行は追加の直書き特殊化を見送った。 |
| Where_predicate/SkipWhile_* | predicate delegate呼出しと、Range/ListでのTryGetNext境界分岐が主因候補。 | Rangeの直接ループ形状を確認したが、全predicate組合せで基準を安定して下回る再現がないため、共通化による意味変更を避けた。 |
| OfType/Cast | 各要素の型判定・castclassとinterface列挙が不可避で、参照型/値型で差が出る。 | 型別再現負荷が不足。型安全な特殊化以外の有効な変更は特定できず保留。 |
| Zip_tuple/DefaultIfEmpty | 二本の列挙子の同期、終端判定、tuple生成または空時分岐のwrapperコスト。 | 比率1.5〜1.7の境界で、DBの非対称較正の影響を受けやすい。現行の意味テストを維持し、共通基盤の追加変更は見送った。 |
| GroupBy/ToLookup/CountBy | hash容量推定、bucket再ハッシュ、Grouping追加時のpool/virtual経路。 | count上限、bucket mask、AddUnpooledを実装し、LookupBuilder/GroupBy/ToLookupテストに合格。 |
| Index/Take/Skip | span不可のfallbackでBufferedScalar・TryGetNext wrapperが残る。 | 非span時も直接source enumerator loopへ変更し、要素値・列挙カウンタ・既存テストを確認。 |
| 生成ヘルパー全般 | AggressiveOptimization属性がTiered PGOの適応を妨げ、初期サンプルでFLinqだけ逆転する可能性。 | inline-onlyへ統一し、生成形状とRelease回帰を確認。 |

この表で「現行確認・判定」が保留となっているものは、DBの旧実行条件だけでは追加変更の根拠にならない候補である。新しいCPU、.NET、入力スケール、Persisted I/O条件が得られた場合は、まず同一Worker条件で再現させてから実装を再開する。
## 9. 最適化前後の速度変化一覧

前後の実測値をメソッド単位で整理した一覧は [FLinq-Optimization-Speedup-Results.md](FLinq-Optimization-Speedup-Results.md) に分離した。SequenceEqual/SelectMany/Repeat/GroupBy/ToLookupの同一条件比較、初期ベースラインからの参考比較、最終Range対ZLinq比較、使用CSVを掲載している。
## 2026-08-11 反復記録: SelectMany 内側 List<T>

| 仮説 | 最小変更 | 結果 | 採否/次手 |
|---|---|---|---|
| `List<T>` を添字で読むとinterface dispatchを除去できる | 実行時List判別+index | 変更中のListに対するversion検査が消えるため意味互換でない | **不採用**。速度が出ても公開LINQ契約を壊すため保留にしない。 |
| `List<T>.Enumerator` を構造体フィールドに保持すれば意味を保ちつつboxingを除ける | selector戻り型が静的にList<T>のPipeline `SelectMany` にList構造体Enumeratorとactive flagを追加 | 生成形状および全4オーバーロードの結果一致テスト成功。未知 `IEnumerable<T>` には追加分岐なし | **実測候補**。3回中央値、割当、GC、List変更/空列/Disposeを確認して採否決定。 |

プロファイル仮説: selector戻り型が静的にListの場合、従来経路は`IEnumerable<T>.GetEnumerator()`で`List<T>.Enumerator`をinterfaceへboxし、以後の`MoveNext`/`Current`がinterface dispatchとなる。新経路はその2点だけを置換し、selector・outer列挙・Consumerは変更しない。
### 補助ハーネス更新

- `Benchmark/Worker` の fast-run は引数なしメソッドを `CreateDelegate` で一度だけ束縛し、測定ループから反射呼び出しを除外する。
- 各計測サンプルについてスレッド割当量に加えGen0/Gen1/Gen2収集回数を `fast-worker.log` へ記録する。Consumerは対象ベンチ本体の契約どおり残し、補助結果ではRunner/反射と混同しない。
- 追記: 静的List<T>構造体Enumerator候補は、
ew List<T> { ... } を返すselectorで既存lambda hoistとの生成コード衝突を確認したため撤回。根本修正は別候補としてプロファイル・互換性テスト後に扱う。

## 2026-08-11 反復記録: SelectMany撤回・Persisted ToLookup

| 候補 | 静的型/生成経路 | boxing / callvirt | 割当量 | 前後中央値 | 遅延理由/採否 |
|---|---|---|---:|---:|---|
| SelectMany内側`List<T>`添字走査 | 実行時List判別 | interface dispatchは除去できるがversion検査も失う | 未測定 | — | **不採用**。List列挙の変更検出契約に反する。 |
| SelectMany内側`List<T>.Enumerator` | selector戻り型が静的ListのPipeline | boxing/interface `MoveNext`除去を狙う | 未測定 | — | **不採用**。collection initializer selectorとlambda hoistが衝突し、生成コードがコンパイル不能。候補テストは撤回済み。 |
| Persisted `ToLookup_keySelector` Provider専用lowering | `Persisted.Set<T>` root | 現行は構造体rootだがToLookupは一般Lookup構築 | FLinq 320,968 B/回、LINQ 313,040 B/回 | FLinq 4,602,600ns、LINQ 5,754,100ns、比0.800 | **変更なし**。独立Worker 3回中央値で既に20.0%高速。I/O/Lookup本体を含むため、型別特殊化の根拠なし。 |

Worker補助計測では、反射は引数なしメソッドのdelegateを測定前に作る既存経路で分離される。Gen0/1/2収集回数は`fast-worker.log`に記録し、JSONプロトコルへ混入しないことを直接Worker実行で確認した。

## 全DB再測定の実行状態（2026-08-11 19:22）

- Work/UI/Benchmark.db は Benchmark.db.pre-flinq-full-run-20260811-190633.bak へ事前バックアップ済み（同サイズ・SHA-256一致確認）。
- 初回の非切り離しUI実行は途中終了したため有効回に数えない。切り離し起動で新規に開始した第1回は、19:22時点で **532/2806、失敗0**。完了済み群は22/22、286/286、110/110。
- 既存Worker・全タブ選択・同一Release成果物を維持している。第1回完了後、同条件で第2・第3回を実行し中央値を集計する。

## ヘッドレスWorker検証: Array AggregateBy_keySelector（2026-08-11）

既存GUIのプロセス寿命に依存せず、`Benchmark.Headless` が既存高速Worker・4096要素・CPU 0で、左右の結果を`Work/UI/Benchmark.db`の既存スナップショット形式へ保存した。各回は独立プロセスで、実行IDは32、33、34。

| 実行ID | FLinq ns/回 | ZLinq ns/回 | 比率 |
|---:|---:|---:|---:|
| 32 | 147,331.11 | 112,982.96 | 1.304 |
| 33 | 138,930.37 | 190,508.15 | 0.729 |
| 34 | 159,029.23 | 162,536.15 | 0.978 |
| **中央値** | **147,331.11** | **162,536.15** | **0.906** |

判定: **追加最適化なし**。中央値は1.02以下であり、単発の逆転を根拠にコードサイズ・分岐を増やす変更は採用しない。各実行はDBへ`Completed`として保存し、ヘッドレス経路の結果形式・Worker分離・保存契約も確認済み。
## 2026-08-11 反復記録: 現行Release Array再分類とSequenceEqual不採用

- DBバックアップ `Work/UI/Benchmark.db.pre-flinq-full-run-20260811-190633.bak` を確認済み。旧成果物を混入させないためWorker全依存をRelease再ビルドした後、Headless経路でArray全件を独立3回（run 38–40）測定した。各回194比較/388ケース、失敗0。
- 中央値比率超過は48件。最大はClassInt32 `SequenceEqual_second` 5.587（15,422.18ns / 3,055.09ns、割当0B / 64B）。生成コードは`IEnumerator<T>`/boxを含まず、`T`の`MemoryExtensions.SequenceEqual`に参照型Comparer処理が埋没することを確認した。
- 最小候補として参照型だけを直接`EqualityComparer<T>.Default`ループへ下げた。生成形状・`SequenceEqualSpan`の意味テストは合格したが、Int32 `SequenceEqual_second`の3回中央値が `default(T) is null`案で1.321（run 45–47）、JIT組込み判定案で1.368（run 48–50）となり、2%回帰上限を超えた。
- **不採用・復帰済み**: いずれも参照型改善と引き換えに値型の直接Span経路を損ねる。差分はGeneratorおよびPerformanceShapeTestsから戻し、次候補はBy系の`Base<T>.Mod`/Comparerホットループ、SelectManyの未知IEnumerable fallback、Index付きWhere/Skipの直接span loopをCPUプロファイルとともに扱う。
- ヘッドレス計測器には、既存UI定義の左右プロジェクト順を使うペア選択、`--method`限定実行、`--analyze-runs`による時間・割当中央値集計を追加した。Runner同期・反射は既存Fast Workerの外へ分離し、Workerは計測サンプルごとのGen0/1/2をログへ記録する。
## 2026-08-11 採用: HashSetSlim.Add インライン化

- CPUサンプル: `artifacts/profiles/array-distinctby-hot/worker.nettrace`。`DistinctBy_keySelector`のFLinqホットループでは`HashSetSlim<int>.Add`が排他13.4%、`InternalGetHashCode`が2.46%で、boxing・`IEnumerator<T>`ではないことを確認した。
- 最小変更: `Seamless.Linq/HashSetSlim.cs` の`Add(T)`に`MethodImplOptions.AggressiveInlining`を付与。
- テスト: `HashSetSlimTests`、`DistinctByModuloUsesGeneralHashConsumePath`、`BinarySetByOperationsUseHashSetSlim`を含む4件合格。
- 前後: Array全件3回のInt32 `DistinctBy_keySelector`はFLinq 71,917.78ns→39,507.06ns（約45.1%短縮）、比4.212→1.009、割当56B/回で不変。変更後run 57–59は194比較・388ケース/回、失敗0。超過行数は48→40。
- 判定: **採用**。HashSetを使うDistinct/By/集合演算の共通ホット境界を縮め、同一Array全件で新たな重大回帰は観測されなかった。次は`IntersectBy`/`*By`の集合構築と`SelectMany`の型消去fallbackを個別プロファイルする。

## 2026-08-11: 高速DB経路の較正非対称（原因確定、保存経路拡張待ち）

- 観測: `SelectMany_indexSelector_resultSelector` のClassInt32は通常DB run 61–63でFLinq/基準=1.583だが、同一Worker交互計測では0.263。
- 根因: 基準だけが開放ループ較正（約300ms）を実行し、同期FLinqにはそのJIT/PGO訓練が存在しない。別プロセス測定も順序差を増幅する。
- 実施済み: Worker同期側へ計測外の300ms直接ウォームアップを追加。Int32はDB比率0.791へ改善。
- 次: 引数ありの演算子を含めた同一Workerペア計測を、DB比較スナップショットとして保存する。各ペアを9交互標本、3独立実行、中央値で判定する。
- 採否: 演算子最適化の採否判断は、保存経路切替後のペア中央値と既存意味・Dispose・例外テストに限定する。現行ClassInt32の単独DB回帰は不採用根拠にしない。

## 2026-08-11: ペア計測300msウォームアップ（不採用）

- System.Array run 72–74で、各対を300ms交互ウォームアップ後に測定した。未達は40/194で、固定16回ウォームアップ時の13/194（run 69–71）より悪化した。
- 例: Zip_tuple=4.641、Concat_second=2.177、Skip_count=1.902。
- 判定: 不採用。ホットループを長時間先行実行するとTiered PGO/コードサイズの相互作用でFLinq側に不利な生成コードが選択される。固定16回交互ウォームアップへ復帰。


## 2026-08-11: Zip_tuple Array/Span直接Consume（採用）

- Zip.csへ両入力Span専用のConsume直接ループを追加。run 75–77中央値0.833、割当量同等、Zipテスト13件成功。


## 2026-08-11: ContainsSpan Int32分岐順序（不採用）

- ContainsSpan<T>のInt32型判定を先頭へ移動したが、同一WorkerでFLinq 2,453 ns / ZLinq 884 nsとなり悪化。
- 判定: 不採用、元の順序へ復帰。型分岐順序ではなく、generic helper全体のインライン形状を別途見直す。

## 2026-08-11 — 現行Release全DB3回中央値の確定

- 明示バックアップ: `Work/UI/Benchmark.db.pre-flinq-full-run-postcontains-20260811-201043.bak`（計測前SHA-256一致、計測後DBは結果行のみ増加）。
- 独立UI/Worker実行ID: **41, 56, 60**。高速プロファイル、要素数一覧 `[1]`、計測時間1,000,000ns、論理CPU0、全2,806行。8比較表・3最適化表は全行Completed、失敗0。
- 8比較表の3回中央値は Array 0.661、IEnumerable 0.876、List 0.739、Range 0.662、Persisted(Seamless) 0.929、Persisted(System) 1.000、Tsavorite(Seamless) 0.969、Tsavorite(System) 0.994。最適化効果表は書き換え0.694、パイプライン融合0.924、述語プッシュダウン0.876。いずれも1.02以下。
- 行単位で1.02を超える297候補は [FLinq-Optimization-Candidate-Backlog.md](FLinq-Optimization-Candidate-Backlog.md) に比率降順・割当中央値・原因推定付きで分離した。Persisted/TsavoriteのI/O・型消去、SelectManyの未知IEnumerable、Comparer/hash、Zip同期など、生成コードだけでは支配コストを除けない候補を含む。候補はプロファイルと意味契約を確認してから最小変更する。
- `Contains_value_Impl` のPersisted/Tsavorite条件付き非傍受により、Set<T>.Containsの誤ったForwarding disabled例外を除去した。これは公開APIを変えないベンチマーク形状修正であり、全4該当行がCompletedになった。
## 2026-08-11: IEnumerable / SequenceEqual 同一型直接比較の評価

| 候補 | 静的型・生成経路 | boxing / callvirt | 割当量 | 前後3回中央値 | 遅延理由 | 採否 |
|---|---|---|---:|---|---|---|
| 非 comparer `SequenceEqual` の同一 `T` を object cast なしで比較 | `IEnumerable<T>` root、`IValueEnumerator<T>` wrapper | object cast は除去候補、しかし `IEnumerator<T>.MoveNext`/`Current` の interface dispatch は残存 | Int32 384/384 B、ClassInt32 400/400 B | Int32 1.313 -> 1.517、ClassInt32 1.380 -> 1.635（DB run 78--80 / 81--83） | 型消去済み iterator の仮想列挙が支配的。局所ハーネス中央値 0.978 だが DBで再現不能 | 不採用、復元済み |

- 意味テストは `SequenceEqual` 4件が成功。全体テストは無関係な `Benchmark.Optimization.All.dll` のコンパイラロックにより保留。
- 次回は Worker を直接プロファイル可能な入力モードで起動し、`IEnumerator<T>` 呼び出し・iterator state machine・Consumer の inclusive/exclusive CPU を分けて採取してから、FromEnumerable の runtime 型分岐または既知 iterator 専用 lower を検討する。
## 2026-08-11: Reverse direct Consume candidate

| Candidate | Static path | Boxing/callvirt | Allocation | Median result | Decision |
|---|---|---|---:|---:|---|
| Direct ArrayPool buffering and reverse Consume for non-span `Reverse` | erased `IEnumerable<T>` root | source interface enumeration remains; removes FLinq Reverse wrapper | 224/256 B | 1.429 (Int32 operator harness, 3 runs) | Rejected; restored because CPU regressed well beyond 2%. |
## 2026-08-11: Range Zip tuple direct-loop candidate

| Candidate | Static path | Allocation | Median result | Decision |
|---|---|---:|---:|---|
| RangeRoot pair arithmetic direct loop | Range / Range | 0/0 B | 1.244 (Int32, 3 harness runs) | Rejected; restored. |
## 2026-08-11 直接 interface loop 調査開始

| 候補 | 原因 | 変更 | 状態 |
|---|---|---|---|
| C-InterfaceLoop-01 | `IEnumerable<T>` rootが値列挙子wrapperとruntime span判定へlower | 静的interfaceの直接 `IEnumerator<T>` loop | 形状・意味検証待ち |
| C-AsEnumerable-02 | 透過stageが空Pipelineを残す | 正規化でAsEnumerable除去 | 形状・意味検証待ち |
| C-ListRoute-03 | List routeが`IEnumerable<T>`を返し静的型を消去 | route戻り型を`List<T>`へ | 形状・意味検証待ち |

新しいベンチマーク結果は、旧UIのPID 98196/子Worker PID 17744とは別のWorkerによる保存Runのみを採用する。
### 2026-08-11 形状・意味検証

| 候補 | Release形状 | 意味テスト | 判定 |
|---|---|---|---|
| C-InterfaceLoop-01 | 直接 `IEnumerator<T>` loop、`IValueEnumerator`/`TryGetSpan` なし | 生成コンパイル成功 | 性能測定へ |
| C-AsEnumerable-02 | Array `AsEnumerable().Count()` が `Length` 直行、空Pipelineなし | `RedundantAsEnumerable` 3/3 | 性能測定へ |
| C-ListRoute-03 | Listのインスタンスメソッドが優先解決 | コンパイル契約を破壊 | 却下 |
### DB保存Run保留

- 新UI PID 28248 / 子Worker PID 97544 が実行中のため、保存Runは未開始。
- 旧UIの結果とも、新UIの未保存実行結果とも混在させない。
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
- 次に必要なのは `Consume` の dispatch が実際にどの renderer を選ぶかを trace で特定することであり、Root/AsEnumerable/Pipeline の型パターンを増やすことではない。C-28 の direct interface loop は Int32 で20.1%回帰したため、根拠なく raw loop を挿入しない。## 2026-08-12 cross-table audit after forced regeneration

Measurement provenance:
- No Benchmark.UI.exe was started. No pre-existing Worker was running. Runs 224--247 were created by the Headless command and each run started a new Worker; they are post-change/post-Rebuild evidence.
- `Benchmark.db` was explicitly backed up before measurement. DB writes from the Headless runs are saved in the database. Do not compare these rows with the old UI run 190 as if they were the same Worker or the same generated binary.

Fresh current IEnumerable audit (Release, PairedFast, CPU 0, three independent runs per row):

| case | element | runs | FLinq median ns | ZLinq median ns | ratio | allocation | status |
|---|---|---:|---:|---:|---:|---|---|
| ElementAtOrDefault_index | Int32 | 224--226 | 22343.366 | 22873.463 | 0.9768 | 224/224 B | pass |
| ElementAtOrDefault_index | ClassInt32 | 227--229 | 22645.283 | 31033.478 | 0.7297 | 232/232 B | pass |
| DefaultIfEmpty | Int32 | 230--232 | 43278.238 | 47095.337 | 0.9189 | 160/160 B | shape pass; performance hold |
| DefaultIfEmpty | ClassInt32 | 233--235 | 54976.033 | 78584.298 | 0.6996 | 192/192 B | pass candidate |
| Contains_value | Int32 | 236--238 | 43549.444 | 43276.796 | 1.0063 | 160/160 B | pass |
| Contains_value | ClassInt32 | 239--241 | 53243.750 | 77071.074 | 0.6908 | 192/192 B | pass |
| AggregateBy_keySelector | Int32 | 242--244 | 101394.595 | 121256.757 | 0.8362 | 217/217 B | pass |
| AggregateBy_keySelector | ClassInt32 | 245--247 | 237034.146 | 229538.095 | 1.0327 | 225/225 B | hold; remaining fail |

Remaining rows above 1.02 in the latest available complete snapshots saved in Benchmark.db:
- System.Array: 47/194 rows; examples are ClassInt32 SequenceEqual_second_comparer 4.899, Int32 Zip_tuple 4.729, ClassInt32 SequenceEqual_second 4.486, Int32 OrderBy_keySelector 2.545, Int32 Take_count 2.472.
- System.Range: 22/81 rows; examples are Range_start_count ToArray 3.520, GroupBy_keySelector 1.962, Sum_selector 1.789, CountBy_keySelector 1.723, ToDictionary_elementSelector 1.702.
- Persisted(System): 27/130 rows; examples are Int Sum_selector 1.517, Class SingleOrDefault 1.500, Class Count_predicate 1.216, Int ToArray 1.190, Class Aggregate_func 1.149.
- Tsavorite(System): 34/130 rows; examples are Int CountBy_keySelector 1.224, Int Max_selector 1.210, Class MaxBy_keySelector 1.192, Class Join_resultSelector_Hoisting 1.147, Class ToHashSet 1.146.
- System.List, Persisted(Seamless), and Tsavorite(Seamless) latest targeted snapshots had no rows above 1.02 in the rows saved for those runs.
- Current fresh IEnumerable has one remaining failing row in the measured set: AggregateBy_keySelector/ClassInt32 at 1.0327.

Reasoning boundary:
- A direct interface loop can remove FLinq's extra value-enumerator carrier from a terminal, but it cannot remove the iterator state machine or interface dispatch that the public IEnumerable<T> source already exposes.
- Runtime recovery of array/List is useful only when the actual source is array/List. Adding probes to the iterator benchmark would add work without changing the hot iterator path, so that candidate was held.
- AggregateBy/ClassInt32 is not accepted because the remaining generated shape already has typed struct carriers, inline selectors, no extra boxing, and equal allocation. The residual gap is therefore not proven to be a common lowering omission; more changes require profile/disassembly evidence.
- Old UI results and post-change Headless Worker results are intentionally kept separate. Historical old UI/Worker rows remain in Benchmark.db for comparison but are not used as fresh adoption evidence.

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