# Linux Browser Managerの正常系確認

## 候補と環境

Ubuntu 24.04.5、WSL2の8論理CPU・メモリ4GB、Xfce・ローカル限定xrdp、専用のLinux Chromeで確認した。サーバーはUbuntu内の.NET 10.0.403で動作し、Kestrelと管理・DB処理を同じPIDで実行した。

固定配置は`F:/Seamless/linux-desktop-recovery-20261109/runtime-release-ui-feed`。Linuxでは`/mnt/f`から参照する。ビルド出力から分離してpublishし、実行中DLLの書換えを避ける。管理領域は既存利用者DBと分けた`/home/kazuh/.local/share/Seamless/acceptance-20261109`で、WSLのFドライブ上の仮想ディスクに格納される。

共有の`Work/VirtualDbFeed`内のSeamless.Database.dllと現在のLinuxサーバーのDLLはSHA-256が異なっていた。検証用の`current-release-feed`へ現在のRelease成果物から12パッケージを作り、同梱アナライザーを含む13個のDLLを固定候補と照合した。既存のフィードとNuGetキャッシュは変更していない。検証用の専用キャッシュを用い、第三者パッケージは既存Linuxキャッシュを復元のフォールバックへ指定した。

## ブラウザーで通した操作

1. DB一覧0件から`LinuxAcceptance`を作成した。
2. 共通編集グリッドで`Items`を作り、`Id:int`を主キー、`Price:decimal`を値列にした。下書きを保管した。
3. 専用C#プロジェクトの保存をGUIから実行した。保存先は特殊文字を含む`Linux Project & Source`とした。名前空間UserCodeと二つのC#ファイルを表示した。
4. Ubuntu SDKでDebug・Releaseの両構成をビルドした。警告・エラーは両方0件だった。
5. ManagerでReleaseを選択し、DB下書きへ明示反映した。DLL・PDB・ソース・19依存DLLの検査後に取り込み、変更確認・明示適用でv2へ公開した。
6. データ画面からキー1・価格125.5の行を追加し、150.75へ更新した。再読込後の入力値をDOMで照合した。
7. 二つ目の`LinuxAcceptanceSecond`を作り、左ツリーメニューから`LinuxAcceptanceRenamed`へ変更した。新規名入力のEsc取消と、空欄のフォーカス移動取消を確認した。
8. 正常停止ファイルを使って検証用サーバーの終了を待ち、同じ管理領域を別PIDで再起動した。PID14724から17209への変更、公開版v2、キー1・価格150.75、変更後DB名の保持を確認した。
9. 専用プロジェクトを再度開いてコメントを追加・同期し、DB下書きへ反映した。保存済みの同期基準v1は、この操作で最新公開版v2へ更新された。コメントを保持して再ビルド・再取り込み・v3への明示適用が成功し、価格150.75の行を保持した。

再オープン時の構成は既定のDebugだった。Release DLLとの条件定義の不一致を検査が拒否することを確認し、画面でReleaseを選択してDB下書きへ反映すると成功した。古い基準表示だけから継続編集の失敗を判断せず、既存の公開版整列処理を通常操作で確認した。

## 実行・操作証拠

証跡ルートは`F:/Seamless/linux-desktop-recovery-20261109`。GUI操作の判定はDOMの役割・ラベル・文字列・入力値を用いた。画像はManagerの対象ページだけを取得した。

- `linux-acceptance-before-restart.json`と`linux-acceptance-before-restart.png`
- `linux-acceptance-after-restart.json`と`linux-acceptance-after-restart.png`
- `gui-project-build`と`gui-project-rebuild`
- `current-release-feed-hashes.json`
- `acceptance-owner.json`、`acceptance-restart`、`acceptance-server.stdout.log`
- `package-feed-red`、`package-feed-green2/Debug`・`Release`
- `ui-feed-final2/Debug`・`Release`

フィード解決の近接試験は修正前3失敗・1成功、修正後の保存・同期21件が両構成で成功した。型補完・作成画面・ソース解析・ブラウザー破棄の関連30件も両構成で成功した。既存の反射試験が現在の任意引数を渡していなかったため、試験呼出しを修正して再確認した。

## OS所有者の接続確認

固定ReleaseサーバーPID17209を継続利用し、既存のkazuh（UID1000）とnobody（UID65534）を別プロセスから実行した。OSの権限、管理ロール、サーバー設定は変更していない。

