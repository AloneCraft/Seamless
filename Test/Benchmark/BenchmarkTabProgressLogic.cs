using UI;

namespace Test.Benchmark;

public sealed class BenchmarkTabProgressLogic{
    [Fact]
    public void CreateCellsFromSelectedCases_UsesRunSelectionSnapshotForSourceTab(){
        var sourceTab = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);

        var cells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases(
            [
                Case("System.Array.FLinq","System.Array","Where","Count=4096"),
                Case("System.Array.ZLinq","System.Array","Where","Count=4096"),
                Case("System.List.FLinq","System.Collections.Generic.List","Where","Count=4096"),
                Case("System.Array.FLinq","System.Array","Where","Count=4096"),
            ],
            sourceTab);

        Assert.Equal(2,cells.Count);
        Assert.All(cells,static cell => Assert.True(cell.IsSelected));
        Assert.Contains(cells,static cell => cell.Project=="System.Array.FLinq");
        Assert.Contains(cells,static cell => cell.Project=="System.Array.ZLinq");
        Assert.Single(cells.Select(static cell => cell.PairKey).Distinct(StringComparer.OrdinalIgnoreCase));
        Assert.All(cells,static cell => Assert.False(string.IsNullOrWhiteSpace(cell.PairKey)));
    }

    [Fact]
    public void CreateCellsFromSelectedCases_SeparatesSharedSeamlessLinqProjectByAssembly(){
        var persisted = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessPersisted);
        var tsavorite = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SeamlessTsavorite);
        var persistedCase = Case(
            persisted.RightProject,
            persisted.RightAssemblyName,
            "Persisted.Linq.Enumerable.LinqInt32",
            "Aggregate_func",
            "");
        var tsavoriteCase = Case(
            tsavorite.RightProject,
            tsavorite.RightAssemblyName,
            "Tsavorite.Linq.Enumerable.LinqInt32",
            "Aggregate_func",
            "");

        {
            var persistedCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases([persistedCase],persisted);
            var tsavoriteCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases([persistedCase],tsavorite);

            Assert.Single(persistedCells);
            Assert.Empty(tsavoriteCells);
            var persistedProgress = global::UI.BenchmarkTabProgressLogic.Calculate(
                persistedCells,
                [
                    new BenchmarkElementMeasurement(
                        persisted.RightProject,
                        "Aggregate_func",
                        "Persisted.Linq.Enumerable.LinqInt32",
                        "",
                        4096,
                        "Completed",
                        2_000_000),
                ],
                persisted.LeftProject,
                persisted.RightProject,
                4096,
                1_000_000);
            var tsavoriteProgress = global::UI.BenchmarkTabProgressLogic.Calculate(
                tsavoriteCells,
                [
                    new BenchmarkElementMeasurement(
                        persisted.RightProject,
                        "Aggregate_func",
                        "Persisted.Linq.Enumerable.LinqInt32",
                        "",
                        4096,
                        "Completed",
                        2_000_000),
                ],
                tsavorite.LeftProject,
                tsavorite.RightProject,
                4096,
                1_000_000);

            Assert.Equal(1,persistedProgress.Completed);
            Assert.Equal(1,persistedProgress.Total);
            Assert.Equal(0,tsavoriteProgress.Completed);
            Assert.Equal(0,tsavoriteProgress.Total);
        }
        {
            var persistedCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases([tsavoriteCase],persisted);
            var tsavoriteCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases([tsavoriteCase],tsavorite);

            Assert.Empty(persistedCells);
            Assert.Single(tsavoriteCells);
        }
        {
            var persistedCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases([persistedCase,tsavoriteCase],persisted);
            var tsavoriteCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases([persistedCase,tsavoriteCase],tsavorite);

            Assert.Single(persistedCells);
            Assert.Single(tsavoriteCells);
            Assert.Contains(persistedCells,cell => cell.TypeFullName=="Persisted.Linq.Enumerable.LinqInt32");
            Assert.Contains(tsavoriteCells,cell => cell.TypeFullName=="Tsavorite.Linq.Enumerable.LinqInt32");
        }
    }

    [Fact]
    public void Calculate_UsesSelectedCellsAsDenominator(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",false),
                new BenchmarkTabProgressCell("Select","Left",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",1_000_000),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(2,progress.Total);
        Assert.Equal("1/2",progress.FractionText);
        Assert.Equal(0.5,progress.Fraction);
    }

    [Fact]
    public void Calculate_DoesNotCompleteShortCompletedCellsWithoutPairThreshold(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",200),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(0,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotTreatFiveOfSixCellsAsComplete(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("A","Left","Type.A","",true,"row-a"),
                new BenchmarkTabProgressCell("A","Right","Type.A","",true,"row-a"),
                new BenchmarkTabProgressCell("B","Left","Type.B","",true,"row-b"),
                new BenchmarkTabProgressCell("B","Right","Type.B","",true,"row-b"),
                new BenchmarkTabProgressCell("C","Left","Type.C","",true,"row-c"),
                new BenchmarkTabProgressCell("C","Right","Type.C","",true,"row-c"),
            ],
            [
                new BenchmarkElementMeasurement("Left","A","Type.A","",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Right","A","Type.A","",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Left","B","Type.B","",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Right","B","Type.B","",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Left","C","Type.C","",4096,"Failed",null),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(5,progress.Completed);
        Assert.Equal(6,progress.Total);
        Assert.Equal("5/6",progress.FractionText);
        Assert.NotEqual(1,progress.Fraction);
    }

    [Fact]
    public void Calculate_KeepsMultiTabPartialRunIncompleteAcrossTabSwitch(){
        var array = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
        var range = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemRange);
        var list = BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemList);
        var selectedCases = new[]{
            Case(array.LeftProject,"System.Array","Aggregate_func",""),
            Case(array.RightProject,"System.Array","Aggregate_func",""),
            Case(array.LeftProject,"System.Array","AggregateBy_keySelector",""),
            Case(array.RightProject,"System.Array","AggregateBy_keySelector",""),
            Case(range.LeftProject,"System.Collections.Generic.IEnumerable","Aggregate_seed_func",""),
            Case(range.RightProject,"System.Collections.Generic.IEnumerable","Aggregate_seed_func",""),
            Case(range.LeftProject,"System.Collections.Generic.IEnumerable","Aggregate_seed_func_resultSelector",""),
            Case(range.RightProject,"System.Collections.Generic.IEnumerable","Aggregate_seed_func_resultSelector",""),
            Case(list.LeftProject,"System.Collections.Generic.List","All_predicate",""),
            Case(list.RightProject,"System.Collections.Generic.List","All_predicate",""),
            Case(list.LeftProject,"System.Collections.Generic.List","Any",""),
            Case(list.RightProject,"System.Collections.Generic.List","Any",""),
        };

        var listCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases(selectedCases,list);
        var listMeasurements = new[]{
            new BenchmarkElementMeasurement(list.LeftProject,"All_predicate","System.Collections.Generic.List","",4096,"Completed",2_000_000),
            new BenchmarkElementMeasurement(list.RightProject,"All_predicate","System.Collections.Generic.List","",4096,"Completed",2_000_000),
        };
        var listProgress = global::UI.BenchmarkTabProgressLogic.Calculate(
            listCells,
            listMeasurements,
            list.LeftProject,
            list.RightProject,
            4096,
            1_000_000);

        Assert.Equal(2,listProgress.Completed);
        Assert.Equal(4,listProgress.Total);
        Assert.NotEqual(1,listProgress.Fraction);

        var arrayCells = global::UI.BenchmarkTabProgressSourceLogic.CreateCellsFromSelectedCases(selectedCases,array);
        var arrayProgress = global::UI.BenchmarkTabProgressLogic.Calculate(
            arrayCells,
            [
                new BenchmarkElementMeasurement(array.LeftProject,"Aggregate_func","System.Array","",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement(array.RightProject,"Aggregate_func","System.Array","",4096,"Completed",2_000_000),
            ],
            array.LeftProject,
            array.RightProject,
            4096,
            1_000_000);

        Assert.Equal(2,arrayProgress.Completed);
        Assert.Equal(4,arrayProgress.Total);

        var restoredListProgress = global::UI.BenchmarkTabProgressLogic.Calculate(
            listCells,
            listMeasurements,
            list.LeftProject,
            list.RightProject,
            4096,
            1_000_000);

        Assert.Equal(listProgress.Completed,restoredListProgress.Completed);
        Assert.Equal(listProgress.Total,restoredListProgress.Total);
        Assert.NotEqual(1,restoredListProgress.Fraction);
    }

    [Fact]
    public void Calculate_TreatsFailedCellsAsTerminal(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Failed",null),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(1,progress.Total);
    }

    [Fact]
    public void Calculate_TreatsAdaptiveThresholdStopAsTerminalForSelectedPairCells(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Where","Left",true),
                new BenchmarkTabProgressCell("Where","Right",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Where",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Right","Where",4096,"Completed",100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(2,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_TreatsRightThresholdAsPairCompletionWhenPairKeyMatches(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
                new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-1"),
            ],
            [
                new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(2,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotCompleteOtherRowWithDifferentPairKey(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Any","Left","System.Array.FLinq","",true,"row-1"),
                new BenchmarkTabProgressCell("Any","Right","System.Array.ZLinq","",true,"row-2"),
            ],
            [
                new BenchmarkElementMeasurement("Left","Any","System.Array.FLinq","",4096,"Completed",100),
                new BenchmarkElementMeasurement("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(1,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotCompleteDifferentScalableTypeWithSameMethod(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Add","Scalable.Persisted.List","Scalable.Persisted.Collections.List.ListString",true),
            ],
            [
                new BenchmarkElementMeasurement("Scalable.Persisted.List","Add","Scalable.Persisted.Collections.List.ListInt32",4096,"Completed",100),
            ],
            "Scalable.Persisted.List",
            "Scalable.Persisted.List.Previous",
            8192,
            1_000_000);

        Assert.Equal(0,progress.Completed);
        Assert.Equal(1,progress.Total);
    }

    [Fact]
    public void Calculate_DoesNotCompleteDifferentArgumentKeyWithSameMethodAndType(){
        var progress = global::UI.BenchmarkTabProgressLogic.Calculate(
            [
                new BenchmarkTabProgressCell("Add","Left","Type","Count=2",true),
                new BenchmarkTabProgressCell("Add","Right","Type","Count=2",true),
            ],
            [
                new BenchmarkElementMeasurement("Left","Add","Type","Count=1",4096,"Completed",2_000_000),
                new BenchmarkElementMeasurement("Right","Add","Type","Count=1",4096,"Completed",100),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(0,progress.Completed);
        Assert.Equal(2,progress.Total);
    }

    private static BenchmarkCase Case(string project,string type,string method,string argumentKey) =>
        new(project,ResolveAssemblyName(project),type,method,argumentKey,[]);

    private static BenchmarkCase Case(string project,string assemblyName,string type,string method,string argumentKey) =>
        new(project,assemblyName,type,method,argumentKey,[]);

    private static string ResolveAssemblyName(string project) =>
        BenchmarkSourceTabs.TryGetByProject(project)?.GetAssemblyName(project)??"Test.Assembly";
}



