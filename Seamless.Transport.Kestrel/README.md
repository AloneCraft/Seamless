# Seamless.Transport.Kestrel

.NET 10の公開APIを使うサーバー用の接続基盤。DB、FLinq、管理GUI、利用者認証に依存しない。

- `KestrelTcpHost`: 独自TCPプロトコルを`ConnectionHandler`へ接続する。必要な入口だけHTTPS接続ミドルウェアを使う。HTTPへの変換はしない。
- `KestrelHttpHost`: 標準HTTP処理へ要求を渡す。MVC/Razorを登録しない。
- `ConnectionQuota`: TCPと独立したIPC受付の間で接続枠を共有する。
- `LengthPrefixedPipe`: 長さ付きフレームの入力を解析中だけ借用する。解析結果に借用バッファの参照を残さないこと。大きい分割本文は受信パイプを消費しながらプールへ退避し、終了・例外時に消去して返却する。
- `DuplexPipeStream`: 既存Stream処理用の借用アダプター。DisposeしてもKestrel所有のパイプを完了しない。

入力と出力はそれぞれ一つの処理が使用する。ホストは接続とパイプの所有者、呼び出し元は認証主体・要求・取引・結果列挙の所有者とする。独自TCPへHTTPの要求長・タイムアウト制限が適用されるとは扱わない。64KiBの読み書き設定はバックプレッシャーの閾値であり、全接続の厳密なメモリ上限ではない。

停止時には接続の停止とアプリ処理の完了を別に待つ。Kestrelの停止期限後にもハンドラーが残り得るため、内部のApplicationLifetimeで開始済み処理のfinally完了まで所有者を保持する。

NuGetパッケージは追加しない。実行にはASP.NET Core Runtime 10を使い、通常のプロジェクトのビルドには.NET 10 SDKを使う。クライアントライブラリはこのプロジェクトを参照しない。

Seamless本体のMITライセンスは[LICENSE](../LICENSE)を参照。ASP.NET Core/.NETランタイムはframework-dependent配布に同梱しない。ランタイムを別途配布する場合は、その配布物に付随するライセンスと第三者通知を保持する。

依存フレームワークの一次資料: [ASP.NET Core LICENSE](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt)、[ASP.NET Core第三者通知](https://github.com/dotnet/aspnetcore/blob/main/THIRD-PARTY-NOTICES.txt)。
