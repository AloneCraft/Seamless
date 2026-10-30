# Seamless 全体の責務分離リファクタリング計画

作成日: 2026-09-28。対象: `G:\GitHub\Seamless`。調査時の HEAD は `f65062c6`、作業ツリーは clean。本文の「確認」はソース・設定・テスト定義の静的確認であり、今回の実行成功を意味しない。各実装単位へ着手する際は HEAD、dirty 差分、成果物、環境、現行テスト結果を取り直す。

## 目的と今回の判断

利用者が覚える型・作成手順・例外規則を減らし、実装では状態の所有者と依存方向を明確にする。一つの変更で追う経路を狭め、単独で検証できる構造にする。性能は同条件の測定で評価し、正しさ、データ保全、検証の再現性を先に確立する。

今回の会話で次を決定した。

| 論点 | 決定 |
|---|---|
| 対象 | リポジトリ全体を調べ、優先順位を付けて段階的に実施する |
| 公開 API | 必要な破壊的変更を許容する。リポジトリ内の利用側を移行してから旧 API を削除する |
| 保存済みデータ | 新たな保存形式変更で旧データの読込互換は要求しない。明示バックアップ後に新規作成する。アプリ起動時の自動変換・自動削除はしない |
| 機能範囲 | 既存機能の整理と不具合修正に集中する。SQL ルーチン実行、演算子の対応拡張などの新機能は別計画とする |
| 性能判定 | 旧固定 run ID 性能ゲートを削除し、`Verification.Core.VerificationComparison` を合否判定の正本にする。時間と割り当て量の両方を回帰判定する。Benchmark の測定画面と履歴表示は利用機能として維持する |
| 比較対象との速度 | 今回の必須条件は変更前からの非回帰。FLinq と System.Linq/ZLinq 等の差は同条件で記録し、劣位ケースを原因と効果で優先付けして改善する。全ケースで比較対象より速いことを今回の一律ゲートにはしない |
| UI 受け入れ | Windows の Debug/Release で実画面まで確認する。MacCatalyst は別の受け入れ段階とする |
| 同時使用の検出 | Tsavorite の操作用オブジェクトへ同時に複数操作が入った場合は Release でも例外にする。別スレッドからの順次利用は許可し、代表ケースで性能影響を測って採用する |

## 現行の根拠と資料の扱い

