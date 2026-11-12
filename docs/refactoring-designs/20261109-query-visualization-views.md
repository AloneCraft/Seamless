# クエリ可視化の三つの表示

## 目的と合意

利用者の添付要求に基づき、データサンキー、実行タイムライン、実行DAGを別の視点として提供する。同じ対象・版・成果物・計画・実行識別を使い、選択・検索・詳細を連動する。既存のノードツリー、DOT、三段階構造、保存・比較・最適化追跡を引き継ぐ。

既存の[可視化計画](20261109-query-visualization-plan.md)へ追加する。手動Managerへの操作改善は固定R4として先に検証し、この追加機能の合格とは分ける。

## 現在の観測可能範囲

| 所有元 | 取得できるもの | 未取得・区分すべきもの |
| --- | --- | --- |
| ExecutionPlanDocument | 元演算、採用Fragment、式、接続種別、ソース、最適化判断 | 演算別の実行区間・行数・転送量 |
| GeneratedCode | 実生成本文、型解決済み基本ブロックと循環CFG | 並列スケジューラーの実行DAG、補助本文全体 |
| ObjectExecutionMeasurement | 単調時計の全体Elapsed、DBゲート待機、初回出力、観測件数・返却件数・部分取得 | 受付slot待ち、dispatcher待ち、演算別CPU・I/O・Worker |
| QueryTraceCapture | Started、GateAcquired、IdentityBound、FirstOutput、Sample、Completed | 演算・Worker・転送のイベント |
| LinqCounterScope | 演算種類別のスコープ集計 | 個々のNodeId・InvocationIdへの対応 |
| PartitionedLookupBuilder | 実producer、Channel、Task.Runのworker、CPU枠、buildの合流 | クエリと結び付くWorker別の区間記録 |
| SQL QueryBinder | 列、Rows delegate、実際のMergeJoin・Sort等 | SQLの公開論理／物理ノード、推定行数・cost |

経過時間はTrace予約後、DBゲート待機直前から測る。初回出力は値の取得後・JSON化前で、全体時間には列挙・JSON化・read sessionの解放を含む。送信完了は含まない。受付のslot待ちと組み込みdispatcherの待ちはこの時計の開始前である。

観測件数は上限確認の先読みを含む場合がある。返却件数と部分取得を分け、内部ToArray等の実体化件数や各エッジへ割り振らない。RecorderのBackpressureTicksとホストCPU利用状況もクエリ別の値として配分しない。

## 共通モデル

既存の計画、全体実測、Traceの公開契約を維持し、可視化用の投影と追加の詳細観測を分離する。

- 対象はDefinitionId、ObjectId、Version、ArtifactHashで固定する。
- 計画識別はMethodKey、SourceHash、GeneratedBodyHash、CallSiteから生成する。NodeId単独で別計画のノードを一致させない。
- データのポートとリンクは、依存辺・ソース対応・物理転送とは別に保持する。入力役割と共有識別を持たせる。
- 数量には値、単位、Observed／Estimated／Derived／Unknown、集計範囲、完全性、欠測理由を持たせる。ゼロと未知を区別する。
- 交換時の大きな数量は正規化文字列として保持し、幅を描く段階だけ有界な浮動小数点へ変換する。元の正確な値をラベルに使う。
- 詳細実行にはExecutionId、InstanceId、PlanNodeRef、Worker、Thread、ClockDomainと実区間を設ける。再列挙・再呼出しは別Instanceとする。
- 時計の共通原点を記録していない複数実行は、一つの単調時計のworkerレーンへ並べない。既存UTC境界表示を区別して維持する。

## データサンキー

最初はSourceOperatorと明示されたOriginalDataFlowから論理接続を投影する。SelectedReference、SourceMapping、ExpressionDependencyを流量に変換しない。実転送の観測まではLogicalReferenceと表示する。

入力帯と出力帯を独立に描くカスタムSVGを使う。Filter・GroupByの減少、JOINの増幅・減少、Concatの連結、共有参照に保存則を置かない。未知量は中立の破線、既知ゼロはゼロ量として表示する。既定は行数の実測、単位・推定／実測／比較・線形／対数と表示補正を明示する。現記録だけの実行ではノード量をUnknownとし、クエリ全体件数は独立した要約へ置く。

JOINは左右入力と出力、出力／左、出力／右を分ける。INNER JOINの選択率は出力／左右積として算出し、入力ゼロは比率不明とする。CROSS JOINの左右積は算出期待値であり実測と分ける。出力／入力合計をJOIN共通の増幅率にしない。役割が未記録の入力にBuild／Probeを補わない。

## 実行タイムライン

横軸を実経過時間としたガント形式にする。初回接続はクエリ全体バー、包含するDBゲート待機、初回出力と実境界・サンプルのマーカーとする。ゼロ時間のイベントはマーカー、未取得の区間は未取得と表示する。包含区間を単純合計せず、CPU時間や独立タスクへ読み替えない。

演算・worker・queue・I/O・資源のレーンは、対応する実観測を追加してから描く。ストリーミング、build/probe、barrierは実際の開始終了と依存条件を用いる。実測比較がない高速化倍率を算出しない。

## 実行DAGとドリルダウン

既存の上下依存配置と単一の表示用Resultを再利用する。論理演算、実際に採用された処理、元の式、生成C# CFGを区別する。循環CFGを実行DAGへ混在させない。ストリーミング・制御・ブロッキング等は明示された種類だけ表示する。実行イベントと区間が揃うまで実測クリティカルパスを計算しない。

選択・検索・表示モードを親の共通状態へ引き上げる。タイムラインに対応区間がないノードでは、その旨を表示し、クエリ全体バーを演算時間として強調しない。

## 保存と再生

既存形式1の読取とコードマスクを維持する。新しい数量・区間・共通表示設定を保存するときは版を明示し、旧資料の量を未知、区間を空として扱う。2MiB等の既存上限と件数・範囲・識別・hashの検査を引き継ぐ。異なる計画のノード対応が確定しなければ未確定と表示する。

アニメーションは既定で停止する。実イベント再生は記録時刻・欠落・再生倍率を持つ。模式表示は実速度・実測の証拠として扱わず、モードと凡例を区別する。動きを減らす設定を尊重する。

## 実装と確認の順序

1. 共通識別、ポート、未知量、区間、出所の契約と境界試験。
2. 元の論理接続と数量非保存を扱うサンキー、共通選択・検索。
3. 既存の単一実行実測を使う横方向ガントと境界マーカー。
4. SQL binderが実際に選んだ構造の保存と、opt-inの入力・出力・列挙区間計測。
5. 並列lookupのproducer／worker／batch／barrierの実観測と実行依存。
6. 旧資料互換、保存・比較・出力と通常Managerの連動。
7. 代表SQL、複数JOIN、GroupBy、Cartesian product、部分列挙・共有・並列・欠落・高負荷を確認する。
8. 計測オーバーヘッド、予算、Windows／Linux、通常起動の受入。

Phase1の調査結果は上記の実コードに基づく。追加三表示の実装・実行検証はこれから進める。独立fixtureの成功を実SQLや実worker計測の成功へ広げない。
