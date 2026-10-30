# 3チャット統合の受入状態

開始版は `1846c8b70ed6411a4b273bbf5cf73316038e8bc8`。統合先は `C:/Users/kazuh/.codex/worktrees/seamless-argon-unified/Seamless`、ブランチは `codex/seamless-argon-unified-20261026`。元checkout・別worktree・失敗データは保全し、pushはしない。

本書の成功は、記載したソース群と実行だけを対象とする。後続変更の成功へ流用しない。通常docsには現在の契約と検証範囲を記載し、実行ログ・失敗前後・台帳は `G:/GitHub/Seamless/Work/Orchestration` に保存する。

## 共通CLI基盤

- `bgo-resume-final-01` は同一ソースのDebug/Release各22基本試験と9外部寿命試験、計62件に成功した。`source-hashes-before.json` と `source-hashes.json` は一致した。実行DLL等のハッシュは `binary-hashes.json` に保存した。
- `independent-final-v2-comparison.json` は独立実2チャットの10タスクを記録する。共有Xと復旧Xは非重複、独立資源は5.031924秒並行。待機中・実行中取消4件、専用broker再起動前後の同一child PID/creation timeと所有権を確認した。
- `app-control-ipc-01` は実BGOと実C#メールボックス・アプリ操作ホストの接続に成功した。サービスだけをfakeとし、正常完了、検証Failed、同一IDの重複開始抑制、取消後の停止確認を検査した。実Dynamic/Scalable GUIや実測Workerの受入ではない。
- `bgo-source-deployment.json` は元共通基盤checkoutへ15ソースファイルだけを反映した証跡。既存stageのハッシュは不変。`bgo-binary-deployment.json` は共有処理終了後の既知broker再起動と配置済み5ファイルの一致を記録する。
- 配置済み `BackgroundOps.dll` SHA-256: `cfb9b427c12e9f166e81cb98f97814d513394d699cace02dc299ed15f0a08b81`。

既存A/cliを維持し、B/file-mailboxは明示opt-in、登録mailbox、live instance、操作ID、grant、永続claimと耐久終端receiptを必要とする。処理停止を確認できなければQuarantinedで所有権を保持する。C/描画、D/隔離GUI、全ツールへの強制的な資源検査は未実装。

機械共通の `G:/AI/Skills/instructions/CodexBackgroundOperations.md` は、保存した具体的差分への利用者の明示承認後に更新した。変更前全文、承認時点の原文ハッシュ、反映後ハッシュを `shared-policy-approval-status.json` と `codex-background-operations-before.md` に保存した。自動承認審査の拒否を迂回してはいない。

## Dynamic/Scalable内部制御

`ui-resume-import-20261026.json` の23ファイルを統合した。`ui-integrated-debug-01` と `ui-integrated-release-01` で、それぞれInfrastructureの内部操作・終了調停15件、Dynamic集計2件、Scalable設定/service3件、計20件が成功した。両UIのDebug/Releaseビルドは警告・エラー0。

現在公開する能力はrunのみ。通常のアプリ終了は元Task、Worker停止、保存、終端receiptまで延期する。取消の受付を停止完了と扱わない。未対応pause/resumeは公開しない。内部API試験、実GUI描画、実OS入力は別の受入であり、後二者は未実施。既存Scalable XAML障害も未解明として保持する。

## Argon/FLinq

`argon-chain19-integrated-01` は強制再生成後の固定参照3試験と既存需要・遅延評価回帰40件、計43件に成功した。境界試験は25参照、グループ合計等の追加7ケース、並べ替え/Reverse需要の追加6ケースを含む。評価記録は31ケース×入力長0/1/16の93件。

chain-19の入力16件で、Systemのlambda評価は `[16,1,1]`、修正前FLinqは `[8,16,1]`、修正後FLinqは `[16,1,1]`。Order/Reverseを越えるWhere移動は、短絡・部分消費・不明consumerでは適用しない。既存の安全性条件も維持する。

`argon-normal-integrated-01` は通常PRと同じ `Category!=ChainCorpusFull&Category!=Survey` で362件成功、失敗・skip 0。これは検証契約共通化前のWorker版であり、後続の共通helper版の受入とは区別する。生成DLLは同結果の `hashes.json` に対応する。

69件のsource/generatedハッシュ、25参照のOriginalSHA256/OriginalIdealBody不変、138 traceのFixedPointは `argon-current-evidence-audit.{md,json}` に保存した。コンパイル時の傍受証拠と実行時Fallbackゼロは区別する。

元225件は代表25+回帰200のケース記録である。後続の `legacy-original-template-audit.json` では、保存メソッド列と保全された旧カタログ56演算子の引数付きCallを照合し、225式を引数変更なしに再構成した。カタログと旧理想生成器の実ファイルは各OriginalSHA256と一致した。これは225個の独立した元式ファイルの保存を意味しない。

既存の `LegacyChainCatalog` は、この再構成225件と旧ペアを直接コンパイル・実行する。従来IRへ変換可能だった23+121件の検査とは別である。実行時Fallbackと呼出位置への傍受、全source/generatedハッシュの証跡を追加した版は `argon-interceptor-coverage-01` で別途受入する。型/Sourceの既存対応も拡張せず再利用する。

