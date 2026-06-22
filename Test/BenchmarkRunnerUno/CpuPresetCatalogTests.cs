using Benchmark.Runner.Uno;

namespace Test.BenchmarkRunnerUno;

public sealed class CpuPresetCatalogTests{
    [Fact]
    public void CpuCoreInfo_DisplayText_UsesLogicalCpuAndGroup(){
        var performancePrimary = new CpuCoreInfo(0,CpuCoreKind.Performance,0,0,1,true);
        var performanceSibling = new CpuCoreInfo(1,CpuCoreKind.Performance,0,0,2,true);
        var efficiency = new CpuCoreInfo(16,CpuCoreKind.Efficiency,8,0,0,true);
        var unknown = new CpuCoreInfo(30,CpuCoreKind.Unknown,15,15,0,false);

        Assert.Equal("0(P0)",performancePrimary.DisplayText);
        Assert.Equal("1(P0)",performanceSibling.DisplayText);
        Assert.Equal("16(E0)",efficiency.DisplayText);
        Assert.Equal("30(Core 15)",unknown.DisplayText);
    }

    [Fact]
    public void Build_CreatesExpectedPresets_ForHybridTopology(){
        var cpuCores = CreateHybridCpuSet();

        var presets = CpuPresetCatalog.Build(cpuCores)
            .ToDictionary(static preset => preset.Key,static preset => preset.LogicalProcessorIndexes.ToArray(),StringComparer.Ordinal);

        Assert.Equal(Enumerable.Range(0,28).ToArray(),presets["ALL"]);
        Assert.Empty(presets["CLEAR"]);
        Assert.Equal([0,2,4,6,8,10,12,14],presets["P1T"]);
        Assert.Equal(Enumerable.Range(0,16).ToArray(),presets["P2T"]);
        Assert.Equal([16,20,24],presets["E1T"]);
        Assert.Equal([16,17,20,21,24,25],presets["E2T"]);
        Assert.Equal([16,17,18,20,21,22,24,25,26],presets["E3T"]);
        Assert.Equal(Enumerable.Range(16,12).ToArray(),presets["E4T"]);
    }

    [Fact]
    public void Build_HidesUnavailableEfficiencyThreadPresets(){
        CpuCoreInfo[] cpuCores = [
            new CpuCoreInfo(0,CpuCoreKind.Efficiency,0,0,0,true),
            new CpuCoreInfo(1,CpuCoreKind.Efficiency,0,0,0,true),
            new CpuCoreInfo(2,CpuCoreKind.Efficiency,1,1,0,true),
            new CpuCoreInfo(3,CpuCoreKind.Efficiency,1,1,0,true),
        ];

        var presetKeys = CpuPresetCatalog.Build(cpuCores)
            .Select(static preset => preset.Key)
            .ToArray();

        Assert.Equal(["ALL","CLEAR","E1T","E2T"],presetKeys);
    }

    [Fact]
    public void Build_OnlyExposesAvailableKinds(){
        CpuCoreInfo[] performanceOnly = [
            new CpuCoreInfo(0,CpuCoreKind.Performance,0,0,0,true),
            new CpuCoreInfo(1,CpuCoreKind.Performance,0,0,0,true),
            new CpuCoreInfo(2,CpuCoreKind.Performance,1,1,0,true),
            new CpuCoreInfo(3,CpuCoreKind.Performance,1,1,0,true),
        ];
        CpuCoreInfo[] unknownOnly = [
            new CpuCoreInfo(0,CpuCoreKind.Unknown,0,0,0,false),
            new CpuCoreInfo(1,CpuCoreKind.Unknown,1,1,0,false),
        ];

        Assert.Equal(["ALL","CLEAR","P1T","P2T"],CpuPresetCatalog.Build(performanceOnly).Select(static preset => preset.Key).ToArray());
        Assert.Equal(["ALL","CLEAR"],CpuPresetCatalog.Build(unknownOnly).Select(static preset => preset.Key).ToArray());
    }

