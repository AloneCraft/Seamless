# Seamless管理ホストとブラウザーManager

Windows/Linuxのサーバーと同じプロセス内のKestrelが、共通のRazor管理画面を配信する。業務クライアントはC# EXEからネイティブTCP/TLSを利用する。Managerの組み込み同期・サーバー同居・独立サーバーの構成と現在の受入範囲は [ブラウザー統一と実行形態](../../Document/20261114Argon.md) を参照する。

## プロジェクト

`Publish.ps1`の配布物は、専用DBプロジェクト用の`packages`を同梱する。同じビルドの12パッケージを内容識別付きの版へ固定し、サーバーDLLとの対応を`source-packages.json`で確認する。新規プロジェクトはこの版を選ぶ。既存のプロジェクト参照は明示した設定に従う。[パッケージ対応の契約と確認範囲](../../Document/20261114Argon.md)

ネイティブ資産はpublishの直下出力に加え、deps.jsonの`runtimes/<RID>/native`階層を保持する。TsavoriteのP/Invokeがこの相対名を使うため、Linuxの通常起動にも必要である。

| プロジェクト | 責務 |
|---|---|
| `Seamless.Management` | 通信契約、TLSクライアント、Roslynの編集投影、ソース交換形式、計画のPE読取とDOT出力 |
| `Seamless.Management.Server` | 認証・権限、永続システムDB、ビルド、検証DB、世代公開、CLI |
| `Seamless.Management.Components` | 共通のRazor画面、操作メニュー、設計・ソース編集 |
| `Seamless.Management.UI` | 未移行機能の参照用に保全した旧WPFソース |
| `Tests/Management.Tests` | 単体・実TLS・永続化・移行・公開境界でのプロセス停止試験 |

`Seamless.Management.slnx`から関連プロジェクトを開ける。既存の管理対象DBへこのホストを自動接続したり、既存の保存形式を起動時に変換したりする機能は持たない。新しい管理領域でDB定義を明示的に公開する。

## 起動

配布ZIPは.NETランタイムを同梱しない。実行に必要なものと、通常のプロジェクトをビルドするためのSDKを分けて導入する。

`Invoke-Checks.ps1`の通常実行はリポジトリ内の`bin/obj`をビルド先、`Work/Management`をログ・一時ファイル・キャッシュの保存先とする。稼働DBから分離する試験DBと試験用キャッシュは、実行ごとに新しい`F:/Seamless`配下へ作成する。成果物自体の隔離も必要な場合だけ`-WorkRoot F:/Seamless/<実行名>`を指定する。既存のNuGetキャッシュをコピーせず参照するには`-PackageFallbackRoot <キャッシュの絶対パス>`を指定でき、書込先は選択した作業領域のままとする。

各実行先の`execution-input.json`に、対象・構成・フィルター・出力方式・隔離DBとキャッシュのパス・入口スクリプトのハッシュを記録する。プロセスを開始したら、実行器は`process-start.json`に実行器と子プロセスのPID・開始時刻を保存する。`TestSeamless/Get-GatewayVerificationSummary.ps1 -RunRoot <実行先> -Suite ManagementChecks`は、終了記録があれば同じ試行の`receipt.json`と`checks.trx`を集計する。終了記録がない場合は、子プロセスのPIDと開始時刻を照合できれば`Running`、開始記録や試行の確保だけが残っていれば`Unknown`と表示する。旧形式の`NoReceipt`や`Missing`も含め、終了や失敗を推定せず元の実行ハンドルを確認する。同じ証跡フォルダーを再利用すると実行器が拒否するため、再試験を明示的に開始するときは新しいフォルダーを指定する。

Gatewayの生成ビルド試験には、通常出力／分離出力に対応する`SEAMLESS_TEST_GENERATOR_ASSEMBLY`と、主キャッシュまたは明示したfallback内の`SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY`を渡す。選択したパスは同じ入力記録へ残す。生成器だけの対照実験では`SEAMLESS_TEST_FLINQ_GENERATOR_ASSEMBLY`に保全済みDLLを明示できるが、通常の合格を旧生成器への差替えで代替しない。指定がなければ選択した出力のFLinq生成器を使う。現在の回帰では、実行時DLLだけでなく実際のanalyzerのDLL/PDBとソースの対応も確認する。

`Invoke-Checks.ps1 -AllowCancellation` で始めた検証は、実行中の工程の `process-start.json` にある `Task.cancellationRequestFile` と同名の空ファイルを、同じ工程フォルダーに作ると取消を要求できる。`-WithUI` のWebホストビルド中は `ui-build` が工程フォルダーとなる。取消要求があっても、実行器が子プロセスの終了と出力回収を確認するまで `Cancelled` にはならない。取消が完了すると検証入口は終了コード2を返し、後続の試験を開始しない。終了を確認できない場合は `Unknown` の記録を確認する。

固定済みの実行体から直接テストする工程には `Invoke-VerifiedTestStage.ps1` を使える。`-InputPath` に対象ソースまたはビルド済み出力と、解決済みの依存物・SDK・設定を指定し、`-Conditions` に構成と環境条件を記録する。引数の `{results}` は新しい `-AttemptRoot` に置き換わるので、その場所に `checks.trx` を出力するよう指定する。引数と条件は証跡に記録するため、認証情報を含めない。工程が合格してから、開始・終了記録とTRXのハッシュを保存する。`-ReuseFrom` では、同じ入力・条件と有効な合格証跡を再照合し、新しい実行を起動せずに参照を記録する。照合できない場合は停止し、入力変更を確認したうえで新しい試行として実行する。

