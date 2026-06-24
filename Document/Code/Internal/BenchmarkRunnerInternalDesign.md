---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/BenchmarkRunnerInternalDesign.md
evidence_sources:
  - Benchmark/Benchmark.csproj
  - Benchmark/MainPage.xaml.cs
  - Benchmark/BenchmarkWorkerHost.cs
  - Benchmark/BenchmarkRunnerModels.cs
  - Benchmark/CpuTopologyProvider.cs
  - Benchmark/BenchmarkReportFormatter.cs
confidence: medium
review_status: draft
---

# Benchmark 内部設計

## 対象
`Benchmark` は Syncfusion Toolkit for .NET MAUI desktop app として構成され、BenchmarkDotNet benchmark assembly を UI から選択し、CPU affinity ごとの worker process で実行する（`Benchmark/Benchmark.csproj:1`, `Benchmark/Benchmark.csproj:3`, `Benchmark/Benchmark.csproj:24`, `Benchmark/Benchmark.csproj:33`）。

## 主要クラス

| クラス/領域 | 責務 | 実装証拠 |
|---|---|---|
| `MainPage` | benchmark project/method の読み込み、UI 状態、worker 起動、run queue、report 生成を管理する。 | `Benchmark/MainPage.xaml.cs:541`, `Benchmark/MainPage.xaml.cs:589`, `Benchmark/MainPage.xaml.cs:688`, `Benchmark/MainPage.xaml.cs:841`, `Benchmark/MainPage.xaml.cs:1131` |
| `BenchmarkWorkerHost` | worker mode 判定、stdin/stdout protocol、BenchmarkDotNet 実行、結果 message を返す。 | `Benchmark/BenchmarkWorkerHost.cs:14`, `Benchmark/BenchmarkWorkerHost.cs:17`, `Benchmark/BenchmarkWorkerHost.cs:21`, `Benchmark/BenchmarkWorkerHost.cs:48`, `Benchmark/BenchmarkWorkerHost.cs:69` |
| `BenchmarkRunnerModels` | UI bindable model、project/method/run item、worker task/result、report row を定義する。 | `Benchmark/BenchmarkRunnerModels.cs:7`, `Benchmark/BenchmarkRunnerModels.cs:25`, `Benchmark/BenchmarkRunnerModels.cs:154`, `Benchmark/BenchmarkRunnerModels.cs:162`, `Benchmark/BenchmarkRunnerModels.cs:270` |
| `CpuTopologyProvider` | OS 別に CPU topology と affinity availability を復元する。 | `Benchmark/CpuTopologyProvider.cs:7`, `Benchmark/CpuTopologyProvider.cs:37`, `Benchmark/CpuTopologyProvider.cs:90`, `Benchmark/CpuTopologyProvider.cs:112`, `Benchmark/CpuTopologyProvider.cs:218` |
| `BenchmarkReportFormatter` | run result を CSV/HTML に整形する。 | `Benchmark/BenchmarkReportFormatter.cs:6`, `Benchmark/BenchmarkReportFormatter.cs:7`, `Benchmark/BenchmarkReportFormatter.cs:27` |

## ランタイムフロー

1. desktop entry point は worker mode なら `BenchmarkWorkerHost.RunAsync`、通常なら MAUI host を起動する（`Benchmark/Platforms/Desktop/Program.cs:10`, `Benchmark/Platforms/Desktop/Program.cs:22`）。
2. UI は benchmark assembly を探索し、`BenchmarkAttribute` が付いた method を project/method model に変換する（`Benchmark/MainPage.xaml.cs:845`, `Benchmark/MainPage.xaml.cs:871`, `Benchmark/MainPage.xaml.cs:924`）。
3. selected CPU ごとに worker process を起動し、task message を送信する（`Benchmark/MainPage.xaml.cs:688`, `Benchmark/MainPage.xaml.cs:752`）。
4. worker は filter 付きで BenchmarkDotNet を実行し、結果を JSON message として返す（`Benchmark/BenchmarkWorkerHost.cs:39`, `Benchmark/BenchmarkWorkerHost.cs:90`, `Benchmark/BenchmarkWorkerHost.cs:96`）。
5. UI は `BenchmarkComparison.csv/html` を出力する（`Benchmark/MainPage.xaml.cs:1153`, `Benchmark/MainPage.xaml.cs:1160`）。

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
- 通常設計書へ反映する際、MAUI runner を製品機能とするか開発支援ツールとするかを確認する。
