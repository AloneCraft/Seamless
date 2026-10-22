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
- 浮動小数点: double の SIMD Sum と float の蓄積型で差が出ることを実測で確認した(下の「浮動小数点の Sum」)。直すか許容するかは別途判断する。
- Persisted/Tsavorite の独自型は保存できない可能性がある。経路テストで確かめた型だけ有効にする。
- 文化圏: string の並べ替えは文化圏依存のため Invariant に固定する。
- ハッシュ安定性: `ElementType` は既定値なら書き出さず、既存ハッシュを保つ。
- チェーン検証の回帰チェーンを失う: 段階4の書き出しを段階7の前に必ず行う。

## 未決事項

- [ ] FLinq の double Sum（SIMD で足す順が変わる）と float Sum（float のまま蓄積する）を「誤り」として直すか、「許容」とするか。実測済みで、直さず記録している。
- [ ] Persisted/Tsavorite で string・Int256・Order などが保存できるか（できなければ未対応として記録）。

## 手順3で実装済みの検査と Worker プロトコル v5（2026-10-22 時点）

- 展開検査: `FLinqExpansionValidator`。警告 `FLINQ002` と、生成物の `UnsupportedLinqExpansion(` / `LinqInterceptorFailure(` を、コンパイル前に `ExpansionIncomplete` として失敗にする。
- フォールバック検査: 検証の実行（計測ではない方）を `LinqCounterScope` で囲み、FLinq 2 経路の `EnumerableFallback` が 0 でなければ `ExpansionIncomplete` にする。FLinq は黙って System.Linq へ切り替えず、展開できないと例外で止まる生成コードを出す方針なので、この検査は「その例外を作った」ことを数える。展開検査と見ているものがほぼ同じなので、状態は 1 つにまとめた。
- 例外の型の一致: `QueryIrDocument.ExpectExceptions`（false のときは JSON に出ない）。true のとき、空の入力で例外になる終端・範囲外の `ElementAt`・重複キーの `ToDictionary` も静的な安全検査で弾かない。Worker は、System.Linq が例外を投げたら他の実装も同じ型の例外を投げることを一致とし、違えば `SemanticMismatch` にする。全実装が同じ型の例外を投げたクエリは、計測なしの `Succeeded` で終わる。
- 再現テスト出力: `ReproTestEmitter`。生成クラス（ZLinq なし）と、System / FLinq 最適化なし / FLinq 最適化ありを `DynamicQueryComparer` で比べる xUnit テストを1ファイルにする。実際にコンパイルして実行し、通る場合と失敗する場合の両方を確かめてある。置き先のテストプロジェクトは `Benchmark.Dynamic.Core` を参照する必要がある。
- プロトコル v5: `WorkerRunRequest` に `GenerationMode`（Random / Exhaustive）、`ExhaustiveIntermediateCount`、`ExhaustiveTerminalIds`、`ExpectExceptions` を追加した。全組み合わせモードでは、成り立たない並びを飛ばした上で QueryIndex 番目を使う（`ExhaustiveQueryProvider`）。
- 未実装: 複数の入力件数（0・1・16・256）での検証。IR が件数を持つ形のままでは、件数ごとの再検証に作り直しが要る。必要になったら別途設計する。

## 回帰コーパス（手順4、2026-10-22 時点）

