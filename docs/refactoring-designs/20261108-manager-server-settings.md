# Browser Managerのサーバー設定

## 対象と入口

実施順6の接続・サーバー設定を共通管理APIとBrowser Managerへ接続した。システムの「サーバー設定」またはコマンド検索から開く。管理TCP、ローカルManagerのポート、通信予算、応答送信期限、参照監査、外部HTTPS Managerの起点と待受IPを編集する。通信予算と外部受付は詳細を開いたときに表示する。

IManagementOperationsのServerSettingsAsync・SaveServerSettingsAsyncを、同居、所有者IPC、所有者ブラウザー、認証済み利用者ブラウザー、組み込みの専用DBスレッドへ接続した。サーバーが起動設定のコンテキストを接続した場合にServerSettingsV1を広告する。取得・保存は現在のアカウントの全DBに対するManage権限を確認する。

公開ManagementApplicationHostと標準のProgram.Mainのserve入口が、保存設定を読み込み、各実行形態の管理TCP・Kestrel・通信上限・参照監査の初期化へ適用する。組み込み同期の起動、参照監査の専用DBスレッドへの保存、通常停止の確認は [共通ホスト](20261108-management-application-host.md) に記載する。従来の直接作成したInProcessManagementHostはコンテキストを持たず、対応能力を広告せず、設定APIにUnsupportedを返す。

## 保存と起動の契約

- 保存先は管理ストアのserverSettings・activeとする。設定本体とServerSettingsChanged履歴を同じ既存の取引・WALで確定する。
- 取得値には保存版、稼働設定、次回設定、再起動待ち、起動引数の指定項目、実際のポートとManager起点、PIDを含める。管理領域・証明書・起動設定ファイルは参照情報として表示する。
- 保存要求はExpectedVersionを必要とする。確認から更新まで同じ管理ストアの排他内で行い、古い版はConflictとして拒否する。
- 解決順は明示的な起動引数、保存済み設定、起動ファイルと既定値の順とする。起動引数で固定した欄は読み取り専用にし、APIからの変更も拒否する。一時的な起動引数の値を保存値へ取り込まない。
- 保存内容は次回起動時に反映する。画面では稼働中の値と次回の値を分け、差があれば再起動待ちを表示する。
- IP、ポート、正の通信予算、応答期限、監査期間・保存間隔を検査する。外部ManagerはHTTPSの起点を必要とし、既存の利用者認証・認可を使う。形式5未満の管理領域で参照監査を有効にする場合は、既存の明示更新を必要とする。
- 保存失敗や競合では入力を保持する。別画面へ移って戻っても編集を保持し、取消で直前に取得または保存した値へ戻す。未保存のまま再取得する操作は保存・取消を案内する。

## 実装と検証の対応

契約はSeamless.Management/ServerSettingsContracts.cs、保存処理はSeamless.Management.Server/ManagementService.ServerSettings.cs、起動への適用は同プロジェクトのProgram.csにある。画面はSeamless.Management.Components/サーバー設定ビュー.razorと同名のコードビハインドを使う。

ServerSettingsOperationsの最終候補はDebug・Release各3件が成功した。先行Release候補では既存のLocalServerSetup.Initializeも成功した。その後の変更はRazorの保存版表示とその検査であり、Program.Mainの初期設定処理は同じ候補を再利用する。最終の変更プロジェクトのビルドと試験に警告・エラーはない。

- 共通5入口の取得・保存、能力表示、再オープン後の保存値、セッション失効、現在権限による拒否を確認した。組み込みでは試験用コンテキストを接続し、未接続時のUnsupportedも確認した。
- 不正値と起動引数で固定された項目を拒否し、拒否後に版と値を保持した。同じ版からの同時保存は1件成功・1件Conflictとなった。
- 共通描画試験で保存、未保存の画面切替、競合後の入力保持、取消・最新取得、不正な応答期限の保持を確認した。
- 実際のProgram.Mainを起動・停止・再起動し、Managerポート、送信待ち予算、実行中の参照監査期間と保存間隔、履歴の保存を確認した。CLIのport=0を保存上の14336へ混入させないことも確認した。

WindowsのRelease配布物と正式オフラインバックアップした隔離DBで、ブラウザーの通常操作を確認した。Managerポート51131、送信待ち予算1048576バイト、監査期間120秒・保存間隔10秒を保存し、再起動待ちを表示した。監査期間150秒を未保存のまま画面移動しても保持し、取消で120秒へ戻った。

PID31312を正常停止し、同じ起動引数でPID25888へ再起動した。Managerが保存した51131で待ち受け、送信待ち予算1048576、実行中の監査期間120秒・保存間隔10秒、保存版v1、再起動待ちの解消をDOMと画面で確認した。最初の画面確認で保存版が文字列v@現在.Versionと表示された問題を修正し、両構成の描画試験と再起動後の画面で確認した。

証跡は `F:\Seamless\manager-settings-20261108` のfinal-Debug-tests・final-Release-testsのTRX、release-tests、server-run1・server-run2の終了記録、ready1.json・ready2.json、manager-settings-before-20261108.json・manager-settings-after-20261108.jsonにある。最終候補のハッシュと結果の対応はverification.jsonに記録する。画面は `Work/manager-settings-restarted-20261108.png`。使用したサーバーは通常停止した。

## 継続する範囲

共通ホストへの設定接続は上の記録へ引き継ぐ。外部HTTPS設定の実ブラウザー操作、再起動時のポート衝突・証明書不整合の画面表示、保存中の強制終了による復旧、Linux・正式配布物の全体受入を残す。管理ストアの障害注入はW10へ接続する。共通ロールと割り当てはW06-USERS-ROLESで継続する。