- 公式の`manager --root --launch-file`で、kazuhは起動用セッションを取得した。nobodyは管理領域の探索段階で拒否され、起動用ファイルを作らなかった。
- 管理領域の探索を介さず、既知の実IPCソケットへ接続して`localIdentity`を要求した。kazuhは本人として応答を取得した。nobodyはOSの`EACCES`で接続を拒否された。直後のkazuhの要求も成功した。
- ソケット所有者UID1000、モード0755を読み取りで確認した。この別UIDの試験はソケット接続前のOS拒否であり、接続受理後の資格情報検査を単独で検証した結果へ広げない。
- 証跡は`owner-access-probe3/owner-access-result.json`と`owner-ipc-peer-probe/result.json`。初回の試験ハーネスは長いTMPDIRがUnixソケットの108バイト上限を超え、次の試行は起点URLの区切り文字の比較で失敗した。実サーバーと同じ一時ディレクトリと、URIの起点・パスを分けた比較に修正して確認した。製品の認可処理は変更していない。

## 名前変更の応答欠落と再接続

固定Releaseの隔離DBを専用Linux Chromeで操作した。ブラウザーの実WebSocketに登録したmessageリスナーで描画応答の配信を止め、別ページから名前変更の確定を確認した。元ページでは名前入力が残り、応答が3フレーム欠落した。接続を閉じて再接続すると、同じページに変更後の名前が一件表示され、入力も終了した。画像は`rename-browser-recovered.png`、DOMと条件は`rename-browser-recovery.json`に保存した。

最初のPlaywrightによるWebSocketルートでは欠落フレーム0件だったため、その通常の名前変更を障害試験の合格へ数えていない。ページの実WebSocket上で欠落を観測した試行だけを採用した。別のCDP接続を作る画像保存はタイムアウトし、既存の専用ブラウザー接続から画像を取得した。全ブラウザーの終了や個人用プロファイルの操作は行っていない。

試験後に所有者の10分セッションが失効したため、公式のOS所有者CLIで開き直し、検証DB名を`LinuxAcceptanceRenamed`へ戻した。再読み込み後も名前一件を確認し、試験用の追加ページを閉じた。既存利用者DBと下書きは変更していない。同じ操作IDの再送・再起動後の履歴一件は、別の管理IPCの近接試験で確認した。

## 継続する受入

### Linux Chromeの表示言語

Chrome本体の表示言語はLinuxのロケールに従い、Windows版の「この言語でGoogle Chromeを表示」はLinux版で提供されない。ManagerはHTTPのAccept-Languageから日本語・英語を選ぶ。[Googleの説明](https://support.google.com/chrome/answer/173424?hl=ja)

利用者の言語設定変更の依頼で、Chromeの設定とセッションをバックアップして正常再起動した。最初に追加した固定日本語起動指定は、その後の「ブラウザー設定に合わせる」という指示で置き換えた。現在は`~/.local/bin/seamless-chrome`が通常プロファイルの`intl.selected_languages`、未指定時は`intl.accept_languages`を読み、日本語または英語の起動言語へ接続する。ユーザー用の`~/.local/share/applications/google-chrome.desktop`から使用する。優先言語自体を書き換えない。Chrome本体への反映は終了後の通常起動で行う。Managerへの反映はブラウザー設定変更後のページ再読み込みで行う。

選択処理5条件と、実プロファイルの英語優先・起動引数en-USの一致を確認した。再起動後もChromeのウィンドウは一つで、同じLinuxサーバーのManagerを表示した。バックアップの場所と起動・照合は`chrome-ui-language-rollout.json`へ記録した。

後続の隔離LinuxヘッドレスChromeで、設定画面のAdd languagesから日本語を追加し、Move to the topで優先順位を変更した。Managerの送信ヘッダー`ja,en-US;q=0.9,en;q=0.8`、応答言語ja、html言語ja、接続後の日本語操作を確認した。設定画面で英語を最上位に戻すと応答とhtmlがenになり、更新済み価格147.25を保持した。ヘッダーの上書きは使用していない。`cohort-browser-proof.json`と`cohort-gui-ja-preferences.png`・`cohort-gui-en-preferences.png`に記録した。この往復はヘッドレスの実設定操作であり、表示中の通常プロファイルを手動変更した確認とは区別する。

この固定配置の検証では旧10分固定のセッションを使い、名前変更時の認証拒否と入力保持、OS所有者CLIから開き直した後の成功を確認した。新しい合意は8時間固定であり、ソースの既定値へ反映した。時計の境界試験と、新候補の配置後の起動を別に確認する。

正式配布パッケージ、既存の通常NuGetキャッシュ、全スキーマ・全ビュー・全関数、外部HTTPSと認可の全組合せ、各障害条件、物理キーボード操作はこの正常系の成功へ含めない。残作業はW03・W04・W09・W11・W13で継続する。
