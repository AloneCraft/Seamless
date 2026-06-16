# Tsavorite.Linq.Enumerable 正規化

## 背景

`Tsavorite.Linq.Enumerable.cs` は未完成の一部実装であり、既存の `Tsavorite.Linq.HashSet` が `IHashSet<T>` 向け LINQ API の実体を持っている。
ただし名前としては `System.Linq.Enumerable` に対応する入口は `Tsavorite.Linq.Enumerable` が妥当である。

## 現状

- `src/Tsavorite.Collections/Linq/HashSet.cs` は `public static partial class HashSet` として多数の `IHashSet<T>` 拡張メソッドを持つ。
- `src/Tsavorite.Collections/Linq/Enumerable.cs` は concrete `Tsavorite.Collections.HashSet<T>` 向けの未完成実装だった。
- テストには `global::Tsavorite.Linq.Enumerable` 直接呼び出しがあり、既に `Enumerable` 名を正とする期待が混在している。
- `tests/Test.Tsavorite/Database/Database0.cs` だけが `global::Tsavorite.Linq.HashSet.SelectMany` を直接参照していた。

## 目的

`Tsavorite.Linq.Enumerable` を `System.Linq.Enumerable` に対応する正規 API として定義し、`Tsavorite.Linq.HashSet` 互換ラッパーは残さない。

## 非目的

- この変更では個々の LINQ メソッドの性能改善や storage-level 展開は行わない。
- `System.Collections.Generic` を使う既存 iterator 実装の全面置換は別作業にする。
- 旧 `Tsavorite.Linq.HashSet` の public API 互換は維持しない。

## 提案設計

- 未完成の `src/Tsavorite.Collections/Linq/Enumerable.cs` を削除する。
- 既存の `src/Tsavorite.Collections/Linq/HashSet.cs` を `src/Tsavorite.Collections/Linq/Enumerable.cs` にリネームする。
- クラス名を `public static partial class Enumerable` に変更する。
- 直接 `Tsavorite.Linq.HashSet` を呼んでいるコードは `Tsavorite.Linq.Enumerable` に更新する。

## 影響範囲

- 拡張メソッド呼び出し `source.Where(...)` などは namespace import が同じなら基本的に維持される。
- `Tsavorite.Linq.HashSet.Xxx(...)` のような直接 static 呼び出しは破壊的変更になる。
- `System.Linq.Enumerable` と同様に、通常実装は汎用 iterator ベースで正しさを担保し、パフォーマンスは generator/intercept 展開で改善する前提にする。

## テスト計画

- `rg` で `Tsavorite.Linq.HashSet` 直接参照が残っていないことを確認する。
- `dotnet build` で API 名変更に伴うコンパイルエラーを確認する。
- 可能なら `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj` を実行し、既存 LINQ/Intercept テストを確認する。

## リスクと対策

- 旧 direct static API 利用者には破壊的変更になる。今回はユーザー合意により互換ラッパーなしで進める。
- `Enumerable` 名は `System.Linq.Enumerable` と衝突しやすい。直接呼び出しが必要な箇所は `global::Tsavorite.Linq.Enumerable` を使う。

## 合意状態

ユーザー指示により、互換ラッパーなしで `Linq.HashSet` を `Linq.Enumerable` に移行する。
