# WSL2上のLinux実行検証

## 環境

Windows再起動後、Ubuntu 24.04 LTSをWSLの公式配布入口から導入した。保存先は `F:\Seamless\linux-20261108\Ubuntu-24.04`。既存のdocker-desktopディストリビューションと以前のダウンロード済みイメージを保持する。

実起動でUbuntu 24.04.5 LTS、Linux 6.18.54.1-microsoft-standard-WSL2、x86_64、8仮想CPU、`/mnt/wslg`の存在を確認した。管理画面は共通Browser Managerを使う。

Microsoftの公式dotnet-install.shでSDK 10.0.403を `/opt/seamless/dotnet` へ導入し、dotnet --infoを実行した。Windowsの検証SDKと版を揃える。LinuxのSDKと一時領域はFドライブ上のディストリビューション内に置く。

## ビルドと証跡

ソースは `/mnt/g/GitHub/Seamless` を参照し、出力は `/mnt/f/Seamless/linux-20261108/artifacts`、NuGetキャッシュは `/var/tmp/seamless/nuget` へ分離する。Windowsの成果物を上書きしない。Linux側はtasksetの0〜7とDOTNET_PROCESSOR_COUNT=8を使う。

保存先 `F:\Seamless\linux-20261108` のubuntu-install、first-start、sdk-installに起動結果とreceipt.jsonを保存する。setup-sdk.shとbuild-server.shが実行入口である。

## 受入範囲

UbuntuとSDKの起動、サーバーのLinux Debug・Releaseビルドを確認した。両構成で警告・エラーは0件。Linuxのローカル接続情報試験2件、変更した接続情報と管理ホストのWindows回帰はDebug・Release各9件が成功した。

実起動でlibaio.so.1とliburing.so.2の不足を確認し、Ubuntuのlibaio1t64・liburing2を導入した。通常のCLIでLinuxユーザーkazuhのサーバーを起動し、同じユーザーの所有者IPCからManagerの起動セッションを発行した。WindowsからWSLへのHTTP確認では未認証401、セッション交換204、管理ページ・CSS・スクリプト200、不正Origin403を確認した。証拠は同保存先のmanager-http-verification.json。画面上の対話操作は別途確認する。

DB作成・編集・ビルド成果物の取り込み・適用・再起動、業務用IPCとTLS、Linux配布物全体の受入は未検証として残す。

## Ubuntuデスクトップ全体の表示

利用者の選択により、Ubuntu内へXfceとxrdp・xorgxrdpを追加した。Windowsのリモートデスクトップで `127.0.0.1:3390` へ接続する。xrdpはIPv4ループバックだけで待ち受け、TLSを使用する。rootログインを無効にし、ユーザーkazuhがUbuntuで設定したパスワードを使う。既存設定は変更前にバックアップした。

初回インストーラーのWindows側実行枠は1200秒でTimeoutとなったが、Linux側の元のapt処理は続いていた。PID・開始クロックを照合して同じ処理の終了を待ち、dpkg監査と対象パッケージの設定完了を別工程desktop-state-verificationで確認した。元のTimeout記録を保持する。desktop-configureではサービス起動、Windowsからの接続確認ではTCP到達を確認した。computer-useによる起動はアプリ承認待ちがタイムアウトした。

初回ログイン後の黒画面では、Xorgへの接続と認証は成功したが、Xfce配下の各部品がDISPLAY=wayland-0で失敗した。WAYLAND_DISPLAYを削除してもGTKは既存のWSLgソケットを自動選択していた。同じXorg上のGTK最小実験で自動選択時wayland-0、GDK_BACKEND=x11指定時:10.0を確認した。ユーザーの.xsession内へこの指定を追加し、旧セッションの終了を確認して接続ウィンドウを一つだけ起動した。11月9日00時03分の再接続後、xfwm4・xfce4-panel・xfdesktopの起動と、利用者の画像でデスクトップ表示を確認した。ログと比較実験はgtk-default-display・gtk-x11-display・desktop-x11-fixへ保存する。