- [Atras の不変条件と禁止事項](../AtrasReverseEnginnering/02_Contracts.md)、[Atras の受け入れ項目](../AtrasReverseEnginnering/04_Acceptance.md)、[Opus の全体像](../Opus5.5ReverseEnginnering/00_全体像と共通ルール.md)を、契約候補と網羅性の確認に使う。両資料は過去の HEAD `d3a5a3e8` を基準にした部分があるため、現行コード・現在の決定を優先する。
- `6ac6c388` で FLinq 終端の列挙子解放、`f905f118` で複数の既知失敗テスト、`10b6bb52` で低層設定と Persisted の一時作業コレクション、`f65062c6` で FLinq の二つの傍受問題への修正が入った。過去の「未実装」「未コミット」「失敗」を現行障害として転記しない。
- [低層設定の現行記録](../低層設定の公開と観測_実装計画.md)にある公開 `TsavoriteTuning`、`StorageTuning`、統計取得、`.hindex` 旧形式の明示拒否を前提にする。予算予約量はプロセス全体のメモリ上限と同義ではない。
- 現行の [共通 Store](../../Seamless.Execution/Store.cs) は寿命・予約予算・明示 `Save` を持つ。[Tsavorite 拡張](../../Seamless.Collections.Tsavorite/Stores/StoreExtensions.cs)と [Persisted 拡張](../../Seamless.Collections.Persisted/StoreExtensions.cs)は共通 Store から作成する入口を持つ。一方、[DatabaseContainer](../../Seamless.Database/DatabaseContainer.cs)や一部のテスト・Benchmark は旧 factory を直接使用している。
- [Persisted Dictionary の Compact](../../Seamless.Collections.Persisted/Dictionary.cs) は全件をメモリ上の辞書へ集めて整列する。[SerializedList の Rewrite](../../Seamless.Collections/Persisted/SerializedList.cs)にも全件配列化がある。大規模入力と資源予算の契約に対する修復候補である。
- [ScopedQuery](../../Seamless.Linq/ScopedQuery.cs) は `IEnumerable<T>` を実装し、一部の演算子だけを自前で公開する。[現在の Analyzer](../../Seamless.Linq.Analyzers/UnorderedEnumerableAnalyzer.cs)は unordered の位置依存操作を扱う。scope 付きクエリの一覧外の呼び出しが通常 LINQ に解決される境界を、実際の呼び出しと生成コードで確認し、暗黙の全件メモリ処理を検出する。
- [FLinq のサービス](../../Seamless.FLinq.Generator/SourceGenerator/LinqGenerationServices.cs)と [出力 Coordinator](../../Seamless.FLinq.Generator/SourceGenerator/GeneratedSourceRenderCoordinator.cs)は元の巨大な partial 実装へ委譲している箇所がある。型を分けただけで処理や状態の所有者が移っていない部分を対象にする。
- [Benchmark の UI 実行ストア](../../Shared/UI.Storage/UiRunStore.cs)は、通常 Benchmark の既存 DB がスキーマ不一致のとき、自動でバックアップ後に本体・WAL・SHMを削除する。[現行テスト](../../Test/Benchmark/UiRunStore.cs)もその動作を期待している。これはリポジトリの DB 変更規則と矛盾する。実行被害は今回確認していないが、設計変更前にコードとテストの契約を是正する。
- [Verification の比較実装](../../Verification.Core/VerificationComparison.cs)は条件一致、結果ダイジェスト、5回以上の測定、中央値と既定5%境界を扱う。[現行説明](../verification.md)でも旧固定ゲートを互換用途としている。
- 旧ゲートは左右の結果ダイジェスト、CPU、アフィニティ、LoopCount、割り当て量を検査する。一方、[現行の Headless adapter](../../Verification.Infrastructure/HeadlessBenchmarkVerificationAdapter.cs)は左右の時間と割り当てを収集するが、`ResultDigest=null` で `Passed` を返す。[paired Worker](../../Benchmark/Worker/Program.cs)も結果ダイジェストを返さない。現在の比較器は null 同士のダイジェストを一致と扱う。旧ゲートの削除前にこの空白を埋める。
- 通常 UI/Scalable/Headless と Dynamic UI の Worker 構築経路は異なる。前者の `ProjectReference` 成功を後者の VS F5 成功と読み替えない。既存 UI 自動テストにはアプリ実行ファイル未指定による skip があるため、Windows 実画面の操作・再起動を別に確認する。Opus 資料の一部にある Parameter Sweep DB 版20と Worker build contract の旧失敗は現行コードの状態ではない。

## 目指す責務の境界

| 境界 | 正本となる責務 | 外へ押し出さないもの |
|---|---|---|
| `Seamless.Execution` | Store/scope の寿命、保存世代、共有予算、Codec と一時資源の所有 | Tsavorite/Garnet のデバイス詳細、UI/Verification の履歴DB |
| Tsavorite / Persisted | エンジン固有の作成、保存状態、走査、件数・索引・I/O の規則 | 共通 Store の寿命や予算の別実装 |
| `Seamless.Linq` | 通常 .NET の意味と scope 付きクエリの意味を区別した実行、退避処理、列挙子の寿命 | FLinq 生成器の解析・診断状態 |
| FLinq 生成器 | 対象発見、解析、最適化、Fragment 構築、描画、診断を入力単位の状態で組み立てる | ランタイムの保存責務、UI 固有の判定 |
| Database / Import | transaction・WAL・復旧の意味、SQL 解析/生成/取込をそれぞれの段階で担当する | Store の保存を DB commit と同一視すること、未対応 SQL の暗黙実行 |
| Benchmark / Dynamic | 入力・Worker・測定値・診断・履歴の取得と提示 | 独自の最終合否基準 |
| Verification.Core / Infrastructure | 計画、条件照合、合否、baseline、replay/resume/shrink、成果物の検証 | 画面固有の補正・判定 |
| 各 UI / CLI | 操作と表示、取消し・再実行の導線 | エンジンの状態、測定の正本、DB の自動変更 |

この表は新しい基底クラスやインターフェースの追加を要求しない。既存の責務を移した結果、委譲だけの層や重複した経路が減ることを受け入れ条件にする。

## 全段階で守る不変条件

