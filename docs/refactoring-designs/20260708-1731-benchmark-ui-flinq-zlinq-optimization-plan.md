# Benchmark UI / FLinq-ZLinq Optimization Plan

## 目的

Benchmark UIで計測・DB保存・Run選択・DB読込中の再入を防ぎ、Array/IEnumerableの全ベンチマークをReleaseで安定して計測できる状態にする。
その後、DBまたは画面の計測結果から`FLinq平均時間 / ZLinq平均時間`が大きい順に原因を解析し、メソッド固有の最適化を改善余地がなくなるまで繰り返す。

## 合意済み前提

- 初回計測はReleaseで行う。
- 要素数は既存constの`4096`を使う。
- 初回計測時間は`1ms`として扱う。
- 本計測はRelease、`4096ms`、8物理コア、HTなしで行う。
- 8コアHTなしは、UI上のCPU選択で物理8コア分だけ選び、同一物理コアの2本目の論理CPUは選ばない意味とする。
- 対象はArrayタブとIEnumerableタブに表示される全ベンチマーク行・全FLinq/ZLinqセル。
- 差分ランキングは`FLinq平均時間 / ZLinq平均時間`の比率が大きい順に並べる。
- 改善対象の閾値は`FLinq/ZLinq > 1.10`とし、`<= 1.10`は原則として改善余地なし扱いにする。
- `Aggregate`の改善が`Sum`に効く場合でも、`Sum`は別メソッドとして再ランキング・再評価し、固有最適化余地が残っていれば追加で直す。
- Runドロップダウンで`New`を選んでもDBレコードは削除せず、画面表示と未保存/一時状態だけをクリアする。
- DB保存は自動保存とする。
- 計測開始時に全対象ベンチマーク行を空状態でバッチinsertし、1行完了するたびに該当行をupdateする。
- 1行単位更新とし、Left/Rightの片側だけ完了した時点でも、その行を片側Completed・もう片側Running/Queuedなどの途中状態としてupdateする。
- 完了イベントごとに全レコードを追加insertしてDB行を増殖させる方式は禁止する。
- 診断カウンターは最初から実装計画に含め、値をDB保存対象にも含める。
- DBスキーマ変更が必要な場合は、対象DBを明示的にバックアップしてから直接スキーマ変更する。

## UI操作ブロック方針

ブロック対象:

- 計測開始ボタン
- DB保存
- Runドロップダウン
- DB読込
- `New`選択
- 停止要求後の停止ボタン

ブロック対象外:

- CPU選択
- 実行プロファイル
- 時間設定
- 要素数設定
- ベンチマーク行/セル選択

ブロック対象外の設定変更は、実行中の計測には影響させず、次回計測から反映する。
停止ボタンは計測中だけ有効にし、停止要求を1回送った後は処理完了まで無効にする。

## WBS

### Task 1: UI再入防止と操作ブロックを設計・実装する

完了条件:

- 計測開始ボタンは、計測中・DB保存中・DB読込中・Run選択適用中に押せない。
- Runドロップダウン、DB読込、DB保存、`New`選択は、対象処理中に操作できない。
- 停止ボタンは計測中だけ押せる。
- 停止要求後は停止ボタンも押せない。
- CPU選択、実行プロファイル、時間設定、要素数設定、ベンチマーク行/セル選択は操作可能だが、実行中の計測には影響しない。
- UI無効化だけに依存せず、コマンド入口でも再入を防ぐ。

検証方法:

- 計測開始ボタン連打でRunが二重作成されないことをテストする。
- 停止ボタン連打で停止要求が1回だけ処理されることをテストする。
- DB読込中にRun選択が再入しないことをテストする。
- 実行中にブロック対象外設定を変更しても、現在のRunの条件が変わらないことを確認する。

リスク:

- UIだけを無効化しても、コマンド入口が再入可能だと二重実行が起きる。
- 停止要求中の状態が曖昧だと、停止ボタンが再度押せる。
- 除外対象の設定変更が実行中Runへ混入すると、DB上の計測条件と実測条件がずれる。

### Task 2: DB自動保存を空行insert + 行update方式に整理する

完了条件:

- Run開始時に、対象となる全ベンチマーク行を空状態でバッチinsertする。
- 1行完了ごとに、同じDB行をupdateする。
- Left/Rightの片側だけ完了した場合も、同じ行に途中状態を保存する。
- Run完了時に最終ステータスを確定する。
- 停止・失敗・クラッシュ後も、未完了行を含む途中状態としてDBから読める。
- 完了イベントごとに全件insertされず、DB行数がベンチマーク行数に対して不自然に増えない。

検証方法:

- 100行中10行だけ選択したケースで、1行完了ごとに100行追加されないことをDB行数で確認する。
- 途中停止後にRunを再読込しても画面表示が落ちないことを確認する。
- 片側のみCompletedの行をDBから読んで、画面上も片側Completed・片側Running/Queued相当で表示できることを確認する。

リスク:

- 初期insertとupdateの一意キーがずれると重複行が発生する。
- 途中状態を正しく扱わないと、再読込時にMissingまたはFailedとして誤表示される。
- DBスキーマ変更をアプリ起動時の自動削除に任せると、既存DBの保護要件に反する。

