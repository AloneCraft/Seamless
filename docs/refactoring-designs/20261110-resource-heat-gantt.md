# 資源使用ヒートガント

## 合意と入力

2026年11月10日の利用者添付指示を、既存のサンキー・実行DAG・横時間軸Ganttの並行拡張として扱う。添付は第7章の「サンプリング値」で終わる。読み取れた要件を本書へ引き継ぐ。

## 共通時間軸と表示

実クエリ区間、Worker・Thread・Taskなどの記録、動的ハードウェアレーンを同じ単調時計へ対応付ける。時計原点が異なる記録には明示的な対応情報を要求する。縦スクロールと同期した横時間軸を持ち、既存Ganttの選択・検索・最大化基盤を再利用する。

レーンはCPU Package/Core/Logical Processor、実NUMA、物理ストレージ、NIC RX/TX、取得可能なPCIe等の構成情報から作る。容量・帯域、Busy・帯域飽和度・IOPS飽和度を区別し、百分率には分母と出典を表示する。

## 計測契約

ホスト全体、クエリ寄与、ノード寄与を区別し、直接計測・相関算出・推定・不明を保存する。共有資源の重複計上を防ぎ、帰属できない量はその他・不明に残す。0、計測不能、欠落、データなしを区別する。数msの実行ではサンプル分解能を明示し、イベント・累積カウンタ差分を併用する。

既存のObjectExecutionStatisticsはクエリ全体とゲート待機等を持つ。ノード、Worker、CPUコア、NUMA帯域、デバイスごとの寄与は収集境界の調査と接続が必要である。既存BenchmarkCpuTopologyProviderはWindowsトポロジー情報の再利用候補として調査する。

## 表示方式

連続・段階・モノクロ・色覚対応パレット、凡例、数値、帯域等の折れ線を選択する。ノード選択と時間範囲で対応レーンを強調する。再生速度は表示状態として扱い、記録取得と分離する。

## 実装順序と受入

1. 既存の時計・記録・トポロジー・OSカウンタを照合し、利用可能性と指標の意味を確定する。
2. 有界な資源サンプルと動的レーン、共通時計のデータ契約を追加する。
3. Windows/Linuxの取得可能なホスト指標を接続し、負荷を記録する。
4. クエリとノードへの帰属は実際のイベント・実行ID・Thread等の対応に基づいて追加する。
5. ヒートガントと保存・読込を既存可視化へ接続し、実クエリと実カウンタで照合する。

各段階で対応OS、権限、カウンタ分解能、収集負荷、欠落と未取得を報告する。実ハードウェアとOSで取得した値を受入証拠とし、模式データは表示単体試験として区別する。

## 現在の実装と検証

固定UI候補r2b（source1558ファイル、manifest `AA5D260B80DBF8F8954B611B6D76BA18BC016026A757C337D498F027730CEAC0`）はWindows Releaseビルドが警告・エラー0件、近接128件が成功した。同じソースのUbuntu Release publishと試験DLLビルドも警告・エラー0件で成功した。同じUbuntu候補の資源収集・ヒートガント・再生・形式2交換27件も成功し、稼働中サーバーとChromeの保護照合が前後一致した。実ブラウザーによる今回UIのUbuntu受入は後続へ残る。

Windowsの隔離ブラウザーでItemListを実行し、同じ実行IDの軽量実測と102レーンのOS観測を表示した。Host/ProcessとQuery/Nodeの帰属を区別し、クエリに帰属できる値がない場合は未収集と表示する。Processの42サンプル、配色・数値・折線、形式2のJSON/SVG/HTML保存、同実行のオフライン表示をDOMで照合した。証拠は `F:/Seamless/manager-object-ui-20261110/r2b-dom-heat-r1/heat.json` と画像・出力ファイルへ保存した。記録上限と応答1MiB上限でサンプルの省略を観測し、2200件の既知の省略と理由を表示した。継続する未観測区間を0へ置き換えない。

この確認で資源初期化がDBゲート待機へ含まれる境界問題を検出した。次の作業版は実Wait直前を記録し、互換nullable `GateWaitStartedOffset` を統計・履歴へ追加して正しい位置へ描画する。旧MemoryPack履歴と形式1交換の既定値を維持する試験を追加した。まだ次候補の合格へ含めない。

## 計測と保存の契約

- Normal/Profiled二経路と有界QueryProfilerを追加した。通常経路の行ループへ新しいProfilerの分岐や時計読み取りを追加せず、入口で一度選択する。最初の対応は同期Sum消費区間で、元private/helper内部・Worker・ノード別CPUは未収集と明示する。
- ノード実測は対象・版・成果物・実行と、実MethodKey・SourceHash・GeneratedBodyHash・callsite・ノードIDを照合する。入力は消費側へ届いた値、出力はscalar戻り値であり、元ソースの走査数や論理portへ配分しない。
- 資源履歴は専用の独立期間Storeへ2KiBチャンクで保存し、全体SHA・完全性を検査する。キュー4件/8MiB、256期間/64MiB、本文1MiBを初期上限とする。BestEffortの非待機保存を実装し、保存状態と欠損・破損・探索上限を返す。完全保存の待機モードはまだ提供していない。
- 形式2はノード数量と固定コードの投影診断も保持し、コード除外時に識別子を対応付けてマスクする。計測用の最終C#本文は通常本文と分けて記録・検査・表示する。

