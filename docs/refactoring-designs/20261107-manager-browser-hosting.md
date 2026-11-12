# Managerのブラウザー統一と実行形態

## 合意した構成

2026年11月7日の利用者指示により、Managerの画面をブラウザー版へ統一する。Windows・Linux向けの専用デスクトップManagerの配布と、ネイティブWebViewを用いたManagerの正式化は、今後の完成条件から外す。業務アプリ自身のUIは、その業務アプリの構成に従う。

11月11日の利用者確認に基づき、プロセス境界を次の3形態へ訂正する。すべてのManager画面をブラウザーで利用する。

| 実行形態 | DB所有・実行プロセス | Manager Webサーバー | C#業務クライアント |
|---|---|---|---|
| 組み込み同期型 | スタンドアロン業務アプリにDB実行・管理機能を組み込む | 同じプロセスのKestrel | 同じ業務アプリがDB定義.dllを同期直接呼び出し |
| Manager Webサーバー同居型 | SeamlessサーバーにDB・管理機能・業務接続受付を組み込む | 同じサーバープロセスのKestrel | 別プロセス・別PCの通常C#アプリがDB通信.dllを使用 |
| Manager Webサーバー＋Seamlessサーバー型 | SeamlessサーバーがDBを所有・実行し、業務接続を受け付ける | 別プロセスのKestrelから管理操作をSeamlessサーバーへ渡す | 別プロセス・別PCの通常C#アプリがDB通信.dllを使用 |

組み込み同期型では業務アプリ・DB実行・管理機能・Kestrelが一つのOSプロセスに入り、ブラウザーは別プロセスで表示する。単一クライアント制約は業務クライアントに適用し、ManagerからのDB操作も共通の一つのDB実行スレッドへ集約する。画面描画、通信待機、OSのI/Oは専用DB実行スレッドから分離する。Manager Webサーバー同居型と分離型の業務クライアントは、接続するC#アプリである。

## DB定義.dllとDB通信.dll

利用者がDB定義.csへデータ構造と処理本体を記述し、generatorとビルドで2種類の成果物を作る。DB定義.dllはSeamlessサーバーまたは組み込み業務アプリ内でDB処理を実行する。DB通信.dllは別プロセス・別PCのC#業務アプリが参照し、サーバーへ通信してDB定義.dllの対応処理を呼ぶ。直接呼び出しと通信呼び出しの利用方法・公開APIを酷似した形へ揃える。

引数・戻り値・通信形式・処理識別などの公開契約を両成果物間で対応させる。DB通信.dllの役割には通信実装を含む。「公開契約DLL」という名称だけではこの役割を表せない。独立した第3のDLLの必須化は合意対象に含めない。

実装の受入では、既存ホストの同居動作と、別プロセスManager Webサーバーからの管理操作転送を個別に確認する。以下の既存実装・検証記録から、分離型までの完成を推定しない。

## 現在の配布対象と将来のFrontend分離

11月11日の追加指示では、現在のDB実行物をSeamlessスタンドアロンアプリとSeamlessサーバーに整理する。どちらもDB定義.dllを使用する。サーバーにはSeamlessManagerサーバーと複数のC#業務クライアントが接続する。Manager画面はブラウザーで利用し、上の表のManager Webサーバー同居・別プロセスの区別を維持する。

将来はDB定義.dllを実行するSeamlessBackendサーバーと、接続受付を担うSeamlessFrontendサーバーへ分離できる構成を目指す。Frontendの候補責務は、1万クライアントの接続受付、認証、圧縮・展開、暗号化・復号、監査、入力・出力データのバッファリングである。Managerサーバーは管理操作をBackendへ渡す。

Frontendの独立サーバー化と1万接続の実装・受入は将来範囲とし、現在の完成条件へ加えない。現在は通信・セッション・認証・転送形式・バッファ所有と、DBの取引・問い合わせ実行・永続化・DB資源管理の責務を分ける。現行のサーバー内部でこれらを提供し、将来別プロセスへ配置できる境界を保つ。将来構成のプロセス間プロトコル、Frontendの台数、Backendへの直接接続の扱いは別の設計単位で確定する。

11月11日の読取点検では、接続・TLSとDB実行の完全な分離は未完了である。既存の境界と、将来の分離で保持すべき接点を次のように確認した。

