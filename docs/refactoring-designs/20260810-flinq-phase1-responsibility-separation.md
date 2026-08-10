# FLinq SourceGenerator 第1段階: 既存Fragmentを活かす責務分離

## 目的

Seamless.FLinq.Generatorの巨大partialに残る責務を、既存Fragment、PipelineBuilder、演算子Variantの意味論を変えずに分離する。公開API、Seamless.Linq、生成時の遅延評価、評価順、例外、Dispose、Provider順序は変更しない。

## 境界

| 境界 | 責務 | 状態所有 |
|---|---|---|
| LinqConfigurationResolver | 属性と継承設定の解決 | 対象モデルに固定したLinqGenerationConfiguration |
| LinqTargetDiscoveryService | interception対象の発見 | SemanticModelと包含型 |
| LinqExpressionAnalyzer | lowering、外側呼出し、lambda境界の解析 | 対象単位の解析結果 |
| LinqOptimizationExecutor | Pipeline stageの正規化とtrace | 対象単位の設定・trace |
| LinqFragmentBuilder | Registry Bind、Root、ProviderNative、lambda、既存Fragmentの構築 | GenerationState/FragmentBuildContext |
| GeneratedSourceRenderer | 既存生成文字列の描画 | GeneratedSourceRenderModel |
| Output Coordinator | 入力ファイル単位のグループ化、診断、Hint名、AddSource | ファイル単位 |

## 残すもの

Fragmentのclosure、enumerator、文字列組立とPipelineBuilderのアルゴリズムは第1段階で保持する。SequenceEqualは比較子なしVariantを専用化し、不要なcomparer field/argumentだけを生成しない。

## 除去した共有状態

Fragmentのフィールド反射走査、汎用Constructor Binder、ThreadStaticの最適化Context・匿名構造体Registry・pin/suspendは通常生成経路から除外した。Fragmentの子は明示Visitor、描画情報はFragmentRenderContextで伝播する。

## 検証

Registryの重複、Priority、Factory型、Coverage、Capability、ProviderNative整合性をGenerator実行時と単体テストで検証する。SourceGeneratorテスト、入力ファイル単位のIncremental cache、NoRewriteを含むReleaseビルド、パッケージ内容を継続検証する。

## 第2段階

closure、enumerator、生成文字列の密結合を、Fragmentの意味論を維持した中間表現または描画契約へ移す。第1段階ではこの再設計を行わない。