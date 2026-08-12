# FLinq DB改善候補バックログ（実行ID41/56/60）

生成条件: `Work/UI/Benchmark.db` の8比較表から、各行を実行ID41・56・60で突合し、`FLinq/基準` の3回中央値が1.02を超える297行を比率降順で抽出。単発外れ値を含むため、採用前にCPU/割当プロファイルと同一ソース群の回帰を確認する。

| 表 | 型 | メソッド | 引数キー | 3回中央値 | run41 / run56 / run60 | FLinq ns | 基準 ns | FLinq/基準 割当B | 原因推定 |
|---|---|---|---|---:|---|---:|---:|---:|---|
| Persisted(System) | Persisted<ClassInt32> | Contains_value |  | 439.667 | 41:622.333,56:263.400,60:439.667 | 263,800.0 | 600.0 | 184,984/88 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | Contains_value |  | 421.800 | 41:484.000,56:205.200,60:421.800 | 193,600.0 | 500.0 | 105,712/32 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Contains_value |  | 4.324 | 41:17.505,56:2.591,60:4.324 | 24,388.9 | 5,444.4 | 160/160 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<ClassInt32> | Contains_value |  | 4.235 | 41:26.766,56:3.437,60:4.235 | 59,350.0 | 10,500.0 | 96,264/192 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<int> | SelectMany_selector |  | 2.499 | 41:1.552,56:2.624,60:2.499 | 50,920.0 | 20,380.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| IEnumerable | IEnumerable<int> | Single |  | 2.333 | 41:2.333,56:2.720,60:1.567 | 119.0 | 51.0 | 48/24 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Array | Array<ClassInt32> | Contains_value |  | 1.906 | 41:21.761,56:1.906,60:0.432 | 16,934.7 | 6,378.6 | 24/24 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<int> | SelectMany_indexSelector |  | 1.838 | 41:1.838,56:1.899,60:0.795 | 37,320.0 | 20,300.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| IEnumerable | IEnumerable<int> | Reverse |  | 1.764 | 41:2.014,56:1.574,60:1.764 | 56,933.3 | 33,766.7 | 224/256 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<ClassInt32> | SelectMany_indexSelector |  | 1.740 | 41:2.017,56:1.554,60:1.740 | 40,300.0 | 20,640.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| List | List<int> | Max |  | 1.715 | 41:7.011,56:1.715,60:0.935 | 2,971.7 | 1,732.6 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<int> | OrderByDescending_keySelector |  | 1.698 | 41:1.316,56:1.717,60:1.698 | 449,700.0 | 303,800.0 | 160/192 | Comparer・hash容量・bucket/Grouping生成 |
| Range | Range_start_count | ThenBy_keySelector |  | 1.685 | 41:1.639,56:1.715,60:1.685 | 385,800.0 | 229,000.0 | 96/224 | Comparer・hash容量・bucket/Grouping生成 |
| Range | Range_start_count | OrderByDescending_keySelector |  | 1.657 | 41:1.783,56:1.253,60:1.657 | 374,400.0 | 226,000.0 | 88/128 | Comparer・hash容量・bucket/Grouping生成 |
| List | List<int> | Zip_tuple |  | 1.587 | 41:2.588,56:1.260,60:1.587 | 22,583.3 | 17,300.0 | 64/64 | 2列同期・終端判定・tuple生成 |
| IEnumerable | IEnumerable<ClassInt32> | Single |  | 1.579 | 41:1.579,56:1.923,60:1.570 | 101.8 | 64.9 | 80/48 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | SelectMany_selector |  | 1.573 | 41:2.059,56:1.573,60:1.190 | 28,266.7 | 19,233.3 | 2,736/2,800 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Tsavorite(System) | Tsavorite<ClassInt32> | SelectMany_resultSelector |  | 1.570 | 41:1.570,56:1.615,60:0.489 | 42,225.0 | 26,900.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Range | Range_start_count | OrderBy_keySelector |  | 1.567 | 41:1.518,56:2.019,60:1.567 | 364,800.0 | 230,800.0 | 88/128 | Comparer・hash容量・bucket/Grouping生成 |
| List | List<int> | OrderBy_keySelector |  | 1.539 | 41:1.751,56:1.413,60:1.539 | 534,500.0 | 338,900.0 | 160/192 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | SelectMany_indexSelector |  | 1.528 | 41:1.528,56:2.139,60:0.803 | 36,100.0 | 24,560.0 | 2,736/2,816 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Range | Range_start_count | ThenByDescending_keySelector |  | 1.528 | 41:1.528,56:1.728,60:1.314 | 372,800.0 | 235,100.0 | 96/224 | Comparer・hash容量・bucket/Grouping生成 |
| Array | Array<int> | Zip_tuple |  | 1.528 | 41:1.445,56:1.684,60:1.528 | 20,457.1 | 14,157.1 | 64/64 | 2列同期・終端判定・tuple生成 |
| Array | Array<ClassInt32> | Order |  | 1.510 | 41:0.379,56:1.510,60:1.525 | 658,900.0 | 436,500.0 | 160/192 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | Any |  | 1.500 | 41:1.667,56:1.500,60:1.000 | 300.0 | 300.0 | 80/48 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<int> | Contains_value |  | 1.495 | 41:2.210,56:0.989,60:1.495 | 670.6 | 448.6 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<int> | SelectMany_indexSelector |  | 1.489 | 41:1.489,56:1.563,60:0.837 | 32,300.0 | 21,900.0 | 2,736/2,816 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<int> | SelectMany_resultSelector |  | 1.479 | 41:1.479,56:0.578,60:1.589 | 40,375.0 | 27,300.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| IEnumerable | IEnumerable<int> | TakeLast_count |  | 1.470 | 41:1.692,56:1.340,60:1.470 | 56,000.0 | 36,566.7 | 224/264 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | CountBy_keySelector |  | 1.464 | 41:1.464,56:1.541,60:1.449 | 96,150.0 | 65,300.0 | 160/216 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(System) | Tsavorite<int> | SelectMany_selector |  | 1.460 | 41:1.460,56:1.540,60:1.075 | 28,216.7 | 19,580.0 | 2,736/2,800 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| List | List<ClassInt32> | ToLookup_keySelector |  | 1.455 | 41:1.457,56:1.051,60:1.455 | 186,800.0 | 134,900.0 | 216,968/208,960 | Comparer・hash容量・bucket/Grouping生成 |
| List | List<int> | SelectMany_indexSelector_resultSelector |  | 1.452 | 41:1.486,56:1.452,60:1.255 | 41,725.0 | 28,775.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| List | List<ClassInt32> | Contains_value |  | 1.451 | 41:1.451,56:17.682,60:0.164 | 16,400.0 | 10,887.5 | 24/24 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | OrderBy_keySelector |  | 1.445 | 41:1.445,56:1.269,60:1.483 | 467,100.0 | 327,200.0 | 384/352 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | SelectMany_selector |  | 1.442 | 41:1.394,56:1.442,60:1.488 | 32,860.0 | 22,080.0 | 2,736/2,800 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Tsavorite(System) | Tsavorite<ClassInt32> | SelectMany_selector |  | 1.437 | 41:1.437,56:1.773,60:1.407 | 30,820.0 | 19,620.0 | 2,736/2,800 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Tsavorite(Seamless) | Tsavorite<int> | SelectMany_indexSelector |  | 1.433 | 41:1.472,56:1.433,60:0.773 | 33,880.0 | 24,500.0 | 2,736/2,816 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<ClassInt32> | SingleOrDefault_predicate |  | 1.400 | 41:1.031,56:1.400,60:1.517 | 398,200.0 | 286,000.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<ClassInt32> | Zip_tuple |  | 1.399 | 41:1.399,56:2.086,60:1.239 | 78,200.0 | 48,500.0 | 64/64 | 2列同期・終端判定・tuple生成 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | ToLookup_elementSelector |  | 1.393 | 41:1.439,56:1.393,60:0.609 | 1,190,800.0 | 890,600.0 | 323,088/282,552 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | SingleOrDefault |  | 1.391 | 41:1.609,56:1.391,60:1.292 | 103.5 | 74.4 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Cast |  | 1.386 | 41:1.056,56:1.386,60:1.412 | 416,500.0 | 313,900.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | TakeLast_count |  | 1.379 | 41:1.379,56:1.473,60:1.001 | 107,800.0 | 87,800.0 | 232/272 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | Max_selector |  | 1.361 | 41:1.535,56:1.361,60:0.737 | 1,025,700.0 | 839,300.0 | 133,272/100,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<ClassInt32> | SingleOrDefault |  | 1.356 | 41:2.687,56:1.283,60:1.356 | 99.5 | 77.5 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Count_predicate |  | 1.353 | 41:1.593,56:0.967,60:1.353 | 419,700.0 | 310,200.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | Any |  | 1.333 | 41:1.000,56:1.500,60:1.333 | 300.0 | 300.0 | 104/48 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | Single |  | 1.333 | 41:1.333,56:1.000,60:1.333 | 400.0 | 300.0 | 56/104 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | SelectMany_indexSelector |  | 1.333 | 41:1.333,56:1.519,60:0.915 | 31,320.0 | 22,480.0 | 2,736/2,816 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<ClassInt32> | Single_predicate |  | 1.332 | 41:1.332,56:1.364,60:0.897 | 399,900.0 | 309,900.0 | 229,512/229,560 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | SequenceEqual_second |  | 1.325 | 41:1.417,56:0.652,60:1.325 | 52,100.0 | 38,866.7 | 384/384 | Comparer/型消去またはspan経路の差 |
| Array | Array<ClassInt32> | OrderDescending |  | 1.316 | 41:0.350,56:1.316,60:1.563 | 658,000.0 | 470,800.0 | 160/192 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | ToArray |  | 1.315 | 41:2.066,56:0.963,60:1.315 | 327,100.0 | 248,700.0 | 147,528/147,560 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | DefaultIfEmpty |  | 1.311 | 41:1.874,56:1.138,60:1.311 | 28,800.0 | 20,680.0 | 160/160 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<ClassInt32> | GroupJoin_inner |  | 1.302 | 41:1.751,56:1.302,60:0.758 | 282,300.0 | 216,800.0 | 147,512/213,064 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<ClassInt32> | CountBy_keySelector |  | 1.287 | 41:1.401,56:1.246,60:1.287 | 134,200.0 | 100,600.0 | 168/224 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(System) | Tsavorite<int> | MaxBy_keySelector |  | 1.268 | 41:0.804,56:1.326,60:1.268 | 684,400.0 | 539,900.0 | 18,576/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | SelectMany_selector |  | 1.266 | 41:1.336,56:1.266,60:1.125 | 30,400.0 | 22,750.0 | 2,736/2,800 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<int> | GroupJoin_inner |  | 1.263 | 41:2.113,56:0.937,60:1.263 | 574,700.0 | 485,600.0 | 327,832/442,752 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Range | Range_start_count | Zip_tuple |  | 1.262 | 41:1.262,56:1.402,60:1.208 | 22,900.0 | 17,520.0 | 0/0 | 2列同期・終端判定・tuple生成 |
| Array | Array<ClassInt32> | SingleOrDefault |  | 1.254 | 41:1.162,56:3.147,60:1.254 | 94.7 | 78.6 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | Zip_tuple |  | 1.250 | 41:1.250,56:1.211,60:1.653 | 34,633.3 | 26,300.0 | 384/384 | 2列同期・終端判定・tuple生成 |
| List | List<ClassInt32> | SelectMany_resultSelector |  | 1.247 | 41:1.247,56:2.520,60:0.452 | 59,625.0 | 47,800.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| List | List<ClassInt32> | Max |  | 1.246 | 41:1.569,56:1.246,60:1.147 | 57,600.0 | 42,300.0 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | Distinct |  | 1.243 | 41:1.525,56:1.127,60:1.243 | 60,600.0 | 48,200.0 | 216/216 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<int> | Single_predicate |  | 1.236 | 41:0.990,56:1.344,60:1.236 | 249,000.0 | 245,000.0 | 131,136/131,320 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | Order |  | 1.233 | 41:1.223,56:1.261,60:1.233 | 171,400.0 | 140,200.0 | 64/144 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | UnionBy_keySelector |  | 1.230 | 41:1.332,56:1.230,60:0.874 | 1,601,500.0 | 1,373,700.0 | 151,864/474,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | SelectMany_resultSelector |  | 1.228 | 41:1.385,56:1.228,60:1.151 | 40,000.0 | 32,650.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<ClassInt32> | Join_innerKey_Hoisting |  | 1.225 | 41:1.225,56:1.311,60:1.172 | 709,400.0 | 589,800.0 | 450,904/491,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Sum_selector |  | 1.224 | 41:2.002,56:1.224,60:0.930 | 372,100.0 | 303,900.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | SelectMany_indexSelector_resultSelector |  | 1.218 | 41:1.199,56:1.814,60:1.218 | 43,233.3 | 33,633.3 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Tsavorite(Seamless) | Tsavorite<int> | SelectMany_resultSelector |  | 1.215 | 41:1.395,56:1.138,60:1.215 | 39,966.7 | 32,900.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<ClassInt32> | Min |  | 1.213 | 41:1.426,56:1.017,60:1.213 | 371,900.0 | 289,700.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | SelectMany_indexSelector_resultSelector |  | 1.210 | 41:1.210,56:1.202,60:1.295 | 37,325.0 | 31,050.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| IEnumerable | IEnumerable<ClassInt32> | TryGetNonEnumeratedCount |  | 1.209 | 41:1.209,56:0.819,60:1.783 | 87.2 | 66.2 | 72/72 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | SequenceEqual_second_comparer |  | 1.203 | 41:1.203,56:1.540,60:1.025 | 50,050.0 | 41,600.0 | 384/384 | Comparer/型消去またはspan経路の差 |
| List | List<int> | SelectMany_resultSelector |  | 1.201 | 41:1.142,56:2.490,60:1.201 | 34,725.0 | 30,275.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| IEnumerable | IEnumerable<ClassInt32> | GroupBy_elementSelector_resultSelector |  | 1.199 | 41:1.360,56:1.199,60:0.814 | 195,100.0 | 165,200.0 | 173,544/165,544 | Comparer・hash容量・bucket/Grouping生成 |
| Persisted(System) | Persisted<int> | SelectMany_indexSelector |  | 1.199 | 41:2.163,56:1.060,60:1.199 | 28,200.0 | 21,100.0 | 2,736/2,816 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<ClassInt32> | Sum_selector |  | 1.199 | 41:1.317,56:1.190,60:1.199 | 403,900.0 | 337,000.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | TryGetNonEnumeratedCount |  | 1.198 | 41:1.023,56:1.219,60:1.198 | 75.3 | 62.9 | 64/64 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | SkipWhile_predicate |  | 1.197 | 41:1.202,56:1.197,60:1.189 | 28,766.7 | 24,200.0 | 224/288 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<int> | GroupBy_keySelector |  | 1.196 | 41:1.196,56:1.273,60:0.877 | 736,900.0 | 625,900.0 | 175,472/184,256 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | ToLookup_elementSelector |  | 1.193 | 41:0.995,56:1.333,60:1.193 | 198,600.0 | 167,800.0 | 190,064/182,056 | Comparer・hash容量・bucket/Grouping生成 |
| IEnumerable | IEnumerable<int> | Order |  | 1.190 | 41:1.225,56:1.190,60:0.948 | 179,500.0 | 146,500.0 | 288/304 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<ClassInt32> | Select_selector |  | 1.189 | 41:1.323,56:0.684,60:1.189 | 357,600.0 | 337,300.0 | 327,680/327,872 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | ToArray |  | 1.188 | 41:0.897,56:1.248,60:1.188 | 399,700.0 | 336,400.0 | 262,216/262,248 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | ToLookup_keySelector |  | 1.188 | 41:1.083,56:1.188,60:1.230 | 681,300.0 | 629,100.0 | 208,400/184,232 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | Cast |  | 1.188 | 41:1.188,56:1.034,60:1.948 | 262,000.0 | 222,200.0 | 131,120/131,120 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | ElementAtOrDefault_index |  | 1.186 | 41:1.213,56:1.135,60:1.186 | 28,575.0 | 25,175.0 | 232/232 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | ElementAtOrDefault_index |  | 1.185 | 41:1.185,56:1.228,60:1.044 | 14,300.0 | 12,557.1 | 224/224 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | SelectMany_indexSelector_resultSelector |  | 1.184 | 41:1.239,56:1.184,60:0.639 | 34,400.0 | 30,900.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| List | List<ClassInt32> | Min |  | 1.182 | 41:1.182,56:1.199,60:1.071 | 62,350.0 | 52,750.0 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | Append_element |  | 1.181 | 41:1.143,56:1.209,60:1.181 | 25,475.0 | 21,650.0 | 224/224 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<ClassInt32> | SelectMany_indexSelector_resultSelector |  | 1.177 | 41:1.816,56:0.418,60:1.177 | 41,000.0 | 29,200.0 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Persisted(System) | Persisted<ClassInt32> | Select_selector |  | 1.177 | 41:1.217,56:1.177,60:1.173 | 363,400.0 | 305,100.0 | 327,728/327,784 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<ClassInt32> | Single |  | 1.172 | 41:1.349,56:1.172,60:1.107 | 97.3 | 87.9 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Array | Array<int> | SingleOrDefault |  | 1.170 | 41:1.202,56:1.121,60:1.170 | 78.5 | 65.9 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Join_outerKey_Hoisting |  | 1.169 | 41:1.185,56:0.869,60:1.169 | 1,700,300.0 | 1,588,000.0 | 294,032/299,464 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | DistinctBy_keySelector |  | 1.166 | 41:1.166,56:1.102,60:1.233 | 319,900.0 | 274,300.0 | 131,176/189,776 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | Join_innerKey_Hoisting |  | 1.165 | 41:1.094,56:1.165,60:1.218 | 187,700.0 | 166,000.0 | 133,640/138,376 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Average_selector |  | 1.165 | 41:1.165,56:0.923,60:1.184 | 945,600.0 | 835,700.0 | 100,400/100,632 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | Where_SourceShape |  | 1.163 | 41:1.259,56:1.163,60:1.073 | 273,600.0 | 235,200.0 | 131,072/131,264 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | GroupBy_resultSelector |  | 1.163 | 41:1.069,56:1.163,60:1.186 | 194,800.0 | 172,500.0 | 200,616/192,616 | Comparer・hash容量・bucket/Grouping生成 |
| Persisted(Seamless) | Persisted<int> | LongCount_predicate |  | 1.163 | 41:1.163,56:0.983,60:1.275 | 304,500.0 | 248,000.0 | 131,072/131,208 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<int> | TryGetNonEnumeratedCount |  | 1.158 | 41:0.966,56:1.158,60:1.688 | 71.3 | 61.8 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<ClassInt32> | SequenceEqual_second_comparer |  | 1.155 | 41:1.268,56:1.133,60:1.155 | 111,800.0 | 98,650.0 | 400/400 | Comparer/型消去またはspan経路の差 |
| Tsavorite(System) | Tsavorite<int> | SelectMany_indexSelector_resultSelector |  | 1.155 | 41:1.155,56:1.277,60:0.581 | 33,550.0 | 29,050.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| IEnumerable | IEnumerable<ClassInt32> | Last |  | 1.153 | 41:1.153,56:1.135,60:1.201 | 48,100.0 | 41,366.7 | 232/232 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<ClassInt32> | GroupBy_elementSelector_resultSelector |  | 1.153 | 41:0.610,56:1.368,60:1.153 | 511,300.0 | 480,500.0 | 402,752/416,080 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | LastOrDefault |  | 1.151 | 41:1.103,56:1.151,60:1.252 | 19,383.3 | 17,116.7 | 224/224 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Average_selector |  | 1.151 | 41:0.993,56:1.239,60:1.151 | 351,700.0 | 293,100.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | SingleOrDefault_predicate |  | 1.151 | 41:1.106,56:1.160,60:1.151 | 937,800.0 | 808,100.0 | 100,400/100,632 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | SequenceEqual_second |  | 1.149 | 41:1.149,56:1.178,60:1.114 | 117,100.0 | 103,800.0 | 400/400 | Comparer/型消去またはspan経路の差 |
| Tsavorite(System) | Tsavorite<ClassInt32> | DefaultIfEmpty |  | 1.147 | 41:1.152,56:1.082,60:1.147 | 946,100.0 | 821,200.0 | 100,480/100,560 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | ToLookup_elementSelector |  | 1.147 | 41:1.178,56:1.014,60:1.147 | 387,400.0 | 335,000.0 | 321,016/313,040 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Sum |  | 1.144 | 41:1.159,56:1.144,60:0.868 | 107,900.0 | 102,300.0 | 98,360/98,360 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | Count_predicate |  | 1.144 | 41:0.906,56:1.186,60:1.144 | 271,900.0 | 277,200.0 | 131,072/131,208 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | SingleOrDefault |  | 1.143 | 41:1.143,56:1.000,60:1.143 | 800.0 | 700.0 | 2,096/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | Single |  | 1.143 | 41:1.143,56:1.250,60:0.875 | 800.0 | 800.0 | 2,096/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Join_inner |  | 1.141 | 41:1.141,56:0.991,60:1.438 | 1,152,200.0 | 1,009,500.0 | 206,384/250,400 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Range | Range_start_count | Take_count |  | 1.139 | 41:1.271,56:1.139,60:0.969 | 9,988.9 | 9,455.6 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Average |  | 1.137 | 41:1.333,56:0.962,60:1.137 | 346,100.0 | 291,400.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | LastOrDefault |  | 1.137 | 41:1.137,56:1.121,60:1.246 | 47,566.7 | 42,433.3 | 232/232 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Join_innerKey_Hoisting |  | 1.135 | 41:1.159,56:1.021,60:1.135 | 1,744,000.0 | 1,561,500.0 | 293,960/299,464 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | ToLookup_keySelector |  | 1.135 | 41:1.260,56:1.135,60:1.082 | 425,900.0 | 384,800.0 | 446,392/438,416 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | Join_inner |  | 1.134 | 41:1.134,56:0.695,60:1.146 | 611,500.0 | 533,600.0 | 491,672/590,232 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | SingleOrDefault |  | 1.134 | 41:1.134,56:1.951,60:1.075 | 93.6 | 82.5 | 80/48 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<ClassInt32> | GroupBy_keySelector |  | 1.130 | 41:1.128,56:1.130,60:1.525 | 188,800.0 | 167,400.0 | 200,616/192,616 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(System) | Tsavorite<int> | Single_predicate |  | 1.130 | 41:1.376,56:1.064,60:1.130 | 633,100.0 | 560,400.0 | 20,704/4,296 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | LongCount_predicate |  | 1.129 | 41:1.129,56:0.838,60:1.335 | 334,400.0 | 312,100.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | GroupBy_keySelector |  | 1.127 | 41:2.832,56:1.051,60:1.127 | 494,200.0 | 417,600.0 | 429,872/438,440 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | AggregateBy_keySelector |  | 1.126 | 41:0.761,56:1.181,60:1.126 | 216,000.0 | 190,400.0 | 224/224 | Comparer・hash容量・bucket/Grouping生成 |
| Persisted(Seamless) | Persisted<int> | Min |  | 1.125 | 41:0.908,56:1.228,60:1.125 | 241,000.0 | 214,200.0 | 131,072/131,208 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | OfType |  | 1.125 | 41:1.125,56:0.854,60:1.127 | 912,500.0 | 827,900.0 | 100,400/100,400 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | Single |  | 1.124 | 41:1.237,56:0.971,60:1.124 | 94.1 | 93.2 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | DefaultIfEmpty |  | 1.124 | 41:1.124,56:1.544,60:1.001 | 415,300.0 | 369,600.0 | 229,424/229,504 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<int> | Order |  | 1.123 | 41:0.942,56:1.144,60:1.123 | 163,400.0 | 142,800.0 | 64/144 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<ClassInt32> | GroupBy_elementSelector_resultSelector |  | 1.120 | 41:0.872,56:1.120,60:1.208 | 151,200.0 | 133,600.0 | 173,376/165,376 | Comparer・hash容量・bucket/Grouping生成 |
| Persisted(System) | Persisted<ClassInt32> | ToList |  | 1.120 | 41:1.120,56:0.914,60:1.305 | 414,300.0 | 334,900.0 | 262,248/295,264 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | Join_inner |  | 1.119 | 41:1.119,56:1.232,60:0.911 | 1,124,900.0 | 1,072,200.0 | 206,624/250,688 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | ToLookup_keySelector |  | 1.118 | 41:1.199,56:1.116,60:1.118 | 130,800.0 | 109,100.0 | 190,056/182,048 | Comparer・hash容量・bucket/Grouping生成 |
| IEnumerable | IEnumerable<int> | Join_outerKey_Hoisting |  | 1.115 | 41:1.161,56:0.949,60:1.115 | 199,700.0 | 179,100.0 | 133,912/138,696 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<ClassInt32> | Max |  | 1.115 | 41:1.115,56:1.167,60:1.023 | 62,850.0 | 57,800.0 | 168/168 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Min_selector |  | 1.114 | 41:0.938,56:1.130,60:1.114 | 345,800.0 | 312,300.0 | 229,424/229,424 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | ToLookup_keySelector |  | 1.114 | 41:0.930,56:1.258,60:1.114 | 401,000.0 | 322,800.0 | 321,016/313,040 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Prepend_element |  | 1.113 | 41:1.094,56:1.113,60:1.140 | 23,075.0 | 20,800.0 | 224/224 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | Where_SourceShape |  | 1.112 | 41:1.118,56:1.112,60:0.838 | 278,800.0 | 267,000.0 | 131,120/131,176 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Count |  | 1.112 | 41:1.016,56:1.137,60:1.112 | 19,416.7 | 17,466.7 | 160/160 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Any_predicate |  | 1.111 | 41:1.000,56:1.200,60:1.111 | 600.0 | 500.0 | 160/248 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Range | Range_start_count | GroupBy_resultSelector |  | 1.110 | 41:1.111,56:0.980,60:1.110 | 75,000.0 | 70,750.0 | 173,376/165,376 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(System) | Tsavorite<int> | Min |  | 1.106 | 41:1.106,56:1.102,60:1.252 | 560,300.0 | 506,400.0 | 2,168/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Except_second |  | 1.106 | 41:1.106,56:1.471,60:0.865 | 1,472,100.0 | 1,347,200.0 | 151,864/295,952 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | Join_inner |  | 1.102 | 41:1.102,56:0.695,60:1.237 | 561,100.0 | 535,500.0 | 327,832/442,768 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Union_second |  | 1.101 | 41:1.198,56:0.653,60:1.101 | 76,700.0 | 68,000.0 | 376/376 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | SkipLast_count |  | 1.101 | 41:1.284,56:1.071,60:1.101 | 64,800.0 | 59,600.0 | 224/272 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | Average |  | 1.099 | 41:1.099,56:1.052,60:1.246 | 247,400.0 | 221,600.0 | 131,120/131,120 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Count_predicate |  | 1.098 | 41:1.154,56:1.018,60:1.098 | 616,400.0 | 552,800.0 | 18,576/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | CountBy_keySelector |  | 1.097 | 41:1.128,56:1.097,60:0.912 | 894,700.0 | 891,200.0 | 100,480/173,768 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | Average |  | 1.095 | 41:0.598,56:1.095,60:1.157 | 268,000.0 | 231,600.0 | 131,072/131,208 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | SelectMany_indexSelector_resultSelector |  | 1.095 | 41:1.095,56:1.130,60:1.093 | 34,450.0 | 31,450.0 | 2,736/2,840 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| List | List<ClassInt32> | TryGetNonEnumeratedCount |  | 1.094 | 41:1.094,56:1.047,60:1.177 | 79.4 | 75.8 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | Min |  | 1.089 | 41:1.089,56:1.188,60:1.059 | 27,650.0 | 25,550.0 | 160/160 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<int> | SingleOrDefault_predicate |  | 1.089 | 41:1.289,56:0.914,60:1.089 | 232,900.0 | 224,300.0 | 131,072/131,208 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Where_SourceShape |  | 1.088 | 41:1.088,56:0.996,60:1.282 | 1,001,300.0 | 842,100.0 | 100,400/100,688 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<int> | LongCount |  | 1.087 | 41:1.129,56:1.087,60:0.936 | 152.8 | 145.0 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | OfType |  | 1.085 | 41:1.118,56:0.736,60:1.085 | 307,800.0 | 288,000.0 | 229,424/229,472 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<ClassInt32> | Single |  | 1.084 | 41:1.090,56:1.015,60:1.084 | 88.6 | 82.5 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<int> | First |  | 1.084 | 41:1.588,56:1.084,60:1.024 | 142.8 | 89.9 | 64/64 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | SingleOrDefault_predicate |  | 1.084 | 41:1.125,56:0.811,60:1.084 | 238,600.0 | 226,400.0 | 131,120/131,120 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Zip_second |  | 1.082 | 41:0.979,56:1.098,60:1.082 | 32,825.0 | 31,866.7 | 384/384 | 2列同期・終端判定・tuple生成 |
| Tsavorite(System) | Tsavorite<int> | OfType |  | 1.082 | 41:1.084,56:1.082,60:0.945 | 548,500.0 | 551,900.0 | 2,168/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | GroupBy_elementSelector_resultSelector |  | 1.082 | 41:1.082,56:0.920,60:1.126 | 405,100.0 | 374,400.0 | 304,448/317,776 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | TryGetNonEnumeratedCount |  | 1.081 | 41:2.164,56:0.742,60:1.081 | 81.1 | 80.5 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Array | Array<ClassInt32> | ToLookup_keySelector |  | 1.081 | 41:0.568,56:1.086,60:1.081 | 148,300.0 | 139,700.0 | 216,968/208,960 | Comparer・hash容量・bucket/Grouping生成 |
| Range | Range_start_count | Zip_second |  | 1.080 | 41:1.086,56:1.074,60:1.080 | 16,550.0 | 15,233.3 | 0/0 | 2列同期・終端判定・tuple生成 |
| IEnumerable | IEnumerable<ClassInt32> | Chunk_size |  | 1.080 | 41:1.080,56:1.179,60:0.862 | 76,000.0 | 70,400.0 | 49,168/41,144 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<int> | ToLookup_elementSelector |  | 1.079 | 41:0.987,56:1.079,60:1.321 | 675,800.0 | 604,400.0 | 208,400/184,232 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | GroupBy_resultSelector |  | 1.079 | 41:1.393,56:1.079,60:0.928 | 323,900.0 | 349,100.0 | 304,496/317,680 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Max |  | 1.078 | 41:1.078,56:1.132,60:0.663 | 26,950.0 | 25,000.0 | 160/160 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | Cast |  | 1.077 | 41:1.288,56:1.077,60:0.794 | 793,200.0 | 754,500.0 | 100,480/100,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | LongCount |  | 1.077 | 41:1.067,56:1.077,60:1.087 | 159.6 | 146.8 | 0/0 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Join_innerKey_Hoisting |  | 1.077 | 41:1.077,56:0.987,60:1.443 | 685,500.0 | 636,700.0 | 450,808/491,688 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | ToDictionary_keySelector |  | 1.076 | 41:1.124,56:1.076,60:0.981 | 580,800.0 | 523,500.0 | 115,928/99,568 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | LongCount |  | 1.076 | 41:1.123,56:1.064,60:1.076 | 156.5 | 139.3 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | ToLookup_elementSelector |  | 1.076 | 41:0.621,56:1.314,60:1.076 | 410,200.0 | 381,300.0 | 419,320/411,344 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | OfType |  | 1.075 | 41:0.925,56:1.151,60:1.075 | 242,100.0 | 217,400.0 | 131,120/131,120 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | Sum_selector |  | 1.075 | 41:0.741,56:1.345,60:1.075 | 275,900.0 | 256,700.0 | 131,120/131,120 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<ClassInt32> | LongCount |  | 1.072 | 41:1.103,56:1.072,60:0.965 | 156.8 | 142.2 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | MinBy_keySelector |  | 1.070 | 41:1.059,56:1.070,60:1.213 | 858,700.0 | 802,300.0 | 133,272/100,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Except_second |  | 1.070 | 41:1.174,56:0.970,60:1.070 | 88,550.0 | 82,750.0 | 376/376 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | CountBy_keySelector |  | 1.069 | 41:0.972,56:1.073,60:1.069 | 1,049,400.0 | 977,800.0 | 100,400/173,920 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | ToArray |  | 1.069 | 41:0.970,56:1.077,60:1.069 | 872,500.0 | 822,300.0 | 133,272/133,224 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | Aggregate_func |  | 1.068 | 41:1.139,56:1.066,60:1.068 | 345,700.0 | 324,200.0 | 327,704/327,704 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Where_predicate |  | 1.068 | 41:1.114,56:0.851,60:1.068 | 874,500.0 | 860,400.0 | 100,480/100,536 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | Join_innerKey_Hoisting |  | 1.067 | 41:1.195,56:1.067,60:1.042 | 504,900.0 | 473,100.0 | 294,680/335,280 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | TakeWhile_predicate |  | 1.067 | 41:0.283,56:1.067,60:1.432 | 305.4 | 240.9 | 224/288 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | GroupBy_keySelector |  | 1.066 | 41:1.066,56:0.817,60:1.157 | 935,500.0 | 877,400.0 | 300,928/309,496 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | Min |  | 1.066 | 41:0.851,56:1.157,60:1.066 | 63,800.0 | 58,850.0 | 168/168 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<ClassInt32> | SequenceEqual_second_comparer |  | 1.065 | 41:1.065,56:0.324,60:1.169 | 17,550.0 | 49,700.0 | 64/64 | Comparer/型消去またはspan経路の差 |
| Tsavorite(System) | Tsavorite<int> | LongCount |  | 1.065 | 41:1.033,56:1.102,60:1.065 | 150.3 | 143.7 | 0/0 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Max_selector |  | 1.064 | 41:0.837,56:1.086,60:1.064 | 584,000.0 | 557,400.0 | 18,576/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | Join_outerKey_Hoisting |  | 1.063 | 41:1.063,56:1.015,60:1.122 | 501,500.0 | 480,600.0 | 294,584/335,456 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Range | Range_start_count | CountBy_keySelector |  | 1.063 | 41:1.063,56:0.942,60:1.066 | 42,200.0 | 39,600.0 | 0/56 | Comparer・hash容量・bucket/Grouping生成 |
| Array | Array<int> | ToLookup_keySelector |  | 1.063 | 41:0.731,56:1.547,60:1.063 | 105,800.0 | 96,500.0 | 189,896/181,888 | Comparer・hash容量・bucket/Grouping生成 |
| Array | Array<ClassInt32> | Aggregate_func |  | 1.062 | 41:1.382,56:0.400,60:1.062 | 134,400.0 | 126,500.0 | 98,280/98,280 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<ClassInt32> | Where_predicate |  | 1.062 | 41:1.484,56:1.062,60:1.014 | 327,200.0 | 308,200.0 | 229,424/229,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | SingleOrDefault_predicate |  | 1.061 | 41:1.290,56:1.061,60:0.959 | 302,100.0 | 302,600.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Join_innerKey_Hoisting |  | 1.061 | 41:1.061,56:1.083,60:0.846 | 1,587,700.0 | 1,465,800.0 | 294,120/299,160 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | Single_predicate |  | 1.061 | 41:0.797,56:1.090,60:1.061 | 578,900.0 | 577,500.0 | 4,224/4,440 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Aggregate_seed_func |  | 1.060 | 41:1.161,56:0.925,60:1.060 | 624,700.0 | 570,000.0 | 2,168/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Min_selector |  | 1.060 | 41:1.060,56:1.004,60:1.272 | 683,100.0 | 539,900.0 | 18,576/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Min |  | 1.060 | 41:1.120,56:1.060,60:0.997 | 828,600.0 | 813,700.0 | 100,480/100,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | SingleOrDefault_predicate |  | 1.059 | 41:1.059,56:1.047,60:1.155 | 556,900.0 | 525,800.0 | 18,576/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Where_predicate |  | 1.059 | 41:1.059,56:1.151,60:0.883 | 608,100.0 | 591,400.0 | 2,168/2,224 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Single |  | 1.059 | 41:0.941,56:1.059,60:1.150 | 1,800.0 | 1,700.0 | 2,120/2,200 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | GroupBy_elementSelector_resultSelector |  | 1.058 | 41:1.058,56:0.928,60:1.362 | 1,017,500.0 | 961,700.0 | 273,776/287,200 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | GroupBy_keySelector |  | 1.056 | 41:2.082,56:0.971,60:1.056 | 359,700.0 | 340,500.0 | 304,496/313,064 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | ExceptBy_keySelector |  | 1.056 | 41:1.318,56:1.056,60:0.930 | 1,610,900.0 | 1,525,600.0 | 151,704/474,840 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | Join_resultSelector_Hoisting |  | 1.056 | 41:1.517,56:0.466,60:1.056 | 196,800.0 | 141,500.0 | 133,640/138,376 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Single |  | 1.056 | 41:1.000,56:1.056,60:1.056 | 1,900.0 | 1,800.0 | 2,120/2,200 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | SingleOrDefault |  | 1.056 | 41:1.118,56:1.000,60:1.056 | 1,900.0 | 1,700.0 | 2,120/2,200 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | GroupBy_resultSelector |  | 1.055 | 41:1.055,56:0.979,60:1.077 | 1,003,300.0 | 950,700.0 | 300,848/315,800 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | MaxBy_keySelector |  | 1.055 | 41:0.651,56:1.055,60:1.149 | 354,000.0 | 398,600.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<ClassInt32> | Zip_tuple |  | 1.054 | 41:1.054,56:0.998,60:1.077 | 52,650.0 | 48,900.0 | 64/64 | 2列同期・終端判定・tuple生成 |
| Array | Array<ClassInt32> | Min |  | 1.053 | 41:1.053,56:0.956,60:1.066 | 25,950.0 | 24,800.0 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Array | Array<int> | Intersect_second |  | 1.052 | 41:0.822,56:1.175,60:1.052 | 76,200.0 | 70,850.0 | 56/56 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<int> | Any_predicate |  | 1.052 | 41:1.094,56:1.030,60:1.052 | 515,800.0 | 490,600.0 | 18,576/2,256 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | GroupBy_resultSelector |  | 1.050 | 41:1.338,56:1.050,60:1.042 | 410,000.0 | 393,400.0 | 429,872/444,592 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | DefaultIfEmpty |  | 1.049 | 41:1.049,56:0.979,60:1.101 | 66,900.0 | 63,750.0 | 168/192 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<ClassInt32> | Min |  | 1.049 | 41:1.087,56:0.991,60:1.049 | 310,300.0 | 295,700.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | AggregateBy_keySelector |  | 1.049 | 41:1.135,56:0.968,60:1.049 | 965,400.0 | 962,000.0 | 100,536/173,792 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Chunk_size |  | 1.049 | 41:0.964,56:1.049,60:1.201 | 38,633.3 | 34,633.3 | 24,776/20,752 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | Any_predicate |  | 1.048 | 41:0.988,56:1.048,60:1.376 | 755,300.0 | 764,500.0 | 133,272/100,568 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | Except_second |  | 1.047 | 41:1.078,56:0.829,60:1.047 | 929,200.0 | 910,600.0 | 4,248/262,984 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | ToArray |  | 1.047 | 41:0.880,56:1.100,60:1.047 | 226,200.0 | 216,000.0 | 147,480/147,648 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Index |  | 1.047 | 41:1.047,56:1.087,60:0.998 | 28,000.0 | 26,750.0 | 224/224 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | Intersect_second |  | 1.045 | 41:1.048,56:1.045,60:0.962 | 393,300.0 | 391,600.0 | 196,760/320,304 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Aggregate_seed_func_resultSelector |  | 1.045 | 41:1.197,56:1.045,60:0.960 | 879,000.0 | 841,000.0 | 100,400/100,632 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | Cast |  | 1.045 | 41:1.045,56:1.023,60:1.138 | 546,400.0 | 522,800.0 | 2,096/2,096 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<ClassInt32> | First |  | 1.045 | 41:1.045,56:0.722,60:1.057 | 102.4 | 98.1 | 64/64 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| List | List<int> | Chunk_size |  | 1.044 | 41:1.044,56:4.813,60:0.847 | 1,510.9 | 1,669.4 | 16,568/16,568 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | ToHashSet |  | 1.044 | 41:1.087,56:0.951,60:1.044 | 872,700.0 | 837,200.0 | 230,608/197,856 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<ClassInt32> | TryGetNonEnumeratedCount |  | 1.042 | 41:1.183,56:1.028,60:1.042 | 79.0 | 71.8 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | Join_inner |  | 1.042 | 41:1.042,56:1.034,60:1.537 | 1,520,200.0 | 1,470,000.0 | 299,320/397,880 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<ClassInt32> | FirstOrDefault |  | 1.042 | 41:1.042,56:1.853,60:0.841 | 119.5 | 123.2 | 64/64 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<int> | Intersect_second |  | 1.041 | 41:1.046,56:1.041,60:0.732 | 412,900.0 | 397,500.0 | 196,664/320,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Aggregate_seed_func_resultSelector |  | 1.041 | 41:1.104,56:1.041,60:1.028 | 874,400.0 | 840,200.0 | 100,480/100,480 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | OfType |  | 1.041 | 41:1.219,56:1.041,60:0.679 | 230,500.0 | 235,400.0 | 131,072/131,072 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<ClassInt32> | First |  | 1.040 | 41:0.921,56:1.040,60:1.072 | 106.2 | 115.4 | 64/64 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<ClassInt32> | ToLookup_keySelector |  | 1.040 | 41:0.974,56:1.040,60:1.141 | 184,500.0 | 174,100.0 | 217,136/209,128 | Comparer・hash容量・bucket/Grouping生成 |
| Range | Range_start_count | Concat_second |  | 1.040 | 41:1.040,56:1.366,60:1.011 | 16,740.0 | 16,560.0 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(Seamless) | Persisted<int> | InvariantHoisting |  | 1.038 | 41:1.282,56:0.945,60:1.038 | 237,700.0 | 229,000.0 | 131,072/131,264 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | CountBy_keySelector |  | 1.038 | 41:1.062,56:0.970,60:1.038 | 372,100.0 | 369,000.0 | 229,376/302,800 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | InvariantHoisting |  | 1.038 | 41:1.236,56:0.806,60:1.038 | 597,400.0 | 575,600.0 | 2,096/2,368 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| List | List<int> | FirstOrDefault_predicate |  | 1.037 | 41:1.029,56:1.135,60:1.037 | 232.5 | 224.2 | 64/152 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| IEnumerable | IEnumerable<int> | SelectMany_indexSelector |  | 1.036 | 41:0.979,56:1.036,60:1.898 | 37,100.0 | 35,633.3 | 2,736/2,736 | 内側IEnumerableのIEnumerator/interface dispatch・selector/Dispose |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | DefaultIfEmpty |  | 1.036 | 41:0.776,56:1.036,60:1.136 | 854,000.0 | 824,400.0 | 100,400/100,712 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | ToLookup_keySelector |  | 1.036 | 41:1.161,56:0.895,60:1.036 | 352,400.0 | 358,100.0 | 320,968/313,128 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | Append_element |  | 1.036 | 41:1.055,56:0.745,60:1.036 | 65,900.0 | 63,600.0 | 256/256 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<ClassInt32> | ToList |  | 1.035 | 41:1.140,56:1.035,60:0.981 | 800,300.0 | 793,800.0 | 133,304/133,264 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Count_predicate |  | 1.035 | 41:1.212,56:0.989,60:1.035 | 301,600.0 | 291,400.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<int> | Take_count |  | 1.034 | 41:1.034,56:1.081,60:0.956 | 18,883.3 | 17,480.0 | 224/224 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Persisted(System) | Persisted<int> | DefaultIfEmpty |  | 1.034 | 41:1.046,56:0.890,60:1.034 | 242,800.0 | 240,800.0 | 131,120/131,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Where_predicate |  | 1.033 | 41:0.819,56:1.033,60:1.176 | 871,100.0 | 840,100.0 | 100,400/100,688 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | DistinctBy_keySelector |  | 1.033 | 41:1.173,56:1.033,60:0.943 | 886,600.0 | 884,400.0 | 100,536/159,136 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | ExceptBy_keySelector |  | 1.033 | 41:0.987,56:1.033,60:1.682 | 1,380,000.0 | 1,398,700.0 | 151,864/295,968 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Range | Range_start_count | GroupBy_keySelector |  | 1.032 | 41:1.120,56:0.617,60:1.032 | 81,350.0 | 78,800.0 | 173,376/165,376 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(System) | Tsavorite<ClassInt32> | Select_selector |  | 1.031 | 41:1.031,56:1.498,60:0.966 | 977,800.0 | 880,500.0 | 198,784/198,840 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | UnionBy_keySelector |  | 1.031 | 41:0.505,56:1.049,60:1.031 | 443,900.0 | 432,100.0 | 196,760/454,976 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Union_second |  | 1.031 | 41:1.098,56:1.031,60:0.908 | 1,462,000.0 | 1,418,300.0 | 151,704/474,688 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| IEnumerable | IEnumerable<ClassInt32> | Reverse |  | 1.030 | 41:1.029,56:1.030,60:1.097 | 123,200.0 | 116,300.0 | 232/264 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Range | Range_start_count | Consume |  | 1.030 | 41:1.005,56:1.030,60:1.050 | 8,630.0 | 8,380.0 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(System) | Tsavorite<int> | Except_second |  | 1.030 | 41:0.998,56:1.058,60:1.030 | 850,500.0 | 852,100.0 | 4,392/119,712 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Average_selector |  | 1.029 | 41:1.234,56:1.029,60:1.013 | 321,500.0 | 317,500.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<ClassInt32> | Join_outerKey_Hoisting |  | 1.029 | 41:0.989,56:1.029,60:1.205 | 661,200.0 | 613,000.0 | 450,904/491,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | LongCount |  | 1.029 | 41:1.029,56:1.067,60:0.782 | 164.4 | 161.4 | 0/0 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Min_selector |  | 1.029 | 41:1.012,56:1.029,60:1.207 | 935,000.0 | 864,200.0 | 100,400/100,632 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Where_predicate |  | 1.028 | 41:2.860,56:0.912,60:1.028 | 295,900.0 | 324,500.0 | 229,376/229,568 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Where_SourceShape |  | 1.028 | 41:1.028,56:1.194,60:0.966 | 329,700.0 | 311,800.0 | 229,376/229,568 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | Aggregate_seed_func_resultSelector |  | 1.027 | 41:0.922,56:1.068,60:1.027 | 567,300.0 | 571,200.0 | 2,168/2,168 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<int> | Single |  | 1.025 | 41:1.105,56:1.025,60:0.960 | 78.2 | 76.2 | 0/0 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Array | Array<ClassInt32> | Zip_second |  | 1.025 | 41:1.093,56:1.025,60:0.993 | 61,700.0 | 60,750.0 | 49,216/49,216 | 2列同期・終端判定・tuple生成 |
| IEnumerable | IEnumerable<int> | GroupBy_keySelector |  | 1.024 | 41:1.024,56:1.143,60:1.002 | 121,500.0 | 118,200.0 | 173,536/165,536 | Comparer・hash容量・bucket/Grouping生成 |
| Tsavorite(Seamless) | Tsavorite<int> | UnionBy_keySelector |  | 1.024 | 41:1.352,56:1.000,60:1.024 | 964,800.0 | 841,000.0 | 4,248/262,896 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<int> | Where_predicate |  | 1.023 | 41:1.023,56:0.956,60:1.077 | 249,100.0 | 260,500.0 | 131,072/131,264 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(System) | Persisted<int> | CountBy_keySelector |  | 1.023 | 41:1.364,56:0.757,60:1.023 | 439,900.0 | 355,600.0 | 131,120/204,408 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<ClassInt32> | ToLookup_elementSelector |  | 1.023 | 41:1.087,56:0.579,60:1.023 | 928,700.0 | 907,800.0 | 323,168/282,400 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Persisted(Seamless) | Persisted<ClassInt32> | Min_selector |  | 1.023 | 41:1.103,56:1.023,60:0.980 | 316,600.0 | 309,300.0 | 229,376/229,512 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(System) | Tsavorite<int> | GroupBy_elementSelector_resultSelector |  | 1.022 | 41:1.032,56:0.934,60:1.022 | 663,500.0 | 644,600.0 | 175,544/188,736 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Array | Array<ClassInt32> | SequenceEqual_second |  | 1.022 | 41:2.741,56:1.022,60:0.952 | 20,216.7 | 19,766.7 | 64/64 | Comparer/型消去またはspan経路の差 |
| List | List<ClassInt32> | OrderDescending |  | 1.022 | 41:1.085,56:1.022,60:0.913 | 704,500.0 | 695,500.0 | 160/192 | 終端処理・分岐・計測揺らぎ（要プロファイル） |
| Tsavorite(Seamless) | Tsavorite<int> | SingleOrDefault_predicate |  | 1.021 | 41:1.033,56:1.005,60:1.021 | 564,400.0 | 552,800.0 | 2,096/2,312 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<int> | Max |  | 1.021 | 41:0.989,56:1.021,60:1.062 | 554,100.0 | 550,000.0 | 2,096/2,312 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |
| Tsavorite(Seamless) | Tsavorite<ClassInt32> | Single_predicate |  | 1.020 | 41:0.917,56:1.151,60:1.020 | 897,500.0 | 844,600.0 | 102,552/102,784 | ストレージ走査/I/Oまたは公開IEnumerable型消去 |

