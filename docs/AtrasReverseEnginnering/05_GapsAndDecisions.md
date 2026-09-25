# 未実装・既知問題・許容事項・確認事項

調査日 2026-09-25。実装確認と実行検証を分離する。根拠 S-ID は [機能一覧](01_Functions.md)、優先度/依存関係は [Phase 計画](03_Phases.md) を参照。

## 優先バックログ

| ID | 状態・問題 | 実装または検証すべき作業 | 優先 / Phase | 閉じるための条件 |
|---|---|---|---|---|
| GAP-01 | **現行化不足**。QA handoff は9/19の「全て未コミット」等を残す一方、state/history には9/21のcommit記録。S-16 | 現行 Git と比較して問題/移行/受け入れ状態を更新。古いテスト成功を現在成功にしない | P0 / 1 | 現行指紋に結びついた機能/テスト/残件基準表。AC-BASELINE |
| GAP-02 | **現行コードの制約違反**。Persisted Dictionary.Compact が全件を BCL Dictionary に集め、OrderBy→ToArray する。S-04 | 最新値/削除/順序契約を保ち、予算内の走査・外部処理へ置換。現行の peak と失敗境界を先に測る | P1 / 2A | 大規模入力の意味同値、再 open、予算/peak/temp 計測。AC-RESOURCE/COLLECTION |
| GAP-03 | **歴史的失敗、現在再現待ち**。DB 終了停止、FLinq Sum 後 session 漏れ、checkpoint 共有違反等。S-16 | 現行ソース/生成器で再現し、DB transaction、列挙子、scope の寿命を切り分ける。現在再現しないものは過去記録として扱う | 再現したデータ/解放問題 P0、それ以外 P1 / 2A/2B | 対象失敗の前後証跡、周辺寿命回帰、testhost 正常終了。AC-LIFETIME/DB/RECOVERY |
| GAP-04 | **現行 Capability の不具合由来除外**。Dynamic の一部 FLinq route と ZLinq 比較経路。S-13 | 下表の項目を route ごとに再現し修復。意図した unordered 除外とは分ける | P1 / 2B | 生成/実行/基準比較が成功した経路だけ除外解除。AC-DYNAMIC/LINQ/GENERATOR |
| GAP-05 | **受け入れ不足と構造差**。通常UIのWorker依存修正はあるがVS F5未確認。Dynamic UI は Build 後の MSBuild と固定出力コピーを使用。S-11/S-13/S-16 | 現行 clean restore/build、Worker のみ変更後 F5、起動 path/hash を確認。再現した不足だけ build graph 修復 | P1 / 3 | 対象 app/config の最新 payload/実起動一致。AC-WORKER |
| GAP-06 | **UI 受け入れ/手順不足**。UITest README の旧 exe/TFM、環境未指定 skip、Dynamic smoke だけでは業務シナリオの証拠不足。S-11/S-13 | 操作手順を現行化し、実行/取消/保存/履歴/replay/shrink の画面を確認 | P1 / 3 | 対象 Debug/Release matrix の実画面証跡。skipを除く。AC-UI/VERIFY |
| GAP-07 | **未実装確認**。SQL から生成する TableFunction/ScalarFunction/StoredProcedure が例外 delegate の経路を持つ。S-10 | 関係代数からの実行経路を選定 subset で実装。最初に解析/生成/実行の対応表を作る | P2 / 4A | 対象 routine が SQL 基準と一致して実行でき、非対応を診断。AC-SQL |
| GAP-08 | **未実装領域・製品範囲未決**。SQL 制約/trigger はmetadata取得とfingerprintだけでは執行しない。SQL view の自動維持も確認した経路にない。S-09/S-10 | 必要な制約と view freshness を選定する。自動維持/trigger は選んだときだけ実装 task 化 | P2以降、条件付き / 4A | 選定範囲の違反拒否、更新追随/再計算、transaction 原子性。非選定は未対応明示 |
| GAP-09 | **未統合/移行未完了**。旧 factory/path API が残り、AppendOnlyList に共通 Store 拡張がない。S-04/S-05 | AppendOnlyList の Store 接続、現行利用側の移行、互換層整理。旧 API 削除は別判断 | P2 / 4C | 作成・所有・Save/reopen・consumer 互換。AC-API/RECOVERY/PACKAGE |
| GAP-10 | **性能改善候補/責務分離残件**。Render 契約はあるが Coordinator が既存 partial writer を呼び、MethodBodyRewriter に多数の責務が残る。S-07/S-17 | 性能は最新測定から候補選択。構造は通常文/TryGetNonEnumeratedCount/UnsafeAccessor 等を独立 task で分離。完全 IR 化は必要性を確認 | P3 / 4B | 意味・診断・cache の同値、実際の責務移動、性能候補の有効測定。AC-GENERATOR/COMPARE |
| GAP-11 | **文書/配布の整合不足**。README/docs の `Seamless.FLinq` と実 csproj の `Seamless.FLinq.Generator`、旧統合仕様の旧 TFM/旧path 等。S-02/S-07/S-15 | 正本 package/project 名を現行 artifact で確定し関連文書を訂正。全 project/共有source登録と独立 consumer を検証 | P0の棚卸し、出荷 gate / 1/5 | 対応対象のリンク/TFM/package/依存が一致し生成・実行成功。AC-PROJECT/PACKAGE |