`-MinimumPassedTests` と `-RequiredPassedTest` で合格件数の下限とTRX上の必須テスト名を指定できる。`-RequiredArtifact` は試行フォルダーからの相対ファイル名を指定し、合格時と再利用時に内容を照合する。Webホストの試験では互換名の `-ManagementUiEvidence` を指定すると、`SEAMLESS_MANAGEMENT_SERVER_TEST_EXE` の配置フォルダーも照合し、HTTP検証JSONの出力先をその試行フォルダーに設定する。`manager-web-http.json` を `-RequiredArtifact` に列挙する。

Seamless・.NETホスト・VSTest・NuGet関連の環境変数は、名前と値の集合のハッシュを照合する。別の環境変数が試験条件に影響するときは `-EnvironmentVariable` に名前を追加する。値は証跡へ書かない。変更前の証跡形式からの再利用には、現在の形式で合格記録を作り直す。

固定済み成果物の工程を順番に実行するには、`Invoke-VerifiedRegression.ps1 -PlanPath <計画JSON> -AttemptRoot <新しい試行フォルダー>` を使う。計画は `Format` が `1` のオブジェクトで、`Stages` に実行順の配列を置く。各要素に一意の `Name` と、`Invoke-VerifiedTestStage.ps1` の実行条件を指定する。試行先と再利用先は工程ごとに実行器が割り当てる。

失敗した工程以降は開始せず、`regression-result.json` に合格済み工程と停止箇所を記録する。`-ReuseFrom <前回の試行フォルダー>` では、前回の終端記録が再開可能で、合格済み工程が新しい計画の先頭と同じ順序の場合に、各工程の元の証跡を照合する。不一致はそこで停止し、合格記録や後続工程を自動で作り直さない。終了を確認できない試行は、元のプロセスと記録を照合するまで再開対象から外す。この入口の合格は計画に含めた工程の結果を表す。

`New-WindowsRegressionPlan.ps1` は、固定した候補の `-SnapshotManifest` と、同じ候補をDebug・Releaseでビルドした `-ArtifactRoot` を受け取る。MemoryPack生成器のパスを `-MemoryPackGenerator`、使用する.NET SDKとランタイムの版を `-SdkVersion` と `-RuntimeVersion`、計画ファイルを `-OutputPath` に指定する。Gatewayと管理の各構成をフィルターなしで実行する四工程を出力し、回帰再開条件になっているFLinq・復旧ケースと、Managerの実接続・描画の証跡を必須にする。計画を作成する際に、生成器と、テスト・Managerの `.deps.json` が指定するプロジェクトDLL/PDBを固定ソースと照合する。パッケージ由来の依存物は内容のハッシュに含める。

四工程は、構成ごとの生成器とManagerを指定し、工程の前後で固定ソースと計画に記録した成果物のハッシュを照合する。成果物のハッシュを持たない既存の固定候補計画は作成し直す。試験データとキャッシュは `F:/Seamless` 配下の短い隔離パスへ分け、その所在を `test-runtime.json` に保存する。配布物を照合する際は、完了した全件回帰の元の合格工程を参照する。

`Test-DistributionSource.ps1 -PublishedDirectory <展開したアプリのフォルダー> -ReferenceDirectory <回帰で使用したbinフォルダー> -OutputPath <新しいJSON記録>`は、deps.jsonに記載された自前プロジェクトのDLLについて、DLLとPDBの識別子、回帰成果物と配布物の通常ソース集合・チェックサム、現在のファイルとの一致を検査する。生成コードの仮想文書とNuGet依存元のソースは対象外で、リポジトリソースを照合できないプロジェクトDLLも記録する。未照合項目を合格として扱わず、配布ファイルのマニフェスト照合・第三者通知・実行試験と合わせて判定する。DLL/PDBの組違い、古いソース、既存の照合JSONへの上書きは拒否する。

固定回帰と配布を対応付けるときは `-RegressionStage <元の合格工程フォルダー>` を追加する。合格記録に結び付いた入力ハッシュ、TRXと開始・終了記録のハッシュ、VSTestが実行したテストDLLの配置先またはManagerの配置先を確認する。そのうえで、照合対象の参照DLLとPDBが工程の入力記録にあるパスとハッシュに一致することを検査する。再利用フォルダーを使った場合は `stage-reuse.json` の `OriginalAttempt` を指定する。照合で検査した回帰範囲は元の工程の範囲に従う。

管理TCP/TLSはKestrelを使い、同一OS利用者のIPCは既存NamedPipeを維持する。[通信基盤の仕様と制限](../../Document/Kestrel通信基盤.md)を参照する。クライアントにASP.NET Core依存は追加しない。

ローカルManagerは `manager --root <稼働中サーバーの保存先>` で開く。OS所有者を確認したIPCから短時間・一回用の起動情報を取得し、ブラウザーのセッションへ交換する。同じOSアカウントの利用者はSeamless独自パスワードの入力を省ける。Managerの起動情報は `manager-connection.json` に保存し、ネイティブTCPの待受け先から独立させる。

