namespace Benchmark.UI.UITests;
//設定 > システム > 開発者向け > 開発者モード
//cmd
//  appium driver run windows install-wad
//  npm install -g appium
//  appium driver install windows
//  appium --address 127.0.0.1 --port 4723
//powershell
//  Set-Location "G:\GitHub\Seamless"
//  $env:BENCHMARK_UI_APP_PATH="G:\GitHub\Seamless\Benchmark\UI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\UI.exe"
//  dotnet test ".\Benchmark.UI.UITests\Benchmark.UI.UITests.csproj" -f net10.0-windows10.0.19041.0
//dotnet test Benchmark.UI.UITests\Benchmark.UI.UITests.csproj -f net10.0-windows10.0.19041.0
//Invoke-WebRequest http://127.0.0.1:4723/status

public sealed class SourceTabUiTests{
    [BenchmarkUiAutomationFact]
    public void SourceTab_ClickSecondTab_ShowsSecondSourceGrid(){
        using var session=new BenchmarkUiSession();

        session.WaitForAutomationId(BenchmarkUiAutomationIds.SourceTabView);
        session.WaitForAutomationId(BenchmarkUiAutomationIds.Grid("SystemArray"));

        session.ClickAutomationId(BenchmarkUiAutomationIds.SourceTab("SystemRange"));

        session.WaitForAutomationId(BenchmarkUiAutomationIds.Grid("SystemRange"));
    }

    [BenchmarkUiAutomationFact]
    public void タブを切り変えてもチェック状態を引き継がれないことを確認(){
        using var session=new BenchmarkUiSession();

        session.WaitForAutomationId(BenchmarkUiAutomationIds.Grid("SystemArray"));
        session.ClickAutomationId(BenchmarkUiAutomationIds.AllSelectionCheckHost);
        Assert.True(session.IsAutomationIdChecked(BenchmarkUiAutomationIds.AllSelectionCheck));

        session.ClickAutomationId(BenchmarkUiAutomationIds.SourceTab("SystemRange"));
        session.WaitForAutomationId(BenchmarkUiAutomationIds.Grid("SystemRange"));
        Assert.False(session.IsAutomationIdChecked(BenchmarkUiAutomationIds.AllSelectionCheck));
    }

    [BenchmarkUiAutomationFact]
    public void タブ往復後も各タブのグリッド行をクリックできる(){
        using var session=new BenchmarkUiSession();
        var arrayGrid=BenchmarkUiAutomationIds.Grid("SystemArray");
        var rangeGrid=BenchmarkUiAutomationIds.Grid("SystemRange");
        var cellCheckHost=BenchmarkUiAutomationIds.CellCheckHost("LeftCell");
        var cellCheck=BenchmarkUiAutomationIds.CellCheck("LeftCell");

        session.WaitForAutomationId(arrayGrid);
        session.ClickDescendantAutomationId(arrayGrid,cellCheckHost);
        Assert.True(session.IsDescendantAutomationIdChecked(arrayGrid,cellCheck));

        session.ClickAutomationId(BenchmarkUiAutomationIds.SourceTab("SystemRange"));
        session.WaitForAutomationId(rangeGrid);
        session.ClickDescendantAutomationId(rangeGrid,cellCheckHost);
        Assert.True(session.IsDescendantAutomationIdChecked(rangeGrid,cellCheck));

        session.ClickAutomationId(BenchmarkUiAutomationIds.SourceTab("SystemArray"));
        session.WaitForAutomationId(arrayGrid);
        Assert.True(session.IsDescendantAutomationIdChecked(arrayGrid,cellCheck));
    }
}
