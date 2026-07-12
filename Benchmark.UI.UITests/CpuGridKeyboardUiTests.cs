using OpenQA.Selenium;

namespace Benchmark.UI.UITests;

public sealed class CpuGridKeyboardUiTests{
    [BenchmarkUiAutomationFact]
    public void ShiftSelectionAndSpace_ToggleSelectedCpuCellsAfterMouseInput(){
        using var session=new BenchmarkUiSession();
        var checks=session.WaitForDisplayedAutomationIds(BenchmarkUiAutomationIds.CpuCellCheck,2);
        var targets=checks.Take(2).ToArray();
        var before=targets.Select(session.IsChecked).ToArray();
        Assert.Equal(before[0],before[1]);

        session.ClickCpuCell(targets[0]);
        session.ClickCpuCell(targets[1],Keys.Shift);
        session.SendKeys(Keys.Space);

        var expected=!before[0];
        Assert.All(targets,check => Assert.Equal(expected,session.IsChecked(check)));

        session.SendKeys(Keys.Space);
        Assert.All(targets,check => Assert.Equal(before[0],session.IsChecked(check)));
    }
}