この表は `NotSupportedException` や TODO の検索結果を機械的に数えたものではない。読み取り専用 collection の変更拒否、Enumerator.Reset、処理専用 callback、Codec の意図した拒否は、文脈なしに未実装件数へ加えない。

## Dynamic の除外を分けて扱う

現行 [CapabilityProfile.cs](../Benchmark/Benchmark.Dynamic.Core/CapabilityProfile.cs) 135–170 と [DynamicRouteShapeTests.cs](../Benchmark.Dynamic.Tests/DynamicRouteShapeTests.cs) を根拠とする。以下は除外と理由が現在のコードにあるという Fact であり、記載された不具合を今回再現した意味ではない。

| 経路 | 現行コードが示す理由 | 計画上の扱い |
|---|---|---|
| FLinq SelectManyXor | generated enumerator struct の捕捉に関する CS1673 | 現行再現→最小修復→意味/形状試験→対象除外解除 |
| FLinq Range ElementAt | index local に関する CS0103 | 現行生成物を確認して修復 |
| FLinqNoOptimizations UnionByIdentity | 不完全な和集合 | 誤結果の再現を優先し、重複・空・双方の差集合を含める |
| provider Contains | instance method に bind され FLinq interceptor が生成されない | provider 直呼び機能の失敗と断定しない。最適化 route としての測定可否を整理 |
| ZLinq OrderBy/ThenBy 比較経路 | 決定的 fixture で comparer 不整合 | ZLinq 実装・生成 Source・fixture を切り分け、FLinq 製品バグと混同しない |
| unordered provider の位置依存操作 | 順序契約を満たさない | 意図した制限として保持。対応拡張は明示 opt-in の仕様に沿う |

## 既知問題の鮮度

| 記録 | 現在の扱い | 次の確認 |
|---|---|---|
| BUG-0005: DB テスト134件後の終了停止、BUG-0007: Sum 後の read session 漏れ | 9/19 QA の歴史的観測。現在の dirty を含むソースでは再現未実施 | 生成器/テスト DLL 指紋を揃え、該当操作・writer・Dispose・process 終了を追う |
| BUG-0006: checkpoint async 共有違反、BUG-0008 等の LINQ 失敗 | 歴史的観測。無条件に現行障害へ転記しない | state の対象と現行テストを照合して限定再現 |
| BUG-0009: VS F5 と Worker 更新 | build/reference 修正と過去の端末検証記録は存在。VS 操作の受け入れ待ち | 現行 VS で Worker のみ変更→F5→起動先/hash 確認 |
| Verification.db の既存 run | 調査時の read-only 確認では6 run、最新は2026-09-12の Scalable quick Passed。現行全体の証拠ではない | Phase 1/3 で現行ソース指紋の run を作る |
| TestResults 直下の既存 TRX | 調査したものは8/30の少数テスト。ファイル名が Fail でも中身が passed のものがある | XML の実数と現行実行終了を根拠にする |
| 過去の FLinq 性能 backlog | 採用/却下/保留と異なる入力・Worker が混在する歴史記録 | 現行到達コードと比較可能な測定で再評価。古い順位や比率を使わない |

## 今回直さなくてもよい制限・差異

以下はこの計画の暫定的な許容範囲である。既存制限を維持するものであり、新たな不具合を許可するものではない。

