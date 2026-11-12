# FLinq Source Generator

[`Seamless.FLinq.Generator`](../Seamless.FLinq.Generator/) は `System.Linq.Enumerable` 呼び出しを解析し、入力ファイル単位の interceptor ソースを出力する Roslyn incremental generator です。生成コードは [`Seamless.Linq`](../Seamless.Linq/) の実行時補助を使います。

## 評価の契約

- 結果・順序・例外を `System.Linq` に合わせ、`System.Linq` が評価しない要素に利用者のラムダを適用しません。
- `.Anchor` を含むラムダの評価回数を維持します。
- 直接渡したラムダをインライン展開の対象にします。キャスト、変数、フィールド、メソッドグループを経由する場合とdelegate構文では、呼び出し時に選ばれたデリゲートを保持します。変数の宣言時の初期化式へ戻しません。
- デリゲートを保持する理由は、採用した実行計画から生成コードのコメントへ出力します。外側の可変ローカルを変更する直接ラムダは、元のクロージャセルを共有する既存経路を使います。
- 並べ替え、`Reverse`、`Distinct` を省略できるかは、下流が必要とする順序・重複と、途中停止までに評価するラムダを確認して判断します。

下流が必要とする範囲の解析と `GroupBy` 融合の設計、検証結果は [FLinq の評価範囲の設計](refactoring-designs/20261024-2208-flinq-demand-analysis.md) を参照してください。

## Registry中心設計

演算子の選択・適用・能力判定は `OperatorRegistry` に集約しています。登録内容、生成サービス、Render 契約の詳細は [FLinq Registry 内部設計](../Document/Code/Internal/FLinqRegistryInternalDesign.md) を参照してください。

## Coverageと診断

公開 `System.Linq.Enumerable` API 名は Registry の Coverage 一覧と照合します。実装対象外は `OperatorCoverageKind.Excluded` で明示します。重複 Key、Priority 競合、Factory 型、Coverage、ProviderNative プロファイルの整合性は Generator 初期化時に検証し、不整合時は `FLINQ006` を報告します。

## 配布互換性

Generator アセンブリは `netstandard2.0`、Roslyn 依存は `Microsoft.CodeAnalysis.CSharp` と `Microsoft.CodeAnalysis.Analyzers` の 5.6.0 です。生成コードのランタイムは `Seamless.Linq` の `net10.0` を前提とします。FLinq NuGet は同じバージョンの `Seamless.Linq` パッケージに依存し、`Seamless.Linq.dll` を同梱しません。

## 検証

- [LinqFragmentCoverageTests](../Benchmark.Dynamic.Tests/Migration/SourceGenerator/LinqFragmentCoverageTests.cs) で公開 API の Coverage、Key、Priority、Factory 型、Capability を確認します。
- [IncrementalCacheTests](../Benchmark.Dynamic.Tests/Migration/SourceGenerator/IncrementalCacheTests.cs) で入力ファイル単位のキャッシュ分離を確認します。
- [Benchmark.System.Linq.Enumerable.FLinq](../Benchmark/Benchmark.System.Linq.Enumerable.FLinq/) で Release の Benchmark-as-test を実行します。Dynamic の再現手順は [チェーン検証の設計と記録](refactoring-designs/20261022-dynamic-verifier-consolidation.md) を参照してください。
- `dotnet pack Seamless.FLinq.Generator/Seamless.FLinq.Generator.csproj -c Release` で作成したパッケージの analyzer TFM、依存、ランタイム DLL の重複を確認します。
