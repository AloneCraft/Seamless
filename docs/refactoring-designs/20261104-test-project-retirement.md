# Dynamicへの検証集約とTestプロジェクト廃止

## 背景
ユーザー承認済み計画に基づき、FLinq・ZLinqの振る舞い検証をDynamicへ集約し、DB・ストレージ・画面の検証を既存の専門プロジェクトへ移す。最終的にTest/Test.csprojを廃止する。

## 現状の観察
開始時点で旧Test配下に598個の追跡ファイルがある。DB生成モデル184ファイル、FLinq関連、ストレージ、ベンチマーク画面の検証が同居する。Execution.TestsとDatabase.Transactions.Testsに旧Testソースへの直接リンクがある。開始時点の未コミット変更を含む内容を移行対象として保護する。
移行前のnet10.0検出結果は2250ケース。ファイル単位の対応とハッシュは20261104-test-migration.jsonに保存する。実行ログ・隔離成果物はF:/CodexTemp/test-retirement-20261104、短いテストデータはF:/CodexTemp/stmに置く。開始時は成果物をDへ置いたが、利用者の追加指示によりFへ移設した。17385ファイル・5377710190バイトのSHA-256を照合後、Dの専用作業フォルダーと52個のテストデータディレクトリを削除済み。移設記録はF:/CodexTemp/test-retirement-20261104/d-to-f-transfer.json。

## 過去設計から引き継ぐ仕様
20261022-dynamic-verifier-consolidation、20261103-dynamic-fallback-consolidationを引き継ぐ。生成時・実行時のフォールバック検出を併用する。元のケースと保証を移植して検証するまで削除しない。ZLinqの非対応と失敗を区別する。既存の固定30ケースの識別子は維持する。

## 目的
- GUIと自動テストから同じ振る舞いケースを実行できるようにする。
- 静的ビルド・生成器契約・検出器契約を少数のパラメーター化した入口へまとめる。
- 新規テストプロジェクトを増やさず、Testプロジェクトと旧参照を廃止する。

## 非目的
実データの変更、履歴DBの自動移行、テスト本数を減らすための保証削除、無関係な製品改修。

## 提案設計
移行元・移行先・開始時ハッシュ・検証項目の対応表を保持する。Dynamicはソース一式、fixture、実行対象、結果・例外・評価回数・解放・展開条件を持つ共通ケースを実行する。静的ビルドは実MSBuildで代表ケースを検証する。GUIは共通Workerの判定を使い、ケース検索・再実行・診断表示を提供する。

DBモデルと共有fixtureはSeamless.TestKit、DB生成はDatabase.Compilation.Tests、問い合わせはDatabase.Query.Tests、トランザクションはDatabase.Transactions.Tests、実SQL連携はDatabase.Verification.Testsへ置く。Tsavoriteと共通ストレージはTest.Tsavorite.Collections、Persisted固有はTest.Persisted.Collections、実行制御はExecution.Testsへ移す。旧Testにあったベンチマーク共通ロジックとWindows画面の契約はBenchmark.Infrastructure.Testsへ移し、対象フレームワークで分ける。Dynamic画面とWorkerの検証は既存Benchmark.Dynamic.Testsを使う。

## 影響範囲
旧Test全体、既存移行先、Dynamic Core/Worker/UI、友元アセンブリ、ソースリンク、共有プロジェクト、ソリューション生成、CI、現行ドキュメント。

## 移行手順
1. 対応表と移行前検出結果を保存する。
2. 共有fixtureと専門テストを段階移行し、移動ごとにビルドと検証を行う。
3. Dynamic共通ケースとGUIを拡張し、振る舞い検証と例外契約を集約する。
4. 旧パス参照・友元・CIを付け替え、対応表の未移行をゼロにして旧プロジェクトを削除する。

## テスト計画
移行前後の保証対応、Debug/Release、CoreOnly/Windows構成、生成時・実行時の展開、1段/2段全組み合わせ、実Workerの結果保存、GUI検索・実行・再実行を確認する。未実行とskipを合格へ含めない。利用者の追加指示により作業・成果物・データ・キャッシュ・ログはすべてF。共通8CPU以内、共有BGOは使わない。

## リスクと対策
未コミット変更の上書きを防ぐため、ファイル移動は移行元ハッシュと移行先の不存在を確認する。fixtureの名前空間とデータの意味を維持する。静的ビルドの保証を動的コンパイルのみで代用しない。診断メタデータを実コード形状として判定しない。

## 未決事項
この移行の実SQL接続とGUI実画面の受入は完了した。接続設定のない環境ではSQL試験は明示的にSkipとなる。GUIの受入は専用Win32デスクトップ上のUI Automation操作とウィンドウ描画で行い、操作工程と画像を構成別に保存した。