    [Fact]
    public void ReportFormatter_KeepsDetailedCsv_AndBuildsAggregatedHtml(){
        var rows = new[]{
            new ReportRow("System LINQ","Method","Completed","1.0 ns","0.1","0.2","0","P0","0(P0)","artifact",0),
        };

        var csv = BenchmarkReportFormatter.BuildCsv(rows);
        var html = BenchmarkReportFormatter.BuildHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));

        Assert.Contains("LogicalCpu,Core,CpuDisplay,ArtifactFolder,ExitCode",csv,StringComparison.Ordinal);
        Assert.Contains("0(P0)",csv,StringComparison.Ordinal);
        Assert.Contains("<thead><tr><th>Method</th><th>linq</th><th>zlinq</th><th>flinq</th></tr></thead>",html,StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Project</th>",html,StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Status</th>",html,StringComparison.Ordinal);
        Assert.DoesNotContain("CPU Display",html,StringComparison.Ordinal);
    }

    [Fact]
    public void ReportFormatter_AggregatesSystemBenchmarkMeans_ByMethod(){
        var rows = new[]{
            CreateReportRow("System FLinq","Select","Completed","5 ns"),
            CreateReportRow("System LINQ","Select","Completed","2 ns"),
            CreateReportRow("System ZLinq","Select","Completed","3 ns"),
        };

        var html = BenchmarkReportFormatter.BuildHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));

        Assert.Contains("<tr><td>Select</td><td class=\"num\">2 ns</td><td class=\"num\">3 ns</td><td class=\"num\">5 ns</td></tr>",html,StringComparison.Ordinal);
    }

    [Fact]
    public void ReportFormatter_IgnoresNonSystemProjects_InAggregatedReports(){
        var rows = new[]{
            CreateReportRow("System LINQ","Select","Completed","2 ns"),
            CreateReportRow("Tsavorite LINQ","Select","Completed","99 ns"),
            CreateReportRow("Custom Project","CustomOnly","Completed","100 ns"),
        };

        var html = BenchmarkReportFormatter.BuildHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));
        var chart = BenchmarkReportFormatter.BuildChartHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));

        Assert.Contains("<tr><td>Select</td><td class=\"num\">2 ns</td><td class=\"num\"></td><td class=\"num\"></td></tr>",html,StringComparison.Ordinal);
        Assert.DoesNotContain("99 ns",html,StringComparison.Ordinal);
        Assert.DoesNotContain("CustomOnly",html,StringComparison.Ordinal);
        Assert.DoesNotContain("99 ns",chart,StringComparison.Ordinal);
        Assert.DoesNotContain("CustomOnly",chart,StringComparison.Ordinal);
    }

    [Fact]
    public void ReportFormatter_RendersMissingAndInvalidMeans_AsEmptyCellsAndZeroWidthSegments(){
        var rows = new[]{
            CreateReportRow("System LINQ","Where","Completed","2 ns"),
            CreateReportRow("System ZLinq","Where","Failed","3 ns"),
            CreateReportRow("System FLinq","Where","Completed","not measured"),
            CreateReportRow("System LINQ","AllMissing","Failed","1 ns"),
        };

        var html = BenchmarkReportFormatter.BuildHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));
        var chart = BenchmarkReportFormatter.BuildChartHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));

        Assert.Contains("<tr><td>Where</td><td class=\"num\">2 ns</td><td class=\"num\"></td><td class=\"num\"></td></tr>",html,StringComparison.Ordinal);
        Assert.Contains("<tr><td>AllMissing</td><td class=\"num\"></td><td class=\"num\"></td><td class=\"num\"></td></tr>",html,StringComparison.Ordinal);
        Assert.Contains("<div class=\"method\" title=\"AllMissing\">AllMissing</div>",chart,StringComparison.Ordinal);
        Assert.Contains("<div class=\"segment zlinq\" style=\"width:0%\" title=\"zlinq:  (0.0%)\">",chart,StringComparison.Ordinal);
        Assert.Contains("<div class=\"segment flinq\" style=\"width:0%\" title=\"flinq:  (0.0%)\">",chart,StringComparison.Ordinal);
    }

    [Fact]
    public void ReportFormatter_BuildChartHtml_UsesMeanTotalRatios(){
        var rows = new[]{
            CreateReportRow("System LINQ","Select","Completed","2 ns"),
            CreateReportRow("System ZLinq","Select","Completed","3 ns"),
            CreateReportRow("System FLinq","Select","Completed","5 ns"),
        };

        var chart = BenchmarkReportFormatter.BuildChartHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));

        Assert.Contains("<div class=\"segment linq\" style=\"width:20%\" title=\"linq: 2 ns (20.0%)\">",chart,StringComparison.Ordinal);
        Assert.Contains("<div class=\"segment zlinq\" style=\"width:30%\" title=\"zlinq: 3 ns (30.0%)\">",chart,StringComparison.Ordinal);
        Assert.Contains("<div class=\"segment flinq\" style=\"width:50%\" title=\"flinq: 5 ns (50.0%)\">",chart,StringComparison.Ordinal);
        Assert.Contains("<div class=\"axis\"><span>0</span><span>0.25</span><span>0.5</span><span>0.75</span><span>1</span></div>",chart,StringComparison.Ordinal);
    }

    private static IReadOnlyList<CpuCoreInfo> CreateHybridCpuSet(){
        var items = new List<CpuCoreInfo>();
        for(var coreIndex=0;coreIndex<8;coreIndex++){
            var logicalBase = coreIndex*2;
            items.Add(new CpuCoreInfo(logicalBase,CpuCoreKind.Performance,coreIndex,coreIndex,0,true));
            items.Add(new CpuCoreInfo(logicalBase+1,CpuCoreKind.Performance,coreIndex,coreIndex,0,true));
        }

        for(var coreIndex=0;coreIndex<3;coreIndex++){
            var physicalCoreIndex = 8+coreIndex;
            var logicalBase = 16+(coreIndex*4);
            items.Add(new CpuCoreInfo(logicalBase,CpuCoreKind.Efficiency,physicalCoreIndex,coreIndex,0,true));
            items.Add(new CpuCoreInfo(logicalBase+1,CpuCoreKind.Efficiency,physicalCoreIndex,coreIndex,0,true));
            items.Add(new CpuCoreInfo(logicalBase+2,CpuCoreKind.Efficiency,physicalCoreIndex,coreIndex,0,true));
            items.Add(new CpuCoreInfo(logicalBase+3,CpuCoreKind.Efficiency,physicalCoreIndex,coreIndex,0,true));
        }

        return items;
    }

    private static ReportRow CreateReportRow(string project,string method,string status,string mean){
        return new ReportRow(project,method,status,mean,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,null);
    }
}