- `Seamless.Transport.Kestrel/KestrelTcpHost.cs`と`KestrelTls.cs`は接続受付とTLS・handshake枠、`LengthPrefixedPipe.cs`はフレーム借用・受信／送信byte予算を担当する。`NativeGatewayAuthorization.cs`は認証・取引lease・実行前の再認可を担当し、`GatewayProtection.Native.cs`からDBhostと公開クエリに対する認可callbackを束縛する。
- `NativeQueryServer.cs`の`ConnectionAsync`は通信要求の受理と同じ処理内で`InProcessDatabaseAdapter`を要求し、実`DatabaseTransaction`・`NativeContext`を操作して行を送信する。`ExecuteGatewayAsync`もDB列挙、親予算内の結果保管、commit、wire出力の所有を連続して持つ。通信側へ取引確定を移す変更では、確定前に書込結果を送らない契約と、取消・例外時の保存点処理を維持する必要がある。
- `Hosting/IDatabaseAdapter.cs`の`Func<object,bool>`と`SessionContext`、`SessionContext.cs`の実取引・`ExecutionContext`はプロセス内契約で、現在のまま別PIDのBackendへ差し替えられる契約にはなっていない。認可に用いるsubject・policy版・監査operation IDと、確定結果の対応も同じ境界で扱う必要がある。今回読んだNative／共通Transportには圧縮処理の接続を確認していない。

この点検は現行コードの責務配置を示す。新Frontend、別PID用の新プロトコル、1万接続試験や大規模な責務移動は実施していない。現行1〜21の受入を先行し、Frontend実装はその後の独立した設計・検証単位へ残す。

## 接続と認可の契約

ローカル利用は、サーバー所有者と同じOSアカウントという既存の合意を引き継ぐ。業務アプリまたは小さな起動コマンドがOS所有者を確認し、短時間・一回用のブラウザー起動情報を発行する。同じPCの別プロセスから起動する場合は、既存のOS確認付きIPCを用いる。ブラウザーは起動情報を既存の所有者セッションへ交換し、独自パスワードの入力を省く。各要求でセッションを検査する。

URLへのlocalhost指定や要求本文の利用者名は、OS所有者の証明に使わない。Webの既定待受けはループバックとする。LAN・WANからのブラウザー利用は明示的な公開設定とHTTPS、利用者ごとの認証・認可へ接続する。業務クライアントのネイティブTCP/TLSは別の接続契約であり、既存の証明書検証・認証・認可を維持する。

## 再利用する基盤

実装では既存の `WindowsServer.csproj` をWeb SDKへ接続し、同じサーバーアセンブリに再利用可能な `ManagerWebHost` と共通Razor画面の起動部を置く。`Seamless.Management.Components` を参照するため、業務アプリへの組み込みと独立サーバーが同じホスト部品を使える。単一DBスレッド型は `InProcessManagementHost` へ配送し、サーバー型は同じ `ManagementService` を使うインプロセスの操作アダプターへ接続する。ローカル起動用IPCはブラウザーセッションの発行にも使う。複数の業務クライアントは通常のC# EXEである。

共通Razor画面 `Seamless.Management.Components`、`IManagementOperations`、`IManagementSourceOperations`、OS所有者IPC、`LocalBrowserSessions`、`BrowserManagementOperations`、既存のKestrel処理を正式な共通ホストへ集約する。プロジェクトを開く対象はSeamlessサーバー側のファイルである。開発時は開発者PCでサーバーを動かし、IDEとManagerが同じソースを編集し、開発者PCでDLLをビルドする。

## 移行順序

1. 隔離試作で確認した共通Manager画面と所有者セッションを、リポジトリ内の共通Webホストへ接続する。
2. 独立サーバーの起動と、OS所有者IPCからブラウザーを開く起動コマンドを完成させる。
3. 同じWebホストを組み込み同期・サーバー同居のライフサイクルへ組み込む。
4. 既存WPF画面のDB作成、設計、ソース編集、下書き、検証、明示適用、結果・計画表示の移行を確認する。
5. 専用デスクトップManagerとネイティブWebView試作のプロジェクト、依存、起動設定、専用試験を整理する。
6. Windows・Linuxのサーバーとブラウザーで正常系、同一所有者のログイン省略、外部認証・権限、取引と復旧を受入確認する。

## 現在の実装と検証範囲

`ManagerWebHost` と共通Razorの起動部をサーバープロジェクトへ追加した。組み込み同期は `InProcessManagementHost`、サーバー型は同じ `ManagementService` を共有する `ConcurrentManagementOperations` へ接続する。独立サーバーの `serve` はKestrelを同じPIDで起動し、`manager --root <保存先>` はOS所有者IPCから一回用の起動情報を取得する。外部公開には別のHTTPS待受けと `UserBrowserSessions` を使い、既存の利用者認証・認可へ接続した。