### Task 3: span経路診断カウンターを追加しDBに保存する

完了条件:

- `DirectSpanUsed`を記録できる。
- `TryGetSpanCalled`を記録できる。
- `TryGetSpanSucceeded`を記録できる。
- `TryGetSpanFailed`を記録できる。
- `EnumerationLoopUsed`を記録できる。
- `FastCopyUsed`を記録できる。
- `HashCapacityHintUsed`を記録できる。
- カウンター値をRun/Method/SourceTab/Project/ArgumentKey/ElementCountに紐づけてDB保存できる。
- 診断値をDBまたは画面/レポートから確認できる。

検証方法:

- Array由来のrootでdirect spanが記録されることを確認する。
- IEnumerable由来のrootで`TryGetSpanCalled`と成功/失敗が記録されることを確認する。
- spanに入れないケースで`EnumerationLoopUsed`が増えることを確認する。
- `ToHashSet`/`Union`系でcapacity hint利用有無を確認する。

リスク:

- 診断カウンター自体がベンチマーク時間を大きく歪める可能性がある。
- メソッド別に紐づかない集計だと、差分ランキングの原因分析に使えない。
- `FallbackDiagnostics`が空であることとspan利用成功は同義ではない。

### Task 4: 初回Release 1ms計測を実行できる状態にする

完了条件:

- Release構成で起動できる。
- 要素数は既存constの`4096`を使う。
- 計測時間は`1ms`に設定される。
- ArrayタブとIEnumerableタブの全行・全FLinq/ZLinqセルを対象にできる。
- 全対象がCompletedになるまで、失敗原因を調査して修正できる作業ループが定義されている。

検証方法:

- Arrayタブの全対象がCompletedになることを確認する。
- IEnumerableタブの全対象がCompletedになることを確認する。
- Failed/Missing/Stoppedが残る場合は、メソッド・Project・ArgumentKey・例外・DB状態で分類する。

リスク:

- 1msは短いため、計測値の比較精度よりも成功/失敗検出が主目的になる。
- 生成コード失敗、DB保存失敗、UI投影失敗を混同すると、最適化対象を誤る。

### Task 5: DB保存後のNewクリアと再読込を検証する

完了条件:

- 成功後、自動保存されたRunがRunドロップダウンに出る。
- `New`を選ぶと画面上の現在表示と一時状態がクリアされる。
- DBレコードは削除されない。
- 保存済みRunをドロップダウンから選ぶと、保存前と同じ行数・ステータス・数値で表示される。

検証方法:

- `New`選択前後でDBのRun/行数が減っていないことを確認する。
- 保存済みRun再読込後、Array/IEnumerableそれぞれの行数、Completed数、Mean、Allocated、診断カウンターが保存前と一致することを確認する。

リスク:

- `New`選択がDB削除やRun選択状態の破壊を伴うと、再読込検証が成立しない。
- UI表示は合っていてもDB保存が欠けている場合、アプリ再起動後に復元できない。

### Task 6: FLinq/ZLinq差分ランキングを作成する

完了条件:

- DBを優先し、DBが不完全な場合のみ画面表示を補助情報として使う。
- 同一SourceTab、Method、Type、ArgumentKey、ElementCountのFLinq/ZLinqを対応付ける。
- `FLinq平均時間 / ZLinq平均時間`を計算する。
- 比率が大きい順に並べる。
- `FLinq/ZLinq > 1.10`のみ改善対象として抽出する。

検証方法:

- 対応するZLinq結果がない行をランキング対象外または要調査として分離する。
- Meanが0または未計測の行をランキング対象外または要調査として分離する。
- ランキング上位に診断カウンターを併記する。

リスク:

- 異なる引数や異なるSourceTabを比較すると誤ったランキングになる。
- 失敗行を0扱いすると比率が壊れる。
- DBと画面の値が不一致の場合、原因分析前に保存/読込整合を直す必要がある。

### Task 7: 上位メソッドを1件ずつ原因分析する

完了条件:

- ランキング最上位から1メソッドずつ扱う。
- 原因をroot、pipeline、terminal、診断不足、DB/画面不整合に分類する。
- `Array`で遅い場合は、`ArrayRoot`/direct spanが保たれているか確認する。
- `IEnumerable`で遅い場合は、runtime `TryGetSpan`の成功/失敗と列挙落ちを確認する。
- `ToList`、`Union`、`ToHashSet`、`ToLookup`、`Aggregate`、`Sum`は、上位に出た場合に固有の原因を確認する。

検証方法:

- 生成コードまたは診断カウンターでspan経路の通過有無を確認する。
- Hash系はHashBuild/HashProbe/capacity hintを確認する。
- materialize系はFastCopyUsedとbuffer addを確認する。
- delegate系はLogicalDelegate/PhysicalDelegateを確認する。

リスク:

- `Aggregate`共通改善だけで`Sum`固有の数値型fast path確認を省くと、要求を満たさない。
- set系はアルゴリズム上の差と実装上の無駄を分けないと、不要な最適化を行う。
- span利用率だけを原因と決め打ちすると、capacityやdelegate呼び出しの問題を見逃す。