## 2026-08-11 — C-InterfaceLoop-01（実装・検証待ち）

- ベンチマーク名: `IEnumerable` 全般、および Persisted/Tsavorite の `IEnumerable` 比較。
- ソース型/要素型: 静的 `IEnumerable<T>` / `T`。
- 終端操作: `Consume` へlowerされる全終端。
- FLinq修正前形状: `Root` の nullable `IEnumerator<T>` を内包する `IValueEnumerator<T>` struct、runtime Array/List `TryGetSpan`、`TryGetNext` wrapper。
- 目標形状: `using var e=((IEnumerable<T>)source).GetEnumerator(); while(e.MoveNext()) consumer.Consume(e.Current);`。interface dispatchは静的境界として許容し、boxing・runtime型回復・追加状態機械は作らない。
- ZLinq形状: `FromEnumerable<T>` 値enumerator。直接loopとの差はinterface契約そのものに限定する。
- 失型地点: `AsX<T>(IEnumerable<T>)` 後にRoot wrapper生成。
- delegate/comparer: 本候補では追加しない。
- 割当/GC/時間: 未測定。
- 最小修正: `Root.GetConsumeExecutionStatement` を `useEnumerableInterface` 限定でoverride。
- 採否基準: 新Worker保存3 Runの中央値、同源比較で回帰2%超なし・改善3%以上・比率1.02以下。

