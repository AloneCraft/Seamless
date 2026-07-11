namespace Benchmark.UI.UITests;
//設定 > システム > 開発者向け > 開発者モード
//cmd
//  appium driver run windows install-wad
//  npm install -g appium
//  appium driver install windows
//  appium --address 127.0.0.1 --port 4723
//powershell
//  Set-Location "G:\GitHub\Seamless"
//  $env:BENCHMARK_UI_APP_PATH="G:\GitHub\Seamless\Benchmark\Benchmark.UI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\UI.exe"
//  dotnet test ".\Benchmark.UI.UITests\Benchmark.UI.UITests.csproj" -f net10.0-windows10.0.19041.0
//dotnet test Benchmark.UI.UITests\Benchmark.UI.UITests.csproj -f net10.0-windows10.0.19041.0
//Invoke-WebRequest http://127.0.0.1:4723/status

public sealed class SourceTabUiTests{
    //タブは SfTabItem の Name(HeaderText、選択中は"Selected "前置)で操作・検証する。
    //グリッド行はセルチェック(SfCheckBox)の AutomationId で操作し、非表示タブ側の複製は Displayed で除外する。
    //注意: このテストは実プロファイルの選択状態を変更するため、終了時に必ず元へ戻す。
    private const string ArrayTab = "System.Array";
    private const string EnumerableTab = "System.Collections.Generic.IEnumerable";

    [BenchmarkUiAutomationFact]
    public void SourceTab_ClickSecondTab_ShowsSecondSourceGrid(){
        using var session=new BenchmarkUiSession();

        session.WaitForName("Selected "+ArrayTab);
        session.WaitForAutomationId(BenchmarkUiAutomationIds.CellCheck("LeftCell"));

        session.ClickName(EnumerableTab);

        session.WaitForName("Selected "+EnumerableTab);
        session.WaitForAutomationId(BenchmarkUiAutomationIds.CellCheck("LeftCell"));
    }

    [BenchmarkUiAutomationFact]
    public void タブを切り替えてもチェック状態が保持される(){
        using var session=new BenchmarkUiSession();
        var allCheck=BenchmarkUiAutomationIds.AllSelectionCheck;

        session.WaitForName("Selected "+ArrayTab);
        session.WaitForAutomationId(BenchmarkUiAutomationIds.CellCheck("LeftCell"));
        if(!session.IsAutomationIdChecked(allCheck)){
            //未選択または部分選択から全選択の確定状態にそろえる
            session.ClickAutomationId(allCheck);
            session.WaitUntilAutomationIdChecked(allCheck,true);
        }
        try{
            session.ClickName(EnumerableTab);
            session.WaitForName("Selected "+EnumerableTab);
            session.WaitUntilAutomationIdChecked(allCheck,true);

            session.ClickName(ArrayTab);
            session.WaitForName("Selected "+ArrayTab);
            session.WaitUntilAutomationIdChecked(allCheck,true);
        }
        finally{
            //全選択のまま残さないよう解除して終える
            session.ClickAutomationId(allCheck);
            session.WaitUntilAutomationIdChecked(allCheck,false);
        }
    }

    [BenchmarkUiAutomationFact]
    public void タブ往復後も各タブのグリッド行をクリックできる(){
        using var session=new BenchmarkUiSession();
        var cellCheck=BenchmarkUiAutomationIds.CellCheck("LeftCell");

        session.WaitForName("Selected "+ArrayTab);
        var arrayBefore=session.IsAutomationIdChecked(cellCheck);
        try{
            {
                //1タブ目の先頭行セルチェックをクリックできることを確認する
                session.ClickAutomationId(cellCheck);
                session.WaitUntilAutomationIdChecked(cellCheck,!arrayBefore);
            }
            {
                //2タブ目でも先頭行セルチェックをクリックできることを確認する
                session.ClickName(EnumerableTab);
                session.WaitForName("Selected "+EnumerableTab);
                var rangeBefore=session.IsAutomationIdChecked(cellCheck);
                session.ClickAutomationId(cellCheck);
                session.WaitUntilAutomationIdChecked(cellCheck,!rangeBefore);
                //元へ戻す
                session.ClickAutomationId(cellCheck);
                session.WaitUntilAutomationIdChecked(cellCheck,rangeBefore);
            }
            {
                //1タブ目へ戻ってもクリック状態が保持され、再クリックできることを確認する
                session.ClickName(ArrayTab);
                session.WaitForName("Selected "+ArrayTab);
                session.WaitUntilAutomationIdChecked(cellCheck,!arrayBefore);
            }
        }
        finally{
            //1タブ目の先頭行セルチェックを元へ戻す
            session.ClickName(ArrayTab);
            session.WaitForName("Selected "+ArrayTab);
            session.ClickAutomationId(cellCheck);
            session.WaitUntilAutomationIdChecked(cellCheck,arrayBefore);
        }
    }

    [BenchmarkUiAutomationFact]
    public void TopAllSelectionCheck_TogglesAllSourceTabs(){
        using var session=new BenchmarkUiSession();
        var topCheck=BenchmarkUiAutomationIds.TopAllSelectionCheck;
        var gridHeaderCheck=BenchmarkUiAutomationIds.AllSelectionCheck;

        session.WaitForName("Selected "+ArrayTab);
        session.WaitForAutomationId(BenchmarkUiAutomationIds.CellCheck("LeftCell"));
        if(!session.IsAutomationIdChecked(topCheck)){
            session.ClickAutomationId(topCheck);
            session.WaitUntilAutomationIdChecked(topCheck,true);
        }
        try{
            session.WaitUntilAutomationIdChecked(gridHeaderCheck,true);

            session.ClickName(EnumerableTab);
            session.WaitForName("Selected "+EnumerableTab);
            session.WaitUntilAutomationIdChecked(topCheck,true);
            session.WaitUntilAutomationIdChecked(gridHeaderCheck,true);

            session.ClickName(ArrayTab);
            session.WaitForName("Selected "+ArrayTab);
            session.WaitUntilAutomationIdChecked(topCheck,true);
            session.WaitUntilAutomationIdChecked(gridHeaderCheck,true);
        }
        finally{
            session.ClickAutomationId(topCheck);
            session.WaitUntilAutomationIdChecked(topCheck,false);
        }
    }
}
