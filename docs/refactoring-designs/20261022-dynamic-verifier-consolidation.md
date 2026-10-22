# チェーン検証の Benchmark.Dynamic への統合と要素型13種の追加

作成日: 2026-10-22。対象: `G:\GitHub\Seamless`（ブランチ tpce-benchmark、起点 `0817592f`）。本文の「確認」はソースの静的確認であり、実行成功を意味しない。

## 背景

gemini argon が作った `Samples/Sample.FLinqChainVerifier`（.Core / .Cli / MAUI）が、FLinq 生成器の改善に大きく効いた。利用者は「この1つで他のベンチ・テストの大部分が不要になった」と考えている。

調査の結果、そのままでは置き換えにならない。一方、既存の `Benchmark.Dynamic` がほぼ同じ役割（ランダムなチェーンを Roslyn + 生成器で組み立て、System.Linq と比較）をより多く担っている。重複する2つの仕組みを Benchmark.Dynamic に一本化する。

## 現状の観察

チェーン検証（確認済み）:
- 要素は `int` のみ。Persisted/Tsavorite は偽物（`Samples/Sample.FLinqChainVerifier.Core/Execution/動的検証ランナー.cs:418` で `配列.Where(_=>true)`）。
- 計測は Stopwatch 20回で、FLinq が勝った時点で測り直しをやめる。手書き理想のない行の「理想時間」は opt×0.95。合格しきい値も独自。
- `--replay` は段階番号が1つずれ、シードも固定値に戻る。CI で実行されていない。
- 演算子は30中間＋26終端。段階は ペア / 代表10本 / 全組み合わせ(23,374) / ランダム。

Benchmark.Dynamic（確認済み）:
- 本物の8ソース、ZLinq、縮小（`QueryShrinker.cs`）、SQLite 履歴、JSON 行プロトコル、CI（`dotnet-ci.yml`）、`Verification.Infrastructure` から利用。
- IR は int 専用（`QueryIr.cs` の検証が int 以外を拒否）。ランダム生成のみで全組み合わせ生成は無い。
- 展開検査は文字列1つ。FLINQ002 は警告のため見逃す。`EnumerableFallback` と例外の型の一致は見ていない。

他プロジェクト（要点）: 永続コレクション・Scalable・TPC-E・ParameterSweep・Worker/Headless・UI・Audit・ZLinq 系・Verification 系は、チェーン検証では代替できない。空の `Test.Persisted` / `Test.Tsavorite`、残骸 `Test.Tsavorite.Collection` / `Benchmarl.Dynamic` / `Worker.Aot` / `Aot.Protocol` は消しても失うものがない。

## 過去設計から引き継ぐ仕様

`docs/refactoring-designs/20260928-seamless-repository-refactoring-plan.md` より:
- Benchmark/Dynamic は独自の最終合否を持たない。性能判定の正本は `Verification.Core.VerificationComparison`。
- System.Linq と FLinq の意味の一致（結果・順序・例外・評価回数）を守る。
- `.projitems` / `.shproj` の整合と `Seamless.slnx` 登録。軽量 solution は `Update-FocusedSolutions.ps1` から再生成する。
- 保存済み DB はアプリが自動変換・自動削除しない。スキーマ変更は明示の移行スクリプトで行う。

## 目的

1. ランダムチェーン比較を Benchmark.Dynamic に一本化する。
2. チェーン検証の良い部分（全組み合わせ生成、不足していた演算子、展開検査、フォールバック検査、再現テスト出力、UI）を移植する。
3. 要素型を13種に広げる。
4. 重複・残骸のプロジェクトを削除する。

## 非目的

- 永続コレクション、DB、Scalable、TPC-E、ParameterSweep、計測基盤、ZLinq 系、BDN 系、Audit の削除。
- チェーン検証の偏った計測・偽の理想時間・独自しきい値・決め打ちの診断文の移植。
- Quadruple の IEEE 完全準拠（非正規化数、全丸めモード、文字列解析）。
- FLinq 生成器本体の修正（今回見つかった差は記録し、直す・許容するの判断は別途）。

## 提案設計

段階0〜8（承認済みの計画 `C:\Users\kazuh\.claude\plans\gemini-argon-process-1-1-swift-rabin.md` と同じ）。