`Publish.ps1`はMITのLICENSEと、実際のpublish出力に含まれる依存資産の`THIRD-PARTY-NOTICES.md`・`ThirdParty/packages.json`・同梱通知をZIPへ保持する。パッケージにないApache条文とDiskANNの通知は`Licenses/sources.json`の公式取得元を記録して含める。DiskANNパッケージにはビルド元コミットの記載がないため、取得した通知のコミットをパッケージのビルドコミットと断定しない。独立した配布検証の`-WorkRoot`と`-LogRoot`は`F:/Seamless`配下を指定する。両方の既定は`F:/Seamless/Management`で、SDK用ホーム・一時ファイル・NuGetキャッシュも選択した作業領域へ保存する。`Publish.ps1 -PackageFallbackRoot <既存キャッシュ>`を指定した場合、第三者通知も主キャッシュとその参照先から取得し、参照先へは書き込まない。Windowsだけを確認する場合は、配布と検証の両方へ`-Targets ServerWindows`を指定する。

framework-dependent配布でも、起動用の.NET apphostは含まれる。`Publish.ps1`は同じSDK条件で`ResolveFrameworkReferences`の`AppHostSourcePath`を取得し、元バイナリのSHA-256が`Licenses/AppHost.sources.json`と一致する場合だけ、その公式パッケージのLICENSE・THIRD-PARTY-NOTICESを同梱する。配布側の起動ファイルと通知のハッシュは`ThirdParty/apphost.json`に記録する。SDK更新等で未知のapphostになった場合は、対応する公式パッケージと元バイナリを照合して通知カタログを更新するまでpublishを止める。ランタイムやSDK本体の同梱とは別である。Gatewayのtool packは既存の通知生成ターゲットから同じ処理を呼び、既存キャッシュを参照する場合は`SeamlessPackageFallbackRoot`で明示する。

配布版の起動を確認する場合は、`Test-Package.ps1 -Distribution <配布先> -Targets ServerWindows -VerifyWindowsConnection` を実行する。隔離したサーバーを空きポートで起動し、正式CLIによるOS所有者IPCの起動情報取得、未認証HTTPの拒否、認証後の画面とCSS・Blazorスクリプトの取得、正常停止と接続記述子の削除を確認する。ブラウザーの描画・入力は別のUI操作確認で受け入れる。

| 用途 | 必要な導入物 |
|---|---|
| Manager画面を表示 | Webブラウザー |
| Windows／Linuxの管理サーバーを実行 | ASP.NET Core Runtime 10（現在の配布はx64。Microsoft.NETCore.AppとMicrosoft.AspNetCore.Appが必要） |
| VS／VS Code／CLIで.csprojをビルド | .NET 10 SDK |
| Gatewayの .NET ツールを `dotnet pack` で作成 | .NET 10 SDKとPowerShell 7（MSBuildの子シェルから `pwsh` を起動できること） |

導入先は[.NET 10公式ダウンロード](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)。通常利用では「Run apps - Runtime」から必要なランタイムを選び、開発では「Build apps - SDK」を選ぶ。SDK導入済みで必要な共有フレームワークもそろっていれば追加導入は不要。ランタイム不足時は.NETホストの起動エラーにも不足するフレームワークと導入先が表示される。サーバーにはMicrosoft.AspNetCore.Appを含むASP.NET Core Runtimeが必要である。

ManagerからのDB定義・クエリのコンパイルは、サーバーに同梱したRoslynと生成器を使用する。通常の.csprojをSDKでビルドする処理とは異なり、この経路のためのSDK導入は要求しない。SDKを含まない隔離ランタイムで配布版の起動、IPC接続、空DBのコンパイルと公開を確認した。SDK未導入の別PCと、ランタイム不足時の案内の実画面は未検証。

TLS証明書は接続するDNS名をSANに持ち、有効期間と信頼チェーンを満たすものを用意する。秘密鍵を持つPFXはサーバー側だけで使用する。

比較環境でのビルド・実行は`TestSeamless/Invoke-LocalProcess.ps1`を通し、子孫を含む共通の最大8論理CPUに制限する。通常の作業はリポジトリ内で行い、分離が必要な作業・実データ・キャッシュ・ログは`F:/Seamless`配下に置く。

## 構造・公開問い合わせとC#ソースの閲覧権限

DBの閲覧権限で、テーブルの列・型・キーや、関数の引数・戻り値などの構造を参照できる。C#の本文・過去版・ソース位置・解析結果・静的計画を取得するには、そのDBの閲覧権限に加えて編集権限が必要である。現在の管理APIでは編集権限を開発者向けのソース閲覧権限として使う。

編集権限がない利用者が公開済み定義を選ぶと、本文を取得せず、公開メソッドと稼働版の情報を表示する。問い合わせを実行する権限があれば、その画面の「公開問い合わせを実行…」、または公開問い合わせノードの右クリックから実行できる。ソースの閲覧権限は実行の条件にしない。オブジェクトの選択では、ソースの権限不足とソース未登録を区別して案内する。

対応能力は`DefinitionInfoV1`（本文を含まない公開情報）と`ProtectedSourceV1`（本文・ソース由来の情報の制限）。`definitionInfo`は定義のヘッダー・稼働版・DLL識別情報・テンプレートを除いた公開メソッド情報を返す。公開問い合わせの情報は実行権限でも取得できる。TLSとローカルOS認証のIPCで同じ制限を適用する。クライアント側の表示制御だけに依存しない。

## 起動とデバッグ

