# Kestrel通信基盤

## 通信の入口

管理RPC、ネイティブRPC、TDSは独自プロトコルを維持し、Kestrelの公開ConnectionHandlerとIDuplexPipeで接続を扱う。HTTP APIはKestrelの標準HTTP処理へ接続する。SQL、DB取引、WAL、世代の公開と復旧はプロトコルの内側に残る。MVC、Razor、gRPCへ変換しない。

管理TCPだけにTLSを適用する。証明書・ホスト名の検証はクライアントで維持する。ネイティブとTDSは既存どおりLoopbackへ限定する。TDSのPreLoginを通常のTLS接続へ置き換えない。

管理IPCはCurrentUserOnlyのNamedPipeを残し、TCPと同じ要求処理へ接続する。IPCのOS利用者をTCPへ渡さない。TCPの受付を認証・認可の成功として扱わない。

## HTTPの公開設定

利用者の方針に従い、HTTPの既定はIPv6対応環境で`IPAddress.IPv6Any`、それ以外で`IPAddress.Any`とし、全インターフェースへ待ち受ける。IPv6対応環境ではIPv4/IPv6の両方を確認する。OSのHttpListener権限でLoopbackへ自動的に戻す動作はなくなった。公開IP、ルーター、ファイアウォールの設定によって外部からの到達範囲が決まる。本変更でそれらの設定は変更しない。

`HttpServerOptions.ListenAddress`で明示的なアドレスを選ぶ。`Certificate`を指定すると、そのHTTPリスナーをHTTPSにする。証明書は呼び出し元が所有し、リスナー停止後に破棄する。`GatewayServerOptions.Http`からも設定できる。ポート0を指定したとき、実際のポートはStart後にPortから取得する。

```csharp
var options=new GatewayServerOptions{
    HttpPort=8443,
    Http=new(){ListenAddress=IPAddress.Any,Certificate=serverCertificate}
};
```

既存のHTTPセッション・取引トークンは利用者認証ではない。HTTPへ組織認証・共通認可を接続する作業は未完了であり、Kestrel化やHTTPSだけでその実装が完了した意味にはしない。外部公開の認証構成は別途必要になる。既存のURL、JSON、セッションヘッダー、取引IDと結果照会を維持する。旧公開`HttpListenerContext`オーバーロードも同じ要求処理へ接続する互換入口として残す。

## 所有権とコピー

Kestrelが接続・受送信パイプを所有する。管理要求とネイティブ要求は長さ付きフレームから復元する。受信済み本文がそろっている場合はReadOnlySequenceから直接JSON/MemoryPackを読み、解析が終わってからAdvanceToする。分割された大きい本文では、受信済み部分を消費しながら上限内のプール領域へ退避する。全本文がパイプへ届くまで保持し続けるとバックプレッシャーで停止するため、この場合はコピーが必要になる。

ネイティブ出力はPipeWriterへヘッダーと既存の行ペイロードを書き、一度のFlushで送る。INativeQueryModuleのbyte[]結果の公開契約は変更しない。ラムダの版付き検証器とクライアント側の既存フレーム処理にはbyte[]の所有が残る。TDSはStream用の追加Pipeを作らず、KestrelのPipeを直接使う。TLS・復元先オブジェクトの割り当てもあるため、完全ゼロコピーとは呼ばない。

通常の接続切断は未確定取引を取り消す。明示的取引は接続を占有し続ける。結果列挙は切断・取消・サーバー停止で解放する。永続的な操作IDを持つ適用の結果は、通信の終了と区別して照会する。通信失敗した更新の自動再送は追加しない。

Kestrelの接続停止が完了しても、開始済みのアプリ処理が終わったとは扱わない。共通のApplicationLifetimeで新しい処理の登録を止め、TCPハンドラーとHTTP要求のfinallyが完了するまで待ってからホストを破棄する。Kestrelの接続停止期限を超える処理でも同様である。取消を無視する処理を強制的に安全終了できるという保証ではなく、残った処理の実行中にDB・セッションの所有者を先に破棄しないための境界である。

