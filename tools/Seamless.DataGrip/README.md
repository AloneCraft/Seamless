# DataGrip接続の段階実装

`Document/20261114Argon.md` に対する実装・検証用。2026-11-02 の実装指示を合意根拠とする。

## 段階2の着手結果

段階1の専用ホストを維持し、JDBCの契約を修正した。SQLエンジンと共有トランザクション実装は変更していない。

- `?` を文字列・識別子・コメントと区別して数え、未設定・余分なパラメーターを送信前に拒否する。入力エラー後も接続を利用できる。
- 型指定付き `setObject` で指定されたJDBC型を保持する。INTEGER変換の形式不正・範囲外、型付きNULL、日時小数秒を検証する。未対応のCalendar・scale付き等のoverloadは黙って無視せず拒否する。
- 保存点を接続と取引世代へ結び付ける。他接続の保存点、解放済み・commit後・先行保存点へのrollbackで失効した保存点を拒否する。名前付き／番号付きのAPIを区別する。
- Statementが他の要求の結果を閉じないよう所有を検査する。ResultSetの同一性、複数結果の暗黙close、接続closeによる従属資源のcloseを維持する。
- network timeoutとquery timeoutを分ける。無視していたREAD_COMMITTEDと未完成batchは対応済みと広告せず、明示的に未対応エラーとする。

`Stage2JdbcAcceptance` は13ケース。修正前はCRUD以外の12ケースで失敗し、修正後はDebug・Release × 初回・再オープンの4条件で全13ケースが成功した。段階1の2試験も各条件で成功した。正常停止4回、既存runの再初期化拒否2回も確認した。

DataGrip側の追加部品は [plugin/README.md](plugin/README.md) を参照する。独自DBMS識別と表別名を使わないDML生成設定を持つ、JDBCとは別のプラグインである。2026.1.4.1のSDKでビルド・リンク・設定の確認は成功した。分離headless IDEはライセンスがなく起動できず、既存IDEへの導入は許可待ち。新プラグインでのGUI編集は未受け入れである。

最終成果物は `D:/CodexTemp/datagrip-stage1-20261102-03/`。修正前後・SDK・ビルドの証跡は `F:/CodexTemp/datagrip-stage1-20261102-05/`、13ケースの最終試験とSDK成果物の記録は `F:/CodexTemp/datagrip-stage1-20261102-07/`。段階2全体には、キーなし行識別、取消の要求所有権、確定結果照会、準備済みメタデータ等の未実装・未受け入れが残る。

## 目的と境界

新規の `DataGripStage1` DB にだけ接続し、SeamlessDB という製品名で JDBC 接続、表・列・主キー、型・NULL、データグリッドと表編集ダイアログの利用可否を調べる。SQL Server、AdventureWorks、既存の DataGrip 設定を初期化しない。DBeaver は必須対象に含めない。

この段階の通信は loopback 専用の実験契約であり、正式な配備・認証・通信方式の採用を意味しない。既存 MemoryPack Native 経路は保持する。スキーマ世代・行識別・取消の要求ID・確定結果照会・38桁数値・全JDBC契約を必要とする段階2以降の受け入れとは分ける。

## 参考試作の監査と採否

- フレームと型タグの骨格、SQL実行基盤への接続、SQLを発行しないメタデータ取得を、段階1の実験に限定して採用候補とする。
- 接続前にも任意操作を受理する状態、別接続から接続IDだけで取消できる状態、最大値の検査前にメモリを確保する箇所は、そのまま採用しない。
- `getTableTypes` 未実装、メタデータのcatalog・列pattern無視、ワイルドカードのescape無視、`getMoreResults` の完了後再読取、JDBC型とJavaクラスの不一致、未設定引数、batch失敗、savepoint所有権・isolation申告は、未完成として扱う。
- proxy方式そのものは小さい接続試験に利用できる。未対応機能を成功扱いしたり、実機確認なしにGUI編集対応済みとはしない。
- バイナリ方式は小さいフレームと逐次行転送を検証する仮選択。JSON等との転送量・割当・時間の比較と、恒久契約の採用は後続判断として残す。

## 検証と保護

作業・DB・ビルド成果物はD、キャッシュ・ログ・開始時ハッシュはF。`TestSeamless/Invoke-LocalProcess.ps1` を通し、子孫共通のCPU affinityを0xFF以下にする。専用runのowner情報、PID・生成時刻、ファイルlockを保持し、stop fileで自分のサーバーだけを停止する。既存の共有DBや他者のプロセスを停止しない。

JDBC経由のテストとDataGrip MCPのテストを記録する。実データグリッド・表編集ダイアログの確認は別項目にする。GUI操作はホスト入力の利用可否を確認してから行う。