- 置き場所: `Benchmark.Dynamic.Tests/Corpus/representative-chains.json`（25件: 手書きの理想コード付きだったペア15本＋代表チェーン10本）と `regressions.json`（200件: 全組み合わせ100件＋ランダム探索100件）。移植元の最終実行（2026-10-21、`Work/VerifierReport/results.json`）の識別子とメソッド名の並びだけを保存している。
- 変換: `CoreCorpusChainBuilder`。固定した並びで骨組みを生成し、各演算子の引数を移植元の固定の呼び出しに合わせて差し替え、`QueryIrNormalizer` で件数・順序を再計算して検証する。空の入力や範囲外の添字で例外になる並びも許す（`ExpectExceptions`）。
- 近似: 移植元のキー射影つき演算（`UnionBy` / `IntersectBy` / `ExceptBy` / `MinBy` / `MaxBy`、`Join` / `GroupJoin` のキー）は、Dynamic に無いので恒等キー等の版にしている。`x&31` は法32、`Select(x*3+1)` は `x*3` にしている。
- 変換できた件数: 代表チェーン 23/25、回帰 121/200（計144件）。作れない理由は、`SelectMany`（FLinq が CS1673 で扱えない既知の制約）と、Dynamic の順序の規則（順序なしの入力への位置依存演算子や `Zip`）。
- 結果: 変換できた144件は、System / FLinq（最適化なし・あり）/ ZLinq で一致している（既知の不具合の表は空）。これらは移植元が直し切ったチェーンなので、回帰の防止として働く。
- `PairwiseChainIdealTests.cs` は削除し、検証器に依存しない静的チェーンのテストは `Test.System.Linq.Enumerable.FLinq/StaticChainIdealTests.cs` へ移した。`Test.System.Linq.Enumerable.FLinq` は検証器を参照しなくなった。

## 移植中に見つかった FLinq の不具合（2026-10-22 に修正済み）

当初は「生成器の修正は非目的」として記録だけにしていたが、1件ずつ直して再テストを繰り返すのを避けるため、見つかった分をまとめて直す方針に変えた(利用者指示)。修正の内容は後の節「FLinq の不具合の修正」にある。`DynamicChainCorpusTests.cs` の `KnownFlinqDefects` は空になった。

見つかった当時の症状(参考):

- 最適化あり経路だけが System.Linq と食い違う: `Append(54).TakeWhile(...)`、`Prepend(-58).Take(2)`、`Prepend(-1).GroupBy(...).Select(g=>g.Key)`、`Where(偶数).Union(inner)`
- 生成コードがコンパイルできない: `Concat(inner).Take(15)`(CS0103)、`Zip(...).Take(3)`(CS0165)、`Enumerable.Range` に対する `CountBy` / `AggregateBy`(CS0103)

あわせて、Worker の実行先変換が `ToHashSet` / `ToDictionary` / `ToLookup` の出力順序を検証側と違う規則で決めていた不具合を直した（順序なしの入力が `ToLookup` に来ると実行全体が Fatal になっていた）。

## 要素型の次元（手順5、2026-10-22 時点）

