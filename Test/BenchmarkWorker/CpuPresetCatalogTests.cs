using Benchmark.Worker;

namespace Test.BenchmarkWorker;

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
    public void CpuSelectionTable_BuildsCompactSections_ForHybridAndUnknownTopology(){
        var cpuCores = CreateHybridCpuSet()
            .Concat([
                new CpuCoreInfo(28,CpuCoreKind.Unknown,11,0,0,true),
                new CpuCoreInfo(29,CpuCoreKind.Unknown,11,0,0,true),
            ])
            .ToArray();

        var sections = CpuSelectionTableLogic.BuildSections(cpuCores);

        Assert.Equal(["P core","E core","Unknown core"],sections.Select(static section => section.Title).ToArray());
        Assert.Equal(8,sections[0].Rows.Count);
        Assert.Equal(2,sections[0].ColumnCount);
        Assert.Equal("0-1",sections[0].Rows[0].Label);
        Assert.Equal([0,1],sections[0].Rows[0].LogicalProcessorIndexes);
        Assert.Equal(3,sections[1].Rows.Count);
        Assert.Equal(4,sections[1].ColumnCount);
        Assert.Equal("16-19",sections[1].Rows[0].Label);
        Assert.Equal("28-29",sections[2].Rows[0].Label);
    }

    [Fact]
    public void CpuSelectionTable_BulkSelection_KeepsAtLeastOneCpuSelected(){
        var cpuCores = CreateHybridCpuSet().ToArray();
        cpuCores[0].IsSelected=true;
        cpuCores[5].IsSelected=true;

        CpuSelectionTableLogic.ApplyBulkSelection(cpuCores,[0,5],false);

        Assert.Single(cpuCores,static cpu => cpu.IsSelected);
        Assert.True(cpuCores[0].IsSelected);

        CpuSelectionTableLogic.ApplyBulkSelection(cpuCores,[16,17,18,19],true);

        Assert.True(cpuCores[0].IsSelected);
        Assert.True(cpuCores[16].IsSelected);
        Assert.True(cpuCores[17].IsSelected);
        Assert.True(cpuCores[18].IsSelected);
        Assert.True(cpuCores[19].IsSelected);
    }

    [Fact]
    public void UiSortValue_AlwaysPlacesMissingValuesLast(){
        var values = new[]{
            ("missing",UiSortValue.Missing),
            ("two",UiSortValue.FromNumber(2)),
            ("one",UiSortValue.FromNumber(1)),
        };

        var ascending = values
            .OrderBy(static item => item.Item2,Comparer<UiSortValue>.Create((left,right) => UiSortValue.Compare(left,right,false)))
            .Select(static item => item.Item1)
            .ToArray();
        var descending = values
            .OrderBy(static item => item.Item2,Comparer<UiSortValue>.Create((left,right) => UiSortValue.Compare(left,right,true)))
            .Select(static item => item.Item1)
            .ToArray();

        Assert.Equal(["one","two","missing"],ascending);
        Assert.Equal(["two","one","missing"],descending);
    }

    [Fact]
    public void ComparisonFormatter_BuildsFixedPairs_WithLeftDividedByRight(){
        var rows = new[]{
            CreateReportRow("SystemFLinq","Select","Completed",5,10),
            CreateReportRow("SystemZLinq","Select","Completed",4,0),
            CreateReportRow("PersistedFLinq","Select","Completed",6,0),
            CreateReportRow("PersistedLinq","Select","Completed",3,12),
            CreateReportRow("TsavoriteFLinq","Select","Completed",2,16),
            CreateReportRow("TsavoriteLinq","Select","Completed",4,16),
        };

        var comparison = Assert.Single(BenchmarkComparisonFormatter.BuildComparisonRows(rows));

        Assert.Equal("1.25",comparison.SystemRatio.TimeRatio.DisplayText);
        Assert.Equal("2.00",comparison.PersistedRatio.TimeRatio.DisplayText);
        Assert.Equal("0.50",comparison.TsavoriteRatio.TimeRatio.DisplayText);
        Assert.Equal("−",comparison.PersistedRatio.AllocatedRatio.DisplayText);
        Assert.Equal("−",comparison.PersistedFLinqTsavoriteFLinqRatio.AllocatedRatio.DisplayText);
    }

    [Fact]
    public void ComparisonFormatter_WritesAndReadsWideNumericCsv(){
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("SystemFLinq","Select","Completed",5,10),
            CreateReportRow("SystemZLinq","Select","Completed",4,0),
        ]);

        var csv = BenchmarkComparisonFormatter.WriteCsv(rows);
        var read = Assert.Single(BenchmarkComparisonFormatter.ReadCsv(csv));

        Assert.StartsWith("ベンチマーク,SystemFLinq時間ns,SystemFLinq割当bytes,SystemZLinq時間ns,SystemZLinq割当bytes",csv,StringComparison.Ordinal);
        Assert.Contains(",5,10,4,0,",csv,StringComparison.Ordinal);
        Assert.Equal(5,read.SystemFLinq.TimeNanoseconds);
        Assert.Equal(0,read.SystemZLinq.AllocatedBytes);
        Assert.Equal("1.25",read.SystemRatio.TimeRatio.DisplayText);
    }

    [Fact]
    public void ComparisonFormatter_UsesEmptyCsvCells_ForMissingValues(){
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("SystemFLinq","Select","Failed",5,10),
            CreateReportRow("SystemZLinq","Select","Completed",4,0),
        ]);

        var csv = BenchmarkComparisonFormatter.WriteCsv(rows);

        Assert.Contains("Select,,,4,0",csv,StringComparison.Ordinal);
        Assert.Equal("-",Assert.Single(rows).SystemRatio.TimeRatio.DisplayText);
    }

    [Fact]
    public void ComparisonFormatter_RejectsOldNamesAndOldCsv(){
        Assert.Throws<InvalidDataException>(() => BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("System LINQ","Select","Completed",1,0),
        ]));
        Assert.Throws<InvalidDataException>(() => BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("System FLinq","Select","Completed",1,0),
        ]));
        Assert.Throws<InvalidDataException>(() => BenchmarkComparisonFormatter.ReadCsv("Project,Method,Mean\nSystemFLinq,Select,1 ns\n"));
    }

    [Fact]
    public void ComparisonFormatter_Judgement_UsesFivePercentTieWindow(){
        var win = RatioValue.FromNumeric(0.94);
        var tieLow = RatioValue.FromNumeric(0.95);
        var tieHigh = RatioValue.FromNumeric(1.05);
        var lose = RatioValue.FromNumeric(1.06);

        Assert.Equal(RatioJudgement.LeftWin,win.Judgement);
        Assert.Equal(RatioJudgement.Tie,tieLow.Judgement);
        Assert.Equal(RatioJudgement.Tie,tieHigh.Judgement);
        Assert.Equal(RatioJudgement.LeftLose,lose.Judgement);
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

    private static ReportRow CreateReportRow(string project,string method,string status,double meanNanoseconds,long allocatedBytes){
        return new ReportRow(project,method,string.Empty,string.Empty,status,meanNanoseconds+" ns",string.Empty,string.Empty,allocatedBytes+" B",string.Empty,string.Empty,meanNanoseconds,0,0,allocatedBytes,0,0,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,null);
    }
}