1. **残骸削除**: 空・残骸の5プロジェクトと slnx / `Update-FocusedSolutions.ps1` の登録。
2. **要素型カタログ**: `Benchmark.Dynamic.Core/ElementTypeCatalog.cs`（`QueryElementType`、`ElementTypeDescriptor`）。最初は Int のみ。固定シードで出力ソースと IR ハッシュがバイト単位で変わらないことをゴールデンテストで固定する。
3. **機能移植（int）**: 演算子（TakeLast, SkipLast, TakeWhile, SkipWhile, Append, Prepend, DefaultIfEmpty, OrderByDescending, 非恒等キー, GroupBy_*, Zip）と終端（Count/Any(p), Average(selector), Aggregate, SequenceEqual, CountBy, AggregateBy）。全組み合わせ生成（深さ2/3）。展開未完了（目印文字列・FLINQ002・呼び出しごとの傍受漏れ）と `EnumerableFallback==0` の検査。複数入力件数と例外の型の一致。`ReproTestEmitter`。Worker プロトコル v5。
4. **回帰コーパス**: チェーン検証の ペア・代表・回帰チェーン（`S3_Array_0027` など）を IR に変換して `Corpus/*.json` とし、`CorpusRegressionTests.cs`（`Category=ChainCorpus`）で実行する。`PairwiseChainIdealTests.cs` を置き換える。
5. **要素型13種**: int, long, float, double, decimal, string, Int256(struct/class), Quadruple(struct/class), int?, record `Order`, `CollidingKey`。型ライブラリは新規 `Benchmark.Dynamic.ElementTypes`。IR v2（`ElementType` は既定値なら書き出さない）、DB スキーマ v3、能力表は (ソース, 型) 単位。浮動小数点の差は `FloatingPointDivergence` として記録する。
6. **UI**: `Benchmark.Dynamic.UI` に「ライブ検証」タブを追加（段階レール、チェーン式、生成コード、再現テストのコピー、棒グラフ、ログ、一時停止/再開、シード再現）。
7. **削除と付け替え**: `Samples/Sample.FLinqChainVerifier*`、`DynamicLinq.Runner`、`Shared/DynamicLinq`。`EnumerableComparisonCatalog.cs` は `Test/System/Linq/Intercept/` へ移す。
8. **CI**: PR は `ChainCorpus`、夜間は実行コーパス。

## 影響範囲

- 削除: `Samples/Sample.FLinqChainVerifier{,.Core,.Cli}`、`DynamicLinq.Runner{,.slnx}`、`Shared/DynamicLinq`、`Test.Persisted`、`Test.Tsavorite`、`Test.Tsavorite.Collection`、`Benchmarl.Dynamic`、`Benchmark/Worker.Aot`、`Benchmark/Aot.Protocol`。
- 変更: `Benchmark.Dynamic.{Core,Worker,Infrastructure,UI}`、`Benchmark.Dynamic.Tests`、`Benchmark.Dynamic.UITests`、`Verification.Infrastructure/DynamicVerificationAdapter.cs`、`Test/Test.csproj`、`Test.System.Linq.Enumerable.FLinq`、各 slnx、`Update-FocusedSolutions.ps1`、`.github/workflows/dotnet-ci.yml`、`Benchmark.Dynamic実装計画.md`。
- 互換性: IR v1 は int として読める。DB は v3 へ明示の移行スクリプトで移行する。Worker プロトコルは v5 になり UI・Worker・Verification を同時に再ビルドする。

## 移行手順

段階ごとにビルド・テストしてコミットする。push はしない。

- 段階0: 作業ツリーのコミット（完了: `0817592f`）。
- 段階1〜2: 動作を変えない整理。
- 段階3〜4: 移植と回帰コーパス。**チェーン検証を消す前に**、書き出した全項目が Dynamic で合格することを確認する。
- 段階5〜6: 型と UI。
- 段階7〜8: 旧プロジェクトの削除と CI。

## テスト計画

- ゴールデン: 固定シードの出力ソースと IR ハッシュ（段階2の前後で不変）。
- 経路テスト: 演算子ごと、要素型×ソースごと（`DynamicRouteShapeTests.cs`）。
- 型の単体テスト: Quadruple の最近接偶数丸め、NaN の `Equals` と `==`、ハッシュの一貫性、Int256 の折り返し。
- 回帰コーパス: `CorpusRegressionTests.cs`。
- コマンド: `dotnet build Seamless.slnx`、`Benchmark.Dynamic.Tests`（通常と `CoreOnly=true`）、`Test.System.Linq.Enumerable.FLinq`、`Test`（`EnumerableComparisonCatalogTests`、`OptimizationFixedPointTests`）、`Verification.Tests`、`Benchmark.Dynamic.UI` の Release ビルド。
- UI は Windows の Debug/Release で実画面を確認する（段階の自動進行、一時停止、シード再現、テストコピー、型の切り替え）。

## リスクと対策

- 新演算子は IR の変更を要する（SequenceEqual は第2ソース、Zip は件数が min、GroupBy_* は合成演算）。検証・正規化・縮小まで対応しないと再生が壊れる。
- CI 時間: 23k の3段を実行すると1〜1.5時間。まとめてコンパイルし、範囲で分割し、夜間のみにする。
- ALC のアンロード漏れ: バッチごとに確認し、既存のストレステストを維持する。独自型は既定コンテキストから参照する。
- 浮動小数点: double の SIMD Sum と float の蓄積型は差が出る見込み（推測）。別途判断する。
- Persisted/Tsavorite の独自型は保存できない可能性がある。経路テストで確かめた型だけ有効にする。
- 文化圏: string の並べ替えは文化圏依存のため Invariant に固定する。
- ハッシュ安定性: `ElementType` は既定値なら書き出さず、既存ハッシュを保つ。
- チェーン検証の回帰チェーンを失う: 段階4の書き出しを段階7の前に必ず行う。

## 未決事項

- [ ] FLinq の double Sum（SIMD で順序が変わる）と float Sum（float のまま蓄積する可能性）を「誤り」として直すか、「許容」とするか。
- [ ] Persisted/Tsavorite で string・Int256・Order などが保存できるか（できなければ未対応として記録）。

## 合意状態

- 2026-10-22 利用者承認済み: 段階的整理、Benchmark.Dynamic への一本化、要素型13種（int?・record・ハッシュ衝突型を含む）、Quadruple は LINQ に必要な演算のみ、UI の取り込み、実装前の現状コミット。
- 未合意: 上の未決事項2件。