1. **意味:** System.Linq と FLinq は対象 API で結果、重複、順序、例外、評価回数、遅延性、列挙子解放が一致する。通常の .NET 等値性と scope の決定的バイト一致、新しい `Work` コレクションの明示比較子を混同しない。
2. **資源:** 大規模経路は共有予算へ予約し、全件がメモリへ載る前提にしない。予約不足や未対応は理由付きで失敗する。scope と列挙子、ファイル lock、Tsavorite accessor の寿命が一致する。
3. **保存:** 永続 Store の確定は明示 `Save` のみ。DB は commit 済み WAL のみを復旧し、全対象の checkpoint が終わるまで必要な WAL を保持する。読取中の writer 待機、同一文脈の昇格拒否、入れ子 rollback の意味を維持する。
4. **コレクション:** データ本体と操作用オブジェクトを区別し、Count の既知・不明・再計数を混ぜない。`long` を正本とし、`int` の無言の桁あふれを許さない。複数 accessor 生存中の索引拡張保留を維持する。Release の誤用検出は「同時操作」を判定し、作成スレッドの違いだけでは拒否しない。
5. **生成:** Registry、能力、診断位置、生成名、trace 順序、入力単位の incremental cache を維持する。型名に依存する FLinq の既存最適化と公開 API の変更を同じ移行単位で確認する。
6. **測定:** ソース・Worker・DLL・入力・条件・結果を対応付ける。対象ケースで結果の一致を確認できない場合は性能を合格にしない。paired 測定の左右で CPU/アフィニティ/LoopCount が異なる結果を同条件として扱わない。条件不一致、未完了、取消し、skipped、誤結果を成功へ読み替えない。baseline は明示操作だけで更新する。
7. **プロジェクト:** `.projitems` と `.shproj` の GUID/import 名、`Seamless.slnx` 登録を一致させる。軽量 solution は `Update-FocusedSolutions.ps1` から再生成する。既存の利用者変更と保存データは所有者確認なしに上書きしない。

## 禁止事項

- 症状を隠す固定値、例外の握り潰し、待機・再試行の増加だけで完了とすること。未対応経路を別の意味へ黙ってフォールバックすること。
- 全件 `List`、`Dictionary`、`ToArray` で予算を迂回すること。小さい入力だけに替えて大規模経路が修復されたと報告すること。
- DB や履歴を起動時の schema 判定、版の増加、自動削除で移行すること。保存形式を変える場合は対象データを明示バックアップし、新規作成・復旧手順を示す。
- 新旧 API を恒久的な二重正本にすること。利用側の移行前に旧 API を削除すること。保存・WAL・比較子の異なるエンジンを形だけ共通化すること。
- `.g.cs` だけを直すこと、古い生成器や Worker/DLL で得た結果を現在の検証結果とすること、異条件の性能値を比較すること。
- 失敗テストの除外、baseline の自動更新、実画面未実施を成功へ読み替えること。無関係な大規模整形・改名・新機能追加を同じ変更に混ぜること。

## 作業順序と小さな受け入れ単位