- 型の一覧: `ElementTypeCatalog`。`QueryElementType` は Int, Long, Float, Double, Decimal, String, Int256Struct, Int256Class, QuadrupleStruct, QuadrupleClass, NullableInt, Order(複数項目の record)、CollidingKey(ハッシュ衝突型)の13種。型ごとに `ElementTypeDescriptor` が、CLR 型、比較できるか、述語・選択子・キー式・リテラル・合成の式、入力データの作り方、特殊な値を持つ。
- 独自型: `Benchmark.Dynamic.Core/ElementTypes`(`Int256`、`Quadruple`、`ElementEntities`)。独立プロジェクトにはせず Core に置いた(Worker が動的にコンパイルするコードから参照できればよいため)。Int256 は ulong×4 の2の補数で + と * は折り返す。Quadruple は binary128 で、+ * / と比較・int からの変換・NaN・最近接偶数丸めだけを実装(非正規化数は作らない)。`INumber<T>` は実装しない。
- IR: 要素の列の値型は従来どおり `QueryValueType.Int` のまま(int の IR・出力ソース・ハッシュはバイト単位で変わらない。ゴールデンテスト `CoreElementTypeGolden` で確認)。`QueryIrDocument.ElementType` は既定値(Int)なら JSON に出さない。int 以外のときは、要素そのものを返す終端の結果を `ElementQueryAtom`(要素の正規形の文字列)で持つ。
- 使える演算子: 型ごとに外す。要素の列を別の型へ変える演算(GroupBy 系、GroupJoin、SelectIndexed、ToDictionary / ToLookup、AggregateXor、Average、ToIntPair / ToIntGroup、SelectMany)は全型で対象外。比較できない型(Order)では、キー指定なしの順序づけと Min / Max / MinBy / MaxBy を生成しない。
- Source: int 以外は配列・List・IEnumerable だけ(`QueryCapabilityProfiles.SupportsElementType`)。Range は int 専用。Persisted / Tsavorite は、保存が通るかを未確認なので対象外。
- 入力データ: 型ごと・スロットごとにシードから決定的に作る。`WorkerRunRequest.IncludeSpecialValues` で、NaN・-0・無限大・null・空文字列・Int256 / Quadruple の境界値などを約1/16の位置に混ぜられる。
- 比較: 要素の正規形(`ElementCanonicalizer`)。浮動小数点は NaN を揃えて -0 を区別し、decimal は桁まで区別し、string の null と空文字列を区別する。
- プロトコル: `WorkerRunRequest` の末尾に `ElementType`、`IncludeSpecialValues`。能力表の識別子は要素型が int でなければ要素型を含む(int は従来と同じ)。
- 検証: `DynamicElementTypeTests`(12型 × 24本のランダムチェーンを実 Worker で4経路比較、List / IEnumerable の Source でも比較、特殊な値つき)。`CoreElementTypes` / `CoreTypedQueries` / `CoreElementTypeGolden` がコア側の単体テスト。
- 未実装: DB の `element_type` 列(スキーマ v3 と移行)、UI の要素型欄と絞り込み、Persisted / Tsavorite での独自型、浮動小数点だけの差を別状態(`FloatingPointDivergence`)にする方針、文化圏の Invariant 固定(string の順序づけは現状 Ordinal 比較に依存する)。

### 要素型の次元で見つかった FLinq の不具合(2026-10-22 に修正済み)

`DynamicFixedDefectTests.要素型の次元で見つけて直した不具合は直ったまま` が6件を回帰として確かめる。症状(参考):

- ラムダの中の `new T(...)` の型名を変数のように hoist して `new this.__hoistN(...)` を出し、コンパイルできない(CS1526)。
- hoist した変数を宣言せず、コンパイルできない(CS0103 / CS1061)。`ExceptBy` の後の述語などで、int 以外の要素型だけに出る。
- 最適化あり経路だけが System.Linq と食い違う3件: `UnionBy` / `Distinct` の後の `Any`、`Distinct().Prepend(...)` の後の `Any`。
- Sum の選択子の中の桁あふれを FLinq(最適化なし)が checked にして `OverflowException` にする。

## DB の要素型列と UI の要素型欄（手順5の続き、2026-10-22 時点）

- DB: スキーマ版3。`DynamicQueries.element_type`(既定 `Int`)を足した。新規DBは `001_initial.sql` が版3を直接作る。版2の既存DBは `Storage/Schema/Migrations/002_element_type.sql` を `Apply-BenchmarkDynamicMigration.ps1 -TargetVersion 3` で上げる(適用前にバックアップを取る既存の手順)。版が違うDBへは今までどおり書き込めない。移行SQLの動作は `DynamicDatabaseMigrationTests` で確かめている。
- 実行条件: `動的実行設定.要素型` / `動的実行条件Snapshot.ElementType`(古い保存は int として読む)。能力表・Worker要求・保存IRの検証・縮小・Run再実行・履歴の条件文字列に通した。int 以外は、配列・List・IEnumerable 以外のSourceを選ぶと実行前に入力エラーにする。
- UI: 設定ストリップに要素型の選択欄(`動的設定.要素型`)。選んだ値は設定として保存する。
- 未実装のまま: Persisted / Tsavorite での独自型、浮動小数点だけの差を別状態にする方針、文化圏の Invariant 固定。