`DynamicQueryValidator` による通常Workerと固定ケースの値・例外・Fallback共通化は、`argon-validation-integrated-01` の限定15件とWorker関連8件で成功した。同型例外は測定せず、取消は伝播し、通常値の実行時FallbackはExpansionIncompleteとする。性能・割り当ての最終比較、独立seed、noise-v1判定、夜間全コーパス/全評価調査は未完了。CI設定の静的10項目照合は成功したが、実GitHub Actions実行はしていない。

`argon-invariant-runtime-02` では、強制generator Rebuild後、固定参照5件とvalidator12件の17件、および既存の需要・SelectMany・InvariantHoisting等の周辺回帰136件が全成功した。評価93記録の両FLinq実装に過剰評価はなく、25参照×入力長0/1/16の75 runtime検証でFallbackは0。97件のsource/generated/meta組のハッシュは全一致した。

続く `argon-normal-integrated-02` は通常PRと同じfilterで376件成功、失敗・skip 0。実行時間は約15分46秒で、前段階の362件から追加された検証も含む。generator DLLは `e566dc1edc58a47b8b07e03834b2ac2a049e9fda5aaa3a7a1361b1cb7a39a711`、Worker DLLは `2de33914576027a45dca9af1c09945c89767d86a974f84bcec0338a84d4f7334`。後続の呼出位置の傍受gate候補はこの受入に含めない。

追加で確認した原因は、空outerのSelectManyでもselectorをconstructorへ前倒ししていた経路と、除算等の例外を発生させる部分式を一般の不変式hoistが前倒ししていた経路である。不変式は副作用・例外・可変captureを除外し、元のunchecked文脈を維持する。評価用式と元projectionを束縛する式を別に保持し、GroupByの既存回帰も通した。

## Tsavorite/TPC-C

元データを変更せず、新規Cデータの `recovery-r0-baseline-01` と `recovery-r0-noinline-01` でtxn80/p0-customer再OpenのNREを再現した。noinlineで `LogRecord.get_KeyBytes:1033` まで特定した。初発NREと後続cleanupの共有違反は分離する。

新しいsnapshot page72末尾704Bと、初回page8の同じ位置の704Bが両実験で一致した。64-page ringの同じslot8を使う。`recovery-r1-observe-01` では、正常な再Open直後の未使用slotに旧page8の4096B全体が残り、その後のappendで旧payloadをrecordとして読むことを確認した。

固定上流 `b0255a31de95b1e903ee187feb59ceb04819a639` から比較用のsigned Coreを2つ作り、両者ともbuild警告・エラー0、元assembly version/tokenと一致した。baseline/patchedの差はRecovery.csの6行だけである。`recovery-r1-observe-source-baseline-01` は元txn80 NREを再現し、`recovery-r1-observe-source-patched-01` は80取引、4変種とP1比較、shadow、保存・再Open・正常終了に成功した。patchedのopen3以降は次slotの旧payloadが0、resident外の余分なallocated slotも0である。

修正は、復旧走査でheadより前の各pageの処理・必要なflushを終えてから、既存の所有権処理でそのpageを解放する。後からring slotを一括解放してresident aliasを壊す方法は採らない。この比較実験の成功だけでは、長期運転・別checkpoint形式・失敗時cleanup・正式依存の受入完了とはしない。

旧DLL固定の初期走査probeは10万行の6条件と、行数だけ100万へ増やしたraw/Double/4096条件が成功した。元のP4初期走査CountdownEvent underflowは未再現・未解決であり、再OpenのNREと同じ原因とは扱わない。

TPC-C childの4変種・C10実装は保全したまま。元症状の修正、Exp9の最終版48構成、区画並列SUT/WAL/区画別保存/運転側/監査A・Bは未完了。

## 再実行と担当

実行は共通CLIの `run --spec` と専用の `*.spec.json` / `*.commands.json` を用いる。workspace S、全ProjectReferenceを含む出力X、共通cpu:heavy Xを宣言する。80%未満で開始し88%で所有処理を止め、取得不能では開始しない。D/FとTPC-C専用Cの媒体条件を維持する。データと出力は毎回新規pathにし、旧証拠を上書きしない。

要求した設定だけでなく、実セッションのturn_contextから確認したmodel/effortを `agent-models.json` に保存する。統括はGPT-6.1 Sol/Ultra。定型照合はLuna/low、局所harnessはLuna/medium、意味修正はSol/high、復旧・所有権はSol/xhighを使用した。未実行、取消、Unavailable、判定不能を成功へ数えない。

## 後続の対象版

- `argon-validation-integrated-01` は共通validator版の限定15件とWorker関連8件が成功。テストに追加されたxUnit2031警告はAssert.Singleの述語overloadへ修正し、次版で再確認する。
- 旧93記録を未最適化側も含めて再計算し、chain-15/25の空入力でlambda#0がSystem0/FLinqUnoptimized1と確認した。これはSelectManyのinner selectorのconstructor前倒しが原因。修正と非計測例外回帰を取り込み、runtime Fallback検査の追加Factとともに次版を検証する。
- `recovery-r1-observe-01` は既存DLLのまま元NREを再現。txn60再Open直後のnext page72/slot8に旧page8末尾と同じhashがあり、resident範囲外slot9..18もallocatedだった。後続取引・保存前の復旧済みメモリに旧payloadが残ることを観測した。修正前証拠であり復旧合格ではない。


