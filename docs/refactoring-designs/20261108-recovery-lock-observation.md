# クラッシュ復旧ロックの即時試験と所有者観測

## 引き継ぐ観測

W10-LOCKの元の不合格は、子プロセスの強制終了・終了待ち後にmanagement.lockを排他取得できなかったケースである。調査開始前の単独・診断付き全件・Gateway CLIとの並行試験では再現せず、PublicationCrashへIOExceptionを最大10秒再試行する処理が追加されていた。今回その再試行を除去し、逐次反復で共有違反を再現した。現在の試験は終了シグナルを待って一度だけオープンし、成功・失敗の診断を保存する。

## 今回の変更

再試行を除去し、終了待ち後の最初のManagementStoreオープンで判定する。成功・失敗ともPID・作成時刻・終了待ちとオープンの時刻を記録する。失敗時は既存のWindows Restart Manager診断を再利用し、例外の対象ファイルと所有者を保存する。観測の失敗は元の製品例外と区別し、試験を成功に変えない。

子プロセスではManagementStoreの所有ハンドルの継承フラグを読み、準備記録へ保存する。診断用のreflection・P/Invokeはテスト側に置く。既知の別プロセスが保持するロックで所有者PID・作成時刻を照合し、診断器の有効性を確認する。

## 実験と判定

Prepared／Committedの通常試験に加え、回数を明示する専用プローブで即時オープンを繰り返した。継承フラグ・カーネルのシグナル状態・旧条件との比較で終了条件を識別した。修正は試験の終了待ちと観測に適用し、最終候補の全件・配布受入を後続ゲートとして保持する。

## 合意状態

復旧ロックの原因を調査し、再試行による成功と製品修正を区別する既存合意に基づく。

## 再現と原因の識別

再試行を除去した逐次反復で、3回目のPreparedからmanagement.lockの共有違反を再現した。子プロセスは終了コード-1、所有ハンドルの継承フラグ0で、Restart Managerの観測時には所有者を捕捉しなかった。Gateway CLIの並行実行はこの再現に含めていない。

WindowsのWaitForSingleObjectを追加した観測では、実行環境.NET 10.0.14のWaitForExitAsyncが完了した直後も、12回すべてのプロセスハンドルがWAIT_TIMEOUT（258）だった。.NETのHasExitedは終了コードを先に確認する互換経路を持ち、WaitForExitAsyncもこの判定を通る。WaitForExitのWindows実装はプロセスハンドルのシグナルを待つ。[終了判定の実装](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessManager.Windows.cs)、[非同期待機の実装](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs)、[ハンドル待機の実装](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs)

同じ候補で旧条件とハンドル待機を交互に比較し、旧条件は12回中共有違反1回、ハンドル待機は12回すべてで即時オープンと復旧検査が成功した。固定sleepとファイルオープンの再試行は使用していない。元の試験が必要とした資源終了の条件を、カーネルの終了シグナルへ修正する。

## 修正範囲

PublicationCrashの正式な復旧試験は、準備待ちを含む既存の60秒期限の残り時間でWaitForExitを使用し、Windowsではシグナル状態0を追加検査する。旧条件の比較は専用プローブに限定する。即時オープンの成功と失敗の診断を保持する。

同じ終了条件に依存して保存領域を再オープンするAuditDeliveryTests、AuditUpgradeCrash、NativeUpdateAudit、UpdateAuditUpgradeと、AuthorizationPolicyStorageのクラッシュ復旧経路の子プロセス待機もハンドル待機へ揃える。AuthorizationPolicyStorageの通常CLI初期登録経路には非同期待機を維持し、後述の独立正常終了観測とクラッシュ復旧経路を区別する。通常の接続情報の生存判定と、製品の保存形式・ファイル共有条件は維持する。残るプロセス利用箇所と全体受入はW10の後続確認へ接続する。

## 修正後の検証結果

Debugの関連48件が成功した。Releaseの関連48件は、保存先の更新が継続したまま実行側の480秒制限で打ち切られた。結果TRXがなく、広域Releaseの合格証拠へ含めない。同じRelease DLLを再ビルドせず、PublicationCrashとRecoveryFileOwnershipの中心4件を切り出して実行し、34秒で全件成功した。DebugとReleaseのPrepared／Committedでは、Windowsのシグナル0と再試行なしのオープン成功を記録した。最終候補の全件・配布受入を継続する。

