# 残存5テストプロジェクトの統合

## 背景

`20261104-post-migration-inventory.md`で確認した5候補について、利用者が保証を維持した統合、重複6ケースの整理、フォールバック確認不足の修正と自動テストを指示した。

## 現状の観察

Headless・Audit・ParameterSweepは共通計測基盤のテストへ、Scalableの2ストアは既存Scalableテストへ集約できる。ScalableはBENCH_AS_TESTと継承されたBenchTheoryを持つ。旧コーパスと固定境界値の直接実行には実行時FallbackCountのassertionがない。要素型の固定6ケースは同じseed・query indexの広域試験にも含まれる。

## 過去設計から引き継ぐ仕様

`20261104-test-project-retirement.md`のケース移行照合、生成時・実行時の両検出、元ソースの保証維持、Fへの成果物配置を引き継ぐ。通常CIの短い固定回帰と夜間網羅の役割を維持する。

## 目的

- Headless・Audit・ParameterSweepをBenchmark.Infrastructure.Testsへ物理移動し、Windows依存をframework条件で区切る。
- Scalable Persisted・TsavoriteをBenchmark.Scalable.Testsへ物理移動し、共有ソース由来の全検出ケースを引き継ぐ。
- 旧5csprojとsolution・実行手順の登録を整理し、新規テストプロジェクトを0件にする。
- 型別固定6ケースの不具合識別情報を残し、同一クエリの二重実行を除く。
- 境界値・旧コーパスの結果／例外比較を実行時フォールバック禁止と同時に検証する。

## 非目的

製品API・保存形式・比較実装・通常CIと夜間検証の役割を変更する要求は含まれない。

## 提案設計

移行元のテスト型名とnamespaceを維持し、完全ケース名を移行前後で照合する。HeadlessはInfrastructureのWindows targetだけへ含める。Auditの監査対象DLLはビルドで得た出力一覧を使い、Fの隔離成果物でも同じ構成を監査できるようにする。Audit用の同名型を持つアセンブリはビルド依存として扱う。

Scalableは両方の共有providerソースをBENCH_AS_TESTでコンパイルする。共有ソースの重複取り込みを避け、既存の独自試験と継承ケースを同じassemblyで発見する。

重複6ケースは広域側のケース選択へ統合し、既知修正キーを検証する軽量assertionを残す。直接実行はDynamicFallbackCaptureで囲み、正常・例外のどちらもFallbackCount 0を確認してから結果を比較する。検出器の故意の正例は独立して維持する。

## 影響範囲

移行元5プロジェクト、Infrastructure.Tests、Scalable.Tests、Dynamic.Tests、Headlessのfriend設定、共有importの重複防止、Seamless.slnx、Update-FocusedSolutions.ps1、関連docsとCI。

## 移行手順

1. F上に移行元5件のDebug検出ケースと対象ソースのhashを保存する。
2. 検出漏れが分かる対応表と、フォールバック観測の正負試験を用意する。
3. ソース・依存・friend設定を移し、重複6ケースと直接実行の観測を整理する。
4. 移行後Debugの検出ケースを比較し、全件の所有先を確認する。
5. Debug／Releaseで関連テストを実行する。失敗は原因を区別して修正し、対象を再実行する。
6. 旧5csprojを削除し、focused solutionsと現在の操作方法を更新する。

## テスト計画

| 対象 | 構成と実行範囲 |
| --- | --- |
| Infrastructure.Tests net10.0 | Debug／Releaseの全件。ParameterSweep・Auditを含む |
| Infrastructure.Tests net10.0-windows | Debug／Releaseの全件。Headlessと既存Windows契約を含む |
| Scalable.Tests net10.0 | Debug／Releaseの全件。旧2assemblyからの全ケース照合 |
| Dynamic.Tests CoreOnly | Debug／Releaseの全件。元1206振る舞い・376契約を含む |
| Dynamic.Tests Windows | Debug／Releaseで変更した型別・固定境界・旧コーパス・検出器の試験 |
| Solution／参照 | 旧プロジェクト参照0、実体とmasterの対応、shared GUID・import、focused再生成、diff check |

## リスクと対策

共有ソースの継承ケースの欠落は完全ケース名で検知する。複数frameworkの条件差を明示して、Windows試験とnet10.0試験をそれぞれ実行する。監査はビルド出力を照合して古いDLLの利用を防ぐ。既存の未コミット変更と所有者の異なるプロセスを保護する。重い処理は共通8論理CPUと既存artifact lockを使用し、作業・cache・data・logはFへ配置する。

