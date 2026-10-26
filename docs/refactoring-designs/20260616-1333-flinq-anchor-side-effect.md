# FLinq Anchor Side Effect Policy

## 背景
FLinq は LINQ chain をコンパイル時に展開し、delegate 呼び出し、iterator allocation、不要な stage を減らす。既存実装では `DistinctBy` 連続削減や `Where` 並べ替えのような評価回数・評価順を変え得る最適化が複数箇所に分散している。

`src/Benchmark.Tsavorite.Collections/Linq/AnchorExtensions.cs` には `value.Anchor()` 拡張メソッドがあり、現在は `SelectMany` の一部 hoist 抑止で文字列検出されているだけである。

## 現状の観察
- `Shared/Common/Benchmarks/DistinctBy_keySelector.cs` は `DistinctBy(x => x % 1000)` を 1-4 段重ねる。
- BDN 実測では `FLinq4` が特に遅く、生成コードでは各段ごとに `HashSet<int>` が作られている。
- `Pipeline.NormalizeStages` には連続 `DistinctBy` を畳む処理があるが、`.Consume(this.consumer)` の Fragment 経路には効いていない。
- `AlgebraicRewriter` には `Where` 並べ替え用の安全判定がある。`Anchor` は評価回数・順序の境界を明示する。2026-10-26 の不変式調査により、Anchor 不在を任意式の無例外・無副作用保証として扱わない。

## 目的
- Anchor のない式の評価回数減少は許すが、System.Linq が評価しない式を先行評価して例外や副作用を増やさない。不変式の巻き上げは安全が証明できる式に限る。
- `expr.Anchor` を付けた場合だけ、`expr` を副作用ありとして扱い、該当式の削除・複製・順序変更を避ける。
- `Anchor` はチェーン全体の最適化停止ではなく、該当変換だけを止めるマーカーにする。
- `DistinctBy_keySelector` では重複 `DistinctBy` を削減し、FLinq が最速になる方向へ改善する。

## 非目的
- 実行時に副作用を厳密解析する仕組みは入れない。
- `Anchor` なしの式について LINQ と同じ評価回数を保証することは目的にしない。減少は許すが、正常値・例外の一致と評価回数の上限は守る。
- ベンチマーク専用の文字列置換や `x % 1000` 特化は行わない。

## 提案設計
- `Anchor` API を `value.Anchor()` 拡張メソッドから C# 14 extension property の `value.Anchor` へ置き換える。
- `Benchmark.Tsavorite.Collections` は `net10.0` / C# 14 前提へ寄せる。
- FLinq source generator 側に `Anchor` 検出の共通ヘルパーを追加する。
- `FuncFragment` に `ContainsAnchor` 相当の情報を持たせ、lambda 生成時に本文へ `Anchor` が含まれるか記録する。
- 不変式hoistは、変更されないprimitive local/by-value parameterとuncheckedの組み込み算術・bit・比較・論理に限定する。method全体(内包lambda/local functionを含む)で代入・増減・ref/out/in渡しがある値、ref/in/out parameterやref localは不変とみなさない。division/remainder、checked演算・変換、decimal、参照のdereference、property/field/indexer、method/ctorは元のlambda位置へ残す。FLinqTransparentはidentityの契約であり、この安全条件を緩める属性ではない。
- 生成コードでは `Anchor` は identity として除去する。
- `DistinctBy` 連続削減、`Distinct` 連続削減、`Where` merge/reorder、`Reverse` cancel、`Take/Skip` merge、`OrderBy` direct consume、`SelectMany` hoist など、評価回数・順序を変え得る最適化は `Anchor` を含む stage をまたがない。
- 通常の loop inline、`Consume` 直結、allocation 削減は `Anchor` があっても可能な範囲で継続する。

## 影響範囲
- `src/Benchmark.Tsavorite.Collections/Linq/AnchorExtensions.cs`
- `src/FLinq/SourceGenerator/Linq/Fragments/Func.cs`
- `src/FLinq/SourceGenerator/LinqFragmentBuilder.cs`
- `src/FLinq/SourceGenerator/LinqGeneratedCodeRendering.cs`
- `src/FLinq/SourceGenerator/Linq/Fragments/Pipeline.cs`
- `src/FLinq/SourceGenerator/Linq/Fragments/SelectMany.cs`
- `tests/Test.Benchmark.Tsavorite.Collections` または `Test`

## 移行手順
- `Anchor()` 呼び出しは `Anchor` プロパティへ置き換える。
- 既存の `Anchor()` メソッドは削除し、互換 API は残さない。
- 既存の文字列検出は共通ヘルパーへ置き換える。

## テスト計画
- `Anchor` 拡張プロパティが `net10.0` / C# 14 でビルドできること。
- `DistinctBy(x => x % 1000).DistinctBy(x => x % 1000)` が 1 段へ畳まれること。
- `DistinctBy(x => GetKey(x).Anchor).DistinctBy(x => GetKey(x).Anchor)` は 2 段を維持し、`GetKey` の評価回数を保つこと。
- `Anchor` があっても `Where` / `Select` / `Consume` 直結は継続すること。
- `dotnet test Test/Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DistinctBy_keySelector` を通す。
- 可能なら既存 DLL または再ビルド後の BDN で `DistinctBy_keySelector` を再測定する。

## リスクと対策
- C# 14 extension property は `net8/net9` 構成でビルドできない。今回は `net10.0` / C# 14 前提へ寄せることで対応する。
- `Anchor` 除去が漏れると生成コードが不要な API 呼び出しを残す。rendering の正規化で `Anchor` を identity として扱う。
- `Anchor` を含む stage の最適化抑止が粗すぎると性能が落ちる。チェーン全体ではなく該当変換単位で抑止する。

## 未解決事項
- なし。方針は会話で合意済み。

## 合意状態
- 2026-10-26: 不変式の前倒しは上記の無例外性と不変性が確認できる範囲だけに限定する。空sourceでの例外・副作用増加を期待していた旧テストは、空入力で無評価、非空入力では実際の値・例外を確認する回帰へ置き換える。
- `expr.Anchor` の receiver である `expr` を副作用ありと判定する。
- `Anchor` は拡張プロパティへ置き換える。
- Anchor境界を全ての展開判断で維持する。個別の書き換えもSystemとの正常値・例外一致と評価増加なしの契約を守る。