## 2026-08-11 — C-AsEnumerable-02（実装・検証待ち）

- ベンチマーク名: Array/List/Range/Persisted/Tsavorite の透過 `AsEnumerable` を含むchain。
- FLinq修正前形状: `AsEnumerable` stageが残留し、単独stage/終端で空 `PipelineEnumerable`。
- 目標形状: stageを生成前正規化で除去し、sourceのSpan/Count/Copy/Dispose能力をそのまま終端へ渡す。
- 静的型喪失: 透過stage後のPipeline wrapper。
- 割当/GC/時間: 未測定。
- 最小修正: `RemoveTransparentAsEnumerableStages`。

## 2026-08-11 — C-ListRoute-03（実装・検証待ち）

- ベンチマーク名: `List` 全比較。
- ソース型/要素型: `List<T>` / `T`。
- FLinq修正前形状: `AsX(List<T>) -> IEnumerable<T>` によりList span/value enumerator経路を喪失。
- ZLinq形状: `FromList<T>`。
- 最小修正: benchmark内部routeの戻り型だけを `List<T>` に維持。
- 割当/GC/時間: 未測定。
### C-ListRoute-03 — 却下

`List<T>` 戻り値は `Reverse`、`Sort` 等のListインスタンスメソッドを優先解決するため、LINQ chainの意味を変える。最小修正でも契約違反となるので採用しない。

