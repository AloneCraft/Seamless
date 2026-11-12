# 管理ホストの共通起動と組み込み設定

## 背景と観察

ManagerWebHostとInProcessManagementHostは公開されているが、保存設定の解決・参照監査・受付の構築は標準serve入口に集まっている。組み込みの参照監査は未接続で、ReadAuditBufferの既定ワーカーと破棄時の最終保存は専用DBスレッドの外で動く。

## 引き継ぐ仕様と目的

組み込み同期は同じPIDの専用DB実行スレッドとローカルManagerを使う。サーバー同居と独立サーバーは同じ管理サービス、非同期受付、認証済み入口を使う。保存済み設定の解決と次回起動反映を、共通の公開ホストへ接続する。所有者IPC、TCPのTLS・認証、外部HTTPSの利用者認証を各入口で維持する。

## 設計と実装順

ManagementApplicationHostで既存の起動部品と寿命をまとめ、CreateAsyncとOpenAsyncを分ける。実行形態、管理領域、証明書、既定設定を受け、保存値を優先して解決する。CLIの明示引数は既存の最優先規則を引き継ぐ。標準serveも同じサーバー起動処理を使う。

組み込み同期ではローカルManagerの所有者セッションを一つとし、参照監査の定期保存と終了時保存を専用DBスレッドへ配送する。通信待機とタイマーはDBスレッドの外で行う。設定画面は実行形態を示し、その形態で利用する項目を編集できるようにする。

サーバー型では既存のConcurrentManagementOperations、ManagementServer、ManagerWebHostを使用する。起動途中の失敗は構築済みの所有資源を閉じ、通常終了は受付・画面・実行操作・管理サービス・保存領域の順で寿命を閉じる。既存ストアの形式を起動時に変更しない。

## 検証と残る境界

両形態の保存・再起動反映、同一PID、組み込みの監査保存スレッドと最終保存、ポート使用中の起動失敗後の資源解放、標準serveの既存試験を確認する。業務APIから実クエリ全体が同じDBスレッドで実行される条件と、生成クライアントを参照しない業務呼び出しはW07の別受入として追跡する。

## 公開APIと寿命

既存の管理領域はOpenAsync、新しい専用領域の作成はCreateAsyncを使う。両方ともManagementHostOptionsで実行形態を明示する。Defaultsは保存値がない場合の既定値となり、保存済み設定を優先する。標準serveの明示起動引数は既存の最優先規則を使う。

```csharp
await using var host=await ManagementApplicationHost.OpenAsync(
    new ManagementHostOptions(root,ManagementExecutionMode.Embedded));
var databases=host.Embedded!.Databases();
var launch=host.Manager.Sessions.CreateLaunch();
ManagerBrowserLauncher.Open(launch);
```

上のDatabasesは同じプロセスの同期直接呼び出しである。生成された業務クライアントDLLを参照する入口を経由しない。実際の業務クエリAPIの受入はW07-BUSINESSで続ける。

```csharp
await using var host=await ManagementApplicationHost.OpenAsync(
    new ManagementHostOptions(root,ManagementExecutionMode.Server){
        Certificate=certificatePath,
        Defaults=new ManagementServerSettings{Port=14336}
    });
```

サーバー型は既存のOS所有者IPC、認証を必要とするTCP/TLS、ローカルManager、明示設定した外部HTTPS Managerを同じPIDで所有する。CertificatePasswordを渡す場合は呼出元が保持する秘密情報として扱い、設定画面・保存設定・起動記録には含めない。標準CLIのserveもこのホストのサーバー起動処理を使う。

ManagementHostOptions.ConcurrentCapacityはサーバー型の同居Manager・外部Manager・TCP／IPCの通常管理要求で共有する。接続数の上限は別に維持する。停止開始時に共有枠の新規受付・待機を取り消し、受理済み処理と接続の終了後にサービス・保存領域を閉じる。実装境界と検証は [共通要求実行枠](20261108-shared-management-admission.md) を参照する。

組み込み同期は所有者ブラウザーのセッション容量を1とする。ネイティブTCP・外部受付・その通信予算はサーバー型の編集項目として、共通設定画面では読み取り専用にし、APIからの変更もExecutionModeで拒否する。保存済みの外部受付が有効な領域を組み込み同期で開く場合は、サーバー型の選択または同設定の明示変更を必要とする。

参照監査は定期タイマーから専用DBスレッドへ保存を配送し、受け入れ済み操作の終了後にも同じスレッドで最終保存する。組み込み用のReadAuditBufferは自身のワーカーと破棄時の別スレッド保存を起動せず、この配送・最終保存を使う。既存の直接作成したInProcessManagementHost.Create・Openは従来の構成を使い、設定付きの起動はManagementApplicationHostを入口とする。

## 実行結果

最終候補のホスト4試験がDebug・Releaseで成功した。先行Debug候補では設定・監査バッファ・初期設定を合わせた15件、最終Release候補では既存Webホストを含む19件が成功した。先行Debug後の変更はホストの失敗後処理とその試験であり、設定・監査バッファの共通経路の証拠を再利用した。変更プロジェクトのビルドと最終試験の警告・エラーは0件。

- 実クエリを公開・実行し、参照監査の定期保存と終了時保存が専用DB実行スレッドで行われ、ストアを閉じて再読込した後にも合計2件が保存されていることを確認した。
- 両形態の同一PID、組み込みの直接APIと所有者ブラウザー、設定の保存・再オープン後の反映を確認した。
- サーバー型で所有者IPC、TLS認証前の拒否・認証後の取得、4並行要求、Managerポート・監査期間・通信予算の再起動反映を確認した。
- 使用中ポートによる起動失敗と取消後に同じ保存領域を開けることを確認した。接続情報ファイルをロックして後処理を失敗させた場合も、失敗を通知して所有する保存領域を閉じることを確認した。
- 既存のserve、初期設定、設定画面、監査バッファ、ManagerWebHostの接続・セッション・静的アセットの関連試験を確認した。

WindowsのRelease候補では、公開OpenAsyncを使う試験用アプリをPID65068・専用DBスレッド4で起動した。既存のNative DBとRowsを表示する同じ共通画面から、Managerポート57359、監査期間180秒・保存間隔30秒を保存し、再起動待ちを確認した。PID60040・専用DBスレッド4で同じ領域を再起動し、同じManagerポート、実行中の監査期間180秒・保存間隔30秒、保存版v2、再起動待ちの解消、組み込み用の読み取り専用項目をDOMと画面で確認した。

証跡は `F:\Seamless\manager-host-settings-20261108` のdebug-results、final-Debug-tests、final-Release-tests、probe-run1・probe-run2、control1・control2のready.json、embedded-settings-before-20261108.json・embedded-settings-after-20261108.json。候補のコード・DLLと対応はverification.jsonに記録する。画面は `Work/embedded-settings-restarted-20261108.png`。検証用アプリは通常停止した。

## 継続する受入

同期スカラー・行操作・公開関数とWAL監査転記の専用DBスレッド配送を追加した。組み込みの定期・最終転記と、投影失敗後の再起動による残存WAL回収を確認した。実装・検証は [組み込み同期業務API](20261108-embedded-business-api.md) に集約する。

集合ストリーム、非同期継続を含む残りの業務経路、大量旧世代の監査・予算・強制終了、外部HTTPS、実デバッガー操作、Linux・正式配布物の全体受入を残す。

## 合意状態

利用者の組み込み同期・サーバー同居・ブラウザー統一と、W06-SETTINGS-HOSTの継続指示に基づく実装設計。既存の公開ホスト部品からの起動経路も引き継ぐ。
