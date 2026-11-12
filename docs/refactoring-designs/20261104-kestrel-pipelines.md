# Kestrel公開APIとPipelinesへの通信基盤移行

## 目的と維持する境界
利用者の明示指示に従い、管理TCP/TLS、ネイティブRPC、TDS、HTTPを順に移行する。DB・FLinq・GUIへ依存しない小さいサーバー用接続基盤を分離する。公開ConnectionHandler・接続ミドルウェア・IDuplexPipeを使い、HTTP以外のプロトコルはそのまま保持する。クライアントにASP.NET Coreの依存を追加しない。MVC/Razor、Kestrel内部コードのコピー・フォークは導入しない。

TCP/TLSの接続寿命、認証主体、要求、明示取引、結果、永続操作を別に扱う。証明書・ホスト名の検証、IPCの同一OS利用者制限、接続ごとの取引占有、切断時の未確定取引の取消、WAL・世代公開・結果照会を維持する。移行を理由に更新要求を自動再送しない。

## 移行前の正式経路と制限（設計時の基準）

以下は移行前の基準である。移行後の現在の入口・設定と候補ごとの検証結果は、[通信仕様](../kestrel-transport.md)と[検証記録](../kestrel-verification-20261104.md)を参照する。
管理はTcpListener/SslStreamとNamedPipeを使い、合計32接続、TLSと最初の要求の期限は各15秒、長さ付きJSONの上限は16MiB。クライアントは要求ごとに接続する。IPCはCurrentUserOnlyを維持する小さいアダプターを残せる。

ネイティブはLoopbackのTcpListenerとMemoryPackで、接続内の入力Channelは容量1、フレームは種別を含む16MiBまで。ラムダでは種別別の上限と構造検査も行う。クライアントは接続再利用と明示取引の占有を行う。現状の入口をインターネット向けの認証済み入口として扱わない。

TDSはIPv4/IPv6のLoopbackを同じポートで待ち受け、backlogは1024。セッションは既にPipeReader/PipeWriterを受け取る。HTTPはHttpListenerで全インターフェースを試し、権限不足時はローカルへ切り替える。HTTPのセッション・取引トークンは利用者の認証とは異なる。

独自プロトコルへHTTP用の制限が自動適用されるとは扱わない。既存にない全体の接続・実行・送信者/受信者の期限や予算は、移行時に不足として記録する。上限を1万に増やすだけで完了としない。

## 構成
新しいサーバー用の小さいプロジェクトで、Kestrelの起動・終了、公開接続ハンドラー、共用できる接続数の枠、Pipeと既存Stream処理の接続を提供する。WindowsServerの既存Microsoft.AspNetCore.App FrameworkReferenceを活用する。HTTPは標準の要求処理へ移し、既存エンドポイントの入出力・セッション処理を引き継ぐ。

既存クライアントを使って起動の差替えを検証する。段階的にフレーム処理をPipeへ接続し、完全なフレームはReadOnlySequenceから解析し、不完全なフレームでは上限内の所有バッファへ段階的に移す。AdvanceTo後に入力を保持せず、プールへ返す領域の寿命を明示する。MemoryPackが実際に減らすコピーと、TLS・DTO生成等に残るコピーを区別する。

## 作業順と基準
変更前の正式ソース・ハッシュ・行数・バイト数を`F:/CodexTemp/SeamlessKestrel-20261104-01/baseline-source`へ保存した。Git HEADとの差分を、保護する既存未コミット変更の量と混同しない。性能の基準は正式な旧サーバーを使用し、比較プローブと設定・ランタイムを保持する。

管理→ネイティブ→TDS→HTTP→共通化と旧コード整理の順で進める。ソース行数（空行込み/非空行）、配布物のバイト数、実行時メモリは別々に記録する。性能はRelease、同一PC内、共通8論理CPU、同じ認証・取引・耐久性条件で比較する。1万接続は段階的に増やし、負荷生成側・一時ポート・CPU・メモリの制約、失敗と未実行を報告する。

## 検証と配布
管理のTLS/IPC・ログイン・証明書不一致・GUI、ネイティブの準備/実行/逐次結果/取引/切断取消、TDS/DataGrip、HTTP、ヘッダーと本文の分割・連続フレーム・不正長・途中切断・低速通信・停止/再起動を確認する。Windows Debug/Releaseと利用できるLinuxを区別する。MITと配布対象の第三者通知を保持する。実行用ランタイムと開発用SDKを区別し、ソリューションへ新しいプロジェクトを登録する。

## 未決事項と残る範囲
HTTPは利用者の回答によりインターネット公開に対応する方針となった。待受けアドレスとHTTPSを明示設定可能にし、認証・認可の実装状態と区別する。HTTPは社内・インターネットの両方に対応できるが、インターネット公開に必要なHTTPS・本人確認・共通認可が既に完成したとは扱わない。接続維持・アイドル解放・操作後切断の三方針、全入口の共通認可、NICを通した別PC試験はKestrelへの差替えだけで完了としない。

### 共通の実行枠と既存取引の完了

HTTPでは現在、Kestrelの要求枠を取引の所有権確認より先に取得する。枠が1つのとき、別のSQL要求がDBロックを待って枠を占有すると、ロック所有者のCOMMIT・ROLLBACKは503になった。待機側を取り消して枠が戻ると同じ取引を完了できた。Nativeの保護入口には、所有する取引IDの既知の制御を個別枠から外す先行実装がある。TDSはトランザクションマネージャーのメッセージとSQLバッチの両方から取引を制御できる。[再現記録](../kestrel-verification-20261104.md#2026-11-06-httpの同時要求枠と既存取引の完了)

HTTP・TDS・Nativeに一つの要求枠をそのまま掛ける構成では、上の待ちを全入口へ広げる。共通上限では、新規要求の枠と既存取引が完了できる枠を分ける。方式は明示取引ごとに開始時から予約する案と、確定・取消の専用枠だけを持つ案を比較中。前者は待機中も枠を使い、開ける取引数も制限する。後者は、専用枠が別取引の確定処理で埋まった場合にも進める条件を別途定める。

既存取引の扱いを適用する前に、HTTPのセッションと取引ID、保護HTTPでは元の資格情報の証明、Nativeでは接続と取引ID、TDSではその接続のセッションを検査する。SQLバッチ内の確定後に別の処理が続く場合は、その処理を開始済み取引に含めない。取引枠の方式を確定した後、通常枠が埋まった状態での確定・取消、取引ID不一致、切断・期限・停止での回収、HTTPセッションの待機、TDSの複数文を通す。通信上の既存エラー形式、認可、WAL、公開の契約を維持する。

公開APIの一次資料: [ConnectionHandler](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.connections.connectionhandler?view=aspnetcore-10.0)、[UseConnectionHandler](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.connections.connectionbuilderextensions.useconnectionhandler?view=aspnetcore-10.0)、[Pipelines](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines)。対象は.NET 10の公開APIとする。
