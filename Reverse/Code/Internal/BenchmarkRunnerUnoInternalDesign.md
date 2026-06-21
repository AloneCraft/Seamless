---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/BenchmarkRunnerUnoInternalDesign.md
evidence_sources:
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkReportFormatter.cs
confidence: medium
review_status: draft
---

# Benchmark.Runner.Uno 内部設計

## 対象
`Benchmark.Runner.Uno` は Uno Platform desktop app として構成され、BenchmarkDotNet benchmark assembly を UI から選択し、CPU affinity ごとの worker process で実行する（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:1`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:3`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:24`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:33`）。

## 主要クラス

| クラス/領域 | 責務 | 実装証拠 |
|---|---|---|
| `MainPage` | benchmark project/method の読み込み、UI 状態、worker 起動、run queue、report 生成を管理する。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:541`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:589`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:688`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:841`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:1131` |
| `BenchmarkWorkerHost` | worker mode 判定、stdin/stdout protocol、BenchmarkDotNet 実行、結果 message を返す。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:14`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:17`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:21`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:48`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:69` |
| `BenchmarkRunnerModels` | UI bindable model、project/method/run item、worker task/result、report row を定義する。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs:7`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs:25`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs:154`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs:162`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs:270` |
| `CpuTopologyProvider` | OS 別に CPU topology と affinity availability を復元する。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs:7`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs:37`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs:90`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs:112`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs:218` |
| `BenchmarkReportFormatter` | run result を CSV/HTML に整形する。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkReportFormatter.cs:6`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkReportFormatter.cs:7`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkReportFormatter.cs:27` |

## ランタイムフロー

1. desktop entry point は worker mode なら `BenchmarkWorkerHost.RunAsync`、通常なら Uno host を起動する（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/Platforms/Desktop/Program.cs:10`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Platforms/Desktop/Program.cs:22`）。
2. UI は benchmark assembly を探索し、`BenchmarkAttribute` が付いた method を project/method model に変換する（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:845`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:871`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:924`）。
3. selected CPU ごとに worker process を起動し、task message を送信する（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:688`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:752`）。
4. worker は filter 付きで BenchmarkDotNet を実行し、結果を JSON message として返す（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:39`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:90`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:96`）。
5. UI は `BenchmarkComparison.csv/html` を出力する（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:1153`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:1160`）。

## Review Gate: Code Internal

### Confirmed
- Uno desktop app と worker process の二重モードで構成される。
- BenchmarkDotNet method を reflection で抽出し、CPU affinity ごとに worker を走らせる。
- CSV/HTML report 生成が実装されている。

### Inferred
- confidence: medium。CPU 種別/affinity を分けた benchmark 比較が主目的である。
- confidence: medium。worker process 分離は UI 応答性と affinity 制御のためと推測できる。

### Unknown
- UI 操作仕様の正式な要求。
- worker protocol の互換性保証、再試行ポリシー、失敗時 artifact 保持方針。

### Questions
- 通常設計書へ反映する際、Uno runner を製品機能とするか開発支援ツールとするかを確認する。