固定r3g（source1575ファイル、manifest `A7214B9406D6E002C963C9F3E073C691F5A66AA2856A5AFEB1AB2AD738A888AE`）はWindows Releaseビルドが警告・エラー0件、対象別UIと共通機能の129件、新しい計測・投影・資源履歴48件が成功した。生成名整理で実際に適用した対応を計測処理へ渡し、Spanと列挙子、Current失敗、利用者の同名引数・delegateを区別する。Normal本文の生成は維持し、Profiledの入力は値取得後に計数する。Ubuntu同ソースのRelease publish・試験DLLビルドも警告・エラー0件で成功し、共通177件がすべて成功した。両環境のソース・成果物と既存プロセスを前後照合した。

Windowsのr3d実ブラウザーでは対象別タブ・セル移動・構造C#往復・不正入力保持・DB内関数6と登録関数11を確認した。資源計測は実行ID `3bd0aa9c-9a44-46d1-949f-3f688b34b00b` で経過345.5916ms、ゲート開始offset317.411ms、ゲート待機0.0013msを得て、準備時間と実Waitを分離できた。OS観測の保存状態Saved、形式2のJSON/SVG/HTML、再読込後の履歴から同じ実行IDの復元をDOMで確認した。再表示は問い合わせを実行しない。証拠は `F:/Seamless/manager-object-ui-20261110/r3d-dom-objects-r2`、`r3d-dom-heat-r1`、`r3d-dom-heat-history-r1` に保存した。

Windowsのr3dブラウザーでは登録関数profile-sumの戻り値5、物理消費区間の入力2・出力1、Normal/Profiledの別本文SHA、Auto経路、DAG詳細とGanttの同実行IDを確認した。証拠は `F:/Seamless/manager-object-ui-20261110/r3d-dom-profile-r2/node-profile.json` と画像に保存した。通常の生成・CFG・束縛・保存形式39件はr3d、履歴・トレース40件はr3eの固定DLLで成功しており、最新候補の環境受入と分けて扱う。

## 残る取得範囲と保存制約

- CPUレーンはOS論理プロセッサ単位。Package/Coreの階層とP/Eの表示、Worker/Taskのスケジューリング記録、ノード別CPU/I/O帰属、NUMA帯域とPCIe/PMCの実収集は未実装である。ホストやProcessの値を代用せず、未収集を表示する。
- ノードの最初の対応は同期Sumの包含消費区間。元helper内部や複数の非対応枝を分解した計測は後続である。
- 保存済みOS観測を復元できる。ノード実測は現在応答と形式2交換に保持し、サーバー再起動後に履歴から復元する専用保存は後続である。
- 欠損履歴の復旧用OpenExistingは既存ロックとmanifestを必須とし、欠損したディレクトリ・manifestを再作成しない。r3eの履歴・トレース40件に検査直後削除の2境界を含む。component/segmentを検査とopenの間に外部削除する競合窓は残り、全ファイル削除競合の受入は継続する。

## 最終UI候補と稼働切替

最終UI候補r3hはmanifest `DFED2803165CBF2CD7BA405D57D44D989217E366FF303BB8B49E6C544F985E91`。r3gとの差はDB情報の版表示とDraft翻訳を補正したUI3ファイルだけであり、計測・生成器・保存のソースは一致する。Windows Releaseの警告・エラー0件、共通129件・履歴40件・計画意味11件・生成/保存39件が成功した。件数には重複があるため合算を固有ケース数として扱わない。計測の型別48件とUbuntu177件は同じ計測ソースのr3g証拠を再利用する。r3hのUbuntu Release publishも成功した。

Windowsのr3h DOMでは対象編集の往復、DB内関数6と登録関数11、DB情報v1、英語Draftを照合した。Ubuntu r3g DOMでは同じ操作に加え、実Profiledの入力2/出力1、Normal/Profiledの別本文hash、同実行IDのDAG/Gantt、OSヒートの形式2 JSON/SVG/HTMLを確認した。正常停止・再起動後にも同じOS観測の実行IDを履歴から復元した。証拠は `r3h-dom-objects-r1`、`r3h-dom-language-r2`、`r3g-linux-dom-objects-r2`、`r3g-linux-dom-profile-r2`、`r3g-linux-dom-heat-r2`、`r3g-linux-dom-heat-restart-r1` へ保存した。

利用者の保存済み確認後、Windowsの実利用領域を349ファイル31,595,453bytesの全SHA一致backup付きで再開した。現ホストPID50928、同じ管理領域の形式6、VisualPlanDemo v1を確認し、既定ブラウザーへ所有者用の単一セッションで開いた。Linuxは634ファイル31,829,492bytesの停止後backup（CLI対象外のmanagement.lockを除外）と試行コピーを確認し、PID143270/start8558897で同root・同portの新版へ切り替えた。DB1・定義1・下書き2・利用者1・履歴12の論理SHAは前後一致した。公開Table0のためLinux本番の行照合は適用外。既存Chrome93455を維持して同プロファイルの新タブへ新所有者セッションを渡した。本人ChromeはCDPなしのため、表示引き渡し成功と、隔離ChromeでのDOM成功を分ける。

切替証拠は `F:/Seamless/manager-object-ui-20261110/live-windows-r3h/rollout.json` と `linux-live-rollout-r3h-r4/final-proof.json`、`desktop-open-proof.json`。ai-harnessのbackupサブコマンドは未実装のため、既存のSeamless CLI/停止後コピーとSHA照合を使用した。切替補助の単一セッション制約と、Probe publishをServer単体で起動する際の静的アセットmanifest名を修正した。Linuxの試行失敗時は旧版へ正常復帰して論理一致を確認した。
