# GroupBy の遅延集約の比較(GroupByLazyBench)

`GroupBy(k).Select(g => 集約).First()` や `.Take(n)` で、先頭の n グループだけ集約する形(生成器の `GroupLimit`)が、System.Linq より速いかを調べる単体の測定です。
ハードや .NET の版で結果が変わりうるので、次の .NET の版・別のマシンでビルドし直したときに、同じ表を取り直して比べるために置いています。

## 比べているもの

| 名前 | 内容 |
|---|---|
| System | System.Linq そのまま |
| Fused | 全グループを 1 回の走査で集約(評価の回数が System.Linq より多い。基準としてだけ置く) |
| Lazy / LazyPool | 1 回目でグループ番号だけ記録し、2 回目で先頭 n グループの要素だけ集約(LazyPool は ArrayPool 版) |
| Limit | 生成器に入れた形。1 回の走査で、グループが n 個に達したら新しいグループを作らず、n グループの外の要素は集約しない |

源の型(int・参照型 `Rec`・32 バイトの構造体 `Big32`)× キーの型(int・string・Guid)× 件数(1,000 / 100,000 / 1,000,000)× グループ数(10 / 1,000)× 取る数(1 / 5 / グループ数の半分)を回します。

## 取り直し方

```bash
cd Benchmark/Experiments/GroupByLazyBench
dotnet run -c Release -- result-<yyyyMMdd>-<machine>.txt
```

- 重い測定なので、性能コア 1 個で、他の作業を止めて回す(測定中に他のビルド・テストを走らせない)。
- 出力の先頭に、.NET の版・OS・CPU を書く。

## 判断の基準

- `Limit/System < 1` が全部の行で成り立つこと(Limit が System.Linq より遅い行が 1 つでもあれば、その型・大きさでは遅延集約をやめる条件を生成器に足す)。
- 成り立たなくなる行が出たら、その行の型(ソース型・キー型・要素の大きさ)と、`Limit/Fused` の値を設計書に書く。

## 結果

- `result-20261023-Intel-Model183-dotnet10.0.12.txt`(2026-10-23、.NET 10.0.12、Intel Family 6 Model 183、性能コア 1 個に固定、108 行): `Limit/System` は 0.11〜1.08。
  1 を超えた行は 1 つだけで、`Big32`(32 バイトの構造体)× `Guid` キー × 1,000 件 × 10 グループ × 先頭 5 グループ(1.08、誤差の範囲。同じ条件で `take=1` は 0.7 台)。ほかの 107 行は 1 未満。
  `Limit/Fused` は 0.31〜1.32(Fused は評価回数が System.Linq より多いので、速さの基準にはしない)。
