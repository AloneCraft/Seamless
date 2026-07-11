# Tsavorite.Linq.Enumerable shared benchmark 設計

## 背景

`Tsavorite.Linq.Enumerable` は `System.Linq.Enumerable` と同名の LINQ API を `Benchmark.Tsavorite.Collections.IHashSet<T>` 向けに提供する。既存の `Shared/Common/Benchmarks` は `System.Linq.Enumerable` / FLinq / ZLinq の同一シナリオ比較を持つが、Tsavorite storage-backed source との比較軸がない。

## 方針

- `Shared/Common/Benchmarks` に `TsavoriteEnumerableCatalog` を追加し、`MethodName` パラメータで System LINQ と Tsavorite LINQ を同一シナリオ名で比較する。
- ベンチマークメソッドは `Linq` と `Tsavorite` の2本にする。`Linq` は `System.Collections.Generic.HashSet<T>`、`Tsavorite` は `Benchmark.Tsavorite.Collections.IHashSet<T>` を source にする。
- `GlobalSetup` で source を構築し、測定対象に source 構築コストを混ぜない。
- `BENCH_AS_TEST` では `Results_match` が全 `MethodName` を小さいデータで実行し、System と Tsavorite の結果を正規化して比較する。
- 順序非依存の hash storage 差分を避けるため、テスト側の正規化では enumerable 結果を文字列化してソートする。順序そのものを検証する API は既存の単体/Intercept テストに任せる。

## 対象

対象は `IHashSet<T>` source を取る `Tsavorite.Linq.Enumerable` の System LINQ 対応メソッドに限定する。`Range` / `Repeat` / `Empty` のような source なし factory、`Cast` / `OfType` / `AsQueryable` のような現在の `IHashSet<T>` API と対応しないもの、既存 benchmark 固有の hoisting/source-shape シナリオはこの catalog では扱わない。

## 受け入れ条件

- `Benchmark` の開始メニューに `TsavoriteEnumerableCatalog` が表示される。
- `Tests` で catalog の System/Tsavorite 結果比較が通る。
- `Benchmark` の単一ファイル集計で `Tsavorite` 列と `MethodName` 別行が出せる。
- 既存 `Linq` / `FLinq` / `ZLinq` benchmark はそのままビルドできる。