Google公式配布のChromeと日本語フォントfonts-noto-cjkを導入した。Chrome 157.0.8103.38の実行と日本語フォント解決を確認した。Ubuntu標準のGNOMEと異なるデスクトップ環境としてXfceを選択した。ブラウザーの対話操作は未検証。

再接続用ファイルは `F:\Seamless\linux-20261108\Ubuntu-desktop.rdp`。`Open-UbuntuDesktop.ps1` はUbuntuを起動してからこの接続ファイルを開く。Ubuntu側の `~/Desktop/Seamless.desktop` は `~/.local/bin/seamless-server` から通常のserve CLIを起動し、同居Managerをブラウザーで開く。検証用サーバーはstop-fileの正規経路で正常停止し、kazuh-server-runのPassedと終了を確認した。起動アイコンの構文・デスクトップファイル妥当性は確認済みで、アイコンからの対話起動は確認待ち。ログインパスワードは保存しない。

利用者の「ここで実行を見たい」という指定により、Xfce画面へ実行ターミナルを開き、Linuxの.NETからサーバーを起動した。確認時PID22017がManagerの127.0.0.1:38699と管理TLSの127.0.0.1:42679を待ち受ける。DBは /home/kazuh/.local/share/Seamless/manager に置く。同じ画面の専用Chromeで認証済みSeamless Manager・DB一覧0件を表示し、利用者の画像とウィンドウ名で確認した。専用検証プロファイルは .cache/Seamless/browser-manager、ローカルの検証用CDPは9227。検証終了時にこのデバッグ入口を閉じる。通常Chromeの個人用プロファイルは操作対象へ含めない。DB作成以降の対話操作は未検証。

## 正式業務RPCのLinux確認

正式生成クライアントのIPC・TLS代表12件に続き、NativeRecordTransportTests・NativeTransport・接続ポリシー・接続プールのDebug・Release各102件が成功した。Gateway有無、別PIDの生成クライアント、取消、先行2行の保持、再接続・同じ接続の継続、資源返却を含む。証跡はnative-owner-Debugとnative-regression-Debug・Release。

管理ホスト・IPC・認証付きTLSの101件は、11月9日の修正後Linux Debugで101件成功した。初回43失敗のうち41件はOIDC試験発行元のWindows専用パス検査によるもので、Windowsの制限を保持してLinuxの明示された絶対パスを許可した。残る失敗では、完了した要求の取消トークンを接続の寿命通知として待っていた試験と、Linuxの子プロセス開始UTCの完全一致を修正した。要求トークンの公開契約を維持し、実接続から切断後のロールバック・キー再利用・再起動後の保持を確認する。アイドル取引の期限と緊急無効化の関連2ケースも同じ観測へ揃えた。

修正前の2失敗は`F:/Seamless/linux-desktop-recovery-20261109/native-management-red`、修正後4件は`native-management-green`、同じ101件全体は`management-host-green`に保持する。Releaseも元の101件へソース解析・破棄17件を加えた118件が成功した。全体はxUnitのコレクションを逐次実行し、systemdの専用スコープにMemoryMax1400M・MemorySwapMax256Mを適用した。試験環境の上限と製品の資源会計は別に評価する。

11月9日にXfce・systemd・DBus・xrdpを再開し、修正後の共通画面をLinux Chrome内に表示した。さらに隔離した固定ReleaseホストでGUIのDB作成・テーブル編集・専用プロジェクト生成、Ubuntu SDKのDebug・Releaseビルド、成果物検査・取り込み・明示適用、行追加・更新、DB名変更、正常再起動後の保持を確認した。継続編集後の再適用でも行を保持した。正式配布物と残る全GUI操作・障害条件は継続する。[Linux GUIの条件と証拠](20261109-linux-manager-acceptance.md)

## Linuxのローカルプロセス検出

初回のLinuxサーバーはPID350として稼働し、公開開始日時は10:47:31.7155656 UTCだった。別の2プロセスでは同じPIDの開始日時を約9.95秒後の値として取得した。カーネルのboot_idとstatの開始クロック15760は一致した。既存のUTC日時の完全一致により、稼働中サーバーの検出が失敗することを確認した。

