using UI;

namespace Test.Benchmark;

public sealed class BenchmarkChartSelectionLayoutLogic{
    [Fact]
    public void CreateBarRects_RightBarEndsAtPlotRightForEvenValues(){
        var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(
            plotLeft:100,
            plotRight:500,
            leftValue:50,
            rightValue:50);

        Assert.Equal(300,rects.LeftBarRect.Right);
        Assert.Equal(rects.LeftBarRect.Right,rects.RightBarRect.Left);
        Assert.Equal(500,rects.RightBarRect.Right);
        Assert.Equal(200,rects.RightBarRect.Width);
    }

    [Fact]
    public void CreateBarRects_RightBarCoversRemainingWidthForUnevenValues(){
        var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(
            plotLeft:100,
            plotRight:500,
            leftValue:96,
            rightValue:100);

        Assert.Equal(rects.LeftBarRect.Right,rects.RightBarRect.Left);
        Assert.Equal(500,rects.RightBarRect.Right);
        Assert.Equal(rects.RightBarRect.Right-rects.RightBarRect.Left,rects.RightBarRect.Width);
    }

    [Fact]
    public void CalculateStackBoundary_ClampsPercentToPlotRange(){
        Assert.Equal(100,global::UI.BenchmarkChartSelectionLayoutLogic.CalculateStackBoundary(100,500,-10));
        Assert.Equal(500,global::UI.BenchmarkChartSelectionLayoutLogic.CalculateStackBoundary(100,500,110));
    }

    [Fact]
    public void CreateBarLabelPoints_UsesEachBarLeftEdge(){
        var points = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarLabelPoints(
            new BenchmarkChartBarRect(100,250,150),
            new BenchmarkChartBarRect(250,500,250),
            y:24,
            padding:6);

        Assert.Equal(106,points.LeftLabelPoint.X);
        Assert.Equal(256,points.RightLabelPoint.X);
        Assert.Equal(24,points.LeftLabelPoint.Y);
        Assert.Equal(24,points.RightLabelPoint.Y);
    }
}



