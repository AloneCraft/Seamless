# Ordered Firstと返却ordered queryの意味修正候補

## 背景
59の160比較にはCSV誤期待13、NoneのC6/C7/C9意味差6、C5の可視key未計測12がある。旧60のbefore/after SHAは再照合した。

## 現状の観察
C5は返却AsEnumerableの内側OrderByを不透明なRootにし、BCL key lambdaを無計測で残す。OrderingBaseをそのまま返すとconstructorのBuildが評価を早める。既存PipelineEnumerableは初回TryGetNextにsortとkey inline評価を遅延し、ValueEnumerableは未開始のstructを列挙ごとにコピーする。

## 過去設計から引き継ぐ仕様
元inputと元CSV bytesを保持し、effectiveCSVはSystem保存観測だけから4行訂正する。v2の実入口定義を保持する。値、例外、trace、遅延、反復、Disposeをcountとは別gateで確認する。

## 目的
述語付きFirstと二重OrderByの単一key需要を修正し、C5返却queryの可視lambdaを実際に生成し計測する。

## 非目的
全AsEnumerable、任意IEnumerable source、ThenBy全深度、代数最適化、公開runtime API、性能受入の一般化は行わない。

## 提案設計
旧60の3製品候補と1fixture候補を保持する。C5は返却式直下、System OrderBy/OrderByDescending、array入力に限定してopaque Root化より先に通常source fragmentを構築する。Allの既存PipelineEnumerableを保持し、Noneの単一OrderingBaseは同じsource/key/comparer/contextの一段PipelineEnumerableへ写す。融合や最適化の設定は変えない。既存ValueEnumerable返却/boxing経路を使い、sortを初回MoveNextまで遅延する。

## 影響範囲
First.cs、FirstOrDefault.cs、OrderBy.cs、FLinqIncrementalGenerator.FragmentBuilder.cs、OrderedFirstDemand/OrderingBase.cs。Rootの共有sourceは変更しない。

## 移行手順
専用worktreeでRootの現在bytesとSHAをsnapshotし候補作成。親がhash一致を確認して最小差分を取り込み、BGOでfresh build/testする。子はbuild/testしない。

## テスト計画
既存160比較を維持する。追加1fixtureは元inputを変更せず別構文木へLifecycleProbeを追加し、System/None/Allで返却前後、GetEnumerator、初回MoveNext、入力arrayの変更、反復列挙、throw時入口を比較する。physical/fallback=0を維持する。生成C#にC5のBCL OrderBy lambdaが残らないことを確認する。

## リスクと対策
Noneで使うPipelineは単一sortの遅延実装に限る。opaqueな他sourceや非返却consumerは既存経路を保持する。支持メンバー、capture、counter contextを通常fragment builderで受け渡す。性能・allocationは未受入と明示する。

## 未決事項
親のfresh compile/runtimeによる受入は未実行。範囲外のescaped ordered queryへ拡張しない。

## 合意状態
親からsource-only候補作成を明示依頼済。Root取込み/build/testと最終受入は親が実施する。


## 統合時点の検証状態

61候補を親が取り込む段階。compile/runtimeは未受入。既存160比較の期待を緩和せず、元入力・元CSVとSystem観測からの実効CSVを分離する。CounterEntry APIのnullケースは、CSVに省略された標準messageを独立BCL例外から補い、旧/v2の完全な例外・trace比較を維持する。親の65 cohortでfresh Generator再生成と実ロードDLLを照合し、意味・遅延・入口回数を受け入れる。
