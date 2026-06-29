# 終了ダイアログ フローチャート

```mermaid
flowchart TD
  A["AppWindow.Closing"] --> B{app.IsCloseConfirmed}
  B -- はい --> C["即時終了"]
  B -- いいえ --> D["args.Cancel = true"]
  D --> E["HandleWindowClosingAsync を開始"]
  E --> F["app.RequestCloseAsync()"]

  F --> G{closeConfirmed または closeInProgress}
  G -- はい --> H["closeConfirmed または closeInProgress が true なら既存値を返却"]
  G -- いいえ --> I{MainPage を取得}
  I -- 失敗 --> J["MainPage が無い場合は closeConfirmed = true を返却"]
  I -- 成功 --> K["closeInProgress = true"]
  K --> L["MainPage.RequestCloseAsync()"]

  L --> M{viewModel is null}
  M -- はい --> J
  M -- いいえ --> N{ベンチマーク実行中 or 一時停止中}
  N -- はい --> O["ダイアログA: ベンチマークを中止して終了しますか？"]
  N -- いいえ --> P{未保存結果あり}
  O -- はい --> Q["StatusText = Stopping before close を設定し StopAsync 実行"]
  O -- いいえ --> R["false を返却"]
  Q --> P

  P -- いいえ --> J
  P -- はい --> S["ダイアログB: 保存しますか？"]
  S -- はい --> T["SaveCurrentBenchmarkResultsAsync(true) を実行して true を返却"]
  T --> J
  S -- いいえ --> U["ダイアログC: 終了しますか？"]
  U -- はい --> V["DiscardUnsavedBenchmarkResults() を実行して true を返却"]
  U -- いいえ --> R

  J --> W["closeInProgress=false を確定（finally）"]
  W --> Y{RequestCloseAsync 結果}
  R --> X["false を返却"]
  X --> Y
  Y -- true --> Z["window.Close() 実行"]
  Y -- false --> AA["終了をキャンセル"]
  Z --> AB["Close 完了"]
```

### 参照元
- [Benchmark/MauiProgram.cs](G:\GitHub\Seamless\Benchmark\MauiProgram.cs)
- [Benchmark/App.xaml.cs](G:\GitHub\Seamless\Benchmark\App.xaml.cs)
- [Benchmark/MainPage.cs](G:\GitHub\Seamless\Benchmark/MainPage.cs)