Linuxで公開する接続情報に、カーネル起動ID・プロセス相対開始クロック・公開日時を結び付けたProcessIdentityを追加する。読み取り時は現在のカーネル情報から照合し、PID再利用と日時の書き換えを拒否する。Windowsと旧形式の接続情報は既存の日時照合を使う。DB保存形式は同じものを使う。[procのstarttime](https://man7.org/linux/man-pages/man5/proc_pid_stat.5.html)

導入方法は公式資料へ合わせる。[Ubuntuへの.NET導入](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install) [dotnet-install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)

## 2026年11月11日の正式SDK候補

同じ953ソースをWindowsとUbuntuのSDK10.0.403でそれぞれRelease publishした。Linux出力は独立したDLL群で、Windowsのportable DLL再利用と区別する。18 ProjectReferenceのビルドは警告0・エラー0、配布199ファイルと元UI asset／manifestを照合した。Linux CLI setup-local→serve→同一OS所有者IPC managerと専用Chrome157で、DB作成・公開v1・同じブラウザーのF5後の選択保持を確認した。さらに、Linux製の製品DLL55個を参照して同じ87件の近接試験をLinux SDKでビルド・実行し全成功、原本199ファイルは不変だった。所有scopeはinactive、残存PID0を確認している。

実行時のTsavoriteはMicrosoft.Garnet2.2.8のDLLである。導入済みUbuntu libaio1t64に対し、既存resolverは書込み可能なアプリ配置へlibaio.so.1の互換リンクを生成し、Nativeデバイスの4096バイトwrite/read/closeが成功した。単独lddの「libaio.so.1 not found」だけで未導入と判断しない。ローカルGarnet HEADは実配布DLLとdefault device・callback署名が異なるため、今回の契約は実ロードDLLの型・SHAと動作を根拠にする。

書込み禁止の独立配置では、NativeStorageDeviceの作成後、最初のwriteがDllNotFoundExceptionとなった。アプリローカルのリンク作成権限が原因で、OSパッケージは導入済みだった。別の配置で互換リンクを準備してからディレクトリ0555／ファイル0444へ変更すると、同じNative write/read/closeが成功した。配布時の準備は対象Linuxホスト上で行い、グローバルリンクは変更しない。正式候補と稼働中の固定Managerへこの実験を適用していない。

Linux配布ZIPには`Prepare-LinuxNativeCompatibility.sh`を同梱する。展開したServerLinuxディレクトリ内で、書込み禁止にする前に対象ホスト上で次を実行する。スクリプトはLinux x86_64のELFと必要なsymbol versionを検査し、既に解決済みなら変更せず終了する。libaio1t64が必要な環境ではアプリローカルの互換リンクだけを排他的に準備する。既存のregularファイル・danglingリンク・異なるtargetは上書きせずエラーにする。

```bash
bash ./Prepare-LinuxNativeCompatibility.sh "$PWD"
```

書込み禁止・未準備の具体的な失敗、書込み可能時の準備、read-onlyでの繰返しno-op、既存競合3形状の拒否と、準備後のread-only Native4096バイトwrite/read/closeを実スクリプトで確認した。再実行とI/O後も互換リンクのinode・ctime・mtimeは不変で、正式原本とOSライブラリーは変更していない。シェルはLFをGit属性で固定する。Windowsのcross-publishはスクリプトをmanifest・ZIP作成前に同梱し、ビルドホストでLinuxの互換リンクを生成しない。新スクリプトを含む全体の再publish／ZIP配布受入は別工程とする。

確認証拠は`F:/Seamless/manager-formal-publish-20261111/linux-r2`の`summary.json`／`cli-gui-r3/summary.json`／`libaio-summary.json`、87件は`F:/Seamless/function-result-inference-20261111/linux-built-formal-r2-near-r1/result-summary.json`、書込み禁止の対照は`F:/Seamless/formal-r2-readonly-native-20261111`へ分けて保存する。この正式Linux候補による代表関数のSDK Release検査・取り込み・明示適用・正常再起動も、独立した新規DBで後続確認した。正式feed12pkg／13DLL・SDK依存・実ロード6 DLLをSHA照合し、Windows最終とbyte一致するソースから、GUI編集・保管と通常library／C#14／ReleaseのSDKビルドを通した。公開v2で4／5の2行、IEnumerable<long>、元本文・コメントを表示し、SDK DLL FA57E26Fと実世代module.dllが一致。公開定義全体とArtifactHash86B2F761は正常再起動後も保持した。APIによる初期DB／SourceProject準備と後続のGUI操作を区別する。125ファイルの全SHAバックアップと所有host／Chromeのexit0・scope inactiveを確認した。証拠は`F:/Seamless/formal-r2-sdk-acceptance-20261111/linux-r1/summary.json`と`gui-proof.json`、`execution-proof.json`。旧DB互換、全GUI機能、他CPUアーキテクチャと固定URL切替は継続する。

### 正式r3の独立Linux SDKと再起動確認

if条件と文の寿命修正、中立な未適用案内を含む971ソースのmanifest2AC25354をWindowsと共有し、Ubuntu SDK10.0.403で18 ProjectReferenceを独立Release publishした。警告・エラー0、配布200ファイル、UI元asset18件／200 endpoint、英語衛星、同梱のLinux準備スクリプトを照合した。Linux製のCore29D6／UIF478／Server3372／DB7D18／DB生成器2CB8／FLinq9434と、Windows製r3 DLLは別候補とする。全.soは16ファイルで、うちlinux-x64は5ファイル。互換リンクの準備は専用runtimeコピーだけに適用し、正式原本は変更していない。

Linux製品55DLLを参照する管理87件と条件／取引9件は、Linux SDKでbuild警告・エラー0、全96件成功。条件試験の初回はハーネスに古い試験DLL名が残りvstest起動前に停止した。同じビルド済みConditions.Tests.dllへ実行パスを訂正し、buildを再利用して9件を確認した。成功したTRXの実行数・結果は親側でも直接再照合した。Windows正式r3も同じ96件を独立確認している。

Linux正式feed12パッケージ／13DLLで、通常library／C#14／ReleaseのSDK成果物を作成し、実Linux ChromeからC#・構造・引数の編集と下書き保管、検査・取り込み・変更確認・明示適用v2を確認した。結果4／5、IEnumerable<long>の読取専用表示、元本文・コメントを保持した。ConditionValues(long)のSum／CountがSDK生成本文で2個の実local functionと元位置の条件呼出しになり、埋込PlanのMethodKeyとも対応する。SDK DLL5B75293Dと実世代module.dllが一致し、ArtifactHashD58C3122・公開Definition全体は正常再起動前後で保持された。125ファイルの停止後SHAバックアップと所有host／Chromeのexit0・全scope inactiveを確認した。初期DB／SourceProjectはAPIで準備し、GUIで行った工程と区別する。Windowsの最終ソースとは改行正規化後に一致し、byte一致とは扱わない。

証拠は`F:/Seamless/manager-formal-publish-20261111/linux-r3/summary.json`、`near-management/result-summary.json`、`near-conditions/result-summary.json`と`F:/Seamless/formal-r3-sdk-acceptance-20261111/linux-r1/summary.json`、`condition-helper-selection-proof.json`へ集約する。旧DB互換、全ZIP配布、他CPU、物理IMEと固定URL反映は継続する。

同じ正式Linux r3の別隔離DBでは、既存Itemsの1/10・2/20へ左ツリーからNotes/string?と移行初期式 `"initial"` を追加し、構造／C#往復・保管・SDK Release・GUI検査／取り込み／明示適用v2を通した。追加列の両行はinitial、SDK DLLと世代moduleの47FEE9ECが一致し、公開定義全体とArtifactF6CDCC71、全行は正常再起動後も保持された。147ファイルを全SHAバックアップし、実ロード6DLL・feed13・正式原本200の一致と全所有scope inactive／残存0を照合した。初期DB／行とプロジェクトexportはAPI、後続の列編集・適用・行確認はGUI、SDKビルドはCLIと分ける。初回は移植ハーネスのWindows proofパスによりGUI開始前に待機し、元証拠を残してLinuxの専用proofへ修正した。新規行の既定値は未対応の案内を維持する。証拠は`F:/Seamless/formal-r3-column-acceptance-20261111/linux-r1/summary.json`、各GUI proof、`execution-proof.json`。
