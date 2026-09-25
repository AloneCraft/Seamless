# 優先順位と Phase 計画

現行機能の根拠は [機能一覧](01_Functions.md)、共通契約は [不変条件](02_Contracts.md)、受け入れ ID は [検証計画](04_Acceptance.md)、未完了 ID は [バックログ](05_GapsAndDecisions.md) に定義する。

## 優先順位の決め方

最初にデータ損失・誤結果・終了不能・実行不能を防ぎ、その次に検証結果を信用できる状態を作る。未実装機能は利用価値と依存関係で選択し、最適化は正しさと測定の基準が揃ってから行う。過去の遅い計測値の大きさだけで優先度を決めない。

| 優先度 | 対象 | 理由 |
|---|---|---|
| P0 | 現行基準と証跡の整合、再現したデータ破損・誤結果・資源解放漏れ | 以降の作業の安全性と判断根拠になる |
| P1 | Compact の無制限メモリ、Dynamic の不具合由来除外、終了・復旧、最新 Worker・比較・UI 受け入れ | 既存機能を信用して利用できる状態を作る |
| P2 | SQL routine の対応 subset、AppendOnlyList 共通 Store 接続、利用側 API 移行 | 明確な機能不足と統合不足を解消する |
| P3 | 根拠ある FLinq 性能改善、残る renderer 責務分離、対応範囲の拡張 | 正しい基準の下で効果と保守性を評価できる |
| 出荷ゲート | 配布・文書・全体回帰・rollback 演習 | 優先度が低いのではなく、依存する変更の後に必要なゲート |

Q-01 はユーザー回答「1」により確定した。既存機能の正しさ・データ保全・検証の再現性を優先し、Phase 1～3 の対象ゲートを満たした後に P2/P3 の未実装機能・高速化へ進む。未実装機能の具体的な対応範囲や破壊的変更は、残る Q-02～Q-05 の必要な判断を経て確定する。緊急性は現行再現で更新し、現在正常な過去バグを修正対象として扱わない。

## WBS と依存関係

> **番号付与ルール**: 依存レベルは作業順序を表す。小さい番号は上流、同じ番号は論理的に並列実行可能、大きい番号は依存先の完了後に進む。Phase 名は識別子であり、階層ドット番号ではない。

| 依存レベル | Phase / Epic | 主な Feature・成果物 | 依存先 | 対応バックログ |
|---|---|---|---|---|
| 1 | Phase 1: 現行基準の確立 | 機能・能力・既知問題・テスト基準表、ソース/成果物指紋 | なし | GAP-01、GAP-11 |
| 2 | Phase 2A: 永続化・資源・DB の正しさ | 個別再現、最小修復、資源境界・復旧の回帰 | Phase 1 | GAP-02、GAP-03 |
| 2 | Phase 2B: LINQ / FLinq の正しさ | 除外理由の再現、生成/実行の修復、Capability 更新 | Phase 1 | GAP-04 |
| 3 | Phase 3: 検証と画面の再現性 | Worker 一致、比較/再現の証跡、実画面受け入れ | Phase 2A、2B | GAP-05、GAP-06 |
| 4 | Phase 4A: Database の未実装を補う | 承認された SQL subset の実行・拒否・互換表 | Phase 3、Q-03 の決定 | GAP-07、GAP-08 |
| 4 | Phase 4B: FLinq の性能・保守性 | 採用候補の実測、最小責務分離、生成差分 | Phase 3、Q-01 に沿う対象選択 | GAP-10 |
| 4 | Phase 4C: 共通 Store への利用側移行 | AppendOnlyList 接続、利用側移行表、互換性判断 | Phase 3、破壊的変更時のみ Q-02 | GAP-09 |
| 5 | Phase 5: 出荷可能範囲の受け入れ | 全体回帰、pack/消費、現行文書、戻し方、残件表 | 対象に選んだ全 Phase 4、Q-04/Q-05 の範囲確定 | GAP-11、残件の明示 |

同じレベルでも同じ `bin/obj` を使ったビルドやテストは並列に実行しない。独立した checkout・出力先・データ領域で衝突を避ける。Phase 4 の調査は先行してよいが、修正の採否は Phase 3 の測定/正しさゲートを通す。

