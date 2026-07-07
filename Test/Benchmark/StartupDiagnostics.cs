#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class StartupDiagnostics{
    [Fact]
    public void BreadcrumbRingBuffer_KeepsRecentEvents(){
        var prefix = "breadcrumb-ring-"+Guid.NewGuid().ToString("N",System.Globalization.CultureInfo.InvariantCulture)+"-";
        for(var index = 0; index<130; index++){
            global::UI.StartupDiagnostics.AddBreadcrumb(prefix+index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var text = global::UI.StartupDiagnostics.GetCrashContextText("test");

        Assert.Contains(prefix+"129",text);
        Assert.DoesNotContain(prefix+"0",text);
    }

    [Fact]
    public void RunUiAction_LogsCrashContextAndRethrows(){
        var context = "ui-action-"+Guid.NewGuid().ToString("N",System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<InvalidOperationException>(() => global::UI.StartupDiagnostics.RunUiAction(
            context,
            static () => throw new InvalidOperationException("boom")));

        var text = global::UI.StartupDiagnostics.GetCrashContextText("test");
        Assert.Contains("UI dispatch start: "+context,text);
    }
}
#endif
