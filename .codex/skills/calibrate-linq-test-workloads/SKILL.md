---
name: calibrate-linq-test-workloads
description: Seamless の System LINQ benchmark-as-test 負荷を再調整する。Shared/Seamless.Linq.Enumerable/Methods1.cs の要素数または反復回数を、Test.System.Linq.Enumerable.Linq、Test.System.Linq.Enumerable.FLinq、Test.System.Linq.Enumerable.ZLinq の Release 直列 Stopwatch 計測が目標時間内に収まるよう調整するときに使う。
---

# LINQ テスト負荷調整

## 概要

この skill は、Seamless の System LINQ benchmark-as-test の負荷量を再調整するために使う。基準にするのは xUnit の表示時間ではなく、`[Benchmark]` メソッド本体の Stopwatch 計測時間。

## 対象範囲

- 対象は `Test.System.Linq.Enumerable.Linq`、`Test.System.Linq.Enumerable.FLinq`、`Test.System.Linq.Enumerable.ZLinq` のみ。
- ユーザーが明示的に範囲を広げない限り、調整対象は `Shared/Seamless.Linq.Enumerable/Methods1.cs` の負荷定数と `*_iterationCount` だけにする。
- 要素数の基準は LINQ の Release 直列 Stopwatch 計測に置く。
- LINQ の benchmark メソッドは `500 ms` 目安に対して余裕を持たせ、`450 ms` 以下を目標にする。
- FLinq と ZLinq は成功確認だけを行う。FLinq または ZLinq の時間超過だけを理由に、共有負荷を追加で縮小しない。
- 固定閾値や固定インデックスの意味維持より、負荷量の目標達成を優先する。

## 測定

リポジトリルートから `scripts/measure-system-linq-workload.ps1` を実行する。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\.codex\skills\calibrate-linq-test-workloads\scripts\measure-system-linq-workload.ps1 `
  -ProjectPath .\Test.System.Linq.Enumerable.Linq\Test.System.Linq.Enumerable.Linq.csproj `
  -Configuration Release `
  -WarmupCount 1 `
  -IterationCount 1
```

このスクリプトはテストプロジェクトをビルドし、`artifacts/` 配下に一時的な `net10.0` runner を作る。runner はテストアセンブリを reflection で読み込み、public な `[Benchmark]` メソッドを warmup として1回呼び出した後、1回分を測定して CSV を出力する。

## 調整手順

1. warmup 有効で LINQ の Release 計測を行う。
2. `ElapsedMs` が `450` を超える行を探す。
3. 遅いメソッドを、`Shared/Seamless.Linq.Enumerable/Methods1.cs` 内で使っている負荷定数または `*_iterationCount` に対応付ける。
4. 複数メソッドが同じ定数を共有する場合は、その定数に依存する LINQ メソッドのうち最も遅いものを基準にする。
5. `current * 450 / elapsedMs` で縮小後の値を計算し、次の規則で切り下げる。
   - `>= 1_000_000`: 直近下位の `100_000` 単位
   - `>= 100_000`: 直近下位の `10_000` 単位
   - `>= 10_000`: 直近下位の `1_000` 単位
   - それ以外: 直近下位の `100` 単位、最小値は `1`
6. この調整では、既存の負荷量を増やさない。
7. `test.runsettings` を直列実行設定にする。
   - `MaxCpuCount` = `1`
   - `MaxParallelThreads` = `1`
   - `ParallelizeTestCollections` = `false`
8. LINQ を再測定し、全 LINQ benchmark 行が目標以下になるまで、または残りが明らかな測定揺れと判断できるまで反復する。

## 検証

編集後、次を実行する。

```powershell
python C:\Users\kazuh\.codex\skills\.system\skill-creator\scripts\quick_validate.py .\.codex\skills\calibrate-linq-test-workloads
powershell -NoProfile -ExecutionPolicy Bypass -File .\.codex\skills\calibrate-linq-test-workloads\scripts\measure-system-linq-workload.ps1 -Configuration Release
dotnet test .\Test.System.Linq.Enumerable.Linq\Test.System.Linq.Enumerable.Linq.csproj -c Release --no-restore
dotnet test .\Test.System.Linq.Enumerable.FLinq\Test.System.Linq.Enumerable.FLinq.csproj -c Release --no-restore
dotnet test .\Test.System.Linq.Enumerable.ZLinq\Test.System.Linq.Enumerable.ZLinq.csproj -c Release --no-restore
```

目標を超えた LINQ 行が残る場合は、該当メソッドと測定された `ElapsedMs` を報告する。