### C-InterfaceLoop-01 / C-AsEnumerable-02 — 形状・意味テスト合格、性能測定待ち

- 生成コードは直接interface loop（interface dispatchは境界に限定）を確認。
- `AsEnumerable().Count()` はArrayの `Length` 直行形状を確認。
- 既存 `RedundantAsEnumerable` 3件がReleaseで合格。
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
- 次に必要なのは `Consume` の dispatch が実際にどの renderer を選ぶかを trace で特定することであり、Root/AsEnumerable/Pipeline の型パターンを増やすことではない。C-28 の direct interface loop は Int32 で20.1%回帰したため、根拠なく raw loop を挿入しない。## 2026-08-12 C-IEnumerable-DirectLowering-30 (ASCII audit record)

Scope: System.Collections.Generic.IEnumerable<>; source is Base<T>.CreateSourceEnumerable iterator; element types Int32 and ClassInt32; terminals ElementAtOrDefault_index, DefaultIfEmpty, Contains_value, AggregateBy_keySelector.

ZLinq reference shape:
- FromEnumerable<T> is a struct carrier inside ValueEnumerable<TEnumerator,T>.
- ElementAtOrDefault first calls TryCopyTo(SingleSpan, index), then EnumeratorHelper.TryConsumeGetAt; the latter is the safe unknown-enumerable fallback.
- Contains first uses TryGetSpan and then a typed TryGetNext loop; the FromEnumerable overload also has collection fast paths.
- DefaultIfEmpty keeps a generic DefaultIfEmpty<TEnumerator,T> carrier; it does not remove all wrappers. The important property is that TEnumerator remains a concrete struct.

