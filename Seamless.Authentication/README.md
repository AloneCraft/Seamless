# Seamless.Authentication

組織サインインのプロトコルとクライアント資格情報の更新を扱う.NET 10ライブラリ。サーバー側の操作権限は`Seamless.Security`で判定する。

`OidcSignInRequest`は認可コード＋PKCEの要求を生成し、ブラウザーから戻ったURLを検証する。`OidcClientSession`はAPIアクセストークンを検証し、OSに保存した更新用資格情報から再開・更新する。ブラウザーの起動とコールバックの受付、GUIと接続プロトコルへの組み込みは呼び出し側の処理であり、現在の管理GUIにはまだ組み込まれていない。

## 保存済みサインインからの再開

Windowsでは`WindowsOidcCredentialStore`を使う。`settings`は信頼するサーバー設定に対応した発行元・API対象・クライアントIDであり、`instanceId`は接続するSeamlessインスタンスのIDとする。`subject`は前回検証された本人の不透明なIDで、表示名やメールアドレスへ読み替えない。

```csharp
var store=new WindowsOidcCredentialStore();
await using var session=await OidcClientSession.ResumeAsync(
    settings,instanceId,subject,store,cancellationToken:cancellationToken);
if(session is null){
    // 保存情報がない、または認証元が失効を返した。ブラウザーでサインインする。
    return;
}
var accessToken=await session.GetAccessTokenAsync(cancellationToken);
```

更新は有効期限の30秒前から行う。同じセッションの同時要求は更新を共有する。通常のアクセストークン取得はメモリから返し、保存領域や認証元へ問い合わせない。更新で受け取った主体・API対象・IDトークンを検証し、保存に成功してから新しいアクセストークンを返す。

クライアントの検証済み情報や`Current`を、サーバー側の認可の代用にしない。サーバーはアクセストークンを自ら検証し、現在の所属と操作開始時の権限を確認する。

## 初回サインインと終了

```csharp
using var request=await OidcSignInRequest.PrepareAsync(
    settings,loopbackRedirect,cancellationToken);
// request.StartUrlを利用者がブラウザーで開き、戻り先URLを受け取る。
var result=await request.CompleteAsync(callbackUri,cancellationToken);
await using var session=await OidcClientSession.SignInAsync(
    settings,instanceId,store,result,cancellationToken:cancellationToken);
```

既定のscopeは`openid profile offline_access seamless.database`。発行元が更新用資格情報を提供しなかった場合、現在のサインインだけを利用できる。異なる認証元やクライアントのサインイン結果を流用しない。

通常終了の`DisposeAsync`は更新用資格情報を保存領域に残す。サインアウトは`SignOutAsync`で保存情報とメモリ上のアクセストークンを削除する。削除失敗・取消では例外を返すため、呼び出し側はサインアウト完了として表示しない。これは組織アカウント全体や他アプリのログアウトを行うAPIではない。

## 障害と保管アダプター

`invalid_grant`は該当する保存情報だけを削除して再サインインへ戻す。通信障害や認証元の一時障害では保存情報を保持し、失敗を呼び出し側へ返す。更新応答の喪失時に自動再送は行わない。保存に失敗したローテーション済み資格情報は再利用を保証できず、再サインインが必要になる場合がある。

`IOidcSessionCredentialStore.WithLockAsync`は、保存情報の読取からローテーション後の保存までを同じ保存キーについてプロセス間で直列化する契約である。Windowsアダプターは同じOS利用者に限定した名前付きMutexで実装し、他のログオンセッションとも共有する。アダプターの待機用スレッドは最大4とし、Mutexの取得・解放を同じバックグラウンドスレッドで行う。同じキーのロックをコールバック内から再取得しない。

Windowsの保存上限はUTF-8で2560バイトで、超過・保管不能をエラーにする。macOS／Linuxの保管アダプターは未実装であり、平文ファイルへの代替保存は行わない。開発用HTTPは明示的なLoopback設定だけで利用する。
