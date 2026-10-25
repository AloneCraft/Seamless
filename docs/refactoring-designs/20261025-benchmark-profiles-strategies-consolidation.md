# プロファイル・測定履歴・ストラテジー統一とプロジェクト整理

## 背景

2026-10-25、利用者が本設計の実装・検証・コミットを承認した。Dynamicをクエリ検証の中心とし、独立したScalableを大規模測定に使う。両者の測定から、配布可能な最適化済みプロファイルを作る。

## 現状の観察

- 起点は master / 7f9669cd。開始時の既存変更は `G:\GitHub\Seamless\Work\Consolidation\baseline.patch` と baseline-status.txt に保存した。生成器、Dynamicの評価回数検査、文書などの未コミット変更は本作業へ混ぜない。
- DynamicのDBは版3で、通常実行は保存するがライブ検証は保存しない。Scalable.UIは旧画面を共有し、Headlessも旧Workerを起動する。
- 現行Tsavoriteには最小64バイトの索引・4096バイトのページがあるが、新ストアの既定では予想件数と予算から大きさを選ぶ。
- 索引拡張には並列区間の制約があり、ページなどの変更にはストア再構成を要する。

## 過去設計から引き継ぐ仕様

- 20261022-dynamic-verifier-consolidation: 理想コード・回帰例を移してから旧検証器を消す。推測した理想時間は作らない。
- 20261024-2208-flinq-demand-analysis: 進行中のFLinq改修が参照するテストは改修の区切りまで維持する。
- DBはバックアップ付きの明示的操作で移行し、起動時に旧DBを自動破棄しない。
- 本設計は20260928のVerification中心の判定と旧測定画面維持の方針を更新する。比較を共通測定基盤へ移し、Verificationは役割移行後に削除する。

## 目的

1. MinimumとOptimized、初期設定と拡張アルゴリズム、資源上限を分離する。
2. 役割の名前空間と静的ストラテジーを、差し替え可能なアルゴリズムの共通規則とする。
3. DynamicとScalableの結果・比較・設定変更を共有DBに記録する。
4. 測定・別入力での確認を経た設定を、条件別のOptimizedとして出力する。
5. 理想コードを保持して重複するプロジェクトを整理する。

## 非目的

- 旧公開API名・旧コレクション保存形式の互換維持。既存原本の削除はしない。
- 利用アプリから開発用SQLite DBへの依存。
- 未測定の値を最適化済みと表示すること。
- 使用中の旧Worker・Headless・Audit対象・個別最適化比較の一括削除。

## 提案設計

### ライブラリ

- `Seamless.Execution.Profile`: Minimum / Optimized。既定はMinimum。明示値 > 読込済み測定プロファイル > 同梱値 > Minimum。
- `Seamless.Execution.Growth.IStrategy<TSelf>`: structとstatic abstract。Fixed / Doubling / Estimatedを同梱し、利用者定義も同じ契約で扱う。状態はストアごとに独立する。
- ストラテジーはContextからDecisionを返す。エンジンが資源予約・サイズ条件・並列条件を確認して適用する。Fixedは索引・常駐枠を固定し、ファイルの通常の成長や件数自体は固定しない。
- Optimizedが条件や上限に適合しなければMinimumへ戻し、採用設定と理由を公開する。不正な明示値はエラー。
- シリアライザーは `Seamless.Collections.Serializers` に Auto<T> / Blittable<T> / MemoryPack<T> / RawByteArray、方式タグはBlittable / MemoryPackへ統一する。
- 比較・走査は実装とBCL境界を棚卸しし、役割別名前空間と型確定した実行部へ移す。DB接続や資源所有などのサービスは責務に合う構造を維持する。

### 共通測定基盤

- 非配布 `Benchmark.Infrastructure` に記録・比較・プロファイル選定・資源監視を置く。
- 共有DBは `G:\GitHub\Seamless\Work\Benchmark\Benchmark.db`。実行・ケース・区間・生測定・設定変更・固定基準・比較・出力プロファイルを記録する。
- 条件には入力、型、件数、シード、クエリ、保存方式、最適化、指定/実設定、拡張方式の版/設定、コード/実行環境を含める。
- 固定基準/任意2回/設定変更実験を区別する。有効測定は既定7回、中央値とばらつきを表示、悪化の目安は5%。未完了・条件不一致・正しさ不一致は性能と区別する。
- 既存Dynamic履歴を明示移行し、復元不能な条件はUnknownとして保持する。旧履歴を自動学習に使わない。