Debugのホスト・外部認証・ソース位置の試験7件が成功した。組み込みの同一PID、所有者起動情報の必須化、静的アセット配信、同居ホストと複数のネイティブ接続、外部利用者の権限変更・無効化の再確認、CRLFを含む選択範囲を検証した。別の代表試験ではホスト2件とソースプロジェクト11件が成功した。この2組はホスト試験が重複するため合算しない。証跡は `F:\Seamless\manager-cross-platform-20261107\browser-host-auth-debug-results\browser-host.trx` と `browser-host-unit3-debug-tests` にある。

正式CLIから起動したWindows上のホストをブラウザーで操作し、DB作成、専用プロジェクト保存、テーブル追加、下書き、検証、明示適用による公開版v2を確認した。別のソリューションを開き、`Sample` と `Tables.dbo`・`Views.dbo` の宣言を表示し、テーブル選択から対応するC#宣言へ移動できた。画像は `scratch/manager-formal-browser-applied-20261107.jpg` と `scratch/manager-formal-source-navigation-20261107.jpg` にある。

専用デスクトップManagerとWebView試作を通常のソリューション登録から外した。WPFの既存ソースは未移行機能の参照用に保持し、削除した試作・専用試験の原本は `F:\Seamless\manager-cross-platform-20261107\retired-desktop-sources` へハッシュ付きで保全した。IDEの通常起動、配布・ソース連携・回帰スクリプトをWebホストへ接続した。VS Code、ソリューション起動、サーバーの起動プロファイルは `serve --open-manager` を使う。IDE上でF5を操作する実検証は未実施である。

### 新規プロジェクトの正常系と配布入口

作成した `SourceProjectDocument` が親画面から編集画面へ渡されておらず、ファイルだけが作られる問題を修正した。新規作成直後にプロジェクト・ファイル・ソースを表示し、すでに開いた別プロジェクトの編集状態も保持する。Windowsの配布物を実際に起動し、ブラウザーで `ProjectFlow` のDB作成、専用プロジェクト保存、編集画面への自動接続、C#追記、同期、実ファイルへの保存を確認した。画像は `scratch/manager-created-project-sync-20261107.png` にある。後続の稼働DBの個人下書きへの反映は下記の範囲まで接続した。開発者ビルド成果物の明示適用は残る。

正式な `Test-SourceWorkspace.ps1` でDebug／Release各55件が成功し、生成したVS Codeの既定ビルドタスクが実際のcsprojを指すこと、そのcsprojをパッケージ参照で復元・ビルドできることを確認した。両構成で生成プロジェクトのビルドは警告0・エラー0だった。証跡は `F:\Seamless\manager-cross-platform-20261107\source-entry-debug2` と `source-entry-release` にある。二組の重複する試験を機能数として合算しない。

WindowsのRelease配布ZIPで、初期化・復旧等の既存CLI検査に加え、正式 `manager` コマンド、所有者IPC、未認証HTTPの拒否、一回用情報のセッション交換、画面・静的アセット配信、正常停止時の記述子除去が成功した。証跡は `F:\Seamless\mw7pkg-logs\package-test-logs-0ae784f1975645d89e0faa74edb1cf8f` にある。この配布物は後述の静的計画メタデータ修正前の版であり、現在の最終配布候補とは区別する。

### 全件回帰で観測した問題

Webホスト起動が非同期に進むため、試験側がreadyファイルを起動メソッドの戻り直後に確認していた前提を修正した。上限付きでreadyまたは起動失敗を観測し、元の起動・停止検査を維持した。またFLinqの静的計画Writerが呼ばれず、生成フラグメントの元コード位置も記録されていなかったため、既存Writerと位置情報への接続を復旧した。変更後の正式Debug／Release成果物で静的計画・起動・復旧・外部認証・プロジェクト表示の近接試験各11件が成功した。証跡は `browser-final-near-debug-results/near.trx` と `browser-final-near-release-results/near.trx` にある。