証跡は `F:\Seamless\recovery-lock-20261108` のrepeat-Debug、signal-probe、wait-comparison、final-Debug-tests、final-Release-tests、core-Release-testsに分けた。前二つの失敗・観測、交互比較、正式試験の合否と打切りを区別し、verification.jsonへソースと候補DLLのハッシュを記録した。診断JSONは各隔離DBルートの隣へ保存する。

## 残る終了条件の監査と正常CLIの確認

2026年11月10日、既存の強制終了原因の再調査を避け、残る保存先の引き継ぎを監査した。Management ServerとExecutionの製品コードには、DB所有の子プロセスを非同期待機した後で同じ保存先を開き直す経路は見つからなかった。SDKコンパイラーの待機、接続先の生存確認、DB所有プロセスの終了待ちは分けて扱う。

| 対象 | 現在の終了条件と残り |
| --- | --- |
| PublicationCrash・監査のクラッシュ復旧・ManagementTypedUpgradeCrash | 通常の復旧判定前は保持ハンドルを待つ。PublicationCrashの旧条件比較プローブは意図的な非同期待機を残す。失敗時cleanupの監査は最終受入へ残す。 |
| AuthorizationPolicyStorageの通常初期登録CLI | 下記Windows Release代表で意図した4拒否と正常登録1回の終了シグナル0、同じroot・backupの再openを確認した。CLI自体は正常return前にownerとstoreをDisposeする。期限切れ・強制終了cleanupは未検証。 |
| NativeTlsのGateway再起動 | 下記のWindows Release代表で同じstorageの2回起動・TLS取引・WAL ID維持が成功し、両終了シグナル0を観測した。失敗時cleanupと最終候補の配布受入は残す。 |
| GatewaySubjectStatus | 下記Windows Release代表で、利用者無効化によるHTTP／Native書込みの取消と同一storage再起動後の状態保持が成功し、両終了シグナル0を観測した。失敗時cleanupは未検証。 |
| その他のGateway起動経路 | GatewayHttpAuthorization、NativeGatewayCommandAuthorization等の代表を今回実行した結果へ一般化しない。 |
| Manager接続専用の子プロセス・SDKビルド | DB所有者としての引き継ぎではない。ファイルや資格情報の寿命はそれぞれの検証範囲で扱う。 |
| Invoke-Comparison | 通常停止はPIDと作成時刻を確認してプロセス一覧からの消失を待つ。起動失敗時にはHasExitedでWaitForExitを省く分岐が残る。今回同ハーネスは実行していない。 |

新しい`ProcessResourceHandoff`のWindows Release独立試験では、自作seedを正規backupで別rootへコピーし、実CLIのreset-passwordとupgrade-managed-typesを実行した。各seedは57ファイル・2,164,282バイト。両ケースとも.NET 10.0.14で非同期待機直後と保持ハンドル待機後のシグナルが0だった。再試行なしの即時オープン、元InstanceId・設定・利用者・形式6のbackupを確認し、移行後の形式7も確認した。2件とも成功し、今回の正常CLIでは共有違反を再現しなかった。非同期待機の一般的な終了保証や、既存の強制終了失敗を覆す結果として扱わない。

今回製品の保存・共有条件は変更していない。利用者のDBと稼働中デモにはアクセスしていない。試験の終了処理は保持ハンドルを期限付きで待ち、最終のシグナル0を確認する。

証拠は`F:\Seamless\manager-object-ui-20261110\w10-handoff`の`results-r1/handoff.trx`、`run-r1`、各`db/handoff-*/copy.handoff.json`にある。使用DLLと試験ソースのハッシュは同領域の`verification.json`へ記録する。最終候補の全件・配布受入と、上表の未検証経路は引き続きW10-LOCK-ACCEPTへ残す。

## NativeTlsの同一storage再起動

2026年11月10日、`NativeTls.GatewayCommandPublishesTlsAndReopensTheSameDatabase`の既存代表を別の隔離rootで実行した。元のGateway設定、TLSとホスト名検証、接続policyの切替、取引rollback、2回起動の順序を維持し、非同期待機直後の実プロセスシグナルだけを記録へ追加した。追加の終了待ち、固定sleep、共有違反の再試行は入れていない。Manager画面の任意検証は有効にしていない。

