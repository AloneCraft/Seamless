# 履歴保存方式の小規模比較

現行の`HistoryStore`と、Tsavoriteのhead索引＋専用追記arenaを比較する独立したprototype。製品の共通取引、WAL、Gateway、Native、FLinqの保存先を切り替える機能ではない。単体appendの時間を業務commitの時間とは呼ばない。

## 条件

- 1,024キー、キー8/32バイト。主索引64バイトと32KiBを両方式で使う。
- 行32バイトで1/8/64版、行512バイトで1/8版。
- Memoryのログ/arena上限8MiB、Storageの常駐設定8MiBまたは64KiB。専用版にはheadのnativeログとarena cacheが別にあるため、合計の実量・予約を比較する。
- 各ownerの共有メモリ予算64MiB、一時ディスク予算256MiB。上限いっぱいのメモリを先に確保する指定ではない。
- 同じGarnet 2.2.5、Release、共通8論理CPU。ウォームアップ後5反復し、方式の先行順を交替する。
- 5形状×キー2種×保存/常駐3種×索引2種×方式2種×5反復の600標本。

`-BufferedControl`はStorageの両方式をBuffered/RandomAccessへ揃える追加の400標本。既定NativeデバイスとBuffered arenaの差を、履歴形式だけの改善へ読み替えないための対照条件である。別のTaskRoot/LogRootで実行し、snapshotを分けて保持する。BufferedのOS file cacheはowner予算やprocess private bytesの外にある。

各caseは新規の専用データで始める。保存、再open、回収、値の照合を終え、絶対パスと所有markerを確認した後にcaseのディレクトリを回収する。全case分の保存領域を累積しない。Fのspec、ソースmanifest、結果、receipt、ログは残る。

## 実行

`BuildLogRoot/packages`にはGarnet 2.2.5と推移依存を含む隔離キャッシュを用意する。既存の自分の検証キャッシュを`-BuildLogRoot`で再利用できる。ログとキャッシュはF、ソースsnapshot・temp・実データはDを使う。

```powershell
& .\TestSeamless\Invoke-HistoryStoreModes.ps1 `
    -TaskRoot 'D:\CodexTemp\Seamless\history-modes' `
    -LogRoot 'F:\CodexTemp\Seamless\history-modes' `
    -BuildLogRoot 'F:\CodexTemp\Seamless\my-existing-cache' `
    -Configuration Debug -VerifyOnly

& .\TestSeamless\Invoke-HistoryStoreModes.ps1 `
    -TaskRoot 'D:\CodexTemp\Seamless\history-modes' `
    -LogRoot 'F:\CodexTemp\Seamless\history-modes' `
    -BuildLogRoot 'F:\CodexTemp\Seamless\my-existing-cache' `
    -Configuration Release

# I/Oを揃える場合は、別のTaskRoot/LogRootにして上のRelease実行へ-BufferedControlを付ける。

& .\TestSeamless\Test-HistoryStoreModes.ps1 `
    -TaskRoot 'D:\CodexTemp\Seamless\history-modes' `
    -LogRoot 'F:\CodexTemp\Seamless\history-modes'
```

Debug/Releaseの検証入口は、版選択、削除/再挿入、ページ境界、保存ページの不変性、回収、再open、世代付き参照、schema拒否、checksum、切詰め、満杯、確定後の遅延ファイル回収を確認する。1,024個の32バイトキーの回収も確認する。これは比較中に見つけた、現行接続がcallbackのアドレスを誤用する不具合の再発防止を含む。

## 専用arenaの構成

4KiBページと有界cacheを持つ。40バイトのヘッダーはmagic、commit版、同じ行のprev offset、行長、削除フラグ、非暗号学的checksumを格納する。行の先頭にあるrow IDと版も照合する。ページをまたぐnodeを作らず、拡張・cache・回収の新旧領域は先に共通予算へ予約する。返す行はコピーし、page spanを外へ持ち出さない。

headは通常のTsavorite辞書にoffsetを保存する。その索引、overflow、ログの費用も比較へ含める。独立した履歴索引を削っただけでTsavoriteの固定費が消えるとは扱わない。

arenaはheadと同じStore manifestのcomponentで、参照先とheadをflushしてからmanifestを確定する。保存済みの最終ページは後の追記で書き換えない。静止回収では最新版だけを新世代へ移し、削除headを外す。共通保存の確定後に古い保存を無効にして旧ファイルを回収し、削除できなければ次の後始末へ保留する。公開参照はopenのIDと回収世代で検査する。

## 測定の意味

append、最新/最古の読取り、保存、再open、静止回収を別々に記録する。読取りは128要求のp50/p95/p99を各反復で測る。Storageにはアプリ側cacheを一度排出した後のbatchもある。OS cacheは消さず、すべての要求がcoldであるとは扱わない。

監査の`OwnerPeakMemory`は、各caseの再open前後のownerの予約ピークの最大値から求め、回収中も含む。追加時だけのピークとは区別する。[実測結果と採用判断](../../../docs/refactoring-designs/20261103-history-store-comparison.md)を参照する。

`Before/After`は各時点のnative索引payload、nativeログ確保量、arena buffer、Store予約を分ける。予約量とプロセス全体のメモリは別で、プロセスprivate bytes/working setは20ms周期の観測ピークである。managed割当は各区間の累積値。OS転送量は検証や保存・回収を含むcase全体のプロセス転送カウンターで、物理ディスクI/Oではない。arenaのRead/WriteCallsは各時点のarena世代の値である。

保存容量はarenaとnative/checkpoint/manifestを分けたファイル長の合計。WALはこのprototypeに含まない。共通WALのSync、複数表公開、並行操作・Pendingの寿命を含む採用試験は、製品へ接続する場合の別のgateとして残る。未知のcodec、一般の複合キーからのrow ID割当、非静止回収、任意の過去版の永続公開は今回の実装範囲ではない。