FLinq current shape after forced Release Rebuild:
- AsX<T>() still has public return type IEnumerable<T>, so the concrete iterator type is intentionally not guessed.
- Root direct terminal lowering uses one source local, ArgumentNullException.ThrowIfNull, one IEnumerator<T>, MoveNext and Current. Unknown IEnumerable<T> has a safe fallback and no IValueEnumerator wrapper in the terminal.
- ElementAtOrDefault evaluates source/index once, returns default for a negative index, disposes the interface enumerator, and preserves the unknown fallback.
- Contains evaluates value once; array/List/ICollection/IReadOnlySet probes are optional runtime fast paths and failure returns to the original direct IEnumerator<T> fallback.
- DefaultIfEmpty uses a direct non-empty do/while and emits the default value only when the first MoveNext is false.

Candidate A - runtime array/List recovery for static IEnumerable ElementAtOrDefault: HOLD/REJECT for this source group.
- Hypothesis: mirror ZLinq TryCopyTo by adding source is T[] and source is List<T> before the interface loop.
- Reason not applied: the measured source is an iterator, not an array/List. The extra type probes execute on every call and cannot remove the iterator state machine. The existing Root struct already has TryGetSpan runtime array/List recovery for generic paths, while the direct terminal intentionally stays one interface loop.
- Code comparison: ZLinq `TryCopyTo -> TryConsumeGetAt`; proposed FLinq `if(source is T[]/List<T>) -> indexer/span -> IEnumerator fallback`; current FLinq direct terminal `IEnumerator fallback` only for the statically unknown iterator. This is the smallest safe shape for the benchmark source.
- Semantics checked: null source, negative index, out-of-range index, empty sequence, Current and Dispose are preserved.

