# OSSへの完全移行ゲート

## 合意と状態
2026-10-29追加合意。標準OSS、有償任意を維持し、ScriptDomからの完全移行を目標とする。GSP製品への全面依存ではない。SqlParserCS0.6.5は候補であり、必須構文・診断・実行互換が未検証のまま採用完了としない。着手時18ファイル（15 C#、3 csproj）に直接利用があり、製品コードの完全移行は未完了。

## 対象と手順
1. 導入fixtureの基本SQL・CRUD・SSMS/MySQL Workbench初期化SQL・バッチ/引数型宣言・不正入力を固定し、旧経路と独立期待値を保存。171viewの抽出は任意の対応状況調査に限る。
2. OSS parserのAST/位置/診断/処理速度/割当を試験。失敗構文を必須補完/対象外へ分類し、利用者の目的補正によりXML/.nodes/.value/APPLY/PIVOT/複雑なUDFは補完対象にしない。既に動く機能は再利用可能な範囲で残し、未対応は明示する。
3. Seamless所有の構文表現と演算enumを導入。旧binderの閉包から外部ASTを排除し、型付きIRへlowering。既存bounded実行アルゴリズム・SQL Server意味論は維持。
4. SELECTに加えてGateway CRUD、RoutineAnalyzer、SystemCatalogEmulator、TDS ParameterDeclarations、SqlConsole、ComparisonRunnerの検査/SQL生成を移す。
5. package利用側を再buildしDebug/Releaseで導入fixtureの結果・metadataとSSMS/MySQL Workbenchの基本操作を確認し、ScriptDomの直接/推移的package参照を除去する。全ビュー一致を除去条件にしない。途中の旧経路は比較用であり完全移行と呼ばない。

Microsoft.SqlServer.TransactSql.ScriptDomと同じnamespaceに偽装クラスを作って依存ゼロを装わない。ソーステキストgrepのゼロだけで完了にしない。履歴文書の名称・比較資料は削除対象ではない。評価用旧版は非出荷の隔離環境だけで用い、最終製品/consumerの依存へ入れない。

## 完了判定
- sourceの直接namespace利用、csproj参照、全buildのproject.assets.json、出荷nupkg/nuspec/同梱DLLと公開signatureにScriptDom依存がない。
- 基本SQLに必要なSQL Server認証/DB選択/RPC/prepare/引数/型/NULL/multi-result/取消/reset/取引とSSMSの接続・一覧・SQLエディタ操作が回帰しない。接続だけを完全互換にしない。
- 171ビューの完全実行は必須ではない。導入用fixtureの基本SQLをSQL Server/MySQL原本と照合し、列metadataとSQL意味論を確認する。既存DBの未対応ビュー等は診断・報告し、結果を捏造しない。
- typed nativeの生成器・実行・取引/保存を変えず、計画cache・逐次送信・資源上限を保持する。
- 未対応・測定不能・環境無し・実装無しを成功へ数えない。導入体験に必須の基本構文がOSS未対応なら代替OSSまたは狭い範囲の実装を検討し、vendor独自拡張の穴埋めはせず、削除だけ先行しない。

## 依存棚卸しツール
`python tools/sql-migration/audit.py --root .` は候補をJSONで表示する。`--require-clean` は残存時exit1、未評価assets時exit2とする。`--assets <project.assets.json>` を複数指定して実際の復元依存を検査する。source/packageの検査でありSQL実行・バイナリ公開signature・nupkg・GUI検証を代替しない。第三者の未コミット変更を自動修正しない。
