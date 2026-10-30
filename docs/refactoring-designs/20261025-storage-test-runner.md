# ストレージテストの保存先をD/Fへ固定するランナー

## 合意と目的

2026-10-25の利用者指定により、このPCのSSDテストは `D:\SeamlessTestData`、HDDテストは `F:\SeamlessTestData` を使用する。ランナー実装時は実行を保留し、その後の利用者指示でテストと測定を再開した。実画面の操作許可は別に確認する。

従来のランナーは `Work/Consolidation/Invoke-Checked.ps1` にあり、親のCドライブのTEMPを継承していた。共有テストには作業ディレクトリへ直接データを作る経路もある。既存の統合設計にあるD/F指定とメモリ監視を引き継ぐ。

## 実装方針

- Git管理する `Invoke-StorageTest.ps1` を正本とし、従来の `Invoke-Checked.ps1` はこのランナーへ委譲する。
- `-Storage SSD` を既定とし、`-Storage HDD` でFを選ぶ。一度の実行は一方の媒体を使用し、SSD/HDDの比較は直列に実行する。
- 実行時にドライブと物理媒体を照合する。ドライブ不在、媒体の不一致、保存先のリパースポイント、作成失敗は実行前にエラーとし、CやGへフォールバックしない。
- 実行ごとに専用フォルダを作り、子プロセスの `TEMP` / `TMP` / `SEAMLESS_TEST_DATA_ROOT` を設定する。親プロセスやOSの環境変数は変更しない。
- データの実行フォルダ名は32文字のGUIDだけとする。実行名と時刻をパスへ重ねると、長いテスト名とTsavoriteのチェックポイント階層が長さ制限を超えるため、実行名・時刻との対応はログとメタデータに保存する。
- 作業ディレクトリ基準の共有テストも `SEAMLESS_TEST_DATA_ROOT` を参照する。ランナー外では従来の保存先を維持し、ライブラリの製品既定値は変更しない。
- Scalableの要求JSONは原本を変更せず実行用コピーの `TemporaryDirectory` を選択媒体へ設定する。ParameterSweepの `--working` とTPC-Eの `--work` の明示指定は選択媒体内に限定する。
- ParameterSweepの作業先未指定時とTPC-Eの既定作業先も、その実行のテストデータフォルダ配下へ置く。ソリューションや要求JSONの相対パスを壊さないため、dotnet自体の作業ディレクトリはリポジトリ直下を維持する。
- 実行メタデータとログを `Work/Consolidation` に残し、実際の媒体・データ保存先を追跡可能にする。履歴DBと既存バックアップを移動・削除しない。
- ランナーの媒体メタデータに加え、測定条件とOptimized候補の `StorageKey` に実際の保存先ボリュームの識別子を記録する。D/Fで異なるキーとなり、別ボリュームの候補は自動適用しない。保存先が不明な旧ディスク履歴は保持するが、性能比較と候補採用には使わない。
- コンソールには末尾12行を各800文字まで表示する。共有履歴の詳細JSONは1行が数MBになることがあり、`Get-Content -Tail` による逆向きの読み込み自体が長時間かかるため、.NETの行列挙で前から読み、表示用の末尾だけを保持する。完全な出力はログファイルに保持する。
- 物理メモリ85%以上では開始せず、実行中に88%以上へ達したらランナーが開始した子プロセスだけを終了し、成功扱いにしない。

## 使用方法

PowerShell 7で、リポジトリ直下から呼び出す。

```powershell
& ./Invoke-StorageTest.ps1 -Name execution-ssd -Storage SSD -Arguments @('test','Execution.Tests/Execution.Tests.csproj','--no-restore','-m:1')
& ./Invoke-StorageTest.ps1 -Name execution-hdd -Storage HDD -Arguments @('test','Execution.Tests/Execution.Tests.csproj','--no-restore','-m:1')
& ./Work/Consolidation/Invoke-Checked.ps1 -Name scalable-ssd -Storage SSD -Arguments @('Benchmark/Benchmark.Scalable.Headless/bin/Release/net10.0/Benchmark.Scalable.Headless.dll','run','--request','Work/Consolidation/scalable-large-sort.json')
```

データフォルダは自動削除しない。中断時の調査と再開に使うため、利用終了後にログの `DataDirectory` と使用中でないことを確認して片付ける。ドライブ指定はこのPCの実行環境の設定であり、他PCでドライブを代替して成功扱いにしない。

## 検証と限界

保存先対応の作成時は利用者指定により静的確認だけを行い、PowerShell構文エラー0件と `git diff --check` の成功を確認した。その後、利用者のテスト再開指示により、確定コミット `acd9749a` の分離コピーでReleaseの実行確認を行った。別作業の生成器の未コミット変更は検証へ混ぜていない。

- D/FでそれぞれExecution 43件、Infrastructure 8件、Scalable 23件、Persisted.Collections 163件、Tsavorite.Collections 368件が成功。実ファイルがD/F配下に作られることも確認した。
- 当初の長い実行フォルダ名ではTsavoriteの保存・復元19件がパス長エラーとなった。同じDLLのままGUIDだけのフォルダ名へ変え、該当領域28件と全368件の成功を確認した。元の失敗結果も保持する。
- 検証コピーにはこのランナーのパス長修正を適用した。対象コミットと修正ハッシュ、ログ・TRXは `Work/Consolidation/active-validation.json` が指す検証ディレクトリに保存する。
- 画面操作は行っていない。テスト成功を性能改善や実画面検証の成功とは扱わない。
- 共有履歴35件・約7.4MBのJSONを読む回帰確認では、`Get-Content -Tail` の表示準備に長時間CPUを使う問題も再現した。前向きのストリーミング読み込みへ変更後、ランナー全体が約4秒で正常終了し、完全ログのSHA-256が修正前と一致することを確認した。途中で停止した表示処理は成功扱いにせず、正常終了した再実行を確認結果とする。

任意のテストコードが絶対パスを独自に指定することまでOSレベルで禁止するランナーではない。新しいストレージテストではTEMPまたは共通のテスト保存先を使い、明示保存先を追加する場合はこの制約へ接続する。