Candidate B - DefaultIfEmpty direct terminal: SHAPE ADOPTED, broad performance adoption HOLD.
- Shape test added: PerformanceShapeTests.StaticIEnumerableDefaultIfEmptyConsumeUsesDirectInterfaceLoop.
- Release shape test passed. The generated method contains GetEnumerator, MoveNext, Current and the default fallback, and does not contain IValueEnumerator or TryGetNext.
- ClassInt32 fresh median ratio is 0.6996 (Run 233--235), but Int32 is 0.9189 (Run 230--232). Because the old 218--223 Worker was before forced regeneration and the source-group 3%/2% gate is not stable across the two element types, keep the implementation as a shape fix but do not claim a universal performance adoption.

Candidate C - ElementAtOrDefault direct interface terminal: ADOPT.
- Existing shape test passed in Release.
- Fresh ratios: Int32 0.9768 (224--226), ClassInt32 0.7297 (227--229); allocations 224 B and 232 B on both sides.

Candidate D - Contains direct collection/fallback terminal: ADOPT.
- Existing shape tests passed in Release, including one-time value evaluation and comparer fallback.
- Fresh ratios: Int32 1.0063 (236--238), ClassInt32 0.6908 (239--241); allocations 160 B and 192 B on both sides.

Candidate E - AggregateBy direct consume: HOLD.
- Fresh ratios: Int32 0.8362 (242--244), ClassInt32 1.0327 (245--247).
- ClassInt32 remains above 1.02. Generated code already inlines keySelector/reducer and uses DictionarySlim; the remaining cost is likely DictionarySlim lookup/ref mutation plus ClassInt32 Number/Mod calls and iterator MoveNext inlining. No speculative dictionary/comparer rewrite was applied because it would alter semantics and affect the passing Int32 case.

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