初回は開発者PCで `setup-local --root <空の保存先> --admin <管理者> --config <設定ファイル>` を明示実行する。パスワードは非表示入力、または自動試験用の `SEAMLESS_MANAGEMENT_PASSWORD` から受け取り、引数や設定ファイルへ保存しない。通常の設定ファイルは保存先・証明書・待受け設定を持つ。

```powershell
# 共通画面を配信し、OS所有者のセッションでブラウザーを開く
Seamless.Management.Server serve --config <設定ファイル> --open-manager
# 稼働中の同じホストのManagerを追加で開く
Seamless.Management.Server manager --root <保存先>
```

Visual Studioのソリューション起動プロファイルは「Seamless Server + Browser Manager」、プロジェクト側は「Server + Browser Manager」を選ぶ。VS Codeの同名Debug／Release設定も、サーバーを一つ起動して `--open-manager` を渡す。`--workspace-config` はGit管理対象外の `seamless-management.local.json` を読む。IDE画面でのF5とブレークポイントの受入は、CLI起動の確認と区別する。

ローカルWebの既定待受けは数値ループバック・空きポートである。固定する場合は `--manager-port <ポート>` を指定する。外部ブラウザー用の明示設定は `--manager-external-origin https://<DNS名>:<ポート>/` と、必要に応じて `--manager-external-address <待受けIP>` を使う。既存の秘密鍵付き証明書によりHTTPSを提供し、利用者ごとにログイン・認可する。ブラウザーが接続するDNS名と証明書のSAN・信頼チェーンを一致させる。

組み込みホストは `InProcessManagementHost.Create/Open` を `ManagerWebHost.StartAsync` へ渡し、同じPID内の管理APIとDB実行スレッドへ接続する。同居サーバーは `ConcurrentManagementOperations` とネイティブ受付から同じ `ManagementService` を使う。各ホストはライフサイクルに従って `Dispose/DisposeAsync` で終了する。詳しい受入の残りは設計文書の再開一覧へ記録する。

```powershell
# Debugの管理契約を検証
./tools/management/Invoke-Checks.ps1 -Configuration Debug
# Webホストをビルドする。BuildUI／WithUIは既存スクリプト用の互換名
./tools/management/Invoke-Checks.ps1 -Configuration Debug -BuildUI
./tools/management/Invoke-Checks.ps1 -Configuration Debug -WithUI
# ソース同期の試験と、作成したDBプロジェクトの実ビルド
./tools/management/Test-SourceWorkspace.ps1 -Configuration Debug
# 共通画面を含むWindows/LinuxサーバーのRelease ZIP
./tools/management/Publish.ps1
```

`serve --ready-file <ファイル>` はPID・生成時刻・インスタンスID・ネイティブポート・Managerの配信元とパスを記録する。`--stop-file <ファイル>` は、そのファイル作成で正常停止を要求する。`manager --launch-file <新しいファイル>` は描画試験等のため起動情報をファイルへ渡す。このファイルには短時間の起動能力が含まれるため、利用後に削除し、ログへ内容を出力しない。

## 操作の流れ

DB作成、テーブル設計、C#プレビュー、一括変更、外部キーと移行デバッグの手順は[SchemaChanges.md](SchemaChanges.md)を参照する。

Windows Releaseを通常起動し、DB作成から自己参照・循環参照の投入、一括スキーマ変更、移行後の読み取りまで画面から確認した結果は[UIWalkthrough.md](UIWalkthrough.md)を参照する。実画面で確認したプレビュー更新の遅延と、サンプル表を仮定した新規クエリ雛形を修正済み。

接続直後は「概要・オブジェクト」を表示する。中央のDB名または左のツリーのDB／スキーマを選ぶと一覧を取得する。名前・スキーマ完全一致・種類でサーバー側で絞り込み、取得したページだけを表示する。「条件を解除」「続きを取得」「選択対象の定義を開く」を使用できる。ダブルクリックまたはEnterでも定義ソースへ移動する。一覧の選択だけでは問い合わせを実行しない。取得失敗と検索結果なしを区別し、取得済み件数と続きの有無を表示する。

接続設定は起動時と接続後に折り畳む。TCP接続先が未登録の場合は、その旨と手動入力の案内を表示する。接続中の方式と接続先は左の状態表示・DBツリーで確認できる。未選択時は属性欄を閉じ、診断が空なら下部の診断欄も閉じる。スクロールバーは内容がはみ出す場合だけ表示する。新規パスワードが12文字未満、または確認欄と不一致の場合、ダイアログを閉じず赤いエラーを表示して入力を保持する。

ログイン後、「稼働DB・スキーマ・オブジェクト」を展開して一覧を段階的に取得する。テーブル・ビュー・関数・ストアドプロシージャ・システムテーブルに対応する。スキーマを持たない宣言は「スキーマなし」に表示する。対象オブジェクトの選択で、列・キー型・キーメンバー・引数・戻り値と、保存されている稼働版ソースの宣言位置を表示する。対応が不明な情報は補完しない。メタデータの取得でDBコンストラクター・getter・関数を実行しない。稼働版のソース／DLL／マニフェストの識別情報を照合し、一覧取得後に管理領域の版が変わった場合は再取得を要求する。