## Phase 1: 現行基準を確定する

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-01～F-19 の状態を更新し、現在の全プロジェクト、テスト、公開 API、能力除外、既存 DB/成果物を棚卸しする。HEAD・dirty・環境を固定し、QA 記録を現状と照合する |
| 不変条件 | INV-13、14、17、18。過去 PASS と現在 PASS、未実装と意図した対象外を分ける |
| 禁止事項 | NG-06、08、10、12。QA の「全て未コミット」等を現行事実としてコピーしない。失敗数を減らすために除外を増やさない |
| 前提条件 | リポジトリと既存 dirty の所有、実 SDK、実行中プロセス、対象データの保全範囲を記録できる |
| 変更範囲 / スコープ | 証跡・QA state/handoff の整合、現行説明の明白な誤り、隔離実行計画。ソース修正は後続 Phase に渡す |
| 非対象 / Out of Scope | API 削除、保存形式変更、性能最適化、SQL 機能拡張、既存 DB の初期化 |
| 完了条件 / 成功条件 | すべての機能群に状態と根拠、現在の失敗/未実行/人間待ちが付く。主要テスト群を現在の成果物で実行し、完了・失敗・停止を区別した基準表を作る。環境で実行できない範囲は明示して後続の gate に残す |
| 検証項目 / 受け入れ条件 | AC-BASELINE、AC-PROJECT。テスト結果 XML の内部件数/状態と実行終了コードを照合。過去の限定成功を全回帰としない |
| 移行条件 | 現行基準表を以後の変更前証跡にする。旧 QA 記録は履歴として保存し、現行状態と区別する |
| ロールバック条件 | 誤った checkout・不整合な成果物・dirty 取り違えがあれば計測を無効化し、最後に一致した基準から再取得。文書訂正のみなら今回変更した文書差分だけ戻す |
| 既知の問題 / 許容事項 | 過去の BUG-0005/0006/0007/0008/0009 は再現待ちで可。ただし状態不明を解決済みにしない。実行不能が下流の証明を妨げるときは当該下流を開始しない |

## Phase 2A: 永続化・資源・DB の正しさを固める

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-01～F-04、F-06、F-09/F-10/F-12。Compact の全件メモリ化を予算内の処理へ置換する。過去の session 漏れ・DB 終了停止・checkpoint 競合を再現し、現在も失敗するものだけ根因修復する |
| 不変条件 | INV-02～INV-10。最新値、件数、明示 Save、WAL commit、checkpoint、所有権、共有予算を維持する |
| 禁止事項 | NG-01～NG-05、08、11。Dispose 省略・timeout 延長・GC 強制だけで終了問題を隠さない |
| 前提条件 | Phase 1 の基準、隔離 DB/Store、再現に必要な trace とタイムアウト上限。DB 終了と FLinq の関連は Phase 2B と同一再現を共有する |
| 変更範囲 / スコープ | Execution、Collections.Persisted/Tsavorite、Database の該当箇所、問題を通る LINQ の寿命処理、関連テストと現行契約文書。Compact のアルゴリズムと予算計上 |
| 非対象 / Out of Scope | SQL routine 新規実行、全クラス再編、旧公開 API の一括削除、保存形式の無断変更 |
| 完了条件 / 成功条件 | 各修復に変更前失敗/変更後成功または同等の再現証跡がある。Compact は出力同値に加え設定予算内で動く。対象 DB テストは本体成功に加え process 終了まで確認する |
| 検証項目 / 受け入れ条件 | AC-RESOURCE、AC-LIFETIME、AC-CODEC、AC-COLLECTION、AC-DB、AC-RECOVERY、AC-IMPORT。単一巨大値、重複、取消し、例外、早期列挙終了、再 open、並列 accessor、異常終了 |
| 移行条件 | データ形式を維持できる修復から小単位で置換する。形式変更が必要と判明したら別の移行設計と fixture を追加し、それまでは既存形式を保持する |
| ロールバック条件 | データ不一致・commit 境界違反・予算迂回・新しい停止を検出したら書込みを止め、失敗証跡を保存する。対象コードと対応した隔離データを Phase 前へ戻す。ユーザーデータに実験しない |
| 既知の問題 / 許容事項 | Count 再走査 O(n)、複数 accessor 生存中の索引拡張保留、Persisted の Memory Store 拒否は現行制限として許容。OS 電源断保証は別の実験がない限り未検証 |