## ライブ検証タブ（手順6、2026-10-22 時点）

- 置き場所: `Benchmark.Dynamic.UI` の最後のタブ「ライブ検証」(`Views/動的ライブ検証画面.cs`)。計画では先頭のタブだったが、既存の UI テストが先頭タブ前提の探し方をしているため末尾にした。
- 動き: `動的ライブ検証サービス` が Worker を起動し、ペア全組み合わせ(中間1個+終端)→3段全組み合わせ(中間2個+終端)→ランダム探索(終わらない)の順に検証して、結果を1件ずつ渡す。結果を渡す側が待てば、そのまま Worker への背圧になるので、一時停止・速度調整は画面側の待ちだけで済み、プロトコルに Pause / Resume は足していない。
- 画面: 左=段階、中央=式・判定・診断・検証コード・FLinq展開後のコード・再現 xUnit テスト(コピーボタン)、右=展開率と結果一致率のメーター・実行時間とメモリ割り当ての棒グラフ(System / FLinq / ZLinq の中央値、参考表示)・押すと見返せるログ。上部で Source・要素型・特殊な値・Seed・試行番号(ランダム探索の何番目か)・速度を選ぶ。
- 判定は Worker が決めたものをそのまま出す。画面は独自のしきい値を持たず、速度は合否に使わない。
- Worker 側の追加: `WorkerSourceExecutionCompletedEvent.ExpandedSourceText`(FLinq の生成器が出した展開後のコード)。`FixtureValues`(Core)が int 入力の規則を持ち、再現テストの入力が Worker の入力と一致することをテストで確かめている。
- 再現テストは int の配列・List・IEnumerable・Range だけ対応。int 以外の要素型と永続系は、その旨のコメントを出す。
- 開始ボタンを押したときだけ動く(タブを開いただけでは CPU を使わない)。ウィンドウを閉じると Worker を止める。
- 確認: 実画面で、開始→ペア全組み合わせが流れ、式・判定・展開コード・棒グラフ・メーター・ログが更新されることを見た(58件の時点で、合格47・要調査11。要調査は記録済みの FLinq 不具合)。Appium は未起動のため、UI テストの追加分(ID の存在確認)は未実行。
- 未実装: 全組み合わせの件数を画面に出す進捗バー、段階の途中からの再開、複数の入力件数。

### 夜間の3段全組み合わせで見つかった不具合(2026-10-22、修正済み)

SystemArray・シード 0xC0DE・入力16件で、4066件中110件が失敗した(状態は SemanticMismatch 78、CompilationFailed 30、ExpansionIncomplete 2)。その後 SelectMany・DefaultIfEmpty に対応して再実行し、14件を足した。計124件の並びは `Benchmark.Dynamic.Tests/Corpus/fixed-triple-defects.txt` に記録し、`DynamicFixedDefectTests` が全件の一致を確かめる。以下は当時の分布(参考)。

- 先頭の演算子に偏りがある: `groupBy.keyOnly` 25件、`prepend` 20件、`append` 14件、`concat` 9件、`where` 7件、`skip` 7件、`select` 6件。2番目は `concat`・`union`・`unionBy`・`take`・`where`・`skip`・`skipWhile`・`takeWhile`・`zip`・`join`・`groupJoin` が多い。終端は `sum` 40件、`toArray` 26件、`count` 24件。
- 代表の式: `source.Append(-18).Take(7).ToArray()`、`source.Append(-25).Take(2).Count()`、`source.Append(11).DistinctBy(p=>(p%3+3)%3).Sum(p=>(long)p)`、`source.Append(-21).SkipWhile(p=>p<-11).Count()`。先に書いた最適化ありだけが食い違う不具合(`Append` / `Prepend` の後)と同じ系統とみられる(推測)。

### 浮動小数点の Sum(2026-10-22 実測、差は許容と決定)