## 資源制限

| 対象 | 実装で維持・設定した境界 | 残る境界 |
|---|---|---|
| 管理 | TCP+IPC合計32。TLSと最初の要求は各15秒。JSON本文16MiB | 要求実行・低速送信先の独立した期限、全体の出力予算 |
| ネイティブ | 1接続の実行は逐次。入力Channel容量1。種別を含むフレーム16MiB、ラムダ準備は既存の小さい上限と構造検査 | 全体の接続数・同時実行数・準備処理数・低速送受信の期限 |
| TDS | LoopbackのIPv4/IPv6、backlog1024。メッセージ16MiB、パケット長検査。入力Channel容量1 | 全体の接続数・同時実行数・ログイン/低速送受信の期限 |
| HTTP | セッション既定128、アイドル5分。Kestrel標準HTTP処理。任意のHTTPS証明書、TLS15秒 | 利用者認証・共通認可、本文長・全体の同時実行予算 |
| ソケットPipe | read/write各64KiBの閾値、非同期I/O、NoDelay | 接続数×閾値とは別の、復元中フレーム・DB・全サーバーの予算 |

Channel容量1とは別に、受信処理が次の要求を復元して投入を待つ領域が一つある。64KiBはバッファの厳密な容量上限ではなく、最大サイズの一つの書き込みはこれを超え得る。HTTP本文長は旧APIに上限がなかったためKestrelの既定上限を無効化しており、Internet向けの上限設計は未完了である。独自TCPにHTTP用の期限・上限が自動適用されるとは扱わない。

## 実行環境と検証

サーバーはASP.NET Core Runtime 10を必要とする。管理サーバーは従来からこのFrameworkReferenceを持つ。Gatewayにもサーバー基盤経由の依存が加わる。Native/Managementのクライアントにはこの依存を追加しない。通常の.csprojのビルドにはSDKが必要であり、運用時の共有ランタイムと区別する。

Windows Debug/Releaseの既存通信テストと追加の分割・不正フレーム・借用・切断・HTTPSテストを使う。性能比較はReleaseで、移行開始時の作業ツリーから凍結したGateway DLLと同じクライアント・試験プログラムを使う。`Seamless.Database.Gateway.Tests.dll --transport-probe`は正式NativeQueryServer/Clientを別プロセスで起動し、小RPC、大きい結果、再接続、待機接続を測る。独立したecho実験ではない。全子孫を共通8論理CPUへ制限し、Dを作業・データ、Fをログ・キャッシュにする。

待機1万TCP接続と、1万の同時問い合わせ・認証・取引・結果転送の保証は区別する。物理NICを通す別PC試験、接続戦略三種のGUI、全入口の認可、全体予算はこの差替えだけで完成しない。

比較を再実行する場合は、Releaseのテスト出力と、移行前の整合するGateway DLLを用意して`TestSeamless/Invoke-TransportComparison.ps1`へ指定する。既存のクライアント・DBの依存DLLを同じものにそろえ、移行対象以外の変更が混ざったビルドを基準にしない。短時間に繰り返すとTIME_WAITと一時ポートが負荷生成側の制限になる。上限に達した結果を1万接続成功と数えず、OSのポート再利用・TIME_WAIT設定は変更しない。試験ツールは各方式の開始前にTIME_WAITが2,048件以下へ回復するのを既定で最大240秒待つ。これは要求の再送ではなく、負荷開始前の環境確認であり、空きポート数や負荷成功を保証しない。TCP状態を取得できない場合は開始しない。`-Variants baseline`または`-Variants kestrel`で単独にも実行できる。

Windowsでの今回の測定値、失敗を含む条件、未実行範囲は[検証結果](kestrel-verification-20261104.md)を参照する。Kestrel化だけで一律に高速化・メモリ削減できたとは判定しない。