「公開済み定義（稼働版）」と「個人の下書き」は別項目。公開定義の選択時に下書きへ自動置換しない。下書きを再開する場合は下書き一覧を選ぶ。対応するソースがないオブジェクトは「ソース未登録」と表示する。全体のManage権限がある利用者にはシステムDBの実際の構造も表示するが、行データやパスワードハッシュを返さず、通常の編集・適用には接続しない。

パスワードを忘れた場合、別の管理者がログインできれば「利用者」から再設定する。最後の管理者もログインできない場合は対象サーバーを停止し、上記`reset-password`を使う。停止中の排他ロックを保持して明示した退避先へバックアップし、その成功後に既存利用者のハッシュだけを更新する。対象利用者がなければ作成せず失敗する。権限・有効無効状態・定義・データを維持し、復旧履歴を残す。バックアップ先が空でない場合や稼働中は変更しない。環境変数の変更だけで既存パスワードは変わらない。オンラインでのパスワード変更後は以前のセッションも失効する。

1. 接続後、左の「通常のデータベース」を右クリックして「新しいデータベース…」を選び、ツリー内で名前を入力して「作成」を押す。既存テーブルは対象ノードの右クリックから「設計を編集」へ進む。
2. C#または構造図の選択部分で編集する。GUI変更は対応する文字列範囲だけを書き換える。カスタムコード・コメント・入力途中の内容を保存できる。入力途中は最後の有効な図を「更新前」と表示する。
3. 個人の下書きとして保存し、静的計画を確認する。未公開の下書きも「個人の下書き」の一覧から再開できる。保存・解析・図の選択は問い合わせを実行しない。
4. DB定義から空の検証DBを作り、JSONで明示的にデータを投入する。問い合わせの下書きは検証DBでだけ実行できる。既存の個人用検証DBの選択・破棄にも対応する。
5. 差分・依存問い合わせの再コンパイル結果・直近の検証結果・稼働版を確認して明示適用する。未検証・検証失敗は警告として表示し、適用可否の最終判断は信頼する開発者が行う。構文・型・移行・制約エラーは適用を拒否する。
6. 切断や応答喪失では、表示された操作IDで結果を照会する。GUIは直近のIDを接続設定領域に保存する。取消要求の受付を取消完了とは表示しない。

権限はView、Execute、Edit、Validate、Apply、Manageを対象DBごとに設定する。利用者の作成・権限変更には全体のManage権限が必要。一般利用者は許可された公開定義と計画を閲覧し、公開済み問い合わせを実行する。自作C#は信頼する開発者に限る。追加アセンブリ、外部I/O、プロセス、リフレクション、dynamic、取引・保存先・DB寿命の直接制御などはコンパイル前の検査で拒否する。この検査は任意の悪意あるコードのサンドボックスではない。

問い合わせの入口は一意なpublic staticメソッド。`ManagementExecutionContext`から束縛されたDBを取得する。検証実行では稼働DBを名前指定で取得できない。`CancellationToken`による協調的な取消を利用できる。終わらない任意計算を強制中断する隔離実行環境は提供しない。

データを更新する問い合わせは定義の`WritesData`を明示する。読み取り専用定義は読取りセッション、更新定義は取引内で実行する。例外・取消・更新結果の上限超過・列挙途中の破棄では確定しない。結果は最大10,000行、標準1,000行。読み取り結果は上限までを返す。通常GUIは稼働DBの更新問い合わせを実行する前に確認する。

パスワードだけを変更する場合は「パスワード再設定」を使用する。対象利用者の複数DBに対する権限と有効・無効状態を保ち、変更後にその利用者のセッションを失効させる。自身のパスワードを変更した場合は、新しい値でログインし直す。

## 永続化と公開

アカウントのパスワードはASP.NET Core IdentityのPasswordHasherでハッシュ化する。利用者、権限、個人下書き、不変の共有版、ビルド・計画、検証結果、適用ID、履歴はSeamlessの永続取引テーブルへ保存する。定義・履歴のページ取得は永続した連番を使い、全件をメモリへ読み込まない。一覧は軽量な識別情報を返し、ソースは開く対象だけを取得する。大きなソース・計画は同じ取引内の64K文字チャンクへ分割し、長さとSHA-256を検証して読み戻す。

DLLとデータ世代は管理領域の`generations`以下に保存する。DLL、ソース、マニフェスト、保存済み計画の識別情報を照合する。DB変更は稼働世代を保持して非公開世代へ行を逐次移す。新旧アセンブリを別名で参照するC#の型付きコピーを生成し、制約を検証してから、主定義・依存する問い合わせ・操作結果・履歴をシステムDBの同じ取引で公開する。操作の対象DBは待機し、無関係なDBは継続できる。

### 監査テーブルと既存保存先の明示更新

新規の初期設定は管理DB形式4を作成する。以前の形式3は起動時には変換せず、そのまま従来の管理機能を利用できる。形式4では`AuditSearchV1`能力を公開し、`ManagementClient.AuditEventsAsync`と`ReadSummariesAsync`で詳細履歴・集計をページ取得する。現在の取得APIはサーバー管理権限を要求する。詳細履歴は自動削除しない。

既存の保存先へ監査テーブルを追加するときは、対象サーバーを停止し、未使用のバックアップ先を指定して実行する。以下の`Seamless.Management.Server.exe`は`WindowsServer.csproj`のビルド・配布先の実行ファイルを指定する。