### アプリ

- Dynamicの通常・ライブを同じ保存経路へ接続し、履歴比較とプロファイル出力を設ける。手書き理想がある固定ケースを実測する。
- Scalable.UIを独立した画面へ更新する。Scalable.Coreと独立Headlessが操作/LINQ/容量探索を実行する。入力は分割生成する。
- 自動調整は測定アプリ内で行う。1要因ずつ候補を変更し、操作区間の境界で再構成する。旧データを残し、新構成の件数と内容が一致したときだけ切り替える。
- 評価には停止・移行時間を含める。固定設定との対照と別入力による確認を行い、採用は明示操作とする。
- 同梱/書出しプロファイルには適用範囲・根拠の実行ID・設定値・ストラテジー版を含める。

## 影響範囲・API移行対応表

| 旧形 | 新形 |
|---|---|
| AutoSerializer<T> | Serializers.Auto<T> |
| BlittableSerializer<T> | Serializers.Blittable<T> |
| MemoryPackSerializer<T> | Serializers.MemoryPack<T> |
| RawByteArraySerializer | Serializers.RawByteArray |
| BlittableStrategy / MemoryPackSerializerStrategy | Serializers.Blittable / Serializers.MemoryPack |
| AutoGrowIndexだけの判断 | Growthの型付きストラテジーとエンジン側適用 |
| 予想件数から既定初期値を拡大 | Minimumで最小開始、拡張は別判断 |
| VerificationComparison | 共通測定の比較サービス |
| UIごとの履歴 | 共有Benchmark DB |

## 移行手順

- [ ] 0: 開始状態・基準検証・設計/API表の保存。
- [ ] 1: ライブラリAPI・命名・Growth・プロファイルと対象テスト。
- [ ] 2: 共通DBと移行・比較・Dynamicの記録/履歴/理想コード。
- [ ] 3: Scalable独立化・画面・自動調整・Optimized候補の実測。
- [ ] 4: 固有テストと理想/回帰の移植後、Verification/旧UI/ParameterSweep.UI/旧検証器/Runnerを整理。
- [ ] 5: ソリューション・CI・文書・起動設定整合、全体検証、明示パスでコミット。
- [ ] 6: 完了後に既存チャット「1022Argon ベンチマークプロジェクト統合」へ確定コミット/API表/DB移行/テスト結果を渡し、影響部分だけ再実行依頼。

## テスト計画

Minimum/Fixed/拡張/独自戦略/状態分離/予算/保存再開、プロファイル優先順位と理由、DB並行書込と移行、比較判定、ライブ保存、理想コード同等性、再構成失敗とデータ保持を自動テストする。本体・Collections・Execution・Dynamic・Audit・TPC-E・サンプルを回帰確認し、Dynamic/ScalableはDebug/Releaseビルドと実画面確認を分けて記録する。

## リスクと対策

- 重い処理は直列。物理メモリ85%で新規負荷/拡張を抑止、90%に近づく前に自分の測定を中止。中止は合格にしない。
- 初期の未コミット変更を上書き・一括ステージしない。競合する既存変更は切り分け、段階ごとの検証と明示パスのコミットを行う。
- DBバックアップはSQLiteの整合したスナップショットとし、移行元を残す。通常起動から移行しない。
- 理想/回帰の未移植例が残る間は、元の実装を削除しない。
- pushしない。後続チャットを実装完了前に再開させない。

## 未決事項

製品方針の未決事項なし。実装で判明するバックエンドの変更可否は、未対応理由を記録し安全な区切りでの再構成に集約する。検証済みでない条件を最適化済みとして採用しない。

## 合意状態

2026-10-25 利用者承認済み。実装中。変更・テスト・DB操作・コミットの実績は下記へ追記する。

## 実装と検証の記録

- 開始時の差分を保存。CIMアクセス不可のためメモリ監視にはWindows APIを使用する。
