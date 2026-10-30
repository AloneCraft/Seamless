# SSMSの管理用一時表バッチと結果長設定

## 観測と境界
LOCK_TIMEOUT修正の反映後、実SSMSのクエリエディターでSetTextSizeStatementの40515が発生した。診断用14334番への再接続では、サーバー情報取得用の#SVer作成・xp_msver・host_info・SELECT・DROPを含むバッチが3906となった（`F:/Seamless/Comparison/tds-sql-trace-06/sql.jsonl`）。比較DBの読み取り専用は維持する。

既存SystemCatalogEmulatorには#SVerの互換応答があるが、ExecuteBatchが先頭CREATEをDDLへ振り分けるため到達しない。文字列に#SVerがあるだけの既存判定も厳密化する。

## 修正方針
- 採取したサーバー情報SQLを製品のEmbeddedResourceとして保持する。構文解析後の全非triviaトークンを比較し、この既知の管理バッチだけを既存互換応答へ接続する。追加・変更されたSQLは受理しない。
- 旧3結果の接続初期化も`SsmsConnectionInitialization.sql`との全トークン比較にする。SQL内の単語だけでバッチ全体を成功扱いにしない。DatabaseEngineTypeの引用符付き/なしの同義別名だけを明示的に許可する。通常の3本のSELECTは通常の複文経路で処理する。
- #SVerをユーザーDBに作らない。応答のServer_ServerTypeは実SQLのCAST(NULL AS int)と整合するNULL値・NULL可能metadataにする。後続の既知DROPは、同じ接続で互換バッチを実行した場合だけ一度受理し、resetで権利を消す。一般の一時表は提供しない。
- TEXTSIZEは値を切り捨てない既存の結果送信に対応する上限2147483647だけを受理する。小さな制限値や式を無視しない。@@TEXTSIZEはこの互換profileの上限をintで返す。実SSMSが送る値の確認は管理バッチの障害とは別に行う。
- 本物のSQL Serverへ同じSSMSから新規クエリを開き、92イベントを採取した（`F:/Seamless/Comparison/ssms-source-xevent-02/`、切り詰めなし）。TEXTSIZEの実送信値は2147483647だった。設定バッチ全体を`SsmsQuerySettings.sql`として保持し、そのまま回帰する。
- 同バッチのQUERY_GOVERNOR_COST_LIMIT 0はコスト制限なし、DEADLOCK_PRIORITY NORMAL（数値0も同義）は現在の共通優先度として受理する。正のコスト制限やLOW/HIGHなどの優先度を無視せず拒否する。コスト推定器やSQL Serverのdeadlock victim選択は今回実装しない。
- 任意SQLの結果や元DBの値を固定結果へ置き換えない。管理情報の限定互換として扱い、Core/C12の保存・取引・WALは変更しない。

## 受け入れ
実採取バッチの単一結果・列名・型・NULL、接続cleanup/reset、改変バッチや#SVerを含むだけのSQLの拒否、読み取り専用維持を確認する。TEXTSIZEは大きなUnicode/binaryの非切捨て・制限値拒否・後続SQL・接続再利用を検証する。新NuGetで同じ実バッチとSSMS相当の接続設定を実行し、データ世代の切り替え前後の確認を行う。実SSMS画面は利用者の確認まで未検証とする。

## ソース検証
最初の機能試験は修正前2件失敗/1件成功（管理バッチ3906とTEXTSIZE40515）。正常SQL Serverから採取した設定バッチ全体も、QUERY_GOVERNOR未対応で修正前に失敗した。旧初期化の優先順位競合・追加SQL黙殺の試験は修正前2件失敗/7件成功だった。

修正後はSSMS初期化、ロック待機、DDL、SharedV2、Runtimeカタログ、大値送信、元SQL Serverの大値読取を含めDebug/Release各67件成功、skip 0。証跡は `F:/CodexTemp/SsmsLockTimeout/initialization-routing-verified-{Debug,Release}.trx`。独立レビューで指摘された旧Contains分岐の競合も全token照合へ修正し、再レビューで追加の具体的欠陥は指摘されなかった。

## パッケージと稼働確認
19製品パッケージ`0.0.0-ssms.20261030003108756`から再構築した世代`20261030T003606639Z-0439e74d`はValidatedとなった。94対象の取り込み・TDS全値/列情報、SQL/native75ケース、生成受け入れ8項目、CRUD・取引・準備済みRPC、元DB一覧の再確認が成功した。

2026-10-30 10:04 JSTに14333番へ切り替え、正常性確認とSSMS資産公開が成功した。稼働先にSSMS同梱SMOで接続し、採取した管理バッチと設定一式、表/ビュー/ルーチン一覧、生成メソッドとBasic.sqlの4結果を確認した（`F:/CodexTemp/SmoAdventureWorks-v11-live/result.json`）。PackageReference利用EXEによる同じSQLの比較も成功した（`F:/CodexTemp/LiveBasic-v11/result.json`）。

利用者は同版への実SSMS接続と「新しいクエリ」の作成の両方に成功したと報告した。これにより今回の接続初期化エラーの解消を実UIでも確認した。ソリューション内の相対参照を修正した後、Basic.sqlを開いて4結果（20/67/20/290行）すべての実行にも成功した。オブジェクト展開と生成メソッド表示など、他のGUI受け入れは個別に確認する。
