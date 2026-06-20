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
    public void ReportFormatter_AddsCpuDisplayColumn_ToCsvAndHtml(){
        var rows = new[]{
            new ReportRow("Proj","Method","Completed","1.0","0.1","0.2","0","P0","0(P0)","artifact",0),
        };

        var csv = BenchmarkReportFormatter.BuildCsv(rows);
        var html = BenchmarkReportFormatter.BuildHtml(rows,new DateTimeOffset(2026,6,20,12,34,56,TimeSpan.Zero));

        Assert.Contains("LogicalCpu,Core,CpuDisplay,ArtifactFolder,ExitCode",csv,StringComparison.Ordinal);
        Assert.Contains("0(P0)",csv,StringComparison.Ordinal);
        Assert.Contains("<th>CPU Display</th>",html,StringComparison.Ordinal);
        Assert.Contains("0(P0)",html,StringComparison.Ordinal);
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
}
