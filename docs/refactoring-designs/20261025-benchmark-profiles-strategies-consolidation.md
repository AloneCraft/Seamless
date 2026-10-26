# プロファイル・測定履歴・ストラテジー統一とプロジェクト整理

## 背景

2026-10-25、利用者が本設計の実装・検証・コミットを承認した。Dynamicをクエリ検証の中心とし、独立したScalableを大規模測定に使う。両者の測定から、配布可能な最適化済みプロファイルを作る。

## 現状の観察

- 起点は master / 7f9669cd。開始時の既存変更は `G:\GitHub\Seamless\Work\Consolidation\baseline.patch` と baseline-status.txt に保存した。生成器、Dynamicの評価回数検査、文書などの未コミット変更は本作業へ混ぜない。
- DynamicのDBは版3で、通常実行は保存するがライブ検証は保存しない。Scalable.UIは旧画面を共有し、Headlessも旧Workerを起動する。
- 現行Tsavoriteには最小64バイトの索引・4096バイトのページがあるが、新ストアの既定では予想件数と予算から大きさを選ぶ。
- 索引拡張には並列区間の制約があり、ページなどの変更にはストア再構成を要する。

## 過去設計から引き継ぐ仕様

- 20261022-dynamic-verifier-consolidation: 理想コード・回帰例を移してから旧検証器を消す。推測した理想時間は作らない。
- 20261024-2208-flinq-demand-analysis: 進行中のFLinq改修が参照するテストは改修の区切りまで維持する。
- DBはバックアップ付きの明示的操作で移行し、起動時に旧DBを自動破棄しない。
- 本設計は20260928のVerification中心の判定と旧測定画面維持の方針を更新する。比較を共通測定基盤へ移し、Verificationは役割移行後に削除する。

## 目的

1. MinimumとOptimized、初期設定と拡張アルゴリズム、資源上限を分離する。
2. 役割の名前空間と静的ストラテジーを、差し替え可能なアルゴリズムの共通規則とする。
3. DynamicとScalableの結果・比較・設定変更を共有DBに記録する。
4. 測定・別入力での確認を経た設定を、条件別のOptimizedとして出力する。
5. 理想コードを保持して重複するプロジェクトを整理する。

## このPCでのストレージテストの保存先（2026-10-25 利用者指定）

### 保存先を含む候補選択（2026-10-26）

最終検証で、D/Fを分けて測った候補が同じCPU/OS条件として選択されることを再現した。ストレージを使う測定・比較・プロファイルには、実際の保存先ボリュームの識別子 `StorageKey` を追加する。キーはボリュームルート・ファイルシステム・ラベル・総容量から求め、同じ媒体の実行ごとの一時フォルダ名は含めない。ドライブをまたいだ性能比較は条件不一致とし、候補選択も一致する保存先に限定する。メモリは専用の `Memory` キーとする。識別できない旧ディスク記録は原本を保持し、性能判定・プロファイル採用へ自動使用しない。通常アプリのStoreと測定前の説明も同じ規則を使う。ボリューム交換や構成変更でキーが変わった場合は再測定が必要であり、媒体種別だけから設定を流用しない。

- 次回の実行から、SSDのテストデータは `D:\SeamlessTestData`、HDDのテストデータは `F:\SeamlessTestData` 配下の実行ごとの専用ディレクトリへ置く。DはCSSD-M2L1KSFT6KEのNVMe SSD、FはWDC WD60EZAZ-00SF3B0のSATA HDDであることを実機で確認した。
- Cドライブには新しいストレージテストデータを作らない。実行前に対象プロセスの `TEMP` / `TMP`、ストアの明示保存先、外部ソートの一時領域、作業ディレクトリ基準のテスト保存先を確認し、対象のDまたはF配下へそろえる。OS全体の環境変数は変更しない。
- `Invoke-StorageTest.ps1` と従来の `Work/Consolidation/Invoke-Checked.ps1` で `-Storage SSD`（既定、D）または `-Storage HDD`（F）を指定する。ランナーとテスト保存先の対応は [実行手順](20261025-storage-test-runner.md) を参照する。実際のファイル配置と媒体を確認してから測定へ進む。
- 比較時には使用ドライブ・媒体・実データと一時領域の保存先を結果へ記録する。以前Cで測った結果をD/Fでの実測結果として流用しない。
- 測定履歴の正本 `G:\GitHub\Seamless\Work\Benchmark\Benchmark.db` と既存バックアップ・証拠ログは保全する。今回の指定で履歴DBを移動しない。
- Cの後片付けは、今回のテストが作った使われていない一時データだけを対象とする。所有者不明の古いデータ、使用中のデータ、測定履歴は削除しない。
- 利用者がPCを使用する間は、画面操作と新しい重いテスト・性能測定を開始しない。

