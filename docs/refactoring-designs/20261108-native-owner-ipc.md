# 正式業務RPCのOS所有者IPC

## 引き継ぐ契約

同一PCの業務クライアントはサーバー所有者と同じOSアカウントを確認して接続し、パスワード入力を省略する。別PCのTLS・認証・認可は既存の入口へ接続する。DBファイルの所有はサーバープロセスに置く。

## 観察と設計

管理RPCのLocalOwnerIdentityはCurrentUserOnlyとWindowsの接続元SIDを確認する。OS確認部分をSeamless.Transport.KestrelのLocalPipeOwnerへ取り出し、管理のUserAccount変換は既存型に保持する。正式NativeQueryServerも同じ確認を使う。

NativeQueryServer.CreateLocalOwnerとNativeQueryClient.ForLocalOwnerを追加する。有限の接続・要求・フレーム予算を持つ非同期NamedPipeServerStreamを受付け、既存LengthPrefixedPipeとConnectionAsyncへ渡す。最初の有界フレームを読んだ後、解析・実行より前に接続元を確認する。Windowsの偽装はSID取得だけに限定する。LinuxのCurrentUserOnlyによる接続時確認も引き継ぐ。

クライアントは同一ホストのCurrentUserOnly NamedPipeClientStreamを使い、既存の行ストリーム・取消・取引・接続解放へ接続する。指定するレコード予算と型制限も既存会計へ渡す。通信方式の自動切替は設けない。受付停止後は接続処理と取引の終了を待つ。接続数の既定は32、上限254とする。

## 確認する範囲

Windowsで正式生成クライアントの共有・循環・正規化、部分読取、実行中取消、明示取引、切断時の未確定取引回収、停止・再開を確認する。管理IPC・TCP/TLSを回帰対象とする。別OSアカウント、Linux、共通ApplicationHost・配布物との統合は作業台帳へ個別に残す。

OS確認は.NETのCurrentUserOnlyとRunAsClientの契約へ合わせる。[PipeOptions](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0) [RunAsClient](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.runasclient?view=net-10.0)

## 合意状態

合意済みの同一所有者IPCを正式業務RPCへ実装する。既存のwire形式、TLS認証、DB保存形式と取引規則を引き継ぐ。

## 実行結果

Windowsの生成クライアントで、Gateway有無×明示取引有無の取消・先行行保持4組、突然の切断で未確定行を取り消して同じキーへ書き直す2組、停止中の処理取消・予算回収と再開2組がDebug・Releaseで成功した。別PIDの生成クライアントもGateway有無の2組で同じIPC接続を継続し、先行2行だけを確定した。子のNamedPipeClientStream使用、PIDと作成時刻、CPU affinity 0xFF、レコード予算0を保存する。

共通受信ループ・TCP/TLS・所有型・接続プールを含む回帰は両構成各102件、共通化した管理IPCの所有者確認・予算・無効アカウント拒否・TLS認証は各16件が成功した。Transport・Client・Gateway・Management.Serverの両構成ビルドは警告・エラー0件。証跡は `F:\Seamless\native-owner-ipc-20261108` のpostboot-*-tests、postboot-management-*-testsと各ビルドのTRX・receipt.json・ログへ保存する。

最初の試験は変更前DLLのIPC署名不足でコンパイルに失敗した。Gatewayの初回ビルドではSystem名前空間の衝突を検出し、global指定へ修正した。広域試験はWindows再起動で中断し、管理側には再起動前のメモリ不足も記録された。元の失敗を保持し、元プロセスの停止とメモリ監視の復旧を確認してから同じDLL・新しい隔離DBで構成ごとに再実行した。メモリ不足の原因究明はW11へ引き継ぐ。

共通ApplicationHostへの受付統合、別OSアカウントの実拒否、Linux・配布物とIPC先読みの全資源会計は継続する。
