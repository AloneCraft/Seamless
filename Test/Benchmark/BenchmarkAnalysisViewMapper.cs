using UI;

namespace Test.Benchmark;

public sealed class BenchmarkAnalysisViewMapper{
    [Fact]
    public void MapImprovementCandidates_OrdersByImprovementScore(){
        var points = global::UI.BenchmarkAnalysisViewMapper.MapImprovementCandidates([
            Row(("Method","Where"),("ImprovementScore",1.2),("LeftProject","Left"),("RightProject","Right"),("ElementCount",128)),
            Row(("Method","Select"),("ImprovementScore",2.4),("LeftProject","Left"),("RightProject","Right"),("ElementCount",256)),
        ]);

        Assert.Equal(["Select","Where"],points.Select(static point => point.Label).ToArray());
        Assert.Equal(2.4,points[0].Value);
        Assert.Contains("N=256",points[0].DetailText);
    }

    [Fact]
    public void MapCategorySummary_UsesTimeAndAllocationRatios(){
        var points = global::UI.BenchmarkAnalysisViewMapper.MapCategorySummary([
            Row(("Category","Projection"),("AvgMeanRatio",1.5),("AvgAllocationRatio",0.8),("SampleCount",3)),
            Row(("Category","Filter"),("AvgMeanRatio",2.0),("AvgAllocationRatio",1.1),("SampleCount",5)),
        ]);

        Assert.Equal(["Filter","Projection"],points.Select(static point => point.Category).ToArray());
        Assert.Equal(2.0,points[0].AvgMeanRatio);
        Assert.Equal(1.1,points[0].AvgAllocationRatio);
        Assert.Equal(5,points[0].SampleCount);
    }

    [Fact]
    public void MapScaleTrend_GroupsByMethodAndOrdersByElementCount(){
        var series = global::UI.BenchmarkAnalysisViewMapper.MapScaleTrend([
            Row(("Method","Where"),("ElementCount",1024),("AvgMeanRatio",1.8)),
            Row(("Method","Where"),("ElementCount",256),("AvgMeanRatio",1.2)),
            Row(("Method","Select"),("ElementCount",128),("AvgMeanRatio",2.2)),
        ]);

        var where = series.First(item => item.SeriesLabel=="Where");
        Assert.Equal([256,1024],where.Points.Select(static point => (int)point.X).ToArray());
        Assert.Equal([1.2,1.8],where.Points.Select(static point => point.Y).ToArray());
    }

    [Fact]
    public void MapMissingFailureMatrix_MapsSeverity(){
        var badges = global::UI.BenchmarkAnalysisViewMapper.MapMissingFailureMatrix([
            Row(("Method","Where"),("Project","Left"),("Side","L"),("ElementCount",128),("JobStatus","Failed"),("FailureKind","Exit")),
            Row(("Method","Select"),("Project","Right"),("Side","R"),("ElementCount",256),("JobStatus","Completed"),("OppositeMissing",1)),
            Row(("Method","Sum"),("Project","Left"),("Side","L"),("ElementCount",512),("JobStatus","Completed"),("OppositeMissing",0)),
        ]);

        Assert.Equal(BenchmarkBadgeSeverity.Critical,badges[0].Severity);
        Assert.Equal(BenchmarkBadgeSeverity.Warning,badges[1].Severity);
        Assert.Equal(BenchmarkBadgeSeverity.Info,badges[2].Severity);
    }

    [Fact]
    public void MapMeasurementQuality_ClampsGaugeRatios(){
        var gauges = global::UI.BenchmarkAnalysisViewMapper.MapMeasurementQuality([
            Row(("Method","Where"),("Project","Left"),("StdDevRatio",1.8),("ErrorRatio",-0.5),("ElementCount",128),("AssignedLogicalCpu",2)),
        ]);

        var gauge = Assert.Single(gauges);
        Assert.Equal(1,gauge.StdDevRatio);
        Assert.Equal(0,gauge.ErrorRatio);
        Assert.Contains("CPU 2",gauge.DetailText);
    }

    private static IReadOnlyDictionary<string,object?> Row(params (string Key,object? Value)[] values) =>
        values.ToDictionary(static item => item.Key,static item => item.Value,StringComparer.OrdinalIgnoreCase);
}