### Task 8: メソッド固有最適化を1件ずつ実装・検証する

完了条件:

- 1メソッドだけ修正する。
- 対応する単体テスト、fragment coverage、chain test、必要に応じてgenerated code assertionを追加する。
- Release 1msでArray/IEnumerableを再計測する。
- DB保存後、差分ランキングを再作成する。
- 対象メソッドの`FLinq/ZLinq`が`<= 1.10`になる、または残差が契約上妥当と説明できる。

検証方法:

- `dotnet test`で関連テストが通ることを確認する。
- UI計測で対象メソッドがCompletedになることを確認する。
- DB上の診断カウンターが想定経路を示すことを確認する。
- 既存の他メソッドランキングが大きく悪化していないことを確認する。

リスク:

- 1メソッド修正が共通fragmentに影響し、他メソッドを悪化させる可能性がある。
- ベンチマーク時間が短い段階では、比率の揺れを過大評価する可能性がある。
- 改善がSumなど別メソッドへ波及した場合でも、再ランキングなしに完了扱いにしてはいけない。

### Task 9: 改善余地がなくなるまでループする

完了条件:

- 差分ランキングで`FLinq/ZLinq > 1.10`の改善対象がなくなる。
- 残る差分がある場合は、メソッドごとに「契約上妥当」「アルゴリズム差」「追加最適化の副作用が大きい」などの理由が説明されている。
- `Aggregate`改善後も`Sum`を再評価済みである。

検証方法:

- 各ループのランキング結果を保存する。
- 修正前後の比率、診断カウンター、失敗有無を比較する。
- 改善対象ゼロの最終ランキングを確認する。

リスク:

- すべてを一度に修正すると、どの変更が改善/悪化を起こしたか分からなくなる。
- しきい値以下の差分まで追い続けると、リスクに対して効果が小さくなる。

### Task 10: Release 4096ms / 8コアHTなし本計測を行う

完了条件:

- Release構成で実行する。
- 計測時間は`4096ms`に設定する。
- CPU選択は8物理コア、HTなしにする。
- 要素数は既存constの`4096`を使う。
- Array/IEnumerableの全対象がCompletedになる。
- DBに自動保存される。
- `New`で画面をクリア後、保存済みRunを読込んで保存前と同じ行数・ステータス・数値で表示される。

検証方法:

- UI上のCompleted数を確認する。
- DB上のRun、ベンチマーク行、診断カウンターを確認する。
- 再読込後の画面表示とDB値を照合する。
- 最終ランキングで`FLinq/ZLinq > 1.10`が残っていないか確認する。

リスク:

- 4096msでは1msで見えなかった安定性問題やタイムアウトが出る可能性がある。
- CPU選択がUI上は変更できるため、Run開始時のスナップショットをDBへ保存しておかないと条件が曖昧になる。

### Task 11: 本計測失敗時の最大3回リトライを管理する

完了条件:

- Release 4096ms本計測で失敗した場合、原因を分類して修正する。
- 修正後の再計測を1回として数える。
- 最大3回までやり直す。
- 3回失敗した場合はそこで停止し、未解決原因、再現条件、次に見るべきファイル、暫定回避策をまとめる。

検証方法:

- 各リトライについて、失敗メソッド、失敗理由、修正内容、再計測結果を記録する。
- 3回以内に全対象Completedになれば成功扱いにする。
- 3回失敗時は、未解決リストが作成されていることを確認する。

リスク:

- 同じ原因を繰り返し直していないかを記録しないと、リトライ回数の意味がなくなる。
- 計測条件を変えてしまうとリトライ結果を比較できない。

## 主な調査対象ファイル

- `Benchmark/Shared/UI/ViewModels/RunnerViewModel.cs`
- `Benchmark/Shared/UI/RunOrchestrator.cs`
- `Benchmark/Shared/UI/RunStatePersistence.cs`
- `Shared/UI.Storage/UiRunStore.cs`
- `Work/UI/runs.db`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/Root.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/TraversalSupport.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/ToList.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/ToHashSet.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/ToLookup.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/Union.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/Aggregate.cs`
- `Seamless.FLinq/SourceGenerator/Linq/Fragments/Sum.cs`
- `Seamless.Linq/Linq/LinqCounter.cs`

## 完了判定

この計画全体の完了は、以下がすべて証明された時点とする。

- UI再入防止がテストまたは手動検証で確認済み。
- DB保存が空行insert + 行update方式で重複増殖しない。
- 診断カウンターがDB保存され、原因分析に使える。
- Release 1msでArray/IEnumerableの全対象がCompleted。
- DB保存後に`New`でクリアし、保存済みRunを再読込して保存前と同じ表示になる。
- 差分ランキング上の`FLinq/ZLinq > 1.10`がなくなる、またはメソッドごとの残差理由が明確化される。
- Release 4096ms、8物理コアHTなしでArray/IEnumerableの全対象がCompleted。
- 本計測結果がDB保存され、`New`後の再読込で保存前と同じ表示になる。
- 本計測失敗時は最大3回の修正・再計測ルールに従い、成功または未解決理由を記録している。
