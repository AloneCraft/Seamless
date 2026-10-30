# FLinq

生成器の正本は `G:\GitHub\Seamless\Seamless.FLinq.Generator`、生成コードの実行時補助は `G:\GitHub\Seamless\Seamless.Linq` にある。

## 評価の契約

- System.Linqが評価しない要素に利用者のラムダを評価しない。結果・例外をSystem.Linqに合わせる。
- `.Anchor`を含むラムダの評価回数を変えない。
- 並べ替え・Reverse・Distinctの除去は、結果の順序・重複への依存と、途中停止までのラムダ評価を確認して判断する。
- 下流が使う範囲の解析とGroupBy融合の設計は `G:\GitHub\Seamless\docs\refactoring-designs\20261024-2208-flinq-demand-analysis.md` に記載する。実装の検証結果は同文書で管理し、未検証の計画を完了扱いにしない。
- Dynamicの検証と再現手順は `G:\GitHub\Seamless\docs\refactoring-designs\20261022-dynamic-verifier-consolidation.md` を参照する。

旧プロジェクト配置の調査記録と途中の実装計画は `G:\GitHub\Seamless\docs\history\20261025-legacy-code-design\FLinq.md` に分離した。現在の仕様と解釈しない。

## 現行実装: Registryと第1段階責務分離

現在の対象は Seamless.FLinq.Generator です。Generatorはnetstandard2.0、Roslyn依存は5.6.0、生成コードのランタイムはSeamless.Linq net10.0です。公開名前空間と生成属性の契約は維持します。

OperatorRegistryがOperatorKey、Variant、Coverage、Priority、Pipeline capability、ProviderNative、Root、型付きBind/Factoryの正本です。LinqTargetDiscoveryService、LinqExpressionAnalyzer、LinqOptimizationExecutor、LinqFragmentBuilderは既存FragmentとPipelineBuilderを活用しながらRegistryを参照します。GeneratedSourceRenderModelとGeneratedSourceRendererは、入力ファイル単位のtargets・診断・traceを出力Coordinatorへ引き渡します。

Fragmentの子走査は明示Visitorで行い、FragmentRenderContextが含有型、外側型パラメータ、Counter状態を対象単位で保持します。旧来のFragmentフィールド反射走査、ambientなThreadStatic状態、汎用Constructor Binderは生成経路から除外しています。

この段階ではclosure、enumerator、生成文字列のアルゴリズムと出力形状を維持します。これらの密結合を中間表現または描画契約へ分離する作業は第2段階です。