## 非目的

- 旧公開API名・旧コレクション保存形式の互換維持。既存原本の削除はしない。
- 利用アプリから開発用SQLite DBへの依存。
- 未測定の値を最適化済みと表示すること。
- 使用中の旧Worker・Headless・Audit対象・個別最適化比較の一括削除。

## 提案設計

### ライブラリ

- `Seamless.Execution.Profile`: Minimum / Optimized。既定はMinimum。明示値 > 読込済み測定プロファイル > 同梱値 > Minimum。
- `Seamless.Execution.Growth.IStrategy<TSelf>`: structとstatic abstract。Fixed / Doubling / Estimatedを同梱し、利用者定義も同じ契約で扱う。状態はストアごとに独立する。
- ストラテジーはContextからDecisionを返す。エンジンが資源予約・サイズ条件・並列条件を確認して適用する。Fixedは索引・常駐枠を固定し、ファイルの通常の成長や件数自体は固定しない。
- Optimizedが条件や上限に適合しなければMinimumへ戻し、採用設定と理由を公開する。不正な明示値はエラー。
- `ProfileRequest.StorageKey` は `ProfileEnvironment.Storage(directory)` と同じ規則で指定する。Storeは実際の保存先から自動設定する。ディスクの保存先を識別できなければMinimumへ戻し、理由を公開する。`StorageKey` のない旧候補をボリューム不問の候補として解釈しない。
- 証拠版はライブラリのソース・依存パッケージ・コンパイル条件から求め、同梱JSONやGitメタデータから分離する。標準JSONはDLLとともに配置し、DBを使わず読む。[証拠版と同梱の詳細設計](20261026-profile-evidence-bundling.md) を参照する。
- シリアライザーは `Seamless.Collections.Serializers` に Auto<T> / Blittable<T> / MemoryPack<T> / RawByteArray、方式タグはBlittable / MemoryPackへ統一する。
- 比較・走査は実装とBCL境界を棚卸しし、役割別名前空間と型確定した実行部へ移す。DB接続や資源所有などのサービスは責務に合う構造を維持する。

### 共通測定基盤

- 非配布 `Benchmark.Infrastructure` に記録・比較・プロファイル選定・資源監視を置く。
- 共有DBは `G:\GitHub\Seamless\Work\Benchmark\Benchmark.db`。実行・ケース・区間・生測定・設定変更・固定基準・比較・出力プロファイルを記録する。
- 条件には入力、型、件数、シード、クエリ、保存方式、最適化、指定/実設定、拡張方式の版/設定、コード/実行環境を含める。
- 固定基準/任意2回/設定変更実験を区別する。有効測定は既定7回、中央値とばらつきを表示、悪化の目安は5%。未完了・条件不一致・正しさ不一致は性能と区別する。
- 既存Dynamic履歴を明示移行し、復元不能な条件はUnknownとして保持する。旧履歴を自動学習に使わない。

### アプリ

- Dynamicの通常・ライブを同じ保存経路へ接続し、履歴比較とプロファイル出力を設ける。手書き理想がある固定ケースを実測する。
- Scalable.UIを独立した画面へ更新する。Scalable.Coreと独立Headlessが操作/LINQ/容量探索を実行する。入力は分割生成する。
- 自動調整は測定アプリ内で行う。1要因ずつ候補を変更し、操作区間の境界で再構成する。旧データを残し、新構成の件数と内容が一致したときだけ切り替える。
- 評価には停止・移行時間を含める。固定設定との対照と別入力による確認を行い、採用は明示操作とする。
- 同梱/書出しプロファイルには適用範囲・根拠の実行ID・設定値・ストラテジー版を含める。

## 影響範囲・API移行対応表