## 実行で確認した前提差と修正

- 移行前の検出はHeadless 2、Audit 8、ParameterSweep 22、Scalable Persisted 32、Scalable Tsavorite 99の計163ケース。Scalable移行先の157ケースには旧2assemblyの131ケースが全件含まれる。
- AuditのSystem.Globalizationは統合先でBenchmark.Systemと競合したため、global修飾で元の参照先を固定した。Headlessは専用アセンブリエイリアスを使い、テスト対象exeのUI型を参照する。
- 最初のScalable全件実行は157件中52件失敗した。Tsavoriteの旧予算式は索引・ログ・2ページだけを含み、現在の予約会計にあるoverflow pool・管理配列・alignmentを含めていなかった。TestKitから実ストアの会計処理を再利用し、設定したログ全体と想定件数のoverflowを覆う有限予算を算出する。製品の会計・上限制約と設定したログ容量を維持する。
- 再構成失敗試験の30000バイトという初期予算も現在のストア作成条件を満たさなかった。最小ログの実会計から初期予算を求め、初期作成成功・再構成失敗・元データ保持を引き続き確認する。
- Infrastructureの最初の実行は359件中12件がParameterSweepで失敗した。ParameterSweepにも同じ旧予算式が残り、ディスク上限・取消・並行ワーカーを検証する前にメモリ不足となっていた。最大試行件数とログ設定から同じ会計helperで予算を求め、元の成功・ディスク1バイト・取消・後処理のassertionを維持する。
- フォールバックの直接実行試験は、assertion追加前に4件中2件が失敗した。正常終了と例外終了の両方で、故意のフォールバックを不合格として検出する。

## 未決事項

実装上の依存・型名衝突はビルドとケース照合で解消する。利用者による追加の仕様選択を要する事項は現在ない。

## 合意状態

2026-11-04の利用者の実装指示に基づき実施する。

## 実装結果

旧5csprojを廃止し、9ソースを既存2プロジェクトへ物理移動した。新しいテストプロジェクトは0件。通常プロジェクト121→116、共有17、全体138→133、テストプロジェクト24→19となった。直前のTest廃止1件を含め、この2段階で6プロジェクトを削減した。

Headlessの2ケースはInfrastructureのWindows target、Audit 8ケースとParameterSweep 22ケースはnet10.0 targetが所有する。Scalableの旧32＋99ケースは既存26ケースと合わせて157ケースとなり、全件をBENCH_AS_TESTで発見・実行する。共通依存のimportは1回にそろえた。

固定6ケースのキーをDynamicElementTypeTestsへ移し、広域側の実行で必ず合格と解放を確認する。境界値・旧コーパスは共通のDirectQueryExecutionから実行し、正常・例外の両方でFallbackCount 0をassertしてから比較する。故意にfallbackを加算する正例も維持する。

## 最終検証

| 対象 | Debug | Release |
| --- | --- | --- |
| Infrastructure net10.0 全件 | 359合格 | 359合格 |
| Infrastructure Windows 全件 | 410合格 | 405合格 |
| Scalable 全件 | 157合格 | 157合格 |
| Dynamic CoreOnly 全件と追加回帰の照合 | 全件561＋追加1、testId集合562 | 全件556＋追加クラス25、testId集合562 |
| Dynamic Windowsの変更対象 | 51合格 | 51合格 |

全チェックの失敗・Skipは0。移行元163ケースはDebug／Releaseとも完全ケース名で照合し、欠落0・未検証0。CoreOnlyの全件実行中に同時進行で追加された回帰は差分実行し、TRXのtestIdを使って両構成562件の合格集合を照合した。長いTheory引数で表示名が省略されるため、表示名の集合だけでは同一性を判定しない。Windowsの差5件は既存のDEBUG条件による。

元ソースの振る舞い1206ケースは両構成とも全件合格・fallback 0・解放成功。生成器契約376ケースも両構成で全件合格。契約の非ゼロfallback 1ケースは検出器の正例として故意に加算するもの。

機械可読の検証記録は`20261104-test-consolidation-verification.json`。Fの`con-verification.json`にも同じ記録を保存した。移行前の一覧、失敗先行のTRX、構成別ログ、9ソースと廃止5csprojの対応・SHA-256、原本の保全先は`F:/CodexTemp/test-retirement-20261104`にある。

旧5プロジェクトの実行参照0、masterと実体の差分0、共有GUID・import不整合0、focused solution再生成差分0、diff check成功を確認した。
