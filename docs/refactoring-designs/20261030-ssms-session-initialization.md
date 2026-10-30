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