```powershell
.\Seamless.Management.Server.exe upgrade-audit --root D:\Seamless\Management\Instance1 --backup D:\Seamless\Backups\BeforeAudit
```

排他ロックとバックアップに成功してから、隣接するステージ領域へ管理レコードを逐次複製する。利用者のパスワードハッシュ・権限・無効状態、定義・下書き・公開版・履歴・インスタンスIDを維持し、旧履歴を監査へ取り込む。DLL・データの`generations`ディレクトリは変更しない。切替前の`system`は旧ディレクトリとして保持する。ステージと旧ディレクトリも使うため、バックアップ先に加えて元の保存先のあるドライブにも空き領域が必要になる。

プロセス停止で`management.audit-upgrade.json`が残った場合、通常起動は拒否する。サーバーを停止した状態で次の明示取消を行い、旧形式に戻してから原因を解消して再試行する。

```powershell
.\Seamless.Management.Server.exe rollback-audit-upgrade --root D:\Seamless\Management\Instance1
```

これは中断した更新の取消専用であり、正常完了後に新しい操作を行ったDBを過去版へ戻す操作ではない。切替の確定後に中断マニフェストが削除されていれば、取消は拒否する。退避されたディレクトリは自動削除しない。

検索は日時・利用者・DB・操作種別・結果に加え、メッセージと名前空間・クラス・メソッド名などに含まれる文字列を対象とする。空白区切りの複数語はAND条件、大文字小文字は区別しない。ソース閲覧に必要な対象DBの編集権限がない場合、クエリテンプレートの返却とその内容による一致を抑止する。現在は範囲を制限した走査であり、全文検索用の転置索引・形態素解析は持たない。`HasMore`が真なら、結果が0件でも`NextBefore`と`Ceiling`を引き継いで続きを取得する。

管理ツールでは「監査ログ」ボタンまたは同名のタブから検索する。「詳細な操作履歴」と「通常読み取りの集計」を切り替え、検索語・DB・利用者ID・UTCの開始終了時刻・操作種別・結果を指定して「検索」を押す。日時の書式は`yyyy-MM-dd HH:mm:ss`、空欄は制限なし。DBと利用者IDは完全一致、検索語は複数語AND条件である。条件入力だけではサーバーへ要求しない。

結果は最大100件ずつ入れ替えて表示する。「続きを取得」でカーソルを引き継ぐ。0件でも「続きがあります」と表示されていれば続きを取得できる。条件を変えたときは古いカーソルの継続を止め、「検索」で取得し直す。行を選ぶとメソッド名・シグネチャ・コード版・権限版・操作ID等を詳細表示する。検索中は「検索を中止」を利用できる。接続先を変更すると以前の接続の結果・詳細を消去する。監査タブを開いても編集中のソース・下書きは変更しない。

全通信入口の自動監査、通常読み取りの5秒集計バッファはまだ接続していない。集計画面に表示するのは実際に保存された件数であり、まだ収集していない件数は補わない。旧履歴にない権限版やメソッド情報も補わない。

新しくビルド・公開したクエリでは、公開／依存再ビルドの履歴に元の公開メソッドの名前空間・型・名前・シグネチャと、DLLのMVID・MethodDefトークンを自動で付ける。生成された通信入口の名前を利用者のメソッド名として表示しない。LINQのクエリ式・拡張メソッドを含む式から、定数値・補間文字列・コメントを除いたテンプレートも作る。テンプレートは通常の計画APIへ追加せず、監査のソース閲覧権限に従って返す。大きなテンプレートは省略を明示する。既存のDLLを読むだけでは監査情報を追加・書換えせず、情報がない旧版は従来どおり利用できる。実行要求ごとの新しい解析・スタック走査・監査情報の読取は追加していない。

同じDBの複数のスキーマ変更を最終形へまとめて移行する。名前変更・型変換・初期値・削除は版付きの移行定義に記録し、直接C#を変更した場合も対応付けを確認する。削除を含む適用は明示確認と旧世代バックアップを必須とする。依存する公開済み問い合わせも新しい定義でコンパイルできる必要があり、別の問い合わせ下書きを自動採用しない。ネイティブ外部キーは通常更新でも取引の最終状態で検査し、自己参照・循環参照を許容する。自動連鎖操作、複数DBの原子的な一括公開は対象外。適用成功後のデータ復元は別の復旧操作として扱う。

## 静的計画スキーマ1

FLinqの生成に成功したインターセプトメソッドへ`ExecutionPlanAttribute(int schemaVersion,string data)`を出力する。Debug/Release共通。属性はPEメタデータから読み、アセンブリをロード・実行して抽出しない。

- `SourceOperator`: 元のLINQ呼出しと接続、ファイル・文字列範囲。
- `Fragment` / `PipelineStage`: 実際に生成したFragmentと採用した段。選択済みの方式・終端・融合計画を記録し、候補のAggregateCandidateは採用済みとして記録しない。
- `LoweringDecision`: 生成器が記録した方式判断。
- `RuntimeCondition`: 生成本文に実在する分岐条件。どの分岐が実行されたかは含まない。

計画にはメソッド識別子、生成器版とモジュールMVID、元メソッドと生成本文のハッシュ、最適化履歴を保持する。ソース対応がメソッド単位しか分からない場合は`SourceMapping=Method`、不明な場合はUnknownと明示する。方式を推測したり、行数・時間・メモリ・コストを捏造したりしない。元の演算子と物理Fragmentは異なるノードとして表示する。DOTも同じノード・接続を出力する。

