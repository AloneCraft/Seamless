# DataGripのSeamlessDB拡張（段階2・実験用）

これはJDBCドライバーとは別の、DataGrip側の小さいプラグインである。独自の通信・SQL実行・DB保存処理はJDBCと既存Gatewayが担当する。

段階1ではGeneric接続が表別名付きのUPDATEを生成し、現在のT-SQLフロントエンドがエラー102で拒否した。今回はSQL本文を置換せず、公開されているDML生成の設定を変更する。製品識別は独立した `SEAMLESS / SeamlessDB` とし、更新・削除の表別名を不要とする。その他の拡張はGenericへフォールバックする。

想定する生成形の例（実機での生成・送信は未受け入れ）:

```sql
UPDATE dbo.Stage1Items SET Name=? WHERE Id=?;
```

実装の本体は `src/dev/seamless/datagrip/SeamlessDbms.java` と `SeamlessDmlHelper.java`、登録は `resources/META-INF/plugin.xml`。DBMSの検出は実機APIの `Dbms.forConnection` が参照するJDBC URL・driver classを対象にする。SQL Server用の製品名・プロファイルを使わない。

## ビルドと受け入れの境界

```powershell
& tools/Seamless.DataGrip/Build-Plugin.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example
```

ローカルDataGripのSDKへ直接コンパイルし、Java 21のリンク、独立したDBMS識別、UPDATE/DELETEの別名設定をCLIで確認する。通常プラグインと、検証コマンドだけを持つ別の試験用JARを生成する。試験用JARは利用者のIDEに導入しない。

```powershell
& tools/Seamless.DataGrip/Test-Plugin.ps1 -DataRoot D:/CodexTemp/datagrip-stage1-example -CacheRoot F:/CodexTemp/datagrip-stage1-example-tests
```

これはD/Fに専用設定・plugins・キャッシュ・ログを作る独立headlessプロセスであり、既存IDEを再起動せず、既存設定・既存プロジェクトを開かない。公開拡張の実登録、JDBC URL検出、実DML生成と束縛順序の検証を行う設計である。現在の実機では専用設定にライセンスがなく `No valid license found` で終了したため、この経路は未受け入れである。既存のライセンス情報はコピーしていない。

確認済みはDataGrip **2026.1.4.1 / 261.27258.144** のSDKでのビルドとリンク・設定だけ。IDEロード、生成SQL、実画面のグリッド更新は別の受け入れである。対応ビルドは `261.*` に限定し、それ以外へ互換性を推定しない。

既存IDEへの導入は利用者の許可待ち。Windows GUIによるソフトウェア導入にはcomputer-useの確認規約が適用される。現時点では既存IDEへ未導入であり、再起動も行っていない。

列型変更のDDL生成、キーなし表の安全な編集、SQL方言の意味論、取消・要求所有権はこのプラグインの現状の受け入れに含まれない。段階2全体の完成品として配布しない。

## 参照した公式API資料

- [DataGrip Plugin Development](https://plugins.jetbrains.com/docs/intellij/data-grip.html)
- [DataGrip Extension Point and Listener List](https://plugins.jetbrains.com/docs/intellij/data-grip-extension-point-list.html)

これらの公開一覧に加え、インストール済みSDKの `Dbms`、`BaseDmlHelper`、`DmlHelper`、`DbmsExtension`、同梱XMLをCLIで照合した。オンライン資料だけから実機のAPIを推測していない。
