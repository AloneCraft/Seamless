---
layout: default
title: 検証方法
---

# 測定履歴・比較・プロファイル

DynamicはFLinqの結果・例外・生成コードを検証し、Scalableはコレクションと予算付きLINQを大規模入力で測定する。共有履歴は `Work/Benchmark/Benchmark.db` (版4) に保存する。独立したScalableは旧Workerを起動しない。

## 入口

- `Benchmark.Dynamic.slnx`: 通常実行、ライブ検証、固定の理想/参照実装、履歴・比較。
- `Benchmark.Scalable.slnx`: 分割入力の追加・検索・更新・削除・列挙、絞り込み・集約・並べ替え・結合、容量探索。
- `Tests.slnx`: 機能回帰。UI自動化に必要な環境を用意してからUIテストを実行する。
- `Seamless.slnx`: 全ソース。共有ソースは所有するshproj/projitemsも登録する。
- `Benchmark.Headless.slnx` と `Benchmark.Tsavorite.ParameterSweep.slnx`: 残存利用者向けのCLI保守。旧計測アセンブリ・Audit・個別最適化無効化版・理想版は保持する。

軽量ソリューションは `Update-FocusedSolutions.ps1` で再生成する。ProjectReference、子MSBuild、共有ソースの所有プロジェクトをたどる。

## 設定と実行

両画面の設定でMinimum/Optimized、拡張方式、メモリ/一時ファイル、明示する索引・常駐ログ容量、資源上限、読込プロファイルを選ぶ。Dynamicでは最適化フラグも独立に選べる。ライブラリの契約は [profiles.md](profiles.html) を参照する。

Temporaryではバックエンド既定、Buffered、Unbufferedと、Sequential/Randomを比較できる。実際に渡したStorageTuningを測定条件に記録し、異なる方式を同条件の性能回帰としない。MemoryへファイルI/Oを明示した指定はエラーにする。同梱プロファイルはバックエンド既定だけで検証している。

ScalableのCollectionsはNativeのコレクションAPIを使う。Persistedは削除APIを持たないため `Collections.AppendOnly` として記録し、削除を含むTsavoriteの処理と同一条件としない。LINQはScoped/System/Nativeを選び、Systemのメモリ内バッファが予算を超える見込みなら中止する。

CLIの要求ファイル例:

```json
{
  "Count": 4096,
  "Seed": 42,
  "Backend": "Tsavorite",
  "Kind": "Temporary",
  "Workload": "FilterAggregate",
  "Implementation": "Scoped",
  "Profile": "Minimum",
  "Growth": "doubling"
}
```

```powershell
dotnet run --project Benchmark/Benchmark.Scalable.Headless -- run --request Work/request.json
dotnet run --project Benchmark/Benchmark.Scalable.Headless -- history
```

同時にアプリを開いて履歴へ書き込める。重い計測は両アプリ共通の排他で直列化し、待機中は中止可能とする。物理メモリ85%以上では新規実行を始めず、88%で自分の実行を安全に中止する。他アプリのプロセスは終了しない。中止を合格としない。

## 保存と比較

各測定は指定条件・実設定・入力識別情報・実装・最適化フラグ・プロファイル・ストラテジー・環境・ビルド識別情報・結果・各回の時間と割り当て量を持つ。計測していないメモリ/ディスク量は不明として扱う。Scalableはプロセスのピーク常駐量と、保存元・Scoped LINQ作業領域のディスク予約量、Native外部ソートの一時ファイル論理量を合わせたピークを記録する。これらはクエリ専用の生存ヒープ量やファイルの物理サイズとは区別する。

通常実行とライブ検証は同じ詳細/共通履歴を使い、同じトランザクションでチェックポイントを保存する。途中終了したRunを完了済みとしない。Scalableの再構成は旧構成を保持したままコピーし、件数・内容を照合して切り替える。失敗は理由を記録し、旧構成を継続する。拡張と再構成の前後・時刻・所要時間を履歴へ残す。

共有履歴画面で実行を選び、その計測を基準/候補の選択肢へ追加する。

1. 良好な測定を明示的に基準へ固定する。新しい測定で自動更新しない。
2. 任意の2測定を選び、条件差と結果差を保存する。
3. 調整実験では変更設定を表示する。入力・意味論・測定方法・環境が違う結果は性能改善と判定しない。

性能比較には各7回以上の有効測定を要求し、中央値・ばらつき・割り当て量を表示する。悪化の既定しきい値は5%で、比較履歴に保存する。結果不一致、条件不一致、測定不足、未完了、容量探索の失敗種別変化は別の状態として扱う。しきい値を超えたというだけで原因を確定しない。