Windows Releaseで1件が23秒で成功した。Gateway子PID67080と58996はともに終了コード0、非同期待機直後のWindowsシグナル0だった。2回とも同じstorageを開き、TLS接続と取引rollbackが成功し、GatewayWalId `018a85c1-f95d-4991-9fe6-267d34c18f0b`が一致した。今回の正常再起動で共有違反は観測しなかったため、製品の保存・終了処理の修正は行っていない。

独立ビルドは警告・エラー0件。証拠は`F:\Seamless\manager-object-ui-20261110\w10-gateway-handoff`の`results-r1/gateway-handoff.trx`、`run-r1`、`db/7caa4dce23e94607beb51981c541422e/server-0.handoff.json`と`server-1.handoff.json`に分ける。使用DLL、試験・driver、設定と観測JSONのハッシュは同領域の`verification.json`へ記録する。前節の正常管理CLI2件や後述の認可代表と合算してGateway全般の受入成功とは扱わない。失敗時cleanup、Linux、最終配布候補の受入は継続する。

## 共通認可の通常初期登録CLI

2026年11月10日、`AuthorizationPolicyStorage.InitializeCommandRequiresVerifiedAdministratorAndOfflineBackup`を別の隔離rootで実行した。既存のループバックOIDC試験発行元、標準入力からのアクセストークン、認可、排他所有、backup条件を維持し、非同期待機直後のPID・作成時刻・終了シグナルを追加記録した。観測JSONへトークンや標準入出力の本文を保存していない。

Windows Release代表1件が11秒で成功し、5子CLIすべてのWindows終了シグナル0を観測した。使用中root、不正audience、管理権限不足、使用済みbackup先の4回は意図どおり終了コード1で拒否された。これらを終了後の残存ロック不具合として数えない。拒否後のRevisionとbackup非作成、正常登録後の同じrootの認可版1と監査主体、backupの元Revision・未登録状態を照合した。最後の正常登録CLIは終了コード0だった。元の非同期待機順序を変更せず、再試行・固定sleepを追加していない。

独立ビルドは警告・エラー0件。今回終了後の残存ロックを観測しなかったため、製品修正は行っていない。証拠は`F:\Seamless\manager-object-ui-20261110\w10-authorization-handoff`の`results-r1/authorization-handoff.trx`、`run-r1`、各`db/*/authorization-cli-1.handoff.json`から`authorization-cli-5.handoff.json`へ保存した。実DLL・ソース・観測JSONのハッシュは同領域の`verification.json`へ記録する。正常管理CLI2件、NativeTls代表1件と、この共通認可代表1件はそれぞれ別の検証範囲である。期限切れや強制終了時のcleanup、Linux、最終配布候補の受入は未検証として残す。

## GatewaySubjectStatusの同一storage再起動

2026年11月10日、`GatewaySubjectStatus.OneManagementDisableRollsBackHttpAndNativeWritesBeforeTheirClientsClose`を独立rootで実行した。既存のsales／ledgerの初期行、OIDC・TLS認可、HTTP／Nativeの未確定書込み、管理APIによる利用者無効化、再起動後の状態検証を維持し、非同期待機直後のPID・作成時刻・実シグナル・GatewayWalIdだけを記録へ追加した。終了待機、再試行、固定sleep、製品コードは変更していない。任意のManager画面検証は有効にしていない。

Windows Release代表1件が58秒で成功し、独立ビルドは警告・エラー0件だった。beforeの子PID17908とafterの63100はともに終了コード0、非同期待機直後のWindowsシグナル0。同じstorageのGatewayWalId `e73fd837-a47a-45da-ab39-0336aca69e7a`が一致した。HTTP／Nativeの未確定書込みが無効化で取り消され、クライアントを閉じる前に別利用者が同じ行へ書込み可能となったこと、再起動後も無効状態・同じ操作結果・両DBの初期行が保持されることを確認した。共有違反と復旧待機は観測しなかった。

証拠は`F:\Seamless\manager-object-ui-20261110\w10-subject-handoff`の`results-r1/subject-handoff.trx`、`run-r1`、`db/bc3e0e0a19154f9bbd771dc7aacc7a46/before.handoff.json`と`after.handoff.json`にある。実DLL・試験・driver・観測JSONのハッシュは同領域の`verification.json`へ記録する。先行するCLIやNativeTlsの結果とは別の範囲で扱う。期限切れ・強制終了cleanup、その他のGateway起動経路、Linux、最終配布候補の受入は継続する。