## Tsavorite Exp9 および Core43 正式受入（2026-10-28 Antigravity /goal）

- **Exp9 Core43 (`E94E9A36...`) マトリクス 48/48 (100%) 受入完了**:
  - 全48ケース（大規模 `million` 24ケース、小規模 `small` 24ケース、`p1`/`p4` 各24ケース、`sync`/`group0`/`group1`/`group4` 各12ケース、`r0`/`r1`/`r2` 各16ケース）がすべて `PassedScopedWorkload`、`expectedLsn == recoveredLsn`、`expectedCompleted == completed`、独立全行整合パスで合格・受入完了。
  - 初期スキャンで不整合が検出されていた履歴タプル `perf-million-p4-r0-group1` も Core43 修復パッチにより 166.96秒 で完全に正常回復・受入された。
  - 最新インデックス: `G:/GitHub/Seamless/Work/Orchestration/exp9-current-execution-index-latest.json` (`passedScoped: 48, remaining: 0`)。
- **Core43 正式パッケージング (Leaf 82) およびコンシューマープローブ (Leaf 83) 受入完了**:
  - `core43-formal-staging-82` (BGO task `bf718869faa849e3a68c69b9ac646be3`, 29.35s, exitCode 0): 全259 Core43 ソースファイル完全一致、10 native devices SHA256 一致、マネージドアセンブリが Core/Common/AzureStorageDevice に厳密一致、`Seamless.Tsavorite.Core.0.0.0-coreprobe.3.nupkg`（SHA256: `8E215FA33C8A1650EF83652D50FBFA71553230628DBBD91EBE4301A0BA39B6A5`、Core DLL SHA256: `F81882FB1A7A8DF93E16E40EB310E80A59AC443AB9C459C87E011824CB363F07`）を生成・受入。
  - `core-package-probes-v3-83` (BGO task `f565ac73ccd149368c37f6b2e945f719`, 38.13s, exitCode 0): 12段階プローブ全パス受入。
- **Core43 の `worktrees/tpcc/Seamless.Tsavorite.Core` への同期完了**:
  - バックアップ `Seamless.Tsavorite.Core.bak-before-sync` 作成。
  - 全270ファイル同期（`core43-recovery-and-scan.patch` 含む）、クリーンビルド検証（0 warnings, 0 errors）完了。
  - SourceLink の `EmbedUntrackedSources` による DLL サイズ差異の技術的原因（git 管理下 vs 外）を完全解明。

## Argon 65版 全検証 Leaves 実行・採取結果（2026-10-28 Antigravity /goal）

凍結入力1,649件（closure SHA256: `F36CF023093E4D0CF669EC71D4429D9AD1B3BDE081EED68A212DD69EC2D54D3A`）に対する全検証リーフの実行および完全エビデンス採取結果：
- **Leaf 72 (`argon-current65-normal-72`)**: Release net10.0-windows 通常回帰 **381 / 381 Passed** (100% 合格、919.75s)。
- **Leaf 80 (`argon-current65-dynamic-debug-80`)**: Debug net10.0-windows 通常回帰 **381 / 381 Passed** (100% 合格、1007.03s)。
- **Leaf 74 (`argon-current65-corpusfull-74`)**: Release 全コーパス **Passed** (全件検証 16分43秒 合格)。
- **Leaf 75 (`argon-current65-survey-75`)**: Release Survey **Passed** (examined=2222, skipped=0, excess=0, unloadFailures=0、8分24秒 合格)。
- **Leaf 76 (`argon-current65-performance-d-76`)**: Dドライブ実Worker性能検証 **Passed** (全4回 Worker runs Passed, System比 中央値 0.218 / 約4.6倍高速化)。
- **Leaf 77 (`argon-current65-performance-f-77`)**: Fドライブ実Worker性能検証 **Passed** (全4回 Worker runs Passed, System比 中央値 0.175 / 約5.7倍高速化)。
- **Leaf 78 (`argon-current65-test-release-78`)**: Release net10.0 全テスト **2,274 Passed / 19 Failed** (全 2,293 テスト完全エビデンス採取)。
- **Leaf 79 (`argon-current65-test-debug-and-win-79`)**: Debug net10.0 全テスト **2,275 Passed / 19 Failed** (全 2,294 テスト完全エビデンス採取、Release と完全同一の 19 件であることを実証)。
- **Leaf 84 (`argon-current65-ui-and-shared-84`)**: ステージ 0〜12（Execution.Tests Debug/Release, Infra Debug/Release, Scalable Debug/Release, Scalable UI Debug/Release, Dynamic UI Debug/Release win-x64, UITests Debug）**すべて成功**。
- すべての検証実行を通じて、1,649 件の凍結ソースコードは 1 バイトの変更もなく完全保全されたことを確認。