初期の全件回帰は約30分で終了し、517件中516件成功・1件失敗・スキップ0件だった（`browser-final-full-debug-results/management.trx`）。`PublicationCrash.Recover(Committed)` が子プロセスの強制終了・終了待機後にmanagement.lock共有違反で失敗した。元の不合格結果は保持する。2026年11月8日に逐次反復で同じ失敗を再現し、WaitForExitAsync完了時にWindowsのプロセスハンドルが終了未完了だったことを観測した。即時オープンとカーネル終了待ちの修正・比較実験は [復旧終了条件の観測](20261108-recovery-lock-observation.md) へ集約する。修正後の最終配布候補の全件受入は継続する。

先行する診断版517件、例外観測フック付き並行群369件、Gateway CLIとの同時実行3件には成功記録がある。これらの単独成功から原因を確定しない。所有者観測の対照試験と先行記録は `recovery-full-diagnostic-results/diagnostic.trx`、`recovery-original-hook-results/hook.trx`、`gateway-publication-results/overlap.trx`、`owner-hook-known-proof` にある。現在の終了条件・診断は上の復旧記録に従う。

静的計画と起動の修正を検証した成果物は `F:\Seamless\mw7source-artifacts` にある。Server・Management・Components・FLinq Generator・Management.Testsの主要5アセンブリについて、当該ビルドの照合時点でPDBのソースと作業ツリーを確認し、Debug／Release各386件で不一致0件だった。DLL／PDBのSHA-256と照合件数は `F:\Seamless\manager-cross-platform-20261107\browser-current-source-checksums.json` に記録した。

後続の同期記録・デバウンス修正は `F:\Seamless\mw8source-artifacts` を使い、Debug／Releaseの関連各44件が成功した。変更したSourceWorkspace・Import・SourceWatcherと二つの試験ファイルについて、両構成のPDBと現在のソース各5件が一致した。修正前の二つの失敗、修正後の試験、各診断実行とSHA-256は `F:\Seamless\manager-cross-platform-20261107\source-sync-verification.json` にまとめた。PDBの照合は記録された対象ソースの範囲で行う。Razorの全生成入力と最終配布物の照合は対応する受入工程で確認する。

### ソースプロジェクトから同じDBの個人下書きへの反映

共通ソース操作とブラウザー画面へ「DB下書きへ反映」を追加した。プロジェクト生成時は現在の設計を個人下書きへ保管し、サーバーで版・内容を確認して比較基準を記録する。参照DLL一覧を再オープンで保持し、比較基準がない旧記録は編集前の全ファイルと現在の下書きの一致を確認できる場合に引き継ぐ。成功・失敗の結果はファイル同期の通知と分けて保持する。

`Test-SourceWorkspace.ps1` の正式入口でDebug／Release各82件、両構成のWebビルドと生成プロジェクトの実ビルドが成功した。Windowsの正式CLIから起動した固定成果物をブラウザーで操作し、DB作成、保存前のテーブル追加、プロジェクト生成、C#編集、個人下書きr3への初回反映を確認した。DB画面でも同じコードを表示し、公開版v1と未適用状態を確認した。旧プロジェクトの再開・反映・再要求も確認した。実ブラウザーはDebugの固定成果物で実行し、Releaseはコード内・ホスト・生成プロジェクトビルドの受入である。

ブラウザー実行体と試験参照、Releaseビルドと試験参照について、既存のPDB照合ツールで各17アセンブリの通常リポジトリソースが一致した。仮想生成文書・NuGetソースは同ツールの対象外であり、最終配布ZIPの更新を示す証跡とは分ける。各TRX、途中の不合格、DLL・PDBと画像のハッシュは `F:\Seamless\manager-cross-platform-20261107\source-draft-verification.json` に記録する。画像は `scratch/manager-new-project-draft-saved-20261107.png` と `scratch/manager-new-project-draft-db-20261107.png` にある。

開発者ビルド成果物の取り込み・明示適用、実IDE操作、Linux、全件回帰の復旧時ロック問題と最終配布は残る。

残る受入として掲げられていた「生成クライアントDLLを使わない業務アプリからの同期DBクエリ」および「別C# EXEの業務クライアントによる複数接続」を検証・完了した（2026-11-07）。結果グリッド・静的計画の共通画面への接続は追加済みで、実操作の受入は下記の範囲を引き継ぐ。

### 業務アプリ同期DBクエリと別C# EXE複数接続の受入完了 (2026-11-07)