| 旧形 | 新形 |
|---|---|
| AutoSerializer<T> | Serializers.Auto<T> |
| BlittableSerializer<T> | Serializers.Blittable<T> |
| MemoryPackSerializer<T> | Serializers.MemoryPack<T> |
| RawByteArraySerializer | Serializers.RawByteArray |
| BlittableStrategy / MemoryPackSerializerStrategy | Serializers.Blittable / Serializers.MemoryPack |
| AutoGrowIndexだけの判断 | Growthの型付きストラテジーとエンジン側適用 |
| 予想件数から既定初期値を拡大 | Minimumで最小開始、拡張は別判断 |
| VerificationComparison | 共通測定の比較サービス |
| UIごとの履歴 | 共有Benchmark DB |

## 移行手順

- [x] 0: 開始状態・基準検証・設計/API表の保存。
- [x] 1: ライブラリAPI・命名・Growth・プロファイルと対象テスト。
- [x] 2: 共通DBと移行・比較・Dynamicの記録/履歴/理想コード。
- [x] 3: Scalable独立化・画面・自動調整・Optimized候補の実測。
- [x] 4: 固有テストと理想/回帰の移植後、Verification/旧UI/ParameterSweep.UI/旧検証器/Runnerを整理。
- [ ] 5: ソリューション・CI・文書・起動設定整合、全体検証、明示パスでコミット。
- [ ] 6: 完了後に既存チャット「1022Argon ベンチマークプロジェクト統合」へ確定コミット/API表/DB移行/テスト結果を渡し、影響部分だけ再実行依頼。

## テスト計画

Minimum/Fixed/拡張/独自戦略/状態分離/予算/保存再開、プロファイル優先順位と理由、DB並行書込と移行、比較判定、ライブ保存、理想コード同等性、再構成失敗とデータ保持を自動テストする。本体・Collections・Execution・Dynamic・Audit・TPC-E・サンプルを回帰確認し、Dynamic/ScalableはDebug/Releaseビルドと実画面確認を分けて記録する。

## リスクと対策

- 重い処理は直列。物理メモリ85%で新規負荷/拡張を抑止、90%に近づく前に自分の測定を中止。中止は合格にしない。
- 初期の未コミット変更を上書き・一括ステージしない。競合する既存変更は切り分け、段階ごとの検証と明示パスのコミットを行う。
- DBバックアップはSQLiteの整合したスナップショットとし、移行元を残す。通常起動から移行しない。
- 理想/回帰の未移植例が残る間は、元の実装を削除しない。
- pushしない。後続チャットを実装完了前に再開させない。

## 未決事項

製品方針の未決事項なし。実装で判明するバックエンドの変更可否は、未対応理由を記録し安全な区切りでの再構成に集約する。検証済みでない条件を最適化済みとして採用しない。

## 合意状態

2026-10-25 利用者承認済み。実装中。変更・テスト・DB操作・コミットの実績は下記へ追記する。

## 実装と検証の記録

- 開始時の差分を保存。CIMアクセス不可のためメモリ監視にはWindows APIを使用する。

### 2026-10-25 第1段階以後の観測

- `ac1da82b`: Minimum/Growth/Serializer APIの移行。Execution 33件成功。
- 共通DB版4を追加。SQLite Backup APIによる旧Dynamic版3のバックアップと明示移行を実施。原本は `Work/UI/Benchmark.Dynamic.db`、バックアップは `Work/UI/Benchmark.Dynamic.db.backup-20261025T0619065815980Z`。共有先は `Work/Benchmark/Benchmark.db`。移行テストの整合性・外部キー・原本保全・同時書込も成功。
- Dynamic/ScalableのDebugビルド成功。Scalableの独立Coreは、小規模のメモリ/ファイル/再構成/外部ソート等6件成功。
- 旧検証器の25個の手書きループを `Benchmark.Dynamic.Core/FixedCases/chains.json` に式と原文ハッシュ付きで移植。0/1/16件でSystemとの一致を確認し、16件入力のSystem/FLinq/Idealを各7回実測してDB保存。原本の削除は残る回帰ケースの対応確認後とする。
- Fact: Tsavoriteは大きなログ上限でも開始時の常駐ページ本体が8192バイトだった。Nativeのリング上限と論理ログ容量を分離し、最小からのログ拡張を追加。4096件の全件保持、予算停止後の全件保持を含むExecution 34件成功。
- Fact: Persistedの辞書には削除APIがない。Scalableの `Collections.AppendOnly` をTsavoriteの `Collections` と区別する。削除未対応を隠して同じ処理として比較しない。
- Fact: 4096件・Tsavorite/Temporary・FilterAggregate・固定方式で、索引64→4096バイトの1要因だけを変更した選定・別シード検証を各7回測定した。候補採用は両方のMinimum対照比較が5%以上改善した場合だけ許可する。
- Decision: Fixedが固定するのは調整対象の常駐索引・ログ設定。Persistedのオープンアドレッシング索引を含むファイル形式上の増加は、通常のディスク増加として予算内で継続する。件数を固定する意味にはしない。
- 走査契約の同値3定義を `Seamless.Collections.Iteration.IStrategy<TSelf,T>` に統一し、生成器と利用側を同時更新中。
- Codecの静的契約を `Seamless.Execution.Codecs.IStrategy<TSelf,T>`、実行時境界を `IAdapter<T>` とする。レジストリの型選択・反射型のCanonical実装は実行時選択が必要なためアダプター側へ保持する。
- 外部Tsavoriteの `ISessionFunctions` と `IScanIteratorFunctions` は外部が要求するインスタンス契約を維持する。内部のシリアライズと要素処理は型付き静的契約へ接続する。