## Phase 2B: LINQ / FLinq の正しさを固める

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-05～F-08/F-17。SelectManyXor、Range ElementAt、UnionByIdentity 等の不具合由来 Capability 除外を現在の generator で再現する。実際の呼出し/生成/実行経路を修復し、解消した除外だけ更新する |
| 不変条件 | INV-01、02、04、11、12。結果、評価回数、順序、例外、Dispose、診断、生成単位、cache 分離を保つ |
| 禁止事項 | 除外の一括解除、テストの期待値を誤結果に合わせる、System.Linq 黙示転送、provider の instance Contains を interceptor 生成済みと扱う、NG-01/03/09/11 |
| 前提条件 | 現行ソース指紋と generator DLL の一致、再現 Seed/IR、基準意味論。既存 dirty SelectMany.cs を他の修正で上書きしない |
| 変更範囲 / スコープ | 該当 Fragment/registry/discovery/renderer、Seamless.Linq、Dynamic Capability と対象 fixture/回帰テスト。ZLinq 比較経路の問題は生成 Source・fixture・ZLinq 実装を切り分け、FLinq 製品側の修復と別 task にする |
| 非対象 / Out of Scope | 速度向上、全 IR 化、全 LINQ overload 対応、意図された unordered 制限の解除 |
| 完了条件 / 成功条件 | 選定した失敗経路が生成コンパイルと実行の両方で成功し、基準との結果/例外/資源寿命が一致する。残す除外には型・演算・経路・理由・後続 ID がある |
| 検証項目 / 受け入れ条件 | AC-LINQ、AC-GENERATOR、AC-DYNAMIC、AC-LIFETIME。Fragment/Registry/Render/IncrementalCache、DynamicRouteShape、実行意味テストを組み合わせる |
| 移行条件 | 1 不具合 1 修復で生成物を強制再生成する。対象経路の検証後に Capability と現在仕様を更新し、次の修復へ進む |
| ロールバック条件 | 誤結果、例外消失、評価回数増減、解放漏れ、新たな生成失敗なら修復差分を撤回。直前の Capability と generator を組で戻し、再現入力は保持する |
| 既知の問題 / 許容事項 | 明示 Excluded、unordered 位置依存制限、provider 直呼び経路は必要な区別として維持。匿名型の部分最適化等の既存診断は仕様どおりなら許容 |

## Phase 3: Worker・比較・UI の再現性を確立する

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-15～F-18。通常/Scalable/Headless の最新 Worker 受け入れ、Dynamic の Build 後 Worker 構築経路の clean restore/F5 実験、条件付き比較、replay/resume/shrink、実画面の状態遷移を確認・必要時修復する |
| 不変条件 | INV-13～INV-18。測定実体、条件、結果、画面表示、保存後履歴を結びつける |
| 禁止事項 | stale Worker の数値利用、異条件比較、skip の PASS 扱い、baseline 自動更新、既存 DB の init 再実行による置換、NG-04/06/07 |
| 前提条件 | Phase 2 の対象正しさゲート、固定された比較入力、Windows desktop、UI テスト環境、既存 DB backup、VS を実際に操作できること |
| 変更範囲 / スコープ | Worker の build/reference/copy、対応 solution 生成、Verification、Benchmark/Dynamic の状態/保存/画面配線、UITest と操作文書 |
| 非対象 / Out of Scope | UI 全面刷新、旧履歴の自動統合、全 platform 対応追加、FLinq の性能最適化 |
| 完了条件 / 成功条件 | Debug/Release の対象 app matrix で実際の exe/DLL と起動先が一致し、対象画面・保存・再現・中断の受け入れが完了する。VS F5 を確認できなければ該当セルは NEEDS_HUMAN のまま |
| 検証項目 / 受け入れ条件 | AC-WORKER、AC-COMPARE、AC-VERIFY、AC-DYNAMIC、AC-UI。Worker ファイル SHA-256、最低有効測定数、異条件拒否、改変 replay 拒否、途中取消し、画面/DB 結果一致 |
| 移行条件 | Dynamic の経路変更が必要なら build graph/restore/copy を同時に整え、最新成果物を受け入れてから採用。新しい比較は Verification 正本に寄せ、旧 UI ゲートは明示互換の範囲だけ保持 |
| ロールバック条件 | 古い binary 実行、欠落 payload、履歴不整合、UI/CLI 合否相違なら結果を採用せず、直前の build 構成・binary・DB backup を一組で戻す。新規 run を失敗証跡として退避する |
| 既知の問題 / 許容事項 | BUG-0009 は修正実装あり/VS 未受け入れという区分を保持。Dynamic の stale Worker は再現前には仮説。旧 UI の履歴互換を暫定維持してよい |