既存生成器にはstaticクラスなどインターセプト対象外の制約がある。GUIの雛形は対応する非staticクラスのpublic static入口を使う。計画のない定義は、その旨を表示する。

## 検証の範囲

WindowsのDebug/Releaseビルドと管理契約の試験を実施する。TLS試験では利用者が許可した一時的なWindowsユーザー暗号鍵を作成し、finallyで削除と不存在を検査する。証明書ストアへの登録は行わない。この比較環境の作業・データ・キャッシュ・ログはFへ出力する。

`-WithUI` は共通Webホストのビルドを先に行い、管理契約の試験を実行する。HTTP取得、実ブラウザーの描画・操作、配布パッケージの導入は、それぞれ実行した証跡で確認する。

Linux用のCIジョブは`.github/workflows/management.yml`にある。このローカル環境には実行可能なLinuxエンジンがないため、Linux実行結果は未検証。容量不足・全停止点・巨大スキーマ・すべてのFLinq/SQL/DataGrip経路を網羅した製品受け入れも未完了。二次索引、実測計画、NUMA、他プロセス分析、多DB変換・転送はこの管理モジュールには含めない。SQL定義変換・転送・比較は`tools/sql-migration/README.md`の独立した機能を使用する。

### 移行元の検証記録

以下は旧WPF版の受入証拠である。現在のブラウザー版の配布条件と合格範囲は冒頭の設計文書に従う。

### 2026-11-03の検証結果

| 対象 | 結果 | 証跡ディレクトリ |
|---|---|---|
| 管理API・実TLS・GUI内部操作（Debug） | 51件成功 | `uiwalk-fixes-debug-Debug-042ca2c88d6f4bc1854bc5415eb3ae58` |
| 管理API・実TLS・GUI内部操作（Release） | 51件成功 | `uiwalk-fixes-release-Release-781b30086af7422086b7e3c92cbc6fd5` |
| 既存DB取引（Release） | 57件成功 | `schema-transactions-regression-Release-8a79ac9beef34f94a5da0bf05674ba01` |
| 共通取引・履歴（Release） | 115件成功 | `schema-transaction-core-Release-2115c45dbfda4c7a9c7b7cfc9b74860a` |
| SQL制約・DDL・ネイティブ取引の選択回帰（Release） | 9件成功 | `schema-gateway-short-Release-33f92cb0d39145c3b955c8e000280da0` |

基点は`F:/CodexTemp/SeamlessManagement-20261102`。各ディレクトリにTRX、標準出力、PID・生成時刻・CPU affinityを含むreceiptを保存する。GUI内部操作では、空DBの作成、C#プレビュー、下書き・検証と稼働版の分離、表の公開・一覧表示を実行し、`ui-plan.png.designer.png`と`ui-plan.png.created-db.png`を保存した。OSのマウス・キーボード操作やIDEのF5操作を自動実行した試験ではない。

移行テストにはメモリ／ストレージ、名前変更・型変換・削除、スキーマ配下の表、複合キー・自己参照・三者循環、親子の同時キー変更、楽観取引の競合、全表取込、途中の例外・取消、資源予算不足、キー衝突、バックアップ失敗、公開前後の停止・復旧、旧形式ハッシュの互換性を含む。すべての資源不足や全停止点の網羅を意味しない。

SQLの選択回帰では既存の`System.Security.Cryptography.Pkcs 9.0.13`へのNU1902/NU1903警告が出る。今回その依存パッケージは変更していない。Linux用実行環境はDocker用WSLのみが停止状態で、Dockerエンジンにも接続できず、Linux上の実行は未検証。

配布ZIPは`Publish.ps1`がFの一意な`distribution-<id>`へ作成する。各パッケージの`distribution.json`にファイル長・SHA-256・対象RIDを記録する。対象OSの.NET 10 ASP.NET Core Runtime、GUIにはWindows Desktop Runtimeが必要。Linux向けpublish成功をLinux上の実行成功とは数えない。

### 2026-11-04の検証結果

現行ソースから作成した配布ZIPは `F:/CodexTemp/SeamlessDeliveryCurrent-20261104/distribution-a75132cd25f649789c72b653e9f64178` に保存した。3種を展開してマニフェストのファイルサイズとSHA-256を照合し、Windowsの展開版同士でIPC・TCP/TLS、システムDB取得、正常停止を確認した。CLIの初期設定・バックアップ・復元・パスワード復旧も隔離領域で確認した。証跡は同じ基点の `package-test-logs-e28a45b277964c438c58a8787db12902` にある。Linux ZIPはビルド・展開・照合まで確認した。

同じ配布版のWindows ZIPを展開し、SDKを含まない隔離 .NET 10.0.12 ランタイムでManagerを先に起動した。起動待ちからIPCでの自動接続に進み、空DBのコンパイルと公開が成功した。両プロセスのホストログで隔離ランタイムの使用を確認し、停止後には接続情報ファイルが削除された。証跡は同じ基点の `runtime-mrc1d7a40c` に保存した。

ソースからビルドしたWindows Managerとサーバーの実接続では、Debug・Releaseともに読み取り集計あり・なしの2件が合格した。画面シナリオには実行計画、下書き復元・再接続後の統合、監査、閲覧権限、行編集が含まれる。TRXと画像は `F:/CodexTemp/SeamlessManagementCISmoke-20261104/full` に保存した。GitHub ActionsではWindowsの画面試験をサーバー試験の後に実行する構成とし、ホスト上での実行結果は未確認。