決定: この差は許容する(利用者承認)。`DynamicQueryComparer` の `FloatingPointTolerance`(相対 1e-9、順序あり・スカラーの比較のみ)で吸収し、`DynamicFloatingPointSumTests` が差の存在を記録として残す。以下は実測の内容。

`DynamicFloatingPointSumTests` が、実際に FLinq の生成器を通して System.Linq とビット単位で比べる(配列と List、サイズ 1〜1000、各20試行)。

- double の `Sum()`(選択子なし、配列・List): 最適化なし・あり、どちらの FLinq も System.Linq とずれる(乱数データで約4割)。FLinq は `SumSpan`(SIMD、4並列の部分和を最後に足す)、System.Linq は順に足す。手で作った「4並列+`Vector.Sum`」の結果は FLinq と 320/320 一致、「順に足した結果」は System.Linq と 320/320 一致した。足す順の違いが原因。
- float の `Sum()`: ずれる(乱数データで約5割)。System.Linq は double で足して float へ戻すが、FLinq は float のまま足す。手で再現した2通りが、それぞれ System.Linq と FLinq に 320/320 一致した。FLinq 側のコメントには float を `SumSpan` から外した理由(System.Linq の仕様に合わせるため)が書かれているが、実際の結果は一致していない。
- 一致するもの: 選択子つきの `Sum`、`Average`(double・float)、整数値だけの入力(厳密に足せる)。
- 80ビットと64ビットの精度の違いではない。x64 の .NET では double の計算はどの経路も64ビットで、差は足す順と蓄積の型から来る。

## FLinq の不具合の修正（2026-10-22）

3段までの全組み合わせ124件と要素型の6件、Sum の選択子、SelectMany、Range の CountBy / AggregateBy を直した。主な根本原因と修正(いずれも `Seamless.FLinq.Generator`):

- 融合してはいけない並びを融合していた: Append / Prepend は列の最後でなければ融合しない、Concat / Union 系は終端つきの融合で前後の位置を限る(`PipelineBuilder.Validate.cs` の `HasSafeConcatLikeOrder`)。順序に依存する段(DistinctBy・添字つき段)の後は順序を無視しない(`CanIgnoreOrderForTerminal`)。
- スカラー走査の適用条件: DefaultIfEmpty は Select / AsEnumerable / Cast とだけ、Reverse の後の状態をもつ絞り込みは逆順に対応する終端だけ、Distinct の後に位置依存の段があれば省かない。
- 宣言漏れ・代入漏れ: Take の終端ラベル、空スパンの型、既定値の宣言、All の述語、ThirdFunc の hoist 変数、ExceptBy / IntersectBy の第2キー(キー選択子を通さない)。
- 巻き上げ(`FragmentBuilder`): 型名・代入先・変換された定数は巻き上げない。
- Sum の選択子は `unchecked` の中で評価する。SelectMany は取り込み変数をローカルへ写す(`CopyCapturedToLocals`)。
- Take(n) は n 個目を通したら止まる。Take より手前の最初のラムダ段(Select / Where など)の前に「上限なら抜ける」判定を置き(`TakeGuard`)、n+1 個目の要素に対して上流のラムダを実行しない(TPC-E 測定で見つかった。System.Linq は呼ばない)。回帰は `Test/System/Linq/Intercept/TakeStopsUpstream.cs`。

生成コードの名前も、人が書いたように読める自然な名前に変えている(`GeneratedNames`): 結果の変数は `arrayResult` 形、巻き上げた値は式の最後の名前か `invariant`、ラムダの引数別名は元の引数名。ぶつかるときだけ番号を足す。構造体の型名と `OptimizedLinq_` 接頭辞は変えていない。

## 合意状態

- 2026-10-22 利用者承認済み: 段階的整理、Benchmark.Dynamic への一本化、要素型13種（int?・record・ハッシュ衝突型を含む）、Quadruple は LINQ に必要な演算のみ、UI の取り込み、実装前の現状コミット。
- 未合意: 上の未決事項2件。