開始時の未コミットファイルのSHA-256一覧は `F:/CodexTemp/datagrip-stage1-20261102-01/baseline.json`。既存変更へ追記する場合は局所差分に限定し、それ以外はハッシュ照合で保護を確認する。

## 再実行

PowerShellからリポジトリルートで実行する。Cドライブの一時出力を避けるため、サーバー・Java起動も同梱スクリプトを利用する。

```powershell
& tools/Seamless.DataGrip/Build-Stage1.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example -Configuration Release
& tools/Seamless.DataGrip/Build-Stage1.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example -Configuration Debug
& tools/Seamless.DataGrip/Start-Stage1.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example -RunName run-first
```

起動コマンドは停止まで待機する。別のPowerShellで `D:/CodexTemp/datagrip-stage1-example/run-first/ready.json` を読み、次を実行する。

```powershell
& tools/Seamless.DataGrip/Test-Stage1.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example -ReadyFile D:/CodexTemp/datagrip-stage1-example/run-first/ready.json
```

DataGripの「データソースおよびドライバー」で新しいユーザードライバーを作り、`D:/CodexTemp/datagrip-stage1-example/jdbc/seamless-jdbc-stage1.jar` と `dev.seamless.jdbc.SeamlessDriver` を指定する。「データソースの作成」から、ready.jsonのURLだけを専用接続に指定する。製品の種類は汎用のままとする。

停止はready.jsonのPIDとcreationUtc、および同じrunのowner.jsonを照合したうえで、run内に `stop` ファイルを作成する。既存runを自動削除・再初期化しない。サーバーの終了コード・receiptを確認するまで所有lockの解放完了と扱わない。

既存の専用DBを再接続する場合だけ、正常停止・オフラインバックアップ後に同じRunNameへ `Start-Stage1.ps1 -Reopen` を明示する。この経路は初期DDL・seedを再実行せず、ownerの旧記録を保存する。`-Reopen` なしの既存run利用、および所有情報のないrunへの `-Reopen` は拒否する。

両構成のCLI回帰確認は次で実行する。毎回異なる専用runを作り、表・型・NULL・SQLエラー後の再利用に加え、取引内の更新・読取・rollback、既存runの再初期化拒否、明示的な再オープン、正常停止を確認する。

```powershell
& tools/Seamless.DataGrip/Test-Lifecycle.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example-tests
```

## 現在の結果

段階1の実接続・GUI能力調査は完了した。Debug・Releaseビルドは警告0・エラー0。DataGrip実画面で表・6列・主キー・2行のグリッド表示を確認した。日本語、decimal、bit、binary、datetime2(7)、NULLを表示できる。

GUI編集の未対応を成功へ数えない。表構造ダイアログは開けるが、`Name` を `nvarchar(80)` から `nvarchar(81)` へ変えるドラフトでは `don't know how to alter type of dbo.Stage1Items.Name` となる。ドラフトは80へ戻して取消し、DDLは実行していない。

グリッド更新のプレビューは `UPDATE dbo.Stage1Items t SET t.Name='GUI確認' WHERE t.Id=1`。送信はエラー102となり、同じSQLでCLIでも再現した。現在のT-SQLフロントエンドと汎用SQL生成の不一致である。GUIの編集値は元へ戻し、独立したJDBC接続でもDBの値が `日本語` のまま維持されていることを確認した。T-SQL形式のパラメーター付き更新、取引内読取、rollbackはCLIで成功する。

この比較で、試作サーバーのSQL経路が `SessionContext.Run` を二重に包み、更新後の取引コンテキストを上書きして読取を待機させる問題を再現した。SQLエンジン内部の既存管理へ委ねる最小修正後、Debug・Release × 初回・再オープン × 基本試験・更新比較の8件が成功した。共有SQLエンジンは変更していない。

段階2ではJDBCの標準CRUDだけで完了とせず、DataGrip側のDBMS／方言・SQL生成拡張と、言語を明示したSQLフロントエンドを扱う。任意SQLの正規表現置換やSQL Serverプロファイルの偽装で回避しない。

最終検証の成果物: `D:/CodexTemp/datagrip-stage1-20261102-02/`。8件のCLI回帰・正常停止・再初期化拒否の証跡: `F:/CodexTemp/datagrip-stage1-20261102-04/`。GUI調査と修正前のタイムアウト: `F:/CodexTemp/datagrip-stage1-20261102-03/`。編集前の有効なオフラインバックアップは `D:/CodexTemp/datagrip-stage1-20261102-01/backup-before-gui-offline-02/`、ハッシュ台帳は `F:/CodexTemp/datagrip-stage1-20261102-02/gui-backup-manifest.json`。DataGrip接続は `D:/CodexTemp/datagrip-stage1-20261102-01/run-01/ready.json` に対応する。