## Phase 4A: Database の未実装を対応範囲ごとに補う

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-11～F-14。まず routine ごとの解析可/生成可/実行可を可視化し、Q-03 で選定した subset に実行経路を追加する。scalar/読取 table function/更新 procedure は依存する機能ごとに小 task へ分ける |
| 不変条件 | INV-01～INV-03、08～10。SQL 型、NULL、比較・順序、エラー、更新 transaction を明示する。view snapshot と自動更新を区別する |
| 禁止事項 | メタデータ保存だけを制約執行と称する、未対応 SQL の成功扱い、全互換の無根拠な保証、既存宛先の先行削除 |
| 前提条件 | Phase 3、Q-03 の subset 決定、SQL Server 基準 fixture、型/NULL/照合順序/分離レベルの対応表、現在の generator と import の正常基準 |
| 変更範囲 / スコープ | Rdbms 関係代数/生成器、SqlServer analyzer/type mapping、Database の必要実行 API、DB Tool 診断、対応テスト/仕様。制約と view 自動維持は選定された分だけ |
| 非対象 / Out of Scope | 選定外の trigger、任意 dynamic SQL、全組込み関数、SQL Server の全面代替、分散 transaction |
| 完了条件 / 成功条件 | 対象 subset の生成コードがコンパイルできるだけでなく実行でき、SQL Server 基準と値・行・NULL・副作用が一致する。未対応部分は正確に診断される。実行 stub を残す API は未実装として表に残す |
| 検証項目 / 受け入れ条件 | AC-SQL、AC-IMPORT、AC-DB、AC-RECOVERY。routine 単位の成功/拒否、制約違反、取消し、再 open、view freshness の契約 |
| 移行条件 | 新しいコード/保存先を staging で検証し、旧版を保持して小さい subset から採用する。実行互換性が変わる場合は schema/manifest と binary の対応を更新する |
| ロールバック条件 | SQL 基準との結果不一致、制約の誤執行、データ破損、公開失敗なら旧宛先 backup と対応する旧 binary/schema へ戻す。新規書込みがある場合は先に退避し、無断で切替後データを捨てない |
| 既知の問題 / 許容事項 | 選定外 routine は明示例外で残してよいが「Database 全機能完了」としない。現行 import の標準検証は件数中心であり、値比較はこの Phase の追加受け入れ |

## Phase 4B: FLinq の性能と保守性を改善する

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-07/F-08。現在の比較可能な結果から候補を選び、到達 Fragment・生成形状・CPU/割当の根拠を確認して一原因ずつ改善する。MethodBodyRewriter 等の残る責務分離は別 task として行う |
| 不変条件 | INV-01、02、04、11～14。正しさ、生成単位、診断、cache、固定 baseline を保持する |
| 禁止事項 | 古い 297 行候補表を現行順位にする、却下済み候補の無根拠再導入、未到達コードの高速化、意味修復と速度改善を同一の採否だけで扱う |
| 前提条件 | Phase 3、現在 baseline、実行中 binary の指紋、固定測定環境、事前に定めた対象 metric と改善判定。性能 task と構造 task を分ける |
| 変更範囲 / スコープ | 対象 Fragment/ProviderNative/render 契約、必要な意味/形状/cache テスト、計測証跡。性能測定に影響する harness 変更は別 baseline にする |
| 非対象 / Out of Scope | 全 Fragment の一括 IR 化、SQL 拡張、API 破壊、データを小さくして数字を改善する変更 |
| 完了条件 / 成功条件 | 最適化は現在の意味テストに合格し、到達する生成コードと有効測定で事前の目標を満たす。責務分離は外部挙動を維持して責務が実際に移る。単なる wrapper 追加を完了としない |
| 検証項目 / 受け入れ条件 | AC-LINQ、AC-GENERATOR、AC-COMPARE。速度/割当/資源使用を別 metric で記録し、周辺型・ソース・終端の回帰を確認 |
| 移行条件 | 1 候補ごとに生成・実行・採否を完結する。移行 adapter は一時的に保持してよいが、解消 task と対象責務を明記する |
| ロールバック条件 | 意味不一致は直ちに撤回。性能未達/再現しない/回帰がある候補は採用せず、前後生成物・生測定・不採用理由を保持して候補差分のみ戻す |
| 既知の問題 / 許容事項 | 明示対象外 overload、未選定の遅いケース、完全 IR 化未完了は残してよい。現行 Verification の既定 5% は回帰判定であり、最適化の必達改善率ではない |

