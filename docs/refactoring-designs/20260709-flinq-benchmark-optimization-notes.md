# FLinq Benchmark Optimization Notes - 2026-07-09

## Summary

通常ベンチの要素数は 4096 固定として扱う。DBには要素数列を保存せず、Run履歴の比較は `FLinqNs / ZLinqNs` の性能比で見る。値が小さいほどFLinq側が速い。

## Implemented Optimizations

- `FirstOrDefault` / `FirstOrDefault(predicate)`: scalar terminal の直接消費を優先し、不要な enumerator wrapper と物理 `MoveNext` 回数を減らす。
- `Concat_second`: 片側ずつ direct consume し、span が取れる入力では `Current` 経由を避ける。
- `TakeWhile` / `SkipWhile`: index predicate の direct consume を追加し、span 経路では単純な for loop に展開する。
- `ToArray` / `ToList`: direct span / fast copy の利用を優先し、中間列挙を減らす。
- Set系: capacity hint と direct consume を利用し、`HashSet` 構築時の再確保と wrapper 経由の列挙を抑える。

## int/string Difference

現DBの最新完了ペアでは、型によって改善優先度が変わる候補が見える。例は `Empty`, `TakeWhile_predicate`, `Cast`, `ToHashSet`, `Distinct`, `Zip_second`, `Contains_value`。

推定原因は、単純な値型サイズまたは参照型サイズだけではない。主な要因は次の組み合わせ:

- delegate / closure / boxing の有無
- comparer と equality のコスト
- string 変換、hash、比較の実装差
- Array / Range / IEnumerable root の違い
- ZLinq側の型別特殊化や fast path の差

そのため改善判断は `int` と `string` を別ランキングとして見て、メソッド固有の最適化を優先する。

## Static Delegate Policy

`TakeWhile_indexPredicate_Impl` と `SkipWhile_indexPredicate_Impl` は static lambda にすると速くなるが、それはベンチコード側のdelegate形状変更による改善であり、FLinq生成器の改善ではない。ベンチは非static lambdaへ戻し、生成側でインライン可能な式は delegate `.Invoke` より先に式展開する。

## Zip Attempt

「Zip subset が悪化した」ときに試した方向は、`Zip.cs` 自体のアルゴリズム変更ではなく、`Pipeline` 側の単一stage direct consume と空 wrapper 伝播に近い経路だった。狙いは、Zip前後の pipeline boundary を薄くして `Current` / wrapper 経由を減らすこと。

Zipは複数sourceを同期して `MoveNext` し、いずれかが尽きた時点で停止する。片側だけspan化してももう片側の進行と終了判定が残り、単純な direct consume の利益が出にくい。今回の方向は効果なしではなく負に出たため、Zip専用の最適化としては撤回する。次に触る場合は、Zip固有に2入力の長さ・span取得・selector展開を同時に扱う。

## DB / UI Notes

- `BenchmarkPerformanceRatioHistory`: RunIdごとの性能比履歴。
- `BenchmarkPerformanceRatioDelta`: 前回Runとの差分。
- チャートは「比較チャート」と「履歴性能比」の2タブに限定する。
- 保存済みDBからロードした行は、保存済み完了結果として完全完了アイコンを表示する。