## Idealと回帰ケース

旧チェーンの25個の手書き参照実装は `Benchmark.Dynamic.Core/FixedCases/chains.json` に式・原文・原本SHA-256とともに保持する。Idealは保存した参照実装を表し、全ケースの最適性を保証する名前ではない。一部は通常のLINQを含む。対応するコードがあるケースだけを同じ入力で実測し、理想コードがないケースの時間を推定で作らない。

56個の旧演算子の原式は `LegacyChainCatalog` に保持する。225件の保存済みケースとペア網羅を0/1/16件の入力で検査する。IRへ近似した追加検査だけで、原式の回帰検査を代替しない。

## Optimizedの出力

同じ条件でMinimumを対照にした固定設定の実験を行う。独立した入力でも候補とMinimumの比較を保存し、両方で正しさと改善を確認してからプロファイルを出力する。選定と検証の入力・Runを使い回さない。自動調整中の途中設定をそのまま標準値へ採用しない。

```powershell
dotnet run --project Benchmark/Benchmark.Scalable.Headless -- compare --before CONTROL_ID --after CANDIDATE_ID --experiment true
dotnet run --project Benchmark/Benchmark.Scalable.Headless -- profile --id my-optimized --training TRAINING_ID --validation VALIDATION_ID --output Work/profiles.json
```

出力は設定、適用条件、ストラテジー、根拠のRunを含む。未検証の件数・型・処理・環境へ外挿しない。利用アプリはJSONを明示的に読み込み、測定DBへ接続しない。標準同梱値への採用も明示操作とする。

## 既存DBの明示移行

DynamicとScalableの書き込みを停止してから、版3のDynamic DBを未作成の共有先へ移す。

```powershell
dotnet run --project Benchmark/Benchmark.Scalable.Headless -- migrate --source Work/UI/Benchmark.Dynamic.db --writers-stopped true
```

SQLite Backup APIで原本の整合したバックアップを作り、そのスナップショットを版4へ移行する。原本は変更しない。既存の共有DBを上書きしない。アプリ起動時に既存DBのスキーマを自動移行・削除しない。旧記録に不足する条件を補造しない。条件不明の記録は自動の性能判定・プロファイル選定から除外する。

旧通常UI/Verification DBは `Work/Benchmark/Legacy` に保全している。旧Verificationの操作書は [移行前の資料](https://github.com/AloneCraft/Seamless/blob/main/docs/history/20261025-benchmark-consolidation/verification.md) としてのみ参照する。現行CIはExecutionとDB回帰を通常ジョブへ移し、重い性能探索を自動開始しない。

## 比較ポリシーとScalableの内容検証

`ComparisonPolicy` の既定版は `noise-v1`。条件、完了、内容、有効標本数を先に検査し、標本内の相対標準偏差が20%を超える場合はInconclusiveにする。その後に中央値の既定5%許容悪化率を判定する。ポリシー版を比較結果へ保存する。これは標本内変動の保守的な判定であり、有意差・信頼区間や独立実行間の再現性を証明するものではない。再標本化による推定は行わない。プロファイル発行では既定Minimum＋Doublingに対する改善を選定と独立入力・実行の両方で要求し、現行ポリシーで比較を再検査する。

Scalableの測定方式は `scalable-content-v2`。従来の読取件数・合計・昇順の検査を保ち、実行中に内容の固定メモリ指標を収集する。独立した期待列との照合は性能タイマーの停止後に行い、追加の照合時間を `VerificationNanoseconds` に記録する。4096件以下は全内容も比較し、より大きい列は件数、合計、異なる2種の混合値の合計で比較する。順序が必要な検証にはSHA-256の順序指標も利用できる。ハッシュ一致を数学的な完全一致証明とは扱わない。旧方式の時間とは直接比較しない。

Sampleの `ProcessBytes` は互換フィールド名として残すが、意味は `ProcessLifetimePeakWorkingSetBytes` (PeakWorkingSet64、プロセス生存期間全体の高水位)である。`ObservedIntervalWorkingSetBytes` は追加チャンク境界と実行前後のチェック地点の最大観測値であり、未観測のピークを含まない。`ReservedPeakMemoryBytes` / `ReservedPeakTemporaryStorageBytes` は共有親予算の予約ピークであり、プロセス実メモリや実ディスク量とは別指標である。追加照合による割り当て・GCは性能区間の指標へ含めない。

ストレージの性能測定でSSDを使う場合は `D:\SeamlessTestData`、HDDは `F:\SeamlessTestData` を明示し、使用許可を確認する。単体・小規模回帰テストの一時領域と、性能判断用の実測を混同しない。