### 統合前の実装で確認した問題と移行時の検査条件

以下は旧実装のコードと開始時点の `7f9669cd` を確認した事実である。修正を移植したことと、統合全体の検証完了は区別する。

| 問題 | 確認した根拠 | 移行先で守る条件 |
|---|---|---|
| Persisted/Tsavoriteが実ストレージでない | 旧 `動的検証ランナー.テスト入力を生成` はArray/List/Range以外を `配列.Where(static _=>true)` にする | 実ストアで実行し、実際のバックエンド・設定を保存する |
| 存在しない理想コードの時間を作る | 同ランナーは理想がない場合 `idealTicks=(long)(swOpt.ElapsedTicks*0.95)`、割り当て量も推定値にする | 理想がある場合だけ実測する。存在しない時間や割り当て量を作らない |
| 速度による再試行打ち切りが片側に有利 | 同ランナーはFLinqが比較対象の1.05倍以内になった場合に再試行を終える | 結果の良否で測定回数を選ばず、既定7回の有効値と中央値・ばらつきを保存する |
| 理想という名前でも全体が手書き最適実装とは限らない | 旧 `チェーン生成器` のGroupBy_Sum/OrderByDescending/Take/ToArrayは末尾が `items.OrderByDescending(...).Take(12).ToArray()` | 原文は保持する。結果一致だけで最適性を認定しない。手書き参照実装であることを表示する |
| 最適化なしFLinqが時間測定から抜ける | 開始時点の `MeasurementEngine.MeasuredImplementations` はSystem/FLinq/ZLinqのみ | FLinqUnoptimizedも同じ入力・測定方式で記録する |
| 旧ケースの意味を近似して移植している | `CoreCorpusChainBuilder` はSelectの `x*3+1` を `x*3`、By演算を恒等キーにする | 近似IRによる検査と原式の回帰検査を区別する。`LegacyChainCatalog` に原式56件を保持し、組み合わせを検査する |
| Tsavoriteのディスク列挙が生存キーを欠落させる | `7f9669cd`を隔離してIndex64/Page4096/Log8192/固定で実行すると、管理件数4096・個別検索4096に対してforeach248。今回のMinimum+Doublingでも4096件中33件、8192件中32件が欠けた | 生ログを逐次走査し、通常のキー検索で得る最新アドレスと一致する版だけを渡す。更新・削除・複数チャンク・統計件数を回帰検査する。欠落した測定をプロファイルの根拠にしない |

性能の未確定候補: 共有DBの実行 `12cfb00126a241739d7c815e0ed8a4f9`、16件・chain-05(Where/FirstOrDefault)は7測定の中央値がFLinq 37.568ns、FLinqUnoptimized 34.953ns、割り当て112B/操作で同じだった。比率1.075、各経路の変動係数は約0.08/0.09である。単一実行の差から性能不具合や改善不足を確定せず、同条件の独立した再測定を必要とする。原測定は共有DBに保持する。

統合時の失敗: 移植テスト22件中、IEnumerable経由の走査用生成コードを検査する2件が失敗した。元のPersistedプロジェクトでは同じ検査が成功した。移植時にAsXの型付きローカル変数を単純な式本体へ変えたため、生成器が元の具象型まで見通した。型消去の境界を元と同じ形に戻し、旧機能の不具合とは分類しない。型消去を検査するテストでは境界のソース形状も保持する。

