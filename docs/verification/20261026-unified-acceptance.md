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

機械共通の `G:/AI/Skills/instructions/CodexBackgroundOperations.md` は変更していない。自動承認審査が永続的な共有規約変更を拒否したため、`shared-policy-proposed.patch` に具体的な差分を保存し、利用者のファイル単位の承認を待つ。共通基盤のローカルdocsは配置済み実装に対応している。

## Dynamic/Scalable内部制御

`ui-resume-import-20261026.json` の23ファイルを統合した。`ui-integrated-debug-01` と `ui-integrated-release-01` で、それぞれInfrastructureの内部操作・終了調停15件、Dynamic集計2件、Scalable設定/service3件、計20件が成功した。両UIのDebug/Releaseビルドは警告・エラー0。

現在公開する能力はrunのみ。通常のアプリ終了は元Task、Worker停止、保存、終端receiptまで延期する。取消の受付を停止完了と扱わない。未対応pause/resumeは公開しない。内部API試験、実GUI描画、実OS入力は別の受入であり、後二者は未実施。既存Scalable XAML障害も未解明として保持する。

## Argon/FLinq

`argon-chain19-integrated-01` は強制再生成後の固定参照3試験と既存需要・遅延評価回帰40件、計43件に成功した。境界試験は25参照、グループ合計等の追加7ケース、並べ替え/Reverse需要の追加6ケースを含む。評価記録は31ケース×入力長0/1/16の93件。

chain-19の入力16件で、Systemのlambda評価は `[16,1,1]`、修正前FLinqは `[8,16,1]`、修正後FLinqは `[16,1,1]`。Order/Reverseを越えるWhere移動は、短絡・部分消費・不明consumerでは適用しない。既存の安全性条件も維持する。

`argon-normal-integrated-01` は通常PRと同じ `Category!=ChainCorpusFull&Category!=Survey` で362件成功、失敗・skip 0。これは検証契約共通化前のWorker版であり、後続の共通helper版の受入とは区別する。生成DLLは同結果の `hashes.json` に対応する。

69件のsource/generatedハッシュ、25参照のOriginalSHA256/OriginalIdealBody不変、138 traceのFixedPointは `argon-current-evidence-audit.{md,json}` に保存した。コンパイル時の傍受証拠と実行時Fallbackゼロは区別する。

元225件は代表25+回帰200のケース記録であり、全件の完全な原式保存・実行を意味しない。従来変換可能だった23+121件だけを225件合格と言い換えない。型/Sourceの既存対応も拡張せず再利用する。

`DynamicQueryValidator` による通常Workerと固定ケースの値・例外・Fallback共通化は実装を取り込んだが、本書作成時点では `argon-validation-integrated-01` が受入待ち。性能・割り当ての最終比較、独立seed、noise-v1判定、夜間全コーパス/全評価調査は未完了。CI設定の静的10項目照合は成功したが、実GitHub Actions実行はしていない。

## Tsavorite/TPC-C

元データを変更せず、新規Cデータの `recovery-r0-baseline-01` と `recovery-r0-noinline-01` でtxn80/p0-customer再OpenのNREを再現した。noinlineで `LogRecord.get_KeyBytes:1033` まで特定した。初発NREと後続cleanupの共有違反は分離する。

新しいsnapshot page72末尾704Bと、初回page8の同じ位置の704Bが両実験で一致した。64-page ringの同じslot8を使う。次の観測は、正常な再Open直後の未使用slotに旧payloadが残っているかを確認する `recovery-r1-observe-01`。この段階では原因修正や復旧合格としない。

旧DLL固定の初期走査probeは10万行の6条件と、行数だけ100万へ増やしたraw/Double/4096条件が成功した。元のP4初期走査CountdownEvent underflowは未再現・未解決であり、再OpenのNREと同じ原因とは扱わない。

TPC-C childの4変種・C10実装は保全したまま。元症状の修正、Exp9の最終版48構成、区画並列SUT/WAL/区画別保存/運転側/監査A・Bは未完了。

## 再実行と担当

実行は共通CLIの `run --spec` と専用の `*.spec.json` / `*.commands.json` を用いる。workspace S、全ProjectReferenceを含む出力X、共通cpu:heavy Xを宣言する。80%未満で開始し88%で所有処理を止め、取得不能では開始しない。D/FとTPC-C専用Cの媒体条件を維持する。データと出力は毎回新規pathにし、旧証拠を上書きしない。

要求した設定だけでなく、実セッションのturn_contextから確認したmodel/effortを `agent-models.json` に保存する。統括はGPT-6.1 Sol/Ultra。定型照合はLuna/low、局所harnessはLuna/medium、意味修正はSol/high、復旧・所有権はSol/xhighを使用した。未実行、取消、Unavailable、判定不能を成功へ数えない。
