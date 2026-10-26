# 3チャット統括実装と軽量モデル併用

## 背景

2026-10-26、利用者がこのチャットへArgon/FLinq、統合後残件とCodexBackgroundOperations、Tsavorite実験/TPC-C本体の実装を集約する計画を承認し、実装を指示した。統括チャットは01a17722-8d35-7691-bd2e-c14fc8ee5db9。

## 現状の観察

- Seamless開始HEADは1846c8b70ed6411a4b273bbf5cf73316038e8bc8、branchはcodex/seamless-optimization-baseline-20261025。開始時tracked/untracked差分なし。
- Argon検証worktreeは4252d8b8起点。chain-08のCore/Worker/テスト変更は検証未完了で、保存済み参照の由来と実効参照を区別する。
- TPC-C本体はtpcc-benchmark、Exp9はrelaxed-cannon-f5276b。保全と所有権確認後、関連変更だけを統合する。
- BGOのmailboxは実装済み。古いREADME/Verification/共通規約の未実装表記と現行コードに差がある。同一最終ソースのDebug/Release受入を確定する。
- 統括は実セッションのturn_contextでgpt-6.1-sol/ultraを確認した。担当の実モデル/effortも実セッションと対応づけて記録する。

## 過去設計から引き継ぐ仕様

- 20261022-dynamic-verifier-consolidation: Dynamicを中心に原式・参照・回帰を保持し、意味検証と性能認定を分ける。
- 20261025-benchmark-profiles-strategies-consolidation: 共通測定DB、明示バックアップ、noise-v1、D/F別条件と外部利用の制約。
- TPC-C承認済み計画: A方式、応答前DurableLsn、区画別保存/復旧、HammerDB由来NOPM、TPC-E回帰。今回SQL Server比較は含めない。
- CodexBackgroundOperations既存設計: 呼出元sandbox、所有子孫のみ管理、S/X一括資源予約、終了確認不能時Quarantined。

## 目的

1. 3チャットの差分・証跡・実行主体を保全して統括窓口へ移管する。
2. 共通CLI基盤を現行版で受け入れ、Argon/TPC-Cへ接続する。
3. 25参照とFLinqを監査・比較・改善しCIへ接続する。
4. Tsavorite初期走査例外を原因から解決しA方式をTPC-C本体へ導入する。
5. Dynamic/Scalableの明示有効化された内部制御と完了/取消の寿命契約を実装する。

## 非目的

SQL Server/HammerDBとの比較、Unity/Omni/GPU実adapter、新しいVM/OS/有料製品導入、非intストレージ等の機能拡張、push、所有不明プロセス停止、チャットarchiveは含めない。既存隔離環境で成立しないGUI受入は明示的に未完了として残す。

## 提案設計

### モデルと担当

- 統括は要求GPT-6.1 Sol/Ultra、同時稼働は統括と最大2agent。
- 定型照合/ログ集計はgpt-6-luna/low、確定仕様の局所実装/文書/CIはluna/medium。
- 複数ファイルの確定仕様実装・既存契約接続はgpt-6.1-terra/medium、意味/性能原因の調査はgpt-6.1-sol/high、並行/永続化/寿命契約の判断はgpt-6.1-sol/xhigh。
- 単純照合はスクリプト。曖昧さ/矛盾/想定外失敗はSolへ上げ、判断確定後の反映は軽量担当へ戻す。
- 設定変更は新規担当へ必要な文脈だけを渡す。要求モデル/effortと観測結果、task IDを台帳へ記録する。

### 所有権と資源

元3チャットと共通基盤担当に新規変更停止と保全を連絡し、所有タスク/dirty/成果物を照合する。編集はworktreeを分け、統括のみが統合先の共有ソース/solution/DB/規約を変更する。workspace Sと全出力X、重い測定cpu:heavy Xを宣言する。source編集ツールに未実装leaseがあるとは扱わない。

重い処理はメモリ80%未満で開始、88%で所有処理を安全中止。機械全体メモリを確認できなければ開始しない。Argon媒体比較はD/F、TPC-Cは承認済み専用C領域を維持し、同一比較の媒体を途中変更しない。外部書込権限を迂回しない。

### 参照と計測

chain-08の原式全体を失敗先行で再現し、checked intグループ合計、キー加算、checked long外側集計と評価順を維持する。元JSON/OriginalSHA256と実効参照を分離し、実効参照をfingerprintと診断へ保存する。25参照をSystem/FLinq無最適化/最適化/Idealで比較。Analyzer強制再生成とC#/DLL hashを保存し、評価回数/傍受漏れ/Fallbackを検査してから性能/allocを改善する。