## Phase 4C: 共通 Store へ利用側を段階移行する

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | F-01/F-03/F-04。AppendOnlyList の共通 Store 寿命/保存/復元接続を設計・追加し、旧 factory/path API 利用側を現在数え直して小群ずつ移行する |
| 不変条件 | INV-03～INV-08、16、17。共通 Store の利用モデル、所有者、保存境界、メモリ/ディスク予算を維持 |
| 禁止事項 | 旧 API 一括削除、保存形式の無断変換、Memory Store 拒否を勝手にディスク利用へ変える、古い移行件数を正確な現在数とする |
| 前提条件 | Phase 3、現在の public API/利用箇所/保存 fixture 一覧。AppendOnlyList の共通 Store 下での契約を既存 Store に合わせて定義する。削除や非互換には Q-02 の決定が必要 |
| 変更範囲 / スコープ | StoreExtensions、AppendOnlyList adapter、対象利用側、テスト/README、必要な project 登録。旧 API を内部で保持して利用側だけ移す段階を許容 |
| 非対象 / Out of Scope | 他 engine への置換、全クラス階層再設計、承認のない public rename/削除、既存データ全 migration |
| 完了条件 / 成功条件 | 対象利用側の作成→使用→Save→Dispose→reopen が成立し、資源所有が一意になる。旧 API の残存箇所と理由がゼロまたは明示された互換層だけになる。廃止未合意なら削除完了と報告しない |
| 検証項目 / 受け入れ条件 | AC-API、AC-COLLECTION、AC-LIFETIME、AC-RECOVERY、AC-PROJECT、AC-PACKAGE。旧/新 API の対応 fixture と複数 collection を検証 |
| 移行条件 | adapter → 一利用側 → 一ライブラリ群 → 全選定利用側の順で切替える。public 廃止は互換方針と release 範囲を満たした最後の独立 task |
| ロールバック条件 | 保存・件数・所有・パッケージ消費の互換性が崩れたら当該利用側だけ旧経路へ戻す。新保存形式が必要になった場合は binary とデータを組で戻す |
| 既知の問題 / 許容事項 | 互換用の旧 API 残存、固定長 List の reflection 構築、未合意の Memory Store 制限変更は残してよい。NativeAOT 対応をこの移行に暗黙追加しない |

## Phase 5: 出荷範囲を受け入れる

| 項目 | 実行契約 |
|---|---|
| 行うべき機能・作業 | 選定済み F-ID 全体を統合検証し、NuGet の pack と独立 consumer、CLI/UI、現行文書、移行/復旧手順、残件一覧を完成する |
| 不変条件 | INV-01～INV-18。runtime と tool の依存境界、共有 source 可視性、現在仕様と実装/テストの一致 |
| 禁止事項 | 限定 PASS の全機能化、未実施 UI の合格、旧 package 名のまま配布手順を承認、未合意の公開/廃止/履歴削除 |
| 前提条件 | 対象に選んだ Phase 4 が終了。選ばなかったものの理由と未対応能力が公開範囲に反映済み。Q-04/Q-05 が必要範囲で確定 |
| 変更範囲 / スコープ | packaging、CI の必要 gate、solution 登録、操作/互換文書、release 候補の証跡。新規機能は各前段へ戻す |
| 非対象 / Out of Scope | 明示指示なしの NuGet publish、Git push、旧データ消去、追加 platform の新設 |
| 完了条件 / 成功条件 | 対象の全回帰が現在 binary で終了し、実画面/配布 consumer/復旧演習が合格。残る NEEDS_HUMAN と対象契約を壊す既知失敗がない。対応外は説明が一致する |
| 検証項目 / 受け入れ条件 | AC-PACKAGE、AC-PROJECT、AC-ROLLBACK と全関連 AC。CI 定義の存在に加え実際の run 証跡を保存する |
| 移行条件 | 受け入れ済みの組合せを release 候補として固定し、コード版・package 版・schema/Codec・DB backup・manifest・成果物 hash を紐付ける |
| ロールバック条件 | consumer の生成/実行失敗、依存漏れ、UI 回帰、保存互換破壊なら候補を取り下げ、直前に受け入れ済みの一式へ戻す。公開済み package の上書きで回避しない |
| 既知の問題 / 許容事項 | 選定外 SQL subset、未選定最適化、未対応 platform を明示して残せる。データ損失、誤結果、偽 PASS、対象 UI の未受け入れは許容しない |

## Phase 開始・終了時の記録

各 task に担当、変更対象、F-ID/INV-ID/AC-ID、前提となるソース指紋、再現方法、完了条件、実測結果、未完了、戻し先を記録する。所要時間は再現と影響範囲の把握後に見積もる。証拠のない工数や期日を確約しない。
