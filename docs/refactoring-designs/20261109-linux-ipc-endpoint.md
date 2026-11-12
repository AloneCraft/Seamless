# Linux管理IPCの接続先

## 再現と原因

固定ReleaseのLinuxサーバーで、所有者の公式Manager起動CLIにサーバーと同じTMPDIRを渡すと成功し、クライアント側だけを`/tmp`へ変えるとタイムアウトした。証跡は`F:/Seamless/linux-desktop-recovery-20261109/owner-access-probe3`と`owner-access-tmpdir-red`。

.NET 10の論理パイプ名は各プロセスの一時ディレクトリへ解決される。絶対パスはそのまま利用できる。[.NETのパス解決](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Unix.cs)

## 修正する境界

- 管理サーバーはLinuxの待受けパスを構築時に固定し、その絶対パスで待ち受ける。
- 一時的なローカル接続情報へ任意の`UnixPipePath`を追加し、既存の論理名・インスタンスID・PID・プロセス識別を保持する。DBの保存形式は変更しない。
- Manager起動時は記録されたパスを利用する。相対パス、インスタンスの論理名に対応しないファイル名、`.`・`..`を含むパスは拒否する。
- 旧接続情報で新項目がない場合は従来の論理名を使う。既存の公開コンストラクターとメソッドの署名を保持する。
- CurrentUserOnlyとOS資格情報の検査を継続する。TCP/TLSの認証・認可は別の入口として扱う。

## 検証

隔離した管理サーバーとWebホストを起動し、異なるTMPDIRを持つ別プロセスから公式Manager起動APIを呼ぶ。Manager用接続情報と従来のローカルサーバー接続情報の両経路を通す。相対・不一致パスの拒否、既存接続情報の読み取り、OS所有者IPC、ホスト停止を近接回帰で確認する。固定配置の既存利用者DBを移行・再起動して試験しない。

最終のパス正規化を含む候補で、名前変更・応答喪失・DBと問い合わせDLLの回収を加えたLinux Debug・Release各37件が成功した。Windowsは長い検証ルートで34成功・3失敗を観測し、短い保存先で該当Debug3件とRelease37件が成功した。長いパスの製品制約はW10-PATHとして残す。証跡は`manager-boundary-linux-final`、`manager-boundary-windows`、`manager-boundary-windows2`。

上記の候補は8時間期限への変更前である。期限の変更はLocalBrowserSessionsの境界と既存セッション操作を別に検証する。稼働中の固定配置に新しい接続情報の項目がない場合は、そのホストを更新・再起動してから新経路を利用する。
