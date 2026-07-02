using UI;

namespace Test.Benchmark;

public sealed class BenchmarkChartSelectionLayoutLogic{
    [Fact]
    public void CreateShareTexts_FormatsFractionsThatSumToOne(){
        var texts = global::UI.BenchmarkChartSelectionLayoutLogic.CreateShareTexts(1,9);

        Assert.Equal("0.10",texts.LeftText);
        Assert.Equal("0.90",texts.RightText);
    }

    [Fact]
    public void CreateShareTexts_KeepsRoundedDisplaySumAtOne(){
        var texts = global::UI.BenchmarkChartSelectionLayoutLogic.CreateShareTexts(995,5);

        Assert.Equal("1.00",texts.LeftText);
        Assert.Equal("0.00",texts.RightText);
    }

    [Fact]
    public void CreateRatio_UsesMeasurementRatioNotShare(){
        var ratio = global::UI.BenchmarkChartSelectionLayoutLogic.CreateRatio(1.38,1.24);
        var shareTexts = global::UI.BenchmarkChartSelectionLayoutLogic.CreateShareTexts(1.38,1.24);

        Assert.Equal(1.1129032258064515,ratio.GetValueOrDefault(),12);
        Assert.Equal("0.53",shareTexts.LeftText);
        Assert.Equal("0.47",shareTexts.RightText);
    }

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
    public void CreateBarRects_UsesStackBoundaryFromShare(){
        {
            // 50:50
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(100,500,50,50);

            Assert.Equal(300,rects.LeftBarRect.Right);
            Assert.Equal(300,rects.RightBarRect.Left);
        }
        {
            // 89:11
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(100,500,89,11);

            Assert.Equal(456,rects.LeftBarRect.Right);
            Assert.Equal(456,rects.RightBarRect.Left);
        }
        {
            // 99:1
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(100,500,99,1);

            Assert.Equal(496,rects.LeftBarRect.Right);
            Assert.Equal(496,rects.RightBarRect.Left);
        }
        {
            // 1:99
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(100,500,1,99);

            Assert.Equal(104,rects.LeftBarRect.Right);
            Assert.Equal(104,rects.RightBarRect.Left);
        }
        {
            // 0:100
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(100,500,0,100);

            Assert.Equal(100,rects.LeftBarRect.Right);
            Assert.Equal(100,rects.RightBarRect.Left);
        }
        {
            // 100:0
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarRects(100,500,100,0);

            Assert.Equal(500,rects.LeftBarRect.Right);
            Assert.Equal(500,rects.RightBarRect.Left);
        }
    }

    [Fact]
    public void CalculateStackBoundary_ClampsPercentToPlotRange(){
        Assert.Equal(100,global::UI.BenchmarkChartSelectionLayoutLogic.CalculateStackBoundary(100,500,-10));
        Assert.Equal(500,global::UI.BenchmarkChartSelectionLayoutLogic.CalculateStackBoundary(100,500,110));
    }

    [Fact]
    public void CreateBarLabelPoints_UsesStackBoundarySides(){
        var points = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBarLabelPoints(
            new BenchmarkChartBarRect(100,250,150),
            new BenchmarkChartBarRect(250,500,250),
            y:24,
            padding:6);

        Assert.Equal(244,points.LeftLabelPoint.X);
        Assert.Equal(256,points.RightLabelPoint.X);
        Assert.Equal(24,points.LeftLabelPoint.Y);
        Assert.Equal(24,points.RightLabelPoint.Y);
    }

    [Fact]
    public void CreateBoundaryLabelRects_CentersLabelsOnBarHeightAtBoundary(){
        var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBoundaryLabelRects(
            new BenchmarkChartBarRect(100,250,150),
            new BenchmarkChartBarRect(250,500,250),
            centerY:24,
            labelWidth:42,
            labelHeight:16,
            padding:6);

        Assert.Equal(202,rects.LeftLabelRect.X);
        Assert.Equal(256,rects.RightLabelRect.X);
        Assert.Equal(16,rects.LeftLabelRect.Y);
        Assert.Equal(16,rects.RightLabelRect.Y);
        Assert.Equal(16,rects.LeftLabelRect.Height);
        Assert.Equal(16,rects.RightLabelRect.Height);
    }

    [Fact]
    public void CreateBoundaryLabelRects_KeepsLabelsReadableWhenOneSideIsNarrow(){
        {
            // 99:1 のように右側が狭い場合でも、右ラベルの幅を潰さず安全位置へ逃がす。
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBoundaryLabelRects(
                new BenchmarkChartBarRect(100,496,396),
                new BenchmarkChartBarRect(496,500,4),
                centerY:24,
                labelWidth:42,
                labelHeight:16,
                padding:6);

            Assert.Equal(410,rects.LeftLabelRect.X);
            Assert.Equal(458,rects.RightLabelRect.X);
            Assert.Equal(42,rects.LeftLabelRect.Width);
            Assert.Equal(42,rects.RightLabelRect.Width);
        }
        {
            // 1:99 のように左側が狭い場合でも、左ラベルの幅を潰さず安全位置へ逃がす。
            var rects = global::UI.BenchmarkChartSelectionLayoutLogic.CreateBoundaryLabelRects(
                new BenchmarkChartBarRect(100,104,4),
                new BenchmarkChartBarRect(104,500,396),
                centerY:24,
                labelWidth:42,
                labelHeight:16,
                padding:6);

            Assert.Equal(100,rects.LeftLabelRect.X);
            Assert.Equal(148,rects.RightLabelRect.X);
            Assert.Equal(42,rects.LeftLabelRect.Width);
            Assert.Equal(42,rects.RightLabelRect.Width);
        }
    }
}



