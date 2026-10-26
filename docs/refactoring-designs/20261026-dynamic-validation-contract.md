# Dynamic Worker と固定ケースの検証契約の共通化

## 背景

統括の明示依頼に従い、FixedCaseRunner と DynamicWorkerExecutionRunner の実行結果・例外の捕捉と比較を共通化する。既存 Run 署名、Task/取消、ALC 解放を維持する。

## 現状の観察

- WorkerHost は System / FLinqUnoptimized / FLinq / ZLinq の存在する実装を実行し、値を Validate、通常例外を型名・メッセージ・スタックの文字列として保持する。OperationCanceledException は捕捉しない。
- 例外があれば ExpectExceptions=false は ExecutionFailed。true は System と各実装の例外型 / 正常値を比較し、同型例外なら Succeeded として測定しない。
- 例外枝が最優先であり、正常値の意味不一致が次、runtime fallback はその後に ExpansionIncomplete とする。fallback の数は Run と値の Validate が正常完了した場合だけ記録する。
- FixedCaseRunner は値を直接実行して比較しており、全実装同型例外でも失敗する。測定が 0 件という理由だけで unknown case と誤判定する。

## 過去設計から引き継ぐ仕様

`docs/refactoring-designs/20261022-dynamic-verifier-consolidation.md` の例外型一致・測定skip・検証実行中だけの LinqCounterScope と runtime fallback 不合格を引き継ぐ。今回、既存 Worker の例外枝が fallback 判定に先行する順序も維持する。

## 目的

1. 両実行経路で同一の捕捉・比較・測定可能性判定を使う。
2. 固定ケースの全実装同型例外を意味一致として保存し、性能数値を作らない。
3. catalog 一致 0 件と、検証成功・測定skip を区別する。

## 非目的

generator / Core / CI / UI / DB schema、元 JSON・OriginalSHA256、測定アルゴリズムの変更。新しいテスト専用注入 API は追加しない。

## 提案設計

- production の `DynamicQueryValidator.Validate(LoadedDynamicQuery, primary, secondary, implementations, expectExceptions, cancellationToken)` を追加する。既存公開 LoadedDynamicQuery と同じ利用境界で、実行結果の意味検証を行う通常用途の API とする。
- 結果 record は Results / Exceptions / Comparisons / Fallbacks / Status と測定可否を返す。例外自体や動的 Type は保持せず文字列を返して ALC を保持しない。捕捉メソッドの NoInlining を維持する。
- WorkerHost はこの結果を既存 WorkerValidationArtifact と status/diagnostic に写す。例外・不一致・fallback の順序と JSON の既存 Results/Exceptions/Comparisons 形を維持する。
- FixedCaseRunner は System / FLinqUnoptimized / FLinq / Ideal を指定し、例外一致を許す。意味不一致 / fallback は失敗。同型例外は Run.SettingsJSON の Validation 配列へ元 ID・SHA・結果 / 例外 / 比較 / skip 理由を保存して continue。正常値だけ既存 MeasurementEngine へ渡す。
- 正常終了・失敗終了とも蓄積済み検証証跡を SettingsJSON へ保存する。既存取消の再throwと失敗run保存は維持する。
- `matched.Count==0` だけを unknown とする。全 case が同型例外なら Run=Passed、Measurement=0 を許す。

## 影響範囲

`Benchmark/Benchmark.Dynamic.Worker/{WorkerHost.cs,FixedCaseRunner.cs,DynamicQueryValidator.cs}` と限定回帰テストのみ。

## 移行手順

設計保存 → 既存 Worker の捕捉・比較を helper へ抽出 → Worker に接続 → Fixed に接続 → 回帰を追加 → source freeze → 統括による BGO 直列 build/test。

## テスト計画

- 動的コンパイルして公開 Validate を呼び、正常値一致・同型例外・異型例外・正常値/例外混在・例外許可false・取消・fallbackと意味不一致の優先順を確認。
- LinqCounterScope の通常公開 API で実行時 fallback を記録する最小 Source を使う。例外の前に fallback を記録して投げた場合も、既存 Worker 同様にその実装の fallback 数を保存しないことを確認。
- 各 loaded query の ALC 解放を検証する。
- FixedCaseRunner は専用一時 DB と既存 catalog の正常入力を使い、Passed / 4 measurements / settings証跡、unknown と取消の失敗・伝播を確認する。
- 既存 DynamicExceptionParityTests / FixedCaseCatalog を関連回帰とする。

## リスクと対策

比較順や JSON の変更は UI と status に影響するため、既存順序・artifact形を維持する。exception / delegate の保持による ALC leak は文字列化と NoInlining と解放検証で防ぐ。performance measurement は正常値の意味一致かつ fallback 0 の場合だけ作る。

## 未決事項

既存 25 件は空でも例外にならない終端、または Prepend 付きの First であり、FixtureValues は count<=65536 の中心化された連番に限定される。したがって、元 catalog / fixture を改変せず FixedCaseRunner.Run の例外skip分岐を直接起こす既知の通常入力はない。統括確認により、同型例外skipは共通 production Validate 経路の試験と Fixed Run の正常・unknown・settings保存の試験を組み合わせる。専用の catalog 注入点やテスト用公開 API は追加しない。Fixed の例外skip保存分岐そのものは未実行の検証限界として残す。

## 合意状態

統括が上記範囲の共通化と限定回帰を明示依頼済み。設計を先に保存して局所実装を開始する。build/test/app は本担当では実行しない。

## 統括側の検証

本担当の build / test / app 実行は 0。source freeze 後、統括の BGO 資源宣言の下で次を直列実行する。

```powershell
dotnet build Seamless.FLinq.Generator/Seamless.FLinq.Generator.csproj -c Release --no-restore -t:Rebuild -m:1 -p:UseSharedCompilation=false --disable-build-servers
dotnet test Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Benchmark.Dynamic.Tests.DynamicQueryValidator|FullyQualifiedName~Benchmark.Dynamic.Tests.FixedCaseCatalog' --logger 'trx;LogFileName=validation-contract.trx' --results-directory '<fresh-artifact-directory>'
dotnet test Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~Benchmark.Dynamic.Tests.DynamicExceptionParityTests|FullyQualifiedName~Benchmark.Dynamic.Tests.WorkerVerticalSliceTests' --logger 'trx;LogFileName=worker-validation-regression.trx' --results-directory '<fresh-artifact-directory>'
```

新規限定回帰は 10 Theory + 取消 Fact + Fixed Run Fact の 12 件。既存 FixedCaseCatalog 3 件を含む最初の filter は計 15 件を期待する。既存 DynamicExceptionParityTests は各 theory が多数 query を実行するため、統括の資源状況を確認して別枠で実行する。成功は性能改善の証拠とは扱わない。
