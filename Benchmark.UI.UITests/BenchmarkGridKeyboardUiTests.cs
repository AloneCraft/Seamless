using OpenQA.Selenium;

namespace Benchmark.UI.UITests;

public sealed class BenchmarkGridKeyboardUiTests{
    private const string ArrayTab="System.Array";

    [BenchmarkUiAutomationFact]
    public void ArrowAndSpace_MoveSelectionAndToggleCurrentRow(){
        using var session=new BenchmarkUiSession();
        session.WaitForName("Selected "+ArrayTab);
        var checks=session.WaitForDisplayedAutomationIds(BenchmarkUiAutomationIds.RowCheck,2);
        var before=checks.Take(2).Select(session.IsChecked).ToArray();
        try{
            session.ClickNormalCell(checks[0]);
            session.SendKeysAutomationId(BenchmarkUiAutomationIds.Grid(ArrayTab),Keys.ArrowDown);
            session.SendKeysAutomationId(BenchmarkUiAutomationIds.Grid(ArrayTab),Keys.Space);

            Assert.Equal(before[0],session.IsChecked(checks[0]));
            Assert.NotEqual(before[1],session.IsChecked(checks[1]));
        }
        finally{
            this.RestoreChecks(session,checks,before);
        }
    }

    [BenchmarkUiAutomationFact]
    public void ShiftAndCtrl_UseExtendedRowSelectionAndSpaceTogglesSelection(){
        using var session=new BenchmarkUiSession();
        session.WaitForName("Selected "+ArrayTab);
        var checks=session.WaitForDisplayedAutomationIds(BenchmarkUiAutomationIds.RowCheck,3);
        var before=checks.Take(3).Select(session.IsChecked).ToArray();
        try{
            session.ClickNormalCell(checks[0]);
            session.ClickNormalCell(checks[1],Keys.Shift);
            session.ClickNormalCell(checks[2],Keys.Control);
            session.SendKeysAutomationId(BenchmarkUiAutomationIds.Grid(ArrayTab),Keys.Space);

            var expected=before.All(static value => value) ? false : true;
            Assert.All(checks.Take(3),check => Assert.Equal(expected,session.IsChecked(check)));
        }
        finally{
            this.RestoreChecks(session,checks,before);
        }
    }

    private void RestoreChecks(BenchmarkUiSession session,IReadOnlyList<IWebElement> checks,IReadOnlyList<bool> states){
        for(var index=0;index<states.Count;index++){
            if(session.IsChecked(checks[index])!=states[index]){
                session.ClickNormalCell(checks[index]);
                session.SendKeysAutomationId(BenchmarkUiAutomationIds.Grid(ArrayTab),Keys.Space);
            }
        }
    }
}