| 依存段階 | 変更の単位 | 完了条件・主要な確認 |
|---|---|---|
| 0 | **現行基準を確定**。HEAD・dirty・SDK・既存 DB/成果物、QA state/history/handoff、テスト XML、Worker/生成器指紋を対応付ける。参照資料の実装済み・未実装・歴史的失敗を現行化する | 同じ条件で変更前後を比較できる。`Test/Test.csproj` の固定 `bin` パス Analyzer など、古い成果物を拾う経路を特定し、修正時に成果物同一性を試験する |
| 1 | **既存契約と衝突する経路を個別修復**。UI 実行 DB の自動削除を明示拒否へ変え、現在の誤結果・終了不能・資源漏れだけを再現して最小修復する。Compact/Rewrite は意味と保存状態を保つ予算付きの処理へ段階的に置き換える。ScopedQuery の一覧外呼び出しを特定し、利用側を修正してから診断を警告→エラーへ上げる | 修正前の再現と修正後の同条件確認。スキーマ不一致時に DB・WAL・SHM が変更されない。Compact の内容、最新値、順序、再 open、予約予算と peak を確認する。scope 付き列が通常 LINQ へ黙って落ちない |
| 2 | **公開 API と所有者を設計**。Store→エンジン別 Create→操作→Save/Dispose の代表利用例、通常/詳細設定、失敗時の例外を確定する。現行の型名に依存する生成器・利用側を列挙する | 二つのエンジンと DB/クエリの代表的な利用例を外部 consumer でコンパイル・実行できる設計になる。`AppendOnlyList` の共通 Store 接続が既存の Save/Load 契約に収まるか先に確認する |
| 3 | **ストレージと DB を移行**。共通 Store を使う入口に揃え、Tsavorite の件数・accessor・容量・checkpoint と Persisted のファイル/予算の所有を明確にする。Tsavorite の同時操作を Release で検出する。`DatabaseContainer` の物理ストレージ作成・解放を内部に分ける | DB→LINQ→テスト基盤→Benchmark/Dynamic の利用側順に、各区切りでビルドと関連テストが通る。DB は WAL、取引、故障状態、Save/再 open、Dispose 後の終了を検証する。同時操作は拒否し、別スレッドでの順次操作は成功する。検出費用は同条件の代表測定で判定する |
| 4 | **LINQ と FLinq の責務を実際に移す**。対象発見・解析・最適化・Fragment 構築・描画・診断の状態を入力単位に閉じる。既存 Coordinator から巨大 partial への委譲を一つずつ解消する | 各移動の前後で生成コード、診断、意味、列挙子解放、incremental cache が一致する。単なるラッパー追加を完了としない。実際の性能候補は別変更として測定して採否を決める |
| 5 | **Benchmark/Verification/UI の判定を一本化**。旧固定ゲートの呼び出し元、manifest、Headless 経路、関連テストと手順を移行する。paired 測定と同じ入力・経路で時間計測外の正しさ確認を設計し、結果一致の証拠を保存する。CPU/アフィニティ/LoopCount を検査する。Verification に時間と割り当て量の回帰判定を実装・試験してから旧実装を削除する | Verification と CLI/UI が同じ条件・結果から同じ合否になる。結果や割り当て標本が欠けたケースを Passed にしない。旧 run 249 の固定上限を新基準へ持ち込まない。通常系と Dynamic をそれぞれ clean build・VS F5・実起動 Worker の path/hash で確認する。履歴再読込、取消・再開・replay/shrink、Windows Debug/Release の実画面を確認し、skip を受け入れ済みに数えない |
| 6 | **旧 API の削除と出荷前整合**。全 consumer 移行後に旧 factory/path API と不要な互換層を削除する。現在有効な README/仕様、package、共有 project と軽量 solution を整合させる | 旧 API 参照がゼロ、独立した外部 consumer が restore/build/代表操作に成功、選定した全回帰と性能比較が完了する。旧データの明示 backup→新規作成→代表値確認→戻し方を隔離コピーで演習する |

段階1の独立した修復と段階2の設計調査は並行できる。ストレージ、DB、生成器、UI は同じ `bin/obj`、DB、Worker 出力を共有する実行を並列化しない。各区切りで対象ファイル・変更前の証拠・変更後の証拠・既知残件を記録し、次へ進む。

## 受け入れ判定

| 目的 | 判定方法 |
|---|---|
| 使いやすさ | 新規 consumer の代表コードで、作成・設定・操作・保存・破棄が同じ順序で読める。既定は短く、詳細設定は必要時だけ指定できる。誤用・未対応は対象を示して失敗する |
| テストしやすさ | Store、集合、DB、生成器、測定、比較をそれぞれ単独で試験できる。内部型名や移動だけに過敏なテストを増やさず、公開挙動と重要な低層不変条件を確認する |
| 一貫性 | 型、例外、ライフサイクル、比較条件の正本が一つ。意図的に異なる .NET equality / byte equality / Work comparer と Store Save / DB commit は明示的に区別される |
| 性能 | 対象ケースの結果が正しく、ケース版・入力・環境・CPU・構成が一致する測定を各5回以上取る。変更前との時間中央値・割り当て量を両方判定する。時間は現行の既定5%回帰境界を起点とし、割り当て量の境界値と小さい値・ゼロの扱いを実測基準から定めてテストする。FLinq と比較対象の差は優先順位付きの改善候補表にする。大規模経路では予約予算、peak private bytes、temp 使用量も見る。境界超過やノイズは成功扱いにしない |
| 見通しと保守性 | 状態の所有者、呼出し方向、削除した重複経路を差分レビューで示せる。公開 API 数や単なる委譲層を無理由に増やさない。実装・テスト・現行文書が一致する |

各段階の範囲外にある既知失敗は、現在再現したか、影響、後続作業を記録して扱う。狭いテストの成功を全体受け入れとしない。SQL ルーチン実行、演算子の網羅拡大、MacCatalyst 実画面の受け入れは別計画とする。

## 実装前に再確認する事項

各作業の実行直前に最新 HEAD、関連する dirty 差分、同じ入力・成果物でのテストと性能基準を取り直す。Release の同時使用検出で代表ケースの既定5%回帰境界を超えたら、検出方法を見直して再測定する。DB が同じ操作用オブジェクトを別スレッドから順次使う経路は、性能とは独立した必須の正しさ条件とする。