| 制限・差異 | 許容理由 / 解除条件 |
|---|---|
| Persisted は Memory Store で作成不可 | ファイルが必要な現在の契約。利用を要求するなら別の寿命/予算設計が必要 |
| Tsavorite 辞書上書き等で Count の再計算 O(n) | 正確さを優先した現在の制約。高速化は正確性と並列性の証明後 |
| 複数 accessor の生存中は index growth を保留 | 現在の並列操作制約。expectedCount 等の既存手段を維持。制約解除を無検証で行わない |
| accessor の thread affinity は契約であり、DEBUG 警告と全構成の強制拒否は異なる | 現行実装は利用側のスレッドごとの作成を要求する。Release でも自動防止すると宣言しない |
| scope 付き byte equality と通常 .NET equality の差 | 別 Semantics の意図した差。+0/-0 等を無理に一致させない |
| FLinq Registry の明示 Excluded と unordered 制限 | 未対応境界/安全境界の仕様。全対応を完了条件にしない |
| SQL routine の選定外 subset、trigger、view 自動維持 | Q-03 で必要範囲を決めるまで拡張しない。ただし未対応を明示する |
| import の分離レベル | 通常 Snapshot、memory-optimized table が存在する場合 ReadCommitted を選ぶ。全関係で同一時点 snapshot を無条件保証しない |
| 旧 API / 旧 UI / 旧履歴の互換経路 | 既存利用を維持するため段階移行中は許容。残存一覧と廃止判断を残す |
| 完全な Fragment IR 化、固定長 List の reflection 構築 | 現在の機能に必須の未実装とは限らない。保守性/測定/配布目標から必要性を判断 |
| OS 停止・電源断・媒体故障耐性 | 今回未検証。アプリ再 open や子プロセス停止試験を電源断保証へ拡大しない |

許容できないものは、対象機能のデータ損失、誤結果、未対応の黙示成功、資源予算の回避、別binaryの結果混入、skip/未実施のPASS化、既存ユーザー変更の破壊である。

## 製品判断と一問ずつの確認

コードで回答できる事項は上記の調査で処理する。以下は製品の到達点/互換性の選択である。**Q-01 は確定済み。ユーザーへは同時に複数質問せず、Q-02 以降の必要な項目だけ順に確認する。** 未回答の項目を承認済みと扱わず、その選択に依存しない調査・文書化は進める。

| ID | 確認事項 | 現在の状態 / 回答までの扱い |
|---|---|---|
| Q-01 | 最優先の到達点 | **確定済み：ユーザー回答「1」**。既存機能の正しさ・データ保全・検証の再現性を固め、その後に未実装機能と高速化を進める。旧 API 廃止や SQL 対応範囲等の決定は含まない |
| Q-02 | 今回は旧 API を残して内部利用側を移すか、今回の移行完了後に削除するか、新旧両方を継続して正式サポートするか | **質問済み・回答待ち**。DB、テスト、Benchmark の旧 API 利用を現行コードで確認済み。外部利用者の互換要件はコードから確定できない。回答前は削除・非互換変更をしない |
| Q-03 | SQL はどの routine/制約/view freshness を必要とするか | 未質問。最小の解析/生成/実行対応表を用意し、read-only subset から始める案と更新/制約まで含む案の差を説明して確認する |
| Q-04 | 旧 Benchmark UI/旧ゲートの互換維持を続けるか、Verification への導線移行と廃止を計画に含めるか | 未質問。現在は互換維持が正本の説明であるため保持する |
| Q-05 | 今回の出荷受け入れは現行 Windows 構成に限定するか、条件付き MacCatalyst 対応も含めるか | 未質問。第一案は現行 Windows。全 UI の全 platform を暗黙に保証しない |

Q-02 の現行利用根拠は、`Seamless.Database/DatabaseContainer.cs` のテーブル作成、`Test/Seamless/Stores/HashStore.cs` の旧 factory テスト、`Shared/Common/TestInfrastructure/PersistedBase.cs` のファイル Set 作成、`Benchmark/Benchmark.Tsavorite.ParameterSweep/ParameterSweepRunner.cs` の容量探索、`Benchmark/Benchmark.Dynamic.Worker/SourceFixtureFactory.cs` の入力生成である。旧 API の残存は未使用の宣言だけではない。互換性の選択はこれらの移行順序と外部 consumer の受け入れ範囲へ反映する。
Q-02～Q-05 は回答が必要になった Phase の前までに確認する。ユーザーが既に今回の会話で選んだ事項は再質問せず、この表と Phase の該当行へ反映する。

## 文書作成としての完了と実装計画の承認

文書作成の完了条件は、要求された11種類の情報が全 Phase にあり、機能・未実装・優先順位・検証・移行・戻し先が追跡でき、現行根拠と提案が区別され、リンクと変更範囲を確認できることである。

これは全 Phase の実装完了でも、未回答の製品判断の確定でもない。回答が得られたら当該選択だけを更新し、引き続き未解決の判断を一問ずつ確認する。
