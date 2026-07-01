namespace UI;

public sealed class BenchmarkToolbarAndCloseLogicTests{
    [Fact]
    public void IdleRunButtonUsesDirectState(){
        var state = BenchmarkToolbarStateLogic.CreateState(
            BenchmarkToolbarButtonRole.Run,
            new BenchmarkToolbarStateInput(
                IsRefreshing:false,
                IsBenchmarkRunActive:false,
                IsBenchmarkPaused:false,
                CanUseRunButton:true,
                CanStopBenchmark:false,
                CanPauseBenchmark:false));

        Assert.True(state.IsEnabled);
        Assert.True(state.InvokesPrimaryAction);
        Assert.False(state.ShowDisabledVisual);
        Assert.False(state.ShouldPulse);
    }

    [Fact]
    public void IdleRunButtonStaysDisabledWithoutSelection(){
        var state = BenchmarkToolbarStateLogic.CreateState(
            BenchmarkToolbarButtonRole.Run,
            new BenchmarkToolbarStateInput(
                IsRefreshing:false,
                IsBenchmarkRunActive:false,
                IsBenchmarkPaused:false,
                CanUseRunButton:false,
                CanStopBenchmark:false,
                CanPauseBenchmark:false));

        Assert.False(state.IsEnabled);
        Assert.False(state.InvokesPrimaryAction);
        Assert.True(state.ShowDisabledVisual);
        Assert.False(state.ShouldPulse);
    }

    [Fact]
    public void RunningPauseButtonUsesDirectState(){
        var state = BenchmarkToolbarStateLogic.CreateState(
            BenchmarkToolbarButtonRole.Pause,
            new BenchmarkToolbarStateInput(
                IsRefreshing:false,
                IsBenchmarkRunActive:true,
                IsBenchmarkPaused:false,
                CanUseRunButton:false,
                CanStopBenchmark:true,
                CanPauseBenchmark:true));

        Assert.True(state.IsEnabled);
        Assert.True(state.InvokesPrimaryAction);
        Assert.False(state.ShowDisabledVisual);
        Assert.False(state.ShouldPulse);
    }

    [Fact]
    public void PausedRunButtonPulsesAndExecutes(){
        var state = BenchmarkToolbarStateLogic.CreateState(
            BenchmarkToolbarButtonRole.Run,
            new BenchmarkToolbarStateInput(
                IsRefreshing:false,
                IsBenchmarkRunActive:false,
                IsBenchmarkPaused:true,
                CanUseRunButton:true,
                CanStopBenchmark:true,
                CanPauseBenchmark:false));

        Assert.True(state.IsEnabled);
        Assert.True(state.InvokesPrimaryAction);
        Assert.False(state.ShowDisabledVisual);
        Assert.True(state.ShouldPulse);
    }

    [Fact]
    public void PausedPauseButtonLooksDisabledButStaysClickableForGuardDialog(){
        var state = BenchmarkToolbarStateLogic.CreateState(
            BenchmarkToolbarButtonRole.Pause,
            new BenchmarkToolbarStateInput(
                IsRefreshing:false,
                IsBenchmarkRunActive:false,
                IsBenchmarkPaused:true,
                CanUseRunButton:true,
                CanStopBenchmark:true,
                CanPauseBenchmark:false));

        Assert.True(state.IsEnabled);
        Assert.False(state.InvokesPrimaryAction);
        Assert.True(state.ShowDisabledVisual);
        Assert.False(state.ShouldPulse);
    }

    [Fact]
    public void PausedPauseButtonRequiresDecisionDialog(){
        Assert.True(BenchmarkToolbarPauseGuardLogic.RequiresDecisionDialog(BenchmarkToolbarButtonRole.Pause,true));
        Assert.False(BenchmarkToolbarPauseGuardLogic.RequiresDecisionDialog(BenchmarkToolbarButtonRole.Run,true));
        Assert.False(BenchmarkToolbarPauseGuardLogic.RequiresDecisionDialog(BenchmarkToolbarButtonRole.Pause,false));
    }

    [Fact]
    public void CloseDialogWhilePausedOffersOnlyStopAndCancel(){
        var options = BenchmarkCloseDialogLogic.CreateOptions(
            isBenchmarkRunActive:false,
            isBenchmarkPaused:true,
            hasUnsavedResults:true);

        Assert.Collection(
            options,
            option => {
                Assert.Equal(BenchmarkCloseAction.StopAndClose,option.Action);
                Assert.Equal(BenchmarkToolbarButtonRole.Stop,option.Kind);
            },
            option => {
                Assert.Equal(BenchmarkCloseAction.Cancel,option.Action);
                Assert.Equal(BenchmarkToolbarButtonRole.Pause,option.Kind);
            });
    }

    [Fact]
    public void CloseDialogWithUnsavedIdleResultsOffersDiscardAndCancel(){
        var options = BenchmarkCloseDialogLogic.CreateOptions(
            isBenchmarkRunActive:false,
            isBenchmarkPaused:false,
            hasUnsavedResults:true);

        Assert.Collection(
            options,
            option => {
                Assert.Equal(BenchmarkCloseAction.StopAndClose,option.Action);
                Assert.Equal(BenchmarkToolbarButtonRole.Stop,option.Kind);
            },
            option => {
                Assert.Equal(BenchmarkCloseAction.Cancel,option.Action);
                Assert.Equal(BenchmarkToolbarButtonRole.Pause,option.Kind);
            });
    }
}
