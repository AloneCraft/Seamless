using Benchmark;

namespace Test.BenchmarkWorker;

[global::Seamless.FLinq.LinqIntercept(false)]
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
    public void BenchmarkCsvTimestampParser_FormatsExtractedTimestamp_WithSlashSeparatedDisplay(){
        var timestamp = BenchmarkCsvTimestampParser.TryExtractTimestamp(@"G:\GitHub\Seamless\Work\Benchmark\20260626-032235\BenchmarkComparison-N1.csv");

        Assert.NotNull(timestamp);
        Assert.Equal("2026/06/26 03:22:35",BenchmarkTimestampDisplay.FormatLocal(timestamp.Value));
    }

    [Fact]
    public void BenchmarkProjectLiveStateLogic_UsesLiveStateOnlyForRunningAndCompletedVisuals(){
        Assert.True(BenchmarkProjectLiveStateLogic.IsPending(BenchmarkProjectLiveState.Idle,false));
        Assert.False(BenchmarkProjectLiveStateLogic.IsPending(BenchmarkProjectLiveState.Idle,true));
        Assert.True(BenchmarkProjectLiveStateLogic.IsRunning(BenchmarkProjectLiveState.Running));
        Assert.False(BenchmarkProjectLiveStateLogic.IsRunning(BenchmarkProjectLiveState.Completed));
        Assert.True(BenchmarkProjectLiveStateLogic.IsCompleted(BenchmarkProjectLiveState.Completed));
        Assert.False(BenchmarkProjectLiveStateLogic.IsCompleted(BenchmarkProjectLiveState.Idle));
    }

    [Fact]
    public void BenchmarkMeasurementHighlightLogic_RetainsFreshWhenSameElementIsRunning(){
        var next = BenchmarkMeasurementHighlightLogic.AdvanceForRunningElement(
            BenchmarkMeasurementHighlightState.Fresh,
            4,
            4);

        Assert.Equal(BenchmarkMeasurementHighlightState.Fresh,next);
    }

    [Fact]
    public void BenchmarkMeasurementHighlightLogic_DemotesFreshWhenNextElementStarts(){
        var next = BenchmarkMeasurementHighlightLogic.AdvanceForRunningElement(
            BenchmarkMeasurementHighlightState.Fresh,
            4,
            8);

        Assert.Equal(BenchmarkMeasurementHighlightState.RetainedDuringRun,next);
    }

    [Fact]
    public void BenchmarkMeasurementHighlightLogic_ClearsHighlightAfterRun(){
        var next = BenchmarkMeasurementHighlightLogic.ClearAfterRun(BenchmarkMeasurementHighlightState.RetainedDuringRun);

        Assert.Equal(BenchmarkMeasurementHighlightState.None,next);
    }

    [Fact]
    public void ComparisonFormatter_BuildsActiveSourcePair_WithLeftDividedByRight(){
        var rows = new[]{
            CreateReportRow("System.Array.FLinq","Select","Completed",5,10),
            CreateReportRow("System.Array.ZLinq","Select","Completed",4,0),
        };

        var comparison = Assert.Single(BenchmarkComparisonFormatter.BuildComparisonRows(rows));

        Assert.Equal("1.25",comparison.PairRatio.TimeRatio.DisplayText);
        Assert.Equal("+",comparison.PairRatio.AllocatedRatio.DisplayText);
        Assert.Equal(BenchmarkSourceTab.SystemArray,comparison.SourceTab.Tab);
    }

    [Fact]
    public void ComparisonFormatter_WritesAndReadsWideNumericCsv(){
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("System.Array.FLinq","Select","Completed",5,10),
            CreateReportRow("System.Array.ZLinq","Select","Completed",4,0),
        ]);

        var csv = BenchmarkComparisonFormatter.WriteCsv(rows);
        var read = Assert.Single(BenchmarkComparisonFormatter.ReadCsv(csv));

        Assert.Contains("System.Array.FLinq",csv,StringComparison.Ordinal);
        Assert.Contains("System.Array.ZLinq",csv,StringComparison.Ordinal);
        Assert.Contains("Select,5,10,4,0",csv,StringComparison.Ordinal);
        Assert.Equal(5,read.Left.TimeNanoseconds);
        Assert.Equal(0,read.Right.AllocatedBytes);
        Assert.Equal("1.25",read.PairRatio.TimeRatio.DisplayText);
    }

    [Fact]
    public void ComparisonFormatter_UsesEmptyCsvCells_ForMissingValues(){
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("System.Array.FLinq","Select","Failed",5,10),
            CreateReportRow("System.Array.ZLinq","Select","Completed",4,0),
        ]);

        var csv = BenchmarkComparisonFormatter.WriteCsv(rows);

        Assert.Contains("Select,,,4,0",csv,StringComparison.Ordinal);
        Assert.Equal("-",Assert.Single(rows).PairRatio.TimeRatio.DisplayText);
    }

    [Fact]
    public void ComparisonFormatter_TreatsCompletedRowsWithoutTimeAsMissing(){
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow("System.Array.FLinq","Select","Completed",0,10),
            CreateReportRow("System.Array.ZLinq","Select","Completed",4,0),
        ]);

        var comparison = Assert.Single(rows);
        var csv = BenchmarkComparisonFormatter.WriteCsv(rows);

        Assert.False(comparison.Left.HasMeasuredValue);
        Assert.True(comparison.Right.HasMeasuredValue);
        Assert.Contains("Select,,,4,0",csv,StringComparison.Ordinal);
        Assert.Equal("-",comparison.PairRatio.TimeRatio.DisplayText);
    }

    [Fact]
    public void ComparisonFormatter_ReadCsv_IgnoresAllocatedWithoutTime(){
        var csv = BenchmarkComparisonFormatter.WriteCsv([
            new BenchmarkComparisonRow(
                "Select",
                BenchmarkSourceTabs.Default,
                ProjectMetrics.FromMeasurement(5,10),
                ProjectMetrics.FromMeasurement(4,0)),
        ]).Replace("Select,5,10,4,0","Select,,10,4,0",StringComparison.Ordinal);

        var read = Assert.Single(BenchmarkComparisonFormatter.ReadCsv(csv));

        Assert.False(read.Left.HasMeasuredValue);
        Assert.True(read.Right.HasMeasuredValue);
        Assert.Equal(0,read.Right.AllocatedBytes);
    }

    [Fact]
    public void ComparisonFormatter_WritesZeroAllocationOnlyWhenTimeExists(){
        var csv = BenchmarkComparisonFormatter.WriteCsv([
            new BenchmarkComparisonRow(
                "Select",
                BenchmarkSourceTabs.Default,
                ProjectMetrics.FromMeasurement(4400,0),
                ProjectMetrics.FromMeasurement(6800,null)),
        ]);
        var read = Assert.Single(BenchmarkComparisonFormatter.ReadCsv(csv));

        Assert.Contains("Select,4400,0,6800,",csv,StringComparison.Ordinal);
        Assert.Equal(0,read.Left.AllocatedBytes);
        Assert.Null(read.Right.AllocatedBytes);
    }

    [Fact]
    public void BenchmarkWorkerRuntime_CreateConfig_AllowsDebugBenchmarking(){
        var config = BenchmarkWorkerRuntime.CreateConfig("artifacts");

        Assert.True(BenchmarkWorkerRuntime.AllowsDebugBenchmarking(config));
        Assert.False(BenchmarkWorkerRuntime.HasUsableStatistics(null));
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
    public void ComparisonFormatter_RejectsProjectsOutsideSelectedSourceTab(){
        var systemTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
        var tsavoriteTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessTsavorite);

        Assert.Throws<InvalidDataException>(() => BenchmarkComparisonFormatter.BuildComparisonRows([
            CreateReportRow(systemTab.LeftProject,"Select","Completed",1,0),
            CreateReportRow(tsavoriteTab.LeftProject,"Select","Completed",1,0),
        ],systemTab));
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
