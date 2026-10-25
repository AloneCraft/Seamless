# ストレージテストの保存先をD/Fへ固定するランナー

## 合意と目的

2026-10-25の利用者指定により、このPCのSSDテストは `D:\SeamlessTestData`、HDDテストは `F:\SeamlessTestData` を使用する。ランナーの対応とコミットを行い、今回はテスト、ビルド、測定、画面操作を行わない。

従来のランナーは `Work/Consolidation/Invoke-Checked.ps1` にあり、親のCドライブのTEMPを継承していた。共有テストには作業ディレクトリへ直接データを作る経路もある。既存の統合設計にあるD/F指定とメモリ監視を引き継ぐ。

## 実装方針

- Git管理する `Invoke-StorageTest.ps1` を正本とし、従来の `Invoke-Checked.ps1` はこのランナーへ委譲する。
- `-Storage SSD` を既定とし、`-Storage HDD` でFを選ぶ。一度の実行は一方の媒体を使用し、SSD/HDDの比較は直列に実行する。
- 実行時にドライブと物理媒体を照合する。ドライブ不在、媒体の不一致、保存先のリパースポイント、作成失敗は実行前にエラーとし、CやGへフォールバックしない。
- 実行ごとに専用フォルダを作り、子プロセスの `TEMP` / `TMP` / `SEAMLESS_TEST_DATA_ROOT` を設定する。親プロセスやOSの環境変数は変更しない。
- 作業ディレクトリ基準の共有テストも `SEAMLESS_TEST_DATA_ROOT` を参照する。ランナー外では従来の保存先を維持し、ライブラリの製品既定値は変更しない。
- Scalableの要求JSONは原本を変更せず実行用コピーの `TemporaryDirectory` を選択媒体へ設定する。ParameterSweepの `--working` とTPC-Eの `--work` の明示指定は選択媒体内に限定する。
- ParameterSweepの作業先未指定時とTPC-Eの既定作業先も、その実行のテストデータフォルダ配下へ置く。ソリューションや要求JSONの相対パスを壊さないため、dotnet自体の作業ディレクトリはリポジトリ直下を維持する。
- 実行メタデータとログを `Work/Consolidation` に残し、実際の媒体・データ保存先を追跡可能にする。履歴DBと既存バックアップを移動・削除しない。
- 物理メモリ85%以上では開始せず、実行中に88%以上へ達したらランナーが開始した子プロセスだけを終了し、成功扱いにしない。

## 使用方法

PowerShell 7で、リポジトリ直下から呼び出す。以下は次回用のコマンド例であり、今回実行しない。

```powershell
& ./Invoke-StorageTest.ps1 -Name execution-ssd -Storage SSD -Arguments @('test','Execution.Tests/Execution.Tests.csproj','--no-restore','-m:1')
& ./Invoke-StorageTest.ps1 -Name execution-hdd -Storage HDD -Arguments @('test','Execution.Tests/Execution.Tests.csproj','--no-restore','-m:1')
& ./Work/Consolidation/Invoke-Checked.ps1 -Name scalable-ssd -Storage SSD -Arguments @('Benchmark/Benchmark.Scalable.Headless/bin/Release/net10.0/Benchmark.Scalable.Headless.dll','run','--request','Work/Consolidation/scalable-large-sort.json')
```

データフォルダは自動削除しない。中断時の調査と再開に使うため、利用終了後にログの `DataDirectory` と使用中でないことを確認して片付ける。ドライブ指定はこのPCの実行環境の設定であり、他PCでドライブを代替して成功扱いにしない。

## 検証と限界

今回はPowerShellの構文解析、差分確認、保存先を選ぶコードの静的レビューだけを行う。D/Fへの書き込み、dotnetの起動、テスト、ビルド、画面操作は行わない。実際のファイル配置、媒体ごとのテスト成否、子プロセスへの継承は次回の実行で確認する。

静的確認ではランナー本体と互換入口のPowerShell構文エラーが0件であること、および今回の変更の `git diff --check` にエラーがないことを確認した。これをテスト実行成功とは扱わない。

任意のテストコードが絶対パスを独自に指定することまでOSレベルで禁止するランナーではない。新しいストレージテストではTEMPまたは共通のテスト保存先を使い、明示保存先を追加する場合はこの制約へ接続する。