225は保全された代表25件と回帰200件のケース記録数であり、全225件の完全な原式テキストが保存されていることは未確認。従来の144件だけの近似IR実行を225原式の監査完了とは扱わない。25理想参照は別の現存JSONに全式を保持しており、chain-08に加えてchain-20のグループ合計もchecked int、キー加算はunchecked intへ修正する。

生成器のグループ融合は整数/decimal合計のオーバーフローを記録し、投影がその合計を評価した時点で通知する。条件分岐で使わないグループと、キー選択中の先行例外を維持する。複合式中のスカラーは明示引数を持つstaticローカル関数として元の式位置で呼び、条件分岐・短絡・評価順を維持する。捕捉変数は明示引数として渡す。

SelectManyのcollection selectorは、外側要素を取得できた後にその要素につき1回だけ評価する。外側引数を参照しないことだけを理由にconstructorへ前倒ししない。空外側では内側の式を評価せず、例外と副作用の開始順を維持する。固定参照の評価回数は両FLinq実装を対象にし、末尾未評価lambdaを0として全件集約する。

### BGO/アプリ内部制御

既存A/cliを維持し、能力照会、操作ID、対象instance、受付/実行中/取消要求/終端、成果物参照を追加する。登録mailboxを利用し、UI処理はDispatcher経由で既存serviceへ委譲。内部処理/Workerが残る間はCLI終了だけで資源を解放しない。操作は照会/開始/対応するpause-resume/取消/結果取得に限定する。

外部操作はB/file-mailboxの明示opt-inとする。live PID/creation time、instance、operation ID、乱数grantを束縛し、永続claim応答後だけserviceを開始する。終端はWorker停止・保存完了を含む耐久receiptで確認する。claim応答不明でもservice未開始を確定できる操作はRejectedで閉じ、後から開始しない。取消はCancelled/Unavailableとし、検証成功と取消を混同しない。Windowsの通常closeは元Taskとreceipt完了まで延期する。pauseを実装していないserviceは能力として公開しない。

mailboxの要求ファイル読取中の共有違反がbrokerの未処理例外となる経路を独立probeで観測し、排他handleを持つ要求による再現試験を追加した。未dispatchの要求だけを保留し、独立mailboxと既存leaseの調停を維持する。アプリやCLIを代理実行する権限拡大はしない。

### TPC-C

保全原本を変更せずコピーで走査の最小再現を作る。通常列挙/保存走査/実験側寿命を区別しiteratorとcallbackを識別する。原因層だけを修正し、待ち/再試行/検査無効化で隠さない。Exp9復旧/性能マトリクスの後、4変種/runner、SUT、WAL、区画別保存、socket運転側、NOPM、監査A/B、故障注入へ接続する。共通部品変更時はTPC-Eを回帰確認。

## 影響範囲

SeamlessのDynamic/Core/Worker/Tests/CI、Tsavorite/共有storageの原因箇所、TPC-C/Core/Transactions/Server/Tests/CLI、Dynamic/Scalable UI、BGOおよび共通規約。新共有ソースはprojitems/shproj/slnx同時更新、focused solutionは正規スクリプトで生成する。

## 移行手順

移管保全→BGO現行受入→ArgonとTPC-Cの独立調査/実装→共有修正を統括で統合→内部制御→Debug/Release関連回帰→証跡/文書整合→対象限定ローカルcommit。段階状態はWork/Orchestrationへ保存する。

## テスト計画

- BGO同一最終source Debug/Release全受入と独立実2チャットのX非重複/独立並列/取消/回復。
- 25参照の結果/例外/順序/評価、旧不具合、現行13型対応、全評価回数survey、傍受の実行証拠。
- Tsavorite列挙/保存/破棄/再Open、元症状の修正前後。
- TPC-C4変種、rollback、片側保存、復旧中停止、二重適用/欠落を検出する外部参照検査と故障注入。
- 内部制御の重複開始/取消中/アプリ終了/Worker残留/再接続。内部APIと実GUI受入は分離。
- 正しさ固定後にnoise-v1と独立seedで時間/alloc比較。未実行/取消/Unavailable/Inconclusiveは合格にしない。

## リスクと対策

他のworktreeと実行主体の差分を保全してから移管する。DBは書込停止と明示backup後に対象へ直接変更する。重い処理は機械全体で直列化し、他者taskを取消しない。Luna成果も差分と外部挙動で統括が確認する。

## 未決事項

製品仕様の選択は承認済み。実行時にのみ確定する事項はBGO/外部worktree書込権限、現行版の受入、走査例外の原因、既存GUI隔離能力。Scalable XAML一回障害は原因を確認するまで未解明として保持する。

## 合意状態

利用者が2026-10-26に全文計画を提示して実装を明示承認。追加の実装確認を求めず、承認範囲で継続する。実装途中の意味ある設計変更は本書へ更新する。
