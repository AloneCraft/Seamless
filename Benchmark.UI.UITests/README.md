# Benchmark UI Automation

WinAppDriver/Appiumで`Benchmark.UI`のWindows UIを操作するテストです。

## 実行準備

1. Windowsの開発者モードを有効にする。
2. WinAppDriverまたはAppium Windows Driverを`http://127.0.0.1:4723`で起動する。
3. `Benchmark\Benchmark.UI\Benchmark.UI.csproj`をWindows向けにビルドする。
4. `BENCHMARK_UI_APP_PATH`へ生成された`UI.exe`の絶対パスを設定する。

例:

```powershell
$env:BENCHMARK_UI_APP_PATH="G:\GitHub\Seamless\Benchmark\Benchmark.UI\bin\Debug\net10.0-windows10.0.19041.0\win10-x64\UI.exe"
dotnet test Benchmark.UI.UITests\Benchmark.UI.UITests.csproj -f net10.0-windows10.0.19041.0
```

`BENCHMARK_UI_APPIUM_URL`を設定すると接続先を変更できます。未設定時は`http://127.0.0.1:4723`を使います。