画面試験と分けた管理サーバーの回帰は、Windows Debugで274件、Windows Releaseでテストの並行上限を4に指定した実行で274件が合格した。以前のRelease実行で観測した公開直前の強制終了後の共有違反と初期設定試験の停止待ちタイムアウトは、この比較条件では再現しなかった。証跡は `F:/CodexTemp/SeamlessManagementCISmoke-20261104` の `server-debug-serial`、`server-release-four-collections`、`server-release-diagnostic` に保存した。CIのサーバー試験にも同じ並行上限を指定した。

基準更新の中断復旧を含む更新版は `F:/CodexTemp/SeamlessManagementDelivery-20261104/distribution-ac69e14ff2164bd1ab2a5dc5d294b368` に生成した。3つのZIPを展開し、マニフェストのSHA-256とサイズ、展開先のWindowsサーバーCLIを確認した。配布試験の作業ルートとログルートは同じF上のフォルダーを指定できる。実行結果は同じルートの `package-test-logs-d0942d05962941c3a3efb298271ac1dc` に保存した。

ZIPから展開したWindows Managerを、テストプロセスが起動した隔離サーバーに接続し、WPFアプリ内の操作でDB作成、基準更新と中断復旧、表・行編集、閲覧権限を確認した。Releaseの実接続シナリオ1件が成功し、証跡は `F:/CodexTemp/SeamlessManagementDelivery-20261104/package-ui` に保存した。Linux版の実行、IDEの同時起動、OS入力、SDKなしの環境はこの試験の範囲外。

接続確認を追加したWindows ManagerのZIPは `F:/CodexTemp/SeamlessManagementDelivery-20261104/distribution-bd7e2ce43868433e9980e3b3a5db5252/WindowsUI.zip`。展開したWindowsサーバーとManagerの組み合わせで、ローカルIPCのOS利用者認証とTCP/TLSのログインを別々に実行し、システムDBの取得を確認した。サーバーを正常停止し、接続情報ファイルの削除を確認した。証跡は `F:/CodexTemp/SeamlessManagementDelivery-20261104/packaged-pair-73a2b7ead859430f9a9af00047cb6017` に保存した。

Visual Studioの両 `.slnLaunch` とVS Codeの同時起動設定が同じ二つの `.csproj` を参照することを照合した。各 `launchSettings.json` の起動引数を使い、F上の隔離ワークスペースにある共通設定を配布版のサーバーとManagerから読み取った。相対パスで指定した管理領域と証明書、IPCとTCP/TLSによるシステムDB取得、正常停止を確認した。証跡は `F:/CodexTemp/SeamlessManagementDelivery-20261104/workspace-launch-853904995bcf4201b8a1dc5399cfaa89`。CLI起動とWPFアプリ内の操作で検証しており、IDE画面からの同時起動操作は未検証。

Managerを先に起動する順序も、展開したサーバーとManagerで検証した。Managerの待機表示を描画し、接続情報ファイルがないことを確認してからサーバーを起動した。接続情報の定期確認により同一OS利用者のIPCで自動接続し、システムDBを取得した。サーバーの正常停止も確認した。証跡は `F:/CodexTemp/SeamlessManagementDelivery-20261104/manager-first-936499f2b2ec4b45b336fd78880fa220`。追加試験入りのManager ZIPは `F:/CodexTemp/SeamlessManagementDelivery-20261104/distribution-0801827a5e184d43aec839db79ce0c03/WindowsUI.zip`。

SDKを含まないF上の .NET 10.0.12 ランタイムへ起動先とPATHを限定し、`dotnet --list-sdks` が空であること、両プロセスのホストログに隔離ランタイムのパスがあることを確認した。ManagerのIPC接続後に空DBを作成し、サーバーでのコンパイル・公開と稼働カタログへの反映を確認した。試験記録は `F:/CodexTemp/SeamlessManagementDelivery-20261104/runtime-only-c29e7fd681084d93a834bab6655e167d`、追加試験入りのManager ZIPは `F:/CodexTemp/SeamlessManagementDelivery-20261104/distribution-a9fe46f9b8ed4eb58d953e910b1d8db8/WindowsUI.zip`。

Windows配布版のランタイム不足時の診断を、基本の.NETランタイムだけを置いた隔離ホストで確認した。Managerは `Microsoft.WindowsDesktop.App`、サーバーは `Microsoft.AspNetCore.App` の10.0 x64と対応する導入URLを標準エラーに表示し、起動を中止した。試験時はホストのエラーダイアログを無効にした。証跡は `F:/CodexTemp/SeamlessManagementDelivery-20261104/missing-runtime-a26820c6a7a44ea28b132198b8a37e54`。

`package-test-652d9b24aa9942c59483bb7546c9bcdb`で全配布物のSHA-256照合と、隔離した新規領域に対するWindows CLIのhelp/init/backup/restore/reset-password/setup-local、環境変数による設定取得を確認した。

配布試験の`environment`は、環境変数から指定した不存在の証明書で起動が拒否されることを確認する負例である。その子プロセスのreceiptは終了コード1・Failedとなるが、試験全体では期待した拒否として扱う。正常なTLS接続は上記管理API試験で別途確認した。
