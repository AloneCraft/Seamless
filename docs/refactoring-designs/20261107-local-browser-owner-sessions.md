# ローカル所有者のブラウザー起動セッション

## 背景

Managerの共通画面試作では、所有者専用ファイルから起動情報を渡していた。利用者が要求したOS確認済みのローカル所有者とブラウザー利用の対応を、製品の管理IPCへ接続する。画面の接続が残る場合にも、新しい管理操作の受付時に失効を検査する。

## 現状の観察

- `ManagementServer.ForLocalOwner` は、CurrentUserOnlyの受け入れと接続元確認を通ったIPCを識別する。Windowsでは実行試験を行った。LinuxのOS資格情報経路は実行確認が残る。
- `ManagementClient` と `InProcessManagementHost` は `IManagementOperations` を実装する。下書き・検証・公開の処理は既存の `ManagementService` へ接続する。
- ブラウザーの対話接続には、認証時点の主体が残る。HTTP要求のCookie検査と、各管理操作の受付検査を組み合わせる。[ASP.NET Coreの認証と認可](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)

## 過去設計から引き継ぐ仕様

ローカルのパスワードなし操作は、サーバー所有者と同じOSアカウントを対象とする。Webは既定でループバックへ待ち受ける。外部公開は明示設定とHTTPS、利用者ごとの認証・認可を使う。下書き、明示適用、操作ID、取引、永続化の規則を引き継ぐ。

## 目的

所有者のIPC要求から短命の起動情報を発行し、一回の引き換えでブラウザーセッションを作る。期限切れ、明示失効、リスナー停止を後続の管理操作へ適用する。

## 提案設計と実装

### 起動と寿命

`LocalBrowserSessions` はインスタンスIDとプロセスのOS所有者を保持し、数値のループバックアドレスを持つWeb起点へ一度だけ結び付ける。

- 起動能力は暗号学的乱数256ビットで作る。既定の有効期間は2分。
- Webパスはインスタンスごとの乱数128ビットを含む。この配下へ画面・接続・CookieのPathを限定し、パスも起動URLの一部として所有者IPCから渡す。
- 引き換えは同じ起点で一回だけ成功する。並行した引き換えも同じ排他区間で処理する。
- セッションは別の乱数256ビットを使い、既定の有効期間は8時間の固定期限とする。操作によって期限を延長せず、期限後はOS所有者の起動操作で開き直す。2026年11月9日の利用者指定を反映する。
- 起動待ちと有効セッションを合わせた上限は既定16件。組み込み型の試作では1件を指定する。
- 内部辞書には資格情報のSHA-256をキーとして保持する。期限切れ領域は発行時に回収する。
- 発行済みの起動能力とセッションは `RevokeAll` でまとめて失効する。リスナー停止時は管理器を終了し、発行も終了する。
- 寿命と上限はコンストラクターで変更できる。起動能力は10分以内、セッションは1日以内、件数は1024以内とする。

これらの既定値と上限は、今回の実装上の選択として記録する。

### 発行する主体

`ManagementServer.ForLocalOwner` のブラウザー対応オーバーロードへ、同じインスタンスの `LocalBrowserSessions` を渡す。`browserLaunch` と `browserRevoke` は、要求本文の利用者名やトークンによらず、接続上で検証したOS所有者を使う。

TCPと従来のSeamless利用者名対応IPCから、この二つの操作を呼ぶと `Unsupported` を返す。通常のTCP操作は既存のTLS・認証・認可経路を使う。

`BrowserLaunchInfo.Url` は資格情報を含む。`ToString` は資格情報を出力しない。呼び出し元はURLのフラグメントを起動ページで消去してから、同じ起点へのPOSTで引き換える。

### Web要求と管理操作

隔離したWeb試作で、Cookie認証の `OnValidatePrincipal` から管理器を検査する。署名済みCookieが残っていても、失効したセッションを拒否する。[Cookie主体の検査](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.cookievalidateprincipalcontext?view=aspnetcore-10.0)

`BrowserManagementOperations` は、対話接続が取得したセッションIDを各呼び出しで検査してから、共通管理操作へ渡す。有効な時点で受け付けた操作は既存の取引規則で完了させる。

Web試作は、正確なHost・Origin、非公開のパス、起動能力、HttpOnly・SameSite Strict Cookie、ASP.NET Coreの偽造要求対策を組み合わせる。Cookieは同じホストのポート間で共有されるため、CookieのPathをインスタンス固有の非公開パスへ限定する。公開する起動情報へはWeb起点だけを含め、パスと能力は所有者への応答へ含める。[Cookieのポート境界](https://www.rfc-editor.org/rfc/rfc6265#section-8.5)

製品Webホストへの配線と外部利用者の認証は、後続の統合対象とする。

## 影響範囲

`BrowserLaunchInfo`、`ManagementClient.Browser`、`LocalBrowserSessions`、`BrowserManagementOperations`、`ManagementServer`、対応する契約試験を追加・更新した。共通画面への接続試作は `F:\Seamless\manager-cross-platform-20261107\session-ui-trials` に分離している。

## 移行手順

1. ホストが同じインスタンスIDのセッション管理器を作る。
2. Kestrelの実際のループバック起点を結び付ける。
3. 所有者IPCへセッション管理器を接続する。
4. WebのCookie検査と、スコープごとの管理操作ラッパーを接続する。
5. デスクトップのブラウザー起動操作から、所有者IPCで取得したURLを開く。

## テスト計画と確認結果

Windows Debug・Releaseで各20件が成功した。共通作成APIと専用スレッドの回帰を含む。

- 一回限りの引き換え、並行時の単一成功、起点、期限、上限、失効、終了を確認した。
- 所有者IPCでの発行と失効、TCPと従来IPCでの拒否、インスタンス不一致、リスナー停止後の発行拒否を確認した。
- 既存の操作ラッパーから、期限切れ・失効後の下書き保存を拒否し、保存されていないことを確認した。
- 実HTTPで14条件を確認し、追加試験では同じ署名済みCookieが失効前200、所有者IPCからの失効後401になった。
- 実ブラウザーの接続を残して失効させ、一覧更新と新規DB作成の拒否を確認した。所有者IPCから実カタログを読み、拒否したDBが作られていないことを照合した。

証跡は同じF配下の `browser-session-verification.json` に集約する。

## リスクと対策

起動URLの漏出を抑えるため、短命・一回限りとし、フラグメントの消去とログ出力の抑制を使う。セッションのメモリ使用量は件数上限で管理する。管理操作を長時間接続の認証状態だけに依存させず、受付時に管理器を検査する。

## 未決事項と残る受入

製品のWeb起動入口と共通UIへの配線、セッション失効時の画面操作の無効化、外部HTTPS・個別認可、Linux/WSLgの受入を進める。共通UI技術とDB所有境界の選定状態は、対応する設計書と会話で確認する。

## 合意状態

OS確認済みの所有者、パスワードなしのローカル利用、ループバック既定、Webへのセッション引き渡しは利用者の要求を引き継いだ。寿命・件数の既定値とAPIの具体形は、今回の可逆な実装判断である。