## 実装した境界
- 旧595ソースを既存プロジェクトへ移し、旧Test固有のcsproj・global using・assembly設定の3ファイルを廃止した。新しいテストプロジェクトは追加していない。
- Dynamic Coreが233の振る舞いソースを資産として持ち、WorkerのRegressionSuiteが独自型・補助ソースごとコンパイルする。GUIの検索・再実行と自動検証は同じケースIDとランナーを使う。固定30ケースは別の既存IDのまま維持する。
- 生成器・ランタイムの残留契約は共通Theory、実MSBuildはpartial・ジェネリック・独自型の3ケース、検出器は正負例に集約した。テストランナーの補助LINQはassemblyでopt-outし、実MSBuildの対象だけを明示opt-inする。
- 生成物の形状は診断用属性・手順コメント・非実行テキストを除いて検査する。元の診断情報が必要な契約は生の生成物を読む。EmitCompilerGeneratedFilesの過去ファイルは、現在のアセンブリにある型か照合して除外する。
- 期待値を渡すdynamicは呼出元の静的型へ解決し、匿名型の期待値整形はプロパティ読み取りへ変えた。さらにxunit.assertの型別キャッシュを、回帰ソースと同じcollectible AssemblyLoadContextへ読み込む。解放確認を省略せず、全1206ケース実行後の解放を確認した。
- 入れ子のCounterScope、例外、Counter.Resetでも実行時フォールバックを失わない観測範囲を追加した。故意にカウンターを加算する検出器契約と、フォールバック禁止の振る舞いケースは区別する。
- 通常の並列書込みfixtureは必要ログ容量を開始前に確保し、容量不足を起こすfixtureは固定成長を明示した。Windowsのチェックポイント階層がパス長制限に当たらないよう、検証のデータルートを短いFのパスへ置く。
- 順序固定の短いStopwatch測定は負荷で逆転したため、5%閾値を機能合否から外しTimingObservationへ保持した。値の一致と割当量のassertionは保持し、速度判定には独立した測定経路を使う。
- GUIの回帰実行は既存BenchmarkRunsにrunと指紋を保存し、ケース別結果と展開コードをJSONL／ソースファイルに保存する。測定サンプルは作らない。既存DBのスキーマ変更・自動移行は追加していない。
- 実画面試験でタブ見出しにUIAの選択操作がないことと、Borderの段階識別子がUIAへ現れないことを確認した。タブ見出しはInvoke可能な標準Button、段階識別子は可視の見出しLabelへ配置した。
- 約40MBの展開コードをEditorへ一括設定するとUIA操作がタイムアウトした。表示を最大16,384文字のプレビューに制限し、全文ファイルの保存とSHA-256照合を維持した。履歴の配置先はSEAMLESS_BENCHMARK_DATA_ROOTで指定できる。
- 実SQL連携のCLI試験は接続DB名から生成先を確認し、子ビルドと追加DBのファイルをFのテストディレクトリへ配置する。

## 移行の照合
移行前2250ケース・2004メソッドについて、移行先のTRXと完全IDのJSONLを照合し、欠落・未検証はともに0。機械可読の結果は20261104-test-coverage.json、各ファイルの開始時・現在のハッシュと構成別receiptは20261104-test-migration.jsonに保存する。通常の操作説明はdocs/testing.md。

## 合意状態
ユーザーはTestプロジェクトの廃止、振る舞いケースのGUI再実行、保証を維持した入口集約を選択し、2026-11-04に本計画の実装を指示した。

## 検証結果
- 移行前2250ケースとの照合は欠落0件、2250件合格、未検証0件。
- Dynamic共通ソース1206ケースはDebug・Releaseで全件合格、実行時フォールバック0、回帰アセンブリの解放も成功。
- 最終CoreOnly検証はDebug・Release各541件合格。Windowsの通常検証はDebug763件合格。
- 中間演算子1個＋終端は152件、中間演算子2個＋終端の間引きなし検証は生成可能な4365件を全件実行し、不一致・解放失敗0件。
- 専門プロジェクトのDB生成・問い合わせ・トランザクション・ストレージ・実行制御・共通画面契約は移行先で検証済み。実SQL接続6件を含むDatabase.Verification.TestsはDebug・Release各39件合格、Skip0件。
- GUIはDebug・Releaseの両方で、起動、ライブタブ、検索、選択、初回実行、再実行、展開コード可視表示の7工程を合格した。既存Benchmark.Dynamic.UITestsから隔離デスクトップで実行し、構成別に6枚の描画画像を保存した。両構成とも2回分の合格履歴と結果JSONL、フォールバック0、生成コード全文のSHA-256一致を確認した。
- 最後の生成器Release再生成、CoreOnly Release、GUI ReleaseビルドとUI試験ビルドはFだけで実行した。GitHub Actions上の実行はこのローカル結果から推定しない。
- GUIと同じWorker CLIから1ケースを実行し、新規のF上のDBへ合格状態・結果JSONL・展開コードと指紋を保存できた。フォールバック0、生成コードのSHA-256一致、測定サンプル0件を確認。構成別receiptと実SQL・GUIの受入結果は20261104-test-verification.json。