- 型消去の移植修正後、統合先の固有テスト22件がすべて成功した。最適化なしFLinqを測定対象に追加したため、通常実行の測定件数・実装名を検査する期待値も4系列へ追従する。
- `030afeb0`: 静的Codec/Iteration/Equality契約と最小常駐ログの拡張。Execution 36件成功。測定済みプロファイルの適用範囲は同梱JSONの条件に限定する。
- ライブ保存、通常のリポジトリ、詳細/共有履歴の原子的なチェックポイントを検査する9件が成功した。共通履歴の書込を意図的に失敗させ、詳細/共有の両方がロールバックすることも確認した。
- 元Dynamic DBの実データはRun 0件・測定0件だった。原本の版3を保持し、共有先の版4で整合性を再確認した。通常UIとVerificationの既存DBも `Work/Benchmark/Legacy` にSQLite Backup APIで保全済みである。
- 長時間の組み合わせ性能検査を含む途中の2実行は、機能回帰と原式の検査を分けるため中止した。これらは合格実績に含めない。

### 統合先の対応と現在の採用条件

- 旧25個の参照実装: `FixedCaseCatalog`、原文は `FixedCases/chains.json`。0/1/16件で結果照合、各7回の実測を保存した。
- 旧56演算子の呼出式・ペア/3段の生成: `LegacyChainCatalog`。225個の保存済み原式とペア網羅を0/1/16件でSystem/最適化なし/ありと照合し、すべて成功した。近似IRの追加検査とは役割を分ける。
- 旧DynamicLinqカタログ: `Test/System/Linq/Intercept/EnumerableComparisonCatalog.cs`。旧CSVレポートのプロセス起動は削除し、カタログ比較を本体へ残す。固有FLinqテストと合わせて90件成功した。
- 旧5プロジェクトの固有テスト: `Test/FLinqBackends`。参照型のMemoryPack定義は `Seamless.TestKit.ClassInt32` へ置く。共通ベンチの例外なし実行を重複させず、割り当て・境界・保存後・参照型・型消去の固有検査を保持する。
- Comparison/容量の判定規則: `Benchmark.Infrastructure.Comparison` とそのテスト。正常完了Runだけを基準固定・性能判定・プロファイル採用の対象とする。中断結果の保存と、合格判定は区別する。
- 通常/ライブの詳細・共有履歴は同一トランザクション。実ストアの拡張・拒否理由・時刻・所要時間を共通履歴へ投影する。初期設定と、測定時点の実際の容量を別に記録する。
- ストリーミング実行・容量探索: `Benchmark.Scalable.Core`。Scoped LINQ、再構成失敗時の全件保持、Buffered/Unbufferedの両バックエンドを含む16件成功。新旧索引の同時常駐も予算に含めるExecution 37件が成功した。
- `50409aad`: 上限内のEstimatedと拡張所要時間・実容量の記録。予想件数をMinimumの初期サイズへ流用しない。
- 同梱候補は `tsavorite-4096-doubling-ssd-fbc51054-20261026` と `tsavorite-4096-doubling-hdd-fbc51054-20261026`。索引4096B/ログ8192B/Doubling、Native/FilterAggregate、Tsavorite/Temporary、Int32のキーと値、4096件、測定した環境・Release構成・保存先ボリューム・バックエンド既定I/Oに限定する。
- 既定Minimum＋Doublingを対照とし、seed42/43を各7回測定した。時間中央値の比はDで0.27967/0.26590、Fで0.23714/0.23514。両入力でnoise-v1と5%以上の改善条件を通過した。生測定・比較・根拠RunIDを共有DBへ保存し、候補データだけでなく採用理由も取得できる。未測定の条件へ外挿しない。
- 旧同梱候補と原測定は保持するが、証拠版や保存先を確認できない記録は現行候補へ自動採用しない。Fixed最小を対照にするだけでは、通常の既定より良いという証拠にしない。
- 削除した追跡ソースのコピーは `Work/Consolidation/RetiredSources`。削除対象にあった開始時の未コミットREADMEもここへ保全し、他作業の生成器・ドキュメント差分は混ぜない。
- 整理後の全体Debugビルドは85個のcsproj・16個のshprojで成功した。起動設定と通常CIを更新し、直接Compile Includeした共有ソースの所有プロジェクトも生成器で追跡する。