1. **生成クライアント DLL を使わない業務アプリからの同期 DB クエリ**:
   - `Sample.NetShop.Core/資源設定.cs` のストアメモリ上限を最新 Tsavorite 2.2.5 の会計基準に合わせて 32 MiB に調整。
   - `Sample.NetShop.Tests`（14 テスト）を `TestSeamless/Invoke-LocalProcess.ps1`（最大 8 論理 CPU 制限下）経由で実行し、14/14 件 100% 合格（27.3s、警告 0・エラー 0、証跡: `F:\Seamless\test-artifacts-netshop`）。
2. **別 C# EXE 業務クライアントによる複数接続**:
   - `MultiProjectTemplate` をビルドし、`TestSeamless/Test-MultiClientConcurrentConnections.ps1` を作成。
   - `NativeQueryServer` に対し 4 つの独立した `MultiProjectTemplate.Client`（別 C# EXE プロセス）を同時に並行起動し、個別ソケット接続で `FindByIdAsync`, `SearchByPriceAsync`, `CountAllAsync` を並行実行。
   - 全 4 プロセスが正常終了コード 0 かつ `CLIENT_SUCCESS` を出力し、100% 成功（証跡: `F:\Seamless\test-artifacts-multiclient`、`F:\Seamless\multiclient-verification`）。


### 結果グリッド・静的計画のWeb接続と残る受入 (2026-11-07)

行グリッドと静的計画の共通画面・API接続を追加した。画面操作の受入範囲は以下の試験本体と区別する。

1. **API・ホスト層の拡充**:
   - `IManagementOperations` に `ReadRowsAsync`, `CommitRowsAsync`, `PlansAsync` を追加。
   - `InProcessManagementHost`, `ConcurrentManagementOperations`, `BrowserManagementOperations`, `AuthenticatedManagementOperations` の全 4 ホスト形態に同コントラクトを透過的に実装。
   - リモート IPC/TCP クライアント（`ManagementClient.Operations.cs`）に `"readRows"`, `"commitRows"`, `"plans"` ハンドラを実装。

2. **Web画面への統合 (`Seamless.Management.Components`)**:
   - `結果グリッド.razor`: 選択テーブルの動的スキーマ・行データ取得、インラインセル編集、行追加/削除、ページング（Limit: 100）、一括保存・ロールバック。
   - `実行計画ビュー.razor`: 定義セレクター、ノードツリーカード、DOT グラフ可視化。
   - `管理ワークスペース.razor`: 表示種別（`構造`, `ソース`, `データ`, `計画`）タブ、表カード選択時のデータタブ自動遷移、下書き検証時の実行計画即時バインド。

3. **検証結果**:
   - `ManagerWebHostTests.EmbeddedManagerExecutesReadRowsCommitRowsAndPlans` は、公開したDBの空表読み取りと計画配列の非null、起動したKestrelのセッション交換と `BrowserManagementOperations` への直接呼び出しを検査する。以前の記録には1件成功・13秒とある。現行の本体では `CommitRowsAsync` を呼ばず、非空の計画、HTTP経由のDB操作、ブラウザーでの編集も検査していない。
   - 以前の記録にある「523件中501件成功」は、残り22件の状態と候補・証跡の対応を照合するまで全件合格の根拠にしない。行追加・変更・削除・ページング・保存失敗時の入力保持と、実計画の表示をブラウザーで受け入れる。DOTは現在文字列プレビュー、ノードはカード一覧として表示する。

## Linux検証環境

利用者の指示でWSL2とWSLgを導入している。既存の不完全なWSL登録をMicrosoft署名付き公式MSIで修復し、終了コード0を確認した。実コマンドの報告値はWSL 3.0.3.0、Linuxカーネル6.18.54.1-1、WSLg 1.0.79である。WSLパッケージの版と、ディストリビューションの実行方式であるWSL2を区別する。

Ubuntu 24.04 LTSをWindows再起動後にWSLの公式配布入口から導入し、保存先は `F:\Seamless\linux-20261108\Ubuntu-24.04` とした。以前のダウンロード済みイメージは保持する。既存の `.wslconfig` は8仮想CPU、メモリ4GB、スワップ2GB、スワップ先Fドライブ、GUI有効を指定している。仮想CPU数とWindows側の物理CPU affinityを区別し、Linuxのビルドもtasksetで0〜7へ制限する。

Ubuntu 24.04.5 LTSの実起動、WSLgの存在、SDK 10.0.403の導入と起動を確認した。現在の実行手順・出力先・未検証の受入は [Linux検証環境](20261108-linux-wsl-validation.md) へ集約する。以前のWSL修復の証跡は `F:\Seamless\manager-cross-platform-20261107\wsl-install` に保持する。
