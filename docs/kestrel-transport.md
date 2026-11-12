# Kestrel通信基盤

## 通信の入口

管理RPC、ネイティブRPC、TDSは独自プロトコルを維持し、Kestrelの公開ConnectionHandlerとIDuplexPipeで接続を扱う。HTTP APIはKestrelの標準HTTP処理へ接続する。SQL、DB取引、WAL、世代の公開と復旧はプロトコルの内側に残る。MVC、Razor、gRPCへ変換しない。

管理TCPはTLSを必須とし、ネイティブTCPにも明示的なTLS設定を用意する。証明書・ホスト名の検証はクライアントで維持する。平文・TLS専用のネイティブとTDSはLoopbackへ限定する。認証付きネイティブは明示したIPを指定できる。TDSのPreLoginを通常のTLS接続へ置き換えない。HTTPのHTTPS設定は後述する。

管理IPCはCurrentUserOnlyのNamedPipeを残し、TCPと同じ要求処理へ接続する。IPCのOS利用者をTCPへ渡さない。TCPの受付を認証・認可の成功として扱わない。

ネイティブの直接利用では`NativeQueryServer.CreateTls(registry, certificate, port)`、Gatewayでは`GatewayServerOptions.NativeCertificate`を指定する。クライアントは`NativeQueryClient.ForTls(host, port, trustedRoot)`でTLSを明示する。`trustedRoot`省略時はOSの信頼ストア、指定時はそのルートによる標準のチェーン検証を使い、ホスト名・有効期限を検査する。失効確認はOnlineであり、認証局の情報に到達できない場合も検証を省略しない。TLSのバージョンと暗号スイートはOSに任せる。[.NETのTLS指針](https://learn.microsoft.com/en-us/dotnet/core/extensions/sslstream-best-practices)

サーバー証明書は秘密鍵を必須とし、サーバーとクライアントは渡された証明書を所有しない。呼出し元は利用終了後に破棄する。TLS成功後だけクライアントの接続を公開し、ハンドシェイク失敗・取消ではソケットとTLSストリームを解放する。ハンドシェイク期限はサーバー・クライアントとも15秒。既存コンストラクターによる平文Loopback入口は維持するが、TLS失敗から平文へ自動切替しない。既存ネイティブRPCのMemoryPack要求・応答とフレーム形式を維持する。

配布CLIは設定JSONのトップレベル`NativeCertificatePath`からPFXを読み込み、必要なパスワードは`SEAMLESS_GATEWAY_NATIVE_CERT_PASSWORD`から取得する。相対パスは設定ファイル基準で、設定不備はDBを開く前に拒否する。起動通知の`NativeTls`はTLS設定の有無を示す。設定変更は新しいサーバーinstanceの作成時に反映し、既存接続を途中で切り替えない。TLS専用の入口はLoopbackを維持する。以下の認証付き入口と、将来の相互TLSは別の機能である。

## 認証付きネイティブRPC

通常RPC・明示的取引には、`NativeQueryClient.ForAuthenticatedTls(accessTokenProvider, host, port, trustedRoot)`を使用できる。TLSとホスト名の検証後に有界のOIDC認証frameを交換し、成功後だけ通常要求を送る。`AuthenticatedPrincipal`は接続中の認証元ID・主体を表す。切断・初回認証失敗時には接続を捨て、更新要求を自動再送しない。Nativeの既定待受けはIPv4 Loopbackで、認証付きTLSにだけ明示IPを指定できる。共通資源上限等の未完了条件があり、待受け指定の追加でインターネット向け全体受入を完了したとは扱わない。

直接利用の新overloadは`NativeQueryServer.CreateAuthenticated(registry, endpoint, certificate, authorization, limits)`である。`endpoint`は`IPEndPoint`を指定する。従来の署名と第5引数の`default`呼出し、平文コンストラクタ、`CreateTls`は従来のLoopback動作を維持する。IPとportは取込み時に保持し、IPv6のScopeIdを保ったcopyを作る。`ListenAddress`の戻り値もcopyであり、呼出し元の変更で待受け先は変わらない。同一instanceのStop／Startは保持したIPを再利用する。

Gateway CLIは、既存の`Authorization.Native`、`NativeCertificatePath`に加え、`Endpoints.NativeListenAddress`へIPv4／IPv6の文字列を指定する。省略時は`127.0.0.1`。`::1`はIPv6 Loopback、`0.0.0.0`／`::`は明示した全interfaceの待受け指定であり、クライアントへ渡す接続先や証明書名ではない。クライアントは実際のホスト名／IPを使い、その名前に対応する証明書を検証する。bind失敗時に別IPへ切り替えない。

非既定IPでは、秘密鍵付きTLS、実認証器、有限の接続／要求枠・全buffer・受信／送信期限を必須とする。CLIではDBと管理storeを開く前に構成を検査し、実サーバーは実際の認証器で再検査する。`ValidateNativeTransport(true)`は宣言のpreflightであり、本人確認を行うAPIではない。起動通知の`NativeListenAddress`と`NativePort`、consoleのIPv6括弧付き表示はサーバーが保持する値を使う。

型付き RPC は登録済みモジュールの公開クエリ ID を使う。各要求で呼び出し主体の認可を行い、明示的取引では開始時の権限を使用する。Gateway CLI は公開世代・DLL・依存 DB と資源の承認を検査する。`Authorization.Native` は未知の設定項目を拒否し、削除済みの `Lambdas` 設定が残る構成も起動時の JSON 検査でエラーとする。

ホストは`NativeQueryServer.CreateAuthenticated`と有限の`NativeServerLimits`を指定する。保存・ロード時に検証した公開RPCだけを許可する場合は、`NativeGatewayAuthorization.ForPublishedQueries`を使用する。`NativeQueryExecution`は、その取引から見えるquery世代、manifest／DLL hash、実module・definition、全依存DBのWAL・DB ID・物理世代・schema fingerprint・DLL hashを照合した情報である。実行前の再解決後にも同じ権限leaseで照合し、最新一覧だけと比較して旧取引を拒否しない。引数は実際のmoduleの型から作った検査器で、MemoryPack復元前に確認する。型を推測したり、要求ごとにDLLを読み直したりしない。

Gateway CLIでは共通の`Authorization`設定を追加した。管理領域の初期ポリシーとOIDC設定には、後述する既存の`initialize-authorization`を使う。既存の`HttpAuthorization`も維持し、二つを同時には指定しない。共通設定には`ManagementRoot`、`ManagementInstanceId`、`GatewayWalId`、`Databases`、必要なら`ManagementEndpoint`を指定する。HTTPとNativeは管理ストアを一重に所有し、保存ポリシーの変更を次の操作から反映する。

Gatewayは同じDBを組織認証で保護する場合、本人確認がない入口を併設しない。HTTPだけを保護する構成（旧`HttpAuthorization`を含む）では、HTTPSと設定した認可管理TLSが起動し、TDSとNativeは停止した状態になる。明示的に認証を設定したHTTPS、Native、TDSは同じGatewayで併設できる。停止した入口のポートは`--ready-file`で`null`となる。組み込み利用時にNativeの待受けを停止する設定は`GatewayServerOptions.NativeEnabled=false`。従来の非保護構成ではループバックTDSを使用できる。

Gateway CLIで保護TDSを起動する場合は、`Authorization.ProtectTds=true`、`Endpoints.TdsEnabled=true`（既定）、`TdsCertificatePath`を設定する。TDS専用構成はさらに`Authorization.ProtectHttp=false`を指定する。証明書は秘密鍵付きPFXで、相対パスの基準は設定JSONのディレクトリ。PFXのパスワードは環境変数`SEAMLESS_GATEWAY_TDS_CERT_PASSWORD`から読み込む。保護TDSで`Endpoints.RequestLogPath`は指定できない。起動情報の`TdsAuthenticated`と`TdsPort`で、組織認証付きの入口と実際のポートを確認する。

WindowsのSqlClient 7.0.0で `tcp:localhost,<TdsPort>` がタイムアウトする場合は、証明書のSANに含まれるループバックIPを指定する。この環境では `tcp:127.0.0.1,<TdsPort>` と `tcp:[::1],<TdsPort>` を厳格TLSで確認した。アプリ側でSqlClientの管理ネットワーク実装を起動時に選ぶ場合は、`localhost` でも厳格TLSとSQL実行を確認した。[SqlClientのAppContextスイッチ](https://learn.microsoft.com/en-us/sql/connect/ado-net/appcontext-switches#enable-managed-networking-on-windows)

組み込みホストでは、`TdsServer.CreateAuthenticated`で明示アクセストークン用のTDS 8.0ループバック入口を作成できる。秘密鍵付き証明書、OIDC検証器を持つ認可設定、本人に束縛された共通認可セッションの生成処理を渡す。SqlClientは`Encrypt=Strict`で接続し、`SqlConnection.AccessToken`に登録済み認証元のAPIアクセストークンを設定する。接続先の証明書はクライアントで検証する。

取引外では操作開始時、明示取引内では取引開始時の権限を使う。共通Gateway取引を持つホストで、単文の`BEGIN TRANSACTION`・`COMMIT TRANSACTION`・`ROLLBACK TRANSACTION`、SQL文のセーブポイント、SqlClientの既定のローカル取引の開始・確定・取消に対応する。取引中の要求は接続内の現在の記述子を指定する。期限超過・切断・アカウント無効化によって接続が終了すると未確定取引を戻す。

型付き引数のある単文は、`sp_executesql`または準備済みSQLのRPCで実行できる。準備済みハンドルは接続とDBに束縛し、件数と保持するSQL・引数名の合計を制限する。準備したSQLの権限は実行のたびに評価する。保護入口は共通認可でSELECT・INSERT・UPDATE・DELETEを扱う。SELECTの結果メタデータは `sp_prepare` の `RETURN_METADATA` とJDBCの `getMetaData()` から取得できる。

本文がSELECT・INSERT・UPDATE・DELETEで構成される登録済みストアドプロシージャは、最大64文まで名前付きRPCまたはSQLの `EXEC` で呼び出せる。Gateway設定の `Databases[].SqlObjects` には現在のDB世代ID、`Procedure`、スキーマ名、プロシージャ名、認可リソースIDを指定する。実行者には `Execute` が必要で、委任する読取は同じDBのマッピング済みテーブルを `DelegatedReadResources` へ列挙する。テーブルの明示拒否は委任より優先する。更新には実行者自身のテーブル権限が必要。

SELECTだけで構成される本文では、全ての文の参照権限と引数を確認してから、結果セットを本文の順に逐次送信する。明示取引中はその取引の状態を参照する。明示取引がない場合、世代管理を使うGatewayでは一つの読取スナップショットを、世代管理を使わないGatewayでは対象DBの読取ロックを応答終了まで保持する。

更新を含む複数文は一つの取引で本文の順に実行する。混在するSELECTの行は予算付きの一時領域へ順に保存し、呼び出し全体が成功してから結果セットと更新件数を順に送信する。保護TDSの更新文は完了トークンに文種を付け、Microsoft JDBCの `getUpdateCount()` でも更新件数を取得できる。暗黙取引の場合は確定後に送信する。途中で失敗すると、暗黙取引は呼び出し全体をロールバックする。継続可能な明示取引では、プロシージャを呼び出す直前まで戻し、それ以前の変更を保持する。一時領域は応答完了・中断時に解放する。

登録済みプロシージャの本文で `SET @出力引数 = 式` を使える。SELECT・INSERT・UPDATE・DELETEと混在でき、後続文は代入後の値を参照する。式は共通認可の下で本文の順に評価する。結果は予算付きの一時領域に保存し、更新がある場合は呼び出し元の型への変換まで成功してから取引を確定する。名前付きRPCの `Output` と `InputOutput` では、成功した応答に宣言型と元のRPCの引数位置を載せる。整数、ビット、数値、実数、GUID、および宣言長4,000以下の文字・バイナリ型を最大64出力まで扱う。[TDSのRETURNVALUE形式](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-tds/7091f6f6-b83d-4ed2-afeb-ba5013dfb18f)

Microsoft JDBCの `CallableStatement` などが使う `sp_executesql` と準備済みRPCでも、SQL本文が登録済みプロシージャへの単一の `EXEC ... OUTPUT` なら出力引数を利用できる。RPCの出力フラグ、パラメーター宣言、`EXEC` の変数指定を照合する。

保護TDSの直接SQLバッチでは、`DECLARE`、登録済みプロシージャへの単一の `EXEC ... OUTPUT`、宣言済み変数だけを射影する `SELECT` の順で実行できる。変数の初期値には対応するリテラルまたは先に宣言した変数を使い、初期値を省略した変数はNULLとする。出力先は一意で、変数はそのバッチ内だけで有効。プロシージャが返す結果に続けて、最後のSELECTの結果を返す。出力付きの一般SQLは別対応。

Microsoft JDBCの一覧取得では、保護TDSからデータベース名、スキーマ名、テーブル・ビュー名、列、主キーを取得できる。データベースとオブジェクトには `Discover` が必要。スキーマ一覧には、一覧権限のあるテーブルまたはビューを含むスキーマを表示する。表・列・主キーのメタデータは接続先DBの世代から読み取る。`getColumns()` の表変数バッチは、`sp_columns_170` または `sp_columns_100` への呼び出しと、その出力だけを読む形に限定する。列候補とその出力を読むSELECTは、同じ要求のメモリ予算を共有する。DataGrip画面での一覧取得は別工程。

`time`・`datetime2`・`datetimeoffset` の列一覧は、小数秒の宣言桁数と、それに応じた文字列表現の長さを返す。宣言が省略された場合の小数秒はTDSの値送信と同じ既定の7桁を使う。保護TDSから返す `time` の型番号は、Microsoft JDBCがJDBCの `TIME` へ変換するODBCの番号を使う。[SQL Serverの日時カタログ仕様](https://learn.microsoft.com/en-us/sql/relational-databases/native-client-odbc-date-time/metadata-catalog)

`varchar(max)`・`nvarchar(max)`・`varbinary(max)` の列一覧では、ODBCの最大長指定である `0` を返す。Microsoft JDBCの `getColumns()` はこれを `Integer.MAX_VALUE` に変換する。[SQLColumnsのMAX型仕様](https://learn.microsoft.com/en-us/sql/relational-databases/native-client-odbc-api/sqlcolumns) [Microsoft JDBCの列メタデータ](https://learn.microsoft.com/en-us/sql/connect/jdbc/reference/getcolumns-method-sqlserverdatabasemetadata)

| Native保護設定 | 内容 |
|---|---|
| `Authorization.ProtectHttp` | 既定true。Native専用ではfalseにする。falseならHTTPを起動せず、HTTP証明書を要求しない。 |
| `Authorization.Native.Queries` | 承認した公開RPCの一覧。対象DB ID、module/query ID、query世代ID、manifest／DLL hash、永続resource ID、全依存DBの承認とアクセス集合を持つ。 |
| `Queries[].Databases` | 各依存DBのDB ID、物理世代ID、schema fingerprint、DLL hash。すべて一致する実行物だけを許可する。 |
| `Queries[].Access` | resource ID、`Permissions`、`Delegated`を明示する。委任しないアクセスは実行者にも要求し、委任したアクセスはその正確なDLLとともに信頼する開発者が承認する。公開RPC自体のExecute権限は常に要求する。 |
| `Authorization.Native.ApprovalFile` | 起動時の `Queries` に加え、稼働中に公開版の承認を追加するローカルJSON。相対パスの基準はGateway設定ファイル。省略時は起動時の承認だけを使う。 |
| `Authorization.Native.Limits` | 接続・TLS確立・要求・受信frame・保持要求・未完了送信の有限上限と期限。`MaximumConcurrentHandshakes`の既定は32。`MaximumConcurrentRequests`の既定64は、保護入口の認可lease取得と所属再確認も含む。従来のNative専用上限と重ねて指定しない。共有バッファ予算を使う場合、その値はNative上限以下にする。 |
| `MaximumConcurrentAuthentications` / `AuthenticationTimeout` | `Authorization.Native`内の初回アプリ認証枠と期限。既定32・15秒。枠超過では有界の最初のframeを受け取ってからBusyを返し、認証元への照会を始めない。 |
| `Authorization.Native.TransactionOutcomeKeySha256` | 認証付きNativeの取引結果照会を有効にするSHA-256の16進値。指定時は、同じ32バイト鍵をBase64で `SEAMLESS_GATEWAY_NATIVE_OUTCOME_KEY` に設定する。省略時は従来の保護入口の動作を維持する。 |
| `Authorization.MaximumConcurrentIdentityVerifications` | 保護HTTP、Native、権限管理RPCが共有するOIDC検証枠。旧`HttpAuthorization`でも指定できる。既定32、起動時に正の整数を指定する。 |

承認ファイルには `ManagementInstanceId`、`GatewayWalId`、追加承認の累積一覧 `Queries` を記述する。Gatewayの設定と同じアクセス制御のローカル領域に置き、初回は空の `Queries` でも起動できる。更新時は同じディレクトリの一時ファイルを完成させ、承認ファイルを原子的に置き換える。最大4 MiBで、Gatewayは起動時と稼働中に読み込む。稼働中に追加する項目は、現在公開されている世代と成果物が完全に一致する場合にだけ受理する。新しい項目すべてが有効なら一度に反映し、読み取り・検証に失敗した場合や稼働中に既存項目を変更・削除しようとした場合は従来の承認を維持する。再起動時は起動設定と、その時点の承認ファイルから読み直す。ファイルによる承認はC#のビルドや公開操作とは独立する。

認可用TLSリスナーが `GatewayPublicationIdentityV1` を通知する場合は、`ManagementClient.GatewayPublicationAsync` で現在公開されているクエリの識別情報を取得できる。対象DBの認可リソースID、モジュールID、クエリIDを指定する。GatewayはOIDC認証と対象・全依存DBの `ManagePermissions` を確認する。応答は管理インスタンスID、WAL ID、公開版と依存DBの世代ID、マニフェスト・DLL・スキーマの照合情報を返す。未承認の公開版も対象になり、ソース、実行コード、行データは応答に含めない。この操作は参照専用で、承認は別の明示操作とする。

### Managerで組織アカウントを使って公開版を参照する

接続設定の「組織アカウントでGatewayに接続…」で、管理者から受け取った接続プロファイルを選ぶ。接続先にはGatewayの認可管理用TLSポートを指定する。専用画面で「組織サインイン」を選び、既定のブラウザーで認証を完了する。管理用TLSの証明書・ホスト名、インスタンスIDと対応能力を照合してから認証へ進む。

```json
{
  "ホスト": "gateway.example.net",
  "ポート": 14436,
  "インスタンスID": "22222222-2222-4222-8222-222222222222",
  "証明書": "gateway-root.cer",
  "コールバックポート": 0,
  "認証元": {
    "id": "33333333-3333-4333-8333-333333333333",
    "authority": "https://login.example.net",
    "audience": "seamless-database-api",
    "clientId": "seamless-manager"
  }
}
```

- `証明書` は、OSの信頼ストアを使う場合は省略できる。相対パスはプロファイルのフォルダーを基準に解決する。
- 認証元には公開クライアントとして `http://127.0.0.1:{port}/callback` を登録する。`コールバックポート` が0または省略の場合はOSが空きポートを割り当てる。認証元が登録ポートの一致を要求する場合は同じ固定ポートを指定する。[RFC 8252](https://www.rfc-editor.org/rfc/rfc8252.html#section-7.3)
- 開発用HTTP認証は `認証元.developmentLoopback` を明示し、Gatewayも同じPCにある場合だけ選択する。
- 次に開いたときの再開に使う更新用資格情報はWindows資格情報マネージャーへ保存する。Managerの設定には接続プロファイルに対応する主体IDだけを保存する。専用画面の「サインアウト」で、その保存済み資格情報と主体IDを削除する。

サインイン後、公開クエリを選択して「公開版を取得」を押す。対象DBとすべての依存DBに `ManagePermissions` があるクエリだけを、モジュールIDとクエリIDの順に1ページずつ表示する。承認の有無にかかわらず、現在有効な公開版を表示する。一覧を利用できない旧サーバーでは、DBの認可資源ID、モジュールID、クエリIDを入力して照会する。

`GatewayPublicationListV1` を持つ認可管理リスナーでは `ManagementClient.GatewayPublicationsAsync` を利用できる。ページサイズの既定は50件、最大は100件。続きのカーソルは公開カタログと認可の状態に結び付く。公開版・認可ポリシー・適用される役割が変わった場合は一覧を更新する。詳細取得には選択した世代IDを渡し、別の世代が公開された場合は再選択を求める。

### 選択した公開版を承認する

公開版の詳細を取得し、「選択した公開版の承認…」を開く。対象DBで公開クエリと同名の実行資源を選び、必要なアクセスの資源・操作・委任の有無を指定する。各資源は依存DBの認可カタログから選ぶ。委任の指定により、実行者がその資源への直接の権限を持たなくても、承認された公開版に限って指定した操作が許可される。承認するコードとアクセスの指定を照合してから確定する。

- **承認JSONを書き出す**。対象DBと全依存DBの `ManagePermissions` が必要。サーバーで現行世代と指定を検証した1件分のJSONを、新しいファイルに保存する。既存の承認一覧へ取り込むときは管理者が既存項目を保持する。書き出し操作による稼働中の権限変更はない。
- **サーバーに承認を登録**。Gateway設定で `Native.ApprovalFile` と `Native.ManageApprovalFile: true` を指定し、対象インスタンスとWALに対応する承認ファイルを事前に用意する。呼出しには上記に加え `ManageServer` が必要。この構成では稼働中のファイル更新をGatewayの管理RPCに集約する。外部編集は停止中に行う。Gatewayは承認と登録ID・主体・権限版を同じファイルに保存し、保存が成功してから承認を反映する。

同じ公開世代と同じ承認内容を再度要求した場合は登録済みの結果を返す。同じ世代への異なる承認は競合として拒否する。応答が途切れたときは、画面は結果を確認できなかった旨を表示する。登録済みの承認は対応する実行物のみに有効で、別の世代へは引き継がれない。

保護入口はTLS確立の前に専用枠を取得する。満員時はプロトコル応答を送らず接続を閉じる。確立成功時はアプリ認証を始める前に枠を返し、失敗・期限・切断・停止でも返す。TLS期限は15秒で、接続総数と初回認証の枠は別に数える。上限はリスナー起動時に設定する正の整数である。

保護入口の新規要求は、認可leaseの取得・認証元への所属再確認・対象DBの解決より前に要求枠を取得する。通常RPCと新規取引を含め、終了・失敗・取消まで枠を保持する。枠不足時は既存のrecoverable Busyを返し、照会・DB実行を開始しない。開始済みの同じ取引IDに対する既知の制御（入れ子開始、確定、取消、savepoint、復帰）は枠を消費せず、既存leaseと取引所有権の検査を続ける。従来の開発入口の取引制御は維持する。

初回本人確認と所属再確認は同じ検証枠を使う。満員時は新たな検証を待たせず、HTTPでは503、Nativeでは初回`Busy`または既存接続への回復可能エラー、権限管理RPCでは`AuthenticationBusy`を返す。検証終了・失敗・取消で枠を返す。既存の認可leaseで行う取引制御は検証枠を取得しない。枠は一つのGatewayが所有する検証器に適用され、アプリの接続総数、IdentityModelの内部メタデータ更新、別プロセスとの全体上限は別に扱う。

認証付きNativeクライアントでは、`ConnectAsync`後に`ReauthenticateAsync`を呼ぶと、同じTLS接続で資格情報を更新できる。主体は初回認証に固定する。取引中の更新は拒否し、結果列挙中は送受信の順番を待つ。更新が規定のエラーで拒否された場合は以前の資格情報を維持する。送信後に応答を確認できなければ接続を閉じ、問い合わせや更新要求を自動再送しない。

承認情報のGUI編集・自動書き出しは未接続であり、現時点では検証した成果物の情報を設定へ登録する。名前や公開型契約の一致だけで、別のDLLへ承認を引き継がない。依存DBの最新世代だけへ書き換えて旧取引の承認を失わせず、必要な旧世代の承認も保持する。これらの設定変更は次の起動から反映する。

`ForPublishedQueries`で検証した通常RPCでは、引数・戻り値の実型がともに`bool`、`byte`、`sbyte`、`char`、`short`、`ushort`、`int`、`uint`、`long`、`ulong`、`float`、`double`のいずれかで、両方の完全なwire契約hashが一致すれば、クライアントのDB fingerprintが旧版でも呼び出せる。内部DDL後も公開契約が同じなら、同じクライアントDLLを利用できる。サーバーのmoduleと実DBのfingerprint、および新DLL・DB世代の承認は必ず照合する。公開型を変えた場合は旧型の要求を拒否する。

現段階の資格はこの12種の固定幅型に限る。配列・Nullable・string・Guid・decimal・日時型・独自DTO等は従来のDB fingerprint確認を維持する。legacy静的登録とラムダの世代確認も維持する。v1 hashと保存済みmanifestは変更していない。DTOなど全型へ広げるための、配置・formatterを識別する版付き契約は未完成であり、固定幅型の確認結果をその完了と扱わない。

Nativeを保護する新設定では、認可が未接続のTDSを起動しない。Native専用ならHTTPも起動しない。起動通知の`NativeAuthenticated`は利用者認証の有無を示し、無効なHTTP／TDSのportはnullになる。`NativeTls`だけを認証済みの意味に使わない。管理用TLS証明書にはHTTP証明書を優先し、Native専用ではNative証明書を共用する。従来の開発用設定のHTTP・TDSの既定動作は維持する。TDS認証、全プロトコル共通の資源上限は引き続き実装対象である。

管理サーバーは起動設定の `maximumBufferedRequestBytes` またはCLIの `--maximum-buffered-request-bytes` で、TCP/TLSと同一ユーザーIPCが共有する要求予算を設定できる。正のバイト数を指定し、省略時は上限なし。検証済みのフレーム長を本文の復元前に確保し、要求処理と応答書き込みの終了まで保持する。枠を取れなかった場合は、要求を解析・実行せず接続を閉じる。通信サイズの合計であり、解析後のヒープサイズや応答のサイズは別に扱う。

管理RPCの要求で `reuseConnection: true` を指定すると、TCP/TLS・同一ユーザーIPCで次の要求を同じ接続へ送れる。サーバーは成功時の最後の応答に `reuseIdleMilliseconds: 15000` を返す。クライアントはこの終端を読み終えてから次の要求を送る。結果の各行に再利用の許可は付けず、エラーまたは許可のない終端の後は接続を閉じる。許可を返した後は15秒以内に次の完全なフレームを受け取る。認証・認可と要求バッファは毎回検査し、待機中の接続も共通の32接続枠に含める。従来形式は一接続一要求として扱う。

`ManagementClient.ConnectionPolicy` で操作完了時に解放する既定の動作、未使用時に解放、接続維持を指定できる。再利用を選んだ場合は、同じ資格情報で使用できる待機接続をクライアントごとに最大1本保持する。別の操作が接続を使用中なら新しい接続で送る。アイドル解放はクライアントとサーバーの期限のうち早い方で閉じる。接続維持は保持中の接続にだけ `info` を送り、更新の再送やバックグラウンド再接続は行わない。再利用方針を選んだクライアントは、使用を終えたら `Dispose` する。進行中の要求は終了時に接続を返さず閉じる。

管理画面の「接続設定」にある「TCP / TLS」「ローカルIPC」タブで、方針を個別に選べる。TCP/TLSは「接続先を保存」でプロファイルへ、IPCは「ローカルIPCで接続」が成功したときにそのサーバーのインスタンスへ保存する。変更は次回のログインまたはIPC接続で適用する。未使用時解放は画面では5秒。旧設定の既定は操作完了ごとの切断で、保存する設定にパスワードは含めない。

管理サーバーの `maximumPendingWriteBytes`（CLIは `--maximum-pending-write-bytes`）は、TCP/TLS・IPCが応答送信時に共有する上限。直列化済みの応答と長さヘッダーの枠を書き込み前に確保し、フラッシュ完了・取消・失敗で解放する。`responseWriteTimeout`（CLIは `--response-write-timeout-ms`）は応答フレームごとの送信期限で、指定しない場合は期限を設けない。送信を完了できない場合は処理失敗の応答を重ねず、接続を閉じる。永続的な操作IDを持つ処理の結果は再接続して照会できる。送信上限はJSONの直列化中のメモリや、送信完了後に下位層が保持するバイトを含まない。

取引結果は `GetTransactionOutcomeAsync` で、開始時と同じDB名・利用者から照会する。Gatewayを再起動する場合も照会鍵とWALを引き継ぐ。別主体・別DBでは対応する結果がなければ `Unknown`。既存のメモリ保持のみの取消結果は再起動後に `Unknown` となり得る。照会の設定前に開始した取引はこの照会の対象外。組み込みホストは `NativeGatewayAuthorization` の `TransactionOutcomeKey` または公開RPC factoryの鍵付きoverloadを使う。

## HTTPの公開設定

利用者の方針に従い、HTTPの既定はIPv6対応環境で`IPAddress.IPv6Any`、それ以外で`IPAddress.Any`とし、全インターフェースへ待ち受ける。IPv6対応環境ではIPv4/IPv6の両方を確認する。OSのHttpListener権限でLoopbackへ自動的に戻す動作はなくなった。公開IP、ルーター、ファイアウォールの設定によって外部からの到達範囲が決まる。本変更でそれらの設定は変更しない。

`HttpServerOptions.ListenAddress`で明示的なアドレスを選ぶ。`Certificate`を指定すると、そのHTTPリスナーをHTTPSにする。証明書は呼び出し元が所有し、リスナー停止後に破棄する。`GatewayServerOptions.Http`からも設定できる。ポート0を指定したとき、実際のポートはStart後にPortから取得する。

配布CLIの `--gateway <configuration.json>` では、JSONのトップレベルに `HttpCertificatePath` を指定すると、HTTPリスナーがHTTPSで起動する。相対パスは設定ファイルがあるディレクトリを基準に解決する。秘密鍵付きPFXを使用し、パスワード付きPFXのパスワードはプロセスの `SEAMLESS_GATEWAY_HTTP_CERT_PASSWORD` から取得する。PFXの読み込み失敗や秘密鍵の欠落はストレージを開く前にエラーにする。[.NETのPFX読み込みAPI](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificateloader.loadpkcs12fromfile?view=net-10.0)

```csharp
var options=new GatewayServerOptions{
    HttpPort=8443,
    Http=new(){ListenAddress=IPAddress.Any,Certificate=serverCertificate,MaximumRequestBodyBytes=1024*1024}
};
```

`MaximumRequestBodyBytes` はリスナー起動時の設定。未指定（null）は従来どおり上限なし、0は本文なしのみ許可する。上限を超えたSQLリクエストにはHTTP 413を返し、既存のセッションは維持する。長さ指定とチャンク転送の両方に適用する。HTTP/1.xでは読み残した本文がある接続を再利用しないよう、413の応答に接続終了を明示する。

`MaximumConcurrentRequests` はリスナー起動時に指定する正の整数で、未指定（null）は従来どおり上限なし。枠はHTTP要求処理の開始から応答の書き込みと後処理が戻るまでを数える。上限に達した場合はアプリケーション内で待たせずHTTP 503を返す。接続数、アイドル中のセッション数、応答がクライアントへ届くまでの時間は別に扱う。

`HttpServerOptions.MaximumConnections` は、応答後に待機中のkeep-aliveも含め、HTTPリスナーが維持する接続数を制限する。起動時に正の整数を指定し、未指定（null）は従来どおり上限なし。Gateway CLIのJSONでは `Endpoints.Http.MaximumConnections` に指定する。上限を超えた接続はKestrelが閉じる。HTTPの503を返す同時要求枠とは独立し、HTTPセッションの有効期限やTDS・ネイティブRPCの接続数はそれぞれの設定で扱う。[Kestrelの接続上限](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserverlimits.maxconcurrentconnections?view=aspnetcore-10.0)

`HttpServerOptions.MaximumBufferedRequestBodyBytes` は、同時処理するSQL要求の本文に対する共有上限。起動時に正のバイト数を指定し、省略時は上限なし。Content-Lengthがあれば本文の読み取り前に、チャンク転送では読み取ったバイトをデコードに渡す前に確保する。一要求だけで上限を超える場合は413、他の要求が枠を使用している場合は503を返す。拒否したSQLは実行しない。枠は応答完了、失敗、切断時に解放する。対象は本文の通信サイズで、デコード後のメモリ、Kestrel内部のパイプ、応答のメモリは別に扱う。

HTTPセッション・取引トークンは取引状態の継続に使う。ホストが `HttpGatewayAuthorization` にOIDC検証器・現行の共通認可・DB識別子の解決を渡した場合、保護モードでHTTPSを要求する。カタログのDB一覧は `Discover` 権限で絞り込み、セッションは認証元と主体の組へ束縛する。ホストが解決済みDBの永続テーブルIDをポリシーの対象へ対応付けられる場合は、テーブル一覧を `Discover` で絞り込み、行の取得に `Read` を要求する。対象の対応付けがない場合、表の取得を拒否する。

対象の対応付けがある保護モードでは、`POST /api/sql` で物理テーブル、ビュー、SQL定義のスカラー関数を参照する単一の `SELECT` を受け付ける。各DBの `Discover`、テーブル・ビューの `Read`、関数の `Execute` を要求する。ビューや関数の中で参照する対象にも呼び出し元の権限を適用し、管理者が対象ごとに指定した読取委任を併用できる。計画は要求間で共有しない。

選択したDBの物理テーブルに対し、単独の `INSERT`・`UPDATE`・`DELETE` を受け付ける。実際の対象テーブルに更新種別ごとの権限を要求し、`UPDATE` と `DELETE` には `Read` も必要となる。利用者が指定した更新式・条件で使う関数を認可する。各文は既存の取引で実行する。ストアドプロシージャ、テーブル関数、生成済みデリゲート、システムカタログ、一時表、`SELECT INTO`、CTE付きの更新、`USE`、複文、操作結果照会は保護モードの未対応範囲である。

配布Gatewayの保護モードでは、HTTPセッションを作成した後に `BEGIN TRANSACTION` を実行できる。応答の `session.transactionId` を `X-Seamless-Transaction` に指定し、同じ `X-Seamless-Session` と、開始時のBearer資格情報を各要求に渡す。`COMMIT TRANSACTION` または `ROLLBACK TRANSACTION` まで開始時の権限を使用する。サインイン期限切れ後も、その取引だけを期限内に継続できる。終了後の要求には通常の認証が必要となる。`HttpAuthorization.MaximumTransactionDuration` の既定は30分。HTTPセッションのアイドル期限も別に適用される。

配布CLIの `HttpAuthorization` で保護モードを選べる。管理DBの `ManagementRoot` と `ManagementInstanceId`、Gatewayの `GatewayWalId`、`Databases` を指定する。`Databases` の各要素は永続 `DatabaseId` とポリシーの `ResourceId` を持ち、必要に応じて `Tables` に永続 `TableId` と `ResourceId` の組を指定する。起動時に管理領域を排他取得するため、同じ管理領域を使う別の管理サーバーは停止しておく。`ManagementRoot` の相対パスは設定ファイルの場所が基準になる。既存のGatewayストレージを使用し、起動成功時の `--ready-file` には共通WAL IDも出力する。

オンラインでDB内の権限を変更する場合は `HttpAuthorization.ManagementEndpoint` に `ListenAddress`（例 `"127.0.0.1"`）と `Port` を明示する。省略時は追加のリスナーを起動しない。ポート0はOSによる割当で、実際の値を起動通知の `AuthorizationManagementPort` に返す。HTTPのサーバー証明書を使い、既存管理RPCのTLS・フレーム処理を共用する。アドレス・ポート設定は次の起動から反映する。

このリスナーの能力は `AuthorizationManagementV1`。`ManagementClient.DatabaseAuthorizationAsync` で対象DBの編集情報を取得し、`ChangeDatabaseAuthorizationAsync` で基準版と空でない操作IDを持つ `DatabaseAuthorizationEdit` を送る。各呼出しにOIDC APIアクセストークンを指定する。DB管理者は担当DB内の通常権限を編集でき、管理権限の再委任にはサーバー管理権限が必要。既存のSeamlessアカウントによるログイン操作は、このリスナーでは受け付けない。

`AuthorizationDatabaseListV1` がある場合は、`ManagementClient.AuthorizationDatabasesAsync` で権限編集の対象DBを取得できる。OIDCで認証し、DB単位の `ManagePermissions` を評価する。明示的に拒否されたDBは一覧に含めない。公開クエリの登録状況にかかわらず、認可ポリシーに登録された対象DBの名前と資源IDが返る。最大100件と512 KiBの項目予算でページに分ける。続きには同じ主体で `Version` と `NextCursor` を指定し、ポリシー版が変わって `Conflict` になった場合は最初から取得する。

管理ツールの「Gateway管理」で組織サインインし、「DBの権限…」を開く。左からDBを選ぶと、右に対象・ロール・許可／拒否・割り当てが表示される。ロール行を選んで利用者またはグループの主体IDを入力すると割り当てできる。割り当て行を選ぶと解除できる。確定前に対象DB・権限版・ロール・主体を確認する。応答を確認できなかった場合は、表示された操作IDを控え、同じ組織アカウントで結果を照会する。

選択したDBの「ロールを作成…」またはロール行の「選択したロールを編集…」から、名前、対象、操作、許可または明示拒否を指定する。規則は複数指定できる。編集時は同じ権限版から既存規則をすべて取得する。画面で扱う規則は1ロールにつき4,096件まで。これを超える既存ロールの画面編集は中止する。サーバーは対象DBの権限、基準版、DB管理権限の再委任制限を引き続き確認する。「選択したロールを削除…」でロールとその割り当てを一緒に削除する前に確認を表示する。

編集情報の `Items` はリソース・ロール名・ロール内の規則・割り当てのいずれか一つを持つ項目で、最大100件と512 KiBのJSON項目予算でページに分ける。`HasMore` がtrueなら同じDBと返された `Version`・`NextCursor` を次の呼出しへ指定する。途中に権限変更が確定した場合は `Conflict` を返すので、古いページと混ぜず最初から取得する。一つの項目自体が上限を超える場合は `Limit` を返し、内容を切り詰めない。

変更後は次のHTTP要求から保存したポリシーを使う。開始済み取引は開始時の権限を保持する。応答を失った場合は、同じ主体で `AuthorizationChangeResultAsync` に操作IDを渡して結果を照会する。同じIDで変更を再適用しようとした場合は競合として拒否し、自動再送しない。結果が未登録の場合に、変更が失敗済みと判断しない。組織アカウントの無効化・再有効化は、権限割り当てとは別の操作としてGateway管理画面から行う。

ビューとSQL定義のスカラー関数を許可する場合、対象DBの `SqlObjects` に `GenerationId`、`Kind`（`View` または `ScalarFunction`）、`Schema`、`Name`、`ResourceId` を指定する。スキーマ名とオブジェクト名は保存定義の表記と一致させる。`GenerationId` は起動通知 `--ready-file` の `Databases` に出る。スキーマを再生成した後は新しい世代の対応が必要であり、同名であっても旧世代の対応からの参照は拒否する。

個々の `SqlObjects` で `DelegatedReadResources` に同一DBのテーブルの `ResourceId` を指定すると、そのビューや関数の定義内でだけ読取を委任できる。テーブルの対応付けは `Tables` にも必要となる。呼び出し元の明示拒否は優先する。呼び出し元が書いたSQLや関数引数、内側から呼び出した別のビュー・関数には委任を引き継がない。設定はGateway起動時に読み込み、変更は再起動後に反映する。

ホストが認可を渡さない場合は既存のHTTP経路を使う。既存のURL、JSON、セッションヘッダー、取引IDと結果照会を維持する。旧公開`HttpListenerContext`オーバーロードも同じ要求処理へ接続する互換入口として残す。[HTTP認証と認可の設計](refactoring-designs/20261105-gateway-http-authorization.md)

管理CLIの `initialize-authorization` で、停止中の管理領域に版1の共通認可ポリシーを登録できる。OIDCのAPI用アクセストークンをリダイレクトした標準入力から受け取り、管理権限を確認し、指定先へバックアップしてからポリシーとOIDC設定を保存する。Gatewayはこの保存済みの設定で発行元を検証する。[初期登録の引数と保存仕様](refactoring-designs/20261104-authorization-policy-storage.md)

## 所有権とコピー

Kestrelが接続・受送信パイプを所有する。管理要求とネイティブ要求は長さ付きフレームから復元する。受信済み本文がそろっている場合はReadOnlySequenceから直接JSON/MemoryPackを読み、解析が終わってからAdvanceToする。分割された大きい本文では、受信済み部分を消費しながら上限内のプール領域へ退避する。全本文がパイプへ届くまで保持し続けるとバックプレッシャーで停止するため、この場合はコピーが必要になる。

ネイティブ出力はPipeWriterへヘッダーと既存の行ペイロードを書き、一度のFlushで送る。INativeQueryModuleのbyte[]結果の公開契約は変更しない。ラムダの版付き検証器とクライアント側の既存フレーム処理にはbyte[]の所有が残る。TDSはStream用の追加Pipeを作らず、KestrelのPipeを直接使う。TLS・復元先オブジェクトの割り当てもあるため、完全ゼロコピーとは呼ばない。

通常の接続切断は未確定取引を取り消す。明示的取引は接続を占有し続ける。結果列挙は切断・取消・サーバー停止で解放する。永続的な操作IDを持つ適用の結果は、通信の終了と区別して照会する。通信失敗した更新の自動再送は追加しない。

管理RPCのTCP/TLS問い合わせでは、切断通知を問い合わせ実行と結果送信へ伝える。確定前に通知を検知した更新は取り消す。すでに確定した更新の応答を失った場合は、その事実と取消を区別する。ローカルIPCの切断は既存の読み書き失敗で扱う。

Kestrelの接続停止が完了しても、開始済みのアプリ処理が終わったとは扱わない。共通のApplicationLifetimeで新しい処理の登録を止め、TCPハンドラーとHTTP要求のfinallyが完了するまで待ってからホストを破棄する。Kestrelの接続停止期限を超える処理でも同様である。取消を無視する処理を強制的に安全終了できるという保証ではなく、残った処理の実行中にDB・セッションの所有者を先に破棄しないための境界である。

## 資源制限

| 対象 | 実装で維持・設定した境界 | 残る境界 |
|---|---|---|
| 管理 | TCP+IPC合計32。TLSと最初の要求は各15秒。JSON本文16MiB。任意の要求・応答の共有予算、応答送信期限 | 要求の実行期限、デコード後や直列化中のメモリ上限 |
| ネイティブ | 1接続の実行は逐次。入力Channel容量1。任意の接続・同時要求上限、受信復元・要求・送信の予算、フレーム送受信期限。種別を含むフレーム16MiB、ラムダ準備は既存の小さい上限と構造検査 | 準備専用の処理枠、取引や結果生成も含む全体の資源予算 |
| TDS | LoopbackのIPv4/IPv6、backlog1024。両待受けで共有する任意の接続上限、ログイン・未完メッセージ受信・パケット送信の期限、受信復元・要求・送信の共有予算。メッセージ16MiB、パケット長検査、入力Channel容量1 | 全体の同時実行数、デコード後の要求や取引の資源予算 |
| HTTP | セッション既定128、アイドル5分。Kestrel標準HTTP処理。任意のHTTPS証明書・接続上限・本文長上限・同時要求上限、TLS15秒。本文と応答送信の予算、応答送信期限、埋め込みホストの保護モード | 配布CLIでの認証設定、SQLの対象別認可、デコード後や結果生成の資源予算 |
| ソケットPipe | read/write各64KiBの閾値、非同期I/O、NoDelay | 接続数×閾値とは別の、復元中フレーム・DB・全サーバーの予算 |

Channel容量1とは別に、受信処理が次の要求を復元して投入を待つ領域が一つある。64KiBはバッファの厳密な容量上限ではなく、最大サイズの一つの書き込みはこれを超え得る。HTTP本文長の既定は旧APIと同じ上限なし。リスナー単位の設定と全体の受信予算は別である。独自TCPにHTTP用の期限・上限が自動適用されるとは扱わない。

`GatewayServerOptions.NativeMaximumConnections` はGatewayの構築時に指定する正の整数。未指定（null）は従来どおり上限なし。上限到達時は新しいネイティブ接続を閉じ、受付済みの接続はそのまま要求を処理する。TDSや管理RPCとの合算には使わない。

`GatewayServerOptions.NativeMaximumConcurrentRequests` はネイティブの問い合わせとラムダの準備・実行が共有する任意の枠。未指定（null）は上限なし。枠が埋まっている場合は要求を実行せず、通常RPCでは回復可能エラー、ラムダでは未実行を示す `Busy` を返す。取引の開始・確定・取消・結果照会はこの枠の対象外。

### ネイティブクライアントの接続方針

ネイティブクライアントには生成時に `NativeConnectionPolicy` を渡せる。旧コンストラクター・factoryの既定は接続維持（`KeepAlive`）。`ReleaseWhenIdle` は設定時間だけ未使用なら解放し、`ReleaseAfterOperation` は通常RPCの結果の終端、個々のラムダ要求、または明示取引の外側の完了で解放する。明示的な `ConnectAsync` と接続中の資格情報更新では次の要求用の接続を保持する。解放方針を指定した認証付き接続の再接続では、認証元ID・主体が最初の接続と一致することを確認する。結果列挙の途中破棄や通信障害で切断しても、要求の自動再送は行わない。[接続方針の設計](refactoring-designs/20261103-native-network-scalability.md#ネイティブクライアントでの初期実装)

Gatewayは起動時の `Endpoints.NativeRecommendedConnectionPolicy` にリスナーの推奨値を設定できる。例えば次の設定では5秒のアイドル解放を推奨する。

```json
{
  "Endpoints": {
    "NativeRecommendedConnectionPolicy": {
      "Lifetime": "ReleaseWhenIdle",
      "IdleTimeout": "00:00:05"
    },
    "NativeConnectionPolicyProfiles": {
      "interactive": { "Lifetime": "KeepAlive" },
      "batch": { "Lifetime": "ReleaseAfterOperation" }
    }
  }
}
```

`ForAuthenticatedTlsWithServerConnectionPolicy` または `ForTlsWithServerConnectionPolicy` で作成したクライアントは、最初の接続でTLSと必要な本人確認を完了した後、15秒の期限で推奨値を取得する。開発用の平文Loopbackでは `UseServerConnectionPolicy` で有効にできる。取得した設定はクライアントの寿命中に固定する。サーバーが推奨値の未設定を明示した場合は `KeepAlive` を使用する。アプリが `NativeConnectionPolicy` を明示したときはその値を使用し、推奨値を問い合わせない。適用中の値は `ConnectionPolicy`、受け取った値は `ServerRecommendedConnectionPolicy` で参照できる。

用途別の設定は `NativeConnectionPolicyProfiles` に最大128件登録できる。名前はASCII英数字とピリオド、アンダースコア、ハイフンの1〜64文字で、大文字と小文字を区別する。対応するクライアントは `ForAuthenticatedTlsWithServerConnectionPolicy("batch", getToken, host, port, trustedRoot)` で用途を指定できる。登録のない名前にはリスナーの推奨値を返す。設定ファイルの変更は次のGateway起動から反映する。用途名は接続方針の選択に使い、認可やサーバーの資源上限は各操作に通常どおり適用する。

Managerでは「接続設定 → Gateway → 業務用の接続方針を編集…」を開き、Gatewayの起動に使用するJSONファイルを選ぶ。「待受けの既定」と用途行の方針を編集し、「起動設定に保存」で確定する。未使用時に解放する場合は待機秒数を入力する。組織アカウントによる接続の入口も同じGatewayタブにある。保存には選択した設定ファイルの書き込み権限が必要で、読み込んだ後に内容が変更されていた場合は開き直す。方針以外の設定値を保持してファイルを置き換え、稼働中のGatewayは起動時の方針を使い続ける。

問い合わせはフレーム種別10で、リスナーの推奨値には版1の1バイト、用途別の推奨値には版2の1バイトに続けてASCIIの用途名を送る。両方の応答は種別11・6バイトで、版1、方針コード（0は未設定、1は維持、2はアイドル解放、3は操作ごとに解放）、32ビット符号なしリトルエンディアンのアイドルミリ秒で構成する。アイドル解放以外のミリ秒は0。サーバーでは通常の要求枠内で処理し、認証付きの入口では新規操作として主体を確認する。旧サーバー、切断、不正な応答で取得できない場合は、最初の業務要求を送る前にエラーを返す。用途別の照会には対応サーバーを使い、既存クライアントの版1はそのまま利用できる。

#### 業務クライアントの接続プール

`NativeConnectionPool` は接続先・TLS信頼設定・認証方式ごとに作成する。認証付きの `ForAuthenticatedTls` には期待する認証元ID・主体と、その主体専用のトークン取得処理を渡す。接続を再確立するたび、サーバーから返された主体を照合する。資格情報の範囲が異なる場合は別のプールを使う。

`RentAsync` で取得した `NativeQueryClient` を、取引や結果列挙を完了または破棄した後で `DisposeAsync` して返す。既定は貸出16本・待機128件・待機期限5秒。待機が満杯なら即時拒否し、待機中は取消できる。プールを破棄すると、待機中の取得は終了し、返却済みの物理接続は閉じる。借りているクライアントは返却まで利用できる。

```csharp
using var pool = NativeConnectionPool.ForAuthenticatedTls(
    host, port, new NativeConnectionPoolOptions {
        UseServerConnectionPolicy = true,
        ServerConnectionPolicyProfile = "batch"
    },
    expectedPrincipal, getToken, trustedRoot);
await using var client = await pool.RentAsync(cancellationToken);
// client を型付きRPC、ラムダ、取引に使用する。
```

貸出ごとに独立したクライアントを生成し、返却後はそのクライアントを使えない。物理接続は初回使用時に確立し、再利用できる接続だけをプールに戻す。物理接続を再利用する場合は取得済みの推奨方針も引き継ぐ。アイドル解放の期限は最後の操作の終了時点から数え、返却で延長しない。操作完了ごとの切断を指定した場合、貸出・待機の上限を適用しつつ、物理接続は操作完了時に閉じる。更新の応答を失った場合は、業務要求を再送せず、通信エラーを呼び出し元へ返す。

`GatewayServerOptions.NativeIncompleteFrameTimeout` は未完のフレームを検出してから、残りのヘッダー・本文を待つ期限。未指定（null）は期限なし。断片が追加で届いても期限を延ばさない。フレーム間のアイドル時間、問い合わせの実行時間、応答の送信待ちには適用しない。

`GatewayServerOptions.NativeFrameWriteTimeout` は応答フレームごとの送信パイプ待機の期限。未指定（null）は期限なし。期限切れの接続を閉じ、列挙と未確定取引の解放には通常の切断処理を使う。この期限はクライアントによる受領や、更新の確定結果を保証するものではない。

`GatewayServerOptions.NativeMaximumReceiveBufferBytes` は分割フレームの復元に借りる配列の共有上限をバイトで指定する。正の整数を起動時に設定し、省略時は上限なし。配列を借りる前にバケット容量を確保し、確保できない接続は当該要求を実行せずに閉じる。許可する要求は、型契約などのフレーム本体全体を含むサイズから配列のバケット容量を算出し、分割されてもその容量を確保できる値を設定する。本文の復元、エラー、取消のいずれでも、配列の返却後に枠を解放する。入力パイプからその場で借りて解析できるフレーム、プールが返却後に保持する配列、デシリアライズ後の要求オブジェクト、送信側の予算はこの設定に含まれない。

`GatewayServerOptions.MaximumReceiveBufferBytes` はGatewayのネイティブRPCとTDSが共有する受信復元予算。TDSは連結中の配列と拡張時に同時に存在する旧・新の配列、最終ペイロードへのコピーを予算に含める。枠が足りないメッセージは実行せず、その接続を閉じる。値は起動時に指定し、ネイティブ専用の上限とは同時に指定できない。

`GatewayServerOptions.MaximumBufferedRequestBytes`（CLI設定の `Endpoints.MaximumBufferedRequestBytes`）は、受信復元とは別にGatewayのネイティブRPC・TDS・HTTP SQL本文が共有する要求予算。起動時に正のバイト数を指定する。ネイティブはフレーム長を解析前に、TDSは復元済みのペイロード長を実行待ちへ入れる前に確保する。HTTPはContent-Lengthを読み取り前に確保し、チャンク転送では読み取ったバイト数に合わせて枠を増やす。HTTP固有の本文予算も指定した場合は両方を満たす必要がある。HTTPは一要求だけで本文上限を超えると413、他の要求が枠を使っていると503を返す。ネイティブRPCとTDSは確保できなければ送信元の接続を閉じる。枠は応答処理の終了まで保持し、切断・取消・解析失敗でも解放する。省略時はGateway全体の要求予算を設けず、各入口の設定に従う。デコード後のヒープ使用量、管理RPC、パイプや送信側のメモリは別の制限を使う。

`GatewayServerOptions.MaximumPendingWriteBytes` は、ネイティブRPCのフレーム、TDSのパケット、HTTPの応答本文を送る間の共有上限。起動時に正のバイト数を指定し、省略時は上限なし。ネイティブRPCとTDSはヘッダーを含むサイズをパイプへの書き込み前に確保し、フラッシュの完了・失敗・取消で解放する。HTTPは応答全体を確保する代わりに、最大16 KiBの送信単位で書く。SQL結果の一時ファイルからコピーする場合は、コピー用バッファの容量を送信完了まで保持する。枠を取れない場合は応答を書き始めず、または途中で接続を閉じる。すでに確定した更新の結果は、利用可能な経路で既存の結果照会を使う。フラッシュ後にソケットやパイプが保持するバイト数、行生成、管理RPCの応答は別に扱う。

`HttpServerOptions.MaximumPendingWriteBytes` はHTTPだけに上限を設定する場合に使い、Gateway全体の送信上限とは同時指定できない。`ResponseWriteTimeout` はHTTPの各送信単位に適用し、期限を超えた応答は中断する。いずれも省略時は制限なし。

`GatewayServerOptions.MaximumConnections`（CLI設定の `Endpoints.MaximumConnections`）は、同じGatewayのネイティブRPC・TDS・HTTPが共有する接続数の上限。正の整数を起動時に指定する。TLSを含む場合はハンドシェイク前に枠を取得し、接続終了時に返す。到達時は新しい接続を閉じ、受付済みの接続は処理を継続する。省略時はGateway全体の接続枠を設けない。各プロトコルの個別上限は共有上限と併用できる。別ホストの管理RPC・IPC、OSの待受キュー、実行中要求数、バッファ量は別の制限を使う。

`GatewayServerOptions.MaximumConcurrentTlsHandshakes`（CLI設定の `Endpoints.MaximumConcurrentTlsHandshakes`）は、同じGatewayのHTTPSとネイティブRPCのTLS確立処理が共有する上限。起動時に正の整数を指定し、認証付きネイティブRPCの個別上限と併用できる。上限に達した接続はTLS確立前に閉じる。枠はTLS確立、失敗、15秒の確立期限、切断で返す。省略時はGateway共通の確立枠を設けない。確立済みの接続、平文の待受け、TDS、別ホストの管理RPC・IPCはこの枠を使わない。

`GatewayServerOptions.TdsMaximumConnections` もGatewayの構築時に指定する正の整数で、未指定（null）は従来どおりTDS固有の上限なし。IPv4・IPv6のループバック待受けが一つの上限を共有する。上限到達時は新しいTDS接続を閉じ、受付済みのSQL接続は維持する。

`GatewayServerOptions.TdsLoginTimeout` はTDS接続の処理開始からログイン応答の書き込み完了までの期限。PreLoginのやり取りで期限は延長されない。ログイン後のアイドル時間・SQL実行には適用しない。未指定（null）は期限なし。

`GatewayServerOptions.TdsPacketWriteTimeout` はTDS応答の各パケットを送信パイプへ書き出す際の待機期限。期限に達した接続は閉じ、結果列挙と未確定取引は切断処理で解放する。未指定（null）は期限なし。SQLの実行時間や、クライアントによる応答の受領はこの設定の対象外。

`GatewayServerOptions.TdsIncompleteMessageTimeout` は途中まで届いたTDSメッセージの受信期限。ヘッダーの一部や終端に達していないパケットを受け取ると計時し、続く断片で延長しない。メッセージ間のアイドル待機には適用しない。未指定（null）は期限なし。

## 実行環境と検証

サーバーはASP.NET Core Runtime 10を必要とする。管理サーバーは従来からこのFrameworkReferenceを持つ。Gatewayにもサーバー基盤経由の依存が加わる。Native/Managementのクライアントにはこの依存を追加しない。通常の.csprojのビルドにはSDKが必要であり、運用時の共有ランタイムと区別する。

Windows Debug/Releaseの既存通信テストと追加の分割・不正フレーム・借用・切断・HTTPSテストを使う。性能比較はReleaseで、移行開始時の作業ツリーから凍結したGateway DLLと同じクライアント・試験プログラムを使う。`Seamless.Database.Gateway.Tests.dll --transport-probe`は正式NativeQueryServer/Clientを別プロセスで起動し、小RPC、大きい結果、再接続、待機接続を測る。独立したecho実験ではない。再実行では全子孫を共通8論理CPUへ制限し、基準DLL、Releaseテスト成果物、作業、データ、キャッシュ、ログをFへ置く。

待機1万TCP接続と、1万の同時問い合わせ・認証・取引・結果転送の保証は区別する。物理NICを通す別PC試験、接続戦略三種のGUI、全入口の認可、全体予算はこの差替えだけで完成しない。

比較を再実行する場合は、Releaseのテスト出力と、移行前の整合するGateway DLLを用意して`TestSeamless/Invoke-TransportComparison.ps1`へ指定する。既存のクライアント・DBの依存DLLを同じものにそろえ、移行対象以外の変更が混ざったビルドを基準にしない。短時間に繰り返すとTIME_WAITと一時ポートが負荷生成側の制限になる。上限に達した結果を1万接続成功と数えず、OSのポート再利用・TIME_WAIT設定は変更しない。試験ツールは各方式の開始前にTIME_WAITが2,048件以下へ回復するのを既定で最大240秒待つ。これは要求の再送ではなく、負荷開始前の環境確認であり、空きポート数や負荷成功を保証しない。TCP状態を取得できない場合は開始しない。`-Variants baseline`または`-Variants kestrel`で単独にも実行できる。

Windowsでの今回の測定値、失敗を含む条件、未実行範囲は[検証結果](kestrel-verification-20261104.md)を参照する。Kestrel化だけで一律に高速化・メモリ削減できたとは判定しない。
